using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media.Immutable;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The status chip of <see cref="ArmingSwitch"/>: an icon and a word in a tinted pill whose width is the widest of the
/// six words, so it never changes between them (Arc Arming switch, design reference: 28 px tall, padding 0 10 0 8, gap 6,
/// a 16 px icon, 13 px text, the tone at 11% behind it). A change fades the old icon and word out (150 ms) while the new
/// ones come in after 40 to 60 ms: the word rises 5 px and the icon turns from -40° and grows from 0.7 on a 440 ms
/// settle. The loader icon spins (900 ms a turn) while the action runs. Drawn in <see cref="Render"/>, so the swap and
/// the spin need no template nodes.
/// </summary>
internal sealed class ArmingSwitchChip : Control
{
    // Inks from the theme: the six tones (TextMuted, TextSecondary, Warning twice, Success, Danger) and the text.
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ArmingSwitchChip>();

    public static readonly StyledProperty<IBrush?> SafeToneProperty =
        AvaloniaProperty.Register<ArmingSwitchChip, IBrush?>(nameof(SafeTone));

    public static readonly StyledProperty<IBrush?> OpenToneProperty =
        AvaloniaProperty.Register<ArmingSwitchChip, IBrush?>(nameof(OpenTone));

    public static readonly StyledProperty<IBrush?> ArmedToneProperty =
        AvaloniaProperty.Register<ArmingSwitchChip, IBrush?>(nameof(ArmedTone));

    public static readonly StyledProperty<IBrush?> DoneToneProperty =
        AvaloniaProperty.Register<ArmingSwitchChip, IBrush?>(nameof(DoneTone));

    public static readonly StyledProperty<IBrush?> FailedToneProperty =
        AvaloniaProperty.Register<ArmingSwitchChip, IBrush?>(nameof(FailedTone));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<ArmingSwitchChip>();

    private const double ChipHeight = 28;
    private const double PadLeft = 8;
    private const double PadRight = 10;
    private const double IconSize = 16;
    private const double Gap = 6;
    private const double TextSize = 13;
    private const double SwapSeconds = 0.44;

    private static readonly Geometry ShieldAlert =
        ArcIcons.Parse("M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M12 8v4 M12 16h.01"); // Lucide shield-alert

    private readonly string[] _texts = { "", "", "", "", "", "" };
    private readonly TextLayout?[] _layouts = new TextLayout?[6];
    private double _textWidth;
    private ArmingSwitchStatus _status;
    private ArmingSwitchStatus _previous;
    private double _swap = 1;   // seconds since the last change, normalised by SwapSeconds (1 = settled)
    private double _spin;       // degrees of the loader
    private readonly LiveLoop _loop;
    private readonly Dictionary<Color, Pen> _pens = new();

    public ArmingSwitchChip()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? SafeTone { get => GetValue(SafeToneProperty); set => SetValue(SafeToneProperty, value); }

    public IBrush? OpenTone { get => GetValue(OpenToneProperty); set => SetValue(OpenToneProperty, value); }

    public IBrush? ArmedTone { get => GetValue(ArmedToneProperty); set => SetValue(ArmedToneProperty, value); }

    public IBrush? DoneTone { get => GetValue(DoneToneProperty); set => SetValue(DoneToneProperty, value); }

