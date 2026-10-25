using TKCode.Arc.DesignTokens;
using TKCode.Arc.Themes.Pixel;
using AtomUI;
using AtomUI.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Theme;

/// <summary>The pixel theme package: token overrides resolve in both appearances and the default can be restored.</summary>
public class PixelThemeTests
{
    private static async Task ApplyPixelAsync(bool dark)
    {
        var manager = Avalonia.Application.Current!.GetThemeManager()!;
        var result = await manager.ApplyThemeAsync(ArcPixelTheme.CreateRequest(dark, scope: ArcPixelScope.All), TestContext.Current.CancellationToken);
        result.Status.ToString().ShouldNotBe("Failed", result.ToString());
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTheory]
    [InlineData(false, "#F4F4F4", "#1A1C2C", "#3B5DC9")]
    [InlineData(true, "#1A1C2C", "#F4F4F4", "#73EFF7")]
    public async Task Pixel_Tokens_Resolve_In_Light_And_Dark(bool dark, string background, string foreground, string accent)
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            await ApplyPixelAsync(dark);
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Background)).ShouldBe(Color.Parse(background));
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Foreground)).ShouldBe(Color.Parse(foreground));
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Accent)).ShouldBe(Color.Parse(accent));
            AsRadius(Find(host, ArcDesignTokenKind.RadiusControl)).ShouldBe(new CornerRadius(0));
            AsRadius(Find(host, ArcDesignTokenKind.RadiusPill)).ShouldBe(new CornerRadius(0));
            foreach (var kind in new[] { ArcDesignTokenKind.ShadowResting, ArcDesignTokenKind.ShadowRaised, ArcDesignTokenKind.ShadowFloating, ArcDesignTokenKind.ControlThumbShadow })
            {
                var shadows = (BoxShadows)Find(host, kind)!;
                shadows.Count.ShouldBe(1);
                shadows[0].Blur.ShouldBe(0);
                shadows[0].OffsetX.ShouldBe(Math.Round(shadows[0].OffsetX));
            }
            var font = Find(host, ArcDesignTokenKind.BodyFontFamily).ShouldBeOfType<FontFamily>();
            font.ToString().ShouldContain("VT323");
            Find(host, ArcDesignTokenKind.DisplayFontFamily).ShouldBeOfType<FontFamily>().ToString().ShouldContain("Silkscreen");
            Find(host, ArcDesignTokenKind.MotionMode).ShouldBe("full");
            Find(host, ArcDesignTokenKind.BlurSoft).ShouldBe(0.0);
            Find(host, ArcDesignTokenKind.ReduceTransparency).ShouldBe(true);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Pixel_Overrides_Control_Tokens_And_Button_Look()
    {
        var button = new Button { Content = "Save" };
        var window = TestHelpers.Show(button);
        try
        {
            await ApplyPixelAsync(false);
            button.CornerRadius.ShouldBe(new CornerRadius(0));
            TestHelpers.ColorOf(button.Background).ShouldBe(Color.Parse("#1A1C2C"));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Switching_Back_To_Default_Restores_The_Arc_Values()
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            await ApplyPixelAsync(false);
            AsRadius(Find(host, ArcDesignTokenKind.RadiusControl)).ShouldBe(new CornerRadius(0));
            await TestHelpers.ApplyAsync(null);
            AsRadius(Find(host, ArcDesignTokenKind.RadiusControl)).ShouldBe(new CornerRadius(18));
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Background)).ShouldNotBe(Color.Parse("#F4F4F4"));
            ((BoxShadows)Find(host, ArcDesignTokenKind.ShadowFloating)!)[0].Blur.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcPixelTheme.LightThemeId, "#F4F4F4")]
    [InlineData(ArcPixelTheme.DarkThemeId, "#1A1C2C")]
    public async Task Compiled_Theme_Ids_Apply_Without_A_Config(string themeId, string background)
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            var manager = Avalonia.Application.Current!.GetThemeManager()!;
            await manager.ApplyThemeAsync(new ThemeRequest(themeId, null, ThemeTransitionReason.UserRequest), TestContext.Current.CancellationToken);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Background)).ShouldBe(Color.Parse(background));
            AsRadius(Find(host, ArcDesignTokenKind.RadiusPanel)).ShouldBe(new CornerRadius(0));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Pixel_Reaches_Controls_Borders_Focus_Ring_Radii_And_Derived_Colors()
    {
        var panel = new StackPanel();
        var button = new Button { Content = "Save" };
        var split = new SplitButton();
        var input = new Input();
        var card = new Card();
        var toggle = new Switch();
        var badge = new Badge { Content = "New" };
        panel.Children.Add(button);
        panel.Children.Add(split);
        panel.Children.Add(input);
        panel.Children.Add(card);
        panel.Children.Add(toggle);
        panel.Children.Add(badge);
        var window = TestHelpers.Show(panel);
        try
        {
            var defaults = new[] { button.BorderThickness, input.BorderThickness, card.BorderThickness, toggle.BorderThickness, badge.BorderThickness };
            var focus = TestHelpers.Descendant<Border>(button, "PART_FocusRing");
            focus.BorderThickness.ShouldBe(new Thickness(2));
            var defaultRadii = new[] { button.CornerRadius, card.CornerRadius, badge.CornerRadius };
            defaultRadii.ShouldAllBe(r => r.TopLeft > 0);
            defaults.ShouldAllBe(t => t == new Thickness(1) || t == default);
            var defaultBadge = TestHelpers.ColorOf(badge.TryFindResource(BadgeTokenKind.SuccessBorder, badge.ActualThemeVariant, out var d) ? d : null);

            await ApplyPixelAsync(false);

            button.BorderThickness.ShouldBe(new Thickness(2));
            TestHelpers.Descendant<FieldFrame>(input).BorderThickness.ShouldBe(new Thickness(2));
            TestHelpers.Descendant<FieldFrame>(input).CornerRadius.ShouldBe(new CornerRadius(0));
            card.BorderThickness.ShouldBe(new Thickness(2));
            focus.BorderThickness.ShouldBe(new Thickness(3));
            foreach (var radius in new[] { button.CornerRadius, input.CornerRadius, card.CornerRadius, toggle.CornerRadius, badge.CornerRadius, focus.CornerRadius })
            {
                radius.ShouldBe(new CornerRadius(0));
            }
            var pixelBadge = TestHelpers.ColorOf(badge.TryFindResource(BadgeTokenKind.SuccessBorder, badge.ActualThemeVariant, out var p) ? p : null);
            pixelBadge.ShouldBe(Color.Parse("#257179"));
            pixelBadge.ShouldNotBe(defaultBadge);

            await TestHelpers.ApplyAsync(null);
            button.BorderThickness.ShouldBe(new Thickness(1));
            focus.BorderThickness.ShouldBe(new Thickness(2));
            card.CornerRadius.ShouldBe(defaultRadii[1]);
            badge.CornerRadius.ShouldBe(defaultRadii[2]);
            TestHelpers.ColorOf(badge.TryFindResource(BadgeTokenKind.SuccessBorder, badge.ActualThemeVariant, out var r) ? r : null).ShouldBe(defaultBadge);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Pixel_Zeroes_Radius_Steps_And_Thickens_One_Sided_Edges_And_Default_Is_Restored()
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            var steps = Enum.GetNames<ArcDesignTokenKind>().Where(n => n.StartsWith("RadiusStep", StringComparison.Ordinal)).Select(Enum.Parse<ArcDesignTokenKind>).ToArray();
            var edges = Enum.GetNames<ArcDesignTokenKind>().Where(n => n.StartsWith("EdgeThickness", StringComparison.Ordinal)).Select(Enum.Parse<ArcDesignTokenKind>).ToArray();
            steps.Length.ShouldBeGreaterThan(20);
            edges.Length.ShouldBe(9);

            await ApplyPixelAsync(false);
            foreach (var step in steps)
            {
                AsRadius(Find(host, step)).ShouldBe(new CornerRadius(0), step.ToString());
            }
            Find(host, ArcDesignTokenKind.EdgeThicknessTop).ShouldBe(new Thickness(0, 2, 0, 0));
            Find(host, ArcDesignTokenKind.EdgeThicknessRightBottom).ShouldBe(new Thickness(0, 0, 2, 2));
            Find(host, ArcDesignTokenKind.EdgeThicknessNoBottom).ShouldBe(new Thickness(2, 2, 2, 0));

            await TestHelpers.ApplyAsync(null);
            AsRadius(Find(host, ArcDesignTokenKind.RadiusStep12)).ShouldBe(new CornerRadius(12));
            AsRadius(Find(host, ArcDesignTokenKind.RadiusStep14p5)).ShouldBe(new CornerRadius(14.5));
            AsRadius(Find(host, ArcDesignTokenKind.RadiusStep19Top)).ShouldBe(new CornerRadius(19, 19, 0, 0));
            Find(host, ArcDesignTokenKind.EdgeThickness).ShouldBe(new Thickness(1));
            Find(host, ArcDesignTokenKind.EdgeThicknessTop).ShouldBe(new Thickness(0, 1, 0, 0));
            Find(host, ArcDesignTokenKind.EdgeThicknessNoLeft).ShouldBe(new Thickness(0, 1, 1, 1));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Code_Built_Parts_Follow_The_Radius_Tokens_Through_BindCornerRadius()
    {
        var border = new Border().BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        var pill = new Border().BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        var glide = new TKCode.Arc.Primitives.GlidingIndicator().BindCornerRadius(ArcDesignTokenKind.RadiusStep18);
        var window = TestHelpers.Show(new StackPanel { Children = { border, pill, glide } });
        try
        {
            border.CornerRadius.ShouldBe(new CornerRadius(12));
            pill.CornerRadius.ShouldBe(new CornerRadius(9999));
            glide.CornerRadius.ShouldBe(new CornerRadius(18));

            await ApplyPixelAsync(false);
            border.CornerRadius.ShouldBe(new CornerRadius(0));
            pill.CornerRadius.ShouldBe(new CornerRadius(0));
            glide.CornerRadius.ShouldBe(new CornerRadius(0));

            await TestHelpers.ApplyAsync(null);
            border.CornerRadius.ShouldBe(new CornerRadius(12));
            glide.CornerRadius.ShouldBe(new CornerRadius(18));
            Should.Throw<InvalidOperationException>(() => new TextBlock().BindCornerRadius(ArcDesignTokenKind.RadiusStep12));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void Rendering_Helper_Sets_And_Clears_Hard_Edges()
    {
        var host = new Border();
        ArcPixelRendering.Apply(host, true);
        RenderOptions.GetEdgeMode(host).ShouldBe(EdgeMode.Aliased);
        RenderOptions.GetBitmapInterpolationMode(host).ShouldBe(Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        ArcPixelRendering.Apply(host, false);
        RenderOptions.GetEdgeMode(host).ShouldBe(EdgeMode.Unspecified);
        RenderOptions.GetBitmapInterpolationMode(host).ShouldBe(Avalonia.Media.Imaging.BitmapInterpolationMode.Unspecified);
    }

    [Fact]
    public void Config_Without_A_Package_Scope_Names_Only_The_Core_Controls()
    {
        ArcPixelTheme.CreateConfig(false, ArcPixelScope.None).Controls.Count.ShouldBe(1);
        ArcPixelTheme.CreateConfig(false).Controls.Count.ShouldBeGreaterThan(50);
    }

    [Fact]
    public void Package_Has_No_Controls_And_Only_References_Core()
    {
        var assembly = typeof(ArcPixelTheme).Assembly;
        assembly.GetExportedTypes().Any(t => typeof(Control).IsAssignableFrom(t)).ShouldBeFalse();
        assembly.GetReferencedAssemblies().Any(a => a.Name == "TKCode.Arc").ShouldBeFalse();
        ArcPixelTheme.LightThemeId.ShouldStartWith("Arc");
        ArcPixelTheme.DarkThemeId.ShouldStartWith("Arc");
    }

    private static CornerRadius AsRadius(object? value) => value switch
    {
        CornerRadius radius => radius,
        _ => throw new InvalidCastException(value?.GetType().ToString())
    };

    private static object? Find(Control host, ArcDesignTokenKind kind)
        => host.TryFindResource(kind, host.ActualThemeVariant, out var value) ? value : null;
}
