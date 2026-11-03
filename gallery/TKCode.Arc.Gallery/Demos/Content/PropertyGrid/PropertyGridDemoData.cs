using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public enum DemoLineType
{
    Continuous,
    Dashed,
    Dotted,
    [Description("Dash dot")] DashDot
}

public enum DemoLineWeight
{
    [Description("By layer")] ByLayer,
    [Description("0.13 mm")] Thin,
    [Description("0.35 mm")] Medium,
    [Description("0.70 mm")] Heavy
}

/// <summary>A point in a drawing. A struct: the grid edits a copy and writes it back to the shape.</summary>
[PropertyGridModel]
public struct DemoPoint
{
    public DemoPoint(double x, double y, double z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [PropertyGridOptions(FractionDigits = 4)]
    public double X { get; set; }

    [PropertyGridOptions(FractionDigits = 4)]
    public double Y { get; set; }

    [PropertyGridOptions(FractionDigits = 4)]
    public double Z { get; set; }

    public override readonly string ToString() => string.Format(CultureInfo.CurrentCulture, "{0:0.####}, {1:0.####}, {2:0.####}", X, Y, Z);
}

/// <summary>What every shape of the demo drawing has: the properties of the General category.</summary>
public abstract class DemoEntity : INotifyPropertyChanged
{
    private Color _color = Color.Parse("#2F6BFF");
    private string _layer = "Walls";
    private DemoLineType _lineType;
    private double _lineTypeScale = 1;
    private DemoLineWeight _lineWeight;
    private int _transparency;
    private bool _visible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    [Category("General"), Description("The color the shape is drawn in.")]
    public Color Color { get => _color; set => Set(ref _color, value); }

    [Category("General"), PropertyGridOptions(EditorKey = "Layer"), Description("The layer the shape belongs to; the layer decides what is shown and printed.")]
    public string Layer { get => _layer; set => Set(ref _layer, value); }

    [Category("General"), DisplayName("Line type")]
    public DemoLineType LineType { get => _lineType; set => Set(ref _lineType, value); }

    [Category("General"), DisplayName("Line type scale"), PropertyGridOptions(Minimum = 0.01, FractionDigits = 2, Step = 0.1)]
    public double LineTypeScale { get => _lineTypeScale; set => Set(ref _lineTypeScale, value); }

    [Category("General"), DisplayName("Line weight")]
    public DemoLineWeight LineWeight { get => _lineWeight; set => Set(ref _lineWeight, value); }

    [Category("General"), PropertyGridOptions(Minimum = 0, Maximum = 90), Description("0 is opaque; 90 is the most transparent a shape can be.")]
    public int Transparency { get => _transparency; set => Set(ref _transparency, value); }

    [Category("General")]
    public bool Visible { get => _visible; set => Set(ref _visible, value); }

    /// <summary>Raises PropertyChanged for every property: computed values (area, end points) follow any edit.</summary>
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public override string ToString() => GetType().Name.Replace("Demo", string.Empty, StringComparison.Ordinal);
}

[PropertyGridModel]
public sealed class DemoArc : DemoEntity
{
    private DemoPoint _center = new(-62688.3572, -866975.6469);
    private double _radius = 1624.1491;
    private double _startAngle = 45;
    private double _endAngle = 135;
    private double _thickness;

    [Category("3D Visualization"), Description("Extrusion along the Z axis.")]
    public double Thickness { get => _thickness; set => Set(ref _thickness, value); }

    [Category("Geometry"), Description("The center of the circle the arc lies on.")]
    public DemoPoint Center { get => _center; set => Set(ref _center, value); }

    [Category("Geometry"), PropertyGridOptions(Minimum = 0, FractionDigits = 4, Classes = "key")]
    public double Radius { get => _radius; set => Set(ref _radius, value); }

    [Category("Geometry"), DisplayName("Start angle"), PropertyGridOptions(Minimum = 0, Maximum = 360, FractionDigits = 1)]
    public double StartAngle { get => _startAngle; set => Set(ref _startAngle, value); }

    [Category("Geometry"), DisplayName("End angle"), PropertyGridOptions(Minimum = 0, Maximum = 360, FractionDigits = 1)]
    public double EndAngle { get => _endAngle; set => Set(ref _endAngle, value); }

    [Category("Geometry"), DisplayName("Start point"), Description("Computed from the center, radius and start angle.")]
    public DemoPoint StartPoint => PointAt(_startAngle);

    [Category("Geometry"), DisplayName("End point"), Description("Computed from the center, radius and end angle.")]
    public DemoPoint EndPoint => PointAt(_endAngle);

    [Category("Geometry"), DisplayName("Arc length")]
    public double ArcLength => _radius * Sweep;

    [Category("Geometry")]
    public double Area => 0.5 * _radius * _radius * (Sweep - Math.Sin(Sweep));

    private double Sweep => ((_endAngle - _startAngle) % 360 + 360) % 360 * Math.PI / 180;

    private DemoPoint PointAt(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new DemoPoint(_center.X + _radius * Math.Cos(radians), _center.Y + _radius * Math.Sin(radians), _center.Z);
    }
}

[PropertyGridModel]
public sealed class DemoLine : DemoEntity
{
    private DemoPoint _start = new(-63267.2866, -868493.1123);
    private DemoPoint _end = new(-62111.9133, -868494.0582);

    [Category("Geometry"), DisplayName("Start point")]
    public DemoPoint Start { get => _start; set => Set(ref _start, value); }

    [Category("Geometry"), DisplayName("End point")]
    public DemoPoint End { get => _end; set => Set(ref _end, value); }

    [Category("Geometry")]
    public double Length => Math.Sqrt(Math.Pow(_end.X - _start.X, 2) + Math.Pow(_end.Y - _start.Y, 2) + Math.Pow(_end.Z - _start.Z, 2));
}

[PropertyGridModel]
public sealed class DemoPolyline : DemoEntity
{
    private bool _closed;
    private double _elevation;

    [Category("Geometry"), Description("The corners of the polyline, in order. Each vertex expands into X, Y and Z.")]
    public ObservableCollection<DemoPoint> Vertices { get; } = [new(0, 0), new(1200, 0), new(1200, 800), new(0, 800)];

    [Category("Geometry")]
    public bool Closed { get => _closed; set => Set(ref _closed, value); }

    [Category("Geometry")]
    public double Elevation { get => _elevation; set => Set(ref _elevation, value); }

    [Category("Data"), Description("Free-form tags; the add button appends an empty one.")]
    public List<string> Tags { get; } = ["exterior", "level 1"];

    [Category("Data"), DisplayName("Vertex count")]
    public int VertexCount => Vertices.Count;
}

internal static class PropertyGridDemoData
{
    public static readonly IReadOnlyList<string> Layers = ["0", "Walls", "Doors", "Windows", "Furniture", "Dimensions"];

    /// <summary>The drawing of the multi-select demo: an arc and two lines on different layers.</summary>
    public static DemoEntity[] Drawing() =>
    [
        new DemoArc(),
        new DemoLine { Layer = "Doors" },
        new DemoLine { Start = new DemoPoint(0, 0), End = new DemoPoint(2400, 0), Color = Color.Parse("#E5484D") }
    ];
}
