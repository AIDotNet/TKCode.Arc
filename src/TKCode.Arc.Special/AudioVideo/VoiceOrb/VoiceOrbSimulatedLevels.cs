namespace TKCode.Arc;

/// <summary>
/// The levels a <see cref="VoiceOrb"/> shows when the host pushes none: a deterministic, speech-like envelope (a pulse
/// per syllable every 150 to 350 ms with a breath between phrases) for the input, and a steadier one for the output. Both
/// are pure functions of time, so a seed always gives the same voice. No microphone is opened.
/// </summary>
public sealed class VoiceOrbSimulatedLevels : IVoiceOrbLevelSource
{
    // A syllable rises in 40 ms and decays exponentially; the input decays faster than the output.
    private const double Attack = 0.04;
    private const double InputSlot = 0.24;
    private const double InputDecay = 0.13;
    private const double OutputSlot = 0.27;
    private const double OutputDecay = 0.22;

    /// <summary>Creates a source with the given <paramref name="seed"/>.</summary>
    public VoiceOrbSimulatedLevels(int seed = 7)
    {
        Seed = seed;
    }

    /// <summary>The seed: different seeds give different, equally plausible voices.</summary>
    public int Seed { get; }

    /// <summary>The shared default source.</summary>
    public static VoiceOrbSimulatedLevels Default { get; } = new();

    /// <inheritdoc />
    public double ReadInput(double seconds) => Envelope(seconds, (uint)Seed, InputSlot, InputDecay, 9);

    /// <inheritdoc />
    public double ReadOutput(double seconds) => Envelope(seconds, (uint)Seed * 2654435761u + 17u, OutputSlot, OutputDecay, 12);

    private static double Envelope(double t, uint seed, double slot, double decay, int phrase)
    {
        if (t <= 0 || double.IsNaN(t) || double.IsInfinity(t))
        {
            return 0;
        }
        var index = (long)Math.Floor(t / slot);
        var sum = 0.0;
        for (var i = index - 4; i <= index; i++)
        {
            if (i < 0)
            {
                continue;
            }
            // The last two slots of every phrase are a breath.
            var within = (int)(i % phrase);
            if (within >= phrase - 2)
            {
                continue;
            }
            var jitter = Hash(i, seed);
            var amplitude = 0.3 + 0.6 * Hash(i, seed ^ 0x9E3779B9u);
            // Some syllables are skipped, so the rhythm is not a metronome.
            if (Hash(i, seed ^ 0x85EBCA6Bu) < 0.18)
            {
                continue;
            }
            var onset = i * slot + jitter * 0.4 * slot;
            sum += amplitude * Pulse(t - onset, decay);
        }
        return Math.Clamp(sum, 0, 1);
    }

    private static double Pulse(double x, double decay)
    {
        if (x <= 0)
        {
            return 0;
        }
        if (x < Attack)
        {
            return x / Attack;
        }
        return Math.Exp(-(x - Attack) / decay);
    }

    private static double Hash(long i, uint seed)
    {
        unchecked
        {
            var h = (uint)i * 0x27D4EB2Du ^ seed;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
