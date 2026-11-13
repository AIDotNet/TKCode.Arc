using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>
/// The keycap keyboard: boards, scaling, typing by keys and clicks, the Shift latch, custom rows, the text area, keyboard
/// and automation, and the shortcut-viewer members kept from before 1.2.0.
/// </summary>
public class KeycapKeyboardTests
{
    private static KeycapKeyboard ShortcutKeyboard() => new()
    {
        Platform = KeycapPlatform.Windows,
        Bindings =
        {
            new KeycapBinding { Gesture = "Ctrl+C", Label = "Copy" },
            new KeycapBinding { Gesture = "Ctrl+Shift+P", Label = "Run" },
            new KeycapBinding { Gesture = "F1", Label = "Help" }
        }
    };

    private static double RowUnits(KeycapBoard board, int row)
        => board.Keys.Where(k => k.Row == row).Max(k => k.X + k.Width);

    [Fact]
    public void The_compact_board_is_a_sixty_key_board_fifteen_units_wide()
    {
        var board = KeycapKeyboardLayout.Board(KeycapLayout.Compact, mac: true, functionRow: false);
        board.Keys.Length.ShouldBe(60);
        board.Rows.ShouldBe(5);
        for (var row = 0; row < 5; row++)
        {
            RowUnits(board, row).ShouldBe(15, 1e-9);
        }
        board.Keys[board.AccentIndex].Kind.ShouldBe(KeycapKind.Enter);
        board.Keys.Where(k => k.Row == 4).Select(k => k.Label).ShouldBe(["ctrl", "opt", "cmd", "", "cmd", "opt", "ctrl"]);
        board.Keys.Where(k => k.IsCorner).Select(k => k.AccessibleName).Distinct()
            .ShouldBe(["Delete", "Tab", "Caps lock", "Return", "Shift", "Control", "Option", "Command"], ignoreOrder: true);

        var function = KeycapKeyboardLayout.Board(KeycapLayout.Compact, mac: false, functionRow: true);
        function.Rows.ShouldBe(6);
        RowUnits(function, 0).ShouldBe(15, 1e-9);
        function.Keys.Where(k => k.Row == 5).Select(k => k.Label).ShouldBe(["ctrl", "win", "alt", "", "alt", "win", "ctrl"]);
    }

    [Fact]
    public void The_letters_board_has_three_letter_rows_and_an_accent_space_bar()
    {
        var board = KeycapKeyboardLayout.Board(KeycapLayout.Letters, mac: true, functionRow: false);
        board.Keys.Length.ShouldBe(29);
        board.Rows.ShouldBe(4);
        board.Units.ShouldBe(10);
        RowUnits(board, 1).ShouldBe(9);
        RowUnits(board, 2).ShouldBe(10);
        board.Keys[board.AccentIndex].Kind.ShouldBe(KeycapKind.Space);
        board.Keys[board.AccentIndex].Width.ShouldBe(6);
    }

