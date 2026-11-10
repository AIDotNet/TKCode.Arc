using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Widgets;

/// <summary>The keycap keyboard: the layouts, shortcut text, lit shortcuts, bindings, clicks, real presses and automation.</summary>
public class KeycapKeyboardTests
{
    private static KeycapKeyboard Keyboard() => new()
    {
        Platform = KeycapPlatform.Windows,
        Bindings =
        {
            new KeycapBinding { Gesture = "Ctrl+C", Label = "Copy" },
            new KeycapBinding { Gesture = "Ctrl+Shift+P", Label = "Run" },
            new KeycapBinding { Gesture = "F1", Label = "Help" }
        }
    };

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Every_row_is_fifteen_units_wide(bool mac, bool functionRow)
    {
        var keys = KeycapKeyboardLayout.Keys(mac, functionRow);
        foreach (var row in keys.GroupBy(k => Math.Floor(k.Y)))
        {
            // Up and down share one place; count the top half only.
            var width = row.Where(k => k.Physical != PhysicalKey.ArrowDown).Sum(k => k.Width);
            width.ShouldBe(KeycapKeyboardLayout.Columns, 1e-9);
        }
        keys.Select(k => k.Physical).Distinct().Count().ShouldBe(keys.Length);
    }

    [Theory]
    [InlineData("Ctrl+Shift+K", KeyModifiers.Control | KeyModifiers.Shift, PhysicalKey.K)]
    [InlineData("⇧⌘P", KeyModifiers.Shift | KeyModifiers.Meta, PhysicalKey.P)]
    [InlineData("Cmd+,", KeyModifiers.Meta, PhysicalKey.Comma)]
    [InlineData("Ctrl++", KeyModifiers.Control, PhysicalKey.Equal)]
    [InlineData("Alt+F4", KeyModifiers.Alt, PhysicalKey.F4)]
    [InlineData("option+up", KeyModifiers.Alt, PhysicalKey.ArrowUp)]
    [InlineData("Esc", KeyModifiers.None, PhysicalKey.Escape)]
    [InlineData("Shift", KeyModifiers.Shift, PhysicalKey.None)]
    public void Shortcut_text_is_read_in_any_common_spelling(string text, KeyModifiers modifiers, PhysicalKey key)
    {
        KeycapKeyboardLayout.TryParse(text, out var m, out var k).ShouldBeTrue();
        m.ShouldBe(modifiers);
        k.ShouldBe(key);
    }

