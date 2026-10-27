using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Which token fills a readout card.</summary>
internal enum AnalyticsReadoutSurface
{
    /// <summary>SurfaceRaised (most cards).</summary>
    SurfaceRaised,

    /// <summary>Surface (Journey flow; Uptime light).</summary>
    Surface
}

/// <summary>Which shadow token a readout card casts.</summary>
internal enum AnalyticsReadoutShadow
{
    /// <summary>ShadowRaised (Metrics dashboard, KPI drilldown, Journey flow).</summary>
    Raised,

    /// <summary>ShadowFloating (Revenue explorer, Cohort retention, Connected scatter, Uptime, Choropleth).</summary>
    Floating
}

/// <summary>
/// The look of a drawn readout card (all lengths in px). Presets carry the measurements of each block; copy one with
/// <c>with { … }</c> to adjust. Text: title line, optional big value line, then rows (swatch, label left, value right).
/// </summary>
internal sealed record AnalyticsReadoutStyle
{
    /// <summary>Fixed card width; 0 sizes the card to its content (at least <see cref="MinWidth"/>).</summary>
    public double Width { get; init; }

    /// <summary>Smallest width when sized to content.</summary>
    public double MinWidth { get; init; } = 96;

    /// <summary>Inner padding.</summary>
    public Thickness Padding { get; init; } = new(10, 7);

    /// <summary>Corner radius.</summary>
    public double CornerRadius { get; init; } = 10;

    /// <summary>Fill token.</summary>
    public AnalyticsReadoutSurface Surface { get; init; } = AnalyticsReadoutSurface.SurfaceRaised;

    /// <summary>Shadow token.</summary>
    public AnalyticsReadoutShadow Shadow { get; init; } = AnalyticsReadoutShadow.Raised;

    /// <summary>Title font size (12).</summary>
    public double TitleSize { get; init; } = 12;

    /// <summary>Title line height (16.8).</summary>
    public double TitleLineHeight { get; init; } = 16.8;

    /// <summary>Title in Medium weight and Foreground (Revenue explorer's month) instead of regular TextMuted.</summary>
    public bool TitleIsStrong { get; init; }

    /// <summary>Title in TextSecondary instead of TextMuted (Journey flow, Cohort retention, Uptime).</summary>
    public bool TitleIsSecondary { get; init; }

    /// <summary>Space under the title (0; Revenue explorer 8).</summary>
    public double TitleGap { get; init; }

    /// <summary>Value font size (14, Medium, tabular, Foreground).</summary>
    public double ValueSize { get; init; } = 14;

    /// <summary>Value line height (18.2).</summary>
    public double ValueLineHeight { get; init; } = 18.2;

    /// <summary>Space under the value line (0; Journey flow 6).</summary>
    public double ValueGap { get; init; }

    /// <summary>Row font size (12).</summary>
    public double RowSize { get; init; } = 12;

    /// <summary>Row height (16.8; Revenue explorer 22).</summary>
    public double RowHeight { get; init; } = 16.8;

    /// <summary>Row labels in TextSecondary (true) or TextMuted (false).</summary>
    public bool RowLabelIsSecondary { get; init; }

    /// <summary>Swatch size (8) and radius (2).</summary>
    public double SwatchSize { get; init; } = 8;

    /// <summary>Swatch corner radius.</summary>
    public double SwatchRadius { get; init; } = 2;

    /// <summary>Gap between a row's label and value (12) and between swatch and label (8).</summary>
    public double ColumnGap { get; init; } = 12;

    /// <summary>
    /// Metrics dashboard / KPI drilldown: min width 96, padding 7/10, radius 10, SurfaceRaised, ShadowRaised; date 12
    /// TextMuted, value 14/500, "previous" row 12 TextMuted.
    /// </summary>
    public static AnalyticsReadoutStyle Compact { get; } = new();

