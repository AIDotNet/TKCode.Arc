using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using TKCode.Arc;
using Xunit;

namespace TKCode.Arc.Blocks.Tests;

public sealed class BlocksSelectiveRegistrationTests
{
    [AvaloniaFact]
    public void UseArcBlocks_Selective_Registers_Only_Chosen_Tokens_And_Themes()
    {
        var baseRuntime = ArcRuntime.Install(new Application(), new ArcBuilder().UseArcControls());

        var builder = new ArcBuilder()
            .UseArcBlocks(blocks => blocks
                .UseSettings()
                .UseSidebar()
                .UseSettingsGroup()); // idempotent duplicate
        var runtime = ArcRuntime.Install(new Application(), builder);

        Assert.Contains("TKCode.Arc.Blocks", runtime.Packages);
        Assert.DoesNotContain("TKCode.Arc.Markdown", runtime.Packages);
        Assert.Contains("TKCode.Arc", runtime.Packages);
        Assert.Contains("TKCode.Arc.Core", runtime.Packages);

        Assert.Equal(baseRuntime.Theme.Descriptors.Count + 6, runtime.Theme.Descriptors.Count);
        Assert.Contains(runtime.Theme.Descriptors, t => t.Identity == "SettingsPage");
        Assert.Contains(runtime.Theme.Descriptors, t => t.Identity == "SettingsPageNavItem");
        Assert.Contains(runtime.Theme.Descriptors, t => t.Identity == "SettingsSection");
        Assert.Contains(runtime.Theme.Descriptors, t => t.Identity == "SettingsGroup");
        Assert.Contains(runtime.Theme.Descriptors, t => t.Identity == "SettingsRow");
        Assert.Contains(runtime.Theme.Descriptors, t => t.Identity == "Sidebar");
        Assert.DoesNotContain(runtime.Theme.Descriptors, t => t.Identity == "CommandPalette");

        Assert.Equal(baseRuntime.Themes.AssetCount + 4, runtime.Themes.AssetCount);
        Assert.True(runtime.Themes.Defines(typeof(SettingsPage)));
        Assert.True(runtime.Themes.Defines(typeof(SettingsGroup)));
        Assert.True(runtime.Themes.Defines(typeof(SettingsRow)));
        Assert.True(runtime.Themes.Defines(typeof(Sidebar)));
        Assert.False(runtime.Themes.Defines(typeof(CommandPalette)));

        Assert.True(runtime.Themes.TryGetResource(typeof(SettingsGroup), null, out var groupTheme));
        Assert.IsType<ControlTheme>(groupTheme);
        Assert.True(runtime.Themes.TryGetResource(typeof(Sidebar), null, out var sidebarTheme));
        Assert.IsType<ControlTheme>(sidebarTheme);
    }

    [Fact]
    public void UseArcBlocks_UseAll_Cannot_Be_Mixed_With_Individual_Selections()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ArcBuilder().UseArcBlocks(blocks => blocks.UseSidebar().UseAll()));
    }
}