    [Fact]
    public void Custom_rows_read_characters_and_key_names()
    {
        var board = KeycapKeyboardLayout.Custom([KeycapKeyboardLayout.Tokens("k e y Backspace"), KeycapKeyboardLayout.Tokens("shift space enter")]);
        board.Keys.Select(k => k.Kind).ShouldBe(
            [KeycapKind.Character, KeycapKind.Character, KeycapKind.Character, KeycapKind.Backspace, KeycapKind.Shift, KeycapKind.Space, KeycapKind.Enter]);
        board.Keys[0].Physical.ShouldBe(PhysicalKey.K);
        board.Keys[board.AccentIndex].Kind.ShouldBe(KeycapKind.Enter);
        KeycapKeyboardLayout.Custom([KeycapKeyboardLayout.Tokens("a b space")]).Keys[2].Kind.ShouldBe(KeycapKind.Space);
        KeycapKeyboardLayout.Tokens("  a   ; banana , ").ShouldBe(["a", ";", "banana", ","]);
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
    public void Defaults_match_the_design()
    {
        var keyboard = new KeycapKeyboard();
        keyboard.Value.ShouldBe(string.Empty);
        keyboard.Layout.ShouldBe(KeycapLayout.Compact);
        keyboard.MaxLength.ShouldBe(280);
        keyboard.ShowsText.ShouldBeTrue();
        keyboard.Tracking.ShouldBe(KeycapTracking.WhenFocused);
        keyboard.Label.ShouldBe("Keyboard");
        keyboard.KeySize.ShouldBe(44);
        keyboard.Platform.ShouldBe(KeycapPlatform.Auto);
        keyboard.IsMac.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Every_measure_scales_from_one_key_unit()
    {
        var keyboard = new KeycapKeyboard { Width = 744 };
        var window = TestHelpers.Show(keyboard);
        try
        {
            // (744 − 2·16 − 2) / (15·1.14 − 0.14) ≈ 41.86 px.
            keyboard.Unit.ShouldBe(41.863, 0.01);
            var skin = keyboard.Skin!;
            skin.Gap.ShouldBe(keyboard.Unit * 0.14, 1e-9);
            skin.Travel.ShouldBe(keyboard.Unit * 0.085, 1e-9);
            skin.LegendSize.ShouldBe(keyboard.Unit * 0.3, 1e-9);
            var first = keyboard.KeyBounds(0);
            first.Width.ShouldBe(keyboard.Unit, 1e-6);
            first.Height.ShouldBe(keyboard.Unit * 1.085, 1e-6);
            var delete = keyboard.Keys.ToList().FindIndex(k => k.Kind == KeycapKind.Backspace);
            keyboard.KeyBounds(delete).Width.ShouldBe(2 * keyboard.Unit + skin.Gap, 1e-6);
            keyboard.KeyBounds(delete).Right.ShouldBe(keyboard.PlateBounds.Right - 1 - 16, 0.01);
            // Two lines of 32 px text at line height 1.3, plus 4 px, then 20 px to the plate.
            // (Layout rounding may round the text area by up to a pixel.)
            keyboard.TextArea.Bounds.Height.ShouldBe(2 * 32 * 1.3 + 4, 1);
            keyboard.PlateBounds.Top.ShouldBe(2 * 32 * 1.3 + 4 + 20, 1e-6);

            keyboard.Layout = KeycapLayout.Letters;
            Dispatcher.UIThread.RunJobs();
            keyboard.Unit.ShouldBe(54);
            keyboard.PlateBounds.Width.ShouldBeLessThan(744);
            keyboard.KeySize = 30;
            Dispatcher.UIThread.RunJobs();
            keyboard.Unit.ShouldBe(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_keys_shrink_to_fit_a_narrow_width()
    {
        var keyboard = ShortcutKeyboard();
        var window = TestHelpers.Show(keyboard);
        try
        {
            window.Width = 330;
            Dispatcher.UIThread.RunJobs();
            keyboard.Bounds.Width.ShouldBeLessThanOrEqualTo(330);
            var last = keyboard.KeyBounds(keyboard.Keys.Count - 1);
            last.Right.ShouldBeLessThanOrEqualTo(keyboard.Bounds.Width);
            keyboard.TextArea.TextSize.ShouldBe(20);
            var peer = ControlAutomationPeer.CreatePeerForElement(keyboard);
            peer.GetName().ShouldBe("Keyboard");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_with_focus_fills_the_text()
    {
        var keyboard = new KeycapKeyboard();
        var changes = new List<KeycapKeyboardValueChangedEventArgs>();
        keyboard.ValueChanged += (_, e) => changes.Add(e);
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.Focus();
            window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.H).ShouldBeTrue();
            window.KeyTextInput("h");
            window.KeyReleaseQwerty(PhysicalKey.H, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.H).ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.None);
            window.KeyTextInput("i");
            window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.None);
            keyboard.Value.ShouldBe("hi");
            changes.Count.ShouldBe(2);
            changes[1].OldValue.ShouldBe("h");
            changes[1].NewValue.ShouldBe("hi");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            keyboard.Value.ShouldBe("hi\n");
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            keyboard.Value.ShouldBe("h");

            // Shortcuts with Ctrl pass through: the key lights, nothing is typed.
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
            keyboard.IsDown(PhysicalKey.A).ShouldBeTrue();
            window.KeyTextInput("a");
            window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
            keyboard.Value.ShouldBe("h");
            keyboard.TextArea.Text.ShouldBe("h");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_longest_text_drops_its_oldest_characters()
    {
        var keyboard = new KeycapKeyboard { MaxLength = 5, Value = "abcdefgh" };
        keyboard.Value.ShouldBe("defgh");
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.Invoke(PhysicalKey.Z);
            keyboard.Value.ShouldBe("efghz");
            keyboard.MaxLength = 3;
            keyboard.Value.ShouldBe("ghz");
            keyboard.Clear();
            keyboard.Value.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicked_caps_type_and_a_clicked_shift_latches_for_one_character()
    {
        var keyboard = new KeycapKeyboard();
        var invoked = new List<KeycapKeyInvokedEventArgs>();
        keyboard.KeyInvoked += (_, e) => invoked.Add(e);
        var window = TestHelpers.Show(keyboard);
        try
        {
            window.Width = 800;
            window.Height = 700;
            Dispatcher.UIThread.RunJobs();
            void Click(PhysicalKey key)
            {
                var index = keyboard.Keys.ToList().FindIndex(k => k.Physical == key);
                var at = keyboard.TranslatePoint(keyboard.KeyBounds(index).Center, window)!.Value;
                window.MouseDown(at, MouseButton.Left);
                window.MouseUp(at, MouseButton.Left);
            }

            Click(PhysicalKey.A);
            keyboard.Value.ShouldBe("a");
            invoked.Single().KeyName.ShouldBe("A");

            Click(PhysicalKey.ShiftLeft);
            keyboard.IsDown(PhysicalKey.ShiftLeft).ShouldBeTrue();
            keyboard.IsDown(PhysicalKey.ShiftRight).ShouldBeTrue();
            keyboard.PressOf(PhysicalKey.ShiftRight).ShouldBe(KeycapKeyboardSkin.LatchDepth);
            Click(PhysicalKey.Backspace);
            keyboard.Value.ShouldBe(string.Empty);
            keyboard.IsDown(PhysicalKey.ShiftLeft).ShouldBeTrue();
            Click(PhysicalKey.Digit1);
            keyboard.Value.ShouldBe("!");
            keyboard.IsDown(PhysicalKey.ShiftLeft).ShouldBeFalse();
            Click(PhysicalKey.Slash);
            Click(PhysicalKey.Enter);
            Click(PhysicalKey.Space);
            keyboard.Value.ShouldBe("!/\n ");

            Click(PhysicalKey.ShiftRight);
            Click(PhysicalKey.ShiftRight);
            Click(PhysicalKey.Q);
            keyboard.Value.ShouldEndWith("q");

            keyboard.IsClickable = false;
            Click(PhysicalKey.C);
            keyboard.Value.ShouldEndWith("q");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_focused_key_is_a_button_pressed_with_enter_or_space()
    {
        var keyboard = new KeycapKeyboard();
        var window = TestHelpers.Show(keyboard);
        try
        {
            var key = keyboard.KeyVisuals.First(v => v.Entry.Physical == PhysicalKey.Backquote);
            key.IsTabStop.ShouldBeTrue();
            keyboard.KeyVisuals.Count(v => v.IsTabStop).ShouldBe(60);
            key.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyTextInput(" ");
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            keyboard.Value.ShouldBe("``");

            var peer = ControlAutomationPeer.CreatePeerForElement(keyboard.KeyVisuals.First(v => v.Entry.Kind == KeycapKind.Enter));
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetName().ShouldBe("Return");
            ((IInvokeProvider)peer).Invoke();
            keyboard.Value.ShouldBe("``\n");

            var text = ControlAutomationPeer.CreatePeerForElement(keyboard.TextArea);
            text.GetAutomationControlType().ShouldBe(AutomationControlType.Edit);
            text.GetName().ShouldBe(KeycapKeyboard.TextName);
            ((IValueProvider)text).IsReadOnly.ShouldBeTrue();
            ((IValueProvider)text).Value.ShouldBe("``\n");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_macro_pad_is_one_custom_row_without_the_text_area()
    {
        var keyboard = new KeycapKeyboard { ShowsText = false, Rows = { new KeycapRow("g o ! backspace") } };
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.Keys.Count.ShouldBe(4);
            keyboard.RowCount.ShouldBe(1);
            keyboard.TextArea.IsVisible.ShouldBeFalse();
            keyboard.PlateBounds.Top.ShouldBe(0);
            keyboard.DesiredSize.Height.ShouldBe(keyboard.PlateBounds.Height, 1);
            keyboard.AccentIndex.ShouldBe(-1);
            keyboard.Invoke(PhysicalKey.G);
            keyboard.Invoke(PhysicalKey.O);
            keyboard.Value.ShouldBe("go");
            keyboard.Invoke(PhysicalKey.Backspace);
            keyboard.Value.ShouldBe("g");

            keyboard.Rows[0].IsEnabled = false;
            keyboard.KeyVisuals.All(v => !v.IsTabStop).ShouldBeTrue();
            keyboard.Rows.Add(new KeycapRow("enter"));
            keyboard.RowCount.ShouldBe(2);
            keyboard.Keys[keyboard.AccentIndex].Kind.ShouldBe(KeycapKind.Enter);
            keyboard.Rows[1].IsVisible = false;
            keyboard.RowCount.ShouldBe(1);
            keyboard.Rows.Clear();
            keyboard.Keys.Count.ShouldBe(60);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Window_listening_lights_keys_meant_for_other_controls_but_types_nothing_into_them()
    {
        var keyboard = ShortcutKeyboard();
        keyboard.Tracking = KeycapTracking.Window;
        var box = new TextBox();
        var other = new Button { Content = "Other" };
        var window = TestHelpers.Show(new StackPanel { Children = { box, other, keyboard } });
        try
        {
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.K).ShouldBeTrue();
            window.KeyTextInput("k");
            window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.K).ShouldBeFalse();
            keyboard.Value.ShouldBe(string.Empty);

            other.Focus();
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
            window.KeyTextInput("k");
            window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.None);
            keyboard.Value.ShouldBe("k");

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
    public void Reduced_motion_removes_the_travel_but_keeps_the_light()
    {
        var keyboard = new KeycapKeyboard();
        var window = TestHelpers.Show(keyboard, ArcMotionLevel.Reduced);
        try
        {
            keyboard.Focus();
            window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
            keyboard.IsDown(PhysicalKey.J).ShouldBeTrue();
            keyboard.PressOf(PhysicalKey.J).ShouldBe(0);
            keyboard.GlowOf(PhysicalKey.J).ShouldBe(1);
            window.KeyTextInput("j");
            keyboard.TextArea.ArrivingCount.ShouldBe(0);
            keyboard.TextArea.IsCaretVisible.ShouldBeTrue();
            window.KeyReleaseQwerty(PhysicalKey.J, RawInputModifiers.None);
            keyboard.GlowOf(PhysicalKey.J).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    // --- the shortcut-viewer members --------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_shortcut_lights_its_key_and_modifiers_in_the_accent()
    {
        var keyboard = ShortcutKeyboard();
        keyboard.Shortcut = "Ctrl+Shift+P";
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.IsLit(PhysicalKey.P).ShouldBeTrue();
            keyboard.IsLit(PhysicalKey.ControlLeft).ShouldBeTrue();
            keyboard.IsLit(PhysicalKey.ShiftLeft).ShouldBeTrue();
            keyboard.IsLit(PhysicalKey.AltLeft).ShouldBeFalse();
            var p = keyboard.Keys.ToList().FindIndex(k => k.Physical == PhysicalKey.P);
            keyboard.IsAccentKey(p).ShouldBeTrue();
            keyboard.IsAccentKey(keyboard.AccentIndex).ShouldBeFalse();
            keyboard.Shortcut = null;
            keyboard.IsLit(PhysicalKey.P).ShouldBeFalse();
            keyboard.IsAccentKey(keyboard.AccentIndex).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bindings_show_while_exactly_their_modifiers_are_held()
    {
        var keyboard = ShortcutKeyboard();
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
    public void With_bindings_a_clicked_modifier_holds_until_the_next_key()
    {
        var keyboard = ShortcutKeyboard();
        var invoked = new List<KeycapKeyInvokedEventArgs>();
        keyboard.KeyInvoked += (_, e) => invoked.Add(e);
        var window = TestHelpers.Show(keyboard);
        try
        {
            window.Width = 800;
            window.Height = 700;
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
            keyboard.Value.ShouldBe(string.Empty);
            keyboard.ActiveModifiers.ShouldBe(KeyModifiers.None);
            keyboard.IsDown(PhysicalKey.ControlLeft).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Real_presses_play_the_keyboard_while_it_has_focus()
    {
        var keyboard = ShortcutKeyboard();
        var invoked = new List<KeycapKeyInvokedEventArgs>();
        keyboard.KeyInvoked += (_, e) => invoked.Add(e);
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.Focus();
            window.KeyPressQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Control);
            keyboard.ActiveModifiers.ShouldBe(KeyModifiers.Control);
            keyboard.PressOf(PhysicalKey.ControlLeft).ShouldBe(KeycapKeyboardSkin.PressDepth);
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
    public void Platform_picks_the_modifier_words_and_the_help_text()
    {
        var keyboard = ShortcutKeyboard();
        var window = TestHelpers.Show(keyboard);
        try
        {
            keyboard.Keys.Where(k => k.Row == 4).Take(3).Select(k => k.Label).ShouldBe(["ctrl", "win", "alt"]);
            keyboard.Platform = KeycapPlatform.Mac;
            keyboard.Keys.Where(k => k.Row == 4).Take(3).Select(k => k.Label).ShouldBe(["ctrl", "opt", "cmd"]);
            keyboard.Shortcut = "Cmd+K";
            var peer = ControlAutomationPeer.CreatePeerForElement(keyboard);
            peer.GetHelpText().ShouldBe("Shortcut Command+K.");
        }
        finally
        {
            window.Close();
        }
    }
}
