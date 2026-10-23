using Avalonia;

namespace TKCode.Arc;

/// <summary>The angles (degrees) and layer gap of a fully exploded stack.</summary>
internal readonly record struct ExplodedComposition(double Tilt, double Turn, double Gap)
{
    /// <summary>The wide composition (Arc's default): rotateX 60°, rotateZ −40°, 136 px between layers.</summary>
    public static readonly ExplodedComposition Wide = new(60, -40, 136);

    /// <summary>A compact control with a stage taller than wide turns less and spreads further: 62°, −20°, 150 px.</summary>
    public static readonly ExplodedComposition Tall = new(62, -20, 150);
}

/// <summary>Unit-scale bounds of the fully exploded stack (stage-center origin) and the right-most label anchor.</summary>
internal readonly record struct ExplodedBounds(double MinX, double MaxX, double MinY, double MaxY, double MaxAnchorX)
{
    public double Height => MaxY - MinY;

    public double CenterY => (MinY + MaxY) / 2;

    public bool IsEmpty => MaxX < MinX || MaxY < MinY;
}

/// <summary>
/// How the stage fits the stack: a flat scale, an exploded scale and offset; the scene scale and offset follow the tilt
/// (<c>s = lerp(flat, exploded, tilt)</c>, <c>t = tilt · offset</c>), not the separation.
/// </summary>
internal readonly record struct ExplodedFit(double FlatScale, double ExplodedScale, Vector ExplodedOffset)
{
    public double ScaleAt(double tilt) => FlatScale + (ExplodedScale - FlatScale) * tilt;

    public Vector OffsetAt(double tilt) => ExplodedOffset * tilt;
}

/// <summary>
/// The 3D scene of an <see cref="ExplodedView"/> (Arc Exploded view, design reference). Chain, outside in: the stage
/// scaler <c>translate(t) scale(s)</c> around the stage center, <c>perspective</c> with its origin at that center, the
/// group <c>rotateX(tilt) rotateZ(turn)</c>, and each layer's <c>translateZ(z)</c>. Layers are parallel planes, so each is
/// drawn with one projective matrix and painted in index order (bottom layer farthest).
/// </summary>
internal static class ExplodedViewGeometry
{
    /// <summary>Default perspective distance.</summary>
    public const double DefaultPerspective = 2400;

    /// <summary>The exploded stack may use this share of the stage height.</summary>
    public const double FitHeight = 0.88;

    /// <summary>The stack plus its labels may use this share of the stage width.</summary>
    public const double FitWidth = 0.94;

    /// <summary>The flat screen may use this share of the stage height.</summary>
    public const double FlatHeight = 0.9;

    /// <summary>The flat screen never grows past this scale.</summary>
    public const double FlatMaxScale = 1.1;

    /// <summary>Room added to the widest label when fitting.</summary>
    public const double LabelRoom = 3.5;

    /// <summary>
    /// Projects a point of a layer (centered coordinates, depth <paramref name="z"/>) through the group rotation and the
    /// perspective, at unit scale with the stage center as origin.
    /// </summary>
    public static Point Project(double x, double y, double z, double tiltDegrees, double turnDegrees, double perspective)
    {
        var turn = ShowpieceProjection.Radians(turnDegrees);
        var tilt = ShowpieceProjection.Radians(tiltDegrees);
        var cosB = Math.Cos(turn);
        var sinB = Math.Sin(turn);
        var cosA = Math.Cos(tilt);
        var sinA = Math.Sin(tilt);
        var x1 = x * cosB - y * sinB;
        var y1 = x * sinB + y * cosB;
        var y2 = y1 * cosA - z * sinA;
        var z2 = y1 * sinA + z * cosA;
        var f = ShowpieceProjection.PerspectiveScale(z2, perspective);
        return new Point(x1 * f, y2 * f);
    }

