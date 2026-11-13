using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// One keycap of a <see cref="KeycapKeyboard"/>: a light, template-free control that draws its housing, cap, face and
/// legend, is its own tab stop and button for assistive technology, and reports pointer and Enter/Space presses to the
/// keyboard, which owns every key's state and motion.
/// </summary>
internal sealed class KeycapKeyVisual : Control
{
    private readonly KeycapKeyboard _owner;
    private TextLayout? _legend;
    private TextLayout? _activeLegend;
    private TextLayout? _binding;
    private bool _spaceDown;
    private bool _enterDown;

    static KeycapKeyVisual()
    {
        FocusableProperty.OverrideDefaultValue<KeycapKeyVisual>(true);
        ClipToBoundsProperty.OverrideDefaultValue<KeycapKeyVisual>(false);
    }

    public KeycapKeyVisual(KeycapKeyboard owner, int index, KeycapKeyboardKey key)
    {
        _owner = owner;
        Index = index;
        Entry = key;
        Cursor = new Cursor(StandardCursorType.Hand);
        // Keys sit on fractional key units, as the design measures them; rounding would make equal keys unequal.
        UseLayoutRounding = false;
    }

    public int Index { get; }

    public KeycapKeyboardKey Entry { get; }

    /// <summary>Lays out the legends for a new skin or binding label (outside the render pass).</summary>
    public void Rebuild(KeycapKeyboardSkin skin, bool accent, string? bindingLabel, IBrush? bindingBrush)
    {
        DisposeText();
        if (Entry.Label.Length > 0)
        {
            var size = Entry.IsCorner ? skin.CornerSize : skin.LegendSize;
            var rest = accent ? skin.AccentLegend : Entry.IsCorner ? skin.CornerLegend : skin.Legend;
            _legend = Layout(Entry.Label, skin, size, rest);
            _activeLegend = accent ? null : Layout(Entry.Label, skin, size, skin.ActiveLegend);
        }
        if (bindingLabel is { Length: > 0 })
        {
            _binding = new TextLayout(bindingLabel, skin.Typeface, skin.CornerSize, bindingBrush ?? skin.BindingText,
                TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight,
                Math.Max(1, Bounds.Width - skin.FaceInset.Left - skin.FaceInset.Right - skin.CornerPadding.Left * 2));
        }
        InvalidateVisual();
    }

    private static TextLayout Layout(string text, KeycapKeyboardSkin skin, double size, IBrush brush)
        => new(text, skin.Typeface, size, brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null,
            FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, size);

    public void DisposeText()
    {
        _legend?.Dispose();
        _activeLegend?.Dispose();
        _binding?.Dispose();
        _legend = null;
        _activeLegend = null;
        _binding = null;
    }

