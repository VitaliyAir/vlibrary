import { useState, type FormEvent } from 'react';
import type { BookFile, NoteFields, NoteType } from '../api/types';
import { ErrorMessage } from './common';

/** UC-6: a note or quote with the page number (typed by hand) and the file the page refers to. */
export function NoteForm({
  files,
  initial,
  submitLabel,
  onSubmit,
  onCancel,
  pending,
  error,
}: {
  files: BookFile[];
  initial?: NoteFields;
  submitLabel: string;
  onSubmit: (fields: NoteFields) => Promise<unknown>;
  onCancel?: () => void;
  pending?: boolean;
  error?: unknown;
}) {
  const [type, setType] = useState<NoteType>(initial?.type ?? 'Note');
  const [text, setText] = useState(initial?.text ?? '');
  const [page, setPage] = useState(initial?.pageNumber?.toString() ?? '');
  const [fileId, setFileId] = useState(initial?.fileMetaId?.toString() ?? (files.length === 1 ? String(files[0].id) : ''));

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    await onSubmit({
      type,
      text,
      pageNumber: page ? Number(page) : null,
      fileMetaId: fileId ? Number(fileId) : null,
    });
    if (!initial) {
      setText('');
      setPage('');
    }
  };

  return (
    <form className="note-form" onSubmit={submit}>
      <div className="row">
        <select value={type} onChange={(e) => setType(e.target.value as NoteType)} aria-label="Type">
          <option value="Note">Note</option>
          <option value="Quote">Quote</option>
        </select>
        <input type="number" min={1} placeholder="Page" value={page} onChange={(e) => setPage(e.target.value)} className="narrow" aria-label="Page" />
        {files.length > 1 && (
          <select value={fileId} onChange={(e) => setFileId(e.target.value)} aria-label="File">
            <option value="">(no file)</option>
            {files.map((f) => (
              <option key={f.id} value={f.id}>
                {f.fileName}
              </option>
            ))}
          </select>
        )}
      </div>
      <textarea required rows={3} placeholder={type === 'Quote' ? 'Quote text…' : 'Your note…'} value={text} onChange={(e) => setText(e.target.value)} />
      <ErrorMessage error={error} />
      <div className="row">
        <button type="submit" className="primary" disabled={pending || !text.trim()}>
          {submitLabel}
        </button>
        {onCancel && (
          <button type="button" onClick={onCancel}>
            Cancel
          </button>
        )}
      </div>
    </form>
  );
}
