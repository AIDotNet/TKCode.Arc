using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The wrapped text of one note line: the shared <see cref="AiStreamText"/> (it grows chunk by chunk while the notes stream,
/// each new chunk only fading in over 140ms), plus what an action item does when it is checked off: a 1px strikethrough
/// sweeps left to right across every wrapped line (<see cref="Strike"/>, 0 to 1 of the total line length) while the color
/// eases to <see cref="MutedForeground"/> (<see cref="Muted"/>). The color is a brush the text keeps, so neither the sweep
/// nor the color ever relayouts.
/// </summary>
internal sealed class MeetingSummaryLineText : AiStreamText
{
    /// <summary>The color a checked line eases to.</summary>
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<MeetingSummaryLineText, IBrush?>(nameof(MutedForeground));

    /// <summary>How far the color has eased to the muted color (0 to 1).</summary>
    public static readonly StyledProperty<double> MutedProperty =
        AvaloniaProperty.Register<MeetingSummaryLineText, double>(nameof(Muted));

    /// <summary>How much of the strikethrough is drawn (0 to 1 of all wrapped lines).</summary>
    public static readonly StyledProperty<double> StrikeProperty =
        AvaloniaProperty.Register<MeetingSummaryLineText, double>(nameof(Strike));

    /// <summary>One chunk's fade in.</summary>
    internal static readonly TimeSpan ChunkFade = TimeSpan.FromMilliseconds(140);

    private readonly SolidColorBrush _brush = new();
    private IReadOnlyList<Rect>? _lineRects;
    private ImmutablePen? _strikePen;

    static MeetingSummaryLineText()
    {
        AffectsRender<MeetingSummaryLineText>(StrikeProperty, MutedProperty);
    }

    public MeetingSummaryLineText()
    {
        FadeDuration = ChunkFade;
        FontWeight = FontWeight.Normal;
    }

    /// <inheritdoc cref="MutedForegroundProperty"/>
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    /// <inheritdoc cref="MutedProperty"/>
    public double Muted { get => GetValue(MutedProperty); set => SetValue(MutedProperty, value); }

    /// <inheritdoc cref="StrikeProperty"/>
    public double Strike { get => GetValue(StrikeProperty); set => SetValue(StrikeProperty, value); }

    /// <summary>The text color: the foreground eased toward the muted color by <see cref="Muted"/>.</summary>
    internal Color DisplayedColor => _brush.Color;

    protected override IBrush? TextBrush
    {
        get
        {
            UpdateBrush();
            return _brush;
        }
    }

    protected override void OnLayoutChanged(TextLayout layout) => _lineRects = null;

    protected override void RenderOverlay(DrawingContext context, TextLayout layout)
    {
        var strike = Math.Clamp(Strike, 0, 1);
        if (strike <= 0.001)
        {
            return;
        }
        _lineRects ??= TextRangeRenderer.GetRects(layout, 0, Length);
        var total = 0.0;
        foreach (var rect in _lineRects)
        {
            total += rect.Width;
        }
        var left = total * strike;
        _strikePen ??= new ImmutablePen(new ImmutableSolidColorBrush(StrikeColor()), 1);
        foreach (var rect in _lineRects)
        {
            if (left <= 0)
            {
                break;
            }
            var length = Math.Min(rect.Width, left);
            left -= rect.Width;
            // Snapped to the half pixel so the 1px line stays crisp.
            var y = Math.Round(rect.Y + rect.Height / 2) + 0.5;
            context.DrawLine(_strikePen, new Point(rect.X, y), new Point(rect.X + length, y));
        }
    }

    private Color StrikeColor() => MutedForeground is ISolidColorBrush muted ? muted.Color : Colors.Gray;

    private void UpdateBrush()
    {
        var from = Foreground is ISolidColorBrush f ? f.Color : Colors.Black;
        var to = MutedForeground is ISolidColorBrush m ? m.Color : from;
        var t = Math.Clamp(Muted, 0, 1);
        _brush.Color = Color.FromArgb(
            Mix(from.A, to.A, t), Mix(from.R, to.R, t), Mix(from.G, to.G, t), Mix(from.B, to.B, t));
    }

    private static byte Mix(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == MutedForegroundProperty || change.Property == MutedProperty)
        {
            UpdateBrush();
            if (change.Property == MutedForegroundProperty)
            {
                _strikePen = null;
            }
            InvalidateVisual();
        }
    }
}
