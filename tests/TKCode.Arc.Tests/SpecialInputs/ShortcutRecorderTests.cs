using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.SpecialInputs;

public class ShortcutRecorderTests
{
    private static KeyGesture G(Key key, KeyModifiers modifiers = KeyModifiers.None) => new(key, modifiers);

    // ---- formatter ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Format_Uses_Glyphs_On_Mac_And_Names_Elsewhere()
    {
        var mac = ShortcutFormatter.Format(G(Key.K, KeyModifiers.Meta | KeyModifiers.Shift), ShortcutPlatform.Mac);
        mac.Text.ShouldBe("⇧⌘K");
        mac.Spoken.ShouldBe("Shift Command K");
        var other = ShortcutFormatter.Format(G(Key.K, KeyModifiers.Control | KeyModifiers.Shift), ShortcutPlatform.Other);
        other.Text.ShouldBe("Ctrl+Shift+K");
        other.Spoken.ShouldBe("Control Shift K");
        ShortcutFormatter.Format(G(Key.K, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta), ShortcutPlatform.Mac)
            .Text.ShouldBe("⌃⌥⇧⌘K");
        ShortcutFormatter.Format(G(Key.K, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta), ShortcutPlatform.Other)
            .Spoken.ShouldBe("Control Alt Shift Windows K");
    }

    [Fact]
    public void Tokens_Name_Keys_Like_Arc()
    {
        var mac = ShortcutFormatter.Tokens(G(Key.Enter, KeyModifiers.Meta), ShortcutPlatform.Mac);
        mac.Select(t => t.Label).ShouldBe(new[] { "⌘", "↩" });
        mac[1].Spoken.ShouldBe("Return");
        mac[0].Id.ShouldBe("meta");
        ShortcutFormatter.Tokens(G(Key.Back), ShortcutPlatform.Mac)[0].ShouldBe(new ShortcutKeyCap("⌫", "backspace", "Delete"));
        ShortcutFormatter.Tokens(G(Key.Delete), ShortcutPlatform.Mac)[0].Spoken.ShouldBe("Forward delete");
        ShortcutFormatter.Tokens(G(Key.Escape), ShortcutPlatform.Mac)[0].Label.ShouldBe("esc");
        ShortcutFormatter.Tokens(G(Key.Escape), ShortcutPlatform.Other)[0].Label.ShouldBe("Esc");
        ShortcutFormatter.Tokens(G(Key.Delete), ShortcutPlatform.Other)[0].Label.ShouldBe("Del");
        ShortcutFormatter.Tokens(G(Key.PageDown), ShortcutPlatform.Other)[0].ShouldBe(new ShortcutKeyCap("PgDn", "pagedown", "Page down"));
        ShortcutFormatter.Tokens(G(Key.Up), ShortcutPlatform.Other)[0].ShouldBe(new ShortcutKeyCap("↑", "up", "Up arrow"));
        ShortcutFormatter.Tokens(G(Key.OemOpenBrackets), ShortcutPlatform.Other)[0].Spoken.ShouldBe("Left bracket");
        ShortcutFormatter.Tokens(G(Key.OemPlus), ShortcutPlatform.Other)[0].ShouldBe(new ShortcutKeyCap("=", "=", "Equals"));
        ShortcutFormatter.Tokens(G(Key.OemTilde), ShortcutPlatform.Other)[0].Spoken.ShouldBe("Backtick");
        ShortcutFormatter.Tokens(G(Key.F5), ShortcutPlatform.Other)[0].ShouldBe(new ShortcutKeyCap("F5", "f5", "F5"));
        ShortcutFormatter.Tokens(G(Key.NumPad3, KeyModifiers.Alt), ShortcutPlatform.Other)[1].ShouldBe(new ShortcutKeyCap("3", "3", "3"));
    }

