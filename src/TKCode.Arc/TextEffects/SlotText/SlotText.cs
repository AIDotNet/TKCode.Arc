// Ported from Arc Slot text (MIT, Copyright (c) 2026 Elia Kuratli): the reel strips, their seeded shuffle, the keying of
// reels from the aligned end, and every motion constant.
using System.Buffers;
using System.Globalization;
using System.Text;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Which end of a <see cref="SlotText"/> its reels are matched from when the length changes.</summary>
public enum SlotTextAlign
{
    /// <summary>Reels are matched from the first character; new ones grow on the right.</summary>
    Start,

    /// <summary>Reels are matched from the last character; new ones grow on the left, so 999 to 1,000 keeps the ones.</summary>
    End
}

/// <summary>
/// Arc slot text: text and numbers that spin into their new value like slot machine reels. Every character is a reel;
/// digits count through the wheel in the direction the number moved, letters shuffle through the alphabet, and reels stop
/// one after another from left to right with a soft landing that never passes the target. Drawn in one control: a reel is
/// a few numbers and cached glyphs, so a change runs no layout unless a width changes.
/// </summary>
public class SlotText : Control
{
    /// <summary>The value to show. A number goes through <c>Format</c>; anything else shows as text. A value written in XAML is text, so bind a number or set it from code to get grouping.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<SlotText, object?>(nameof(Value));

    /// <summary>Formats a number. Null uses en-US grouping with up to three decimals, so 12480 reads 12,480.</summary>
    public static readonly StyledProperty<Func<double, string>?> FormatProperty =
        AvaloniaProperty.Register<SlotText, Func<double, string>?>(nameof(Format));

    /// <summary>How long the first reel spins. Later reels add <c>Stagger</c> each.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<SlotText, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(0.9));

    /// <summary>The time between reels stopping, left to right.</summary>
    public static readonly StyledProperty<TimeSpan> StaggerProperty =
        AvaloniaProperty.Register<SlotText, TimeSpan>(nameof(Stagger), TimeSpan.FromSeconds(0.07));

    /// <summary>Extra full turns a digit makes before it lands. 0 rolls straight to the new digit.</summary>
    public static readonly StyledProperty<int> SpinsProperty =
        AvaloniaProperty.Register<SlotText, int>(nameof(Spins), 1);

    /// <summary>Which end reels are matched from when the length changes. Null uses End for numbers and Start for text.</summary>
    public static readonly StyledProperty<SlotTextAlign?> AlignProperty =
        AvaloniaProperty.Register<SlotText, SlotTextAlign?>(nameof(Align));

    /// <summary>Announces new values politely to screen readers.</summary>
    public static readonly StyledProperty<bool> AnnounceProperty =
        AvaloniaProperty.Register<SlotText, bool>(nameof(Announce));

    /// <summary>The font of the reels.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<SlotText>();

    /// <summary>The font size of the reels; their geometry scales with it.</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SlotText>();

    /// <summary>The font weight of the reels.</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<SlotText>();

    /// <summary>The color of the text.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SlotText>();

    internal static readonly StyledProperty<double> LineHeightEmProperty =
        AvaloniaProperty.Register<SlotText, double>(nameof(LineHeightEm), 1.15);

    internal static readonly StyledProperty<double> CellPitchEmProperty =
        AvaloniaProperty.Register<SlotText, double>(nameof(CellPitchEm), 1.6);

    internal static readonly StyledProperty<double> WindowInsetYEmProperty =
        AvaloniaProperty.Register<SlotText, double>(nameof(WindowInsetYEm), 0.22);

    internal static readonly StyledProperty<double> WindowInsetXEmProperty =
        AvaloniaProperty.Register<SlotText, double>(nameof(WindowInsetXEm), 0.08);

    internal static readonly StyledProperty<double> EdgeFadeEmProperty =
        AvaloniaProperty.Register<SlotText, double>(nameof(EdgeFadeEm), 0.24);

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public Func<double, string>? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="StaggerProperty"/>
    public TimeSpan Stagger { get => GetValue(StaggerProperty); set => SetValue(StaggerProperty, value); }

    /// <inheritdoc cref="SpinsProperty"/>
    public int Spins { get => GetValue(SpinsProperty); set => SetValue(SpinsProperty, value); }

