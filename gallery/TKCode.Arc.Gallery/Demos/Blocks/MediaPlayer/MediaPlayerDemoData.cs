using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The Media player demos' playlist (all invented): three 24-second loops by a made-up act, Low Static, synthesized here as
/// WAV files (no recordings ship with the gallery), each with a cover drawn from gradients and simple shapes. The audio is
/// generated once per run and shared by every demo.
/// </summary>
public static class MediaPlayerDemoData
{
    private const int SampleRate = 22050;
    private const double LoopSeconds = 24;

    private static readonly Lazy<byte[]> s_harbor = new(() => Synthesize(HarborLights));
    private static readonly Lazy<byte[]> s_kites = new(() => Synthesize(PaperKites));
    private static readonly Lazy<byte[]> s_orbit = new(() => Synthesize(SlowOrbit));

    /// <summary>A fresh copy of the playlist (tracks belong to one player at a time).</summary>
    public static MediaTrack[] Tracks() =>
    [
        new()
        {
            Title = "Harbor lights",
            Artist = "Low Static",
            Album = "Night ferry",
            Duration = TimeSpan.FromSeconds(LoopSeconds),
            Cover = HarborCover(),
            CoverDescription = "Lamps over dark water under a dusk sky",
            Source = AudioSource.FromBytes(s_harbor.Value),
        },
        new()
        {
            Title = "Paper kites",
            Artist = "Low Static",
            Album = "Night ferry",
            Duration = TimeSpan.FromSeconds(LoopSeconds),
            Cover = KitesCover(),
            CoverDescription = "Three paper kites against a pale morning sky",
            Source = AudioSource.FromBytes(s_kites.Value),
        },
        new()
        {
            Title = "Slow orbit",
            Artist = "Low Static",
            Album = "Night ferry",
            Duration = TimeSpan.FromSeconds(LoopSeconds),
            Cover = OrbitCover(),
            CoverDescription = "A ringed planet and a small moon in deep blue space",
            Source = AudioSource.FromBytes(s_orbit.Value),
        },
    ];

    #region Audio

    private delegate void Score(float[] samples);

    private static byte[] Synthesize(Score score)
    {
        var samples = new float[(int)(SampleRate * LoopSeconds)];
        score(samples);
        var peak = 0f;
        foreach (var s in samples)
        {
            peak = Math.Max(peak, Math.Abs(s));
        }
        if (peak > 0)
        {
            var gain = 0.6f / peak;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }
        return WavFile.Encode(samples, SampleRate);
    }

