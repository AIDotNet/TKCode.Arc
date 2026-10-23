using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The border colors the stage mixes from the theme: plain, a chip at its active station, a failed call, a new block.</summary>
internal sealed class AgentLoopInk
{
    public AgentLoopInk(Color border, Color accent, Color danger)
    {
        Border = new ImmutableSolidColorBrush(border);
        // CSS color-mix(in oklab, accent 40%, border) and friends.
        ChipActive = new ImmutableSolidColorBrush(Oklch.MixOklab(border, accent, 0.4));
        ChipError = new ImmutableSolidColorBrush(Oklch.MixOklab(border, danger, 0.35));
        BlockFresh = new ImmutableSolidColorBrush(Oklch.MixOklab(border, accent, 0.45));
    }

    public IBrush Border { get; }

    public IBrush ChipActive { get; }

    public IBrush ChipError { get; }

    public IBrush BlockFresh { get; }

    public static AgentLoopInk Fallback { get; } = new(Color.FromRgb(0xE6, 0xE6, 0xE6), Color.FromRgb(0x1F, 0x7D, 0xCF), Color.FromRgb(0xD9, 0x2D, 0x20));
}

/// <summary>How a chip looks: at rest (ready or finished), lit at its active station, or dimmed while another station works.</summary>
internal enum AgentLoopChipLook
{
    Rest,
    Active,
    Dim
}

/// <summary>Small text helpers shared by the parts.</summary>
internal static class AgentLoopText
{
    public static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };

    /// <summary>A one-line text block bound to a text color token.</summary>
    public static TextBlock Line(double size, double lineHeight, ArcDesignTokenKind color, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock
        {
            FontSize = size,
            LineHeight = lineHeight,
            FontWeight = weight,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        return block;
    }

    /// <summary>A glyph bound to a color token.</summary>
    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>
    /// <paramref name="text"/> cut with an ellipsis to fit <paramref name="maxWidth"/> (a layout-time helper for text
    /// that cannot trim itself, such as a shimmer).
    /// </summary>
    public static string Trim(string text, Typeface typeface, double size, double maxWidth)
    {
        if (string.IsNullOrEmpty(text) || Width(text, typeface, size) <= maxWidth + 0.5)
        {
            return text;
        }
        var lo = 0;
        var hi = text.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (Width(text[..mid].TrimEnd() + "…", typeface, size) <= maxWidth)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return lo == 0 ? "…" : text[..lo].TrimEnd() + "…";
    }

    /// <summary>The width of one line of text.</summary>
    public static double Width(string text, Typeface typeface, double size)
        => new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, null).WidthIncludingTrailingWhitespace;
}

/// <summary>
/// The host a chip or a context block drops in with: a fade with a 6px drop (and for chips a 0.98 scale growing from
/// the pin) on a critically damped spring (ω ≈ 22, no bounce), and a short fade out when it is replaced. Driven by the
/// explainer's wall clock so it finishes while the story is paused, at the measured pace whatever the speed.
/// </summary>
internal sealed class AgentLoopDrop : Decorator
{
    private static readonly SpringSpec s_spring = SpringSpec.FromVisual(0.24, 0);
    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private readonly TranslateTransform _move = new();
    private readonly ScaleTransform _scale = new();
    private readonly double _scaleFrom;
    private ExplainerValue _enter = new(1);
    private ExplainerValue _exit = new(1);

    public AgentLoopDrop(Control child, double scaleFrom)
    {
        _scaleFrom = scaleFrom;
        Child = child;
        RenderTransform = new TransformGroup { Children = { _scale, _move } };
    }

    /// <summary>True once it started leaving.</summary>
    public bool IsExiting { get; private set; }

    /// <summary>The point it grows from: x in its own coordinates, top edge.</summary>
    public double OriginX
    {
        set => RenderTransformOrigin = new RelativePoint(value, 0, RelativeUnit.Absolute);
    }

    /// <summary>Drops in after <paramref name="delay"/> seconds (at once when not animated).</summary>
    public void Enter(double wall, double delay, bool animate)
    {
        if (animate)
        {
            _enter.Jump(0);
            _enter.Spring(1, wall, s_spring, delay);
        }
        else
        {
            _enter.Jump(1);
        }
        Update(wall);
    }

    /// <summary>Fades out over <paramref name="duration"/> seconds (at once when not animated).</summary>
    public void Exit(double wall, double duration, bool animate)
    {
        IsExiting = true;
        if (animate)
        {
            _exit.Tween(0, wall, duration, s_easeOut);
        }
        else
        {
            _exit.Jump(0);
        }
        Update(wall);
    }

