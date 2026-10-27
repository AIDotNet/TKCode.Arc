using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the hover card of a <see cref="CohortRetention"/> shows.</summary>
/// <param name="Title">"Jun 2026 cohort" or "Average" (14, Medium, Foreground).</param>
/// <param name="Month">"Month 5" on the right of the title (14, TextMuted).</param>
/// <param name="Value">The exact rate ("48.5%"; 22, Medium, tabular).</param>
/// <param name="FirstLabel">First row's label ("Still active").</param>
/// <param name="FirstValue">First row's value ("193 of 398").</param>
/// <param name="SecondLabel">Second row's label ("Vs average" / "Weighted over").</param>
/// <param name="SecondValue">Second row's value ("+0.1 pts" / "12 cohorts").</param>
/// <param name="SecondTone">The second value's colour: good (Success), bad (Danger) or flat (Foreground).</param>
internal sealed record CohortRetentionCardContent(string Title, string Month, string Value, string FirstLabel, string FirstValue,
    string SecondLabel, string SecondValue, AnalyticsTone SecondTone);

/// <summary>
/// The plot's hover card (measured: 200 wide, padding 12 / 14, 1px Border, radius 16, SurfaceRaised, ShadowFloating,
/// 124 high). It appears in place with a 120ms ease-out fade, slides to each new cell on a spring (k 600, c 48) while shown
/// and fades out in 160ms. The shared <c>AnalyticsReadout</c> cannot draw a two-part title or a coloured row value, so the
/// block draws its own card with the same motion model.
/// </summary>
/// <remarks>Why code-driven: the card lives inside the self-drawn plot and follows a cursor that moves per cell.</remarks>
internal sealed class CohortRetentionCard
{
    private const double PaddingX = 14;
    private const double PaddingTop = 12;
    private const double TitleLine = 19.6;
    private const double ValueGap = 6;
    private const double ValueLine = 28.6;
    private const double RowsGap = 8;
    private const double RowLine = 16.8;
    private const double RowGap = 2;
    private const double CornerRadius = 16;

    private AnalyticsSpringChannel _x = new(0, 0.05, 0.5);
    private AnalyticsSpringChannel _y = new(0, 0.05, 0.5);
    private AnalyticsTween _opacity = new(0);
    private bool _shown;

    /// <summary>Card width (200).</summary>
    public double Width { get; set; } = 200;

    /// <summary>Card height: padding, title, value and two rows (124).</summary>
    public static double Height => Math.Round(PaddingTop * 2 + TitleLine + ValueGap + ValueLine + RowsGap + RowLine * 2 + RowGap + 2);

    /// <summary>The content drawn now.</summary>
    public CohortRetentionCardContent? Content { get; set; }

    /// <summary>True between <see cref="Show"/> and <see cref="Hide"/>.</summary>
    public bool IsShown => _shown;

    /// <summary>True while drawn.</summary>
    public bool IsVisible => _shown || _opacity.Value > 0.001;

    /// <summary>Current top-left.</summary>
    public Point Position => new(_x.Value, _y.Value);

    /// <summary>Current opacity.</summary>
    public double Opacity => Math.Clamp(_opacity.Value, 0, 1);

    /// <summary>Where the card is heading.</summary>
    public Point Target => new(_x.Target, _y.Target);

    /// <summary>True while a fade or the follow spring runs.</summary>
    public bool IsMoving => _x.IsMoving || _y.IsMoving || _opacity.IsRunning;

    /// <summary>The card's size.</summary>
    public Size Size => new(Width, Height);

    /// <summary>Shows the card at <paramref name="position"/>: a hidden card appears there; a shown card glides there (Full motion).</summary>
    public void Show(Point position, ArcMotionLevel level)
    {
        if (!IsVisible || level != ArcMotionLevel.Full)
        {
            _x.Jump(position.X);
            _y.Jump(position.Y);
        }
        else
        {
            _x.SpringTo(position.X, AnalyticsSprings.ReadoutFollow);
            _y.SpringTo(position.Y, AnalyticsSprings.ReadoutFollow);
        }
        if (_shown)
        {
            return;
        }
        _shown = true;
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(1);
            return;
        }
        _opacity.Start(1, ArcMotionPresets.Instant, AnalyticsEasings.EaseOut);
    }

    /// <summary>Fades the card out where it is.</summary>
    public void Hide(ArcMotionLevel level)
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        _x.Stop();
        _y.Stop();
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
            return;
        }
        _opacity.Start(0, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut);
    }

    /// <summary>Removes the card at once.</summary>
    public void Clear()
    {
        _shown = false;
        _opacity.Jump(0);
        _x.Stop();
        _y.Stop();
    }

    /// <summary>Advances fades and the follow spring; true while anything moves.</summary>
    public bool Advance(double dt) => _x.Step(dt) | _y.Step(dt) | _opacity.Advance(dt);

    /// <summary>Draws the card (nothing when invisible).</summary>
    public void Draw(DrawingContext context, AnalyticsTextCache cache, AnalyticsPalette palette)
    {
        if (!IsVisible || Content is not { } content || !palette.IsReady)
        {
            return;
        }
        var rect = new Rect(Position, Size);
        using (context.PushOpacity(Opacity))
        {
            context.DrawRectangle(palette.Brush(palette.SurfaceRaised), palette.Pen(palette.Border, 1), new RoundedRect(rect.Deflate(0.5), CornerRadius),
                palette.ShadowFloating);
            var body = new Typeface(palette.BodyFont);
            var medium = new Typeface(palette.BodyFont, FontStyle.Normal, palette.Medium);
            var left = rect.X + PaddingX;
            var right = rect.Right - PaddingX;
            var y = rect.Y + 1 + PaddingTop;

            var month = cache.Get(content.Month, body, 14);
            month.Draw(context, palette.Brush(palette.TextMuted), new Point(right - month.Width, y + (TitleLine - month.Height) / 2));
            var title = cache.GetTrimmed(content.Title, medium, 14, Math.Max(1, right - left - month.Width - 4));
            title.Draw(context, palette.Brush(palette.Foreground), new Point(left, y + (TitleLine - title.Height) / 2));
            y += TitleLine + ValueGap;

            var value = cache.Get(content.Value, medium, 22, true);
            value.Draw(context, palette.Brush(palette.Foreground), new Point(left, y + (ValueLine - value.Height) / 2));
            y += ValueLine + RowsGap;

            DrawRow(context, cache, palette, body, content.FirstLabel, content.FirstValue, palette.Foreground, left, right, y);
            y += RowLine + RowGap;
            var tone = content.SecondTone switch
            {
                AnalyticsTone.Good => palette.Success,
                AnalyticsTone.Bad => palette.Danger,
                _ => palette.Foreground
            };
            DrawRow(context, cache, palette, body, content.SecondLabel, content.SecondValue, tone, left, right, y);
        }
    }

    private static void DrawRow(DrawingContext context, AnalyticsTextCache cache, AnalyticsPalette palette, Typeface font, string label,
        string value, Color valueColor, double left, double right, double y)
    {
        var valueText = cache.Get(value, font, 12, true);
        valueText.Draw(context, palette.Brush(valueColor), new Point(right - valueText.Width, y + (RowLine - valueText.Height) / 2));
        var labelText = cache.GetTrimmed(label, font, 12, Math.Max(1, right - left - valueText.Width - 8));
        labelText.Draw(context, palette.Brush(palette.TextSecondary), new Point(left, y + (RowLine - labelText.Height) / 2));
    }
}
