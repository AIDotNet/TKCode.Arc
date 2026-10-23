using Avalonia;

namespace TKCode.Arc;

/// <summary>Which caption layer a card shows in a layout (the layers fade out and in separately).</summary>
internal enum LayoutMorphCaption
{
    None = -1,

    /// <summary>Grid (captioned): title and subtitle under the picture.</summary>
    Compact = 0,

    /// <summary>List: title and meta beside the thumbnail, value on the right.</summary>
    Row = 1,

    /// <summary>Carousel: title and value, subtitle under them.</summary>
    Detail = 2,

    /// <summary>The opened detail.</summary>
    Sheet = 3
}

/// <summary>
/// Where one card is and what it looks like (Arc Layout morph, design reference). Coordinates are world px with the
/// origin at the stage center (x right, y down, z toward the viewer); the card turns about its center as CSS
/// <c>rotateY(a) rotateX(b) rotateZ(c)</c>. The media rectangle is in the card's border box.
/// </summary>
internal struct LayoutMorphPose
{
    public double X;
    public double Y;
    public double Z;
    public double RotY;
    public double RotX;
    public double RotZ;
    public double Width;
    public double Height;
    public double Radius;
    public double MediaX;
    public double MediaY;
    public double MediaWidth;
    public double MediaHeight;
    public double MediaRadius;

    /// <summary>The layout's own opacity at rest (Carousel cards behind the ring: 0).</summary>
    public double Opacity;

    /// <summary>Hover pull-out along the card's own axes at full lift: up (Stack 60) and out (Stack 36, Helix 28).</summary>
    public double PullY;

    public double PullZ;

    public LayoutMorphCaption Caption;

    /// <summary>False for the opened card: it is drawn flat in front of the receding world, outside the camera.</summary>
    public bool UsesCamera;

    public readonly Point Center => new(X, Y);

    /// <summary>
    /// Linear interpolation of every geometric value (Arc moves them all on one progress); the Y turn takes the
    /// shortest way round (Arc sometimes goes half a turn the long way; the group spec rules that out).
    /// </summary>
    public static LayoutMorphPose Lerp(in LayoutMorphPose a, in LayoutMorphPose b, double t)
    {
        var p = b;
        p.X = a.X + (b.X - a.X) * t;
        p.Y = a.Y + (b.Y - a.Y) * t;
        p.Z = a.Z + (b.Z - a.Z) * t;
        p.RotY = a.RotY + ShowpieceProjection.WrapAngle(b.RotY - a.RotY) * t;
        p.RotX = a.RotX + (b.RotX - a.RotX) * t;
        p.RotZ = a.RotZ + (b.RotZ - a.RotZ) * t;
        p.Width = a.Width + (b.Width - a.Width) * t;
        p.Height = a.Height + (b.Height - a.Height) * t;
        p.Radius = a.Radius + (b.Radius - a.Radius) * t;
        p.MediaX = a.MediaX + (b.MediaX - a.MediaX) * t;
        p.MediaY = a.MediaY + (b.MediaY - a.MediaY) * t;
        p.MediaWidth = a.MediaWidth + (b.MediaWidth - a.MediaWidth) * t;
        p.MediaHeight = a.MediaHeight + (b.MediaHeight - a.MediaHeight) * t;
        p.MediaRadius = a.MediaRadius + (b.MediaRadius - a.MediaRadius) * t;
        return p;
    }
}

/// <summary>
/// The world camera of the 3D layouts: <c>translate3d(0, TranslateY, −Radius) rotateX(Tilt) translate3d(0, FocusY, Radius)</c>.
/// The ring's turn is folded into the card angles (the front position), so the camera has no Y turn of its own.
/// </summary>
internal struct LayoutMorphCamera
{
    public double Radius;
    public double Tilt;
    public double TranslateY;
    public double FocusY;

    public static LayoutMorphCamera Identity => default;

    public readonly ShowpieceTransform3D Transform =>
        ShowpieceTransform3D.Translation(0, FocusY, Radius)
            .Then(ShowpieceTransform3D.RotationX(Tilt))
            .Then(ShowpieceTransform3D.Translation(0, TranslateY, -Radius));

    public static LayoutMorphCamera Lerp(in LayoutMorphCamera a, in LayoutMorphCamera b, double t) => new()
    {
        Radius = a.Radius + (b.Radius - a.Radius) * t,
        Tilt = a.Tilt + (b.Tilt - a.Tilt) * t,
        TranslateY = a.TranslateY + (b.TranslateY - a.TranslateY) * t,
        FocusY = a.FocusY + (b.FocusY - a.FocusY) * t
    };
}

