import { Link } from 'react-router';
import type { BookListItem } from '../api/types';
import { filesStateInfo } from '../format';
import { Cover } from './Cover';
import { Empty, StatusBadge } from './common';

/** Catalog grid: covers with title and author under them (the list view shows more details). */
export function BookGrid({ books, empty = 'No books.' }: { books: BookListItem[]; empty?: string }) {
  if (books.length === 0) return <Empty>{empty}</Empty>;

  return (
    <ul className="book-grid">
      {books.map((book) => (
        <li
          key={book.id}
          className={`book-card ${filesStateInfo[book.filesState].className}`}
          title={filesStateInfo[book.filesState].label || undefined}
        >
          <Link to={`/books/${book.id}`} className="book-card-link">
            <Cover cover={book.cover} title={book.title} format={book.files[0]?.format} size="card" />
            <span className="book-card-title">{book.title}</span>
          </Link>
          <div className="muted book-card-author">{book.authors.join(', ') || 'Unknown author'}</div>
          <div className="book-card-badges">
            <StatusBadge status={book.readingStatus} />
            {book.rating != null && <span className="badge">★ {book.rating}</span>}
          </div>
        </li>
      ))}
    </ul>
  );
}
