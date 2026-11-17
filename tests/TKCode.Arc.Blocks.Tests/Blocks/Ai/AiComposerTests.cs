using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The composer's frame, text and value, placeholder, sizes and breakpoints.</summary>
public class AiComposerTests
{
    #region Look and geometry

    [AvaloniaFact]
    public void An_Empty_Composer_Is_A_100px_Prompt_Box_With_A_36px_Toolbar()
    {
        // A short hint: the headless test font is wide, and a hint that wraps makes the box taller (see below).
        using var rig = new AiComposerRig(c => c.Placeholder = "Ask");
        var composer = rig.Composer;
        var surface = composer.Surface!;
        surface.CornerRadius.ShouldBe(new CornerRadius(26));
        surface.BorderThickness.ShouldBe(new Thickness(1));
        surface.Padding.ShouldBe(new Thickness(10));
        // 1 + 10 + 40 (one 24px line + 2 × 8) + 2 + 36 + 10 + 1 (spec 3.3)
        composer.Bounds.Height.ShouldBe(100, 0.5);
        rig.Editor.Bounds.Height.ShouldBe(40, 0.5);
        composer.PlusButton!.Bounds.Size.ShouldBe(new Size(36, 36));
        rig.Send.Bounds.Size.ShouldBe(new Size(36, 36));
        composer.SendState.ShouldBe(AiSendState.Empty);
        composer.ToolToggles.Count.ShouldBe(2);
        composer.ModelPillControl!.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task The_Surface_Takes_Its_Colors_From_The_Theme_And_Darkens_Its_Border_On_Hover_And_Focus()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        Color Token(ArcDesignTokenKind kind) => ArcTokens.GetColor(composer, kind);
        Color Border() => TestHelpers.ColorOf(composer.Surface!.BorderBrush);
        TestHelpers.ColorOf(composer.Surface!.Background).ShouldBe(Token(ArcDesignTokenKind.SurfaceRaised));
        Border().ShouldBe(Token(ArcDesignTokenKind.Border));
        rig.Window.MouseMove(rig.CenterOf(composer.Editor!), RawInputModifiers.None);
        rig.Pump();
        Border().ShouldBe(Oklch.MixOklab(Token(ArcDesignTokenKind.Border), Token(ArcDesignTokenKind.BorderStrong), 0.45));
        rig.Focus();
        Border().ShouldBe(Token(ArcDesignTokenKind.BorderStrong));

        var light = TestHelpers.ColorOf(composer.Surface.Background);
        await TestHelpers.ApplyAsync(TestHelpers.Dark);
        try
        {
            var dark = TestHelpers.ColorOf(composer.Surface.Background);
            dark.ShouldNotBe(light);
            dark.ShouldBe(Token(ArcDesignTokenKind.SurfaceRaised));
            Border().ShouldBe(Token(ArcDesignTokenKind.BorderStrong));
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void The_Text_Area_Grows_A_Line_At_A_Time_To_Eight_Lines_Then_Scrolls()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.TypeEach("one\ntwo\nthree");
        rig.Editor.Bounds.Height.ShouldBe(40 + 2 * 24, 0.5);
        rig.Composer.Bounds.Height.ShouldBe(100 + 2 * 24, 0.5);
        rig.TypeEach("\n4\n5\n6\n7\n8\n9\n10");
        // 8 lines: 8 × 24 + 16 = 208, then the text scrolls inside.
        rig.Editor.Bounds.Height.ShouldBe(208, 0.5);
    }

    [AvaloniaFact]
    public void MinLines_And_MaxLines_Reach_The_Text_Area()
    {
        using var rig = new AiComposerRig(c =>
        {
            c.MinLines = 2;
            c.MaxLines = 3;
        });
        rig.Editor.MinLines.ShouldBe(2);
        rig.Editor.MaxLines.ShouldBe(3);
        rig.Editor.Bounds.Height.ShouldBe(2 * 24 + 16, 0.5);
        rig.Focus();
        rig.TypeEach("1\n2\n3\n4\n5");
        rig.Editor.Bounds.Height.ShouldBe(3 * 24 + 16, 0.5);
    }

    [AvaloniaFact]
    public void A_Hint_That_Wraps_Grows_The_Empty_Box_So_It_Shows_Whole()
    {
        using var rig = new AiComposerRig(c => c.Placeholder = "Ask", width: 320);
        rig.Editor.Bounds.Height.ShouldBe(40, 0.5);
        rig.Composer.Placeholder = string.Join(" ", Enumerable.Repeat("hint", 20));
        rig.Pump();
        rig.Editor.Bounds.Height.ShouldBeGreaterThan(64 - 0.5);
        rig.Focus();
        rig.Type("a");
        rig.Editor.Bounds.Height.ShouldBe(40, 0.5);
    }

    #endregion

    #region Text and value

    [AvaloniaFact]
    public void Typing_Writes_Text_And_Value()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("Plan the release notes");
        rig.Composer.Text.ShouldBe("Plan the release notes");
        rig.Composer.Value.Text.ShouldBe("Plan the release notes");
        rig.Composer.Value.Tokens.ShouldBeEmpty();
        rig.Composer.SendState.ShouldBe(AiSendState.Ready);
    }

