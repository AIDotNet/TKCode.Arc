using System.Text.Json;
using System.Text.Json.Nodes;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What a <see cref="JsonViewer"/> copy action put on the clipboard.</summary>
public enum JsonViewerCopyKind
{
    Value,
    Path
}

/// <summary>Arguments of <see cref="JsonViewer.Copied"/>.</summary>
public sealed class JsonViewerCopiedEventArgs : RoutedEventArgs
{
    public JsonViewerCopiedEventArgs(RoutedEvent routedEvent, JsonViewerCopyKind kind, string path, string text) : base(routedEvent)
    {
        Kind = kind;
        Path = path;
        Text = text;
    }

    public JsonViewerCopyKind Kind { get; }

    /// <summary>Path of the row, as in <c>root.users[0].name</c>.</summary>
    public string Path { get; }

    /// <summary>The copied text.</summary>
    public string Text { get; }
}

/// <summary>Arguments of <see cref="JsonViewer.CurrentRowChanged"/> (Arc <c>onSelect</c>).</summary>
public sealed class JsonViewerRowEventArgs : RoutedEventArgs
{
    public JsonViewerRowEventArgs(RoutedEvent routedEvent, string path, JsonElement value, JsonViewerValueType valueType) : base(routedEvent)
    {
        Path = path;
        Value = value;
        ValueType = valueType;
    }

    public string Path { get; }

    public JsonElement Value { get; }

    public JsonViewerValueType ValueType { get; }
}

/// <summary>
/// A collapsible tree for JSON. Values are colored by type, branches unfold row by row on a smooth spring, search
/// highlights every match and opens the branches that hold them, long arrays and objects load in pages, and each
/// row can copy its value or path. Follows the WAI-ARIA tree pattern: arrows move and open, Home/End jump,
/// Enter/Space toggle, Ctrl/Cmd+C copies the current value (Shift: its path) and / focuses the search.
/// Rows are a flat, virtualized list (see <see cref="TablesTreeRowsPanel"/>), so very large documents stay smooth.
/// Input: <see cref="Json"/> (text), <see cref="Value"/> (<see cref="JsonNode"/>) or <see cref="Element"/>.
/// </summary>
[TemplatePart(RowsPart, typeof(JsonViewerRowsPanel))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(SearchPart, typeof(TextBox))]
[TemplatePart(PreviousMatchPart, typeof(AvaloniaButton))]
[TemplatePart(NextMatchPart, typeof(AvaloniaButton))]
[TemplatePart(ExpandAllPart, typeof(AvaloniaButton))]
[TemplatePart(CollapseAllPart, typeof(AvaloniaButton))]
[TemplatePart(PathCopyPart, typeof(AvaloniaButton))]
[PseudoClasses(CompactPseudoClass, SearchingPseudoClass)]
public class JsonViewer : TemplatedControl
{
    public const string RowsPart = "PART_Rows";
    public const string ScrollPart = "PART_Scroll";
    public const string SearchPart = "PART_Search";
    public const string PreviousMatchPart = "PART_PreviousMatch";
    public const string NextMatchPart = "PART_NextMatch";
    public const string ExpandAllPart = "PART_ExpandAll";
    public const string CollapseAllPart = "PART_CollapseAll";
    public const string PathCopyPart = "PART_PathCopy";
    public const string CompactPseudoClass = ":compact";
    public const string SearchingPseudoClass = ":searching";

    #region Properties

    /// <summary>JSON text to show.</summary>
    public static readonly StyledProperty<string?> JsonProperty =
        AvaloniaProperty.Register<JsonViewer, string?>(nameof(Json));

    /// <summary>A <see cref="JsonNode"/> to show.</summary>
    public static readonly StyledProperty<JsonNode?> ValueProperty =
        AvaloniaProperty.Register<JsonViewer, JsonNode?>(nameof(Value));

    /// <summary>A <see cref="JsonElement"/> to show (cloned, so its document may be disposed afterwards).</summary>
    public static readonly StyledProperty<JsonElement?> ElementProperty =
        AvaloniaProperty.Register<JsonViewer, JsonElement?>(nameof(Element));

    /// <summary>Name of the root in paths, as in <c>root.users[0].name</c>.</summary>
    public static readonly StyledProperty<string> RootNameProperty =
        AvaloniaProperty.Register<JsonViewer, string>(nameof(RootName), "root");

    /// <summary>Levels open when data arrives (1 opens the root).</summary>
    public static readonly StyledProperty<int> ExpandDepthProperty =
        AvaloniaProperty.Register<JsonViewer, int>(nameof(ExpandDepth), 1);

