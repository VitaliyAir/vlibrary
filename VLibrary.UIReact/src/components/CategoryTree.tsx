import { Fragment, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import type { Category } from '../api/types';

interface TreeNode {
  category: Category;
  children: TreeNode[];
}

/** Categories come flat and ordered by path; the tree is built from parentId. */
function buildTree(categories: Category[]): TreeNode[] {
  const nodes = new Map(categories.map((c) => [c.id, { category: c, children: [] as TreeNode[] }]));
  const roots: TreeNode[] = [];
  for (const node of nodes.values()) {
    const parent = node.category.parentId != null ? nodes.get(node.category.parentId) : undefined;
    (parent ? parent.children : roots).push(node);
  }
  return roots;
}

/** The chain from the top level down to the category, e.g. [Library, Math]; empty when unknown. */
export function categoryTrail(categories: Category[], id: number | null | undefined): Category[] {
  const byId = new Map(categories.map((c) => [c.id, c]));
  const trail: Category[] = [];
  for (let current = id != null ? byId.get(id) : undefined; current; current = current.parentId != null ? byId.get(current.parentId) : undefined) {
    trail.unshift(current);
  }
  return trail;
}

/**
 * "All books › Library › Math". With onSelect the parts are buttons (the catalog keeps its other filters);
 * without it they are links to the catalog filtered by that category.
 */
export function CategoryBreadcrumb({ trail, onSelect }: { trail: Category[]; onSelect?: (id: number | undefined) => void }) {
  if (trail.length === 0) return null;
  return (
    <nav className="breadcrumb" aria-label="Category">
      {onSelect && (
        <>
          <button type="button" className="link" onClick={() => onSelect(undefined)}>
            All books
          </button>
          <span className="sep">›</span>
        </>
      )}
      {trail.map((c, i) => (
        <Fragment key={c.id}>
          {i > 0 && <span className="sep">›</span>}
          {onSelect ? (
            <button type="button" className="link" onClick={() => onSelect(c.id)}>
              {c.name}
            </button>
          ) : (
            <Link to={`/?categoryId=${c.id}`}>{c.name}</Link>
          )}
        </Fragment>
      ))}
    </nav>
  );
}

/** Collapsible category tree for the catalog; the selected category and its parents are expanded. */
export function CategoryTree({
  categories,
  selectedId,
  onSelect,
}: {
  categories: Category[];
  selectedId?: number;
  onSelect: (id: number | undefined) => void;
}) {
  const roots = useMemo(() => buildTree(categories), [categories]);
  const [open, setOpen] = useState<Set<number>>(new Set());

  // Keep the path to the selected category visible (also when it was chosen elsewhere, e.g. from a book card).
  useEffect(() => {
    const trail = categoryTrail(categories, selectedId);
    if (trail.length > 1) {
      setOpen((current) => new Set([...current, ...trail.slice(0, -1).map((c) => c.id)]));
    }
  }, [categories, selectedId]);

  const toggle = (id: number) =>
    setOpen((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const renderNodes = (nodes: TreeNode[]) => (
    <ul>
      {nodes.map(({ category, children }) => (
        <li key={category.id}>
          <div className="tree-item">
            {children.length > 0 ? (
              <button
                type="button"
                className="tree-toggle"
                aria-label={open.has(category.id) ? 'Collapse' : 'Expand'}
                onClick={() => toggle(category.id)}
              >
                {open.has(category.id) ? '▾' : '▸'}
              </button>
            ) : (
              <span className="tree-toggle" />
            )}
            <button
              type="button"
              className={`tree-name ${category.id === selectedId ? 'active' : ''}`}
              onClick={() => onSelect(category.id)}
            >
              {category.name}
            </button>
            <span className="tree-count">{category.totalFiles}</span>
          </div>
          {children.length > 0 && open.has(category.id) && renderNodes(children)}
        </li>
      ))}
    </ul>
  );

  return (
    <div className="category-tree-list">
      <div className="tree-item">
        <span className="tree-toggle" />
        <button type="button" className={`tree-name ${selectedId == null ? 'active' : ''}`} onClick={() => onSelect(undefined)}>
          All books
        </button>
      </div>
      {roots.length > 0 ? renderNodes(roots) : <div className="muted tree-empty">No categories yet.</div>}
    </div>
  );
}