    [Fact]
    public void Parse_Resolves_Mod_Per_Platform()
    {
        ShortcutFormatter.Parse("mod+shift+k", ShortcutPlatform.Mac).ShouldBe(G(Key.K, KeyModifiers.Meta | KeyModifiers.Shift));
        ShortcutFormatter.Parse("mod+shift+k", ShortcutPlatform.Other).ShouldBe(G(Key.K, KeyModifiers.Control | KeyModifiers.Shift));
        ShortcutFormatter.Parse("ctrl+alt+/", ShortcutPlatform.Mac).ShouldBe(G(Key.OemQuestion, KeyModifiers.Control | KeyModifiers.Alt));
        ShortcutFormatter.Parse("f12", ShortcutPlatform.Other).ShouldBe(G(Key.F12));
        ShortcutFormatter.Parse("cmd+=", ShortcutPlatform.Other).ShouldBe(G(Key.OemPlus, KeyModifiers.Meta));
        ShortcutFormatter.TryParse("mod+shift", ShortcutPlatform.Other, out _).ShouldBeFalse();
        ShortcutFormatter.TryParse("mod+nope", ShortcutPlatform.Other, out _).ShouldBeFalse();
        Should.Throw<FormatException>(() => ShortcutFormatter.Parse("", ShortcutPlatform.Mac));
    }

