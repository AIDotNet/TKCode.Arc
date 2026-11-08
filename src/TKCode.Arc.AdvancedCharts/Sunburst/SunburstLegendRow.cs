using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One row of a <see cref="Sunburst"/> legend: swatch, label, value and share of the centre. A named button: choosing a
/// branch moves it to the centre, choosing a leaf highlights its segment, and hovering or focusing highlights the segment.
/// </summary>
internal sealed class SunburstLegendRow : Avalonia.Controls.Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double Inset = 8;
    private const double SwatchSize = 10;
    private const double SwatchGap = 8;
    private const double ColumnGap = 10;
    private const double ShareWidth = 44;

    private static Cursor? s_hand;

    private readonly Sunburst _owner;
    private SunburstEntry? _entry;
    private SunburstEntry? _center;
    private FormattedText? _label;
    private FormattedText? _value;
    private FormattedText? _share;
    private int _textVersion = -1;
    private double _textWidth = -1;
    private bool _hover;
    private bool _pressed;

    public SunburstLegendRow(Sunburst owner)
    {
        _owner = owner;
        Focusable = true;
        FocusAdorner = null;
    }

    internal SunburstEntry? Entry => _entry;

    internal string AccessibleName => AutomationProperties.GetName(this) ?? string.Empty;

    public void SetEntry(SunburstEntry entry, SunburstEntry center)
    {
        _entry = entry;
        _center = center;
        _textVersion = -1;
        AutomationProperties.SetName(this, $"{entry.Label}, {_owner.Format(entry.Value)}, {ChartsBMath.Percent(Sunburst.ShareOf(entry, center), 1)}");
        s_hand ??= new Cursor(StandardCursorType.Hand);
        Cursor = entry.IsBranch ? s_hand : Cursor.Default;
        InvalidateVisual();
    }

    internal void Invoke()
    {
        if (_entry is not { } entry)
        {
            return;
        }
        if (entry.IsBranch)
        {
            _owner.ZoomTo(entry.Id, SunburstFocusChangeSource.Legend);
        }
        else
        {
            _owner.Stage.SetActive(entry, null);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? _owner.LegendMinWidth : availableSize.Width, _owner.LegendRowHeight);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hover = true;
            _owner.Stage.SetActive(_entry, null, tooltip: false);
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        if (!IsFocused)
        {
            _owner.Stage.ClearActive();
        }
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Invoke();
        }
        _pressed = false;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Invoke();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _owner.Stage.SetActive(_entry, null, tooltip: false);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_hover)
        {
            _owner.Stage.ClearActive();
        }
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    public override void Render(DrawingContext context)
    {
        if (_entry is not { } entry || _center is not { } center)
        {
            return;
        }
        var p = _owner.Palette;
        var size = Bounds.Size;
        var radius = _owner.LegendRowRadius;
        var box = new Rect(size);
        context.DrawRectangle(p.Brush(_hover ? ChartsBMath.Alpha(p.Foreground, 0.05) : Colors.Transparent), null, new RoundedRect(box, radius));
        var x = Inset;
        var fill = _owner.Stage.FillOf(entry);
        ChartsBTooltip.DrawSwatch(context, p, new Rect(x, (size.Height - SwatchSize) / 2, SwatchSize, SwatchSize), fill, ChartsBSwatchShape.Round);
        x += SwatchSize + SwatchGap;
        if (_textVersion != p.Version || Math.Abs(_textWidth - size.Width) > 0.5)
        {
            _textVersion = p.Version;
            _textWidth = size.Width;
            _value = ChartsBText.Make(_owner.Format(entry.Value), p, p.TypeSM, p.TextSecondary);
            _share = ChartsBText.Make(Sunburst.LegendShare(Sunburst.ShareOf(entry, center)), p, p.TypeXS, p.TextMuted);
            var room = size.Width - x - Inset - ShareWidth - ColumnGap * 2 - _value.WidthIncludingTrailingWhitespace;
            _label = ChartsBText.Trimmed(entry.Label, p, p.TypeSM, p.Foreground, Math.Max(10, room));
        }
        context.DrawText(_label!, new Point(x, (size.Height - _label!.Height) / 2));
        var right = size.Width - Inset;
        context.DrawText(_share!, new Point(right - _share!.WidthIncludingTrailingWhitespace, (size.Height - _share.Height) / 2));
        right -= ShareWidth + ColumnGap;
        context.DrawText(_value!, new Point(right - _value!.WidthIncludingTrailingWhitespace, (size.Height - _value.Height) / 2));
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(box.Deflate(1), Math.Max(0, radius - 1)));
        }
    }

    private sealed class RowPeer(SunburstLegendRow owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetHelpTextCore()
            => ((SunburstLegendRow)Owner).Entry is { IsBranch: true } ? "Zooms in" : null;

        public void Invoke() => ((SunburstLegendRow)Owner).Invoke();
    }
}
