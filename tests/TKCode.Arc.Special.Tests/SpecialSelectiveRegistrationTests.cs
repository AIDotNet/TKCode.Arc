using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using TKCode.Arc;
using Xunit;

namespace TKCode.Arc.Special.Tests;

public sealed class SpecialSelectiveRegistrationTests
{
    [AvaloniaFact]
    public void UseArcSpecial_Selective_Registers_Only_Chosen_Tokens_And_Themes()
    {
        var baseRuntime = ArcRuntime.Install(new Application(), new ArcBuilder().UseArcControls());

        var builder = new ArcBuilder()
            .UseArcSpecial(special => special
                .UseImageReveal()
                .UseImageReveal()); // idempotent duplicate
        var runtime = ArcRuntime.Install(new Application(), builder);

        Assert.Contains("TKCode.Arc.Special", runtime.Packages);
        Assert.Contains("TKCode.Arc", runtime.Packages);
        Assert.Contains("TKCode.Arc.Core", runtime.Packages);

        Assert.Equal(baseRuntime.Theme.Descriptors.Count + 1, runtime.Theme.Descriptors.Count);
        Assert.Contains(runtime.Theme.Descriptors, t => t.Identity == "ImageReveal");
        Assert.DoesNotContain(runtime.Theme.Descriptors, t => t.Identity == "EmojiPicker");

        Assert.Equal(baseRuntime.Themes.AssetCount + 1, runtime.Themes.AssetCount);
        Assert.True(runtime.Themes.Defines(typeof(ImageReveal)));
        Assert.False(runtime.Themes.Defines(typeof(EmojiPicker)));

        Assert.True(runtime.Themes.TryGetResource(typeof(ImageReveal), null, out var revealTheme));
        Assert.IsType<ControlTheme>(revealTheme);
    }

    [Fact]
    public void UseArcSpecial_UseAll_Cannot_Be_Mixed_With_Individual_Selections()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ArcBuilder().UseArcSpecial(special => special.UseImageReveal().UseAll()));
    }
}
