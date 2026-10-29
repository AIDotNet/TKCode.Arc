using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Theme;

/// <summary>S0.2: the ArcDesign carrier token resolves for any element and follows appearance and accent.</summary>
public class ArcDesignTokenTests
{
    [AvaloniaFact]
    public async Task Primary_Button_Uses_Foreground_Fill_In_Light_And_Dark()
    {
        await TestHelpers.ApplyAsync(null);
        var button = new Button { Content = "Save changes" };
        var window = TestHelpers.Show(button);
        try
        {
            TestHelpers.ColorOf(button.Background).ShouldBe(Color.Parse("#0B0B0B"));
            TestHelpers.ColorOf(button.Foreground).ShouldBe(Color.Parse("#FFFFFF"));

            await TestHelpers.ApplyAsync(TestHelpers.Dark);
            TestHelpers.ColorOf(button.Background).ShouldBe(Color.Parse("#F2F2F2"));
            TestHelpers.ColorOf(button.Foreground).ShouldBe(Color.Parse("#141414"));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Accent_Override_On_ArcDesign_Resolves_The_Preset_In_Both_Appearances()
    {
        var host = new Avalonia.Controls.Border();
        var window = TestHelpers.Show(host);
        try
        {
            await TestHelpers.ApplyAsync(t => t.Accent(ArcAccentPreset.Violet));
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Accent)).ShouldBe(Color.Parse("#7747FF"));

            await TestHelpers.ApplyAsync(t => t.Appearance(ArcAppearance.Dark).Accent(ArcAccentPreset.Violet));
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Accent)).ShouldBe(Color.Parse("#A78BFF"));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Root_ColorPrimary_Maps_Back_To_Preset_In_Dark()
    {
        var host = new Avalonia.Controls.Border();
        var window = TestHelpers.Show(host);
        try
        {
            await TestHelpers.ApplyAsync(t => t.Appearance(ArcAppearance.Dark).Accent(Color.Parse("#0562EF")));
            TestHelpers.ColorOf(Find(host, ArcDesignTokenKind.Accent)).ShouldBe(Color.Parse("#6BA3FF"));
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    private static object? Find(Avalonia.Controls.Control host, ArcDesignTokenKind kind)
        => host.TryFindResource(kind, host.ActualThemeVariant, out var value) ? value : null;
}
