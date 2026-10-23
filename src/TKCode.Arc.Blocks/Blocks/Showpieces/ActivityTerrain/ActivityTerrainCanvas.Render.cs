using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

internal sealed partial class ActivityTerrainCanvas
{
    // Corner order of a bar: bottom (y0) then top (y1), each x0z0, x1z0, x1z1, x0z1.
    private static readonly int[] s_faceMinusX = [0, 3, 7, 4];
    private static readonly int[] s_facePlusX = [1, 2, 6, 5];
    private static readonly int[] s_faceMinusZ = [0, 1, 5, 4];
    private static readonly int[] s_facePlusZ = [3, 2, 6, 7];
    private static readonly int[] s_faceTop = [4, 5, 6, 7];
    private static readonly string[] s_hourLong = ["00:00", "03:00", "06:00", "09:00", "12:00", "15:00", "18:00", "21:00"];
    private static readonly string[] s_hourShort = ["00", "03", "06", "09", "12", "15", "18", "21"];
    private static readonly string[] s_pillLong = BuildHours(":00");
    private static readonly string[] s_pillShort = BuildHours(string.Empty);

    private const byte MaskMinusX = 1;
    private const byte MaskPlusX = 2;
    private const byte MaskMinusZ = 4;
    private const byte MaskPlusZ = 8;
    private const byte MaskTop = 16;
    private const int BrushCacheLimit = 8192;

    private readonly Point[] _corners = new Point[Cells * 8];
    private readonly Point[] _drawnCorners = new Point[Cells * 8];
    private readonly byte[] _mask = new byte[Cells];
    private readonly byte[] _drawnMask = Unbuilt();
    private readonly bool[] _visible = new bool[Cells];
    // Per bar: side X, side Z, top; a face shows as the unit square through its matrix (see ActivityTerrainQuad).
    private readonly Matrix[] _faceMatrices = new Matrix[Cells * 3];
    private readonly bool[] _faceShown = new bool[Cells * 3];
    // The highlighted bar's top face as a path, for its outline and the x-ray pass.
    private StreamGeometry? _outline;
    private int _outlineIndex = -1;
    private readonly int[] _order = new int[Cells];
    private readonly double[] _orderKeys = new double[Cells];
    private double _sortX = double.NaN;
    private double _sortZ = double.NaN;
    private double _shadeMinusX, _shadePlusX, _shadeMinusZ, _shadePlusZ;
    private readonly Point[] _floor = new Point[4];
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _faceBrushes = new();

    private bool _colorsKnown;
    private bool _colorTargetsDirty = true;
    private ActivityTerrainOklab _low;
    private ActivityTerrainOklab _dimTarget;
    private ActivityTerrainOklab _toneAccent, _toneNeutral, _toneSuccess, _toneWarning, _toneDanger;
    private IImmutableSolidColorBrush? _floorBrush;
    private IImmutableSolidColorBrush? _bandBrush;
    private IImmutableSolidColorBrush? _shadowBrush;
    private IImmutableSolidColorBrush? _pillBrush;
    private IPen? _guidePen;
    private IPen? _outlinePen;
    private IPen? _xrayPen;
    private Color _labelColor;
    private Color _pillTextColor;
    private FontFamily? _fontFamily;
    private Typeface _labelTypeface;
    private Typeface _pillTypeface;

    private static byte[] Unbuilt()
    {
        // No bar's faces are built yet: no mask has every bit set.
        var masks = new byte[Cells];
        Array.Fill(masks, byte.MaxValue);
        return masks;
    }

    private static string[] BuildHours(string suffix)
    {
        var hours = new string[ActivityTerrainMath.Hours];
        for (var h = 0; h < hours.Length; h++)
        {
            hours[h] = h.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + suffix;
        }
        return hours;
    }

    // Colors -------------------------------------------------------------------------------------------------------------

    protected override void OnThemeChanged()
    {
        _colorsKnown = false;
        _colorTargetsDirty = true;
        _faceBrushes.Clear();
        _floorBrush = null;
        _fontFamily = null;
    }

