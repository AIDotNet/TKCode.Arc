using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The text area above a <see cref="KeycapKeyboard"/>: the typed text, centered and wrapped, pinned to its newest line in a
/// box two lines tall (older lines scroll up and can be scrolled back with the wheel), a caret after the last character
/// that holds still while typing and blinks when idle, and new characters that fade in out of a slight blur.
/// </summary>
internal sealed class KeycapKeyboardText : Control
{
    /// <summary>How long a new character takes to fade in and sharpen.</summary>
    internal const double ArrivalSeconds = 0.15;

    /// <summary>How long the caret holds still after a key before it blinks again.</summary>
    internal static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>Half the caret's blink period: on, then off, for this long each.</summary>
    internal static readonly TimeSpan BlinkHalf = TimeSpan.FromMilliseconds(530);

    private const double SidePadding = 8;
    private const double ArrivalBlur = 2;

    private readonly KeycapKeyboard _owner;
    private readonly List<Arrival> _arrivals = [];
    private readonly List<ArrivalGlyph> _glyphs = [];
    private readonly RectangleGeometry _clip = new();
    private string _text = string.Empty;
    private string _placeholder = string.Empty;
    private TextLayout? _layout;
    private TextLayout? _placeholderLayout;
    private bool _layoutValid;
    private double _builtWidth = double.NaN;
    private double _scroll;
    private DispatcherTimer? _blink;
    private bool _caretOn = true;
    private bool _blinking;

    static KeycapKeyboardText()
    {
        FocusableProperty.OverrideDefaultValue<KeycapKeyboardText>(true);
        ClipToBoundsProperty.OverrideDefaultValue<KeycapKeyboardText>(false);
    }

    public KeycapKeyboardText(KeycapKeyboard owner)
    {
        _owner = owner;
        Cursor = new Cursor(StandardCursorType.Ibeam);
    }

    public FontFamily TextFont { get; set; } = FontFamily.Default;

    public IBrush? TextBrush { get; set; }

    public IBrush? PlaceholderBrush { get; set; }

    public IBrush? CaretBrush { get; set; }

    public IPen? FocusPen { get; set; }

    /// <summary>The font size, set by the keyboard from its width.</summary>
    public double TextSize { get; private set; } = 32;

    /// <summary>Height of one line: 1.3 of the font size.</summary>
    public double LineHeight => TextSize * 1.3;

    /// <summary>The box's height: two lines and 4 px.</summary>
    public static double HeightFor(double fontSize) => 2 * fontSize * 1.3 + 4;

    /// <summary>The text shown (tests).</summary>
    internal string Text => _text;

    /// <summary>Whether the caret is drawn now (tests).</summary>
    internal bool IsCaretVisible => _caretOn;

    /// <summary>The number of characters still fading in (tests).</summary>
    internal int ArrivingCount => _arrivals.Count;

    public void SetFontSize(double size)
    {
        if (!size.Equals(TextSize))
        {
            TextSize = size;
            Invalidate();
        }
    }

    public void SetPlaceholder(string placeholder)
    {
        if (placeholder != _placeholder)
        {
            _placeholder = placeholder;
            Invalidate();
        }
    }

