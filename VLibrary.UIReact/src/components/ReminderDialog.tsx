import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useReminders, useReviewNote } from '../api/hooks';
import type { ReviewAnswer } from '../api/types';
import { Modal } from './common';

const shownKey = 'vlibrary.remindersShown';

function alreadyShown(): boolean {
  try {
    return sessionStorage.getItem(shownKey) === '1';
  } catch {
    return false;
  }
}

/** UC-8: once per browser session, show the notes the API picked for spaced repetition. */
export function ReminderDialog() {
  const [enabled] = useState(() => !alreadyShown());
  const reminders = useReminders(enabled);
  const review = useReviewNote();
  const navigate = useNavigate();
  const [index, setIndex] = useState(0);
  const [closed, setClosed] = useState(false);

  const items = reminders.data ?? [];
  if (!enabled || closed || index >= items.length) return null;

  const { note, authors } = items[index];

  const close = () => {
    try {
      sessionStorage.setItem(shownKey, '1');
    } catch {
      // storage unavailable: the reminder may show again
    }
    setClosed(true);
  };

  const answer = async (value: ReviewAnswer) => {
    await review.mutateAsync({ noteId: note.id, answer: value });
    if (index + 1 >= items.length) close();
    else setIndex(index + 1);
  };

  return (
    <Modal title="Remember this?" onClose={close}>
      <div className="reminder">
        <blockquote className={note.type === 'Quote' ? 'quote' : ''}>{note.text}</blockquote>
        <p className="muted">
          {note.bookTitle}
          {authors.length > 0 ? ` — ${authors.join(', ')}` : ''}
          {note.pageNumber ? `, p. ${note.pageNumber}` : ''}
        </p>
        <div className="row">
          <button className="primary" disabled={review.isPending} onClick={() => answer('Remember')}>
            Remember
          </button>
          <button disabled={review.isPending} onClick={() => answer('Forgot')}>
            Forgot
          </button>
          <button disabled={review.isPending} onClick={() => answer('Skip')}>
            Skip
          </button>
          <button
            className="link"
            onClick={() => {
              close();
              navigate(`/books/${note.bookId}`);
            }}
          >
            Open book
          </button>
        </div>
      </div>
    </Modal>
  );
}
