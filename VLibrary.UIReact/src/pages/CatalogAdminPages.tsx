import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import {
  coverApi,
  useAuthorMutations,
  useAuthors,
  useCheckLibrary,
  useCoverStatus,
  useCoversChanged,
  useResetFailedCovers,
  useSettings,
  useUpdateSettings,
} from '../api/hooks';
import type { Author } from '../api/types';
import { ErrorMessage, Loading, Pagination } from '../components/common';

export function AuthorsPage() {
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const authors = useAuthors(q, page);
  const mutations = useAuthorMutations();
  const [editing, setEditing] = useState<number | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => {
      setQ(text.trim());
      setPage(1);
    }, 350);
    return () => clearTimeout(timer);
  }, [text]);

  return (
    <section>
      <h1>Authors</h1>
      <input className="search" type="search" placeholder="Search authors…" value={text} onChange={(e) => setText(e.target.value)} />
      <ErrorMessage error={authors.error ?? mutations.update.error ?? mutations.remove.error} />
      {authors.isLoading && <Loading />}
      <table className="table">
        <tbody>
          {authors.data?.items.map((author) =>
            editing === author.id ? (
              <AuthorEditor key={author.id} author={author} onDone={() => setEditing(null)} />
            ) : (
              <tr key={author.id}>
                <td>
                  <Link to={`/?authorId=${author.id}`}>{author.displayName}</Link>
                </td>
                <td className="muted">{author.booksCount} book(s)</td>
                <td>
                  <button className="link" onClick={() => setEditing(author.id)}>
                    Edit
                  </button>
                  {author.booksCount === 0 && (
                    <button className="link danger" onClick={() => mutations.remove.mutate(author.id)}>
                      Delete
                    </button>
                  )}
                </td>
              </tr>
            ),
          )}
        </tbody>
      </table>
      <Pagination page={page} pageSize={50} total={authors.data?.totalCount ?? 0} onChange={setPage} />
    </section>
  );
}

function AuthorEditor({ author, onDone }: { author: Author; onDone: () => void }) {
  const { update } = useAuthorMutations();
  const [lastName, setLastName] = useState(author.lastName ?? '');
  const [firstName, setFirstName] = useState(author.firstName ?? '');
  const [middleName, setMiddleName] = useState(author.middleName ?? '');
  const [initials, setInitials] = useState(author.nameInitials ?? '');
  const clean = (v: string) => v.trim() || null;

  return (
    <tr>
      <td colSpan={3}>
        <form
          className="row"
          onSubmit={(e) => {
            e.preventDefault();
            update.mutate(
              {
                id: author.id,
                fields: { lastName: clean(lastName), firstName: clean(firstName), middleName: clean(middleName), nameInitials: clean(initials), biography: null },
              },
              { onSuccess: onDone },
            );
          }}
        >
          <input required placeholder="Last name" value={lastName} onChange={(e) => setLastName(e.target.value)} />
          <input placeholder="First name" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
          <input placeholder="Middle name" value={middleName} onChange={(e) => setMiddleName(e.target.value)} />
          <input placeholder="Initials" className="narrow" value={initials} onChange={(e) => setInitials(e.target.value)} />
          <button type="submit" className="primary" disabled={update.isPending}>
            Save
          </button>
          <button type="button" onClick={onDone}>
            Cancel
          </button>
        </form>
      </td>
    </tr>
  );
}

/** UC-8 settings. */
export function SettingsPage() {
  const settings = useSettings();
  const update = useUpdateSettings();

  if (settings.isLoading) return <Loading />;
  if (!settings.data) return <ErrorMessage error={settings.error} />;
  const value = settings.data;

  return (
    <section>
      <h1>Settings</h1>
      <div className="panel">
        <h3>Reminders</h3>
        <label className="check">
          <input type="checkbox" checked={value.remindersEnabled} onChange={(e) => update.mutate({ ...value, remindersEnabled: e.target.checked })} />
          Show a note to remember when the app starts
        </label>
        <label>
          Notes per start{' '}
          <select value={value.remindersPerStartup} onChange={(e) => update.mutate({ ...value, remindersPerStartup: Number(e.target.value) })}>
            {[1, 2, 3, 5, 10].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </label>
        <ErrorMessage error={update.error} />
      </div>
      <LibraryPanel />
    </section>
  );
}

/** Files live in VStorage; "Check files now" marks files whose object is gone as missing. */
function LibraryPanel() {
  const check = useCheckLibrary();

  return (
    <div className="panel">
      <h3>Library</h3>
      <p className="muted">
        Files live in VStorage. Categories are managed (and backed up) on the <Link to="/categories">Categories</Link> page.
      </p>
      <div className="row">
        <button className="primary" disabled={check.isPending} onClick={() => check.mutate()}>
          {check.isPending ? 'Checking…' : 'Check files now'}
        </button>
        {check.data && (
          <span className="muted">
            {check.data.filesOk} ok · {check.data.filesMissing} missing in storage
          </span>
        )}
      </div>
      <ErrorMessage error={check.error} />
      <CoversSection />
    </div>
  );
}

/**
 * B1: covers of files imported before covers existed (or tried without result). Browser-driven like the import:
 * each call makes up to 5 covers (files downloaded from VStorage with your token) until nothing remains.
 */
function CoversSection() {
  const status = useCoverStatus();
  const changed = useCoversChanged();
  const reset = useResetFailedCovers();
  const [running, setRunning] = useState(false);
  const [done, setDone] = useState(0);
  const [paused, setPaused] = useState<string | null>(null);
  const stopRequested = useRef(false);

  const generate = async () => {
    setRunning(true);
    setPaused(null);
    stopRequested.current = false;
    try {
      for (;;) {
        const result = await coverApi.generate(5);
        setDone((d) => d + result.generated);
        if (result.remaining === 0 || result.processed === 0 || stopRequested.current) break;
      }
    } catch (e) {
      // 503: VStorage unavailable — nothing lost, "Generate" continues where it stopped.
      setPaused(e instanceof Error ? e.message : String(e));
    } finally {
      setRunning(false);
      await changed();
    }
  };

  const data = status.data;
  return (
    <div className="covers-section">
      <h4>Covers</h4>
      {data && (
        <p className="muted">
          {data.withCover} file(s) with a cover · {data.pending} not generated yet · {data.withoutCover} without a cover
          (no picture, damaged file or DjVuLibre missing)
        </p>
      )}
      <div className="row">
        {!running ? (
          <button className="primary" disabled={!data || data.pending === 0} onClick={() => void generate()}>
            Generate missing covers{data && data.pending > 0 ? ` (${data.pending})` : ''}
          </button>
        ) : (
          <>
            <span>Generating… {done} made</span>
            <button onClick={() => (stopRequested.current = true)}>Stop</button>
          </>
        )}
        {data && data.withoutCover > 0 && !running && (
          <button disabled={reset.isPending} onClick={() => reset.mutate()} title="Try those files again with the next Generate">
            Retry files without cover ({data.withoutCover})
          </button>
        )}
      </div>
      {paused && <div className="error">Paused: {paused} — press Generate again to continue.</div>}
      <ErrorMessage error={status.error ?? reset.error} />
    </div>
  );
}
