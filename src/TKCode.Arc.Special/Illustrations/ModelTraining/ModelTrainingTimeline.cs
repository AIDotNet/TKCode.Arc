namespace TKCode.Arc;

/// <summary>What a pass of signals does: a batch sheet flying in, a forward hop or a backward hop.</summary>
internal enum ModelTrainingPassKind
{
    TrainingFlight,
    HeldOutFlight,
    Forward,
    Backward
}

/// <summary>
/// One timed pass of a <see cref="ModelTraining"/> run: a sheet flying from a stack into the input layer, signals
/// riding the connections between two layers (forward), or the error riding them back (backward). <c>Gap</c> is the
/// layer pair (0 = input to the first hidden layer); <c>Update</c> is the training step a backward pass belongs to.
/// </summary>
internal readonly record struct ModelTrainingPass(ModelTrainingPassKind Kind, double Start, double Duration, int Gap, int Update)
{
    public double End => Start + Duration;

    /// <summary>The linear progress 0..1 at <paramref name="t"/>.</summary>
    public double Progress(double t) => Duration <= 0 ? (t >= Start ? 1 : 0) : Math.Clamp((t - Start) / Duration, 0, 1);

    public bool IsActive(double t) => t >= Start && t <= End;
}

/// <summary>
/// The schedule of a <see cref="ModelTraining"/> run and everything that is a function of its time (Arc Model training,
/// design reference: one run of 21.45 s at speed 1 with the default six training steps). Stage starts: batch 0, forward
/// 3.2, backward 6.2, epochs 9.2, evaluate 9.2 + epochs length; the first 0.8 s relax the finished model of the previous
/// run back to an untrained one, so a looping run is seamless. Built once per data change; reading allocates nothing.
/// </summary>
internal sealed class ModelTrainingTimeline
{
    /// <summary>The relax time at the start of a run (previous fills empty, weights return, readouts swap at <see cref="SwapAt"/>).</summary>
    public const double ResetLength = 0.8;

    /// <summary>When the readouts swap from the finished run to the untrained model.</summary>
    public const double SwapAt = 0.35;

    public const double ForwardStart = 3.2;
    public const double BackwardStart = 6.2;
    public const double EpochsStart = 9.2;

    /// <summary>The spacing of the training cycles in the epochs stage and the length of one.</summary>
    public const double CyclePeriod = 1.34;
    public const double CycleLength = 1.8;

    /// <summary>The evaluate stage's length (held-out flight, one slow forward pass, the count, the hold).</summary>
    public const double EvaluateLength = 5.1;

    /// <summary>The loss curve's fall rate (Arc's curve halves its excess about every 0.18 of the run).</summary>
    private const double CurveRate = 3.8;

    private readonly List<ModelTrainingPass> _passes = new();
    private double[] _glideStart = [];
    private double[] _glideLength = [];
    private double[] _values = [];

    /// <summary>The number of training steps (one in the first three stages, the rest in the epochs stage).</summary>
    public int Steps { get; private set; } = 6;

    public int Epochs { get; private set; } = 12;

    public double LossFrom { get; private set; } = 0.69;

    public double LossTo { get; private set; } = 0.08;

    public double Accuracy { get; private set; } = 94.2;

    public double Target { get; private set; } = 90;

    /// <summary>How far one update moves the weights (0..1, from the learning rate).</summary>
    public double Rate { get; private set; } = 0.35;

    public double EpochsLength { get; private set; }

    public double EvaluateStart { get; private set; }

    public double Length { get; private set; }

    /// <summary>When the first forward pass reaches the outputs: the first loss is measured.</summary>
    public double FirstMeasure { get; private set; }

    public IReadOnlyList<ModelTrainingPass> Passes => _passes;

    /// <summary>The validation loss shown at the end, just above the last training loss.</summary>
    public double ValidationLoss => LossTo + (LossFrom - LossTo) * 0.05;

    public bool PassesTarget => Accuracy >= Target;

