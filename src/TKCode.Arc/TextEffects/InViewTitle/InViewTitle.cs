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

namespace TKCode.Arc;

/// <summary>How an <see cref="InViewTitle"/> reveals itself.</summary>
public enum InViewTitleVariant
{
    /// <summary>Each word rises 0.9em out of its own clip.</summary>
    Word,

    /// <summary>Each line (see <see cref="InViewTitle.Lines"/>) rises out of its clip, 80ms apart.</summary>
    Line,

    /// <summary>Words sharpen from an 8px blur while lifting 0.2em (default; suits most headings).</summary>
    Blur,

    /// <summary>Tight letter spacing opens up while each word clears a soft blur.</summary>
    Tracking,

    /// <summary>A soft-edged mask uncovers the title from left to right.</summary>
    Wipe
}

/// <summary>
/// A section title that reveals itself when 45% of it scrolls into view (Arc InViewTitle, P18 + P20): opacity leads,
/// the blur clears next and the movement settles last, so the title is readable before it fully arrives. Titles
/// already scrolled past show at once. With <see cref="IsReplayEnabled"/> it hides again (a quick fade with no
/// stagger) when it leaves and replays on every entry. Reduced motion shows the title without movement, blur or
/// masks. Drawn as one control: no per-word controls.
/// </summary>
public class InViewTitle : Control
{
    /// <summary>The title (also its accessible name).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<InViewTitle, string?>(nameof(Text));

    /// <summary>How the title reveals itself (Arc <c>variant</c>, blur by default).</summary>
    public static readonly StyledProperty<InViewTitleVariant> VariantProperty =
        AvaloniaProperty.Register<InViewTitle, InViewTitleVariant>(nameof(Variant), InViewTitleVariant.Blur);

    /// <summary>Heading level (Arc <c>as</c>, default h2).</summary>
    public static readonly StyledProperty<TextLevel> LevelProperty =
        AvaloniaProperty.Register<InViewTitle, TextLevel>(nameof(Level), TextLevel.H2);

    /// <summary>Explicit lines for the <see cref="InViewTitleVariant.Line"/> variant (no reflow); defaults to the whole text.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> LinesProperty =
        AvaloniaProperty.Register<InViewTitle, IReadOnlyList<string>?>(nameof(Lines));

    /// <summary>Replays on every entry (Arc <c>once={false}</c>).</summary>
    public static readonly StyledProperty<bool> IsReplayEnabledProperty =
        AvaloniaProperty.Register<InViewTitle, bool>(nameof(IsReplayEnabled));

    /// <summary>Letter spacing in em.</summary>
    public static readonly StyledProperty<double> TrackingProperty =
        AvaloniaProperty.Register<InViewTitle, double>(nameof(Tracking));

    /// <summary>Line height as a multiple of the font size.</summary>
    public static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<InViewTitle, double>(nameof(Leading), 1.1);

    /// <summary>Starting blur of the blur variant (BlurText).</summary>
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<InViewTitle, double>(nameof(BlurRadius), 8);

