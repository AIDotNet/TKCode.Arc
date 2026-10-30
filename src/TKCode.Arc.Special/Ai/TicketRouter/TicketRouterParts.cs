using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The color a <see cref="TicketRouterChip"/> gives its glyph.</summary>
internal enum TicketRouterTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger
}

/// <summary>A label of the classifier strip: a glyph and a word on a muted pill. The glyph's tone never carries meaning alone.</summary>
[PseudoClasses(":accent", ":success", ":warning", ":danger")]
internal sealed class TicketRouterChip : TemplatedControl
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<TicketRouterChip, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TicketRouterChip, string?>(nameof(Text));

    public static readonly StyledProperty<TicketRouterTone> ToneProperty =
        AvaloniaProperty.Register<TicketRouterChip, TicketRouterTone>(nameof(Tone));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public TicketRouterTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
        {
            var tone = Tone;
            PseudoClasses.Set(":accent", tone == TicketRouterTone.Accent);
            PseudoClasses.Set(":success", tone == TicketRouterTone.Success);
            PseudoClasses.Set(":warning", tone == TicketRouterTone.Warning);
            PseudoClasses.Set(":danger", tone == TicketRouterTone.Danger);
        }
    }
}

/// <summary>The confidence: a short meter and the percentage. Below the review threshold the fill turns to the warning color.</summary>
[TemplatePart("PART_Fill", typeof(Border))]
[PseudoClasses(":low")]
internal sealed class TicketRouterMeter : TemplatedControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<TicketRouterMeter, double>(nameof(Value));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TicketRouterMeter, string?>(nameof(Text));

    public static readonly StyledProperty<bool> ShowsBarProperty =
        AvaloniaProperty.Register<TicketRouterMeter, bool>(nameof(ShowsBar), true);

    public static readonly StyledProperty<bool> IsLowProperty =
        AvaloniaProperty.Register<TicketRouterMeter, bool>(nameof(IsLow));

    private Border? _fill;

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool ShowsBar { get => GetValue(ShowsBarProperty); set => SetValue(ShowsBarProperty, value); }

    public bool IsLow { get => GetValue(IsLowProperty); set => SetValue(IsLowProperty, value); }

    /// <summary>The fill's width drawn now (tests).</summary>
    internal double FillWidth => _fill?.Width ?? 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _fill = e.NameScope.Find<Border>("PART_Fill");
        UpdateFill();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            UpdateFill();
        }
        else if (change.Property == IsLowProperty)
        {
            PseudoClasses.Set(":low", IsLow);
        }
    }

    private void UpdateFill()
    {
        if (_fill?.Parent is Avalonia.Layout.Layoutable track)
        {
            var width = double.IsNaN(track.Width) ? 0 : track.Width;
            _fill.Width = Math.Round(Math.Clamp(Value, 0, 1) * width);
        }
    }
}

/// <summary>
/// The incoming ticket: the channel glyph and name, the customer and company, when it arrived, the subject and the first
/// words. Every line is one line high and cut with an ellipsis, so the card never changes height. The subject line is the
/// piece the ticket's flight starts from (<see cref="FlightKey"/>).
/// </summary>
[TemplatePart("PART_Flight", typeof(Control))]
internal sealed class TicketRouterTicketCard : TemplatedControl
{
    public static readonly StyledProperty<Geometry?> ChannelGlyphProperty =
        AvaloniaProperty.Register<TicketRouterTicketCard, Geometry?>(nameof(ChannelGlyph));

    public static readonly StyledProperty<string?> ChannelProperty =
        AvaloniaProperty.Register<TicketRouterTicketCard, string?>(nameof(Channel));

    public static readonly StyledProperty<string?> CustomerProperty =
        AvaloniaProperty.Register<TicketRouterTicketCard, string?>(nameof(Customer));

    public static readonly StyledProperty<string?> CompanyProperty =
        AvaloniaProperty.Register<TicketRouterTicketCard, string?>(nameof(Company));

    public static readonly StyledProperty<string?> ReceivedAtProperty =
        AvaloniaProperty.Register<TicketRouterTicketCard, string?>(nameof(ReceivedAt));

