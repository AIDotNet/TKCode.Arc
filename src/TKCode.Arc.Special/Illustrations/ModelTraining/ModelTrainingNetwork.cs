using Avalonia;

namespace TKCode.Arc;

/// <summary>One connection of the drawn network: its layer pair, its two units, its curve and its weights.</summary>
internal sealed class ModelTrainingEdge
{
    public ModelTrainingEdge(int gap, int from, int to)
    {
        Gap = gap;
        From = from;
        To = to;
    }

    public int Gap { get; }

    public int From { get; }

    public int To { get; }

    /// <summary>The S-curve between the two units (rebuilt with the layout).</summary>
    public ExplainerPath Path { get; } = new();

    /// <summary>The untrained (random) stroke width.</summary>
    public double StartWidth { get; set; }

    /// <summary>The stroke width once learned (only used when <see cref="Learned"/> is above 0).</summary>
    public double LearnedWidth { get; set; }

    /// <summary>How much this connection ends up carrying the answer (0 = not at all, 1 = fully).</summary>
    public double Learned { get; set; }
}

/// <summary>
/// The geometry of a <see cref="ModelTraining"/> diagram for one width (Arc Model training, design reference): the two
/// data stacks on the left (side by side above the network below 480 px), the layers on 2.5D plates, the output rows,
/// the layer labels and the hover regions. Wide (diagram width W): sheets min(100, 0.15 W) wide and 0.68 of that high,
/// input at (sheet + 19) + 0.097 W, output at W − 112, units 44 apart around y 148, labels on y 254, height 316.
/// </summary>
internal sealed class ModelTrainingLayout
{
    /// <summary>Below this diagram width the stacks move above the network.</summary>
    public const double NarrowWidth = 480;

    private readonly List<ModelTrainingEdge> _edges = new();
    private double[] _layerX = [];
    private int[] _sizes = [3, 4, 4, 2];
    private int _seed = 1;

    public double Width { get; private set; }

    public double Height { get; private set; } = 316;

    public bool IsNarrow { get; private set; }

    public double SheetWidth { get; private set; } = 96;

    public double SheetHeight { get; private set; } = 65;

    public double SheetRadius { get; private set; } = 9;

    /// <summary>The offset from one sheet to the one behind it.</summary>
    public Vector SlotStep { get; private set; } = new(5, -5);

    /// <summary>The centre of the front sheet of the training stack and of the held-out stack.</summary>
    public Point TrainingFront { get; private set; }

    public Point HeldOutFront { get; private set; }

    /// <summary>Visible sheets in each stack (one more waits hidden behind them).</summary>
    public int TrainingSheets => 4;

    public int HeldOutSheets => 3;

    /// <summary>Where the stacks' names and counts start (left, baseline).</summary>
    public Point TrainingText { get; private set; }

    public Point HeldOutText { get; private set; }

    public Rect TrainingHit { get; private set; }

    public Rect HeldOutHit { get; private set; }

    public double NodeRadius { get; private set; } = 6.5;

    public double NodeSpacing { get; private set; } = 44;

    public double CenterY { get; private set; } = 148;

    public double PlateWidth { get; private set; } = 28;

    /// <summary>The back plate's offset from the face (the 2.5D depth).</summary>
    public Vector PlateDepth { get; private set; } = new(5, -5);

    public double LabelBaseline { get; private set; } = 254;

    public IReadOnlyList<int> Sizes => _sizes;

    public IReadOnlyList<ModelTrainingEdge> Edges => _edges;

    public int LayerCount => _sizes.Length;

    /// <summary>Sets the layer sizes and the weights' seed; the layout is solved again by the next <see cref="Solve"/>.</summary>
    public void SetNetwork(int[] sizes, int seed)
    {
        _sizes = sizes;
        _seed = seed;
        _edges.Clear();
        for (var g = 0; g < sizes.Length - 1; g++)
        {
            for (var i = 0; i < sizes[g]; i++)
            {
                for (var j = 0; j < sizes[g + 1]; j++)
                {
                    _edges.Add(new ModelTrainingEdge(g, i, j));
                }
            }
        }
        AssignWeights();
        Width = 0;
    }

