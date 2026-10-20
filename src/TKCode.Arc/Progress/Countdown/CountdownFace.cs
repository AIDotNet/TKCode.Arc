using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The running face of a <see cref="Countdown"/>, drawn directly: every digit is a clipped wheel whose window feathers
/// at the top and bottom, turning the short way round (00 → 59 is one step down on a six-digit tens wheel) with a blur
/// that grows with its offset (P14, P29). The first values roll in from zero on a smooth spring, staggered per digit
/// (P18); each later second turns on the snappy spring. Large shows unit names under the digits and colons between
/// groups; compact shows one line with a quiet unit letter after each group.
/// </summary>
internal sealed class CountdownFace : Control
{
    public static readonly StyledProperty<CountdownVariant> VariantProperty =
        AvaloniaProperty.Register<CountdownFace, CountdownVariant>(nameof(Variant));

    public static readonly StyledProperty<FontFamily> DigitFontFamilyProperty =
        AvaloniaProperty.Register<CountdownFace, FontFamily>(nameof(DigitFontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> DigitFontSizeProperty =
        AvaloniaProperty.Register<CountdownFace, double>(nameof(DigitFontSize), 40);

    public static readonly StyledProperty<FontWeight> DigitFontWeightProperty =
        AvaloniaProperty.Register<CountdownFace, FontWeight>(nameof(DigitFontWeight), FontWeight.Medium);

    public static readonly StyledProperty<FontFamily> NameFontFamilyProperty =
        AvaloniaProperty.Register<CountdownFace, FontFamily>(nameof(NameFontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> NameFontSizeProperty =
        AvaloniaProperty.Register<CountdownFace, double>(nameof(NameFontSize), 12);

    public static readonly StyledProperty<FontWeight> NameFontWeightProperty =
        AvaloniaProperty.Register<CountdownFace, FontWeight>(nameof(NameFontWeight), FontWeight.Medium);

    /// <summary>Digit letter spacing in em (display tracking on the large variant).</summary>
    public static readonly StyledProperty<double> TrackingProperty =
        AvaloniaProperty.Register<CountdownFace, double>(nameof(Tracking));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<CountdownFace, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<CountdownFace, IBrush?>(nameof(MutedForeground));

    public CountdownVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public FontFamily DigitFontFamily { get => GetValue(DigitFontFamilyProperty); set => SetValue(DigitFontFamilyProperty, value); }
    public double DigitFontSize { get => GetValue(DigitFontSizeProperty); set => SetValue(DigitFontSizeProperty, value); }
    public FontWeight DigitFontWeight { get => GetValue(DigitFontWeightProperty); set => SetValue(DigitFontWeightProperty, value); }
    public FontFamily NameFontFamily { get => GetValue(NameFontFamilyProperty); set => SetValue(NameFontFamilyProperty, value); }
    public double NameFontSize { get => GetValue(NameFontSizeProperty); set => SetValue(NameFontSizeProperty, value); }
    public FontWeight NameFontWeight { get => GetValue(NameFontWeightProperty); set => SetValue(NameFontWeightProperty, value); }
    public double Tracking { get => GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    private sealed class Wheel
    {
        public required string Key { get; init; }
        public int Base { get; set; } = 10;
        public int Digit { get; set; }
        public double Target { get; set; }
        public bool Revealed { get; set; }
        public MotionValue<double>? Position { get; set; }
        public MotionValue<double>? Presence { get; set; }
    }

    private sealed class Group
    {
        public required CountdownUnits Unit { get; init; }
        public List<Wheel> Wheels { get; } = new();
        public FormattedText? Name { get; set; }
        public double Width { get; set; }
    }

    private readonly List<Group> _groups = new();
    private readonly Dictionary<string, Wheel> _wheels = new(StringComparer.Ordinal);
    private readonly FormattedText?[] _digits = new FormattedText?[10];
    private FormattedText? _colon;
    private double _cell;
    private double _availableWidth = double.PositiveInfinity;
    private IBrush? _feather;
    private double _featherHeight;
    private MotionValue<double>? _pending;

    static CountdownFace()
    {
        AffectsMeasure<CountdownFace>(VariantProperty, DigitFontFamilyProperty, DigitFontSizeProperty, DigitFontWeightProperty,
            NameFontFamilyProperty, NameFontSizeProperty, NameFontWeightProperty, TrackingProperty);
        AffectsRender<CountdownFace>(ForegroundProperty, MutedForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CountdownFace>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == MutedForegroundProperty ||
            change.Property == DigitFontFamilyProperty || change.Property == DigitFontSizeProperty ||
            change.Property == DigitFontWeightProperty || change.Property == NameFontFamilyProperty ||
            change.Property == NameFontSizeProperty || change.Property == NameFontWeightProperty ||
            change.Property == TrackingProperty || change.Property == VariantProperty)
        {
            Array.Clear(_digits);
            _colon = null;
            _feather = null;
            foreach (var group in _groups)
            {
                group.Name = null;
            }
        }
    }

    /// <summary>Dims the digits to 0.3 until the clock is first read; the dim clears over the considered duration.</summary>
    public void SetPending(bool pending, bool animate)
    {
        var target = pending ? 0.3 : 1;
        _pending ??= CreateValue(target, measure: false);
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _pending.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
        }
        else
        {
            _pending.Jump(target);
        }
    }

    internal double DigitOpacity => _pending?.Value ?? 1;

    /// <summary>Digits currently targeted, left to right (tests and accessibility checks).</summary>
    internal string DisplayedDigits => string.Concat(_groups.SelectMany(g => g.Wheels).Select(w => w.Digit.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Shows new values. The first values (and new wheels) roll in from zero; later changes turn the short way.</summary>
    public void SetParts(IReadOnlyList<CountdownPart> parts, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var full = animate && IsLoaded && level == ArcMotionLevel.Full;
        var structureChanged = parts.Count != _groups.Count || parts.Where((p, i) => _groups[i].Unit != p.Unit).Any();
        if (structureChanged)
        {
            _groups.Clear();
            foreach (var part in parts)
            {
                _groups.Add(new Group { Unit = part.Unit });
            }
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var g = 0; g < parts.Count; g++)
        {
            var part = parts[g];
            var group = _groups[g];
            var text = Math.Max(0, part.Value).ToString("00", CultureInfo.InvariantCulture);
            var wheels = new List<Wheel>(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var key = $"{part.Unit}{text.Length - i}";
                seen.Add(key);
                var digit = text[i] - '0';
                var baseSize = CountdownTime.BaseFor(part.Unit, g == 0, text.Length, i);
                if (!_wheels.TryGetValue(key, out var wheel))
                {
                    wheel = new Wheel { Key = key, Base = baseSize };
                    wheel.Position = CreateValue(0, measure: false);
                    wheel.Presence = CreateValue(1, measure: false);
                    _wheels[key] = wheel;
                    if (full && group.Wheels.Count > 0)
                    {
                        // A column that appears later (a group gains a digit) fades in.
                        wheel.Presence.Jump(0);
                        _ = wheel.Presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
                    }
                }
                wheel.Base = baseSize;
                Turn(wheel, digit, Math.Min((g * 2 + i) * ArcMotionPresets.StaggerItem.TotalSeconds, 0.3), full, level);
                wheels.Add(wheel);
            }
            if (group.Wheels.Count != wheels.Count)
            {
                InvalidateMeasure();
            }
            group.Wheels.Clear();
            group.Wheels.AddRange(wheels);
        }
        foreach (var key in _wheels.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _wheels.Remove(key);
        }
        if (structureChanged)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private void Turn(Wheel wheel, int digit, double revealDelay, bool full, ArcMotionLevel level)
    {
        if (wheel.Digit == digit)
        {
            wheel.Revealed = true;
            return;
        }
        wheel.Target += CountdownTime.ShortestTurn(wheel.Digit, digit, wheel.Base);
        wheel.Digit = digit;
        var first = !wheel.Revealed;
        wheel.Revealed = true;
        if (!full || level != ArcMotionLevel.Full)
        {
            wheel.Position?.Jump(wheel.Target);
            return;
        }
        _ = wheel.Position?.AnimateTo(wheel.Target, first
            ? MotionSpec.Spring(ArcSpring.Smooth, TimeSpan.FromSeconds(revealDelay))
            : MotionSpec.Spring(ArcSpring.Snappy));
    }

    private MotionValue<double> CreateValue(double initial, bool measure)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            if (measure)
            {
                InvalidateMeasure();
            }
            InvalidateVisual();
        };
        return value;
    }

    private FormattedText Digit(int d)
    {
        if (_digits[d] is { } cached)
        {
            return cached;
        }
        var text = new FormattedText(d.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(DigitFontFamily, FontStyle.Normal, DigitFontWeight), DigitFontSize, Foreground ?? Brushes.Black);
        _digits[d] = text;
        return text;
    }

    private FormattedText Colon() => _colon ??= new FormattedText(":", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(DigitFontFamily, FontStyle.Normal, DigitFontWeight), DigitFontSize, MutedForeground ?? Brushes.Gray);

    private FormattedText NameOf(Group group, bool compact) => group.Name ??= new FormattedText(
        CountdownTime.Name(group.Unit, compact), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(compact ? DigitFontFamily : NameFontFamily, FontStyle.Normal, NameFontWeight), NameFontSize,
        MutedForeground ?? Brushes.Gray);

    private bool Compact => Variant == CountdownVariant.Compact;

    private double LineHeight => Compact ? DigitFontSize * 1.3 : DigitFontSize;

    private double ColonWidth => Colon().WidthIncludingTrailingWhitespace + 0.24 * DigitFontSize;

    protected override Size MeasureOverride(Size availableSize)
    {
        _availableWidth = availableSize.Width;
        var tracking = Tracking * DigitFontSize;
        _cell = Enumerable.Range(0, 10).Max(d => Digit(d).WidthIncludingTrailingWhitespace) + tracking;
        var compact = Compact;
        var width = 0.0;
        for (var g = 0; g < _groups.Count; g++)
        {
            var group = _groups[g];
            var digits = group.Wheels.Count * _cell;
            var name = NameOf(group, compact).WidthIncludingTrailingWhitespace;
            if (compact)
            {
                group.Width = digits + 1 + name;
                width += group.Width + (g > 0 ? 6 : 0);
            }
            else
            {
                // On a narrow card the names outgrow the digits; an even column floor keeps the colons evenly spaced.
                var container = double.IsInfinity(availableSize.Width) ? 4 * 60.0 + 1.6 * DigitFontSize : availableSize.Width;
                var floor = Math.Min((container - 1.6 * DigitFontSize) / 4, NameFontSize * 4.2);
                group.Width = Math.Max(Math.Max(digits, name), Math.Max(0, floor));
                width += group.Width + (g > 0 ? ColonWidth : 0);
            }
        }
        var height = compact ? LineHeight : DigitFontSize + 6 + NameFontSize;
        return new Size(Math.Ceiling(width), Math.Ceiling(height));
    }

    public override void Render(DrawingContext context)
    {
        var compact = Compact;
        var size = DigitFontSize;
        var x = 0.0;
        var digitTop = compact ? (LineHeight - size) / 2 : 0;
        for (var g = 0; g < _groups.Count; g++)
        {
            var group = _groups[g];
            if (g > 0)
            {
                if (compact)
                {
                    x += 6;
                }
                else
                {
                    var colon = Colon();
                    using (context.PushOpacity(0.6))
                    {
                        context.DrawText(colon, new Point(x + 0.12 * size, digitTop + (size - colon.Height) / 2 - 0.06 * size));
                    }
                    x += ColonWidth;
                }
            }
            var digitsWidth = group.Wheels.Count * _cell;
            var name = NameOf(group, compact);
            var digitsX = compact ? x : x + (group.Width - digitsWidth) / 2;
            for (var i = 0; i < group.Wheels.Count; i++)
            {
                DrawWheel(context, group.Wheels[i], new Rect(digitsX + i * _cell, digitTop, _cell, size));
            }
            if (compact)
            {
                var digitBaseline = digitTop + (size - Digit(0).Height) / 2 + Digit(0).Baseline;
                context.DrawText(name, new Point(digitsX + digitsWidth + 1, digitBaseline - name.Baseline));
            }
            else
            {
                context.DrawText(name, new Point(x + (group.Width - name.WidthIncludingTrailingWhitespace) / 2,
                    size + 6 + (NameFontSize - name.Height) / 2));
            }
            x += group.Width;
        }
    }

    private void DrawWheel(DrawingContext context, Wheel wheel, Rect cell)
    {
        var size = DigitFontSize;
        var feather = 0.16 * size;
        var window = new Rect(cell.X, cell.Y - feather, cell.Width, cell.Height + 2 * feather);
        if (_feather is null || Math.Abs(_featherHeight - window.Height) > 0.01)
        {
            _featherHeight = window.Height;
            var edge = Math.Clamp(1.5 * feather / window.Height, 0, 0.5);
            _feather = new ImmutableLinearGradientBrush(
                [
                    new ImmutableGradientStop(0, Colors.Transparent), new ImmutableGradientStop(edge, Colors.Black),
                    new ImmutableGradientStop(1 - edge, Colors.Black), new ImmutableGradientStop(1, Colors.Transparent)
                ],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        }
        var position = wheel.Position?.Value ?? wheel.Digit;
        var presence = Math.Clamp(wheel.Presence?.Value ?? 1, 0, 1) * Math.Clamp(DigitOpacity, 0, 1);
        var baseSize = wheel.Base;
        using (context.PushOpacity(presence))
        using (context.PushClip(window))
        using (context.PushOpacityMask(_feather, window))
        {
            for (var d = 0; d < baseSize; d++)
            {
                var offset = (((d - position) % baseSize + baseSize * 1.5) % baseSize) - baseSize / 2.0;
                var abs = Math.Abs(offset);
                if (abs >= 1)
                {
                    continue;
                }
                var glyph = Digit(d);
                var origin = new Point(cell.X + (cell.Width - glyph.WidthIncludingTrailingWhitespace) / 2,
                    cell.Y + (size - glyph.Height) / 2 + offset * size);
                if (abs < 0.02)
                {
                    context.DrawText(glyph, origin);
                    continue;
                }
                using (context.PushOpacity(1 - abs))
                {
                    var effect = ProgressBlur.Get(abs * 2);
                    using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, cell.Translate(new Vector(0, offset * size))))
                    {
                        context.DrawText(glyph, origin);
                    }
                }
            }
        }
    }
}

/// <summary>
/// The live mark of a finished <see cref="Countdown"/>: a success dot with a ring that swells to 2.6× and fades every
/// 2s (P21). It pulses only while attached, visible and at full motion.
/// </summary>
internal sealed class CountdownPulse : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<CountdownPulse, IBrush?>(nameof(Fill));

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    private readonly ProgressLoop _loop;
    private double _phase;

    static CountdownPulse()
    {
        AffectsRender<CountdownPulse>(FillProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CountdownPulse>(false);
    }

    public CountdownPulse()
    {
        ClipToBounds = false;
        _loop = new ProgressLoop(this, dt =>
        {
            _phase = (_phase + dt / 2.0) % 1;
            InvalidateVisual();
            return true;
        });
    }

    internal bool IsPulsing => _loop.IsRunning;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty || change.Property == ArcMotion.LevelProperty)
        {
            UpdateLoop();
        }
    }

    internal void UpdateLoop()
    {
        if (IsEffectivelyVisible && TopLevel.GetTopLevel(this) is not null && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var fill = Fill;
        if (fill is null)
        {
            return;
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = Math.Min(Bounds.Width, Bounds.Height) / 2;
        context.DrawEllipse(fill, null, center, r, r);
        if (!_loop.IsRunning)
        {
            return;
        }
        // Keyframes: from { opacity .5; scale 1 } 70%, to { opacity 0; scale 2.6 } on the standard curve.
        var p = Math.Min(1, _phase / 0.7);
        var eased = ArcEasings.Standard.Ease(p);
        var opacity = 0.5 * (1 - eased);
        if (opacity <= 0.001)
        {
            return;
        }
        var scale = 1 + 1.6 * eased;
        using (context.PushOpacity(opacity))
        {
            context.DrawEllipse(fill, null, center, r * scale, r * scale);
        }
    }
}
