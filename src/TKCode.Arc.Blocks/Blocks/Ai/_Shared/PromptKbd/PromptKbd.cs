using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A small key cap for a key or a shortcut ("{{", "⌘↵"): monospace 11 / 15.4 in the secondary text color inside a 1px
/// border with radius 5 (Prompt studio's hint line). <see cref="IsMuted"/> is the variant on a dark button (the Run
/// button): no border, the body font at 12 / 16.8 with 0.24 letter spacing, the button's own text color at 62%.
/// The static members say how the run shortcut reads on this platform: "⌘↵" on macOS and iOS, "Ctrl ↵" elsewhere.
/// </summary>
/// <remarks>
/// The cap reads as text named by <see cref="Text"/>; a control that shows it as a hint of its own shortcut hides it
/// from screen readers and states the shortcut itself (<see cref="RunAcceleratorKey"/>).
/// </remarks>
[PseudoClasses(MutedPseudoClass)]
internal class PromptKbd : TemplatedControl
{
    public const string MutedPseudoClass = ":muted";

    /// <summary>The key or the shortcut shown in the cap.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptKbd, string?>(nameof(Text));

    /// <summary>The borderless variant on a dark button (<c>:muted</c>).</summary>
    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<PromptKbd, bool>(nameof(IsMuted));

    static PromptKbd()
    {
        FocusableProperty.OverrideDefaultValue<PromptKbd>(false);
        IsTabStopProperty.OverrideDefaultValue<PromptKbd>(false);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsMutedProperty"/>
    public bool IsMuted { get => GetValue(IsMutedProperty); set => SetValue(IsMutedProperty, value); }

    /// <summary>
    /// Test seam: true or false pretends to run on macOS or elsewhere; null (the default) asks the operating system.
    /// </summary>
    internal static bool? PlatformOverride { get; set; }

    /// <summary>Whether the shortcuts read the Mac way (Command instead of Control).</summary>
    public static bool IsMacPlatform => PlatformOverride ?? (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS());

    /// <summary>The cap of the run shortcut: "⌘↵" on macOS, "Ctrl ↵" elsewhere.</summary>
    public static string RunShortcutText => IsMacPlatform ? "⌘↵" : "Ctrl ↵";

    /// <summary>The name of the shortcut's modifier in running text ("Cmd" on macOS, "Ctrl" elsewhere), for tooltips and names.</summary>
    public static string ModifierName => IsMacPlatform ? "Cmd" : "Ctrl";

    /// <summary>The run shortcut as an accelerator key for screen readers ("Cmd+Enter" or "Ctrl+Enter").</summary>
    public static string RunAcceleratorKey => ModifierName + "+Enter";

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsMutedProperty)
        {
            PseudoClasses.Set(MutedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new KbdPeer(this);

    /// <summary>Reads as one piece of text (the cap's text block is not exposed separately).</summary>
    private sealed class KbdPeer(PromptKbd owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : ((PromptKbd)Owner).Text;
    }
}