    /// <summary>The start of stage <paramref name="stage"/> (0 to 4).</summary>
    public double StageStart(int stage) => stage switch
    {
        <= 0 => 0,
        1 => ForwardStart,
        2 => BackwardStart,
        3 => EpochsStart,
        _ => EvaluateStart
    };

    /// <summary>The end of stage <paramref name="stage"/>.</summary>
    public double StageEnd(int stage) => stage >= 4 ? Length : StageStart(stage + 1);

    /// <summary>The moment a paused reader sees for a stage (its still frame).</summary>
    public double StillTime(int stage) => stage switch
    {
        <= 0 => 2.6,
        1 => ForwardStart + 0.05 + 0.92 + 0.4,
        2 => BackwardStart + 0.05 + 0.87 + 0.66,
        3 => EpochsStart + EpochsLength * 0.62,
        _ => Length
    };

    /// <summary>The stage at <paramref name="t"/>.</summary>
    public int StageAt(double t)
    {
        for (var s = 4; s > 0; s--)
        {
            if (t >= StageStart(s))
            {
                return s;
            }
        }
        return 0;
    }

    /// <summary>The share of a stage's bar filled at <paramref name="t"/> (the batch bar fills after the relax).</summary>
    public double StageProgress(int stage, double t)
    {
        var start = stage == 0 ? ResetLength : StageStart(stage);
        var end = StageEnd(stage);
        return end <= start ? 1 : Math.Clamp((t - start) / (end - start), 0, 1);
    }

