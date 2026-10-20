using System.Diagnostics;
using SkiaSharp;
using VLibrary.Infrastructure.Metadata;

namespace VLibrary.Infrastructure.Covers
{
    /// <summary>
    /// DjVu first page through DjVuLibre "ddjvu" (optional): rendered to a PPM file in the temp folder and decoded here
    /// (SkiaSharp cannot read PPM).
    /// </summary>
    internal static class DjvuCover
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        /// <summary>Metadata:DdjvuPath, else "ddjvu(.exe)" next to Metadata:DjvusedPath; null when not installed.</summary>
        public static string? ResolveTool(MetadataOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.DdjvuPath))
            {
                return File.Exists(options.DdjvuPath) ? options.DdjvuPath : null;
            }

            if (string.IsNullOrWhiteSpace(options.DjvusedPath))
            {
                return null;
            }

            var folder = Path.GetDirectoryName(options.DjvusedPath);
            var candidate = folder == null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ddjvu.exe" : "ddjvu");
            return candidate != null && File.Exists(candidate) ? candidate : null;
        }

        public static async Task<SKBitmap?> RenderAsync(string? ddjvu, string path, CancellationToken cancellationToken)
        {
            if (ddjvu == null)
            {
                return null;
            }

            var output = Path.Combine(Path.GetTempPath(), $"vlibrary-cover-{Guid.NewGuid():N}.ppm");
            try
            {
                var startInfo = new ProcessStartInfo(ddjvu)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-format=ppm");
                startInfo.ArgumentList.Add("-page=1");
                startInfo.ArgumentList.Add("-size=600x900");
                startInfo.ArgumentList.Add(path);
                startInfo.ArgumentList.Add(output);

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(Timeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    throw;
                }

                return process.ExitCode == 0 && File.Exists(output) ? PpmReader.Decode(await File.ReadAllBytesAsync(output, cancellationToken)) : null;
            }
            finally
            {
                File.Delete(output);
            }
        }
    }

    /// <summary>Binary PPM ("P6", 8 bits per channel) as produced by ddjvu -format=ppm.</summary>
    internal static class PpmReader
    {
        public static SKBitmap? Decode(byte[] data)
        {
            var position = 0;
            if (NextToken(data, ref position) != "P6"
                || !int.TryParse(NextToken(data, ref position), out var width)
                || !int.TryParse(NextToken(data, ref position), out var height)
                || !int.TryParse(NextToken(data, ref position), out var maxValue)
                || width <= 0 || height <= 0 || maxValue != 255)
            {
                return null;
            }

            position++; // the single whitespace after the header
            if (data.Length - position < width * height * 3)
            {
                return null;
            }

            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
            var pixels = new byte[width * height * 4];
            for (int i = 0, j = position; i < pixels.Length; i += 4, j += 3)
            {
                pixels[i] = data[j];
                pixels[i + 1] = data[j + 1];
                pixels[i + 2] = data[j + 2];
                pixels[i + 3] = 255;
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            return bitmap;
        }

        private static string NextToken(byte[] data, ref int position)
        {
            // Skip whitespace and "#" comment lines between header fields.
            while (position < data.Length && (char.IsWhiteSpace((char)data[position]) || data[position] == '#'))
            {
                if (data[position] == '#')
                {
                    while (position < data.Length && data[position] != '\n')
                    {
                        position++;
                    }
                }

                position++;
            }

            var start = position;
            while (position < data.Length && !char.IsWhiteSpace((char)data[position]))
            {
                position++;
            }

            return System.Text.Encoding.ASCII.GetString(data, start, position - start);
        }
    }
}