    /// <inheritdoc cref="AlignProperty"/>
    public SlotTextAlign? Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <inheritdoc cref="AnnounceProperty"/>
    public bool Announce { get => GetValue(AnnounceProperty); set => SetValue(AnnounceProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    internal double LineHeightEm { get => GetValue(LineHeightEmProperty); set => SetValue(LineHeightEmProperty, value); }
    internal double CellPitchEm { get => GetValue(CellPitchEmProperty); set => SetValue(CellPitchEmProperty, value); }
    internal double WindowInsetYEm { get => GetValue(WindowInsetYEmProperty); set => SetValue(WindowInsetYEmProperty, value); }
    internal double WindowInsetXEm { get => GetValue(WindowInsetXEmProperty); set => SetValue(WindowInsetXEmProperty, value); }
    internal double EdgeFadeEm { get => GetValue(EdgeFadeEmProperty); set => SetValue(EdgeFadeEmProperty, value); }

    /// <summary>The text shown now, after formatting.</summary>
    public string Text => _text;

    private const string Digits = "0123456789";
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string NoBreakSpace = " ";

    /// <summary>Fast out, long soft landing, never past the target.</summary>
    private static readonly Easing s_landing = new SplineEasing(0.12, 0.8, 0.16, 1);

    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    /// <summary>A reel's identity across values: a leading symbol by position, the rest from the aligned end.</summary>
    private readonly record struct ReelKey(char Kind, int Index);

    private sealed class Reel
    {
        public required ReelKey Key { get; init; }
        public required string Char { get; set; }
        public required string[] Strip { get; set; }
        public MotionValue<double>? Position { get; set; }
        public MotionValue<double>? Width { get; set; }
        public bool Exiting { get; set; }
        public bool Matched { get; set; }
    }

    private List<Reel> _reels = new();
    private List<Reel> _nextReels = new();
    private readonly HashSet<ReelKey> _initialKeys = new();
    private readonly Dictionary<string, TextLayout> _glyphs = new(StringComparer.Ordinal);
    private ImmutableLinearGradientBrush? _mask;
    private string _text = string.Empty;
    private object? _lastValue;
    private bool _rising = true;
    private bool _built;

    static SlotText()
    {
        AffectsMeasure<SlotText>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightEmProperty);
        AffectsRender<SlotText>(ForegroundProperty, CellPitchEmProperty, WindowInsetXEmProperty, WindowInsetYEmProperty, EdgeFadeEmProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SlotText>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == FormatProperty || change.Property == AlignProperty)
        {
            Rebuild();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            ClearGlyphs();
        }
        else if (change.Property == WindowInsetYEmProperty || change.Property == EdgeFadeEmProperty || change.Property == LineHeightEmProperty)
        {
            _mask = null;
        }
        else if (change.Property == AnnounceProperty)
        {
            AutomationProperties.SetLiveSetting(this, Announce ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_built)
        {
            Rebuild();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearGlyphs();
    }

    private void ClearGlyphs()
    {
        foreach (var glyph in _glyphs.Values)
        {
            glyph.Dispose();
        }
        _glyphs.Clear();
    }

    private static bool IsNumber(object? value) =>
        value is double or float or decimal or int or long or short or byte or uint or ulong or ushort or sbyte;

    private string TextOf(object? value)
    {
        if (IsNumber(value))
        {
            var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return Format is { } format ? format(number) : number.ToString("#,##0.###", s_enUs);
        }
        return value as string ?? value?.ToString() ?? string.Empty;
    }

    /// <summary>Whole characters: a surrogate pair or a letter with its marks stays on one reel.</summary>
    private static List<string> CharactersOf(string text)
    {
        var characters = new List<string>(text.Length);
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            characters.Add(elements.GetTextElement());
        }
        return characters;
    }

    private static bool IsLetterOrNumber(string character)
        => Rune.DecodeFromUtf16(character, out var rune, out _) == OperationStatus.Done && (Rune.IsLetter(rune) || Rune.IsNumber(rune));

    private void Rebuild()
    {
        var value = Value;
        var text = TextOf(value);
        if (!Equals(value, _lastValue))
        {
            _rising = !IsNumber(value) || !IsNumber(_lastValue) ||
                      Convert.ToDouble(value, CultureInfo.InvariantCulture) >= Convert.ToDouble(_lastValue, CultureInfo.InvariantCulture);
            _lastValue = value;
        }
        _text = text;
        AutomationProperties.SetName(this, text);

        var animate = IsLoaded && ArcMotion.IsFull(this);
        // Until the control is on screen every value counts as the first one: nothing enters, nothing spins.
        var initial = !_built || !IsLoaded;
        if (initial)
        {
            _initialKeys.Clear();
        }
        _built = true;

        var characters = CharactersOf(text);
        var fromEnd = (Align ?? (IsNumber(value) ? SlotTextAlign.End : SlotTextAlign.Start)) == SlotTextAlign.End;
        // A leading run of symbols (a currency sign, a plus) is keyed from the start and never spins into a digit; the rest is
        // keyed from the aligned end, so separators and suffixes stay on their own reels when the number grows.
        var prefix = 0;
        if (fromEnd)
        {
            while (prefix < characters.Count && !IsLetterOrNumber(characters[prefix]))
            {
                prefix++;
            }
        }

        var reels = _reels;
        foreach (var reel in reels)
        {
            reel.Matched = false;
        }
        var next = _nextReels;
        next.Clear();
        for (var order = 0; order < characters.Count; order++)
        {
            var key = order < prefix
                ? new ReelKey('p', order)
                : fromEnd ? new ReelKey('e', characters.Count - 1 - order) : new ReelKey('s', order);
            if (initial)
            {
                _initialKeys.Add(key);
            }
            var reel = Find(reels, key);
            if (reel is null)
            {
                reel = CreateReel(key, characters[order], order, animate);
            }
            else
            {
                Retarget(reel, characters[order], order, animate);
            }
            reel.Matched = true;
            next.Add(reel);
        }
        // Reels with no character left shrink away where they stood, then are removed.
        for (var index = 0; animate && index < reels.Count; index++)
        {
            var gone = reels[index];
            if (gone.Matched)
            {
                continue;
            }
            next.Insert(Math.Min(index, next.Count), gone);
            if (!gone.Exiting)
            {
                gone.Exiting = true;
                gone.Width ??= WidthValue(GlyphWidth(gone.Char));
                _ = RemoveWhenGone(gone, gone.Width.AnimateTo(0, MotionSpec.Spring(SpringSpec.FromVisual(0.35, 0))));
            }
        }
        _nextReels = reels;
        _reels = next;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static Reel? Find(List<Reel> reels, ReelKey key)
    {
        // A value has a few dozen characters at most: a scan beats building a dictionary per change.
        foreach (var reel in reels)
        {
            if (!reel.Matched && reel.Key == key)
            {
                return reel;
            }
        }
        return null;
    }

    private async Task RemoveWhenGone(Reel reel, Task<bool> shrink)
    {
        // False means the character came back and retargeted the width.
        if (await shrink && reel.Exiting)
        {
            _reels.Remove(reel);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private TimeSpan SpinTime(int order) => Duration + order * Stagger;

    private Reel CreateReel(ReelKey key, string character, int order, bool animate)
    {
        if (!animate || _initialKeys.Contains(key))
        {
            return new Reel { Key = key, Char = character, Strip = [character] };
        }
        var strip = BuildStrip(string.Empty, character, Math.Max(0, Spins), _rising, character[0] + order);
        var reel = new Reel { Key = key, Char = character, Strip = strip };
        Spin(reel, 0, order);
        // A new reel grows from nothing.
        reel.Width = WidthValue(0);
        _ = reel.Width.AnimateTo(GlyphWidth(character), WidthSpec(order));
        return reel;
    }

    /// <summary>A new target restarts the reel from the cell on screen now, keeping its sub-cell offset, so nothing jumps.</summary>
    private void Retarget(Reel reel, string character, int order, bool animate)
    {
        var wasExiting = reel.Exiting;
        reel.Exiting = false;
        if (reel.Char == character)
        {
            if (wasExiting && reel.Width is { } back)
            {
                _ = back.AnimateTo(GlyphWidth(character), WidthSpec(order));
            }
            return;
        }
        if (!animate)
        {
            reel.Char = character;
            reel.Strip = [character];
            reel.Position?.Jump(0);
            reel.Width?.Jump(GlyphWidth(character));
            return;
        }
        var oldWidth = SlotWidth(reel);
        var current = reel.Position?.Value ?? 0;
        var index = Math.Clamp((int)Math.Round(current, MidpointRounding.AwayFromZero), 0, reel.Strip.Length - 1);
        var visible = reel.Strip[index];
        // Arc's seed: a blank first cell gives NaN there, which its generator turns into 1.
        var seed = visible.Length == 0 ? 1 : character[0] * 31 + visible[0] * 7 + order + reel.Strip.Length;
        reel.Char = character;
        reel.Strip = BuildStrip(visible, character, Math.Max(0, Spins), _rising, seed);
        Spin(reel, current - index, order);
        var width = GlyphWidth(character);
        if (Math.Abs(width - oldWidth) > 0.01 || reel.Width is { IsAnimating: true })
        {
            if (reel.Width is { } motion)
            {
                if (!motion.IsAnimating)
                {
                    motion.Jump(oldWidth);
                }
            }
            else
            {
                reel.Width = WidthValue(oldWidth);
            }
            _ = reel.Width.AnimateTo(width, WidthSpec(order));
        }
    }

    private void Spin(Reel reel, double from, int order)
    {
        var last = reel.Strip.Length - 1;
        reel.Position ??= PositionValue();
        reel.Position.Jump(from);
        _ = reel.Position.AnimateTo(last, MotionSpec.Tween(last == 0 ? TimeSpan.FromSeconds(0.2) : SpinTime(order), s_landing));
    }

    private MotionSpec WidthSpec(int order)
        => MotionSpec.Spring(SpringSpec.FromVisual(Math.Min(SpinTime(order).TotalSeconds, 0.6), 0));

    private MotionValue<double> PositionValue()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private MotionValue<double> WidthValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private static string? ClassOf(string character)
    {
        if (character.Length == 0)
        {
            // Arc's includes("") is true, so a blank cell counts as a digit: a new digit reel rolls up from 0.
            return Digits;
        }
        if (character.Length != 1)
        {
            return null;
        }
        return Digits.Contains(character[0]) ? Digits : Lower.Contains(character[0]) ? Lower : Upper.Contains(character[0]) ? Upper : null;
    }

    /// <summary>The reel's cells, from what is visible now to the target.</summary>
    internal static string[] BuildStrip(string from, string to, int spins, bool rising, int seed)
    {
        if (from == to)
        {
            return [to];
        }
        // The target chooses the set; a blank is only ever a starting cell.
        var set = to.Length == 0 ? null : ClassOf(to);
        if (set is null)
        {
            return [from, to];
        }
        if (ReferenceEquals(set, Digits) && ReferenceEquals(ClassOf(from), Digits))
        {
            var a = from.Length == 0 ? 0 : from[0] - '0';
            var b = to[0] - '0';
            var steps = spins * 10 + (rising ? (b - a + 10) % 10 : (a - b + 10) % 10);
            var digits = new string[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                digits[i] = Digits[(((a + (rising ? i : -i)) % 10) + 10) % 10].ToString();
            }
            return digits;
        }
        // Deterministic shuffle, so the same change always spins the same letters.
        var state = unchecked((uint)seed);
        if (state == 0)
        {
            state = 1;
        }
        var count = Math.Max(2, spins * 5 + 2);
        var strip = new string[count + 2];
        strip[0] = from;
        for (var i = 0; i < count; i++)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            strip[i + 1] = set[(int)(state / 4294967296.0 * set.Length)].ToString();
        }
        strip[count + 1] = to;
        return strip;
    }

    private TextLayout Glyph(string cell)
    {
        var key = cell == " " ? NoBreakSpace : cell;
        if (!_glyphs.TryGetValue(key, out var glyph))
        {
            glyph = new TextLayout(key, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black,
                TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, textDecorations: null, FlowDirection.LeftToRight,
                double.PositiveInfinity, double.PositiveInfinity, double.NaN, 0, 1, s_tabular);
            _glyphs[key] = glyph;
        }
        return glyph;
    }

    private double GlyphWidth(string cell) => cell.Length == 0 ? 0 : Glyph(cell).WidthIncludingTrailingWhitespace;

    private double SlotWidth(Reel reel)
    {
        // A width at rest follows the font, so a font change needs no bookkeeping here.
        if (reel.Width is { } width && (width.IsAnimating || reel.Exiting))
        {
            return Math.Max(0, width.Value);
        }
        return GlyphWidth(reel.Char);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        foreach (var reel in _reels)
        {
            width += SlotWidth(reel);
        }
        return new Size(width, LineHeightEm * FontSize);
    }

    private ImmutableLinearGradientBrush Mask()
    {
        if (_mask is null)
        {
            var edge = Math.Clamp(EdgeFadeEm / (LineHeightEm + 2 * WindowInsetYEm), 0, 0.5);
            _mask = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb(0, 0, 0, 0)),
                new ImmutableGradientStop(edge, Colors.Black),
                new ImmutableGradientStop(1 - edge, Colors.Black),
                new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))
            ], startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        }
        return _mask;
    }

