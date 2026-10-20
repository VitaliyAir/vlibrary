import { Link } from 'react-router';
import { useDuplicateMutations, useDuplicates } from '../api/hooks';
import { Cover } from '../components/Cover';
import { Empty, ErrorMessage, Loading } from '../components/common';
import type { DuplicateReason } from '../api/types';
import { formatSize } from '../format';

const reasonLabels: Record<DuplicateReason, string> = {
  IsbnMatch: 'Same ISBN',
  TitleMatch: 'Same title',
  TitleWordsMatch: 'Same title words',
  ContentTitleMatch: 'Same title inside the file',
  SizeMatch: 'Same type and size',
};

/** UC-2: decide for each imported file that looks like an existing book. */
export function DuplicatesPage() {
  const duplicates = useDuplicates();
  const { link, createBook, remove } = useDuplicateMutations();
  const busy = link.isPending || createBook.isPending || remove.isPending;

  return (
    <section>
      <h1>Possible duplicates</h1>
      <ErrorMessage error={duplicates.error ?? link.error ?? createBook.error ?? remove.error} />
      {duplicates.isLoading && <Loading />}
      {duplicates.data?.length === 0 && <Empty>Nothing to review.</Empty>}

      {duplicates.data?.map((item) => (
        <article key={item.fileMetaId} className="panel duplicate">
          <header className="duplicate-header">
            <Cover
              cover={item.coverVersion != null ? { fileId: item.fileMetaId, version: item.coverVersion } : null}
              title={item.metadata?.title ?? item.fileName}
              format={item.format}
              size="medium"
            />
            <div>
              <span className="badge">{item.format}</span> <strong>{item.fileName}</strong>{' '}
              <span className="muted">{formatSize(item.sizeBytes)}</span>
              <div className="path muted">{item.libraryPath}</div>
            </div>
          </header>
          {item.metadata && (
            <p>
              Found in file: <strong>{item.metadata.title ?? '—'}</strong>
              {item.metadata.authors.length > 0 && ` — ${item.metadata.authors.join(', ')}`}
              {item.metadata.year && `, ${item.metadata.year}`}
              {item.metadata.isbn && `, ISBN ${item.metadata.isbn}`}
              <span className="muted"> (from {item.metadata.source})</span>
            </p>
          )}

          <table className="table">
            <tbody>
              {item.candidates.map((c) => (
                <tr key={c.bookId}>
                  <td>
                    <Link to={`/books/${c.bookId}`}>{c.title}</Link>
                    <div className="muted">
                      {c.authors.join(', ')}
                      {c.yearPublished ? `, ${c.yearPublished}` : ''}
                      {c.isbn ? `, ISBN ${c.isbn}` : ''} · {c.filesCount} file(s)
                    </div>
                  </td>
                  <td>
                    <span className="badge">
                      {reasonLabels[c.reason]} · {Math.round(c.score * 100)}%
                    </span>
                  </td>
                  <td>
                    <button className="primary small" disabled={busy} onClick={() => link.mutate({ fileId: item.fileMetaId, bookId: c.bookId })}>
                      Same book — link file
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="row">
            <button disabled={busy} onClick={() => createBook.mutate(item.fileMetaId)}>
              Different book — create new
            </button>
            <button className="danger" disabled={busy} onClick={() => remove.mutate(item.fileMetaId)}>
              Remove file from library
            </button>
          </div>
        </article>
      ))}
    </section>
  );
}