    public static readonly StyledProperty<string?> SubjectProperty =
        AvaloniaProperty.Register<TicketRouterTicketCard, string?>(nameof(Subject));

    public static readonly StyledProperty<string?> PreviewProperty =
        AvaloniaProperty.Register<TicketRouterTicketCard, string?>(nameof(Preview));

    private Control? _flight;
    private string? _flightKey;

    public Geometry? ChannelGlyph { get => GetValue(ChannelGlyphProperty); set => SetValue(ChannelGlyphProperty, value); }

    public string? Channel { get => GetValue(ChannelProperty); set => SetValue(ChannelProperty, value); }

    public string? Customer { get => GetValue(CustomerProperty); set => SetValue(CustomerProperty, value); }

    public string? Company { get => GetValue(CompanyProperty); set => SetValue(CompanyProperty, value); }

    public string? ReceivedAt { get => GetValue(ReceivedAtProperty); set => SetValue(ReceivedAtProperty, value); }

    public string? Subject { get => GetValue(SubjectProperty); set => SetValue(SubjectProperty, value); }

    public string? Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }

    /// <summary>The shared-element key of the subject line: the key of the row this ticket will become.</summary>
    public string? FlightKey
    {
        get => _flightKey;
        set
        {
            _flightKey = value;
            if (_flight is not null)
            {
                SharedElement.SetKey(_flight, value);
            }
        }
    }

    public void Show(TicketMessage message, TicketRouterLabels labels)
    {
        ChannelGlyph = TicketRouterGlyphs.Channel(message.Channel);
        Channel = labels.Channel(message.Channel);
        Customer = message.Customer;
        Company = string.IsNullOrEmpty(message.Company) ? null : "· " + message.Company;
        ReceivedAt = message.ReceivedAt;
        Subject = message.Subject;
        Preview = message.Preview;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _flight = e.NameScope.Find<Control>("PART_Flight");
        if (_flight is not null)
        {
            SharedElement.SetKey(_flight, _flightKey);
        }
    }
}

/// <summary>The stage when there is no ticket: "No tickets yet" or "All caught up", with a line under it.</summary>
internal sealed class TicketRouterNotice : TemplatedControl
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<TicketRouterNotice, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TicketRouterNotice, string?>(nameof(Text));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<TicketRouterNotice, string?>(nameof(Hint));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
}

/// <summary>
/// One ticket in a lane: the assignee's avatar, the subject and a meta line. A review ticket is a real button (its name
/// says the subject, the confidence and the lane it goes to); a team ticket is not focusable. A landing lights the row
/// with the accent tint, which then fades.
/// </summary>
[TemplatePart("PART_Highlight", typeof(Border))]
[PseudoClasses(":review")]
internal sealed class TicketRouterRow : AvaloniaButton
{
    public static readonly StyledProperty<string?> SubjectProperty =
        AvaloniaProperty.Register<TicketRouterRow, string?>(nameof(Subject));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<TicketRouterRow, string?>(nameof(Meta));

    public static readonly StyledProperty<string?> AssigneeProperty =
        AvaloniaProperty.Register<TicketRouterRow, string?>(nameof(Assignee));

    public static readonly StyledProperty<bool> IsReviewProperty =
        AvaloniaProperty.Register<TicketRouterRow, bool>(nameof(IsReview));

    private Border? _highlight;
    private MotionValue<double>? _glow;

    public TicketRouterRow(RoutedTicket ticket)
    {
        Ticket = ticket;
        SharedElement.SetKey(this, ticket.Key);
        SharedElement.SetMode(this, SharedElementMode.Position);
        Focusable = false;
        IsTabStop = false;
        IsHitTestVisible = false;
    }

    protected override Type StyleKeyOverride => typeof(TicketRouterRow);

    public RoutedTicket Ticket { get; }

    public string? Subject { get => GetValue(SubjectProperty); set => SetValue(SubjectProperty, value); }

    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    public string? Assignee { get => GetValue(AssigneeProperty); set => SetValue(AssigneeProperty, value); }

    public bool IsReview { get => GetValue(IsReviewProperty); set => SetValue(IsReviewProperty, value); }

