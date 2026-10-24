// Ported from Arc's free otp-input block (MIT, Copyright (c) 2026 Elia Kuratli): the .glyph spans and the .ring.
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Draws, above the slots of an <see cref="OtpInput"/>, each slot's character and the one focus ring of the row.
/// A character that arrives rises 0.35em out of a 4px blur on the enter curve (240ms; a paste lands as a left to right
/// wave, 35ms per slot); one that leaves fades, shrinks to .9 and blurs 2px (120ms). The ring glides between slots on
/// Arc's snappy spring while focus stays in the row, and fades in (160ms) and scales up from .94 where focus lands from
/// outside. Code-driven: these are presences and a velocity-keeping glide, which a ControlTheme cannot express; and a
/// single drawn layer keeps the row to one visual instead of a control per character.
/// </summary>
internal sealed class OtpInputOverlay : Control
{
    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<OtpInputOverlay, IBrush?>(nameof(GlyphBrush));

    public static readonly StyledProperty<FontFamily> GlyphFontFamilyProperty =
        AvaloniaProperty.Register<OtpInputOverlay, FontFamily>(nameof(GlyphFontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> GlyphFontSizeProperty =
        AvaloniaProperty.Register<OtpInputOverlay, double>(nameof(GlyphFontSize), 18);

    public static readonly StyledProperty<double> GlyphOpacityProperty =
        AvaloniaProperty.Register<OtpInputOverlay, double>(nameof(GlyphOpacity), 1);

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<OtpInputOverlay, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<IBrush?> RingInvalidBrushProperty =
        AvaloniaProperty.Register<OtpInputOverlay, IBrush?>(nameof(RingInvalidBrush));

    public static readonly StyledProperty<IBrush?> RingSuccessBrushProperty =
        AvaloniaProperty.Register<OtpInputOverlay, IBrush?>(nameof(RingSuccessBrush));

    public static readonly StyledProperty<CornerRadius> RingRadiusProperty =
        AvaloniaProperty.Register<OtpInputOverlay, CornerRadius>(nameof(RingRadius), new CornerRadius(18));

    /// <summary>motion's default curve for a tween without an ease (fitted on the live ring: cubic-bezier(0, 0, .58, 1)).</summary>
    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>Arc's <c>spring.snappy</c> as the source writes it ({visualDuration .26, bounce 0}).</summary>
    internal static readonly SpringSpec Snappy = SpringSpec.FromVisual(0.26, 0);

    private const double RiseEm = 0.35;
    private const double BlurSoft = 4;
    private const double BlurSubtle = 2;
    private const double ExitScale = 0.9;
    private const double RingRestScale = 0.94;

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private sealed class Glyph
    {
        public required string Text { get; init; }
        public MotionValue<double>? Enter { get; set; }
        public MotionValue<double>? Exit { get; set; }
        public bool FadeOnly { get; set; }
    }

    private sealed class SlotGlyphs
    {
        public Glyph? Current { get; set; }
        public List<Glyph> Leaving { get; } = new();
    }

    private readonly List<SlotGlyphs> _slots = new();
    private readonly Dictionary<string, TextLayout> _layouts = new(StringComparer.Ordinal);
    private readonly SolidColorBrush _ringStroke = new(Colors.Transparent);
    private Pen? _ringPen;
    private MotionValue<double>? _ringX;
    private MotionValue<double>? _ringOpacity;
    private MotionValue<double>? _ringScale;
    private MotionValue<Color>? _ringColor;
    private double _ringWidth;
    private OtpInputRingTone _ringTone;
    private bool _ringShown;

    static OtpInputOverlay()
    {
        AffectsRender<OtpInputOverlay>(GlyphOpacityProperty, RingRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<OtpInputOverlay>(false);
        ClipToBoundsProperty.OverrideDefaultValue<OtpInputOverlay>(false);
    }

    /// <summary>The row whose slot boxes the overlay draws into.</summary>
    public OtpInputRow? Row { get; set; }

    /// <summary>Character color (<c>--foreground</c>).</summary>
    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    /// <summary>Character font (the body font).</summary>
    public FontFamily GlyphFontFamily { get => GetValue(GlyphFontFamilyProperty); set => SetValue(GlyphFontFamilyProperty, value); }

    /// <summary>Character size (<c>--text-lg</c>, 18).</summary>
    public double GlyphFontSize { get => GetValue(GlyphFontSizeProperty); set => SetValue(GlyphFontSizeProperty, value); }

    /// <summary>Opacity of every character (.48 while the field is disabled).</summary>
    public double GlyphOpacity { get => GetValue(GlyphOpacityProperty); set => SetValue(GlyphOpacityProperty, value); }

    /// <summary>Ring color (<c>--accent-strong</c>).</summary>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <summary>Ring color while the field is invalid (<c>--danger</c>).</summary>
    public IBrush? RingInvalidBrush { get => GetValue(RingInvalidBrushProperty); set => SetValue(RingInvalidBrushProperty, value); }

    /// <summary>Ring color in the success state (<c>--success</c>; ours).</summary>
    public IBrush? RingSuccessBrush { get => GetValue(RingSuccessBrushProperty); set => SetValue(RingSuccessBrushProperty, value); }

    /// <summary>Ring corner radius (<c>--radius-control</c>).</summary>
    public CornerRadius RingRadius { get => GetValue(RingRadiusProperty); set => SetValue(RingRadiusProperty, value); }

    /// <summary>True while the ring is shown (focus is in the row).</summary>
    internal bool IsRingShown => _ringShown;

    /// <summary>The ring's current left edge (row coordinates), opacity and scale, for tests.</summary>
    internal (double X, double Width, double Opacity, double Scale) RingState =>
        (_ringX?.Value ?? 0, _ringWidth, _ringOpacity?.Value ?? 0, _ringScale?.Value ?? RingRestScale);

    /// <summary>The ring's current color, for tests.</summary>
    internal Color RingColor => _ringColor?.Value ?? ToneColor(_ringTone);

    /// <summary>Each slot's shown character ("" for none) and its entry progress (1 = settled), for tests.</summary>
    internal IReadOnlyList<(string Text, double Progress)> GlyphStates =>
        _slots.Select(s => s.Current is { } g ? (g.Text, g.Enter?.Value ?? 1) : (string.Empty, 1.0)).ToList();

    /// <summary>How many leaving characters are still drawn, for tests.</summary>
    internal int LeavingCount => _slots.Sum(s => s.Leaving.Count);

    // ---- characters -------------------------------------------------------------------------------------------

    /// <summary>
    /// Shows <paramref name="characters"/> (one per slot, "" for an empty slot). A slot whose character changed lets the
    /// old one leave and the new one rise in; with <paramref name="wave"/> (several slots changed at once) the new ones
    /// wait 35ms per slot index. <paramref name="animate"/> false places everything at once (first layout, Arc's
    /// <c>initial={false}</c>).
    /// </summary>
    public void SetCharacters(IReadOnlyList<string> characters, bool wave, bool animate)
    {
        var level = animate && IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        while (_slots.Count > characters.Count)
        {
            var last = _slots[^1];
            DisposeSlot(last);
            _slots.RemoveAt(_slots.Count - 1);
        }
        while (_slots.Count < characters.Count)
        {
            _slots.Add(new SlotGlyphs());
        }
        for (var i = 0; i < characters.Count; i++)
        {
            var slot = _slots[i];
            var next = characters[i];
            var current = slot.Current?.Text ?? string.Empty;
            if (current == next)
            {
                continue;
            }
            if (slot.Current is { } old)
            {
                slot.Current = null;
                Leave(slot, old, level);
            }
            if (next.Length > 0)
            {
                slot.Current = Arrive(next, i, wave, level);
            }
        }
        InvalidateVisual();
    }

    private Glyph Arrive(string text, int index, bool wave, ArcMotionLevel level)
    {
        var glyph = new Glyph { Text = text };
        if (level == ArcMotionLevel.Off)
        {
            return glyph;
        }
        var enter = new MotionValue<double>(this, 0, MotionVectors.Double);
        enter.Changed += _ => InvalidateVisual();
        glyph.Enter = enter;
        if (level == ArcMotionLevel.Full)
        {
            var delay = wave ? TimeSpan.FromMilliseconds(ArcMotionPresets.StaggerItem.TotalMilliseconds * index) : TimeSpan.Zero;
            _ = enter.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
        }
        else
        {
            // Reduced: an instant fade only (the source's reduced branch has no rise, no blur and no stagger).
            glyph.FadeOnly = true;
            _ = enter.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, EaseOut));
        }
        return glyph;
    }

    private void Leave(SlotGlyphs slot, Glyph glyph, ArcMotionLevel level)
    {
        glyph.Enter?.Dispose();
        glyph.Enter = null;
        if (level != ArcMotionLevel.Full)
        {
            // Reduced and Off: the source removes a leaving character at once ({ duration: 0 }).
            return;
        }
        var exit = new MotionValue<double>(this, 0, MotionVectors.Double);
        exit.Changed += _ => InvalidateVisual();
        glyph.Exit = exit;
        slot.Leaving.Add(glyph);
        _ = Finish(slot, glyph, exit.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)));
    }

    private async Task Finish(SlotGlyphs slot, Glyph glyph, Task<bool> exit)
    {
        await exit;
        if (slot.Leaving.Remove(glyph))
        {
            glyph.Exit?.Dispose();
            glyph.Exit = null;
            InvalidateVisual();
        }
    }

    private static void DisposeSlot(SlotGlyphs slot)
    {
        slot.Current?.Enter?.Dispose();
        foreach (var glyph in slot.Leaving)
        {
            glyph.Exit?.Dispose();
        }
        slot.Leaving.Clear();
        slot.Current = null;
    }

    // ---- ring -------------------------------------------------------------------------------------------------

    private void EnsureRing()
    {
        if (_ringX is not null)
        {
            return;
        }
        _ringX = new MotionValue<double>(this, 0, MotionVectors.Double);
        _ringX.Changed += _ => InvalidateVisual();
        _ringOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _ringOpacity.Changed += _ => InvalidateVisual();
        _ringScale = new MotionValue<double>(this, RingRestScale, MotionVectors.Double);
        _ringScale.Changed += _ => InvalidateVisual();
        _ringColor = new MotionValue<Color>(this, ToneColor(_ringTone), MotionVectors.Color);
        _ringColor.Changed += c =>
        {
            _ringStroke.Color = c;
            InvalidateVisual();
        };
        _ringStroke.Color = _ringColor.Value;
    }

    /// <summary>
    /// Puts the ring on <paramref name="slot"/>. It glides there (snappy spring, velocity kept) when it is already shown
    /// and <paramref name="glide"/> is set; otherwise it appears there: no travel, a 160ms fade and a scale up from .94.
    /// </summary>
    public void ShowRing(Rect slot, bool glide)
    {
        EnsureRing();
        var full = IsLoaded && ArcMotion.IsFull(this);
        _ringWidth = slot.Width;
        if (glide && _ringShown && full)
        {
            _ = _ringX!.AnimateTo(slot.X, MotionSpec.Spring(Snappy));
        }
        else
        {
            _ringX!.Jump(slot.X);
        }
        _ringShown = true;
        if (full)
        {
            _ = _ringOpacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut));
            _ = _ringScale!.AnimateTo(1, MotionSpec.Spring(Snappy));
        }
        else
        {
            _ringOpacity!.Jump(1);
            _ringScale!.Jump(1);
        }
        InvalidateVisual();
    }

    /// <summary>Focus left the row: the ring fades out and shrinks back to .94 where it is.</summary>
    public void HideRing()
    {
        if (!_ringShown)
        {
            return;
        }
        _ringShown = false;
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _ringOpacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut));
            _ = _ringScale!.AnimateTo(RingRestScale, MotionSpec.Spring(Snappy));
        }
        else
        {
            _ringOpacity!.Jump(0);
            _ringScale!.Jump(RingRestScale);
        }
        InvalidateVisual();
    }

    /// <summary>The row was resized: keep the shown ring on its slot without travel (Arc's ResizeObserver).</summary>
    public void SnapRing(Rect slot)
    {
        if (!_ringShown || _ringX is null)
        {
            return;
        }
        if (Math.Abs(_ringX.Target - slot.X) < 0.01 && Math.Abs(_ringWidth - slot.Width) < 0.01)
        {
            return;
        }
        _ringX.Jump(slot.X);
        _ringWidth = slot.Width;
        InvalidateVisual();
    }

    /// <summary>The ring's color follows the field state over 160ms (instant without full motion: Arc's CSS turns the transition off).</summary>
    public void SetRingTone(OtpInputRingTone tone)
    {
        _ringTone = tone;
        if (_ringColor is null)
        {
            return;
        }
        var target = ToneColor(tone);
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _ringColor.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _ringColor.Jump(target);
        }
    }

    private Color ToneColor(OtpInputRingTone tone)
    {
        var brush = tone switch
        {
            OtpInputRingTone.Invalid => RingInvalidBrush,
            OtpInputRingTone.Success => RingSuccessBrush,
            _ => RingBrush
        };
        return brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
    }

    // ---- rendering --------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphBrushProperty || change.Property == GlyphFontFamilyProperty || change.Property == GlyphFontSizeProperty)
        {
            ClearLayouts();
            InvalidateVisual();
        }
        else if (change.Property == RingBrushProperty || change.Property == RingInvalidBrushProperty || change.Property == RingSuccessBrushProperty)
        {
            // A theme or accent change: the ring takes its new color at once.
            _ringColor?.Jump(ToneColor(_ringTone));
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearLayouts();
    }

    private void ClearLayouts()
    {
        foreach (var layout in _layouts.Values)
        {
            layout.Dispose();
        }
        _layouts.Clear();
    }

    private TextLayout LayoutOf(string text)
    {
        if (!_layouts.TryGetValue(text, out var layout))
        {
            layout = new TextLayout(text, new Typeface(GlyphFontFamily), GlyphFontSize, GlyphBrush ?? Brushes.Black,
                TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, textDecorations: null, FlowDirection.LeftToRight,
                double.PositiveInfinity, double.PositiveInfinity, double.NaN, 0, 1, s_tabular);
            _layouts[text] = layout;
        }
        return layout;
    }

    public override void Render(DrawingContext context)
    {
        if (Row is { } row)
        {
            for (var i = 0; i < _slots.Count && i < row.Children.Count; i++)
            {
                var box = row.SlotRect(i);
                var slot = _slots[i];
                foreach (var leaving in slot.Leaving)
                {
                    DrawLeaving(context, leaving, box);
                }
                if (slot.Current is { } glyph)
                {
                    DrawCurrent(context, glyph, box);
                }
            }
        }
        DrawRing(context);
    }

    private void DrawCurrent(DrawingContext context, Glyph glyph, Rect box)
    {
        var layout = LayoutOf(glyph.Text);
        var p = glyph.Enter?.Value ?? 1;
        var opacity = p * GlyphOpacity;
        if (opacity <= 0.001)
        {
            return;
        }
        var origin = new Point(box.Center.X - layout.WidthIncludingTrailingWhitespace / 2, box.Center.Y - layout.Height / 2);
        var dy = glyph.FadeOnly ? 0 : RiseEm * GlyphFontSize * (1 - p);
        var blur = glyph.FadeOnly ? 0 : BlurSoft * (1 - p);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(Matrix.CreateTranslation(0, dy)))
        using (PushBlur(context, blur, box))
        {
            layout.Draw(context, origin);
        }
    }

    private void DrawLeaving(DrawingContext context, Glyph glyph, Rect box)
    {
        var layout = LayoutOf(glyph.Text);
        var q = glyph.Exit?.Value ?? 1;
        var opacity = (1 - q) * GlyphOpacity;
        if (opacity <= 0.001)
        {
            return;
        }
        var origin = new Point(box.Center.X - layout.WidthIncludingTrailingWhitespace / 2, box.Center.Y - layout.Height / 2);
        var scale = 1 - (1 - ExitScale) * q;
        var center = box.Center;
        var transform = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(transform))
        using (PushBlur(context, BlurSubtle * q, box))
        {
            layout.Draw(context, origin);
        }
    }

    private void DrawRing(DrawingContext context)
    {
        if (_ringOpacity is null || _ringX is null || _ringScale is null || Row is null)
        {
            return;
        }
        var opacity = _ringOpacity.Value;
        if (opacity <= 0.001 || _ringWidth <= 0)
        {
            return;
        }
        var height = Row.Bounds.Height;
        var rect = new Rect(_ringX.Value, 0, _ringWidth, height);
        var scale = _ringScale.Value;
        var center = rect.Center;
        var transform = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
        _ringPen ??= new Pen(_ringStroke, 1);
        // A 1px border inside the box, like the CSS border of the ring; a radius past half the box clamps as CSS does.
        var inner = rect.Deflate(0.5);
        var radius = Math.Min(RingRadius.TopLeft, Math.Min(rect.Width, rect.Height) / 2) - 0.5;
        using (context.PushOpacity(opacity))
        using (context.PushTransform(transform))
        {
            context.DrawRectangle(null, _ringPen, inner, Math.Max(0, radius), Math.Max(0, radius));
        }
    }

    private static DrawingContext.PushedState? PushBlur(DrawingContext context, double radius, Rect bounds)
    {
        var effect = ArcBlur.Get(radius);
        return effect is null ? null : context.PushEffect(effect, bounds.Inflate(8));
    }
}

/// <summary>Which color the ring takes.</summary>
internal enum OtpInputRingTone
{
    Normal,
    Invalid,
    Success
}
