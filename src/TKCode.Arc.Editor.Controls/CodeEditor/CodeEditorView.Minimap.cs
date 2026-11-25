using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;
using TKCode.Arc.Editor.Standalone;
using TKCode.Arc.Editor.ViewModel;
using TKCode.Arc.Editor.ViewParts;

namespace TKCode.Arc;

// The minimap and the overview ruler (upstream's Minimap and DecorationsOverviewRuler view parts). The engine computes
// both (TKCode.Arc.Editor.ViewParts); the view lays them out, resolves their colors from the theme brushes below and
// shows them in two visuals of its own: CodeEditorMinimap (the zoomed out lines and the slider) and
// CodeEditorOverviewRuler (the marks beside the vertical scroll bar). With the minimap on, the text ends left of it
// and the overview ruler gets a column of its own at the right edge; without it, the ruler lies over the text under the
// scroll bar, as in Monaco.
internal sealed partial class CodeEditorView
{
    /// <summary>Defines the <see cref="OverviewRulerWidth"/> property.</summary>
    public static readonly StyledProperty<double> OverviewRulerWidthProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(OverviewRulerWidth), 14);

    /// <summary>Defines the <see cref="MinimapMaxColumn"/> property.</summary>
    public static readonly StyledProperty<double> MinimapMaxColumnProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(MinimapMaxColumn), 120);

    /// <summary>Defines the <see cref="MinimapSliderOpacity"/> property.</summary>
    public static readonly StyledProperty<double> MinimapSliderOpacityProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(MinimapSliderOpacity), 0.12);

    /// <summary>Defines the <see cref="MinimapSliderHoverOpacity"/> property.</summary>
    public static readonly StyledProperty<double> MinimapSliderHoverOpacityProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(MinimapSliderHoverOpacity), 0.18);

    /// <summary>Defines the <see cref="MinimapSliderActiveOpacity"/> property.</summary>
    public static readonly StyledProperty<double> MinimapSliderActiveOpacityProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(MinimapSliderActiveOpacity), 0.26);

    /// <summary>Defines the <see cref="MinimapBackgroundBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MinimapBackgroundBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(MinimapBackgroundBrush));

    /// <summary>Defines the <see cref="MinimapSliderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MinimapSliderBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(MinimapSliderBrush));

    /// <summary>Defines the <see cref="MinimapSelectionBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MinimapSelectionBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(MinimapSelectionBrush));

    /// <summary>Defines the <see cref="OverviewRulerCursorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> OverviewRulerCursorBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(OverviewRulerCursorBrush));

    /// <summary>Defines the <see cref="OverviewRulerBorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> OverviewRulerBorderBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(OverviewRulerBorderBrush));

    // the properties that change how the minimap and the overview ruler look (an immutable table)
    private static readonly HashSet<AvaloniaProperty> s_minimapProperties =
    [
        OverviewRulerWidthProperty, MinimapMaxColumnProperty, MinimapSliderOpacityProperty,
        MinimapSliderHoverOpacityProperty, MinimapSliderActiveOpacityProperty, MinimapBackgroundBrushProperty,
        MinimapSliderBrushProperty, MinimapSelectionBrushProperty, OverviewRulerCursorBrushProperty,
        OverviewRulerBorderBrushProperty,
    ];

    // upstream's theme color ids of the overview ruler and minimap decoration colors, with the theme color each is drawn
    // in and the opacity upstream's default gives it (an immutable table)
    private static readonly Dictionary<string, (CodeEditorThemeColor Color, double Opacity)> s_decorationThemeColors = new(StringComparer.Ordinal)
    {
        [MarkerDecorationsService.OverviewRulerError] = (CodeEditorThemeColor.Error, 1),
        [MarkerDecorationsService.OverviewRulerWarning] = (CodeEditorThemeColor.Warning, 1),
        [MarkerDecorationsService.OverviewRulerInfo] = (CodeEditorThemeColor.Info, 1),
        ["editorOverviewRuler.findMatchForeground"] = (CodeEditorThemeColor.Warning, 0.5),
        ["editorOverviewRuler.rangeHighlightForeground"] = (CodeEditorThemeColor.Info, 0.6),
        ["editorOverviewRuler.selectionHighlightForeground"] = (CodeEditorThemeColor.Muted, 0.8),
        ["editorOverviewRuler.wordHighlightForeground"] = (CodeEditorThemeColor.Muted, 0.8),
        ["editorOverviewRuler.wordHighlightStrongForeground"] = (CodeEditorThemeColor.Info, 0.8),
        ["editorOverviewRuler.wordHighlightTextForeground"] = (CodeEditorThemeColor.Muted, 0.8),
        ["editorOverviewRuler.bracketMatchForeground"] = (CodeEditorThemeColor.Muted, 1),
        ["editorOverviewRuler.modifiedForeground"] = (CodeEditorThemeColor.Info, 0.6),
        ["editorOverviewRuler.addedForeground"] = (CodeEditorThemeColor.Info, 0.6),
        ["editorOverviewRuler.deletedForeground"] = (CodeEditorThemeColor.Error, 0.6),
        [MarkerDecorationsService.MinimapError] = (CodeEditorThemeColor.Error, 0.7),
        [MarkerDecorationsService.MinimapWarning] = (CodeEditorThemeColor.Warning, 1),
        [MarkerDecorationsService.MinimapInfo] = (CodeEditorThemeColor.Info, 1),
        ["minimap.findMatchHighlight"] = (CodeEditorThemeColor.Warning, 1),
        ["minimap.selectionOccurrenceHighlight"] = (CodeEditorThemeColor.Muted, 0.8),
    };

    private const string MinimapSelectionHighlight = "minimap.selectionHighlight";

    private readonly ComputeOptionsMemory _minimapMemory = new();
    private EditorMinimapLayoutInfo _minimapLayout = new() { RenderMinimap = RenderMinimap.None };
    private CodeEditorMinimap? _minimapView;
    private CodeEditorOverviewRuler? _overviewRulerView;
    private Minimap? _minimap;
    private DecorationsOverviewRuler? _overviewRuler;
    private MinimapViewHandler? _minimapHandler;
    private StandaloneThemeService? _minimapThemeService;

    /// <summary>The width of the overview ruler at the right edge (Monaco's vertical scroll bar size, 14).</summary>
    public double OverviewRulerWidth
    {
        get => GetValue(OverviewRulerWidthProperty);
        set => SetValue(OverviewRulerWidthProperty, value);
    }

    /// <summary>The most columns the minimap shows (Monaco's <c>minimap.maxColumn</c>, 120).</summary>
    public double MinimapMaxColumn
    {
        get => GetValue(MinimapMaxColumnProperty);
        set => SetValue(MinimapMaxColumnProperty, value);
    }

    /// <summary>The opacity of the minimap slider at rest.</summary>
    public double MinimapSliderOpacity
    {
        get => GetValue(MinimapSliderOpacityProperty);
        set => SetValue(MinimapSliderOpacityProperty, value);
    }

    /// <summary>The opacity of the minimap slider under the pointer.</summary>
    public double MinimapSliderHoverOpacity
    {
        get => GetValue(MinimapSliderHoverOpacityProperty);
        set => SetValue(MinimapSliderHoverOpacityProperty, value);
    }

    /// <summary>The opacity of the minimap slider while it is dragged.</summary>
    public double MinimapSliderActiveOpacity
    {
        get => GetValue(MinimapSliderActiveOpacityProperty);
        set => SetValue(MinimapSliderActiveOpacityProperty, value);
    }

    /// <summary>The background of the minimap (and of the overview ruler beside it); null uses the token theme's background.</summary>
    public IBrush? MinimapBackgroundBrush
    {
        get => GetValue(MinimapBackgroundBrushProperty);
        set => SetValue(MinimapBackgroundBrushProperty, value);
    }

    /// <summary>The color of the minimap slider (drawn at the slider opacities).</summary>
    public IBrush? MinimapSliderBrush
    {
        get => GetValue(MinimapSliderBrushProperty);
        set => SetValue(MinimapSliderBrushProperty, value);
    }

    /// <summary>The color of the selections in the minimap.</summary>
    public IBrush? MinimapSelectionBrush
    {
        get => GetValue(MinimapSelectionBrushProperty);
        set => SetValue(MinimapSelectionBrushProperty, value);
    }

    /// <summary>The color of the cursor marks in the overview ruler (drawn at 70%).</summary>
    public IBrush? OverviewRulerCursorBrush
    {
        get => GetValue(OverviewRulerCursorBrushProperty);
        set => SetValue(OverviewRulerCursorBrushProperty, value);
    }

    /// <summary>The color of the overview ruler's left and top edges.</summary>
    public IBrush? OverviewRulerBorderBrush
    {
        get => GetValue(OverviewRulerBorderBrushProperty);
        set => SetValue(OverviewRulerBorderBrushProperty, value);
    }

    /// <summary>Whether the minimap shows (the editor's ShowMinimap and a layout that leaves it room).</summary>
    internal bool IsMinimapVisible => _minimapLayout.RenderMinimap != RenderMinimap.None && _minimapLayout.MinimapWidth > 0;

    /// <summary>The right edge of the text area: left of the minimap and the overview ruler when the minimap shows.</summary>
    internal double TextAreaRight => IsMinimapVisible
        ? Math.Max(0, Math.Min(Bounds.Width, _minimapLayout.MinimapLeft))
        : Bounds.Width;

    /// <summary>The minimap's layout (for tests).</summary>
    internal EditorMinimapLayoutInfo MinimapLayout => _minimapLayout;

    /// <summary>The engine's minimap, or null without a view model.</summary>
    internal Minimap? MinimapPart => _minimap;

    /// <summary>The engine's overview ruler, or null without a view model.</summary>
    internal DecorationsOverviewRuler? OverviewRulerPart => _overviewRuler;

    /// <summary>The visual of the minimap (for tests).</summary>
    internal CodeEditorMinimap? MinimapView => _minimapView;

    /// <summary>The visual of the overview ruler (for tests).</summary>
    internal CodeEditorOverviewRuler? OverviewRulerView => _overviewRulerView;

    internal double PixelRatio => TopLevel.GetTopLevel(this)?.RenderScaling is { } scaling && scaling > 0 ? scaling : 1;

    private static bool IsMinimapProperty(AvaloniaProperty property) => s_minimapProperties.Contains(property);

    private void OnMinimapPropertyChanged(AvaloniaProperty property)
    {
        if (property == OverviewRulerWidthProperty || property == MinimapMaxColumnProperty)
        {
            UpdateConfiguration();
            return;
        }

        UpdateMinimapParts();
        _minimapView?.InvalidateVisual();
        _overviewRulerView?.InvalidateVisual();
    }

    /// <summary>
    /// Lays the minimap out at the view's size (upstream's EditorLayoutInfoComputer.computeMinimapLayout with the
    /// vertical scroll bar's width as the overview ruler's); called while the view model's options are made, so the
    /// text width leaves the minimap its room.
    /// </summary>
    private void UpdateMinimapLayout()
    {
        var editor = _editor;
        var minimapOptions = GetEditorMinimapOptions();
        if (editor is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            _minimapLayout = new EditorMinimapLayoutInfo { RenderMinimap = RenderMinimap.None };
            UpdateMinimapParts();
            return;
        }

        _minimapLayout = EditorLayoutInfoComputer.ComputeMinimapLayout(new MinimapLayoutInput
        {
            OuterWidth = Math.Truncate(Bounds.Width),
            OuterHeight = Math.Truncate(Bounds.Height),
            LineHeight = _lineHeight,
            TypicalHalfwidthCharacterWidth = _charWidth,
            PixelRatio = PixelRatio,
            ScrollBeyondLastLine = true,
            PaddingTop = 0,
            PaddingBottom = 0,
            Minimap = minimapOptions,
            VerticalScrollbarWidth = OverviewRulerWidth,
            ViewLineCount = _viewModel?.GetLineCount() ?? editor.Model.GetLineCount(),
            RemainingWidth = Math.Max(0, Bounds.Width - GutterWidth),
            IsViewportWrapping = editor.WordWrap,
        }, _minimapMemory);

        UpdateMinimapParts();
    }

    private EditorMinimapOptions GetEditorMinimapOptions() => EditorMinimapOptions.Default with
    {
        Enabled = _editor?.ShowMinimap ?? false,
        MaxColumn = Math.Max(1, (int)MinimapMaxColumn),
    };

    /// <summary>Hands the engine's minimap and overview ruler their options and colors.</summary>
    private void UpdateMinimapParts()
    {
        if (_minimap is { } minimap)
        {
            minimap.SetOptions(CreateMinimapOptions());
            minimap.InvalidateDecorations();
        }

        if (_overviewRuler is { } ruler)
        {
            ruler.SetSettings(CreateOverviewRulerSettings());
            ruler.Invalidate();
        }

        _minimapView?.InvalidateVisual();
        _overviewRulerView?.InvalidateVisual();
    }

    private MinimapOptions CreateMinimapOptions()
    {
        var colorMap = _editor?.LanguageServices.ThemeService.GetColorMap();
        var tracker = new MinimapTokensColorTracker(colorMap);
        var layoutInfo = new EditorLayoutInfo { Width = Bounds.Width, Height = Math.Truncate(Bounds.Height), Minimap = _minimapLayout };
        return new MinimapOptions(
            layoutInfo,
            GetEditorMinimapOptions(),
            PixelRatio,
            _lineHeight,
            _charWidth,
            paddingTop: 0,
            paddingBottom: 0,
            scrollBeyondLastLine: true,
            tracker,
            new MinimapColors
            {
                Background = ToRgba(MinimapBackgroundBrush) is { A: 255 } background ? background : null,
                Selection = ToRgba(MinimapSelectionBrush),
            });
    }

    private OverviewRulerSettings CreateOverviewRulerSettings()
    {
        RgbaColor? background = null;
        if (IsMinimapVisible)
        {
            // upstream: the default background when the minimap is on the right
            background = ToRgba(MinimapBackgroundBrush)
                ?? _minimap?.Options.DefaultBackgroundColor;
        }

        return new OverviewRulerSettings(
            new OverviewRulerPosition(OverviewRulerWidth, Math.Truncate(Bounds.Height), 0, 0),
            _lineHeight,
            PixelRatio,
            overviewRulerLanes: 3,
            renderBorder: true,
            borderColor: ToRgba(OverviewRulerBorderBrush),
            hideCursor: false,
            cursorColor: ToRgba(OverviewRulerCursorBrush),
            backgroundColor: background);
    }

    /// <summary>A brush as a color (a solid color brush with its opacity), or null.</summary>
    internal static RgbaColor? ToRgba(IBrush? brush, double opacity = 1)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return null;
        }

        var color = solid.Color;
        var alpha = Math.Clamp(Math.Round(color.A * solid.Opacity * opacity, MidpointRounding.AwayFromZero), 0, 255);
        return new RgbaColor(color.R, color.G, color.B, (byte)alpha);
    }

    /// <summary>
    /// A decoration color of the overview ruler or the minimap: a hex value, or one of upstream's theme color ids mapped
    /// to the view's theme brushes; null for a color the theme does not have.
    /// </summary>
    internal RgbaColor? ResolveDecorationColor(string color)
    {
        if (string.IsNullOrEmpty(color))
        {
            return null;
        }

        if (color[0] == '#')
        {
            return RgbaColor.TryParseHex(color);
        }

        if (color == MinimapSelectionHighlight)
        {
            return ToRgba(MinimapSelectionBrush);
        }

        return s_decorationThemeColors.TryGetValue(color, out var themed) ? ToRgba(GetThemeBrush(themed.Color), themed.Opacity) : null;
    }

    private RgbaColor? ResolveMinimapDecorationColor(ModelDecorationMinimapOptions options)
    {
        var isDark = _editor?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var color = isDark && !options.DarkColor.IsEmpty ? options.DarkColor : options.Color;
        return ResolveDecorationColor(color.Value ?? color.ThemeColorId ?? string.Empty);
    }

    // ---- view model lifetime

    /// <summary>Creates the minimap and the overview ruler for a new view model.</summary>
    private void OnViewModelCreatedForMinimap()
    {
        if (_viewModel is null || _editor is null)
        {
            return;
        }

        if (_minimapView is null)
        {
            _minimapView = new CodeEditorMinimap(this);
            _overviewRulerView = new CodeEditorOverviewRuler(this);
            VisualChildren.Add(_overviewRulerView);
            VisualChildren.Add(_minimapView);
            InvalidateArrange();
        }

        _minimapThemeService = _editor.LanguageServices.ThemeService;
        _minimapThemeService.DidColorThemeChange += OnMinimapColorThemeChanged;

        _minimap = new Minimap(_viewModel, CreateMinimapOptions()) { DecorationColorResolver = ResolveMinimapDecorationColor };
        _overviewRuler = new DecorationsOverviewRuler(_viewModel, CreateOverviewRulerSettings()) { ColorResolver = ResolveDecorationColor };
        _minimapHandler = new MinimapViewHandler(this);
        _viewModel.AddViewEventHandler(_minimapHandler);
        _minimapView.OnPartsChanged();
        _minimapView.InvalidateVisual();
        _overviewRulerView!.InvalidateVisual();
    }

    /// <summary>Drops the minimap and the overview ruler of the view model that goes away.</summary>
    private void OnViewModelDetachingForMinimap()
    {
        if (_viewModel is not null && _minimapHandler is not null)
        {
            _viewModel.RemoveViewEventHandler(_minimapHandler);
        }

        if (_minimapThemeService is not null)
        {
            _minimapThemeService.DidColorThemeChange -= OnMinimapColorThemeChanged;
            _minimapThemeService = null;
        }

        _minimapHandler = null;
        _minimap?.Dispose();
        _minimap = null;
        _overviewRuler?.Dispose();
        _overviewRuler = null;
        _minimapView?.OnPartsChanged();
    }

    private void OnMinimapColorThemeChanged(StandaloneTheme theme) => UpdateMinimapParts();

    /// <summary>Passes the view events to the minimap and the overview ruler and repaints what has to be drawn again.</summary>
    private sealed class MinimapViewHandler(CodeEditorView view) : ViewEventHandler
    {
        public override void HandleEvents(IReadOnlyList<ViewEvent> events)
        {
            if (view._minimap is { } minimap)
            {
                minimap.HandleEvents(events);
                if (minimap.ShouldRender())
                {
                    view._minimapView?.InvalidateVisual();
                }
            }

            if (view._overviewRuler is { } ruler)
            {
                ruler.HandleEvents(events);
                if (ruler.ShouldRender())
                {
                    view._overviewRulerView?.InvalidateVisual();
                }
            }
        }
    }
}
