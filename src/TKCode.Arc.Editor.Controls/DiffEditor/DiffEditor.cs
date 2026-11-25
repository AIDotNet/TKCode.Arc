// The diff editor, modelled on vscode's DiffEditorWidget (src/vs/editor/browser/widget/diffEditor/diffEditorWidget.ts
// and its features and components, commit b57d5c9e; MIT license), as a behavior reference: two CodeEditors side by
// side or one inline, the diff drawn as decorations, view zones and gutter columns, hidden unchanged regions, moved
// blocks, revert arrows and an overview ruler.
// Differences: an Avalonia templated control with properties instead of a widget over two models; the models belong to
// the two editors and are set through OriginalText / ModifiedText. Split into partial files by concern
// (DiffEditor.Diff.cs: computing; .Decorations.cs; .ViewZones.cs: alignment, inline removed lines, unchanged regions;
// .Navigation.cs: scrolling, F7, revert).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc;

/// <summary>
/// Shows the differences between two texts, modelled on the Monaco diff editor: the original text (read-only) and the
/// modified text side by side with their lines aligned, or inline in one editor; inserted and removed lines and
/// characters highlighted, unchanged regions that can be hidden, moved blocks, arrows that revert a change, an overview
/// ruler of the changes, and F7 / Shift+F7 to go to the next / previous change. The diff is computed again after edits.
/// </summary>
[TemplatePart(PartOriginalEditor, typeof(CodeEditor))]
[TemplatePart(PartModifiedEditor, typeof(CodeEditor))]
[TemplatePart(PartGrid, typeof(Grid))]
[TemplatePart(PartMovedBlocks, typeof(DiffEditorMovedBlocksStrip))]
[TemplatePart(PartOverviewRuler, typeof(DiffEditorOverviewRuler))]
public sealed partial class DiffEditor : TemplatedControl
{
    internal const string PartOriginalEditor = "PART_OriginalEditor";
    internal const string PartModifiedEditor = "PART_ModifiedEditor";
    internal const string PartGrid = "PART_Grid";
    internal const string PartMovedBlocks = "PART_MovedBlocks";
    internal const string PartOverviewRuler = "PART_OverviewRuler";

    /// <summary>The original text, shown read-only (left, or as removed lines inline).</summary>
    public static readonly StyledProperty<string> OriginalTextProperty =
        AvaloniaProperty.Register<DiffEditor, string>(nameof(OriginalText), string.Empty);

