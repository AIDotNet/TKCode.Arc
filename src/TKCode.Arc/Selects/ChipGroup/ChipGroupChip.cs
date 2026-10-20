using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// One chip of a <see cref="ChipGroup"/>, drawn directly. The control is only the hit area and the layout box; the
/// visible surface trails the box while its width morphs: when the chip's width changes the visible edge keeps the
/// old width and springs to the new one, the check grows from the left edge into the room the label hands over,
/// and the label slides across (P26, P5). Neighbours glide via the panel's FLIP (P19).
/// </summary>
internal sealed class ChipGroupChip : Control
{
    /// <summary>Room the check takes from the label: a 14px glyph and the space after it.</summary>
    internal const double CheckSlot = 18;

    private static readonly Geometry s_check = ArcIcons.Parse("M4 12.5 9.5 18 20 6.5");

    private readonly ChipGroup _owner;
    private readonly TranslateTransform _flipTransform = new();
    private TextLayout? _label;
    private TextLayout? _previousLabel;
    private double _labelMaxWidth = -1;
    private string _text;
    private bool _selected;
    private bool _pendingMorph;
    private double _lastWidth = -1;
    private bool _hover;
    private bool _pressed;
    private readonly SolidColorBrush _textBrush = new();
    private ImmutableSolidColorBrush? _fillBrush;
    private Pen? _borderPen;
    private Pen? _checkPen;
    private Pen? _ringPen;
    private Color _checkColor;
    private MotionValue<double>? _lag;
    private MotionValue<Vector>? _flip;
    private MotionValue<double>? _press;
    private MotionValue<double>? _tint;
    private MotionValue<double>? _swap;
    private MotionValue<double>? _presence;
    private bool _exiting;

    public ChipGroupChip(ChipGroup owner, object? key, string text, bool isMore)
    {
        _owner = owner;
        ItemKey = key;
        _text = text;
        IsMore = isMore;
        Focusable = true;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderTransform = _flipTransform;
        ZIndex = isMore ? 0 : 1;
    }

    public object? ItemKey { get; }

    public bool IsMore { get; }

    public int EntryIndex { get; set; } = -1;

    public string Text => _text;

    public bool IsSelectedChip => _selected;

    /// <summary>For the more chip: whether the folded chips are shown.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Leaving chips are out of the flow and only fade where they were.</summary>
    public bool IsLeaving => _exiting;

    internal Point? LastPosition { get; set; }

    internal bool IsFresh { get; set; } = true;

    // ---------------------------------------------------------------- state