/// <summary>The size class of a stage (Arc's container breakpoints).</summary>
internal readonly struct LayoutMorphMetrics
{
    public LayoutMorphMetrics(double width, double height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        IsNarrow = Width < LayoutMorphEngine.NarrowWidth;
        IsDense = Width < LayoutMorphEngine.DenseWidth;
        Pad = IsDense ? 14 : 24;
        Gap = IsDense ? 8 : 14;
        Perspective = LayoutMorphEngine.PerspectiveFor(Width);
    }

    public double Width { get; }

    public double Height { get; }

    /// <summary>Below 720: icon-only view bar, framed stage and panel, stacked detail.</summary>
    public bool IsNarrow { get; }

    /// <summary>Below 520: padding 14, gaps 8, smaller radii, four masonry columns.</summary>
    public bool IsDense { get; }

    public double Pad { get; }

    public double Gap { get; }

    public double Perspective { get; }

    public double InnerWidth => Width - 2 * Pad;

    public double InnerHeight => Height - 2 * Pad;
}

/// <summary>The grid's shape for a stage: columns, rows, card size and whether captions show.</summary>
internal readonly record struct LayoutMorphGridShape(int Columns, int Rows, double CardWidth, double CardHeight, bool IsCaptioned);

/// <summary>Where the opened detail's text sits inside the card (padding-box insets) and how it is laid out.</summary>
internal readonly record struct LayoutMorphSheetLayout(double Left, double Top, double Right, double Bottom, bool IsStacked)
{
    /// <summary>The sheet rectangle in a card of the given border-box size (border 1px).</summary>
    public Rect In(double width, double height) =>
        new(1 + Left, 1 + Top, Math.Max(0, width - 2 - Left - Right), Math.Max(0, height - 2 - Top - Bottom));
}

/// <summary>
/// Layout morph's pure geometry (Arc Layout morph, design reference; measurements in
/// <c>design/components/blocks/showpieces/layout-morph.md</c> sections 4 and 8): the six layouts and their breakpoints,
/// the opened detail, the distance stagger, the flight lift and tilt, the ring visibility and the release snap.
/// </summary>
internal static class LayoutMorphEngine
{
    /// <summary>Below this container width the block is narrow (strictly less).</summary>
    public const double NarrowWidth = 720;

    /// <summary>Below this width the layouts use padding 14 and gaps 8.</summary>
    public const double DenseWidth = 520;

    /// <summary>Total stagger of a layout change: the farthest card starts this much after the change.</summary>
    public static readonly TimeSpan DefaultStagger = TimeSpan.FromMilliseconds(330);

    /// <summary>The captioned 8-column grid needs cells at least this wide (Arc: 112 at 1044, squares at 1035).</summary>
    public const double CaptionedGridMinCell = 112;

    /// <summary>A captioned grid card is this much taller than wide (two 16px caption rows and their spacing).</summary>
    public const double CaptionHeight = 40;

    /// <summary>Masonry keeps 8 or 6 columns while they are at least this wide (Arc: between 105.5 and 107.8).</summary>
    public const double MasonryMinColumn = 106;

    /// <summary>List rows: 56 high, 8 apart; a column needs about 304px.</summary>
    public const double ListRowHeight = 56;

    public const double ListRowGap = 8;

    /// <summary>The carousel's visibility window: opaque within 0.80 rad of the front, gone beyond 1.15 rad.</summary>
    public const double CarouselFadeStart = 0.80;

    public const double CarouselFadeEnd = 1.15;

    /// <summary>Helix: ten cards per turn, tilted 0.24 rad toward the viewer.</summary>
    public const int HelixCardsPerTurn = 10;

    public const double HelixTilt = -0.24;

    /// <summary>Opacity of helix cards that face away (a step, not a fade).</summary>
    public const double HelixBackOpacity = 0.72;

    /// <summary>Opacity of the other cards while one is open.</summary>
    public const double OpenDimOpacity = 0.12;

    /// <summary>How far the world recedes while a card is open (the open card comes forward by the same amount).</summary>
    public const double OpenRecede = 360;

    /// <summary>Flight lift: 0.1 × the travel distance at the peak of the progress velocity.</summary>
    public const double FlightLiftPerPixel = 0.1;

    /// <summary>Flight tilt: 6.2e-5 rad per px of horizontal travel at the peak, leaning into the motion.</summary>
    public const double FlightTiltPerPixel = 6.2e-5;

    /// <summary>
    /// e/ω of the progress spring (ω = 10): the velocity of a critically damped 0 → 1 spring is ω²t·e^(−ωt), whose
    /// peak is ω/e, so lift = 0.1·D·(e/ω)·velocity has the measured shape 0.1·D·(ωt)·e^(1−ωt).
    /// </summary>
    public const double FlightShapeScale = Math.E / 10;

    /// <summary>Release projection of the carousel and helix (Arc ≈ 0.24–0.25s).</summary>
    public const double SnapHorizon = 0.245;

    /// <summary>The default stage height for a container width: 600, 540 at 720 and below, 500 below 480, 460 below 360.</summary>
    public static double StageHeightFor(double width) =>
        width < 360 ? 460 : width < 480 ? 500 : width <= NarrowWidth ? 540 : 600;

    /// <summary>The stage's perspective distance: 1.15 × its width, between 900 and 1500.</summary>
    public static double PerspectiveFor(double width) => Math.Clamp(1.15 * width, 900, 1500);

