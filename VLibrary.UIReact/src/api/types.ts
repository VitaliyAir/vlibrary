// DTOs of VLibrary.Api (JSON is camelCase, enums are strings).

export type ReadingStatus = 'NotStarted' | 'InProgress' | 'Read' | 'Abandoned';
export type FileFormat = 'PDF' | 'DJVU' | 'DJV' | 'DOC' | 'DOCX' | 'EPUB' | 'TXT';
export type FileStatus = 'Ok' | 'Missing';
export type BookFilesState = 'NoFiles' | 'Ok' | 'Partial' | 'Missing';
export type NoteType = 'Note' | 'Quote';
export type Language = 'Russian' | 'English' | 'French';
export type ImportStatus = 'Running' | 'Completed' | 'Cancelled' | 'Failed';
export type ImportOutcome = 'Imported' | 'PendingReview' | 'AlreadyInLibrary' | 'Conflict' | 'DeletedDuplicateSkipped' | 'Failed';
export type DuplicateReason = 'IsbnMatch' | 'TitleMatch' | 'TitleWordsMatch' | 'ContentTitleMatch' | 'SizeMatch';
export type BookSort = 'Title' | 'Added' | 'LastOpened' | 'Rating' | 'Year';
export type ReviewAnswer = 'Remember' | 'Forgot' | 'Skip';

export const readingStatuses: ReadingStatus[] = ['NotStarted', 'InProgress', 'Read', 'Abandoned'];
export const fileFormats: FileFormat[] = ['PDF', 'EPUB', 'DJVU', 'DJV', 'DOC', 'DOCX', 'TXT'];
export const languages: Language[] = ['Russian', 'English', 'French'];

export interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface NamedRef {
  id: number;
  name: string;
}

/** An author of a book with the name parts (the author rows of the book card). */
export interface BookAuthor extends NamedRef {
  lastName: string | null;
  firstName: string | null;
  middleName: string | null;
  initials: string | null;
}

export interface BookFile {
  id: number;
  fileName: string;
  categoryId: number;
  /** Category path + file name, e.g. "Library/Math/Geometry.pdf". */
  libraryPath: string;
  format: FileFormat;
  sizeBytes: number;
  pageCount: number | null;
  status: FileStatus;
  isPrimary: boolean;
  /** Set when the file has a cover: GET /files/{id}/cover?v={coverVersion}. */
  coverVersion: number | null;
}

export interface BookListItem {
  id: number;
  title: string;
  subtitle: string | null;
  authors: string[];
  yearPublished: number | null;
  rating: number | null;
  readingStatus: ReadingStatus;
  lastOpenedAt: string | null;
  createdAt: string;
  tags: string[];
  files: BookFile[];
  filesState: BookFilesState;
  notesCount: number;
  cover: CoverRef | null;
}

export interface BookDetails {
  id: number;
  title: string;
  subtitle: string | null;
  description: string | null;
  isbn: string | null;
  edition: string | null;
  publisher: string | null;
  yearPublished: number | null;
  totalPages: number | null;
  language: Language | null;
  tableOfContents: string | null;
  glossary: string | null;
  rating: number | null;
  readingStatus: ReadingStatus;
  lastOpenedAt: string | null;
  createdAt: string;
  updatedAt: string;
  authors: BookAuthor[];
  tags: NamedRef[];
  files: BookFile[];
  shelves: NamedRef[];
  filesState: BookFilesState;
  notesCount: number;
  cover: CoverRef | null;
}

export interface BookFields {
  title: string;
  subtitle?: string | null;
  description?: string | null;
  isbn?: string | null;
  edition?: string | null;
  publisher?: string | null;
  yearPublished?: number | null;
  totalPages?: number | null;
  language?: Language | null;
  tableOfContents?: string | null;
  glossary?: string | null;
  authors?: string[] | null;
  tags?: string[] | null;
}

export interface Author {
  id: number;
  displayName: string;
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
  nameInitials: string | null;
  booksCount: number;
}

export interface AuthorFields {
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
  nameInitials: string | null;
  biography: string | null;
}

export interface Tag {
  id: number;
  name: string;
  booksCount: number;
}

export interface Shelf {
  id: number;
  name: string;
  description: string | null;
  sortOrder: number;
  booksCount: number;
}

export interface ShelfDetails {
  id: number;
  name: string;
  description: string | null;
  sortOrder: number;
  books: BookListItem[];
}

