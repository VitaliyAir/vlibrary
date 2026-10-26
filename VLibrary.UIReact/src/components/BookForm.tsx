import { useState, type FormEvent } from 'react';
import { useTags } from '../api/hooks';
import { languages, type BookAuthor, type BookDetails, type BookFields, type Language } from '../api/types';
import { splitList } from '../format';
import { ErrorMessage } from './common';

interface AuthorRow {
  lastName: string;
  /** First name or initials ("Лев" or "Л.Н."). */
  firstName: string;
  middleName: string;
}

const emptyAuthor: AuthorRow = { lastName: '', firstName: '', middleName: '' };

const authorRow = (a: BookAuthor): AuthorRow => ({
  lastName: a.lastName ?? a.name,
  firstName: a.firstName ?? a.initials ?? '',
  middleName: a.firstName ? (a.middleName ?? '') : '',
});

/** "Толстой, Лев Николаевич", "Толстой, Л.Н." or "Толстой": the comma form the server splits into the name parts. */
const authorText = (row: AuthorRow): string => {
  const rest = [row.firstName, row.middleName].map((s) => s.trim()).filter(Boolean).join(' ');
  return rest ? `${row.lastName.trim()}, ${rest}` : row.lastName.trim();
};

const optional = (value: string) => (value.trim() ? value.trim() : null);

/** Tags separated by commas, with suggestions for the last one. */
function TagsInput({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const lastTag = value.split(',').pop()?.trim() ?? '';
  const suggestions = useTags(lastTag);
  return (
    <label className="inline-field">
      <span className="field-caption">
        Tags <span className="muted">(comma-separated)</span>
      </span>
      <input list="tag-suggestions" value={value} onChange={(e) => onChange(e.target.value)} />
      <datalist id="tag-suggestions">
        {suggestions.data?.map((tag) => (
          <option key={tag.id} value={[...value.split(',').slice(0, -1).map((t) => t.trim()), tag.name].filter(Boolean).join(', ')} />
        ))}
      </datalist>
    </label>
  );
}

function LanguageSelect({ value, onChange }: { value: Language | ''; onChange: (value: Language | '') => void }) {
  return (
    <label>
      Language
      <select value={value} onChange={(e) => onChange(e.target.value as Language | '')}>
        <option value="">—</option>
        {languages.map((l) => (
          <option key={l} value={l}>
            {l}
          </option>
        ))}
      </select>
    </label>
  );
}

/** One row per author: last name, first name or initials, middle name; rows without a last name are not saved. */
function AuthorsEditor({ rows, onChange }: { rows: AuthorRow[]; onChange: (rows: AuthorRow[]) => void }) {
  const set = (index: number, field: keyof AuthorRow, value: string) =>
    onChange(rows.map((row, i) => (i === index ? { ...row, [field]: value } : row)));

  return (
    <fieldset className="authors-editor">
      <legend>Authors</legend>
      {rows.map((row, index) => (
        <div key={index} className="author-row">
          <input placeholder="Last name" aria-label="Last name" value={row.lastName} onChange={(e) => set(index, 'lastName', e.target.value)} />
          <input
            placeholder="First name or initials"
            aria-label="First name or initials"
            value={row.firstName}
            onChange={(e) => set(index, 'firstName', e.target.value)}
          />
          <input placeholder="Middle name" aria-label="Middle name" value={row.middleName} onChange={(e) => set(index, 'middleName', e.target.value)} />
          <button type="button" className="small" title="Remove this author" onClick={() => onChange(rows.filter((_, i) => i !== index))}>
            ×
          </button>
        </div>
      ))}
      <button type="button" className="small" onClick={() => onChange([...rows, emptyAuthor])}>
        + Add author
      </button>
    </fieldset>
  );
}

/** The main fields next to the cover, edited in place; the other fields are under "Add more details" (BookForm). */
export function BookQuickForm({
  book,
  onSubmit,
  pending,
  error,
}: {
  book: BookDetails;
  onSubmit: (fields: BookFields) => void;
  pending?: boolean;
  error?: unknown;
}) {
  const initial = () => ({
    title: book.title,
    authors: book.authors.length > 0 ? book.authors.map(authorRow) : [emptyAuthor],
    tags: book.tags.map((t) => t.name).join(', '),
    isbn: book.isbn ?? '',
    year: book.yearPublished?.toString() ?? '',
    pages: book.totalPages?.toString() ?? '',
    language: (book.language ?? '') as Language | '',
  });
  const [form, setForm] = useState(initial);
  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm({ ...form, [key]: value });
  const changed = JSON.stringify(form) !== JSON.stringify(initial());

  const submit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit({
      title: form.title.trim(),
      subtitle: book.subtitle,
      authors: form.authors.filter((a) => a.lastName.trim()).map(authorText),
      tags: splitList(form.tags, /[,;]/),
      isbn: optional(form.isbn),
      publisher: book.publisher,
      edition: book.edition,
      yearPublished: form.year ? Number(form.year) : null,
      totalPages: form.pages ? Number(form.pages) : null,
      language: form.language || null,
      description: book.description,
      tableOfContents: book.tableOfContents,
      glossary: book.glossary,
    });
  };

  return (
    <form className="book-form" onSubmit={submit}>
      <label>
        Title
        <input required maxLength={500} value={form.title} onChange={(e) => set('title', e.target.value)} />
      </label>
      <AuthorsEditor rows={form.authors} onChange={(rows) => set('authors', rows)} />
      <TagsInput value={form.tags} onChange={(value) => set('tags', value)} />
      <div className="grid-4">
        <label>
          ISBN
          <input value={form.isbn} onChange={(e) => set('isbn', e.target.value)} />
        </label>
        <label>
          Year
          <input type="number" min={1000} max={2100} value={form.year} onChange={(e) => set('year', e.target.value)} />
        </label>
        <label>
          Pages
          <input type="number" min={1} value={form.pages} onChange={(e) => set('pages', e.target.value)} />
        </label>
        <LanguageSelect value={form.language} onChange={(value) => set('language', value)} />
      </div>
      <ErrorMessage error={error} />
      <div className="row">
        <button type="submit" className="primary" disabled={pending || !changed || !form.title.trim()}>
          Save
        </button>
        {changed && (
          <button type="button" onClick={() => setForm(initial())}>
            Undo changes
          </button>
        )}
      </div>
    </form>
  );
}