    /// <summary>The card a layout starts with in front (Carousel: the third, Helix: the middle one).</summary>
    public static int DefaultFront(LayoutMorphMode mode, int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        return mode == LayoutMorphMode.Helix ? (count - 1) / 2 : Math.Min(2, count - 1);
    }

    /// <summary>The first card the keyboard reaches (Arc's initial tab stop is the third card).</summary>
    public static int DefaultTabStop(int count) => count <= 0 ? -1 : Math.Min(2, count - 1);

    // Layouts ----------------------------------------------------------------------------------------------------------

    /// <summary>The target pose of every card and the camera for <paramref name="mode"/>.</summary>
    /// <param name="mode">The layout.</param>
    /// <param name="m">The stage's size class.</param>
    /// <param name="aspects">Each item's height / width (masonry).</param>
    /// <param name="front">The continuous index of the card in front (carousel and helix).</param>
    /// <param name="poses">Receives one pose per card.</param>
    /// <param name="camera">Receives the world camera (identity except for the helix).</param>
    /// <param name="scrollRange">How far the list can scroll (0 when it fits).</param>
    public static void Arrange(LayoutMorphMode mode, in LayoutMorphMetrics m, ReadOnlySpan<double> aspects, double front,
        Span<LayoutMorphPose> poses, out LayoutMorphCamera camera, out double scrollRange)
    {
        camera = LayoutMorphCamera.Identity;
        scrollRange = 0;
        if (poses.Length == 0)
        {
            return;
        }
        switch (mode)
        {
            case LayoutMorphMode.Masonry:
                ArrangeMasonry(m, aspects, poses);
                break;
            case LayoutMorphMode.List:
                scrollRange = ArrangeList(m, poses);
                break;
            case LayoutMorphMode.Stack:
                ArrangeStack(m, poses);
                break;
            case LayoutMorphMode.Carousel:
                ArrangeCarousel(m, front, poses);
                break;
            case LayoutMorphMode.Helix:
                camera = ArrangeHelix(m, front, poses);
                break;
            default:
                ArrangeGrid(m, poses);
                break;
        }
    }

    private static LayoutMorphPose Card(double cx, double cy, double w, double h, double radius)
    {
        return new LayoutMorphPose
        {
            X = cx,
            Y = cy,
            Width = w,
            Height = h,
            Radius = radius,
            Opacity = 1,
            UsesCamera = true,
            Caption = LayoutMorphCaption.None,
            // Full-bleed media: the padding box inside the 1px border, with the inner radius.
            MediaX = 1,
            MediaY = 1,
            MediaWidth = Math.Max(0, w - 2),
            MediaHeight = Math.Max(0, h - 2),
            MediaRadius = Math.Max(0, radius - 1)
        };
    }

    /// <summary>
    /// Grid shape: 8 captioned columns while a cell is at least 112 wide (cell = min(⌊(W − 2p − 7g)/8⌋,
    /// ⌊(H − 2p − (r − 1)g)/r⌋ − 40)); otherwise square cards in 6, 5 or 4 columns, whichever gives the largest cell
    /// (ties keep more columns): 6 × 4 from 1035 to 648, 5 × 5 at 630, 4 × 6 at 388 and below.
    /// </summary>
    public static LayoutMorphGridShape GridShape(in LayoutMorphMetrics m, int count)
    {
        count = Math.Max(1, count);
        var p = m.Pad;
        var g = m.Gap;
        var rows8 = (count + 7) / 8;
        var cell8 = Math.Min(Math.Floor((m.Width - 2 * p - 7 * g) / 8),
            Math.Floor((m.Height - 2 * p - (rows8 - 1) * g) / rows8) - CaptionHeight);
        if (cell8 >= CaptionedGridMinCell)
        {
            return new LayoutMorphGridShape(8, rows8, cell8, cell8 + CaptionHeight, true);
        }
        var bestCols = 6;
        var bestRows = 1;
        var best = double.NegativeInfinity;
        for (var cols = 6; cols >= 3; cols--)
        {
            var rows = (count + cols - 1) / cols;
            var cell = Math.Min(Math.Floor((m.Width - 2 * p - (cols - 1) * g) / cols),
                Math.Floor((m.Height - 2 * p - (rows - 1) * g) / rows));
            if (cell > best)
            {
                best = cell;
                bestCols = cols;
                bestRows = rows;
            }
        }
        best = Math.Max(16, best);
        return new LayoutMorphGridShape(bestCols, bestRows, best, best, false);
    }

