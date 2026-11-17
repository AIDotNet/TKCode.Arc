using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiSendButton"/>: the four states (pseudo-classes, colors, names, tooltips, automation), presses that do
/// nothing in Empty and Blocked, the progress ring (jump at the start, monotonic chase, catching up with chunks, fill and
/// fade at the end, freeze on stop, reset) and the arrow / stop morph at every motion level. Frames run on a manual clock.
/// </summary>
public class AiSendButtonTests
{
    private const string Send = "Send message";
    private const string Stop = "Stop generating";
    private const string Empty = "Type a message to send";
    private const string Blocked = "Send after the uploads finish";

    private sealed class Rig : IDisposable
    {
        private readonly IDisposable _clockInstall;

        public ManualFrameClock Clock { get; } = new();
        public AiSendButton Button { get; }
        public Window Window { get; }
        public int Clicks { get; private set; }

        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, AiSendState state = AiSendState.Empty, double progress = 0,
                   AiSendButtonAppearance appearance = AiSendButtonAppearance.Ring)
        {
            _clockInstall = Clock.Install();
            Button = new AiSendButton
            {
                Appearance = appearance,
                SendName = Send,
                StopName = Stop,
                EmptyName = Empty,
                BlockedName = Blocked,
                Progress = progress,
                State = state
            };
            Button.Click += (_, _) => Clicks++;
            Window = TestHelpers.Show(new StackPanel { Margin = new Thickness(24), Children = { Button } }, level);
            Dispatcher.UIThread.RunJobs();
        }

        public ArcRing Ring => Button.Ring!;
        public ArcRing Track => Button.Track!;
        public AiSendGlyph Glyph => Button.Glyph!;

        /// <summary>Runs <paramref name="count"/> frames of 1/60 s, then the continuations they posted.</summary>
        public void Frames(int count)
        {
            CoreFrames.Run(Clock, count);
            Dispatcher.UIThread.RunJobs();
        }

        public Point Center => Button.TranslatePoint(new Point(18, 18), Window) ?? default;

