import { Link } from 'react-router';
import type { BookListItem } from '../api/types';
import { filesStateInfo } from '../format';
import { Cover } from './Cover';
import { Empty, StatusBadge } from './common';
import { OpenFileButton } from './OpenFileButton';

export function BookList({ books, showFiles, empty = 'No books.' }: { books: BookListItem[]; showFiles?: boolean; empty?: string }) {
  if (books.length === 0) return <Empty>{empty}</Empty>;

  return (
    <ul className="book-list">
      {books.map((book) => (
        <li key={book.id} className={filesStateInfo[book.filesState].className} title={filesStateInfo[book.filesState].label || undefined}>
          <Link to={`/books/${book.id}`} className="cover-link" tabIndex={-1}>
            <Cover cover={book.cover} title={book.title} format={book.files[0]?.format} size="small" />
          </Link>
          <div className="book-main">
            <Link to={`/books/${book.id}`} className="book-title">
              {book.title}
            </Link>
            {book.subtitle && <span className="muted"> — {book.subtitle}</span>}
            <div className="muted">
              {book.authors.join(', ') || 'Unknown author'}
              {book.yearPublished ? ` · ${book.yearPublished}` : ''}
              {book.notesCount > 0 ? ` · ${book.notesCount} note(s)` : ''}
            </div>
            {book.tags.length > 0 && (
              <div className="tags">
                {book.tags.map((tag) => (
                  <span key={tag} className="tag">
                    {tag}
                  </span>
                ))}
              </div>
            )}
            {showFiles && book.files.map((file) => <OpenFileButton key={file.id} file={file} compact />)}
          </div>
          <div className="book-side">
            <StatusBadge status={book.readingStatus} />
            {filesStateInfo[book.filesState].label && (
              <span className={`badge ${filesStateInfo[book.filesState].className}`}>{filesStateInfo[book.filesState].label}</span>
            )}
            {book.rating != null && <span className="badge">★ {book.rating}</span>}
            {!showFiles && <span className="muted">{book.files.map((f) => f.format).join(' · ')}</span>}
          </div>
        </li>
      ))}
    </ul>
  );
}
