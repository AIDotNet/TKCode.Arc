using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
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

    [AvaloniaFact]
    public async Task Content_Widths_And_Media_Sizes_Resolve_From_Xaml_And_Can_Be_Overridden()
    {
        // Issue #110: settings pages and QR codes had to hard-code their widths.
        var page = (StackPanel)AvaloniaRuntimeXamlLoader.Load(
            """
            <StackPanel xmlns="https://github.com/avaloniaui"
                        xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                        MaxWidth="{arc:ArcDesignTokenResource ContentMaxWidthMD}">
                <Border Width="{arc:ArcDesignTokenResource MediaSizeLG}"
                        Height="{arc:ArcDesignTokenResource MediaSizeLG}" />
            </StackPanel>
            """);
        var window = TestHelpers.Show(page);
        try
        {
            page.MaxWidth.ShouldBe(760);
            var qr = (Avalonia.Controls.Border)page.Children[0];
            qr.Width.ShouldBe(192);
            qr.Height.ShouldBe(192);

            ArcTokens.Get<double>(page, ArcDesignTokenKind.ContentMaxWidthSM).ShouldBe(640);
            ArcTokens.Get<double>(page, ArcDesignTokenKind.ContentMaxWidthLG).ShouldBe(880);
            ArcTokens.Get<double>(page, ArcDesignTokenKind.ContentMaxWidthXL).ShouldBe(1120);
            ArcTokens.Get<double>(page, ArcDesignTokenKind.MediaSizeSM).ShouldBe(64);
            ArcTokens.Get<double>(page, ArcDesignTokenKind.MediaSizeMD).ShouldBe(128);
            ArcTokens.Get<double>(page, ArcDesignTokenKind.MediaSizeXL).ShouldBe(256);

            await TestHelpers.ApplyAsync(t => t.Token(nameof(ArcDesignTokenKind.ContentMaxWidthMD), "680"));
            page.MaxWidth.ShouldBe(680);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void The_Width_And_Media_Tokens_Are_Appended_So_Earlier_Kinds_Keep_Their_Numbers()
    {
        // ArcDesignTokenKind numbers tokens in declaration order; code compiled against an earlier version keeps its numbers.
        var kinds = Enum.GetValues<ArcDesignTokenKind>();
        kinds[^8..].ShouldBe(
        [
            ArcDesignTokenKind.ContentMaxWidthSM, ArcDesignTokenKind.ContentMaxWidthMD, ArcDesignTokenKind.ContentMaxWidthLG,
            ArcDesignTokenKind.ContentMaxWidthXL, ArcDesignTokenKind.MediaSizeSM, ArcDesignTokenKind.MediaSizeMD,
            ArcDesignTokenKind.MediaSizeLG, ArcDesignTokenKind.MediaSizeXL
        ]);
        ((int)ArcDesignTokenKind.ContentMaxWidthSM).ShouldBe((int)ArcDesignTokenKind.ReduceTransparency + 1);
    }

    private static object? Find(Avalonia.Controls.Control host, ArcDesignTokenKind kind)
        => host.TryFindResource(kind, host.ActualThemeVariant, out var value) ? value : null;
}
