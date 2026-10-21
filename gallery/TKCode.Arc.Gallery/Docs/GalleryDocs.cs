using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;

namespace TKCode.Arc.Gallery.Docs;

/// <summary>
/// The docs pages' Markdown. A component's page comes in the requested language, or in en-US with a notice when it
/// has no translation; each <c>&lt;!-- api: Type --&gt;</c> line becomes that type's API reference, built from the
/// generated <see cref="GalleryDocData"/> (descriptions translated through <c>Docs/Api/&lt;language&gt;.xlf</c>,
/// defaults read from the Avalonia property metadata), and each <c>&lt;Demo name="..." /&gt;</c> line becomes a live
/// example (<see cref="GalleryDemos"/>).
/// </summary>
public static partial class GalleryDocs
{
    public const string SourceLanguage = "en-US";

    private const string None = "–";

    private static readonly Dictionary<string, GalleryDocPage[]> s_pages = GalleryDocData.Pages
        .GroupBy(p => p.ClassName, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

    private static readonly Dictionary<string, GalleryApiType> s_types =
        GalleryDocData.Types.ToDictionary(t => t.Name, StringComparer.Ordinal);

    private static readonly Dictionary<(string Language, string Key), string> s_translations =
        GalleryDocData.Translations.ToDictionary(t => (t.Language.ToLowerInvariant(), t.Key), t => t.Text);

    /// <summary>Whether <paramref name="className"/> has a page (in any language).</summary>
    public static bool Has(string className) => s_pages.ContainsKey(className);

    /// <summary>Whether <paramref name="className"/>'s en-US page shows live examples, so it is the component's whole page.</summary>
    public static bool HasDemos(string className)
        => s_pages.TryGetValue(className, out var pages) && Find(pages, SourceLanguage) is { } page && DemoTag().IsMatch(page.Body);

    /// <summary>The demos <paramref name="className"/>'s page in <paramref name="language"/> shows, in order (<c>Component.Name</c>).</summary>
    public static IReadOnlyList<string> DemoIds(string className, string language)
    {
        if (!s_pages.TryGetValue(className, out var pages))
        {
            return [];
        }
        var page = Find(pages, language) ?? Find(pages, SourceLanguage) ?? pages[0];
        return DemoTag().Matches(page.Body).Select(m => DemoId(className, m.Groups[1].Value)).ToArray();
    }

    /// <summary>Every component with a page.</summary>
    public static IEnumerable<string> ClassNames => s_pages.Keys;

    /// <summary>
    /// The NuGet package <paramref name="className"/> ships in (its assembly, such as <c>TKCode.Arc</c> or
    /// <c>TKCode.Arc.Illustrations</c>), from the type its page documents; null when the page documents no such type.
    /// </summary>
    public static string? PackageOf(string className)
        => s_types.TryGetValue(className, out var type) ? type.Owner.Assembly.GetName().Name : null;

    /// <summary>The languages <paramref name="className"/>'s page is written in, en-US first.</summary>
    public static IReadOnlyList<string> Languages(string className)
        => s_pages.TryGetValue(className, out var pages) ? pages.Select(p => p.Language).ToArray() : [];

    /// <summary>The one-sentence summary of <paramref name="className"/>'s page in <paramref name="language"/> (en-US when not translated).</summary>
    public static string? Summary(string className, string language)
        => s_pages.TryGetValue(className, out var pages) ? (Find(pages, language) ?? Find(pages, SourceLanguage) ?? pages[0]).Summary : null;

    /// <summary>
    /// <paramref name="className"/>'s page in <paramref name="language"/> (en-US when it is not translated), or null when
    /// the component has no page.
    /// </summary>
    public static GalleryDoc? Get(string className, string language)
    {
        if (!s_pages.TryGetValue(className, out var pages))
        {
            return null;
        }
        var page = Find(pages, language) ?? Find(pages, SourceLanguage) ?? pages[0];
        var text = GalleryDocText.For(language);
        var markdown = new StringBuilder();
        if (!string.Equals(page.Language, language, StringComparison.OrdinalIgnoreCase))
        {
            markdown.Append("> ").Append(text.Untranslated).Append("\n\n");
        }
        else if (page.IsOutdated)
        {
            markdown.Append("> ").Append(text.Outdated).Append("\n\n");
        }
        markdown.Append(Directive().Replace(page.Body, match => ApiReference(match.Groups[1].Value, language).TrimEnd()));
        var body = markdown.ToString();
        return new GalleryDoc(page.ClassName, page.Language, language, page.Title, page.Summary,
            DemoTag().Replace(body, match => DemoBlock(className, match, body)),
            DemoTag().Replace(body, match => DemoSource(className, match.Groups[1].Value)),
            page.IsOutdated);
    }

    /// <summary>
    /// The block StreamMark renders as a live example (<c>GalleryDemoPlugin</c>). Its block parser does not end a
    /// component at a self-closing tag, so the tag is closed on the same line. A demo above the first section is the
    /// page's preview.
    /// </summary>
    private static string DemoBlock(string className, Match match, string markdown)
    {
        var hero = !markdown.AsSpan(0, match.Index).Contains("\n## ", StringComparison.Ordinal) && !markdown.StartsWith("## ", StringComparison.Ordinal);
        return $"<demo name=\"{DemoId(className, match.Groups[1].Value)}\"{(hero ? " hero=\"true\"" : string.Empty)}></demo>";
    }

    /// <summary>A demo in copied Markdown: its files as code blocks (AXAML, then the code-behind).</summary>
    private static string DemoSource(string className, string name)
        => GalleryDemos.Get(DemoId(className, name)) is { } demo
            ? string.Join("\n\n", demo.Files.Select(f => $"```{(f.Language == "axaml" ? "xml" : f.Language)}\n{f.Source}\n```"))
            : string.Empty;

    private static string DemoId(string className, string name) => name.Contains('.', StringComparison.Ordinal) ? name : $"{className}.{name}";

    /// <summary>The API reference of <paramref name="typeName"/> as Markdown: a property table, an event table, its
    /// pseudo-classes and the base class it inherits the rest from.</summary>
    public static string ApiReference(string typeName, string language)
    {
        if (!s_types.TryGetValue(typeName, out var type))
        {
            return string.Empty;
        }
        var text = GalleryDocText.For(language);
        var markdown = new StringBuilder();
        markdown.Append("### ").Append(type.Name).Append("\n\n");
        if (Translate(language, type.Name, type.Summary) is { } summary)
        {
            markdown.Append(summary).Append("\n\n");
        }

        var properties = type.Members.Where(m => m.Kind == GalleryApiMemberKind.Property).ToList();
        if (properties.Count > 0)
        {
            Row(markdown, text.Property, text.Type, text.Default, text.Description);
            markdown.Append("|---|---|---|---|\n");
            foreach (var member in properties)
            {
                Row(markdown, Code(member.Name), Code(member.Type), DefaultOf(member, type.Owner), DescriptionCell(member, language, text));
            }
            markdown.Append('\n');
        }

        var events = type.Members.Where(m => m.Kind == GalleryApiMemberKind.Event).ToList();
        if (events.Count > 0)
        {
            Row(markdown, text.Event, text.Arguments, text.Description);
            markdown.Append("|---|---|---|\n");
            foreach (var member in events)
            {
                Row(markdown, Code(member.Name), Code(member.Type), DescriptionCell(member, language, text));
            }
            markdown.Append('\n');
        }

        if (type.PseudoClasses.Length > 0)
        {
            markdown.Append(text.PseudoClasses).Append(' ').Append(string.Join(", ", type.PseudoClasses.Select(p => $"`{p}`"))).Append("\n\n");
        }
        if (type.BaseName is { } baseName)
        {
            markdown.Append(string.Format(CultureInfo.InvariantCulture, text.Inherits, baseName)).Append("\n\n");
        }
        return markdown.ToString();
    }

    private static GalleryDocPage? Find(GalleryDocPage[] pages, string language)
        => pages.FirstOrDefault(p => string.Equals(p.Language, language, StringComparison.OrdinalIgnoreCase));

    private static string? Translate(string language, string key, string? english)
        => s_translations.TryGetValue((language.ToLowerInvariant(), key), out var translated) ? translated : english;

    private static void Row(StringBuilder markdown, params string[] cells)
    {
        markdown.Append('|');
        foreach (var cell in cells)
        {
            markdown.Append(' ').Append(cell).Append(" |");
        }
        markdown.Append('\n');
    }

    /// <summary>
    /// The description, after the read-only and attached tags. The tags stay out of the name cell: StreamMark shrinks
    /// every column of a table that is too wide, so a wider name column breaks short types and defaults mid-word.
    /// </summary>
    private static string DescriptionCell(GalleryApiMember member, string language, GalleryDocText text)
    {
        var cell = new StringBuilder();
        if (member.IsAttached)
        {
            cell.Append('*').Append(text.Attached).Append("* ");
        }
        if (member.IsReadOnly)
        {
            cell.Append('*').Append(text.ReadOnly).Append("* ");
        }
        return cell.Append(Escape(Translate(language, member.Key, member.Summary) ?? string.Empty)).ToString();
    }

    /// <summary>The default from the property metadata of <paramref name="owner"/>, so overridden defaults show.</summary>
    private static string DefaultOf(GalleryApiMember member, Type owner)
    {
        if (member.Property?.GetMetadata(owner) is not IStyledPropertyMetadata metadata)
        {
            return None;
        }
        return metadata.DefaultValue switch
        {
            null => Code("null"),
            string value => Code($"\"{value}\""),
            bool value => Code(value ? "true" : "false"),
            Enum value => Code(value.ToString()),
            double.NaN => Code("NaN"),
            IFormattable value => Code(value.ToString(null, CultureInfo.InvariantCulture)),
            var value => value.ToString() is { Length: > 0 } shown && shown != value.GetType().FullName ? Code(shown) : None
        };
    }

    private static string Code(string value) => $"`{Escape(value)}`";

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    [GeneratedRegex(@"^[ \t]*<!--[ \t]*api:[ \t]*([A-Za-z_][A-Za-z0-9_]*)[ \t]*-->[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex Directive();

    // The same pattern as the generator's DocPage, which checks that every demo exists.
    [GeneratedRegex(@"^[ \t]*<Demo[ \t]+name=""([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)""[ \t]*/>[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex DemoTag();

    /// <summary>The words the generated parts of a page are made of, per language (en-US for the rest).</summary>
    private sealed record GalleryDocText(string Property, string Type, string Default, string Description, string Event,
                                         string Arguments, string PseudoClasses, string Inherits, string ReadOnly,
                                         string Attached, string Untranslated, string Outdated)
    {
        private static readonly GalleryDocText s_english = new(
            "Property", "Type", "Default", "Description", "Event", "Arguments", "Pseudo-classes:",
            "Derives from `{0}`: every member of `{0}` works as well.", "Read-only.", "Attached.",
            "This page is not translated yet; it shows the English original.",
            "The English page changed after this translation was made; some details may be out of date.");

        private static readonly GalleryDocText s_simplified = new(
            "属性", "类型", "默认值", "说明", "事件", "参数", "伪类：",
            "派生自 `{0}`，`{0}` 的成员同样可用。", "只读。", "附加属性。",
            "此页尚未翻译，以下为英文原文。",
            "英文原文在翻译之后有更新，部分内容可能已过时。");

        private static readonly GalleryDocText s_traditional = new(
            "屬性", "型別", "預設值", "說明", "事件", "引數", "虛擬類別：",
            "衍生自 `{0}`，`{0}` 的成員同樣可用。", "唯讀。", "附加屬性。",
            "此頁尚未翻譯，以下為英文原文。",
            "英文原文在翻譯之後有更新，部分內容可能已過時。");

        public static GalleryDocText For(string language) => language.ToLowerInvariant() switch
        {
            "zh-cn" or "zh-hans" or "zh-sg" => s_simplified,
            "zh-tw" or "zh-hant" or "zh-hk" or "zh-mo" => s_traditional,
            _ => s_english
        };
    }
}
