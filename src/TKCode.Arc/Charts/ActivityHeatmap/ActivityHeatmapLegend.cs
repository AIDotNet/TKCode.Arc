using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Legend of an <see cref="ActivityHeatmap"/>: a caption naming the highlighted level, and "Less [five swatches] More".
/// One tab stop; ← / → / Home / End move between swatches and preview their level, Enter / Space pin it, Escape clears.
/// </summary>
internal sealed class ActivityHeatmapLegend : Avalonia.Controls.Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double SwatchWidth = 15;
    private const double SwatchHeight = 20;
    private const double Gap = 8;

    private readonly ActivityHeatmap _owner;
    private readonly MotionValue<double> _roll;
    private FormattedText? _less;
    private FormattedText? _more;
    private FormattedText? _caption;
    private FormattedText? _oldCaption;
    private string _captionText = string.Empty;
    private int _version = -1;
    private int _focus;
    private int _hover = -1;
    private IDisposable? _previewTimer;
    private ChartsBAutomationPeer? _peer;

    public ActivityHeatmapLegend(ActivityHeatmap owner)
    {
        _owner = owner;
        Focusable = true;
        _roll = new MotionValue<double>(this, 1, MotionVectors.Double);
        _roll.Changed += _ => InvalidateVisual();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    internal string Caption => _captionText;

    internal int FocusedLevel => _focus;

    string IChartsBAccessible.ChartRoleDescription => "legend";

    string IChartsBAccessible.ChartName => "Highlight days by level";

    string IChartsBAccessible.ChartHelp => _captionText.Length > 0 ? _captionText : _owner.LegendDescription;

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    public void InvalidateText()
    {
        _version = -1;
        OnHighlightChanged();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>The caption says how many days sit at the highlighted level; it rises in when it changes.</summary>
    public void OnHighlightChanged()
    {
        var palette = _owner.Palette;
        var level = _owner.Highlight;
        var text = string.Empty;
        if (level is { } l)
        {
            var days = _owner.Model.PerLevel[l];
            var count = $"{_owner.Number(days)} {(days == 1 ? "day" : "days")}";
            text = Bounds.Width > 0 && Bounds.Width <= 440 ? count : $"{count} with {_owner.RangeText(l, brief: true)}";
        }
        if (text == _captionText && _caption is not null && _version == palette.Version)
        {
            InvalidateVisual();
            return;
        }
        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && _caption is not null;
        _oldCaption = animate ? _caption : null;
        _captionText = text;
        _caption = text.Length == 0 ? null : ChartsBText.Make(text, palette, palette.TypeXS, palette.TextSecondary);
        if (animate)
        {
            _roll.Jump(0);
            _ = _roll.AnimateTo(1, MotionSpec.Tween(ArcMotion.IsFull(this) ? ArcMotionPresets.Standard : ArcMotionPresets.Fast, ArcEasings.Enter));
        }
        else
        {
            _roll.Jump(1);
        }
        _peer?.Announce();
        InvalidateVisual();
    }

    private void EnsureText()
    {
        var palette = _owner.Palette;
        if (_version == palette.Version && _less is not null)
        {
            return;
        }
        _version = palette.Version;
        _less = ChartsBText.Make("Less", palette, palette.TypeXS, palette.TextMuted);
        _more = ChartsBText.Make("More", palette, palette.TypeXS, palette.TextMuted);
        if (_captionText.Length > 0)
        {
            _caption = ChartsBText.Make(_captionText, palette, palette.TypeXS, palette.TextSecondary);
        }
    }

    private double ScaleLeft => Bounds.Width - (_more?.WidthIncludingTrailingWhitespace ?? 0) - Gap - 5 * SwatchWidth - Gap -
                                (_less?.WidthIncludingTrailingWhitespace ?? 0);

    private double SwatchesLeft => ScaleLeft + (_less?.WidthIncludingTrailingWhitespace ?? 0) + Gap;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureText();
        var width = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        return new Size(width, Math.Max(SwatchHeight, (_less?.Height ?? 16)));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && _owner.Highlight is not null)
        {
            OnHighlightChanged();
        }
    }

    private int LevelAt(Point point)
    {
        var x = point.X - SwatchesLeft;
        if (x < 0 || x >= 5 * SwatchWidth || point.Y < 0 || point.Y > Bounds.Height)
        {
            return -1;
        }
        return (int)(x / SwatchWidth);
    }

    private void Preview(int? level)
    {
        _previewTimer?.Dispose();
        _previewTimer = null;
        if (level is null)
        {
            _previewTimer = DispatcherTimer.RunOnce(() => _owner.SetPreview(null), TimeSpan.FromMilliseconds(90));
            return;
        }
        _owner.SetPreview(level);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var level = LevelAt(e.GetPosition(this));
        if (level == _hover)
        {
            return;
        }
        _hover = level;
        Cursor = level >= 0 ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        Preview(level >= 0 ? level : null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            _hover = -1;
            Preview(null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var level = LevelAt(e.GetPosition(this));
        if (level >= 0)
        {
            _focus = level;
            _owner.TogglePin(level);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Preview(_focus);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Preview(null);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        int? next = e.Key switch
        {
            Key.Left => _focus - 1,
            Key.Right => _focus + 1,
            Key.Home => 0,
            Key.End => 4,
            _ => null
        };
        if (next is { } target)
        {
            e.Handled = true;
            _focus = Math.Clamp(target, 0, 4);
            Preview(_focus);
            InvalidateVisual();
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            _owner.TogglePin(_focus);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _owner.SetPreview(null);
        }
    }

    public override void Render(DrawingContext context)
    {
        EnsureText();
        var palette = _owner.Palette;
        var height = Bounds.Height;
        var x = ScaleLeft;
        context.DrawText(_less!, new Point(x, (height - _less!.Height) / 2));
        var swatches = SwatchesLeft;
        for (var level = 0; level < 5; level++)
        {
            var box = new Rect(swatches + level * SwatchWidth, (height - SwatchHeight) / 2, SwatchWidth, SwatchHeight);
            context.DrawRectangle(palette.Brush(_owner.LevelColor(level)), null, new RoundedRect(box.CenterRect(new Rect(0, 0, 11, 11)), 3));
            if (_owner.PinnedLevel == level)
            {
                context.DrawRectangle(null, palette.Pen(palette.Foreground, 1.5), new RoundedRect(box.Deflate(0.75), 3.25));
            }
            if (level == _focus && IsFocused && Classes.Contains(":focus-visible"))
            {
                context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), new RoundedRect(box.Inflate(2), 6));
            }
        }
        context.DrawText(_more!, new Point(swatches + 5 * SwatchWidth + Gap, (height - _more!.Height) / 2));
        var room = Math.Max(0, ScaleLeft - 12);
        using (context.PushClip(new Rect(0, -4, room, height + 8)))
        {
            var roll = _roll.Value;
            var shift = palette.TypeXS * 0.3;
            if (roll < 0.999 && _oldCaption is { } old)
            {
                using (context.PushOpacity(1 - ChartsBMath.Clamp(roll * 2, 0, 1)))
                {
                    context.DrawText(old, new Point(0, (height - old.Height) / 2 - shift * roll));
                }
            }
            if (_caption is { } caption)
            {
                using (context.PushOpacity(roll))
                {
                    context.DrawText(caption, new Point(0, (height - caption.Height) / 2 + shift * (1 - roll)));
                }
            }
        }
    }
}
