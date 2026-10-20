using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The centered value group of a <see cref="NumberField"/>: prefix, number and suffix travel as one group (Arc
/// <c>.value</c>). The number cell is the rolling digits, or the editor's text while a draft is open, so the value keeps
/// one position whether it rolls or is edited; the editor (caret and selection) overlays the cell and reaches 1em past it.
/// The group carries three offsets on one transform: the limit kick (y), the scrub follow (x) and the typing glide (x).
/// </summary>
/// <remarks>
/// Code-driven motion: the kick needs an initial velocity and the glide is measured from layout before and after a
/// change, neither of which a ControlTheme transition can express.
/// </remarks>
internal sealed class NumberFieldValueGroup : Panel
{
    /// <summary>Arc's kick: the morph spring written as stiffness and damping so it can carry a velocity.</summary>
    private static readonly SpringSpec s_kick = SpringSpec.FromVisual(0.42, 0.16);

    private readonly TranslateTransform _offset = new();
    private MotionValue<double>? _bump;
    private MotionValue<double>? _scrub;
    private MotionValue<double>? _shift;
    private double _contentLeft = double.NaN;
    private double? _shiftFrom;
    private bool _isEditing;

    public NumberFieldValueGroup()
    {
        RenderTransform = _offset;
    }

    /// <summary>While true the number cell is the editor's text and the digits are left out.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing != value)
            {
                _isEditing = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The current vertical kick offset (tests).</summary>
    internal double BumpOffset => _bump?.Value ?? 0;

    /// <summary>The current scrub follow offset (tests).</summary>
    internal double ScrubOffset => _scrub?.Value ?? 0;

    /// <summary>The current typing glide offset (tests).</summary>
    internal double ShiftOffset => _shift?.Value ?? 0;

    private NumberFieldAffixText? PrefixText => Children.Count > 0 ? Children[0] as NumberFieldAffixText : null;

    private RollingDigits? Digits => Children.Count > 1 ? Children[1] as RollingDigits : null;

    private TextPresenter? Editor => Children.Count > 2 ? Children[2] as TextPresenter : null;

    private NumberFieldAffixText? SuffixText => Children.Count > 3 ? Children[3] as NumberFieldAffixText : null;

    /// <summary>Strains the group toward a limit and lets it spring home (velocity in px/s, negative is up).</summary>
    public void Kick(double velocity)
    {
        _bump ??= Create();
        _ = _bump.AnimateTo(0, MotionSpec.Spring(s_kick), velocity);
    }

    /// <summary>Follows a scrub past a limit (already rubber-banded).</summary>
    public void SetScrub(double offset)
    {
        _scrub ??= Create();
        _scrub.Jump(offset);
    }

    /// <summary>Springs the scrub follow home (snappy).</summary>
    public void ReleaseScrub()
    {
        if (_scrub is null)
        {
            return;
        }
        _ = _scrub.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
    }

    /// <summary>Remembers where the group sits so the next layout can glide from there instead of hopping.</summary>
    public void CaptureShift()
    {
        if (_shiftFrom is not null || double.IsNaN(_contentLeft))
        {
            return;
        }
        _shiftFrom = _contentLeft;
        // A change that does not move the group leaves no stale capture for a later layout.
        Dispatcher.UIThread.Post(() => _shiftFrom = null, DispatcherPriority.Background);
    }

    private MotionValue<double> Create()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => ApplyOffset();
        return value;
    }

    private void ApplyOffset()
    {
        _offset.X = (_scrub?.Value ?? 0) + (_shift?.Value ?? 0);
        _offset.Y = _bump?.Value ?? 0;
    }

    private double NumberWidth()
    {
        if (_isEditing)
        {
            return Editor?.DesiredSize.Width ?? 0;
        }
        return Digits?.DesiredSize.Width ?? 0;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = new Size(double.PositiveInfinity, availableSize.Height);
        var height = 0.0;
        foreach (var c in Children)
        {
            c.Measure(child);
            height = Math.Max(height, c.DesiredSize.Height);
        }
        var width = (PrefixText?.DesiredSize.Width ?? 0) + NumberWidth() + (SuffixText?.DesiredSize.Width ?? 0);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var prefix = PrefixText?.DesiredSize.Width ?? 0;
        var number = NumberWidth();
        var suffix = SuffixText?.DesiredSize.Width ?? 0;
        var left = (finalSize.Width - prefix - number - suffix) / 2;
        if (_shiftFrom is { } from)
        {
            _shiftFrom = null;
            var delta = from - left;
            if (Math.Abs(delta) >= 0.5)
            {
                _shift ??= Create();
                var velocity = _shift.Velocity;
                _shift.Jump(_shift.Value + delta);
                _ = _shift.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph), velocity);
            }
        }
        _contentLeft = left;

        Place(PrefixText, left, finalSize.Height, prefix);
        Place(Digits, left + prefix, finalSize.Height, Digits?.DesiredSize.Width ?? 0);
        if (Editor is { } editor)
        {
            // The editor reaches 1em past the number so the caret at the end is never clipped.
            var em = editor.FontSize;
            Place(editor, left + prefix, finalSize.Height, Math.Max(number + em, editor.DesiredSize.Width));
        }
        Place(SuffixText, left + prefix + number, finalSize.Height, suffix);
        return finalSize;
    }

    private static void Place(Control? child, double x, double height, double width)
    {
        if (child is null)
        {
            return;
        }
        var h = child.DesiredSize.Height;
        child.Arrange(new Rect(x, (height - h) / 2, width, h));
    }
}

