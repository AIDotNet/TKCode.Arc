using System.Diagnostics;
using Shouldly;

namespace TKCode.Arc.Core.Tests.Audio;

public class SimulatedAudioPlayerTests
{
    private static AudioSource Wav(double seconds) => AudioSource.FromBytes(WavFile.Encode(new float[(int)(8000 * seconds)], 8000));

    private static async Task<SimulatedAudioPlayer> Loaded(double seconds)
    {
        var player = new SimulatedAudioPlayer();
        await player.LoadAsync(Wav(seconds), TestContext.Current.CancellationToken);
        return player;
    }

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            clock.ElapsedMilliseconds.ShouldBeLessThan(timeoutMs);
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void New_Player_Is_Idle_And_Ignores_Controls()
    {
        using var player = new SimulatedAudioPlayer();
        player.State.ShouldBe(AudioPlaybackState.Idle);
        player.Play();
        player.Seek(TimeSpan.FromSeconds(1));
        player.State.ShouldBe(AudioPlaybackState.Idle);
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task Loading_Reads_The_Length_And_Pauses_At_Zero()
    {
        using var player = await Loaded(2);
        player.State.ShouldBe(AudioPlaybackState.Paused);
        player.Duration.ShouldBe(TimeSpan.FromSeconds(2));
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task Unknown_Length_Uses_The_Fallback()
    {
        using var player = new SimulatedAudioPlayer();
        await player.LoadAsync(AudioSource.FromUri(new Uri("https://example.invalid/a.mp3")), TestContext.Current.CancellationToken);
        player.Duration.ShouldBe(SimulatedAudioBackend.FallbackDuration);
    }

    [Fact]
    public async Task Playing_Advances_And_Pausing_Holds()
    {
        using var player = await Loaded(10);
        player.Play();
        player.State.ShouldBe(AudioPlaybackState.Playing);
        await WaitFor(() => player.Position > TimeSpan.FromMilliseconds(100));
        player.Pause();
        player.State.ShouldBe(AudioPlaybackState.Paused);
        var held = player.Position;
        await Task.Delay(120, TestContext.Current.CancellationToken);
        player.Position.ShouldBe(held);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(9, 2)]
    public async Task Seek_Is_Clamped_To_The_Audio(double to, double expected)
    {
        using var player = await Loaded(2);
        player.Seek(TimeSpan.FromSeconds(to));
        player.Position.ShouldBe(TimeSpan.FromSeconds(expected));
    }

    [Fact]
    public async Task Reaching_The_End_Ends_And_Play_Restarts()
    {
        using var player = await Loaded(0.3);
        player.Play();
        await WaitFor(() => player.State == AudioPlaybackState.Ended);
        player.Position.ShouldBe(player.Duration);
        player.Play();
        player.State.ShouldBe(AudioPlaybackState.Playing);
        player.Position.ShouldBeLessThan(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task Seeking_Back_From_The_End_Pauses()
    {
        using var player = await Loaded(0.3);
        player.Play();
        await WaitFor(() => player.State == AudioPlaybackState.Ended);
        player.Seek(TimeSpan.FromSeconds(0.1));
        player.State.ShouldBe(AudioPlaybackState.Paused);
    }

    [Fact]
    public async Task Looping_Wraps_Instead_Of_Ending()
    {
        using var player = await Loaded(0.3);
        player.IsLooping = true;
        player.Play();
        await Task.Delay(700, TestContext.Current.CancellationToken);
        player.State.ShouldBe(AudioPlaybackState.Playing);
        player.Position.ShouldBeLessThan(player.Duration);
    }

    [Fact]
    public void Volume_Is_Clamped_And_Independent_Of_Mute()
    {
        using var player = new SimulatedAudioPlayer();
        player.Volume = 3;
        player.Volume.ShouldBe(1);
        player.Volume = double.NaN;
        player.Volume.ShouldBe(0);
        player.Volume = 0.4;
        player.IsMuted = true;
        player.Volume.ShouldBe(0.4);
    }

    [Fact]
    public async Task Events_Report_State_And_Length()
    {
        var states = new List<AudioPlaybackState>();
        var lengths = 0;
        using var player = new SimulatedAudioPlayer();
        player.StateChanged += (_, _) => { lock (states) { states.Add(player.State); } };
        player.DurationChanged += (_, _) => Interlocked.Increment(ref lengths);
        await player.LoadAsync(Wav(1), TestContext.Current.CancellationToken);
        player.Play();
        player.Pause();
        await WaitFor(() => { lock (states) { return states.Count >= 3; } });
        lock (states)
        {
            states.ShouldBe([AudioPlaybackState.Paused, AudioPlaybackState.Playing, AudioPlaybackState.Paused]);
        }
        lengths.ShouldBe(1);
    }

    [Fact]
    public void The_Default_Backend_Is_Silent()
    {
        ArcAudio.Backend.ShouldBeSameAs(SimulatedAudioBackend.Instance);
        ArcAudio.Backend.IsAudible.ShouldBeFalse();
        using var player = ArcAudio.CreatePlayer();
        player.ShouldBeOfType<SimulatedAudioPlayer>();
    }
}