    [Fact]
    public void Text_that_names_no_key_is_refused()
    {
        KeycapKeyboardLayout.TryParse("Ctrl+Banana", out _, out _).ShouldBeFalse();
        KeycapKeyboardLayout.TryParse("", out _, out _).ShouldBeFalse();
        KeycapKeyboardLayout.TryParse(null, out _, out _).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_shortcut_lights_its_key_and_modifiers()
    {
        var keyboard = Keyboard();
        keyboard.Shortcut = "Ctrl+Shift+P";
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.IsLit(PhysicalKey.P).ShouldBeTrue();
            keyboard.IsLit(PhysicalKey.ControlLeft).ShouldBeTrue();
            keyboard.IsLit(PhysicalKey.ShiftLeft).ShouldBeTrue();
            keyboard.IsLit(PhysicalKey.AltLeft).ShouldBeFalse();
            keyboard.Shortcut = null;
            keyboard.IsLit(PhysicalKey.P).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bindings_show_while_exactly_their_modifiers_are_held()
    {
        var keyboard = Keyboard();
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.BindingLabelOf(PhysicalKey.F1).ShouldBeNull();
            keyboard.BindingLabelOf(PhysicalKey.C).ShouldBeNull();
            keyboard.ActiveModifiers = KeyModifiers.Control;
            keyboard.BindingLabelOf(PhysicalKey.C).ShouldBe("Copy");
            keyboard.BindingLabelOf(PhysicalKey.P).ShouldBeNull();
            keyboard.ActiveModifiers = KeyModifiers.Control | KeyModifiers.Shift;
            keyboard.BindingLabelOf(PhysicalKey.C).ShouldBeNull();
            keyboard.BindingLabelOf(PhysicalKey.P).ShouldBe("Run");
            keyboard.ShowsFunctionRow = true;
            keyboard.ActiveModifiers = KeyModifiers.None;
            keyboard.BindingLabelOf(PhysicalKey.F1).ShouldBe("Help");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_clicked_modifier_holds_until_the_next_key()
    {
        var keyboard = Keyboard();
        var invoked = new List<KeycapKeyInvokedEventArgs>();
        keyboard.KeyInvoked += (_, e) => invoked.Add(e);
        var window = TestHelpers.Show(keyboard);
        try
        {
            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            void Click(PhysicalKey key)
            {
                var index = keyboard.Keys.ToList().FindIndex(k => k.Physical == key);
                var at = keyboard.TranslatePoint(keyboard.KeyBounds(index).Center, window)!.Value;
                window.MouseDown(at, MouseButton.Left);
                window.MouseUp(at, MouseButton.Left);
            }

            Click(PhysicalKey.ControlLeft);
            keyboard.ActiveModifiers.ShouldBe(KeyModifiers.Control);
            keyboard.BindingLabelOf(PhysicalKey.C).ShouldBe("Copy");
            keyboard.IsDown(PhysicalKey.ControlLeft).ShouldBeTrue();
            invoked.ShouldBeEmpty();

            Click(PhysicalKey.C);
            invoked.Count.ShouldBe(1);
            invoked[0].Key.ShouldBe(PhysicalKey.C);
            invoked[0].Modifiers.ShouldBe(KeyModifiers.Control);
            invoked[0].Binding!.Label.ShouldBe("Copy");
            keyboard.ActiveModifiers.ShouldBe(KeyModifiers.None);
            keyboard.IsDown(PhysicalKey.ControlLeft).ShouldBeFalse();

            keyboard.IsClickable = false;
            Click(PhysicalKey.C);
            invoked.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Real_presses_play_the_keyboard_while_it_has_focus()
    {
        var keyboard = Keyboard();
        var invoked = new List<KeycapKeyInvokedEventArgs>();
        keyboard.KeyInvoked += (_, e) => invoked.Add(e);
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.Focus();
            window.KeyPressQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Control);
            keyboard.ActiveModifiers.ShouldBe(KeyModifiers.Control);
            keyboard.PressOf(PhysicalKey.ControlLeft).ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);
            keyboard.IsDown(PhysicalKey.C).ShouldBeTrue();
            invoked.Single().Binding!.Label.ShouldBe("Copy");
            window.KeyReleaseQwerty(PhysicalKey.C, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.C).ShouldBeFalse();
            keyboard.PressOf(PhysicalKey.C).ShouldBe(0);
            keyboard.ActiveModifiers.ShouldBe(KeyModifiers.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Window_tracking_watches_presses_meant_for_other_controls()
    {
        var keyboard = Keyboard();
        keyboard.Tracking = KeycapTracking.Window;
        var box = new TextBox();
        var window = TestHelpers.Show(new StackPanel { Children = { box, keyboard } });
        try
        {
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.K).ShouldBeTrue();
            window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.K).ShouldBeFalse();
            keyboard.Tracking = KeycapTracking.None;
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.K).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mac_labels_and_order_on_the_bottom_row()
    {
        var keyboard = Keyboard();
        keyboard.Platform = KeycapPlatform.Mac;
        var window = TestHelpers.Show(keyboard);
        try
        {
            var bottom = keyboard.Keys.Where(k => k.Y == 4).Take(3).Select(k => k.Label).ToArray();
            bottom.ShouldBe(["⌃", "⌥", "⌘"]);
            keyboard.Shortcut = "Cmd+K";
            var peer = ControlAutomationPeer.CreatePeerForElement(keyboard);
            peer.GetHelpText().ShouldBe("Shortcut Command+K.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_keys_shrink_to_fit_a_narrow_width()
    {
        var keyboard = Keyboard();
        var window = TestHelpers.Show(keyboard);
        try
        {
            window.Width = 330;
            Dispatcher.UIThread.RunJobs();
            keyboard.Bounds.Width.ShouldBeLessThanOrEqualTo(330);
            var last = keyboard.KeyBounds(keyboard.Keys.Count - 1);
            last.Right.ShouldBeLessThanOrEqualTo(keyboard.Bounds.Width);
            var peer = ControlAutomationPeer.CreatePeerForElement(keyboard);
            peer.GetName().ShouldBe("Keyboard");
        }
        finally
        {
            window.Close();
        }
    }
}
