using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class OtpInputTests
{
    private static Window Show(Control control, double width = 600, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static OtpInputSlot Slot(OtpInput otp, int index) => otp.Slots[index];

    private static string Shown(OtpInput otp) => string.Join("|", otp.Slots.Select(s => s.Text ?? string.Empty));

    private static int FocusedIndex(OtpInput otp)
    {
        for (var i = 0; i < otp.Slots.Count; i++)
        {
            if (otp.Slots[i].IsFocused)
            {
                return i;
            }
        }
        return -1;
    }

    private static void Type(Window window, string text)
    {
        foreach (var c in text)
        {
            window.KeyTextInput(c.ToString());
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private const double FrameMs = 1000.0 / 60.0;

    /// <summary>Runs exactly <paramref name="count"/> 60Hz frames (the scheduler's first frame counts 1/60s too).</summary>
    private static void Frames(ManualFrameClock clock, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(TimeSpan.FromTicks(1));
        }
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Recorder : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_Are_Arcs_Six_Digit_Field()
    {
        var otp = new OtpInput();
        var window = Show(otp);
        try
        {
            otp.Length.ShouldBe(6);
            otp.Value.ShouldBe(string.Empty);
            otp.Label.ShouldBe("Verification code");
            otp.InputMode.ShouldBe(OtpInputMode.Numeric);
            otp.IsComplete.ShouldBeFalse();
            otp.Slots.Count.ShouldBe(6);
            var row = TestHelpers.Descendant<OtpInputRow>(otp);
            row.Bounds.Width.ShouldBe(292, 0.01);
            row.Bounds.Height.ShouldBe(48, 0.01);
            Slot(otp, 0).Bounds.ShouldBe(new Rect(0, 0, 42, 48));
            Slot(otp, 5).Bounds.X.ShouldBe(250, 0.01);
            AutomationProperties.GetName(Slot(otp, 0)).ShouldBe("Verification code, digit 1 of 6");
            AutomationProperties.GetName(Slot(otp, 5)).ShouldBe("Verification code, digit 6 of 6");
            AutomationProperties.GetName(row).ShouldBe("Verification code");
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(row);
            peer.GetAutomationControlType().ShouldBe(Avalonia.Automation.Peers.AutomationControlType.Group);
            peer.GetName().ShouldBe("Verification code");
            Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(otp).IsControlElement().ShouldBeFalse();
            // The label sits 8 above the row (14px on a 19.6 line).
            var inputs = TestHelpers.Descendant<Panel>(otp, OtpInput.InputsPart);
            inputs.TranslatePoint(default, otp)!.Value.Y.ShouldBe(27.6, 0.5);
            inputs.HorizontalAlignment.ShouldBe(Avalonia.Layout.HorizontalAlignment.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Value_Fills_Slots_From_The_Start()
    {
        var otp = new OtpInput { Value = "482" };
        var window = Show(otp);
        try
        {
            Shown(otp).ShouldBe("4|8|2|||");
            Slot(otp, 0).Classes.Contains(OtpInputSlot.FilledPseudoClass).ShouldBeTrue();
            Slot(otp, 3).Classes.Contains(OtpInputSlot.FilledPseudoClass).ShouldBeFalse();
            otp.Overlay!.GlyphStates.Select(g => g.Text).ShouldBe(["4", "8", "2", "", "", ""]);
            otp.Value = "123456789";
            Shown(otp).ShouldBe("1|2|3|4|5|6");
            otp.IsComplete.ShouldBeTrue();
            otp.Classes.Contains(OtpInput.CompletePseudoClass).ShouldBeTrue();
            otp.Value = null!;
            otp.Value.ShouldBe(string.Empty);
            Shown(otp).ShouldBe("|||||");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Fills_The_Slot_And_Moves_On()
    {
        var otp = new OtpInput();
        var window = Show(otp);
        var changes = new List<OtpInputValueChangedEventArgs>();
        otp.ValueChanged += (_, e) => changes.Add(e);
        try
        {
            otp.FocusSlot(0).ShouldBeTrue();
            Type(window, "4");
            otp.Value.ShouldBe("4");
            FocusedIndex(otp).ShouldBe(1);
            Type(window, "a");
            otp.Value.ShouldBe("4");
            FocusedIndex(otp).ShouldBe(1);
            Type(window, "82");
            otp.Value.ShouldBe("482");
            FocusedIndex(otp).ShouldBe(3);
            changes.Select(c => c.NewValue).ShouldBe(["4", "48", "482"]);
            changes.ShouldAllBe(c => c.ChangeSource == OtpInputChangeSource.Typing && c.IsUserEdit);
            otp.Value = "";
            changes[^1].ChangeSource.ShouldBe(OtpInputChangeSource.Programmatic);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Mode_Accepts_Letters()
    {
        var otp = new OtpInput { InputMode = OtpInputMode.Text, Length = 4 };
        var window = Show(otp);
        try
        {
            otp.FocusSlot(0);
            Type(window, "aB-1");
            otp.Value.ShouldBe("aB-1");
            Avalonia.Input.TextInput.TextInputOptions.GetContentType(Slot(otp, 0)).ShouldBe(Avalonia.Input.TextInput.TextInputContentType.Normal);
            otp.InputMode = OtpInputMode.Numeric;
            Avalonia.Input.TextInput.TextInputOptions.GetContentType(Slot(otp, 0)).ShouldBe(Avalonia.Input.TextInput.TextInputContentType.Digits);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Filled_Slot_Takes_No_More_Typing_Until_Selected()
    {
        var otp = new OtpInput { Value = "482" };
        var window = Show(otp);
        try
        {
            otp.FocusSlot(1);
            Type(window, "5");
            otp.Value.ShouldBe("482");
            FocusedIndex(otp).ShouldBe(1);
            Slot(otp, 1).SelectAll();
            Type(window, "5");
            otp.Value.ShouldBe("452");
            FocusedIndex(otp).ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Past_The_End_Lands_In_The_First_Empty_Slot()
    {
        // Arc keeps the code as one string: a character typed into slot 6 of "42" becomes the third character.
        var otp = new OtpInput { Value = "42" };
        var window = Show(otp);
        try
        {
            otp.FocusSlot(5);
            Type(window, "7");
            otp.Value.ShouldBe("427");
            Shown(otp).ShouldBe("4|2|7|||");
            FocusedIndex(otp).ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Slots()
    {
        var otp = new OtpInput { Value = "482" };
        var window = Show(otp);
        try
        {
            otp.FocusSlot(0);
            Press(window, PhysicalKey.ArrowLeft);
            FocusedIndex(otp).ShouldBe(0);
            Press(window, PhysicalKey.ArrowRight);
            Press(window, PhysicalKey.ArrowRight);
            FocusedIndex(otp).ShouldBe(2);
            for (var i = 0; i < 6; i++)
            {
                Press(window, PhysicalKey.ArrowRight);
            }
            FocusedIndex(otp).ShouldBe(5);
            Press(window, PhysicalKey.ArrowLeft);
            FocusedIndex(otp).ShouldBe(4);
            otp.Value.ShouldBe("482");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_On_An_Empty_Slot_Clears_The_One_Before_And_Moves_Back()
    {
        var otp = new OtpInput { Value = "482" };
        var window = Show(otp);
        var changes = new List<OtpInputValueChangedEventArgs>();
        otp.ValueChanged += (_, e) => changes.Add(e);
        try
        {
            otp.FocusSlot(3);
            Press(window, PhysicalKey.Backspace);
            otp.Value.ShouldBe("48");
            FocusedIndex(otp).ShouldBe(2);
            changes.Single().ChangeSource.ShouldBe(OtpInputChangeSource.Deletion);
            // Slot 3 is now empty too: the next Backspace clears slot 2 and moves on.
            Press(window, PhysicalKey.Backspace);
            otp.Value.ShouldBe("4");
            FocusedIndex(otp).ShouldBe(1);
            otp.FocusSlot(0);
            Press(window, PhysicalKey.Backspace);
            otp.Value.ShouldBe(string.Empty);
            Press(window, PhysicalKey.Backspace);
            FocusedIndex(otp).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_On_A_Filled_Slot_Deletes_Before_The_Caret()
    {
        var otp = new OtpInput();
        var window = Show(otp);
        try
        {
            otp.FocusSlot(0);
            Type(window, "4");
            Type(window, "8");
            otp.FocusSlot(1);
            // The caret is after the "8" (a controlled update leaves it at the end).
            Slot(otp, 1).CaretIndex.ShouldBe(1);
            Press(window, PhysicalKey.Backspace);
            otp.Value.ShouldBe("4");
            FocusedIndex(otp).ShouldBe(1);
            // With the caret before the character, Backspace has nothing to delete (as in the browser).
            otp.Value = "48";
            Slot(otp, 1).CaretIndex = 0;
            Press(window, PhysicalKey.Backspace);
            otp.Value.ShouldBe("48");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delete_Clears_The_Slot_And_Later_Characters_Close_Up()
    {
        var otp = new OtpInput { Value = "482" };
        var window = Show(otp);
        try
        {
            otp.FocusSlot(1);
            Press(window, PhysicalKey.Delete);
            otp.Value.ShouldBe("42");
            Shown(otp).ShouldBe("4|2||||");
            FocusedIndex(otp).ShouldBe(1);
            otp.FocusSlot(4);
            Press(window, PhysicalKey.Delete);
            otp.Value.ShouldBe("42");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_Spreads_The_Code_From_The_Focused_Slot()
    {
        var otp = new OtpInput { Value = "482" };
        var completed = new List<string>();
        var command = new Recorder();
        otp.CompletedCommand = command;
        otp.Completed += (_, e) => completed.Add(e.Code);
        var changes = new List<OtpInputValueChangedEventArgs>();
        otp.ValueChanged += (_, e) => changes.Add(e);
        var window = Show(otp);
        try
        {
            otp.FocusSlot(1);
            otp.ApplyPaste(1, " 91-3 5\n");
            otp.Value.ShouldBe("49135");
            FocusedIndex(otp).ShouldBe(5);
            changes.Single().ChangeSource.ShouldBe(OtpInputChangeSource.Paste);
            completed.ShouldBeEmpty();
            otp.ApplyPaste(0, "123456789");
            otp.Value.ShouldBe("123456");
            completed.ShouldBe(["123456"]);
            command.Parameters.ShouldBe(["123456"]);
            otp.ApplyPaste(0, "no digits");
            otp.Value.ShouldBe("123456");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Ctrl_V_Pastes_The_Clipboard()
    {
        var otp = new OtpInput();
        var window = Show(otp);
        try
        {
            var clipboard = window.Clipboard.ShouldNotBeNull();
            await clipboard.SetTextAsync("135790");
            otp.FocusSlot(0);
            Press(window, PhysicalKey.V, RawInputModifiers.Control);
            for (var i = 0; i < 5; i++)
            {
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
            }
            otp.Value.ShouldBe("135790");
            FocusedIndex(otp).ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Completed_Fires_For_Edits_That_Fill_Every_Slot()
    {
        var otp = new OtpInput { Length = 4 };
        var completed = new List<string>();
        otp.Completed += (_, e) => completed.Add(e.Code);
        var window = Show(otp);
        try
        {
            otp.Value = "1234";
            completed.ShouldBeEmpty();
            otp.Value = "";
            otp.FocusSlot(0);
            Type(window, "987");
            completed.ShouldBeEmpty();
            Type(window, "6");
            completed.ShouldBe(["9876"]);
            otp.IsComplete.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Keeps_Navigation_But_Ignores_Edits()
    {
        var otp = new OtpInput { Value = "48", IsReadOnly = true };
        var window = Show(otp);
        try
        {
            otp.Classes.Contains(OtpInput.ReadOnlyPseudoClass).ShouldBeTrue();
            otp.FocusSlot(2);
            Type(window, "2");
            Press(window, PhysicalKey.Backspace);
            otp.ApplyPaste(2, "1234");
            otp.Value.ShouldBe("48");
            Press(window, PhysicalKey.ArrowLeft);
            FocusedIndex(otp).ShouldBe(1);
            Press(window, PhysicalKey.Delete);
            otp.Value.ShouldBe("48");
            otp.IsReadOnly = false;
            Press(window, PhysicalKey.Delete);
            otp.Value.ShouldBe("4");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Error_Makes_The_Field_Invalid()
    {
        var otp = new OtpInput { Description = "Use the six digit code from your email." };
        var window = Show(otp);
        try
        {
            otp.ErrorMessage = "That code didn't match.";
            otp.IsInvalid.ShouldBeTrue();
            otp.Classes.Contains(OtpInput.InvalidPseudoClass).ShouldBeTrue();
            otp.Slots.ShouldAllBe(s => s.Classes.Contains(OtpInputSlot.InvalidPseudoClass));
            AutomationProperties.GetHelpText(Slot(otp, 0)).ShouldBe("Use the six digit code from your email. That code didn't match.");
            var field = TestHelpers.Descendant<Field>(otp, OtpInput.FieldPart);
            field.ErrorMessage.ShouldBe("That code didn't match.");
            field.Description.ShouldBe("Use the six digit code from your email.");
            otp.ErrorMessage = null;
            otp.IsInvalid.ShouldBeFalse();
            otp.Slots.ShouldAllBe(s => !s.Classes.Contains(OtpInputSlot.InvalidPseudoClass));
            otp.IsInvalid = true;
            otp.Slots.ShouldAllBe(s => s.Classes.Contains(OtpInputSlot.InvalidPseudoClass));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Success_Colors_The_Slots_Unless_Invalid()
    {
        var otp = new OtpInput { Value = "123456", IsSuccess = true };
        var window = Show(otp);
        try
        {
            otp.Classes.Contains(OtpInput.SuccessPseudoClass).ShouldBeTrue();
            otp.Slots.ShouldAllBe(s => s.Classes.Contains(OtpInputSlot.SuccessPseudoClass));
            otp.ErrorMessage = "Expired.";
            otp.Slots.ShouldAllBe(s => !s.Classes.Contains(OtpInputSlot.SuccessPseudoClass));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Slots_Cannot_Be_Focused_And_Characters_Dim()
    {
        var otp = new OtpInput { Value = "482", IsEnabled = false };
        var window = Show(otp);
        try
        {
            otp.FocusSlot(0).ShouldBeFalse();
            Slot(otp, 0).IsEffectivelyEnabled.ShouldBeFalse();
            otp.Overlay!.GlyphOpacity.ShouldBe(0.48, 1e-6);
            Slot(otp, 0).Opacity.ShouldBe(0.48, 1e-6);
            otp.IsEnabled = true;
            otp.Overlay.GlyphOpacity.ShouldBe(1, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Fields_Tighten_And_Shrink_The_Slots()
    {
        var otp = new OtpInput();
        var host = new Border { Width = 320, Child = otp, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(host);
        try
        {
            otp.Classes.Contains(OtpInput.NarrowPseudoClass).ShouldBeTrue();
            var row = TestHelpers.Descendant<OtpInputRow>(otp);
            row.Gap.ShouldBe(6);
            row.Bounds.Height.ShouldBe(44, 0.01);
            row.Bounds.Width.ShouldBe(282, 0.01);
            host.Width = 200;
            Dispatcher.UIThread.RunJobs();
            row.Bounds.Width.ShouldBe(200, 0.01);
            Slot(otp, 0).Bounds.Width.ShouldBe((200 - 30) / 6.0, 1);
            Slot(otp, 5).Bounds.Right.ShouldBe(200, 1);
            host.Width = 400;
            Dispatcher.UIThread.RunJobs();
            otp.Classes.Contains(OtpInput.NarrowPseudoClass).ShouldBeFalse();
            row.Gap.ShouldBe(8);
            row.Bounds.Width.ShouldBe(292, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Length_Rebuilds_The_Slots()
    {
        var otp = new OtpInput { Value = "4821" };
        var window = Show(otp);
        try
        {
            otp.Length = 4;
            otp.Slots.Count.ShouldBe(4);
            otp.IsComplete.ShouldBeTrue();
            Shown(otp).ShouldBe("4|8|2|1");
            AutomationProperties.GetName(Slot(otp, 3)).ShouldBe("Verification code, digit 4 of 4");
            otp.Length = 0;
            otp.Length.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Auto_Focus_Focuses_The_First_Slot()
    {
        var otp = new OtpInput { Value = "48", AutoFocus = true };
        var window = Show(otp);
        try
        {
            FocusedIndex(otp).ShouldBe(0);
            otp.Overlay!.IsRingShown.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Ring_Follows_Focus_And_Leaves_With_It()
    {
        var otp = new OtpInput { Value = "482" };
        var other = new Button { Content = "Elsewhere" };
        var panel = new StackPanel { Children = { otp, other } };
        var window = Show(panel);
        try
        {
            var overlay = otp.Overlay!;
            overlay.IsRingShown.ShouldBeFalse();
            otp.FocusSlot(3);
            overlay.IsRingShown.ShouldBeTrue();
            overlay.RingState.X.ShouldBe(150, 0.01);
            overlay.RingState.Width.ShouldBe(42, 0.01);
            overlay.RingState.Opacity.ShouldBe(1, 0.001);
            Press(window, PhysicalKey.ArrowRight);
            overlay.RingState.X.ShouldBe(200, 0.01);
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            overlay.IsRingShown.ShouldBeFalse();
            overlay.RingState.Opacity.ShouldBe(0, 0.001);
            otp.ErrorMessage = "Wrong.";
            overlay.RingColor.ShouldBe(TestHelpers.ColorOf(overlay.RingInvalidBrush));
            otp.ErrorMessage = null;
            overlay.RingColor.ShouldBe(TestHelpers.ColorOf(overlay.RingBrush));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Ring_Glides_On_The_Snappy_Spring_And_Fades_In_From_Outside()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var otp = new OtpInput { Value = "482" };
        var window = Show(otp, level: ArcMotionLevel.Full);
        try
        {
            var overlay = otp.Overlay!;
            otp.FocusSlot(3);
            // Arriving from outside: no travel, a fade and a scale up from .94.
            overlay.RingState.X.ShouldBe(150, 0.01);
            overlay.RingState.Opacity.ShouldBe(0, 0.001);
            overlay.RingState.Scale.ShouldBe(0.94, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            overlay.RingState.Opacity.ShouldBeInRange(0.6, 0.85);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            overlay.RingState.Opacity.ShouldBe(1, 0.001);
            overlay.RingState.Scale.ShouldBe(1, 0.002);

            Press(window, PhysicalKey.ArrowRight);
            overlay.RingState.X.ShouldBe(150, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            // Critically damped, visualDuration .26: about 60% of the way after 100ms (measured on Arc: 60%).
            ((overlay.RingState.X - 150) / 50).ShouldBeInRange(0.5, 0.7);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            overlay.RingState.X.ShouldBe(200, 0.05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typed_Characters_Rise_In_And_A_Paste_Lands_As_A_Wave()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var otp = new OtpInput();
        var window = Show(otp, level: ArcMotionLevel.Full);
        try
        {
            var overlay = otp.Overlay!;
            otp.FocusSlot(0);
            Type(window, "4");
            overlay.GlyphStates[0].ShouldBe(("4", 0.0));
            Frames(clock, 7);
            // 117ms of 240 on the enter curve.
            overlay.GlyphStates[0].Progress.ShouldBeInRange(0.8, 0.99);
            Frames(clock, 12);
            overlay.GlyphStates[0].Progress.ShouldBe(1, 0.001);

            otp.ApplyPaste(0, "135790");
            // The "4" leaves (120ms) while the new characters wait 35ms per slot index.
            overlay.LeavingCount.ShouldBe(1);
            Frames(clock, 2);
            overlay.GlyphStates[0].Progress.ShouldBeGreaterThan(0);
            overlay.GlyphStates[1].Progress.ShouldBe(0, 1e-6);
            Frames(clock, 6);
            overlay.LeavingCount.ShouldBe(0);
            overlay.GlyphStates[5].Progress.ShouldBe(0, 1e-6);
            // Slot 6 starts at 175ms: after the eleventh frame (183ms), not the tenth (167ms).
            Frames(clock, 2);
            overlay.GlyphStates[5].Progress.ShouldBe(0, 1e-6);
            Frames(clock, 1);
            overlay.GlyphStates[5].Progress.ShouldBeGreaterThan(0);
            Frames(clock, 30);
            overlay.GlyphStates.ShouldAllBe(g => Math.Abs(g.Progress - 1) < 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Characters_And_Jumps_The_Ring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var otp = new OtpInput();
        var window = Show(otp, level: ArcMotionLevel.Reduced);
        try
        {
            var overlay = otp.Overlay!;
            otp.FocusSlot(0);
            overlay.RingState.Opacity.ShouldBe(1, 0.001);
            Type(window, "4");
            overlay.RingState.X.ShouldBe(50, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            overlay.GlyphStates[0].Progress.ShouldBeInRange(0.3, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            overlay.GlyphStates[0].Progress.ShouldBe(1, 0.001);
            Press(window, PhysicalKey.ArrowLeft);
            Press(window, PhysicalKey.Delete);
            overlay.LeavingCount.ShouldBe(0);
            otp.ErrorMessage = "Wrong.";
            clock.Advance(TimeSpan.FromMilliseconds(100));
            otp.ShakeOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Error_Shakes_The_Row_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var otp = new OtpInput();
        var window = Show(otp, level: ArcMotionLevel.Full);
        try
        {
            otp.ErrorMessage = "That code didn't match.";
            var offsets = new List<double>();
            for (var frame = 1; frame <= 24; frame++)
            {
                Frames(clock, 1);
                offsets.Add(otp.ShakeOffset);
                if (frame * FrameMs < 360)
                {
                    otp.ShakeOffset.ShouldBe(OtpInput.ShakeAt(frame * FrameMs / 360), 0.05);
                }
            }
            offsets.Min().ShouldBeLessThan(-5.9);
            offsets.Max().ShouldBeGreaterThan(4.8);
            offsets[^1].ShouldBe(0, 1e-6);
            // Clearing the error does not shake; the same error after a clear does.
            otp.ErrorMessage = null;
            Frames(clock, 4);
            otp.ShakeOffset.ShouldBe(0, 1e-6);
            otp.ErrorMessage = "That code didn't match.";
            Frames(clock, 4);
            otp.ShakeOffset.ShouldBe(-6, 0.05);
            // A different error shakes again; the same one set twice does not.
            Frames(clock, 30);
            otp.ErrorMessage = "Expired.";
            Frames(clock, 4);
            otp.ShakeOffset.ShouldBe(-6, 0.05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Selects_A_Filled_Slot_So_Typing_Replaces_It()
    {
        var otp = new OtpInput { Value = "482" };
        var window = Show(otp);
        try
        {
            otp.FocusSlot(0);
            Press(window, PhysicalKey.Tab);
            FocusedIndex(otp).ShouldBe(1);
            Slot(otp, 1).HasSelection.ShouldBeTrue();
            Type(window, "5");
            otp.Value.ShouldBe("452");
            FocusedIndex(otp).ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Several_Characters_At_Once_Spread_Like_A_Paste()
    {
        // An input method or SMS autofill commits the whole code into one slot.
        var otp = new OtpInput();
        var completed = new List<string>();
        otp.Completed += (_, e) => completed.Add(e.Code);
        var window = Show(otp);
        try
        {
            otp.FocusSlot(0);
            window.KeyTextInput("482913");
            Dispatcher.UIThread.RunJobs();
            otp.Value.ShouldBe("482913");
            FocusedIndex(otp).ShouldBe(5);
            completed.ShouldBe(["482913"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Field_That_Is_Not_Stretched_Is_As_Wide_As_Its_Widest_Part()
    {
        var otp = new OtpInput { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        var window = Show(otp, width: 800);
        try
        {
            otp.Bounds.Width.ShouldBe(292, 0.5);
            otp.Bounds.X.ShouldBe((800 - 292) / 2.0, 0.5);
            var stretched = new OtpInput();
            window.Content = stretched;
            Dispatcher.UIThread.RunJobs();
            stretched.Bounds.Width.ShouldBe(800, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cutting_A_Selected_Character_Clears_The_Slot()
    {
        var otp = new OtpInput { Value = "482" };
        var changes = new List<OtpInputValueChangedEventArgs>();
        otp.ValueChanged += (_, e) => changes.Add(e);
        var window = Show(otp);
        try
        {
            otp.FocusSlot(2);
            Slot(otp, 2).SelectAll();
            Press(window, PhysicalKey.X, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            otp.Value.ShouldBe("48");
            changes.Single().ChangeSource.ShouldBe(OtpInputChangeSource.Deletion);
            Shown(otp).ShouldBe("4|8||||");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Shake_Keyframes_Use_The_Standard_Curve_Per_Segment()
    {
        OtpInput.ShakeAt(0).ShouldBe(0);
        OtpInput.ShakeAt(0.2).ShouldBe(-6, 1e-9);
        OtpInput.ShakeAt(0.4).ShouldBe(5, 1e-9);
        OtpInput.ShakeAt(1).ShouldBe(0);
        // Arc's row was at -4.15 about 17ms into the shake (fitted start): the standard curve (-4.45), not linear (-1.4).
        OtpInput.ShakeAt(17 / 360.0).ShouldBe(-4.15, 0.4);
    }

    [Fact]
    public void Invalid_Fill_Mixes_Danger_Into_The_Surface()
    {
        var mixed = OtpInputToken.MixSrgb(Avalonia.Media.Color.FromRgb(200, 0, 0), Avalonia.Media.Colors.White, 0.07);
        mixed.ShouldBe(Avalonia.Media.Color.FromRgb(251, 237, 237));
    }
}