/// <summary>
/// Prefix or suffix copy of a <see cref="NumberField"/> (Arc <c>AffixText</c>). Characters are keyed by position, so
/// "seat" → "seats" only opens the new "s": a new character opens its width on the morph spring while it scales up from
/// .6, fades in and rises in the direction of change (snappy); a leaving one closes on the smooth spring, drops its
/// opacity at once and lifts away. Reduced motion fades new characters in and drops leaving ones at once. Drawn in
/// <see cref="Render"/>; per-character blur is not drawn (DrawingContext has no blur). Code-driven: characters enter and
/// leave per key, which a ControlTheme transition on fixed properties cannot express.
/// </summary>
internal sealed class NumberFieldAffixText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<NumberFieldAffixText, string?>(nameof(Text));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<NumberFieldAffixText>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<NumberFieldAffixText>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<NumberFieldAffixText>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<NumberFieldAffixText>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Direction of the last value change: 1 up, -1 down (new characters rise from below when it grew).</summary>
    public int Direction { get; set; } = 1;

    private sealed class Entry
    {
        public required string Key { get; init; }
        public required string Glyph { get; init; }
        public MotionValue<double>? Size { get; set; }
        public MotionValue<double>? Fade { get; set; }
        public MotionValue<double>? Rise { get; set; }
        public bool Leaving { get; set; }
    }

    private readonly List<Entry> _entries = new();
    private readonly Dictionary<string, FormattedText> _glyphs = new(StringComparer.Ordinal);
    private double _lineHeight = double.NaN;

    static NumberFieldAffixText()
    {
        AffectsMeasure<NumberFieldAffixText>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<NumberFieldAffixText>(ForegroundProperty);
    }

    /// <summary>The characters currently laid out, leaving ones included (tests).</summary>
    internal int EntryCount => _entries.Count;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Rebuild();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            _glyphs.Clear();
            _lineHeight = double.NaN;
        }
    }

    private void Rebuild()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && level != ArcMotionLevel.Off;
        var full = animate && level == ArcMotionLevel.Full;
        var direction = Direction >= 0 ? 1 : -1;

        var wanted = new List<(string Key, string Glyph)>();
        var index = 0;
        foreach (var rune in (Text ?? string.Empty).EnumerateRunes())
        {
            var glyph = rune.ToString();
            wanted.Add(($"{index}:{glyph}", glyph));
            index++;
        }

        var present = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            if (!entry.Leaving)
            {
                present[entry.Key] = entry;
            }
        }
        var next = new List<Entry>(wanted.Count + 2);
        foreach (var (key, glyph) in wanted)
        {
            if (present.Remove(key, out var kept))
            {
                next.Add(kept);
                continue;
            }
            var entry = new Entry { Key = key, Glyph = glyph };
            if (animate)
            {
                entry.Size = Create(full ? 0 : 1, measure: true);
                entry.Fade = Create(0, measure: false);
                entry.Rise = Create(full ? direction * 0.3 : 0, measure: false);
                if (full)
                {
                    _ = entry.Size.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
                    _ = entry.Fade.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
                    _ = entry.Rise.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
                }
                else
                {
                    _ = entry.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
                }
            }
            next.Add(entry);
        }
        // Leaving characters keep their place while they close (full motion only).
        for (var i = 0; i < _entries.Count; i++)
        {
            var gone = _entries[i];
            if (gone.Leaving)
            {
                next.Insert(Math.Min(i, next.Count), gone);
                continue;
            }
            if (!present.ContainsKey(gone.Key))
            {
                continue;
            }
            if (!full)
            {
                gone.Size?.Dispose();
                continue;
            }
            gone.Leaving = true;
            gone.Size ??= Create(1, measure: true);
            gone.Fade ??= Create(1, measure: false);
            gone.Rise ??= Create(0, measure: false);
            _ = gone.Fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            _ = gone.Rise.AnimateTo(-direction * 0.3, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            var leaving = gone;
            _ = gone.Size.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(_ =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (leaving.Leaving && _entries.Remove(leaving))
                    {
                        InvalidateMeasure();
                        InvalidateVisual();
                    }
                }), TaskScheduler.Default);
            next.Insert(Math.Min(i, next.Count), gone);
        }
        _entries.Clear();
        _entries.AddRange(next);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private MotionValue<double> Create(double initial, bool measure)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        if (measure)
        {
            value.Changed += _ => InvalidateMeasure();
        }
        else
        {
            value.Changed += _ => InvalidateVisual();
        }
        return value;
    }

    private FormattedText Glyph(string text)
    {
        if (!_glyphs.TryGetValue(text, out var glyph))
        {
            glyph = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black);
            _glyphs[text] = glyph;
        }
        return glyph;
    }

    private double LineHeight()
    {
        if (double.IsNaN(_lineHeight))
        {
            _lineHeight = Glyph("0").Height;
        }
        return _lineHeight;
    }

    private double SlotWidth(Entry entry) => Glyph(entry.Glyph).WidthIncludingTrailingWhitespace * Math.Max(0, entry.Size?.Value ?? 1);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        foreach (var entry in _entries)
        {
            width += SlotWidth(entry);
        }
        return new Size(width, _entries.Count == 0 ? 0 : LineHeight());
    }

    public override void Render(DrawingContext context)
    {
        var height = LineHeight();
        var x = 0.0;
        foreach (var entry in _entries)
        {
            var width = SlotWidth(entry);
            if (width <= 0.01)
            {
                continue;
            }
            var size = entry.Size?.Value ?? 1;
            var scale = 0.6 + 0.4 * size;
            var opacity = Math.Clamp(entry.Fade?.Value ?? 1, 0, 1);
            var rise = (entry.Rise?.Value ?? 0) * FontSize;
            var centre = new Point(x + width / 2, height / 2);
            var transform = Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateScale(scale, scale) *
                            Matrix.CreateTranslation(centre.X, centre.Y + rise);
            using (context.PushTransform(transform))
            using (context.PushClip(new Rect(x, -height, width, height * 3)))
            using (context.PushOpacity(opacity))
            {
                context.DrawText(Glyph(entry.Glyph), new Point(x, 0));
            }
            x += width;
        }
    }
}

