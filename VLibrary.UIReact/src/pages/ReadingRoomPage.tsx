import { useState } from 'react';
import { Link } from 'react-router';
import { useNoteMutations, useReadingRoom } from '../api/hooks';
import type { BookListItem } from '../api/types';
import { Empty, ErrorMessage, Loading, StatusBadge } from '../components/common';
import { Cover } from '../components/Cover';
import { OpenFileButton } from '../components/OpenFileButton';
import { NoteForm } from '../components/NoteForm';
import { filesStateInfo } from '../format';

/** UC-5: book lists; a book is opened by its file path. UC-6: quick note while reading. */
export function ReadingRoomPage() {
  const room = useReadingRoom();

  if (room.isLoading) return <Loading />;
  if (!room.data) return <ErrorMessage error={room.error} />;

  return (
    <section>
      <h1>Reading room</h1>
      <Shelf title="Recently opened" books={room.data.recentlyOpened} empty="Open a book from its file link to see it here." />
      <Shelf title="In progress" books={room.data.inProgress} empty="No books in progress." />
      {room.data.shelves.map((shelf) => (
        <Shelf key={shelf.id} title={shelf.name} books={shelf.books} more={shelf.booksCount - shelf.books.length} shelfId={shelf.id} />
      ))}
    </section>
  );
}

function Shelf({ title, books, empty, more = 0, shelfId }: { title: string; books: BookListItem[]; empty?: string; more?: number; shelfId?: number }) {
  return (
    <div className="room-shelf">
      <h2>{title}</h2>
      {books.length === 0 ? (
        <Empty>{empty ?? 'Empty shelf.'}</Empty>
      ) : (
        <div className="room-books">
          {books.map((book) => (
            <RoomBook key={book.id} book={book} />
          ))}
        </div>
      )}
      {more > 0 && shelfId && <Link to={`/?shelfId=${shelfId}`}>+{more} more…</Link>}
    </div>
  );
}

function RoomBook({ book }: { book: BookListItem }) {
  const [noting, setNoting] = useState(false);
  const notes = useNoteMutations();

  return (
    <article className={`room-book ${filesStateInfo[book.filesState].className}`} title={filesStateInfo[book.filesState].label || undefined}>
      <Link to={`/books/${book.id}`} tabIndex={-1}>
        <Cover cover={book.cover} title={book.title} format={book.files[0]?.format} size="medium" />
      </Link>
      <Link to={`/books/${book.id}`} className="book-title">
        {book.title}
      </Link>
      <div className="muted">{book.authors.join(', ')}</div>
      <StatusBadge status={book.readingStatus} />
      {book.files.map((file) => (
        <OpenFileButton key={file.id} file={file} compact />
      ))}
      {noting ? (
        <NoteForm
          files={book.files}
          submitLabel="Save note"
          pending={notes.create.isPending}
          error={notes.create.error}
          onSubmit={(fields) => notes.create.mutateAsync({ bookId: book.id, fields }).then(() => setNoting(false))}
          onCancel={() => setNoting(false)}
        />
      ) : (
        <button className="small" onClick={() => setNoting(true)}>
          + Quick note
        </button>
      )}
    </article>
  );
}
