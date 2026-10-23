using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The agent labels left of the plot, one 44 px row per agent lined up with the plot's rows: a color dot, the name
/// (14 px, medium, cut with an ellipsis) and under it the model's mark and name (12 px, muted). Below a 520 px
/// container the column narrows from 176 to 132 px and the model line hides (the span card still names the model).
/// </summary>
internal sealed class AgentTimelineLabelColumn : Control
{
    public static readonly StyledProperty<IBrush?> NameBrushProperty =
        AvaloniaProperty.Register<AgentTimelineLabelColumn, IBrush?>(nameof(NameBrush));

    public static readonly StyledProperty<IBrush?> ModelBrushProperty =
        AvaloniaProperty.Register<AgentTimelineLabelColumn, IBrush?>(nameof(ModelBrush));

    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<AgentTimelineLabelColumn, IBrush?>(nameof(MarkBrush));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<AgentTimelineLabelColumn, bool>(nameof(IsNarrow));

    private readonly StackPanel _rows = new() { Margin = new Thickness(0, AgentTimelineLayout.TopPadding, 0, 0) };
    private AgentTimelineModel _model = AgentTimelineModel.Empty;
    private Func<int, IBrush?>? _dot;

    public AgentTimelineLabelColumn()
    {
        VisualChildren.Add(_rows);
        LogicalChildren.Add(_rows);
    }

    public IBrush? NameBrush { get => GetValue(NameBrushProperty); set => SetValue(NameBrushProperty, value); }

    public IBrush? ModelBrush { get => GetValue(ModelBrushProperty); set => SetValue(ModelBrushProperty, value); }

    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <summary>The rows (tests).</summary>
    internal Avalonia.Controls.Controls Rows => _rows.Children;

    /// <summary>Shows the agents of <paramref name="model"/>; <paramref name="dot"/> gives each row's color.</summary>
    public void SetModel(AgentTimelineModel model, Func<int, IBrush?> dot)
    {
        _model = model;
        _dot = dot;
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NameBrushProperty || change.Property == ModelBrushProperty || change.Property == MarkBrushProperty ||
            change.Property == IsNarrowProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        var narrow = IsNarrow;
        Width = AgentTimelineLayout.LabelWidth(narrow);
        _rows.Margin = new Thickness(narrow ? AgentTimelineLayout.NarrowLabelLeftPadding : AgentTimelineLayout.LabelLeftPadding,
            AgentTimelineLayout.TopPadding, 12, 0);
        foreach (var row in _model.Rows)
        {
            var agent = row.Agent;
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("8,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = _dot?.Invoke(row.Index),
                VerticalAlignment = VerticalAlignment.Center
            });
            var name = new TextBlock
            {
                Text = agent.Name,
                FontSize = 14,
                LineHeight = 19.6,
                FontWeight = FontWeight.Medium,
                Foreground = NameBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);
            if (!narrow && agent.Model is { } model)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(10, 0, 0, 0) };
                line.Children.Add(new ArcGlyph
                {
                    Data = model.Mark ?? ArcIcons.Sparkles,
                    Size = 12,
                    Foreground = model.MarkBrush ?? MarkBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });
                line.Children.Add(new TextBlock
                {
                    Text = model.Name,
                    FontSize = 12,
                    LineHeight = 16.8,
                    Foreground = ModelBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetColumn(line, 1);
                Grid.SetRow(line, 1);
                grid.Children.Add(line);
            }
            var cell = new Border { Height = AgentTimelineLayout.RowHeight, Child = grid };
            var spoken = agent.Model is { } m ? $"{agent.Name}, {m.Name}" : agent.Name;
            if (!string.IsNullOrEmpty(agent.Role))
            {
                spoken += ", " + agent.Role;
            }
            AutomationProperties.SetName(cell, spoken);
            _rows.Children.Add(cell);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _rows.Measure(new Size(Width, double.PositiveInfinity));
        return new Size(Width, _rows.DesiredSize.Height + AgentTimelineLayout.TopPadding);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _rows.Arrange(new Rect(0, 0, finalSize.Width, _rows.DesiredSize.Height + AgentTimelineLayout.TopPadding));
        return finalSize;
    }
}

/// <summary>The legend's 14 × 8 swatches: the thinking tint, the tool fill and the waiting hatch, in the neutral color.</summary>
internal sealed class AgentTimelineLegendSwatch : Control
{
    private readonly AgentTimelineSpanKind _kind;
    private readonly AgentTimelinePalette _palette;