    private void EnsureColors()
    {
        if (_colorsKnown || !IsAttached)
        {
            return;
        }
        var surface = ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
        var surfaceMuted = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted);
        var foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        var background = ArcTokens.GetColor(this, ArcDesignTokenKind.Background);
        // The low end of the scale (and the legend's first swatch): halfway between SurfaceMuted and BorderStrong.
        _low = ActivityTerrainPalette.Low(this);
        _dimTarget = ActivityTerrainOklab.FromColor(surfaceMuted);
        _toneAccent = ActivityTerrainOklab.FromColor(ActivityTerrainPalette.ToneColor(this, ActivityTerrainTone.Accent));
        _toneNeutral = ActivityTerrainOklab.FromColor(ActivityTerrainPalette.ToneColor(this, ActivityTerrainTone.Neutral));
        _toneSuccess = ActivityTerrainOklab.FromColor(ActivityTerrainPalette.ToneColor(this, ActivityTerrainTone.Success));
        _toneWarning = ActivityTerrainOklab.FromColor(ActivityTerrainPalette.ToneColor(this, ActivityTerrainTone.Warning));
        _toneDanger = ActivityTerrainOklab.FromColor(ActivityTerrainPalette.ToneColor(this, ActivityTerrainTone.Danger));
        // The floor sits between SurfaceMuted and Surface (light #F9F9F9, dark #222).
        _floorBrush = new ImmutableSolidColorBrush(Oklch.MixOklab(surfaceMuted, surface, 0.2));
        _bandBrush = new ImmutableSolidColorBrush(foreground, 0.05);
        _shadowBrush = new ImmutableSolidColorBrush(foreground, 0.1);
        _pillBrush = new ImmutableSolidColorBrush(foreground);
        _guidePen = new ImmutablePen(new ImmutableSolidColorBrush(foreground, 0.55), 1);
        _outlinePen = new ImmutablePen(new ImmutableSolidColorBrush(foreground, 0.9), 1.25, lineJoin: PenLineJoin.Round);
        // Avalonia dash lengths are multiples of the thickness: 3px on, 3px off at 1.25px.
        _xrayPen = new ImmutablePen(new ImmutableSolidColorBrush(foreground, 0.5), 1.25,
            new ImmutableDashStyle([3 / 1.25, 3 / 1.25], 0), lineJoin: PenLineJoin.Round);
        _labelColor = ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted);
        _pillTextColor = background;
        _colorsKnown = true;
        if (_colorTargetsDirty && _hasData)
        {
            JumpColors();
        }
        else if (!_hasData)
        {
            _colorTargetsDirty = false;
            for (var i = 0; i < Cells; i++)
            {
                _colorL[i].Jump(_low.L);
                _colorA[i].Jump(_low.A);
                _colorB[i].Jump(_low.B);
            }
        }
    }

    internal ActivityTerrainOklab ToneColor(ActivityTerrainTone tone) => tone switch
    {
        ActivityTerrainTone.Neutral => _toneNeutral,
        ActivityTerrainTone.Success => _toneSuccess,
        ActivityTerrainTone.Warning => _toneWarning,
        ActivityTerrainTone.Danger => _toneDanger,
        _ => _toneAccent
    };

    private IImmutableSolidColorBrush FaceBrush(in ActivityTerrainOklab color)
    {
        var c = color.ToColor();
        var key = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        if (_faceBrushes.TryGetValue(key, out var brush))
        {
            return brush;
        }
        if (_faceBrushes.Count >= BrushCacheLimit)
        {
            _faceBrushes.Clear();
        }
        brush = new ImmutableSolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        _faceBrushes[key] = brush;
        return brush;
    }

    /// <summary>Cached face brushes (tests: a steady scene stops adding).</summary>
    internal int FaceBrushCount => _faceBrushes.Count;

    // Scene ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Projects every bar for <paramref name="view"/>: corners (with lift), the faces that face the camera, whether the
    /// bar is on the canvas at all, and the painter order (far to near by ground distance from the camera).
    /// </summary>
    private void BuildScene(in ShowpieceOrbitView view, Size size)
    {
        var camera = ActivityTerrainMath.CameraPosition(view.Yaw, view.Pitch, view.Distance, view.Target);
        if (camera.X != _sortX || camera.Z != _sortZ)
        {
            _sortX = camera.X;
            _sortZ = camera.Z;
            for (var k = 0; k < Cells; k++)
            {
                var i = _order[k];
                var cell = ActivityTerrainCell.FromIndex(i);
                var dx = ActivityTerrainMath.CellX(cell.Hour) - camera.X;
                var dz = ActivityTerrainMath.CellZ(cell.Day) - camera.Z;
                // Farthest first; the index breaks exact ties so the order never flickers.
                _orderKeys[k] = -(dx * dx + dz * dz) + i * 1e-9;
            }
            Array.Sort(_orderKeys, _order);
        }
        // A side is shaded as the left or the right face by where its normal points on screen (light from the upper left).
        var cos = Math.Cos(view.Yaw);
        var sin = Math.Sin(view.Yaw);
        _shadeMinusX = -cos < 0 ? ActivityTerrainMath.LeftShade : ActivityTerrainMath.RightShade;
        _shadePlusX = cos < 0 ? ActivityTerrainMath.LeftShade : ActivityTerrainMath.RightShade;
        _shadeMinusZ = -sin < 0 ? ActivityTerrainMath.LeftShade : ActivityTerrainMath.RightShade;
        _shadePlusZ = sin < 0 ? ActivityTerrainMath.LeftShade : ActivityTerrainMath.RightShade;

        const double half = ActivityTerrainMath.BarSize / 2;
        for (var i = 0; i < Cells; i++)
        {
            var cell = ActivityTerrainCell.FromIndex(i);
            var cx = ActivityTerrainMath.CellX(cell.Hour);
            var cz = ActivityTerrainMath.CellZ(cell.Day);
            double x0 = cx - half, x1 = cx + half, z0 = cz - half, z1 = cz + half;
            var y0 = _lift[i].Value;
            var y1 = y0 + Math.Max(0, _height[i].Value);
            var b = i * 8;
            var ok = view.Project(new ShowpieceVec3(x0, y0, z0), out _corners[b], out _)
                     & view.Project(new ShowpieceVec3(x1, y0, z0), out _corners[b + 1], out _)
                     & view.Project(new ShowpieceVec3(x1, y0, z1), out _corners[b + 2], out _)
                     & view.Project(new ShowpieceVec3(x0, y0, z1), out _corners[b + 3], out _)
                     & view.Project(new ShowpieceVec3(x0, y1, z0), out _corners[b + 4], out _)
                     & view.Project(new ShowpieceVec3(x1, y1, z0), out _corners[b + 5], out _)
                     & view.Project(new ShowpieceVec3(x1, y1, z1), out _corners[b + 6], out _)
                     & view.Project(new ShowpieceVec3(x0, y1, z1), out _corners[b + 7], out _);
            if (!ok)
            {
                _visible[i] = false;
                continue;
            }
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            for (var c = 0; c < 8; c++)
            {
                var p = _corners[b + c];
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }
            // Bars wholly off the canvas are neither drawn nor hit (Arc submits them anyway).
            _visible[i] = maxX >= -1 && minX <= size.Width + 1 && maxY >= -1 && minY <= size.Height + 1;
            byte mask = 0;
            if (camera.X < x0)
            {
                mask |= MaskMinusX;
            }
            else if (camera.X > x1)
            {
                mask |= MaskPlusX;
            }
            if (camera.Z < z0)
            {
                mask |= MaskMinusZ;
            }
            else if (camera.Z > z1)
            {
                mask |= MaskPlusZ;
            }
            if (camera.Y > y1)
            {
                mask |= MaskTop;
            }
            _mask[i] = mask;
        }
        view.Project(new ShowpieceVec3(-ActivityTerrainMath.FloorHalfX, 0, -ActivityTerrainMath.FloorHalfZ), out _floor[0], out _);
        view.Project(new ShowpieceVec3(ActivityTerrainMath.FloorHalfX, 0, -ActivityTerrainMath.FloorHalfZ), out _floor[1], out _);
        view.Project(new ShowpieceVec3(ActivityTerrainMath.FloorHalfX, 0, ActivityTerrainMath.FloorHalfZ), out _floor[2], out _);
        view.Project(new ShowpieceVec3(-ActivityTerrainMath.FloorHalfX, 0, ActivityTerrainMath.FloorHalfZ), out _floor[3], out _);
    }

    // Drawing -------------------------------------------------------------------------------------------------------------

    protected override void RenderStage(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        EnsureColors();
        if (!_colorsKnown)
        {
            return;
        }
        var view = CurrentView(size);
        BuildScene(view, size);
        DrawFloor(context);
        if (_highlighted is { IsValid: true } highlighted)
        {
            DrawCrosshair(context, view, highlighted);
        }
        DrawBars(context, view);
        if (_highlighted is { IsValid: true } xray && _visible[xray.Index] && _outline is { } top)
        {
            // Once more after everything: the top outline shows through taller bars in front (dashed).
            context.DrawGeometry(null, _xrayPen, top);
        }
        DrawLabels(context, view, size);
        if (_hasData && _max <= 0 && Owner?.NoDataText is { Length: > 0 } noData)
        {
            var text = TextCache.Get(noData, _labelTypeface, 13);
            text.Draw(context, TextCache.Brush(_labelColor), new Point((size.Width - text.Width) / 2, (size.Height - text.Height) / 2));
        }
    }

    private void DrawFloor(DrawingContext context)
    {
        if (ActivityTerrainQuad.TryUnitSquareTo(_floor[0], _floor[1], _floor[2], _floor[3], out var matrix))
        {
            FillQuad(context, _floorBrush!, matrix);
        }
    }

    private void DrawCrosshair(DrawingContext context, in ShowpieceOrbitView view, ActivityTerrainCell cell)
    {
        var cx = ActivityTerrainMath.CellX(cell.Hour);
        var cz = ActivityTerrainMath.CellZ(cell.Day);
        // Row band to the left edge, column band to the front edge, both on the floor.
        FillFloorQuad(context, view, _bandBrush!, ActivityTerrainMath.BandOuterX, cx, cz - 0.5, cz + 0.5);
        FillFloorQuad(context, view, _bandBrush!, cx - 0.5, cx + 0.5, cz, ActivityTerrainMath.BandOuterZ);
        if (view.Project(new ShowpieceVec3(cx, 0, cz), out var center, out _))
        {
            if (view.Project(new ShowpieceVec3(ActivityTerrainMath.BandOuterX, 0, cz), out var left, out _))
            {
                context.DrawLine(_guidePen!, center, left);
            }
            if (view.Project(new ShowpieceVec3(cx, 0, ActivityTerrainMath.BandOuterZ), out var front, out _))
            {
                context.DrawLine(_guidePen!, center, front);
            }
        }
    }

    /// <summary>A rectangle of the floor (world x and z), filled.</summary>
    private static void FillFloorQuad(DrawingContext context, in ShowpieceOrbitView view, IBrush brush, double x0, double x1, double z0, double z1)
    {
        if (view.Project(new ShowpieceVec3(x0, 0, z0), out var a, out _) && view.Project(new ShowpieceVec3(x1, 0, z0), out var b, out _) &&
            view.Project(new ShowpieceVec3(x1, 0, z1), out var c, out _) && view.Project(new ShowpieceVec3(x0, 0, z1), out var d, out _) &&
            ActivityTerrainQuad.TryUnitSquareTo(a, b, c, d, out var matrix))
        {
            FillQuad(context, brush, matrix);
        }
    }

    /// <summary>The unit square through <paramref name="matrix"/>: one anti-aliased quad for Skia, no geometry.</summary>
    private static void FillQuad(DrawingContext context, IBrush brush, in Matrix matrix)
    {
        using (context.PushTransform(matrix))
        {
            context.DrawRectangle(brush, null, ActivityTerrainQuad.UnitSquare);
        }
    }

    private void DrawBars(DrawingContext context, in ShowpieceOrbitView view)
    {
        var highlighted = _highlighted is { IsValid: true } h ? h.Index : -1;
        if (highlighted != _outlineIndex)
        {
            // A new highlight (or none): its outline is built when its bar is drawn.
            _outline = null;
            _outlineIndex = highlighted;
        }
        for (var k = 0; k < Cells; k++)
        {
            var i = _order[k];
            if (!_visible[i])
            {
                continue;
            }
            var b = i * 8;
            var f = i * 3;
            var mask = _mask[i];
            var rebuilt = false;
            if (_drawnMask[i] != mask || !SamePoints(_corners, _drawnCorners, b, 8))
            {
                RebuildFaces(i, mask);
                rebuilt = true;
            }
            if (_lift[i].Value > 0.002)
            {
                // A lifted bar leaves its footprint on the floor as a shadow.
                var cell = ActivityTerrainCell.FromIndex(i);
                var cx = ActivityTerrainMath.CellX(cell.Hour);
                var cz = ActivityTerrainMath.CellZ(cell.Day);
                const double half = ActivityTerrainMath.BarSize / 2;
                FillFloorQuad(context, view, _shadowBrush!, cx - half, cx + half, cz - half, cz + half);
            }
            var top = new ActivityTerrainOklab(_colorL[i].Value, _colorA[i].Value, _colorB[i].Value);
            var dim = _dim[i].Value * ActivityTerrainMath.DimAmount;
            if (_faceShown[f])
            {
                var shade = (mask & MaskMinusX) != 0 ? _shadeMinusX : _shadePlusX;
                FillQuad(context, FaceBrush(top.Scale(shade).Mix(_dimTarget, dim)), _faceMatrices[f]);
            }
            if (_faceShown[f + 1])
            {
                var shade = (mask & MaskMinusZ) != 0 ? _shadeMinusZ : _shadePlusZ;
                FillQuad(context, FaceBrush(top.Scale(shade).Mix(_dimTarget, dim)), _faceMatrices[f + 1]);
            }
            if (i == highlighted && (rebuilt || _outline is null))
            {
                _outline = Face(b, s_faceTop);
            }
            if ((mask & MaskTop) != 0)
            {
                if (_faceShown[f + 2])
                {
                    FillQuad(context, FaceBrush(top.Mix(_dimTarget, dim)), _faceMatrices[f + 2]);
                }
                if (i == highlighted)
                {
                    context.DrawGeometry(null, _outlinePen, _outline!);
                }
            }
        }
    }

    /// <summary>The matrices of the faces a bar shows for <paramref name="mask"/>, from its projected corners.</summary>
    private void RebuildFaces(int i, byte mask)
    {
        var b = i * 8;
        var f = i * 3;
        _faceShown[f] = (mask & MaskMinusX) != 0 ? FaceQuad(b, s_faceMinusX, out _faceMatrices[f])
            : (mask & MaskPlusX) != 0 && FaceQuad(b, s_facePlusX, out _faceMatrices[f]);
        _faceShown[f + 1] = (mask & MaskMinusZ) != 0 ? FaceQuad(b, s_faceMinusZ, out _faceMatrices[f + 1])
            : (mask & MaskPlusZ) != 0 && FaceQuad(b, s_facePlusZ, out _faceMatrices[f + 1]);
        _faceShown[f + 2] = (mask & MaskTop) != 0 && FaceQuad(b, s_faceTop, out _faceMatrices[f + 2]);
        Array.Copy(_corners, b, _drawnCorners, b, 8);
        _drawnMask[i] = mask;
    }

    /// <summary>
    /// A face's matrix: its corners pushed out by the seam outset, the unit square mapped onto them. False for a face too
    /// thin to show (see <see cref="ActivityTerrainQuad.MinThickness"/>).
    /// </summary>
    private bool FaceQuad(int b, int[] corners, out Matrix matrix)
    {
        var p0 = _corners[b + corners[0]];
        var p1 = _corners[b + corners[1]];
        var p2 = _corners[b + corners[2]];
        var p3 = _corners[b + corners[3]];
        if (ActivityTerrainQuad.Thickness(p0, p1, p2, p3) < ActivityTerrainQuad.MinThickness)
        {
            matrix = default;
            return false;
        }
        // A face that cannot be pushed out (it would not stay convex) is drawn as it is.
        ActivityTerrainQuad.TryOutset(ref p0, ref p1, ref p2, ref p3, ActivityTerrainQuad.SeamOutset);
        return ActivityTerrainQuad.TryUnitSquareTo(p0, p1, p2, p3, out matrix);
    }

    /// <summary>
    /// A face as last drawn (tests): its projected corners and the matrix its unit square went through. <paramref name="face"/>
    /// is 0 for the side along x, 1 for the side along z, 2 for the top. False when the face was not drawn: the bar was off
    /// the canvas, the face turned away from the camera or it was too thin to show.
    /// </summary>
    internal bool TryGetDrawnFace(int index, int face, out Point a, out Point b, out Point c, out Point d, out Matrix matrix)
    {
        var mask = _drawnMask[index];
        int[]? corners = face switch
        {
            0 => (mask & MaskMinusX) != 0 ? s_faceMinusX : (mask & MaskPlusX) != 0 ? s_facePlusX : null,
            1 => (mask & MaskMinusZ) != 0 ? s_faceMinusZ : (mask & MaskPlusZ) != 0 ? s_facePlusZ : null,
            _ => (mask & MaskTop) != 0 ? s_faceTop : null
        };
        var shown = _visible[index] && mask != byte.MaxValue && corners is not null && _faceShown[index * 3 + face];
        var o = index * 8;
        a = shown ? _drawnCorners[o + corners![0]] : default;
        b = shown ? _drawnCorners[o + corners![1]] : default;
        c = shown ? _drawnCorners[o + corners![2]] : default;
        d = shown ? _drawnCorners[o + corners![3]] : default;
        matrix = shown ? _faceMatrices[index * 3 + face] : default;
        return shown;
    }

    /// <summary>The highlighted bar's top outline as last drawn (tests: it is rebuilt only when that bar moves).</summary>
    internal Geometry? OutlineGeometry => _outline;

    private StreamGeometry Face(int b, int[] corners) =>
        Polygon(_corners[b + corners[0]], _corners[b + corners[1]], _corners[b + corners[2]], _corners[b + corners[3]]);

    /// <summary>A closed quadrilateral of straight segments.</summary>
    private static StreamGeometry Polygon(Point a, Point b, Point c, Point d)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(a, true);
            context.LineTo(b);
            context.LineTo(c);
            context.LineTo(d);
            context.EndFigure(true);
        }
        return geometry;
    }

    private static bool SamePoints(Point[] current, Point[] drawn, int start, int count)
    {
        for (var k = start; k < start + count; k++)
        {
            if (current[k] != drawn[k])
            {
                return false;
            }
        }
        return true;
    }

    // Labels ---------------------------------------------------------------------------------------------------------------

    private void EnsureTypefaces()
    {
        var family = TextElement.GetFontFamily(this);
        if (ReferenceEquals(family, _fontFamily))
        {
            return;
        }
        _fontFamily = family;
        _labelTypeface = new Typeface(family);
        _pillTypeface = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
    }

    private void DrawLabels(DrawingContext context, in ShowpieceOrbitView view, Size size)
    {
        EnsureTypefaces();
        const double fontSize = 11;
        var top = _viewMode == ActivityTerrainViewMode.Top;
        var days = Owner?.DayAbbreviationList ?? ActivityTerrain.DefaultDayAbbreviations;

        // Day labels: every 1, 2, 3 or 6 days, whichever first keeps them apart.
        Span<Point> dayAnchors = stackalloc Point[ActivityTerrainMath.Days];
        Span<bool> dayOk = stackalloc bool[ActivityTerrainMath.Days];
        for (var d = 0; d < ActivityTerrainMath.Days; d++)
        {
            dayOk[d] = view.Project(new ShowpieceVec3(ActivityTerrainMath.DayLabelX, 0, ActivityTerrainMath.CellZ(d)), out dayAnchors[d], out _);
        }
        // Spacing of the first pair (Mon → Tue, 00 → 03), as measured on Arc's terrain.
        var perDay = dayOk[0] && dayOk[1] ? Distance(dayAnchors[0], dayAnchors[1]) : 0;
        var step = ActivityTerrainMath.DayLabelStep(perDay, top);

        Span<Point> hourAnchors = stackalloc Point[8];
        Span<bool> hourOk = stackalloc bool[8];
        for (var k = 0; k < 8; k++)
        {
            hourOk[k] = view.Project(new ShowpieceVec3(ActivityTerrainMath.CellX(k * 3), 0, ActivityTerrainMath.HourLabelZ), out hourAnchors[k], out _);
        }
        var perThree = hourOk[0] && hourOk[1] ? Distance(hourAnchors[0], hourAnchors[1]) : 0;
        var longHours = ActivityTerrainMath.UsesLongHourLabels(perThree);

        // A highlighted day and hour become pills; regular labels too close to a pill step aside: days nearer than the
        // day spacing (hovering Fri hides Thu and Sun, keeps Mon), hours whose text would touch the pill.
        Point? dayPill = null, hourPill = null;
        ShowpieceText? dayPillText = null, hourPillText = null;
        if (_highlighted is { IsValid: true } cell)
        {
            if (cell.Day < days.Count && dayOk[cell.Day])
            {
                dayPill = dayAnchors[cell.Day];
                dayPillText = TextCache.Get(days[cell.Day], _pillTypeface, fontSize);
            }
            if (view.Project(new ShowpieceVec3(ActivityTerrainMath.CellX(cell.Hour), 0, ActivityTerrainMath.HourLabelZ), out var anchor, out _))
            {
                hourPill = anchor;
                hourPillText = TextCache.Get(longHours ? s_pillLong[cell.Hour] : s_pillShort[cell.Hour], _pillTypeface, fontSize);
            }
        }
        var labelBrush = TextCache.Brush(_labelColor);
        var daySpacing = top ? ActivityTerrainMath.TopDayLabelSpacing : ActivityTerrainMath.DayLabelSpacing;
        for (var d = 0; d < ActivityTerrainMath.Days && d < days.Count; d += step)
        {
            if (!dayOk[d] || dayPill is { } pill && Distance(pill, dayAnchors[d]) < daySpacing)
            {
                continue;
            }
            DrawCentered(context, TextCache.Get(days[d], _labelTypeface, fontSize), labelBrush, dayAnchors[d]);
        }
        for (var k = 0; k < 8; k++)
        {
            if (!hourOk[k])
            {
                continue;
            }
            var text = TextCache.Get(longHours ? s_hourLong[k] : s_hourShort[k], _labelTypeface, fontSize);
            if (hourPill is { } pill && hourPillText is not null &&
                Distance(pill, hourAnchors[k]) < (hourPillText.Width + 12 + text.Width) / 2 + 4)
            {
                continue;
            }
            DrawCentered(context, text, labelBrush, hourAnchors[k]);
        }
        if (dayPill is { } dayAnchor && dayPillText is not null)
        {
            DrawPill(context, dayPillText, dayAnchor);
        }
        if (hourPill is { } hourAnchor && hourPillText is not null)
        {
            DrawPill(context, hourPillText, hourAnchor);
        }
    }

    private static void DrawCentered(DrawingContext context, ShowpieceText text, IBrush brush, Point anchor) =>
        text.Draw(context, brush, new Point(anchor.X - text.Width / 2, anchor.Y - text.Height / 2));

    /// <summary>A highlighted axis label: 20px pill, 6px each side, Foreground fill, medium text in the background color.</summary>
    private void DrawPill(DrawingContext context, ShowpieceText text, Point anchor)
    {
        var width = text.Width + 12;
        var rect = new Rect(anchor.X - width / 2, anchor.Y - 10, width, 20);
        context.DrawRectangle(_pillBrush, null, new RoundedRect(rect, 10));
        DrawCentered(context, text, TextCache.Brush(_pillTextColor), anchor);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // Hit testing ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The bar under a canvas point: the scene projected as it is drawn, searched near to far (the reverse of the
    /// painter order), testing each bar's visible faces. -1 for the floor and empty canvas.
    /// </summary>
    internal int HitTestIndex(Point position)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || !_hasData)
        {
            return -1;
        }
        BuildScene(CurrentView(size), size);
        for (var k = Cells - 1; k >= 0; k--)
        {
            var i = _order[k];
            if (!_visible[i])
            {
                continue;
            }
            var b = i * 8;
            var mask = _mask[i];
            if ((mask & MaskTop) != 0 && Contains(b, s_faceTop, position))
            {
                return i;
            }
            if ((mask & MaskMinusX) != 0 && Contains(b, s_faceMinusX, position) ||
                (mask & MaskPlusX) != 0 && Contains(b, s_facePlusX, position) ||
                (mask & MaskMinusZ) != 0 && Contains(b, s_faceMinusZ, position) ||
                (mask & MaskPlusZ) != 0 && Contains(b, s_facePlusZ, position))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The projected center of a bar's top face (tests and demos).</summary>
    internal Point? BarTopCenter(ActivityTerrainCell cell)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }
        var view = CurrentView(size);
        var i = cell.Index;
        var top = new ShowpieceVec3(ActivityTerrainMath.CellX(cell.Hour), _lift[i].Value + Math.Max(0, _height[i].Value),
            ActivityTerrainMath.CellZ(cell.Day));
        return view.Project(top, out var screen, out _) ? screen : null;
    }

    private bool Contains(int b, int[] face, Point p)
    {
        var inside = false;
        for (int a = 0, z = face.Length - 1; a < face.Length; z = a++)
        {
            var pa = _corners[b + face[a]];
            var pz = _corners[b + face[z]];
            if (pa.Y > p.Y != pz.Y > p.Y && p.X < (pz.X - pa.X) * (p.Y - pa.Y) / (pz.Y - pa.Y) + pa.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