    public IBrush? FailedTone { get => GetValue(FailedToneProperty); set => SetValue(FailedToneProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The status shown (tests read it).</summary>
    public ArmingSwitchStatus Status => _status;

    /// <summary>The text shown for the current status.</summary>
    public string Text => _texts[(int)_status];

    static ArmingSwitchChip()
    {
        AffectsRender<ArmingSwitchChip>(ForegroundProperty, SafeToneProperty, OpenToneProperty, ArmedToneProperty,
            DoneToneProperty, FailedToneProperty);
    }

    public void SetTexts(string safe, string open, string armed, string running, string done, string failed)
    {
        var changed = false;
        changed |= Put(0, safe);
        changed |= Put(1, open);
        changed |= Put(2, armed);
        changed |= Put(3, running);
        changed |= Put(4, done);
        changed |= Put(5, failed);
        if (changed)
        {
            ResetLayouts();
        }
    }

    private bool Put(int i, string? text)
    {
        text ??= string.Empty;
        if (_texts[i] == text)
        {
            return false;
        }
        _texts[i] = text;
        return true;
    }

    public void SetStatus(ArmingSwitchStatus status, bool animate)
    {
        if (status == _status)
        {
            UpdateLoop();
            return;
        }
        _previous = _status;
        _status = status;
        _swap = animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off ? 0 : 1;
        UpdateLoop();
        InvalidateVisual();
    }

    private void UpdateLoop()
    {
        if (_swap < 1 || _status == ArmingSwitchStatus.Running)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private bool Step(double seconds)
    {
        var dt = Math.Min(seconds, 0.05);
        var reduced = !ArcMotion.IsFull(this);
        _swap = Math.Min(1, _swap + (dt / (reduced ? 0.2 : SwapSeconds)));
        if (_status == ArmingSwitchStatus.Running || (_previous == ArmingSwitchStatus.Running && _swap < 1))
        {
            _spin = (_spin + (dt * 400)) % 360;
        }
        InvalidateVisual();
        var more = _swap < 1 || _status == ArmingSwitchStatus.Running;
        return more;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == ForegroundProperty)
        {
            ResetLayouts();
        }
    }

    private void ResetLayouts()
    {
        for (var i = 0; i < _layouts.Length; i++)
        {
            _layouts[i]?.Dispose();
            _layouts[i] = null;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private TextLayout Layout(int i)
    {
        return _layouts[i] ??= new TextLayout(_texts[i], new Typeface(FontFamily), TextSize,
            Foreground ?? Brushes.White, lineHeight: 18.2, maxLines: 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _textWidth = 0;
        for (var i = 0; i < _texts.Length; i++)
        {
            _textWidth = Math.Max(_textWidth, Layout(i).WidthIncludingTrailingWhitespace);
        }
        return new Size(Math.Ceiling(PadLeft + IconSize + Gap + _textWidth + PadRight), ChipHeight);
    }

    private IBrush? ToneOf(ArmingSwitchStatus status) => status switch
    {
        ArmingSwitchStatus.Open => OpenTone,
        ArmingSwitchStatus.Armed or ArmingSwitchStatus.Running => ArmedTone,
        ArmingSwitchStatus.Done => DoneTone,
        ArmingSwitchStatus.Failed => FailedTone,
        _ => SafeTone
    };

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush s ? s.Color : Colors.Gray;

    private static Geometry IconOf(ArmingSwitchStatus status) => status switch
    {
        ArmingSwitchStatus.Open => ShieldAlert,
        ArmingSwitchStatus.Armed => ArcIcons.Zap,
        ArmingSwitchStatus.Running => ArcIcons.LoaderCircle,
        ArmingSwitchStatus.Done => ArcIcons.CircleCheck,
        ArmingSwitchStatus.Failed => ArcIcons.TriangleAlert,
        _ => ArcIcons.ShieldCheck
    };

    private Pen PenOf(Color color)
    {
        if (!_pens.TryGetValue(color, out var pen))
        {
            if (_pens.Count > 32)
            {
                _pens.Clear();
            }
            pen = new Pen(new ImmutableSolidColorBrush(color), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _pens[color] = pen;
        }
        return pen;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var full = ArcMotion.IsFull(this);
        var u = Math.Clamp(_swap, 0, 1);
        // The pill's tone crosses over in 200 ms (0.45 of the swap).
        var toneMix = Math.Clamp(u / 0.45, 0, 1);
        var tone = ArcColor.LerpOklab(ColorOf(ToneOf(_previous)), ColorOf(ToneOf(_status)), Ease(toneMix));
        var pill = new RoundedRect(new Rect(size), size.Height / 2);
        context.DrawRectangle(new ImmutableSolidColorBrush(Oklch.WithAlpha(tone, 0.11 * tone.A / 255.0)), null, pill);

        var ms = u * SwapSeconds * 1000;
        // Outgoing: 150 ms fade, the icon turns away and shrinks.
        if (u < 1)
        {
            var outFade = 1 - Math.Clamp(ms / 150, 0, 1);
            if (outFade > 0)
            {
                DrawContent(context, _previous, outFade, full ? -40 * (1 - outFade) : 0, full ? 1 - (0.3 * (1 - outFade)) : 1,
                    full ? -5 * (1 - outFade) : 0);
            }
        }
        var iconIn = Math.Clamp((ms - 60) / 150, 0, 1);
        var textIn = Math.Clamp((ms - 40) / 150, 0, 1);
        var settle = full ? Settle(Math.Clamp((ms - 50) / 440, 0, 1)) : 1;
        DrawContent(context, _status, u >= 1 ? 1 : Math.Min(iconIn, 1), full ? -40 * (1 - settle) : 0,
            full ? 0.7 + (0.3 * settle) : 1, full ? 5 * (1 - settle) : 0, u >= 1 ? 1 : textIn);
    }

    private void DrawContent(DrawingContext context, ArmingSwitchStatus status, double iconOpacity, double turn,
                             double scale, double rise, double textOpacity = -1)
    {
        if (textOpacity < 0)
        {
            textOpacity = iconOpacity;
        }
        var color = ColorOf(ToneOf(status));
        var cy = Bounds.Height / 2;
        var iconCentre = new Point(PadLeft + (IconSize / 2), cy);
        if (iconOpacity > 0.001)
        {
            var spin = status == ArmingSwitchStatus.Running ? _spin : 0;
            var m = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(IconSize / 24 * scale, IconSize / 24 * scale)
                    * Matrix.CreateRotation((turn + spin) * Math.PI / 180) * Matrix.CreateTranslation(iconCentre.X, iconCentre.Y);
            using (context.PushOpacity(iconOpacity))
            using (context.PushTransform(m))
            {
                context.DrawGeometry(null, PenOf(color), IconOf(status));
            }
        }
        if (textOpacity > 0.001)
        {
            var layout = Layout((int)status);
            var y = cy - (layout.Height / 2) + rise;
            using (context.PushOpacity(textOpacity))
            {
                layout.Draw(context, new Point(PadLeft + IconSize + Gap, y));
            }
        }
    }

    private static double Ease(double t) => 1 - Math.Pow(1 - t, 3);

    // A critically damped settle, the shape of Arc's 440 ms linear() curve.
    private static double Settle(double t) => 1 - ((1 + (7 * t)) * Math.Exp(-7 * t));
}