    /// <summary>
    /// Revenue explorer: width 204, padding 10/12, radius 14, ShadowFloating; month title 14/500 Foreground with 8 below;
    /// rows 22 high, 12 TextSecondary label with an 8×8 r2 swatch, 12 tabular Foreground value.
    /// </summary>
    public static AnalyticsReadoutStyle Panel { get; } = new()
    {
        Width = 204, Padding = new Thickness(12, 10), CornerRadius = 14, Shadow = AnalyticsReadoutShadow.Floating,
        TitleSize = 14, TitleLineHeight = 19.6, TitleIsStrong = true, TitleGap = 8, RowHeight = 22, RowLabelIsSecondary = true
    };

    /// <summary>Cohort retention: width 200, padding 12/14, radius 16, ShadowFloating, 12 TextSecondary.</summary>
    public static AnalyticsReadoutStyle Card { get; } = new()
    {
        Width = 200, Padding = new Thickness(14, 12), CornerRadius = 16, Shadow = AnalyticsReadoutShadow.Floating,
        TitleIsSecondary = true, ValueSize = 22, ValueLineHeight = 28.6, ValueGap = 6, RowLabelIsSecondary = true
    };

    /// <summary>Journey flow: width 212, padding 10/12/11, radius 12, Surface, ShadowRaised; value 15/21 with 6 below.</summary>
    public static AnalyticsReadoutStyle Flow { get; } = new()
    {
        Width = 212, Padding = new Thickness(12, 10, 12, 11), CornerRadius = 12, Surface = AnalyticsReadoutSurface.Surface,
        TitleIsSecondary = true, TitleGap = 2, ValueSize = 15, ValueLineHeight = 21, ValueGap = 6, RowLabelIsSecondary = true
    };

    /// <summary>Connected scatter: width 176, padding 10/12, radius 14, ShadowFloating.</summary>
    public static AnalyticsReadoutStyle Scatter { get; } = new()
    {
        Width = 176, Padding = new Thickness(12, 10), CornerRadius = 14, Shadow = AnalyticsReadoutShadow.Floating, RowLabelIsSecondary = true
    };

    /// <summary>Uptime status bars: width 256, padding 10/12, radius 12, ShadowFloating (Surface in light).</summary>
    public static AnalyticsReadoutStyle Uptime { get; } = new()
    {
        Width = 256, Padding = new Thickness(12, 10), CornerRadius = 12, Shadow = AnalyticsReadoutShadow.Floating,
        TitleIsSecondary = true, RowLabelIsSecondary = true
    };
}

/// <summary>One row of a readout card.</summary>
/// <param name="Label">Left text.</param>
/// <param name="Value">Right text (tabular).</param>
/// <param name="Swatch">Swatch colour, or null for none.</param>
/// <param name="IsEmphasized">Label and value in Medium Foreground (Revenue explorer's "Net new").</param>
/// <param name="HasRule">A 1px BorderSubtle rule above the row with 4px around it.</param>
/// <param name="IsMuted">The whole row in TextMuted (a hidden series, a previous period).</param>
internal readonly record struct AnalyticsReadoutRow(string Label, string? Value = null, Color? Swatch = null,
    bool IsEmphasized = false, bool HasRule = false, bool IsMuted = false);

/// <summary>What a readout card shows.</summary>
/// <param name="Title">First line (date, month, node name).</param>
/// <param name="Value">Optional big value line.</param>
/// <param name="Rows">Rows under it.</param>
internal sealed record AnalyticsReadoutContent(string Title, string? Value = null, IReadOnlyList<AnalyticsReadoutRow>? Rows = null);

