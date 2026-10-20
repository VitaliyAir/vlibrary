// TanStack Query hooks per API resource.
import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { api, withQuery } from './client';
import type {
  Author,
  AuthorFields,
  BookDetails,
  BookFields,
  BookListItem,
  BookSort,
  Category,
  ClassifierNode,
  CoverStatus,
  GenerateCoversResult,
  DuplicateReview,
  FileFormat,
  ImportBatch,
  ImportBatchDetails,
  ImportFileResult,
  ImportOptions,
  ImportOutcome,
  LibraryCheckResult,
  Note,
  NoteFields,
  OpenedFile,
  PagedList,
  PrecheckFile,
  PrecheckResult,
  ReadingRoom,
  ReadingStatus,
  Reminder,
  ReviewAnswer,
  Settings,
  Shelf,
  ShelfDetails,
  Tag,
} from './types';

export interface BookSearch {
  q?: string;
  shelfId?: number;
  onShelf?: boolean;
  status?: ReadingStatus;
  tag?: string;
  format?: FileFormat;
  minRating?: number;
  classifierId?: number;
  authorId?: number;
  categoryId?: number;
  sort?: BookSort;
  page?: number;
  pageSize?: number;
}

const keys = {
  books: ['books'] as const,
  book: (id: number) => ['books', id] as const,
  notes: ['notes'] as const,
  shelves: ['shelves'] as const,
  imports: ['imports'] as const,
  duplicates: ['duplicates'] as const,
  authors: ['authors'] as const,
  classifiers: ['classifiers'] as const,
  readingRoom: ['reading-room'] as const,
  settings: ['settings'] as const,
  categories: ['categories'] as const,
};

/** Most changes affect several views (lists, card, reading room), so everything book-related is refreshed. */
function useInvalidateLibrary() {
  const client = useQueryClient();
  return () =>
    Promise.all(
      [keys.books, keys.notes, keys.shelves, keys.readingRoom, keys.duplicates, keys.authors, keys.classifiers, keys.categories].map((queryKey) =>
        client.invalidateQueries({ queryKey }),
      ),
    );
}

// Books

export const useBooks = (search: BookSearch) =>
  useQuery({
    queryKey: [...keys.books, 'search', search],
    queryFn: () => api.get<PagedList<BookListItem>>(withQuery('/books', { ...search })),
    placeholderData: keepPreviousData,
  });

export const useBook = (id: number) =>
  useQuery({ queryKey: keys.book(id), queryFn: () => api.get<BookDetails>(`/books/${id}`), enabled: id > 0 });

export function useBookMutations(id: number) {
  const invalidate = useInvalidateLibrary();
  const options = { onSuccess: invalidate };
  return {
    update: useMutation({ mutationFn: (fields: BookFields) => api.put(`/books/${id}`, fields), ...options }),
    remove: useMutation({ mutationFn: () => api.delete(`/books/${id}`), ...options }),
    setRating: useMutation({ mutationFn: (rating: number | null) => api.put(`/books/${id}/rating`, { rating }), ...options }),
    setStatus: useMutation({ mutationFn: (status: ReadingStatus) => api.put(`/books/${id}/status`, { status }), ...options }),
    addToShelf: useMutation({ mutationFn: (shelfId: number) => api.post(`/shelves/${shelfId}/books/${id}`), ...options }),
    removeFromShelf: useMutation({ mutationFn: (shelfId: number) => api.delete(`/shelves/${shelfId}/books/${id}`), ...options }),
    setPrimaryFile: useMutation({ mutationFn: (fileId: number) => api.post(`/books/${id}/files/${fileId}/primary`), ...options }),
    detachFile: useMutation({ mutationFn: (fileId: number) => api.post(`/books/${id}/files/${fileId}/detach`), ...options }),
  };
}

export function useCreateBook() {
  const invalidate = useInvalidateLibrary();
  return useMutation({
    mutationFn: (fields: BookFields) => api.post<{ id: number }>('/books', fields),
    onSuccess: invalidate,
  });
}

// Files (UC-5: the API returns a short-lived VStorage link and records the opening)

export function useOpenFile() {
  const invalidate = useInvalidateLibrary();
  return useMutation({
    mutationFn: (fileId: number) => api.post<OpenedFile>(`/files/${fileId}/opened`),
    // Also on error: a file VStorage no longer has is marked Missing by the API.
    onSettled: invalidate,
  });
}

// Notes

export const useBookNotes = (bookId: number) =>
  useQuery({ queryKey: [...keys.notes, 'book', bookId], queryFn: () => api.get<Note[]>(`/books/${bookId}/notes`), enabled: bookId > 0 });

export const useNoteSearch = (q: string, page: number) =>
  useQuery({
    queryKey: [...keys.notes, 'search', q, page],
    queryFn: () => api.get<PagedList<Note>>(withQuery('/notes/search', { q, page, pageSize: 30 })),
    placeholderData: keepPreviousData,
  });

