using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TKCode.Arc.DesignTokens;
using static TKCode.Arc.ArmingSwitchPaint;

namespace TKCode.Arc;

// The inks: read from ArmingSwitchToken and the ArcDesign accent once per theme, turned into immutable brushes and pens
// in plate units, so a frame only pushes transforms and opacities.
internal sealed partial class ArmingSwitchStage
{
    private bool _lookDirty = true;
    private bool _textDirty = true;

    // Plate.
    private IBrush? _plateFill, _plateSheen, _plateChamfer, _glow, _streak, _streakFaint, _base, _baseSheen, _ear, _barrel, _barrelShadow;
    private IBrush? _engrave, _engraveHi, _engraveLo, _tick;
    private IPen? _plateEdge, _bevelHi, _bevelLo, _baseRing, _baseHi;
    private BoxShadows _plateShadow, _baseShadow, _partShadow;
    private IBrush? _screwWell, _screwHead, _screwSocket, _screwSpec;
    private IBrush? _ledOff, _ledLit;
    private IBrush? _noise;
    private double _brushOpacity = 0.24;

    // Lamp.
    private IBrush? _bezel, _bezelGroove, _lensFill, _facetA, _facetB, _gloss, _lensShade;
    private IPen? _bezelRing, _lensRing, _lensRingLight, _lensRingDark, _ringTrack, _ringTrackHi, _fineRingLight, _fineRingDark;
    private readonly IBrush?[] _lit = new IBrush?[3];
    private readonly IBrush?[] _bloom = new IBrush?[3];
    private readonly IBrush?[] _spill = new IBrush?[3];
    private readonly Color[] _lampColours = new Color[3];
    private Color _accent = Colors.Gray;
    private IBrush? _glintBand;

    // Assembly.
    private IBrush? _washer, _washerGroove, _bushing, _bushingHole, _shaft, _band, _ball, _ballSpec, _ballShade;
    private readonly IBrush?[] _nutFaces = new IBrush?[6];
    private IPen? _leverShadow, _leverShadowSoft;

    // Cover.
    private Color _cover = Colors.Gray;
    private IBrush? _lid, _lidSheen, _wallTop, _wallSide, _wallBottom, _wallBottomSheen, _tab, _rib, _glare, _coverShadow;
    private IPen? _lidRing, _lidHi, _lidLo, _tabRing, _contact;
    private double _coverShadowOpacity = 0.72;

    // Engraving (pixels).
    private Engraved _armText, _safeText, _readyText, _holdText;

    /// <summary>An engraved label as an outline (filled three times for the groove), with its advance width and height.</summary>
    private readonly record struct Engraved(Geometry? Shape, double Width, double Height);
    private double _engraveSize;

    public void InvalidateLook()
    {
        _lookDirty = true;
        _textDirty = true;
        InvalidateVisual();
    }

    private void InvalidateText()
    {
        _textDirty = true;
        InvalidateVisual();
    }

    private Color Ink(ArmingSwitchTokenKind kind, Color fallback)
    {
        if (!this.TryFindResource(ArmingSwitchTokens.Key(kind), ActualThemeVariant, out var value))
        {
            return fallback;
        }
        return value switch
        {
            Color c => c,
            ISolidColorBrush b => b.Color,
            _ => fallback
        };
    }

    private double Number(ArmingSwitchTokenKind kind, double fallback) =>
        this.TryFindResource(ArmingSwitchTokens.Key(kind), ActualThemeVariant, out var value) && value is double d ? d : fallback;