    [Fact]
    public void Physical_Keys_Map_To_Their_Keys()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            ShortcutFormatter.KeyFromPhysical(Enum.Parse<PhysicalKey>(c.ToString())).ShouldBe(Enum.Parse<Key>(c.ToString()));
        }
        for (var d = 0; d <= 9; d++)
        {
            ShortcutFormatter.KeyFromPhysical(Enum.Parse<PhysicalKey>($"Digit{d}")).ShouldBe(Enum.Parse<Key>($"D{d}"));
            ShortcutFormatter.KeyFromPhysical(Enum.Parse<PhysicalKey>($"NumPad{d}")).ShouldBe(Enum.Parse<Key>($"D{d}"));
        }
        for (var f = 1; f <= 24; f++)
        {
            ShortcutFormatter.KeyFromPhysical(Enum.Parse<PhysicalKey>($"F{f}")).ShouldBe(Enum.Parse<Key>($"F{f}"));
        }
        ShortcutFormatter.KeyFromPhysical(PhysicalKey.NumPadEnter).ShouldBe(Key.Enter);
        ShortcutFormatter.KeyFromPhysical(PhysicalKey.Backquote).ShouldBe(Key.OemTilde);
        ShortcutFormatter.KeyFromPhysical(PhysicalKey.CapsLock).ShouldBeNull();
        ShortcutFormatter.AreEquivalent(G(Key.NumPad1, KeyModifiers.Control), G(Key.D1, KeyModifiers.Control)).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Matches_Reads_The_Physical_Key_And_Exact_Modifiers()
    {
        var target = new Border { Focusable = true, Width = 100, Height = 40 };
        var results = new List<bool>();
        var gesture = G(Key.K, KeyModifiers.Alt);
        target.KeyDown += (_, e) => results.Add(ShortcutFormatter.Matches(e, gesture, ShortcutPlatform.Other));
        var window = TestHelpers.Show(target);
        try
        {
            target.Focus();
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Alt);
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Alt | RawInputModifiers.Shift);
            window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.Alt);
            results.ShouldBe(new[] { true, false, false });
        }
        finally
        {
            window.Close();
        }
    }

    // ---- recorder ----------------------------------------------------------------------------------------------------

    private sealed record Harness(ShortcutRecorder Recorder, ShortcutRecorderButton Button, Window Window, AvaloniaButton Other)
    {
        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        public void Release(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyReleaseQwerty(key, modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        public void Record(PhysicalKey key, RawInputModifiers modifiers)
        {
            Key(PhysicalKey.Enter);
            Recorder.IsRecording.ShouldBeTrue();
            Key(key, modifiers);
        }

        public ShortcutRecorderMessageSlot Message => TestHelpers.Descendant<ShortcutRecorderMessageSlot>(Recorder);

        public ShortcutRecorderActionButton Action(string name) => TestHelpers.Descendant<ShortcutRecorderActionButton>(Recorder, name);
    }

    private static Harness Open(Action<ShortcutRecorder>? setup = null, Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        var recorder = new ShortcutRecorder { Label = "Command palette", Platform = ShortcutPlatform.Other };
        setup?.Invoke(recorder);
        var other = new AvaloniaButton { Content = "Elsewhere" };
        var window = TestHelpers.Show(new StackPanel { Spacing = 8, Children = { recorder, other } }, level);
        var button = TestHelpers.Descendant<ShortcutRecorderButton>(recorder);
        button.Focus();
        Dispatcher.UIThread.RunJobs();
        return new Harness(recorder, button, window, other);
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var recorder = new ShortcutRecorder();
        recorder.Value.ShouldBeNull();
        recorder.ResetValue.ShouldBeNull();
        recorder.Placeholder.ShouldBe("Record shortcut");
        recorder.RecordingPlaceholder.ShouldBe("Press keys");
        recorder.RecordingHint.ShouldBe("Esc to cancel, Backspace to clear");
        recorder.UseAnywayText.ShouldBe("Use anyway");
        recorder.CancelText.ShouldBe("Cancel");
        recorder.ClearText.ShouldBe("Clear shortcut");
        recorder.IsReservedWarningEnabled.ShouldBeTrue();
        recorder.IsModifierRequired.ShouldBeTrue();
        recorder.IsLabelVisible.ShouldBeTrue();
        recorder.IsRecording.ShouldBeFalse();
        recorder.Bindings.ShouldBeEmpty();
        recorder.EffectivePlatform.ShouldBe(OperatingSystem.IsMacOS() ? ShortcutPlatform.Mac : ShortcutPlatform.Other);
    }

    [AvaloniaFact]
    public void Shows_The_Placeholder_And_The_Arc_Shell()
    {
        var h = Open();
        try
        {
            h.Button.PlaceholderText.ShouldBe("Record shortcut");
            h.Button.CapLabels.ShouldBeEmpty();
            var frame = TestHelpers.Descendant<FieldFrame>(h.Recorder);
            frame.Bounds.Height.ShouldBe(44);
            h.Button.Padding.ShouldBe(new Thickness(9, 0, 6, 0));
            h.Action(ShortcutRecorder.ClearButtonPart).IsVisible.ShouldBeFalse();
            h.Action(ShortcutRecorder.ResetButtonPart).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(h.Button).ShouldBe("Command palette Record shortcut");
            // Focus without keyboard navigation does not darken the border (Arc answers :focus-visible only).
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.BorderStrong));
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Records_A_Chord_From_Physical_Keys()
    {
        var h = Open(r => r.Value = G(Key.K, KeyModifiers.Control));
        try
        {
            ShortcutRecorderValueChangedEventArgs? args = null;
            h.Recorder.ValueChanged += (_, e) => args = e;
            h.Key(PhysicalKey.Enter);
            h.Recorder.IsRecording.ShouldBeTrue();
            h.Recorder.Classes.ShouldContain(ShortcutRecorder.RecordingPseudoClass);
            h.Recorder.LastAnnouncement.ShouldBe("Recording. Press the new shortcut, or Escape to cancel.");
            h.Button.PlaceholderText.ShouldBe("Press keys");
            h.Recorder.MessageText.ShouldBe("Esc to cancel, Backspace to clear");
            AutomationProperties.GetName(h.Button).ShouldBe("Command palette Recording");
            var peer = ControlAutomationPeer.CreatePeerForElement(h.Button);
            ((IToggleProvider)peer).ToggleState.ShouldBe(ToggleState.On);

            h.Key(PhysicalKey.J, RawInputModifiers.Control | RawInputModifiers.Shift);
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.Value.ShouldBe(G(Key.J, KeyModifiers.Control | KeyModifiers.Shift));
            h.Button.CapLabels.ShouldBe(new[] { "Ctrl", "Shift", "J" });
            h.Recorder.LastAnnouncement.ShouldBe("Shortcut set to Control Shift J");
            AutomationProperties.GetName(h.Button).ShouldBe("Command palette Control Shift J");
            ((IToggleProvider)peer).ToggleState.ShouldBe(ToggleState.Off);
            args.ShouldNotBeNull();
            args!.OldValue.ShouldBe(G(Key.K, KeyModifiers.Control));
            args.NewValue.ShouldBe(G(Key.J, KeyModifiers.Control | KeyModifiers.Shift));
            args.Replaced.ShouldBeNull();
            h.Recorder.MessageText.ShouldBeNull();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Held_Modifiers_Show_As_Live_Caps()
    {
        var h = Open(r => r.Platform = ShortcutPlatform.Mac);
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.ShiftLeft);
            h.Button.CapLabels.ShouldBe(new[] { "⇧" });
            h.Key(PhysicalKey.MetaLeft, RawInputModifiers.Shift);
            h.Button.CapLabels.ShouldBe(new[] { "⇧", "⌘" });
            h.Release(PhysicalKey.ShiftLeft, RawInputModifiers.Shift | RawInputModifiers.Meta);
            h.Button.CapLabels.ShouldBe(new[] { "⌘" });
            h.Release(PhysicalKey.MetaLeft, RawInputModifiers.Meta);
            h.Button.CapLabels.ShouldBeEmpty();
            h.Button.PlaceholderText.ShouldBe("Press keys");
            h.Recorder.IsRecording.ShouldBeTrue();
            // Caps Lock never makes a shortcut.
            h.Key(PhysicalKey.CapsLock);
            h.Recorder.IsRecording.ShouldBeTrue();
            h.Key(PhysicalKey.K, RawInputModifiers.Meta | RawInputModifiers.Alt);
            h.Recorder.Value.ShouldBe(G(Key.K, KeyModifiers.Meta | KeyModifiers.Alt));
            h.Button.CapLabels.ShouldBe(new[] { "⌥", "⌘", "K" });
            AutomationProperties.GetName(h.Button).ShouldBe("Command palette Option Command K");
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bare_Key_Asks_For_A_Modifier_Unless_It_Is_A_Function_Key()
    {
        var h = Open();
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.K);
            h.Recorder.IsRecording.ShouldBeTrue();
            h.Recorder.Value.ShouldBeNull();
            h.Recorder.MessageText.ShouldBe("Include Ctrl or Alt");
            h.Recorder.LastAnnouncement.ShouldBe("Include Ctrl or Alt");
            h.Key(PhysicalKey.K, RawInputModifiers.Shift);
            h.Recorder.IsRecording.ShouldBeTrue();
            h.Key(PhysicalKey.F5);
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.Value.ShouldBe(G(Key.F5));
            h.Recorder.MessageText.ShouldBeNull();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Mac_Nudge_Names_The_Mac_Modifiers()
    {
        var h = Open(r => r.Platform = ShortcutPlatform.Mac);
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.Digit1, RawInputModifiers.Shift);
            h.Recorder.MessageText.ShouldBe("Include ⌘, ⌃, or ⌥");
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Keys_Record_When_No_Modifier_Is_Required()
    {
        var h = Open(r => r.IsModifierRequired = false);
        try
        {
            h.Record(PhysicalKey.J, RawInputModifiers.None);
            h.Recorder.Value.ShouldBe(G(Key.J));
            h.Recorder.IsRecording.ShouldBeFalse();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Cancels_And_Backspace_Clears()
    {
        var h = Open(r => r.Value = G(Key.K, KeyModifiers.Control));
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.Escape);
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.Value.ShouldBe(G(Key.K, KeyModifiers.Control));
            h.Recorder.LastAnnouncement.ShouldBe("Recording canceled");
            h.Button.CapLabels.ShouldBe(new[] { "Ctrl", "K" });

            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.Backspace);
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.Value.ShouldBeNull();
            h.Recorder.LastAnnouncement.ShouldBe("Shortcut cleared");
            h.Button.PlaceholderText.ShouldBe("Record shortcut");
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_And_Delete_Clear_When_Idle()
    {
        var h = Open(r => r.Value = G(Key.K, KeyModifiers.Control));
        try
        {
            h.Key(PhysicalKey.Delete);
            h.Recorder.Value.ShouldBeNull();
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.LastAnnouncement.ShouldBe("Shortcut cleared");
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Stops_Recording_Without_Recording_Tab()
    {
        var h = Open(r => r.Value = G(Key.K, KeyModifiers.Control));
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.Tab);
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.Value.ShouldBe(G(Key.K, KeyModifiers.Control));
            h.Button.IsFocused.ShouldBeFalse();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Losing_Focus_Stops_Recording_And_Clears_The_Nudge()
    {
        var h = Open();
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.K);
            h.Recorder.MessageText.ShouldBe("Include Ctrl or Alt");
            h.Other.Focus();
            Dispatcher.UIThread.RunJobs();
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.MessageText.ShouldBeNull();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Chords_While_Recording_Do_Not_Reach_Window_Handlers()
    {
        var h = Open();
        try
        {
            var seen = 0;
            h.Window.KeyDown += (_, _) => seen++;
            h.Key(PhysicalKey.Enter);
            h.Key(PhysicalKey.K, RawInputModifiers.Control);
            seen.ShouldBe(0);
            h.Recorder.Value.ShouldBe(G(Key.K, KeyModifiers.Control));
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Taken_Shortcut_Asks_Before_It_Is_Used()
    {
        var save = new ShortcutBinding(G(Key.S, KeyModifiers.Control), "Save");
        var h = Open(r =>
        {
            r.Value = G(Key.K, KeyModifiers.Control);
            r.Bindings.Add(save);
            r.Bindings.Add(new ShortcutBinding(G(Key.F, KeyModifiers.Control), "Find"));
        });
        try
        {
            ShortcutRecorderValueChangedEventArgs? args = null;
            h.Recorder.ValueChanged += (_, e) => args = e;
            h.Record(PhysicalKey.S, RawInputModifiers.Control);
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.Value.ShouldBe(G(Key.K, KeyModifiers.Control));
            h.Recorder.Classes.ShouldContain(ShortcutRecorder.ConflictPseudoClass);
            h.Recorder.MessageText.ShouldBe("Already used by Save");
            h.Recorder.LastAnnouncement.ShouldBe("Control S is used by Save. Use anyway, or record another.");
            h.Button.CapLabels.ShouldBe(new[] { "Ctrl", "S" });
            AutomationProperties.GetName(h.Button).ShouldBe("Command palette Control S");
            AutomationProperties.GetHelpText(h.Button).ShouldBe("Already used by Save");
            // During a conflict the reset button offers the way back and clear is hidden.
            h.Action(ShortcutRecorder.ClearButtonPart).IsShown.ShouldBeFalse();
            h.Action(ShortcutRecorder.ResetButtonPart).IsShown.ShouldBeTrue();
            var frame = TestHelpers.Descendant<FieldFrame>(h.Recorder);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.Warning));

            var links = h.Message.Links!.Value;
            links.UseAnyway.Text.ShouldBe("Use anyway");
            links.Cancel.Text.ShouldBe("Cancel");
            links.UseAnyway.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Value.ShouldBe(G(Key.S, KeyModifiers.Control));
            args.ShouldNotBeNull();
            args!.Replaced.ShouldBeSameAs(save);
            h.Recorder.LastAnnouncement.ShouldBe("Shortcut set to Control S");
            h.Recorder.Classes.ShouldNotContain(ShortcutRecorder.ConflictPseudoClass);
            h.Button.IsFocused.ShouldBeTrue();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_Keeps_The_Previous_Shortcut()
    {
        var h = Open(r =>
        {
            r.Value = G(Key.K, KeyModifiers.Control);
            r.Bindings.Add(new ShortcutBinding(G(Key.F, KeyModifiers.Control), "Find"));
        });
        try
        {
            var changes = 0;
            h.Recorder.ValueChanged += (_, _) => changes++;
            h.Record(PhysicalKey.F, RawInputModifiers.Control);
            h.Message.Links!.Value.Cancel.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Value.ShouldBe(G(Key.K, KeyModifiers.Control));
            h.Recorder.LastAnnouncement.ShouldBe("Kept the previous shortcut");
            h.Recorder.MessageText.ShouldBeNull();
            h.Button.CapLabels.ShouldBe(new[] { "Ctrl", "K" });
            changes.ShouldBe(0);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Reserved_Combinations_Warn_And_Report_No_Replaced_Binding()
    {
        var h = Open();
        try
        {
            ShortcutRecorderValueChangedEventArgs? args = null;
            h.Recorder.ValueChanged += (_, e) => args = e;
            h.Record(PhysicalKey.W, RawInputModifiers.Control);
            h.Recorder.MessageText.ShouldBe("Reserved for Close tab");
            h.Recorder.LastAnnouncement.ShouldBe("Control W is reserved for Close tab. Use anyway, or record another.");
            h.Message.Links!.Value.UseAnyway.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Value.ShouldBe(G(Key.W, KeyModifiers.Control));
            args!.Replaced.ShouldBeNull();

            h.Recorder.IsReservedWarningEnabled = false;
            h.Record(PhysicalKey.C, RawInputModifiers.Control);
            h.Recorder.Value.ShouldBe(G(Key.C, KeyModifiers.Control));
            h.Recorder.MessageText.ShouldBeNull();

            // Reserved list follows the platform's "mod": ⌘T on a Mac, Ctrl+Shift+T reopens a tab elsewhere.
            ShortcutRecorder.ReservedBindings(ShortcutPlatform.Mac).ShouldContain(b => b.Gesture!.Equals(G(Key.T, KeyModifiers.Meta)));
            ShortcutRecorder.ReservedBindings(ShortcutPlatform.Other)
                .ShouldContain(b => b.Label == "Reopen tab" && b.Gesture!.Equals(G(Key.T, KeyModifiers.Control | KeyModifiers.Shift)));
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Recording_The_Current_Shortcut_Is_Not_A_Conflict()
    {
        var h = Open(r =>
        {
            r.Value = G(Key.S, KeyModifiers.Control);
            r.Bindings.Add(new ShortcutBinding(G(Key.S, KeyModifiers.Control), "Save"));
        });
        try
        {
            h.Record(PhysicalKey.S, RawInputModifiers.Control);
            h.Recorder.MessageText.ShouldBeNull();
            h.Recorder.Value.ShouldBe(G(Key.S, KeyModifiers.Control));
            h.Recorder.LastAnnouncement.ShouldBe("Shortcut set to Control S");
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_And_Clear_Follow_The_Value()
    {
        var defaultValue = G(Key.K, KeyModifiers.Control);
        var h = Open(r =>
        {
            r.Value = defaultValue;
            r.ResetValue = defaultValue;
        });
        try
        {
            var reset = h.Action(ShortcutRecorder.ResetButtonPart);
            var clear = h.Action(ShortcutRecorder.ClearButtonPart);
            reset.IsShown.ShouldBeFalse();
            clear.IsShown.ShouldBeTrue();
            clear.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(clear).ShouldBe("Clear shortcut");
            AutomationProperties.GetName(reset).ShouldBe("Reset to Control K");

            h.Record(PhysicalKey.J, RawInputModifiers.Alt);
            reset.IsShown.ShouldBeTrue();
            clear.IsShown.ShouldBeTrue();

            reset.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Value.ShouldBe(defaultValue);
            reset.IsShown.ShouldBeFalse();
            reset.IsVisible.ShouldBeFalse();
            h.Button.IsFocused.ShouldBeTrue();

            clear.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Value.ShouldBeNull();
            clear.IsShown.ShouldBeFalse();
            reset.IsShown.ShouldBeTrue();
            h.Recorder.LastAnnouncement.ShouldBe("Shortcut cleared");

            // While recording neither action shows.
            h.Key(PhysicalKey.Enter);
            reset.IsShown.ShouldBeFalse();
            clear.IsShown.ShouldBeFalse();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Programmatic_Values_Update_The_Caps_Quietly()
    {
        var h = Open();
        try
        {
            var events = new List<ShortcutRecorderValueChangedEventArgs>();
            h.Recorder.ValueChanged += (_, e) => events.Add(e);
            h.Recorder.Value = G(Key.P, KeyModifiers.Control | KeyModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            h.Button.CapLabels.ShouldBe(new[] { "Ctrl", "Shift", "P" });
            h.Recorder.LastAnnouncement.ShouldBe(string.Empty);
            events.Count.ShouldBe(1);
            h.Recorder.Value = G(Key.P, KeyModifiers.Control | KeyModifiers.Shift);
            events.Count.ShouldBe(1);
            h.Recorder.Platform = ShortcutPlatform.Mac;
            Dispatcher.UIThread.RunJobs();
            h.Button.CapLabels.ShouldBe(new[] { "⌃", "⇧", "P" });
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Click_Starts_Recording_And_A_Second_Click_Cancels()
    {
        var h = Open();
        try
        {
            h.Button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            h.Recorder.IsRecording.ShouldBeTrue();
            h.Button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.LastAnnouncement.ShouldBe("Recording canceled");

            var peer = (IToggleProvider)ControlAutomationPeer.CreatePeerForElement(h.Button);
            peer.Toggle();
            h.Recorder.IsRecording.ShouldBeTrue();
            h.Recorder.StopRecording();
            h.Recorder.IsRecording.ShouldBeFalse();
            h.Recorder.StartRecording();
            h.Recorder.IsRecording.ShouldBeTrue();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Recorders_Do_Not_Record()
    {
        var h = Open(r =>
        {
            r.Value = G(Key.K, KeyModifiers.Control);
            r.IsEnabled = false;
        });
        try
        {
            h.Recorder.StartRecording();
            h.Recorder.IsRecording.ShouldBeFalse();
            TestHelpers.Descendant<FieldFrame>(h.Recorder).Opacity.ShouldBe(0.5);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_And_Recording_Set_The_Border()
    {
        var h = Open();
        try
        {
            var frame = TestHelpers.Descendant<FieldFrame>(h.Recorder);
            h.Other.Focus();
            h.Button.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Classes.ShouldContain(ShortcutRecorder.RecorderFocusVisiblePseudoClass);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.Foreground));
            // Arc's cascade: the focus-visible rule (0,3,0) outranks the recording border (0,2,0); the tint still shows.
            h.Key(PhysicalKey.Enter);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.Foreground));
            h.Key(PhysicalKey.Escape);

            // With pointer focus the recording border is the accent, and the field takes the accent tint.
            h.Other.Focus();
            h.Button.Focus(NavigationMethod.Pointer);
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Classes.ShouldNotContain(ShortcutRecorder.RecorderFocusVisiblePseudoClass);
            h.Key(PhysicalKey.Enter);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.Accent));
            TestHelpers.Descendant<Border>(h.Recorder, "PART_Tint").Opacity.ShouldBe(0.04, 0.0001);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Mouse_Hover_Darkens_The_Border_Only_While_Idle()
    {
        var h = Open();
        try
        {
            var frame = TestHelpers.Descendant<FieldFrame>(h.Recorder);
            var center = frame.TranslatePoint(new Point(frame.Bounds.Width / 2, frame.Bounds.Height / 2), h.Window)!.Value;
            h.Window.MouseMove(center);
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Classes.ShouldContain(ShortcutRecorder.HoverPseudoClass);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.Foreground));
            h.Key(PhysicalKey.Enter);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.Accent));
            h.Key(PhysicalKey.Escape);
            h.Window.MouseMove(new Point(1, 299));
            Dispatcher.UIThread.RunJobs();
            h.Recorder.Classes.ShouldNotContain(ShortcutRecorder.HoverPseudoClass);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(h.Recorder, ArcDesignTokenKind.BorderStrong));
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Label_Still_Names_The_Recorder()
    {
        var h = Open(r =>
        {
            r.IsLabelVisible = false;
            r.Description = "Opens the palette.";
        });
        try
        {
            var label = TestHelpers.Descendant<TextBlock>(h.Recorder, "PART_Label");
            label.IsEffectivelyVisible.ShouldBeFalse();
            AutomationProperties.GetName(h.Button).ShouldBe("Command palette Record shortcut");
            h.Recorder.MessageText.ShouldBe("Opens the palette.");
            AutomationProperties.GetHelpText(h.Button).ShouldBe("Opens the palette.");
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Animates_Without_Errors_And_Breathes_Only_While_Recording()
    {
        var h = Open(r => r.Value = G(Key.K, KeyModifiers.Control), Motion.ArcMotionLevel.Full);
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Button.IsBreathing.ShouldBeTrue();
            h.Key(PhysicalKey.ControlLeft);
            h.Key(PhysicalKey.J, RawInputModifiers.Control);
            for (var i = 0; i < 5; i++)
            {
                Dispatcher.UIThread.RunJobs();
            }
            h.Button.IsBreathing.ShouldBeFalse();
            h.Recorder.Value.ShouldBe(G(Key.J, KeyModifiers.Control));
            h.Button.CapLabels.ShouldBe(new[] { "Ctrl", "J" });
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Keeps_The_Light_Steady()
    {
        var h = Open(null, Motion.ArcMotionLevel.Reduced);
        try
        {
            h.Key(PhysicalKey.Enter);
            h.Recorder.IsRecording.ShouldBeTrue();
            h.Button.IsBreathing.ShouldBeFalse();
        }
        finally
        {
            h.Window.Close();
        }
    }
}
