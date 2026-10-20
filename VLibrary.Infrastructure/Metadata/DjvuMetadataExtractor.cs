using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using VLibrary.Application.Features.Imports.Metadata;
using VLibrary.Domain.Enums;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Infrastructure.Metadata
{
    /// <summary>
    /// DjVu metadata through DjVuLibre "djvused" (optional: configure Metadata:DjvusedPath).
    /// </summary>
    internal sealed partial class DjvuMetadataExtractor(IOptions<MetadataOptions> options) : IFormatMetadataExtractor
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        public bool CanRead(FileTypesEnum format) => format is FileTypesEnum.DJVU or FileTypesEnum.DJV;

        public async Task<ExtractedMetadata?> ReadAsync(string path, CancellationToken cancellationToken)
        {
            var djvused = options.Value.DjvusedPath;
            if (string.IsNullOrWhiteSpace(djvused) || !File.Exists(djvused))
            {
                return null;
            }

            var startInfo = new ProcessStartInfo(djvused)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            // -u: print non-ASCII text as UTF-8; "n" prints the page count.
            startInfo.ArgumentList.Add("-u");
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add("print-meta; n");
            startInfo.ArgumentList.Add(path);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            string output;
            try
            {
                output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            return process.ExitCode == 0 ? Parse(output) : null;
        }

        internal static ExtractedMetadata Parse(string output)
        {
            var metadata = new ExtractedMetadata { Source = "djvu" };

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(line, out var pages))
                {
                    metadata.PageCount = pages;
                    continue;
                }

                var match = MetaLineRegex().Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var value = match.Groups["value"].Value.Replace("\\\"", "\"").Trim();
                switch (match.Groups["key"].Value.ToLowerInvariant())
                {
                    case "title": metadata.Title = value; break;
                    case "author": metadata.Authors = AuthorNamesSplitter.Split(value); break;
                    case "isbn": metadata.Isbn = value; break;
                    case "publisher": metadata.Publisher = value; break;
                    case "year" when int.TryParse(value, out var year): metadata.Year = year; break;
                }
            }

            return metadata;
        }

        [GeneratedRegex("^(?<key>\\w+)\\s+\"(?<value>.*)\"$")]
        private static partial Regex MetaLineRegex();
    }
}
