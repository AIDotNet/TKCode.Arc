using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>
/// The voice recorder's pure logic against the numbers measured from Arc Voice recorder (notes 2b, 6.3): bar counts,
/// sample counts, resampling, formats, the level generator's shape, the live waveform's geometry and the springs.
/// </summary>
public class VoiceRecorderLogicTests
{
    [Theory]
    [InlineData(1, 18)]
    [InlineData(2.5, 18)]
    [InlineData(3, 18)]
    [InlineData(3.5, 19)]
    [InlineData(4, 19)]
    [InlineData(4.5, 20)]
    [InlineData(5, 21)]
    [InlineData(6, 22)]
    [InlineData(7, 23)]
    [InlineData(8, 24)]
    [InlineData(9, 26)]
    [InlineData(10, 27)]
    [InlineData(12, 30)]
    [InlineData(14, 32)]
    [InlineData(16, 35)]
    [InlineData(20, 36)]
    [InlineData(60, 36)]
    [InlineData(0, 18)]
    public void BarCountMatchesTheMeasuredLengths(double seconds, int bars)
    {
        Assert.Equal(bars, VoiceRecorderPeaks.BarCount(seconds));
    }

    [Theory]
    [InlineData(3.5, 39)]
    [InlineData(60, 667)]
    [InlineData(0, 0)]
    [InlineData(0.05, 1)]
    [InlineData(0.09, 1)]
    public void SampleCountIsOnePer90Milliseconds(double seconds, int samples)
    {
        Assert.Equal(samples, VoiceRecorderPeaks.SampleCount(seconds));
    }

    [Fact]
    public void ResampleTakesTheLoudestOfEachStretch()
    {
        // 39 samples into 19 bars: every bar is the larger of neighbouring samples (bucket starts at ⌊i·39/19⌋).
        var samples = Enumerable.Range(0, 39).Select(i => 0.12 + (i % 7) * 0.1).ToArray();
        var bars = VoiceRecorderPeaks.Resample(samples, 19);
        Assert.Equal(19, bars.Length);
        for (var b = 0; b < 19; b++)
        {
            var start = b * 39 / 19;
            var end = (b + 1) * 39 / 19;
            Assert.Equal(samples[start..end].Max(), bars[b], 6);
        }
    }

    [Fact]
    public void ShortTakesStretchTheirOwnSamples()
    {
        // Fewer samples than bars: each bar repeats the sample under it, never a sample that was not recorded.
        var samples = new[] { 0.2, 0.5, 0.9 };
        var bars = VoiceRecorderPeaks.Resample(samples, 40);
        Assert.Equal(40, bars.Length);
        Assert.All(bars, value => Assert.Contains(value, samples));
        Assert.Equal(0.2, bars[0]);
        Assert.Equal(0.9, bars[^1]);
        Assert.All(VoiceRecorderPeaks.Resample([], 5), value => Assert.Equal(VoiceRecorderPeaks.Silence, value));
    }

    [Fact]
    public void LevelsAreDeterministicAndShapedLikeSpeech()
    {
        var a = VoiceRecorderPeaks.Levels(4, 60);
        var b = VoiceRecorderPeaks.Levels(4, 60);
        var c = VoiceRecorderPeaks.Levels(5, 60);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(667, a.Length);
        Assert.All(a, value =>
        {
            Assert.InRange(value, VoiceRecorderPeaks.Silence, VoiceRecorderPeaks.Loudest);
            Assert.Equal(Math.Round(value, 2), value, 9);
        });
        // About a second of silence first.
        Assert.All(a[..11], value => Assert.Equal(VoiceRecorderPeaks.Silence, value));
        // Phrases and pauses: plenty of silence and plenty of loud syllables.
        var silent = a.Count(value => value <= 0.13);
        var loud = a.Count(value => value >= 0.4);
        Assert.InRange(silent / (double)a.Length, 0.3, 0.75);
        Assert.InRange(loud / (double)a.Length, 0.08, 0.5);
        // The first 2 seconds of a longer take are the same as a 2 second take (one voice per take number).
        Assert.Equal(VoiceRecorderPeaks.Levels(4, 2), a[..VoiceRecorderPeaks.SampleCount(2)]);
    }

    [Fact]
    public void GenerateGivesTheBarsOfAMessage()
    {
        var bars = VoiceRecorderPeaks.Generate(1, 14);
        Assert.Equal(32, bars.Length);
        Assert.Equal(bars, VoiceRecorderPeaks.Generate(1, 14));
        Assert.Equal(36, VoiceRecorderPeaks.Generate(2, 45).Length);
    }

    [Theory]
    [InlineData(14, "0:14")]
    [InlineData(3.5, "0:03")]
    [InlineData(60, "1:00")]
    [InlineData(0.99, "0:00")]
    [InlineData(125.7, "2:05")]
    public void ClockRoundsDown(double seconds, string text)
    {
        Assert.Equal(text, VoiceRecorderClock.Format(seconds));
    }

    [Theory]
    [InlineData(2.69, "0:02.6")]
    [InlineData(2.4, "0:02.4")]
    [InlineData(60, "1:00.0")]
    [InlineData(0, "0:00.0")]
    public void TimerShowsTenthsRoundedDown(double seconds, string text)
    {
        Assert.Equal(text, VoiceRecorderClock.FormatTenths(seconds));
    }

