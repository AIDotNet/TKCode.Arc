using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Places the DonutChart ring and its legend: side by side (ring left, 32px gap) when the chart is at least
/// 460px wide, otherwise the ring centered above the legend with a 24px gap.
/// </summary>
internal sealed class DonutChartLayout : Panel
{
    private const double Breakpoint = 460;
    private const double SideGap = 32;
    private const double StackGap = 24;

    private bool Side(double width) => Children.Count > 1 && Children[1].IsVisible && width >= Breakpoint;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }
        var ring = Children[0];
        var legend = Children.Count > 1 ? Children[1] : null;
        ring.Measure(availableSize);
        if (legend is null || !legend.IsVisible)
        {
            return ring.DesiredSize;
        }
        if (Side(availableSize.Width))
        {
            legend.Measure(new Size(Math.Max(0, availableSize.Width - ring.DesiredSize.Width - SideGap), availableSize.Height));
            return new Size(availableSize.Width, Math.Max(ring.DesiredSize.Height, legend.DesiredSize.Height));
        }
        legend.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? Math.Max(ring.DesiredSize.Width, legend.DesiredSize.Width) : availableSize.Width,
            ring.DesiredSize.Height + StackGap + legend.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }
        var ring = Children[0];
        var legend = Children.Count > 1 ? Children[1] : null;
        var ringSize = ring.DesiredSize;
        if (legend is null || !legend.IsVisible)
        {
            ring.Arrange(new Rect((finalSize.Width - ringSize.Width) / 2, 0, ringSize.Width, ringSize.Height));
            return finalSize;
        }
        if (Side(finalSize.Width))
        {
            var height = finalSize.Height;
            ring.Arrange(new Rect(0, (height - ringSize.Height) / 2, ringSize.Width, ringSize.Height));
            var left = ringSize.Width + SideGap;
            legend.Arrange(new Rect(left, (height - legend.DesiredSize.Height) / 2, Math.Max(0, finalSize.Width - left), legend.DesiredSize.Height));
            return finalSize;
        }
        ring.Arrange(new Rect((finalSize.Width - ringSize.Width) / 2, 0, ringSize.Width, ringSize.Height));
        legend.Arrange(new Rect(0, ringSize.Height + StackGap, finalSize.Width, legend.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The DonutChart legend: one row per segment. A row that joins or leaves (a part folding into Other) opens or
/// closes its height on the smooth spring, so the rows below glide instead of jumping.
/// </summary>
internal sealed class DonutChartLegend : Panel
{
    internal DonutChart? Owner { get; set; }

    internal void Sync(IReadOnlyList<DonutChart.Slice> slices)
    {
        var existing = new Dictionary<string, DonutChartLegendRow>(StringComparer.Ordinal);
        foreach (var child in Children)
        {
            if (child is DonutChartLegendRow row)
            {
                existing[row.SliceKey] = row;
            }
        }
        var order = new List<Control>();
        foreach (var slice in slices)
        {
            if (!existing.Remove(slice.Key, out var row))
            {
                row = new DonutChartLegendRow { Legend = this };
            }
            row.Configure(slice);
            order.Add(row);
        }
        // Rows that leave close their height first, keeping their place.
        foreach (var row in existing.Values)
        {
            var index = Children.IndexOf(row);
            row.Leave();
            order.Insert(Math.Min(order.Count, Math.Max(0, index)), row);
        }
        for (var i = 0; i < order.Count; i++)
        {
            var current = i < Children.Count ? Children[i] : null;
            if (!ReferenceEquals(current, order[i]))
            {
                Children.Remove(order[i]);
                Children.Insert(i, order[i]);
            }
        }
        while (Children.Count > order.Count)
        {
            Children.RemoveAt(Children.Count - 1);
        }
        Refresh();
    }

    internal void Remove(DonutChartLegendRow row) => Children.Remove(row);

    internal void Refresh()
    {
        foreach (var child in Children)
        {
            if (child is DonutChartLegendRow row)
            {
                row.RefreshState();
            }
        }
    }

    internal IEnumerable<DonutChartLegendRow> Rows()
    {
        foreach (var child in Children)
        {
            if (child is DonutChartLegendRow { IsLeaving: false } row)
            {
                yield return row;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = 0, width = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var presence = child is DonutChartLegendRow row ? row.Presence : 1;
            height += (child.DesiredSize.Height + 2) * presence;
            width = Math.Max(width, child.DesiredSize.Width);
        }
        return new Size(width, Math.Max(0, height - 2));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        foreach (var child in Children)
        {
            var presence = child is DonutChartLegendRow row ? row.Presence : 1;
            child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
            y += (child.DesiredSize.Height + 2) * presence;
        }
        return finalSize;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Owner?.SetPreview(null);
    }

    internal void MoveFocus(DonutChartLegendRow from, Key key)
    {
        var rows = Rows().ToList();
        var index = rows.IndexOf(from);
        if (index < 0 || rows.Count == 0)
        {
            return;
        }
        var next = key switch
        {
            Key.Down or Key.Right => index + 1,
            Key.Up or Key.Left => index - 1,
            Key.Home => 0,
            _ => rows.Count - 1
        };
        rows[((next % rows.Count) + rows.Count) % rows.Count].Focus(NavigationMethod.Directional);
    }
}

/// <summary>
/// One self-drawn legend row: dot, label (and the members of Other), value and share. Values count to new
/// numbers on the smooth spring; a hidden segment keeps its row, its dot empties to a ring and its text quiets.
/// </summary>
internal sealed class DonutChartLegendRow : Control, Avalonia.Rendering.ICustomHitTest
{
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private static readonly SpringSpec Count = SpringSpec.FromVisual(0.4, 0);
    private MotionValue<double>? _value;
    private MotionValue<double>? _share;
    private MotionValue<double>? _presence;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _hidden;
    private MotionValue<double>? _press;
    private DonutChart.Slice? _slice;
    private bool _pressed;
    private string _valueText = string.Empty;
    private double _valueShown = double.NaN;
    private string _shareText = string.Empty;
    private double _shareShown = double.NaN;

    public DonutChartLegendRow()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    internal DonutChartLegend? Legend { get; set; }

    private DonutChart? Owner => Legend?.Owner;

    internal string SliceKey => _slice?.Key ?? string.Empty;

    internal DonutChart.Slice? Slice => _slice;

    internal bool IsLeaving { get; private set; }

    internal double Presence => _presence?.Value ?? 1;

    internal bool IsSliceHidden => Owner?.IsSliceHidden(SliceKey) ?? false;

    private MotionValue<double> CreateValue(double initial, bool measure = false)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            if (measure)
            {
                Legend?.InvalidateMeasure();
            }
            InvalidateVisual();
        };
        return value;
    }

    internal void Configure(DonutChart.Slice slice)
    {
        var fresh = _slice is null;
        _slice = slice;
        if (IsLeaving)
        {
            IsLeaving = false;
        }
        _presence ??= CreateValue(fresh && Owner?.IsLoaded == true && Legend?.Children.Count > 0 ? 0 : 1, measure: true);
        if (_presence.Target < 1)
        {
            _ = _presence.AnimateTo(1, Owner?.IsMotionFull == true ? MotionSpec.Spring(ArcSpring.Smooth) : MotionSpec.Instant);
        }
        InvalidateMeasure();
    }

    internal void Leave()
    {
        IsLeaving = true;
        IsTabStop = false;
        _presence ??= CreateValue(1, measure: true);
        _ = Close();
    }

    private async Task Close()
    {
        var done = await _presence!.AnimateTo(0, Owner?.IsMotionFull == true ? MotionSpec.Spring(ArcSpring.Smooth) : MotionSpec.Instant);
        if (done && IsLeaving)
        {
            Legend?.Remove(this);
        }
    }

    internal void RefreshState()
    {
        var owner = Owner;
        if (owner is null || _slice is null)
        {
            return;
        }
        var hidden = owner.IsSliceHidden(_slice.Key);
        var total = owner.Total;
        var share = hidden || total <= 0 ? 0 : _slice.Value / total;
        var level = owner.MotionLevel;
        var spec = level == ArcMotionLevel.Full ? MotionSpec.Spring(Count) : MotionSpec.Instant;
        _value ??= CreateValue(_slice.Value);
        _share ??= CreateValue(share);
        if (Math.Abs(_value.Target - _slice.Value) > 1e-9)
        {
            _ = _value.AnimateTo(_slice.Value, spec);
        }
        if (Math.Abs(_share.Target - share) > 1e-9)
        {
            _ = _share.AnimateTo(share, spec);
        }
        _hidden ??= CreateValue(hidden ? 1 : 0);
        _ = _hidden.AnimateTo(hidden ? 1 : 0, level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        AutomationProperties.SetName(this, hidden ? $"{_slice.Label}, hidden" : owner.Describe(_slice));
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        if (owner is null || _slice is null)
        {
            return new Size(0, 40);
        }
        owner.EnsurePalette();
        var palette = owner.Palette;
        var lineHeight = palette.SizeSM * 1.4;
        var height = 12 + lineHeight + (_slice.Members is not null ? palette.SizeXS * 1.4 : 0);
        return new Size(double.IsInfinity(availableSize.Width) ? 260 : availableSize.Width, Math.Max(40, height));
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch || IsLeaving)
        {
            return;
        }
        Fade(ref _hover, 1);
        Owner?.SetPreview(IsSliceHidden ? null : SliceKey);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Fade(ref _hover, 0);
        SetPressed(false);
    }

    private void Fade(ref MotionValue<double>? value, double to)
    {
        value ??= CreateValue(0);
        _ = value.AnimateTo(to, Owner?.MotionLevel == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !IsLeaving)
        {
            e.Handled = true;
            SetPressed(true);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        SetPressed(false);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Activate();
        }
    }

    private void SetPressed(bool pressed)
    {
        if (_pressed == pressed)
        {
            return;
        }
        _pressed = pressed;
        _press ??= CreateValue(1);
        _ = _press.AnimateTo(pressed ? 0.985 : 1, Owner?.IsMotionFull == true
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Instant);
    }

    internal void Activate()
    {
        if (_slice is not null)
        {
            Owner?.ActivateRow(_slice.Key);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var owner = Owner;
        if (owner is null)
        {
            base.OnKeyDown(e);
            return;
        }
        switch (e.Key)
        {
            case Key.Escape when owner.ActiveKey is not null:
                e.Handled = true;
                owner.Select(null);
                return;
            case Key.Enter or Key.Space:
                e.Handled = true;
                Activate();
                return;
            case Key.Down or Key.Right or Key.Up or Key.Left or Key.Home or Key.End:
                e.Handled = true;
                Legend?.MoveFocus(this, e.Key);
                return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional || Classes.Contains(":focus-visible"))
        {
            Owner?.SetPreview(IsSliceHidden ? null : SliceKey);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (Legend is { } legend && !legend.Children.Any(c => c.IsFocused))
        {
            Owner?.SetPreview(null);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        if (owner is null || _slice is null)
        {
            return;
        }
        var palette = owner.Palette;
        var texts = owner.Texts;
        var size = Bounds.Size;
        var presence = Presence;
        using var clip = context.PushClip(new Rect(0, 0, size.Width, size.Height * presence + 0.5));
        using var fade = context.PushOpacity(presence);
        var scale = _press?.Value ?? 1;
        var center = new Point(size.Width / 2, size.Height / 2);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale)
                                                    * Matrix.CreateTranslation(center.X, center.Y));
        var row = new RoundedRect(new Rect(size), 12);
        var current = owner.CurrentKey == _slice.Key || owner.ActiveKey == _slice.Key;
        var wash = Math.Max(_hover?.Value ?? 0, current ? 1 : 0);
        if (wash > 0.001)
        {
            context.DrawRectangle(palette.Brush(Oklch.WithAlpha(palette.Foreground, 0.05 * wash)), null, row);
        }
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), row.Inflate(2, 2));
        }
        var hidden = _hidden?.Value ?? 0;
        var color = owner.SliceColor(_slice);
        var dotCenter = new Point(10 + 5, size.Height / 2);
        context.DrawEllipse(palette.Brush(Oklch.WithAlpha(color, 1 - ChartPalette.Step(hidden))), palette.Pen(color, 1.5), dotCenter, 4.25, 4.25);

        var shareWidth = palette.SizeSM * 3.25;
        var shareRight = size.Width - 10;
        var share = _share?.Value ?? 0;
        if (double.IsNaN(_shareShown) || Math.Abs(share - _shareShown) > 1e-6)
        {
            _shareShown = share;
            _shareText = DonutChart.Share(share);
        }
        var value = _value?.Value ?? _slice.Value;
        var valueRounded = Math.Abs(_slice.Value - Math.Round(_slice.Value)) < 1e-9 ? Math.Round(value) : value;
        if (double.IsNaN(_valueShown) || Math.Abs(valueRounded - _valueShown) > 1e-9)
        {
            _valueShown = valueRounded;
            _valueText = owner.FormatValue(valueRounded);
        }
        var muted = ChartPalette.Step(hidden);
        var shareColor = ChartPalette.Lerp(palette.Foreground, palette.TextMuted, muted);
        var valueColor = ChartPalette.Lerp(palette.TextSecondary, palette.TextMuted, muted);
        var labelColor = ChartPalette.Lerp(palette.Foreground, palette.TextMuted, muted);
        var shareText = texts.Get(palette, _shareText, palette.SizeSM, shareColor, medium: true);
        var valueText = texts.Get(palette, _valueText, palette.SizeSM, valueColor);
        var mid = size.Height / 2;
        context.DrawText(shareText, new Point(shareRight - shareText.WidthIncludingTrailingWhitespace, mid - shareText.Height / 2));
        var valueRight = shareRight - shareWidth - 10;
        context.DrawText(valueText, new Point(valueRight - valueText.WidthIncludingTrailingWhitespace, mid - valueText.Height / 2));
        var textLeft = 10 + 10 + 10;
        var textWidth = Math.Max(1, valueRight - valueText.WidthIncludingTrailingWhitespace - 10 - textLeft);
        var label = texts.Get(palette, _slice.Label, palette.SizeSM, labelColor, maxWidth: textWidth);
        var members = DonutChart.MembersText(_slice);
        if (members is null)
        {
            context.DrawText(label, new Point(textLeft, mid - label.Height / 2));
        }
        else
        {
            var membersText = texts.Get(palette, members, palette.SizeXS, palette.TextMuted, maxWidth: textWidth);
            var top = mid - (label.Height + membersText.Height) / 2;
            context.DrawText(label, new Point(textLeft, top));
            context.DrawText(membersText, new Point(textLeft, top + label.Height));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DonutChartLegendRowAutomationPeer(this);

    internal bool IsPressedState => Owner?.LegendAction == DonutLegendAction.Select
        ? Owner.ActiveKey == SliceKey
        : !IsSliceHidden;
}

/// <summary>Each legend row is a toggle: shown (Toggle mode) or pinned (Select mode).</summary>
internal sealed class DonutChartLegendRowAutomationPeer : ControlAutomationPeer, IToggleProvider
{
    public DonutChartLegendRowAutomationPeer(DonutChartLegendRow owner) : base(owner)
    {
    }

    private DonutChartLegendRow Row => (DonutChartLegendRow)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    ToggleState IToggleProvider.ToggleState => Row.IsPressedState ? ToggleState.On : ToggleState.Off;

    void IToggleProvider.Toggle() => Row.Activate();
}