    /// <summary>Lays the diagram out for <paramref name="width"/>; returns false when nothing changed.</summary>
    public bool Solve(double width)
    {
        width = Math.Max(160, Math.Round(width, 1));
        if (Math.Abs(width - Width) < 0.05 && _layerX.Length == _sizes.Length)
        {
            return false;
        }
        Width = width;
        IsNarrow = width < NarrowWidth;
        var maxUnits = 1;
        foreach (var n in _sizes)
        {
            maxUnits = Math.Max(maxUnits, n);
        }
        _layerX = new double[_sizes.Length];
        if (IsNarrow)
        {
            Height = 301;
            SheetWidth = 64;
            SheetHeight = 41;
            SheetRadius = 6;
            SlotStep = new Vector(4, -4);
            TrainingFront = new Point(2 + SheetWidth / 2, 36.5);
            HeldOutFront = new Point(106 + SheetWidth / 2, 36.5);
            TrainingText = new Point(2, 75);
            HeldOutText = new Point(106, 75);
            TrainingHit = new Rect(0, 4, 80, 93);
            HeldOutHit = new Rect(104, 8, 80, 89);
            NodeRadius = 5.5;
            NodeSpacing = Math.Min(36, 144 / Math.Max(1.0, maxUnits - 1));
            CenterY = 201;
            PlateWidth = 22;
            PlateDepth = new Vector(4, -4);
            LabelBaseline = 291;
            SpreadLayers(15, width - 78);
        }
        else
        {
            Height = 316;
            SheetWidth = Math.Round(Math.Min(100, 0.15 * width), 2);
            SheetHeight = Math.Round(SheetWidth * 0.68, 2);
            SheetRadius = 9;
            SlotStep = new Vector(5, -5);
            var trainingGroup = 15 + SheetHeight + 40;
            var heldOutGroup = 10 + SheetHeight + 40;
            var gap = Math.Max(4, (311 - trainingGroup - heldOutGroup) / 3);
            var front = 2 + SheetWidth / 2;
            TrainingFront = new Point(front, gap + 15 + SheetHeight / 2);
            var heldOutTop = gap + trainingGroup + gap;
            HeldOutFront = new Point(front, heldOutTop + 10 + SheetHeight / 2);
            TrainingText = new Point(2, TrainingFront.Y + SheetHeight / 2 + 18);
            HeldOutText = new Point(2, HeldOutFront.Y + SheetHeight / 2 + 18);
            var column = SheetWidth + 19;
            TrainingHit = new Rect(0, gap, column, trainingGroup);
            HeldOutHit = new Rect(0, heldOutTop, column, heldOutGroup);
            NodeRadius = 6.5;
            NodeSpacing = Math.Min(44, 176 / Math.Max(1.0, maxUnits - 1));
            CenterY = 148;
            PlateWidth = 28;
            PlateDepth = new Vector(5, -5);
            LabelBaseline = 254;
            SpreadLayers(column + 0.097 * width, width - 112);
        }
        BuildPaths();
        return true;
    }

    /// <summary>The x of layer <paramref name="layer"/>'s units.</summary>
    public double LayerX(int layer) => _layerX[layer];

    /// <summary>The centre of unit <paramref name="unit"/> of layer <paramref name="layer"/>.</summary>
    public Point Node(int layer, int unit)
    {
        var n = _sizes[layer];
        return new Point(_layerX[layer], CenterY + (unit - (n - 1) / 2.0) * NodeSpacing);
    }

    /// <summary>The face of layer <paramref name="layer"/>'s plate (the back plate sits <see cref="PlateDepth"/> behind it).</summary>
    public Rect Plate(int layer)
    {
        var n = _sizes[layer];
        var height = (n - 1) * NodeSpacing + PlateWidth + (IsNarrow ? 10 : 12);
        return new Rect(_layerX[layer] - PlateWidth / 2, CenterY - height / 2, PlateWidth, height);
    }