    private void EnsureLook()
    {
        if (!_lookDirty)
        {
            return;
        }
        _lookDirty = false;
        var plateA = Ink(ArmingSwitchTokenKind.PlateLight, Color.Parse("#5b5d60"));
        var plateB = Ink(ArmingSwitchTokenKind.PlateMid, Color.Parse("#46484b"));
        var plateC = Ink(ArmingSwitchTokenKind.PlateDark, Color.Parse("#37393b"));
        var hi = Ink(ArmingSwitchTokenKind.ChromeHigh, Colors.White);
        var mid = Ink(ArmingSwitchTokenKind.ChromeMid, Colors.Gray);
        var lo = Ink(ArmingSwitchTokenKind.ChromeLow, Colors.DimGray);
        var edge = Ink(ArmingSwitchTokenKind.ChromeEdge, Colors.Black);
        var shadow = Ink(ArmingSwitchTokenKind.PartShadow, Color.FromArgb(140, 0, 0, 0));
        var drop = Ink(ArmingSwitchTokenKind.PlateDrop, Color.FromArgb(128, 0, 0, 0));
        var transparent = Colors.Transparent;
        _brushOpacity = Number(ArmingSwitchTokenKind.BrushOpacity, 0.24);
        _coverShadowOpacity = Number(ArmingSwitchTokenKind.CoverShadowOpacity, 0.72);

        // Plate: brushed gradient, vertical sheen, diagonal chamfer, bevel, edge and drop shadows (W = 1 here).
        const double ph = ArmingSwitchView.PlateHeight;
        _plateFill = Linear(100, 1, ph, (0, plateB), (0.2, plateA), (0.46, plateB), (0.68, plateA), (0.86, plateB), (1, plateC));
        _plateSheen = Linear(180, 1, ph, (0, Color.FromArgb(41, 255, 255, 255)), (0.3, transparent), (0.72, transparent), (1, Color.FromArgb(15, 0, 0, 0)));
        _plateChamfer = Linear(162, 1, ph, (0, Ink(ArmingSwitchTokenKind.ChamferLight, transparent)), (0.34, transparent),
            (0.66, transparent), (1, Ink(ArmingSwitchTokenKind.ChamferShadow, transparent)));
        _plateEdge = Pen(Ink(ArmingSwitchTokenKind.PlateEdge, transparent), 0.0023);
        _bevelHi = Pen(Ink(ArmingSwitchTokenKind.BevelHighlight, transparent), 0.0025);
        _bevelLo = Pen(Ink(ArmingSwitchTokenKind.BevelShadow, transparent), 0.0025);
        _plateShadow = new BoxShadows(new BoxShadow { OffsetY = 0.026, Blur = 0.044, Spread = -0.018, Color = drop },
            new[] { new BoxShadow { OffsetY = 0.005, Blur = 0.01, Spread = -0.003, Color = drop } });
        var glow = Ink(ArmingSwitchTokenKind.Glow, transparent);
        _glow = Radial(0.22, 0.08, 0.58 * 0.72, 0.56 * 0.72, (0, glow), (1, A(glow, 0)));
        var streak = Ink(ArmingSwitchTokenKind.Streak, transparent);
        _streak = Radial(0.5, 0.42, 0.5 * 0.72, 0.62 * 0.72, (0, streak), (1, A(streak, 0)));
        _streakFaint = Radial(0.5, 0.5, 0.5 * 0.7, 0.7 * 0.7, (0, A(streak, 0.55)), (1, A(streak, 0)));

        // Base, hinge barrel and ears.
        _base = Linear(180, 0.31, 0.47, (0, Ink(ArmingSwitchTokenKind.BaseTop, Colors.Black)), (1, Ink(ArmingSwitchTokenKind.BaseBottom, Colors.Black)));
        _baseSheen = Linear(100, 0.31, 0.47, (0, Color.FromArgb(15, 255, 255, 255)), (0.4, transparent), (0.7, Color.FromArgb(10, 255, 255, 255)), (1, transparent));
        _baseHi = Pen(Ink(ArmingSwitchTokenKind.BaseHighlight, transparent), 0.0022);
        _baseRing = Pen(Color.FromArgb(56, 0, 0, 0), 0.0015);
        _baseShadow = new BoxShadows(new BoxShadow { OffsetX = 0.005, OffsetY = 0.01, Blur = 0.018, Spread = -0.004, Color = shadow });
        _partShadow = new BoxShadows(new BoxShadow { OffsetX = 0.005, OffsetY = 0.007, Blur = 0.01, Spread = -0.002, Color = shadow });
        _ear = Linear(90, 0.026, 0.054, (0, lo), (0.4, hi), (0.7, mid), (1, lo));
        _barrel = Linear(180, 0.296, 0.021, (0, lo), (0.34, hi), (0.58, mid), (1, edge));
        _barrelShadow = Solid(Color.FromArgb(64, 0, 0, 0));

        // Engraving and screws.
        _engrave = Solid(Ink(ArmingSwitchTokenKind.Engrave, Colors.White));
        _engraveHi = Solid(Ink(ArmingSwitchTokenKind.EngraveHighlight, transparent));
        _engraveLo = Solid(Ink(ArmingSwitchTokenKind.EngraveShadow, transparent));
        _tick = Solid(A(Ink(ArmingSwitchTokenKind.Engrave, Colors.White), 0.72));
        _screwWell = Radial(0.5, 0.5, 0.72, 0.72, (0, Color.FromArgb(77, 0, 0, 0)), (0.58 / 0.72, Color.FromArgb(77, 0, 0, 0)),
            (0.7 / 0.72, Color.FromArgb(20, 0, 0, 0)), (1, transparent));
        _screwHead = Radial(0.38, 0.3, 0.9, 0.9, (0, Ink(ArmingSwitchTokenKind.ScrewLight, Colors.LightGray)),
            (0.78, Ink(ArmingSwitchTokenKind.ScrewDark, Colors.Gray)), (1, Ink(ArmingSwitchTokenKind.ScrewDark, Colors.Gray)));
        _screwSocket = Linear(180, 1, 1, (0, Oklch.ToColor(0.12, 0, 0)), (1, Oklch.ToColor(0.34, 0, 0)));
        _screwSpec = Radial(0.5, 0.5, 0.5, 0.5, (0, Color.FromArgb(217, 255, 255, 255)), (0.7, transparent), (1, transparent));
        var green = Ink(ArmingSwitchTokenKind.LampGreen, Colors.LimeGreen);
        _ledOff = Solid(Ink(ArmingSwitchTokenKind.LedOff, Colors.DarkGreen));
        _ledLit = Radial(0.4, 0.35, 0.75, 0.75, (0, Lighten(green, 0.6)), (0.62, green), (1, green));

        // Lamp: bezel, lens, facets, ring and lights.
        _bezel = Conic(210, (0, hi), (0.16, mid), (0.36, lo), (0.54, mid), (0.70, hi), (0.86, mid), (1, hi));
        _bezelGroove = Radial(0.5, 0.5, 0.7071, 0.7071, (0, transparent), (0.66 * 0.7071 / 0.7071 * 0.7071 / 0.7071, transparent),
            (0.66, transparent), (0.69, Color.FromArgb(82, 0, 0, 0)), (0.72, transparent), (1, transparent));
        _bezelRing = Pen(Color.FromArgb(51, 0, 0, 0), 0.0018);
        _fineRingLight = Pen(Color.FromArgb(26, 255, 255, 255), 0.001);
        _fineRingDark = Pen(Color.FromArgb(15, 0, 0, 0), 0.001);
        _lensFill = Radial(0.5, 0.4, 0.594, 0.594, (0, Ink(ArmingSwitchTokenKind.LensLight, Colors.SaddleBrown)),
            (1, Ink(ArmingSwitchTokenKind.LensDark, Colors.Black)));
        _lensShade = Linear(180, 1, 1, (0, Color.FromArgb(110, 0, 0, 0)), (0.16, transparent), (1, transparent));
        _lensRing = Pen(Color.FromArgb(82, 0, 0, 0), 0.0032);
        _lensRingLight = Pen(Color.FromArgb(23, 255, 255, 255), 0.009);
        _lensRingDark = Pen(Color.FromArgb(18, 0, 0, 0), 0.009);
        _facetA = Solid(Color.FromArgb(33, 255, 255, 255));
        _facetB = Solid(Color.FromArgb(31, 0, 0, 0));
        _gloss = Radial(0.5, 0.4, 0.6, 0.6, (0, Color.FromArgb(184, 255, 255, 255)), (1, transparent));
        _ringTrack = Pen(Ink(ArmingSwitchTokenKind.RingTrack, transparent), 0.00672);
        _ringTrackHi = Pen(Ink(ArmingSwitchTokenKind.RingTrackHighlight, transparent), 0.0028);
        _lampColours[0] = Ink(ArmingSwitchTokenKind.LampAmber, Colors.Orange);
        _lampColours[1] = green;
        _lampColours[2] = Ink(ArmingSwitchTokenKind.LampRed, Colors.Red);
        for (var i = 0; i < 3; i++)
        {
            var c = _lampColours[i];
            _lit[i] = Radial(0.5, 0.44, 0.7433, 0.7433, (0, Lighten(c, 0.72)), (0.13, Lighten(c, 0.72)), (0.46, c), (1, Shade(c, 0.7)));
            _bloom[i] = Radial(0.5, 0.5, 0.7071, 0.7071, (0, A(c, 0.62)), (0.17, A(c, 0.62)), (0.30, A(c, 0.24)), (0.62, A(c, 0)), (1, A(c, 0)));
            _spill[i] = Radial(0.5, 0.5, 0.5, 0.5, (0, A(c, 0.46)), (0.472, A(c, 0.16)), (1, A(c, 0)));
        }
        _accent = (Owner?.AccentBrush ?? Owner?.AccentInk) is ISolidColorBrush accent ? accent.Color : Colors.LightGray;
        _glintBand = Linear(104, 0.34, 0.744, (0, transparent), (0.22, transparent), (0.48, Color.FromArgb(128, 255, 255, 255)),
            (0.56, Color.FromArgb(36, 255, 255, 255)), (0.76, transparent), (1, transparent));

        // The toggle: washer, nut facets, bushing, shaft, reflected band, ball.
        _washer = Conic(200, (0, Oklch.ToColor(0.36, 0, 0)), (0.25, Oklch.ToColor(0.2, 0, 0)), (0.5, Oklch.ToColor(0.31, 0, 0)),
            (0.75, Oklch.ToColor(0.17, 0, 0)), (1, Oklch.ToColor(0.36, 0, 0)));
        _washerGroove = Radial(0.5, 0.5, 0.7071, 0.7071, (0, transparent), (0.40, transparent), (0.43, Color.FromArgb(102, 0, 0, 0)),
            (0.47, transparent), (1, transparent));
        var faces = new[] { hi, mid, lo, edge, mid, hi };
        for (var i = 0; i < 6; i++)
        {
            _nutFaces[i] = Solid(faces[i]);
        }
        _bushing = Radial(0.5, 0.5, 0.5, 0.5, (0, mid), (0.5, hi), (0.75, lo), (1, mid));
        _bushingHole = Radial(0.5, 0.5, 0.7071, 0.7071, (0, Oklch.ToColor(0.1, 0, 0)), (0.30, Oklch.ToColor(0.1, 0, 0)),
            (0.34, lo), (0.37, A(lo, 0)), (1, A(lo, 0)));
        _shaft = Linear(90, 0.04, 0.135, (0, edge), (0.12, lo), (0.26, mid), (0.38, hi), (0.52, mid), (0.74, lo), (0.90, mid), (1, edge));
        _band = Linear(180, 0.04, 0.27, (0, transparent), (0.30, transparent), (0.42, Color.FromArgb(77, 0, 0, 0)),
            (0.49, Color.FromArgb(158, 255, 255, 255)), (0.56, Color.FromArgb(26, 255, 255, 255)), (0.66, transparent), (1, transparent));
        _ball = Radial(0.36, 0.30, 0.83, 0.83, (0, hi), (0.09, hi), (0.36, mid), (0.70, lo), (1, edge));
        _ballShade = Linear(180, 1, 1, (0, Color.FromArgb(70, 255, 255, 255)), (0.12, transparent), (0.8, transparent), (1, Color.FromArgb(82, 0, 0, 0)));
        _ballSpec = Radial(0.5, 0.5, 0.5, 0.5, (0, Color.FromArgb(242, 255, 255, 255)), (0.7, transparent), (1, transparent));
        _leverShadow = Pen(Color.FromArgb(107, 0, 0, 0), 0.026, PenLineCap.Round);
        _leverShadowSoft = Pen(Color.FromArgb(46, 0, 0, 0), 0.04, PenLineCap.Round);

        // The cover: the accent (or CoverBrush) with its lightness kept in the theme's band.
        var source = (Owner?.CoverBrush ?? Owner?.AccentInk) as ISolidColorBrush;
        var (l, ch, hue) = Oklch.FromColor(source?.Color ?? Colors.Gray);
        var lmin = Number(ArmingSwitchTokenKind.CoverLightnessMin, 0.54);
        var lmax = Number(ArmingSwitchTokenKind.CoverLightnessMax, 0.64);
        _cover = Oklch.ToColor(Math.Clamp(l, lmin, lmax), ch, hue);
        var wallAlpha = Number(ArmingSwitchTokenKind.CoverWallOpacity, 0.76);
        var deep = Shade(_cover, 0.9167);
        var light = Shade(_cover, 1.05);
        _lid = Linear(180, 0.26, 0.4, (0, A(_cover, 0.40)), (0.58, A(_cover, 0.50)), (1, A(_cover, 0.64)));
        _lidSheen = Linear(180, 0.26, 0.4, (0, Color.FromArgb(36, 255, 255, 255)), (0.22, transparent), (1, transparent));
        _lidRing = Pen(A(light, 0.88), 0.002);
        _lidHi = Pen(Color.FromArgb(102, 255, 255, 255), 0.0035);
        _lidLo = Pen(A(Shade(_cover, 0.75), 0.8), 0.005);
        _wallTop = Linear(180, 0.26, 0.16, (0, A(deep, 0.96)), (1, A(_cover, wallAlpha)));
        _wallSide = Linear(90, 0.16, 0.4, (0, A(deep, 0.96)), (1, A(_cover, wallAlpha)));
        _wallBottom = Linear(180, 0.26, 0.16, (0, A(_cover, wallAlpha)), (1, A(deep, 0.96)));
        _wallBottomSheen = Linear(180, 0.26, 0.16, (0, Color.FromArgb(66, 255, 255, 255)), (0.18, transparent), (1, transparent));
        _tab = Linear(180, 0.12, 0.03, (0, A(_cover, 0.68)), (1, A(_cover, 0.56)));
        _tabRing = Pen(A(light, 0.88), 0.0018);
        _rib = Solid(A(Shade(_cover, 0.7778), 0.9));
        _glare = Linear(166, 0.416, 0.28, (0, transparent), (0.18, transparent), (0.44, Color.FromArgb(128, 255, 255, 255)),
            (0.54, Color.FromArgb(31, 255, 255, 255)), (0.74, transparent), (1, transparent));
        _coverShadow = Solid(Shade(_cover, 0.595));
        _contact = Pen(Color.FromArgb(115, 0, 0, 0), 0.0022);

        _noise ??= MakeNoise();
    }

