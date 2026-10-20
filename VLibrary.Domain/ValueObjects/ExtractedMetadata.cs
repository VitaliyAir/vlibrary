namespace VLibrary.Domain.ValueObjects
{
    /// <summary>
    /// Metadata read from a book file during import (stored with the file as JSON).
    /// Used to create a book later when the file waits in the duplicate review queue.
    /// </summary>
    public class ExtractedMetadata
    {
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public List<string> Authors { get; set; } = [];
        public string? Isbn { get; set; }
        public int? Year { get; set; }
        public string? Publisher { get; set; }
        public string? Language { get; set; }
        public int? PageCount { get; set; }

        /// <summary>Where the title came from: pdf, epub, djvu or filename.</summary>
        public string Source { get; set; } = "filename";

        /// <summary>
        /// A separate instance: EF stores the metadata as JSON owned by exactly one file, so copies must not share it.
        /// </summary>
        public ExtractedMetadata Clone()
        {
            var clone = (ExtractedMetadata)MemberwiseClone();
            clone.Authors = [.. Authors];
            return clone;
        }
    }
}