    /// <summary>The region that shows the layer's hover detail.</summary>
    public Rect LayerHit(int layer)
    {
        var plate = Plate(layer);
        var pad = IsNarrow ? 14 : 12;
        return new Rect(plate.X - pad, plate.Y - 9, plate.Width + 2 * pad + 5, plate.Height + 13);
    }

    /// <summary>The centre of sheet <paramref name="slot"/> (0 = front) of a stack.</summary>
    public Point Sheet(bool heldOut, double slot)
    {
        var front = heldOut ? HeldOutFront : TrainingFront;
        return front + SlotStep * slot;
    }

    /// <summary>Where the output rows start (left of the class names).</summary>
    public double OutputTextX => _layerX[^1] + (IsNarrow ? 22 : 29);

    private void SpreadLayers(double first, double last)
    {
        var count = _layerX.Length;
        for (var l = 0; l < count; l++)
        {
            _layerX[l] = count == 1 ? first : first + (last - first) * l / (count - 1);
        }
    }

    private void BuildPaths()
    {
        var reach = NodeRadius + 1;
        foreach (var edge in _edges)
        {
            var a = Node(edge.Gap, edge.From);
            var b = Node(edge.Gap + 1, edge.To);
            var start = new Point(a.X + reach, a.Y);
            var end = new Point(b.X - reach, b.Y);
            var mid = (start.X + end.X) / 2;
            edge.Path.Clear().MoveTo(start).CubicTo(new Point(mid, start.Y), new Point(mid, end.Y), end, 28);
        }
    }

    private void AssignWeights()
    {
        foreach (var edge in _edges)
        {
            var key = (edge.Gap + 1) * 7919 + edge.From * 131 + edge.To * 17 + _seed * 104729;
            edge.StartWidth = 0.72 + 0.55 * Noise(key);
            edge.LearnedWidth = 1.96 + 0.31 * Noise(key + 5);
            edge.Learned = 0;
        }
        // About three in four units after the input keep one strong incoming connection, a few keep a second, and no unit
        // feeds more than two: a few paths through the network carry the answer, the rest stay light.
        for (var g = 0; g < _sizes.Length - 1; g++)
        {
            var from = _sizes[g];
            var fed = new int[from];
            var last = g == _sizes.Length - 2;
            for (var j = 0; j < _sizes[g + 1]; j++)
            {
                if (!last && Noise(g * 61 + j * 23 + _seed * 17) > 0.75)
                {
                    continue;
                }
                var first = Pick(fed, (int)(Noise(g * 97 + j * 13 + _seed * 31) * from) % from);
                Mark(g, first, j, 1);
                fed[first]++;
                if (Noise(g * 53 + j * 29 + _seed * 7) < 0.3 && from > 1)
                {
                    var second = Pick(fed, (first + 1 + (int)(Noise(g * 11 + j * 3 + _seed) * (from - 1))) % from);
                    if (second != first)
                    {
                        Mark(g, second, j, 0.7 + 0.25 * Noise(g * 5 + j * 41 + _seed));
                        fed[second]++;
                    }
                }
            }
        }
    }

    /// <summary>The unit <paramref name="preferred"/>, or the next one that feeds fewer than two strong connections.</summary>
    private static int Pick(int[] fed, int preferred)
    {
        for (var k = 0; k < fed.Length; k++)
        {
            var i = (preferred + k) % fed.Length;
            if (fed[i] < 2)
            {
                return i;
            }
        }
        return preferred;
    }

    private void Mark(int gap, int from, int to, double learned)
    {
        foreach (var edge in _edges)
        {
            if (edge.Gap == gap && edge.From == from && edge.To == to)
            {
                edge.Learned = Math.Max(edge.Learned, learned);
                return;
            }
        }
    }

    /// <summary>A stable value in [0, 1) for a key.</summary>
    internal static double Noise(int key)
    {
        var x = Math.Sin(key * 12.9898 + 78.233) * 43758.5453;
        return x - Math.Floor(x);
    }
}
