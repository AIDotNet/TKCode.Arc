using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>Typewriter terminal: the script clock, and the control's playback, keyboard, events and motion levels.</summary>
public class TypewriterTerminalTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static TypewriterTerminalPlayer Player(bool looping = false)
    {
        var player = new TypewriterTerminalPlayer { Speed = 20, CommandLead = 0.5, AfterCommand = 0.4, BetweenOutput = 0.1, LoopDelay = 1, IsLooping = looping };
        // "ls" typed, then two lines of output.
        player.Load([2, 5, 5], [true, false, false], [0, 0, 0.3]);
        return player;
    }

    private static void Run(TypewriterTerminalPlayer player, double seconds)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += 0.01)
        {
            player.Step(0.01);
        }
    }

    // --- clock -----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_command_shows_its_prompt_then_types_after_the_lead()
    {
        var player = Player();
        player.IsAtPrompt.ShouldBeTrue();
        Run(player, 0.45);
        player.Chars.ShouldBe(0);
        Run(player, 0.06);
        player.Chars.ShouldBe(1);
        player.IsTyping.ShouldBeTrue();
    }

    [Fact]
    public void Keystrokes_are_uneven_but_keep_the_pace()
    {
        var player = new TypewriterTerminalPlayer { Speed = 20, CommandLead = 0 };
        player.Load([200], [true], [0]);
        Run(player, 5);
        // 20 per second for 5 seconds, give or take the human unevenness.
        player.Chars.ShouldBeInRange(80, 120);
    }

    [Fact]
    public void Output_follows_the_enter_pause_and_each_line_its_own_pause()
    {
        var player = Player();
        Run(player, 0.5 + 0.2);
        player.Line.ShouldBe(0);
        player.Chars.ShouldBe(2);
        // The Enter pause ends the command, and the first output line appears with it.
        Run(player, 0.4);
        player.Line.ShouldBe(2);
        // The second waits its own 0.3s pause on top of the 0.1s between lines.
        Run(player, 0.2);
        player.Line.ShouldBe(2);
        Run(player, 0.25);
        player.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void A_long_frame_catches_up_without_skipping_lines()
    {
        var player = Player();
        player.Step(10).ShouldBe(2);
        player.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void A_looping_script_starts_over_after_the_loop_delay()
    {
        var player = Player(looping: true);
        Run(player, 1.6);
        player.IsFinished.ShouldBeTrue();
        Run(player, 1);
        player.IsFinished.ShouldBeFalse();
        player.Line.ShouldBe(0);
    }

    [Fact]
    public void Typing_instantly_puts_the_whole_command_down_at_once()
    {
        var player = Player();
        player.TypesInstantly = true;
        Run(player, 0.51);
        player.Chars.ShouldBe(2);
    }

    // --- control ---------------------------------------------------------------------------------------------------

    private static TypewriterTerminal Terminal(bool autoPlay = true) => new()
    {
        Title = "deploy",
        AutoPlay = autoPlay,
        TypingSpeed = 40,
        Lines =
        {
            TypewriterTerminalLine.Command("make"),
            TypewriterTerminalLine.Output("building", TypewriterTerminalTone.Muted),
            TypewriterTerminalLine.Output("done", TypewriterTerminalTone.Success)
        }
    };

    [AvaloniaFact]
    public void Plays_the_script_to_the_end_and_reports_each_line()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var terminal = Terminal();
        var window = TestHelpers.Show(terminal, ArcMotionLevel.Full);
        var lines = new List<int>();
        var completed = 0;
        terminal.LineCompleted += (_, e) => lines.Add(e.Index);
        terminal.Completed += (_, _) => completed++;
        terminal.IsPlaying.ShouldBeTrue();
        frames.Advance(TimeSpan.FromSeconds(3), Frame);
        lines.ShouldBe([0, 1, 2]);
        completed.ShouldBe(1);
        terminal.IsFinished.ShouldBeTrue();
        terminal.CompletedLines.ShouldBe(3);
        window.Close();
    }

    [AvaloniaFact]
    public void Reserves_the_height_of_the_whole_script()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var terminal = Terminal();
        var window = TestHelpers.Show(terminal, ArcMotionLevel.Full);
        var before = terminal.Bounds.Height;
        frames.Advance(TimeSpan.FromSeconds(3), Frame);
        terminal.Bounds.Height.ShouldBe(before);
        var screen = TestHelpers.Descendant<TypewriterTerminalScreen>(terminal);
        screen.LayoutCount.ShouldBe(3);
        screen.Bounds.Height.ShouldBeGreaterThanOrEqualTo(screen.LineHeight * 4 - 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Space_pauses_and_resumes_End_completes_Home_restarts()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var terminal = Terminal();
        var window = TestHelpers.Show(terminal, ArcMotionLevel.Full);
        terminal.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        terminal.IsPlaying.ShouldBeFalse();
        frames.Advance(TimeSpan.FromSeconds(3), Frame);
        terminal.CompletedLines.ShouldBe(0);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        terminal.IsPlaying.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        terminal.IsFinished.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        terminal.IsFinished.ShouldBeFalse();
        terminal.CompletedLines.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Without_motion_the_whole_script_shows_at_once()
    {
        var terminal = Terminal();
        var completed = 0;
        terminal.Completed += (_, _) => completed++;
        var window = TestHelpers.Show(terminal);
        terminal.IsFinished.ShouldBeTrue();
        completed.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void The_replay_button_shows_once_finished_and_starts_over()
    {
        var terminal = Terminal(autoPlay: false);
        var window = TestHelpers.Show(terminal);
        var replay = TestHelpers.Descendant<Avalonia.Controls.Button>(terminal, TypewriterTerminal.ReplayPart);
        replay.IsVisible.ShouldBeFalse();
        terminal.Complete();
        replay.IsVisible.ShouldBeTrue();
        AutomationProperties.GetName(replay).ShouldBe("Replay");
        terminal.IsLooping = true;
        replay.IsVisible.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Is_named_by_its_title_and_describes_the_whole_transcript()
    {
        var terminal = Terminal(autoPlay: false);
        var window = TestHelpers.Show(terminal);
        AutomationProperties.GetName(terminal).ShouldBe("deploy");
        var peer = ControlAutomationPeer.CreatePeerForElement(terminal);
        peer.GetHelpText().ShouldBe("$ make\nbuilding\ndone");
        window.Close();
    }

    [AvaloniaFact]
    public void Editing_the_script_rebuilds_it()
    {
        var terminal = Terminal(autoPlay: false);
        var window = TestHelpers.Show(terminal);
        terminal.Lines.Add(TypewriterTerminalLine.Output("extra"));
        terminal.Lines[0].Text = "make all";
        window.UpdateLayout();
        TestHelpers.Descendant<TypewriterTerminalScreen>(terminal).LayoutCount.ShouldBe(4);
        ControlAutomationPeer.CreatePeerForElement(terminal).GetHelpText().ShouldBe("$ make all\nbuilding\ndone\nextra");
        window.Close();
    }
}