    private static void ArrangeGrid(in LayoutMorphMetrics m, Span<LayoutMorphPose> poses)
    {
        var n = poses.Length;
        var shape = GridShape(m, n);
        var g = m.Gap;
        var cols = Math.Min(shape.Columns, n);
        var rows = (n + shape.Columns - 1) / shape.Columns;
        var w = shape.CardWidth;
        var h = shape.CardHeight;
        var left = -(cols * w + (cols - 1) * g) / 2;
        var top = -(rows * h + (rows - 1) * g) / 2;
        var radius = shape.IsCaptioned ? 16 : m.IsDense ? 10 : 12;
        for (var i = 0; i < n; i++)
        {
            var col = i % shape.Columns;
            var row = i / shape.Columns;
            var pose = Card(left + col * (w + g) + w / 2, top + row * (h + g) + h / 2, w, h, radius);
            if (shape.IsCaptioned)
            {
                // The picture sits 5px inside the border (Arc: 6px from the outer edge, w − 10 square, radius 11).
                pose.MediaX = 6;
                pose.MediaY = 6;
                pose.MediaWidth = w - 10;
                pose.MediaHeight = w - 10;
                pose.MediaRadius = 11;
                pose.Caption = LayoutMorphCaption.Compact;
            }
            poses[i] = pose;
        }
    }

    /// <summary>Masonry columns: 4 when dense, else 8 or 6 while a column stays at least 106 wide, else 5.</summary>
    public static int MasonryColumns(in LayoutMorphMetrics m)
    {
        if (m.IsDense)
        {
            return 4;
        }
        if ((m.InnerWidth - 7 * m.Gap) / 8 >= MasonryMinColumn)
        {
            return 8;
        }
        return (m.InnerWidth - 5 * m.Gap) / 6 >= MasonryMinColumn ? 6 : 5;
    }

    /// <summary>
    /// Masonry: each card goes, in data order, into the shortest column (leftmost on ties) at the column width × its
    /// aspect ratio. When the tallest column is taller than the inner box, cards and gaps all shrink by the same
    /// factor so it fits exactly; the block is centered vertically.
    /// </summary>
    private static void ArrangeMasonry(in LayoutMorphMetrics m, ReadOnlySpan<double> aspects, Span<LayoutMorphPose> poses)
    {
        var n = poses.Length;
        var cols = MasonryColumns(m);
        var g = m.Gap;
        var cw = (m.InnerWidth - (cols - 1) * g) / cols;
        Span<double> heights = stackalloc double[cols];
        Span<int> column = n <= 256 ? stackalloc int[n] : new int[n];
        Span<double> offset = n <= 256 ? stackalloc double[n] : new double[n];
        for (var i = 0; i < n; i++)
        {
            var j = 0;
            for (var k = 1; k < cols; k++)
            {
                if (heights[k] < heights[j] - 1e-9)
                {
                    j = k;
                }
            }
            column[i] = j;
            offset[i] = heights[j];
            heights[j] += cw * Aspect(aspects, i) + g;
        }
        var tallest = 0.0;
        for (var k = 0; k < cols; k++)
        {
            tallest = Math.Max(tallest, heights[k] - g);
        }
        var s = tallest > m.InnerHeight && tallest > 0 && m.InnerHeight > 0 ? m.InnerHeight / tallest : 1;
        var top = -tallest * s / 2;
        var left = -m.Width / 2 + m.Pad;
        var radius = m.IsDense ? 10 : 12;
        for (var i = 0; i < n; i++)
        {
            var h = cw * Aspect(aspects, i) * s;
            poses[i] = Card(left + column[i] * (cw + g) + cw / 2, top + offset[i] * s + h / 2, cw, h, radius);
        }
    }

    private static double Aspect(ReadOnlySpan<double> aspects, int i)
    {
        var a = i < aspects.Length ? aspects[i] : 1;
        return double.IsFinite(a) && a > 0 ? Math.Clamp(a, 0.25, 4) : 1;
    }

    /// <summary>List columns: clamp(⌊(W − 34) / 304⌋, 1, 3).</summary>
    public static int ListColumns(in LayoutMorphMetrics m) => Math.Clamp((int)Math.Floor((m.Width - 34) / 304), 1, 3);

    /// <summary>
    /// List: filled column by column (8 rows each for 24 cards in 3 columns), rows 56 high and 8 apart. A list that fits
    /// is centered; a taller one starts at the top padding and scrolls (returns the scroll range).
    /// </summary>
    private static double ArrangeList(in LayoutMorphMetrics m, Span<LayoutMorphPose> poses)
    {
        var n = poses.Length;
        var cols = ListColumns(m);
        var rows = (n + cols - 1) / cols;
        var g = m.Gap;
        var w = (m.InnerWidth - (cols - 1) * g) / cols;
        var content = rows * ListRowHeight + (rows - 1) * ListRowGap;
        var fits = content <= m.InnerHeight + 0.01;
        var top = fits ? -content / 2 : -m.Height / 2 + m.Pad;
        var left = -m.Width / 2 + m.Pad;
        for (var i = 0; i < n; i++)
        {
            var col = i / rows;
            var row = i % rows;
            var pose = Card(left + col * (w + g) + w / 2, top + row * (ListRowHeight + ListRowGap) + ListRowHeight / 2,
                w, ListRowHeight, 14);
            pose.MediaX = 7;
            pose.MediaY = 7;
            pose.MediaWidth = 44;
            pose.MediaHeight = 44;
            pose.MediaRadius = 9;
            pose.Caption = LayoutMorphCaption.Row;
            poses[i] = pose;
        }
        return fits ? 0 : content - m.InnerHeight;
    }

