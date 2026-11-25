// Ported from vscode (src/vs/base/test/common/keyCodes.test.ts, commit b57d5c9e), MIT license.
// The four upstream tests; the last test is not upstream: it checks that every key code has a name (upstream's table
// builder throws when one is missing, which here would fail the type initializer).

using Shouldly;
using Xunit;
using static TKCode.Arc.Editor.KeyCodes;

namespace TKCode.Arc.Editor.Tests;

public sealed class KeyCodesTests
{
    private static void TestBinaryEncoding(Keybinding? expected, int k, OperatingSystemKind os)
    {
        var actual = Keybindings.DecodeKeybinding(k, os);
        if (expected is null)
        {
            actual.ShouldBeNull();
        }
        else
        {
            actual.ShouldNotBeNull();
            actual.Equals(expected).ShouldBeTrue($"{actual} != {expected}");
        }
    }

    private static Keybinding Kb(bool ctrl, bool shift, bool alt, bool meta, KeyCode keyCode) =>
        new KeyCodeChord(ctrl, shift, alt, meta, keyCode).ToKeybinding();

    private const int Enter = (int)KeyCode.Enter;

    [Fact]
    public void Mapping_For_Minus()
    {
        // [147, 83, 0, ScanCode.Minus, 'Minus', KeyCode.US_MINUS, '-', 189, 'VK_OEM_MINUS', '-', 'OEM_MINUS'],
        EventKeyCodeMap[189].ShouldBe(KeyCode.Minus);
        NativeWindowsKeyCodeToKeyCode["VK_OEM_MINUS"].ShouldBe(KeyCode.Minus);
        ScanCodeUtils.LowerCaseToEnum("minus").ShouldBe(ScanCode.Minus);
        ScanCodeUtils.ToEnum("Minus").ShouldBe(ScanCode.Minus);
        ScanCodeUtils.ToString(ScanCode.Minus).ShouldBe("Minus");
        ImmutableCodeToKeyCode[(int)ScanCode.Minus].ShouldBe(KeyCode.DependsOnKbLayout);
        ImmutableKeyCodeToCode[(int)KeyCode.Minus].ShouldBe(ScanCode.DependsOnKbLayout);
        KeyCodeUtils.ToString(KeyCode.Minus).ShouldBe("-");
        KeyCodeUtils.FromString("-").ShouldBe(KeyCode.Minus);
        KeyCodeUtils.ToUserSettingsUS(KeyCode.Minus).ShouldBe("-");
        KeyCodeUtils.ToUserSettingsGeneral(KeyCode.Minus).ShouldBe("OEM_MINUS");
        KeyCodeUtils.FromUserSettings("-").ShouldBe(KeyCode.Minus);
        KeyCodeUtils.FromUserSettings("OEM_MINUS").ShouldBe(KeyCode.Minus);
        KeyCodeUtils.FromUserSettings("oem_minus").ShouldBe(KeyCode.Minus);
    }

    [Fact]
    public void Mapping_For_Space()
    {
        // [21, 10, 1, ScanCode.Space, 'Space', KeyCode.Space, 'Space', 32, 'VK_SPACE', empty, empty],
        EventKeyCodeMap[32].ShouldBe(KeyCode.Space);
        NativeWindowsKeyCodeToKeyCode["VK_SPACE"].ShouldBe(KeyCode.Space);
        ScanCodeUtils.LowerCaseToEnum("space").ShouldBe(ScanCode.Space);
        ScanCodeUtils.ToEnum("Space").ShouldBe(ScanCode.Space);
        ScanCodeUtils.ToString(ScanCode.Space).ShouldBe("Space");
        ImmutableCodeToKeyCode[(int)ScanCode.Space].ShouldBe(KeyCode.Space);
        ImmutableKeyCodeToCode[(int)KeyCode.Space].ShouldBe(ScanCode.Space);
        KeyCodeUtils.ToString(KeyCode.Space).ShouldBe("Space");
        KeyCodeUtils.FromString("Space").ShouldBe(KeyCode.Space);
        KeyCodeUtils.ToUserSettingsUS(KeyCode.Space).ShouldBe("Space");
        KeyCodeUtils.ToUserSettingsGeneral(KeyCode.Space).ShouldBe("Space");
        KeyCodeUtils.FromUserSettings("Space").ShouldBe(KeyCode.Space);
        KeyCodeUtils.FromUserSettings("space").ShouldBe(KeyCode.Space);
    }

