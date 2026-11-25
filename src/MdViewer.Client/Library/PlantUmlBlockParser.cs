using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MdViewer.Library;

internal sealed class PlantUmlBlockParser : BlockParser
{
    public PlantUmlBlockParser() => OpeningCharacters = ['@'];

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent || processor.GetCurrentContainerOpened() is not MarkdownDocument ||
            !IsStartTag(processor.Line.AsSpan().TrimEnd()) || !HasClosingTag(processor.Line))
        {
            return BlockState.None;
        }

        var block = new CodeBlock(this)
        {
            Column = processor.Column,
            Span = new SourceSpan(processor.Start, processor.Line.End)
        };
        block.GetAttributes().AddClass("language-plantuml");
        processor.NewBlocks.Push(block);
        return BlockState.Continue;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        block.UpdateSpanEnd(processor.Line.End);
        var state = IsEndTag(processor.Line.AsSpan().TrimEnd()) ? BlockState.Break : BlockState.Continue;
        processor.GoToColumn(0);
        return state;
    }

    private static bool HasClosingTag(StringSlice openingLine)
    {
        // Leave an unmatched opener in the Markdown stream so it cannot consume the rest of the document.
        foreach (var line in openingLine.Text.AsSpan(openingLine.End + 1).EnumerateLines())
        {
            var text = line.Trim();
            if (IsEndTag(text))
            {
                return true;
            }

            if (IsStartTag(text))
            {
                break;
            }
        }

        return false;
    }

    private static bool IsStartTag(ReadOnlySpan<char> line)
    {
        const string tag = "@startuml";
        return line.StartsWith(tag, StringComparison.OrdinalIgnoreCase) &&
            (line.Length == tag.Length || char.IsWhiteSpace(line[tag.Length]));
    }

    private static bool IsEndTag(ReadOnlySpan<char> line) => line.Equals("@enduml", StringComparison.OrdinalIgnoreCase);
}