    /// <summary>Stack card: w = clamp(0.19·W, 96, 200) rounded, h = 1.36·w rounded.</summary>
    public static Size StackCardSize(in LayoutMorphMetrics m)
    {
        var w = Math.Round(Math.Clamp(0.19 * m.Width, 96, 200));
        return new Size(w, Math.Round(1.36 * w));
    }

    /// <summary>
    /// The fan's half angle: 0.65 rad, narrowed so the outermost center stays K from the stage edge (R·sin θ ≤ W/2 − K,
    /// K = pad + w/2; dense K = pad + 0.8·w, fitted to Arc at 447, 358 and 327).
    /// </summary>
    public static double StackHalfAngle(in LayoutMorphMetrics m)
    {
        var size = StackCardSize(m);
        var radius = 2.3 * size.Height;
        var k = m.Pad + (m.IsDense ? 0.8 : 0.5) * size.Width;
        var room = (m.Width / 2 - k) / radius;
        if (room >= Math.Sin(0.65))
        {
            return 0.65;
        }
        return Math.Max(0.05, Math.Asin(Math.Clamp(room, 0, 1)));
    }

    /// <summary>
    /// Stack: card i turned θᵢ = −θm + 2θm·i/(n − 1) with its center on a circle of radius 2.3·h (625 for 272): x =
    /// R·sin θ, y = C − R·cos θ, C centering the fan vertically (from the middle card's top edge to the outer cards'
    /// lowest corner). Later cards lie on
    /// top (z 0.8 per card). Hovering pulls a card 60 up and 36 out along its own axes.
    /// </summary>
    private static void ArrangeStack(in LayoutMorphMetrics m, Span<LayoutMorphPose> poses)
    {
        var n = poses.Length;
        var size = StackCardSize(m);
        double w = size.Width, h = size.Height;
        var radius = 2.3 * h;
        var half = StackHalfAngle(m);
        double top = double.PositiveInfinity, bottom = double.NegativeInfinity;
        for (var i = 0; i < n; i++)
        {
            var theta = n == 1 ? 0 : -half + 2 * half * i / (n - 1);
            var cy = -radius * Math.Cos(theta);
            // Arc's fan box: the highest center's unturned top edge and the lowest turned corner (fits 1312, 447, 358).
            top = Math.Min(top, cy - h / 2);
            bottom = Math.Max(bottom, cy + h / 2 * Math.Cos(theta) + w / 2 * Math.Abs(Math.Sin(theta)));
        }
        var c = -(top + bottom) / 2;
        for (var i = 0; i < n; i++)
        {
            var theta = n == 1 ? 0 : -half + 2 * half * i / (n - 1);
            var pose = Card(radius * Math.Sin(theta), c - radius * Math.Cos(theta), w, h, 14);
            pose.RotZ = theta;
            pose.Z = 0.8 * i;
            pose.PullY = 60;
            pose.PullZ = 36;
            poses[i] = pose;
        }
    }

    /// <summary>Carousel card: w = clamp(0.3·W, 210, 320) rounded, h = w + 76.</summary>
    public static Size CarouselCardSize(in LayoutMorphMetrics m)
    {
        var w = Math.Round(Math.Clamp(0.3 * m.Width, 210, 320));
        return new Size(w, w + 76);
    }

    /// <summary>Carousel ring radius: max(720, 1.1·W).</summary>
    public static double CarouselRadius(in LayoutMorphMetrics m) => Math.Max(720, 1.1 * m.Width);

    /// <summary>Angle between neighbours on the ring: (w + 1.6·gap) / R (Arc: 342.4 / 1188 = 0.2882 at 1080).</summary>
    public static double CarouselStep(in LayoutMorphMetrics m) => (CarouselCardSize(m).Width + 1.6 * m.Gap) / CarouselRadius(m);

