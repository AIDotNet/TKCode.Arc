using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaKey = Avalonia.Input.Key;

namespace TKCode.Arc;

/// <summary>
/// One row of the <see cref="WaffleChart"/> legend: swatch, name and a share that rolls to its new value on the smooth
/// spring. A toggle: click, Enter or Space pins the category; hover (mouse) or keyboard focus previews it.
/// </summary>
internal sealed class WaffleLegendItem : Avalonia.Controls.Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double Padding = 10;
    private readonly WaffleChart _owner;
    private readonly MotionValue<double> _share;
    private readonly MotionValue<double> _on;
    private readonly MotionValue<double> _dim;
    private readonly MotionValue<double> _emphasis;
    private readonly MotionValue<double> _press;
    private WaffleCategory? _item;
    private FormattedText? _label;
    private FormattedText? _value;
    private string? _valueText;
    private double _labelWidth = -1;
    private int _version = -1;
    private bool _isOn;
    private bool _isPinned;
    private bool _hover;

    public WaffleLegendItem(WaffleChart owner)
    {
        _owner = owner;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        _share = Value(0);
        _on = Value(0);
        _dim = Value(0);
        _emphasis = Value(0);
        _press = Value(0);
    }

    public string? Key => _item?.Key;

    public bool IsPinned => _isPinned;

    internal string ShareText => _owner.ShareText(_share.Value);

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public void Bind(WaffleCategory item, double share)
    {
        var changed = !ReferenceEquals(item, _item) && item.Label != _item?.Label;
        _item = item;
        if (changed)
        {
            InvalidateText();
        }
        if (ArcMotion.IsFull(this) && IsLoaded && Math.Abs(_share.Value - share) > 1e-9)
        {
            _ = _share.AnimateTo(share, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _share.Jump(share);
        }
        AutomationProperties.SetName(this, item.Label);
        InvalidateVisual();
    }

    public void SetState(bool on, bool dim, bool pinned)
    {
        _isOn = on;
        _isPinned = pinned;
        Animate(_on, on ? 1 : 0);
        Animate(_dim, dim ? 1 : 0);
        UpdateEmphasis();
    }

    public void InvalidateText()
    {
        _label = null;
        _value = null;
        InvalidateVisual();
    }

    private void UpdateEmphasis() => Animate(_emphasis, _isOn || _isPinned || _hover ? 1 : 0);

    private void Animate(MotionValue<double> value, double target)
    {
        if (ArcMotion.IsFull(this) && IsLoaded)
        {
            _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            value.Jump(target);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var palette = _owner.Palette;
        var height = Math.Max(36, palette.TypeBase * 1.5 + 8);
        return new Size(double.IsInfinity(availableSize.Width) ? 220 : availableSize.Width, height);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse && _item is not null)
        {
            _owner.CancelPreviewClear();
            _hover = true;
            UpdateEmphasis();
            _owner.SetPreview(_item.Key);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        UpdateEmphasis();
        Animate(_press, 0);
        // The list clears its preview only when the pointer leaves it, not between two rows.
        _owner.ClearPreviewSoon();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _ = _press.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_press.Target > 0.5)
        {
            Animate(_press, 0);
            if (_item is not null && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                _owner.TogglePin(_item.Key);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && _item is not null && e.Key is AvaloniaKey.Enter or AvaloniaKey.Space)
        {
            e.Handled = true;
            _owner.TogglePin(_item.Key);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_item is not null && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _owner.CancelPreviewClear();
            _owner.SetPreview(_item.Key);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _owner.SetPreview(null);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    public override void Render(DrawingContext context)
    {
        if (_item is null)
        {
            return;
        }
        var palette = _owner.Palette;
        var size = Bounds.Size;
        var shareText = _owner.ShareText(_share.Value);
        var valueSize = palette.TypeBase;
        if (_value is null || _valueText != shareText || _version != palette.Version)
        {
            _valueText = shareText;
            _value = ChartsBText.Make(shareText, palette, valueSize, palette.Foreground, FontWeight.Medium);
        }
        var valueWidth = Math.Max(_value.WidthIncludingTrailingWhitespace, valueSize * 3.2);
        var labelRoom = Math.Max(0, size.Width - Padding * 2 - 20 - 10 - valueWidth);
        if (_label is null || _version != palette.Version || Math.Abs(_labelWidth - labelRoom) > 0.5)
        {
            _version = palette.Version;
            _labelWidth = labelRoom;
            _label = ChartsBText.Trimmed(_item.Label, palette, palette.TypeSM, palette.TextSecondary, labelRoom);
        }
        var scale = 1 - 0.02 * _press.Value;
        var center = new Rect(size).Center;
        using var transform = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                                    Matrix.CreateTranslation(center.X, center.Y));
        using var opacity = context.PushOpacity(1 - 0.5 * _dim.Value);
        if (_on.Value > 0.001)
        {
            context.DrawRectangle(palette.Brush(ChartsBMath.Alpha(palette.Foreground, 0.05 * _on.Value)), null,
                new RoundedRect(new Rect(size), 12));
        }
        ChartsBTooltip.DrawSwatch(context, palette, new Rect(Padding, (size.Height - 10) / 2, 10, 10), _owner.ColorOf(_item.Key),
            _owner.ShapeOf(_item.Key));
        _label.SetForegroundBrush(palette.Brush(ChartsBMath.Lerp(palette.TextSecondary, palette.Foreground, _emphasis.Value)));
        context.DrawText(_label, new Point(Padding + 20, (size.Height - _label.Height) / 2));
        context.DrawText(_value, new Point(size.Width - Padding - _value.WidthIncludingTrailingWhitespace, (size.Height - _value.Height) / 2));
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), new RoundedRect(new Rect(size).Inflate(2), 14));
        }
    }

    private sealed class Peer(WaffleLegendItem owner) : ControlAutomationPeer(owner), IToggleProvider
    {
        private WaffleLegendItem Item => (WaffleLegendItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetHelpTextCore() => Item._item is { } item ? $"{Item._owner.ShareText(Item._owner.ShareOf(item))}" : null;

        public ToggleState ToggleState => Item.IsPinned ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            if (Item._item is { } item)
            {
                Item._owner.TogglePin(item.Key);
            }
        }
    }
}