    /// <summary>The landing highlight's opacity drawn now (tests).</summary>
    internal double HighlightOpacity => _glow?.Value ?? 0;

    /// <summary>Lights the row and lets the tint fade (kept with reduced motion; Off shows the end state).</summary>
    public void Highlight()
    {
        _glow ??= CreateGlow();
        _glow.Jump(1);
        ApplyGlow(1);
        _ = _glow.AnimateTo(0, MotionSpec.Tween(TicketRouterMotion.HighlightFade, ArcEasings.Standard, TicketRouterMotion.HighlightHold));
    }

    private MotionValue<double> CreateGlow()
    {
        var glow = new MotionValue<double>(this, 0, MotionVectors.Double);
        glow.Changed += ApplyGlow;
        return glow;
    }

    private void ApplyGlow(double value)
    {
        if (_highlight is not null)
        {
            _highlight.Opacity = Math.Clamp(value, 0, 1);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _highlight = e.NameScope.Find<Border>("PART_Highlight");
        ApplyGlow(_glow?.Value ?? 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsReviewProperty)
        {
            var review = IsReview;
            PseudoClasses.Set(":review", review);
            Focusable = review;
            IsTabStop = review;
            IsHitTestVisible = review;
        }
    }
}

/// <summary>
/// The box a lane row lives in: it enters by springing its height from 0 to the row's measured height and leaves by
/// springing back to 0 while the row fades, on the bounce-free 0.24s spring. Code-driven because the target is the row's
/// measured height and the removal waits for the spring to settle; a ControlTheme transition can express neither. The box
/// clips only while it shrinks, so a row flying in from the stage is never cut by its own box.
/// </summary>
internal sealed class TicketRouterRowSlot : Control
{
    private readonly RectangleGeometry _clip = new();
    private readonly MotionValue<double> _height;
    private double _natural;
    private bool _entering;
    private bool _follows;

    public TicketRouterRowSlot(TicketRouterRow row, bool animate)
    {
        Row = row;
        VisualChildren.Add(row);
        LogicalChildren.Add(row);
        _height = new MotionValue<double>(this, 0, MotionVectors.Double);
        _height.Changed += _ =>
        {
            InvalidateMeasure();
            UpdateClip();
        };
        _entering = animate;
        _follows = !animate;
    }

    public TicketRouterRow Row { get; }

    /// <summary>True once the slot started leaving.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>The height drawn now (tests).</summary>
    internal double DisplayedHeight => _follows ? _natural : Math.Max(0, _height.Value);

    /// <summary>Springs to 0 while the row fades, then raises <paramref name="done"/>.</summary>
    public void Leave(Action<TicketRouterRowSlot> done)
    {
        if (IsLeaving)
        {
            return;
        }
        IsLeaving = true;
        Row.IsHitTestVisible = false;
        if (_follows)
        {
            _follows = false;
            _height.Jump(_natural);
        }
        _ = LeaveAsync(done);
    }

