import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useCategories, useCategoryMutations } from '../api/hooks';
import type { Category, CategoryChange } from '../api/types';
import { BackupPanel } from '../components/BackupPanel';
import { Empty, ErrorMessage, Loading } from '../components/common';
import { categoryOptions } from '../format';

type Mode = 'view' | 'rename' | 'move' | 'add';

/**
 * The library tree. Import creates it from your folders; here it can be corrected: rename, move with all
 * subcategories and files (a same-named category at the target is merged), add and delete. VStorage follows, and the
 * old names keep leading imports from the old folders to the right place.
 */
export function CategoriesPage() {
  const categories = useCategories();
  const { create } = useCategoryMutations();
  const [name, setName] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const list = categories.data ?? [];

  return (
    <section>
      <h1>Categories</h1>
      <p className="muted">
        Import creates categories from your folders. Rename them, move them with all subcategories and files, or add new
        ones; the files in VStorage get the same new places. Old names are remembered, so importing the old folder again
        puts new books into the right category.
      </p>
      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault();
          create.mutate({ name: name.trim(), parentId: null }, { onSuccess: () => setName('') });
        }}
      >
        <input required maxLength={200} placeholder="New top-level category" value={name} onChange={(e) => setName(e.target.value)} />
        <button type="submit" className="primary" disabled={!name.trim() || create.isPending}>
          Add
        </button>
      </form>
      {message && <p className="book-summary">{message}</p>}
      <ErrorMessage error={categories.error ?? create.error} />
      {categories.isLoading && <Loading />}
      {categories.data?.length === 0 && <Empty>No categories yet — the first import creates them.</Empty>}
      <ul className="category-tree">
        {list.map((category) => (
          <CategoryRow key={category.id} category={category} categories={list} onChanged={setMessage} />
        ))}
      </ul>
      <BackupPanel />
    </section>
  );
}

function CategoryRow({ category, categories, onChanged }: { category: Category; categories: Category[]; onChanged: (message: string) => void }) {
  const mutations = useCategoryMutations();
  const [mode, setMode] = useState<Mode>('view');
  const [text, setText] = useState('');
  const [parentId, setParentId] = useState('');
  const depth = category.path.split('/').length - 1;
  const hasChildren = categories.some((c) => c.parentId === category.id);

  // "Move to": the top level or any category outside this one's subtree.
  const targets = useMemo(
    () => categoryOptions(categories.filter((c) => c.id !== category.id && !c.path.startsWith(category.path + '/'))),
    [categories, category],
  );

  const save = (name: string, newParentId: number | null) => {
    const clash = categories.find(
      (c) => c.id !== category.id && c.parentId === newParentId && c.name.toLowerCase() === name.trim().toLowerCase(),
    );
    if (clash && !window.confirm(`“${clash.path}” already exists. Merge “${category.path}” into it with all subcategories and files?`)) return;
    mutations.update.mutate(
      { id: category.id, name: name.trim(), parentId: newParentId },
      {
        onSuccess: (change) => {
          setMode('view');
          onChanged(describe(change));
        },
      },
    );
  };

  return (
    <li style={{ paddingLeft: `${depth * 1.25}rem` }}>
      {mode === 'rename' ? (
        <form
          className="row"
          onSubmit={(e) => {
            e.preventDefault();
            save(text, category.parentId);
          }}
        >
          <input required maxLength={200} value={text} onChange={(e) => setText(e.target.value)} autoFocus />
          <button type="submit" disabled={mutations.update.isPending}>
            Save
          </button>
          <button type="button" onClick={() => setMode('view')}>
            Cancel
          </button>
        </form>
      ) : mode === 'move' ? (
        <form
          className="row"
          onSubmit={(e) => {
            e.preventDefault();
            save(category.name, parentId ? Number(parentId) : null);
          }}
        >
          <span>Move “{category.name}” to</span>
          <select value={parentId} onChange={(e) => setParentId(e.target.value)} autoFocus>
            <option value="">(top level)</option>
            {targets.map((t) => (
              <option key={t.id} value={t.id}>
                {t.label}
              </option>
            ))}
          </select>
          <button type="submit" disabled={mutations.update.isPending}>
            Move
          </button>
          <button type="button" onClick={() => setMode('view')}>
            Cancel
          </button>
        </form>
      ) : (
        <span className="tree-node">
          <Link to={`/?categoryId=${category.id}`}>{category.name}</Link>{' '}
          <span className="muted">
            ({category.files}
            {category.totalFiles !== category.files ? ` / ${category.totalFiles}` : ''})
          </span>{' '}
          <button
            className="link"
            onClick={() => {
              setText(category.name);
              setMode('rename');
            }}
          >
            Rename
          </button>
          <button
            className="link"
            onClick={() => {
              setParentId(category.parentId?.toString() ?? '');
              setMode('move');
            }}
          >
            Move
          </button>
          <button
            className="link"
            onClick={() => {
              setText('');
              setMode('add');
            }}
          >
            + Sub
          </button>
          {!hasChildren && category.totalFiles === 0 && (
            <button className="link danger" onClick={() => window.confirm(`Delete “${category.path}”?`) && mutations.remove.mutate(category.id)}>
              Delete
            </button>
          )}
        </span>
      )}
      {category.redirects.length > 0 && (
        <div className="muted">
          also imported as:{' '}
          {category.redirects.map((r) => (
            <span key={r.id}>
              {r.path} ({r.state.toLowerCase()}){' '}
              <button
                className="link danger"
                title="Forget this old name: an import from that folder will create a new category"
                onClick={() => mutations.remove.mutate(r.id)}
              >
                ×
              </button>{' '}
            </span>
          ))}
        </div>
      )}
      {mode === 'add' && (
        <form
          className="row"
          onSubmit={(e) => {
            e.preventDefault();
            mutations.create.mutate({ name: text.trim(), parentId: category.id }, { onSuccess: () => setMode('view') });
          }}
        >
          <input required maxLength={200} placeholder="Subcategory" value={text} onChange={(e) => setText(e.target.value)} autoFocus />
          <button type="submit" disabled={mutations.create.isPending}>
            Add
          </button>
          <button type="button" onClick={() => setMode('view')}>
            Cancel
          </button>
        </form>
      )}
      <ErrorMessage error={mutations.update.error ?? mutations.create.error ?? mutations.remove.error} />
    </li>
  );
}

function describe(change: CategoryChange): string {
  const missing = change.missingInStorage > 0 ? ` ${change.missingInStorage} file(s) are missing in VStorage.` : '';
  return `${change.merged ? 'Merged into' : 'Now'} “${change.path}”: ${change.filesMoved} file(s) got the new place in VStorage.${missing}`;
}