    private void EnsureText()
    {
        if (!_textDirty || Owner is not { } owner)
        {
            return;
        }
        _textDirty = false;
        var family = TextElement.GetFontFamily(this);
        var typeface = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
        var min = Number(ArmingSwitchTokenKind.EngraveMinFontSize, 9);
        _engraveSize = Math.Max(min, 0.029 * _w);
        var small = Math.Max(min, 0.024 * _w);
        _armText = Engraving(owner.ArmLabel, typeface, _engraveSize, 0.11 * _w);
        _safeText = Engraving(owner.SafeLabel, typeface, _engraveSize, 0.11 * _w);
        _readyText = Engraving(owner.ReadyLabel, typeface, small, 0.22 * _w);
        _holdText = Engraving(owner.HoldLabel, typeface, _engraveSize, 0.38 * _w);
    }

    private static Engraved Engraving(string? text, Typeface typeface, double size, double maxWidth)
    {
        var formatted = new FormattedText(text ?? string.Empty, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, size, Brushes.White)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        return new Engraved(formatted.BuildGeometry(new Point(0, 0)), formatted.WidthIncludingTrailingWhitespace, formatted.Height);
    }

    /// <summary>
    /// The brushed grain: a 300 px tile of long horizontal streaks (fractal noise stretched along x, as Arc's
    /// baseFrequency .0025 × .9), baked as white and black at the overlay strength so it lightens and darkens the plate.
    /// </summary>
    private static IBrush? MakeNoise()
    {
        try
        {
            const int side = 300;
            var bitmap = new WriteableBitmap(new PixelSize(side, side), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            var pixels = new int[side * side];
            var random = new Random(7);
            var rows = new double[side];
            for (var y = 0; y < side; y++)
            {
                rows[y] = random.NextDouble();
            }
            // Slow value noise along x: four control points per row, smoothly interpolated (wraps at the tile edge).
            var knots = new double[side * 5];
            for (var i = 0; i < knots.Length; i++)
            {
                knots[i] = random.NextDouble();
            }
            for (var y = 0; y < side; y++)
            {
                var y2 = (y + 1) % side;
                for (var x = 0; x < side; x++)
                {
                    var fx = x / (double)side * 4;
                    var k = (int)fx;
                    var t = fx - k;
                    t = t * t * (3 - (2 * t));
                    var a = knots[(y * 5) + k];
                    var b = knots[(y * 5) + ((k + 1) % 4)];
                    var slow = a + ((b - a) * t);
                    var v = (0.55 * ((rows[y] * 0.7) + (rows[y2] * 0.3))) + (0.45 * slow);
                    var d = (v - 0.5) * 2;
                    var alpha = (int)Math.Clamp(Math.Abs(d) * 85, 0, 255);
                    var c = d > 0 ? alpha : 0;
                    pixels[(y * side) + x] = (alpha << 24) | (c << 16) | (c << 8) | c;
                }
            }
            using (var locked = bitmap.Lock())
            {
                for (var y = 0; y < side; y++)
                {
                    Marshal.Copy(pixels, y * side, locked.Address + (y * locked.RowBytes), side);
                }
            }
            return new ImageBrush(bitmap)
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.Fill,
                DestinationRect = new RelativeRect(0, 0, side, side, RelativeUnit.Absolute)
            }.ToImmutable();
        }
        catch (Exception)
        {
            // No bitmap support (some headless hosts): the plate simply has no grain.
            return null;
        }
    }
}