/// <summary>
/// A floating readout card drawn into a plot's Render (no popup, no layout): content, position, fade and scale state,
/// placement rules, and drawing. Show it with <see cref="Show"/> at a top-left position computed with the placement
/// helpers, hide it with <see cref="Hide"/>, and call <see cref="Advance"/> from the plot's frame loop. Defaults follow
/// the most common measurements: appear in place (no slide from the old position), fade in 120ms ease-out, fade out
/// 160ms ease-out; while shown the position follows <see cref="FollowSpring"/> (null = jump, Metrics dashboard / KPI
/// drilldown / Journey flow). Revenue explorer and Uptime also scale from 0.97 (set <see cref="EnterScale"/>).
/// Reduced motion keeps the fades and jumps positions; Off jumps everything.
/// </summary>
/// <remarks>Why code-driven: the card lives inside one self-drawn plot and follows a cursor that moves per frame.</remarks>
internal sealed class AnalyticsReadout
{
    private AnalyticsSpringChannel _x = new(0, 0.05, 0.5);
    private AnalyticsSpringChannel _y = new(0, 0.05, 0.5);
    private AnalyticsTween _opacity = new(0);
    private AnalyticsTween _scale = new(1);
    private AnalyticsTween _swap = new(1);
    private AnalyticsReadoutContent? _previous;
    private bool _shown;

    /// <summary>A readout with <paramref name="style"/> (default <see cref="AnalyticsReadoutStyle.Compact"/>).</summary>
    public AnalyticsReadout(AnalyticsReadoutStyle? style = null)
    {
        Style = style ?? AnalyticsReadoutStyle.Compact;
    }

    /// <summary>The look.</summary>
    public AnalyticsReadoutStyle Style { get; set; }

    /// <summary>The content drawn now.</summary>
    public AnalyticsReadoutContent? Content { get; private set; }

    /// <summary>Spring the card follows a new position with while shown; null jumps (default).</summary>
    public SpringSpec? FollowSpring { get; set; }

    /// <summary>Fade-in time (120ms ease-out; Revenue explorer 160ms (.16, 1, .3, 1)).</summary>
    public TimeSpan EnterDuration { get; set; } = ArcMotionPresets.Instant;

    /// <summary>Fade-in easing.</summary>
    public Easing EnterEasing { get; set; } = AnalyticsEasings.EaseOut;

    /// <summary>Fade-out time (160ms ease-out; Uptime 120ms (.22, 1, .36, 1)). Zero removes at once (Journey flow).</summary>
    public TimeSpan ExitDuration { get; set; } = ArcMotionPresets.Fast;

    /// <summary>Fade-out easing.</summary>
    public Easing ExitEasing { get; set; } = AnalyticsEasings.EaseOut;

    /// <summary>Scale the card grows from when it appears (1 = none; Revenue explorer / Uptime 0.97).</summary>
    public double EnterScale { get; set; } = 1;

    /// <summary>Scale the card shrinks to while leaving (1 = none; Revenue explorer 0.97, Uptime 0.98).</summary>
    public double ExitScale { get; set; } = 1;

    /// <summary>
    /// Cross-fade a content change while shown: old 120ms (.22, 1, .36, 1) out, new 240ms (.16, 1, .3, 1) in (Journey flow).
    /// Off by default (text is replaced at once).
    /// </summary>
    public bool CrossfadesContent { get; set; }

    /// <summary>True between <see cref="Show"/> and <see cref="Hide"/>.</summary>
    public bool IsShown => _shown;

    /// <summary>True while drawn (shown, or fading out).</summary>
    public bool IsVisible => _shown || _opacity.Value > 0.001;

    /// <summary>Current top-left position.</summary>
    public Point Position => new(_x.Value, _y.Value);

    /// <summary>Current opacity.</summary>
    public double Opacity => Math.Clamp(_opacity.Value, 0, 1);

    /// <summary>True while a fade, scale or follow is running.</summary>
    public bool IsMoving => _x.IsMoving || _y.IsMoving || _opacity.IsRunning || _scale.IsRunning || _swap.IsRunning;

