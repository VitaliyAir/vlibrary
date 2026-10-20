import { useEffect, useMemo, useRef, useState, type ChangeEvent } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../api/client';
import { importApi, useCancelImport, useCategories, useImport, useImportOptions, useImports, useImportsChanged } from '../api/hooks';
import type { ImportBatch, ImportOptions, ImportOutcome } from '../api/types';
import { Empty, ErrorMessage, Loading, Pagination } from '../components/common';
import { categoryOptions, formatDate, formatSize, outcomeLabels, warningOutcomes } from '../format';

const outcomes = Object.keys(outcomeLabels) as ImportOutcome[];
const PrecheckChunk = 1000;

interface PickedFile {
  file: File;
  /** Folders from the picked folder down, e.g. ["Templ", "Library", "Math"]. */
  folders: string[];
  name: string;
}

interface Selection {
  files: PickedFile[];
  unsupported: number;
  excluded: number;
  tooLarge: number;
  /** Folders shared by all files: the chips where the category path can start. */
  commonFolders: string[];
}

interface QueueItem {
  relativePath: string;
  file: File;
}

type Phase = 'idle' | 'running' | 'paused' | 'finished';

/** Splits the picked files into importable ones and the skipped ones (type, excluded folder, size). */
function analyse(list: FileList, options: ImportOptions): Selection {
  const extensions = new Set(options.supportedExtensions.map((e) => e.toLowerCase()));
  const excludedFolders = new Set(options.excludedFolders.map((f) => f.toLowerCase()));
  const selection: Selection = { files: [], unsupported: 0, excluded: 0, tooLarge: 0, commonFolders: [] };

  for (const file of Array.from(list)) {
    const parts = (file.webkitRelativePath || file.name).split('/').filter(Boolean);
    const name = parts.pop() ?? file.name;
    if (parts.some((folder) => excludedFolders.has(folder.toLowerCase()))) {
      selection.excluded++;
      continue;
    }
    const dot = name.lastIndexOf('.');
    if (dot < 0 || !extensions.has(name.slice(dot).toLowerCase())) {
      selection.unsupported++;
      continue;
    }
    if (file.size > options.maxFileBytes) {
      selection.tooLarge++;
      continue;
    }
    selection.files.push({ file, folders: parts, name });
  }

  selection.files.sort((a, b) => [...a.folders, a.name].join('/').localeCompare([...b.folders, b.name].join('/')));
  const first = selection.files[0]?.folders ?? [];
  let common = first.length;
  for (const picked of selection.files) {
    let i = 0;
    while (i < common && i < picked.folders.length && picked.folders[i] === first[i]) i++;
    common = i;
  }
  selection.commonFolders = first.slice(0, common);
  return selection;
}

/**
 * UC-1: pick a folder (or files) on this computer, choose where the category path starts and an optional parent
 * category, then the files are uploaded one by one: VLibrary -> VStorage, the library keeps the meta info.
 */
