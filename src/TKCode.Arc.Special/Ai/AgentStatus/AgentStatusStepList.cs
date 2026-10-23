using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The latest steps of a run as the panel lists them (Arc Agent status): per row an icon (16px), the label (wrapping onto
/// more lines instead of being cut) and a time; 28px rows with 12px between the columns. A new row fades in at the bottom
/// in 155ms; when the list is full the top row fades out in 120ms where it stood and the rest slide up on the Smooth
/// spring; a step that changes from the present tense to the past fades its old label out in 110ms and then shows the
/// new one; the running step's label shimmers. Self-drawn (a list of rows with wrapped text and per-row springs on one
/// clock) so a run of steps costs one control; the text is also exposed as the list's accessible name.
/// </summary>
internal sealed class AgentStatusStepList : AgentStatusInked
{
    public const double RowMinHeight = 28;
    public const double IconColumn = 16;
    public const double ColumnGap = 12;
    public const double LineHeight = 20;

    private sealed class Row
    {
        public object Key = null!;
        public AgentStatusStep Step = null!;
        public string Label = string.Empty;
        public string? PendingLabel;
        public Geometry? Icon;
        public AgentStatusStepState State;
        public string Time = string.Empty;
        public TextLayout? Layout;
        public TextLayout? Highlight;
        public TextLayout? TimeLayout;
        public double Height = RowMinHeight;
        public double Target;
        public ExplainerValue Y = new(0);
        public ExplainerValue Opacity = new(1);
        public ExplainerValue LabelOpacity = new(1);
        public double RemoveAt = double.MaxValue;
        public double ExitY;
        public double LayoutWidth = -1;
        public AgentStatusStepState LayoutState;
    }

    private static readonly ImmutableLinearGradientBrush s_band = new(
        [
            new ImmutableGradientStop(0, Color.FromArgb(0, 0, 0, 0)),
            new ImmutableGradientStop(0.5, Color.FromArgb(255, 0, 0, 0)),
            new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))
        ], startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));

    private readonly LiveLoop _loop;
    private readonly List<Row> _rows = new();
    private readonly List<Row> _exiting = new();
    private double _now;
    private double _width = 358;
    private ArcMotionLevel _level = ArcMotionLevel.Full;
    private bool _paused;
    private Pen? _iconPen;
    private Color _iconPenColor;

    public AgentStatusStepList()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    /// <summary>The rows shown now.</summary>
    public int RowCount => _rows.Count;

    /// <summary>The rows still fading out at the top.</summary>
    public int ExitingCount => _exiting.Count;

    /// <summary>The text of row <paramref name="index"/> as drawn (the old label while the new one waits).</summary>
    public string LabelOf(int index) => _rows[index].Label;

    /// <summary>The time of row <paramref name="index"/> as drawn.</summary>
    public string TimeOf(int index) => _rows[index].Time;

    /// <summary>The height of row <paramref name="index"/> (28, 48 for two lines).</summary>
    public double HeightOf(int index) => _rows[index].Height;

    /// <summary>The current y of row <paramref name="index"/>.</summary>
    public double YOf(int index) => _rows[index].Y.Value(_now);

    /// <summary>The laid-out y of row <paramref name="index"/> (where it comes to rest).</summary>
    public double TargetYOf(int index) => _rows[index].Target;

    /// <summary>The opacity of row <paramref name="index"/>.</summary>
    public double OpacityOf(int index) => _rows[index].Opacity.Value(_now);

    /// <summary>The state of row <paramref name="index"/>.</summary>
    public AgentStatusStepState StateOf(int index) => _rows[index].State;

    /// <summary>The total height of the rows at rest.</summary>
    public double ListHeight { get; private set; }

    /// <summary>Gives the time shown for a step (whole seconds while it runs, one decimal when done); null for none.</summary>
    public Func<AgentStatusStep, string?>? TimeText { get; set; }

    /// <summary>True while the run is paused: the running label stops shimmering and takes the secondary color.</summary>
    public bool IsPaused
    {
        get => _paused;
        set
        {
            if (_paused != value)
            {
                _paused = value;
                foreach (var row in _rows)
                {
                    row.LayoutWidth = -1;
                }
                Relayout(false);
                InvalidateVisual();
            }
        }
    }

    /// <summary>The motion level the list draws at.</summary>
    public ArcMotionLevel Level
    {
        get => _level;
        set
        {
            _level = value;
            if (value == ArcMotionLevel.Off)
            {
                _exiting.Clear();
                foreach (var row in _rows)
                {
                    row.Y.Jump(row.Target);
                    row.Opacity.Jump(1);
                    ApplyPendingLabel(row);
                }
                Relayout(false);
            }
            EnsureLoop();
        }
    }

    private bool Animates => _level != ArcMotionLevel.Off && (TopLevel.GetTopLevel(this) is not null);

    private bool IsFull => _level == ArcMotionLevel.Full && (TopLevel.GetTopLevel(this) is not null);

    /// <summary>
    /// Shows <paramref name="steps"/> (already the steps to list: not pending, the latest ones). Rows are matched by id;
    /// with <paramref name="animate"/> new rows fade in, removed ones fade out and the others slide.
    /// </summary>
    public void SetSteps(IReadOnlyList<AgentStatusStep> steps, bool animate)
    {
        animate &= Animates;
        var full = animate && IsFull;
        var old = new Dictionary<object, Row>();
        foreach (var row in _rows)
        {
            old[row.Key] = row;
        }
        var next = new List<Row>(steps.Count);
        foreach (var step in steps)
        {
            var key = (object?)step.Id ?? step;
            if (old.Remove(key, out var row))
            {
                if (row.Label != step.Label && row.PendingLabel != step.Label)
                {
                    if (full && row.State != AgentStatusStepState.Waiting)
                    {
                        row.PendingLabel = step.Label;
                        row.LabelOpacity.Tween(0, _now, 0.11, ArcEasings.Standard);
                    }
                    else
                    {
                        row.Label = step.Label;
                        row.PendingLabel = null;
                        row.LabelOpacity.Jump(1);
                        row.LayoutWidth = -1;
                    }
                }
                row.Step = step;
                row.Icon = step.Icon;
                if (row.State != step.State)
                {
                    row.State = step.State;
                    row.LayoutWidth = -1;
                }
                next.Add(row);
                continue;
            }
            var created = new Row { Key = key, Step = step, Label = step.Label, Icon = step.Icon, State = step.State };
            if (animate)
            {
                created.Opacity.Jump(0);
                created.Opacity.Tween(1, _now, 0.155, ArcEasings.Enter);
            }
            next.Add(created);
        }
        foreach (var gone in old.Values)
        {
            if (animate)
            {
                gone.ExitY = gone.Y.Value(_now);
                gone.Opacity.Tween(0, _now, 0.12, ArcEasings.Exit);
                gone.RemoveAt = _now + 0.12;
                _exiting.Add(gone);
            }
        }
        _rows.Clear();
        _rows.AddRange(next);
        Relayout(animate);
        RefreshTimes();
        EnsureLoop();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Recomputes every row's time text (called by the owner on its tick); only changed texts are rebuilt.</summary>
    public void RefreshTimes()
    {
        var changed = false;
        foreach (var row in _rows)
        {
            var text = TimeText?.Invoke(row.Step) ?? string.Empty;
            if (text != row.Time || row.TimeLayout is null)
            {
                row.Time = text;
                row.TimeLayout?.Dispose();
                row.TimeLayout = text.Length == 0 ? null : MakeLayout(text, 12, ColorOf(MutedInk, Colors.Gray), double.PositiveInfinity, 16);
                row.LayoutWidth = -1;
                changed = true;
            }
        }
        if (changed)
        {
            Relayout(false);
            InvalidateVisual();
        }
    }

    protected override void OnInksChanged()
    {
        foreach (var row in _rows)
        {
            row.LayoutWidth = -1;
            row.TimeLayout = null;
        }
        _iconPen = null;
        RefreshTimes();
        Relayout(false);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private TextLayout MakeLayout(string text, double size, Color color, double maxWidth, double lineHeight, bool wrap = false) =>
        new(text, Typeface, size, new ImmutableSolidColorBrush(color), TextAlignment.Left,
            wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, TextTrimming.None, null, FlowDirection.LeftToRight, maxWidth,
            double.PositiveInfinity, lineHeight, 0, 0, AgentStatusInked.Tabular);

    private void Relayout(bool animate)
    {
        var y = 0.0;
        foreach (var row in _rows)
        {
            var timeWidth = row.TimeLayout?.WidthIncludingTrailingWhitespace ?? 0;
            var textWidth = Math.Max(20, _width - IconColumn - ColumnGap - (timeWidth > 0 ? timeWidth + ColumnGap : 0));
            if (row.Layout is null || row.LayoutWidth != textWidth || row.LayoutState != row.State)
            {
                row.Layout?.Dispose();
                row.Highlight?.Dispose();
                var live = row.State == AgentStatusStepState.Active;
                var color = row.State switch
                {
                    AgentStatusStepState.Done => ColorOf(SecondaryInk, Colors.Gray),
                    AgentStatusStepState.Skipped => ColorOf(MutedInk, Colors.Gray),
                    AgentStatusStepState.Active => _paused ? ColorOf(SecondaryInk, Colors.Gray) : ColorOf(ShimmerBaseInk, Colors.Gray),
                    _ => ColorOf(ForegroundInk, Colors.Black)
                };
                row.Layout = MakeLayout(row.Label, FontSize, color, textWidth, LineHeight, wrap: true);
                row.Highlight = live ? MakeLayout(row.Label, FontSize, ColorOf(ForegroundInk, Colors.Black), textWidth, LineHeight, wrap: true) : null;
                row.LayoutWidth = textWidth;
                row.LayoutState = row.State;
                row.Height = Math.Max(RowMinHeight, 8 + Math.Max(1, row.Layout.TextLines.Count) * LineHeight);
            }
            row.Target = y;
            y += row.Height;
        }
        foreach (var row in _rows)
        {
            var fresh = row.Opacity.Value(_now) < 0.01;
            if (animate && IsFull && !fresh && Math.Abs(row.Y.Target - row.Target) > 0.01)
            {
                row.Y.Spring(row.Target, _now, ArcMotionPresets.Smooth);
            }
            else if (Math.Abs(row.Y.Target - row.Target) > 0.01)
            {
                row.Y.Jump(row.Target);
            }
        }
        ListHeight = y;
    }

    private void ApplyPendingLabel(Row row)
    {
        if (row.PendingLabel is { } pending)
        {
            row.Label = pending;
            row.PendingLabel = null;
            row.LabelOpacity.Jump(1);
            row.LayoutWidth = -1;
        }
    }

    internal bool Advance(double seconds) => Step(seconds);

    private bool Step(double dt)
    {
        _now += dt;
        var relayout = false;
        for (var i = _exiting.Count - 1; i >= 0; i--)
        {
            if (_now >= _exiting[i].RemoveAt)
            {
                _exiting.RemoveAt(i);
            }
        }
        foreach (var row in _rows)
        {
            if (row.PendingLabel is not null && row.LabelOpacity.Value(_now) <= 0.001)
            {
                ApplyPendingLabel(row);
                row.LabelOpacity.Jump(1);
                relayout = true;
            }
        }
        if (relayout)
        {
            Relayout(false);
            InvalidateMeasure();
        }
        InvalidateVisual();
        return NeedsLoop();
    }

    private bool NeedsLoop()
    {
        if (_level == ArcMotionLevel.Off)
        {
            return false;
        }
        if (_exiting.Count > 0)
        {
            return true;
        }
        foreach (var row in _rows)
        {
            if (row.Y.IsMoving(_now) || row.Opacity.IsMoving(_now) || row.LabelOpacity.IsMoving(_now) || row.PendingLabel is not null)
            {
                return true;
            }
            if (IsFull && !_paused && row.State == AgentStatusStepState.Active)
            {
                return true;
            }
        }
        return false;
    }

    private void EnsureLoop()
    {
        if (NeedsLoop())
        {
            _loop.Start();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureLoop();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? _width : availableSize.Width;
        if (Math.Abs(width - _width) > 0.01)
        {
            _width = width;
            Relayout(false);
        }
        var text = string.Join(", ", _rows.Select(r => r.Label));
        AutomationProperties.SetName(this, text);
        return new Size(width, ListHeight);
    }

    public override void Render(DrawingContext context)
    {
        foreach (var row in _exiting)
        {
            DrawRow(context, row, row.ExitY);
        }
        foreach (var row in _rows)
        {
            DrawRow(context, row, row.Y.Value(_now));
        }
    }

    private void DrawRow(DrawingContext context, Row row, double y)
    {
        var opacity = row.Opacity.Value(_now);
        if (opacity <= 0.003 || row.Layout is null)
        {
            return;
        }
        var iconColor = row.State switch
        {
            AgentStatusStepState.Active => ColorOf(ForegroundInk, Colors.Black),
            AgentStatusStepState.Waiting => ColorOf(AccentInk, Colors.Black),
            AgentStatusStepState.Failed => ColorOf(DangerInk, Colors.Red),
            _ => ColorOf(MutedInk, Colors.Gray)
        };
        if (_iconPen is null || _iconPenColor != iconColor)
        {
            _iconPenColor = iconColor;
            _iconPen = new Pen(new ImmutableSolidColorBrush(iconColor), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        using (context.PushOpacity(opacity))
        {
            if (row.Icon is { } icon)
            {
                using (context.PushTransform(Matrix.CreateScale(16.0 / 24, 16.0 / 24) * Matrix.CreateTranslation(0, y + 6)))
                {
                    context.DrawGeometry(null, _iconPen, icon);
                }
            }
            var origin = new Point(IconColumn + ColumnGap, y + 4);
            var labelOpacity = row.LabelOpacity.Value(_now);
            using (context.PushOpacity(labelOpacity))
            {
                if (row.Highlight is not null && !(IsFull && !_paused))
                {
                    // No sweep: a running label is the text color, or the secondary color while the run is paused.
                    (_paused ? row.Layout : row.Highlight).Draw(context, origin);
                }
                else if (row.Highlight is not null)
                {
                    row.Layout.Draw(context, origin);
                    var w = Math.Max(1, row.Layout.WidthIncludingTrailingWhitespace);
                    var phase = _now % 2.4 / 2.4;
                    var center = phase < 0.72 ? -0.5 * w + 2 * w * ArcEasings.InOut.Ease(phase / 0.72) : 1.5 * w;
                    var half = 0.36 * w;
                    var band = new Rect(origin.X + center - half, origin.Y, 2 * half, row.Layout.Height);
                    using (context.PushClip(band))
                    using (context.PushOpacityMask(s_band, band))
                    {
                        row.Highlight.Draw(context, origin);
                    }
                }
                else
                {
                    row.Layout.Draw(context, origin);
                }
            }
            if (row.TimeLayout is { } time)
            {
                time.Draw(context, new Point(_width - time.WidthIncludingTrailingWhitespace, y + 4 + (LineHeight - time.Height) / 2));
            }
        }
    }
}
