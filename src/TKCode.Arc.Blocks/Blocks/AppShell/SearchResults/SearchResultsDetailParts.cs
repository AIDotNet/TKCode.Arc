using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The panel of a modal in a <see cref="SearchResults"/> (the result detail and the narrow filters): a header with a
/// title and a round close button, the scrolling content, and an actions row. In sheet form (<see cref="IsSheet"/>) it
/// carries a grab handle and rounds only its top corners.
/// </summary>
[TemplatePart(CloseButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[PseudoClasses(SheetPseudoClass, HasActionsPseudoClass)]
internal sealed class SearchResultsSurface : ContentControl
{
    public const string CloseButtonPart = "PART_Close";
    public const string ScrollPart = "PART_Scroll";
    public const string SheetPseudoClass = ":sheet";
    public const string HasActionsPseudoClass = ":has-actions";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SearchResultsSurface, string?>(nameof(Title));

    public static readonly StyledProperty<string> CloseTextProperty =
        AvaloniaProperty.Register<SearchResultsSurface, string>(nameof(CloseText), "Close");

    public static readonly StyledProperty<bool> IsSheetProperty =
        AvaloniaProperty.Register<SearchResultsSurface, bool>(nameof(IsSheet));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<SearchResultsSurface, object?>(nameof(Actions));

    private AvaloniaButton? _close;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string CloseText { get => GetValue(CloseTextProperty); set => SetValue(CloseTextProperty, value); }

    /// <summary>Bottom-sheet form: a grab handle, only the top corners round.</summary>
    public bool IsSheet { get => GetValue(IsSheetProperty); set => SetValue(IsSheetProperty, value); }

    /// <summary>The buttons under the content.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>Raised when the close button is pressed.</summary>
    public event EventHandler? CloseRequested;

    internal AvaloniaButton? CloseButton => _close;

    static SearchResultsSurface()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsSurface>(true);
        FocusAdornerProperty.OverrideDefaultValue<SearchResultsSurface>(null);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SearchResultsSurface>(AutomationControlType.Window);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        base.OnApplyTemplate(e);
        _close = e.NameScope.Find<AvaloniaButton>(CloseButtonPart);
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
            AutomationProperties.SetName(_close, CloseText);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSheetProperty)
        {
            PseudoClasses.Set(SheetPseudoClass, IsSheet);
        }
        else if (change.Property == ActionsProperty)
        {
            PseudoClasses.Set(HasActionsPseudoClass, Actions is not null);
        }
        else if (change.Property == CloseTextProperty && _close is not null)
        {
            AutomationProperties.SetName(_close, CloseText);
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>One row of the detail table: a muted label on the left and the value (with an optional avatar and note) on the right.</summary>
internal sealed class SearchResultsDetailRow : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SearchResultsDetailRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<SearchResultsDetailRow, string?>(nameof(Value));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<SearchResultsDetailRow, string?>(nameof(Note));

    public static readonly StyledProperty<object?> AvatarContentProperty =
        AvaloniaProperty.Register<SearchResultsDetailRow, object?>(nameof(AvatarContent));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    public object? AvatarContent { get => GetValue(AvatarContentProperty); set => SetValue(AvatarContentProperty, value); }

    static SearchResultsDetailRow()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsDetailRow>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == ValueProperty || change.Property == NoteProperty)
        {
            AutomationProperties.SetName(this, $"{Label}: {Value}{(string.IsNullOrEmpty(Note) ? string.Empty : ", " + Note)}");
        }
    }
}

/// <summary>
/// The built-in content of the detail sheet: the title with its matches marked, the kind and location, the body
/// paragraphs and the table of details. Replaced when <see cref="SearchResults.DetailTemplate"/> is set.
/// </summary>
[TemplatePart(HeadingPart, typeof(SearchResultsMarkedText))]
[TemplatePart(SubtitlePart, typeof(SearchResultsMarkedText))]
[TemplatePart(ParagraphsPart, typeof(Panel))]
[TemplatePart(TablePart, typeof(Panel))]
internal sealed class SearchResultsDetailBody : TemplatedControl
{
    public const string HeadingPart = "PART_Heading";
    public const string SubtitlePart = "PART_Subtitle";
    public const string ParagraphsPart = "PART_Paragraphs";
    public const string TablePart = "PART_Table";

