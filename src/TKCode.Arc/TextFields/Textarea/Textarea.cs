using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Arc textarea: a labeled multiline field (Enter adds a line) with the same helper / error rows as
/// <see cref="Input"/>; a live count in the description ("120 characters") rolls only the digits that change.
/// It fills its column with a 110px minimum height and, like Arc's <c>resize: vertical</c>, can be made taller
/// by dragging the corner grip (<see cref="IsResizable"/>). Focus draws Arc's 3px focus ring around the border.
/// Arc's textarea does not grow with its content, so neither does this one; it scrolls instead.
/// </summary>
[TemplatePart(ResizeGripPart, typeof(Control))]
public class Textarea : FieldTextBox
{
    public const string ResizeGripPart = "PART_ResizeGrip";

    /// <summary>Shows the corner grip that drags the field taller (Arc: <c>resize: vertical</c>). Default true.</summary>
    public static readonly StyledProperty<bool> IsResizableProperty =
        AvaloniaProperty.Register<Textarea, bool>(nameof(IsResizable), true);

    /// <summary>Height chosen with the grip, in px; NaN follows the minimum height.</summary>
    public static readonly StyledProperty<double> FrameHeightProperty =
        AvaloniaProperty.Register<Textarea, double>(nameof(FrameHeight), double.NaN);

    /// <summary>Shows the corner grip.</summary>
    public bool IsResizable { get => GetValue(IsResizableProperty); set => SetValue(IsResizableProperty, value); }

    /// <summary>Height chosen with the grip.</summary>
    public double FrameHeight { get => GetValue(FrameHeightProperty); set => SetValue(FrameHeightProperty, value); }

    private Control? _grip;
    private Point? _dragStart;
    private double _dragFrom;

    static Textarea()
    {
        AcceptsReturnProperty.OverrideDefaultValue<Textarea>(true);
        TextWrappingProperty.OverrideDefaultValue<Textarea>(TextWrapping.Wrap);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_grip is not null)
        {
            _grip.PointerPressed -= OnGripPressed;
            _grip.PointerMoved -= OnGripMoved;
            _grip.PointerReleased -= OnGripReleased;
            _grip.PointerCaptureLost -= OnGripCaptureLost;
        }
        base.OnApplyTemplate(e);
        _grip = e.NameScope.Find<Control>(ResizeGripPart);
        if (_grip is not null)
        {
            _grip.PointerPressed += OnGripPressed;
            _grip.PointerMoved += OnGripMoved;
            _grip.PointerReleased += OnGripReleased;
            _grip.PointerCaptureLost += OnGripCaptureLost;
        }
    }

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Frame is null || !e.GetCurrentPoint(_grip).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragStart = e.GetPosition(this);
        _dragFrom = Frame.Bounds.Height;
        e.Pointer.Capture(_grip);
        e.Handled = true;
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStart is not { } start || Frame is null)
        {
            return;
        }
        var delta = e.GetPosition(this).Y - start.Y;
        FrameHeight = Math.Max(Frame.MinHeight, Math.Round(_dragFrom + delta));
        e.Handled = true;
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }
        _dragStart = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnGripCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _dragStart = null;
}

/// <summary>The textarea's corner grip: two short diagonal strokes, like a browser's resize handle.</summary>
internal sealed class TextareaResizeGrip : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<TextareaResizeGrip>();

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private IPen? _pen;
    private IBrush? _penBrush;

    static TextareaResizeGrip()
    {
        AffectsRender<TextareaResizeGrip>(ForegroundProperty);
    }

    protected override Size MeasureOverride(Size availableSize) => new(12, 12);

    public override void Render(DrawingContext context)
    {
        // Hit area is the whole cell; the strokes sit in its lower right corner.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _penBrush = brush;
            _pen = new ImmutablePen(brush.ToImmutable(), 1, lineCap: PenLineCap.Round);
        }
        var w = Bounds.Width;
        var h = Bounds.Height;
        context.DrawLine(_pen, new Point(w - 1.5, h - 8.5), new Point(w - 8.5, h - 1.5));
        context.DrawLine(_pen, new Point(w - 1.5, h - 4.5), new Point(w - 4.5, h - 1.5));
    }
}