    private async Task LeaveAsync(Action<TicketRouterRowSlot> done)
    {
        if (ArcMotion.IsFull(this))
        {
            var fade = new MotionValue<double>(Row, Row.Opacity, MotionVectors.Double);
            fade.Changed += value => Row.Opacity = Math.Clamp(value, 0, 1);
            _ = fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        await _height.AnimateTo(0, MotionSpec.Spring(TicketRouterMotion.RowHeight));
        done(this);
    }

    private async Task EnterAsync()
    {
        if (await _height.AnimateTo(_natural, MotionSpec.Spring(TicketRouterMotion.RowHeight)) && !IsLeaving)
        {
            _follows = true;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Row.Measure(availableSize.WithHeight(double.PositiveInfinity));
        _natural = Row.DesiredSize.Height;
        if (_entering)
        {
            _entering = false;
            _height.Jump(0);
            _ = EnterAsync();
        }
        var height = _follows ? _natural : Math.Max(0, _height.Value);
        return new Size(Row.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Row.Arrange(new Rect(0, 0, finalSize.Width, _natural));
        UpdateClip();
        return finalSize;
    }

    private void UpdateClip()
    {
        if (!IsLeaving)
        {
            Clip = null;
            return;
        }
        _clip.Rect = new Rect(-2, 0, Bounds.Width + 4, Math.Max(0, _height.Value));
        Clip = _clip;
        InvalidateVisual();
    }
}

/// <summary>
/// A lane: its name (with an icon for the review lane) and a count that rolls in reserved tabular width, over a list of
/// row slots of a fixed height (the visible rows), newest first. Each lane renders only its visible rows.
/// </summary>
[TemplatePart("PART_Rows", typeof(StackPanel))]
[PseudoClasses(":review")]
internal sealed class TicketRouterLaneView : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TicketRouterLaneView, string?>(nameof(Label));

    public static readonly StyledProperty<double> CountProperty =
        AvaloniaProperty.Register<TicketRouterLaneView, double>(nameof(Count));

    public static readonly StyledProperty<bool> IsReviewProperty =
        AvaloniaProperty.Register<TicketRouterLaneView, bool>(nameof(IsReview));

    public static readonly StyledProperty<double> RowsHeightProperty =
        AvaloniaProperty.Register<TicketRouterLaneView, double>(nameof(RowsHeight));

    public static readonly StyledProperty<int> VisibleRowsProperty =
        AvaloniaProperty.Register<TicketRouterLaneView, int>(nameof(VisibleRows), 3);

    /// <summary>A row's height plus the space under it (from the token).</summary>
    public static readonly StyledProperty<double> RowExtentProperty =
        AvaloniaProperty.Register<TicketRouterLaneView, double>(nameof(RowExtent), 46);

    private StackPanel? _rows;

    public TicketRouterLaneView(TicketLane lane)
    {
        Lane = lane;
        RowsHeight = VisibleRows * RowExtent;
    }

    public TicketLane Lane { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public double Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    public bool IsReview { get => GetValue(IsReviewProperty); set => SetValue(IsReviewProperty, value); }

    /// <summary>The fixed height of the list: the visible rows and the space under each.</summary>
    public double RowsHeight { get => GetValue(RowsHeightProperty); set => SetValue(RowsHeightProperty, value); }

    public int VisibleRows { get => GetValue(VisibleRowsProperty); set => SetValue(VisibleRowsProperty, value); }

    public double RowExtent { get => GetValue(RowExtentProperty); set => SetValue(RowExtentProperty, value); }

    /// <summary>The row slots on screen, top to bottom, leaving ones included.</summary>
    internal IEnumerable<TicketRouterRowSlot> Slots => _rows?.Children.OfType<TicketRouterRowSlot>() ?? [];

    /// <summary>The rows that are staying, top to bottom.</summary>
    internal IEnumerable<TicketRouterRow> Rows => Slots.Where(s => !s.IsLeaving).Select(s => s.Row);

    /// <summary>Raised when a review row is pressed.</summary>
    public event Action<RoutedTicket>? ReviewPressed;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var previous = _rows;
        _rows = e.NameScope.Find<StackPanel>("PART_Rows");
        if (previous is not null && _rows is not null && !ReferenceEquals(previous, _rows))
        {
            var slots = previous.Children.ToList();
            previous.Children.Clear();
            _rows.Children.AddRange(slots);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsReviewProperty)
        {
            PseudoClasses.Set(":review", IsReview);
        }
        else if (change.Property == VisibleRowsProperty || change.Property == RowExtentProperty)
        {
            RowsHeight = VisibleRows * RowExtent;
        }
    }

    /// <summary>
    /// Brings the rows in line with <paramref name="tickets"/> (newest first, already cut to the visible count): rows that
    /// left the list leave, new rows enter at their place, rows that stay keep their control.
    /// </summary>
    public void Sync(IReadOnlyList<RoutedTicket> tickets, bool animate, Func<RoutedTicket, TicketRouterRow> create)
    {
        if (_rows is null)
        {
            ApplyTemplate();
        }
        if (_rows is null)
        {
            return;
        }
        var wanted = new HashSet<RoutedTicket>(tickets);
        foreach (var slot in _rows.Children.OfType<TicketRouterRowSlot>().ToList())
        {
            if (!slot.IsLeaving && !wanted.Contains(slot.Row.Ticket))
            {
                if (animate)
                {
                    slot.Leave(Remove);
                }
                else
                {
                    Remove(slot);
                }
            }
        }
        for (var i = 0; i < tickets.Count; i++)
        {
            var ticket = tickets[i];
            if (_rows.Children.OfType<TicketRouterRowSlot>().Any(s => !s.IsLeaving && ReferenceEquals(s.Row.Ticket, ticket)))
            {
                continue;
            }
            var row = create(ticket);
            row.Click += (_, _) =>
            {
                if (row.IsReview)
                {
                    ReviewPressed?.Invoke(row.Ticket);
                }
            };
            var slot = new TicketRouterRowSlot(row, animate);
            _rows.Children.Insert(IndexFor(i), slot);
        }
    }

    /// <summary>The row of <paramref name="ticket"/>, if it is on screen and staying.</summary>
    public TicketRouterRow? RowOf(RoutedTicket ticket) => Rows.FirstOrDefault(r => ReferenceEquals(r.Ticket, ticket));

    private int IndexFor(int active)
    {
        var seen = 0;
        for (var i = 0; i < _rows!.Children.Count; i++)
        {
            if (_rows.Children[i] is TicketRouterRowSlot { IsLeaving: false })
            {
                if (seen == active)
                {
                    return i;
                }
                seen++;
            }
        }
        return _rows.Children.Count;
    }

    private void Remove(TicketRouterRowSlot slot) => _rows?.Children.Remove(slot);
}

/// <summary>
/// The lanes, side by side in equal columns with a gap; below 600px of content the card sets two columns and the lanes
/// form a 2 by 2 grid.
/// </summary>
internal sealed class TicketRouterLanePanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<TicketRouterLanePanel, int>(nameof(Columns), 4);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TicketRouterLanePanel, double>(nameof(Gap), 8);

    static TicketRouterLanePanel()
    {
        AffectsMeasure<TicketRouterLanePanel>(ColumnsProperty, GapProperty);
    }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>The columns used for <paramref name="count"/> lanes.</summary>
    internal int ColumnsFor(int count) => Math.Max(1, Math.Min(Math.Max(1, Columns), Math.Max(1, count)));

    /// <summary>The center of column <paramref name="column"/> in a width of <paramref name="width"/>.</summary>
    internal static double ColumnCenter(double width, int columns, double gap, int column)
    {
        var cell = (width - gap * (columns - 1)) / columns;
        return column * (cell + gap) + cell / 2;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return default;
        }
        var columns = ColumnsFor(count);
        var rows = (count + columns - 1) / columns;
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var cell = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        var rowHeight = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(cell, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }
        return new Size(width, rows * rowHeight + (rows - 1) * Gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return finalSize;
        }
        var columns = ColumnsFor(count);
        var cell = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var rowHeight = Children.Max(c => c.DesiredSize.Height);
        for (var i = 0; i < count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            Children[i].Arrange(new Rect(column * (cell + Gap), row * (rowHeight + Gap), cell, rowHeight));
        }
        return finalSize;
    }
}

/// <summary>
/// The connector between the stage and a row of lanes: a stem from the stage, a hairline across the lanes and a tick down
/// into each. A pulse draws along it to the target lane in 240ms (Arc's standard curve), then fades. Drawn in
/// <see cref="Render"/> with cached pens; the pulse is code-driven because its path depends on the target lane. Reduced
/// motion and Off draw no pulse.
/// </summary>
internal sealed class TicketRouterConnector : Control
{
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<TicketRouterConnector, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> PulseBrushProperty =
        AvaloniaProperty.Register<TicketRouterConnector, IBrush?>(nameof(PulseBrush));

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<TicketRouterConnector, double>(nameof(StrokeWidth), 1);