    /// <summary>Applies the values at <paramref name="wall"/>; true while it still moves.</summary>
    public bool Update(double wall)
    {
        var p = _enter.Value(wall);
        Opacity = Math.Clamp(p, 0, 1) * Math.Clamp(_exit.Value(wall), 0, 1);
        _move.Y = -6 * (1 - p);
        var scale = _scaleFrom + (1 - _scaleFrom) * p;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        return _enter.IsMoving(wall) || _exit.IsMoving(wall);
    }

    /// <summary>True once it has left.</summary>
    public bool IsGone(double wall) => IsExiting && !_exit.IsMoving(wall);
}

/// <summary>An empty slot: a dashed rounded outline in the strong border color with a muted line centered in it.</summary>
internal sealed class AgentLoopPlaceholder : Control
{
    internal static readonly StyledProperty<IBrush?> DashBrushProperty =
        AvaloniaProperty.Register<AgentLoopPlaceholder, IBrush?>(nameof(DashBrush));

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private readonly TextBlock _text;
    private Pen? _pen;
    private ExplainerValue _presence = new(1);

    static AgentLoopPlaceholder()
    {
        AffectsRender<AgentLoopPlaceholder>(DashBrushProperty);
    }

    public AgentLoopPlaceholder()
    {
        _text = AgentLoopText.Line(12, 16.8, ArcDesignTokenKind.TextMuted);
        _text.TextAlignment = TextAlignment.Center;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.TextTrimming = TextTrimming.None;
        _text.HorizontalAlignment = HorizontalAlignment.Center;
        _text.Margin = new Thickness(16, 8);
        VisualChildren.Add(_text);
        LogicalChildren.Add(_text);
        ArcTokens.Bind(this, DashBrushProperty, ArcDesignTokenKind.BorderStrong);
        IsHitTestVisible = false;
    }

    public IBrush? DashBrush { get => GetValue(DashBrushProperty); set => SetValue(DashBrushProperty, value); }

    /// <summary>The line in the slot.</summary>
    public string? Text { get => _text.Text; set => _text.Text = value; }

    /// <summary>True while it is shown or fading in.</summary>
    public bool IsPresent => _presence.Target > 0.5;

    /// <summary>Shows or hides it: fades in over 160ms, out over 80-100ms (at once when not animated).</summary>
    public void SetPresent(bool present, double wall, bool animate, double exitSeconds = 0.08)
    {
        if (present == IsPresent && (animate || !_presence.IsMoving(wall)))
        {
            return;
        }
        if (animate)
        {
            _presence.Tween(present ? 1 : 0, wall, present ? 0.16 : exitSeconds, s_easeOut);
        }
        else
        {
            _presence.Jump(present ? 1 : 0);
        }
        Update(wall);
    }

