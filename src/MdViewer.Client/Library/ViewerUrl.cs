namespace MdViewer.Library;

internal static class ViewerUrl
{
    public static string Document(bool local, string? path = null, string? fragment = null) =>
        Build(local, fragment, ("file", path));

    public static string Unresolved(bool local, string origin, string target, bool wiki, string? fragment) =>
        Build(local, fragment, ("from", origin), (wiki ? "wiki" : "missing", target));

    public static string FileApi(string endpoint, string path) => $"api/library/{endpoint}?path={Uri.EscapeDataString(path)}";

    private static string Build(bool local, string? fragment, params (string Key, string? Value)[] parameters)
    {
        var query = string.Join('&', parameters.Prepend((Key: "source", Value: local ? "local" : null))
            .Where(pair => pair.Value is not null)
            .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}"));
        return "/" + (query.Length == 0 ? "" : "?" + query)
            + (string.IsNullOrEmpty(fragment) ? "" : "#" + Uri.EscapeDataString(fragment));
    }
}
