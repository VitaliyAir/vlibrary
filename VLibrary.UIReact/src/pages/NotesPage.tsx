import { useEffect, useState } from 'react';
import { useNoteSearch } from '../api/hooks';
import { Empty, ErrorMessage, Loading, Pagination } from '../components/common';
import { NoteItem } from './BookPage';

/** UC-4: search in personal notes and quotes. */
export function NotesPage() {
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const notes = useNoteSearch(q, page);

  useEffect(() => {
    const timer = setTimeout(() => {
      setQ(text.trim());
      setPage(1);
    }, 350);
    return () => clearTimeout(timer);
  }, [text]);

  return (
    <section>
      <h1>Notes &amp; quotes</h1>
      <input className="search" type="search" placeholder="Search notes…" value={text} onChange={(e) => setText(e.target.value)} autoFocus />
      <ErrorMessage error={notes.error} />
      {notes.isLoading && <Loading />}
      {notes.data?.items.length === 0 && <Empty>{q ? 'No notes match.' : 'No notes yet.'}</Empty>}
      <ul className="notes">
        {notes.data?.items.map((note) => (
          <NoteItem key={note.id} note={note} showBook />
        ))}
      </ul>
      <Pagination page={page} pageSize={30} total={notes.data?.totalCount ?? 0} onChange={setPage} />
    </section>
  );
}
