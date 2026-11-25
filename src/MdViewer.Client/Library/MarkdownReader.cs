using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MdViewer.Library;

internal sealed record Section(string Id, string Name, int Level);
internal sealed record RenderedDocument(string Html, Section[] Sections);

internal static class MarkdownReader
{
    private static readonly MarkdownPipeline Pipeline = CreatePipeline();

    public static async Task<RenderedDocument> RenderAsync(string markdown, string path, LibraryCatalog catalog,
        bool local, Func<string, Task<string?>> imageUrl)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var link in document.Descendants<LinkInline>().ToArray())
        {
            var url = (link.GetDynamicUrl?.Invoke() ?? link.Url ?? "").Trim();
            link.GetDynamicUrl = null;
            link.Url = link.IsImage
                ? await ResolveImageAsync(url, path, imageUrl)
                : ResolveLink(url, path, catalog, local, link is WikiLink);
            if (link.Url is null)
            {
                Unwrap(link);
            }
            else if (link.IsImage)
            {
                var attributes = link.GetAttributes();
                attributes.AddProperty("loading", "lazy");
                attributes.AddProperty("referrerpolicy", "no-referrer");
            }
        }

        foreach (var link in document.Descendants<AutolinkInline>().ToArray())
        {
            var url = link.IsEmail ? "mailto:" + link.Url : link.Url;
            if (!IsExternal(url, allowContact: true))
            {
                link.ReplaceBy(new LiteralInline(link.Url));
            }
        }

        return new RenderedDocument(document.ToHtml(Pipeline), ReadSections(document));
    }

    private static Section[] ReadSections(MarkdownDocument document)
    {
        using var text = new StringWriter();
        var renderer = new HtmlRenderer(text) { EnableHtmlForInline = false, EnableHtmlEscape = false };
        var sections = new List<Section>();
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            if (heading.GetAttributes().Id is { } id && heading.Inline is { } content && heading.Level <= 3)
            {
                text.GetStringBuilder().Clear();
                renderer.Render(content);
                sections.Add(new Section(id, text.ToString().Trim(), heading.Level));
            }
        }

        return sections.ToArray();
    }

    private static MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder().DisableHtml().UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .UsePipeTables().UseTaskLists().UseEmphasisExtras();
        WikiLinkParser.AddTo(builder);
        return builder.Build();
    }

    private static async Task<string?> ResolveImageAsync(string url, string origin, Func<string, Task<string?>> imageUrl)
    {
        if (IsExternal(url))
        {
            return url;
        }

        var path = LibraryPath.Resolve(origin, url.Split('#', '?')[0]);
        return path is not null && LibraryPath.ImageType(path) is not null ? await imageUrl(path) : null;
    }

    private static string? ResolveLink(string url, string origin, LibraryCatalog catalog, bool local, bool wiki)
    {
        if (!wiki && IsExternal(url, allowContact: true))
        {
            return url;
        }

        var hash = url.Split('#', 2);
        var target = wiki ? hash[0].Trim() : hash[0].Split('?', 2)[0];
        if (!wiki && (target.Contains(':') || target.StartsWith("//", StringComparison.Ordinal)))
        {
            return null;
        }

        var fragment = hash.Length == 2 ? Uri.UnescapeDataString(hash[1]) : null;
        if (wiki && fragment is not null)
        {
            fragment = LinkHelper.UrilizeAsGfm(fragment.Trim());
        }

        if (target.Length == 0)
        {
            return ViewerUrl.Document(local, origin, fragment);
        }

        var matches = wiki ? catalog.ResolveWiki(origin, target) : [];
        var path = wiki ? (matches.Length == 1 ? matches[0] : null) : catalog.ResolveMarkdown(origin, target);
        return path is not null ? ViewerUrl.Document(local, path, fragment)
            : ViewerUrl.Unresolved(local, origin, target, wiki, fragment);
    }

    private static bool IsExternal(string url, bool allowContact = false)
    {
        if (url.Any(char.IsControl) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "https" or "http" || (allowContact && uri.Scheme is "mailto" or "tel");
    }

    private static void Unwrap(LinkInline link)
    {
        while (link.FirstChild is { } child)
        {
            child.Remove();
            link.InsertBefore(child);
        }

        link.Remove();
    }
}