        public void Press()
        {
            Window.MouseMove(Center, RawInputModifiers.None);
            Window.MouseDown(Center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        public void Release()
        {
            Window.MouseUp(Center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose()
        {
            Window.Close();
            _clockInstall.Dispose();
        }
    }

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    #region States

    [AvaloniaFact]
    public void Defaults_To_An_Empty_36px_Circle_With_An_Arrow_And_No_Ring()
    {
        using var rig = new Rig();
        var button = rig.Button;
        Assert.Equal(AiSendState.Empty, button.State);
        Assert.Equal(36, button.Bounds.Width);
        Assert.Equal(36, button.Bounds.Height);
        Assert.True(button.IsEnabled);
        Assert.True(button.Focusable);
        Assert.False(rig.Glyph.IsStop);
        Assert.False(rig.Glyph.ShowsStop);
        Assert.Equal(0, rig.Ring.Opacity);
        Assert.Equal(0, rig.Track.Opacity);
        Assert.Equal(0, button.DisplayedProgress);
        // The ring is a 42.5px circle centred on the button: 1.75px clear of its edge with a 1.5px stroke.
        Assert.Equal(42.5, rig.Ring.Bounds.Width, 3);
        Assert.Equal(-3.25, rig.Ring.Bounds.X, 3);
        Assert.Equal(1.5, rig.Ring.Thickness);
        Assert.False(rig.Ring.IsRoundCap);
    }

    [AvaloniaFact]
    public void Each_State_Sets_One_Pseudo_Class()
    {
        using var rig = new Rig();
        var button = rig.Button;
        var classes = new[]
        {
            (AiSendState.Empty, AiSendButton.EmptyPseudoClass),
            (AiSendState.Ready, AiSendButton.ReadyPseudoClass),
            (AiSendState.Blocked, AiSendButton.BlockedPseudoClass),
            (AiSendState.Generating, AiSendButton.GeneratingPseudoClass)
        };
        foreach (var (state, _) in classes)
        {
            button.State = state;
            foreach (var (other, pseudo) in classes)
            {
                Assert.Equal(other == state, button.Classes.Contains(pseudo));
            }
        }
    }

    [AvaloniaFact]
    public void Colors_Follow_The_State()
    {
        using var rig = new Rig();
        var button = rig.Button;
        Assert.Equal(Token(button, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(button.Background));
        Assert.Equal(Token(button, ArcDesignTokenKind.TextMuted), TestHelpers.ColorOf(button.Foreground));
        Assert.Equal(Token(button, ArcDesignTokenKind.TextMuted), TestHelpers.ColorOf(rig.Glyph.Foreground));

        button.State = AiSendState.Ready;
        Assert.Equal(Token(button, ArcDesignTokenKind.Accent), TestHelpers.ColorOf(button.Background));
        Assert.Equal(Token(button, ArcDesignTokenKind.AccentForeground), TestHelpers.ColorOf(rig.Glyph.Foreground));

        button.State = AiSendState.Blocked;
        Assert.Equal(Token(button, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(button.Background));
        Assert.Equal(Token(button, ArcDesignTokenKind.TextMuted), TestHelpers.ColorOf(rig.Glyph.Foreground));

        button.State = AiSendState.Generating;
        Assert.Equal(Token(button, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(button.Background));
        Assert.Equal(Token(button, ArcDesignTokenKind.Foreground), TestHelpers.ColorOf(rig.Glyph.Foreground));
        Assert.Equal(Token(button, ArcDesignTokenKind.Accent), TestHelpers.ColorOf(rig.Ring.Foreground));
        Assert.Equal(Token(button, ArcDesignTokenKind.Border), TestHelpers.ColorOf(rig.Track.TrackBrush));
    }

    [AvaloniaFact]
    public void Hover_Darkens_Only_The_Ready_Button()
    {
        using var rig = new Rig(state: AiSendState.Ready);
        var button = rig.Button;
        rig.Window.MouseMove(rig.Center, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(AiSendButton.HoverPseudoClass, button.Classes);
        Assert.Equal(Token(button, ArcDesignTokenKind.AccentStrong), TestHelpers.ColorOf(button.Background));

        button.State = AiSendState.Empty;
        Assert.Equal(Token(button, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(button.Background));
        button.State = AiSendState.Ready;

        rig.Window.MouseMove(new Point(390, 290), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(AiSendButton.HoverPseudoClass, button.Classes);
        Assert.Equal(Token(button, ArcDesignTokenKind.Accent), TestHelpers.ColorOf(button.Background));
    }

    #endregion

    #region Presses

    [AvaloniaFact]
    public void Empty_And_Blocked_Raise_No_Click_From_Pointer_Or_Keys()
    {
        using var rig = new Rig();
        var button = rig.Button;
        var executed = 0;
        button.Command = new RelayCommand(() => executed++);
        button.Focus(NavigationMethod.Tab);
        foreach (var state in new[] { AiSendState.Empty, AiSendState.Blocked })
        {
            button.State = state;
            rig.Press();
            rig.Release();
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            rig.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            rig.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            rig.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, rig.Clicks);
            Assert.Equal(0, executed);
            // Still enabled and focused: the state is a look, not IsEnabled.
            Assert.True(button.IsEffectivelyEnabled);
            Assert.True(button.IsFocused);
        }
    }

    [AvaloniaFact]
    public void Ready_And_Generating_Click_From_Pointer_Enter_And_Space()
    {
        using var rig = new Rig(state: AiSendState.Ready);
        var button = rig.Button;
        var executed = 0;
        button.Command = new RelayCommand(() => executed++);
        rig.Press();
        rig.Release();
        Assert.Equal(1, rig.Clicks);
        Assert.Equal(1, executed);

        button.State = AiSendState.Generating;
        button.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, rig.Clicks);
        rig.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        rig.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, rig.Clicks);
        Assert.Equal(3, executed);
    }

    [AvaloniaFact]
    public void A_Press_Scales_To_0_92_At_Full_Motion_Only_And_Only_When_It_Acts()
    {
        using (var rig = new Rig(ArcMotionLevel.Full, AiSendState.Ready))
        {
            rig.Press();
            Assert.Equal(0.92, rig.Button.RenderTransform!.Value.M11, 3);
            rig.Release();
            Assert.Equal(1, rig.Button.RenderTransform?.Value.M11 ?? 1, 3);

            rig.Button.State = AiSendState.Empty;
            rig.Press();
            Assert.Equal(1, rig.Button.RenderTransform?.Value.M11 ?? 1, 3);
            rig.Release();
        }
        using (var rig = new Rig(ArcMotionLevel.Off, AiSendState.Generating))
        {
            // Without the scale a press of the stop button shows a color instead.
            rig.Press();
            Assert.Equal(1, rig.Button.RenderTransform?.Value.M11 ?? 1, 3);
            Assert.Equal(Token(rig.Button, ArcDesignTokenKind.Border), TestHelpers.ColorOf(rig.Button.Background));
            rig.Release();
        }
    }

    #endregion

    #region Names and automation

    [AvaloniaFact]
    public void Names_And_Tooltips_Follow_The_State()
    {
        using var rig = new Rig();
        var button = rig.Button;
        var expected = new[]
        {
            (AiSendState.Empty, Empty),
            (AiSendState.Ready, Send),
            (AiSendState.Blocked, Blocked),
            (AiSendState.Generating, Stop)
        };
        foreach (var (state, name) in expected)
        {
            button.State = state;
            Assert.Equal(name, AutomationProperties.GetName(button));
            Assert.Equal(name, Tooltip.GetTip(button));
            Assert.Equal(name, button.CurrentName);
        }

        // Without their own text, Empty and Blocked read as the send button.
        button.EmptyName = null;
        button.BlockedName = null;
        button.State = AiSendState.Empty;
        Assert.Equal(Send, AutomationProperties.GetName(button));
        button.State = AiSendState.Blocked;
        Assert.Equal(Send, Tooltip.GetTip(button));

        // A new reason while blocked shows at once.
        button.BlockedName = "Shorten the message to send it";
        Assert.Equal("Shorten the message to send it", AutomationProperties.GetName(button));
        Assert.Equal("Shorten the message to send it", Tooltip.GetTip(button));

        // No text at all: no name and no tooltip.
        button.State = AiSendState.Generating;
        button.StopName = null;
        Assert.Null(AutomationProperties.GetName(button));
        Assert.Null(Tooltip.GetTip(button));
    }

    [AvaloniaFact]
    public void Automation_Reports_Empty_And_Blocked_As_Not_Enabled_But_Focusable()
    {
        using var rig = new Rig();
        var button = rig.Button;
        var peer = ControlAutomationPeer.CreatePeerForElement(button);
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        foreach (var state in new[] { AiSendState.Empty, AiSendState.Ready, AiSendState.Blocked, AiSendState.Generating })
        {
            button.State = state;
            Assert.Equal(state is AiSendState.Ready or AiSendState.Generating, peer.IsEnabled());
            Assert.True(peer.IsKeyboardFocusable());
            Assert.Equal(button.CurrentName, peer.GetName());
        }
        // The mark and the rings inside are not separate elements.
        Assert.Empty(peer.GetChildren());
    }

    #endregion

    #region Ring

    [AvaloniaFact]
    public void Generating_Shows_The_Progress_So_Far_At_Once_And_Fades_The_Track_In()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Ready, progress: 0.1);
        rig.Button.State = AiSendState.Generating;
        // Arc starts the arc at 10% on the first frame so the start is visible.
        Assert.Equal(0.1, rig.Button.DisplayedProgress, 4);
        Assert.Equal(1, rig.Ring.Opacity);
        Assert.Equal(0, rig.Track.Opacity);

        rig.Frames(6);
        Assert.InRange(rig.Track.Opacity, 0.3, 0.9);
        rig.Frames(10);
        Assert.Equal(1, rig.Track.Opacity);
        Assert.Equal(0.1, rig.Button.DisplayedProgress, 4);
    }

    [AvaloniaFact]
    public void The_Arc_Chases_The_Target_At_15_Percent_Per_Frame_And_Never_Moves_Back()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Generating, progress: 0.1);
        rig.Button.Progress = 0.5;
        Assert.Equal(0.1, rig.Button.DisplayedProgress, 4);
        rig.Frames(1);
        Assert.Equal(0.1 + 0.4 * 0.15, rig.Button.DisplayedProgress, 3);

        var previous = rig.Button.DisplayedProgress;
        for (var i = 0; i < 40; i++)
        {
            if (i == 5)
            {
                // A lower estimate does not pull the arc back.
                rig.Button.Progress = 0.3;
            }
            rig.Frames(1);
            var shown = rig.Button.DisplayedProgress;
            Assert.True(shown >= previous, $"frame {i}: {shown} < {previous}");
            Assert.True(shown <= 0.5 + 1e-9, $"frame {i}: {shown} passed the target");
            previous = shown;
        }
        rig.Frames(60);
        Assert.Equal(0.5, rig.Button.DisplayedProgress, 3);
        Assert.Equal(0.5, rig.Ring.Value, 6);
    }

    [AvaloniaFact]
    public void A_Jump_Of_The_Target_Is_Caught_Up_Smoothly()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Generating, progress: 0.2);
        rig.Button.Progress = 0.9;
        var steps = new List<double>();
        for (var i = 0; i < 12; i++)
        {
            rig.Frames(1);
            steps.Add(rig.Button.DisplayedProgress);
        }
        // Each frame closes 15% of what is left: no step is larger than the first one, none lands at once.
        Assert.Equal(0.2 + 0.7 * 0.15, steps[0], 3);
        for (var i = 1; i < steps.Count; i++)
        {
            Assert.True(steps[i] - steps[i - 1] <= steps[0] - 0.2 + 1e-9);
            Assert.True(steps[i] > steps[i - 1]);
        }
        Assert.True(steps[^1] < 0.9);
    }

