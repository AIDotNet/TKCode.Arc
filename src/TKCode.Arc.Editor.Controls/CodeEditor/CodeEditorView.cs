using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

/// <summary>
/// The surface of a <see cref="CodeEditor"/>: it lays the view model out at its size and font, draws the visible view
/// lines (line numbers, selections, text, cursors) and turns keyboard, text and pointer input into editor commands.
/// Upstream splits the drawing into view parts (ViewLines, ViewCursors, SelectionsOverlay, LineNumbersOverlay, ...);
/// here they are the layers of the view, each a visual of its own (<see cref="CodeEditorLayer"/>), so a cursor blink or
/// a cursor move re-records the cursors and not the text (design/architecture/code-editor.md section 7).
/// </summary>
internal sealed partial class CodeEditorView : Control
{
    /// <summary>Defines the <see cref="LineHeightRatio"/> property.</summary>
    public static readonly StyledProperty<double> LineHeightRatioProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(LineHeightRatio), 1.35);

    /// <summary>Defines the <see cref="CursorWidth"/> property.</summary>
    public static readonly StyledProperty<double> CursorWidthProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(CursorWidth), 2);

    /// <summary>Defines the <see cref="GutterPadding"/> property.</summary>
    public static readonly StyledProperty<double> GutterPaddingProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(GutterPadding), 16);

    /// <summary>Defines the <see cref="GutterLeadingPadding"/> property.</summary>
    public static readonly StyledProperty<double> GutterLeadingPaddingProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(GutterLeadingPadding), 12);

    /// <summary>Defines the <see cref="SelectionBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(SelectionBrush));

    /// <summary>Defines the <see cref="CursorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CursorBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(CursorBrush));

    /// <summary>Defines the <see cref="LineNumberBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LineNumberBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(LineNumberBrush));

    /// <summary>Defines the <see cref="ActiveLineNumberBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ActiveLineNumberBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(ActiveLineNumberBrush));

    /// <summary>Defines the <see cref="CurrentLineBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CurrentLineBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(CurrentLineBrush));

    /// <summary>Defines the <see cref="IndentGuideBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> IndentGuideBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(IndentGuideBrush));

    /// <summary>Defines the <see cref="ActiveIndentGuideBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ActiveIndentGuideBrushProperty =
        AvaloniaProperty.Register<CodeEditorView, IBrush?>(nameof(ActiveIndentGuideBrush));

    /// <summary>Defines the <see cref="IndentGuideWidth"/> property.</summary>
    public static readonly StyledProperty<double> IndentGuideWidthProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(IndentGuideWidth), 1);

    private CodeEditor? _editor;
    private EditorViewModel? _viewModel;
    private ViewHandler? _handler;
    private Typeface _typeface;
    private double _fontSize;
    private double _lineHeight = 19;
    private double _charWidth = 8;

    static CodeEditorView()
    {
        FocusableProperty.OverrideDefaultValue<CodeEditorView>(false);
        ClipToBoundsProperty.OverrideDefaultValue<CodeEditorView>(true);
        CursorProperty.OverrideDefaultValue<CodeEditorView>(new Cursor(StandardCursorType.Ibeam));
    }

    /// <summary>Creates the view and its layers.</summary>
    public CodeEditorView()
    {
        _backgroundLayer = new CodeEditorLayer(CodeEditorLayers.Background, RenderBackground);
        _textLayer = new CodeEditorLayer(CodeEditorLayers.Text, RenderText);
        _decorationsLayer = new CodeEditorLayer(CodeEditorLayers.Decorations, RenderDecorations);
        _cursorLayer = new CodeEditorLayer(CodeEditorLayers.Cursors, RenderCursors);
        _gutterLayer = new CodeEditorLayer(CodeEditorLayers.Gutter, RenderGutter);
        VisualChildren.Add(_backgroundLayer);
        VisualChildren.Add(_textLayer);
        VisualChildren.Add(_decorationsLayer);
        VisualChildren.Add(_cursorLayer);
        VisualChildren.Add(_gutterLayer);
    }

    /// <summary>The line height as a multiple of the font size.</summary>
    public double LineHeightRatio
    {
        get => GetValue(LineHeightRatioProperty);
        set => SetValue(LineHeightRatioProperty, value);
    }

    /// <summary>The width of the insert mode cursor.</summary>
    public double CursorWidth
    {
        get => GetValue(CursorWidthProperty);
        set => SetValue(CursorWidthProperty, value);
    }

    /// <summary>The space between the line numbers and the text.</summary>
    public double GutterPadding
    {
        get => GetValue(GutterPaddingProperty);
        set => SetValue(GutterPaddingProperty, value);
    }

    /// <summary>The space left of the line numbers.</summary>
    public double GutterLeadingPadding
    {
        get => GetValue(GutterLeadingPaddingProperty);
        set => SetValue(GutterLeadingPaddingProperty, value);
    }

    /// <summary>The color of the indent guides.</summary>
    public IBrush? IndentGuideBrush
    {
        get => GetValue(IndentGuideBrushProperty);
        set => SetValue(IndentGuideBrushProperty, value);
    }

    /// <summary>The color of the indent guide of the block the cursor is in.</summary>
    public IBrush? ActiveIndentGuideBrush
    {
        get => GetValue(ActiveIndentGuideBrushProperty);
        set => SetValue(ActiveIndentGuideBrushProperty, value);
    }

    /// <summary>The width of an indent guide.</summary>
    public double IndentGuideWidth
    {
        get => GetValue(IndentGuideWidthProperty);
        set => SetValue(IndentGuideWidthProperty, value);
    }

    /// <summary>The background of selected text.</summary>
    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <summary>The color of the cursors.</summary>
    public IBrush? CursorBrush
    {
        get => GetValue(CursorBrushProperty);
        set => SetValue(CursorBrushProperty, value);
    }

    /// <summary>The color of the line numbers.</summary>
    public IBrush? LineNumberBrush
    {
        get => GetValue(LineNumberBrushProperty);
        set => SetValue(LineNumberBrushProperty, value);
    }

    /// <summary>The color of the line number of the lines with a cursor.</summary>
    public IBrush? ActiveLineNumberBrush
    {
        get => GetValue(ActiveLineNumberBrushProperty);
        set => SetValue(ActiveLineNumberBrushProperty, value);
    }

    /// <summary>The background of the line of the primary cursor, when nothing is selected.</summary>
    public IBrush? CurrentLineBrush
    {
        get => GetValue(CurrentLineBrushProperty);
        set => SetValue(CurrentLineBrushProperty, value);
    }

    internal EditorViewModel? ViewModel => _viewModel;

    internal double LineHeight => _lineHeight;

    internal double CharWidth => _charWidth;

    internal void Attach(CodeEditor editor)
    {
        _editor = editor;
        UpdateFont();
        AttachTokenTheme();
        AttachDecorationStyles();
        AttachGutter();
        CreateViewModel();
    }

    internal void Detach()
    {
        OnViewModelDetaching();
        DetachScrollBars();
        DetachTokenTheme();
        DetachDecorationStyles();
        DetachGutter();
        if (_viewModel is not null && _handler is not null)
        {
            _viewModel.RemoveViewEventHandler(_handler);
        }

        _editor?.DisposeViewModel();
        _viewModel = null;
        _handler = null;
        _editor = null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextElement.FontFamilyProperty
            || change.Property == TextElement.FontSizeProperty
            || change.Property == LineHeightRatioProperty)
        {
            if (_editor is not null)
            {
                UpdateFont();
                CreateViewModel();
            }
        }
        else if (change.Property == TextElement.ForegroundProperty)
        {
            // the caches follow the brush when they draw next
            InvalidateLayers(CodeEditorLayers.Text | CodeEditorLayers.Gutter);
        }
        else if (change.Property == SelectionBrushProperty || change.Property == CurrentLineBrushProperty
            || change.Property == IndentGuideBrushProperty || change.Property == ActiveIndentGuideBrushProperty
            || change.Property == IndentGuideWidthProperty)
        {
            InvalidateLayers(CodeEditorLayers.Background);
        }
        else if (change.Property == CursorBrushProperty || change.Property == CursorWidthProperty)
        {
            InvalidateLayers(CodeEditorLayers.Cursors);
        }
        else if (change.Property == LineNumberBrushProperty || change.Property == ActiveLineNumberBrushProperty)
        {
            InvalidateLayers(CodeEditorLayers.Gutter);
        }
        else if (change.Property == GutterPaddingProperty || change.Property == GutterLeadingPaddingProperty)
        {
            InvalidateLayers(CodeEditorLayers.All);
        }
        else if (IsThemeBrushProperty(change.Property))
        {
            OnThemeBrushChanged();
        }
        else if (IsMinimapProperty(change.Property))
        {
            OnMinimapPropertyChanged(change.Property);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateConfiguration();
        _editor?.RaiseLayoutChange(e.NewSize);
        _editor?.LayoutWidgets();
    }

    private void UpdateFont()
    {
        _typeface = new Typeface(TextElement.GetFontFamily(this));
        _fontSize = TextElement.GetFontSize(this);
        _lineHeight = Math.Max(1, Math.Round(_fontSize * LineHeightRatio));
        using var probe = new Avalonia.Media.TextFormatting.TextLayout("0000000000", _typeface, _fontSize, null);
        _charWidth = Math.Max(1, probe.WidthIncludingTrailingWhitespace / 10);

        // the text is centred in the line box, its baseline on a whole pixel
        var probeBaseline = probe.TextLines.Count > 0 ? probe.TextLines[0].Baseline : probe.Baseline;
        var baseline = Math.Round(((_lineHeight - probe.Height) / 2) + probeBaseline);
        _metrics = new CodeEditorTextMetrics(_typeface, _fontSize, _charWidth, _lineHeight, baseline, TextElement.GetForeground(this) ?? Brushes.Black);
        _textCache?.Clear();
        _lineNumberCache?.Clear();
        _activeLineNumberCache?.Clear();
        _textCache = new CodeEditorTextCache(_metrics);
        UpdatePalette();
        _lineNumberCache = null;
        _activeLineNumberCache = null;
    }

    private void CreateViewModel()
    {
        if (_editor is null)
        {
            return;
        }

        OnViewModelDetaching();
        if (_viewModel is not null && _handler is not null)
        {
            _viewModel.RemoveViewEventHandler(_handler);
        }

        _viewModel = _editor.CreateViewModel(CreateOptions(), ScheduleAtNextAnimationFrame);
        _maxLineWidth = 0;
        _handler = new ViewHandler(this);
        _viewModel.AddViewEventHandler(_handler);
        _viewModel.SetHasFocus(_editor.IsKeyboardFocusWithin);
        OnViewModelCreated();
        OnViewModelCreatedForMinimap();
        InvalidateVisual();
        _editor.OnViewModelReady();
    }

    /// <summary>Hands the view model a new layout (size, wrapping, read-only).</summary>
    internal void UpdateConfiguration()
    {
        if (_editor?.Configuration is { } configuration)
        {
            configuration.UpdateOptions(CreateOptions());
            UpdateScrollBars();
            InvalidateVisual();
        }
    }

    private ViewModelOptions CreateOptions()
    {
        var editor = _editor!;
        UpdateMinimapLayout();
        var textWidth = Math.Max(0, TextAreaRight - GutterWidth);
        var viewportColumns = Math.Max(1, (int)Math.Floor(textWidth / _charWidth) - 1);
        var fontInfo = new FontInfo(
            pixelRatio: 1,
            fontFamily: _typeface.FontFamily.Name,
            fontWeight: "normal",
            fontSize: _fontSize,
            fontFeatureSettings: "normal",
            fontVariationSettings: "normal",
            lineHeight: _lineHeight,
            letterSpacing: 0,
            isMonospace: true,
            typicalHalfwidthCharacterWidth: _charWidth,
            typicalFullwidthCharacterWidth: _charWidth * 2,
            canUseHalfwidthRightwardsArrow: true,
            spaceWidth: _charWidth,
            middotWidth: _charWidth,
            wsmiddotWidth: _charWidth,
            maxDigitWidth: _charWidth,
            isTrusted: true);

        return new ViewModelOptions
        {
            Cursor = EditorOptions.Default with
            {
                ReadOnly = editor.IsReadOnly,
                LineHeight = _lineHeight,
                TypicalHalfwidthCharacterWidth = _charWidth,
                LayoutHeight = Bounds.Height,
            },
            Layout = new ViewLayoutOptions
            {
                LayoutInfo = new ViewLayoutOptions.LayoutInfoOptions(textWidth, Bounds.Height, 0),
                LineHeight = _lineHeight,
                WrappingInfo = new ViewLayoutOptions.WrappingInfoOptions(editor.WordWrap),
                SmoothScrolling = editor.SmoothScrolling,
                FontInfo = new ViewLayoutOptions.FontInfoOptions(_charWidth),
                Minimap = new ViewLayoutOptions.MinimapOptions(false, "right"),
            },
            FontInfo = fontInfo,
            WrappingColumn = editor.WordWrap ? viewportColumns : -1,
            StickyScroll = new StickyScrollOptions(editor.StickyScroll, editor.StickyScrollMaxLineCount),
        };
    }

    // upstream's requestAnimationFrame: the top level's next frame once the view is shown, a render-priority post before
    private IDisposable ScheduleAtNextAnimationFrame(Action callback)
    {
        var cancelled = false;
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.RequestAnimationFrame(_ =>
            {
                if (!cancelled)
                {
                    callback();
                }
            });
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!cancelled)
                {
                    callback();
                }
            }, DispatcherPriority.Render);
        }

        return new CallbackDisposable(() => cancelled = true);
    }

    /// <summary>
    /// Repaints the layers a view event touches (upstream's view parts each answer the events they care about) and
    /// scrolls to the ranges the view model asks to reveal. A cursor or focus change leaves the text alone; scrolling
    /// redraws it from the line cache.
    /// </summary>
    private sealed class ViewHandler(CodeEditorView view) : ViewEventHandler
    {
        public override void HandleEvents(IReadOnlyList<ViewEvent> events)
        {
            var layers = CodeEditorLayers.None;
            var layoutWidgets = false;
            var lineMappingChanged = false;
            foreach (var e in events)
            {
                switch (e)
                {
                    case ViewRevealRangeRequestEvent reveal:
                        // a scroll it causes arrives as a scroll event
                        view.Reveal(reveal);
                        break;
                    case ViewCursorStateChangedEvent:
                        layers |= CodeEditorLayers.Background | CodeEditorLayers.Cursors | CodeEditorLayers.Gutter;
                        layoutWidgets = true;
                        break;
                    case ViewFocusChangedEvent:
                    case ViewCompositionStartEvent:
                    case ViewCompositionEndEvent:
                        layers |= CodeEditorLayers.Cursors;
                        break;
                    case ViewDecorationsChangedEvent:
                        // inline decorations (colorized brackets) color the text; margin decorations are in the gutter
                        layers |= CodeEditorLayers.Background | CodeEditorLayers.Text | CodeEditorLayers.Decorations | CodeEditorLayers.Gutter;
                        break;
                    case ViewTokensChangedEvent:
                    case ViewTokensColorsChangedEvent:
                        layers |= CodeEditorLayers.Text;
                        break;
                    case ViewLanguageConfigurationEvent:
                        break;
                    case ViewFlushedEvent:
                        view.ResetMaxLineWidth();
                        layers |= CodeEditorLayers.All;
                        lineMappingChanged = true;
                        break;
                    case ViewLineMappingChangedEvent:
                        layers |= CodeEditorLayers.All;
                        lineMappingChanged = true;
                        break;
                    case ViewScrollChangedEvent scroll:
                        if (scroll.ScrollTopChanged || scroll.ScrollLeftChanged)
                        {
                            layers |= CodeEditorLayers.All;
                            layoutWidgets = true;
                        }

                        break;
                    default:
                        // lines changed, inserted, deleted, configuration, theme, zones
                        layers |= CodeEditorLayers.All;
                        layoutWidgets = true;
                        break;
                }
            }

            view.InvalidateLayers(layers);
            view.UpdateScrollBars();
            if (lineMappingChanged)
            {
                // the zones follow the new view lines (upstream's ViewZones.onLineMappingChanged)
                view._editor?.OnViewLineMappingChanged();
            }
            else if (layoutWidgets)
            {
                view._editor?.LayoutWidgets();
            }
        }
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