    /// <summary>Lays the run out for the given data.</summary>
    public void Build(int steps, int epochs, double lossFrom, double lossTo, IReadOnlyList<double>? losses, double accuracy, double target, double learningRate)
    {
        Steps = Math.Clamp(steps, 2, 20);
        Epochs = Math.Max(1, epochs);
        Accuracy = Math.Clamp(accuracy, 0, 100);
        Target = Math.Clamp(target, 0, 100);
        Rate = Math.Clamp(learningRate * 7, 0.05, 0.9);
        BuildCurve(lossFrom, lossTo, losses);

        _passes.Clear();
        var n = Steps;
        _glideStart = new double[n];
        _glideLength = new double[n];

        // Step 1, told slowly over the first three stages.
        _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.TrainingFlight, 1.0, 1.5, 0, 1));
        for (var h = 0; h < 3; h++)
        {
            _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.Forward, ForwardStart + 0.05 + h * 0.92, 0.8, h, 1));
        }
        FirstMeasure = ForwardStart + 0.05 + 2 * 0.92 + 0.8;
        for (var j = 0; j < 3; j++)
        {
            _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.Backward, BackwardStart + 0.05 + j * 0.87, 0.8, 2 - j, 1));
        }
        _glideStart[0] = BackwardStart + 0.05 + 2 * 0.87;
        _glideLength[0] = 0.8;

        // The other steps stream through the epochs stage, overlapping.
        var cycles = n - 1;
        for (var c = 0; c < cycles; c++)
        {
            var s = EpochsStart + c * CyclePeriod;
            var update = c + 2;
            _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.TrainingFlight, s, 0.45, 0, update));
            for (var h = 0; h < 3; h++)
            {
                _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.Forward, s + 0.4 + h * 0.28, 0.3, h, update));
            }
            for (var j = 0; j < 3; j++)
            {
                _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.Backward, s + 1.05 + j * 0.22, 0.3, 2 - j, update));
            }
            _glideStart[update - 1] = s + 1.05;
            _glideLength[update - 1] = 0.7;
        }
        EpochsLength = (cycles - 1) * CyclePeriod + CycleLength;
        EvaluateStart = EpochsStart + EpochsLength;

        // The held-out data runs through once, slowly.
        var e = EvaluateStart;
        _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.HeldOutFlight, e + 0.05, 0.8, 0, 0));
        for (var h = 0; h < 3; h++)
        {
            _passes.Add(new ModelTrainingPass(ModelTrainingPassKind.Forward, e + 1.0 + h * 0.5, 0.5, h, 0));
        }
        Length = e + EvaluateLength;
    }

    /// <summary>When the weight update of step <paramref name="update"/> (from 1) ends.</summary>
    public double UpdateEnd(int update) => _glideStart[update - 1] + _glideLength[update - 1];

    /// <summary>When the forward pass of step <paramref name="update"/> reaches the outputs.</summary>
    public double MeasureTime(int update)
    {
        if (update <= 1)
        {
            return FirstMeasure;
        }
        var s = EpochsStart + (update - 2) * CyclePeriod;
        return s + 0.4 + 2 * 0.28 + 0.3;
    }

    /// <summary>The time readouts and the chart show at <paramref name="t"/>: the finished run until the swap.</summary>
    public double ReadoutTime(double t) => t < SwapAt ? Length : t;

    // ---- Loss ---------------------------------------------------------------------------------------------------

    /// <summary>The loss at a share <paramref name="x"/> (0..1) of the training.</summary>
    public double LossAt(double x)
    {
        x = Math.Clamp(x, 0, 1);
        if (_values.Length >= 2)
        {
            var f = x * (_values.Length - 1);
            var i = Math.Min((int)f, _values.Length - 2);
            var u = f - i;
            // Smoothstep between the given values keeps the line round without overshooting them.
            u = u * u * (3 - 2 * u);
            return _values[i] + (_values[i + 1] - _values[i]) * u;
        }
        var fall = (Math.Exp(-CurveRate * x) - Math.Exp(-CurveRate)) / (1 - Math.Exp(-CurveRate));
        var noise = (Math.Sin(x * 31.0 + 0.7) * 0.6 + Math.Sin(x * 53.0 + 2.1) * 0.4) * 0.018 * Math.Sin(Math.PI * x);
        return LossTo + (LossFrom - LossTo) * (fall + noise);
    }

    /// <summary>The highest loss the chart's scale has to fit.</summary>
    public double ChartMax { get; private set; } = 0.75;

    /// <summary>The share of training the loss head has reached at <paramref name="t"/> (0 before the first update).</summary>
    public double HeadAt(double t)
    {
        t = ReadoutTime(t);
        var n = Steps;
        for (var k = n; k >= 1; k--)
        {
            var start = _glideStart[k - 1];
            if (t >= start)
            {
                var q = Math.Clamp((t - start) / _glideLength[k - 1], 0, 1);
                return (k - 1 + ModelTrainingEase.InOutCubic(q)) / n;
            }
        }
        return 0;
    }

    /// <summary>True once the first loss has been measured (the readouts stop being dim).</summary>
    public bool IsMeasured(double t) => ReadoutTime(t) >= FirstMeasure;

    /// <summary>The epoch the readout shows at <paramref name="t"/> (0 before the first measurement).</summary>
    public int EpochAt(double t)
    {
        if (!IsMeasured(t))
        {
            return 0;
        }
        var x = HeadAt(t);
        return Math.Clamp((int)Math.Floor(1 + (Epochs - 1) * x + 1e-9), 1, Epochs);
    }

    /// <summary>The probability the model gives its leading class at <paramref name="t"/>.</summary>
    public double WinnerProbability(double t)
    {
        var rt = ReadoutTime(t);
        if (rt < FirstMeasure)
        {
            return 0.5;
        }
        var x = HeadAt(t);
        var span = LossFrom - LossTo;
        var learned = span <= 0 ? x : Math.Clamp((LossFrom - LossAt(x)) / span, 0, 1);
        var p = 0.52 + 0.42 * learned;
        var passed = EvaluateStart + 2.6;
        if (rt >= passed)
        {
            p += 0.03 * ModelTrainingEase.OutCubic(Math.Clamp((rt - passed) / 0.3, 0, 1));
        }
        return Math.Clamp(p, 0.5, 0.99);
    }

    // ---- Evaluation -----------------------------------------------------------------------------------------------

    /// <summary>0 waiting, 1 running, 2 finished (passed or below target).</summary>
    public int EvalStateAt(double t)
    {
        var rt = ReadoutTime(t);
        if (rt >= EvaluateStart + 2.6)
        {
            return 2;
        }
        return rt >= EvaluateStart + 0.1 ? 1 : 0;
    }

    /// <summary>The evaluation score shown at <paramref name="t"/> (counts up during the held-out pass).</summary>
    public double ScoreAt(double t)
    {
        var rt = ReadoutTime(t);
        var q = Math.Clamp((rt - (EvaluateStart + 1.0)) / 1.45, 0, 1);
        return Accuracy * ModelTrainingEase.OutCubic(q);
    }

    /// <summary>The check mark's drawn share once the evaluation has finished.</summary>
    public double CheckAt(double t) => Math.Clamp((ReadoutTime(t) - (EvaluateStart + 2.6)) / 0.35, 0, 1);

    /// <summary>The validation point's opacity.</summary>
    public double ValidationAlpha(double t) => Math.Clamp((ReadoutTime(t) - (EvaluateStart + 2.65)) / 0.3, 0, 1);

    // ---- Weights ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// How many updates have reached the connections of layer pair <paramref name="gap"/> at <paramref name="t"/>
    /// (fractional while the error crosses them); during the relax it falls from the finished run back to 0.
    /// </summary>
    public double UpdatesAt(int gap, double t)
    {
        if (t < ResetLength)
        {
            return Steps * (1 - ModelTrainingEase.InOutCubic(t / ResetLength));
        }
        var u = 0.0;
        foreach (var pass in _passes)
        {
            if (pass.Kind != ModelTrainingPassKind.Backward || pass.Gap != gap)
            {
                continue;
            }
            if (t < pass.Start)
            {
                break;
            }
            u += pass.Progress(t);
        }
        return u;
    }

    /// <summary>The learned share (0..1) of the weights after <paramref name="updates"/> updates.</summary>
    public double LearnedShare(double updates)
    {
        var keep = 1 - Rate;
        var whole = 1 - Math.Pow(keep, Steps);
        return whole <= 0 ? 0 : Math.Clamp((1 - Math.Pow(keep, Math.Max(0, updates))) / whole, 0, 1);
    }

    private void BuildCurve(double lossFrom, double lossTo, IReadOnlyList<double>? losses)
    {
        var given = new List<double>();
        if (losses is not null)
        {
            foreach (var value in losses)
            {
                if (double.IsFinite(value))
                {
                    given.Add(Math.Clamp(value, 0.001, 10));
                }
            }
        }
        if (given.Count >= 2)
        {
            _values = given.ToArray();
            LossFrom = _values[0];
            LossTo = _values[^1];
        }
        else
        {
            _values = [];
            LossFrom = double.IsFinite(lossFrom) ? Math.Clamp(lossFrom, 0.001, 10) : 0.69;
            LossTo = double.IsFinite(lossTo) ? Math.Clamp(lossTo, 0.0005, LossFrom) : 0.08;
        }
        var max = 0.0;
        for (var i = 0; i <= 64; i++)
        {
            max = Math.Max(max, LossAt(i / 64.0));
        }
        ChartMax = Math.Max(max, LossFrom) * 1.08;
    }
}

/// <summary>The easing curves of the model training run.</summary>
internal static class ModelTrainingEase
{
    public static double InOutCubic(double q)
    {
        q = Math.Clamp(q, 0, 1);
        return q < 0.5 ? 4 * q * q * q : 1 - Math.Pow(-2 * q + 2, 3) / 2;
    }

    public static double OutCubic(double q)
    {
        q = Math.Clamp(q, 0, 1);
        return 1 - Math.Pow(1 - q, 3);
    }

    public static double InOutSine(double q) => -(Math.Cos(Math.PI * Math.Clamp(q, 0, 1)) - 1) / 2;
}
