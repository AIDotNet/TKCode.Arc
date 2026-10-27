using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The round close button of the open chart: a 16px cross, a SurfaceMuted disc on hover, .95 when pressed, a ring on keyboard focus.</summary>
internal sealed class MetricExplorerCloseButton : Control
{
    private readonly AnalyticsPalette _palette = new();
    private readonly LiveLoop _loop;
    private AnalyticsTween _hover = new(0);
    private AnalyticsTween _press = new(0);
    private bool _pressed;
    private int _paletteVersion = -1;

    static MetricExplorerCloseButton()
    {
        FocusableProperty.OverrideDefaultValue<MetricExplorerCloseButton>(true);
    }

    public MetricExplorerCloseButton()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        Cursor = new Cursor(StandardCursorType.Hand);
        ActualThemeVariantChanged += (_, _) => _paletteVersion = -1;
        ResourcesChanged += (_, _) => _paletteVersion = -1;
    }

    /// <summary>Raised on click, Enter or Space.</summary>
    public event Action? Invoked;

    /// <summary>The pressed scale now (tests).</summary>
    internal double Scale => 1 - 0.05 * _press.Value;

    private void Fade(ref AnalyticsTween tween, double target)
    {
        if (ArcMotion.IsFull(this) && this.IsAttachedToVisualTree())
        {
            tween.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            tween.Jump(target);
        }
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var moving = _hover.Advance(dt);
        moving |= _press.Advance(dt);
        InvalidateVisual();
        return moving;
    }

    protected override Size MeasureOverride(Size availableSize) => new(32, 32);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            Fade(ref _hover, 1);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pressed = false;
        Fade(ref _hover, 0);
        Fade(ref _press, 0);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            Focus(NavigationMethod.Pointer);
            Fade(ref _press, 1);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        Fade(ref _press, 0);
        if (_pressed && inside)
        {
            _pressed = false;
            Invoked?.Invoke();
        }
        _pressed = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            Invoked?.Invoke();
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ClosePeer(this);

    private sealed class ClosePeer(MetricExplorerCloseButton owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetAcceleratorKeyCore() => "Escape";

        public void Invoke() => owner.Invoked?.Invoke();
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        if (_paletteVersion < 0 || !_palette.IsReady)
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
        }
        var scale = 1 - 0.05 * _press.Value;
        var center = new Point(size.Width / 2, size.Height / 2);
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            var hover = Math.Clamp(_hover.Value, 0, 1);
            if (hover > 0.004)
            {
                context.DrawEllipse(_palette.Brush(_palette.SurfaceMuted, hover), null, center, size.Width / 2, size.Height / 2);
            }
            var color = AnalyticsColors.Lerp(_palette.TextSecondary, _palette.Foreground, hover);
            var pen = _palette.Pen(color, 1.75 * 16 / 24);
            var s = 16.0 / 24;
            var origin = new Point(center.X - 8, center.Y - 8);
            context.DrawLine(pen, new Point(origin.X + 18 * s, origin.Y + 6 * s), new Point(origin.X + 6 * s, origin.Y + 18 * s));
            context.DrawLine(pen, new Point(origin.X + 6 * s, origin.Y + 6 * s), new Point(origin.X + 18 * s, origin.Y + 18 * s));
        }
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, new Rect(size), size.Width / 2);
        }
    }
}

/// <summary>The chart's key: a solid 16×2 line for this period and a dashed 16×1 line for the previous one, each with its text.</summary>
internal sealed class MetricExplorerLegendKey : Control
{
    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(16);
    private string _current = "This period";
    private string _previous = "Previous period";
    private bool _showPrevious = true;
    private int _paletteVersion = -1;

    static MetricExplorerLegendKey()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<MetricExplorerLegendKey>(false);
    }

    public MetricExplorerLegendKey()
    {
        ActualThemeVariantChanged += (_, _) => Reset();
        ResourcesChanged += (_, _) => Reset();
    }

    private void Reset()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Sets the texts and whether the previous period is part of the key.</summary>
    public void SetContent(string current, string previous, bool showPrevious)
    {
        _current = current;
        _previous = previous;
        _showPrevious = showPrevious;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EnsurePalette()
    {
        if (this.IsAttachedToVisualTree() && (_paletteVersion < 0 || !_palette.IsReady))
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
        }
    }

    private Typeface Face() => new(_palette.IsReady ? _palette.BodyFont : FontFamily.Default);

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        var width = 16 + 8 + _text.Get(_current, Face(), 12, true).WidthIncludingTrailingWhitespace;
        if (_showPrevious)
        {
            width += 16 + 16 + 8 + _text.Get(_previous, Face(), 12, true).WidthIncludingTrailingWhitespace;
        }
        return new Size(Math.Ceiling(width), 16.8);
    }

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        if (!_palette.IsReady)
        {
            return;
        }
        var face = Face();
        var mid = Bounds.Height / 2;
        var x = 0.0;
        context.DrawRectangle(_palette.Brush(_palette.Accent), null, new RoundedRect(new Rect(x, mid - 1, 16, 2), 1));
        x += 16 + 8;
        var current = _text.Get(_current, face, 12, true);
        current.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(x, mid - current.Height / 2));
        if (!_showPrevious)
        {
            return;
        }
        x += current.WidthIncludingTrailingWhitespace + 16;
        context.DrawLine(_palette.Pen(_palette.TextMuted, 1, dash: 2, gap: 2, cap: PenLineCap.Flat), new Point(x, mid), new Point(x + 16, mid));
        x += 16 + 8;
        var previous = _text.Get(_previous, face, 12, true);
        previous.Draw(context, _palette.Brush(_palette.TextSecondary), new Point(x, mid - previous.Height / 2));
    }
}