export function ImportPage() {
  const options = useImportOptions();
  const categories = useCategories();
  const imports = useImports();
  const cancelOther = useCancelImport();
  const changed = useImportsChanged();

  const [selection, setSelection] = useState<Selection | null>(null);
  const [start, setStart] = useState(0);
  const [parentId, setParentId] = useState('');
  const [phase, setPhase] = useState<Phase>('idle');
  const [progress, setProgress] = useState<ImportBatch | null>(null);
  const [current, setCurrent] = useState('');
  const [pauseMessage, setPauseMessage] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [stopped, setStopped] = useState(false);

  const batchId = useRef<number | null>(null);
  const queue = useRef<QueueItem[]>([]);
  const next = useRef(0);
  const cancelRequested = useRef(false);

  const busy = phase === 'running' || phase === 'paused';
  const parent = categories.data?.find((c) => String(c.id) === parentId) ?? null;
  const targetFolders = selection?.commonFolders.slice(start) ?? [];
  const needsParent = !parent && (selection?.files.some((f) => f.folders.length <= start) ?? false);

  // Leaving the page while uploading would interrupt the import.
  useEffect(() => {
    if (phase !== 'running') return;
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [phase]);

  const pick = (event: ChangeEvent<HTMLInputElement>) => {
    const list = event.target.files;
    if (list && list.length > 0 && options.data) {
      setSelection(analyse(list, options.data));
      setStart(0);
      setPhase('idle');
      setProgress(null);
      setError(null);
    }
    event.target.value = '';
  };

  /** Ends the upload; the picked files were handled, so the next import starts with a new pick. */
  const finish = async (batch: ImportBatch | null, wasStopped = false) => {
    if (batch) setProgress(batch);
    setStopped(wasStopped);
    setPhase('finished');
    setCurrent('');
    setSelection(null);
    await changed();
  };

  const runQueue = async () => {
    const id = batchId.current!;
    setPhase('running');
    setPauseMessage(null);
    while (next.current < queue.current.length) {
      if (cancelRequested.current) {
        await importApi.cancel(id).catch(() => undefined);
        await finish(null, true);
        return;
      }
      const item = queue.current[next.current];
      setCurrent(item.relativePath);
      try {
        const result = await importApi.uploadFile(id, item.relativePath, item.file);
        setProgress(result.batch);
        next.current++;
      } catch (e) {
        if (e instanceof ApiError && e.status === 409) {
          // The import was finished elsewhere (cancelled in another tab).
          setError(e);
          await finish(null, true);
          return;
        }
        // VStorage unavailable (503) or a network error: nothing was recorded, the same file can be retried.
        setPauseMessage(e instanceof Error ? e.message : String(e));
        setPhase('paused');
        return;
      }
    }
    await finish(await importApi.complete(id));
  };

  const startImport = async () => {
    if (!selection) return;
    setError(null);
    cancelRequested.current = false;
    const items = selection.files.map((p) => ({ relativePath: [...p.folders.slice(start), p.name].join('/'), file: p.file }));
    const sourceName = targetFolders.join('/') || (selection.files.length === 1 ? selection.files[0].name : 'Selected files');
    try {
      setPhase('running');
      const created = await importApi.create({ sourceName, totalFiles: items.length, parentCategoryId: parent?.id ?? null });
      batchId.current = created.id;
      setSelectedId(created.id);
      await changed();

      // Unchanged files (same category, name, size and time) are not uploaded again.
      const skip = new Set<string>();
      for (let i = 0; i < items.length; i += PrecheckChunk) {
        const chunk = items.slice(i, i + PrecheckChunk);
        const result = await importApi.precheck(
          created.id,
          chunk.map((item) => ({ relativePath: item.relativePath, size: item.file.size, lastModified: item.file.lastModified })),
        );
        result.skip.forEach((path) => skip.add(path));
        setProgress(result.batch);
      }

      queue.current = items.filter((item) => !skip.has(item.relativePath));
      next.current = 0;
      await runQueue();
    } catch (e) {
      setError(e);
      setPhase(batchId.current == null ? 'idle' : 'finished');
    }
  };

  const stop = async () => {
    cancelRequested.current = true;
    if (phase === 'paused' && batchId.current != null) {
      await importApi.cancel(batchId.current).catch(() => undefined);
      await finish(null, true);
    }
  };

  return (
    <section>
      <h1>Import</h1>
      <div className="panel">
        <p className="muted">
          Pick a folder (or single files) on this computer. The files are uploaded to VStorage; the library keeps their meta
          info. Folders become categories.
        </p>
        <div className="row">
          <label className={`button ${busy || !options.data ? 'disabled' : ''}`}>
            Choose folder…
            <input
              type="file"
              multiple
              hidden
              disabled={busy || !options.data}
              ref={(input) => input?.setAttribute('webkitdirectory', '')}
              onChange={pick}
            />
          </label>
          <label className={`button ${busy || !options.data ? 'disabled' : ''}`}>
            Choose files…
            <input
              type="file"
              multiple
              hidden
              disabled={busy || !options.data}
              accept={options.data?.supportedExtensions.join(',')}
              onChange={pick}
            />
          </label>
        </div>
        <ErrorMessage error={options.error ?? categories.error} />

        {selection && (
          <SelectionPreview
            selection={selection}
            start={start}
            onStart={setStart}
            parentId={parentId}
            onParent={setParentId}
            parentOptions={categoryOptions(categories.data ?? [])}
            target={[...(parent ? [parent.path] : []), ...targetFolders].join(' / ')}
            needsParent={needsParent}
            disabled={busy}
          />
        )}

        {selection && phase === 'idle' && (
          <button
            className="primary"
            disabled={selection.files.length === 0 || needsParent}
            onClick={() => void startImport()}
          >
            Import {selection.files.length} file(s)
          </button>
        )}

        {phase !== 'idle' && (
          <div className="upload-status">
            {progress && <Progress batch={progress} />}
            {phase === 'running' && current && <div className="path muted">Uploading {current}</div>}
            {phase === 'running' && (
              <button onClick={() => void stop()} disabled={cancelRequested.current}>
                Cancel
              </button>
            )}
            {phase === 'paused' && (
              <>
                <div className="error">Paused: {pauseMessage}</div>
                <div className="row">
                  <button className="primary" onClick={() => void runQueue()}>
                    Retry
                  </button>
                  <button onClick={() => void stop()}>Stop import</button>
                </div>
              </>
            )}
            {phase === 'finished' && (
              <div>
                <strong>
                  Import{progress ? ` #${progress.id}` : ''} {stopped ? 'stopped' : 'finished'}
                </strong>
                {progress && !stopped && (
                  <>
                    : {progress.importedCount} imported, {progress.unchangedCount} unchanged,{' '}
                    {progress.alreadyInLibraryCount} already in library
                    {progress.conflictCount + progress.deletedDuplicateCount + progress.failedCount > 0 &&
                      `, ${progress.conflictCount + progress.deletedDuplicateCount + progress.failedCount} need attention`}
                  </>
                )}
                {' '}— details in the report below.
                <div className="muted">To import more, choose a folder or files again.</div>
              </div>
            )}
          </div>
        )}
        <ErrorMessage error={error} />
      </div>

      <h2>Recent imports</h2>
      {imports.isLoading && <Loading />}
      {imports.data?.length === 0 && <Empty>No imports yet.</Empty>}
      <table className="table">
        <tbody>
          {imports.data?.map((batch) => (
            <tr key={batch.id} className={batch.id === selectedId ? 'selected' : 'clickable'} onClick={() => setSelectedId(batch.id)}>
              <td>{formatDate(batch.createdAt)}</td>
              <td className="path">
                {batch.parentCategoryPath ? `${batch.parentCategoryPath} / ` : ''}
                {batch.sourceName}
              </td>
              <td>
                <span className={`badge import-${batch.status}`}>{batch.status}</span>
              </td>
              <td>
                <Progress batch={batch} />
              </td>
              <td>
                {batch.status === 'Running' && batch.id !== batchId.current && (
                  <button
                    className="small"
                    title="Stop an upload that was left unfinished (e.g. the tab was closed)"
                    onClick={(e) => {
                      e.stopPropagation();
                      cancelOther.mutate(batch.id);
                    }}
                  >
                    Cancel
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <ErrorMessage error={cancelOther.error} />

      {selectedId != null && <ImportDetails id={selectedId} />}
    </section>
  );
}

function SelectionPreview(props: {
  selection: Selection;
  start: number;
  onStart: (index: number) => void;
  parentId: string;
  onParent: (id: string) => void;
  parentOptions: { id: number; label: string }[];
  target: string;
  needsParent: boolean;
  disabled: boolean;
}) {
  const { selection, start } = props;
  const size = useMemo(() => selection.files.reduce((sum, f) => sum + f.file.size, 0), [selection]);
  const skipped = [
    selection.unsupported > 0 && `${selection.unsupported} unsupported type`,
    selection.excluded > 0 && `${selection.excluded} in excluded folders`,
    selection.tooLarge > 0 && `${selection.tooLarge} too large`,
  ].filter(Boolean);

  return (
    <div className="import-preview">
      <p>
        <strong>{selection.files.length}</strong> book file(s), {formatSize(size)}
        {skipped.length > 0 && <span className="muted"> · skipped: {skipped.join(', ')}</span>}
      </p>

      {selection.commonFolders.length > 0 && (
        <div className="row">
          <span>Categories start at:</span>
          <div className="chips">
            {selection.commonFolders.map((folder, index) => (
              <button
                key={index}
                type="button"
                disabled={props.disabled}
                className={`chip ${index === start ? 'active' : index < start ? 'skipped' : ''}`}
                onClick={() => props.onStart(index)}
              >
                {folder}
              </button>
            ))}
            <button
              type="button"
              disabled={props.disabled}
              className={`chip ${start === selection.commonFolders.length ? 'active' : ''}`}
              title="Do not create categories from these folders"
              onClick={() => props.onStart(selection.commonFolders.length)}
            >
              (none)
            </button>
          </div>
        </div>
      )}

      <label className="row">
        Parent category
        <select value={props.parentId} disabled={props.disabled} onChange={(e) => props.onParent(e.target.value)}>
          <option value="">— top level —</option>
          {props.parentOptions.map((c) => (
            <option key={c.id} value={c.id}>
              {c.label}
            </option>
          ))}
        </select>
      </label>

      <p>
        Files go to: <span className="path">{props.target || '—'}</span>
        {selection.files.some((f) => f.folders.length > selection.commonFolders.length) && (
          <span className="muted"> (subfolders become subcategories)</span>
        )}
      </p>
      {props.needsParent && (
        <p className="warning-text">Some files are not inside a category folder: choose a parent category or an earlier start.</p>
      )}
    </div>
  );
}

function Progress({ batch }: { batch: ImportBatch }) {
  const percent = batch.totalFiles ? Math.round((batch.processedFiles / batch.totalFiles) * 100) : 0;
  return (
    <div className="progress" title={`${batch.processedFiles} of ${batch.totalFiles} files`}>
      <div className="bar" style={{ width: `${percent}%` }} />
      <span>
        {batch.processedFiles}/{batch.totalFiles}
      </span>
    </div>
  );
}

function ImportDetails({ id }: { id: number }) {
  const [outcome, setOutcome] = useState<ImportOutcome | ''>('');
  const [page, setPage] = useState(1);
  const details = useImport(id, outcome, page);

  if (details.isLoading) return <Loading />;
  if (!details.data) return <ErrorMessage error={details.error} />;

  const { batch, items } = details.data;
  const counters: [string, number, boolean][] = [
    ['Imported', batch.importedCount, false],
    ['Waiting for review', batch.pendingReviewCount, false],
    ['Already in library', batch.alreadyInLibraryCount, false],
    ['Unchanged', batch.unchangedCount, false],
    ['Duplicates of deleted books', batch.deletedDuplicateCount, true],
    ['Deleted books, already reported', batch.knownDeletedCount, false],
    ['Conflicts', batch.conflictCount, true],
    ['Failed', batch.failedCount, true],
  ];
  const warnings = batch.conflictCount + batch.deletedDuplicateCount + batch.failedCount;

  return (
    <div className="panel">
      <h3>
        Import #{batch.id}: {batch.parentCategoryPath ? `${batch.parentCategoryPath} / ` : ''}
        {batch.sourceName}
      </h3>
      {batch.error && <div className="error">{batch.error}</div>}
      <div className="counters">
        {counters.map(([label, value, isWarning]) => (
          <div key={label} className={isWarning && value > 0 ? 'warning' : ''}>
            <strong>{value}</strong>
            <span className="muted">{label}</span>
          </div>
        ))}
      </div>
      {warnings > 0 && (
        <p className="warning-text">
          {warnings} line(s) need attention —{' '}
          <button
            className="link"
            onClick={() => {
              setOutcome('');
              setPage(1);
            }}
          >
            show all
          </button>{' '}
          and look for highlighted rows.
        </p>
      )}
      {batch.pendingReviewCount > 0 && (
        <p>
          <Link to="/duplicates">Review possible duplicates →</Link>
        </p>
      )}
      <select
        value={outcome}
        onChange={(e) => {
          setOutcome(e.target.value as ImportOutcome | '');
          setPage(1);
        }}
        aria-label="Outcome"
      >
        <option value="">All results</option>
        {outcomes.map((o) => (
          <option key={o} value={o}>
            {outcomeLabels[o]}
          </option>
        ))}
      </select>
      <table className="table">
        <tbody>
          {items.items.map((item) => (
            <tr key={item.id} className={warningOutcomes.includes(item.outcome) ? 'warning-row' : undefined}>
              <td>
                <span className={`badge outcome-${item.outcome}`}>{outcomeLabels[item.outcome]}</span>
              </td>
              <td className="path">{item.bookId ? <Link to={`/books/${item.bookId}`}>{item.path}</Link> : item.path}</td>
              <td className="muted">{item.message}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <Pagination page={page} pageSize={items.pageSize} total={items.totalCount} onChange={setPage} />
    </div>
  );
}
