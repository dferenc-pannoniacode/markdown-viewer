namespace MdViewer.Library;

public sealed class LibraryTree(string name, string path)
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    public List<LibraryTree> Folders { get; } = [];
    public List<string> Files { get; } = [];

    public static LibraryTree Build(IEnumerable<string> paths)
    {
        var root = new LibraryTree("", "");
        var folders = new Dictionary<string, LibraryTree>(StringComparer.Ordinal) { [""] = root };
        foreach (var path in paths)
        {
            var node = root;
            var remainder = path;
            while (remainder.IndexOf('/') is var slash && slash >= 0)
            {
                var name = remainder[..slash];
                var folderPath = node.Path + name + "/";
                if (!folders.TryGetValue(folderPath, out var folder))
                {
                    folder = new LibraryTree(name, folderPath);
                    node.Folders.Add(folder);
                    folders.Add(folderPath, folder);
                }

                node = folder;
                remainder = remainder[(slash + 1)..];
            }

            node.Files.Add(path);
        }

        return root;
    }
}