/// <summary>
/// Lucide Minus / Plus for a <see cref="NumberField"/> stepper (16px, stroke 1.75 in viewBox units). While pressed the
/// glyph sinks to .82 in 90ms and comes back on the CSS spring; a refused press shakes it once (x keyframes
/// 0, −2.5, 2.5, −1.5, 1, 0 over 0.32s, ease-out per segment). Full motion only. Code-driven: the press and release use
/// different timings on one property and the shake is a keyframe run fired by an event, not a state.
/// </summary>
internal sealed class NumberFieldStepGlyph : Control
{
    public static readonly StyledProperty<bool> IsPlusProperty =
        AvaloniaProperty.Register<NumberFieldStepGlyph, bool>(nameof(IsPlus));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<NumberFieldStepGlyph, double>(nameof(GlyphSize), 16);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<NumberFieldStepGlyph, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<NumberFieldStepGlyph>();

    private static readonly double[] s_shake = [0, -2.5, 2.5, -1.5, 1, 0];
    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly TimeSpan s_pressDuration = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan s_shakeDuration = TimeSpan.FromMilliseconds(320);

    public bool IsPlus { get => GetValue(IsPlusProperty); set => SetValue(IsPlusProperty, value); }
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private MotionValue<double>? _scale;
    private MotionValue<double>? _shake;
    private Pen? _pen;
    private bool _isPressedVisual;

    static NumberFieldStepGlyph()
    {
        AffectsMeasure<NumberFieldStepGlyph>(GlyphSizeProperty);
        AffectsRender<NumberFieldStepGlyph>(IsPlusProperty, StrokeThicknessProperty, ForegroundProperty);
    }

    /// <summary>Current press scale (tests).</summary>
    internal double PressScale => _scale?.Value ?? 1;

    /// <summary>Shakes played so far (tests).</summary>
    internal int ShakeCount { get; private set; }

    /// <summary>Sinks the glyph while its stepper is pressed or held.</summary>
    public bool IsPressedVisual
    {
        get => _isPressedVisual;
        set
        {
            if (_isPressedVisual == value)
            {
                return;
            }
            _isPressedVisual = value;
            _scale ??= Create(1);
            if (!ArcMotion.IsFull(this))
            {
                _scale.Jump(1);
            }
            else if (value)
            {
                _ = _scale.AnimateTo(0.82, MotionSpec.Tween(s_pressDuration, ArcEasings.Standard));
            }
            else
            {
                _ = _scale.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
            }
        }
    }

