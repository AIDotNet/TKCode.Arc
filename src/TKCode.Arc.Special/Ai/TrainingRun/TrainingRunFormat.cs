using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The number and time formats of <see cref="TrainingRun"/>: losses with three decimals, the learning rate in scientific
/// notation ("1.84e-4"), times as "42s", "12m 4s" and "1h 4m", and the smoothing of the train curve. Pure functions,
/// invariant text.
/// </summary>
internal static class TrainingRunFormat
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    /// <summary>The weight of the train curve's exponential smoothing (debiased, as TensorBoard does).</summary>
    public const double SmoothingWeight = 0.8;

    /// <summary>"0.812".</summary>
    public static string Loss(double value) => value.ToString("0.000", s_invariant);

    /// <summary>"2,400".</summary>
    public static string Count(int value) => value.ToString("N0", s_invariant);

    /// <summary>
    /// Splits a learning rate into a mantissa with two decimals and a power of ten: 0.000184 is (1.84, -4). Zero and
    /// values that are not finite give (0, 0).
    /// </summary>
    public static (double Mantissa, int Exponent) Scientific(double value)
    {
        if (!double.IsFinite(value) || value == 0)
        {
            return (0, 0);
        }
        var exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var mantissa = Math.Round(value / Math.Pow(10, exponent), 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(mantissa) >= 10)
        {
            mantissa /= 10;
            exponent++;
        }
        return (mantissa, exponent);
    }

    /// <summary>"1.84e-4".</summary>
    public static string LearningRate(double value)
    {
        var (mantissa, exponent) = Scientific(value);
        return mantissa.ToString("0.00", s_invariant) + Exponent(exponent);
    }

    /// <summary>"e-4".</summary>
    public static string Exponent(int exponent) => "e" + exponent.ToString(s_invariant);

    /// <summary>
    /// The two parts of a time: (value, unit) pairs, the larger first. Under a minute only the seconds ("42s"), under an
    /// hour minutes and seconds ("12m 4s"), then hours and minutes ("1h 4m"). Negative times read as zero.
    /// </summary>
    public static (long Big, TrainingRunTimeUnit BigUnit, long Small, TrainingRunTimeUnit SmallUnit) Parts(TimeSpan time)
    {
        var total = Math.Max(0, (long)Math.Floor(time.TotalSeconds));
        if (total >= 3600)
        {
            return (total / 3600, TrainingRunTimeUnit.Hours, total / 60 % 60, TrainingRunTimeUnit.Minutes);
        }
        if (total >= 60)
        {
            return (total / 60, TrainingRunTimeUnit.Minutes, total % 60, TrainingRunTimeUnit.Seconds);
        }
        return (0, TrainingRunTimeUnit.None, total, TrainingRunTimeUnit.Seconds);
    }

    /// <summary>The time as text with the labels' units: "42s", "12m 4s", "1h 4m".</summary>
    public static string Time(TimeSpan time, TrainingRunLabels labels)
    {
        var (big, bigUnit, small, smallUnit) = Parts(time);
        var tail = small.ToString(s_invariant) + Unit(smallUnit, labels);
        return bigUnit == TrainingRunTimeUnit.None ? tail : big.ToString(s_invariant) + Unit(bigUnit, labels) + " " + tail;
    }

    /// <summary>The label of a unit.</summary>
    public static string Unit(TrainingRunTimeUnit unit, TrainingRunLabels labels) => unit switch
    {
        TrainingRunTimeUnit.Hours => labels.HoursUnit,
        TrainingRunTimeUnit.Minutes => labels.MinutesUnit,
        TrainingRunTimeUnit.Seconds => labels.SecondsUnit,
        _ => string.Empty,
    };

    /// <summary>
    /// The smoothed train curve: an exponential moving average with <paramref name="weight"/>, debiased so the first
    /// values are not pulled toward zero. Writes one value per input into <paramref name="smoothed"/>.
    /// </summary>
    public static void Smooth(IReadOnlyList<double> raw, List<double> smoothed, double weight = SmoothingWeight)
    {
        smoothed.Clear();
        var last = 0.0;
        var debias = 1.0;
        for (var i = 0; i < raw.Count; i++)
        {
            last = last * weight + (1 - weight) * raw[i];
            debias *= weight;
            smoothed.Add(last / (1 - debias));
        }
    }

    /// <summary>
    /// The top of the fixed loss axis from the first samples: the largest of them with 8% headroom, rounded up to a step of
    /// 0.1 below 1, 0.2 below 5, otherwise 1. Never below 0.1.
    /// </summary>
    public static double AxisTop(double largest)
    {
        if (!double.IsFinite(largest) || largest <= 0)
        {
            return 1;
        }
        var top = largest * 1.08;
        var step = top < 1 ? 0.1 : top < 5 ? 0.2 : 1;
        return Math.Max(0.1, Math.Ceiling(top / step - 1e-9) * step);
    }

    /// <summary>An axis label: "2.4", "1.2", "0".</summary>
    public static string AxisLabel(double value) => value.ToString(value >= 10 ? "0" : "0.#", s_invariant);

    /// <summary>The 1-based epoch a step falls in, clamped to the run.</summary>
    public static int EpochOf(int step, int totalSteps, int epochs)
    {
        if (epochs <= 1 || totalSteps <= 0)
        {
            return 1;
        }
        var perEpoch = (double)totalSteps / epochs;
        return Math.Clamp((int)Math.Floor(step / perEpoch) + 1, 1, epochs);
    }
}

/// <summary>A unit of a <see cref="TrainingRunFormat.Parts"/> time.</summary>
internal enum TrainingRunTimeUnit
{
    None,
    Hours,
    Minutes,
    Seconds
}
