import type { BookFilesState, Category, ClassifierNode, ImportOutcome, ReadingStatus } from './api/types';

export const statusLabels: Record<ReadingStatus, string> = {
  NotStarted: 'Not started',
  InProgress: 'In progress',
  Read: 'Read',
  Abandoned: 'Abandoned',
};

export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unit]}`;
}

export function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleString() : '—';
}

/** "a, b; c" -> ["a", "b", "c"] for tags; authors are one per line. */
export const splitList = (text: string, separators: RegExp): string[] =>
  text
    .split(separators)
    .map((s) => s.trim())
    .filter(Boolean);

/** Classifier tree flattened for <select>, with indentation by depth. */
export function flattenClassifiers(nodes: ClassifierNode[], depth = 0): { id: number; label: string }[] {
  return nodes.flatMap((node) => [
    { id: node.id, label: `${'  '.repeat(depth)}${node.title}` },
    ...flattenClassifiers(node.children, depth + 1),
  ]);
}

/** Categories (already ordered by path) for <select>, indented by depth. */
export function categoryOptions(categories: Category[]): { id: number; label: string }[] {
  return categories.map((c) => ({ id: c.id, label: `${'  '.repeat(c.path.split('/').length - 1)}${c.name}` }));
}

/** UI colour class and hint for a book's file availability (Stage 6a, D7). */
export const filesStateInfo: Record<BookFilesState, { className: string; label: string }> = {
  Ok: { className: 'files-ok', label: '' },
  Partial: { className: 'files-partial', label: 'Some files are missing' },
  Missing: { className: 'files-missing', label: 'No file can be opened' },
  NoFiles: { className: 'files-none', label: 'No files' },
};

export const outcomeLabels: Record<ImportOutcome, string> = {
  Imported: 'Imported',
  PendingReview: 'Waiting for review',
  AlreadyInLibrary: 'Already in library',
  Conflict: 'Conflict',
  DeletedDuplicateSkipped: 'Duplicate of deleted book',
  Failed: 'Failed',
};

/** Outcomes shown as warnings in the import report. */
export const warningOutcomes: ImportOutcome[] = ['Conflict', 'DeletedDuplicateSkipped', 'Failed'];
