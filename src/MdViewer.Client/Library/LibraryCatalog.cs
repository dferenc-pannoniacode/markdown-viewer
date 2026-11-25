namespace MdViewer.Library;

internal sealed class LibraryCatalog(IEnumerable<string> paths)
{
    public string[] Documents { get; } = paths.Distinct(StringComparer.Ordinal)
        .Order(StringComparer.OrdinalIgnoreCase).ThenBy(path => path, StringComparer.Ordinal).ToArray();

    public string? DefaultDocument => Find("README.md") ?? Documents.FirstOrDefault();

    public string? Find(string path)
    {
        if (Array.Exists(Documents, item => item == path))
        {
            return path;
        }

        var matches = Documents.Where(item => Equal(item, path)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public string? ResolveMarkdown(string current, string target)
    {
        var path = LibraryPath.Resolve(current, target);
        if (path is null || LibraryPath.IsDocument(path))
        {
            return path is null ? null : Find(path);
        }

        var document = target.EndsWith('/') ? null : Find(path + ".md");
        return document ?? Find(path.Length == 0 ? "README.md" : path + "/README.md");
    }

    public string[] ResolveWiki(string current, string target)
    {
        if (target.Length == 0)
        {
            return Find(current) is { } self ? [self] : [];
        }

        var file = LibraryPath.IsDocument(target) ? target : target + ".md";
        var relative = LibraryPath.Resolve(current, file);
        if (relative is null)
        {
            return [];
        }

        if (Find(relative) is { } nearby)
        {
            return [nearby];
        }

        if (target.StartsWith('/') || target.StartsWith('.'))
        {
            return [];
        }

        var root = LibraryPath.Resolve("", file);
        return root is null ? [] : Documents.Where(path => Equal(path, root)
            || path.EndsWith('/' + root, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private static bool Equal(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