    public AgentTimelineLegendSwatch(AgentTimelineSpanKind kind, AgentTimelinePalette palette)
    {
        _kind = kind;
        _palette = palette;
        Width = 14;
        Height = 8;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, 14, 8);
        var box = new RoundedRect(rect, 3);
        switch (_kind)
        {
            case AgentTimelineSpanKind.Thinking:
                context.DrawRectangle(_palette.LegendThinking, null, box);
                break;
            case AgentTimelineSpanKind.Tool:
                context.DrawRectangle(_palette.NeutralBrush, null, box);
                break;
            default:
                // Hatch with a 4 px period, then a 1 px inset outline; no fill.
                using (context.PushClip(box))
                {
                    var pitch = 4 * Math.Sqrt(2);
                    for (var x = rect.Right - 0.5 * Math.Sqrt(2); x > -rect.Height - 1; x -= pitch)
                    {
                        context.DrawLine(_palette.LegendHatch, new Point(x - 1, rect.Bottom + 1), new Point(x + rect.Height + 1, rect.Top - 1));
                    }
                }
                context.DrawRectangle(null, _palette.LegendInset, new RoundedRect(rect.Deflate(0.5), 2.5));
                break;
        }
    }
}

/// <summary>
/// The footer: the playback controls on the left and the legend on the right, 16 apart; when both do not fit on one
/// line the legend moves under the controls (12 below) and wraps on its own.
/// </summary>
internal sealed class AgentTimelineFooterPanel : Panel
{
    public const double ColumnGap = 16;
    public const double RowGap = 12;

    /// <summary>True when the legend sits under the controls.</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        if (Children.Count < 2)
        {
            foreach (var only in Children)
            {
                only.Measure(availableSize);
            }
            var single = Children.Count == 1 ? Children[0].DesiredSize : default;
            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : single.Width, single.Height);
        }
        var controls = Children[0];
        var legend = Children[1];
        controls.Measure(infinite);
        legend.Measure(infinite);
        var first = controls.IsVisible ? controls.DesiredSize : default;
        var second = legend.DesiredSize;
        var available = double.IsFinite(availableSize.Width) ? availableSize.Width : first.Width + ColumnGap + second.Width;
        var gap = first.Width > 0 ? ColumnGap : 0;
        IsWrapped = first.Width > 0 && first.Width + gap + second.Width > available + 0.5;
        if (IsWrapped)
        {
            legend.Measure(new Size(available, double.PositiveInfinity));
            return new Size(available, first.Height + RowGap + legend.DesiredSize.Height);
        }
        if (first.Width <= 0)
        {
            legend.Measure(new Size(available, double.PositiveInfinity));
            second = legend.DesiredSize;
        }
        return new Size(available, Math.Max(first.Height, second.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            foreach (var only in Children)
            {
                only.Arrange(new Rect(0, 0, only.DesiredSize.Width, finalSize.Height));
            }
            return finalSize;
        }
        var controls = Children[0];
        var legend = Children[1];
        var first = controls.IsVisible ? controls.DesiredSize : default;
        if (IsWrapped)
        {
            controls.Arrange(new Rect(0, 0, first.Width, first.Height));
            legend.Arrange(new Rect(0, first.Height + RowGap, Math.Min(legend.DesiredSize.Width, finalSize.Width), legend.DesiredSize.Height));
            return finalSize;
        }
        controls.Arrange(new Rect(0, (finalSize.Height - first.Height) / 2, first.Width, first.Height));
        if (first.Width <= 0)
        {
            legend.Arrange(new Rect(0, (finalSize.Height - legend.DesiredSize.Height) / 2, Math.Min(legend.DesiredSize.Width, finalSize.Width),
                legend.DesiredSize.Height));
            return finalSize;
        }
        legend.Arrange(new Rect(finalSize.Width - legend.DesiredSize.Width, (finalSize.Height - legend.DesiredSize.Height) / 2,
            legend.DesiredSize.Width, legend.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>A solid 16 px glyph (the play triangle, the pause bars): filled and stroked in the text color with round joins.</summary>
internal sealed class AgentTimelineSolidGlyph : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<AgentTimelineSolidGlyph>();

    private readonly Geometry _data;
    private IPen? _pen;
    private IBrush? _penBrush;

    public AgentTimelineSolidGlyph(Geometry data)
    {
        _data = data;
        Width = 16;
        Height = 16;
        IsHitTestVisible = false;
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    static AgentTimelineSolidGlyph()
    {
        AffectsRender<AgentTimelineSolidGlyph>(ForegroundProperty);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _penBrush = brush;
            var color = brush is ISolidColorBrush solid ? solid.Color : Colors.Black;
            _pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.75, null, PenLineCap.Round, PenLineJoin.Round);
        }
        using (context.PushTransform(Matrix.CreateScale(16.0 / 24, 16.0 / 24)))
        {
            context.DrawGeometry(brush, _pen, _data);
        }
    }
}
