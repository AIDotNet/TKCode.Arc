namespace TKCode.Arc;

/// <summary>One flowing dot. Position is in columns along the band and 0 to 1 across its layer.</summary>
internal struct FunnelDot
{
    public bool IsAlive;
    public int Layer;
    public int ExitAt;
    public double Along;
    public double Lane;
    public double Speed;

    /// <summary>Seconds since the dot started leaving the band; negative while it is inside.</summary>
    public double Leaving;
}

/// <summary>
/// The dot stream: a fixed pool, so a frame allocates nothing. A dot picks its segment by the share of the first step,
/// and at each step leaves with exactly that step's drop-off, so what it shows over time is the real conversion.
/// </summary>
internal sealed class FunnelFlowSim
{
    public const int Capacity = 40;
    public const double LeaveSeconds = 0.7;
    private const double TraverseSeconds = 9;
    private const double SpawnInterval = 0.07;

    private readonly FunnelDot[] _dots = new FunnelDot[Capacity];
    private readonly Random _random;
    private FunnelModel? _model;
    private double[] _weights = [];
    private double _weightSum;
    private double _clock;
    private int _stages;
    private bool _warm;

    public FunnelFlowSim(int seed = 7) => _random = new Random(seed);

    public ReadOnlySpan<FunnelDot> Dots => _dots;

    public int AliveCount
    {
        get
        {
            var alive = 0;
            for (var i = 0; i < Capacity; i++)
            {
                alive += _dots[i].IsAlive ? 1 : 0;
            }
            return alive;
        }
    }

    /// <summary>New numbers or a new view: dots of segments that are no longer shown leave, the others carry on.</summary>
    public void SetModel(FunnelModel model, string? view)
    {
        var reshaped = _model is null || model.StageCount != _stages || model.LayerCount != _weights.Length;
        _model = model;
        _stages = model.StageCount;
        var isolated = model.IsolatedLayer(view);
        if (reshaped)
        {
            _weights = new double[model.LayerCount];
            Array.Clear(_dots);
            _warm = false;
        }
        _weightSum = 0;
        for (var k = 0; k < _weights.Length; k++)
        {
            _weights[k] = _stages == 0 || isolated >= 0 && isolated != k ? 0 : model.LayerCountAt(k, 0);
            _weightSum += _weights[k];
        }
        for (var i = 0; i < Capacity; i++)
        {
            ref var dot = ref _dots[i];
            if (dot.IsAlive && dot.Leaving < 0 && _weights[dot.Layer] <= 0)
            {
                dot.Leaving = 0;
            }
        }
    }

    /// <summary>Fills the band once, so the first frame already shows a stream instead of a first dot crossing alone.</summary>
    public void WarmUp()
    {
        if (_warm)
        {
            return;
        }
        _warm = true;
        for (var i = 0; i < 120; i++)
        {
            Step(0.1);
        }
    }

    public void Step(double seconds)
    {
        if (_model is null || _stages == 0)
        {
            return;
        }
        for (var i = 0; i < Capacity; i++)
        {
            ref var dot = ref _dots[i];
            if (!dot.IsAlive)
            {
                continue;
            }
            if (dot.Leaving >= 0)
            {
                dot.Leaving += seconds;
                dot.Along += dot.Speed * 0.35 * seconds;
                dot.IsAlive = dot.Leaving < LeaveSeconds;
                continue;
            }
            dot.Along += dot.Speed * seconds;
            if (dot.Along >= dot.ExitAt)
            {
                dot.Leaving = 0;
            }
            else if (dot.Along >= _stages)
            {
                dot.IsAlive = false;
            }
        }
        _clock += seconds;
        while (_clock >= SpawnInterval)
        {
            _clock -= SpawnInterval;
            if (!Spawn())
            {
                _clock = 0;
            }
        }
    }

    /// <summary>The stage a new dot of this layer leaves at: stage i with the drop-off of stage i, or never (past the last stage).</summary>
    public int RollExit(int layer)
    {
        for (var i = 1; i < _stages; i++)
        {
            if (_random.NextDouble() < _model!.ExitChance(layer, i))
            {
                return i;
            }
        }
        return _stages + 1;
    }

    private bool Spawn()
    {
        if (_weightSum <= 0)
        {
            return false;
        }
        for (var i = 0; i < Capacity; i++)
        {
            if (_dots[i].IsAlive)
            {
                continue;
            }
            var layer = PickLayer();
            _dots[i] = new FunnelDot
            {
                IsAlive = true,
                Layer = layer,
                ExitAt = RollExit(layer),
                Along = 0,
                Lane = 0.14 + _random.NextDouble() * 0.72,
                Speed = _stages / TraverseSeconds * (0.82 + _random.NextDouble() * 0.36),
                Leaving = -1
            };
            return true;
        }
        return false;
    }

    private int PickLayer()
    {
        var pick = _random.NextDouble() * _weightSum;
        var last = 0;
        for (var k = 0; k < _weights.Length; k++)
        {
            if (_weights[k] <= 0)
            {
                continue;
            }
            last = k;
            pick -= _weights[k];
            if (pick <= 0)
            {
                return k;
            }
        }
        return last;
    }
}