    /// <summary>A visual value changed (font, brushes): lay the text out again.</summary>
    public void Invalidate()
    {
        _layoutValid = false;
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>
    /// Shows new text. <paramref name="typed"/> characters at the end were just typed and fade in at
    /// <paramref name="now"/> (0 when the text was set, deleted from or motion is reduced).
    /// </summary>
    public void SetText(string text, int typed, double now)
    {
        var old = _text.Length;
        _text = text;
        var grown = text.Length - old;
        for (var i = _arrivals.Count - 1; i >= 0; i--)
        {
            var a = _arrivals[i];
            a.FromEnd += grown;
            if (a.FromEnd <= 0 || a.FromEnd > text.Length)
            {
                _arrivals.RemoveAt(i);
            }
            else
            {
                _arrivals[i] = a;
            }
        }
        for (var k = Math.Min(typed, text.Length); k >= 1; k--)
        {
            if (!char.IsWhiteSpace(text[text.Length - k]))
            {
                _arrivals.Add(new Arrival { FromEnd = k, Start = now });
            }
        }
        _scroll = 0;
        Invalidate();
    }

    /// <summary>A key was typed: the caret holds still, and blinks again after <see cref="IdleDelay"/>.</summary>
    public void NoteTyping()
    {
        _caretOn = true;
        _blinking = false;
        if (_blink is not null)
        {
            _blink.Stop();
        }
        StartBlink();
        InvalidateVisual();
    }

    /// <summary>Advances the fading characters; true while any still fades.</summary>
    public bool Step(double now)
    {
        if (_arrivals.Count == 0)
        {
            return false;
        }
        var finished = false;
        for (var i = _arrivals.Count - 1; i >= 0; i--)
        {
            if (now - _arrivals[i].Start >= ArrivalSeconds)
            {
                _arrivals.RemoveAt(i);
                finished = true;
            }
        }
        if (finished)
        {
            // The finished characters join the main text layout.
            _layoutValid = false;
            EnsureLayout();
        }
        UpdateGlyphs(now);
        return _arrivals.Count > 0;
    }

    /// <summary>Drops every fading character at once (motion stopped).</summary>
    public void SettleArrivals()
    {
        if (_arrivals.Count == 0)
        {
            return;
        }
        _arrivals.Clear();
        _layoutValid = false;
        EnsureLayout();
        UpdateGlyphs(0);
    }

    // --- layout ------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, HeightFor(TextSize));

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!finalSize.Width.Equals(_builtWidth))
        {
            _layoutValid = false;
        }
        _clip.Rect = new Rect(finalSize);
        foreach (var glyph in _glyphs)
        {
            glyph.Arrange(new Rect(finalSize));
        }
        EnsureLayout(finalSize.Width);
        UpdateGlyphs(_owner.Clock);
        return finalSize;
    }

    private Typeface Face => new(TextFont);

    private double LetterSpacing => -0.03 * TextSize;

    private void EnsureLayout(double width = double.NaN)
    {
        if (double.IsNaN(width))
        {
            width = Bounds.Width;
        }
        if (_layoutValid)
        {
            return;
        }
        _layoutValid = true;
        _builtWidth = width;
        _layout?.Dispose();
        _layout = null;
        _placeholderLayout?.Dispose();
        _placeholderLayout = null;
        var inner = Math.Max(1, width - 2 * SidePadding);
        if (_text.Length > 0)
        {
            List<ValueSpan<TextRunProperties>>? hidden = null;
            if (_arrivals.Count > 0)
            {
                hidden = new List<ValueSpan<TextRunProperties>>(_arrivals.Count);
                var props = new GenericTextRunProperties(Face, TextSize, foregroundBrush: Brushes.Transparent);
                foreach (var a in _arrivals.OrderBy(a => -a.FromEnd))
                {
                    hidden.Add(new ValueSpan<TextRunProperties>(_text.Length - a.FromEnd, 1, props));
                }
            }
            _layout = new TextLayout(_text, Face, TextSize, TextBrush, TextAlignment.Center, TextWrapping.Wrap, TextTrimming.None,
                null, FlowDirection.LeftToRight, inner, double.PositiveInfinity, LineHeight, LetterSpacing, 0, null, hidden);
        }
        else if (_placeholder.Length > 0)
        {
            _placeholderLayout = new TextLayout(_placeholder, Face, TextSize, PlaceholderBrush, TextAlignment.Left, TextWrapping.NoWrap,
                TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(1, inner - 5), double.PositiveInfinity,
                LineHeight, LetterSpacing);
        }
        ClampScroll();
        InvalidateVisual();
    }

    /// <summary>The top of the text: the last line rests on the box's bottom edge, scrolled back by the wheel.</summary>
    private double TextTop => Bounds.Height - ContentHeight + _scroll;

    private double ContentHeight => _layout is { } layout ? Math.Max(LineHeight, layout.Height) : LineHeight;

    private void ClampScroll() => _scroll = Math.Clamp(_scroll, 0, Math.Max(0, ContentHeight - Bounds.Height));

    // --- arrivals ----------------------------------------------------------------------------------------------------

    private struct Arrival
    {
        public int FromEnd;
        public double Start;
    }

    private void UpdateGlyphs(double now)
    {
        var count = _layout is null ? 0 : _arrivals.Count;
        while (_glyphs.Count < count)
        {
            var glyph = new ArrivalGlyph { Clip = _clip, IsHitTestVisible = false };
            _glyphs.Add(glyph);
            VisualChildren.Add(glyph);
            glyph.Measure(Bounds.Size);
            glyph.Arrange(new Rect(Bounds.Size));
        }
        for (var i = 0; i < _glyphs.Count; i++)
        {
            var glyph = _glyphs[i];
            if (i >= count)
            {
                glyph.Show(null, default, 0);
                continue;
            }
            var a = _arrivals[i];
            var index = _text.Length - a.FromEnd;
            var progress = Math.Clamp((now - a.Start) / ArrivalSeconds, 0, 1);
            var eased = KeycapKeyboardSkin.FadeEasing.Ease(progress);
            var at = _layout!.HitTestTextPosition(index);
            var origin = new Point(SidePadding + at.X, TextTop + at.Y);
            glyph.Show(_text.Substring(index, char.IsHighSurrogate(_text[index]) && index + 1 < _text.Length ? 2 : 1), origin, eased);
            glyph.Typeface = Face;
            glyph.Size = TextSize;
            glyph.Brush = TextBrush;
            glyph.LineHeight = LineHeight;
            glyph.Effect = ArcBlur.Get(ArrivalBlur * (1 - eased));
        }
    }

    /// <summary>One character fading in: drawn on its own so it can carry a blur and an opacity.</summary>
    private sealed class ArrivalGlyph : Control
    {
        private TextLayout? _layout;
        private string? _text;
        private Point _origin;

        public Typeface Typeface { get; set; }

        public double Size { get; set; }

        public IBrush? Brush { get; set; }

        public double LineHeight { get; set; }

        public void Show(string? text, Point origin, double opacity)
        {
            Opacity = opacity;
            if (text != _text || origin != _origin)
            {
                _text = text;
                _origin = origin;
                _layout?.Dispose();
                _layout = null;
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            if (_text is null)
            {
                return;
            }
            _layout ??= new TextLayout(_text, Typeface, Size, Brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None,
                null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, LineHeight);
            _layout.Draw(context, _origin);
        }
    }

    // --- drawing -----------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // Answers the pointer over the whole box (focus, wheel).
        context.FillRectangle(Brushes.Transparent, bounds);
        using (context.PushClip(bounds))
        {
            var top = TextTop;
            Rect caret;
            if (_layout is { } layout)
            {
                layout.Draw(context, new Point(SidePadding, top));
                var end = layout.HitTestTextPosition(_text.Length);
                caret = new Rect(SidePadding + end.X + 2, top + end.Y + TextSize * 0.078, 2, TextSize * 1.08);
            }
            else
            {
                var width = _placeholderLayout?.WidthIncludingTrailingWhitespace ?? 0;
                var inner = bounds.Width - 2 * SidePadding;
                var start = SidePadding + Math.Max(0, (inner - (5 + width)) / 2);
                var lineTop = bounds.Height - LineHeight;
                _placeholderLayout?.Draw(context, new Point(start + 5, lineTop));
                caret = new Rect(start + 2, lineTop + TextSize * 0.078, 2, TextSize * 1.08);
            }
            if (_caretOn && CaretBrush is not null)
            {
                context.DrawRectangle(CaretBrush, null, new RoundedRect(caret, 1));
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusPen is not null)
        {
            context.DrawRectangle(null, FocusPen, bounds.Inflate(3));
        }
    }

    // --- caret -------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _blinking = true;
        StartBlink();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _blink?.Stop();
        _caretOn = true;
    }

    private void StartBlink()
    {
        if (!ArcMotion.IsFull(this) || VisualRoot is null)
        {
            _caretOn = true;
            return;
        }
        if (_blink is null)
        {
            _blink = new DispatcherTimer(DispatcherPriority.Render);
            _blink.Tick += OnBlink;
        }
        _blink.Interval = _blinking ? BlinkHalf : IdleDelay;
        _blink.Start();
    }

    private void OnBlink(object? sender, EventArgs e)
    {
        if (!ArcMotion.IsFull(this))
        {
            _blink?.Stop();
            _caretOn = true;
            InvalidateVisual();
            return;
        }
        if (!_blinking)
        {
            // Idle again: the blink starts from its visible half.
            _blinking = true;
            _blink!.Interval = BlinkHalf;
            return;
        }
        _caretOn = !_caretOn;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var max = Math.Max(0, ContentHeight - Bounds.Height);
        if (max <= 0)
        {
            return;
        }
        _scroll = Math.Clamp(_scroll + e.Delta.Y * LineHeight, 0, max);
        UpdateGlyphs(_owner.Clock);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(NavigationMethod.Pointer);
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

    protected override AutomationPeer OnCreateAutomationPeer() => new TextPeer(this);

    private sealed class TextPeer(KeycapKeyboardText owner) : ControlAutomationPeer(owner), IValueProvider
    {
        private KeycapKeyboardText Text => (KeycapKeyboardText)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : KeycapKeyboard.TextName;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsReadOnly => true;

        public string? Value => Text._text;

        public void SetValue(string? value)
        {
        }
    }
}
