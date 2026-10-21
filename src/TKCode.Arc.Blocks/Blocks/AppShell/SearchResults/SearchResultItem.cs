using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One searchable entry of a <see cref="SearchResults"/>: a document, issue, file, person or anything else your
/// workspace holds. <see cref="Kind"/> decides the facet and the group it is listed under.
/// </summary>
public class SearchResultItem : AvaloniaObject
{
    /// <summary>Stable id; the title is used when it is empty.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<SearchResultItem, string>(nameof(Id), string.Empty);

    /// <summary>The name of the result; searched first and ranked by.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SearchResultItem, string>(nameof(Title), string.Empty);

    /// <summary>A passage of the content; long passages are cut to a window around the first match.</summary>
    public static readonly StyledProperty<string?> SnippetProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Snippet));

    /// <summary>The facet the result belongs to, such as "Docs", "Issues" or "Files"; null lists it under "Other".</summary>
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Kind));

    /// <summary>Where the result lives: a path or a breadcrumb line under the title. Also searched.</summary>
    public static readonly StyledProperty<string?> LocationProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Location));

    /// <summary>A short note on the right of the title, such as "Updated 2d ago". Not searched.</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Meta));

    /// <summary>A 16px icon: a 24×24 Lucide <see cref="Avalonia.Media.Geometry"/> or a control. Default: Lucide FileText.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<SearchResultItem, object?>(nameof(Icon));

    /// <summary>Extra words that also find the result (synonyms, ids, tags).</summary>
    public static readonly StyledProperty<IList<string>?> KeywordsProperty =
        AvaloniaProperty.Register<SearchResultItem, IList<string>?>(nameof(Keywords));

    /// <summary>When false the result is listed dimmed, skipped by the keyboard cursor and cannot be opened.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<SearchResultItem, bool>(nameof(IsEnabled), true);

    /// <summary>Any value you want back when the result is opened.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<SearchResultItem, object?>(nameof(Tag));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SnippetProperty"/>
    public string? Snippet { get => GetValue(SnippetProperty); set => SetValue(SnippetProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public string? Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="LocationProperty"/>
    public string? Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="KeywordsProperty"/>
    public IList<string>? Keywords { get => GetValue(KeywordsProperty); set => SetValue(KeywordsProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    internal string Key => string.IsNullOrEmpty(Id) ? Title : Id;

    /// <summary>The title.</summary>
    public override string ToString() => Title;
}

/// <summary>Arguments of <see cref="SearchResults.ItemInvoked"/>.</summary>
public sealed class SearchResultInvokedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>.</summary>
    public SearchResultInvokedEventArgs(RoutedEvent routedEvent, SearchResultItem item)
        : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The opened result.</summary>
    public SearchResultItem Item { get; }
}