/** UC-3: editable book card fields. Authors one per line, tags separated by commas. */
export function BookForm({
  book,
  submitLabel,
  onSubmit,
  onCancel,
  pending,
  error,
}: {
  book?: BookDetails;
  submitLabel: string;
  onSubmit: (fields: BookFields) => void;
  onCancel?: () => void;
  pending?: boolean;
  error?: unknown;
}) {
  const [title, setTitle] = useState(book?.title ?? '');
  const [subtitle, setSubtitle] = useState(book?.subtitle ?? '');
  const [authors, setAuthors] = useState(book?.authors.map((a) => authorText(authorRow(a))).join('\n') ?? '');
  const [tags, setTags] = useState(book?.tags.map((t) => t.name).join(', ') ?? '');
  const [isbn, setIsbn] = useState(book?.isbn ?? '');
  const [publisher, setPublisher] = useState(book?.publisher ?? '');
  const [edition, setEdition] = useState(book?.edition ?? '');
  const [year, setYear] = useState(book?.yearPublished?.toString() ?? '');
  const [pages, setPages] = useState(book?.totalPages?.toString() ?? '');
  const [language, setLanguage] = useState<Language | ''>(book?.language ?? '');
  const [description, setDescription] = useState(book?.description ?? '');
  const [tableOfContents, setTableOfContents] = useState(book?.tableOfContents ?? '');

  const submit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit({
      title: title.trim(),
      subtitle: optional(subtitle),
      authors: splitList(authors, /\n/),
      tags: splitList(tags, /[,;]/),
      isbn: optional(isbn),
      publisher: optional(publisher),
      edition: optional(edition),
      yearPublished: year ? Number(year) : null,
      totalPages: pages ? Number(pages) : null,
      language: language || null,
      description: optional(description),
      tableOfContents: optional(tableOfContents),
      glossary: book?.glossary ?? null,
    });
  };

  return (
    <form className="book-form" onSubmit={submit}>
      <label>
        Title
        <input required maxLength={500} value={title} onChange={(e) => setTitle(e.target.value)} />
      </label>
      <label>
        Subtitle
        <input maxLength={500} value={subtitle} onChange={(e) => setSubtitle(e.target.value)} />
      </label>
      <label>
        Authors <span className="muted">(one per line: “Толстой, Лев Николаевич”, “Tolstoy, L.N.”)</span>
        <textarea rows={2} value={authors} onChange={(e) => setAuthors(e.target.value)} />
      </label>
      <TagsInput value={tags} onChange={setTags} />
      <div className="grid-3">
        <label>
          ISBN
          <input value={isbn} onChange={(e) => setIsbn(e.target.value)} />
        </label>
        <label>
          Year
          <input type="number" min={1000} max={2100} value={year} onChange={(e) => setYear(e.target.value)} />
        </label>
        <label>
          Pages
          <input type="number" min={1} value={pages} onChange={(e) => setPages(e.target.value)} />
        </label>
        <label>
          Publisher
          <input maxLength={250} value={publisher} onChange={(e) => setPublisher(e.target.value)} />
        </label>
        <label>
          Edition
          <input maxLength={100} value={edition} onChange={(e) => setEdition(e.target.value)} />
        </label>
        <LanguageSelect value={language} onChange={setLanguage} />
      </div>
      <label>
        Description
        <textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
      </label>
      <label>
        Table of contents
        <textarea rows={3} value={tableOfContents} onChange={(e) => setTableOfContents(e.target.value)} />
      </label>
      <ErrorMessage error={error} />
      <div className="row">
        <button type="submit" className="primary" disabled={pending || !title.trim()}>
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
