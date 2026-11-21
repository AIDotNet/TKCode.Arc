using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Theme;

/// <summary>The typed font family methods of <see cref="ArcThemeBuilderExtensions"/> reach Arc controls (#173).</summary>
public class FontFamilyBuilderTests
{
    [AvaloniaTheory]
    [InlineData("Fira Code")]
    [InlineData("Fira Code, Courier New, $Default")]
    [InlineData("fonts:Inter#Inter, $Default")]
    [InlineData("avares://X/Assets#Foo, Bar")]
    public void Family_Text_Survives_The_Token_Round_Trip(string text)
    {
        var state = ArcThemeState.Default.With(b => b.BodyFontFamily(new FontFamily(text)));
        state.TokenOverrides[ArcThemeState.GlobalIdentity]["BodyFontFamily"].ShouldBe(text);
    }

    [AvaloniaFact]
    public async Task Body_Font_Family_Reaches_Arc_Controls_And_Keeps_The_Fallback_Chain()
    {
        var chain = new FontFamily("Fira Code, Courier New, $Default");
        await TestHelpers.ApplyAsync(t => t.BodyFontFamily(chain));
        var button = new Button { Content = "Save" };
        var window = TestHelpers.Show(button);
        try
        {
            button.FontFamily.FamilyNames.Select(n => n).ToArray().ShouldBe(chain.FamilyNames.Select(n => n).ToArray());
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void Family_Methods_Write_The_Matching_Tokens()
    {
        var family = new FontFamily("Fira Code");
        var state = ArcThemeState.Default.With(b => b.FontFamily(family).MonoFontFamily(family));
        var tokens = state.TokenOverrides[ArcThemeState.GlobalIdentity];
        tokens["BodyFontFamily"].ShouldBe("Fira Code");
        tokens["DisplayFontFamily"].ShouldBe("Fira Code");
        tokens["MonoFontFamily"].ShouldBe("Fira Code");
    }

    [AvaloniaFact]
    public void A_Resource_On_A_Panel_Overrides_The_Body_Family_For_Its_Subtree_Only()
    {
        var local = new FontFamily("Fira Code");
        var inside = new Button { Content = "Inside" };
        var outside = new Button { Content = "Outside" };
        var panel = new StackPanel { Children = { inside } };
        panel.Resources[ArcDesignTokens.Key(ArcDesignTokenKind.BodyFontFamily)] = local;
        var window = TestHelpers.Show(new StackPanel { Children = { panel, outside } });
        try
        {
            inside.FontFamily.FamilyNames[0].ShouldBe("Fira Code");
            outside.FontFamily.FamilyNames[0].ShouldNotBe("Fira Code");
        }
        finally
        {
            window.Close();
        }
    }
}