    /// <summary>Paths of open branches (two-way).</summary>
    public static readonly DirectProperty<JsonViewer, IReadOnlyList<string>> ExpandedPathsProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, IReadOnlyList<string>>(nameof(ExpandedPaths), o => o.ExpandedPaths,
            (o, v) => o.ExpandedPaths = v, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSearchableProperty =
        AvaloniaProperty.Register<JsonViewer, bool>(nameof(IsSearchable), true);

    /// <summary>Search text (two-way). Matching is case-insensitive over keys and primitive values.</summary>
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<JsonViewer, string?>(nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<JsonViewer, string>(nameof(SearchPlaceholder), "Search keys and values");

    /// <summary>Children shown per page in a long array or object.</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<JsonViewer, int>(nameof(PageSize), 50, coerce: (_, v) => Math.Max(1, v));

    /// <summary>Shows the copy value and copy path actions.</summary>
    public static readonly StyledProperty<bool> IsCopyableProperty =
        AvaloniaProperty.Register<JsonViewer, bool>(nameof(IsCopyable), true);

    /// <summary>Shows the current row's path under the tree.</summary>
    public static readonly StyledProperty<bool> IsPathVisibleProperty =
        AvaloniaProperty.Register<JsonViewer, bool>(nameof(IsPathVisible), true);

    /// <summary>Height of the scrolling tree area.</summary>
    public static readonly StyledProperty<double> MaxTreeHeightProperty =
        AvaloniaProperty.Register<JsonViewer, double>(nameof(MaxTreeHeight), 420);

    public static readonly DirectProperty<JsonViewer, string?> CurrentPathProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, string?>(nameof(CurrentPath), o => o.CurrentPath);

    public static readonly DirectProperty<JsonViewer, int> MatchCountProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, int>(nameof(MatchCount), o => o.MatchCount);

    public static readonly DirectProperty<JsonViewer, string?> ParseErrorProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, string?>(nameof(ParseError), o => o.ParseError);

    public static readonly RoutedEvent<JsonViewerCopiedEventArgs> CopiedEvent =
        RoutedEvent.Register<JsonViewer, JsonViewerCopiedEventArgs>(nameof(Copied), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<JsonViewerRowEventArgs> CurrentRowChangedEvent =
        RoutedEvent.Register<JsonViewer, JsonViewerRowEventArgs>(nameof(CurrentRowChanged), RoutingStrategies.Bubble);

    // Template state.
    internal static readonly DirectProperty<JsonViewer, bool> IsToolbarVisibleProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, bool>(nameof(IsToolbarVisible), o => o.IsToolbarVisible);
    internal static readonly DirectProperty<JsonViewer, bool> HasQueryProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, bool>(nameof(HasQuery), o => o.HasQuery);
    internal static readonly DirectProperty<JsonViewer, string> MatchSummaryProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, string>(nameof(MatchSummary), o => o.MatchSummary);
    internal static readonly DirectProperty<JsonViewer, string> CurrentTypeLabelProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, string>(nameof(CurrentTypeLabel), o => o.CurrentTypeLabel);
    internal static readonly DirectProperty<JsonViewer, bool> IsPathBarVisibleProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, bool>(nameof(IsPathBarVisible), o => o.IsPathBarVisible);
    internal static readonly DirectProperty<JsonViewer, bool> CanCopyPathProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, bool>(nameof(CanCopyPath), o => o.CanCopyPath);
    internal static readonly DirectProperty<JsonViewer, Control?> PathCopyGlyphProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, Control?>(nameof(PathCopyGlyph), o => o.PathCopyGlyph);
    internal static readonly DirectProperty<JsonViewer, string> AnnouncementProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, string>(nameof(Announcement), o => o.Announcement);
    internal static readonly DirectProperty<JsonViewer, bool> HasErrorProperty =
        AvaloniaProperty.RegisterDirect<JsonViewer, bool>(nameof(HasError), o => o.HasError);

    // Row colors and metrics, set by the theme.
    internal static readonly StyledProperty<IBrush?> RowForegroundProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(RowForeground));
    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(MutedForeground));
    internal static readonly StyledProperty<IBrush?> SecondaryForegroundProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(SecondaryForeground));
    internal static readonly StyledProperty<IBrush?> StringBrushProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(StringBrush));
    internal static readonly StyledProperty<IBrush?> NumberBrushProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(NumberBrush));
    internal static readonly StyledProperty<IBrush?> BooleanBrushProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(BooleanBrush));
    internal static readonly StyledProperty<IBrush?> HighlightFillProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(HighlightFill));
    internal static readonly StyledProperty<IBrush?> RowHoverFillProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(RowHoverFill));
    internal static readonly StyledProperty<IBrush?> MarkFillProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(MarkFill));
    internal static readonly StyledProperty<IBrush?> MarkCurrentFillProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(MarkCurrentFill));
    internal static readonly StyledProperty<IBrush?> ActionPressedFillProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(ActionPressedFill));
    internal static readonly StyledProperty<IBrush?> SuccessBrushProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(SuccessBrush));
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(DangerBrush));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty = AvaloniaProperty.Register<JsonViewer, IBrush?>(nameof(FocusRingBrush));
    internal static readonly StyledProperty<FontFamily?> MonoFontFamilyProperty = AvaloniaProperty.Register<JsonViewer, FontFamily?>(nameof(MonoFontFamily));
    internal static readonly StyledProperty<double> TreeFontSizeProperty = AvaloniaProperty.Register<JsonViewer, double>(nameof(TreeFontSize), 13);
    internal static readonly StyledProperty<double> SmallFontSizeProperty = AvaloniaProperty.Register<JsonViewer, double>(nameof(SmallFontSize), 12);
    internal static readonly StyledProperty<double> RowSlotHeightProperty = AvaloniaProperty.Register<JsonViewer, double>(nameof(RowSlotHeight), 30);
    internal static readonly StyledProperty<double> IndentWidthProperty = AvaloniaProperty.Register<JsonViewer, double>(nameof(IndentWidth), 16);
    internal static readonly StyledProperty<double> CompactWidthProperty = AvaloniaProperty.Register<JsonViewer, double>(nameof(CompactWidth), 420);

    private static readonly AvaloniaProperty[] s_paletteProperties =
    [
        RowForegroundProperty, MutedForegroundProperty, SecondaryForegroundProperty, StringBrushProperty, NumberBrushProperty,
        BooleanBrushProperty, HighlightFillProperty, RowHoverFillProperty, MarkFillProperty, MarkCurrentFillProperty,
        ActionPressedFillProperty, SuccessBrushProperty, DangerBrushProperty, FocusRingBrushProperty, MonoFontFamilyProperty,
        TreeFontSizeProperty, SmallFontSizeProperty, RowSlotHeightProperty, IndentWidthProperty, FontFamilyProperty, BackgroundProperty
    ];

    public string? Json { get => GetValue(JsonProperty); set => SetValue(JsonProperty, value); }
    public JsonNode? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public JsonElement? Element { get => GetValue(ElementProperty); set => SetValue(ElementProperty, value); }
    public string RootName { get => GetValue(RootNameProperty); set => SetValue(RootNameProperty, value); }
    public int ExpandDepth { get => GetValue(ExpandDepthProperty); set => SetValue(ExpandDepthProperty, value); }
    public bool IsSearchable { get => GetValue(IsSearchableProperty); set => SetValue(IsSearchableProperty, value); }
    public string? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }
    public bool IsCopyable { get => GetValue(IsCopyableProperty); set => SetValue(IsCopyableProperty, value); }
    public bool IsPathVisible { get => GetValue(IsPathVisibleProperty); set => SetValue(IsPathVisibleProperty, value); }
    public double MaxTreeHeight { get => GetValue(MaxTreeHeightProperty); set => SetValue(MaxTreeHeightProperty, value); }

    private IReadOnlyList<string> _expandedPaths = Array.Empty<string>();

    /// <summary>Paths of open branches (two-way; setting it replaces the open set).</summary>
    public IReadOnlyList<string> ExpandedPaths
    {
        get => _expandedPaths;
        set
        {
            if (ReferenceEquals(_expandedPaths, value))
            {
                return;
            }
            SetAndRaise(ExpandedPathsProperty, ref _expandedPaths, value ?? Array.Empty<string>());
            if (!_publishingExpanded)
            {
                _expanded.Clear();
                _expanded.UnionWith(_expandedPaths);
                Rebuild(animate: true);
            }
        }
    }

    private string? _currentPath;

    /// <summary>Path of the current row.</summary>
    public string? CurrentPath { get => _currentPath; private set => SetAndRaise(CurrentPathProperty, ref _currentPath, value); }

    private int _matchCount;

    /// <summary>Number of search matches (at most 2000).</summary>
    public int MatchCount { get => _matchCount; private set => SetAndRaise(MatchCountProperty, ref _matchCount, value); }

    private string? _parseError;

    /// <summary>Why <see cref="Json"/> could not be parsed, or null.</summary>
    public string? ParseError { get => _parseError; private set => SetAndRaise(ParseErrorProperty, ref _parseError, value); }

    public event EventHandler<JsonViewerCopiedEventArgs>? Copied
    {
        add => AddHandler(CopiedEvent, value);
        remove => RemoveHandler(CopiedEvent, value);
    }

    /// <summary>Raised when a row becomes the current one (Arc <c>onSelect</c>).</summary>
    public event EventHandler<JsonViewerRowEventArgs>? CurrentRowChanged
    {
        add => AddHandler(CurrentRowChangedEvent, value);
        remove => RemoveHandler(CurrentRowChangedEvent, value);
    }

    /// <summary>Raised after the set of open branches changed.</summary>
    public event EventHandler? ExpandedPathsChanged;

    private bool _isToolbarVisible = true;
    internal bool IsToolbarVisible { get => _isToolbarVisible; private set => SetAndRaise(IsToolbarVisibleProperty, ref _isToolbarVisible, value); }
    private bool _hasQuery;
    internal bool HasQuery { get => _hasQuery; private set => SetAndRaise(HasQueryProperty, ref _hasQuery, value); }
    private string _matchSummary = "0/0";
    internal string MatchSummary { get => _matchSummary; private set => SetAndRaise(MatchSummaryProperty, ref _matchSummary, value); }
    private string _currentTypeLabel = string.Empty;
    internal string CurrentTypeLabel { get => _currentTypeLabel; private set => SetAndRaise(CurrentTypeLabelProperty, ref _currentTypeLabel, value); }
    private bool _isPathBarVisible;
    internal bool IsPathBarVisible { get => _isPathBarVisible; private set => SetAndRaise(IsPathBarVisibleProperty, ref _isPathBarVisible, value); }
    private bool _canCopyPath;
    internal bool CanCopyPath { get => _canCopyPath; private set => SetAndRaise(CanCopyPathProperty, ref _canCopyPath, value); }
    private Control? _pathCopyGlyph;
    internal Control? PathCopyGlyph { get => _pathCopyGlyph; private set => SetAndRaise(PathCopyGlyphProperty, ref _pathCopyGlyph, value); }
    private string _announcement = string.Empty;
    internal string Announcement { get => _announcement; private set => SetAndRaise(AnnouncementProperty, ref _announcement, value); }
    private bool _hasError;
    internal bool HasError { get => _hasError; private set => SetAndRaise(HasErrorProperty, ref _hasError, value); }

    internal IBrush? RowForeground { get => GetValue(RowForegroundProperty); set => SetValue(RowForegroundProperty, value); }
    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    internal IBrush? SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }
    internal IBrush? StringBrush { get => GetValue(StringBrushProperty); set => SetValue(StringBrushProperty, value); }
    internal IBrush? NumberBrush { get => GetValue(NumberBrushProperty); set => SetValue(NumberBrushProperty, value); }
    internal IBrush? BooleanBrush { get => GetValue(BooleanBrushProperty); set => SetValue(BooleanBrushProperty, value); }
    internal IBrush? HighlightFill { get => GetValue(HighlightFillProperty); set => SetValue(HighlightFillProperty, value); }
    internal IBrush? RowHoverFill { get => GetValue(RowHoverFillProperty); set => SetValue(RowHoverFillProperty, value); }
    internal IBrush? MarkFill { get => GetValue(MarkFillProperty); set => SetValue(MarkFillProperty, value); }
    internal IBrush? MarkCurrentFill { get => GetValue(MarkCurrentFillProperty); set => SetValue(MarkCurrentFillProperty, value); }
    internal IBrush? ActionPressedFill { get => GetValue(ActionPressedFillProperty); set => SetValue(ActionPressedFillProperty, value); }
    internal IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    internal FontFamily? MonoFontFamily { get => GetValue(MonoFontFamilyProperty); set => SetValue(MonoFontFamilyProperty, value); }
    internal double TreeFontSize { get => GetValue(TreeFontSizeProperty); set => SetValue(TreeFontSizeProperty, value); }
    internal double SmallFontSize { get => GetValue(SmallFontSizeProperty); set => SetValue(SmallFontSizeProperty, value); }
    internal double RowSlotHeight { get => GetValue(RowSlotHeightProperty); set => SetValue(RowSlotHeightProperty, value); }
    internal double IndentWidth { get => GetValue(IndentWidthProperty); set => SetValue(IndentWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    private JsonDocument? _document;
    private JsonViewerNode? _root;
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pages = new(StringComparer.Ordinal);
    private JsonViewerSearch? _search;
    private string _closedNeedle = string.Empty;
    private readonly HashSet<string> _closedWhileSearching = new(StringComparer.Ordinal);
    private int _matchIndex;
    private List<object> _rows = new();
    private readonly Dictionary<string, int> _rowIndex = new(StringComparer.Ordinal);
    private string _activeRaw = "root";
    private string? _active;
    private string? _lastReported;
    private bool _publishingExpanded;
    private JsonViewerRowsPanel? _panel;
    private ScrollViewer? _scroll;
    private TextBox? _searchBox;
    private AvaloniaButton? _previous;
    private AvaloniaButton? _next;
    private AvaloniaButton? _expandAll;
    private AvaloniaButton? _collapseAll;
    private AvaloniaButton? _pathCopy;
    private MotionValue<double>? _scrollMotion;
    private PausableTimer? _pathCopyReset;

    internal JsonViewerPalette Palette { get; } = new();

    static JsonViewer()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<JsonViewer>(AutomationLandmarkType.Region);
    }

    public JsonViewer()
    {
        AutomationProperties.SetName(this, "JSON");
        PathCopyGlyph = CreatePathGlyph(CopyState.Idle);
    }

    internal JsonViewerRowsPanel? RowsPanel => _panel;

    internal IReadOnlyList<object> VisibleRows => _rows;

    internal JsonViewerSearch? Search => _search;

    internal string? ActiveId => _active;

    internal int MatchIndex => _matchIndex;

    internal string? CurrentMatch => _search is { Matches.Count: > 0 } s ? s.Matches[Math.Min(_matchIndex, s.Matches.Count - 1)] : null;

    #region Public API

    /// <summary>Opens every branch.</summary>
    public void ExpandAll()
    {
        if (_root is null)
        {
            return;
        }
        _expanded.Clear();
        _expanded.UnionWith(JsonViewerPaths.AllBranches(_root.Element, RootName));
        _closedNeedle = Needle;
        _closedWhileSearching.Clear();
        PublishExpanded();
        Rebuild(animate: true);
        // Branches above the current row unfold and push it down; follow it to where it settles.
        MoveTo(_active ?? RootName, focus: false, RevealMode.Nearest);
    }

    /// <summary>Closes everything but the root.</summary>
    public void CollapseAll()
    {
        if (_root is null)
        {
            return;
        }
        _expanded.Clear();
        if (_root.IsBranch)
        {
            _expanded.Add(RootName);
        }
        _closedNeedle = Needle;
        _closedWhileSearching.Clear();
        if (_search is not null)
        {
            foreach (var id in _search.Ancestors)
            {
                if (id != RootName)
                {
                    _closedWhileSearching.Add(id);
                }
            }
        }
        _pages.Clear();
        PublishExpanded();
        Rebuild(animate: true);
        MoveTo(RootName, focus: false, RevealMode.Nearest);
    }

    /// <summary>Opens the branch at <paramref name="path"/>.</summary>
    public void Expand(string path) => Toggle(path, true);

    /// <summary>Closes the branch at <paramref name="path"/>.</summary>
    public void Collapse(string path) => Toggle(path, false);

    /// <summary>Moves to the next search match.</summary>
    public void NextMatch() => StepMatch(1);

    /// <summary>Moves to the previous search match.</summary>
    public void PreviousMatch() => StepMatch(-1);

    /// <summary>Makes the row at <paramref name="path"/> current and moves keyboard focus to it.</summary>
    public void FocusPath(string path) => MoveTo(path, focus: true, RevealMode.Nearest);

    #endregion

    #region Data

    private string Needle => (Query ?? string.Empty).Trim();

    private void LoadFromJson(string? json)
    {
        if (json is null)
        {
            SetDocument(null, null);
            return;
        }
        try
        {
            var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            SetDocument(document, null);
        }
        catch (JsonException ex)
        {
            SetDocument(null, ex.Message);
        }
    }

    private void LoadFromNode(JsonNode? node)
    {
        if (node is null)
        {
            SetDocument(JsonDocument.Parse("null"), null);
            return;
        }
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            node.WriteTo(writer);
        }
        SetDocument(JsonDocument.Parse(stream.ToArray()), null);
    }

    private void SetDocument(JsonDocument? document, string? error)
    {
        _document?.Dispose();
        _document = document;
        SetRoot(document?.RootElement, error);
    }

    private void SetRoot(JsonElement? element, string? error)
    {
        ParseError = error;
        HasError = error is not null;
        _root = element is { } root ? new JsonViewerNode(root, RootName, null, -1, null, 1) : null;
        _expanded.Clear();
        _pages.Clear();
        _closedWhileSearching.Clear();
        _activeRaw = RootName;
        _lastReported = null;
        if (_root is not null)
        {
            _expanded.UnionWith(JsonViewerPaths.AllBranches(_root.Element, RootName, Math.Max(0, ExpandDepth)));
        }
        RunSearch();
        PublishExpanded();
        Rebuild(animate: false);
        _scrollMotion?.Jump(0);
        if (_scroll is not null)
        {
            _scroll.Offset = default;
        }
    }

    private void RunSearch()
    {
        var needle = Needle;
        _search = _root is not null && needle.Length > 0 ? JsonViewerSearch.Run(_root.Element, RootName, needle) : null;
        MatchCount = _search?.Matches.Count ?? 0;
        HasQuery = needle.Length > 0;
        PseudoClasses.Set(SearchingPseudoClass, HasQuery);
        UpdateMatchSummary();
    }

    private void UpdateMatchSummary()
    {
        MatchSummary = MatchCount > 0
            ? $"{Math.Min(_matchIndex, MatchCount - 1) + 1}/{MatchCount}"
            : "0/0";
        if (_previous is not null)
        {
            _previous.IsEnabled = MatchCount > 0;
        }
        if (_next is not null)
        {
            _next.IsEnabled = MatchCount > 0;
        }
    }

    private void PublishExpanded()
    {
        _publishingExpanded = true;
        try
        {
            ExpandedPaths = _expanded.ToArray();
        }
        finally
        {
            _publishingExpanded = false;
        }
        ExpandedPathsChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Rows

    internal bool IsOpen(string id)
    {
        if (_search is not null && _search.Ancestors.Contains(id))
        {
            return !(_closedNeedle == Needle && _closedWhileSearching.Contains(id));
        }
        return _expanded.Contains(id);
    }

    private int LimitOf(string id)
    {
        var pages = _pages.TryGetValue(id, out var p) ? p : 1;
        var needed = _search is not null && _search.Needed.TryGetValue(id, out var n) ? n : 0;
        return Math.Max(pages * PageSize, needed);
    }

    private void Rebuild(bool animate)
    {
        var rows = new List<object>(Math.Max(16, _rows.Count));
        _rowIndex.Clear();
        if (_root is not null)
        {
            Visit(_root, 1, 1, rows);
        }
        _rows = rows;
        IsToolbarVisible = IsSearchable || rows.Count > 1;
        var (active, folded) = ResolveActive();
        _active = active;
        if (_panel is not null)
        {
            _panel.PinnedKey = active;
            var hadFocus = IsFocusOnRow();
            _panel.SetRows(rows, animate && IsLoaded);
            if (!Equals(_panel.HighlightKey, active) || folded)
            {
                _panel.SetHighlight(active, animate: !folded);
            }
            if (hadFocus && folded)
            {
                FocusActiveRow();
            }
            foreach (var row in _panel.RealizedRows)
            {
                row.Refresh();
            }
        }
        UpdateCurrent();
    }

    private void Visit(JsonViewerNode node, int position, int size, List<object> rows)
    {
        var open = node.IsBranch && IsOpen(node.Path);
        _rowIndex[node.Path] = rows.Count;
        rows.Add(new JsonViewerRowModel(node.Path, node, null, node.Level, open, position, size, 0));
        if (!open)
        {
            return;
        }
        var count = node.Count;
        var shown = Math.Min(count, LimitOf(node.Path));
        var more = count - shown;
        var setSize = shown + (more > 0 ? 1 : 0);
        var children = node.Children(shown);
        for (var i = 0; i < shown; i++)
        {
            Visit(children[i], i + 1, setSize, rows);
        }
        if (more > 0)
        {
            var id = node.Path + "::more";
            _rowIndex[id] = rows.Count;
            rows.Add(new JsonViewerRowModel(id, null, node, node.Level + 1, false, setSize, setSize, more));
        }
    }

    /// <summary>The current row; when it is folded away, the nearest visible branch that holds it takes over.</summary>
    private (string? Active, bool Folded) ResolveActive()
    {
        if (_rows.Count == 0)
        {
            return (null, false);
        }
        if (_rowIndex.ContainsKey(_activeRaw))
        {
            return (_activeRaw, false);
        }
        var best = RootName;
        foreach (var model in _rows)
        {
            var row = (JsonViewerRowModel)model;
            if (row.Node is not null && JsonViewerPaths.Within(row.Id, _activeRaw) && row.Id.Length > best.Length)
            {
                best = row.Id;
            }
        }
        return (_rowIndex.ContainsKey(best) ? best : ((JsonViewerRowModel)_rows[0]).Id, true);
    }

    internal JsonViewerRowModel? ActiveRow => _active is not null && _rowIndex.TryGetValue(_active, out var i) ? (JsonViewerRowModel)_rows[i] : null;

    internal int IndexOfId(string id) => _rowIndex.TryGetValue(id, out var i) ? i : -1;

    private void UpdateCurrent()
    {
        var row = ActiveRow;
        CurrentPath = row is null ? null : row.Node is not null ? row.Id : row.Owner?.Path;
        CurrentTypeLabel = row switch
        {
            null => string.Empty,
            { Node: null } => "page",
            { Node: { IsBranch: true } node } => $"{JsonViewerPaths.TypeName(node.Type)}, {JsonViewerPaths.CountLabel(node.Type, node.Count)}",
            { Node: { } node } => JsonViewerPaths.TypeName(node.Type)
        };
        IsPathBarVisible = IsPathVisible && row is not null;
        CanCopyPath = IsCopyable && row?.Node is not null;
        if (row?.Node is { } current && _lastReported != row.Id)
        {
            _lastReported = row.Id;
            RaiseEvent(new JsonViewerRowEventArgs(CurrentRowChangedEvent, current.Path, current.Element, current.Type));
        }
    }

    #endregion

    #region Navigation

    private enum RevealMode
    {
        None,
        Nearest,
        Center
    }

    private void MoveTo(string id, bool focus, RevealMode reveal)
    {
        _activeRaw = id;
        var (active, folded) = ResolveActive();
        _active = active;
        if (_panel is not null && active is not null)
        {
            _panel.PinnedKey = active;
            _panel.SetHighlight(active, animate: !folded);
            foreach (var row in _panel.RealizedRows)
            {
                row.Refresh();
            }
        }
        UpdateCurrent();
        if (focus)
        {
            FocusActiveRow();
        }
        if (reveal != RevealMode.None && active is not null)
        {
            Reveal(IndexOfId(active), reveal == RevealMode.Center);
        }
    }

    private bool IsFocusOnRow() => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is JsonViewerRow row && row.Owner == this;

    private void FocusActiveRow()
    {
        if (_panel is null || _active is null)
        {
            return;
        }
        _panel.Realize(IndexOfId(_active))?.Focus(NavigationMethod.Directional);
    }

    /// <summary>
    /// Keeps the current row in view, springing (P30, smooth) to where it will sit once rows finish unfolding.
    /// Code-driven: the target is computed per navigation and the motion must be retargetable mid-flight.
    /// </summary>
    private void Reveal(int index, bool center)
    {
        if (_scroll is null || _panel is null || index < 0)
        {
            return;
        }
        var rowHeight = _panel.RowHeight;
        var offsetTop = _panel.Margin.Top;
        var top = offsetTop + _panel.SettledRowTop(index);
        var bottom = top + rowHeight;
        var view = _scroll.Viewport.Height;
        var now = _scroll.Offset.Y;
        if (view <= 0)
        {
            return;
        }
        var target = now;
        if (center)
        {
            target = top - view / 2 + rowHeight / 2;
        }
        else if (top < now + 4)
        {
            target = top - 4;
        }
        else if (bottom > now + view - 4)
        {
            target = bottom - view + 4;
        }
        target = Math.Max(0, target);
        if (Math.Abs(target - now) < 1)
        {
            return;
        }
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full || !IsLoaded)
        {
            _scrollMotion?.Jump(target);
            _scroll.Offset = new Vector(_scroll.Offset.X, target);
            return;
        }
        if (_scrollMotion is null)
        {
            _scrollMotion = new MotionValue<double>(this, now, MotionVectors.Double);
            _scrollMotion.Changed += v =>
            {
                if (_scroll is not null)
                {
                    _scroll.Offset = new Vector(_scroll.Offset.X, v);
                }
            };
        }
        if (!_scrollMotion.IsAnimating)
        {
            _scrollMotion.Jump(now);
        }
        _ = _scrollMotion.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    internal void Toggle(string id, bool? next = null)
    {
        var open = IsOpen(id);
        var want = next ?? !open;
        if (want == open || _root is null)
        {
            return;
        }
        if (_search is not null && _search.Ancestors.Contains(id))
        {
            if (_closedNeedle != Needle)
            {
                _closedNeedle = Needle;
                _closedWhileSearching.Clear();
            }
            if (want)
            {
                _closedWhileSearching.Remove(id);
            }
            else
            {
                _closedWhileSearching.Add(id);
            }
            if (want && _expanded.Add(id))
            {
                PublishExpanded();
            }
            Rebuild(animate: true);
            return;
        }
        if (want)
        {
            _expanded.Add(id);
        }
        else
        {
            _expanded.Remove(id);
        }
        PublishExpanded();
        Rebuild(animate: true);
    }

    internal void ShowMore(JsonViewerNode parent)
    {
        _pages[parent.Path] = (int)Math.Ceiling(LimitOf(parent.Path) / (double)PageSize) + 1;
        Rebuild(animate: true);
    }

    private void StepMatch(int step)
    {
        if (_search is null || _search.Matches.Count == 0)
        {
            return;
        }
        var count = _search.Matches.Count;
        _matchIndex = ((Math.Min(_matchIndex, count - 1) + step) % count + count) % count;
        UpdateMatchSummary();
        _activeRaw = _search.Matches[_matchIndex];
        RefreshRows();
        MoveTo(_search.Matches[_matchIndex], focus: false, RevealMode.Center);
    }

    private void RefreshRows()
    {
        if (_panel is null)
        {
            return;
        }
        foreach (var row in _panel.RealizedRows)
        {
            row.Refresh();
        }
    }

    private void OnQueryChanged(string? previous)
    {
        var hadNeedle = !string.IsNullOrWhiteSpace(previous);
        _matchIndex = 0;
        RunSearch();
        if (_search is { Matches.Count: > 0 } search)
        {
            _activeRaw = search.Matches[0];
            Rebuild(animate: true);
            MoveTo(search.Matches[0], focus: false, RevealMode.Center);
        }
        else
        {
            Rebuild(animate: true);
            if (hadNeedle && Needle.Length == 0)
            {
                // Clearing folds the branches search opened; keep the current row in view where it settles.
                MoveTo(_activeRaw, focus: false, RevealMode.Nearest);
            }
        }
    }

    internal void OnRowClicked(JsonViewerRow row)
    {
        if (row.Model is not JsonViewerRowModel model)
        {
            return;
        }
        if (model.Node is null && model.Owner is { } owner)
        {
            ShowMore(owner);
            MoveTo(model.Id, focus: true, RevealMode.Nearest);
            return;
        }
        if (model.Node is { IsBranch: true })
        {
            Toggle(model.Id);
        }
        _panel?.Realize(IndexOfId(model.Id))?.Focus(NavigationMethod.Pointer);
        MoveTo(model.Id, focus: false, RevealMode.Nearest);
    }

    internal void OnRowFocused(JsonViewerRow row)
    {
        if (row.Model is JsonViewerRowModel model && model.Id != _active)
        {
            _activeRaw = model.Id;
            MoveTo(model.Id, focus: false, RevealMode.None);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not JsonViewerRow || ActiveRow is not { } row || (e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            return;
        }
        var mod = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (mod)
        {
            if (e.Key == Key.C && row.Node is not null)
            {
                e.Handled = true;
                _ = CopyAsync(row, (e.KeyModifiers & KeyModifiers.Shift) != 0 ? JsonViewerCopyKind.Path : JsonViewerCopyKind.Value);
            }
            return;
        }
        var index = IndexOfId(row.Id);
        void Go(int target)
        {
            e.Handled = true;
            target = Math.Clamp(target, 0, _rows.Count - 1);
            MoveTo(((JsonViewerRowModel)_rows[target]).Id, focus: true, RevealMode.Nearest);
        }
        switch (e.Key)
        {
            case Key.Down:
                Go(index + 1);
                break;
            case Key.Up:
                Go(index - 1);
                break;
            case Key.Home:
                Go(0);
                break;
            case Key.End:
                Go(_rows.Count - 1);
                break;
            case Key.Right:
                e.Handled = true;
                if (row.Node is { IsBranch: true } branch)
                {
                    if (!row.IsOpen)
                    {
                        Toggle(row.Id, true);
                    }
                    else if (branch.Count > 0)
                    {
                        Go(index + 1);
                    }
                }
                break;
            case Key.Left:
                e.Handled = true;
                if (row.Node is not null && row.IsOpen)
                {
                    Toggle(row.Id, false);
                }
                else if ((row.Node?.Parent ?? row.Owner) is { } parent)
                {
                    MoveTo(parent.Path, focus: true, RevealMode.Nearest);
                }
                break;
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (row.Node is null && row.Owner is { } owner)
                {
                    ShowMore(owner);
                }
                else if (row.Node is { IsBranch: true })
                {
                    Toggle(row.Id);
                }
                break;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!e.Handled && e.Text == "/" && e.Source is JsonViewerRow && IsSearchable && _searchBox is not null)
        {
            e.Handled = true;
            _searchBox.Focus(NavigationMethod.Tab);
            _searchBox.SelectAll();
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                StepMatch((e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1);
                break;
            case Key.Escape when !string.IsNullOrEmpty(Query):
                e.Handled = true;
                SetCurrentValue(QueryProperty, string.Empty);
                break;
            case Key.Down when (e.KeyModifiers & KeyModifiers.Alt) == 0:
                e.Handled = true;
                FocusActiveRow();
                break;
        }
    }

    #endregion

    #region Copy

    internal async Task<bool> CopyAsync(JsonViewerRowModel row, JsonViewerCopyKind kind)
    {
        if (row.Node is not { } node)
        {
            return false;
        }
        var text = kind == JsonViewerCopyKind.Path ? node.Path : JsonViewerPaths.CopyText(node.Element);
        var ok = await ArcClipboard.CopyTextAsync(this, text);
        if (ok)
        {
            Announcement = kind == JsonViewerCopyKind.Path ? $"Copied path {node.Path}" : $"Copied value of {node.Path}";
            RaiseEvent(new JsonViewerCopiedEventArgs(CopiedEvent, kind, node.Path, text));
        }
        else
        {
            Announcement = "Copy failed";
        }
        return ok;
    }

    private async void OnPathCopyClick(object? sender, RoutedEventArgs e)
    {
        if (ActiveRow is not { Node: not null } row)
        {
            return;
        }
        var ok = await CopyAsync(row, JsonViewerCopyKind.Path);
        PathCopyGlyph = CreatePathGlyph(ok ? CopyState.Copied : CopyState.Error);
        if (_pathCopy is not null)
        {
            AutomationProperties.SetName(_pathCopy, ok ? "Copied" : "Copy failed");
        }
        _pathCopyReset ??= new PausableTimer(TimeSpan.FromMilliseconds(1400), () =>
        {
            PathCopyGlyph = CreatePathGlyph(CopyState.Idle);
            if (_pathCopy is not null)
            {
                AutomationProperties.SetName(_pathCopy, "Copy path");
            }
        });
        _pathCopyReset.Restart(TimeSpan.FromMilliseconds(1400));
    }

    private static Control CreatePathGlyph(CopyState state)
    {
        var glyph = new ArcGlyph
        {
            Size = 14,
            StrokeThickness = state == CopyState.Idle ? 1.75 : 2,
            Data = state switch
            {
                CopyState.Copied => ArcIcons.Check,
                CopyState.Error => ArcIcons.X,
                _ => ArcIcons.Copy
            }
        };
        if (state != CopyState.Idle)
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, state == CopyState.Copied ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Danger);
        }
        return glyph;
    }

    #endregion

    #region Lifecycle

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_searchBox is not null)
        {
            _searchBox.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
        }
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_expandAll is not null)
        {
            _expandAll.Click -= OnExpandAllClick;
        }
        if (_collapseAll is not null)
        {
            _collapseAll.Click -= OnCollapseAllClick;
        }
        if (_pathCopy is not null)
        {
            _pathCopy.Click -= OnPathCopyClick;
        }
        if (_scroll is not null)
        {
            _scroll.RemoveHandler(PointerWheelChangedEvent, OnScrollWheel);
        }
        if (_panel is not null)
        {
            _panel.Owner = null;
        }
        _panel = e.NameScope.Find<JsonViewerRowsPanel>(RowsPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _searchBox = e.NameScope.Find<TextBox>(SearchPart);
        _previous = e.NameScope.Find<AvaloniaButton>(PreviousMatchPart);
        _next = e.NameScope.Find<AvaloniaButton>(NextMatchPart);
        _expandAll = e.NameScope.Find<AvaloniaButton>(ExpandAllPart);
        _collapseAll = e.NameScope.Find<AvaloniaButton>(CollapseAllPart);
        _pathCopy = e.NameScope.Find<AvaloniaButton>(PathCopyPart);
        _searchBox?.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_expandAll is not null)
        {
            _expandAll.Click += OnExpandAllClick;
        }
        if (_collapseAll is not null)
        {
            _collapseAll.Click += OnCollapseAllClick;
        }
        if (_pathCopy is not null)
        {
            _pathCopy.Click += OnPathCopyClick;
        }
        _scroll?.AddHandler(PointerWheelChangedEvent, OnScrollWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
        UpdatePalette();
        UpdateMatchSummary();
        if (_panel is not null)
        {
            _panel.Owner = this;
            _panel.Highlight.Fill = HighlightFill;
            // Arc: scroll, row heights and the current-row highlight share the smooth spring.
            _panel.Highlight.Spring = ArcSpring.Smooth;
            _panel.Highlight.CornerRadius = new CornerRadius(9);
            _panel.HighlightInset = new Thickness(0, 1, 0, 1);
            _panel.PinnedKey = _active;
            _panel.SetRows(_rows, animate: false);
            _panel.SetHighlight(_active, animate: false);
        }
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => StepMatch(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => StepMatch(1);

    private void OnExpandAllClick(object? sender, RoutedEventArgs e) => ExpandAll();

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e) => CollapseAll();

    private void OnScrollWheel(object? sender, PointerWheelEventArgs e) => _scrollMotion?.Stop();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == JsonProperty)
        {
            LoadFromJson(Json);
        }
        else if (change.Property == ValueProperty)
        {
            LoadFromNode(Value);
        }
        else if (change.Property == ElementProperty)
        {
            _document?.Dispose();
            _document = null;
            SetRoot(Element?.Clone(), null);
        }
        else if (change.Property == RootNameProperty || change.Property == ExpandDepthProperty)
        {
            if (_root is not null)
            {
                SetRoot(_root.Element, ParseError);
            }
        }
        else if (change.Property == QueryProperty)
        {
            OnQueryChanged(change.GetOldValue<string?>());
        }
        else if (change.Property == PageSizeProperty || change.Property == IsSearchableProperty)
        {
            Rebuild(animate: false);
        }
        else if (change.Property == IsPathVisibleProperty || change.Property == IsCopyableProperty)
        {
            UpdateCurrent();
            RefreshRows();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            var width = Bounds.Width;
            var compact = width > 0 && width <= CompactWidth;
            if (compact != Classes.Contains(CompactPseudoClass))
            {
                PseudoClasses.Set(CompactPseudoClass, compact);
                UpdatePalette();
            }
        }
        else if (Array.IndexOf(s_paletteProperties, change.Property) >= 0)
        {
            UpdatePalette();
        }
    }

    private void UpdatePalette()
    {
        var p = Palette;
        p.Foreground = RowForeground;
        p.Muted = MutedForeground;
        p.Secondary = SecondaryForeground;
        p.String = StringBrush;
        p.Number = NumberBrush;
        p.Boolean = BooleanBrush;
        p.Highlight = HighlightFill;
        p.HoverFill = RowHoverFill;
        p.Mark = MarkFill;
        p.MarkCurrent = MarkCurrentFill;
        p.ActionPressed = ActionPressedFill;
        p.Success = SuccessBrush;
        p.Danger = DangerBrush;
        p.Surface = Background;
        p.FocusRing = FocusRingBrush is { } ring ? new Pen(ring, 2) : null;
        p.MonoFont = MonoFontFamily ?? FontFamily.Default;
        p.BodyFont = FontFamily;
        p.TreeFontSize = TreeFontSize;
        p.SmallFontSize = SmallFontSize;
        p.SlotHeight = RowSlotHeight;
        p.Indent = Classes.Contains(CompactPseudoClass) ? Math.Min(12, IndentWidth) : IndentWidth;
        p.KeyMaxFraction = Classes.Contains(CompactPseudoClass) ? 0.40 : 0.45;
        p.Version++;
        if (_panel is not null)
        {
            _panel.RowHeight = RowSlotHeight;
            _panel.Highlight.Fill = HighlightFill;
            RefreshRows();
            _panel.InvalidateMeasure();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scrollMotion?.Stop();
        _pathCopyReset?.Stop();
    }

    #endregion
}