    [AvaloniaFact]
    public void A_Finished_Reply_Fills_The_Arc_Then_Fades_And_Resets()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Generating, progress: 0.1);
        rig.Button.Progress = 0.6;
        rig.Frames(30);
        rig.Button.Progress = 1;
        rig.Button.State = AiSendState.Ready;
        Assert.Equal(1, rig.Ring.Opacity);

        // About 130 ms to fill, while the arc waits 120 ms before fading.
        rig.Frames(7);
        Assert.True(rig.Button.DisplayedProgress > 0.98, $"{rig.Button.DisplayedProgress}");
        Assert.True(rig.Ring.Opacity > 0.95, $"{rig.Ring.Opacity}");
        Assert.True(rig.Track.Opacity < 1);

        rig.Frames(20);
        Assert.Equal(0, rig.Ring.Opacity);
        Assert.Equal(0, rig.Track.Opacity);
        Assert.Equal(0, rig.Button.DisplayedProgress);
        Assert.Equal(0, rig.Ring.Value);
    }

    [AvaloniaFact]
    public void A_Stopped_Reply_Fades_Where_The_Arc_Is()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Generating, progress: 0.1);
        rig.Button.Progress = 0.7;
        rig.Frames(4);
        var shown = rig.Button.DisplayedProgress;
        Assert.InRange(shown, 0.2, 0.6);

        rig.Button.State = AiSendState.Ready;
        rig.Frames(8);
        Assert.Equal(shown, rig.Button.DisplayedProgress, 6);
        rig.Frames(20);
        Assert.Equal(0, rig.Button.DisplayedProgress);
        Assert.Equal(0, rig.Ring.Opacity);
    }

    [AvaloniaFact]
    public void A_Finish_Reported_Just_After_Leaving_Still_Fills_The_Arc()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Generating, progress: 0.4);
        rig.Button.State = AiSendState.Ready;
        rig.Frames(1);
        rig.Button.Progress = 1;
        rig.Frames(7);
        Assert.True(rig.Button.DisplayedProgress > 0.98);
        rig.Frames(30);
        Assert.Equal(0, rig.Button.DisplayedProgress);
    }

    [AvaloniaFact]
    public void Generating_Again_During_The_Fade_Starts_A_Fresh_Ring()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Generating, progress: 0.8);
        rig.Button.Progress = 1;
        rig.Button.State = AiSendState.Ready;
        rig.Frames(12);
        rig.Button.Progress = 0.1;
        rig.Button.State = AiSendState.Generating;
        Assert.Equal(0.1, rig.Button.DisplayedProgress, 4);
        Assert.Equal(1, rig.Ring.Opacity);
        // The old fade's end does not reset the new ring.
        rig.Frames(40);
        Assert.Equal(0.1, rig.Button.DisplayedProgress, 4);
        Assert.Equal(1, rig.Ring.Opacity);
        Assert.Equal(1, rig.Track.Opacity);
    }

    [AvaloniaFact]
    public void Progress_Outside_Generating_Does_Not_Show_And_Is_Clamped()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Ready);
        rig.Button.Progress = 0.5;
        rig.Frames(10);
        Assert.Equal(0, rig.Button.DisplayedProgress);
        Assert.Equal(0, rig.Ring.Opacity);
        rig.Button.Progress = 2;
        Assert.Equal(1, rig.Button.Progress);
        rig.Button.Progress = double.NaN;
        Assert.Equal(0, rig.Button.Progress);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Arc_And_Fades_The_Ring_In_120ms()
    {
        using var rig = new Rig(ArcMotionLevel.Reduced, AiSendState.Ready, progress: 0.1);
        rig.Button.State = AiSendState.Generating;
        rig.Button.Progress = 0.6;
        Assert.Equal(0.6, rig.Button.DisplayedProgress, 6);
        Assert.False(rig.Ring.IsChasing);
        rig.Frames(8);
        Assert.Equal(1, rig.Track.Opacity);
        Assert.Equal(1, rig.Ring.Opacity);

        rig.Button.Progress = 1;
        Assert.Equal(1, rig.Button.DisplayedProgress, 6);
        rig.Button.State = AiSendState.Ready;
        rig.Frames(3);
        Assert.InRange(rig.Ring.Opacity, 0.2, 0.9);
        rig.Frames(6);
        Assert.Equal(0, rig.Ring.Opacity);
        Assert.Equal(0, rig.Button.DisplayedProgress);
    }

    [AvaloniaFact]
    public void Off_Shows_And_Hides_The_Ring_At_Once()
    {
        using var rig = new Rig(ArcMotionLevel.Off, AiSendState.Ready, progress: 0.25);
        rig.Button.State = AiSendState.Generating;
        Assert.Equal(1, rig.Ring.Opacity);
        Assert.Equal(1, rig.Track.Opacity);
        rig.Button.Progress = 0.75;
        Assert.Equal(0.75, rig.Button.DisplayedProgress, 6);
        rig.Button.State = AiSendState.Empty;
        Assert.Equal(0, rig.Ring.Opacity);
        Assert.Equal(0, rig.Track.Opacity);
        Assert.Equal(0, rig.Button.DisplayedProgress);
        Assert.Equal(0, rig.Ring.Value);
    }

    [AvaloniaFact]
    public void Created_While_Generating_Shows_The_Ring_Without_Motion()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Generating, progress: 0.3);
        Assert.Equal(1, rig.Ring.Opacity);
        Assert.Equal(1, rig.Track.Opacity);
        Assert.Equal(0.3, rig.Button.DisplayedProgress, 6);
        Assert.True(rig.Glyph.ShowsStop);
        Assert.False(rig.Glyph.IsMorphing);
    }

    #endregion

    #region Morph

    [AvaloniaFact]
    public void The_Arrow_Morphs_Into_The_Stop_Square_And_Back_At_Full_Motion()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Ready);
        rig.Button.State = AiSendState.Generating;
        Assert.True(rig.Glyph.IsStop);
        Assert.True(rig.Glyph.ShowsStop);
        Assert.True(rig.Glyph.IsMorphing);
        rig.Frames(6);
        Assert.True(rig.Glyph.IsMorphing);
        rig.Frames(60);
        Assert.False(rig.Glyph.IsMorphing);

        rig.Button.State = AiSendState.Ready;
        Assert.False(rig.Glyph.ShowsStop);
        Assert.True(rig.Glyph.IsMorphing);
        // A quick send again reverses from the outline on screen.
        rig.Frames(4);
        rig.Button.State = AiSendState.Generating;
        Assert.True(rig.Glyph.ShowsStop);
        Assert.True(rig.Glyph.IsMorphing);
        rig.Frames(60);
        Assert.False(rig.Glyph.IsMorphing);
    }

    [AvaloniaFact]
    public void The_Morph_Settles_Within_Half_A_Second()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Ready);
        rig.Button.State = AiSendState.Generating;
        // spring(0.32, 0.05) rests in about 430 ms.
        rig.Frames(36);
        Assert.False(rig.Glyph.IsMorphing);
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Swap_The_Mark_Without_A_Morph()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            using var rig = new Rig(level, AiSendState.Ready);
            rig.Button.State = AiSendState.Generating;
            Assert.True(rig.Glyph.ShowsStop);
            Assert.False(rig.Glyph.IsMorphing);
            rig.Button.State = AiSendState.Empty;
            Assert.False(rig.Glyph.ShowsStop);
            Assert.False(rig.Glyph.IsMorphing);
        }
    }

    [AvaloniaFact]
    public void Empty_Blocked_And_Ready_Share_The_Arrow()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        foreach (var state in new[] { AiSendState.Ready, AiSendState.Blocked, AiSendState.Empty })
        {
            rig.Button.State = state;
            Assert.False(rig.Glyph.ShowsStop);
            Assert.False(rig.Glyph.IsMorphing);
        }
    }

    #endregion

    #region Solid (the AI chat's button)

    [AvaloniaFact]
    public void Solid_Has_No_Ring_And_An_18px_Arrow_That_Crossfades()
    {
        using var rig = new Rig(appearance: AiSendButtonAppearance.Solid);
        var button = rig.Button;
        Assert.Contains(AiSendButton.SolidPseudoClass, button.Classes);
        Assert.Equal(36, button.Bounds.Width);
        Assert.Null(button.Ring);
        Assert.Null(button.Track);
        Assert.Equal(18, rig.Glyph.Size);
        Assert.Equal(18, rig.Glyph.Bounds.Width);
        Assert.Equal(AiSendGlyphSwap.Crossfade, rig.Glyph.Swap);
        Assert.Equal(1, rig.Glyph.ArrowOpacity);
        Assert.Equal(0, rig.Glyph.StopOpacity);
    }

    [AvaloniaFact]
    public void Solid_Generating_Is_An_AccentForeground_Square_On_The_Accent_And_Ignores_Progress()
    {
        using var rig = new Rig(appearance: AiSendButtonAppearance.Solid);
        var button = rig.Button;
        Assert.Equal(Token(button, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(button.Background));
        Assert.Equal(Token(button, ArcDesignTokenKind.TextMuted), TestHelpers.ColorOf(rig.Glyph.Foreground));
        button.State = AiSendState.Ready;
        Assert.Equal(Token(button, ArcDesignTokenKind.Accent), TestHelpers.ColorOf(button.Background));
        Assert.Equal(Token(button, ArcDesignTokenKind.AccentForeground), TestHelpers.ColorOf(rig.Glyph.Foreground));

        button.State = AiSendState.Generating;
        button.Progress = 0.6;
        rig.Frames(20);
        Assert.Equal(Token(button, ArcDesignTokenKind.Accent), TestHelpers.ColorOf(button.Background));
        Assert.Equal(Token(button, ArcDesignTokenKind.AccentForeground), TestHelpers.ColorOf(rig.Glyph.Foreground));
        Assert.True(rig.Glyph.ShowsStop);
        Assert.Equal(0, rig.Glyph.ArrowOpacity);
        Assert.Equal(1, rig.Glyph.StopOpacity);
        Assert.Equal(0, button.DisplayedProgress);

        // Darker under the pointer, like the ready button; the focus ring stays 2px outside the circle.
        rig.Window.MouseMove(rig.Center, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Token(button, ArcDesignTokenKind.AccentStrong), TestHelpers.ColorOf(button.Background));
        button.Focus(NavigationMethod.Tab);
        var ring = button.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_FocusRing");
        Assert.True(ring.IsVisible);
        Assert.Equal(new Thickness(-4), ring.Margin);
    }

    [AvaloniaFact]
    public void Solid_Has_No_Tooltip_But_Names_Every_State()
    {
        using var rig = new Rig(appearance: AiSendButtonAppearance.Solid);
        var button = rig.Button;
        Assert.Null(Tooltip.GetTip(button));
        Assert.Equal(Empty, AutomationProperties.GetName(button));
        button.State = AiSendState.Generating;
        Assert.Null(Tooltip.GetTip(button));
        Assert.Equal(Stop, AutomationProperties.GetName(button));
        button.Appearance = AiSendButtonAppearance.Ring;
        Assert.Equal(Stop, Tooltip.GetTip(button));
    }

    [AvaloniaFact]
    public void Solid_Crossfades_The_Arrow_Out_In_100ms_And_The_Square_In_In_160ms()
    {
        using var rig = new Rig(ArcMotionLevel.Full, AiSendState.Ready, appearance: AiSendButtonAppearance.Solid);
        rig.Button.State = AiSendState.Generating;
        rig.Frames(1);
        Assert.InRange(rig.Glyph.ArrowOpacity, 0.01, 0.99);
        Assert.InRange(rig.Glyph.StopOpacity, 0.01, 0.99);
        Assert.False(rig.Glyph.IsMorphing);
        rig.Frames(6);
        // 116 ms: the arrow is gone, the square nearly there.
        Assert.Equal(0, rig.Glyph.ArrowOpacity);
        Assert.InRange(rig.Glyph.StopOpacity, 0.8, 0.999);
        rig.Frames(3);
        Assert.Equal(1, rig.Glyph.StopOpacity);

        // Back mid-way: from where the marks are.
        rig.Button.State = AiSendState.Empty;
        rig.Frames(2);
        Assert.InRange(rig.Glyph.StopOpacity, 0.01, 0.99);
        Assert.InRange(rig.Glyph.ArrowOpacity, 0.01, 0.99);
        rig.Frames(12);
        Assert.Equal(1, rig.Glyph.ArrowOpacity);
        Assert.Equal(0, rig.Glyph.StopOpacity);
    }

    [AvaloniaFact]
    public void Solid_Crossfades_In_120ms_Reduced_And_Swaps_At_Once_Off()
    {
        using (var reduced = new Rig(ArcMotionLevel.Reduced, AiSendState.Ready, appearance: AiSendButtonAppearance.Solid))
        {
            reduced.Button.State = AiSendState.Generating;
            reduced.Frames(4);
            Assert.InRange(reduced.Glyph.StopOpacity, 0.4, 0.7);
            Assert.InRange(reduced.Glyph.ArrowOpacity, 0.3, 0.6);
            reduced.Frames(4);
            Assert.Equal(1, reduced.Glyph.StopOpacity);
            Assert.Equal(0, reduced.Glyph.ArrowOpacity);
        }
        using var off = new Rig(ArcMotionLevel.Off, AiSendState.Ready, appearance: AiSendButtonAppearance.Solid);
        off.Button.State = AiSendState.Generating;
        Assert.Equal(1, off.Glyph.StopOpacity);
        Assert.Equal(0, off.Glyph.ArrowOpacity);
    }

    [AvaloniaFact]
    public void Turning_Solid_While_Generating_Drops_The_Ring_And_Back_Shows_It()
    {
        using var rig = new Rig(state: AiSendState.Generating, progress: 0.4);
        Assert.Equal(1, rig.Ring.Opacity);
        Assert.Equal(0.4, rig.Button.DisplayedProgress, 3);
        rig.Button.Appearance = AiSendButtonAppearance.Solid;
        Dispatcher.UIThread.RunJobs();
        rig.Button.Measure(Size.Infinity);
        Assert.Null(rig.Button.Ring);
        Assert.Equal(0, rig.Button.DisplayedProgress);
        Assert.True(rig.Glyph.ShowsStop);
        Assert.Equal(1, rig.Glyph.StopOpacity);
        rig.Button.Appearance = AiSendButtonAppearance.Ring;
        rig.Button.Measure(Size.Infinity);
        Assert.NotNull(rig.Button.Ring);
        Assert.Equal(1, rig.Ring.Opacity);
        Assert.Equal(0.4, rig.Button.DisplayedProgress, 3);
    }

    #endregion

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
