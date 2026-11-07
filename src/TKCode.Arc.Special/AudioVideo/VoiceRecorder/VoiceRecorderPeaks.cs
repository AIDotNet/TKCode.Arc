namespace TKCode.Arc;

/// <summary>
/// Deterministic waveforms for voice messages. <see cref="Levels"/> simulates the 90ms input levels of someone talking
/// (phrases of syllables between pauses, now and then a murmur), <see cref="Generate"/> turns a seed and a length into a
/// message's bars, and <see cref="Resample"/> shrinks any level series to a bar count by taking the loudest sample of each
/// stretch. The same seed always gives the same voice. Values run from 0.12 (silence) to 0.98 in steps of 0.01.
/// </summary>
public static class VoiceRecorderPeaks
{
    /// <summary>The length of one level sample: one bar of the live waveform.</summary>
    public static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(90);

    /// <summary>The level that reads as silence (the shortest bar).</summary>
    public const double Silence = 0.12;

    /// <summary>The loudest level.</summary>
    public const double Loudest = 0.98;

    /// <summary>The bars the review waveform shows.</summary>
    public const int ReviewBarCount = 40;

    private const double SampleSeconds = 0.09;

    /// <summary>
    /// The number of bars of a message <paramref name="seconds"/> long: 14 + 1.3 per second, rounded, kept between 18 and 36
    /// (a 6 second message has 22, a 14 second one 32, anything from 17 seconds on 36).
    /// </summary>
    public static int BarCount(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0)
        {
            seconds = 0;
        }
        var raw = Math.Floor(14 + 1.3 * Math.Min(seconds, 1000) + 0.5 + 1e-9);
        return (int)Math.Clamp(raw, 18, 36);
    }

    /// <summary>The number of 90ms samples a take of <paramref name="seconds"/> holds (3.5 seconds hold 39).</summary>
    public static int SampleCount(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0)
        {
            return 0;
        }
        return (int)Math.Ceiling(seconds / SampleSeconds - 1e-9);
    }

    /// <summary>The bars of a message: <see cref="BarCount"/> bars resampled from <see cref="Levels"/> of the same seed.</summary>
    /// <param name="seed">Any number; the same seed gives the same waveform.</param>
    /// <param name="seconds">The message's length in seconds.</param>
    public static double[] Generate(int seed, double seconds) =>
        Resample(Levels(seed, seconds), BarCount(seconds));

    /// <summary>
    /// The simulated input levels of a take: one value per 90ms sample, <see cref="SampleCount"/> of them. Speech starts
    /// after about a second of silence and comes in phrases of 2 to 4 seconds (syllables every 270 to 450ms, the phrase
    /// swelling and fading), separated by pauses of 1.5 to 4.5 seconds, with an occasional quiet murmur.
    /// </summary>
    /// <param name="seed">Any number; the same seed gives the same voice.</param>
    /// <param name="seconds">How long the take runs.</param>
    public static double[] Levels(int seed, double seconds)
    {
        var count = SampleCount(seconds);
        var levels = new double[count];
        var random = new Prng(seed);
        var i = 0;
        // About a second of silence before the first word.
        i = Quiet(levels, i, 11 + random.Next(3), random);
        while (i < count)
        {
            i = Phrase(levels, i, random);
            if (random.NextDouble() < 0.35)
            {
                i = Quiet(levels, i, 6 + random.Next(8), random);
                i = Murmur(levels, i, random);
            }
            i = Quiet(levels, i, 17 + random.Next(34), random);
        }
        return levels;
    }

    /// <summary>
    /// Shrinks or stretches <paramref name="samples"/> to <paramref name="count"/> bars: bar i takes the loudest sample in
    /// [⌊i·n/count⌋, ⌊(i+1)·n/count⌋); with fewer samples than bars each bar repeats the sample under it. No samples give
    /// silent bars.
    /// </summary>
    public static double[] Resample(IReadOnlyList<double> samples, int count)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (count <= 0)
        {
            return [];
        }
        var bars = new double[count];
        var n = samples.Count;
        if (n == 0)
        {
            Array.Fill(bars, Silence);
            return bars;
        }
        for (var b = 0; b < count; b++)
        {
            var start = (int)((long)b * n / count);
            var end = (int)((long)(b + 1) * n / count);
            if (end <= start)
            {
                bars[b] = samples[Math.Min(start, n - 1)];
                continue;
            }
            var max = 0.0;
            for (var s = start; s < end; s++)
            {
                max = Math.Max(max, samples[s]);
            }
            bars[b] = max;
        }
        return bars;
    }

    private static int Quiet(double[] levels, int i, int length, Prng random)
    {
        for (var k = 0; k < length && i < levels.Length; k++, i++)
        {
            // Now and then the room hums a little.
            levels[i] = random.NextDouble() < 0.05 ? Quantize(0.13 + 0.06 * random.NextDouble()) : Silence;
        }
        return i;
    }

    private static int Phrase(double[] levels, int i, Prng random)
    {
        var length = 25 + random.Next(21);
        var amplitude = 0.6 + 0.4 * random.NextDouble();
        // Some phrases open loud, others swell to the middle.
        var skew = random.NextDouble() < 0.5 ? 0.6 : 1.0;
        var pos = 0;
        while (pos < length && i < levels.Length)
        {
            var rise = 1 + random.Next(2);
            var fall = 2 + random.Next(2);
            var centre = (pos + rise) / (double)length;
            var envelope = Math.Pow(Math.Sin(Math.PI * Math.Clamp(centre, 0.05, 0.95)), skew);
            var peak = Silence + (Loudest - 0.04 - Silence) * envelope * amplitude * (0.5 + 0.5 * random.NextDouble());
            var valley = 0.17 + 0.08 * random.NextDouble();
            peak = Math.Max(peak, valley + 0.05);
            for (var k = 1; k <= rise && i < levels.Length; k++, i++, pos++)
            {
                levels[i] = Quantize(valley + (peak - valley) * k / rise);
            }
            for (var k = 1; k <= fall && i < levels.Length; k++, i++, pos++)
            {
                levels[i] = Quantize(peak + (valley - peak) * k / fall);
            }
        }
        // The phrase trails off over two to four samples.
        var tail = 2 + random.Next(3);
        var from = i > 0 ? levels[i - 1] : Silence;
        for (var k = 1; k <= tail && i < levels.Length; k++, i++)
        {
            levels[i] = Quantize(from + (Silence - from) * k / tail);
        }
        return i;
    }

    private static int Murmur(double[] levels, int i, Prng random)
    {
        var length = 11 + random.Next(12);
        var amplitude = 0.05 + 0.21 * random.NextDouble();
        for (var k = 0; k < length && i < levels.Length; k++, i++)
        {
            var envelope = Math.Sin(Math.PI * (k + 0.5) / length);
            var wobble = 0.55 + 0.45 * Math.Abs(Math.Sin(k * 1.9 + amplitude * 10));
            levels[i] = Quantize(Silence + amplitude * envelope * wobble);
        }
        return i;
    }

    private static double Quantize(double value) => Math.Round(Math.Clamp(value, Silence, Loudest), 2, MidpointRounding.AwayFromZero);

    /// <summary>A small, fixed pseudo-random generator (mulberry32), so waveforms never change between runtimes.</summary>
    private sealed class Prng(int seed)
    {
        private uint _state = unchecked((uint)seed * 2654435761u + 0x6D2B79F5u);

        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return ((t ^ (t >> 14)) >> 0) / 4294967296.0;
            }
        }

        public int Next(int exclusiveMax) => Math.Min(exclusiveMax - 1, (int)(NextDouble() * exclusiveMax));
    }
}

/// <summary>The recorder's time formats: m:ss and m:ss.d, both rounded down.</summary>
internal static class VoiceRecorderClock
{
    /// <summary>"0:14", "1:00": whole seconds, rounded down (3.5 seconds read "0:03").</summary>
    public static string Format(double seconds)
    {
        var whole = (long)Math.Floor(Math.Max(0, seconds) + 1e-9);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{whole / 60}:{whole % 60:00}");
    }

    /// <summary>"0:02.6": tenths, rounded down (the running timer).</summary>
    public static string FormatTenths(double seconds)
    {
        var tenths = (long)Math.Floor(Math.Max(0, seconds) * 10 + 1e-6);
        var whole = tenths / 10;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{whole / 60}:{whole % 60:00}.{tenths % 10}");
    }
}
