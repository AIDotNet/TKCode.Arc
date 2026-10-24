using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Draws over a <see cref="WorkflowCanvas"/>'s view in screen coordinates: the selection rectangle while Shift + dragging
/// the empty canvas (the accent's subtle fill under a 1px accent outline), and a short hint next to the pointer, such as
/// why a connection cannot go to the step under it.
/// </summary>
internal sealed class WorkflowOverlayLayer : Control
{
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WorkflowOverlayLayer, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentSubtleBrushProperty =
        AvaloniaProperty.Register<WorkflowOverlayLayer, IBrush?>(nameof(AccentSubtleBrush));

    public static readonly StyledProperty<IBrush?> HintBrushProperty =
        AvaloniaProperty.Register<WorkflowOverlayLayer, IBrush?>(nameof(HintBrush));

    public static readonly StyledProperty<IBrush?> HintBackgroundProperty =
        AvaloniaProperty.Register<WorkflowOverlayLayer, IBrush?>(nameof(HintBackground));

    public static readonly StyledProperty<IBrush?> HintBorderBrushProperty =
        AvaloniaProperty.Register<WorkflowOverlayLayer, IBrush?>(nameof(HintBorderBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<WorkflowOverlayLayer, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<WorkflowOverlayLayer, double>(nameof(FontSize), 12);

    private Rect? _marquee;
    private IPen? _pen;
    private string? _hint;
    private Point _hintPoint;
    private TextLayout? _hintLayout;

    public IBrush? HintBrush
    {
        get => GetValue(HintBrushProperty);
        set => SetValue(HintBrushProperty, value);
    }

    public IBrush? HintBackground
    {
        get => GetValue(HintBackgroundProperty);
        set => SetValue(HintBackgroundProperty, value);
    }

    public IBrush? HintBorderBrush
    {
        get => GetValue(HintBorderBrushProperty);
        set => SetValue(HintBorderBrushProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>A short hint shown next to <see cref="HintPoint"/>, or null.</summary>
    public string? Hint
    {
        get => _hint;
        set
        {
            if (_hint != value)
            {
                _hint = value;
                _hintLayout?.Dispose();
                _hintLayout = null;
                InvalidateVisual();
            }
        }
    }

    /// <summary>The pointer the hint sits next to, in screen coordinates.</summary>
    public Point HintPoint
    {
        get => _hintPoint;
        set
        {
            if (_hintPoint != value)
            {
                _hintPoint = value;
                if (_hint is not null)
                {
                    InvalidateVisual();
                }
            }
        }
    }

    static WorkflowOverlayLayer()
    {
        AffectsRender<WorkflowOverlayLayer>(AccentBrushProperty, AccentSubtleBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WorkflowOverlayLayer>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowOverlayLayer>(AccessibilityView.Raw);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush? AccentSubtleBrush
    {
        get => GetValue(AccentSubtleBrushProperty);
        set => SetValue(AccentSubtleBrushProperty, value);
    }

    /// <summary>The selection rectangle in screen coordinates, or null.</summary>
    public Rect? Marquee
    {
        get => _marquee;
        set
        {
            if (_marquee != value)
            {
                _marquee = value;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty)
        {
            _pen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_marquee is { } rect && rect.Width > 0 && rect.Height > 0)
        {
            _pen ??= AccentBrush is { } accent ? new ImmutablePen(accent.ToImmutable(), 1) : null;
            // Half a pixel in, so the 1px outline sits on whole pixels.
            context.DrawRectangle(AccentSubtleBrush, _pen, rect.Deflate(0.5));
        }
        if (_hint is { Length: > 0 } hint)
        {
            _hintLayout ??= new TextLayout(hint, new Typeface(FontFamily), FontSize, HintBrush ?? Brushes.Black);
            var size = new Size(_hintLayout.WidthIncludingTrailingWhitespace + 16, _hintLayout.Height + 8);
            // Below and right of the pointer, kept inside the view.
            var x = Math.Min(_hintPoint.X + 14, Bounds.Width - size.Width - 4);
            var y = Math.Min(_hintPoint.Y + 18, Bounds.Height - size.Height - 4);
            var box = new Rect(new Point(Math.Max(4, x), Math.Max(4, y)), size);
            var border = HintBorderBrush is { } b ? new ImmutablePen(b.ToImmutable(), 1) : null;
            context.DrawRectangle(HintBackground, border, box.Deflate(0.5), 8, 8);
            _hintLayout.Draw(context, new Point(box.X + 8, box.Y + 4));
        }
    }
}
