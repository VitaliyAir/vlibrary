import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useBook, useBookMutations, useBookNotes, useCategories, useCreateBook, useNoteMutations, useShelves } from '../api/hooks';
import { readingStatuses, type BookDetails, type BookFile, type Note, type ReadingStatus } from '../api/types';
import { BookForm, BookQuickForm } from '../components/BookForm';
import { CategoryBreadcrumb, categoryTrail } from '../components/CategoryTree';
import { Cover } from '../components/Cover';
import { Empty, ErrorMessage, Loading, RatingInput } from '../components/common';
import { OpenFileButton } from '../components/OpenFileButton';
import { NoteForm } from '../components/NoteForm';
import { filesStateInfo, formatDate, formatSize, statusLabels } from '../format';

/** "PDF + EPUB · 11 MB · 812 pages · added 20.10.2026" — sizes and pages of the primary file. */
function bookSummary(b: BookDetails, primary: BookFile | undefined): string {
  const formats = [...new Set(b.files.map((f) => f.format))].join(' + ');
  const pages = b.totalPages ?? primary?.pageCount;
  return [
    formats,
    primary && formatSize(primary.sizeBytes),
    pages ? `${pages} pages` : null,
    `added ${new Date(b.createdAt).toLocaleDateString()}`,
  ]
    .filter(Boolean)
    .join(' · ');
}

export function NewBookPage() {
  const create = useCreateBook();
  const navigate = useNavigate();

  return (
    <section>
      <h1>Add book</h1>
      <BookForm
        submitLabel="Create"
        pending={create.isPending}
        error={create.error}
        onSubmit={(fields) => create.mutate(fields, { onSuccess: (r) => navigate(`/books/${r.id}`) })}
        onCancel={() => navigate(-1)}
      />
    </section>
  );
}

