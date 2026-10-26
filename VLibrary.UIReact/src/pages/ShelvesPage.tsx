import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { useShelf, useShelfMutations, useShelves } from '../api/hooks';
import type { Shelf } from '../api/types';
import { Empty, ErrorMessage, Loading, StatusBadge } from '../components/common';
import { Cover } from '../components/Cover';

function move<T>(items: T[], index: number, delta: number): T[] {
  const target = index + delta;
  if (target < 0 || target >= items.length) return items;
  const copy = [...items];
  [copy[index], copy[target]] = [copy[target], copy[index]];
  return copy;
}

/** UC-7: custom shelves (a book can be on many shelves). */
export function ShelvesPage() {
  const shelves = useShelves();
  const mutations = useShelfMutations();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [name, setName] = useState('');

  const create = (event: FormEvent) => {
    event.preventDefault();
    mutations.create.mutate({ name: name.trim(), description: null }, { onSuccess: (r) => { setName(''); setSelectedId(r.id); } });
  };

  const list = shelves.data ?? [];

  return (
    <section>
      <h1>Shelves</h1>
      <form className="row" onSubmit={create}>
        <input required maxLength={200} placeholder="New shelf, e.g. “Marine biology”" value={name} onChange={(e) => setName(e.target.value)} />
        <button type="submit" className="primary" disabled={!name.trim() || mutations.create.isPending}>
          Create
        </button>
      </form>
      <ErrorMessage error={shelves.error ?? mutations.create.error ?? mutations.update.error ?? mutations.remove.error} />
      {shelves.isLoading && <Loading />}
      {list.length === 0 && !shelves.isLoading && <Empty>No shelves yet.</Empty>}

      <div className="split">
        <ul className="shelf-list">
          {list.map((shelf, index) => (
            <li key={shelf.id} className={shelf.id === selectedId ? 'selected' : ''}>
              <button className="link" onClick={() => setSelectedId(shelf.id)}>
                {shelf.name} <span className="muted">({shelf.booksCount})</span>
              </button>
              <span className="row">
                <button className="small" title="Move up" onClick={() => mutations.reorder.mutate(move(list, index, -1).map((s) => s.id))}>
                  ↑
                </button>
                <button className="small" title="Move down" onClick={() => mutations.reorder.mutate(move(list, index, 1).map((s) => s.id))}>
                  ↓
                </button>
              </span>
            </li>
          ))}
        </ul>
        {selectedId != null && list.some((s) => s.id === selectedId) && (
          <ShelfDetails shelf={list.find((s) => s.id === selectedId)!} onDeleted={() => setSelectedId(null)} />
        )}
      </div>
    </section>
  );
}

function ShelfDetails({ shelf, onDeleted }: { shelf: Shelf; onDeleted: () => void }) {
  const details = useShelf(shelf.id);
  const mutations = useShelfMutations();
  const [name, setName] = useState(shelf.name);
  const [description, setDescription] = useState(shelf.description ?? '');
  const books = details.data?.books ?? [];

  return (
    <div className="panel">
      <form
        className="shelf-edit"
        onSubmit={(e) => {
          e.preventDefault();
          mutations.update.mutate({ id: shelf.id, name: name.trim(), description: description.trim() || null });
        }}
      >
        <input required maxLength={200} value={name} onChange={(e) => setName(e.target.value)} aria-label="Name" />
        <textarea rows={2} placeholder="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
        <div className="row">
          <button type="submit">Save</button>
          <button
            type="button"
            className="danger"
            onClick={() => window.confirm(`Delete shelf “${shelf.name}”? The books stay in the library.`) && mutations.remove.mutate(shelf.id, { onSuccess: onDeleted })}
          >
            Delete shelf
          </button>
        </div>
      </form>

      {details.isLoading && <Loading />}
      {books.length === 0 && !details.isLoading && <Empty>Add books from their book card.</Empty>}
      <ol className="shelf-books">
        {books.map((book, index) => (
          <li key={book.id}>
            <Link to={`/books/${book.id}`} tabIndex={-1}>
              <Cover cover={book.cover} title={book.title} format={book.files[0]?.format} size="small" />
            </Link>
            <Link to={`/books/${book.id}`}>{book.title}</Link>
            <span className="muted"> {book.authors.join(', ')}</span> <StatusBadge status={book.readingStatus} />
            <span className="row">
              <button className="small" onClick={() => mutations.reorderBooks.mutate({ id: shelf.id, ids: move(books, index, -1).map((b) => b.id) })}>
                ↑
              </button>
              <button className="small" onClick={() => mutations.reorderBooks.mutate({ id: shelf.id, ids: move(books, index, 1).map((b) => b.id) })}>
                ↓
              </button>
              <button className="small" onClick={() => mutations.removeBook.mutate({ id: shelf.id, bookId: book.id })}>
                Remove
              </button>
            </span>
          </li>
        ))}
      </ol>
    </div>
  );
}
