import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useBooks, useCategories, useShelves, type BookSearch } from '../api/hooks';
import { fileFormats, readingStatuses, type BookSort, type FileFormat, type ReadingStatus } from '../api/types';
import { BookGrid } from '../components/BookGrid';
import { BookList } from '../components/BookList';
import { CategoryBreadcrumb, CategoryTree, categoryTrail } from '../components/CategoryTree';
import { ErrorMessage, Loading, Pagination } from '../components/common';
import { statusLabels } from '../format';

type CatalogView = 'list' | 'grid';
const ViewKey = 'vlibrary.catalogView';

/** The List / Grid choice is a per-browser convenience; storage may be unavailable (private mode). */
function loadView(): CatalogView {
  try {
    return localStorage.getItem(ViewKey) === 'grid' ? 'grid' : 'list';
  } catch {
    return 'list';
  }
}

function saveView(view: CatalogView) {
  try {
    localStorage.setItem(ViewKey, view);
  } catch {
    // not remembered, still switched
  }
}

const sorts: { value: BookSort; label: string }[] = [
  { value: 'Title', label: 'Title' },
  { value: 'Added', label: 'Recently added' },
  { value: 'LastOpened', label: 'Recently opened' },
  { value: 'Rating', label: 'Rating' },
  { value: 'Year', label: 'Year' },
];

/** UC-4: catalog search and filters. Filters live in the URL so a search can be bookmarked. */
export function CatalogPage() {
  const [params, setParams] = useSearchParams();
  const [text, setText] = useState(params.get('q') ?? '');
  const [view, setView] = useState<CatalogView>(loadView);

  const number = (key: string) => (params.get(key) ? Number(params.get(key)) : undefined);
  const search: BookSearch = {
    q: params.get('q') ?? undefined,
    status: (params.get('status') as ReadingStatus) || undefined,
    shelfId: number('shelfId'),
    onShelf: params.get('onShelf') === 'true' ? true : undefined,
    tag: params.get('tag') ?? undefined,
    format: (params.get('format') as FileFormat) || undefined,
    minRating: number('minRating'),
    authorId: number('authorId'),
    categoryId: number('categoryId'),
    sort: (params.get('sort') as BookSort) || 'Title',
    page: number('page') ?? 1,
    pageSize: 30,
  };

  const books = useBooks(search);
  const shelves = useShelves();
  const categories = useCategories();

  const update = (key: string, value: string | undefined) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    if (key !== 'page') next.delete('page');
    setParams(next, { replace: true });
  };

  // Debounced free-text search.
  useEffect(() => {
    const timer = setTimeout(() => {
      if ((params.get('q') ?? '') !== text.trim()) update('q', text.trim() || undefined);
    }, 350);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [text]);

  const switchView = (next: CatalogView) => {
    setView(next);
    saveView(next);
  };
  const selectCategory = (id: number | undefined) => update('categoryId', id?.toString());
  const trail = categoryTrail(categories.data ?? [], search.categoryId);

  return (
    <section>
      <header className="page-header">
        <h1>Catalog</h1>
        <div className="row">
          <div className="view-toggle" role="group" aria-label="View">
            <button type="button" className={view === 'list' ? 'active' : ''} onClick={() => switchView('list')}>
              List
            </button>
            <button type="button" className={view === 'grid' ? 'active' : ''} onClick={() => switchView('grid')}>
              Grid
            </button>
          </div>
          <Link to="/books/new" className="button">
            + Add book
          </Link>
        </div>
      </header>

      <div className="catalog-layout">
        <aside className="catalog-tree">
          <details open>
            <summary>Categories</summary>
            <CategoryTree categories={categories.data ?? []} selectedId={search.categoryId} onSelect={selectCategory} />
          </details>
        </aside>

        <div className="catalog-main">
          <input
            className="search"
            type="search"
            placeholder="Search title, author, ISBN, tags, notes…"
            value={text}
            onChange={(e) => setText(e.target.value)}
            autoFocus
          />

          <div className="filters">
            <select value={search.status ?? ''} onChange={(e) => update('status', e.target.value)} aria-label="Status">
              <option value="">Any status</option>
              {readingStatuses.map((s) => (
                <option key={s} value={s}>
                  {statusLabels[s]}
                </option>
              ))}
            </select>
            <select value={search.shelfId ?? ''} onChange={(e) => update('shelfId', e.target.value)} aria-label="Shelf">
              <option value="">Any shelf</option>
              {shelves.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
            <label className="check">
              <input type="checkbox" checked={search.onShelf === true} onChange={(e) => update('onShelf', e.target.checked ? 'true' : undefined)} />
              On shelves
            </label>
            <select value={search.format ?? ''} onChange={(e) => update('format', e.target.value)} aria-label="Format">
              <option value="">Any format</option>
              {fileFormats.map((f) => (
                <option key={f} value={f}>
                  {f}
                </option>
              ))}
            </select>
            <select value={search.minRating ?? ''} onChange={(e) => update('minRating', e.target.value)} aria-label="Minimum rating">
              <option value="">Any rating</option>
              {[5, 6, 7, 8, 9, 10].map((r) => (
                <option key={r} value={r}>
                  ★ {r}+
                </option>
              ))}
            </select>
            <input placeholder="Tag" defaultValue={search.tag} onBlur={(e) => update('tag', e.target.value.trim())} className="narrow" />
            <select value={search.sort} onChange={(e) => update('sort', e.target.value)} aria-label="Sort">
              {sorts.map((s) => (
                <option key={s.value} value={s.value}>
                  Sort: {s.label}
                </option>
              ))}
            </select>
            {(search.authorId || search.tag) && (
              <button className="link" onClick={() => setParams({}, { replace: true })}>
                Clear filters
              </button>
            )}
          </div>

          <CategoryBreadcrumb trail={trail} onSelect={selectCategory} />
          <ErrorMessage error={books.error} />
          {books.isLoading ? (
            <Loading />
          ) : (
            <>
              {view === 'grid' ? (
                <BookGrid books={books.data?.items ?? []} empty="No books found. Import a folder to fill the catalog." />
              ) : (
                <BookList books={books.data?.items ?? []} empty="No books found. Import a folder to fill the catalog." />
              )}
              <Pagination page={search.page ?? 1} pageSize={30} total={books.data?.totalCount ?? 0} onChange={(p) => update('page', String(p))} />
            </>
          )}
        </div>
      </div>
    </section>
  );
}
