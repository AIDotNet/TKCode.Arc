using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>One page of a <see cref="DocsSidebar"/>; a page with <see cref="Pages"/> opens and closes them.</summary>
public class DocsSidebarPage : AvaloniaObject
{
    /// <summary>The page's id (usually its path, such as "guides/caching"); <see cref="DocsSidebar.CurrentKey"/> matches it.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<DocsSidebarPage, string?>(nameof(Key));

    /// <summary>The page title in the list.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DocsSidebarPage, string>(nameof(Title), string.Empty);

    /// <summary>A short tag after the title, such as "New" or "Beta"; null shows nothing.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<DocsSidebarPage, string?>(nameof(Badge));

    public DocsSidebarPage()
    {
    }

    public DocsSidebarPage(string key, string title, string? badge = null)
    {
        Key = key;
        Title = title;
        Badge = badge;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="BadgeProperty"/>
    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>The sub-pages (the XAML content). One level is shown.</summary>
    [Content]
    public AvaloniaList<DocsSidebarPage> Pages { get; } = new();

    public override string ToString() => Title;
}

/// <summary>A titled group of pages in a <see cref="DocsSidebar"/>; its heading folds it.</summary>
public class DocsSidebarSection : AvaloniaObject
{
    /// <summary>The section heading.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DocsSidebarSection, string>(nameof(Title), string.Empty);

    /// <summary>Whether the section's pages are shown (two-way). The section of the current page always opens.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<DocsSidebarSection, bool>(nameof(IsExpanded), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public DocsSidebarSection()
    {
    }

    public DocsSidebarSection(string title, params DocsSidebarPage[] pages)
    {
        Title = title;
        Pages.AddRange(pages);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>The pages of the section (the XAML content).</summary>
    [Content]
    public AvaloniaList<DocsSidebarPage> Pages { get; } = new();

    public override string ToString() => Title;
}

/// <summary>One documentation version in the switcher of a <see cref="DocsSidebar"/>.</summary>
/// <param name="Name">The version as it shows on the switcher, such as "v3.2".</param>
/// <param name="Note">A short note in the menu, such as "latest" or "legacy"; null for none.</param>
public sealed record DocsSidebarVersion(string Name, string? Note = null)
{
    /// <summary>The menu text: the name, and the note in brackets.</summary>
    public string MenuText => Note is null ? Name : $"{Name} ({Note})";
}

/// <summary>Arguments of <see cref="DocsSidebar.PageInvoked"/>.</summary>
public sealed class DocsSidebarPageEventArgs : RoutedEventArgs
{
    public DocsSidebarPageEventArgs(RoutedEvent routedEvent, DocsSidebarPage page) : base(routedEvent)
    {
        Page = page;
    }

    /// <summary>The page that was chosen (now the current page).</summary>
    public DocsSidebarPage Page { get; }
}

/// <summary>Arguments of <see cref="DocsSidebar.VersionChanged"/>.</summary>
public sealed class DocsSidebarVersionChangedEventArgs : RoutedEventArgs
{
    public DocsSidebarVersionChangedEventArgs(RoutedEvent routedEvent, string? oldVersion, string? newVersion) : base(routedEvent)
    {
        OldVersion = oldVersion;
        NewVersion = newVersion;
    }

    /// <summary>The version that was chosen before.</summary>
    public string? OldVersion { get; }

    /// <summary>The version now chosen; load its pages into <see cref="DocsSidebar.Sections"/>.</summary>
    public string? NewVersion { get; }
}

/// <summary>The built-in text of a <see cref="DocsSidebar"/>. Replace the whole object to localize it.</summary>
public sealed class DocsSidebarLabels
{
    /// <summary>The word after the product name in the header: "docs".</summary>
    public string Docs { get; init; } = "docs";

    /// <summary>The search box placeholder and accessible name.</summary>
    public string Search { get; init; } = "Search docs";

    /// <summary>The text shown when the search matches no page.</summary>
    public string NoMatches { get; init; } = "No pages match. Try a shorter word.";

    /// <summary>The accessible name of the version switcher: current version ⇒ "Version v3.2".</summary>
    public Func<string, string> VersionName { get; init; } = static version => $"Version {version}";

    /// <summary>The navigation landmark's name.</summary>
    public string Navigation { get; init; } = "Documentation";
}
