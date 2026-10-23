using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>A note line of the span card: an icon and a sentence (in the danger color for a failure).</summary>
internal readonly record struct AgentTimelineCardNote(Geometry Icon, string Text, bool IsDanger);

/// <summary>What the span card shows: always the span's final numbers, even while it still grows.</summary>
internal sealed class AgentTimelineCardInfo
{
    public int Span { get; init; } = -1;

    /// <summary>What the content says; the same span with a new key crossfades to the new content.</summary>
    public string Key { get; init; } = string.Empty;

    public IBrush? Dot { get; init; }

    public string Agent { get; init; } = string.Empty;

    public string? Model { get; init; }

    public string Label { get; init; } = string.Empty;

    /// <summary>The tool's name, or null to show <see cref="Kind"/> instead.</summary>
    public string? Tool { get; init; }

    public Geometry? ToolMark { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Duration { get; init; } = string.Empty;

    public string? Tokens { get; init; }

    public IReadOnlyList<AgentTimelineCardNote> Notes { get; init; } = [];
}

/// <summary>
/// The span card of an <see cref="AgentTimeline"/>: 280 wide, anchored inside the component above the span (below it
/// when there is no room above), clamped 8 px from the sides. It shows the agent and model, what the span did, the tool or
/// kind with the duration and tokens, and notes for failures, retries and handoffs. Motion as measured: it appears with
/// a 160 ms fade while rising 3 px on the Smooth spring; between spans it glides on a critically damped spring (ω ≈ 20,
/// <c>SpringSpec.FromVisual(0.26, 0)</c>), springs to its new height on Smooth and crossfades its content (old out in
/// 100 ms, new in on 160 ms); it leaves after an 80 ms grace with a 120 ms fade. Reduced motion jumps the position and
/// height and keeps the fades. Code-driven because the position, height and opacity follow the pointer from span to span
/// with retargeting springs that a ControlTheme transition cannot keep the velocity of. Hidden from assistive technology:
/// the span's own name already says all of it.
/// </summary>
internal sealed class AgentTimelineCard : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<AgentTimelineCard, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<AgentTimelineCard, IBrush?>(nameof(BorderBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<AgentTimelineCard, CornerRadius>(nameof(CornerRadius), new CornerRadius(18));

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<AgentTimelineCard, BoxShadows>(nameof(BoxShadow));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<AgentTimelineCard, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<AgentTimelineCard, IBrush?>(nameof(SecondaryBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<AgentTimelineCard, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<AgentTimelineCard, IBrush?>(nameof(DangerBrush));

    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<AgentTimelineCard, double>(nameof(CardWidth), 280);

    public static readonly StyledProperty<Thickness> CardPaddingProperty =
        AvaloniaProperty.Register<AgentTimelineCard, Thickness>(nameof(CardPadding), new Thickness(14, 12));

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<AgentTimelineCard, double>(nameof(RowGap), 3);

    /// <summary>The glide between spans: critically damped, ω ≈ 20 rad/s (measured).</summary>
    private static readonly SpringSpec GlideSpring = AiGlyphMotion.Settle;

    private static readonly TimeSpan EnterFade = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ExitFade = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ExitGrace = TimeSpan.FromMilliseconds(80);

    private readonly Border _frame;
    private readonly TranslateTransform _translate = new();
    private readonly MotionValue<Point> _position;
    private readonly MotionValue<double> _rise;
    private readonly MotionValue<double> _fade;
    private ArcContentSwap? _swap;
    private bool _shown;
    private int _span = -1;
    private string _key = string.Empty;

    public AgentTimelineCard()
    {
        IsHitTestVisible = false;
        Focusable = false;
        IsVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        _frame = new Border { ClipToBounds = true, BorderThickness = new Thickness(1) };
        VisualChildren.Add(_frame);
        LogicalChildren.Add(_frame);
        RenderTransform = _translate;
        _position = new MotionValue<Point>(this, default, MotionVectors.Point);
        _rise = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _position.Changed += _ => ApplyMotion();
        _rise.Changed += _ => ApplyMotion();
        _fade.Changed += _ => ApplyMotion();
        Opacity = 0;
        ApplyFrame();
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    public double CardWidth { get => GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }

    public Thickness CardPadding { get => GetValue(CardPaddingProperty); set => SetValue(CardPaddingProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>True while the card is meant to show (it may still be fading in).</summary>
    public bool IsShown => _shown;

    /// <summary>The span shown, or −1.</summary>
    public int Span => _span;

    /// <summary>Where the card is heading (its top-left corner in the layer).</summary>
    public Point Target => _position.Target;

    /// <summary>The height the card is heading to (its content's height).</summary>
    public double TargetHeight { get; private set; }

    /// <summary>The content shown now (tests read its texts).</summary>
    internal Control? CurrentContent => _swap?.Content as Control;

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BackgroundProperty || change.Property == BorderBrushProperty || change.Property == CornerRadiusProperty ||
            change.Property == BoxShadowProperty || change.Property == CardWidthProperty)
        {
            ApplyFrame();
        }
    }

    private void ApplyFrame()
    {
        _frame.Background = Background;
        _frame.BorderBrush = BorderBrush;
        _frame.CornerRadius = CornerRadius;
        _frame.BoxShadow = BoxShadow;
        _frame.Width = CardWidth;
    }

    /// <summary>
    /// Shows <paramref name="info"/>; <paramref name="place"/> gets the card's height and returns its top-left corner. A
    /// hidden card appears there; a showing one glides there and crossfades.
    /// </summary>
    public void Show(AgentTimelineCardInfo info, Func<double, Point> place)
    {
        var full = ArcMotion.IsFull(this);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_shown && info.Span == _span && info.Key == _key && IsVisible)
        {
            Move(place(TargetHeight), full);
            return;
        }
        var content = Build(info);
        content.Measure(new Size(Math.Max(0, CardWidth - 2), double.PositiveInfinity));
        TargetHeight = content.DesiredSize.Height + 2;
        var target = place(TargetHeight);
        var appearing = !IsVisible;
        _shown = true;
        _span = info.Span;
        _key = info.Key;
        if (appearing)
        {
            // A fresh decorator and swap: the first measure jumps to the content's height and the content does not fade.
            _swap = new ArcContentSwap
            {
                Mode = ContentSwapMode.Label,
                EnterOffset = 0,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = content
            };
            _frame.Child = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, Child = _swap };
            IsVisible = true;
            _position.Jump(target);
            if (full)
            {
                _rise.Jump(3);
                _ = _rise.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _rise.Jump(0);
            }
            if (level == ArcMotionLevel.Off)
            {
                _fade.Jump(1);
            }
            else
            {
                _fade.Jump(0);
                _ = _fade.AnimateTo(1, MotionSpec.Tween(EnterFade, ArcEasings.Enter));
            }
        }
        else
        {
            if (_swap is not null)
            {
                _swap.Content = content;
            }
            Move(target, full);
            if (level == ArcMotionLevel.Off)
            {
                _fade.Jump(1);
            }
            else if (_fade.Target < 1)
            {
                _ = _fade.AnimateTo(1, MotionSpec.Tween(EnterFade, ArcEasings.Enter));
            }
        }
        ApplyMotion();
    }

    private void Move(Point target, bool full)
    {
        if (_position.Target == target)
        {
            return;
        }
        if (full)
        {
            _ = _position.AnimateTo(target, MotionSpec.Spring(GlideSpring));
        }
        else
        {
            _position.Jump(target);
        }
    }

    /// <summary>Hides the card: after an 80 ms grace it fades out over 120 ms (at once with <paramref name="animate"/> false).</summary>
    public void Hide(bool animate = true)
    {
        if (!_shown && !IsVisible)
        {
            return;
        }
        _shown = false;
        _span = -1;
        if (!animate || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _fade.Jump(0);
            IsVisible = false;
            ApplyMotion();
            return;
        }
        _ = FadeOut();
    }

    private async Task FadeOut()
    {
        var done = await _fade.AnimateTo(0, MotionSpec.Tween(ExitFade, ArcEasings.Standard, ExitGrace));
        if (done && !_shown)
        {
            IsVisible = false;
        }
    }

    private void ApplyMotion()
    {
        var position = _position.Value;
        _translate.X = position.X;
        _translate.Y = position.Y + _rise.Value;
        Opacity = Math.Clamp(_fade.Value, 0, 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _frame.Measure(new Size(CardWidth, double.PositiveInfinity));
        return _frame.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _frame.Arrange(new Rect(_frame.DesiredSize));
        return _frame.DesiredSize;
    }

    // ---- Content -----------------------------------------------------------------------------------------------------

    private Control Build(AgentTimelineCardInfo info)
    {
        var rows = new StackPanel { Spacing = RowGap, Margin = CardPadding };

        // Head: the agent's dot, name and model.
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        head.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = info.Dot,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });
        var agent = Text(info.Agent, 12, 16.8, TextBrush, FontWeight.Medium);
        Grid.SetColumn(agent, 1);
        head.Children.Add(agent);
        if (!string.IsNullOrEmpty(info.Model))
        {
            var model = Text(info.Model!, 12, 16.8, MutedBrush);
            model.Margin = new Thickness(6, 0, 0, 0);
            model.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(model, 2);
            head.Children.Add(model);
        }
        rows.Children.Add(head);

        // What it did.
        var label = Text(info.Label, 14, 19.6, TextBrush);
        label.TextWrapping = TextWrapping.Wrap;
        rows.Children.Add(label);

        // Tool or kind · duration · tokens.
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 0) };
        if (info.Tool is { } tool)
        {
            var toolRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            toolRow.Children.Add(new ArcGlyph
            {
                Data = info.ToolMark ?? ArcIcons.Wrench,
                Size = 12,
                Foreground = SecondaryBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            toolRow.Children.Add(Text(tool, 12, 16.8, SecondaryBrush));
            meta.Children.Add(toolRow);
        }
        else
        {
            meta.Children.Add(Text(info.Kind, 12, 16.8, SecondaryBrush));
        }
        meta.Children.Add(Text("·", 12, 16.8, MutedBrush));
        meta.Children.Add(Tabular(Text(info.Duration, 12, 16.8, SecondaryBrush)));
        if (info.Tokens is { } tokens)
        {
            meta.Children.Add(Text("·", 12, 16.8, MutedBrush));
            meta.Children.Add(Tabular(Text(tokens, 12, 16.8, SecondaryBrush)));
        }
        rows.Children.Add(meta);

        // Notes: failure, retry, handoffs.
        foreach (var note in info.Notes)
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 2, 0, 0) };
            var brush = note.IsDanger ? DangerBrush : SecondaryBrush;
            line.Children.Add(new ArcGlyph
            {
                Data = note.Icon,
                Size = 13,
                Foreground = brush,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1.9, 6, 0)
            });
            var text = Text(note.Text, 12, 16.8, brush);
            text.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(text, 1);
            line.Children.Add(text);
            rows.Children.Add(line);
        }
        return rows;
    }

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private static TextBlock Tabular(TextBlock text)
    {
        text.FontFeatures = s_tabular;
        return text;
    }

    private static TextBlock Text(string text, double size, double lineHeight, IBrush? brush, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        LineHeight = lineHeight,
        Foreground = brush,
        FontWeight = weight ?? FontWeight.Normal,
        VerticalAlignment = VerticalAlignment.Center
    };
}
