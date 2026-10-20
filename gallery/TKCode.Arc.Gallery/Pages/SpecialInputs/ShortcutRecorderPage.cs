using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class ShortcutRecorderPage
{
    private const double FieldWidth = 320;

    public static Control Create()
    {
        var platform = ShortcutFormatter.CurrentPlatform;
        KeyGesture Mod(string shortcut) => ShortcutFormatter.Parse(shortcut, platform);
        string Caps(string shortcut) => ShortcutFormatter.Format(Mod(shortcut), platform).Text;

        // Arc's default demo: a labelled recorder holding mod+K, with the reset target set to the same default.
        var basic = Recorder("Command palette", Mod("mod+k"));

        // Shortcuts the app already uses: recording one asks before taking it ("Use anyway" reports the binding).
        var conflicts = Recorder("Quick search", Mod("mod+p"));
        conflicts.Bindings.Add(new ShortcutBinding(Mod("mod+s"), "Save"));
        conflicts.Bindings.Add(new ShortcutBinding(Mod("mod+f"), "Find"));
        conflicts.Description = $"Try {Caps("mod+s")} or {Caps("mod+f")}: both are already bound.";

        // Combinations browsers and systems keep (⌘W, ⌘T, ⌘C, ⌘V, ...) warn too, unless the warning is turned off.
        var reserved = Recorder("Toggle sidebar", Mod("mod+b"));
        reserved.Description = $"Try {Caps("mod+w")} or {Caps("mod+c")}.";
        var unreserved = Recorder("Duplicate line", Mod("mod+d"));
        unreserved.IsReservedWarningEnabled = false;
        unreserved.Description = "IsReservedWarningEnabled = false: reserved combinations record directly.";

        // Single-key shortcuts (Gmail style): no modifier required.
        var single = Recorder("Next conversation", Mod("j"));
        single.IsModifierRequired = false;
        single.Description = "Single keys are allowed here; Escape still cancels.";

        // The same binding on both platforms: ⌘ glyphs on a Mac, Ctrl+Shift names elsewhere.
        var mac = Recorder("Mac", ShortcutFormatter.Parse("mod+shift+k", ShortcutPlatform.Mac));
        mac.Platform = ShortcutPlatform.Mac;
        var other = Recorder("Windows and Linux", ShortcutFormatter.Parse("mod+shift+k", ShortcutPlatform.Other));
        other.Platform = ShortcutPlatform.Other;

        var hidden = Recorder("Toggle comments", Mod("mod+/"));
        hidden.IsLabelVisible = false;

        var described = Recorder("Command palette", Mod("mod+k"));
        described.Description = "Opens the command palette from anywhere in the app.";

        var disabled = Recorder("Command palette", Mod("mod+k"));
        disabled.IsEnabled = false;
        disabled.Description = "Managed by your organization.";

        var empty = new ShortcutRecorder { Label = "Start dictation", Width = FieldWidth, HorizontalAlignment = HorizontalAlignment.Left };

        return GalleryUi.Column(20,
            GalleryUi.Card("Default", "Click the field or press Enter, then the combination. Held modifiers light up as caps; the finished shortcut settles into place.",
                GalleryUi.Row(basic)),
            GalleryUi.Card("Conflicts with your shortcuts", "Bindings lists shortcuts already in use. Recording one asks first; Use anyway reports the binding in ValueChanged.Replaced.",
                GalleryUi.Row(conflicts)),
            GalleryUi.Card("Reserved combinations", "Browser and system shortcuts such as close tab, copy and paste warn before they are taken.",
                GalleryUi.Row(GalleryUi.Column(24, reserved, unreserved))),
            GalleryUi.Card("No modifier required", "IsModifierRequired = false accepts single keys. With it on, a bare key asks for Ctrl or Alt (⌘, ⌃ or ⌥ on a Mac); function keys are always allowed.",
                GalleryUi.Row(single)),
            GalleryUi.Card("Platforms", "Platform = Mac or Other forces the cap style; by default the recorder follows the running platform.",
                GalleryUi.Row(mac, other)),
            GalleryUi.Card("Empty", "With no shortcut the placeholder shows; there is nothing to clear.", GalleryUi.Row(empty)),
            GalleryUi.Card("Hidden label", "IsLabelVisible = false keeps the label for screen readers only.", GalleryUi.Row(hidden)),
            GalleryUi.Card("Description", "Helper copy under the field; the recording hint, the modifier nudge and conflicts take its place while they apply.",
                GalleryUi.Row(described)),
            GalleryUi.Card("Disabled", "Faded, on the muted surface, and out of the tab order.", GalleryUi.Row(disabled)),
            GalleryUi.Card("Keyboard", null, Keyboard()),
            GalleryUi.Card("Events", "ValueChanged reports the old and new shortcut and, after Use anyway, the binding it replaced.", Events(Mod)));
    }

    private static ShortcutRecorder Recorder(string label, KeyGesture value) => new()
    {
        Label = label,
        Value = value,
        ResetValue = value,
        Width = FieldWidth,
        HorizontalAlignment = HorizontalAlignment.Left
    };

    private static Control Keyboard() => GalleryUi.Column(8,
        GalleryUi.Text("Enter or Space starts recording; Escape cancels; Backspace or Delete clears (also when not recording).", ArcDesignTokenKind.TextSecondary),
        GalleryUi.Text("Tab stops recording and moves focus without recording Tab. Leaving the field stops recording.", ArcDesignTokenKind.TextSecondary),
        GalleryUi.Text("Keys are read from the physical key, so Alt+K records K, not a typed symbol. Chords the system keeps (⌘Tab, Alt+Tab, Win key combinations) never reach the app.", ArcDesignTokenKind.TextSecondary));

    private static Control Events(Func<string, KeyGesture> mod)
    {
        var recorder = Recorder("Command palette", mod("mod+k"));
        recorder.Bindings.Add(new ShortcutBinding(mod("mod+s"), "Save"));
        var log = new StackPanel { Spacing = 4 };
        log.Children.Add(GalleryUi.Mono("Record a shortcut to see ValueChanged.", ArcDesignTokenKind.TextMuted));
        recorder.ValueChanged += (_, e) =>
        {
            var platform = recorder.EffectivePlatform;
            string Show(KeyGesture? g) => g is null ? "none" : ShortcutFormatter.Format(g, platform).Text;
            var line = $"ValueChanged {Show(e.OldValue)} → {Show(e.NewValue)}" + (e.Replaced is { } replaced ? $" (replaced {replaced.Label})" : string.Empty);
            if (log.Children.Count == 1 && log.Children[0] is TextBlock { Text: "Record a shortcut to see ValueChanged." })
            {
                log.Children.Clear();
            }
            log.Children.Insert(0, GalleryUi.Mono(line));
            while (log.Children.Count > 6)
            {
                log.Children.RemoveAt(log.Children.Count - 1);
            }
        };
        return GalleryUi.Row(GalleryUi.Column(16, recorder, log));
    }
}