    private static double Note(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>A plucked note: a sine with a soft second and third harmonic, a 5 ms attack and an exponential decay.</summary>
    private static void Pluck(float[] samples, double start, double frequency, double length, double level, double decay = 3)
    {
        var first = (int)(start * SampleRate);
        var count = (int)(length * SampleRate);
        for (var i = 0; i < count && first + i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = Math.Min(1, t / 0.005) * Math.Exp(-decay * t);
            var phase = 2 * Math.PI * frequency * t;
            var tone = Math.Sin(phase) + 0.3 * Math.Sin(2 * phase) + 0.12 * Math.Sin(3 * phase);
            samples[first + i] += (float)(level * envelope * tone);
        }
    }

    /// <summary>A pad: slow attack and release, a gentle vibrato.</summary>
    private static void Pad(float[] samples, double start, double frequency, double length, double level)
    {
        var first = (int)(start * SampleRate);
        var count = (int)(length * SampleRate);
        for (var i = 0; i < count && first + i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = Math.Min(1, t / 1.5) * Math.Min(1, (length - t) / 1.5);
            var wobble = 1 + 0.003 * Math.Sin(2 * Math.PI * 0.4 * t);
            var phase = 2 * Math.PI * frequency * wobble * t;
            samples[first + i] += (float)(level * envelope * (Math.Sin(phase) + 0.2 * Math.Sin(2 * phase)));
        }
    }

    /// <summary>A minor arpeggio over a low drone, eighth notes at 90 BPM.</summary>
    private static void HarborLights(float[] samples)
    {
        int[][] chords = [[57, 60, 64, 69], [53, 57, 60, 65], [48, 52, 55, 60], [55, 59, 62, 67]];
        var step = 60.0 / 90 / 2;
        var bar = step * 8;
        for (var b = 0; b * bar < LoopSeconds; b++)
        {
            var chord = chords[b % chords.Length];
            Pad(samples, b * bar, Note(chord[0] - 12), bar, 0.18);
            for (var n = 0; n < 8; n++)
            {
                var pitch = chord[n < 4 ? n : 7 - n];
                Pluck(samples, b * bar + n * step, Note(pitch), 1.2, 0.32);
            }
        }
    }

    /// <summary>Major chords struck on the beat at 104 BPM (I–V–vi–IV), with a bass note on each bar.</summary>
    private static void PaperKites(float[] samples)
    {
        int[][] chords = [[60, 64, 67], [55, 59, 62], [57, 60, 64], [53, 57, 60]];
        var beat = 60.0 / 104;
        var bar = beat * 4;
        for (var b = 0; b * bar < LoopSeconds; b++)
        {
            var chord = chords[b % chords.Length];
            Pluck(samples, b * bar, Note(chord[0] - 24), bar, 0.4, 1.2);
            for (var n = 0; n < 4; n++)
            {
                var level = n == 0 ? 0.26 : 0.18;
                for (var k = 0; k < chord.Length; k++)
                {
                    // A slight strum: each string a few milliseconds after the one below.
                    Pluck(samples, b * bar + n * beat + k * 0.012, Note(chord[k] + 12), 0.9, level, 4);
                }
            }
        }
    }

    /// <summary>A held D minor pad with a slow bell figure every two seconds.</summary>
    private static void SlowOrbit(float[] samples)
    {
        foreach (var pitch in new[] { 38, 50, 53, 57 })
        {
            Pad(samples, 0, Note(pitch), LoopSeconds, 0.12);
        }
        int[] bells = [74, 77, 81, 79, 77, 72];
        for (var i = 0; i * 2.0 < LoopSeconds; i++)
        {
            Pluck(samples, i * 2.0 + 0.5, Note(bells[i % bells.Length]), 2.2, 0.22, 1.6);
        }
    }

    #endregion

    #region Covers

    private static readonly Rect s_canvas = new(0, 0, 440, 200);

    private static IImage Image(params Drawing[] layers)
    {
        var group = new DrawingGroup();
        foreach (var layer in layers)
        {
            group.Children.Add(layer);
        }
        // Clip to the canvas so shapes that run off the edge do not change the picture's bounds.
        group.ClipGeometry = new RectangleGeometry(s_canvas);
        return new DrawingImage(group);
    }

    private static GeometryDrawing Fill(Geometry geometry, IBrush brush) => new() { Geometry = geometry, Brush = brush };

    private static LinearGradientBrush Vertical(params (string Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative) };
        foreach (var (color, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(color), offset));
        }
        return brush;
    }

    private static IImage HarborCover()
    {
        var sky = Fill(new RectangleGeometry(s_canvas), Vertical(("#2B3350", 0), ("#7A5A6E", 0.55), ("#D99A7A", 0.78)));
        var water = Fill(new RectangleGeometry(new Rect(0, 128, 440, 72)), Vertical(("#1E2638", 0), ("#121725", 1)));
        var hills = Fill(StreamGeometry.Parse("M0 132 C60 110 110 118 160 124 C220 104 280 112 330 126 C370 116 410 118 440 124 L440 134 L0 134 Z"),
            new SolidColorBrush(Color.Parse("#2A2F45")));
        var lamps = new GeometryGroup();
        foreach (var x in new[] { 300.0, 318, 336, 356, 372 })
        {
            lamps.Children.Add(new EllipseGeometry(new Rect(x, 120, 3, 3)));
        }
        var glow = new GeometryGroup();
        foreach (var x in new[] { 300.5, 318.5, 336.5, 356.5, 372.5 })
        {
            glow.Children.Add(new RectangleGeometry(new Rect(x, 138, 2, 26)));
        }
        return Image(sky, hills, water, Fill(glow, new SolidColorBrush(Color.Parse("#F2C27A"), 0.35)),
            Fill(lamps, new SolidColorBrush(Color.Parse("#FFD79A"))));
    }

    private static IImage KitesCover()
    {
        var sky = Fill(new RectangleGeometry(s_canvas), Vertical(("#BFD8EC", 0), ("#EAF1F4", 0.7), ("#F6EFE3", 1)));
        var sun = Fill(new EllipseGeometry(new Rect(330, 26, 54, 54)), new SolidColorBrush(Color.Parse("#FFF4D6")));
        var field = Fill(StreamGeometry.Parse("M0 170 C120 150 260 160 440 148 L440 200 L0 200 Z"), new SolidColorBrush(Color.Parse("#A7C49A")));
        Drawing Kite(double x, double y, string color) =>
            Fill(StreamGeometry.Parse(FormattableString.Invariant($"M{x} {y} l18 22 l-18 30 l-18 -30 Z")), new SolidColorBrush(Color.Parse(color)));
        var tails = new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M90 102 C96 128 84 146 98 170 M182 80 C176 110 192 128 180 160 M262 112 C270 136 258 150 266 172"),
            Pen = new Pen(new SolidColorBrush(Color.Parse("#6D7C8A")), 1.2),
        };
        return Image(sky, sun, field, tails, Kite(90, 50, "#E0654F"), Kite(182, 28, "#F2B33D"), Kite(262, 60, "#3F7FBF"));
    }

    private static IImage OrbitCover()
    {
        var space = Fill(new RectangleGeometry(s_canvas), Vertical(("#0D1230", 0), ("#1B2457", 1)));
        var stars = new GeometryGroup();
        var random = new Random(7);
        for (var i = 0; i < 40; i++)
        {
            var size = random.NextDouble() < 0.2 ? 2.0 : 1.2;
            stars.Children.Add(new EllipseGeometry(new Rect(random.NextDouble() * 440, random.NextDouble() * 200, size, size)));
        }
        var planet = Fill(new EllipseGeometry(new Rect(170, 50, 100, 100)), Vertical(("#F1A96B", 0), ("#B4507A", 1)));
        var ring = new GeometryDrawing
        {
            Geometry = new EllipseGeometry(new Rect(140, 88, 160, 26)),
            Pen = new Pen(new SolidColorBrush(Color.Parse("#F5D9B8"), 0.8), 3),
        };
        var moon = Fill(new EllipseGeometry(new Rect(318, 46, 18, 18)), new SolidColorBrush(Color.Parse("#C9D2F0")));
        return Image(space, Fill(stars, new SolidColorBrush(Colors.White, 0.7)), planet, ring, moon);
    }

    #endregion
}