    public static readonly StyledProperty<double> PulseWidthProperty =
        AvaloniaProperty.Register<TicketRouterConnector, double>(nameof(PulseWidth), 2);

    public static readonly StyledProperty<int> LaneCountProperty =
        AvaloniaProperty.Register<TicketRouterConnector, int>(nameof(LaneCount), 4);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TicketRouterConnector, double>(nameof(Gap), 8);

    private Pen? _linePen;
    private Pen? _pulsePen;
    private MotionValue<double>? _draw;
    private MotionValue<double>? _fade;
    private int _target = -1;

    static TicketRouterConnector()
    {
        AffectsRender<TicketRouterConnector>(LineBrushProperty, PulseBrushProperty, StrokeWidthProperty, PulseWidthProperty,
            LaneCountProperty, GapProperty);
    }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? PulseBrush { get => GetValue(PulseBrushProperty); set => SetValue(PulseBrushProperty, value); }

    public double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    public double PulseWidth { get => GetValue(PulseWidthProperty); set => SetValue(PulseWidthProperty, value); }

    public int LaneCount { get => GetValue(LaneCountProperty); set => SetValue(LaneCountProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>The lane the last pulse ran to, or -1.</summary>
    internal int PulseTarget => _target;

    /// <summary>How much of the pulse's path is drawn (0–1).</summary>
    internal double PulseProgress => _draw?.Value ?? 0;

    /// <summary>Draws a pulse to lane <paramref name="lane"/> (full motion only).</summary>
    public void Pulse(int lane)
    {
        _target = lane;
        _draw ??= Value();
        _fade ??= Value();
        if (!ArcMotion.IsFull(this) || lane < 0)
        {
            _draw.Jump(0);
            _fade.Jump(0);
            InvalidateVisual();
            return;
        }
        _draw.Jump(0);
        _fade.Jump(1);
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        if (await _draw!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(TicketRouterMotion.Pulse), ArcEasings.Standard)))
        {
            await _fade!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }

    /// <summary>Hides a running pulse at once.</summary>
    public void Clear()
    {
        _draw?.Jump(0);
        _fade?.Jump(0);
        InvalidateVisual();
    }

    private MotionValue<double> Value()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty || change.Property == StrokeWidthProperty)
        {
            _linePen = null;
        }
        else if (change.Property == PulseBrushProperty || change.Property == PulseWidthProperty)
        {
            _pulsePen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var count = Math.Max(1, LaneCount);
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var gap = Gap;
        var mid = Math.Round(height / 2) + 0.5;
        var stem = Math.Round(width / 2) + 0.5;
        _linePen ??= new Pen(LineBrush, StrokeWidth, lineCap: PenLineCap.Round);
        var first = Math.Round(TicketRouterLanePanel.ColumnCenter(width, count, gap, 0)) + 0.5;
        var last = Math.Round(TicketRouterLanePanel.ColumnCenter(width, count, gap, count - 1)) + 0.5;
        context.DrawLine(_linePen, new Point(stem, 0), new Point(stem, mid));
        context.DrawLine(_linePen, new Point(first, mid), new Point(last, mid));
        for (var i = 0; i < count; i++)
        {
            var x = Math.Round(TicketRouterLanePanel.ColumnCenter(width, count, gap, i)) + 0.5;
            context.DrawLine(_linePen, new Point(x, mid), new Point(x, height));
        }

        var progress = _draw?.Value ?? 0;
        var opacity = _fade?.Value ?? 0;
        if (_target < 0 || _target >= count || progress <= 0 || opacity <= 0)
        {
            return;
        }
        _pulsePen ??= new Pen(PulseBrush, PulseWidth, lineCap: PenLineCap.Round);
        var target = Math.Round(TicketRouterLanePanel.ColumnCenter(width, count, gap, _target)) + 0.5;
        // The path: down the stem, across to the lane, down into it.
        var a = mid;
        var b = Math.Abs(target - stem);
        var c = height - mid;
        var drawn = (a + b + c) * Math.Clamp(progress, 0, 1);
        using (context.PushOpacity(opacity))
        {
            var down = Math.Min(drawn, a);
            context.DrawLine(_pulsePen, new Point(stem, 0), new Point(stem, down));
            if (drawn > a)
            {
                var across = Math.Min(drawn - a, b);
                var x = stem + Math.Sign(target - stem) * across;
                context.DrawLine(_pulsePen, new Point(stem, mid), new Point(x, mid));
                if (drawn > a + b)
                {
                    context.DrawLine(_pulsePen, new Point(target, mid), new Point(target, mid + Math.Min(drawn - a - b, c)));
                }
            }
        }
    }
}