export function useNoteMutations() {
  const invalidate = useInvalidateLibrary();
  const options = { onSuccess: invalidate };
  return {
    create: useMutation({
      mutationFn: ({ bookId, fields }: { bookId: number; fields: NoteFields }) => api.post<{ id: number }>(`/books/${bookId}/notes`, fields),
      ...options,
    }),
    update: useMutation({ mutationFn: ({ id, fields }: { id: number; fields: NoteFields }) => api.put(`/notes/${id}`, fields), ...options }),
    remove: useMutation({ mutationFn: (id: number) => api.delete(`/notes/${id}`), ...options }),
  };
}

// Reminders and settings

export const useReminders = (enabled: boolean) =>
  useQuery({ queryKey: ['reminders'], queryFn: () => api.get<Reminder[]>('/reminders/next'), enabled, staleTime: Infinity });

export const useReviewNote = () =>
  useMutation({ mutationFn: ({ noteId, answer }: { noteId: number; answer: ReviewAnswer }) => api.post(`/reminders/${noteId}/review`, { answer }) });

export const useSettings = () => useQuery({ queryKey: keys.settings, queryFn: () => api.get<Settings>('/settings') });

export function useUpdateSettings() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (settings: Settings) => api.put('/settings', settings),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.settings }),
  });
}

// Reading room

export const useReadingRoom = () => useQuery({ queryKey: keys.readingRoom, queryFn: () => api.get<ReadingRoom>('/reading-room') });

// Shelves

export const useShelves = () => useQuery({ queryKey: keys.shelves, queryFn: () => api.get<Shelf[]>('/shelves') });

export const useShelf = (id: number | null) =>
  useQuery({ queryKey: [...keys.shelves, id], queryFn: () => api.get<ShelfDetails>(`/shelves/${id}`), enabled: id != null });

export function useShelfMutations() {
  const invalidate = useInvalidateLibrary();
  const options = { onSuccess: invalidate };
  return {
    create: useMutation({ mutationFn: (body: { name: string; description: string | null }) => api.post<{ id: number }>('/shelves', body), ...options }),
    update: useMutation({
      mutationFn: ({ id, ...body }: { id: number; name: string; description: string | null }) => api.put(`/shelves/${id}`, body),
      ...options,
    }),
    remove: useMutation({ mutationFn: (id: number) => api.delete(`/shelves/${id}`), ...options }),
    reorder: useMutation({ mutationFn: (ids: number[]) => api.put('/shelves/order', { ids }), ...options }),
    reorderBooks: useMutation({ mutationFn: ({ id, ids }: { id: number; ids: number[] }) => api.put(`/shelves/${id}/books/order`, { ids }), ...options }),
    removeBook: useMutation({ mutationFn: ({ id, bookId }: { id: number; bookId: number }) => api.delete(`/shelves/${id}/books/${bookId}`), ...options }),
  };
}

// Imports and duplicates

export const useImports = () =>
  useQuery({
    queryKey: keys.imports,
    queryFn: () => api.get<ImportBatch[]>('/imports'),
    refetchInterval: (query) => (query.state.data?.some((b) => b.status === 'Running') ? 2000 : false),
  });

export const useImportOptions = () =>
  useQuery({ queryKey: [...keys.imports, 'options'], queryFn: () => api.get<ImportOptions>('/imports/options'), staleTime: Infinity });

export const useImport = (id: number | null, outcome: ImportOutcome | '', page: number) =>
  useQuery({
    queryKey: [...keys.imports, id, outcome, page],
    queryFn: () => api.get<ImportBatchDetails>(withQuery(`/imports/${id}`, { outcome, page, pageSize: 50 })),
    enabled: id != null,
    refetchInterval: (query) => {
      return query.state.data?.batch.status === 'Running' ? 2000 : false;
    },
  });

/**
 * Browser-driven import (UC-1): create -> precheck -> one upload per file -> complete. Plain async calls
 * (not mutations), because the upload loop on the import page sequences them itself.
 */
export const importApi = {
  create: (body: { sourceName: string; totalFiles: number; parentCategoryId: number | null }) =>
    api.post<{ id: number }>('/imports', body),
  precheck: (id: number, files: PrecheckFile[]) => api.post<PrecheckResult>(`/imports/${id}/precheck`, { files }),
  uploadFile: (id: number, relativePath: string, file: File) => {
    const form = new FormData();
    form.append('relativePath', relativePath);
    form.append('lastModified', String(file.lastModified));
    form.append('file', file, file.name);
    return api.upload<ImportFileResult>(`/imports/${id}/files`, form);
  },
  complete: (id: number) => api.post<ImportBatch>(`/imports/${id}/complete`),
  cancel: (id: number) => api.post(`/imports/${id}/cancel`),
};

