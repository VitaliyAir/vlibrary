import { useRef, useState } from 'react';
import { ApiError } from '../api/client';
import { backupApi } from '../api/hooks';
import type { BackupFile } from '../api/types';
import { formatSize } from '../format';

declare global {
  interface Window {
    /** File System Access API (Chrome, Edge); not in TypeScript's DOM types. */
    showDirectoryPicker?: (options?: { id?: string; mode?: 'read' | 'readwrite' }) => Promise<FileSystemDirectoryHandle>;
  }
}

interface Progress {
  total: number;
  copied: number;
  skipped: number;
  bytes: number;
  current: string | null;
  failed: { path: string; error: string }[];
}

/**
 * Backup: every library file goes into a folder you choose, in subfolders like the categories
 * (Library/Physics/Classic/book.pdf). Files already there with the same size are skipped, so another run continues an
 * interrupted one. One file at a time, streamed VStorage -> API -> disk.
 */
export function BackupPanel() {
  const supported = typeof window.showDirectoryPicker === 'function';
  const [progress, setProgress] = useState<Progress | null>(null);
  const [running, setRunning] = useState(false);
  const [paused, setPaused] = useState<string | null>(null);
  const [missing, setMissing] = useState(0);
  const stopRequested = useRef(false);

  const run = async () => {
    let root: FileSystemDirectoryHandle;
    try {
      root = await window.showDirectoryPicker!({ id: 'vlibrary-backup', mode: 'readwrite' });
    } catch {
      return; // the picker was closed
    }

    setRunning(true);
    setPaused(null);
    stopRequested.current = false;
    try {
      const manifest = await backupApi.manifest();
      setMissing(manifest.missing);
      const state: Progress = { total: manifest.files.length, copied: 0, skipped: 0, bytes: 0, current: null, failed: [] };
      const show = () => setProgress({ ...state, failed: [...state.failed] });
      const folders = new Map<string, FileSystemDirectoryHandle>();
      for (const file of manifest.files) {
        if (stopRequested.current) break;
        const path = `${file.categoryPath}/${file.fileName}`;
        state.current = path;
        show();
        try {
          const folder = await folderFor(root, file.categoryPath, folders);
          if (await hasSameSize(folder, file)) {
            state.skipped++;
          } else {
            await copy(folder, file);
            state.copied++;
            state.bytes += file.sizeBytes;
          }
        } catch (e) {
          // VStorage unavailable: stop here, the next run continues. Anything else: note it and go on.
          if (e instanceof ApiError && e.status === 503) {
            setPaused(e.message);
            break;
          }
          state.failed.push({ path, error: e instanceof Error ? e.message : String(e) });
        }
      }
      state.current = null;
      show();
    } catch (e) {
      setPaused(e instanceof Error ? e.message : String(e));
    } finally {
      setRunning(false);
    }
  };

  return (
    <div className="panel">
      <h3>Backup</h3>
      <p className="muted">
        Copies every book into a folder you choose, in subfolders like the categories (e.g. Library/Physics/Classic). Books
        already there with the same size are skipped, so run it again to continue or to add new books. Books removed from
        the library are not deleted from the backup.
      </p>
      {!supported ? (
        <div className="error">This browser cannot write into a folder. Use Chrome or Edge.</div>
      ) : (
        <div className="row">
          {!running ? (
            <button className="primary" onClick={() => void run()}>
              Back up to a folder…
            </button>
          ) : (
            <button onClick={() => (stopRequested.current = true)}>Stop</button>
          )}
        </div>
      )}
      {progress && (
        <p>
          {running ? 'Copying… ' : 'Finished: '}
          {progress.copied} copied ({formatSize(progress.bytes)}) · {progress.skipped} already there · {progress.failed.length} failed · of{' '}
          {progress.total}
          {progress.current && (
            <>
              <br />
              <span className="muted">{progress.current}</span>
            </>
          )}
        </p>
      )}
      {missing > 0 && <p className="muted">{missing} file(s) are missing in VStorage and were left out.</p>}
      {paused && <div className="error">Paused: {paused} — press “Back up to a folder…” and choose the same folder to continue.</div>}
      {progress && progress.failed.length > 0 && (
        <ul className="muted">
          {progress.failed.slice(0, 20).map((f) => (
            <li key={f.path}>
              {f.path}: {f.error}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

async function folderFor(root: FileSystemDirectoryHandle, categoryPath: string, cache: Map<string, FileSystemDirectoryHandle>) {
  let folder = cache.get(categoryPath);
  if (!folder) {
    folder = root;
    for (const name of categoryPath.split('/')) {
      folder = await folder.getDirectoryHandle(name, { create: true });
    }
    cache.set(categoryPath, folder);
  }
  return folder;
}

async function hasSameSize(folder: FileSystemDirectoryHandle, file: BackupFile): Promise<boolean> {
  try {
    const existing = await (await folder.getFileHandle(file.fileName)).getFile();
    return existing.size === file.sizeBytes;
  } catch {
    return false; // not there yet
  }
}

async function copy(folder: FileSystemDirectoryHandle, file: BackupFile) {
  const response = await backupApi.content(file.id);
  const writable = await (await folder.getFileHandle(file.fileName, { create: true })).createWritable();
  // pipeTo closes the file when done; on failure it aborts and the previous content stays.
  await response.body!.pipeTo(writable);
}