    [AvaloniaFact]
    public void Setting_Text_Or_Value_Updates_The_Editor_With_Its_Tokens()
    {
        using var rig = new AiComposerRig();
        rig.Composer.Text = "hello";
        rig.Pump();
        rig.Editor.Text.ShouldBe("hello");
        rig.Composer.Value.Text.ShouldBe("hello");

        var tokens = new[] { new AiPromptToken(AiComposerTokenKind.Command, "recap", "recap", 0, 6) };
        rig.Composer.Value = new AiComposerValue("/recap the review", tokens);
        rig.Pump();
        rig.Editor.Text.ShouldBe("/recap the review");
        rig.Editor.Tokens.Single().ShouldBe(new TokenSpan(AiComposer.CommandKind, "recap", "recap", 0, 6));
        rig.Composer.Text.ShouldBe("/recap the review");
        rig.Composer.Value.Tokens.Single().Kind.ShouldBe(AiComposerTokenKind.Command);
    }

    [AvaloniaFact]
    public void A_Value_Set_Before_The_Template_Reaches_The_Editor()
    {
        var tokens = new[] { new AiPromptToken(AiComposerTokenKind.Mention, "doc-sprint-15", "Sprint 15 review", 5, 22) };
        using var rig = new AiComposerRig(c => c.Value = new AiComposerValue("Read @Sprint 15 review first", tokens));
        rig.Composer.Text.ShouldBe("Read @Sprint 15 review first");
        rig.Editor.Text.ShouldBe("Read @Sprint 15 review first");
        rig.Editor.Tokens.Single().Id.ShouldBe("doc-sprint-15");
    }