export interface ImportBatch {
  id: number;
  sourceName: string;
  parentCategoryId: number | null;
  parentCategoryPath: string | null;
  status: ImportStatus;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  error: string | null;
  totalFiles: number;
  processedFiles: number;
  unchangedCount: number;
  importedCount: number;
  alreadyInLibraryCount: number;
  conflictCount: number;
  deletedDuplicateCount: number;
  knownDeletedCount: number;
  pendingReviewCount: number;
  failedCount: number;
}

/** What the upload picker accepts (GET /imports/options). */
export interface ImportOptions {
  supportedExtensions: string[];
  excludedFolders: string[];
  maxFileBytes: number;
}

/** Result of one uploaded file; outcome null = unchanged (only counted). */
export interface ImportFileResult {
  path: string;
  outcome: ImportOutcome | null;
  message: string | null;
  fileMetaId: number | null;
  bookId: number | null;
  batch: ImportBatch;
}

export interface PrecheckFile {
  relativePath: string;
  size: number;
  /** Unix milliseconds (File.lastModified). */
  lastModified: number;
}

export interface PrecheckResult {
  skip: string[];
  batch: ImportBatch;
}

export type CategoryRedirectState = 'Renamed' | 'Moved' | 'Merged';

/** An old name and place of a category; imports from that folder go to the category. */
export interface CategoryRedirect {
  id: number;
  path: string;
  state: CategoryRedirectState;
}

export interface Category {
  id: number;
  name: string;
  parentId: number | null;
  path: string;
  files: number;
  totalFiles: number;
  redirects: CategoryRedirect[];
}

export interface BackupFile {
  id: number;
  categoryPath: string;
  fileName: string;
  sizeBytes: number;
}

export interface BackupManifest {
  files: BackupFile[];
  missing: number;
}

export interface CategoryChange {
  categoryId: number;
  path: string;
  filesMoved: number;
  merged: boolean;
  missingInStorage: number;
}

export interface ImportItem {
  id: number;
  path: string;
  outcome: ImportOutcome;
  message: string | null;
  fileMetaId: number | null;
  bookId: number | null;
}

export interface ImportBatchDetails {
  batch: ImportBatch;
  items: PagedList<ImportItem>;
}

export interface ExtractedMetadata {
  title: string | null;
  authors: string[];
  isbn: string | null;
  year: number | null;
  publisher: string | null;
  source: string;
}

export interface DuplicateCandidate {
  bookId: number;
  title: string;
  authors: string[];
  isbn: string | null;
  yearPublished: number | null;
  filesCount: number;
  score: number;
  reason: DuplicateReason;
}

export interface DuplicateReview {
  fileMetaId: number;
  libraryPath: string;
  fileName: string;
  format: FileFormat;
  sizeBytes: number;
  importedAt: string;
  metadata: ExtractedMetadata | null;
  candidates: DuplicateCandidate[];
  coverVersion: number | null;
}

export interface Note {
  id: number;
  bookId: number;
  bookTitle: string;
  fileMetaId: number | null;
  fileName: string | null;
  type: NoteType;
  text: string;
  pageNumber: number | null;
  createdAt: string;
  updatedAt: string;
  reviewBox: number;
  nextReviewAt: string;
  lastReviewedAt: string | null;
}

export interface NoteFields {
  type: NoteType;
  text: string;
  pageNumber?: number | null;
  fileMetaId?: number | null;
}

export interface Reminder {
  note: Note;
  authors: string[];
}

export interface ReadingRoom {
  recentlyOpened: BookListItem[];
  inProgress: BookListItem[];
}

export interface Settings {
  remindersEnabled: boolean;
  remindersPerStartup: number;
}

/** A short-lived VStorage link; the browser opens it directly. */
export interface OpenedFile {
  fileId: number;
  url: string;
  expiresAt: string;
  bookId: number | null;
}

/** The cover a book shows (of its primary file). */
export interface CoverRef {
  fileId: number;
  version: number;
}

export interface CoverStatus {
  withCover: number;
  pending: number;
  withoutCover: number;
}

export interface GenerateCoversResult {
  processed: number;
  generated: number;
  remaining: number;
}

export interface LibraryCheckResult {
  filesOk: number;
  filesMissing: number;
}

export interface FileRenamed {
  fileId: number;
  fileName: string;
  libraryPath: string;
  missingInStorage: boolean;
}