/** Refreshes the import list and everything an import changes. */
export function useImportsChanged() {
  const client = useQueryClient();
  const invalidate = useInvalidateLibrary();
  return () => Promise.all([client.invalidateQueries({ queryKey: keys.imports }), invalidate()]);
}

export function useCancelImport() {
  const changed = useImportsChanged();
  return useMutation({ mutationFn: (id: number) => importApi.cancel(id), onSuccess: changed });
}

export const useDuplicates = () => useQuery({ queryKey: keys.duplicates, queryFn: () => api.get<DuplicateReview[]>('/duplicates') });

export function useDuplicateMutations() {
  const invalidate = useInvalidateLibrary();
  const options = { onSuccess: invalidate };
  return {
    link: useMutation({ mutationFn: ({ fileId, bookId }: { fileId: number; bookId: number }) => api.post(`/duplicates/${fileId}/link`, { bookId }), ...options }),
    createBook: useMutation({ mutationFn: (fileId: number) => api.post<{ id: number }>(`/duplicates/${fileId}/create-book`), ...options }),
    remove: useMutation({ mutationFn: (fileId: number) => api.delete(`/duplicates/${fileId}`), ...options }),
  };
}

// Catalog: authors, tags, classifiers

export const useAuthors = (q: string, page: number) =>
  useQuery({
    queryKey: [...keys.authors, q, page],
    queryFn: () => api.get<PagedList<Author>>(withQuery('/authors', { q, page, pageSize: 50 })),
    placeholderData: keepPreviousData,
  });

export function useAuthorMutations() {
  const invalidate = useInvalidateLibrary();
  const options = { onSuccess: invalidate };
  return {
    update: useMutation({ mutationFn: ({ id, fields }: { id: number; fields: AuthorFields }) => api.put(`/authors/${id}`, fields), ...options }),
    remove: useMutation({ mutationFn: (id: number) => api.delete(`/authors/${id}`), ...options }),
  };
}

export const useTags = (q: string) =>
  useQuery({ queryKey: ['tags', q], queryFn: () => api.get<Tag[]>(withQuery('/tags', { q, take: 30 })), staleTime: 30_000 });

export const useClassifiers = () => useQuery({ queryKey: keys.classifiers, queryFn: () => api.get<ClassifierNode[]>('/classifiers') });

export function useClassifierMutations() {
  const invalidate = useInvalidateLibrary();
  const options = { onSuccess: invalidate };
  return {
    create: useMutation({ mutationFn: (body: { title: string; parentId: number | null }) => api.post<{ id: number }>('/classifiers', body), ...options }),
    update: useMutation({
      mutationFn: ({ id, ...body }: { id: number; title: string; parentId: number | null }) => api.put(`/classifiers/${id}`, body),
      ...options,
    }),
    remove: useMutation({ mutationFn: (id: number) => api.delete(`/classifiers/${id}`), ...options }),
  };
}

// Categories and file availability

export const useCategories = () => useQuery({ queryKey: keys.categories, queryFn: () => api.get<Category[]>('/categories') });

export function useCheckLibrary() {
  const invalidate = useInvalidateLibrary();
  return useMutation({ mutationFn: () => api.post<LibraryCheckResult>('/library/check'), onSuccess: invalidate });
}

// Covers (B1)

/**
 * Object URL of a cover image, loaded with the token and cached per file + version (a new cover has a new version).
 * Null while loading or when there is no cover.
 */
export function useCoverUrl(fileId: number | null | undefined, version: number | null | undefined): string | null {
  const image = useQuery({
    queryKey: ['cover', fileId, version],
    queryFn: () => api.blob(`/files/${fileId}/cover?v=${version}`),
    enabled: fileId != null && version != null,
    staleTime: Infinity,
    gcTime: 10 * 60_000,
  });
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!image.data) {
      setUrl(null);
      return;
    }
    const objectUrl = URL.createObjectURL(image.data);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [image.data]);

  return url;
}

const coverStatusKey = ['covers', 'status'] as const;

export const useCoverStatus = () => useQuery({ queryKey: coverStatusKey, queryFn: () => api.get<CoverStatus>('/covers/status') });

/** "Generate missing covers": plain calls, the Library panel repeats them until nothing remains. */
export const coverApi = {
  generate: (take: number) => api.post<GenerateCoversResult>(withQuery('/covers/generate', { take })),
};

/** Refreshes the cover counts and every book list (new covers appear). */
export function useCoversChanged() {
  const client = useQueryClient();
  const invalidate = useInvalidateLibrary();
  return () => Promise.all([client.invalidateQueries({ queryKey: coverStatusKey }), invalidate()]);
}

export function useResetFailedCovers() {
  const changed = useCoversChanged();
  return useMutation({ mutationFn: () => api.post<number>('/covers/reset-failed'), onSuccess: changed });
}