    /// <summary>The modified text (right, or the inline editor); editing it updates the property (two-way by default).</summary>
    public static readonly StyledProperty<string> ModifiedTextProperty =
        AvaloniaProperty.Register<DiffEditor, string>(nameof(ModifiedText), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The language of both texts, by id (<c>csharp</c>) or name (<c>C#</c>), for syntax highlighting. Default <c>plaintext</c>.</summary>
    public static readonly StyledProperty<string> LanguageIdProperty =
        AvaloniaProperty.Register<DiffEditor, string>(nameof(LanguageId), LanguageIds.PlaintextLanguageId);

    /// <summary>Whether the two texts show side by side (default true); otherwise one editor shows the removed lines inline.</summary>
    public static readonly StyledProperty<bool> RenderSideBySideProperty =
        AvaloniaProperty.Register<DiffEditor, bool>(nameof(RenderSideBySide), true);

    /// <summary>Whether long runs of unchanged lines are collapsed to a bar that expands them (default false).</summary>
    public static readonly StyledProperty<bool> HideUnchangedRegionsProperty =
        AvaloniaProperty.Register<DiffEditor, bool>(nameof(HideUnchangedRegions));

    /// <summary>Whether changes of leading and trailing whitespace are ignored (default true, as in Monaco).</summary>
    public static readonly StyledProperty<bool> IgnoreTrimWhitespaceProperty =
        AvaloniaProperty.Register<DiffEditor, bool>(nameof(IgnoreTrimWhitespace), true);

    /// <summary>Whether the modified text cannot be edited either (the original text never can). Default false.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<DiffEditor, bool>(nameof(IsReadOnly));

    // ---- theme brushes and token geometry (set by DiffEditorTheme.axaml)

    internal static readonly StyledProperty<IBrush?> InsertedLineBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(InsertedLineBrush));
    internal static readonly StyledProperty<IBrush?> RemovedLineBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(RemovedLineBrush));
    internal static readonly StyledProperty<IBrush?> InsertedTextBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(InsertedTextBrush));
    internal static readonly StyledProperty<IBrush?> RemovedTextBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(RemovedTextBrush));
    internal static readonly StyledProperty<IBrush?> MovedBlockBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(MovedBlockBrush));
    internal static readonly StyledProperty<IBrush?> SeparatorBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(SeparatorBrush));
    internal static readonly StyledProperty<IBrush?> EmptyLinesBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(EmptyLinesBrush));
    internal static readonly StyledProperty<IBrush?> CollapsedRegionBackgroundProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(CollapsedRegionBackground));
    internal static readonly StyledProperty<IBrush?> CollapsedRegionForegroundProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(CollapsedRegionForeground));
    internal static readonly StyledProperty<IBrush?> RevertArrowBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(RevertArrowBrush));
    internal static readonly StyledProperty<IBrush?> OverviewViewportBrushProperty = AvaloniaProperty.Register<DiffEditor, IBrush?>(nameof(OverviewViewportBrush));
    internal static readonly StyledProperty<double> CharHighlightOpacityProperty = AvaloniaProperty.Register<DiffEditor, double>(nameof(CharHighlightOpacity), 0.3);
    internal static readonly StyledProperty<double> IndicatorWidthProperty = AvaloniaProperty.Register<DiffEditor, double>(nameof(IndicatorWidth), 16);
    internal static readonly StyledProperty<double> RevertColumnWidthProperty = AvaloniaProperty.Register<DiffEditor, double>(nameof(RevertColumnWidth), 16);
    internal static readonly StyledProperty<double> CollapsedRegionHeightProperty = AvaloniaProperty.Register<DiffEditor, double>(nameof(CollapsedRegionHeight), 24);

    private CodeEditor? _originalEditor;
    private CodeEditor? _modifiedEditor;
    private Grid? _grid;
    private DiffEditorMovedBlocksStrip? _movedBlocks;
    private DiffEditorOverviewRuler? _overviewRuler;
    private bool _syncingModifiedText;

    static DiffEditor()
    {
        FocusableProperty.OverrideDefaultValue<DiffEditor>(false);
    }

    /// <summary>Creates an empty diff editor.</summary>
    public DiffEditor()
    {
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>The original text, shown read-only.</summary>
    public string OriginalText
    {
        get => GetValue(OriginalTextProperty);
        set => SetValue(OriginalTextProperty, value);
    }

    /// <summary>The modified text; editing it updates this property.</summary>
    public string ModifiedText
    {
        get => GetValue(ModifiedTextProperty);
        set => SetValue(ModifiedTextProperty, value);
    }

    /// <summary>The language of both texts, by id or name.</summary>
    public string LanguageId
    {
        get => GetValue(LanguageIdProperty);
        set => SetValue(LanguageIdProperty, value);
    }

    /// <summary>Whether the two texts show side by side; otherwise inline in one editor.</summary>
    public bool RenderSideBySide
    {
        get => GetValue(RenderSideBySideProperty);
        set => SetValue(RenderSideBySideProperty, value);
    }

    /// <summary>Whether long runs of unchanged lines are collapsed.</summary>
    public bool HideUnchangedRegions
    {
        get => GetValue(HideUnchangedRegionsProperty);
        set => SetValue(HideUnchangedRegionsProperty, value);
    }

    /// <summary>Whether changes of leading and trailing whitespace are ignored.</summary>
    public bool IgnoreTrimWhitespace
    {
        get => GetValue(IgnoreTrimWhitespaceProperty);
        set => SetValue(IgnoreTrimWhitespaceProperty, value);
    }

    /// <summary>Whether the modified text cannot be edited either.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Raised on the UI thread each time a diff was computed and shown, with its size.</summary>
    public event EventHandler<DiffComputedEventArgs>? DiffComputed;

    internal IBrush? InsertedLineBrush { get => GetValue(InsertedLineBrushProperty); set => SetValue(InsertedLineBrushProperty, value); }

    internal IBrush? RemovedLineBrush { get => GetValue(RemovedLineBrushProperty); set => SetValue(RemovedLineBrushProperty, value); }

    internal IBrush? InsertedTextBrush { get => GetValue(InsertedTextBrushProperty); set => SetValue(InsertedTextBrushProperty, value); }

    internal IBrush? RemovedTextBrush { get => GetValue(RemovedTextBrushProperty); set => SetValue(RemovedTextBrushProperty, value); }

    internal IBrush? MovedBlockBrush { get => GetValue(MovedBlockBrushProperty); set => SetValue(MovedBlockBrushProperty, value); }

    internal IBrush? SeparatorBrush { get => GetValue(SeparatorBrushProperty); set => SetValue(SeparatorBrushProperty, value); }

    internal IBrush? EmptyLinesBrush { get => GetValue(EmptyLinesBrushProperty); set => SetValue(EmptyLinesBrushProperty, value); }

    internal IBrush? CollapsedRegionBackground { get => GetValue(CollapsedRegionBackgroundProperty); set => SetValue(CollapsedRegionBackgroundProperty, value); }

    internal IBrush? CollapsedRegionForeground { get => GetValue(CollapsedRegionForegroundProperty); set => SetValue(CollapsedRegionForegroundProperty, value); }

    internal IBrush? RevertArrowBrush { get => GetValue(RevertArrowBrushProperty); set => SetValue(RevertArrowBrushProperty, value); }

    internal IBrush? OverviewViewportBrush { get => GetValue(OverviewViewportBrushProperty); set => SetValue(OverviewViewportBrushProperty, value); }

    internal double CharHighlightOpacity { get => GetValue(CharHighlightOpacityProperty); set => SetValue(CharHighlightOpacityProperty, value); }

    internal double IndicatorWidth { get => GetValue(IndicatorWidthProperty); set => SetValue(IndicatorWidthProperty, value); }

    internal double RevertColumnWidth { get => GetValue(RevertColumnWidthProperty); set => SetValue(RevertColumnWidthProperty, value); }

    internal double CollapsedRegionHeight { get => GetValue(CollapsedRegionHeightProperty); set => SetValue(CollapsedRegionHeightProperty, value); }

    /// <summary>The editor of the original text (null before the template is applied).</summary>
    internal CodeEditor? OriginalEditor => _originalEditor;

    /// <summary>The editor of the modified text (null before the template is applied).</summary>
    internal CodeEditor? ModifiedEditor => _modifiedEditor;

    internal DiffEditorMovedBlocksStrip? MovedBlocksStrip => _movedBlocks;

    internal DiffEditorOverviewRuler? OverviewRuler => _overviewRuler;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachEditors();

        _grid = e.NameScope.Find<Grid>(PartGrid);
        _originalEditor = e.NameScope.Find<CodeEditor>(PartOriginalEditor);
        _modifiedEditor = e.NameScope.Find<CodeEditor>(PartModifiedEditor);
        _movedBlocks = e.NameScope.Find<DiffEditorMovedBlocksStrip>(PartMovedBlocks);
        _overviewRuler = e.NameScope.Find<DiffEditorOverviewRuler>(PartOverviewRuler);
        _movedBlocks?.Attach(this);
        _overviewRuler?.Attach(this);

        AttachEditors();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelDiff();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_originalEditor is not null && _modifiedEditor is not null && _diff is null)
        {
            ScheduleDiff(immediately: true);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OriginalTextProperty)
        {
            if (_originalEditor is { } original)
            {
                original.Text = change.GetNewValue<string>() ?? string.Empty;
                ScheduleDiff(immediately: true);
            }
        }
        else if (change.Property == ModifiedTextProperty)
        {
            if (!_syncingModifiedText && _modifiedEditor is { } modified)
            {
                modified.Text = change.GetNewValue<string>() ?? string.Empty;
                ScheduleDiff(immediately: true);
            }
        }
        else if (change.Property == LanguageIdProperty)
        {
            if (_originalEditor is not null)
            {
                _originalEditor.LanguageId = LanguageId;
            }

            if (_modifiedEditor is not null)
            {
                _modifiedEditor.LanguageId = LanguageId;
            }
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            if (_modifiedEditor is not null)
            {
                _modifiedEditor.IsReadOnly = IsReadOnly;
            }

            UpdateRevertColumn();
        }
        else if (change.Property == RenderSideBySideProperty)
        {
            UpdateLayoutMode();
            UpdateRevertColumn();
            UpdateViewZones();
        }
        else if (change.Property == IgnoreTrimWhitespaceProperty)
        {
            ScheduleDiff(immediately: true);
        }
        else if (change.Property == HideUnchangedRegionsProperty)
        {
            UpdateUnchangedRegions(_diff);
            UpdateViewZones();
        }
        else if (change.Property == InsertedLineBrushProperty || change.Property == RemovedLineBrushProperty
                 || change.Property == InsertedTextBrushProperty || change.Property == RemovedTextBrushProperty
                 || change.Property == CharHighlightOpacityProperty)
        {
            RegisterDecorationStyles();
            UpdateViewZones();
            _overviewRuler?.InvalidateVisual();
        }
        else if (change.Property == IndicatorWidthProperty || change.Property == RevertColumnWidthProperty)
        {
            UpdateGutterColumns();
        }
        else if (change.Property == CollapsedRegionHeightProperty || change.Property == EmptyLinesBrushProperty
                 || change.Property == CollapsedRegionBackgroundProperty || change.Property == CollapsedRegionForegroundProperty)
        {
            UpdateViewZones();
        }
        else if (change.Property == MovedBlockBrushProperty || change.Property == SeparatorBrushProperty)
        {
            _movedBlocks?.InvalidateVisual();
        }
        else if (change.Property == OverviewViewportBrushProperty || change.Property == RevertArrowBrushProperty)
        {
            _overviewRuler?.InvalidateVisual();
            _revertColumn?.Invalidate();
        }
    }

    private void AttachEditors()
    {
        if (_originalEditor is not { } original || _modifiedEditor is not { } modified)
        {
            return;
        }

        original.IsReadOnly = true;
        original.LanguageId = LanguageId;
        original.Text = OriginalText;
        modified.IsReadOnly = IsReadOnly;
        modified.LanguageId = LanguageId;
        modified.Text = ModifiedText;

        original.TemplateApplied += OnOriginalTemplateApplied;
        HideOriginalVerticalScrollBar(original.GetVisualDescendants().OfType<ScrollBar>().FirstOrDefault(b => b.Name == CodeEditor.PartVerticalScrollBar));
        modified.TextChanged += OnModifiedTextChanged;
        original.DidScrollChange += OnOriginalScrollChanged;
        modified.DidScrollChange += OnModifiedScrollChanged;
        original.DidChangeViewModel += OnOriginalViewModelChanged;
        modified.DidChangeViewModel += OnModifiedViewModelChanged;
        original.DidChangeCursorSelection += OnOriginalCursorChanged;
        modified.DidChangeCursorSelection += OnModifiedCursorChanged;
        modified.DidContentSizeChange += OnContentSizeChanged;
        original.DidContentSizeChange += OnContentSizeChanged;

        RegisterDecorationStyles();
        UpdateGutterColumns();
        UpdateLayoutMode();
        ScheduleDiff(immediately: true);
    }

    private void DetachEditors()
    {
        CancelDiff();
        if (_originalEditor is { } original)
        {
            original.TemplateApplied -= OnOriginalTemplateApplied;
            original.DidScrollChange -= OnOriginalScrollChanged;
            original.DidChangeViewModel -= OnOriginalViewModelChanged;
            original.DidChangeCursorSelection -= OnOriginalCursorChanged;
            original.DidContentSizeChange -= OnContentSizeChanged;
        }

        if (_modifiedEditor is { } modified)
        {
            modified.TextChanged -= OnModifiedTextChanged;
            modified.DidScrollChange -= OnModifiedScrollChanged;
            modified.DidChangeViewModel -= OnModifiedViewModelChanged;
            modified.DidChangeCursorSelection -= OnModifiedCursorChanged;
            modified.DidContentSizeChange -= OnContentSizeChanged;
        }

        ClearDiffPresentation();
        DisposeDecorationStyles();
        RemoveGutterColumns();
        _movedBlocks?.Detach();
        _overviewRuler?.Detach();
        _originalEditor = null;
        _modifiedEditor = null;
        _diff = null;
    }

    private void OnOriginalTemplateApplied(object? sender, TemplateAppliedEventArgs e) =>
        HideOriginalVerticalScrollBar(e.NameScope.Find<ScrollBar>(CodeEditor.PartVerticalScrollBar));

    // upstream: the original editor has no vertical scroll bar of its own; it scrolls with the modified one, whose
    // bar and the overview ruler are at the right edge
    private static void HideOriginalVerticalScrollBar(ScrollBar? bar)
    {
        if (bar is not null)
        {
            bar.Visibility = ScrollBarVisibility.Hidden;
        }
    }

    private void OnModifiedTextChanged(object? sender, EventArgs e)
    {
        if (_modifiedEditor is not { } modified)
        {
            return;
        }

        _syncingModifiedText = true;
        try
        {
            SetCurrentValue(ModifiedTextProperty, modified.Text);
        }
        finally
        {
            _syncingModifiedText = false;
        }

        // the diff follows the edit after a pause (upstream: a 200 ms debounce)
        ScheduleDiff(immediately: false);
    }

    // Side by side: original | strip | modified | ruler. Inline: only the modified editor (the original editor stays in
    // the tree, hidden, so its model and tokens are there for the removed lines).
    private void UpdateLayoutMode()
    {
        if (_grid is not { } grid || _originalEditor is not { } original)
        {
            return;
        }

        var sideBySide = RenderSideBySide;
        grid.ColumnDefinitions[0].Width = sideBySide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        original.IsVisible = sideBySide;
        if (_movedBlocks is { } strip)
        {
            strip.IsVisible = sideBySide;
        }

        _overviewRuler?.InvalidateVisual();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F7 || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None)
        {
            return;
        }

        e.Handled = GoToDiff(next: (e.KeyModifiers & KeyModifiers.Shift) == 0);
    }
}
