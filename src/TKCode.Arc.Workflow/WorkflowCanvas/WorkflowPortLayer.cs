using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>A port as the port layer draws it: where, whether it is the target of a connection, and its label.</summary>
internal readonly record struct WorkflowPortMark(Point Point, bool Output, bool Highlight, string? Label, bool ShowCircle,
    bool Vertical);

/// <summary>
/// Draws above the cards of a <see cref="WorkflowCanvas"/>, in canvas coordinates: the ports of the cards that show them
/// (a 10px ring in the wire color on the surface; 12px in the accent for the port a connection would go to), every
/// labelled port's label (a small pill outside the card), and the line of a connection being made.
/// </summary>
internal sealed class WorkflowPortLayer : Control
{
    public static readonly StyledProperty<IBrush?> WireBrushProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, IBrush?>(nameof(WireBrush));

    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, IBrush?>(nameof(SurfaceBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> LabelBorderBrushProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, IBrush?>(nameof(LabelBorderBrush));

    public static readonly StyledProperty<double> PortSizeProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, double>(nameof(PortSize), 10);

    public static readonly StyledProperty<double> PortHoverSizeProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, double>(nameof(PortHoverSize), 12);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<WorkflowPortLayer, double>(nameof(FontSize), 12);

    private readonly Dictionary<string, TextLayout> _labels = new();
    private IPen? _ringPen;
    private IPen? _accentPen;
    private IPen? _previewPen;
    private IPen? _labelPen;

    static WorkflowPortLayer()
    {
        AffectsRender<WorkflowPortLayer>(WireBrushProperty, SurfaceBrushProperty, AccentBrushProperty, LabelBrushProperty,
            LabelBorderBrushProperty, PortSizeProperty, PortHoverSizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WorkflowPortLayer>(false);
        ClipToBoundsProperty.OverrideDefaultValue<WorkflowPortLayer>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowPortLayer>(AccessibilityView.Raw);
    }

    public IBrush? WireBrush
    {
        get => GetValue(WireBrushProperty);
        set => SetValue(WireBrushProperty, value);
    }

    public IBrush? SurfaceBrush
    {
        get => GetValue(SurfaceBrushProperty);
        set => SetValue(SurfaceBrushProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public IBrush? LabelBorderBrush
    {
        get => GetValue(LabelBorderBrushProperty);
        set => SetValue(LabelBorderBrushProperty, value);
    }

    public double PortSize
    {
        get => GetValue(PortSizeProperty);
        set => SetValue(PortSizeProperty, value);
    }

    public double PortHoverSize
    {
        get => GetValue(PortHoverSizeProperty);
        set => SetValue(PortHoverSizeProperty, value);
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

    /// <summary>The world rectangle this layer covers; drawing happens in world coordinates.</summary>
    public Rect Extent { get; set; }

    /// <summary>The ports to draw (the canvas fills it).</summary>
    public List<WorkflowPortMark> Marks { get; } = new();

    /// <summary>The line of a connection being made, or null.</summary>
    public Geometry? Preview { get; set; }

    /// <summary>Whether the preview reaches a port it can connect to (drawn solid) or follows the pointer (fainter).</summary>
    public bool PreviewSnapped { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WireBrushProperty)
        {
            _ringPen = null;
        }
        else if (change.Property == AccentBrushProperty)
        {
            _accentPen = null;
            _previewPen = null;
        }
        else if (change.Property == LabelBorderBrushProperty)
        {
            _labelPen = null;
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == LabelBrushProperty)
        {
            foreach (var layout in _labels.Values)
            {
                layout.Dispose();
            }
            _labels.Clear();
        }
    }

    public override void Render(DrawingContext context)
    {
        using var _ = context.PushTransform(Matrix.CreateTranslation(-Extent.X, -Extent.Y));
        if (Preview is { } preview && AccentBrush is ISolidColorBrush accent)
        {
            _previewPen ??= new ImmutablePen(new ImmutableSolidColorBrush(accent.Color), 1.75, lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Round);
            using (context.PushOpacity(PreviewSnapped ? 1 : 0.6))
            {
                context.DrawGeometry(null, _previewPen, preview);
            }
        }
        foreach (var mark in Marks)
        {
            if (mark.Label is { Length: > 0 } label)
            {
                DrawLabel(context, mark, label);
            }
        }
        _ringPen ??= WireBrush is { } wire ? new ImmutablePen(wire.ToImmutable(), 1.5) : null;
        _accentPen ??= AccentBrush is { } a ? new ImmutablePen(a.ToImmutable(), 1.5) : null;
        foreach (var mark in Marks)
        {
            if (!mark.ShowCircle)
            {
                continue;
            }
            var radius = (mark.Highlight ? PortHoverSize : PortSize) / 2;
            context.DrawEllipse(SurfaceBrush, mark.Highlight ? _accentPen : _ringPen, mark.Point, radius - 0.75, radius - 0.75);
        }
    }

    /// <summary>A label pill just outside the card, past the port (Arc's code chip: 1px subtle border, radius 6, padding 1 7).</summary>
    private void DrawLabel(DrawingContext context, WorkflowPortMark mark, string label)
    {
        if (!_labels.TryGetValue(label, out var layout))
        {
            layout = new TextLayout(label, new Typeface(FontFamily), FontSize, LabelBrush ?? Brushes.Gray);
            _labels[label] = layout;
        }
        var size = new Size(layout.WidthIncludingTrailingWhitespace + 14, layout.Height + 2);
        // Outside the card, past the port's circle: below an output and above an input when the workflow runs down,
        // right of an output and left of an input when it runs across.
        var gap = PortSize / 2 + 4;
        var origin = (mark.Vertical, mark.Output) switch
        {
            (true, true) => new Point(mark.Point.X - size.Width / 2, mark.Point.Y + gap),
            (true, false) => new Point(mark.Point.X - size.Width / 2, mark.Point.Y - gap - size.Height),
            (false, true) => new Point(mark.Point.X + gap, mark.Point.Y - size.Height / 2),
            _ => new Point(mark.Point.X - gap - size.Width, mark.Point.Y - size.Height / 2)
        };
        var rect = new Rect(origin, size);
        _labelPen ??= LabelBorderBrush is { } border ? new ImmutablePen(border.ToImmutable(), 1) : null;
        context.DrawRectangle(SurfaceBrush, _labelPen, rect.Deflate(0.5), 6, 6);
        layout.Draw(context, new Point(origin.X + 7, origin.Y + 1));
    }
}
