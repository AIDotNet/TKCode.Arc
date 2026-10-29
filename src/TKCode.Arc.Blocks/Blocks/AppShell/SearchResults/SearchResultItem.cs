using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One searchable entry of a <see cref="SearchResults"/>: a document, a person, a file or anything else your workspace
/// holds. <see cref="Kind"/> and <see cref="Attributes"/> feed the filters, the text properties are searched and shown,
/// and the detail properties fill the sheet that opens when the result is chosen.
/// </summary>
public class SearchResultItem : AvaloniaObject
{
    /// <summary>The attribute key under which <see cref="Kind"/> is filtered (a <see cref="SearchValueFilter"/> with this key is the "Type" filter).</summary>
    public const string KindKey = "kind";

    /// <summary>Stable id; the title is used when it is empty.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<SearchResultItem, string>(nameof(Id), string.Empty);

    /// <summary>The name of the result; searched first, marked where it matches and ranked by.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SearchResultItem, string>(nameof(Title), string.Empty);

    /// <summary>
    /// The line under the title (a person's role and city). When null the row shows "<see cref="KindLabel"/> in
    /// <see cref="Location"/>" (or just the location).
    /// </summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Subtitle));

    /// <summary>A passage of the content; long passages are cut to a window around the first match. Searched.</summary>
    public static readonly StyledProperty<string?> SnippetProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Snippet));

    /// <summary>A last, muted line under the snippet, such as an email address. Not searched.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Caption));

    /// <summary>The type of the result ("Docs", "People", "Files"): the value of the filter whose key is <see cref="KindKey"/>.</summary>
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Kind));

    /// <summary>The singular name of the kind ("Doc") used in the subtitle and the detail sheet; <see cref="Kind"/> when null.</summary>
    public static readonly StyledProperty<string?> KindLabelProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(KindLabel));

    /// <summary>Where the result lives: a path or a breadcrumb ("Engineering / Handbook"). Also searched.</summary>
    public static readonly StyledProperty<string?> LocationProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Location));

    /// <summary>A 20px icon: a 24×24 Lucide <see cref="Avalonia.Media.Geometry"/> or a control. Default: Lucide FileText.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<SearchResultItem, object?>(nameof(Icon));

    /// <summary>A photo for a person: the row and the detail show a round avatar instead of the icon.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<SearchResultItem, ImageSource?>(nameof(Avatar));

    /// <summary>A person's name: the avatar shows its initials (and the photo's fallback). Setting it switches the row to an avatar.</summary>
    public static readonly StyledProperty<string?> AvatarNameProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(AvatarName));

    /// <summary>Extra words that also find the result (synonyms, ids, tags).</summary>
    public static readonly StyledProperty<IList<string>?> KeywordsProperty =
        AvaloniaProperty.Register<SearchResultItem, IList<string>?>(nameof(Keywords));

    /// <summary>When the result was last edited; sorts "Newest" and feeds a <see cref="SearchDateFilter"/>.</summary>
    public static readonly StyledProperty<DateTimeOffset?> DateProperty =
        AvaloniaProperty.Register<SearchResultItem, DateTimeOffset?>(nameof(Date));

    /// <summary>The paragraphs shown in the detail sheet, separated by blank lines. Searched and marked there.</summary>
    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Body));

    /// <summary>The address "Copy link" puts on the clipboard.</summary>
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<SearchResultItem, string?>(nameof(Url));

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

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="SnippetProperty"/>
    public string? Snippet { get => GetValue(SnippetProperty); set => SetValue(SnippetProperty, value); }

    /// <inheritdoc cref="CaptionProperty"/>
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public string? Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="KindLabelProperty"/>
    public string? KindLabel { get => GetValue(KindLabelProperty); set => SetValue(KindLabelProperty, value); }

    /// <inheritdoc cref="LocationProperty"/>
    public string? Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <inheritdoc cref="AvatarNameProperty"/>
    public string? AvatarName { get => GetValue(AvatarNameProperty); set => SetValue(AvatarNameProperty, value); }

    /// <inheritdoc cref="KeywordsProperty"/>
    public IList<string>? Keywords { get => GetValue(KeywordsProperty); set => SetValue(KeywordsProperty, value); }

    /// <inheritdoc cref="DateProperty"/>
    public DateTimeOffset? Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <inheritdoc cref="BodyProperty"/>
    public string? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    /// <inheritdoc cref="UrlProperty"/>
    public string? Url { get => GetValue(UrlProperty); set => SetValue(UrlProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>
    /// Values the filters read, by <see cref="SearchFilter.Key"/> ("team" → "Design"). The type of the result is
    /// <see cref="Kind"/>, not an attribute. Change them before the item is added, or call <see cref="SearchResults.Refresh"/>.
    /// </summary>
    public AvaloniaDictionary<string, string> Attributes { get; } = new();

    /// <summary>The key/value rows of the detail sheet ("Team", "Author", "Length").</summary>
    public AvaloniaList<SearchResultDetail> Details { get; } = new();

    internal string Key => string.IsNullOrEmpty(Id) ? Title : Id;

    /// <summary>The value of the filter <paramref name="key"/>: <see cref="Kind"/> for <see cref="KindKey"/>, otherwise the attribute.</summary>
    internal string? ValueOf(string key)
    {
        if (string.Equals(key, KindKey, StringComparison.OrdinalIgnoreCase))
        {
            return Kind;
        }
        return Attributes.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>The title.</summary>
    public override string ToString() => Title;
}

/// <summary>One key/value row of a result's detail sheet.</summary>
public class SearchResultDetail : AvaloniaObject
{
    /// <summary>The muted name on the left ("Team").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SearchResultDetail, string>(nameof(Label), string.Empty);

    /// <summary>The value on the right.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<SearchResultDetail, string?>(nameof(Value));

    /// <summary>A muted remark after the value ("3 weeks ago").</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<SearchResultDetail, string?>(nameof(Note));

    /// <summary>A person's name: a small avatar with their initials is shown before the value.</summary>
    public static readonly StyledProperty<string?> AvatarNameProperty =
        AvaloniaProperty.Register<SearchResultDetail, string?>(nameof(AvatarName));

    /// <summary>A person's photo for the avatar before the value.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<SearchResultDetail, ImageSource?>(nameof(Avatar));

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="AvatarNameProperty"/>
    public string? AvatarName { get => GetValue(AvatarNameProperty); set => SetValue(AvatarNameProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
}

/// <summary>How the results are ordered.</summary>
public enum SearchSortOrder
{
    /// <summary>Titles that start with the first search word first, then titles with the word inside, then the rest; ties keep the source order.</summary>
    BestMatch,

    /// <summary>The most recent <see cref="SearchResultItem.Date"/> first; items without a date last.</summary>
    Newest,

    /// <summary>By title, A to Z.</summary>
    Title
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

/// <summary>The buttons of the detail sheet.</summary>
public enum SearchResultAction
{
    /// <summary>The primary button ("Open doc").</summary>
    Open,

    /// <summary>The secondary button ("Copy link"): the control already copied <see cref="SearchResultItem.Url"/> when it has one.</summary>
    CopyLink
}

/// <summary>Arguments of <see cref="SearchResults.DetailActionInvoked"/>.</summary>
public sealed class SearchResultActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SearchResultActionEventArgs(RoutedEvent routedEvent, SearchResultItem item, SearchResultAction action)
        : base(routedEvent)
    {
        Item = item;
        Action = action;
    }

    /// <summary>The result whose sheet is open.</summary>
    public SearchResultItem Item { get; }

    /// <summary>The pressed button.</summary>
    public SearchResultAction Action { get; }
}

/// <summary>What changed in a search: the text, a filter or the order.</summary>
public enum SearchChangeReason
{
    /// <summary><see cref="SearchResults.Query"/> changed.</summary>
    Query,

    /// <summary>A filter value was ticked, a date range moved or the filters were cleared.</summary>
    Filters,

    /// <summary><see cref="SearchResults.SortOrder"/> changed.</summary>
    Sort
}

/// <summary>Arguments of <see cref="SearchResults.SearchChanged"/>.</summary>
public sealed class SearchChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SearchChangedEventArgs(RoutedEvent routedEvent, SearchChangeReason reason)
        : base(routedEvent)
    {
        Reason = reason;
    }

    /// <summary>What changed.</summary>
    public SearchChangeReason Reason { get; }
}

/// <summary>Arguments of <see cref="SearchResults.QuerySubmitted"/>.</summary>
public sealed class SearchQuerySubmittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SearchQuerySubmittedEventArgs(RoutedEvent routedEvent, string query)
        : base(routedEvent)
    {
        Query = query;
    }

    /// <summary>The submitted text.</summary>
    public string Query { get; }
}