    /// <summary>The motion blur of a reel at <paramref name="velocity"/> cells per second (Arc: 0.05 px per cell/s, at most 2.4 px).</summary>
    internal static double BlurFor(double velocity)
    {
        var amount = Math.Min(Math.Abs(velocity) * 0.05, 2.4);
        return amount < 0.15 ? 0 : amount;
    }

    public override void Render(DrawingContext context)
    {
        var em = FontSize;
        var line = LineHeightEm * em;
        var pitch = CellPitchEm * em;
        var insetX = WindowInsetXEm * em;
        var insetY = WindowInsetYEm * em;
        var x = 0.0;
        foreach (var reel in _reels)
        {
            var width = SlotWidth(reel);
            if (width > 0.01)
            {
                var window = new Rect(x - insetX, -insetY, width + 2 * insetX, line + 2 * insetY);
                using (context.PushClip(window))
                {
                    if (reel.Position is { IsAnimating: true } moving)
                    {
                        // The fade and the blur exist only while a reel moves: at rest the glyph sits clear of the fade,
                        // and a control full of settled reels should not pay for a layer each.
                        var effect = ArcBlur.Get(BlurFor(moving.Velocity));
                        using (context.PushOpacityMask(Mask(), window))
                        using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, window))
                        {
                            DrawCells(context, reel, moving.Value, x, width, line, pitch, insetY);
                        }
                    }
                    else
                    {
                        DrawCells(context, reel, reel.Position?.Value ?? 0, x, width, line, pitch, insetY);
                    }
                }
            }
            x += width;
        }
    }

    private void DrawCells(DrawingContext context, Reel reel, double position, double x, double width, double line, double pitch, double insetY)
    {
        var low = Math.Max(0, (int)Math.Floor(position) - 1);
        var high = Math.Min(reel.Strip.Length - 1, (int)Math.Ceiling(position) + 1);
        for (var index = low; index <= high; index++)
        {
            var cell = reel.Strip[index];
            if (cell.Length == 0)
            {
                continue;
            }
            var glyph = Glyph(cell);
            var top = (index - position) * pitch + (line - glyph.Height) / 2;
            if (top + glyph.Height < -insetY || top > line + insetY)
            {
                continue;
            }
            glyph.Draw(context, new Point(x + (width - glyph.WidthIncludingTrailingWhitespace) / 2, top));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SlotTextAutomationPeer(this);

    private sealed class SlotTextAutomationPeer : ControlAutomationPeer
    {
        public SlotTextAutomationPeer(SlotText owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }

    /// <summary>The characters of the reels shown now, exiting ones included (tests).</summary>
    internal IReadOnlyList<string> ReelCharacters => _reels.Select(reel => reel.Char).ToList();

    /// <summary>The cells of a reel (tests).</summary>
    internal IReadOnlyList<string> ReelStrip(int index) => _reels[index].Strip;

    /// <summary>Where a reel is now, in cells (tests).</summary>
    internal double ReelPosition(int index) => _reels[index].Position?.Value ?? 0;

    /// <summary>Whether a reel is spinning (tests).</summary>
    internal bool ReelIsSpinning(int index) => _reels[index].Position is { IsAnimating: true };

    /// <summary>The width a reel has now (tests).</summary>
    internal double ReelWidth(int index) => SlotWidth(_reels[index]);
}
