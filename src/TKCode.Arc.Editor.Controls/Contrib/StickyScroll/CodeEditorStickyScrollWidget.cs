// Ported from vscode (src/vs/editor/contrib/stickyScroll/browser/stickyScrollWidget.ts: StickyScrollWidget, commit
// b57d5c9e), MIT license, as an Avalonia control that draws.
// Differences:
//  - the lines are drawn by the editor's view (CodeEditorView.RenderStickyLine: the line number and the text with its
//    token colors) instead of DOM line nodes; there are no folding icons, no focus / keyboard navigation of the lines
//    and no context menu;
//  - the colors come from the control theme (Themes/CodeEditorStickyScrollWidgetTheme.axaml): the line under the
//    widget (upstream's editorStickyScroll.border / shadow) and the hovered line (editorStickyScrollHover.background);
//    the background is the editor's (the controller binds it);
//  - a click reveals the line under the pointer at the column clicked (upstream's getEditorPositionFromNode).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Editor.Contrib.StickyScroll;

namespace TKCode.Arc;

/// <summary>The lines that stick at the top of the editor (upstream's StickyScrollWidget): an overlay widget at (0, 0).</summary>
internal sealed class CodeEditorStickyScrollWidget : Control, ICodeEditorOverlayWidget
{
    /// <summary>Defines the <see cref="Background"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<CodeEditorStickyScrollWidget, IBrush?>(nameof(Background));

    /// <summary>Defines the <see cref="BorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<CodeEditorStickyScrollWidget, IBrush?>(nameof(BorderBrush));

    /// <summary>Defines the <see cref="BorderThickness"/> property.</summary>
    public static readonly StyledProperty<double> BorderThicknessProperty =
        AvaloniaProperty.Register<CodeEditorStickyScrollWidget, double>(nameof(BorderThickness), 1);

    /// <summary>Defines the <see cref="HoverBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<CodeEditorStickyScrollWidget, IBrush?>(nameof(HoverBrush));

    private readonly CodeEditor _editor;
    private StickyScrollWidgetState _state = StickyScrollWidgetState.Empty;
    private int _hoveredIndex = -1;

    static CodeEditorStickyScrollWidget()
    {
        AffectsRender<CodeEditorStickyScrollWidget>(BackgroundProperty, BorderBrushProperty, BorderThicknessProperty, HoverBrushProperty);
        AffectsMeasure<CodeEditorStickyScrollWidget>(BorderThicknessProperty);
    }

    public CodeEditorStickyScrollWidget(CodeEditor editor)
    {
        _editor = editor;
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = true;
        IsVisible = false;
    }

    /// <summary>The widget's background (the editor's).</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>The line under the sticky lines.</summary>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>The height of the line under the sticky lines.</summary>
    public double BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }

    /// <summary>The background of the line under the pointer.</summary>
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    /// <inheritdoc />
    public string Id => "editor.contrib.stickyScrollWidget";

    /// <inheritdoc />
    Control ICodeEditorOverlayWidget.Control => this;

    /// <summary>The model lines shown, outermost first (upstream's lineNumbers).</summary>
    public IReadOnlyList<int> LineNumbers => _state.StartLineNumbers;

    /// <summary>The state shown.</summary>
    public StickyScrollWidgetState State => _state;

    /// <summary>The height the lines take (upstream's height).</summary>
    public double WidgetHeight
    {
        get
        {
            var count = _state.StartLineNumbers.Count;
            if (count == 0)
            {
                return 0;
            }

            return (count * LineHeight) + _state.LastLineRelativePosition + BorderThickness;
        }
    }

    private double LineHeight => _editor.View?.LineHeight ?? 19;

    /// <inheritdoc />
    public OverlayWidgetPosition? GetPosition() => new(null) { Coordinates = new Point(0, 0) };

    /// <summary>Shows the lines of <paramref name="state"/> (upstream's setState).</summary>
    public void SetState(StickyScrollWidgetState? state)
    {
        state ??= StickyScrollWidgetState.Empty;
        var changed = !_state.StateEquals(state);
        _state = state;
        IsVisible = state.StartLineNumbers.Count > 0;
        if (_hoveredIndex >= state.StartLineNumbers.Count)
        {
            _hoveredIndex = -1;
        }

        if (changed)
        {
            InvalidateMeasure();
        }

        InvalidateVisual();
    }

    /// <summary>The index of the sticky line at a y relative to the widget, or -1.</summary>
    public int GetLineIndexAt(double y)
    {
        var lineHeight = LineHeight;
        for (var i = 0; i < _state.StartLineNumbers.Count; i++)
        {
            var top = LineTop(i, lineHeight);
            if (y >= top && y < top + lineHeight)
            {
                return i;
            }
        }

        return -1;
    }

    private double LineTop(int index, double lineHeight) =>
        index == _state.StartLineNumbers.Count - 1 ? (index * lineHeight) + _state.LastLineRelativePosition : index * lineHeight;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _editor.GetLayoutSize().Width;
        return new Size(Math.Max(0, width), Math.Max(0, WidgetHeight));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var count = _state.StartLineNumbers.Count;
        if (count == 0 || _editor.View is not { } view)
        {
            return;
        }

        var lineHeight = LineHeight;
        var linesHeight = Math.Max(0, (count * lineHeight) + _state.LastLineRelativePosition);
        if (Background is { } background)
        {
            context.FillRectangle(background, new Rect(0, 0, bounds.Width, linesHeight));
        }

        using (context.PushClip(new Rect(0, 0, bounds.Width, linesHeight)))
        {
            // upstream gives the last line the lowest z-index: it slides under the line above it as its scope ends, so
            // it is drawn first and every other line covers it with its own background
            RenderLine(count - 1);
            for (var i = 0; i < count - 1; i++)
            {
                RenderLine(i);
            }
        }

        void RenderLine(int i)
        {
            var top = LineTop(i, lineHeight);
            var lineBackground = i == _hoveredIndex && HoverBrush is { } hover ? hover : Background;
            if (lineBackground is not null)
            {
                context.FillRectangle(lineBackground, new Rect(0, top, bounds.Width, lineHeight));
            }

            view.RenderStickyLine(context, _state.StartLineNumbers[i], top, bounds.Width);
        }

        if (BorderBrush is { } border && BorderThickness > 0)
        {
            context.FillRectangle(border, new Rect(0, linesHeight, bounds.Width, BorderThickness));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = GetLineIndexAt(e.GetPosition(this).Y);
        if (index != _hoveredIndex)
        {
            _hoveredIndex = index;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var index = GetLineIndexAt(point.Position.Y);
        if (index < 0)
        {
            return;
        }

        e.Handled = true;
        StickyScrollController.Get(_editor)?.OnLineClicked(index, point.Position.X, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }
}