    public void SetText(string text)
    {
        if (_text == text)
        {
            return;
        }
        var animate = IsMore && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        _previousLabel = animate ? _label : null;
        _text = text;
        _label = null;
        _pendingMorph = true;
        if (animate)
        {
            _swap ??= CreateRedraw(1);
            _swap.Jump(0);
            var spec = ArcMotion.IsFull(this)
                ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
                : MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Standard);
            _ = _swap.AnimateTo(1, spec);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void SetSelected(bool selected, bool animate)
    {
        if (_selected == selected)
        {
            return;
        }
        _selected = selected;
        _pendingMorph = animate;
        AnimateTint(animate);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void AnimateTint(bool animate)
    {
        var target = _selected ? 1.0 : 0.0;
        // Created on the first change, starting from the state the chip had before it.
        _tint ??= CreateRedraw(1 - target);
        if (animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            _ = _tint.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _tint.Jump(target);
        }
    }

    private MotionValue<double> CreateRedraw(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>Chips revealed by "+N more" grow in one after another (opacity fast, scale snappy from 0.9).</summary>
    public void PlayEnter(TimeSpan delay)
    {
        _exiting = false;
        var level = ArcMotion.GetEffectiveLevel(_owner);
        if (level == ArcMotionLevel.Off)
        {
            Opacity = 1;
            return;
        }
        _presence ??= new MotionValue<double>(this, 0, MotionVectors.Double);
        _presence.Changed -= ApplyPresence;
        _presence.Changed += ApplyPresence;
        _presence.Jump(0);
        ApplyPresence(0);
        _reducedPresence = level == ArcMotionLevel.Reduced;
        var spec = _reducedPresence
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Standard)
            : MotionSpec.Spring(ArcSpring.Snappy, delay);
        _ = _presence.AnimateTo(1, spec);
    }

    private bool _reducedPresence;

    /// <summary>Folded chips leave faster than they came: opacity and scale 0.9, instant.</summary>
    public Task PlayExit()
    {
        _exiting = true;
        Focusable = false;
        var level = ArcMotion.GetEffectiveLevel(_owner);
        if (level == ArcMotionLevel.Off || !IsLoaded)
        {
            return Task.CompletedTask;
        }
        _presence ??= new MotionValue<double>(this, 1, MotionVectors.Double);
        _presence.Changed -= ApplyPresence;
        _presence.Changed += ApplyPresence;
        _reducedPresence = level == ArcMotionLevel.Reduced;
        return _presence.AnimateTo(0, MotionSpec.Tween(_reducedPresence ? TimeSpan.FromMilliseconds(150) : ArcMotionPresets.Instant,
            ArcEasings.Standard));
    }

    private double _presenceScale = 1;

    private void ApplyPresence(double p)
    {
        Opacity = Math.Clamp(_reducedPresence ? p : Math.Min(1, p * 1.6), 0, 1);
        _presenceScale = _reducedPresence ? 1 : 0.9 + 0.1 * p;
        InvalidateVisual();
    }

    /// <summary>FLIP: the box moved by <paramref name="delta"/>; the visible chip starts where it was and springs home.</summary>
    public void FlipBy(Vector delta)
    {
        if (!ArcMotion.IsFull(_owner) || !IsLoaded)
        {
            _flip?.Jump(default);
            ApplyFlip(default);
            return;
        }
        _flip ??= CreateFlip();
        var velocity = _flip.Velocity;
        _flip.Jump(_flip.Value + delta);
        _ = _flip.AnimateTo(default, MotionSpec.Spring(ArcSpring.Morph), velocity);
    }

    private MotionValue<Vector> CreateFlip()
    {
        var value = new MotionValue<Vector>(this, default, MotionVectors.Vector);
        value.Changed += ApplyFlip;
        return value;
    }

    private void ApplyFlip(Vector offset)
    {
        _flipTransform.X = offset.X;
        _flipTransform.Y = offset.Y;
    }

    private double Lag => _lag?.Value ?? 0;

    // ---------------------------------------------------------------- input

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hover = true;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _exiting)
        {
            return;
        }
        e.Handled = true;
        _pressed = true;
        Focus(NavigationMethod.Pointer);
        AnimatePress(0.97, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        AnimatePress(1, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _owner.OnChipInvoked(this);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressed)
        {
            _pressed = false;
            AnimatePress(1, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
    }

    private void AnimatePress(double target, MotionSpec spec)
    {
        _press ??= CreateRedraw(1);
        if (!ArcMotion.IsFull(_owner) || !IsLoaded)
        {
            _press.Jump(1);
            return;
        }
        _ = _press.AnimateTo(target, spec);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && (e.Key == Key.Enter || e.Key == Key.Space) && !_exiting)
        {
            e.Handled = true;
            _owner.OnChipInvoked(this);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner.OnChipFocused(this);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    // ---------------------------------------------------------------- layout

    private Typeface Typeface => new(TextElement.GetFontFamily(_owner), FontStyle.Normal, _owner.ChipFontWeight);

    private TextLayout Build(string text, double maxWidth) =>
        new(text, Typeface, TextElement.GetFontSize(_owner), _textBrush, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(0, maxWidth), double.PositiveInfinity,
            20, 0, 1, IsMore ? s_tabular : null);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    internal void InvalidateText()
    {
        _label = null;
        _previousLabel = null;
        _checkPen = null;
        _ringPen = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = _owner.ChipPadding;
        var slot = _selected && !IsMore ? CheckSlot : 0;
        var room = double.IsFinite(availableSize.Width) ? availableSize.Width - padding * 2 - slot : double.PositiveInfinity;
        var natural = Build(_text, double.PositiveInfinity);
        var width = Math.Min(natural.WidthIncludingTrailingWhitespace, Math.Max(0, room));
        if (_label is null || Math.Abs(_labelMaxWidth - width) > 0.5)
        {
            _label = Build(_text, Math.Ceiling(width) + 0.5);
            _labelMaxWidth = width;
        }
        return new Size(Math.Ceiling(padding * 2 + slot + width), _owner.ChipHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        if (_lastWidth >= 0 && Math.Abs(width - _lastWidth) > 0.5 && _pendingMorph)
        {
            // The edge keeps the old width and springs to the new one, keeping the speed it already had.
            if (ArcMotion.IsFull(_owner) && IsLoaded)
            {
                _lag ??= CreateRedraw(0);
                var velocity = _lag.Velocity;
                _lag.Jump(_lag.Value + _lastWidth - width);
                _ = _lag.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph), velocity);
            }
            else
            {
                _lag?.Jump(0);
            }
        }
        _pendingMorph = false;
        _lastWidth = width;
        return finalSize;
    }

    // ---------------------------------------------------------------- render

    public override void Render(DrawingContext context)
    {
        var owner = _owner;
        var size = Bounds.Size;
        var lag = Lag;
        var press = _press?.Value ?? 1;
        var scale = press * _presenceScale;
        var center = new Point(size.Width / 2, size.Height / 2);
        using var _ = Math.Abs(scale - 1) > 0.0001
            ? context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                    Matrix.CreateTranslation(center.X, center.Y))
            : default(DrawingContext.PushedState?);

        var hover = _hover && !_exiting;
        var tint = _tint?.Value ?? (_selected ? 1 : 0);
        var colors = owner.ChipColors;
        Color fill, border;
        if (IsMore)
        {
            fill = colors.MoreFill;
            border = hover ? colors.BorderHover : colors.Border;
        }
        else
        {
            var restFill = Lerp(colors.Fill, colors.SelectedFill, tint);
            var restBorder = Lerp(colors.Border, colors.SelectedBorder, tint);
            fill = hover ? Lerp(colors.Fill, colors.SelectedHoverFill, tint) : restFill;
            border = hover ? Lerp(colors.BorderHover, colors.SelectedHoverBorder, tint) : restBorder;
        }

        // The surface: the visible edge trails the layout edge by `lag`.
        var surfaceWidth = Math.Max(size.Height, size.Width + lag);
        var surfaceLeft = 0.0;
        var surface = new RoundedRect(new Rect(surfaceLeft, 0, surfaceWidth, size.Height), size.Height / 2);
        if (_fillBrush is null || _fillBrush.Color != fill)
        {
            _fillBrush = new ImmutableSolidColorBrush(fill);
        }
        if (_borderPen is null || ((ISolidColorBrush)_borderPen.Brush!).Color != border)
        {
            _borderPen = new Pen(new ImmutableSolidColorBrush(border), 1);
        }
        context.DrawRectangle(_fillBrush, _borderPen, surface.Deflate(0.5, 0.5));

        if (IsFocused && IsKeyboardFocusVisible())
        {
            _ringPen ??= new Pen(owner.FocusRingBrush, 2);
            context.DrawRectangle(null, _ringPen, new RoundedRect(new Rect(-3, -3, surfaceWidth + 6, size.Height + 6), size.Height / 2 + 3));
        }

        var padding = owner.ChipPadding;
        var textColor = IsMore
            ? colors.MoreText
            : (hover || _selected) ? colors.TextOn : Lerp(colors.Text, colors.TextOn, tint);
        if (_textBrush.Color != textColor)
        {
            _textBrush.Color = textColor;
        }
        if (IsMore)
        {
            RenderMoreText(context, size, lag);
            return;
        }

        // The check grows from zero width in exactly the gap the label opens as it slides over.
        var slot = _selected ? CheckSlot : 0;
        var grown = Math.Max(0, (slot + lag) / CheckSlot);
        if (grown > 0.001)
        {
            DrawCheck(context, size, grown, colors.Check);
        }
        if (_label is { } label)
        {
            var x = padding + slot + lag;
            using (context.PushClip(new Rect(0, 0, Math.Max(0, surfaceWidth - padding + 2), size.Height)))
            {
                label.Draw(context, new Point(x, (size.Height - label.Height) / 2));
            }
        }
    }

    private void RenderMoreText(DrawingContext context, Size size, double lag)
    {
        // The text stays centred on the visible surface while its edge catches up.
        var visibleWidth = size.Width + lag;
        var swap = _swap?.Value ?? 1;
        var full = ArcMotion.IsFull(_owner);
        var em = TextElement.GetFontSize(_owner);
        if (_previousLabel is { } previous && swap < 1)
        {
            var y = full ? -0.3 * em * swap : 0;
            using (context.PushOpacity(1 - swap))
            {
                previous.Draw(context, new Point((visibleWidth - previous.WidthIncludingTrailingWhitespace) / 2,
                    (size.Height - previous.Height) / 2 + y));
            }
        }
        else if (swap >= 1)
        {
            _previousLabel = null;
        }
        if (_label is { } label)
        {
            var y = full ? 0.3 * em * (1 - swap) : 0;
            using (context.PushOpacity(swap))
            {
                label.Draw(context, new Point((visibleWidth - label.WidthIncludingTrailingWhitespace) / 2,
                    (size.Height - label.Height) / 2 + y));
            }
        }
    }

    private void DrawCheck(DrawingContext context, Size size, double grown, Color color)
    {
        if (_checkPen is null || _checkColor != color)
        {
            _checkPen = new Pen(new ImmutableSolidColorBrush(color), 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _checkColor = color;
        }
        var glyph = 14.0;
        var left = 12.0;
        var top = size.Height / 2 - glyph / 2;
        var scale = Math.Min(grown, 1.1);
        var opacity = Math.Min(1, grown * 1.4);
        var draw = Math.Clamp(grown, 0.001, 1);
        var unit = glyph / 24.0 * scale;
        // Grows from its left edge (transform-origin 0 50%).
        var transform = Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(left, top + glyph / 2 - glyph * scale / 2);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(transform))
        {
            if (draw >= 0.999)
            {
                context.DrawGeometry(null, _checkPen, s_check);
            }
            else
            {
                var length = s_check.ContourLength;
                if (length > 0 && s_check.TryGetSegment(0, length * draw, true, out var segment) && segment is not null)
                {
                    context.DrawGeometry(null, _checkPen, segment);
                }
            }
        }
    }

    private bool IsKeyboardFocusVisible() => Classes.Contains(":focus-visible");

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChipGroupChipAutomationPeer(this);

    /// <summary>Each chip is a button with a pressed state (Arc: aria-pressed); the more chip is expandable.</summary>
    private sealed class ChipGroupChipAutomationPeer : ControlAutomationPeer, IToggleProvider, IExpandCollapseProvider, IInvokeProvider
    {
        private readonly ChipGroupChip _chip;

        public ChipGroupChipAutomationPeer(ChipGroupChip chip) : base(chip) => _chip = chip;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() => _chip.Text;

        protected override bool IsContentElementCore() => !_chip.IsLeaving;

        protected override bool IsControlElementCore() => !_chip.IsLeaving;

        public ToggleState ToggleState => _chip._selected ? ToggleState.On : ToggleState.Off;

        public void Toggle() => _chip._owner.OnChipInvoked(_chip);

        public ExpandCollapseState ExpandCollapseState => !_chip.IsMore
            ? ExpandCollapseState.LeafNode
            : _chip.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (_chip.IsMore && !_chip.IsExpanded)
            {
                _chip._owner.OnChipInvoked(_chip);
            }
        }

        public void Collapse()
        {
            if (_chip.IsMore && _chip.IsExpanded)
            {
                _chip._owner.OnChipInvoked(_chip);
            }
        }

        public void Invoke() => _chip._owner.OnChipInvoked(_chip);
    }
}