    /// <summary>
    /// The matrix that draws a layer control (arranged at the stage's top-left, <c>RenderTransformOrigin</c> top-left) at depth
    /// <paramref name="z"/>, with the scene scale <paramref name="scale"/> about <paramref name="origin"/> (stage center + offset).
    /// </summary>
    public static Matrix LayerMatrix(Size layer, double z, double tiltDegrees, double turnDegrees, double perspective, double scale, Point origin)
    {
        var plane = ShowpieceTransform3D.Translation(-layer.Width / 2, -layer.Height / 2, z)
            .Then(ShowpieceTransform3D.RotationZ(ShowpieceProjection.Radians(turnDegrees)))
            .Then(ShowpieceTransform3D.RotationX(ShowpieceProjection.Radians(tiltDegrees)));
        return ShowpieceProjection.Multiply(ShowpieceProjection.PlaneMatrix(plane, perspective),
            ShowpieceProjection.ScaleThenTranslate(scale, new Vector(origin.X, origin.Y)));
    }

    /// <summary>
    /// The unit-scale bounds of the fully exploded stack: the corners of every layer rectangle (not the rounded corners) and
    /// the right-most label anchor (right edge, <paramref name="anchorFromBottom"/>(i) above the bottom edge).
    /// </summary>
    public static ExplodedBounds MeasureExploded(Size layer, int count, ExplodedComposition composition, double perspective,
                                                 Func<int, double> anchorFromBottom)
    {
        if (count <= 0 || layer.Width <= 0 || layer.Height <= 0)
        {
            return new ExplodedBounds(0, -1, 0, -1, 0);
        }
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        var maxAnchor = double.MinValue;
        var hw = layer.Width / 2;
        var hh = layer.Height / 2;
        for (var i = 0; i < count; i++)
        {
            var z = ExplodedViewProgress.LayerDepth(i, count, composition.Gap, 1);
            for (var corner = 0; corner < 4; corner++)
            {
                var p = Project(corner is 0 or 3 ? -hw : hw, corner < 2 ? -hh : hh, z, composition.Tilt, composition.Turn, perspective);
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }
            var anchor = Project(hw, hh - anchorFromBottom(i), z, composition.Tilt, composition.Turn, perspective);
            maxAnchor = Math.Max(maxAnchor, anchor.X);
        }
        return new ExplodedBounds(minX, maxX, minY, maxY, maxAnchor);
    }

    /// <summary>
    /// Fits the stack to a stage: exploded scale <c>min(0.88·H / h, (0.94·W − G) / (anchorX − minX))</c> with
    /// G = widest label + 3.5, centered vertically on the stack and horizontally on "stack + labels"; flat scale
    /// <c>min(1.1, 0.9·H / layerHeight)</c> (Arc's width limit for the flat screen never applies, so it is left out).
    /// </summary>
    public static ExplodedFit Fit(Size stage, Size layer, in ExplodedBounds bounds, double widestLabel)
    {
        var flat = layer.Height > 0 ? Math.Min(FlatMaxScale, FlatHeight * stage.Height / layer.Height) : 1;
        flat = Math.Max(0, flat);
        if (bounds.IsEmpty || bounds.Height <= 0)
        {
            return new ExplodedFit(flat, flat, default);
        }
        var room = widestLabel > 0 ? widestLabel + LabelRoom : 0;
        var span = bounds.MaxAnchorX - bounds.MinX;
        var byHeight = FitHeight * stage.Height / bounds.Height;
        var byWidth = span > 0 ? (FitWidth * stage.Width - room) / span : byHeight;
        var scale = Math.Max(0, Math.Min(byHeight, byWidth));
        var left = bounds.MinX * scale;
        var right = bounds.MaxAnchorX * scale + room;
        return new ExplodedFit(flat, scale, new Vector(-(right + left) / 2, -bounds.CenterY * scale));
    }

    /// <summary>
    /// Where the stage puts a layer point at a pose: <c>center + t + s·project(point)</c>.
    /// </summary>
    public static Point ToStage(Point projected, Point stageCenter, in ExplodedFit fit, double tilt)
    {
        var s = fit.ScaleAt(tilt);
        var t = fit.OffsetAt(tilt);
        return new Point(stageCenter.X + t.X + s * projected.X, stageCenter.Y + t.Y + s * projected.Y);
    }
}
