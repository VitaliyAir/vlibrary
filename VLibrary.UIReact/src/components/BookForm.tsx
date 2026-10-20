import { useState, type FormEvent } from 'react';
import { useClassifiers, useTags } from '../api/hooks';
import { languages, type BookDetails, type BookFields, type Language } from '../api/types';
import { flattenClassifiers, splitList } from '../format';
import { ErrorMessage } from './common';

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
  const [authors, setAuthors] = useState(book?.authors.map((a) => a.name).join('\n') ?? '');
  const [tags, setTags] = useState(book?.tags.map((t) => t.name).join(', ') ?? '');
  const [isbn, setIsbn] = useState(book?.isbn ?? '');
  const [publisher, setPublisher] = useState(book?.publisher ?? '');
  const [edition, setEdition] = useState(book?.edition ?? '');
  const [year, setYear] = useState(book?.yearPublished?.toString() ?? '');
  const [pages, setPages] = useState(book?.totalPages?.toString() ?? '');
  const [language, setLanguage] = useState<Language | ''>(book?.language ?? '');
  const [classifierId, setClassifierId] = useState(book?.classifier?.id.toString() ?? '');
  const [description, setDescription] = useState(book?.description ?? '');
  const [tableOfContents, setTableOfContents] = useState(book?.tableOfContents ?? '');

  const classifiers = useClassifiers();
  const lastTag = tags.split(',').pop()?.trim() ?? '';
  const tagSuggestions = useTags(lastTag);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const empty = (value: string) => (value.trim() ? value.trim() : null);
    onSubmit({
      title: title.trim(),
      subtitle: empty(subtitle),
      authors: splitList(authors, /\n/),
      tags: splitList(tags, /[,;]/),
      isbn: empty(isbn),
      publisher: empty(publisher),
      edition: empty(edition),
      yearPublished: year ? Number(year) : null,
      totalPages: pages ? Number(pages) : null,
      language: language || null,
      classifierId: classifierId ? Number(classifierId) : null,
      description: empty(description),
      tableOfContents: empty(tableOfContents),
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
        Authors <span className="muted">(one per line: “Толстой Л.Н.”, “Leo Tolstoy”)</span>
        <textarea rows={2} value={authors} onChange={(e) => setAuthors(e.target.value)} />
      </label>
      <label>
        Tags <span className="muted">(comma-separated)</span>
        <input list="tag-suggestions" value={tags} onChange={(e) => setTags(e.target.value)} />
        <datalist id="tag-suggestions">
          {tagSuggestions.data?.map((tag) => (
            <option key={tag.id} value={[...tags.split(',').slice(0, -1).map((t) => t.trim()), tag.name].filter(Boolean).join(', ')} />
          ))}
        </datalist>
      </label>
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
        <label>
          Language
          <select value={language} onChange={(e) => setLanguage(e.target.value as Language | '')}>
            <option value="">—</option>
            {languages.map((l) => (
              <option key={l} value={l}>
                {l}
              </option>
            ))}
          </select>
        </label>
      </div>
      <label>
        Classification
        <select value={classifierId} onChange={(e) => setClassifierId(e.target.value)}>
          <option value="">—</option>
          {flattenClassifiers(classifiers.data ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.label}
            </option>
          ))}
        </select>
      </label>
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
