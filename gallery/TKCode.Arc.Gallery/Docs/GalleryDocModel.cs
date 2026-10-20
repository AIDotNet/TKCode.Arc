using Avalonia;

namespace TKCode.Arc.Gallery.Docs;

/// <summary>One component page in one language, as written in <c>Docs/&lt;Category&gt;/&lt;ClassName&gt;/&lt;Language&gt;.md</c>.</summary>
/// <param name="IsOutdated">A translation whose en-US page changed after it was translated (its source-hash differs).</param>
/// <param name="Body">The Markdown after the front matter, with its <c>&lt;!-- api: Type --&gt;</c> lines still in place.</param>
public sealed record GalleryDocPage(string ClassName, string Language, string Title, string Summary, bool IsOutdated, string Body);

public enum GalleryApiMemberKind
{
    Property,
    Event
}

/// <param name="Type">The property's value type, or the event's argument type.</param>
/// <param name="Key"><c>DeclaringType.Member</c>, the key of its translations.</param>
/// <param name="Property">The Avalonia property (null for events and direct properties), whose metadata holds the default.</param>
public sealed record GalleryApiMember(GalleryApiMemberKind Kind, string Name, string Type, string Key, string? Summary,
                                      AvaloniaProperty? Property, bool IsReadOnly, bool IsAttached);

/// <param name="BaseName">The first non-Arc base class (for example <c>TextBox</c>); its members are not listed.</param>
public sealed record GalleryApiType(string Name, Type Owner, string? Summary, string? BaseName, string[] PseudoClasses,
                                    GalleryApiMember[] Members);

/// <param name="Key"><c>Type</c> or <c>DeclaringType.Member</c>.</param>
public sealed record GalleryApiTranslation(string Language, string Key, string Text);

/// <summary>A page ready to show: Markdown with its API references filled in, in <see cref="Language"/>.</summary>
/// <param name="RequestedLanguage">The language asked for; the page falls back to en-US when it has no translation.</param>
public sealed record GalleryDoc(string ClassName, string Language, string RequestedLanguage, string Title, string Summary,
                                string Markdown, bool IsOutdated)
{
    public bool IsFallback => !string.Equals(Language, RequestedLanguage, StringComparison.OrdinalIgnoreCase);
}
