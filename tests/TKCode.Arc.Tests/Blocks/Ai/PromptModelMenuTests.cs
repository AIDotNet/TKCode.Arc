using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The prompt studio's model menu: rows and prices, the effort row, choices that keep it open, keys and motion.</summary>
public class PromptModelMenuTests
{
    private static PromptMenuHost Show(PromptModelMenu? menu = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        menu ??= PromptMenuSamples.ModelMenu();
        var pill = PromptMenuSamples.ModelPill(menu);
        return PromptMenuHost.Show(pill, menu, level, HorizontalAlignment.Right);
    }

    private static PromptMenuHost Open(PromptModelMenu? menu = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var host = Show(menu, level);
        host.Click(host.Pill);
        return host;
    }

    private static PromptModelRow Row(PromptMenuHost host, int index) => host.ModelMenu.Rows[index];

    private static PromptEffortCell Cell(PromptMenuHost host, ModelEffort level) => host.ModelMenu.Strip!.CellOf(level)!;

    #region Opening and rows

    [AvaloniaFact]
    public void It_Opens_320_Wide_Right_Aligned_6px_Below_The_Pill()
    {
        using var host = Open();
        var menu = host.ModelMenu;
        menu.IsOpen.ShouldBeTrue();
        host.Pill.IsOpen.ShouldBeTrue();
        var surface = menu.Surface!;
        surface.Bounds.Width.ShouldBe(320);
        var pill = host.BoundsOf(host.Pill);
        var box = host.BoundsOf(surface);
        box.Right.ShouldBe(pill.Right, 0.5);
        box.Y.ShouldBe(pill.Bottom + 6, 0.5);
        AutomationProperties.GetName(surface).ShouldBe("Model and effort");
    }

