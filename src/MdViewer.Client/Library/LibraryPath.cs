namespace MdViewer.Library;

public static class LibraryPath
{
    public const int MaxDocumentBytes = 8 * 1024 * 1024;

    public static bool IsDocument(string path) => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public static string? ImageType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".avif" => "image/avif",
        ".bmp" => "image/bmp",
        _ => null
    };

    public static bool IsSegment(string segment) => segment.Length > 0
        && segment[0] != '.' && !segment.Any(ch => char.IsControl(ch) || ch is '/' or '\\' or ':');

    public static string DirectoryOf(string path) => path[..(path.LastIndexOf('/') + 1)];

    public static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    public static string? Resolve(string current, string relative)
    {
        if (relative.StartsWith("//", StringComparison.Ordinal) || relative.Contains('\\'))
        {
            return null;
        }

        var parts = new List<string>();
        if (!relative.StartsWith('/'))
        {
            parts.AddRange(DirectoryOf(current).Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var encoded in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = Uri.UnescapeDataString(encoded);
            if (part == ".")
            {
                continue;
            }

            if (part == ".." && parts.Count > 0)
            {
                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            if (!IsSegment(part))
            {
                return null;
            }

            parts.Add(part);
        }

        return string.Join('/', parts);
    }
}
