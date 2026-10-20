import { useEffect, useRef, type ReactNode } from 'react';
import type { ReadingStatus } from '../api/types';
import { statusLabels } from '../format';

export function ErrorMessage({ error }: { error: unknown }) {
  if (!error) return null;
  const message = error instanceof Error ? error.message : String(error);
  return <div className="error" role="alert">{message}</div>;
}

export function Loading() {
  return <div className="muted">Loading…</div>;
}

export function Empty({ children }: { children: ReactNode }) {
  return <div className="empty">{children}</div>;
}

export function StatusBadge({ status }: { status: ReadingStatus }) {
  return <span className={`badge status-${status}`}>{statusLabels[status]}</span>;
}

export function Pagination({ page, pageSize, total, onChange }: { page: number; pageSize: number; total: number; onChange: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  return (
    <div className="pagination">
      <button disabled={page <= 1} onClick={() => onChange(page - 1)}>
        ‹ Prev
      </button>
      <span>
        Page {page} of {pages} ({total})
      </span>
      <button disabled={page >= pages} onClick={() => onChange(page + 1)}>
        Next ›
      </button>
    </div>
  );
}

/** Rating 1-10; clicking the current value clears it. */
export function RatingInput({ value, onChange, disabled }: { value: number | null; onChange: (value: number | null) => void; disabled?: boolean }) {
  return (
    <div className="rating" aria-label="Rating">
      {Array.from({ length: 10 }, (_, i) => i + 1).map((n) => (
        <button
          key={n}
          type="button"
          disabled={disabled}
          className={value != null && n <= value ? 'on' : ''}
          title={`${n}/10`}
          onClick={() => onChange(value === n ? null : n)}
        >
          ★
        </button>
      ))}
      <span className="muted">{value ? `${value}/10` : 'not rated'}</span>
    </div>
  );
}

export function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  const dialog = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const element = dialog.current;
    element?.showModal();
    return () => element?.close();
  }, []);

  return (
    <dialog ref={dialog} className="modal" onClose={onClose} onCancel={onClose}>
      <header>
        <h2>{title}</h2>
        <button className="link" onClick={onClose} aria-label="Close">
          ✕
        </button>
      </header>
      {children}
    </dialog>
  );
}
