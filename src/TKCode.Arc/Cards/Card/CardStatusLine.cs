using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The card's one-line status (P20). A new status replaces the whole line: the old one lifts 0.3em away with a subtle
/// blur (fast), while the new words rise 0.3em out of a soft blur one after another (standard + enter, 40ms per word).
/// The line clips sideways only and ends in an ellipsis when it is too long. Drawn in <see cref="Render"/> with cached
/// text: no per-word controls. Reduced and Off swap at once (Arc renders no enter or exit under reduced motion).
/// </summary>
internal sealed class CardStatusLine : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CardStatusLine, string?>(nameof(Text));

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<CardStatusLine, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<CardStatusLine>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<CardStatusLine>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<CardStatusLine>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<CardStatusLine>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private const double EnterSeconds = 0.24;
    private const double ExitSeconds = 0.16;
    private const double WordStagger = 0.04;
    private const double TravelEm = 0.3;
    private const double SoftBlur = 4;
    private const double SubtleBlur = 2;

    private static readonly ImmutableBlurEffect[] s_blurs = CreateBlurs();
    private static readonly Easing s_linear = new LinearEasing();

    private sealed class Line
    {
        public required string Text { get; init; }
        public required FormattedText Full { get; init; }
        public required FormattedText[] Words { get; init; }
        public required double[] X { get; init; }
        public required double[] Delays { get; init; }
        public required double Width { get; init; }
        public double Total => Delays.Length == 0 ? 0 : Delays[^1] + EnterSeconds;
    }

    private Line? _line;
    private Line? _leaving;
    private MotionValue<double>? _clock;
    private bool _playing;
    private int _generation;

    static CardStatusLine()
    {
        AffectsMeasure<CardStatusLine>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        AffectsRender<CardStatusLine>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CardStatusLine>(false);
    }

    /// <summary>True while a new status is rolling in.</summary>
    public bool IsRolling => _playing;

    private double ResolvedLineHeight => double.IsNaN(LineHeight) ? FontSize * 1.4 : LineHeight;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            var text = change.GetNewValue<string?>() ?? string.Empty;
            AutomationProperties.SetName(this, text);
            var previous = _line;
            _line = null;
            InvalidateMeasure();
            if (previous is { Text.Length: > 0 } && text.Length > 0 && IsLoaded && ArcMotion.IsFull(this))
            {
                _leaving = previous;
                _ = PlayAsync();
            }
            else
            {
                StopRoll();
            }
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            _line = null;
            _leaving = null;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private Line EnsureLine()
    {
        if (_line is not null)
        {
            return _line;
        }
        var text = Text ?? string.Empty;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        var brush = Foreground ?? Brushes.Black;
        var full = Format(text, typeface, brush);
        full.Trimming = TextTrimming.CharacterEllipsis;
        full.MaxLineCount = 1;
        // Arc splits on whitespace runs and staggers by part index / 2: words rise in 40ms apart.
        var words = new List<FormattedText>();
        var xs = new List<double>();
        var delays = new List<double>();
        var x = 0.0;
        var part = 0;
        var start = 0;
        while (start < text.Length)
        {
            var whitespace = char.IsWhiteSpace(text[start]);
            var end = start;
            while (end < text.Length && char.IsWhiteSpace(text[end]) == whitespace)
            {
                end++;
            }
            var piece = Format(text[start..end], typeface, brush);
            if (!whitespace)
            {
                words.Add(piece);
                xs.Add(x);
                delays.Add(part / 2.0 * WordStagger);
            }
            x += piece.WidthIncludingTrailingWhitespace;
            part++;
            start = end;
        }
        _line = new Line
        {
            Text = text,
            Full = full,
            Words = words.ToArray(),
            X = xs.ToArray(),
            Delays = delays.ToArray(),
            Width = x
        };
        return _line;
    }

    private FormattedText Format(string text, Typeface typeface, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, brush);

    private async Task PlayAsync()
    {
        var generation = ++_generation;
        var line = EnsureLine();
        var total = Math.Max(ExitSeconds, line.Total);
        _clock ??= CreateClock();
        _playing = true;
        _clock.Jump(0);
        await _clock.AnimateTo(total, MotionSpec.Tween(TimeSpan.FromSeconds(total), s_linear));
        if (generation == _generation)
        {
            StopRoll();
        }
    }

    private MotionValue<double> CreateClock()
    {
        var clock = new MotionValue<double>(this, 0, MotionVectors.Double);
        clock.Changed += _ => InvalidateVisual();
        return clock;
    }

    private void StopRoll()
    {
        _generation++;
        _clock?.Stop();
        _playing = false;
        _leaving = null;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var line = EnsureLine();
        var width = double.IsFinite(availableSize.Width) ? Math.Min(line.Width, availableSize.Width) : line.Width;
        return new Size(Math.Ceiling(width), ResolvedLineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var line = EnsureLine();
        line.Full.MaxTextWidth = Math.Max(1, finalSize.Width);
        if (_leaving is { } leaving)
        {
            leaving.Full.MaxTextWidth = Math.Max(1, finalSize.Width);
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var line = _line;
        if (line is null || Bounds.Width <= 0)
        {
            return;
        }
        var height = ResolvedLineHeight;
        var top = (height - line.Full.Height) / 2;
        if (!_playing || _clock is null)
        {
            context.DrawText(line.Full, new Point(0, top));
            return;
        }
        var t = _clock.Value;
        var em = FontSize;
        // The line clips sideways only, so the words rise in unclipped.
        using (context.PushClip(new Rect(0, -height, Bounds.Width, height * 3)))
        {
            if (_leaving is { } leaving)
            {
                var p = ArcEasings.Standard.Ease(Math.Clamp(t / ExitSeconds, 0, 1));
                if (p < 1)
                {
                    DrawPiece(context, leaving.Full, new Point(0, top - TravelEm * em * p), 1 - p, SubtleBlur * p);
                }
            }
            for (var i = 0; i < line.Words.Length; i++)
            {
                var local = Math.Clamp((t - line.Delays[i]) / EnterSeconds, 0, 1);
                if (local <= 0)
                {
                    continue;
                }
                var p = ArcEasings.Enter.Ease(local);
                DrawPiece(context, line.Words[i], new Point(line.X[i], top + TravelEm * em * (1 - p)), p, SoftBlur * (1 - p));
            }
        }
    }

    private static void DrawPiece(DrawingContext context, FormattedText text, Point origin, double opacity, double blur)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            var effect = Blur(blur);
            if (effect is null)
            {
                context.DrawText(text, origin);
                return;
            }
            var area = new Rect(origin.X - 8, origin.Y - 8, text.WidthIncludingTrailingWhitespace + 16, text.Height + 16);
            using (context.PushEffect(effect, area))
            {
                context.DrawText(text, origin);
            }
        }
    }

    private static ImmutableBlurEffect[] CreateBlurs()
    {
        var blurs = new ImmutableBlurEffect[17];
        for (var i = 0; i < blurs.Length; i++)
        {
            blurs[i] = new ImmutableBlurEffect(i * 0.25);
        }
        return blurs;
    }

    private static IEffect? Blur(double radius)
    {
        var index = (int)Math.Round(radius * 4);
        return index <= 0 ? null : s_blurs[Math.Min(index, s_blurs.Length - 1)];
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StatusPeer(this);

    private sealed class StatusPeer : ControlAutomationPeer
    {
        public StatusPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
