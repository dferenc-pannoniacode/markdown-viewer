using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Syntax.Inlines;

namespace MdViewer.Library;

internal sealed class WikiLink : LinkInline;

internal sealed class WikiLinkParser() : InlineParser
{
    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var text = slice.AsSpan();
        if (!text.StartsWith("[[", StringComparison.Ordinal))
        {
            return false;
        }

        var contents = text[2..];
        var end = contents.IndexOfAny("[]\r\n");
        if (end < 1 || !contents[end..].StartsWith("]]", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = contents[..end].ToString().Replace("\\|", "|").Split('|', 2);
        var target = parts[0].Trim();
        if (target.Length == 0)
        {
            return false;
        }

        var link = new WikiLink { Url = target, IsClosed = true };
        var label = parts.Length == 2 && parts[1].Trim().Length > 0 ? parts[1].Trim() : target;
        link.AppendChild(new LiteralInline(label));
        processor.Inline = link;
        slice.Start += end + 4;
        return true;
    }

    public static void AddTo(Markdig.MarkdownPipelineBuilder pipeline)
    {
        var parser = new WikiLinkParser { OpeningCharacters = ['['] };
        pipeline.InlineParsers.InsertBefore<LinkInlineParser>(parser);
    }
}
