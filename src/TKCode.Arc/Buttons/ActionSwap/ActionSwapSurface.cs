using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Which brush the icon of an <see cref="ActionSwap"/> face takes: the text color or a status color.</summary>
internal enum ActionSwapTint
{
    Text,
    Success,
    Warning,
    Danger
}

/// <summary>
/// The content of an <see cref="ActionSwap"/> pill, drawn directly: a leading icon slot and a row of letters that move
/// together. Icons trade places through a blur (the old one shrinks away while the new one grows out of it on a spring,
/// a success tick wipes in from its leading edge, P15), kept letters glide while changed ones rise out of a soft blur
/// (P4), and the row's width, the icon slot and the padding (fold) follow on springs (P5). The pill that wraps it clips
/// what runs ahead of the width.
/// </summary>
internal sealed class ActionSwapSurface : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ActionSwapSurface>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ActionSwapSurface>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ActionSwapSurface>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ActionSwapSurface>();

    /// <summary>The pill's inline padding; Left is used on both sides.</summary>
    public static readonly StyledProperty<Thickness> InsetProperty =
        AvaloniaProperty.Register<ActionSwapSurface, Thickness>(nameof(Inset));

    /// <summary>The pill's height; a folded pill pads down to a circle of this size.</summary>
    public static readonly StyledProperty<double> ControlHeightProperty =
        AvaloniaProperty.Register<ActionSwapSurface, double>(nameof(ControlHeight), 44);

    public static readonly StyledProperty<double> IconExtentProperty =
        AvaloniaProperty.Register<ActionSwapSurface, double>(nameof(IconExtent), 16);

    public static readonly StyledProperty<double> LabelGapProperty =
        AvaloniaProperty.Register<ActionSwapSurface, double>(nameof(LabelGap), 8);

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<ActionSwapSurface, IBrush?>(nameof(SuccessBrush));

    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<ActionSwapSurface, IBrush?>(nameof(WarningBrush));

    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<ActionSwapSurface, IBrush?>(nameof(DangerBrush));

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Thickness Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }
    public double ControlHeight { get => GetValue(ControlHeightProperty); set => SetValue(ControlHeightProperty, value); }
    public double IconExtent { get => GetValue(IconExtentProperty); set => SetValue(IconExtentProperty, value); }
    public double LabelGap { get => GetValue(LabelGapProperty); set => SetValue(LabelGapProperty, value); }
    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    /// <summary>Blur of a glyph or icon that is not yet (or no longer) at rest (Arc <c>blur.soft</c>).</summary>
    private const double BlurRadius = 4;

    private const double SpinnerExtent = 14;
    private const double SpinnerThickness = 1.5;
    private const double SpinnerTurnSeconds = 0.75;

    // Folded pill: the circle around the spinner is 18px of content.
    private const double FoldContent = 18;

    private static readonly SpringSpec WidthSpring = ArcMotionPresets.Morph;
    private static readonly SpringSpec IconSlotSpring = ArcMotionPresets.Smooth;
    private static readonly SpringSpec IconPop = ArcMotionPresets.Snappy;
    private static readonly SpringSpec SuccessPop = SpringSpec.FromVisual(0.45, 0);
    private static readonly TimeSpan GlyphEnter = ArcMotionPresets.Standard;
    private static readonly TimeSpan Leave = ArcMotionPresets.Fast;
    private static readonly TimeSpan Fade = ArcMotionPresets.Instant;

    // The open quarter-ring of the spinner: its top border, centered on the origin.
    private static readonly StreamGeometry SpinnerArc = CreateSpinnerArc();

    private sealed class Glyph
    {
        public required string Char { get; init; }
        public FormattedText? Text { get; set; }
        public double Width { get; set; }
        public double TargetX { get; set; }
        public MotionValue<double>? X { get; set; }
        public MotionValue<double>? Enter { get; set; }
        public MotionValue<double>? Exit { get; set; }
        public bool Leaving { get; set; }
    }

    private sealed class Phase
    {
        public required string Key { get; init; }
        public Geometry? Data { get; init; }
        public bool Spinner { get; init; }
        public ActionSwapTint Tint { get; init; }
        public double Stroke { get; init; } = 2;
        public required MotionValue<double> Scale { get; init; }
        public required MotionValue<double> Opacity { get; init; }
        public required MotionValue<double> Blur { get; init; }
        public required MotionValue<double> Wipe { get; init; }
        public bool Leaving { get; set; }
        public Pen? Pen { get; set; }
    }

    private readonly List<Glyph> _glyphs = new();
    private readonly List<Phase> _phases = new();
    private readonly LiveLoop _spin;
    private MotionValue<double>? _textWidth;
    private MotionValue<double>? _iconWidth;
    private MotionValue<double>? _fold;
    private string _text = string.Empty;
    private bool _gap;
    private string? _iconKey;
    private double _lineHeight;
    private double _angle;
    private bool _fontDirty = true;
    private int _sequence;
    private Pen? _arcPen;
    private Pen? _trackPen;
    private Color _penColor;

    static ActionSwapSurface()
    {
        AffectsMeasure<ActionSwapSurface>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, InsetProperty, ControlHeightProperty,
            IconExtentProperty, LabelGapProperty);
        AffectsRender<ActionSwapSurface>(ForegroundProperty, SuccessBrushProperty, WarningBrushProperty, DangerBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ActionSwapSurface>(false);
    }

    public ActionSwapSurface()
    {
        _spin = new LiveLoop(this, Spin);
    }

    /// <summary>Whether a spinner is showing or leaving (test and diagnostics).</summary>
    internal bool IsSpinning => _spin.IsRunning;

    /// <summary>The text of the live letters.</summary>
    internal string Text => _text;

    /// <summary>The key of the icon face being shown, or null without an icon.</summary>
    internal string? IconKey => _iconKey;

    /// <summary>The icon slot's current width, 0 (no icon) to <see cref="IconExtent"/>.</summary>
    internal double IconSlotWidth => _iconWidth?.Value ?? 0;

    /// <summary>The label row's current width.</summary>
    internal double TextWidth => _textWidth?.Value ?? 0;

    /// <summary>How folded the pill is, 0 (full padding) to 1 (a circle around the icon).</summary>
    internal double FoldValue => _fold?.Value ?? 0;

    /// <summary>How many letters are leaving or entering (diagnostics).</summary>
    internal int LeavingGlyphCount => _glyphs.Count(g => g.Leaving);

    /// <summary>How many icon faces exist, including those still leaving.</summary>
    internal int IconFaceCount => _phases.Count;

    private ArcMotionLevel Level => IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    private bool Animates => Level != ArcMotionLevel.Off;

    private bool Full => Level == ArcMotionLevel.Full;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == FontWeightProperty)
        {
            _fontDirty = true;
        }
        else if (change.Property == ForegroundProperty)
        {
            // Color only: re-brush the shaped letters, metrics stay.
            var brush = Foreground ?? Brushes.Black;
            foreach (var glyph in _glyphs)
            {
                glyph.Text?.SetForegroundBrush(brush);
            }
        }
    }

    #region Show

    /// <summary>
    /// Moves the face to a new label and icon. A changed <paramref name="iconKey"/> swaps the icon; shared leading and
    /// trailing letters of the label keep their identity, so only the changed run of text is replaced.
    /// </summary>
    public void Show(string label, string? iconKey, Geometry? icon, bool spinner, ActionSwapTint tint, bool draw, double iconStroke = 2)
    {
        var hasIcon = icon is not null || spinner;
        ShowIcon(hasIcon ? iconKey : null, icon, spinner, tint, draw, iconStroke);
        ShowText(label, hasIcon && label.Length > 0);
    }

    /// <summary>Eases the padding down to a circle around the icon (folded) or back to the pill's padding.</summary>
    public void SetFolded(bool folded)
    {
        var target = folded ? 1 : 0;
        if (_fold is null)
        {
            _fold = new MotionValue<double>(this, target, MotionVectors.Double);
            _fold.Changed += _ => InvalidateMeasure();
            return;
        }
        if (Math.Abs(_fold.Target - target) < 0.001)
        {
            return;
        }
        if (Full)
        {
            _ = _fold.AnimateTo(target, MotionSpec.Spring(WidthSpring));
        }
        else
        {
            _fold.Jump(target);
            InvalidateMeasure();
        }
    }

    private MotionValue<double> NewValue(double initial, Action? changed = null)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            changed?.Invoke();
            InvalidateVisual();
        };
        return value;
    }

    private void ShowIcon(string? key, Geometry? icon, bool spinner, ActionSwapTint tint, bool draw, double stroke)
    {
        var width = key is null ? 0 : IconExtent;
        if (_iconWidth is null)
        {
            _iconWidth = new MotionValue<double>(this, width, MotionVectors.Double);
            _iconWidth.Changed += _ => InvalidateMeasure();
        }
        else if (Math.Abs(_iconWidth.Target - width) > 0.001)
        {
            if (Full)
            {
                _ = _iconWidth.AnimateTo(width, MotionSpec.Spring(IconSlotSpring));
            }
            else
            {
                _iconWidth.Jump(width);
                InvalidateMeasure();
            }
        }

        if (key == _iconKey)
        {
            return;
        }
        _iconKey = key;
        foreach (var current in _phases.Where(p => !p.Leaving).ToList())
        {
            Retire(current);
        }
        if (key is not null)
        {
            Enter(key, icon, spinner, tint, draw, stroke);
        }
        UpdateSpin();
        InvalidateVisual();
    }

    private void Enter(string key, Geometry? icon, bool spinner, ActionSwapTint tint, bool draw, double stroke)
    {
        var animate = Animates;
        var full = Full;
        var wipe = draw && full;
        var phase = new Phase
        {
            Key = key,
            Data = icon,
            Spinner = spinner,
            Tint = tint,
            Stroke = stroke,
            Scale = NewValue(1),
            Opacity = NewValue(1),
            Blur = NewValue(0),
            Wipe = NewValue(1)
        };
        _phases.Add(phase);
        if (!animate)
        {
            return;
        }
        phase.Opacity.Jump(0);
        if (!full)
        {
            // Reduced: a short fade, no scale, blur or wipe.
            _ = phase.Opacity.AnimateTo(1, MotionSpec.Tween(Fade, ArcEasings.Standard));
            return;
        }
        phase.Scale.Jump(0.5);
        phase.Blur.Jump(BlurRadius);
        _ = phase.Scale.AnimateTo(1, MotionSpec.Spring(wipe ? SuccessPop : IconPop));
        _ = phase.Opacity.AnimateTo(1, MotionSpec.Tween(GlyphEnter, ArcEasings.Enter));
        _ = phase.Blur.AnimateTo(0, MotionSpec.Tween(GlyphEnter, ArcEasings.Enter));
        if (wipe)
        {
            // The tick's stroke wipes in from the leading edge while the icon settles.
            phase.Wipe.Jump(0);
            _ = phase.Wipe.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(360), ArcEasings.Standard, TimeSpan.FromMilliseconds(20)));
        }
    }

    private void Retire(Phase phase)
    {
        phase.Leaving = true;
        if (!Animates)
        {
            RemovePhase(phase);
            return;
        }
        Task<bool> done;
        if (Full)
        {
            _ = phase.Scale.AnimateTo(0.5, MotionSpec.Tween(Leave, ArcEasings.Standard));
            _ = phase.Blur.AnimateTo(BlurRadius, MotionSpec.Tween(Leave, ArcEasings.Standard));
            done = phase.Opacity.AnimateTo(0, MotionSpec.Tween(Leave, ArcEasings.Standard));
        }
        else
        {
            done = phase.Opacity.AnimateTo(0, MotionSpec.Tween(Fade, ArcEasings.Standard));
        }
        _ = RemoveWhen(done, phase);
    }

    private async Task RemoveWhen(Task<bool> done, Phase phase)
    {
        await done;
        RemovePhase(phase);
    }

    private void RemovePhase(Phase phase)
    {
        if (_phases.Remove(phase))
        {
            phase.Scale.Dispose();
            phase.Opacity.Dispose();
            phase.Blur.Dispose();
            phase.Wipe.Dispose();
            UpdateSpin();
            InvalidateVisual();
        }
    }

    private void UpdateSpin()
    {
        if (_phases.Any(p => p.Spinner))
        {
            _spin.Start();
        }
        else
        {
            _spin.Stop();
        }
    }

    private bool Spin(double seconds)
    {
        _angle = (_angle + seconds / SpinnerTurnSeconds * 360) % 360;
        InvalidateVisual();
        return true;
    }

    private static List<string> Split(string text)
    {
        var result = new List<string>(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            result.Add(rune.ToString());
        }
        return result;
    }

    private void ShowText(string text, bool gap)
    {
        if (text == _text && gap == _gap)
        {
            return;
        }
        if (text == _text)
        {
            // Only the gap to the icon changed: the letters glide to their new positions.
            _gap = gap;
            Relayout(Animates && Full);
            InvalidateMeasure();
            return;
        }
        var previous = Split(_text);
        var next = Split(text);
        var live = _glyphs.Where(g => !g.Leaving).ToList();
        var animate = Animates;
        var full = Full;

        // Shared leading and trailing runs keep their identity; a run shorter than two letters is not worth keeping.
        int start = 0, end = 0;
        while (start < previous.Count && start < next.Count && previous[start] == next[start])
        {
            start++;
        }
        while (end < previous.Count - start && end < next.Count - start && previous[^(end + 1)] == next[^(end + 1)])
        {
            end++;
        }
        if (start < 2)
        {
            start = 0;
        }
        if (end < 2)
        {
            end = 0;
        }
        _sequence++;
        // Letters that grow out of a folded pill wait a beat, so they arrive as the shape opens.
        var lead = previous.Count == 0 ? 0.1 : 0;
        var kept = new List<Glyph>(live.Take(start));
        for (var i = start; i < next.Count - end; i++)
        {
            var glyph = new Glyph { Char = next[i] };
            glyph.Text = Shape(glyph.Char);
            glyph.Width = glyph.Text.WidthIncludingTrailingWhitespace;
            glyph.Enter = NewValue(1);
            if (animate)
            {
                glyph.Enter.Jump(0);
                var order = i - start;
                var delay = lead + Math.Min(order * ArcMotionPresets.StaggerChar.TotalSeconds, 0.12);
                _ = glyph.Enter.AnimateTo(1, full
                    ? MotionSpec.Tween(GlyphEnter, ArcEasings.Enter, TimeSpan.FromSeconds(delay))
                    : MotionSpec.Tween(Fade, ArcEasings.Standard));
            }
            kept.Add(glyph);
        }
        kept.AddRange(live.Skip(live.Count - end));

        var leaving = _glyphs.Where(g => g.Leaving).ToList();
        foreach (var gone in live.Where(g => !kept.Contains(g)))
        {
            if (Retire(gone, animate, full))
            {
                leaving.Add(gone);
            }
        }
        _glyphs.Clear();
        _glyphs.AddRange(kept);
        _glyphs.AddRange(leaving);

        _text = text;
        _gap = gap;
        Relayout(animate && full);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Takes a letter out of flow, where it stays and fades away; false when it is gone at once.</summary>
    private bool Retire(Glyph glyph, bool animate, bool full)
    {
        glyph.Leaving = true;
        glyph.X?.Stop();
        if (!animate)
        {
            glyph.X?.Dispose();
            glyph.Enter?.Dispose();
            return false;
        }
        glyph.Exit = NewValue(0);
        var done = glyph.Exit.AnimateTo(1, MotionSpec.Tween(full ? Leave : Fade, ArcEasings.Standard));
        _ = RemoveGlyphWhen(done, glyph);
        return true;
    }

    private async Task RemoveGlyphWhen(Task<bool> done, Glyph glyph)
    {
        await done;
        if (_glyphs.Remove(glyph))
        {
            glyph.X?.Dispose();
            glyph.Enter?.Dispose();
            glyph.Exit?.Dispose();
            InvalidateVisual();
        }
    }

    private void Relayout(bool animate)
    {
        var x = _gap ? LabelGap : 0;
        foreach (var glyph in _glyphs.Where(g => !g.Leaving))
        {
            glyph.TargetX = x;
            if (glyph.X is null)
            {
                glyph.X = NewValue(x);
            }
            else if (animate)
            {
                _ = glyph.X.AnimateTo(x, MotionSpec.Spring(WidthSpring));
            }
            else
            {
                glyph.X.Jump(x);
            }
            x += glyph.Width;
        }
        var total = _glyphs.Any(g => !g.Leaving) ? x : 0;
        if (_textWidth is null)
        {
            _textWidth = new MotionValue<double>(this, total, MotionVectors.Double);
            _textWidth.Changed += _ => InvalidateMeasure();
        }
        else if (animate)
        {
            _ = _textWidth.AnimateTo(total, MotionSpec.Spring(WidthSpring));
        }
        else
        {
            _textWidth.Jump(total);
        }
    }

    #endregion

    #region Layout and render

    private FormattedText Shape(string ch) => new(ch, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black);

    private void EnsureFont()
    {
        if (!_fontDirty)
        {
            return;
        }
        _fontDirty = false;
        foreach (var glyph in _glyphs)
        {
            glyph.Text = Shape(glyph.Char);
            glyph.Width = glyph.Text.WidthIncludingTrailingWhitespace;
        }
        _lineHeight = Shape("Hg").Height;
        Relayout(animate: false);
    }

    private double PaddingX()
    {
        var pad = Inset.Left;
        var fold = Math.Clamp(_fold?.Value ?? 0, -0.5, 1.5);
        return pad + ((ControlHeight - FoldContent) / 2 - pad) * fold;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureFont();
        if (_lineHeight <= 0)
        {
            _lineHeight = Shape("Hg").Height;
        }
        // The spring can dip below zero as it settles on a fold; a negative width would pop the label back.
        var text = Math.Max(0, _textWidth?.Value ?? 0);
        var icon = Math.Max(0, _iconWidth?.Value ?? 0);
        return new Size(Math.Max(0, 2 * PaddingX() + icon + text), _lineHeight);
    }

    private IBrush? TintBrush(ActionSwapTint tint) => tint switch
    {
        ActionSwapTint.Success => SuccessBrush ?? Foreground,
        ActionSwapTint.Warning => WarningBrush ?? Foreground,
        ActionSwapTint.Danger => DangerBrush ?? Foreground,
        _ => Foreground
    };

    public override void Render(DrawingContext context)
    {
        var padX = PaddingX();
        var iconWidth = Math.Max(0, _iconWidth?.Value ?? 0);
        var centerY = Bounds.Height / 2;
        var iconCenter = new Point(padX + iconWidth / 2, centerY);
        foreach (var phase in _phases)
        {
            DrawPhase(context, phase, iconCenter);
        }
        var origin = padX + iconWidth;
        var top = (Bounds.Height - _lineHeight) / 2;
        var full = Full;
        foreach (var glyph in _glyphs)
        {
            if (glyph.Text is null)
            {
                continue;
            }
            double opacity = 1, dy = 0, blur = 0;
            if (glyph.Leaving)
            {
                var q = Math.Clamp(glyph.Exit?.Value ?? 1, 0, 1);
                opacity = 1 - q;
                if (full)
                {
                    dy = -3 * q;
                    blur = BlurRadius * q;
                }
            }
            else if (glyph.Enter is { } enter)
            {
                var p = Math.Clamp(enter.Value, 0, 1);
                opacity = p;
                if (full)
                {
                    dy = 4 * (1 - p);
                    blur = BlurRadius * (1 - p);
                }
            }
            if (opacity <= 0.001)
            {
                continue;
            }
            var x = origin + (glyph.X?.Value ?? glyph.TargetX);
            var bounds = new Rect(x - 8, top + dy - 8, glyph.Width + 16, _lineHeight + 16);
            var effect = blur > 0.05 ? ArcBlur.Get(blur) : null;
            using (context.PushOpacity(opacity))
            using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, bounds))
            {
                context.DrawText(glyph.Text, new Point(x, top + dy));
            }
        }
    }

    private void DrawPhase(DrawingContext context, Phase phase, Point center)
    {
        var opacity = Math.Clamp(phase.Opacity.Value, 0, 1);
        if (opacity <= 0.001 || TintBrush(phase.Tint) is not { } brush)
        {
            return;
        }
        var scale = phase.Scale.Value;
        var blur = phase.Blur.Value;
        var wipe = Math.Clamp(phase.Wipe.Value, 0, 1);
        var half = IconExtent / 2;
        var cell = new Rect(center.X - half, center.Y - half, IconExtent, IconExtent);
        var effect = blur > 0.05 ? ArcBlur.Get(blur) : null;
        var matrix = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(matrix))
        using (context.PushOpacity(opacity))
        using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, cell.Inflate(8)))
        using (wipe < 0.999 ? context.PushClip(new Rect(cell.X, cell.Y, cell.Width * wipe, cell.Height)) : default)
        {
            if (phase.Spinner)
            {
                DrawSpinner(context, brush, center);
            }
            else if (phase.Data is { } data)
            {
                var k = IconExtent / 24.0;
                if (phase.Pen is null || !ReferenceEquals(phase.Pen.Brush, brush))
                {
                    phase.Pen = new Pen(brush, phase.Stroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                }
                var pen = phase.Pen;
                using (context.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation(cell.X, cell.Y)))
                {
                    context.DrawGeometry(null, pen, data);
                }
            }
        }
    }

    private void DrawSpinner(DrawingContext context, IBrush brush, Point center)
    {
        var color = (brush as ISolidColorBrush)?.Color ?? Colors.Black;
        if (_arcPen is null || _trackPen is null || _penColor != color)
        {
            _penColor = color;
            _arcPen = new Pen(new ImmutableSolidColorBrush(color), SpinnerThickness);
            _trackPen = new Pen(new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(color.A * 0.24), color.R, color.G, color.B)), SpinnerThickness);
        }
        var radius = (SpinnerExtent - SpinnerThickness) / 2;
        // A faint full track with one bright arc turning around it.
        context.DrawEllipse(null, _trackPen, center, radius, radius);
        var turn = Matrix.CreateRotation(_angle * Math.PI / 180.0) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(turn))
        {
            context.DrawGeometry(null, _arcPen, SpinnerArc);
        }
    }

    private static StreamGeometry CreateSpinnerArc()
    {
        var radius = (SpinnerExtent - SpinnerThickness) / 2;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var start = new Point(radius * Math.Cos(-3 * Math.PI / 4), radius * Math.Sin(-3 * Math.PI / 4));
            var end = new Point(radius * Math.Cos(-Math.PI / 4), radius * Math.Sin(-Math.PI / 4));
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        return geometry;
    }

    #endregion
}
