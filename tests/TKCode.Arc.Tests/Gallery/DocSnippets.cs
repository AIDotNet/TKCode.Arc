using System.Text;
using Avalonia.Markup.Xaml;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The code blocks of a gallery docs page and the check that its XAML examples load. XAML fragments are wrapped in a
/// <c>StackPanel</c> that declares the default, <c>x</c> and <c>arc</c> namespaces; a block that starts with an element
/// declaring <c>xmlns</c> itself is loaded as written. Loading creates the real controls, so a wrong type name, an unknown
/// property or an invalid value fails the check. Event handler attributes (<c>Click="OnClick"</c>) cannot load without
/// code-behind: show handlers in a <c>csharp</c> block instead.
/// </summary>
/// <remarks>Linked into the command-line docs checker as well; keep it free of xunit.</remarks>
internal static class DocSnippets
{
    private const string Wrapper =
        "<StackPanel xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
        "xmlns:arc=\"https://github.com/AtomUICommunity/TKCode.Arc\">{0}</StackPanel>";

    /// <summary>A fenced block: its info-string language (lowercase, may be empty), its text and the 1-based line it starts on.</summary>
    internal sealed record Block(string Language, string Code, int Line)
    {
        public bool IsXaml => Language is "xml" or "xaml" or "axaml";
    }

    public static List<Block> Blocks(string markdown)
    {
        var blocks = new List<Block>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        StringBuilder? current = null;
        var language = string.Empty;
        var start = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (current is null)
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    language = line.Substring(3).Trim().Split(' ', 2)[0].ToLowerInvariant();
                    current = new StringBuilder();
                    start = i + 1;
                }
            }
            else if (line.StartsWith("```", StringComparison.Ordinal))
            {
                blocks.Add(new Block(language, current.ToString().TrimEnd('\n'), start));
                current = null;
            }
            else
            {
                current.Append(line).Append('\n');
            }
        }
        return blocks;
    }

    /// <summary>Loads <paramref name="xaml"/>; returns null when it loads, otherwise the reason.</summary>
    public static string? ValidateXaml(string xaml)
    {
        var declaresNamespace = xaml.TrimStart().StartsWith("<", StringComparison.Ordinal) && FirstTag(xaml).Contains("xmlns", StringComparison.Ordinal);
        try
        {
            AvaloniaRuntimeXamlLoader.Load(declaresNamespace ? xaml : string.Format(Wrapper, xaml));
            return null;
        }
        catch (Exception exception)
        {
            return exception.GetBaseException().Message;
        }
    }

    private static string FirstTag(string xaml)
    {
        var text = xaml.TrimStart();
        var end = text.IndexOf('>');
        return end < 0 ? text : text.Substring(0, end);
    }
}
