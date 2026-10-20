import { useOpenFile } from '../api/hooks';
import type { BookFile } from '../api/types';
import { formatSize } from '../format';
import { ErrorMessage } from './common';

/**
 * UC-5: a book file lives in VStorage. "Open" asks the API for a short-lived download link and opens it in a new tab;
 * the content goes from VStorage straight to the browser. The API records the opening (NotStarted -> In progress).
 */
export function OpenFileButton({ file, compact }: { file: BookFile; compact?: boolean }) {
  const openFile = useOpenFile();

  const open = () => {
    // Opened before the request: a window opened after an await would be blocked as a pop-up.
    const tab = window.open('', '_blank');
    openFile.mutate(file.id, {
      onSuccess: (opened) => {
        if (tab) tab.location.href = opened.url;
        else window.location.assign(opened.url);
      },
      onError: () => tab?.close(),
    });
  };

  return (
    <div className={`file-path ${file.status !== 'Ok' ? 'missing' : ''}`}>
      <span className="badge">{file.format}</span>
      {file.isPrimary && !compact && <span className="badge primary">Primary</span>}
      <span className="path" title={file.libraryPath}>
        {compact ? file.fileName : file.libraryPath}
      </span>
      <button type="button" className="small" disabled={openFile.isPending} onClick={open}>
        {openFile.isPending ? 'Opening…' : 'Open'}
      </button>
      {!compact && (
        <span className="muted">
          {formatSize(file.sizeBytes)}
          {file.pageCount ? ` · ${file.pageCount} p.` : ''}
          {file.status === 'Missing' ? ' · not found in storage' : ''}
        </span>
      )}
      <ErrorMessage error={openFile.error} />
    </div>
  );
}
