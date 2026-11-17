using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The prompt studio's model pill: mark, short name and effort, its look, and how a new model or effort moves.</summary>
public class PromptModelPillTests
{
    private static PromptModelPill Create(AiModel? model, ModelEffort? effort = ModelEffort.High) => new()
    {
        Model = model,
        Effort = effort,
        EffortText = ModelEffortNames.Name
    };

    private static ArcContentSwap Swap(PromptModelPill pill, string part) => TestHelpers.Descendant<ArcContentSwap>(pill, part);

    [AvaloniaFact]
    public void It_Shows_The_Mark_The_Short_Name_And_The_Effort()
    {
        var models = PromptMenuSamples.Models();
        var pill = Create(models[1]);
        PromptVersionPillTests.Show(pill);
        pill.Bounds.Height.ShouldBe(32);
        pill.Padding.ShouldBe(new Thickness(9, 0));
        pill.CurrentMark!.Size.ShouldBe(14);
        pill.CurrentMark.Mark.ShouldBe(models[1].Mark);
        pill.CurrentMark.Margin.ShouldBe(new Thickness(0, 0, 6, 0));
        pill.CurrentName!.Text.ShouldBe("Core 2");
        pill.CurrentName.IsSecondary.ShouldBeFalse();
        pill.CurrentName.FontWeight.ShouldBe(FontWeight.Medium);
        pill.CurrentEffort!.Text.ShouldBe("High");
        pill.CurrentEffort.IsSecondary.ShouldBeTrue();
        TestHelpers.ColorOf(pill.CurrentEffort.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.TextSecondary));
        pill.CurrentEffort.Margin.ShouldBe(new Thickness(6, 0, 0, 0));
        AutomationProperties.GetName(pill).ShouldBe("Atlas Core 2, High");
    }

    [AvaloniaFact]
    public void The_Effort_Word_Comes_From_Effort_Text()
    {
        var pill = Create(PromptMenuSamples.Models()[0], ModelEffort.ExtraHigh);
        pill.EffortText = PromptMenuSamples.Cell;
        PromptVersionPillTests.Show(pill);
        pill.CurrentEffort!.Text.ShouldBe("XHigh");
        pill.ShownEffortText.ShouldBe("XHigh");
    }

    [AvaloniaFact]
    public void A_Model_Without_Effort_Levels_Or_No_Effort_Shows_No_Effort()
    {
        var models = PromptMenuSamples.Models();
        var pill = Create(models[4]);
        PromptVersionPillTests.Show(pill);
        pill.CurrentEffort.ShouldBeNull();
        pill.CurrentName!.Text.ShouldBe("Nova Flash");
        AutomationProperties.GetName(pill).ShouldBe("Nova Flash");
        pill.Model = models[0];
        pill.CurrentEffort!.Text.ShouldBe("High");
        pill.Effort = null;
        pill.CurrentEffort.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Provider_Supplies_A_Missing_Mark()
    {
        var hexagon = Geometry.Parse("M12 2 L21 7 L21 17 L12 22 L3 17 L3 7 Z");
        var providers = new AvaloniaList<AiModelProvider>();
        var model = new AiModel { Id = "x", Name = "Basalt 4", ProviderId = "basalt", EffortLevels = [ModelEffort.Low] };
        var pill = Create(model, ModelEffort.Low);
        pill.Providers = providers;
        PromptVersionPillTests.Show(pill);
        pill.CurrentMark.ShouldBeNull();
        providers.Add(new AiModelProvider { Id = "basalt", Name = "Basalt", Mark = hexagon });
        pill.CurrentMark!.Mark.ShouldBe(hexagon);
    }

    [AvaloniaFact]
    public void The_Name_Format_Names_The_Pill_With_The_Full_Name()
    {
        var pill = Create(PromptMenuSamples.Models()[2], ModelEffort.Medium);
        pill.NameFormat = (model, effort) => effort is null ? $"Model: {model}" : $"Model: {model}, {effort} effort";
        PromptVersionPillTests.Show(pill);
        AutomationProperties.GetName(pill).ShouldBe("Model: Atlas Lite 2, Medium effort");
        pill.Model = PromptMenuSamples.Models()[4];
        AutomationProperties.GetName(pill).ShouldBe("Model: Nova Flash");
    }

    [AvaloniaFact]
    public void Open_Tints_It_And_A_Press_Asks_To_Open()
    {
        var pill = Create(PromptMenuSamples.Models()[0]);
        var window = PromptVersionPillTests.Show(pill);
        var raised = PromptVersionPillTests.Record(pill);
        pill.IsOpen = true;
        TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.BorderStrong));
        window.MouseDown(PromptVersionPillTests.CenterOf(pill, window), MouseButton.Left, RawInputModifiers.None);
        raised.Count.ShouldBe(1);
        raised[0].Focus.ShouldBe(ModelMenuOpenFocus.Surface);
    }

    [AvaloniaFact]
    public void A_New_Effort_Crossfades_Only_The_Effort_Word()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var pill = Create(PromptMenuSamples.Models()[0], ModelEffort.Low);
        var window = PromptVersionPillTests.Show(pill, ArcMotionLevel.Full);
        CoreFrames.Run(clock, 10);
        PromptVersionPillTests.Settle(window);
        var name = pill.CurrentName;
        var mark = pill.CurrentMark;
        var before = pill.TextSize!.Bounds.Width;

        pill.Effort = ModelEffort.ExtraHigh;
        PromptVersionPillTests.Settle(window);
        pill.CurrentName.ShouldBeSameAs(name);
        pill.CurrentMark.ShouldBeSameAs(mark);
        PromptVersionPillTests.Layers(Swap(pill, PromptModelPill.EffortPart)).ShouldBe(2);
        PromptVersionPillTests.Layers(Swap(pill, PromptModelPill.NamePart)).ShouldBe(1);
        CoreFrames.Run(clock, 3);
        PromptVersionPillTests.Settle(window);
        var moving = pill.TextSize.Bounds.Width;
        moving.ShouldBeGreaterThan(before);
        CoreFrames.Run(clock, 40);
        PromptVersionPillTests.Settle(window);
        pill.TextSize.Bounds.Width.ShouldBeGreaterThan(moving);
        PromptVersionPillTests.WaitFor(() => PromptVersionPillTests.Layers(Swap(pill, PromptModelPill.EffortPart)) == 1).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_New_Model_Crossfades_The_Mark_The_Name_And_The_Effort()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var models = PromptMenuSamples.Models();
        var pill = Create(models[0], ModelEffort.High);
        var window = PromptVersionPillTests.Show(pill, ArcMotionLevel.Full);
        CoreFrames.Run(clock, 10);
        PromptVersionPillTests.Settle(window);
        pill.Model = models[3];
        pill.Effort = ModelEffort.Low;
        PromptVersionPillTests.Settle(window);
        pill.CurrentName!.Text.ShouldBe("Comet Pro");
        pill.CurrentEffort!.Text.ShouldBe("Low");
        PromptVersionPillTests.Layers(Swap(pill, PromptModelPill.MarkPart)).ShouldBe(2);
        PromptVersionPillTests.Layers(Swap(pill, PromptModelPill.NamePart)).ShouldBe(2);
        PromptVersionPillTests.Layers(Swap(pill, PromptModelPill.EffortPart)).ShouldBe(2);
        Swap(pill, PromptModelPill.MarkPart).Mode.ShouldBe(ContentSwapMode.Crossfade);
        Swap(pill, PromptModelPill.NamePart).Mode.ShouldBe(ContentSwapMode.Label);
        CoreFrames.Run(clock, 40);
        PromptVersionPillTests.Settle(window);
        PromptVersionPillTests.WaitFor(() => PromptVersionPillTests.Layers(Swap(pill, PromptModelPill.NamePart)) == 1).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Losing_The_Effort_Fades_It_Out_And_Narrows_The_Pill()
    {
        var models = PromptMenuSamples.Models();
        var pill = Create(models[0], ModelEffort.High);
        var window = PromptVersionPillTests.Show(pill);
        var with = pill.TextSize!.Bounds.Width;
        pill.Model = models[4];
        PromptVersionPillTests.Settle(window);
        pill.CurrentEffort.ShouldBeNull();
        Swap(pill, PromptModelPill.EffortPart).Content.ShouldBeNull();
        pill.TextSize.Bounds.Width.ShouldBeLessThan(with);
    }

    [AvaloniaFact]
    public void Model_Edits_Reach_The_Pill_While_It_Is_Shown()
    {
        var model = PromptMenuSamples.Models()[3];
        var pill = Create(model, ModelEffort.Low);
        PromptVersionPillTests.Show(pill);
        model.ShortName = "Comet";
        pill.CurrentName!.Text.ShouldBe("Comet");
        model.EffortLevels = null;
        pill.CurrentEffort.ShouldBeNull();
    }
}
