using Avalonia;

namespace TKCode.Arc.Gallery.Docs;

/// <summary>
/// One component page in one language, as written in <c>Docs/&lt;Category&gt;/&lt;ClassName&gt;/&lt;Language&gt;.md</c>: what the
/// generator read from it. The Markdown itself is read when the page is shown (<see cref="GalleryTexts"/>).
/// </summary>
/// <param name="IsOutdated">A translation whose en-US page changed after it was translated (its source-hash differs).</param>
/// <param name="Folder">The page's folder below <c>Docs/</c>, such as <c>TextFields/Input</c>; its screenshots are in <c>images/</c> there.</param>
/// <param name="Demos">The demos the page shows, in order, as <c>Component.Name</c>.</param>
/// <param name="Screenshots">The page's screenshot lines, in order: the capture's name and its caption.</param>
public sealed record GalleryDocPage(string ClassName, string Language, string Folder, string Title, string Summary, bool IsOutdated,
                                    string[] Demos, (string Name, string Caption)[] Screenshots)
{
    /// <summary>The page's file, relative to the gallery project: <c>Docs/&lt;Folder&gt;/&lt;Language&gt;.md</c>.</summary>
    public string Path => $"Docs/{Folder}/{Language}.md";
}

public enum GalleryApiMemberKind
{
    Property,
    Event
}

/// <param name="Type">The property's value type, or the event's argument type.</param>
/// <param name="Key"><c>DeclaringType.Member</c>, the key of its translations.</param>
public sealed record GalleryApiMember(GalleryApiMemberKind Kind, string Name, string Type, string Key, string? Summary,
                                      bool IsReadOnly, bool IsAttached);

/// <param name="Package">The assembly that defines the type, which is also its NuGet package (for example <c>TKCode.Arc.Special</c>).</param>
/// <param name="BaseName">The first non-Arc base class (for example <c>TextBox</c>); its members are not listed.</param>
/// <param name="Resolve">
/// The type and its members' Avalonia properties. A function, called when the API table is built, so that reading the page
/// list loads no control's assembly and runs no control's static constructor.
/// </param>
public sealed record GalleryApiType(string Name, string Package, string? Summary, string? BaseName, string[] PseudoClasses,
                                    GalleryApiMember[] Members, Func<GalleryApiBinding> Resolve);

/// <summary>What <see cref="GalleryApiType.Resolve"/> returns.</summary>
/// <param name="Properties">
/// In the order of <see cref="GalleryApiType.Members"/>, each member's Avalonia property, whose metadata holds the default (null
/// for events and direct properties).
/// </param>
public sealed record GalleryApiBinding(Type Owner, AvaloniaProperty?[] Properties);

/// <param name="Key"><c>Type</c> or <c>DeclaringType.Member</c>.</param>
public sealed record GalleryApiTranslation(string Language, string Key, string Text);

/// <summary>A screenshot in a docs page: <c>![Caption](images/Name.png)</c>.</summary>
/// <param name="Light">The light-theme capture; the dark one is <c>Name.dark.png</c> beside it.</param>
public sealed record GalleryScreenshot(string Name, string Caption, Uri Light);

/// <summary>A page ready to show: Markdown with its API references and demos filled in, in <see cref="Language"/>.</summary>
/// <param name="RequestedLanguage">The language asked for; the page falls back to en-US when it has no translation.</param>
/// <param name="Markdown">What the page shows: each demo is a <c>&lt;demo&gt;</c> block that renders live.</param>
/// <param name="CopyMarkdown">What "Copy page" copies: each demo is its files (AXAML, then code-behind) in code blocks.</param>
public sealed record GalleryDoc(string ClassName, string Language, string RequestedLanguage, string Title, string Summary,
                                string Markdown, string CopyMarkdown, bool IsOutdated)
{
    public bool IsFallback => !string.Equals(Language, RequestedLanguage, StringComparison.OrdinalIgnoreCase);
}
