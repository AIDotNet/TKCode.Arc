using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiToolToggle"/>: geometry, off / on / hover / press looks from ArcDesign tokens, toggling by pointer and keys,
/// the toggle automation pattern and name, tooltips, and the icon-only compact width.
/// </summary>
public class AiToolToggleTests
{
    private sealed class Rig : IDisposable
    {
        public AiToolToggle Toggle { get; }
        public Window Window { get; }

        public Rig(Action<AiToolToggle>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off)
        {
            Toggle = new AiToolToggle { Label = "Web search", Icon = ArcIcons.Globe };
            configure?.Invoke(Toggle);
            Window = TestHelpers.Show(new StackPanel { Margin = new Thickness(24), Children = { Toggle } }, level);
            Dispatcher.UIThread.RunJobs();
        }

        public Border Part(string name) => TestHelpers.Descendant<Border>(Toggle, name);

        public Point Center => Toggle.TranslatePoint(new Point(Toggle.Bounds.Width / 2, 18), Window) ?? default;

        public void Hover()
        {
            Window.MouseMove(Center, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        public void Click()
        {
            Window.MouseMove(Center, RawInputModifiers.None);
            Window.MouseDown(Center, MouseButton.Left, RawInputModifiers.None);
            Window.MouseUp(Center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    [AvaloniaFact]
    public void Off_Is_A_36px_Bordered_Pill_With_Icon_And_Label()
    {
        using var rig = new Rig();
        var toggle = rig.Toggle;
        Assert.Equal(36, toggle.Bounds.Height);
        Assert.Equal(new Thickness(10, 0, 12, 0), toggle.Padding);
        Assert.False(toggle.IsChecked);
        var icon = TestHelpers.Descendant<ArcGlyph>(toggle);
        Assert.Equal(16, icon.Size);
        Assert.Equal(1.75, icon.StrokeThickness);
        var label = TestHelpers.Descendant<TextBlock>(toggle, "PART_Label");
        Assert.True(label.IsVisible);
        Assert.Equal("Web search", label.Text);
        // Icon at 10px from the left edge, the label 6px after it, 12px of padding after the label.
        var iconLeft = icon.TranslatePoint(default, toggle)!.Value.X;
        Assert.Equal(10, iconLeft, 1);
        var labelLeft = label.TranslatePoint(default, toggle)!.Value.X;
        Assert.Equal(32, labelLeft, 1);
        Assert.Equal(labelLeft + label.Bounds.Width + 12, toggle.Bounds.Width, 1);

        Assert.Equal(Colors.Transparent, TestHelpers.ColorOf(toggle.Background));
        Assert.Equal(Token(toggle, ArcDesignTokenKind.Border), TestHelpers.ColorOf(rig.Part("PART_Edge").BorderBrush));
        Assert.Equal(Token(toggle, ArcDesignTokenKind.TextSecondary), TestHelpers.ColorOf(icon.Foreground));
        Assert.Equal(Token(toggle, ArcDesignTokenKind.TextSecondary), TestHelpers.ColorOf(label.Foreground));
        Assert.Equal(0, rig.Part("PART_Wash").Opacity);
        Assert.Equal(0, rig.Part("PART_AccentEdge").Opacity);
    }

    [AvaloniaFact]
    public void On_Takes_The_Accent_Tint()
    {
        using var rig = new Rig(t => t.IsChecked = true);
        var toggle = rig.Toggle;
        Assert.Equal(Token(toggle, ArcDesignTokenKind.AccentSubtle), TestHelpers.ColorOf(toggle.Background));
        // The plain border gives way to the accent edge.
        Assert.Equal(0, TestHelpers.ColorOf(rig.Part("PART_Edge").BorderBrush).A);
        var accentEdge = rig.Part("PART_AccentEdge");
        Assert.Equal(0.32, accentEdge.Opacity, 3);
        Assert.Equal(Token(toggle, ArcDesignTokenKind.Accent), TestHelpers.ColorOf(accentEdge.BorderBrush));
        Assert.Equal(Token(toggle, ArcDesignTokenKind.AccentStrong), TestHelpers.ColorOf(TestHelpers.Descendant<ArcGlyph>(toggle).Foreground));
        Assert.Equal(Token(toggle, ArcDesignTokenKind.AccentStrong),
            TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(toggle, "PART_Label").Foreground));
    }

    [AvaloniaFact]
    public void Hover_And_Press_Tint_With_Foreground_Off_And_Accent_On()
    {
        using var rig = new Rig();
        var toggle = rig.Toggle;
        var wash = rig.Part("PART_Wash");
        rig.Hover();
        Assert.Contains(AiToolToggle.HoverPseudoClass, toggle.Classes);
        Assert.Equal(0.06, wash.Opacity, 3);
        Assert.Equal(Token(toggle, ArcDesignTokenKind.Foreground), TestHelpers.ColorOf(wash.Background));
        Assert.Equal(Token(toggle, ArcDesignTokenKind.Foreground), TestHelpers.ColorOf(toggle.Foreground));
        // The border does not change on hover.
        Assert.Equal(Token(toggle, ArcDesignTokenKind.Border), TestHelpers.ColorOf(rig.Part("PART_Edge").BorderBrush));

        rig.Window.MouseDown(rig.Center, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0.1, wash.Opacity, 3);
        rig.Window.MouseUp(rig.Center, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        // Now on, still hovered: the tint is the accent over AccentSubtle (Arc's ~17%), the text stays AccentStrong.
        Assert.True(toggle.IsChecked);
        Assert.Equal(0.06, wash.Opacity, 3);
        Assert.Equal(Token(toggle, ArcDesignTokenKind.Accent), TestHelpers.ColorOf(wash.Background));
        Assert.Equal(Token(toggle, ArcDesignTokenKind.AccentStrong), TestHelpers.ColorOf(toggle.Foreground));

        rig.Window.MouseMove(new Point(390, 290), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(AiToolToggle.HoverPseudoClass, toggle.Classes);
        Assert.Equal(0, wash.Opacity);
    }

    [AvaloniaFact]
    public void Pointer_Enter_And_Space_Toggle_It()
    {
        using var rig = new Rig();
        var toggle = rig.Toggle;
        var changes = 0;
        toggle.IsCheckedChanged += (_, _) => changes++;
        rig.Click();
        Assert.True(toggle.IsChecked);
        rig.Click();
        Assert.False(toggle.IsChecked);

        toggle.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        rig.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(toggle.IsChecked);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.False(toggle.IsChecked);
        Assert.Equal(4, changes);
    }

    [AvaloniaFact]
    public void Automation_Is_A_Named_Toggle()
    {
        using var rig = new Rig();
        var toggle = rig.Toggle;
        var peer = ControlAutomationPeer.CreatePeerForElement(toggle);
        Assert.Equal("Web search", peer.GetName());
        Assert.Empty(peer.GetChildren());
        var provider = peer.GetProvider<IToggleProvider>();
        Assert.NotNull(provider);
        Assert.Equal(ToggleState.Off, provider.ToggleState);
        provider.Toggle();
        Assert.True(toggle.IsChecked);
        Assert.Equal(ToggleState.On, provider.ToggleState);

        toggle.Label = "Extended thinking";
        Assert.Equal("Extended thinking", AutomationProperties.GetName(toggle));
        toggle.Label = null;
        Assert.Null(AutomationProperties.GetName(toggle));
    }

    [AvaloniaFact]
    public void The_Tooltip_Is_The_Tip_Or_Else_The_Label()
    {
        using var rig = new Rig();
        var toggle = rig.Toggle;
        Assert.Equal("Web search", Tooltip.GetTip(toggle));
        toggle.Tip = "Search the web before answering";
        Assert.Equal("Search the web before answering", Tooltip.GetTip(toggle));
        // The accessible name stays the label.
        Assert.Equal("Web search", AutomationProperties.GetName(toggle));
        toggle.Tip = null;
        Assert.Equal("Web search", Tooltip.GetTip(toggle));
        toggle.Label = null;
        Assert.Null(Tooltip.GetTip(toggle));
    }

    [AvaloniaFact]
    public void Compact_Keeps_Only_The_Icon_In_A_36px_Circle()
    {
        using var rig = new Rig(t => t.IsChecked = true, ArcMotionLevel.Full);
        var toggle = rig.Toggle;
        var wide = toggle.Bounds.Width;
        Assert.True(wide > 100);

        toggle.IsCompact = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(AiToolToggle.CompactPseudoClass, toggle.Classes);
        // At once, even at full motion: a breakpoint does not spring.
        Assert.Equal(36, toggle.Bounds.Width);
        Assert.Equal(36, toggle.Bounds.Height);
        Assert.False(TestHelpers.Descendant<TextBlock>(toggle, "PART_Label").IsVisible);
        var icon = TestHelpers.Descendant<ArcGlyph>(toggle);
        var center = icon.TranslatePoint(new Point(8, 8), toggle)!.Value;
        Assert.Equal(18, center.X, 1);
        Assert.Equal(18, center.Y, 1);
        // Border and tint stay; the tooltip and the name still say what it is.
        Assert.Equal(0.32, rig.Part("PART_AccentEdge").Opacity, 3);
        Assert.Equal(Token(toggle, ArcDesignTokenKind.AccentSubtle), TestHelpers.ColorOf(toggle.Background));
        Assert.Equal("Web search", Tooltip.GetTip(toggle));
        Assert.Equal("Web search", AutomationProperties.GetName(toggle));

        toggle.IsCompact = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(wide, toggle.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void Focus_Ring_Shows_For_The_Keyboard_Only()
    {
        using (var rig = new Rig())
        {
            rig.Click();
            Assert.True(rig.Toggle.IsFocused);
            Assert.False(rig.Part("PART_FocusRing").IsVisible);
        }
        using (var rig = new Rig())
        {
            rig.Toggle.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            var ring = rig.Part("PART_FocusRing");
            Assert.True(ring.IsVisible);
            Assert.Equal(new Thickness(-4), ring.Margin);
        }
    }

    [AvaloniaFact]
    public void Disabled_Fades_And_Drops_The_Hover()
    {
        using var rig = new Rig();
        rig.Hover();
        rig.Toggle.IsEnabled = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0.45, rig.Toggle.Opacity, 3);
        Assert.DoesNotContain(AiToolToggle.HoverPseudoClass, rig.Toggle.Classes);
    }
}
