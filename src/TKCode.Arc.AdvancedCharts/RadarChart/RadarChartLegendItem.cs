using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One legend entry of a <see cref="RadarChart"/>: its line key and name. A tab stop of its own; hover or focus brings
/// the profile's shape forward.
/// </summary>
internal sealed class RadarChartLegendItem : Control, Avalonia.Rendering.ICustomHitTest
{
    private const double Gap = 7;
    private const double Inset = 6;

    private readonly RadarChart _owner;
    private FormattedText? _text;
    private int _textVersion = -1;

    public RadarChartLegendItem(RadarChart owner)
    {
        _owner = owner;
        Focusable = true;
        FocusAdorner = null;
    }

    public RadarChartShape? Shape { get; private set; }

    public string Text => Shape?.Series.Label ?? string.Empty;

    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    public void Configure(RadarChartShape shape)
    {
        Shape = shape;
        _textVersion = -1;
        AutomationProperties.SetName(this, $"{shape.Series.Label}, {RadarChart.LineKeyName(shape.Index)} line");
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void InvalidateInk()
    {
        _textVersion = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private FormattedText EnsureText()
    {
        var palette = _owner.Palette;
        if (_text is null || _textVersion != palette.Version)
        {
            _text = ChartsBText.Make(Text, palette, palette.TypeXS, palette.TextSecondary);
            _textVersion = palette.Version;
        }
        return _text;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = EnsureText();
        return new Size(Inset * 2 + _owner.LineKeyWidth + Gap + text.WidthIncludingTrailingWhitespace, Math.Max(24, text.Height + 8));
    }

    public override void Render(DrawingContext context)
    {
        if (Shape is not { } shape)
        {
            return;
        }
        _owner.EnsureInk();
        var text = EnsureText();
        var y = Bounds.Height / 2;
        if (shape.Pen is { } pen)
        {
            context.DrawLine(pen, new Point(Inset, y), new Point(Inset + _owner.LineKeyWidth, y));
        }
        context.DrawText(text, new Point(Inset + _owner.LineKeyWidth + Gap, y - text.Height / 2));
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            var palette = _owner.Palette;
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), new RoundedRect(new Rect(Bounds.Size).Deflate(1), 8));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _owner.BringForward(this, true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsFocused)
        {
            _owner.BringForward(this, false);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner.BringForward(this, true);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _owner.BringForward(this, false);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LegendItemPeer(this);

    private sealed class LegendItemPeer(RadarChartLegendItem owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}