    /// <summary>
    /// Carousel visibility at an angle from the front: smoothstep((1.15 − |φ|) / 0.35). The angle is not wrapped: the
    /// carousel is a strip from the first card to the last laid on the ring, so a card 21 places away stays hidden even
    /// where the ring brings it round beside the front (Arc: card 23 sits at −0.23 rad of card 2, at opacity 0).
    /// </summary>
    public static double CarouselOpacity(double angle)
    {
        var x = (CarouselFadeEnd - Math.Abs(angle)) / (CarouselFadeEnd - CarouselFadeStart);
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    /// <summary>
    /// Carousel: the cards in data order on a ring of radius R around (0, 0, −R), φ = (i − front) × step, at
    /// (R·sin φ, 0, R·(cos φ − 1)) turned φ about Y; captions under the picture. The strip has two ends (nothing comes
    /// round before the first card or after the last; Arc Layout morph, design reference): the front stays between them.
    /// </summary>
    private static void ArrangeCarousel(in LayoutMorphMetrics m, double front, Span<LayoutMorphPose> poses)
    {
        var n = poses.Length;
        var size = CarouselCardSize(m);
        double w = size.Width, h = size.Height;
        var radius = CarouselRadius(m);
        var step = (w + 1.6 * m.Gap) / radius;
        for (var i = 0; i < n; i++)
        {
            var phi = (i - front) * step;
            var pose = Card(radius * Math.Sin(phi), 0, w, h, 22);
            pose.Z = radius * (Math.Cos(phi) - 1);
            pose.RotY = phi;
            pose.MediaX = 9;
            pose.MediaY = 9;
            pose.MediaWidth = w - 16;
            pose.MediaHeight = w - 16;
            pose.MediaRadius = 15;
            pose.Caption = LayoutMorphCaption.Detail;
            pose.Opacity = CarouselOpacity(phi);
            poses[i] = pose;
        }
    }

    /// <summary>Helix card: w = clamp(0.15·W, 66, 160) rounded, h = 1.3·w rounded.</summary>
    public static Size HelixCardSize(in LayoutMorphMetrics m)
    {
        var w = Math.Round(Math.Clamp(0.15 * m.Width, 66, 160));
        return new Size(w, Math.Round(1.3 * w));
    }

    /// <summary>Helix radius: ten cards and gaps per turn, (w + gap) / (2·sin(π/10)) (281.54 at 1080).</summary>
    public static double HelixRadius(in LayoutMorphMetrics m) => (HelixCardSize(m).Width + m.Gap) / (2 * Math.Sin(Math.PI / HelixCardsPerTurn));

    /// <summary>Helix drop per card: 0.15 × (h + gap) (33.3 at 1080, 1.5 card heights and gaps per turn).</summary>
    public static double HelixStep(in LayoutMorphMetrics m) => 0.15 * (HelixCardSize(m).Height + m.Gap);

    /// <summary>The helix angle between neighbours: 2π / 10.</summary>
    public static double HelixAngleStep => 2 * Math.PI / HelixCardsPerTurn;

    /// <summary>A helix card faces away when its angle from the front is more than a quarter turn.</summary>
    public static bool IsHelixBack(double angle) => Math.Abs(ShowpieceProjection.WrapAngle(angle)) > Math.PI / 2 + 1e-9;

    /// <summary>
    /// Helix: card i at θ = (i − front)·2π/10 on a cylinder of radius R, its center (i − (n − 1)/2)·step down. The
    /// camera tilts −0.24 rad about the front card and follows it down the spiral, so the front card stays centered.
    /// </summary>
    private static LayoutMorphCamera ArrangeHelix(in LayoutMorphMetrics m, double front, Span<LayoutMorphPose> poses)
    {
        var n = poses.Length;
        var size = HelixCardSize(m);
        double w = size.Width, h = size.Height;
        var radius = HelixRadius(m);
        var step = HelixStep(m);
        var mid = (n - 1) / 2.0;
        var cardRadius = m.IsDense ? 9 : 12;
        for (var i = 0; i < n; i++)
        {
            var theta = (i - front) * HelixAngleStep;
            var pose = Card(radius * Math.Sin(theta), (i - mid) * step, w, h, cardRadius);
            pose.Z = radius * (Math.Cos(theta) - 1);
            pose.RotY = theta;
            pose.PullZ = 28;
            pose.Opacity = IsHelixBack(theta) ? HelixBackOpacity : 1;
            poses[i] = pose;
        }
        return HelixCamera(m, front, n);
    }

    /// <summary>The helix camera with <paramref name="front"/> in front: tilt −0.24, ty = R·sin(tilt), fy = −(front's y).</summary>
    public static LayoutMorphCamera HelixCamera(in LayoutMorphMetrics m, double front, int count)
    {
        var radius = HelixRadius(m);
        return new LayoutMorphCamera
        {
            Radius = radius,
            Tilt = HelixTilt,
            TranslateY = radius * Math.Sin(HelixTilt),
            FocusY = -(front - (count - 1) / 2.0) * HelixStep(m)
        };
    }

    /// <summary>Arc length the pointer covers per card on the ring (carousel: R·step; helix: R·2π/10).</summary>
    public static double ArcPerCard(LayoutMorphMode mode, in LayoutMorphMetrics m) => mode switch
    {
        LayoutMorphMode.Carousel => CarouselRadius(m) * CarouselStep(m),
        LayoutMorphMode.Helix => HelixRadius(m) * HelixAngleStep,
        _ => 0
    };

    /// <summary>
    /// The card a release lands on: the front position moved by the release velocity over 0.245s, rounded (slow 80px
    /// drags spring back; 300px at 1500px/s goes two cards, at 3400px/s three).
    /// </summary>
    /// <param name="front">The continuous index of the card in front at the release.</param>
    /// <param name="velocityPx">Pointer velocity in px/s (positive = to the right, which brings earlier cards in).</param>
    /// <param name="arcPerCard">Pointer travel per card (<see cref="ArcPerCard"/>).</param>
    public static double SnapFront(double front, double velocityPx, double arcPerCard)
    {
        if (!(arcPerCard > 0))
        {
            return Math.Round(front);
        }
        return Math.Round(front - velocityPx * SnapHorizon / arcPerCard);
    }

    /// <summary>
    /// The front position shown while a drag pulls the carousel or the helix past its first or last card: the pointer's
    /// travel beyond the end meets rubber-band resistance over <paramref name="dimension"/> (Arc: 600px past either end
    /// shows about 200px in both views, which the list's curve over the stage's inner height reproduces).
    /// </summary>
    /// <param name="raw">The front position the pointer asks for (cards; below 0 or above count − 1 past the ends).</param>
    /// <param name="count">The number of cards.</param>
    /// <param name="arcPerCard">Pointer travel per card (<see cref="ArcPerCard"/>).</param>
    /// <param name="dimension">The rubber band's dimension in px.</param>
    public static double StretchFront(double raw, int count, double arcPerCard, double dimension)
    {
        if (count <= 0 || !(arcPerCard > 0))
        {
            return raw;
        }
        return TKCode.Arc.Gestures.RubberBand.Clamp(raw * arcPerCard, 0, (count - 1) * arcPerCard, dimension) / arcPerCard;
    }

    // Opened detail ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The opened card, flat and centered. Wide: hero (W − 140) × (H − 40), radius 28, picture 8px in, (w − 356) ×
    /// (h − 16), radius 20, text from 22px right of it; profile 640 × 392 with a 196 × 236 picture 20px in, radius 16,
    /// text from 240. Narrow: the inner box (W − 24) × (H − 24), radius 24, picture on top (radius 18), text under it.
    /// </summary>
    public static LayoutMorphPose OpenPose(in LayoutMorphMetrics m, LayoutMorphOpenVariant variant, out LayoutMorphSheetLayout sheet)
    {
        LayoutMorphPose pose;
        if (m.IsNarrow)
        {
            var w = Math.Max(120, m.Width - 24);
            var h = Math.Max(160, m.Height - 24);
            pose = Card(0, 0, w, h, 24);
            pose.MediaX = 9;
            pose.MediaY = 9;
            pose.MediaWidth = w - 16;
            pose.MediaHeight = Math.Round(0.461 * h);
            pose.MediaRadius = 18;
            sheet = new LayoutMorphSheetLayout(20, 8 + pose.MediaHeight + 12.6, 20, 70, true);
        }
        else if (variant == LayoutMorphOpenVariant.Hero)
        {
            var w = Math.Max(480, m.Width - 140);
            var h = Math.Max(320, m.Height - 40);
            pose = Card(0, 0, w, h, 28);
            pose.MediaX = 9;
            pose.MediaY = 9;
            pose.MediaWidth = Math.Max(w * 0.42, w - 356);
            pose.MediaHeight = h - 16;
            pose.MediaRadius = 20;
            sheet = new LayoutMorphSheetLayout(8 + pose.MediaWidth + 22, 30, 28, 76, false);
        }
        else
        {
            var w = Math.Min(640, m.Width - 40);
            var h = Math.Min(392, m.Height - 40);
            pose = Card(0, 0, w, h, 28);
            pose.MediaX = 21;
            pose.MediaY = 21;
            pose.MediaWidth = 196;
            pose.MediaHeight = Math.Min(236, h - 42);
            pose.MediaRadius = 16;
            sheet = new LayoutMorphSheetLayout(240, 24, 28, 76, false);
        }
        pose.Caption = LayoutMorphCaption.Sheet;
        pose.UsesCamera = false;
        return pose;
    }

    // Stagger and flight ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Start delays of a layout change: each card waits <paramref name="total"/> × its distance to the origin / the
    /// largest distance (so even the nearest card waits a little; the farthest waits the whole total).
    /// </summary>
    public static void StaggerDelays(ReadOnlySpan<Point> starts, Point origin, double total, Span<double> delays)
    {
        var max = 0.0;
        for (var i = 0; i < starts.Length; i++)
        {
            var d = Distance(starts[i], origin);
            delays[i] = d;
            max = Math.Max(max, d);
        }
        for (var i = 0; i < starts.Length; i++)
        {
            delays[i] = max > 1e-9 && total > 0 ? total * delays[i] / max : 0;
        }
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The lift toward the viewer during a flight of <paramref name="travel"/> px at progress velocity <paramref name="velocity"/> (1/s).</summary>
    public static double FlightLift(double travel, double velocity) => FlightLiftPerPixel * travel * FlightShapeScale * Math.Max(0, velocity);

    /// <summary>The lean (rad, clockwise for travel to the right) during a flight of <paramref name="dx"/> px.</summary>
    public static double FlightTilt(double dx, double velocity) => FlightTiltPerPixel * dx * FlightShapeScale * Math.Max(0, velocity);

    // Covers -----------------------------------------------------------------------------------------------------------

    /// <summary>A cover's inset: 0.11 × its short side (11.66 on a 106 cover, 21.56 on the 196 detail).</summary>
    public static double CoverPadding(double shortSide) => 0.11 * shortSide;

    /// <summary>A cover's title size: 0.13 × the short side, between 10 and 20.</summary>
    public static double CoverTitleSize(double shortSide) => Math.Clamp(0.13 * shortSide, 10, 20);

    /// <summary>A cover's value size: 0.12 × the short side, between 10 and 18.</summary>
    public static double CoverValueSize(double shortSide) => Math.Clamp(0.12 * shortSide, 10, 18);

    // Projection -----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The plane transform of a card: its border box (0..w, 0..h) to world space, with the hover pull, the flight
    /// lean and lift, then the camera, the list scroll and the open recede for cards inside the world.
    /// </summary>
    public static ShowpieceTransform3D CardTransform(in LayoutMorphPose pose, double lift, double flightLift, double flightTilt,
        in LayoutMorphCamera camera, double scrollY, double recede)
    {
        var t = ShowpieceTransform3D.Translation(-pose.Width / 2, -pose.Height / 2, 0);
        if (lift > 0 && (pose.PullY != 0 || pose.PullZ != 0))
        {
            t = t.Then(ShowpieceTransform3D.Translation(0, -pose.PullY * lift, pose.PullZ * lift));
        }
        var rotZ = pose.RotZ + flightTilt;
        if (rotZ != 0)
        {
            t = t.Then(ShowpieceTransform3D.RotationZ(rotZ));
        }
        if (pose.RotX != 0)
        {
            t = t.Then(ShowpieceTransform3D.RotationX(pose.RotX));
        }
        if (pose.RotY != 0)
        {
            t = t.Then(ShowpieceTransform3D.RotationY(pose.RotY));
        }
        t = t.Then(ShowpieceTransform3D.Translation(pose.X, pose.Y, pose.Z + flightLift));
        if (pose.UsesCamera)
        {
            if (camera.Radius != 0 || camera.Tilt != 0 || camera.TranslateY != 0 || camera.FocusY != 0)
            {
                t = t.Then(camera.Transform);
            }
            if (scrollY != 0 || recede != 0)
            {
                t = t.Then(ShowpieceTransform3D.Translation(0, scrollY, -recede));
            }
        }
        return t;
    }

    /// <summary>
    /// A pose that draws exactly like <paramref name="pose"/> under <paramref name="camera"/> (and the scroll and recede)
    /// but outside the camera: the camera is folded into the position and the Y / X / Z turns (YXZ Euler angles).
    /// Used when a card leaves the world to open, and to bring an opened card back.
    /// </summary>
    public static LayoutMorphPose Bake(in LayoutMorphPose pose, in LayoutMorphCamera camera, double scrollY, double recede)
    {
        if (!pose.UsesCamera)
        {
            return pose;
        }
        var world = ShowpieceTransform3D.RotationZ(pose.RotZ)
            .Then(ShowpieceTransform3D.RotationX(pose.RotX))
            .Then(ShowpieceTransform3D.RotationY(pose.RotY))
            .Then(ShowpieceTransform3D.Translation(pose.X, pose.Y, pose.Z))
            .Then(camera.Transform)
            .Then(ShowpieceTransform3D.Translation(0, scrollY, -recede));
        var baked = pose;
        baked.X = world.Tx;
        baked.Y = world.Ty;
        baked.Z = world.Tz;
        // R = Ry(a)·Rx(b)·Rz(c): M23 = −sin b, (M21, M22) = cos b·(sin c, cos c), (M13, M33) = cos b·(sin a, cos a).
        baked.RotX = Math.Asin(Math.Clamp(-world.M23, -1, 1));
        baked.RotZ = Math.Atan2(world.M21, world.M22);
        baked.RotY = Math.Atan2(world.M13, world.M33);
        baked.UsesCamera = false;
        return baked;
    }

    /// <summary>
    /// The card's face points away from the eye (at z = perspective in front of the stage center): it is seen from
    /// behind. In perspective a card beside the center turns its back before its own angle reaches a quarter turn.
    /// </summary>
    public static bool FacesAway(in ShowpieceTransform3D plane, double width, double height, double perspective)
    {
        var center = plane.Transform(width / 2, height / 2, 0);
        var normal = plane.Transform(width / 2, height / 2, 1) - center;
        return -normal.X * center.X - normal.Y * center.Y + normal.Z * (perspective - center.Z) < 0;
    }

    /// <summary>The homography of a plane transform under the stage perspective, in stage px (origin top-left).</summary>
    public static Matrix ScreenMatrix(in ShowpieceTransform3D plane, double perspective, Size stage)
    {
        var m = ShowpieceProjection.PlaneMatrix(plane, perspective);
        return ShowpieceProjection.Multiply(m, ShowpieceProjection.ScaleThenTranslate(1, new Vector(stage.Width / 2, stage.Height / 2)));
    }
}
