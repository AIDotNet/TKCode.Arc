using System.Globalization;
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

namespace TKCode.Arc.Tests.SpecialInputs;

public class OnScreenKeyboardTests
{
    private static Window Show(Control content, double width = 760, double height = 560)
    {
        var window = TestHelpers.Show(content);
        window.Width = width;
        window.Height = height;
        Pump();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static OnScreenKeyboardKeyPresenter KeyNamed(OnScreenKeyboard keyboard, string name) =>
        keyboard.KeyPresenters.First(p => AutomationProperties.GetName(p) == name);

    private static OnScreenKeyboardKey ModelKey(OnScreenKeyboard keyboard, string name) => KeyNamed(keyboard, name).Key!;

    private static void Tap(OnScreenKeyboard keyboard, string name) => keyboard.Invoke(ModelKey(keyboard, name));

    private static void Click(Window window, Visual target) => FocusRingInput.Click(window, target);

    /// <summary>A keyboard under a text box, both inside a focusable panel: a press that fell through to the panel would focus it.</summary>
    private static (Window Window, OnScreenKeyboard Keyboard, TextBox Box, Panel Host) TextBoxAndKeyboard(TextBox? box = null, OnScreenKeyboard? keyboard = null)
    {
        box ??= new TextBox();
        keyboard ??= new OnScreenKeyboard();
        var host = new StackPanel { Focusable = true, Spacing = 12, Children = { box, keyboard } };
        var window = Show(host);
        box.Focus();
        Pump();
        return (window, keyboard, box, host);
    }

    [AvaloniaFact]
    public void Qwerty_Is_The_Default_Layout()
    {
        var keyboard = new OnScreenKeyboard();
        var window = Show(keyboard);
        try
        {
            keyboard.Preset.ShouldBe(OnScreenKeyboardPreset.Qwerty);
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Off);
            var keys = keyboard.KeyPresenters;
            keys.Select(k => k.Row).Distinct().Count().ShouldBe(4);
            string.Concat(keys.Where(k => k.Row == 0).Select(k => k.Label)).ShouldBe("qwertyuiop");
            string.Concat(keys.Where(k => k.Row == 1).Select(k => k.Label)).ShouldBe("asdfghjkl");
            keys.Where(k => k.Row == 2).Select(k => AutomationProperties.GetName(k)).ShouldBe(["Shift", "z", "x", "c", "v", "b", "n", "m", "Backspace"]);
            keys.Where(k => k.Row == 3).Select(k => AutomationProperties.GetName(k)).ShouldBe(["Numbers and symbols", ",", "Space", ".", "Enter"]);

            // The home row is centred: half a key in on both sides.
            var q = KeyNamed(keyboard, "q").Bounds;
            var w = KeyNamed(keyboard, "w").Bounds;
            var a = KeyNamed(keyboard, "a").Bounds;
            var unit = w.X - q.X;
            (a.X - q.X).ShouldBe(unit / 2, 1);
            // Shift and Backspace are 1.5 keys wide; the space bar 5.
            KeyNamed(keyboard, "Shift").Bounds.Width.ShouldBe(unit * 1.5 - 6, 1.5);
            KeyNamed(keyboard, "Space").Bounds.Width.ShouldBe(unit * 5 - 6, 1.5);
            // Glyph keys draw a glyph, character keys a label.
            KeyNamed(keyboard, "Backspace").Glyph.ShouldNotBeNull();
            KeyNamed(keyboard, "Backspace").Classes.ShouldContain(":function");
            KeyNamed(keyboard, "Enter").Classes.ShouldContain(":primary");
            KeyNamed(keyboard, "Numbers and symbols").Classes.ShouldContain(":word");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Natural_Size_Comes_From_The_Tokens_And_Rows_Fill_A_Given_Height()
    {
        var keyboard = new OnScreenKeyboard { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = Show(keyboard, 900, 700);
        try
        {
            // 10 units × 52 − 6, plus 8 padding and a 1px edge on each side; 4 rows × 46 + 3 gaps × 8, plus 18.
            keyboard.Bounds.Width.ShouldBe(10 * 52 - 6 + 18, 0.5);
            keyboard.Bounds.Height.ShouldBe(4 * 46 + 3 * 8 + 18, 0.5);
            KeyNamed(keyboard, "q").Bounds.Height.ShouldBe(46, 0.5);

            keyboard.Height = 418;
            Pump();
            // (418 − 18 − 24) / 4 rows.
            KeyNamed(keyboard, "q").Bounds.Height.ShouldBe(94, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Number_Pads_Follow_The_Culture()
    {
        var keyboard = new OnScreenKeyboard { Preset = OnScreenKeyboardPreset.Numeric, Culture = CultureInfo.GetCultureInfo("de-DE") };
        var window = Show(keyboard);
        try
        {
            keyboard.KeyPresenters.Select(k => AutomationProperties.GetName(k)).ShouldBe(
                ["1", "2", "3", "Backspace", "4", "5", "6", "Left", "7", "8", "9", "Right", "0", "Decimal separator", "Enter"]);
            KeyNamed(keyboard, "Decimal separator").Label.ShouldBe(",");
            keyboard.Culture = CultureInfo.GetCultureInfo("en-US");
            Pump();
            KeyNamed(keyboard, "Decimal separator").Label.ShouldBe(".");
            // 0 is two keys wide.
            KeyNamed(keyboard, "0").Bounds.Width.ShouldBeGreaterThan(KeyNamed(keyboard, "1").Bounds.Width * 1.9);

            keyboard.Preset = OnScreenKeyboardPreset.SignedNumeric;
            Pump();
            var minus = ModelKey(keyboard, "Minus");
            keyboard.GetOutputText(minus).ShouldBe("-");
            KeyNamed(keyboard, "Minus").Label.ShouldBe("−");

            keyboard.Preset = OnScreenKeyboardPreset.Digits;
            Pump();
            keyboard.KeyPresenters.Select(k => AutomationProperties.GetName(k)).ShouldBe(
                ["1", "2", "3", "4", "5", "6", "7", "8", "9", "Backspace", "0", "Enter"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Types_Into_A_Focused_TextBox_At_The_Caret()
    {
        var (window, keyboard, box, _) = TextBoxAndKeyboard(new TextBox { Text = "ac" });
        try
        {
            box.CaretIndex = 1;
            Click(window, KeyNamed(keyboard, "b"));
            box.Text.ShouldBe("abc");
            box.CaretIndex.ShouldBe(2);
            box.IsFocused.ShouldBeTrue();

            Click(window, KeyNamed(keyboard, "Space"));
            box.Text.ShouldBe("ab c");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replaces_The_Selection_Of_An_Arc_Input()
    {
        var input = new Input { Text = "hello world" };
        var keyboard = new OnScreenKeyboard();
        var window = Show(new StackPanel { Children = { input, keyboard } });
        try
        {
            input.Focus();
            input.SelectionStart = 6;
            input.SelectionEnd = 11;
            Pump();
            Click(window, KeyNamed(keyboard, "x"));
            input.Text.ShouldBe("hello x");
            input.CaretIndex.ShouldBe(7);
            input.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Presses_Never_Take_Focus_From_The_Text_Box()
    {
        var (window, keyboard, box, host) = TextBoxAndKeyboard();
        try
        {
            // Mouse: focus moves on press.
            Click(window, KeyNamed(keyboard, "q"));
            box.IsFocused.ShouldBeTrue();
            host.IsFocused.ShouldBeFalse();

            // Touch: focus moves on release.
            var point = FocusRingInput.Center(KeyNamed(keyboard, "w"), window);
            var touch = window.TouchBegin(point);
            window.TouchEnd(touch, point);
            Pump();
            box.IsFocused.ShouldBeTrue();

            // The gap between two keys presses the nearer key, and still keeps focus.
            var e = KeyNamed(keyboard, "e").Bounds;
            var gap = keyboard.KeyPresenters[0].GetVisualParent()!.TranslatePoint(new Point(e.Right + 2, e.Center.Y), window)!.Value;
            window.MouseMove(gap);
            window.MouseDown(gap, MouseButton.Left);
            window.MouseUp(gap, MouseButton.Left);
            Pump();
            box.IsFocused.ShouldBeTrue();
            box.Text.ShouldBe("qwe");
            KeyNamed(keyboard, "e").Focusable.ShouldBeFalse();
            keyboard.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_Capitalises_One_Key()
    {
        var (window, keyboard, box, _) = TextBoxAndKeyboard();
        try
        {
            Tap(keyboard, "Shift");
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.On);
            keyboard.Classes.ShouldContain(":shift");
            KeyNamed(keyboard, "Shift").Classes.ShouldContain(":active");
            KeyNamed(keyboard, "Shift").IsGlyphFilled.ShouldBeTrue();
            // Labels follow Shift.
            keyboard.KeyPresenters[0].Label.ShouldBe("Q");

            Tap(keyboard, "A");
            Tap(keyboard, "b");
            box.Text.ShouldBe("Ab");
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Off);
            keyboard.KeyPresenters[0].Label.ShouldBe("q");

            // A second tap turns it off again (slowly, so it is not a double tap).
            var clock = new TestClock();
            keyboard.TimeProvider = clock;
            Tap(keyboard, "Shift");
            clock.Advance(TimeSpan.FromSeconds(1));
            Tap(keyboard, "Shift");
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Off);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Double_Tap_On_Shift_Locks_Capitals()
    {
        var clock = new TestClock();
        var (window, keyboard, box, _) = TextBoxAndKeyboard(keyboard: new OnScreenKeyboard { TimeProvider = clock });
        try
        {
            Tap(keyboard, "Shift");
            clock.Advance(TimeSpan.FromMilliseconds(150));
            Tap(keyboard, "Shift");
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Locked);
            keyboard.Classes.ShouldContain(":capslock");
            KeyNamed(keyboard, "Shift").Classes.ShouldContain(":locked");
            KeyNamed(keyboard, "Shift").Glyph.ShouldBeSameAs(OnScreenKeyboardGlyphs.CapsLock);

            Tap(keyboard, "A");
            Tap(keyboard, "B");
            box.Text.ShouldBe("AB");
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Locked);

            Tap(keyboard, "Shift");
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Off);
            Tap(keyboard, "c");
            box.Text.ShouldBe("ABc");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Caps_Lock_Capitalises_Letters_And_Shift_Uses_Shift_Text()
    {
        var one = new OnScreenKeyboardKey("1") { ShiftText = "!" };
        var eszett = new OnScreenKeyboardKey("ä");
        var layout = new OnScreenKeyboardLayout { Pages = { new OnScreenKeyboardPage("main", new OnScreenKeyboardRow(one, eszett)) } };
        var keyboard = new OnScreenKeyboard { Layout = layout, Culture = CultureInfo.GetCultureInfo("de-DE") };
        var window = Show(keyboard);
        try
        {
            keyboard.ShiftState = OnScreenKeyboardShiftState.Locked;
            keyboard.GetOutputText(one).ShouldBe("1");
            keyboard.GetOutputText(eszett).ShouldBe("Ä");
            keyboard.ShiftState = OnScreenKeyboardShiftState.On;
            keyboard.GetOutputText(one).ShouldBe("!");
            keyboard.KeyPresenters[0].Label.ShouldBe("!");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Keys_Switch_Pages()
    {
        var (window, keyboard, box, _) = TextBoxAndKeyboard();
        try
        {
            Tap(keyboard, "Shift");
            Tap(keyboard, "Numbers and symbols");
            keyboard.CurrentPage.ShouldBe(OnScreenKeyboardLayout.SymbolsPage);
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Off);
            keyboard.GetVisualDescendants().OfType<SpecialInputsLiveRegion>().Single().Message.ShouldBe("Numbers and symbols");
            string.Concat(keyboard.KeyPresenters.Where(k => k.Row == 0).Select(k => k.Label)).ShouldBe("1234567890");
            Tap(keyboard, "7");
            Tap(keyboard, "More symbols");
            keyboard.KeyPresenters[0].Label.ShouldBe("~");
            Tap(keyboard, "€");
            Tap(keyboard, "Letters");
            keyboard.CurrentPage.ShouldBe(OnScreenKeyboardLayout.LettersPage);
            box.Text.ShouldBe("7€");

            keyboard.CurrentPage = "no such page";
            Pump();
            keyboard.KeyPresenters[0].Label.ShouldBe("q");
        }
        finally
        {
            window.Close();
        }
    }

    // The key-press tests use Arc text inputs: Avalonia's TextBox edits on Backspace, arrows and Enter through the
    // presenter of its template, and this test app (no Fluent or Simple theme) gives a plain TextBox no template.
    [AvaloniaFact]
    public void Backspace_Arrows_And_Enter_Edit_The_Text_Box()
    {
        var keyboard = new OnScreenKeyboard { Preset = OnScreenKeyboardPreset.Numeric };
        var (window, _, box, _) = TextBoxAndKeyboard(new Input { Text = "1234" }, keyboard);
        var enters = 0;
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                enters++;
            }
        });
        try
        {
            box.CaretIndex = 4;
            Tap(keyboard, "Backspace");
            box.Text.ShouldBe("123");
            Tap(keyboard, "Left");
            Tap(keyboard, "Left");
            box.CaretIndex.ShouldBe(1);
            Tap(keyboard, "9");
            box.Text.ShouldBe("1923");
            Tap(keyboard, "Right");
            box.CaretIndex.ShouldBe(3);

            // A single-line box leaves Enter to bubble on (a default button, your handler).
            Tap(keyboard, "Enter");
            enters.ShouldBe(1);
            box.Text.ShouldBe("1923");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Starts_A_New_Line_In_A_Multi_Line_Box_And_Shift_Arrows_Select()
    {
        var (window, keyboard, box, _) = TextBoxAndKeyboard(new Textarea { Text = "ab" });
        try
        {
            box.CaretIndex = 2;
            Tap(keyboard, "Enter");
            box.Text.ShouldBe("ab" + Environment.NewLine);

            box.Text = "abc";
            box.CaretIndex = 3;
            Pump();
            var left = new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Left);
            keyboard.ShiftState = OnScreenKeyboardShiftState.On;
            keyboard.Invoke(left);
            box.SelectionStart.ShouldBe(3);
            box.SelectionEnd.ShouldBe(2);
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Off);
            // Typing replaces the selection.
            Tap(keyboard, "x");
            box.Text.ShouldBe("abx");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Moves_Focus_And_Shift_Tab_Moves_It_Back()
    {
        var first = new TextBox();
        var second = new TextBox();
        var keyboard = new OnScreenKeyboard();
        var window = Show(new StackPanel { Children = { first, second, keyboard } });
        try
        {
            first.Focus();
            Pump();
            var tab = new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Tab);
            keyboard.Invoke(tab);
            Pump();
            second.IsFocused.ShouldBeTrue();
            keyboard.ShiftState = OnScreenKeyboardShiftState.On;
            keyboard.Invoke(tab);
            Pump();
            first.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Target_Overrides_Focus_And_No_Target_Sends_Nothing()
    {
        var focused = new TextBox();
        var target = new TextBox();
        var keyboard = new OnScreenKeyboard();
        var window = Show(new StackPanel { Children = { focused, target, keyboard } });
        try
        {
            keyboard.ResolveTarget().ShouldBeNull();
            InputElement? reported = focused;
            keyboard.ItemInvoked += (_, e) => reported = e.Target;
            Tap(keyboard, "a");
            reported.ShouldBeNull();

            focused.Focus();
            keyboard.Target = target;
            Pump();
            Click(window, KeyNamed(keyboard, "z"));
            target.Text.ShouldBe("z");
            focused.Text.ShouldBeNullOrEmpty();
            focused.IsFocused.ShouldBeTrue();
            reported.ShouldBeSameAs(target);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Custom_Layout_Follows_Its_Keys()
    {
        var a = new OnScreenKeyboardKey("A");
        var b = new OnScreenKeyboardKey("B") { IsEnabled = false };
        var clear = new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Custom) { Text = "clear", Label = "Clear", Width = 2 };
        var escape = new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Key) { KeyCode = Key.Escape, Label = "Esc" };
        var layout = new OnScreenKeyboardLayout
        {
            Name = "Hex",
            Pages = { new OnScreenKeyboardPage("hex", OnScreenKeyboardRow.FromCharacters("0123"), new OnScreenKeyboardRow(a, b, clear, escape)) },
        };
        var keyboard = new OnScreenKeyboard { Layout = layout };
        var (window, _, box, _) = TextBoxAndKeyboard(keyboard: keyboard);
        var escapes = 0;
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                escapes++;
            }
        });
        try
        {
            keyboard.KeyPresenters.Count.ShouldBe(8);
            var bKey = KeyNamed(keyboard, "B");
            bKey.IsEffectivelyEnabled.ShouldBeFalse();
            bKey.Classes.ShouldContain(":disabled");
            Click(window, KeyNamed(keyboard, "A"));
            keyboard.Invoke(b);
            box.Text.ShouldBe("A");

            b.IsEnabled = true;
            Pump();
            bKey.IsEffectivelyEnabled.ShouldBeTrue();
            keyboard.Invoke(b);
            box.Text.ShouldBe("AB");

            // A custom key types nothing by itself; a Key key sends its key.
            OnScreenKeyboardKey? invoked = null;
            keyboard.ItemInvoked += (_, e) => invoked = e.Item;
            Click(window, KeyNamed(keyboard, "Clear"));
            invoked.ShouldBeSameAs(clear);
            box.Text.ShouldBe("AB");
            keyboard.Invoke(escape);
            escapes.ShouldBe(1);

            // Hiding a key closes up its row; a new row appears at once.
            var before = KeyNamed(keyboard, "Clear").Bounds.X;
            a.IsVisible = false;
            Pump();
            KeyNamed(keyboard, "A").IsVisible.ShouldBeFalse();
            KeyNamed(keyboard, "Clear").Bounds.X.ShouldBeLessThan(before);
            layout.Pages[0].Rows.Add(OnScreenKeyboardRow.FromCharacters("EF"));
            Pump();
            keyboard.KeyPresenters.Count.ShouldBe(10);
            KeyNamed(keyboard, "F").Row.ShouldBe(2);
            a.Label = "α";
            Pump();
            KeyNamed(keyboard, "α").ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Spacer_Takes_Room_And_Cannot_Be_Pressed()
    {
        var spacer = new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Spacer, 2);
        var layout = new OnScreenKeyboardLayout
        {
            Pages = { new OnScreenKeyboardPage("p", new OnScreenKeyboardRow(new OnScreenKeyboardKey("A"), spacer, new OnScreenKeyboardKey("B"))) },
        };
        var keyboard = new OnScreenKeyboard { Layout = layout };
        var window = Show(keyboard);
        var invoked = 0;
        keyboard.ItemInvoked += (_, _) => invoked++;
        try
        {
            var gap = keyboard.KeyPresenters[1];
            gap.IsSpacer.ShouldBeTrue();
            gap.IsHitTestVisible.ShouldBeFalse();
            gap.Classes.ShouldContain(":spacer");
            var a = KeyNamed(keyboard, "A").Bounds;
            var b = KeyNamed(keyboard, "B").Bounds;
            // Four units: B starts three units after A.
            (b.X - a.X).ShouldBe((a.Width + 6) * 3, 1.5);
            keyboard.Invoke(spacer);
            invoked.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layouts_Are_Data_That_Xaml_And_Code_Can_Build()
    {
        var row = OnScreenKeyboardRow.FromCharacters("éab");
        row.Keys.Select(k => k.Text).ShouldBe(["é", "a", "b"]);
        var qwerty = OnScreenKeyboardLayout.CreateQwerty();
        qwerty.Pages.Select(p => p.Name).ShouldBe([OnScreenKeyboardLayout.LettersPage, OnScreenKeyboardLayout.SymbolsPage, OnScreenKeyboardLayout.MoreSymbolsPage]);
        qwerty.AllKeys().Count(k => k.Kind == OnScreenKeyboardKeyKind.Backspace).ShouldBe(3);
        OnScreenKeyboardLayout.CreateQwerty().ShouldNotBeSameAs(qwerty);
        qwerty.FindPage("missing").ShouldBeNull();
        new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Backspace).RepeatsOnHold.ShouldBeTrue();
        new OnScreenKeyboardKey("a").RepeatsOnHold.ShouldBeFalse();
        new OnScreenKeyboardKey("a") { IsRepeatable = true }.RepeatsOnHold.ShouldBeTrue();
        new OnScreenKeyboardKey { Width = -3 }.Width.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Holding_Backspace_Repeats_On_The_Clock()
    {
        var clock = new TestClock();
        var keyboard = new OnScreenKeyboard { TimeProvider = clock, RepeatDelay = TimeSpan.FromMilliseconds(500), RepeatInterval = TimeSpan.FromMilliseconds(50) };
        var (window, _, box, _) = TextBoxAndKeyboard(new Input { Text = "abcdefghij" }, keyboard);
        try
        {
            box.CaretIndex = 10;
            var backspace = KeyNamed(keyboard, "Backspace");
            var point = FocusRingInput.Center(backspace, window);
            window.MouseMove(point);
            Pump();
            FocusRingInput.WaitUntilOver(window, backspace, point);
            window.MouseDown(point, MouseButton.Left);
            Pump();
            backspace.IsPressed.ShouldBeTrue();
            box.Text.ShouldBe("abcdefghi");

            clock.Advance(TimeSpan.FromMilliseconds(499));
            box.Text.ShouldBe("abcdefghi");
            clock.Advance(TimeSpan.FromMilliseconds(1));
            box.Text.ShouldBe("abcdefgh");
            clock.Advance(TimeSpan.FromMilliseconds(150));
            box.Text.ShouldBe("abcde");

            window.MouseUp(point, MouseButton.Left);
            Pump();
            backspace.IsPressed.ShouldBeFalse();
            clock.ActiveTimers.ShouldBe(0);
            clock.Advance(TimeSpan.FromSeconds(1));
            box.Text.ShouldBe("abcde");
            box.IsFocused.ShouldBeTrue();

            // Letters do not repeat.
            var a = KeyNamed(keyboard, "a");
            var at = FocusRingInput.Center(a, window);
            window.MouseMove(at);
            window.MouseDown(at, MouseButton.Left);
            Pump();
            clock.Advance(TimeSpan.FromSeconds(1));
            window.MouseUp(at, MouseButton.Left);
            Pump();
            box.Text.ShouldBe("abcdea");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handling_ItemInvoked_Takes_The_Key_Over()
    {
        var (window, keyboard, box, _) = TextBoxAndKeyboard();
        var seen = new List<OnScreenKeyboardItemInvokedEventArgs>();
        keyboard.ItemInvoked += (_, e) =>
        {
            seen.Add(e);
            if (e.Item.Kind is OnScreenKeyboardKeyKind.Shift || e.Text == "a")
            {
                e.Handled = true;
            }
        };
        try
        {
            Tap(keyboard, "a");
            box.Text.ShouldBeNullOrEmpty();
            seen[0].Text.ShouldBe("a");
            seen[0].Target.ShouldBeSameAs(box);
            seen[0].IsRepeat.ShouldBeFalse();
            seen[0].RoutedEvent.ShouldBe(OnScreenKeyboard.ItemInvokedEvent);

            Tap(keyboard, "Shift");
            keyboard.ShiftState.ShouldBe(OnScreenKeyboardShiftState.Off);

            Tap(keyboard, "s");
            box.Text.ShouldBe("s");
            seen.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Are_Named_Buttons_In_A_Keyboard_Group()
    {
        var (window, keyboard, box, _) = TextBoxAndKeyboard();
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(keyboard);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetName().ShouldBe("Keyboard");
            AutomationProperties.SetName(keyboard, "Kiosk keyboard");
            peer.GetName().ShouldBe("Kiosk keyboard");

            var q = ControlAutomationPeer.CreatePeerForElement(KeyNamed(keyboard, "q"));
            q.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            q.GetName().ShouldBe("q");
            q.GetProvider<IToggleProvider>().ShouldBeNull();
            q.GetProvider<IInvokeProvider>()!.Invoke();
            box.Text.ShouldBe("q");

            var shift = ControlAutomationPeer.CreatePeerForElement(KeyNamed(keyboard, "Shift"));
            var toggle = shift.GetProvider<IToggleProvider>()!;
            toggle.ToggleState.ShouldBe(ToggleState.Off);
            toggle.Toggle();
            toggle.ToggleState.ShouldBe(ToggleState.On);
            keyboard.GetVisualDescendants().OfType<SpecialInputsLiveRegion>().Single().Message.ShouldBe("Shift on");
            ControlAutomationPeer.CreatePeerForElement(KeyNamed(keyboard, "Backspace")).GetName().ShouldBe("Backspace");
            ControlAutomationPeer.CreatePeerForElement(KeyNamed(keyboard, "Space")).GetName().ShouldBe("Space");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Disabled_Keyboard_Does_Nothing()
    {
        var (window, keyboard, box, _) = TextBoxAndKeyboard();
        try
        {
            keyboard.IsEnabled = false;
            Pump();
            Tap(keyboard, "a");
            box.Text.ShouldBeNullOrEmpty();
            KeyNamed(keyboard, "a").Classes.ShouldContain(":disabled");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Popup_Opens_Under_A_Focused_Field_And_Closes()
    {
        var pin = new Input();
        var other = new Input();
        OnScreenKeyboard.SetPopup(pin, OnScreenKeyboardPopupMode.Digits);
        var window = Show(new StackPanel { Spacing = 12, Children = { pin, other } });
        try
        {
            OnScreenKeyboard.GetOpenPopupKeyboard(pin).ShouldBeNull();
            pin.Focus();
            Pump();
            var keyboard = OnScreenKeyboard.GetOpenPopupKeyboard(pin);
            keyboard.ShouldNotBeNull();
            keyboard.Preset.ShouldBe(OnScreenKeyboardPreset.Digits);
            keyboard.Target.ShouldBeSameAs(pin);
            Pump();
            Click(window, KeyNamed(keyboard, "4"));
            Tap(keyboard, "2");
            pin.Text.ShouldBe("42");
            pin.IsFocused.ShouldBeTrue();

            // Enter in a single-line field closes it; tapping the field opens it again.
            Tap(keyboard, "Enter");
            Pump();
            OnScreenKeyboard.GetOpenPopupKeyboard(pin).ShouldBeNull();
            Click(window, pin);
            OnScreenKeyboard.GetOpenPopupKeyboard(pin).ShouldBeSameAs(keyboard);

            // Focus leaving closes it.
            other.Focus();
            Pump();
            OnScreenKeyboard.GetOpenPopupKeyboard(pin).ShouldBeNull();

            // A Hide key closes it; a layout replaces the preset.
            var layout = new OnScreenKeyboardLayout { Pages = { new OnScreenKeyboardPage("p", new OnScreenKeyboardRow(new OnScreenKeyboardKey("x"), new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Hide))) } };
            OnScreenKeyboard.SetPopupLayout(pin, layout);
            pin.Focus();
            Pump();
            keyboard.Layout.ShouldBeSameAs(layout);
            Tap(keyboard, "Hide keyboard");
            Pump();
            OnScreenKeyboard.GetOpenPopupKeyboard(pin).ShouldBeNull();

            // Clearing both properties stops it.
            OnScreenKeyboard.SetPopupLayout(pin, null);
            OnScreenKeyboard.SetPopup(pin, OnScreenKeyboardPopupMode.None);
            other.Focus();
            pin.Focus();
            Pump();
            OnScreenKeyboard.GetOpenPopupKeyboard(pin).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A clock the test moves by hand; its timers fire during <see cref="Advance"/>.</summary>
    private sealed class TestClock : TimeProvider
    {
        private readonly List<Timer> _timers = new();
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero).AddTicks(_ticks);

        public int ActiveTimers => _timers.Count(t => t.Due is not null);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state);
            timer.Change(dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            var end = _ticks + by.Ticks;
            while (_timers.Where(t => t.Due is { } due && due <= end).OrderBy(t => t.Due).FirstOrDefault() is { } next)
            {
                _ticks = next.Due!.Value;
                next.Fire();
            }
            _ticks = end;
            Dispatcher.UIThread.RunJobs();
        }

        private sealed class Timer(TestClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _period;

            public long? Due { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._ticks + dueTime.Ticks;
                _period = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
                return true;
            }

            public void Fire()
            {
                Due = _period > 0 ? Due + _period : null;
                callback(state);
            }

            public void Dispose()
            {
                Due = null;
                clock._timers.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