    private readonly List<SearchMatchRange> _ranges = new();
    private SearchResultsMarkedText? _heading;
    private SearchResultsMarkedText? _subtitle;
    private Panel? _paragraphs;
    private Panel? _table;
    private SearchResultItem? _item;
    private string _subtitleText = string.Empty;
    private IReadOnlyList<string> _terms = [];
    private string _matchesLabel = "Matches";

    static SearchResultsDetailBody()
    {
        FocusableProperty.OverrideDefaultValue<SearchResultsDetailBody>(false);
    }

    internal SearchResultsMarkedText? Heading => _heading;

    internal Panel? ParagraphPanel => _paragraphs;

    internal Panel? TablePanel => _table;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _heading = e.NameScope.Find<SearchResultsMarkedText>(HeadingPart);
        _subtitle = e.NameScope.Find<SearchResultsMarkedText>(SubtitlePart);
        _paragraphs = e.NameScope.Find<Panel>(ParagraphsPart);
        _table = e.NameScope.Find<Panel>(TablePart);
        Rebuild();
    }

    /// <summary>Shows <paramref name="item"/> with the <paramref name="terms"/> marked.</summary>
    public void SetItem(SearchResultItem item, string subtitle, IReadOnlyList<string> terms, string matchesLabel)
    {
        _item = item;
        _subtitleText = subtitle;
        _terms = terms;
        _matchesLabel = matchesLabel;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_item is null)
        {
            return;
        }
        var item = _item;
        var terms = _terms as List<string> ?? _terms.ToList();
        var matches = 0;
        if (_heading is not null)
        {
            SearchResultsMatcher.FindRanges(item.Title.AsSpan(), terms, _ranges);
            matches += _ranges.Count;
            _heading.SetContent(item.Title, _ranges);
        }
        if (_subtitle is not null)
        {
            _subtitle.SetContent(_subtitleText, null);
            _subtitle.IsVisible = _subtitleText.Length > 0;
        }
        if (_paragraphs is not null)
        {
            _paragraphs.Children.Clear();
            var body = !string.IsNullOrWhiteSpace(item.Body) ? item.Body : item.Snippet;
            if (!string.IsNullOrWhiteSpace(body))
            {
                foreach (var paragraph in SplitParagraphs(body))
                {
                    var text = new SearchResultsParagraph();
                    SearchResultsMatcher.FindRanges(paragraph.AsSpan(), terms, _ranges);
                    matches += _ranges.Count;
                    text.SetContent(paragraph, _ranges);
                    _paragraphs.Children.Add(text);
                }
            }
        }
        if (_table is not null)
        {
            _table.Children.Clear();
            if (terms.Count > 0 && matches > 0)
            {
                _table.Children.Add(new SearchResultsDetailRow
                {
                    Label = _matchesLabel,
                    Value = matches.ToString(System.Globalization.CultureInfo.CurrentCulture)
                });
            }
            foreach (var detail in item.Details)
            {
                var row = new SearchResultsDetailRow { Label = detail.Label, Value = detail.Value, Note = detail.Note };
                if (!string.IsNullOrEmpty(detail.AvatarName) || detail.Avatar is not null)
                {
                    row.AvatarContent = new Avatar { DisplayName = detail.AvatarName ?? detail.Value, Source = detail.Avatar, Size = AvatarSize.Small, Width = 22, Height = 22 };
                }
                _table.Children.Add(row);
            }
            _table.IsVisible = _table.Children.Count > 0;
        }
    }

    /// <summary>The paragraphs of <paramref name="text"/>: blocks separated by blank lines, line breaks inside a block folded to spaces.</summary>
    internal static IEnumerable<string> SplitParagraphs(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        foreach (var block in normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var folded = block.Replace('\n', ' ');
            if (folded.Length > 0)
            {
                yield return folded;
            }
        }
    }
}