    [AvaloniaFact]
    public void Text_Set_Without_A_Template_Keeps_The_Tokens_It_Still_Holds()
    {
        var composer = new AiComposer
        {
            Value = new AiComposerValue("/recap notes", new[] { new AiPromptToken(AiComposerTokenKind.Command, "recap", "recap", 0, 6) })
        };
        composer.Text = "/recap notes and more";
        composer.Value.Tokens.Count.ShouldBe(1);
        composer.Text = "/reca notes";
        composer.Value.Tokens.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_Placeholder_Falls_Back_To_The_Labels()
    {
        using var rig = new AiComposerRig(c => c.Placeholder = null);
        rig.Editor.Placeholder.ShouldBe(new AiComposerLabels().Placeholder);
        rig.Composer.Placeholder = "Ask me";
        rig.Editor.Placeholder.ShouldBe("Ask me");
    }

    [AvaloniaFact]
    public void The_Text_Area_Is_Named_And_Explains_Its_Keys()
    {
        using var rig = new AiComposerRig();
        AutomationProperties.GetName(rig.Editor).ShouldBe("Message");
        AutomationProperties.GetHelpText(rig.Editor).ShouldBe(new AiComposerLabels().KeyHint);
        rig.Composer.AccessibleName = "Prompt";
        AutomationProperties.GetName(rig.Editor).ShouldBe("Prompt");
    }

    #endregion

    #region Keyboard order

    [AvaloniaFact]
    public void Tab_Goes_Text_Plus_Tools_Model_Send_And_Back()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        rig.Focus();
        var expected = new Avalonia.Input.IInputElement[]
        {
            composer.PlusButton!, composer.ToolToggles[0], composer.ToolToggles[1], composer.ModelPillControl!, rig.Send
        };
        foreach (var element in expected)
        {
            rig.Key(PhysicalKey.Tab);
            rig.Focused.ShouldBe(element);
        }
        for (var i = expected.Length - 2; i >= 0; i--)
        {
            rig.Key(PhysicalKey.Tab, RawInputModifiers.Shift);
            rig.Focused.ShouldBe(expected[i]);
        }
        rig.Key(PhysicalKey.Tab, RawInputModifiers.Shift);
        rig.EditorFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Empty_Send_Button_Can_Still_Take_The_Focus()
    {
        using var rig = new AiComposerRig();
        rig.Send.Focus(NavigationMethod.Tab).ShouldBeTrue();
        rig.Composer.SendState.ShouldBe(AiSendState.Empty);
    }

    #endregion

    #region Breakpoints

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Composers_Own_Width()
    {
        using var rig = new AiComposerRig(c => c.Placeholder = "Ask", width: 745);
        var composer = rig.Composer;
        composer.IsCompact.ShouldBeFalse();
        composer.ToolToggles.ShouldAllBe(t => !t.IsCompact);
        composer.ModelPillControl!.ShowEffort.ShouldBeTrue();

        rig.Window.Width = 600;
        rig.Pump();
        composer.IsCompact.ShouldBeTrue();
        composer.Classes.Contains(":compact").ShouldBeTrue();
        composer.ToolToggles.ShouldAllBe(t => t.IsCompact);
        composer.ModelPillControl.ShowEffort.ShouldBeTrue();

        rig.Window.Width = 500;
        rig.Pump();
        composer.IsNarrow.ShouldBeTrue();
        composer.ModelPillControl.ShowEffort.ShouldBeFalse();
        composer.ModelPillControl.ShowText.ShouldBeTrue();

        rig.Window.Width = 360;
        rig.Pump();
        composer.IsTiny.ShouldBeTrue();
        composer.ModelPillControl.ShowText.ShouldBeFalse();
        composer.Counter!.IsTiny.ShouldBeTrue();
        composer.Surface!.Padding.ShouldBe(new Thickness(8));
        composer.Bounds.Height.ShouldBe(96, 0.5);

        rig.Window.Width = 745;
        rig.Pump();
        composer.IsCompact.ShouldBeFalse();
        composer.IsTiny.ShouldBeFalse();
        composer.Surface.Padding.ShouldBe(new Thickness(10));
    }

    [AvaloniaFact]
    public void At_320_The_Toolbar_Fits_On_One_Row_Without_Overlap()
    {
        using var rig = new AiComposerRig(width: 320);
        var composer = rig.Composer;
        var plus = composer.PlusButton!;
        var right = rig.Send.Parent as Control;
        var lastTool = composer.ToolToggles[^1];
        var toolRight = lastTool.TranslatePoint(new Point(lastTool.Bounds.Width, 0), composer)!.Value.X;
        var rightLeft = right!.TranslatePoint(default, composer)!.Value.X;
        toolRight.ShouldBeLessThan(rightLeft);
        plus.TranslatePoint(default, composer)!.Value.Y.ShouldBe(rig.Send.TranslatePoint(default, composer)!.Value.Y, 0.5);
    }

    #endregion
}