    /// <summary>Starting blur of the tracking variant (BlurSoft).</summary>
    public static readonly StyledProperty<double> SoftBlurRadiusProperty =
        AvaloniaProperty.Register<InViewTitle, double>(nameof(SoftBlurRadius), 4);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<InViewTitle>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<InViewTitle>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<InViewTitle>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<InViewTitle>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public InViewTitleVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public TextLevel Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public IReadOnlyList<string>? Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    public bool IsReplayEnabled { get => GetValue(IsReplayEnabledProperty); set => SetValue(IsReplayEnabledProperty, value); }
    public double Tracking { get => GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    public double Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public double SoftBlurRadius { get => GetValue(SoftBlurRadiusProperty); set => SetValue(SoftBlurRadiusProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Defines the <see cref="IsRevealed"/> property.</summary>
    public static readonly DirectProperty<InViewTitle, bool> IsRevealedProperty =
        AvaloniaProperty.RegisterDirect<InViewTitle, bool>(nameof(IsRevealed), o => o.IsRevealed);

    private bool _isRevealed;

    /// <summary>True once the title is (being) shown; false while it waits to scroll into view.</summary>
    public bool IsRevealed
    {
        get => _isRevealed;
        private set => SetAndRaise(IsRevealedProperty, ref _isRevealed, value);
    }

    /// <summary>The trigger: 45% of the title visible (Arc <c>amount: .45</c>).</summary>
    internal const double Threshold = 0.45;

    private readonly TextEffectsWordLayout _layout = new();
    private MotionValue<double>? _clock;
    private MotionValue<double>? _hide;
    private ViewportVisibility? _visibility;
    private bool _instant = true;
    private double _total;
    private static readonly Easing s_linear = new LinearEasing();
    private static readonly IBrush s_wipeEdge = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Colors.Black), new ImmutableGradientStop(1, Colors.Transparent)],
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
        endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));

    static InViewTitle()
    {
        AffectsMeasure<InViewTitle>(TextProperty, VariantProperty, LinesProperty, TrackingProperty, LeadingProperty,
            FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<InViewTitle>(BlurRadiusProperty, SoftBlurRadiusProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<InViewTitle>(AutomationControlType.Text);
    }

    public InViewTitle()
    {
        EffectiveViewportChanged += (_, e) =>
        {
            // A title already scrolled past (its top above the viewport) shows at once.
            if (!IsRevealed && e.EffectiveViewport.Height > 0 && e.EffectiveViewport.Y > 0 &&
                e.EffectiveViewport.Bottom > 0 && Bounds.Height > 0 && e.EffectiveViewport.Y >= Bounds.Height * (1 - Threshold))
            {
                ShowInstantly();
            }
        };
    }

    /// <summary>Settle duration of the movement track per variant (Arc <c>reveal(…, settle)</c>).</summary>
    internal static double SettleFor(InViewTitleVariant variant) => variant switch
    {
        InViewTitleVariant.Line => 0.86,
        InViewTitleVariant.Tracking or InViewTitleVariant.Wipe => 0.9,
        _ => 0.8
    };

    /// <summary>Stagger of word units: 60ms, shortened so the whole stagger stays within 0.4s.</summary>
    internal static double WordStep(int words) => Math.Min(ArcMotionPresets.StaggerWord.TotalSeconds * 1.5, 0.4 / Math.Max(words, 1));

    /// <summary>Delay of unit <paramref name="index"/> for a variant.</summary>
    internal static double DelayFor(InViewTitleVariant variant, int index, int units) => variant switch
    {
        InViewTitleVariant.Line => index * ArcMotionPresets.StaggerLine.TotalSeconds,
        InViewTitleVariant.Tracking => index * ArcMotionPresets.StaggerChar.TotalSeconds * 2,
        InViewTitleVariant.Wipe => 0,
        _ => index * WordStep(units)
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == LevelProperty)
        {
            AutomationProperties.SetName(this, Text ?? string.Empty);
            AutomationProperties.SetHeadingLevel(this, Level switch
            {
                TextLevel.H1 => 1,
                TextLevel.H2 => 2,
                TextLevel.H3 => 3,
                _ => 0
            });
        }
        else if (change.Property == IsReplayEnabledProperty && TopLevel.GetTopLevel(this) is not null)
        {
            Observe();
        }
        else if (change.Property == ForegroundProperty)
        {
            _layout.Invalidate();
            InvalidateMeasure();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            ShowInstantly();
            return;
        }
        if (!IsRevealed)
        {
            Observe();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        _clock?.Stop();
        _hide?.Stop();
    }

    private void Observe()
    {
        _visibility?.Dispose();
        _visibility = new ViewportVisibility(this, Threshold, once: !IsReplayEnabled);
        _visibility.VisibilityChanged += (_, visible) =>
        {
            if (visible)
            {
                Reveal();
            }
            else if (IsReplayEnabled)
            {
                Conceal();
            }
        };
    }

    /// <summary>Shows the final state without motion.</summary>
    private void ShowInstantly()
    {
        _instant = true;
        _clock?.Stop();
        _hide?.Jump(0);
        IsRevealed = true;
        InvalidateVisual();
    }

    private void Reveal()
    {
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            ShowInstantly();
            return;
        }
        _instant = false;
        IsRevealed = true;
        var units = UnitCount();
        var settle = SettleFor(Variant);
        _total = DelayFor(Variant, Math.Max(units - 1, 0), units) + settle;
        _clock ??= CreateValue(0);
        _hide ??= CreateHide();
        _hide.Jump(0);
        _clock.Jump(0);
        _ = _clock.AnimateTo(_total, MotionSpec.Tween(TimeSpan.FromSeconds(_total), s_linear));
        InvalidateVisual();
    }

    private void Conceal()
    {
        if (!IsRevealed)
        {
            return;
        }
        _clock?.Stop();
        _hide ??= CreateHide();
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        _instant = false;
        _ = _hide.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
    }

    private MotionValue<double> CreateHide()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += hidden =>
        {
            // Fully hidden: wait for the next entry.
            if (hidden >= 1)
            {
                IsRevealed = false;
            }
            InvalidateVisual();
        };
        return value;
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private int UnitCount() => Variant switch
    {
        InViewTitleVariant.Line => Lines is { Count: > 0 } lines ? lines.Count : 1,
        InViewTitleVariant.Wipe => 1,
        _ => TextEffectsWordLayout.Split(Text ?? string.Empty).Sum(l => l.Length)
    };

    private TextEffectsStyle Style() => new(new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize,
        Foreground ?? Brushes.Black, Tracking * FontSize, Leading * FontSize);

    protected override Size MeasureOverride(Size availableSize)
    {
        var style = Style();
        var text = Text ?? string.Empty;
        switch (Variant)
        {
            case InViewTitleVariant.Line:
                _layout.BuildLines(Lines is { Count: > 0 } lines ? lines : [text], style);
                break;
            case InViewTitleVariant.Wipe:
                _layout.BuildBlock(text, style, availableSize.Width);
                break;
            default:
                _layout.BuildWords(text, style, availableSize.Width, balance: false, letters: Variant == InViewTitleVariant.Tracking);
                break;
        }
        return _layout.Size;
    }

    /// <summary>Eased track progress for unit timing, scaled down while hiding.</summary>
    private double Progress(double delay, double duration, Easing easing, double elapsed, double hide)
    {
        var p = TextEffectsTiming.Track(elapsed, delay, duration, easing);
        return p * (1 - hide);
    }

    public override void Render(DrawingContext context)
    {
        if (_instant && IsRevealed)
        {
            DrawPlain(context);
            return;
        }
        var hide = _hide?.Value ?? 0;
        var elapsed = IsRevealed ? _clock?.Value ?? 0 : 0;
        if (!IsRevealed)
        {
            return;
        }
        var em = FontSize;
        var settle = SettleFor(Variant);
        var units = _layout.Words.Count;
        switch (Variant)
        {
            case InViewTitleVariant.Word:
                foreach (var word in _layout.Words)
                {
                    var delay = DelayFor(Variant, word.Index, units);
                    var y = Progress(delay, settle, ArcEasings.Enter, elapsed, hide);
                    var opacity = Progress(delay, settle * 0.56, ArcEasings.Standard, elapsed, hide);
                    if (opacity <= 0.001)
                    {
                        continue;
                    }
                    var clip = new Rect(word.X - 0.14 * em, word.Y - 0.08 * em, word.Width + 0.28 * em, _layout.LineHeight + 0.28 * em);
                    using (context.PushClip(clip))
                    using (context.PushOpacity(opacity))
                    {
                        word.Layout.Draw(context, new Point(word.X, word.Y + 0.9 * em * (1 - y)));
                    }
                }
                break;
            case InViewTitleVariant.Line:
                for (var i = 0; i < _layout.Lines.Count; i++)
                {
                    var line = _layout.Lines[i];
                    var delay = DelayFor(Variant, i, _layout.Lines.Count);
                    var y = Progress(delay, settle, ArcEasings.Enter, elapsed, hide);
                    var opacity = Progress(delay, settle * 0.56, ArcEasings.Standard, elapsed, hide);
                    if (opacity <= 0.001)
                    {
                        continue;
                    }
                    var clip = new Rect(-0.12 * em, line.Y - 0.06 * em, line.Width + 0.24 * em, _layout.LineHeight + 0.24 * em);
                    using (context.PushClip(clip))
                    using (context.PushOpacity(opacity))
                    {
                        line.Layout.Draw(context, new Point(0, line.Y + 1.12 * _layout.LineHeight * (1 - y)));
                    }
                }
                break;
            case InViewTitleVariant.Blur:
                foreach (var word in _layout.Words)
                {
                    var delay = DelayFor(Variant, word.Index, units);
                    var y = Progress(delay, settle, ArcEasings.Enter, elapsed, hide);
                    var focus = Progress(delay, settle * 0.78, ArcEasings.Enter, elapsed, hide);
                    var opacity = Progress(delay, settle * 0.56, ArcEasings.Standard, elapsed, hide);
                    DrawUnit(context, word, opacity, 0.2 * em * (1 - y), BlurRadius * (1 - focus));
                }
                break;
            case InViewTitleVariant.Tracking:
                foreach (var word in _layout.Words)
                {
                    var delay = DelayFor(Variant, word.Index, units);
                    var x = Progress(delay, 1.0, ArcEasings.Enter, elapsed, hide);
                    var focus = Progress(delay, settle * 0.78, ArcEasings.Enter, elapsed, hide);
                    var opacity = Progress(delay, settle * 0.56, ArcEasings.Standard, elapsed, hide);
                    if (opacity <= 0.001)
                    {
                        continue;
                    }
                    if (x >= 1 && focus >= 1 && opacity >= 1 || word.Letters is null || word.LetterX is null)
                    {
                        DrawUnit(context, word, opacity, 0, SoftBlurRadius * (1 - focus));
                        continue;
                    }
                    var center = (word.Letters.Length - 1) / 2.0;
                    var bounds = new Rect(word.X - 0.5 * em, word.Y, word.Width + em, _layout.LineHeight);
                    using (context.PushOpacity(opacity))
                    {
                        var effect = ArcBlur.Get(SoftBlurRadius * (1 - focus));
                        using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, bounds))
                        {
                            for (var i = 0; i < word.Letters.Length; i++)
                            {
                                var dx = (center - i) * 0.06 * em * (1 - x);
                                word.Letters[i].Draw(context, new Point(word.X + word.LetterX[i] + dx, word.Y));
                            }
                        }
                    }
                }
                break;
            case InViewTitleVariant.Wipe:
                if (_layout.Lines.Count > 0)
                {
                    DrawWipe(context, _layout.Lines[0], Progress(0, settle, ArcEasings.Standard, elapsed, hide),
                        Progress(0, settle, ArcEasings.Enter, elapsed, hide));
                }
                break;
        }
    }

    private void DrawUnit(DrawingContext context, TextEffectsWordLayout.Word word, double opacity, double dy, double blur)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        var origin = new Point(word.X, word.Y + dy);
        if (opacity >= 0.999 && Math.Abs(dy) < 0.01 && blur < 0.1)
        {
            word.Layout.Draw(context, origin);
            return;
        }
        using (context.PushOpacity(opacity))
        {
            var effect = ArcBlur.Get(blur);
            if (effect is null)
            {
                word.Layout.Draw(context, origin);
                return;
            }
            using (context.PushEffect(effect, new Rect(word.X, word.Y + dy, word.Width, _layout.LineHeight)))
            {
                word.Layout.Draw(context, origin);
            }
        }
    }

    /// <summary>
    /// The wipe mask is a 90° gradient (opaque to 44%, clear from 56%) sized to 2.3× the padded title and slid from
    /// 100% to 0%: everything left of the soft edge is drawn solid, the edge band through a cached gradient mask.
    /// </summary>
    private void DrawWipe(DrawingContext context, TextEffectsWordLayout.Line block, double wipe, double travel)
    {
        var em = FontSize;
        var boxX = -0.12 * em;
        var boxWidth = block.Width + 0.24 * em;
        var top = -0.1 * em;
        var height = block.Layout.Height + 0.32 * em;
        var position = 1 - wipe;
        var edgeStart = boxX + boxWidth * (1.012 - 1.3 * position);
        var edgeEnd = boxX + boxWidth * (1.288 - 1.3 * position);
        var origin = new Point(-0.12 * em * (1 - travel), 0);
        var solid = new Rect(boxX, top, Math.Max(0, edgeStart - boxX), height);
        if (solid.Width > 0)
        {
            using (context.PushClip(solid))
            {
                block.Layout.Draw(context, origin);
            }
        }
        var band = new Rect(edgeStart, top, Math.Max(0, edgeEnd - edgeStart), height);
        if (band.Width > 0 && band.Right > boxX)
        {
            using (context.PushClip(band))
            using (context.PushOpacityMask(s_wipeEdge, band))
            {
                block.Layout.Draw(context, origin);
            }
        }
    }

    private void DrawPlain(DrawingContext context)
    {
        if (Variant is InViewTitleVariant.Line or InViewTitleVariant.Wipe)
        {
            foreach (var line in _layout.Lines)
            {
                line.Layout.Draw(context, new Point(0, line.Y));
            }
            return;
        }
        foreach (var word in _layout.Words)
        {
            word.Layout.Draw(context, new Point(word.X, word.Y));
        }
    }
}