    [Fact]
    public void LiveBarsTravelAt55PixelsASecond()
    {
        // The newest bar's centre sits 1.5px from the right edge and every bar moves 5px per 90ms.
        Assert.Equal(179.5, VoiceRecorderLiveWave.CentreX(10, 0.9, 181, smooth: true, newest: 10), 6);
        Assert.Equal(179.5 - 55.5556, VoiceRecorderLiveWave.CentreX(10, 1.9, 181, smooth: true, newest: 21), 3);
        // Reduced motion steps one place per sample.
        Assert.Equal(179.5 - 15, VoiceRecorderLiveWave.CentreX(7, 0.95, 181, smooth: false, newest: 10), 6);
    }

    [Fact]
    public void LiveBarsGrowLinearlyAndFadeAtTheLeft()
    {
        Assert.Equal(3, VoiceRecorderLiveWave.GrownHeight(20, 0, smooth: true), 6);
        Assert.Equal(3, VoiceRecorderLiveWave.GrownHeight(20, 0.017, smooth: true), 6);
        Assert.Equal(11.5, VoiceRecorderLiveWave.GrownHeight(20, 0.017 + 0.055, smooth: true), 6);
        Assert.Equal(20, VoiceRecorderLiveWave.GrownHeight(20, 0.2, smooth: true), 6);
        Assert.Equal(20, VoiceRecorderLiveWave.GrownHeight(20, 0, smooth: false), 6);
        // 25% to full over the left quarter (45px of 181).
        Assert.Equal(0.25, VoiceRecorderLiveWave.Alpha(1, 181), 6);
        Assert.Equal(0.25, VoiceRecorderLiveWave.Alpha(11, 181), 6);
        Assert.Equal(26 / 45.25, VoiceRecorderLiveWave.Alpha(26, 181), 6);
        Assert.Equal(1, VoiceRecorderLiveWave.Alpha(46, 181), 6);
    }

    [Fact]
    public void ReviewBarsFlexBetweenOneAndAHalfAndThreePixels()
    {
        // 40 bars in 177.6px: about 2.49px each (Arc measured 2.49).
        var (count, bar, _) = VoiceRecorderBars.Fit(40, 177.6, flexible: true);
        Assert.Equal(40, count);
        Assert.Equal(2.49, bar, 2);
        // Wide: capped at 3px.
        Assert.Equal(3, VoiceRecorderBars.Fit(40, 400, flexible: true).Bar, 6);
        // Too narrow for 1.5px bars: fewer bars instead of clipping (Arc clipped at 320px).
        var narrow = VoiceRecorderBars.Fit(40, 35.6, flexible: true);
        Assert.Equal(10, narrow.Count);
        Assert.True(narrow.Extent <= 35.6 + 1e-9);
        // Bubble bars are 3px and narrow when squeezed.
        Assert.Equal(3, VoiceRecorderBars.Fit(22, 200, flexible: false).Bar, 6);
        Assert.Equal(2, VoiceRecorderBars.Fit(22, 22 * 2 + 21 * 2, flexible: false).Bar, 6);
    }

    [Fact]
    public void SpringsAreTheFittedOnes()
    {
        // ω 12.4 rad/s, ζ 1.00.
        Assert.Equal(153.76, VoiceRecorderMotion.Layout.Stiffness, 2);
        Assert.Equal(24.8, VoiceRecorderMotion.Layout.Damping, 2);
        // At 108ms the mic-to-bar width had covered 38.5% (notes 6.2): a critically damped spring from rest.
        var (x, _) = SpringSolver.Evaluate(VoiceRecorderMotion.Layout, -1, 0, 0.108);
        Assert.InRange(1 + x, 0.36, 0.41);
        var (x2, _) = SpringSolver.Evaluate(VoiceRecorderMotion.Layout, -1, 0, 0.509);
        Assert.InRange(1 + x2, 0.97, 0.995);
        Assert.Equal(404.01, VoiceRecorderMotion.PopIn.Stiffness, 2);
        Assert.Equal(156.25, VoiceRecorderMotion.MicReturn.Stiffness, 2);
    }

    [Fact]
    public void RecordDotBreathesEvery1point2Seconds()
    {
        Assert.Equal(1, VoiceRecorderMotion.DotOpacity(0), 6);
        Assert.Equal(0.35, VoiceRecorderMotion.DotOpacity(0.6), 6);
        Assert.Equal(1, VoiceRecorderMotion.DotOpacity(1.2), 6);
        Assert.InRange(VoiceRecorderMotion.DotOpacity(0.3), 0.6, 0.75);
    }

    [Fact]
    public void SamplesMatchThePreviewShape()
    {
        var thread = VoiceRecorderSamples.Thread();
        Assert.Equal(3, thread.Count);
        Assert.Equal([14.0, 6.0, 9.0], thread.Select(m => m.Duration.TotalSeconds));
        Assert.Equal([true, false, true], thread.Select(m => m.IsIncoming));
        Assert.All(thread, m => Assert.Equal(VoiceRecorderPeaks.BarCount(m.Duration.TotalSeconds), m.Peaks.Count));
        Assert.Equal(12, VoiceRecorderSamples.LongThread().Count);
        Assert.Equal("TN", VoiceRecorderAvatar.Initials(VoiceRecorderSamples.ContactName));
    }
}
