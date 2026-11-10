using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.TextEffects;

public class SlotTextTests
{
    private static void Advance(ManualFrameClock clock, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void A_Number_Is_Grouped_In_En_Us_And_A_String_Shows_As_It_Is()
    {
        var text = new SlotText { Value = 12480 };
        text.Text.ShouldBe("12,480");
        text.Value = 1234.5678;
        text.Text.ShouldBe("1,234.568");
        text.Value = "12480";
        text.Text.ShouldBe("12480");
        text.Value = 5.0;
        text.Format = number => $"${number:0}";
        text.Text.ShouldBe("$5");
    }

    [Fact]
    public void A_Digit_Counts_Through_The_Wheel_In_The_Direction_The_Number_Moved()
    {
        SlotText.BuildStrip("3", "5", 0, true, 1).ShouldBe(["3", "4", "5"]);
        SlotText.BuildStrip("5", "3", 0, false, 1).ShouldBe(["5", "4", "3"]);
        // Rising wraps past 9 instead of turning back.
        SlotText.BuildStrip("9", "1", 0, true, 1).ShouldBe(["9", "0", "1"]);
        var spun = SlotText.BuildStrip("3", "5", 1, true, 1);
        spun.Length.ShouldBe(13);
        spun[0].ShouldBe("3");
        spun[^1].ShouldBe("5");
        SlotText.BuildStrip("7", "7", 1, true, 1).ShouldBe(["7"]);
    }

    [Fact]
    public void A_New_Digit_Rolls_Up_From_Zero()
    {
        var strip = SlotText.BuildStrip(string.Empty, "2", 0, true, 1);
        strip.ShouldBe(["0", "1", "2"]);
    }

    [Fact]
    public void Letters_Shuffle_Deterministically_Through_Their_Own_Case()
    {
        var strip = SlotText.BuildStrip("a", "k", 1, true, 42);
        // Arc's length: from, spins * 5 + 2 fillers, to.
        strip.Length.ShouldBe(9);
        strip[0].ShouldBe("a");
        strip[^1].ShouldBe("k");
        strip.Skip(1).Take(7).ShouldAllBe(cell => cell.Length == 1 && char.IsAsciiLetterLower(cell[0]));
        SlotText.BuildStrip("a", "k", 1, true, 42).ShouldBe(strip);
        SlotText.BuildStrip("a", "k", 1, true, 43).ShouldNotBe(strip);

        SlotText.BuildStrip("a", "K", 1, true, 42).Skip(1).Take(7).ShouldAllBe(cell => char.IsAsciiLetterUpper(cell[0]));
        // What Arc's own generator gives for seed 42, spins 0, over the lowercase alphabet (run in Node).
        SlotText.BuildStrip("a", "b", 0, true, 42).ShouldBe(["a", "g", "c", "b"]);
    }

    [Fact]
    public void A_Symbol_Swaps_In_One_Step()
    {
        SlotText.BuildStrip("-", "+", 3, true, 1).ShouldBe(["-", "+"]);
        SlotText.BuildStrip("5", ",", 3, true, 1).ShouldBe(["5", ","]);
    }

    [Fact]
    public void Blur_Follows_Reel_Speed_Up_To_Its_Limit()
    {
        SlotText.BlurFor(0).ShouldBe(0);
        SlotText.BlurFor(2).ShouldBe(0);
        SlotText.BlurFor(10).ShouldBe(0.5, 1e-9);
        SlotText.BlurFor(-10).ShouldBe(0.5, 1e-9);
        SlotText.BlurFor(1000).ShouldBe(2.4);
    }

    [AvaloniaFact]
    public void The_First_Value_Lands_At_Once_And_Motion_Off_Jumps()
    {
        var text = new SlotText { Value = 120, FontSize = 20 };
        var window = TestHelpers.Show(text);
        try
        {
            text.ReelCharacters.ShouldBe(["1", "2", "0"]);
            text.ReelIsSpinning(0).ShouldBeFalse();
            text.ReelStrip(0).ShouldBe(["1"]);
            text.Bounds.Height.ShouldBe(23, 1e-6);

            text.Value = 345;
            text.ReelCharacters.ShouldBe(["3", "4", "5"]);
            text.ReelStrip(0).ShouldBe(["3"]);
            text.ReelIsSpinning(0).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Changes_Without_Spinning_And_Removes_At_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = 1000 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Reduced);
        try
        {
            text.Value = 25;
            text.ReelCharacters.ShouldBe(["2", "5"]);
            text.ReelIsSpinning(0).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reels_Stop_One_After_Another_Left_To_Right()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = 111 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = 222;
            text.ReelIsSpinning(0).ShouldBeTrue();
            text.ReelStrip(0).Count.ShouldBe(12);
            text.ReelStrip(0)[^1].ShouldBe("2");

            // First reel: 0.9 s; second 0.97 s; third 1.04 s.
            Advance(clock, 0.5);
            text.ReelPosition(0).ShouldBeGreaterThan(0);
            text.ReelPosition(0).ShouldBeLessThan(11);
            Advance(clock, 0.42);
            text.ReelIsSpinning(0).ShouldBeFalse();
            text.ReelPosition(0).ShouldBe(11);
            text.ReelIsSpinning(1).ShouldBeTrue();
            Advance(clock, 0.07);
            text.ReelIsSpinning(1).ShouldBeFalse();
            text.ReelIsSpinning(2).ShouldBeTrue();
            Advance(clock, 0.07);
            text.ReelIsSpinning(2).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Landing_Never_Passes_The_Target()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = 1 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = 2;
            var last = 0.0;
            for (var frame = 0; frame < 70; frame++)
            {
                Advance(clock, 1 / 60.0);
                var position = text.ReelPosition(0);
                position.ShouldBeGreaterThanOrEqualTo(last);
                position.ShouldBeLessThanOrEqualTo(11);
                last = position;
            }
            last.ShouldBe(11);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Falling_Number_Turns_The_Wheel_Backwards()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = 5, Spins = 0 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = 3;
            text.ReelStrip(0).ShouldBe(["5", "4", "3"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Target_Restarts_From_The_Cell_On_Screen()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = 0, Spins = 2 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = 5;
            Advance(clock, 0.1);
            var position = text.ReelPosition(0);
            var nearest = (int)Math.Round(position, MidpointRounding.AwayFromZero);
            var visible = text.ReelStrip(0)[nearest];

            text.Value = 9;
            text.ReelStrip(0)[0].ShouldBe(visible);
            text.ReelStrip(0)[^1].ShouldBe("9");
            // The sub-cell offset is kept, so the glyph on screen does not jump.
            text.ReelPosition(0).ShouldBe(position - nearest, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Number_Grows_On_The_Left_And_Keeps_The_Ones_Reel()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = 999 };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            var before = text.Bounds.Width;
            text.Value = 1000;
            // 1,000: the three trailing reels are the old ones and the two new ones grow from nothing.
            text.ReelCharacters.ShouldBe(["1", ",", "0", "0", "0"]);
            text.ReelWidth(0).ShouldBe(0);
            text.ReelStrip(4)[0].ShouldBe("9");
            text.ReelStrip(0)[^1].ShouldBe("1");
            text.ReelStrip(0)[0].ShouldBe("0");
            text.ReelStrip(1).ShouldBe([string.Empty, ","]);

            Advance(clock, 2);
            text.UpdateLayout();
            text.Bounds.Width.ShouldBeGreaterThan(before);
            text.ReelIsSpinning(0).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Leading_Symbol_Stays_On_Its_Own_Reel()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = 99, Format = number => $"${number:#,##0}" };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = 120;
            text.Text.ShouldBe("$120");
            text.ReelCharacters[0].ShouldBe("$");
            // The dollar reel never spins.
            text.ReelIsSpinning(0).ShouldBeFalse();
            text.ReelStrip(0).ShouldBe(["$"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Is_Matched_From_The_Start_And_Leaving_Reels_Shrink_Away()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = "Launch" };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = "Lift";
            text.Text.ShouldBe("Lift");
            text.ReelCharacters.Count.ShouldBe(6);
            text.ReelStrip(0).ShouldBe(["L"]);
            text.ReelCharacters[1].ShouldBe("i");

            Advance(clock, 2);
            text.ReelCharacters.ShouldBe(["L", "i", "f", "t"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Character_That_Comes_Back_Keeps_Its_Reel()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = "abc" };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = "ab";
            Advance(clock, 0.05);
            text.Value = "abc";
            Advance(clock, 2);
            text.ReelCharacters.ShouldBe(["a", "b", "c"]);
            text.UpdateLayout();
            text.ReelWidth(2).ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Align_Overrides_The_Default_End()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new SlotText { Value = "12", Align = SlotTextAlign.End };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            text.Value = "312";
            // The last two reels are untouched; the new reel is first.
            text.ReelCharacters.ShouldBe(["3", "1", "2"]);
            text.ReelStrip(1).ShouldBe(["1"]);
            text.ReelStrip(2).ShouldBe(["2"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_The_Text_And_Announce_Is_Polite()
    {
        var text = new SlotText { Value = 12480, Announce = true };
        var window = TestHelpers.Show(text);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(text);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            peer.GetName().ShouldBe("12,480");
            AutomationProperties.GetLiveSetting(text).ShouldBe(AutomationLiveSetting.Polite);
            text.Value = 3;
            peer.GetName().ShouldBe("3");
            text.Announce = false;
            AutomationProperties.GetLiveSetting(text).ShouldBe(AutomationLiveSetting.Off);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Space_And_Whole_Characters_Keep_Their_Own_Reels()
    {
        var text = new SlotText { Value = "a b\U0001F600" };
        var window = TestHelpers.Show(text);
        try
        {
            text.ReelCharacters.ShouldBe(["a", " ", "b", "\U0001F600"]);
            text.ReelWidth(1).ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }
}
