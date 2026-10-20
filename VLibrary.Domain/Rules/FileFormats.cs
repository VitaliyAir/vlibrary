using VLibrary.Domain.Enums;

namespace VLibrary.Domain.Rules
{
    public static class FileFormats
    {
        // Explicit map: Enum.TryParse would also accept numbers (".1" -> PDF), so names are matched instead.
        private static readonly Dictionary<string, FileTypesEnum> ByExtension =
            Enum.GetValues<FileTypesEnum>().ToDictionary(f => "." + f.ToString().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> SupportedExtensions { get; } = ByExtension.Keys.ToArray();

        public static bool TryFromExtension(string? extension, out FileTypesEnum format)
        {
            format = default;
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            var key = extension.StartsWith('.') ? extension : "." + extension;
            return ByExtension.TryGetValue(key, out format);
        }

        public static bool IsSupported(string path) => TryFromExtension(Path.GetExtension(path), out _);
    }
}
