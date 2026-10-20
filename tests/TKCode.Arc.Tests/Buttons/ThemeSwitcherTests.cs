using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Buttons;

public class ThemeSwitcherTests
{
    private static Button InnerButton(ThemeSwitcher switcher) => Part<Button>(switcher, ThemeSwitcher.ButtonPart);

    [AvaloniaFact]
    public void Defaults_To_A_Small_Secondary_Button_Naming_The_Next_Theme()
    {
        var switcher = new ThemeSwitcher();
        var window = TestHelpers.Show(switcher);
        try
        {
            var button = InnerButton(switcher);
            button.Variant.ShouldBe(ButtonVariant.Secondary);
            button.Size.ShouldBe(ControlSize.Small);
            button.Bounds.Height.ShouldBe(36, 0.5);
            switcher.Variant.ShouldBe(ThemeSwitcherVariant.Reveal);
            switcher.EffectiveAppearance.ShouldBe(ThemeSwitcherAppearance.Light);
            AutomationProperties.GetName(button).ShouldBe("Switch to dark mode");
            Part<TextBlock>(switcher, "PART_Text").Text.ShouldBe("Switch theme");
            var toggle = Peer(button).GetProvider<IToggleProvider>();
            toggle.ShouldNotBeNull();
            toggle!.ToggleState.ShouldBe(ToggleState.Off);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Icon_Only_Is_A_38px_Square()
    {
        var switcher = new ThemeSwitcher { IsIconOnly = true, Appearance = ThemeSwitcherAppearance.Dark };
        var window = TestHelpers.Show(switcher);
        try
        {
            var button = InnerButton(switcher);
            button.Bounds.Width.ShouldBe(38, 0.5);
            button.Bounds.Height.ShouldBe(38, 0.5);
            Part<TextBlock>(switcher, "PART_Text").IsVisible.ShouldBeFalse();
            switcher.Classes.ShouldContain(":dark");
            AutomationProperties.GetName(button).ShouldBe("Switch to light mode");
            Peer(button).GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handled_Request_Leaves_The_Theme_To_The_App()
    {
        ThemeSwitcherChangeRequestedEventArgs? request = null;
        var switcher = new ThemeSwitcher { Appearance = ThemeSwitcherAppearance.Light, Variant = ThemeSwitcherVariant.Split };
        switcher.ThemeChangeRequested += (_, e) =>
        {
            request = e;
            e.Handled = true;
        };
        var window = TestHelpers.Show(switcher);
        try
        {
            InnerButton(switcher).Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            request.ShouldNotBeNull();
            request!.Next.ShouldBe(ThemeSwitcherAppearance.Dark);
            request.Variant.ShouldBe(ThemeSwitcherVariant.Split);
            request.Trigger.ShouldBe(InnerButton(switcher));
            request.Origin.X.ShouldBeGreaterThan(0);
            switcher.Appearance.ShouldBe(ThemeSwitcherAppearance.Light);
            window.ActualThemeVariant.ShouldBe(ThemeVariant.Light);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_Unhandled_Request_Switches_The_App_Through_The_Theme_Manager()
    {
        var switcher = new ThemeSwitcher();
        var window = TestHelpers.Show(switcher);
        try
        {
            InnerButton(switcher).Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            for (var i = 0; i < 200 && switcher.ActualThemeVariant != ThemeVariant.Dark; i++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }
            switcher.ActualThemeVariant.ShouldBe(ThemeVariant.Dark);
            switcher.EffectiveAppearance.ShouldBe(ThemeSwitcherAppearance.Dark);
            AutomationProperties.GetName(InnerButton(switcher)).ShouldBe("Switch to light mode");
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaTheory]
    [InlineData(1.0, 320, 200)]
    [InlineData(2.0, 640, 400)]
    [InlineData(1.5, 480, 300)]
    public void The_Snapshot_Draws_The_Whole_Bitmap_On_Scaled_Displays(double scaling, int pixelWidth, int pixelHeight)
    {
        // The snapshot is taken at the top level's render scaling, so its DIP size (320x200) is smaller than its pixel
        // size on a scaled display; the draw source must be the pixels or only the top-left part of the window shows.
        using var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96 * scaling, 96 * scaling));
        bitmap.Size.ShouldBe(new Size(320, 200));
        var source = ThemeSwitcherSnapshot.SourceRect(bitmap);
        source.ShouldBe(new Rect(0, 0, pixelWidth, pixelHeight));
    }

    [AvaloniaFact]
    public void Theme_Config_Keeps_The_Rest_Of_The_Theme_And_Toggles_Dark()
    {
        var previous = new ThemeConfigBuilder()
                       .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Compact)
                       .WithToken("FontSize", "15")
                       .WithArc(arc => arc.Accent(ArcAccentPreset.Violet))
                       .Build();
        var dark = ThemeSwitcherThemeApplier.Build(previous, null, dark: true, accent: null);
        dark.Algorithms.ShouldBe([ThemeAlgorithm.Default, ThemeAlgorithm.Compact, ThemeAlgorithm.Dark]);
        dark.Tokens["FontSize"].ShouldBe("15");
        dark.Controls.Count.ShouldBe(previous.Controls.Count);
        var light = ThemeSwitcherThemeApplier.Build(dark, null, dark: false, accent: null);
        light.Algorithms.ShouldBe([ThemeAlgorithm.Default, ThemeAlgorithm.Compact]);

        // Before any request was observed, the current accent preset is carried over.
        var fresh = ThemeSwitcherThemeApplier.Build(null, [ThemeAlgorithm.Default], dark: true, accent: ArcAccentPreset.Blue);
        fresh.Algorithms.ShouldBe([ThemeAlgorithm.Default, ThemeAlgorithm.Dark]);
        fresh.Controls.Count.ShouldBe(1);
    }
}
