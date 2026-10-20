import { useCoverUrl } from '../api/hooks';
import type { CoverRef } from '../api/types';

type CoverSize = 'small' | 'medium' | 'card' | 'large';

/** Same title -> same colour; saturation/lightness chosen so white text stays readable in light and dark themes. */
function placeholderColour(title: string): string {
  let hash = 0;
  for (const char of title) hash = (hash * 31 + char.charCodeAt(0)) | 0;
  return `hsl(${Math.abs(hash) % 360} 38% 42%)`;
}

function initials(title: string): string {
  return title
    .split(/\s+/)
    .filter((word) => /\p{L}|\p{N}/u.test(word))
    .slice(0, 2)
    .map((word) => [...word][0].toUpperCase())
    .join('');
}

/**
 * B1: the cover thumbnail of a book (or of a file). Without a cover (DOC/DOCX/TXT, damaged files, not generated yet)
 * a placeholder shows the format and the first letters of the title.
 */
export function Cover({ cover, title, format, size }: { cover: CoverRef | null; title: string; format?: string; size: CoverSize }) {
  const url = useCoverUrl(cover?.fileId, cover?.version);

  if (url) {
    return <img className={`cover cover-${size}`} src={url} alt="" loading="lazy" />;
  }

  return (
    <div className={`cover cover-${size} cover-placeholder`} style={{ background: placeholderColour(title) }} aria-hidden="true">
      <span className="cover-initials">{initials(title)}</span>
      {format && <span className="cover-format">{format}</span>}
    </div>
  );
}