    [Fact]
    public void Mac_Binary_Encoding()
    {
        void Test(Keybinding? expected, int k) => TestBinaryEncoding(expected, k, OperatingSystemKind.Macintosh);

        Test(null, 0);
        Test(Kb(false, false, false, false, KeyCode.Enter), Enter);
        Test(Kb(true, false, false, false, KeyCode.Enter), KeyMod.WinCtrl | Enter);
        Test(Kb(false, false, true, false, KeyCode.Enter), KeyMod.Alt | Enter);
        Test(Kb(true, false, true, false, KeyCode.Enter), KeyMod.Alt | KeyMod.WinCtrl | Enter);
        Test(Kb(false, true, false, false, KeyCode.Enter), KeyMod.Shift | Enter);
        Test(Kb(true, true, false, false, KeyCode.Enter), KeyMod.Shift | KeyMod.WinCtrl | Enter);
        Test(Kb(false, true, true, false, KeyCode.Enter), KeyMod.Shift | KeyMod.Alt | Enter);
        Test(Kb(true, true, true, false, KeyCode.Enter), KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | Enter);
        Test(Kb(false, false, false, true, KeyCode.Enter), KeyMod.CtrlCmd | Enter);
        Test(Kb(true, false, false, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.WinCtrl | Enter);
        Test(Kb(false, false, true, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Alt | Enter);
        Test(Kb(true, false, true, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.WinCtrl | Enter);
        Test(Kb(false, true, false, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | Enter);
        Test(Kb(true, true, false, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.WinCtrl | Enter);
        Test(Kb(false, true, true, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | Enter);
        Test(Kb(true, true, true, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | Enter);

        Test(
            new Keybinding([
                new KeyCodeChord(false, false, false, false, KeyCode.Enter),
                new KeyCodeChord(false, false, false, false, KeyCode.Tab),
            ]),
            KeyChord(Enter, (int)KeyCode.Tab));
        Test(
            new Keybinding([
                new KeyCodeChord(false, false, false, true, KeyCode.KeyY),
                new KeyCodeChord(false, false, false, false, KeyCode.KeyZ),
            ]),
            KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyY, (int)KeyCode.KeyZ));
    }

    [Fact]
    public void Windows_And_Linux_Binary_Encoding()
    {
        foreach (var os in new[] { OperatingSystemKind.Linux, OperatingSystemKind.Windows })
        {
            void Test(Keybinding? expected, int k) => TestBinaryEncoding(expected, k, os);

            Test(null, 0);
            Test(Kb(false, false, false, false, KeyCode.Enter), Enter);
            Test(Kb(false, false, false, true, KeyCode.Enter), KeyMod.WinCtrl | Enter);
            Test(Kb(false, false, true, false, KeyCode.Enter), KeyMod.Alt | Enter);
            Test(Kb(false, false, true, true, KeyCode.Enter), KeyMod.Alt | KeyMod.WinCtrl | Enter);
            Test(Kb(false, true, false, false, KeyCode.Enter), KeyMod.Shift | Enter);
            Test(Kb(false, true, false, true, KeyCode.Enter), KeyMod.Shift | KeyMod.WinCtrl | Enter);
            Test(Kb(false, true, true, false, KeyCode.Enter), KeyMod.Shift | KeyMod.Alt | Enter);
            Test(Kb(false, true, true, true, KeyCode.Enter), KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | Enter);
            Test(Kb(true, false, false, false, KeyCode.Enter), KeyMod.CtrlCmd | Enter);
            Test(Kb(true, false, false, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.WinCtrl | Enter);
            Test(Kb(true, false, true, false, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Alt | Enter);
            Test(Kb(true, false, true, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.WinCtrl | Enter);
            Test(Kb(true, true, false, false, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | Enter);
            Test(Kb(true, true, false, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.WinCtrl | Enter);
            Test(Kb(true, true, true, false, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | Enter);
            Test(Kb(true, true, true, true, KeyCode.Enter), KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | Enter);

            Test(
                new Keybinding([
                    new KeyCodeChord(false, false, false, false, KeyCode.Enter),
                    new KeyCodeChord(false, false, false, false, KeyCode.Tab),
                ]),
                KeyChord(Enter, (int)KeyCode.Tab));
            Test(
                new Keybinding([
                    new KeyCodeChord(true, false, false, false, KeyCode.KeyY),
                    new KeyCodeChord(false, false, false, false, KeyCode.KeyZ),
                ]),
                KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyY, (int)KeyCode.KeyZ));
        }
    }

    [Fact]
    public void Every_Key_Code_Has_A_Name()
    {
        for (var keyCode = KeyCode.Unknown; keyCode < KeyCode.MaxValue; keyCode++)
        {
            KeyCodeUtils.ToString(keyCode).ShouldNotBeNullOrEmpty($"KeyCode {keyCode}");
            KeyCodeUtils.FromString(KeyCodeUtils.ToString(keyCode)).ShouldBe(keyCode);
        }
    }
}
