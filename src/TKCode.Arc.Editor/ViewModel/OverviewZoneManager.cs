// Ported from vscode (src/vs/editor/common/viewModel/overviewZoneManager.ts, commit b57d5c9e), MIT license.
//
// Differences:
//  - the color to id map is a Dictionary (upstream: an object without prototype); ids start at 1 as upstream, so
//    GetId2Color()[0] is null;
//  - `x | 0` truncations are (int) casts of the doubles.

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>A painted band of the overview ruler, in canvas pixels.</summary>
internal sealed record ColorZone
{
    public ColorZone(double from, double to, int colorId)
    {
        From = (int)from;
        To = (int)to;
        ColorId = colorId;
    }

    public int From { get; }

    public int To { get; }

    public int ColorId { get; }

    public static int Compare(ColorZone a, ColorZone b)
    {
        if (a.ColorId == b.ColorId)
        {
            if (a.From == b.From)
            {
                return a.To - b.To;
            }

            return a.From - b.From;
        }

        return a.ColorId - b.ColorId;
    }
}

/// <summary>A zone in the overview ruler.</summary>
internal sealed class OverviewRulerZone
{
    private ColorZone? _colorZone;

    /// <param name="startLineNumber">The first line of the zone.</param>
    /// <param name="endLineNumber">The last line of the zone.</param>
    /// <param name="heightInLines">If 0, the height in lines is determined by <paramref name="endLineNumber"/>.</param>
    /// <param name="color">The zone's color.</param>
    public OverviewRulerZone(int startLineNumber, int endLineNumber, int heightInLines, string color)
    {
        StartLineNumber = startLineNumber;
        EndLineNumber = endLineNumber;
        HeightInLines = heightInLines;
        Color = color;
    }

    public int StartLineNumber { get; }

    public int EndLineNumber { get; }

    /// <summary>If set to 0, the height in lines will be determined based on <see cref="EndLineNumber"/>.</summary>
    public int HeightInLines { get; }

    public string Color { get; }

    public static int Compare(OverviewRulerZone a, OverviewRulerZone b)
    {
        if (a.Color == b.Color)
        {
            if (a.StartLineNumber == b.StartLineNumber)
            {
                if (a.HeightInLines == b.HeightInLines)
                {
                    return a.EndLineNumber - b.EndLineNumber;
                }

                return a.HeightInLines - b.HeightInLines;
            }

            return a.StartLineNumber - b.StartLineNumber;
        }

        return string.CompareOrdinal(a.Color, b.Color) < 0 ? -1 : 1;
    }

    public void SetColorZone(ColorZone colorZone)
    {
        _colorZone = colorZone;
    }

    public ColorZone? GetColorZones() => _colorZone;
}

/// <summary>Turns overview ruler zones (line ranges with a color) into painted bands of a ruler of a given size.</summary>
internal sealed class OverviewZoneManager
{
    private const int MinimumHeight = 4;

    private readonly Func<int, double> _getVerticalOffsetForLine;
    private readonly Dictionary<string, int> _color2Id = new(StringComparer.Ordinal);
    private readonly List<string?> _id2Color = [null];
    private List<OverviewRulerZone> _zones = [];
    private bool _colorZonesInvalid;
    private double _lineHeight;
    private double _domWidth;
    private double _domHeight;
    private double _outerHeight;
    private double _pixelRatio = 1;
    private int _lastAssignedId;

    public OverviewZoneManager(Func<int, double> getVerticalOffsetForLine)
    {
        _getVerticalOffsetForLine = getVerticalOffsetForLine;
    }

    /// <summary>The color of each id (index 0 is unused).</summary>
    public IReadOnlyList<string?> GetId2Color() => _id2Color;

    public void SetZones(IEnumerable<OverviewRulerZone> newZones)
    {
        _zones = [.. newZones];
        _zones.Sort(OverviewRulerZone.Compare);
    }

    public bool SetLineHeight(double lineHeight)
    {
        if (_lineHeight == lineHeight)
        {
            return false;
        }

        _lineHeight = lineHeight;
        _colorZonesInvalid = true;
        return true;
    }

    public void SetPixelRatio(double pixelRatio)
    {
        _pixelRatio = pixelRatio;
        _colorZonesInvalid = true;
    }

    public double GetDOMWidth() => _domWidth;

    public double GetCanvasWidth() => _domWidth * _pixelRatio;

    public bool SetDOMWidth(double width)
    {
        if (_domWidth == width)
        {
            return false;
        }

        _domWidth = width;
        _colorZonesInvalid = true;
        return true;
    }

    public double GetDOMHeight() => _domHeight;

    public double GetCanvasHeight() => _domHeight * _pixelRatio;

    public bool SetDOMHeight(double height)
    {
        if (_domHeight == height)
        {
            return false;
        }

        _domHeight = height;
        _colorZonesInvalid = true;
        return true;
    }

    public double GetOuterHeight() => _outerHeight;

    public bool SetOuterHeight(double outerHeight)
    {
        if (_outerHeight == outerHeight)
        {
            return false;
        }

        _outerHeight = outerHeight;
        _colorZonesInvalid = true;
        return true;
    }

    public List<ColorZone> ResolveColorZones()
    {
        var colorZonesInvalid = _colorZonesInvalid;
        var lineHeight = Math.Floor(_lineHeight);
        var totalHeight = Math.Floor(GetCanvasHeight());
        var outerHeight = Math.Floor(_outerHeight);
        var heightRatio = totalHeight / outerHeight;
        var halfMinimumHeight = Math.Floor(MinimumHeight * _pixelRatio / 2);

        var allColorZones = new List<ColorZone>(_zones.Count);
        foreach (var zone in _zones)
        {
            if (!colorZonesInvalid && zone.GetColorZones() is { } cached)
            {
                allColorZones.Add(cached);
                continue;
            }

            var offset1 = _getVerticalOffsetForLine(zone.StartLineNumber);
            var offset2 = zone.HeightInLines == 0
                ? _getVerticalOffsetForLine(zone.EndLineNumber) + lineHeight
                : offset1 + (zone.HeightInLines * lineHeight);

            var y1 = Math.Floor(heightRatio * offset1);
            var y2 = Math.Floor(heightRatio * offset2);

            var ycenter = Math.Floor((y1 + y2) / 2);
            var halfHeight = y2 - ycenter;

            if (halfHeight < halfMinimumHeight)
            {
                halfHeight = halfMinimumHeight;
            }

            if (ycenter - halfHeight < 0)
            {
                ycenter = halfHeight;
            }

            if (ycenter + halfHeight > totalHeight)
            {
                ycenter = totalHeight - halfHeight;
            }

            var color = zone.Color;
            if (!_color2Id.TryGetValue(color, out var colorId))
            {
                colorId = ++_lastAssignedId;
                _color2Id[color] = colorId;
                _id2Color.Add(color);
            }

            var colorZone = new ColorZone(ycenter - halfHeight, ycenter + halfHeight, colorId);
            zone.SetColorZone(colorZone);
            allColorZones.Add(colorZone);
        }

        _colorZonesInvalid = false;
        allColorZones.Sort(ColorZone.Compare);
        return allColorZones;
    }
}
