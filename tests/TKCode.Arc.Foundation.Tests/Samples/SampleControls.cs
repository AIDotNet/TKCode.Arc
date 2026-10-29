using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Foundation.Samples.DesignTokens;
using TKCode.Arc.Generated.TKCodeArcFoundationTests;

namespace TKCode.Arc.Foundation.Samples;

/// <summary>A button-like sample control. Its theme is <c>SampleButton/Themes/SampleButtonTheme.axaml</c>.</summary>
public class SampleButton : Button
{
}

[ControlDesignToken]
internal sealed class SampleButtonToken : AbstractControlDesignToken
{
    public Thickness InlinePadding { get; set; }
    public double HoverOpacity { get; set; }

    /// <summary>Accent-independent derived color (from neutrals only), as control tokens are allowed to hold.</summary>
    public Color GhostHoverBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlinePadding = new Thickness(16, 0);
        HoverOpacity = 0.91;
        GhostHoverBackground = isDarkMode ? Color.Parse("#2A2A2A") : Color.Parse("#F1F1F1");
    }
}

/// <summary>A switch-like sample control. Its theme is <c>SampleSwitch/Themes/SampleSwitchTheme.axaml</c>.</summary>
public class SampleSwitch : ToggleButton
{
}

[ControlDesignToken]
internal sealed class SampleSwitchToken : AbstractControlDesignToken
{
    public double TrackWidth { get; set; }
    public double TrackHeight { get; set; }
    public double ThumbSize { get; set; }
    public TimeSpan ThumbTravel { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackWidth = 44;
        TrackHeight = 26;
        ThumbSize = 22;
        ThumbTravel = TimeSpan.FromMilliseconds(240);
    }
}

/// <summary>A control nothing in the tests creates: its theme file must never load.</summary>
public class SampleBadge : TemplatedControl
{
}

[ControlDesignToken]
internal sealed class SampleBadgeToken : AbstractControlDesignToken
{
    public double DotSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DotSize = 8;
    }
}

/// <summary>Package registration of the samples, the way Core and Arc will register after the switch-over.</summary>
public static class SampleArcBuilderExtensions
{
    /// <summary>Times the samples' first-use callback ran in the current application.</summary>
    public static int FirstUseCount { get; set; }

    /// <summary>Package ids in the order their commit callbacks ran.</summary>
    public static List<string> CommitOrder { get; } = new();

    /// <summary>A dependency-only package (no assets), standing in for TKCode.Arc.Core.</summary>
    public static ArcBuilder UseSampleCore(this ArcBuilder builder)
        => builder.UsePackage("Samples.Core", static p => p.OnCommit(static c =>
        {
            CommitOrder.Clear();
            CommitOrder.Add(c.PackageId);
        }));

    /// <summary>Enables the sample tokens and themes.</summary>
    public static ArcBuilder UseFoundationSamples(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static p =>
        {
            ArcGeneratedAssets.AddTo(p.DependsOn(static b => b.UseSampleCore()));
            p.OnCommit(static c =>
            {
                CommitOrder.Add(c.PackageId);
                FirstUseCount = 0;
            });
            p.OnFirstUse(static _ => FirstUseCount++);
        });

    /// <summary>Reads a published ArcDesign-like token the way <c>ArcTokens.Bind</c> does: by the boxed kind.</summary>
    public static IDisposable BindToken(StyledElement target, AvaloniaProperty property, SampleDesignTokenKind kind)
        => target.Bind(property, target.GetResourceObservable(kind));
}
