using MdViewer.Library;

namespace MdViewer;

internal sealed class MountedLibrary(string root)
{
    private const FileAttributes Ignored = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System;
    private readonly string rootPath = Path.GetFullPath(root);

    public string[] ListDocuments()
    {
        var files = new List<string>();
        if (!IsDirectory(rootPath))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = Ignored
        };

        foreach (var file in Directory.EnumerateFiles(rootPath, "*", options))
        {
            var relative = Path.GetRelativePath(rootPath, file).Replace(Path.DirectorySeparatorChar, '/');
            if (LibraryPath.IsDocument(relative) && relative.Split('/').All(LibraryPath.IsSegment))
            {
                files.Add(relative);
            }
        }

        return files.Order(StringComparer.OrdinalIgnoreCase).ThenBy(path => path, StringComparer.Ordinal).ToArray();
    }

    public async Task<IResult> ReadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        var file = LibraryPath.IsDocument(path) ? FindFile(path) : null;
        if (file is null)
        {
            return Results.NotFound();
        }

        await using var stream = File.OpenRead(file);
        if (stream.Length > LibraryPath.MaxDocumentBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var reader = new StreamReader(stream);
        var buffer = new char[4096];
        var text = new System.Text.StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (text.Length + read > LibraryPath.MaxDocumentBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            text.Append(buffer, 0, read);
        }

        return Results.Text(text.ToString(), "text/plain; charset=utf-8");
    }

    public IResult OpenImage(string path)
    {
        var contentType = LibraryPath.ImageType(path);
        var file = contentType is not null ? FindFile(path) : null;
        return file is null ? Results.NotFound() : Results.File(file, contentType);
    }

    private string? FindFile(string relative)
    {
        var parts = relative.Split('/');
        if (!IsDirectory(rootPath) || !parts.All(LibraryPath.IsSegment))
        {
            return null;
        }

        var path = rootPath;
        for (var i = 0; i < parts.Length; i++)
        {
            path = Path.Combine(path, parts[i]);
            var attributes = new FileInfo(path).Attributes;
            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            if (attributes == (FileAttributes)(-1) || (attributes & Ignored) != 0
                || isDirectory != (i < parts.Length - 1))
            {
                return null;
            }
        }

        return path;
    }

    private static bool IsDirectory(string path) => new DirectoryInfo(path) is { Exists: true } directory
        && (directory.Attributes & FileAttributes.ReparsePoint) == 0;
}