    /// <summary>One refusal shake (full motion only).</summary>
    public void Shake()
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        ShakeCount++;
        _shake ??= Create(1);
        _shake.Jump(0);
        _ = _shake.AnimateTo(1, MotionSpec.Tween(s_shakeDuration, new LinearEasing()));
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == StrokeThicknessProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(GlyphSize, GlyphSize);

    private double ShakeOffset()
    {
        var p = Math.Clamp(_shake?.Value ?? 1, 0, 1);
        if (p >= 1)
        {
            return 0;
        }
        var position = p * (s_shake.Length - 1);
        var segment = Math.Min(s_shake.Length - 2, (int)position);
        var local = s_easeOut.Ease(position - segment);
        return s_shake[segment] + (s_shake[segment + 1] - s_shake[segment]) * local;
    }

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        _pen ??= new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var size = GlyphSize;
        var unit = size / 24;
        var scale = _scale?.Value ?? 1;
        var origin = new Point((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        var centre = new Point(origin.X + size / 2, origin.Y + size / 2);
        var transform = Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(origin.X, origin.Y) *
                        Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateScale(scale, scale) *
                        Matrix.CreateTranslation(centre.X + ShakeOffset(), centre.Y);
        using (context.PushTransform(transform))
        {
            context.DrawLine(_pen, new Point(5, 12), new Point(19, 12));
            if (IsPlus)
            {
                context.DrawLine(_pen, new Point(12, 5), new Point(12, 19));
            }
        }
    }
}

/// <summary>
/// The limit note beside a <see cref="NumberField"/> label ("Max 10 seats"). It rises in from the side it guards
/// (+0.45em for a maximum, −0.45em for a minimum) out of a subtle blur, y on the snappy spring and opacity and blur on
/// the fast duration, and fades out in place (standard duration and ease). Reduced motion only fades (instant).
/// Code-driven: the entry offset depends on the guarded edge and the exit is a different motion that ends by hiding.
/// </summary>
internal sealed class NumberFieldLimitNote : TextBlock
{
    private readonly TranslateTransform _rise = new();
    private BlurEffect? _blurEffect;
    private MotionValue<double>? _y;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _blur;
    private bool _shown;
    private bool _hiding;

    public NumberFieldLimitNote()
    {
        RenderTransform = _rise;
        IsVisible = false;
    }

    /// <summary>True while the note is shown or entering (not while it fades out).</summary>
    internal bool IsShown => _shown && !_hiding;

    public void Show(string text, int edge)
    {
        Text = text;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_shown && !_hiding)
        {
            return;
        }
        Ensure();
        IsVisible = true;
        var wasHiding = _hiding;
        _shown = true;
        _hiding = false;
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _y!.Jump(0);
            _blur!.Jump(0);
            _opacity!.Jump(1);
            return;
        }
        var fade = level == ArcMotionLevel.Full ? ArcMotionPresets.Fast : ArcMotionPresets.Instant;
        if (!wasHiding)
        {
            _opacity!.Jump(0);
            if (level == ArcMotionLevel.Full)
            {
                _y!.Jump(edge * 0.45 * FontSize);
                _blur!.Jump(2);
                _ = _y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
                _ = _blur.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
        }
        _ = _opacity!.AnimateTo(1, MotionSpec.Tween(fade, ArcEasings.Standard));
    }

    public void Hide()
    {
        if (!_shown || _hiding)
        {
            return;
        }
        _hiding = true;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off || _opacity is null)
        {
            Finish();
            return;
        }
        var duration = level == ArcMotionLevel.Full ? ArcMotionPresets.Standard : ArcMotionPresets.Instant;
        _opacity.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Standard)).ContinueWith(_ =>
            Dispatcher.UIThread.Post(() =>
            {
                if (_hiding)
                {
                    Finish();
                }
            }), TaskScheduler.Default);
    }

    private void Finish()
    {
        _shown = false;
        _hiding = false;
        _opacity?.Jump(0);
        IsVisible = false;
    }

    private void Ensure()
    {
        if (_y is not null)
        {
            return;
        }
        _y = new MotionValue<double>(this, 0, MotionVectors.Double);
        _y.Changed += v => _rise.Y = v;
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        _blur = new MotionValue<double>(this, 0, MotionVectors.Double);
        _blur.Changed += v =>
        {
            if (v <= 0.01)
            {
                Effect = null;
                return;
            }
            _blurEffect ??= new BlurEffect();
            _blurEffect.Radius = v;
            Effect = _blurEffect;
        };
    }
}
