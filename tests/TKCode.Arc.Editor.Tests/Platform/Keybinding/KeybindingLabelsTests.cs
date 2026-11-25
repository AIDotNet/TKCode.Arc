// Ported from vscode (src/vs/platform/keybinding/test/common/keybindingLabels.test.ts, commit b57d5c9e), MIT license.
// The local helpers become AssertGetLabel, AssertGetAriaLabel, AssertGetElectronAccelerator and AssertGetUserSettingsLabel
// (upstream's 'User Settings label' test names its helper assertElectronAcceleratorLabel but reads getUserSettingsLabel).

using Shouldly;
using Xunit;
using static TKCode.Arc.Editor.KeyCodes;

namespace TKCode.Arc.Editor.Tests;

public sealed class KeybindingLabelsTests
{
    private static ResolvedKeybinding Resolve(OperatingSystemKind os, int keybinding) =>
        KeybindingsTestUtils.CreateUSLayoutResolvedKeybinding(keybinding, os)!;

    private static void AssertGetLabel(OperatingSystemKind os, int keybinding, string? expected) =>
        Resolve(os, keybinding).GetLabel().ShouldBe(expected);

    private static void AssertGetAriaLabel(OperatingSystemKind os, int keybinding, string? expected) =>
        Resolve(os, keybinding).GetAriaLabel().ShouldBe(expected);

    private static void AssertGetElectronAccelerator(OperatingSystemKind os, int keybinding, string? expected) =>
        Resolve(os, keybinding).GetElectronAccelerator().ShouldBe(expected);

    private static void AssertGetUserSettingsLabel(OperatingSystemKind os, int keybinding, string? expected) =>
        Resolve(os, keybinding).GetUserSettingsLabel().ShouldBe(expected);

    [Fact]
    public void Windows_US_label()
    {
        AssertGetLabel(OperatingSystemKind.Windows, (int)KeyCode.KeyA, "A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | (int)KeyCode.KeyA, "Ctrl+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.Shift | (int)KeyCode.KeyA, "Shift+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.Alt | (int)KeyCode.KeyA, "Alt+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.WinCtrl | (int)KeyCode.KeyA, "Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyA, "Ctrl+Shift+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.KeyA, "Ctrl+Alt+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyA, "Shift+Alt+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.Shift | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Shift+Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Alt+Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyA, "Ctrl+Shift+Alt+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Shift+Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Alt+Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Shift+Alt+Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Shift+Alt+Windows+A");
        AssertGetLabel(OperatingSystemKind.Windows, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), "Ctrl+A Ctrl+B");
    }

    [Fact]
    public void Linux_US_label()
    {
        AssertGetLabel(OperatingSystemKind.Linux, (int)KeyCode.KeyA, "A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | (int)KeyCode.KeyA, "Ctrl+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.Shift | (int)KeyCode.KeyA, "Shift+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.Alt | (int)KeyCode.KeyA, "Alt+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.WinCtrl | (int)KeyCode.KeyA, "Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyA, "Ctrl+Shift+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.KeyA, "Ctrl+Alt+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyA, "Shift+Alt+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.Shift | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Shift+Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Alt+Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyA, "Ctrl+Shift+Alt+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Shift+Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Alt+Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Shift+Alt+Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Shift+Alt+Super+A");
        AssertGetLabel(OperatingSystemKind.Linux, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), "Ctrl+A Ctrl+B");
    }

    [Fact]
    public void Mac_US_label()
    {
        AssertGetLabel(OperatingSystemKind.Macintosh, (int)KeyCode.KeyA, "A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | (int)KeyCode.KeyA, "⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.Shift | (int)KeyCode.KeyA, "⇧A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.Alt | (int)KeyCode.KeyA, "⌥A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyA, "⇧⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.KeyA, "⌥⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyA, "⇧⌥A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.Shift | KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃⇧A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃⌥A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyA, "⇧⌥⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃⇧⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃⌥⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃⇧⌥A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "⌃⇧⌥⌘A");
        AssertGetLabel(OperatingSystemKind.Macintosh, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), "⌘A ⌘B");
        AssertGetLabel(OperatingSystemKind.Macintosh, (int)KeyCode.LeftArrow, "←");
        AssertGetLabel(OperatingSystemKind.Macintosh, (int)KeyCode.UpArrow, "↑");
        AssertGetLabel(OperatingSystemKind.Macintosh, (int)KeyCode.RightArrow, "→");
        AssertGetLabel(OperatingSystemKind.Macintosh, (int)KeyCode.DownArrow, "↓");
    }

    [Fact]
    public void Aria_label()
    {
        AssertGetAriaLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Control+Shift+Alt+Windows+A");
        AssertGetAriaLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Control+Shift+Alt+Super+A");
        AssertGetAriaLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Control+Shift+Option+Command+A");
    }

    [Fact]
    public void Electron_Accelerator_label()
    {
        AssertGetElectronAccelerator(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Shift+Alt+Super+A");
        AssertGetElectronAccelerator(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Shift+Alt+Super+A");
        AssertGetElectronAccelerator(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "Ctrl+Shift+Alt+Cmd+A");
        AssertGetElectronAccelerator(OperatingSystemKind.Windows, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), null);
        AssertGetElectronAccelerator(OperatingSystemKind.Linux, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), null);
        AssertGetElectronAccelerator(OperatingSystemKind.Macintosh, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), null);
        AssertGetElectronAccelerator(OperatingSystemKind.Windows, (int)KeyCode.Numpad1, null);
        AssertGetElectronAccelerator(OperatingSystemKind.Linux, (int)KeyCode.Numpad1, null);
        AssertGetElectronAccelerator(OperatingSystemKind.Macintosh, (int)KeyCode.Numpad1, null);
        AssertGetElectronAccelerator(OperatingSystemKind.Macintosh, (int)KeyCode.LeftArrow, "Left");
        AssertGetElectronAccelerator(OperatingSystemKind.Macintosh, (int)KeyCode.UpArrow, "Up");
        AssertGetElectronAccelerator(OperatingSystemKind.Macintosh, (int)KeyCode.RightArrow, "Right");
        AssertGetElectronAccelerator(OperatingSystemKind.Macintosh, (int)KeyCode.DownArrow, "Down");
    }

    [Fact]
    public void User_Settings_label()
    {
        AssertGetUserSettingsLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "ctrl+shift+alt+win+a");
        AssertGetUserSettingsLabel(OperatingSystemKind.Linux, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "ctrl+shift+alt+meta+a");
        AssertGetUserSettingsLabel(OperatingSystemKind.Macintosh, KeyMod.CtrlCmd | KeyMod.Shift | KeyMod.Alt | KeyMod.WinCtrl | (int)KeyCode.KeyA, "ctrl+shift+alt+cmd+a");
        AssertGetUserSettingsLabel(OperatingSystemKind.Windows, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), "ctrl+a ctrl+b");
        AssertGetUserSettingsLabel(OperatingSystemKind.Linux, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), "ctrl+a ctrl+b");
        AssertGetUserSettingsLabel(OperatingSystemKind.Macintosh, KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyA, KeyMod.CtrlCmd | (int)KeyCode.KeyB), "cmd+a cmd+b");
    }

    [Fact]
    public void issue_91235_Do_not_end_with_a()
    {
        AssertGetLabel(OperatingSystemKind.Windows, KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.Alt, "Ctrl+Alt");
    }
}