    public override void Render(DrawingContext context)
    {
        var skin = _owner.Skin;
        if (skin is null)
        {
            return;
        }
        var width = Bounds.Width;
        var unit = skin.Unit;
        var accent = _owner.IsAccentKey(Index);
        var offset = _owner.OffsetOf(Index) * skin.Travel;
        var tint = _owner.TintOf(Index);
        var lit = _owner.LegendOf(Index);

        // The whole box answers the pointer, gaps inside the key included.
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, skin.KeyHeight));
        context.DrawRectangle(skin.Housing, null, new RoundedRect(new Rect(0, skin.Travel, width, unit), skin.Radius), skin.HousingShadows);
        var cap = new Rect(0, offset, width, unit);
        context.DrawRectangle(accent ? skin.CapAccent : skin.CapNeutral, null, new RoundedRect(cap, skin.Radius), skin.CapShadows);
        if (tint > 0.001)
        {
            using (context.PushOpacity(tint))
            {
                context.DrawRectangle(accent ? skin.TintAccent : skin.TintNeutral, null, new RoundedRect(cap, skin.Radius));
            }
        }
        var face = cap.Deflate(skin.FaceInset);
        context.DrawRectangle(accent ? skin.FaceAccent : skin.FaceNeutral, null, new RoundedRect(face, skin.FaceRadius), skin.FaceShadows);
        if (_binding is not null)
        {
            using (context.PushOpacity(0.6))
            {
                context.DrawRectangle(skin.BindingFace, null, new RoundedRect(face, skin.FaceRadius));
            }
        }
        DrawLegend(context, skin, face, accent, lit);
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            // A square 2 px ring 2 px outside the key's whole box (cap and side).
            context.DrawRectangle(null, skin.FocusPen, new Rect(-3, -3, width + 6, skin.KeyHeight + 6));
        }
    }

    private void DrawLegend(DrawingContext context, KeycapKeyboardSkin skin, Rect face, bool accent, double lit)
    {
        var icon = Entry.Icon;
        if (icon != KeycapIcon.None)
        {
            var size = skin.IconSize;
            var at = new Point(face.X + skin.CornerPadding.Left, face.Bottom - skin.CornerPadding.Bottom - size);
            var geometry = icon switch
            {
                KeycapIcon.Backspace => KeycapKeyboardSkin.BackspaceIcon,
                KeycapIcon.Enter => KeycapKeyboardSkin.EnterIcon,
                _ => KeycapKeyboardSkin.ShiftIcon
            };
            var scale = size / 24;
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(at.X, at.Y)))
            {
                if (accent)
                {
                    context.DrawGeometry(null, skin.AccentPen, geometry);
                }
                else
                {
                    if (lit < 0.999)
                    {
                        context.DrawGeometry(null, skin.CornerPen, geometry);
                    }
                    if (lit > 0.001)
                    {
                        using (context.PushOpacity(lit))
                        {
                            context.DrawGeometry(null, skin.ActivePen, geometry);
                        }
                    }
                }
            }
            DrawBinding(context, skin, face);
            return;
        }
        if (_legend is null)
        {
            DrawBinding(context, skin, face);
            return;
        }
        Point origin;
        if (Entry.IsCorner)
        {
            origin = new Point(face.X + skin.CornerPadding.Left, face.Bottom - skin.CornerPadding.Bottom - _legend.Height);
        }
        else
        {
            var y = _binding is null ? face.Y + (face.Height - _legend.Height) / 2 : face.Y + skin.CornerPadding.Top;
            origin = new Point(face.X + (face.Width - _legend.WidthIncludingTrailingWhitespace) / 2, y);
        }
        if (_activeLegend is null || lit < 0.999)
        {
            _legend.Draw(context, origin);
        }
        if (_activeLegend is not null && lit > 0.001)
        {
            using (context.PushOpacity(lit))
            {
                _activeLegend.Draw(context, origin);
            }
        }
        DrawBinding(context, skin, face);
    }

    private void DrawBinding(DrawingContext context, KeycapKeyboardSkin skin, Rect face)
    {
        if (_binding is null)
        {
            return;
        }
        var x = Entry.IsCorner ? face.X + skin.CornerPadding.Left : face.X + (face.Width - _binding.WidthIncludingTrailingWhitespace) / 2;
        var y = Entry.IsCorner ? face.Y + skin.CornerPadding.Top : face.Bottom - skin.CornerPadding.Bottom - _binding.Height;
        _binding.Draw(context, new Point(x, y));
    }

    // --- input -------------------------------------------------------------------------------------------------------

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _owner.SetHover(Index, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner.SetHover(Index, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !_owner.CanClick(Index))
        {
            return;
        }
        // Pressed on touch down; the capture keeps the key down until the finger or button lifts, wherever it goes.
        e.Pointer.Capture(this);
        _owner.PointerDown(Index);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_owner.PointerKey == Index)
        {
            var over = new Rect(Bounds.Size).Contains(e.GetPosition(this));
            _owner.PointerUp(Index, over);
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_owner.PointerKey == Index)
        {
            _owner.PointerUp(Index, false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Enter or Space on a focused key presses it as a button; other keys bubble to the keyboard.
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            var first = e.Key == Key.Space ? !_spaceDown : !_enterDown;
            if (e.Key == Key.Space)
            {
                _spaceDown = true;
            }
            else
            {
                _enterDown = true;
            }
            _owner.ButtonDown(Index, first);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && (_spaceDown || _enterDown))
        {
            if (e.Key == Key.Space)
            {
                _spaceDown = false;
            }
            else
            {
                _enterDown = false;
            }
            e.Handled = true;
            if (!_spaceDown && !_enterDown)
            {
                _owner.ButtonUp(Index);
            }
            return;
        }
        base.OnKeyUp(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        // The space that pressed this key as a button is not typed a second time.
        if (_spaceDown && e.Text == " ")
        {
            e.Handled = true;
            return;
        }
        base.OnTextInput(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_spaceDown || _enterDown)
        {
            _spaceDown = false;
            _enterDown = false;
            _owner.ButtonUp(Index);
        }
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new KeyPeer(this);

    /// <summary>Presses the key once, as a click (assistive technology).</summary>
    internal void Press() => _owner.Tap(Index);

    private sealed class KeyPeer(KeycapKeyVisual owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() => ((KeycapKeyVisual)Owner).Entry.AccessibleName;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => ((KeycapKeyVisual)Owner).Press();
    }
}

/// <summary>
/// The soft light under pressed keys: one layer under every keycap, so a fading glow never covers a neighbouring key and
/// only this layer redraws while glows fade.
/// </summary>
internal sealed class KeycapGlowLayer : Control
{
    private readonly KeycapKeyboard _owner;

    public KeycapGlowLayer(KeycapKeyboard owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
        ClipToBounds = false;
        UseLayoutRounding = false;
    }

    public override void Render(DrawingContext context)
    {
        var skin = _owner.Skin;
        if (skin is null)
        {
            return;
        }
        var keys = _owner.KeyVisuals;
        for (var i = 0; i < keys.Length; i++)
        {
            var glow = _owner.GlowOf(i);
            if (glow <= 0.001)
            {
                continue;
            }
            var box = keys[i].Bounds.Inflate(skin.GlowInset);
            using (context.PushOpacity(glow * keys[i].Opacity))
            {
                context.DrawRectangle(skin.Glow, null, new RoundedRect(box, skin.GlowRadius));
            }
        }
    }
}