    /// <summary>Replaces the content (cross-fades when <see cref="CrossfadesContent"/> and shown).</summary>
    public void SetContent(AnalyticsReadoutContent content, ArcMotionLevel level = ArcMotionLevel.Full)
    {
        if (CrossfadesContent && _shown && Content is not null && level != ArcMotionLevel.Off && !Equals(Content, content))
        {
            _previous = Content;
            _swap.Jump(0);
            _swap.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Enter);
        }
        Content = content;
    }

    /// <summary>
    /// Shows the card with its top-left at <paramref name="position"/>. A hidden card appears in place; a shown card
    /// follows on <see cref="FollowSpring"/> (or jumps).
    /// </summary>
    public void Show(Point position, ArcMotionLevel level = ArcMotionLevel.Full)
    {
        var wasVisible = IsVisible;
        if (!wasVisible || FollowSpring is not { } spring || level != ArcMotionLevel.Full)
        {
            _x.Jump(position.X);
            _y.Jump(position.Y);
        }
        else
        {
            _x.SpringTo(position.X, spring);
            _y.SpringTo(position.Y, spring);
        }
        if (_shown)
        {
            return;
        }
        _shown = true;
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(1);
            _scale.Jump(1);
            return;
        }
        if (!wasVisible)
        {
            _opacity.Jump(0);
            _scale.Jump(level == ArcMotionLevel.Full ? EnterScale : 1);
        }
        _opacity.Start(1, EnterDuration, EnterEasing);
        _scale.Start(1, EnterDuration, EnterEasing);
    }

    /// <summary>Fades the card out where it is.</summary>
    public void Hide(ArcMotionLevel level = ArcMotionLevel.Full)
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        _x.Stop();
        _y.Stop();
        if (level == ArcMotionLevel.Off || ExitDuration <= TimeSpan.Zero)
        {
            _opacity.Jump(0);
            _scale.Jump(1);
            return;
        }
        _opacity.Start(0, ExitDuration, ExitEasing);
        _scale.Start(level == ArcMotionLevel.Full ? ExitScale : 1, ExitDuration, ExitEasing);
    }

    /// <summary>Hides at once (Escape, data change).</summary>
    public void Clear()
    {
        _shown = false;
        _opacity.Jump(0);
        _scale.Jump(1);
        _x.Stop();
        _y.Stop();
        _previous = null;
    }

    /// <summary>Advances fades and the follow spring; true while anything moves.</summary>
    public bool Advance(double dt)
    {
        var moving = _x.Step(dt) | _y.Step(dt) | _opacity.Advance(dt) | _scale.Advance(dt) | _swap.Advance(dt);
        if (!_swap.IsRunning)
        {
            _previous = null;
        }
        return moving;
    }

    /// <summary>The card size for the current content (text measured with <paramref name="cache"/>).</summary>
    public Size Measure(AnalyticsTextCache cache, AnalyticsPalette palette) => Content is null ? default : Measure(Content, cache, palette);

    /// <summary>The card size for <paramref name="content"/>.</summary>
    public Size Measure(AnalyticsReadoutContent content, AnalyticsTextCache cache, AnalyticsPalette palette)
    {
        var s = Style;
        var body = new Typeface(palette.BodyFont);
        var medium = new Typeface(palette.BodyFont, FontStyle.Normal, palette.Medium);
        var inner = cache.Get(content.Title, s.TitleIsStrong ? medium : body, s.TitleSize).Width;
        var height = s.TitleLineHeight + s.TitleGap;
        if (content.Value is { } value)
        {
            inner = Math.Max(inner, cache.Get(value, medium, s.ValueSize, true).Width);
            height += s.ValueLineHeight + s.ValueGap;
        }
        if (content.Rows is { } rows)
        {
            foreach (var row in rows)
            {
                var font = row.IsEmphasized ? medium : body;
                var width = cache.Get(row.Label, font, s.RowSize).Width;
                if (row.Swatch is not null)
                {
                    width += s.SwatchSize + 8;
                }
                if (row.Value is { } v)
                {
                    width += s.ColumnGap + cache.Get(v, font, s.RowSize, true).Width;
                }
                inner = Math.Max(inner, width);
                height += s.RowHeight + (row.HasRule ? 8 : 0);
            }
        }
        var w = s.Width > 0 ? s.Width : Math.Max(s.MinWidth, Math.Ceiling(inner + s.Padding.Left + s.Padding.Right));
        return new Size(w, Math.Ceiling(height + s.Padding.Top + s.Padding.Bottom));
    }

    /// <summary>Draws the card at its current position, opacity and scale (nothing when invisible).</summary>
    public void Draw(DrawingContext context, AnalyticsTextCache cache, AnalyticsPalette palette)
    {
        if (!IsVisible || Content is null || !palette.IsReady)
        {
            return;
        }
        var size = Measure(Content, cache, palette);
        var rect = new Rect(Position, size);
        var center = rect.Center;
        var scale = _scale.Value;
        using (context.PushOpacity(Opacity))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            var s = Style;
            var fill = palette.Brush(s.Surface == AnalyticsReadoutSurface.Surface ? palette.Surface : palette.SurfaceRaised);
            var shadow = s.Shadow == AnalyticsReadoutShadow.Floating ? palette.ShadowFloating : palette.ShadowRaised;
            var rounded = new RoundedRect(rect.Deflate(0.5), s.CornerRadius);
            context.DrawRectangle(fill, palette.Pen(palette.Border, 1), rounded, shadow);
            if (_previous is { } previous && _swap.IsRunning)
            {
                // Old content leaves faster (120ms of the 240ms swap) than the new one arrives.
                var outFade = 1 - Math.Clamp(_swap.Value * 2, 0, 1);
                using (context.PushOpacity(outFade))
                {
                    DrawContent(context, previous, rect, cache, palette);
                }
                using (context.PushOpacity(Math.Clamp(_swap.Value, 0, 1)))
                {
                    DrawContent(context, Content, rect, cache, palette);
                }
            }
            else
            {
                DrawContent(context, Content, rect, cache, palette);
            }
        }
    }

    private void DrawContent(DrawingContext context, AnalyticsReadoutContent content, Rect rect, AnalyticsTextCache cache, AnalyticsPalette palette)
    {
        var s = Style;
        var body = new Typeface(palette.BodyFont);
        var medium = new Typeface(palette.BodyFont, FontStyle.Normal, palette.Medium);
        var left = rect.X + s.Padding.Left;
        var right = rect.Right - s.Padding.Right;
        var y = rect.Y + s.Padding.Top;
        var titleColor = s.TitleIsStrong ? palette.Foreground : s.TitleIsSecondary ? palette.TextSecondary : palette.TextMuted;
        var title = cache.GetTrimmed(content.Title, s.TitleIsStrong ? medium : body, s.TitleSize, right - left);
        title.Draw(context, palette.Brush(titleColor), new Point(left, y + (s.TitleLineHeight - title.Height) / 2));
        y += s.TitleLineHeight + s.TitleGap;
        if (content.Value is { } value)
        {
            var text = cache.Get(value, medium, s.ValueSize, true);
            text.Draw(context, palette.Brush(palette.Foreground), new Point(left, y + (s.ValueLineHeight - text.Height) / 2));
            y += s.ValueLineHeight + s.ValueGap;
        }
        if (content.Rows is not { } rows)
        {
            return;
        }
        foreach (var row in rows)
        {
            if (row.HasRule)
            {
                y += 4;
                var ruleY = Math.Floor(y) + 0.5;
                context.DrawLine(palette.Pen(palette.BorderSubtle, 1, cap: PenLineCap.Flat), new Point(left, ruleY), new Point(right, ruleY));
                y += 4;
            }
            var font = row.IsEmphasized ? medium : body;
            var labelColor = row.IsMuted ? palette.TextMuted : row.IsEmphasized ? palette.Foreground : s.RowLabelIsSecondary ? palette.TextSecondary : palette.TextMuted;
            var valueColor = row.IsMuted ? palette.TextMuted : palette.Foreground;
            var x = left;
            if (row.Swatch is { } swatch)
            {
                var sy = y + (s.RowHeight - s.SwatchSize) / 2;
                context.DrawRectangle(palette.Brush(swatch), null, new RoundedRect(new Rect(x, sy, s.SwatchSize, s.SwatchSize), s.SwatchRadius));
                x += s.SwatchSize + 8;
            }
            var valueWidth = 0.0;
            if (row.Value is { } v)
            {
                var valueText = cache.Get(v, font, s.RowSize, true);
                valueWidth = valueText.Width;
                valueText.Draw(context, palette.Brush(valueColor), new Point(right - valueWidth, y + (s.RowHeight - valueText.Height) / 2));
            }
            var label = cache.GetTrimmed(row.Label, font, s.RowSize, Math.Max(1, right - x - (valueWidth > 0 ? valueWidth + s.ColumnGap : 0)));
            label.Draw(context, palette.Brush(labelColor), new Point(x, y + (s.RowHeight - label.Height) / 2));
            y += s.RowHeight;
        }
    }

    // Placement --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Beside an anchor: <c>x = anchor + gap</c>, flipped to <c>anchor − gap − width</c> when it would pass
    /// <paramref name="bounds"/>' right edge (Revenue explorer 14, Choropleth 16, Cohort 9 from the cell box, Journey flow
    /// 14). <paramref name="y"/> is clamped into the bounds.
    /// </summary>
    public static Point Beside(double anchorX, double y, Size card, Rect bounds, double gap = 14)
    {
        var x = anchorX + gap;
        if (x + card.Width > bounds.Right)
        {
            x = anchorX - gap - card.Width;
        }
        return new Point(x, ClampY(y, card.Height, bounds));
    }

    /// <summary>
    /// Centred on an anchor x and clamped so the card stays inside [<paramref name="left"/>, <paramref name="right"/>]
    /// (Metrics dashboard: pixel clamp to the plot; KPI drilldown clamps the centre to [70, W − 70]).
    /// </summary>
    public static double CenteredX(double anchorX, double cardWidth, double left, double right)
    {
        var x = anchorX - cardWidth / 2;
        return right - left <= cardWidth ? left : Math.Clamp(x, left, right - cardWidth);
    }

    /// <summary>
    /// Metrics dashboard: the half of the plot opposite the point, <paramref name="margin"/> (6) from that edge, so the card
    /// never covers the point: a point in the top half puts the card at the bottom and vice versa.
    /// </summary>
    public static double OppositeHalfY(double pointY, double plotTop, double plotBottom, double cardHeight, double margin = 6)
        => pointY - plotTop <= (plotBottom - plotTop) / 2 ? plotBottom - margin - cardHeight : plotTop + margin;

    /// <summary>
    /// Above an anchor with <paramref name="gap"/> (Connected scatter 14, Uptime 8), or below it when there is no room
    /// above; x centred and clamped into <paramref name="bounds"/>.
    /// </summary>
    public static Point AboveOrBelow(Point anchor, Size card, Rect bounds, double gap = 14)
    {
        var x = CenteredX(anchor.X, card.Width, bounds.Left, bounds.Right);
        var y = anchor.Y - gap - card.Height;
        if (y < bounds.Top)
        {
            y = anchor.Y + gap;
        }
        return new Point(x, y);
    }

    /// <summary>
    /// Beside a pointer with independent flips (Journey flow): (pointer + (14, 16)), left of the pointer when it would pass
    /// the right edge, above it when it would pass the bottom; no clamping.
    /// </summary>
    public static Point NearPointer(Point pointer, Size card, Rect bounds, double dx = 14, double dy = 16)
    {
        var x = pointer.X + dx + card.Width > bounds.Right ? pointer.X - dx - card.Width : pointer.X + dx;
        var y = pointer.Y + dy + card.Height > bounds.Bottom ? pointer.Y - dy - card.Height : pointer.Y + dy;
        return new Point(x, y);
    }

    /// <summary>Clamps a top so a card of <paramref name="height"/> stays inside the bounds.</summary>
    public static double ClampY(double y, double height, Rect bounds)
        => bounds.Height <= height ? bounds.Top : Math.Clamp(y, bounds.Top, bounds.Bottom - height);
}