/** UC-3: the book card — metadata, files (full paths), rating, status, shelves, tags and notes. */
export function BookPage() {
  const id = Number(useParams().id);
  const book = useBook(id);
  const mutations = useBookMutations(id);
  const shelves = useShelves();
  const categories = useCategories();
  const navigate = useNavigate();
  const [editing, setEditing] = useState(false);

  if (book.isLoading) return <Loading />;
  if (book.error || !book.data) return <ErrorMessage error={book.error ?? 'Book not found.'} />;

  const b = book.data;
  const onShelf = new Set(b.shelves.map((s) => s.id));
  const primary = b.files.find((f) => f.isPrimary) ?? b.files[0];
  const trail = categoryTrail(categories.data ?? [], primary?.categoryId);

  const remove = () => {
    const notes = b.notesCount > 0 ? ` Its ${b.notesCount} note(s) will be deleted.` : '';
    if (window.confirm(`Delete “${b.title}”?${notes} The files stay in VStorage.`)) {
      mutations.remove.mutate(undefined, { onSuccess: () => navigate('/') });
    }
  };

  return (
    <section className="book-page">
      <header className="page-header">
        <div>
          <CategoryBreadcrumb trail={trail} />
          <h1>{b.title}</h1>
          {b.subtitle && <p className="subtitle">{b.subtitle}</p>}
          <p className="book-summary">{bookSummary(b, primary)}</p>
          {filesStateInfo[b.filesState].label && (
            <p>
              <span className={`badge ${filesStateInfo[b.filesState].className}`}>{filesStateInfo[b.filesState].label}</span>
            </p>
          )}
          <p>
            {b.authors.map((a, i) => (
              <span key={a.id}>
                {i > 0 && ', '}
                <Link to={`/?authorId=${a.id}`}>{a.name}</Link>
              </span>
            ))}
          </p>
        </div>
        <div className="row">
          <button onClick={() => setEditing(!editing)}>{editing ? 'Close editor' : 'Add more details'}</button>
          <button className="danger" onClick={remove}>
            Delete
          </button>
        </div>
      </header>

      <ErrorMessage error={mutations.setRating.error ?? mutations.setStatus.error ?? mutations.remove.error} />

      {editing ? (
        <BookForm
          book={b}
          submitLabel="Save"
          pending={mutations.update.isPending}
          error={mutations.update.error}
          onSubmit={(fields) => mutations.update.mutate(fields, { onSuccess: () => setEditing(false) })}
          onCancel={() => setEditing(false)}
        />
      ) : (
        <div className="card-grid">
          <div className="panel">
            <div className="book-top">
              <div className="book-cover-col">
                <Cover cover={b.cover} title={b.title} format={b.files[0]?.format} size="large" />
                <RatingInput value={b.rating} disabled={mutations.setRating.isPending} onChange={(r) => mutations.setRating.mutate(r)} />
                <label>
                  Status{' '}
                  <select value={b.readingStatus} onChange={(e) => mutations.setStatus.mutate(e.target.value as ReadingStatus)}>
                    {readingStatuses.map((s) => (
                      <option key={s} value={s}>
                        {statusLabels[s]}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <BookQuickForm
                key={b.updatedAt}
                book={b}
                pending={mutations.update.isPending}
                error={mutations.update.error}
                onSubmit={(fields) => mutations.update.mutate(fields)}
              />
            </div>
            <dl className="facts">
              {b.publisher && (
                <>
                  <dt>Publisher</dt>
                  <dd>{b.publisher}</dd>
                </>
              )}
              <dt>Last opened</dt>
              <dd>{formatDate(b.lastOpenedAt)}</dd>
            </dl>
            {b.tags.length > 0 && (
              <div className="tags">
                {b.tags.map((t) => (
                  <Link key={t.id} to={`/?tag=${encodeURIComponent(t.name)}`} className="tag">
                    {t.name}
                  </Link>
                ))}
              </div>
            )}
            {b.description && <p className="description">{b.description}</p>}
          </div>

          <div className="panel">
            <h3>Shelves</h3>
            {(shelves.data ?? []).length === 0 && <Empty>No shelves yet.</Empty>}
            {shelves.data?.map((shelf) => (
              <label key={shelf.id} className="check">
                <input
                  type="checkbox"
                  checked={onShelf.has(shelf.id)}
                  onChange={(e) => (e.target.checked ? mutations.addToShelf.mutate(shelf.id) : mutations.removeFromShelf.mutate(shelf.id))}
                />
                {shelf.name}
              </label>
            ))}
          </div>
        </div>
      )}

      <h2>Files</h2>
      {b.files.length === 0 ? (
        <Empty>No files: the book is kept as meta info only.</Empty>
      ) : (
        b.files.map((file) => <BookFileRow key={file.id} bookId={b.id} file={file} canSwitch={b.files.length > 1} />)
      )}

      {b.tableOfContents && !editing && (
        <>
          <h2>Contents</h2>
          <pre className="toc">{b.tableOfContents}</pre>
        </>
      )}

      <BookNotes bookId={b.id} files={b.files} />
    </section>
  );
}

/**
 * D7: when a file cannot be opened, the user decides: open the book with another file (copy or other format)
 * or keep the book without this file (the file record stays as meta info only).
 */
function BookFileRow({ bookId, file, canSwitch }: { bookId: number; file: BookFile; canSwitch: boolean }) {
  const { setPrimaryFile, detachFile, renameFile } = useBookMutations(bookId);
  const [renaming, setRenaming] = useState(false);
  const [name, setName] = useState('');
  const [useAsTitle, setUseAsTitle] = useState(false);
  const extension = file.fileName.match(/\.[^.]*$/)?.[0] ?? '';

  return (
    <div className="file-row">
      <OpenFileButton file={file} />
      {renaming ? (
        <form
          className="row"
          onSubmit={(e) => {
            e.preventDefault();
            renameFile.mutate({ fileId: file.id, name: name.trim(), useAsTitle }, { onSuccess: () => setRenaming(false) });
          }}
        >
          <input required maxLength={250} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          <span className="muted">{extension}</span>
          <label className="check">
            <input type="checkbox" checked={useAsTitle} onChange={(e) => setUseAsTitle(e.target.checked)} />
            Also use as book title
          </label>
          <button type="submit" className="small" disabled={renameFile.isPending || !name.trim()}>
            Save
          </button>
          <button type="button" className="small" onClick={() => setRenaming(false)}>
            Cancel
          </button>
        </form>
      ) : (
        <div className="row">
          <button
            className="small"
            onClick={() => {
              setName(extension ? file.fileName.slice(0, -extension.length) : file.fileName);
              setUseAsTitle(false);
              setRenaming(true);
            }}
          >
            Rename
          </button>
          {canSwitch && !file.isPrimary && file.status === 'Ok' && (
            <button className="small" disabled={setPrimaryFile.isPending} onClick={() => setPrimaryFile.mutate(file.id)}>
              Use this file
            </button>
          )}
          {file.status !== 'Ok' && (
            <button
              className="small"
              disabled={detachFile.isPending}
              onClick={() =>
                window.confirm('Keep the book without this file? The file record stays as meta info only.') && detachFile.mutate(file.id)
              }
            >
              Keep book without this file
            </button>
          )}
        </div>
      )}
      <ErrorMessage error={setPrimaryFile.error ?? detachFile.error ?? renameFile.error} />
    </div>
  );
}

/** UC-6: notes and quotes of the book. */
function BookNotes({ bookId, files }: { bookId: number; files: BookFile[] }) {
  const notes = useBookNotes(bookId);
  const mutations = useNoteMutations();
  const [editingId, setEditingId] = useState<number | null>(null);

  return (
    <>
      <h2>Notes &amp; quotes</h2>
      <NoteForm
        files={files}
        submitLabel="Add note"
        pending={mutations.create.isPending}
        error={mutations.create.error}
        onSubmit={(fields) => mutations.create.mutateAsync({ bookId, fields })}
      />
      {notes.isLoading && <Loading />}
      <ul className="notes">
        {notes.data?.map((note) =>
          editingId === note.id ? (
            <li key={note.id}>
              <NoteForm
                files={files}
                initial={note}
                submitLabel="Save"
                pending={mutations.update.isPending}
                error={mutations.update.error}
                onSubmit={(fields) => mutations.update.mutateAsync({ id: note.id, fields }).then(() => setEditingId(null))}
                onCancel={() => setEditingId(null)}
              />
            </li>
          ) : (
            <NoteItem key={note.id} note={note} onEdit={() => setEditingId(note.id)} onDelete={() => mutations.remove.mutate(note.id)} />
          ),
        )}
      </ul>
    </>
  );
}

export function NoteItem({ note, onEdit, onDelete, showBook }: { note: Note; onEdit?: () => void; onDelete?: () => void; showBook?: boolean }) {
  return (
    <li className={`note ${note.type === 'Quote' ? 'quote' : ''}`}>
      <div className="note-text">{note.text}</div>
      <div className="muted">
        {showBook && (
          <>
            <Link to={`/books/${note.bookId}`}>{note.bookTitle}</Link>
            {' · '}
          </>
        )}
        {note.type}
        {note.pageNumber ? ` · p. ${note.pageNumber}` : ''}
        {note.fileName ? ` · ${note.fileName}` : ''} · {formatDate(note.createdAt)}
        {onEdit && (
          <button className="link" onClick={onEdit}>
            Edit
          </button>
        )}
        {onDelete && (
          <button className="link danger" onClick={() => window.confirm('Delete this note?') && onDelete()}>
            Delete
          </button>
        )}
      </div>
    </li>
  );
}
