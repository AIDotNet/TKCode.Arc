using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One legend entry of a <see cref="WindRoseChart"/>: a speed bin's swatch and name. A tab stop of its own; hover or focus
/// highlights that bin in every petal.
/// </summary>
internal sealed class WindRoseLegendItem : Control, Avalonia.Rendering.ICustomHitTest
{
    private const double Gap = 6;
    private const double Inset = 6;
    private const double SwatchSize = 10;

    private readonly WindRoseChart _owner;
    private ChartsBRun? _text;
    private int _textVersion = -1;
    private string _label = string.Empty;
    private bool _hover;

    public WindRoseLegendItem(WindRoseChart owner)
    {
        _owner = owner;
        Focusable = true;
        FocusAdorner = null;
    }

    /// <summary>The bin this entry stands for.</summary>
    public int Bin { get; private set; } = -1;

    public string Text => _label;

    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    public void Configure(int bin, string label, string accessibleName)
    {
        if (Bin != bin || _label != label)
        {
            Bin = bin;
            _label = label;
            _textVersion = -1;
            InvalidateMeasure();
        }
        AutomationProperties.SetName(this, accessibleName);
        InvalidateVisual();
    }

    public void InvalidateInk()
    {
        _textVersion = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private ChartsBRun EnsureText()
    {
        var palette = _owner.Palette;
        if (_text is null || _textVersion != palette.Version)
        {
            _text?.Dispose();
            _text = ChartsBText.Run(_label, palette, palette.TypeXS, palette.TextSecondary);
            _textVersion = palette.Version;
        }
        return _text;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = EnsureText();
        return new Size(Inset * 2 + SwatchSize + Gap + text.Width, Math.Max(24, text.Height + 8));
    }

    public override void Render(DrawingContext context)
    {
        if (Bin < 0)
        {
            return;
        }
        var palette = _owner.Palette;
        var text = EnsureText();
        var y = Bounds.Height / 2;
        var lit = _owner.IsBinLit(Bin);
        if (_hover)
        {
            context.DrawRectangle(palette.Brush(ChartsBMath.Alpha(palette.Foreground, 0.05)), null, new RoundedRect(new Rect(Bounds.Size), 8));
        }
        context.DrawRectangle(palette.Brush(_owner.BinColor(Bin)), null, new RoundedRect(new Rect(Inset, y - SwatchSize / 2, SwatchSize, SwatchSize), 3));
        var origin = new Point(Inset + SwatchSize + Gap, y - text.Height / 2);
        if (lit)
        {
            text.Draw(context, origin, palette.Brush(palette.Foreground));
        }
        else
        {
            text.Draw(context, origin);
        }
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), new RoundedRect(new Rect(Bounds.Size).Deflate(1), 8));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _hover = true;
        _owner.HighlightBin(this, true);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        if (!IsFocused)
        {
            _owner.HighlightBin(this, false);
        }
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner.HighlightBin(this, true);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_hover)
        {
            _owner.HighlightBin(this, false);
        }
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _text?.Dispose();
        _text = null;
        _textVersion = -1;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LegendItemPeer(this);

    private sealed class LegendItemPeer(WindRoseLegendItem owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}