    [AvaloniaFact]
    public void Each_Model_Is_A_38px_Radio_Row_With_Its_Mark_Name_Prices_And_A_Check()
    {
        using var host = Open();
        var rows = host.ModelMenu.Rows;
        rows.Count.ShouldBe(5);
        rows.Select(r => r.Text).ShouldBe(["Atlas Prime 2", "Atlas Core 2", "Atlas Lite 2", "Comet Pro", "Nova Flash"]);
        rows.Select(r => r.Price).ShouldBe(["$6 · $30", "$1.50 · $7", "$0.20 · $0.80", "$2.50 · $12", "$0.10 · $0.40"]);
        var row = rows[1];
        row.Bounds.Height.ShouldBe(38);
        row.Bounds.Width.ShouldBe(320 - 2 - 12, 0.01);
        host.BoundsOf(rows[2]).Y.ShouldBe(host.BoundsOf(rows[1]).Bottom + 2, 0.01);
        row.IsChecked.ShouldBeTrue();
        row.Check!.Opacity.ShouldBe(1);
        rows[0].Check!.Opacity.ShouldBe(0);
        TestHelpers.Descendant<Border>(row, "PART_Tint").Opacity.ShouldBe(1);
        TestHelpers.Descendant<ModelMark>(row).Size.ShouldBe(16);
        row.PriceBlock!.FontSize.ShouldBe(12);
        row.PriceBlock.FontFeatures!.Count.ShouldBe(1);
        TestHelpers.ColorOf(row.PriceBlock.Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.TextMuted));
    }

    [AvaloniaFact]
    public void Assistive_Technology_Hears_The_Full_Prices()
    {
        using var host = Open();
        AutomationProperties.GetName(Row(host, 1)).ShouldBe("Atlas Core 2, $1.50 input, $7 output per million tokens, sample price");
        var peer = ControlAutomationPeer.CreatePeerForElement(Row(host, 1));
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.RadioButton);
        peer.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);
        var group = host.ModelMenu.RowsPanel!;
        AutomationProperties.GetName(group).ShouldBe("Model");
        ControlAutomationPeer.CreatePeerForElement(group).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
    }

    [AvaloniaFact]
    public void Without_A_Price_Name_The_Prices_Are_Read_As_Shown()
    {
        var menu = PromptMenuSamples.ModelMenu();
        menu.PriceName = null;
        using var host = Open(menu);
        AutomationProperties.GetName(Row(host, 0)).ShouldBe("Atlas Prime 2, $6 · $30");
    }

    [Theory]
    [InlineData(2, "$2")]
    [InlineData(10, "$10")]
    [InlineData(0.1, "$0.10")]
    [InlineData(3.5, "$3.50")]
    [InlineData(1.25, "$1.25")]
    [InlineData(0.125, "$0.125")]
    [InlineData(0.00004, "$0.00")]
    [InlineData(1200, "$1200")]
    public void Prices_Drop_Zero_Cents_And_Keep_Two_Decimals_Otherwise(double price, string expected) =>
        PromptPriceText.Format((decimal)price).ShouldBe(expected);

    [Fact]
    public void A_Price_Pair_Ignores_The_Culture()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            PromptPriceText.Pair(1.5m, 7m).ShouldBe("$1.50 · $7");
            PromptPriceText.Pair(null, 7m).ShouldBe("— · $7");
            PromptPriceText.Pair(null, null).ShouldBeNull();
            PromptPriceText.Pair(2m, 4m, p => p.ToString(System.Globalization.CultureInfo.InvariantCulture) + " €").ShouldBe("2 € · 4 €");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    #endregion

    #region Effort row and note

    [AvaloniaFact]
    public void The_Effort_Row_Shows_The_Selected_Models_Levels_With_Short_Words()
    {
        using var host = Open();
        var menu = host.ModelMenu;
        var strip = menu.Strip!;
        strip.IsVisible.ShouldBeTrue();
        strip.Bounds.Height.ShouldBe(36);
        strip.Cells.Select(c => c.Text).ShouldBe(["Low", "Med", "High", "XHigh", "Max"]);
        strip.Cells.Select(c => AutomationProperties.GetName(c)).ShouldBe(["Low", "Medium", "High", "Extra high", "Max"]);
        strip.Cells.ShouldAllBe(c => c.Bounds.Height == 30);
        var widths = strip.Cells.Select(c => c.Bounds.Width).Distinct().ToList();
        widths.Count.ShouldBe(1);
        Cell(host, ModelEffort.High).IsChecked.ShouldBeTrue();
        TestHelpers.ColorOf(Cell(host, ModelEffort.High).Foreground).ShouldBe(ArcTokens.GetColor(strip, ArcDesignTokenKind.Foreground));
        TestHelpers.ColorOf(Cell(host, ModelEffort.Low).Foreground).ShouldBe(ArcTokens.GetColor(strip, ArcDesignTokenKind.TextMuted));
        menu.EffortTitleBlock!.Text.ShouldBe("Effort");
        menu.EffortTitleBlock.IsVisible.ShouldBeTrue();
        AutomationProperties.GetName(strip).ShouldBe("Effort");
        ControlAutomationPeer.CreatePeerForElement(Cell(host, ModelEffort.Medium)).GetAutomationControlType().ShouldBe(AutomationControlType.RadioButton);
        // The highlight sits under the selected cell.
        var thumb = strip.Thumb!;
        var cell = Cell(host, ModelEffort.High);
        thumb.CurrentRect.ShouldBe(new Rect(cell.TranslatePoint(default, thumb)!.Value, cell.Bounds.Size));
        thumb.CurrentOpacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Note_Shows_When_A_Price_Is_A_Sample()
    {
        var models = PromptMenuSamples.Models();
        using var host = Open(PromptMenuSamples.ModelMenu(models));
        var menu = host.ModelMenu;
        menu.Note!.IsVisible.ShouldBeTrue();
        menu.Note.Text.ShouldBe("Input · output per 1M tokens. Every price here is a sample.");
        foreach (var model in models)
        {
            model.IsSamplePrice = false;
        }
        host.Pump();
        menu.Note.IsVisible.ShouldBeFalse();
        menu.Footer!.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Model_Without_Levels_Hides_The_Effort_Row_And_Without_A_Sample_The_Whole_Footer()
    {
        var models = PromptMenuSamples.Models();
        foreach (var model in models)
        {
            model.IsSamplePrice = false;
        }
        var menu = PromptMenuSamples.ModelMenu(models);
        menu.SelectedModel = models[4];
        menu.Effort = null;
        using var host = Open(menu);
        menu.Strip!.IsVisible.ShouldBeFalse();
        menu.EffortTitleBlock!.IsVisible.ShouldBeFalse();
        menu.Footer!.IsVisible.ShouldBeFalse();
    }

    #endregion

    #region Choices

    [AvaloniaFact]
    public void Choosing_A_Model_Keeps_The_Menu_Open_And_Resets_The_Effort_To_Its_Default()
    {
        using var host = Open();
        var menu = host.ModelMenu;
        var events = new List<string>();
        menu.ModelChosen += (_, e) => events.Add($"model {e.Model.Name} from {e.PreviousModel?.Name} (selected {menu.SelectedModel?.Name})");
        menu.EffortChosen += (_, e) => events.Add($"effort {e.Effort} from {e.PreviousEffort} snap {e.IsSnap}");
        host.Click(Row(host, 3));
        events.ShouldBe(["model Comet Pro from Atlas Core 2 (selected Atlas Core 2)", "effort Low from High snap True"]);
        menu.IsOpen.ShouldBeTrue();
        menu.SelectedModel!.Name.ShouldBe("Comet Pro");
        menu.Effort.ShouldBe(ModelEffort.Low);
        Row(host, 3).IsChecked.ShouldBeTrue();
        Row(host, 1).IsChecked.ShouldBeFalse();
        menu.Strip!.Cells.Select(c => c.Level).ShouldBe([ModelEffort.Low, ModelEffort.Medium, ModelEffort.High]);
        Cell(host, ModelEffort.Low).IsChecked.ShouldBeTrue();
        var pill = (PromptModelPill)host.Pill;
        pill.CurrentName!.Text.ShouldBe("Comet Pro");
        pill.CurrentEffort!.Text.ShouldBe("Low");
    }

    [AvaloniaFact]
    public void Another_Models_Levels_Put_The_Highlight_Under_Its_Default()
    {
        using var host = Open();
        host.Click(Row(host, 3));
        host.Pump();
        var thumb = host.ModelMenu.Strip!.Thumb!;
        var low = Cell(host, ModelEffort.Low);
        thumb.CurrentRect.ShouldBe(new Rect(low.TranslatePoint(default, thumb)!.Value, low.Bounds.Size));
        thumb.CurrentOpacity.ShouldBe(1);
        // Back to five levels at High.
        host.Click(Row(host, 0));
        host.Pump();
        var high = Cell(host, ModelEffort.High);
        thumb.CurrentRect.ShouldBe(new Rect(high.TranslatePoint(default, thumb)!.Value, high.Bounds.Size));
    }

    [AvaloniaFact]
    public void Choosing_A_Model_Whose_Default_Is_The_Effort_Reports_No_Effort_Change()
    {
        using var host = Open();
        var menu = host.ModelMenu;
        var efforts = 0;
        menu.EffortChosen += (_, _) => efforts++;
        host.Click(Row(host, 0));
        menu.SelectedModel!.Name.ShouldBe("Atlas Prime 2");
        menu.Effort.ShouldBe(ModelEffort.High);
        efforts.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Choosing_The_Selected_Model_Again_Keeps_The_Effort()
    {
        using var host = Open();
        var menu = host.ModelMenu;
        menu.Effort = ModelEffort.Max;
        var chosen = new List<bool>();
        menu.ModelChosen += (_, e) => chosen.Add(e.IsChange);
        host.Click(Row(host, 1));
        chosen.ShouldBe([false]);
        menu.Effort.ShouldBe(ModelEffort.Max);
    }

    [AvaloniaFact]
    public void A_Model_Without_Levels_Clears_The_Effort()
    {
        using var host = Open();
        var menu = host.ModelMenu;
        ModelMenuEffortEventArgs? reported = null;
        menu.EffortChosen += (_, e) => reported = e;
        host.Click(Row(host, 4));
        menu.Effort.ShouldBeNull();
        reported!.Effort.ShouldBeNull();
        reported.PreviousEffort.ShouldBe(ModelEffort.High);
        menu.Strip!.IsVisible.ShouldBeFalse();
        menu.Note!.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Choosing_A_Level_Keeps_The_Menu_Open()
    {
        using var host = Open();
        var menu = host.ModelMenu;
        var reported = new List<(ModelEffort? Effort, ModelEffort? Previous, bool Snap)>();
        menu.EffortChosen += (_, e) => reported.Add((e.Effort, e.PreviousEffort, e.IsSnap));
        host.Click(Cell(host, ModelEffort.Medium));
        reported.ShouldBe([(ModelEffort.Medium, ModelEffort.High, false)]);
        menu.Effort.ShouldBe(ModelEffort.Medium);
        menu.IsOpen.ShouldBeTrue();
        Cell(host, ModelEffort.Medium).IsChecked.ShouldBeTrue();
        ((PromptModelPill)host.Pill).CurrentEffort!.Text.ShouldBe("Medium");
    }

    [AvaloniaFact]
    public void Reopened_The_Highlight_Is_Under_The_Level_At_Once()
    {
        using var host = Open();
        host.Click(host.Pill);
        host.Click(host.Pill);
        var thumb = host.ModelMenu.Strip!.Thumb!;
        var high = Cell(host, ModelEffort.High);
        thumb.CurrentOpacity.ShouldBe(1);
        thumb.CurrentRect.ShouldBe(new Rect(high.TranslatePoint(default, thumb)!.Value, high.Bounds.Size));
    }

    [AvaloniaFact]
    public void An_Effort_Set_From_Outside_Moves_The_Highlight()
    {
        using var host = Open();
        host.ModelMenu.Effort = ModelEffort.Low;
        host.Pump();
        var thumb = host.ModelMenu.Strip!.Thumb!;
        var cell = Cell(host, ModelEffort.Low);
        thumb.CurrentRect.ShouldBe(new Rect(cell.TranslatePoint(default, thumb)!.Value, cell.Bounds.Size));
        host.ModelMenu.Effort = ModelEffort.ExtraHigh;
        host.ModelMenu.SelectedModel = host.ModelMenu.Rows[2].Model;
        host.Pump();
        // Lite 2 has no Extra high: no cell is selected and the highlight goes.
        host.ModelMenu.Strip.Cells.ShouldAllBe(c => !c.IsChecked);
        thumb.CurrentOpacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Highlight_Glides_On_The_Quick_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = Show(level: ArcMotionLevel.Full);
        host.Click(host.Pill);
        CoreFrames.Run(clock, 60);
        host.Pump();
        var thumb = host.ModelMenu.Strip!.Thumb!;
        var start = thumb.CurrentRect.X;
        host.Click(Cell(host, ModelEffort.Low));
        host.Pump();
        var low = Cell(host, ModelEffort.Low);
        var target = low.TranslatePoint(default, thumb)!.Value.X;
        var t = CoreFrames.Run(clock, 5);
        var expected = target + SpringSolver.Evaluate(ArcMotionPresets.Get(ArcSpring.Quick), start - target, 0, t).Displacement;
        thumb.CurrentRect.X.ShouldBe(expected, 0.05);
        thumb.CurrentRect.X.ShouldBeLessThan(start - 5);
        CoreFrames.Run(clock, 40);
        thumb.CurrentRect.X.ShouldBe(target, 0.01);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Highlight_And_Only_Fades_The_Popover()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = Show(level: ArcMotionLevel.Reduced);
        host.Click(host.Pill);
        host.ModelMenu.Presence!.CurrentPose.ShouldBe(new Point(0, 1));
        CoreFrames.Run(clock, 20);
        host.Pump();
        var thumb = host.ModelMenu.Strip!.Thumb!;
        host.Click(Cell(host, ModelEffort.Low));
        host.Pump();
        var low = Cell(host, ModelEffort.Low);
        thumb.CurrentRect.ShouldBe(new Rect(low.TranslatePoint(default, thumb)!.Value, low.Bounds.Size));
    }

    #endregion

    #region Keys

    [AvaloniaFact]
    public void Opened_With_Keys_Focus_Lands_On_The_Selected_Model()
    {
        using var host = Show();
        host.OpenWithKeys();
        host.Focused.ShouldBe(Row(host, 1));
        host.HasFocusRing(Row(host, 1)).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Arrows_Choose_Models_And_Wrap_And_The_Menu_Stays_Open()
    {
        using var host = Show();
        host.OpenWithKeys();
        var menu = host.ModelMenu;
        host.Key(PhysicalKey.ArrowDown);
        menu.SelectedModel!.Name.ShouldBe("Atlas Lite 2");
        menu.Effort.ShouldBe(ModelEffort.Medium);
        host.Focused.ShouldBe(Row(host, 2));
        host.Key(PhysicalKey.End);
        menu.SelectedModel!.Name.ShouldBe("Nova Flash");
        host.Key(PhysicalKey.ArrowDown);
        menu.SelectedModel!.Name.ShouldBe("Atlas Prime 2");
        host.Key(PhysicalKey.ArrowUp);
        menu.SelectedModel!.Name.ShouldBe("Nova Flash");
        host.Key(PhysicalKey.Home);
        menu.SelectedModel!.Name.ShouldBe("Atlas Prime 2");
        host.Key(PhysicalKey.Enter);
        menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Tab_Reaches_The_Selected_Level_Where_Arrows_Choose_Levels()
    {
        using var host = Show();
        host.OpenWithKeys();
        var menu = host.ModelMenu;
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(Cell(host, ModelEffort.High));
        host.HasFocusRing(Cell(host, ModelEffort.High)).ShouldBeTrue();
        host.Key(PhysicalKey.ArrowRight);
        menu.Effort.ShouldBe(ModelEffort.ExtraHigh);
        host.Focused.ShouldBe(Cell(host, ModelEffort.ExtraHigh));
        host.Key(PhysicalKey.ArrowRight);
        host.Key(PhysicalKey.ArrowRight);
        menu.Effort.ShouldBe(ModelEffort.Low);
        host.Key(PhysicalKey.ArrowLeft);
        menu.Effort.ShouldBe(ModelEffort.Max);
        host.Key(PhysicalKey.Home);
        menu.Effort.ShouldBe(ModelEffort.Low);
        host.Key(PhysicalKey.End);
        menu.Effort.ShouldBe(ModelEffort.Max);
        menu.SelectedModel!.Name.ShouldBe("Atlas Core 2");
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(Row(host, 1));
        host.Key(PhysicalKey.Tab, RawInputModifiers.Shift);
        host.Focused.ShouldBe(Cell(host, ModelEffort.Max));
        host.Key(PhysicalKey.Escape);
        menu.IsOpen.ShouldBeFalse();
        host.Focused.ShouldBe(host.Pill);
        host.HasFocusRing(host.Pill).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Only_The_Selected_Level_Is_A_Tab_Stop()
    {
        using var host = Open();
        host.ModelMenu.Strip!.Cells.Where(c => c.IsTabStop).ShouldHaveSingleItem().Level.ShouldBe(ModelEffort.High);
    }

    #endregion

    #region Motion and data

    [AvaloniaFact]
    public void The_Popover_Scales_About_Its_Top_Right_Corner()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = Show(level: ArcMotionLevel.Full);
        host.Click(host.Pill);
        var presence = host.ModelMenu.Presence!;
        presence.CurrentPose.ShouldBe(new Point(5, 0.97));
        var surface = host.ModelMenu.Surface!;
        var actor = surface.FindAncestorOfType<ArcPopupFrame>()!;
        var corner = surface.TranslatePoint(new Point(surface.Bounds.Width, 0), actor)!.Value;
        var moved = presence.CurrentMatrix.Transform(corner);
        moved.X.ShouldBe(corner.X, 0.001);
        moved.Y.ShouldBe(corner.Y - 5, 0.001);
        CoreFrames.Run(clock, 60);
        presence.CurrentPose.ShouldBe(new Point(0, 1));
    }

    [AvaloniaFact]
    public void Models_Added_While_Open_Show_At_Once_And_Providers_Fill_Missing_Marks()
    {
        var models = new AvaloniaList<AiModel>(PromptMenuSamples.Models());
        var providers = new AvaloniaList<AiModelProvider>();
        var menu = PromptMenuSamples.ModelMenu(models.ToArray());
        menu.Models = models;
        menu.Providers = providers;
        using var host = Open(menu);
        var extra = new AiModel { Id = "basalt-4", Name = "Basalt 4", ProviderId = "basalt", InputPrice = 1m, OutputPrice = 2m };
        models.Add(extra);
        host.Pump();
        menu.Rows.Count.ShouldBe(6);
        menu.Rows[5].Mark.ShouldBeNull();
        var hexagon = Geometry.Parse("M12 2 L21 7 L21 17 L12 22 L3 17 L3 7 Z");
        providers.Add(new AiModelProvider { Id = "basalt", Mark = hexagon });
        menu.Rows[5].Mark.ShouldBe(hexagon);
        extra.OutputPrice = 2.5m;
        menu.Rows[5].Price.ShouldBe("$1 · $2.50");
    }

    [AvaloniaFact]
    public void The_Menu_Holds_No_Words_Of_Its_Own()
    {
        var models = PromptMenuSamples.Models();
        var menu = new PromptModelMenu { Models = models, SelectedModel = models[0], Effort = ModelEffort.High };
        using var host = Open(menu);
        menu.EffortTitleBlock!.IsVisible.ShouldBeFalse();
        menu.Note!.IsVisible.ShouldBeFalse();
        // Without short words the cells fall back to the shared level names.
        menu.Strip!.Cells.Select(c => c.Text).ShouldBe(["Low", "Medium", "High", "Extra high", "Max"]);
    }

    #endregion
}