    /// <summary>Applies the fade at <paramref name="wall"/>.</summary>
    public bool Update(double wall)
    {
        var opacity = Math.Clamp(_presence.Value(wall), 0, 1);
        Opacity = opacity;
        IsVisible = opacity > 0.001;
        return _presence.IsMoving(wall);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DashBrushProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _text.Measure(availableSize);
        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var desired = _text.DesiredSize;
        var height = Math.Min(desired.Height, finalSize.Height);
        _text.Arrange(new Rect(0, (finalSize.Height - height) / 2, finalSize.Width, height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (DashBrush is not { } brush)
        {
            return;
        }
        _pen ??= new Pen(brush, 1, new DashStyle([4, 3], 0));
        var rect = new Rect(Bounds.Size).Deflate(0.5);
        context.DrawRectangle(null, _pen, rect, 13.5, 13.5);
    }
}

/// <summary>
/// A chip in a lane: a tool call (mark, action, a spinner that becomes a check or a cross; "Calling …" that becomes the
/// result) or a check (Retry with its note, or Check passed). 56 high, radius 14, a 1px border that turns accent at its
/// active station; dimmed while another station works.
/// </summary>
internal sealed class AgentLoopChip : Border
{
    private readonly ArcContentSwap? _status;
    private readonly ArcContentSwap _body;
    private readonly AgentLoopLabels _labels;
    private AgentLoopChipLook _look = (AgentLoopChipLook)(-1);

    public AgentLoopChip(AgentLoopChipState state, AgentLoopLabels labels)
    {
        State = state;
        _labels = labels;
        ClipToBounds = true;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("16,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Control mark;
        TextBlock title;
        if (state.Call is { } call)
        {
            mark = AgentLoopText.Glyph(call.Mark ?? AgentLoopDefaults.CallMark, 16, ArcDesignTokenKind.Foreground);
            title = AgentLoopText.Line(14, 19.6, ArcDesignTokenKind.Foreground, FontWeight.Medium);
            title.Text = call.Action;
            _status = new ArcContentSwap
            {
                Mode = ContentSwapMode.Crossfade,
                Width = 16,
                Height = 16,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_status, 2);
            grid.Children.Add(_status);
        }
        else
        {
            var passed = state.Check?.Passed == true;
            mark = AgentLoopText.Glyph(passed ? ArcIcons.CircleCheck : ArcIcons.RotateCcw, 16,
                passed ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Warning);
            title = AgentLoopText.Line(14, 19.6, ArcDesignTokenKind.Foreground, FontWeight.Medium);
            title.Text = passed ? labels.CheckPassed : labels.Retry;
        }
        title.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(title, 1);
        grid.Children.Add(mark);
        grid.Children.Add(title);
        _body = new ArcContentSwap
        {
            Mode = ContentSwapMode.Crossfade,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24, 1, 0, 0),
            ClipToBounds = true
        };
        Grid.SetRow(_body, 1);
        Grid.SetColumnSpan(_body, 3);
        grid.Children.Add(_body);
        Child = grid;
        ShowContent(animate: false);
    }

    /// <summary>What the chip shows.</summary>
    public AgentLoopChipState State { get; }

    /// <summary>Shows the result of a call (spinner → check or cross, "Calling …" → the result).</summary>
    public void Resolve(bool animate) => ShowContent(animate);

    /// <summary>Lights, dims or rests the chip (opacity and border follow with the theme's transitions).</summary>
    public void SetLook(AgentLoopChipLook look, AgentLoopInk ink)
    {
        _look = look;
        var error = State.Call is { Status: AgentLoopCallStatus.Error } && State.IsResolved;
        BorderBrush = look == AgentLoopChipLook.Active ? (error ? ink.ChipError : ink.ChipActive) : ink.Border;
        Opacity = look == AgentLoopChipLook.Dim ? 0.62 : 1;
    }

    /// <summary>The current look.</summary>
    public AgentLoopChipLook Look => _look;

    private void ShowContent(bool animate)
    {
        Control body;
        if (State.Call is { } call)
        {
            Control status;
            if (!State.IsResolved)
            {
                var spinner = new ArcSpinner { Width = 16, Height = 16 };
                ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                status = spinner;
                var calling = new TextShimmer { Text = _labels.Calling(call.Tool), FontSize = 12, IsActive = true, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(calling, TextShimmer.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                body = calling;
            }
            else
            {
                var error = call.Status == AgentLoopCallStatus.Error;
                status = AgentLoopText.Glyph(error ? ArcIcons.X : ArcIcons.Check, 16, error ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Success);
                var result = AgentLoopText.Line(12, 16.8, ArcDesignTokenKind.TextSecondary);
                result.Text = call.Result;
                AutomationProperties.SetName(result, (error ? _labels.Failed(call.Tool) : _labels.Returned(call.Tool)) + " " + call.Result);
                body = result;
            }
            Swap(_status!, status, animate);
        }
        else
        {
            var note = AgentLoopText.Line(12, 16.8, ArcDesignTokenKind.TextSecondary);
            note.Text = State.Check?.Note;
            body = note;
        }
        Swap(_body, body, animate);
    }

    private static void Swap(ArcContentSwap swap, Control content, bool animate)
    {
        if (animate)
        {
            swap.Content = content;
            return;
        }
        var mode = swap.Mode;
        swap.Mode = ContentSwapMode.None;
        swap.Content = content;
        swap.Mode = mode;
    }
}

/// <summary>A block in the context stack: mark, label, token count, and the detail under them.</summary>
internal sealed class AgentLoopBlock : Border
{
    public AgentLoopBlock(AgentLoopContext context, AgentLoopLabels labels)
    {
        Context = context;
        ClipToBounds = true;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("16,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var mark = AgentLoopText.Glyph(context.Mark ?? AgentLoopDefaults.ContextMark, 16, ArcDesignTokenKind.TextSecondary);
        var label = AgentLoopText.Line(14, 19.6, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        label.Text = context.Label;
        label.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label, 1);
        var tokens = AgentLoopText.Line(12, 16.8, ArcDesignTokenKind.TextMuted);
        tokens.FontFeatures = AgentLoopText.Tabular;
        tokens.Text = labels.FormatTokens(Math.Max(0, context.Tokens));
        tokens.Margin = new Thickness(10, 0, 0, 0);
        AutomationProperties.SetName(tokens, tokens.Text + " " + labels.TokensUnit);
        Grid.SetColumn(tokens, 2);
        grid.Children.Add(mark);
        grid.Children.Add(label);
        grid.Children.Add(tokens);
        if (!string.IsNullOrEmpty(context.Detail))
        {
            var detail = AgentLoopText.Line(12, 16.8, ArcDesignTokenKind.TextSecondary);
            detail.Text = context.Detail;
            detail.Margin = new Thickness(10, 1, 0, 0);
            Grid.SetRow(detail, 1);
            Grid.SetColumn(detail, 1);
            Grid.SetColumnSpan(detail, 2);
            grid.Children.Add(detail);
        }
        Child = grid;
        AutomationProperties.SetName(this, context.Label);
    }

    /// <summary>The block shown.</summary>
    public AgentLoopContext Context { get; }

    /// <summary>The accent border of a block that just joined; it fades back to the plain border when Gather is left.</summary>
    public void SetFresh(bool fresh, AgentLoopInk ink) => BorderBrush = fresh ? ink.BlockFresh : ink.Border;

    protected override AutomationPeer OnCreateAutomationPeer() => new AgentLoopListItemPeer(this);
}

/// <summary>A context block reads as a list item.</summary>
internal sealed class AgentLoopListItemPeer : ControlAutomationPeer
{
    public AgentLoopListItemPeer(Control owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
}

/// <summary>A lane under a lower station (or the single lane on narrow cards): a placeholder until a chip drops in.</summary>
internal sealed class AgentLoopLane : Control
{
    private readonly AgentLoopPlaceholder _placeholder = new();
    private readonly List<AgentLoopDrop> _drops = new();
    private double _originX;

    public AgentLoopLane()
    {
        VisualChildren.Add(_placeholder);
        LogicalChildren.Add(_placeholder);
        ClipToBounds = false;
    }

    /// <summary>The placeholder's line.</summary>
    public string? Placeholder { get => _placeholder.Text; set => _placeholder.Text = value; }

    /// <summary>The chip shown (not leaving), or null.</summary>
    public AgentLoopChip? Current { get; private set; }

    /// <summary>Where the pin meets the lane (x in the lane), the point chips grow from.</summary>
    public double OriginX
    {
        get => _originX;
        set
        {
            _originX = value;
            foreach (var drop in _drops)
            {
                drop.OriginX = value;
            }
        }
    }

    /// <summary>Drops <paramref name="chip"/> in: the old chip fades out in 80ms, the new one drops 100ms later.</summary>
    public void Show(AgentLoopChipState chip, AgentLoopLabels labels, double wall, bool animate)
    {
        if (Current?.State == chip)
        {
            return;
        }
        RemoveCurrent(wall, animate, 0.08);
        _placeholder.SetPresent(false, wall, animate);
        var control = new AgentLoopChip(chip, labels);
        var drop = new AgentLoopDrop(control, 0.98) { OriginX = _originX };
        _drops.Add(drop);
        VisualChildren.Add(drop);
        LogicalChildren.Add(drop);
        Current = control;
        drop.Enter(wall, 0.1, animate);
        InvalidateMeasure();
    }

    /// <summary>Back to the placeholder: the chip fades out in 80ms and the placeholder fades in over 160ms.</summary>
    public void Clear(double wall, bool animate)
    {
        RemoveCurrent(wall, animate, 0.08);
        _placeholder.SetPresent(true, wall, animate);
    }

    /// <summary>Advances the fades; true while something moves.</summary>
    public bool Update(double wall)
    {
        var moving = _placeholder.Update(wall);
        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i];
            if (drop.IsGone(wall))
            {
                _drops.RemoveAt(i);
                VisualChildren.Remove(drop);
                LogicalChildren.Remove(drop);
                continue;
            }
            moving |= drop.Update(wall);
        }
        return moving;
    }

    private void RemoveCurrent(double wall, bool animate, double seconds)
    {
        if (Current is null)
        {
            return;
        }
        foreach (var drop in _drops)
        {
            if (ReferenceEquals(drop.Child, Current))
            {
                drop.Exit(wall, seconds, animate);
            }
        }
        Current = null;
        if (!animate)
        {
            Update(wall);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 197, AgentLoopGeometry.RowHeight);
        _placeholder.Measure(size);
        foreach (var drop in _drops)
        {
            drop.Measure(size);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _placeholder.Arrange(rect);
        foreach (var drop in _drops)
        {
            drop.Arrange(rect);
        }
        return finalSize;
    }
}

/// <summary>The context blocks: a slot reserved per loop (the card never grows), a dashed placeholder until the first joins.</summary>
internal sealed class AgentLoopContextList : Control
{
    private readonly AgentLoopPlaceholder _placeholder = new();
    private readonly List<(int Slot, AgentLoopDrop Drop)> _drops = new();
    private int _slots = 1;
    private int _columns = 1;

    public AgentLoopContextList()
    {
        VisualChildren.Add(_placeholder);
        LogicalChildren.Add(_placeholder);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.List);
    }

    /// <summary>The placeholder's line.</summary>
    public string? Placeholder { get => _placeholder.Text; set => _placeholder.Text = value; }

    /// <summary>Slots reserved (one per loop).</summary>
    public int Slots
    {
        get => _slots;
        set
        {
            _slots = Math.Max(1, value);
            InvalidateMeasure();
        }
    }

    /// <summary>Blocks per row (two in the stacked two-column layout).</summary>
    public int Columns
    {
        get => _columns;
        set
        {
            _columns = Math.Clamp(value, 1, 2);
            InvalidateMeasure();
        }
    }

    /// <summary>The blocks shown (not leaving), in slot order.</summary>
    public IEnumerable<AgentLoopBlock> Blocks
    {
        get
        {
            foreach (var (_, drop) in _drops)
            {
                if (!drop.IsExiting && drop.Child is AgentLoopBlock block)
                {
                    yield return block;
                }
            }
        }
    }

    /// <summary>Adds the block of loop <paramref name="slot"/> (it fades in with a short drop).</summary>
    public AgentLoopBlock Add(int slot, AgentLoopContext context, AgentLoopLabels labels, double wall, bool animate)
    {
        _placeholder.SetPresent(false, wall, animate, 0.1);
        var block = new AgentLoopBlock(context, labels);
        var drop = new AgentLoopDrop(block, 1);
        _drops.Add((slot, drop));
        VisualChildren.Add(drop);
        LogicalChildren.Add(drop);
        drop.Enter(wall, 0, animate);
        InvalidateMeasure();
        return block;
    }

    /// <summary>Empties the stack: blocks fade out in 120ms, the placeholder fades in over 160ms.</summary>
    public void Clear(double wall, bool animate)
    {
        foreach (var (_, drop) in _drops)
        {
            if (!drop.IsExiting)
            {
                drop.Exit(wall, 0.12, animate);
            }
        }
        _placeholder.SetPresent(true, wall, animate);
        if (!animate)
        {
            Update(wall);
        }
    }

    /// <summary>Advances the fades; true while something moves.</summary>
    public bool Update(double wall)
    {
        var moving = _placeholder.Update(wall);
        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i].Drop;
            if (drop.IsGone(wall))
            {
                _drops.RemoveAt(i);
                VisualChildren.Remove(drop);
                LogicalChildren.Remove(drop);
                continue;
            }
            moving |= drop.Update(wall);
        }
        return moving;
    }

    /// <summary>The rows the reserved slots take.</summary>
    public int Rows => (_slots + _columns - 1) / _columns;

    private Size CellSize(double width) => new(Math.Max(0, (width - (_columns - 1) * AgentLoopGeometry.BlockGap) / _columns), AgentLoopGeometry.RowHeight);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 239;
        var height = AgentLoopGeometry.BlocksHeight(Rows);
        _placeholder.Measure(new Size(width, height));
        var cell = CellSize(width);
        foreach (var (_, drop) in _drops)
        {
            drop.Measure(cell);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _placeholder.Arrange(new Rect(finalSize));
        var cell = CellSize(finalSize.Width);
        foreach (var (slot, drop) in _drops)
        {
            var row = slot / _columns;
            var column = slot % _columns;
            drop.Arrange(new Rect(column * (cell.Width + AgentLoopGeometry.BlockGap), row * (cell.Height + AgentLoopGeometry.BlockGap), cell.Width, cell.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// A station's label beside its node: an icon and the name (the name carries a calm shimmer while the station is active).
/// Idle labels are muted, the active one is in the text color with an accent icon, visited ones are secondary; the
/// colors follow with the theme's transitions. Names that do not fit are cut with an ellipsis.
/// </summary>
internal sealed class AgentLoopStationLabel : Control
{
    internal static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<AgentLoopStationLabel, IBrush?>(nameof(TextBrush));

    internal static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<AgentLoopStationLabel, IBrush?>(nameof(IconBrush));

    internal static readonly StyledProperty<IBrush?> ShimmerBaseBrushProperty =
        AvaloniaProperty.Register<AgentLoopStationLabel, IBrush?>(nameof(ShimmerBaseBrush));

    private readonly ArcGlyph _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextShimmer _text = new() { IsActive = false, VerticalAlignment = VerticalAlignment.Center };
    private string _name = string.Empty;
    private bool _compact;
    private AgentLoopNodeState _state;
    private double _naturalWidth = -1;
    private string? _shown;
    private (string Name, double Room, bool Compact) _shownFor;

    public AgentLoopStationLabel()
    {
        VisualChildren.Add(_icon);
        VisualChildren.Add(_text);
        LogicalChildren.Add(_icon);
        LogicalChildren.Add(_text);
        IsHitTestVisible = false;
        UpdateFont();
    }

    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    public IBrush? IconBrush { get => GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }

    public IBrush? ShimmerBaseBrush { get => GetValue(ShimmerBaseBrushProperty); set => SetValue(ShimmerBaseBrushProperty, value); }

    /// <summary>The station's icon (hidden on compact cards).</summary>
    public Geometry? Icon { get => _icon.Data; set => _icon.Data = value; }

    /// <summary>The icon sits after the name (Verify, whose label is left of its node).</summary>
    public bool IconAfter { get; set; }

    /// <summary>The station's name.</summary>
    public string StationName
    {
        get => _name;
        set
        {
            _name = value ?? string.Empty;
            _naturalWidth = -1;
            InvalidateMeasure();
        }
    }

    /// <summary>12px without the icon (ring column below 360px), or 14px with it.</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }
            _compact = value;
            _naturalWidth = -1;
            UpdateFont();
            InvalidateMeasure();
        }
    }

    /// <summary>The station's state.</summary>
    public AgentLoopNodeState State
    {
        get => _state;
        set
        {
            _state = value;
            PseudoClasses.Set(":active", value == AgentLoopNodeState.Active);
            PseudoClasses.Set(":visited", value == AgentLoopNodeState.Visited);
            _text.IsActive = value == AgentLoopNodeState.Active;
        }
    }

    /// <summary>The width the label wants without cutting (icon included).</summary>
    public double NaturalWidth
    {
        get
        {
            if (_naturalWidth < 0)
            {
                var text = AgentLoopText.Width(_name, Typeface(), FontSize);
                _naturalWidth = Math.Ceiling(text * 10) / 10 + (_compact ? 0 : 14 + 6);
            }
            return _naturalWidth;
        }
    }

    private double FontSize => _compact ? 12 : 14;

    private double LineHeight => _compact ? 16.8 : 20;

    private Typeface Typeface() => new(_text.FontFamily, FontStyle.Normal, FontWeight.Normal);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextBrushProperty)
        {
            _text.Foreground = TextBrush;
        }
        else if (change.Property == IconBrushProperty)
        {
            _icon.Foreground = IconBrush;
        }
        else if (change.Property == ShimmerBaseBrushProperty)
        {
            _text.BaseForeground = ShimmerBaseBrush;
        }
    }

    private void UpdateFont()
    {
        _text.FontSize = FontSize;
        _icon.Size = 14;
        _icon.IsVisible = !_compact;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var iconPart = _compact ? 0 : 14 + 6;
        var room = Math.Max(0, availableSize.Width - iconPart);
        if (_shownFor != (_name, Math.Floor(room), _compact) || _shown is null)
        {
            _shownFor = (_name, Math.Floor(room), _compact);
            _shown = AgentLoopText.Trim(_name, Typeface(), FontSize, room);
        }
        var shown = _shown;
        if (_text.Text != shown)
        {
            _text.Text = shown;
        }
        // A name cut to fit still reads in full.
        AutomationProperties.SetName(_text, _name);
        _text.Measure(new Size(room, LineHeight));
        _icon.Measure(new Size(14, 14));
        var width = Math.Min(NaturalWidth, iconPart + _text.DesiredSize.Width);
        return new Size(Math.Min(width, availableSize.Width), LineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var iconPart = _compact ? 0 : 14 + 6;
        var textWidth = Math.Max(0, finalSize.Width - iconPart);
        var textHeight = _text.DesiredSize.Height;
        var textTop = (finalSize.Height - textHeight) / 2;
        if (_compact)
        {
            _text.Arrange(new Rect(0, textTop, textWidth, textHeight));
            return finalSize;
        }
        var iconTop = (finalSize.Height - 14) / 2;
        if (IconAfter)
        {
            _text.Arrange(new Rect(0, textTop, textWidth, textHeight));
            _icon.Arrange(new Rect(finalSize.Width - 14, iconTop, 14, 14));
        }
        else
        {
            _icon.Arrange(new Rect(0, iconTop, 14, 14));
            _text.Arrange(new Rect(iconPart, textTop, textWidth, textHeight));
        }
        return finalSize;
    }
}

/// <summary>
/// The middle of the ring: the model's mark and name, and a line under them that reads Ready, the loop counter (its
/// number rolls), or the done / stopped line with its icon. Without a model only the line shows.
/// </summary>
internal sealed class AgentLoopCenter : Control
{
    private readonly ArcGlyph _mark = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _name;
    private readonly ArcContentSwap _line;
    private ArcContentSwap? _number;
    private string? _lineKey;
    private bool _compact;
    private IDisposable? _markColor;

    public AgentLoopCenter()
    {
        _name = AgentLoopText.Line(14, 19.6, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.TextAlignment = TextAlignment.Center;
        _line = new ArcContentSwap { Mode = ContentSwapMode.Crossfade, HorizontalContentAlignment = HorizontalAlignment.Center };
        VisualChildren.Add(_mark);
        VisualChildren.Add(_name);
        VisualChildren.Add(_line);
        LogicalChildren.Add(_mark);
        LogicalChildren.Add(_name);
        LogicalChildren.Add(_line);
        IsHitTestVisible = false;
    }

    /// <summary>The widest the name may be (inside the ring).</summary>
    public double MaxNameWidth { get; set; } = 140;

    /// <summary>The smaller center of narrow cards (mark 20, name 12px).</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }
            _compact = value;
            _name.FontSize = value ? 12 : 14;
            _name.LineHeight = value ? 16.8 : 19.6;
            _mark.Size = value ? 20 : 22;
            InvalidateMeasure();
        }
    }

    /// <summary>Shows <paramref name="model"/> (null hides the mark and the name).</summary>
    public void SetModel(ExplainerModel? model)
    {
        _markColor?.Dispose();
        _markColor = null;
        _mark.IsVisible = model is not null;
        _name.IsVisible = model is not null;
        if (model is null)
        {
            InvalidateMeasure();
            return;
        }
        _mark.Data = model.Mark ?? ExplainerModelLabel.DefaultMark;
        _mark.Size = _compact ? 20 : 22;
        if (model.MarkBrush is { } brush)
        {
            _mark.Foreground = brush;
        }
        else
        {
            _markColor = ArcTokens.Bind(_mark, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Series1);
        }
        _name.Text = model.Name;
        InvalidateMeasure();
    }

    /// <summary>Shows a plain line (Ready).</summary>
    public void ShowText(string text, bool animate)
    {
        var key = "text:" + text;
        if (key == _lineKey)
        {
            return;
        }
        _lineKey = key;
        _number = null;
        var block = Small();
        block.Text = text;
        Swap(block, animate);
    }

    /// <summary>Shows the loop counter; when it already shows, only the number rolls.</summary>
    public void ShowCounter(Func<int, string> format, int loop, bool animate)
    {
        var text = format(loop);
        var number = loop.ToString(System.Globalization.CultureInfo.CurrentCulture);
        var at = text.IndexOf(number, StringComparison.Ordinal);
        var prefix = at >= 0 ? text[..at] : text + " ";
        var suffix = at >= 0 ? text[(at + number.Length)..] : string.Empty;
        var key = "counter:" + prefix + "\u0001" + suffix;
        if (key == _lineKey && _number is not null)
        {
            var digits = Small();
            digits.FontFeatures = AgentLoopText.Tabular;
            digits.Text = number;
            SwapIn(_number, digits, animate);
            return;
        }
        _lineKey = key;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var before = Small();
        before.Text = prefix;
        row.Children.Add(before);
        _number = new ArcContentSwap { Mode = ContentSwapMode.Roll, Direction = 1, ClipToBounds = false };
        var first = Small();
        first.FontFeatures = AgentLoopText.Tabular;
        first.Text = number;
        SwapIn(_number, first, animate: false);
        row.Children.Add(_number);
        if (suffix.Length > 0)
        {
            var after = Small();
            after.Text = suffix;
            row.Children.Add(after);
        }
        Swap(row, animate);
    }

    /// <summary>Shows the done or stopped line with its icon.</summary>
    public void ShowEnding(string text, bool stopped, bool animate)
    {
        var key = (stopped ? "stopped:" : "done:") + text;
        if (key == _lineKey)
        {
            return;
        }
        _lineKey = key;
        _number = null;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(AgentLoopText.Glyph(stopped ? AgentLoopIcons.CircleStop : ArcIcons.CircleCheck, 14,
            stopped ? ArcDesignTokenKind.Warning : ArcDesignTokenKind.Success));
        var block = Small();
        block.Text = text;
        row.Children.Add(block);
        Swap(row, animate);
    }

    /// <summary>The line's text as shown (tests read it).</summary>
    internal string LineKey => _lineKey ?? string.Empty;

    private static TextBlock Small()
    {
        var block = AgentLoopText.Line(12, 16.8, ArcDesignTokenKind.TextMuted);
        block.TextTrimming = TextTrimming.None;
        return block;
    }

    private void Swap(Control content, bool animate) => SwapIn(_line, content, animate);

    private static void SwapIn(ArcContentSwap swap, Control content, bool animate)
    {
        if (animate)
        {
            swap.Content = content;
            return;
        }
        var mode = swap.Mode;
        swap.Mode = ContentSwapMode.None;
        swap.Content = content;
        swap.Mode = mode;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var markRow = _compact ? 26 : 28;
        _mark.Measure(new Size(markRow, markRow));
        _name.Measure(new Size(MaxNameWidth, double.PositiveInfinity));
        _line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = _line.DesiredSize.Width;
        var height = _line.DesiredSize.Height;
        if (_name.IsVisible)
        {
            width = Math.Max(width, Math.Min(_name.DesiredSize.Width, MaxNameWidth));
            height += markRow + 2 + _name.DesiredSize.Height + 2;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        if (_name.IsVisible)
        {
            var markRow = _compact ? 26 : 28;
            _mark.Arrange(new Rect(0, y, finalSize.Width, markRow));
            y += markRow + 2;
            var nameWidth = Math.Min(_name.DesiredSize.Width, finalSize.Width);
            _name.Arrange(new Rect((finalSize.Width - nameWidth) / 2, y, nameWidth, _name.DesiredSize.Height));
            y += _name.DesiredSize.Height + 2;
        }
        _line.Arrange(new Rect(0, y, finalSize.Width, _line.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The footer row: the playback buttons on the left and the speed control on the right, 12 apart; when both do not fit
/// the speed control wraps under the buttons, left aligned.
/// </summary>
internal sealed class AgentLoopFooterPanel : Panel
{
    private const double Gap = 12;

    /// <summary>True when the second child wrapped onto its own row.</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double width = 0;
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(infinite);
        }
        if (Children.Count < 2)
        {
            foreach (var child in Children)
            {
                width = Math.Max(width, child.DesiredSize.Width);
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height);
        }
        var first = Children[0].DesiredSize;
        var second = Children[1].IsVisible ? Children[1].DesiredSize : default;
        var available = double.IsFinite(availableSize.Width) ? availableSize.Width : first.Width + Gap + second.Width;
        IsWrapped = second.Width > 0 && first.Width + Gap + second.Width > available + 0.5;
        if (IsWrapped)
        {
            Children[1].Measure(new Size(available, double.PositiveInfinity));
            return new Size(available, first.Height + Gap + Children[1].DesiredSize.Height);
        }
        return new Size(available, Math.Max(first.Height, second.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }
        var first = Children[0];
        if (Children.Count < 2)
        {
            first.Arrange(new Rect(0, (finalSize.Height - first.DesiredSize.Height) / 2, first.DesiredSize.Width, first.DesiredSize.Height));
            return finalSize;
        }
        var second = Children[1];
        if (IsWrapped)
        {
            first.Arrange(new Rect(0, 0, first.DesiredSize.Width, first.DesiredSize.Height));
            var width = Math.Min(second.DesiredSize.Width, finalSize.Width);
            second.Arrange(new Rect(0, first.DesiredSize.Height + Gap, width, second.DesiredSize.Height));
            return finalSize;
        }
        first.Arrange(new Rect(0, (finalSize.Height - first.DesiredSize.Height) / 2, first.DesiredSize.Width, first.DesiredSize.Height));
        second.Arrange(new Rect(finalSize.Width - second.DesiredSize.Width, (finalSize.Height - second.DesiredSize.Height) / 2,
            second.DesiredSize.Width, second.DesiredSize.Height));
        return finalSize;
    }
}
