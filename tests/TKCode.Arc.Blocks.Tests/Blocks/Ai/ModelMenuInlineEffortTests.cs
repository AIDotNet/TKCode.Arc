using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The composer's model menu (<see cref="ModelMenuEffortLayout.Inline"/>, no "More models"): every model in one list, then
/// the effort as a segmented row in the same panel; choosing a model closes, choosing a level does not.
/// </summary>
public class ModelMenuInlineEffortTests
{
    /// <summary>A composer menu with its anchor (the chip) at the bottom right of a wide window.</summary>
    private static ModelMenuHost Composer(Action<ModelMenu>? configure = null, double width = 900, double height = 720,
                                          ArcMotionLevel level = ArcMotionLevel.Off) =>
        ModelMenuHost.Show(ModelMenuSamples.ComposerMenu(configure), width, height, level, HorizontalAlignment.Right, VerticalAlignment.Bottom);

    private static ModelMenuEffortStrip Strip(ModelMenuHost host) => host.Main.Rows.OfType<ModelMenuEffortStrip>().Single();

    private static ModelMenuEffortHeader Header(ModelMenuHost host) => host.InPanel<ModelMenuEffortHeader>(host.Main).Single();

    private static AiModel Model(ModelMenuHost host, string id) => host.Menu.Models.First(m => m.Id == id);

    private static string[] CellTexts(ModelMenuEffortStrip strip) => strip.Cells.Select(c => c.Text ?? string.Empty).ToArray();

    private static ModelMenuEffortCell Cell(ModelMenuEffortStrip strip, ModelEffort level) => strip.Cells.Single(c => c.Level == level);

    private static Rect ThumbRect(ModelMenuEffortStrip strip) => strip.Thumb!.CurrentRect;

    private static Rect CellRect(ModelMenuEffortStrip strip, ModelEffort level)
    {
        var cell = Cell(strip, level);
        return new Rect(cell.TranslatePoint(default, strip.Thumb!)!.Value, cell.Bounds.Size);
    }

    private static void ThumbShouldSitOn(ModelMenuEffortStrip strip, ModelEffort level)
    {
        var expected = CellRect(strip, level).Deflate(0.5);
        var thumb = ThumbRect(strip);
        thumb.X.ShouldBe(expected.X, 0.01);
        thumb.Y.ShouldBe(expected.Y, 0.01);
        thumb.Width.ShouldBe(expected.Width, 0.01);
        thumb.Height.ShouldBe(29, 0.01);
    }

    #region Defaults and structure

    [AvaloniaFact]
    public void Defaults_Keep_The_Picker_Menu()
    {
        var menu = new ModelMenu();
        menu.EffortLayout.ShouldBe(ModelMenuEffortLayout.Submenu);
        menu.ShowMoreModels.ShouldBeTrue();
        menu.Placement.ShouldBe(PlacementMode.Bottom);
    }

    [AvaloniaFact]
    public void Every_Model_Then_A_Separator_The_Effort_Header_And_The_Segmented_Row()
    {
        using var host = Composer();
        host.Open();
        var main = host.Main;
        main.IsInline.ShouldBeTrue();
        host.BoundsOf(main).Width.ShouldBe(296);
        // Five models (the first four would be "featured"; without More models every model is listed), then one stop
        // for the effort.
        main.Rows.Count.ShouldBe(6);
        main.Rows.Take(5).OfType<ModelMenuModelRow>().Select(r => r.Model.Id)
            .ShouldBe(["aurora-prime-3", "aurora-swift-3", "cobalt-atlas-2", "halcyon-wide-15", "granite-70b"]);
        main.Rows[5].ShouldBeOfType<ModelMenuEffortStrip>();
        host.InPanel<ModelMenuActionRow>(main).ShouldBeEmpty();
        host.InPanel<ModelMenuSeparator>(main).Count().ShouldBe(1);
        // The first model is selected with its default effort.
        host.Menu.SelectedModel!.Id.ShouldBe("aurora-prime-3");
        host.Menu.Effort.ShouldBe(ModelEffort.Medium);
        var header = Header(host);
        header.Title.ShouldBe("Effort");
        header.Description.ShouldBe(ModelEffortNames.Description(ModelEffort.Medium));
        // 5 × 48 rows + separator (1 + 2 × 5) + header (6 + 16.8 + 6) + strip (38 + 2) + padding (2 × 5) + border (2).
        host.BoundsOf(main).Height.ShouldBe(5 * 48 + 11 + 28.8 + 40 + 10 + 2, 1);
    }

    [AvaloniaFact]
    public void Model_Rows_Have_The_Composer_Look()
    {
        using var host = Composer();
        host.Open();
        var row = host.ModelRow(host.Main, "aurora-prime-3");
        row.IsInline.ShouldBeTrue();
        row.Bounds.Height.ShouldBe(48);
        row.Padding.ShouldBe(new Thickness(11, 6, 10, 6));
        var mark = host.InPanel<ModelMark>(host.Main).First();
        mark.Size.ShouldBe(16);
        var texts = row.GetVisualDescendants().OfType<TextBlock>().ToArray();
        var name = texts.First(t => t.Text == "Aurora Prime 3");
        name.FontWeight.ShouldBe(FontWeight.Normal);
        name.FontSize.ShouldBe(14);
        TestHelpers.ColorOf(name.Foreground).ShouldBe(ArcTokens.GetColor(row, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground));
        var blurb = texts.First(t => t.Text == "Deep work: refactors, research, long tasks");
        blurb.FontSize.ShouldBe(12);
        blurb.LineHeight.ShouldBe(16.8, 0.01);
        TestHelpers.ColorOf(blurb.Foreground).ShouldBe(ArcTokens.GetColor(row, TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted));
        // Mark 16 then 10, check 10 before the end; the check is the text color, not the accent.
        var markBox = new Rect(mark.TranslatePoint(default, row)!.Value, mark.Bounds.Size);
        markBox.X.ShouldBe(11, 0.5);
        (name.TranslatePoint(default, row)!.Value.X - markBox.Right).ShouldBe(10, 0.5);
        var check = row.GetVisualDescendants().OfType<ArcGlyph>().Single(g => g.IsVisible);
        check.Size.ShouldBe(16);
        TestHelpers.ColorOf(check.Foreground).ShouldBe(ArcTokens.GetColor(row, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground));
        (row.Bounds.Width - (check.TranslatePoint(default, row)!.Value.X + check.Bounds.Width)).ShouldBe(10, 0.5);
        host.ModelRow(host.Main, "aurora-swift-3").GetVisualDescendants().OfType<ArcGlyph>().ShouldAllBe(g => !g.IsVisible);
        // The list sits 5px inside the 1px border, the highlight is concentric with the 26px panel (20).
        host.Main.List!.Margin.ShouldBe(new Thickness(5));
        host.Main.Highlight!.CornerRadius.ShouldBe(new CornerRadius(20));
        var separator = host.InPanel<ModelMenuSeparator>(host.Main).Single();
        var line = separator.GetVisualDescendants().OfType<Border>().First();
        line.Margin.ShouldBe(new Thickness(-5, 5, -5, 5));
        host.BoundsOf(line).Width.ShouldBe(294, 0.5);
    }

    [AvaloniaFact]
    public void The_Segmented_Row_Has_A_Cell_For_Each_Level_The_Model_Offers()
    {
        using var host = Composer();
        host.Open();
        var strip = Strip(host);
        // "Extra high" does not fit a 53px cell at 12px: the cell says "X-high" (the accessible name keeps the full name).
        CellTexts(strip).ShouldBe(["Low", "Medium", "High", "X-high", "Max"]);
        Cell(strip, ModelEffort.ExtraHigh).DesiredSize.Width.ShouldBeLessThanOrEqualTo(Cell(strip, ModelEffort.ExtraHigh).Bounds.Width);
        strip.Level.ShouldBe(ModelEffort.Medium);
        Cell(strip, ModelEffort.Medium).IsSelected.ShouldBeTrue();
        Cell(strip, ModelEffort.Low).IsSelected.ShouldBeFalse();
        // Track: margin 0 2 2, 1px border, radius 17, padding 3; cells 30 high, radius 13, equal, 2 apart.
        strip.Margin.ShouldBe(new Thickness(2, 0, 2, 2));
        strip.Bounds.Height.ShouldBe(38);
        strip.Bounds.Width.ShouldBe(296 - 2 - 10 - 4, 0.5);
        var widths = strip.Cells.Select(c => c.Bounds.Width).ToArray();
        widths.ShouldAllBe(w => Math.Abs(w - widths[0]) <= 1);
        widths[0].ShouldBe((278 - 6 - 4 * 2) / 5.0, 1);
        strip.Cells.ShouldAllBe(c => c.Bounds.Height == 30);
        (CellRect(strip, ModelEffort.Medium).X - CellRect(strip, ModelEffort.Low).Right).ShouldBe(2, 0.5);
        Cell(strip, ModelEffort.Low).CornerRadius.ShouldBe(new CornerRadius(13));
        // The thumb lies under the selected cell: a 1px border-box, so its stroke is drawn half a pixel inside the cell.
        ThumbShouldSitOn(strip, ModelEffort.Medium);
        strip.Thumb!.CornerRadius.ShouldBe(new CornerRadius(12.5));
        strip.Thumb.StrokeThickness.ShouldBe(1);
        // Selected text in the text color, the others secondary.
        TestHelpers.ColorOf(Cell(strip, ModelEffort.Medium).Foreground)
            .ShouldBe(ArcTokens.GetColor(strip, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground));
        TestHelpers.ColorOf(Cell(strip, ModelEffort.Low).Foreground)
            .ShouldBe(ArcTokens.GetColor(strip, TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextSecondary));
        Cell(strip, ModelEffort.Low).FontSize.ShouldBe(12);
    }

    [AvaloniaFact]
    public void A_Model_With_Three_Levels_Gets_Three_Wider_Cells()
    {
        using var host = Composer(m => m.SelectedModelId = "cobalt-atlas-2");
        host.Open();
        var strip = Strip(host);
        CellTexts(strip).ShouldBe(["Low", "Medium", "High"]);
        // Layout rounding puts the columns on whole pixels.
        strip.Cells[0].Bounds.Width.ShouldBe((278 - 6 - 2 * 2) / 3.0, 1);
    }

    [AvaloniaFact]
    public void A_Model_Without_Effort_Levels_Has_No_Effort_Section()
    {
        using var host = Composer(m => m.SelectedModelId = "granite-70b");
        host.Open();
        host.Menu.Effort.ShouldBeNull();
        host.Main.Rows.Count.ShouldBe(5);
        host.Main.Rows.ShouldAllBe(r => r is ModelMenuModelRow);
        host.InPanel<ModelMenuEffortHeader>(host.Main).ShouldBeEmpty();
        host.InPanel<ModelMenuSeparator>(host.Main).ShouldBeEmpty();
        host.BoundsOf(host.Main).Height.ShouldBe(5 * 48 + 10 + 2, 1);
    }

    [AvaloniaFact]
    public void The_Header_Description_Follows_The_Effort()
    {
        using var host = Composer();
        host.Open();
        var strip = Strip(host);
        host.Menu.Effort = ModelEffort.High;
        host.Pump();
        Header(host).Description.ShouldBe(ModelEffortNames.Description(ModelEffort.High));
        // Updated in place: same strip, thumb under High.
        Strip(host).ShouldBeSameAs(strip);
        strip.Level.ShouldBe(ModelEffort.High);
        Cell(strip, ModelEffort.High).IsSelected.ShouldBeTrue();
        ThumbShouldSitOn(strip, ModelEffort.High);
    }

    [AvaloniaFact]
    public void Opens_Above_The_Chip_Aligned_To_Its_Right_Edge()
    {
        using var host = Composer();
        host.Open();
        var surface = host.BoundsOf(host.Main);
        var anchor = host.BoundsOf(host.Anchor);
        surface.Bottom.ShouldBe(anchor.Top - 8, 0.5);
        surface.Right.ShouldBe(anchor.Right, 0.5);
    }

    [AvaloniaFact]
    public void Right_Aligned_Menus_Still_Keep_12px_From_The_Window_Edge()
    {
        // The chip at the left of a window: aligned right it would leave the window, so it slides back in.
        using var host = ModelMenuHost.Show(ModelMenuSamples.ComposerMenu(), anchorAt: HorizontalAlignment.Left, anchorV: VerticalAlignment.Bottom);
        host.Open();
        host.BoundsOf(host.Main).Left.ShouldBe(12, 0.5);
    }

    #endregion

    #region Choosing

    [AvaloniaFact]
    public void Choosing_A_Model_Closes_And_Gives_Focus_Back_To_The_Chip()
    {
        using var host = Composer();
        var chosen = new List<ModelMenuModelEventArgs>();
        host.Menu.ModelChosen += (_, e) => chosen.Add(e);
        host.Anchor.Focus(NavigationMethod.Tab);
        host.Open(ModelMenuOpenFocus.Selected);
        host.Main.Active.ShouldBe(host.ModelRow(host.Main, "aurora-prime-3"));
        host.Key(PhysicalKey.ArrowDown);
        host.Key(PhysicalKey.Enter);
        chosen.Single().Model.Id.ShouldBe("aurora-swift-3");
        host.Menu.SelectedModel!.Id.ShouldBe("aurora-swift-3");
        host.Menu.IsOpen.ShouldBeFalse();
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldContain(":focus-visible");

        // A click closes too, focus back without a ring.
        host.Open();
        host.Click(host.ModelRow(host.Main, "cobalt-atlas-2"));
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.SelectedModel!.Id.ShouldBe("cobalt-atlas-2");
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldNotContain(":focus-visible");
    }

    [AvaloniaFact]
    public void Clicking_A_Level_Changes_The_Effort_And_Keeps_The_Menu_Open()
    {
        using var host = Composer();
        var efforts = new List<ModelMenuEffortEventArgs>();
        host.Menu.EffortChosen += (_, e) => efforts.Add(e);
        host.Open();
        var strip = Strip(host);
        host.Click(Cell(strip, ModelEffort.High));
        host.Menu.Effort.ShouldBe(ModelEffort.High);
        var chosen = efforts.Single();
        chosen.Effort.ShouldBe(ModelEffort.High);
        chosen.PreviousEffort.ShouldBe(ModelEffort.Medium);
        chosen.IsSnap.ShouldBeFalse();
        host.Menu.IsOpen.ShouldBeTrue();
        host.Menu.MainPresence!.IsShown.ShouldBeTrue();
        Strip(host).ShouldBeSameAs(strip);
        Header(host).Description.ShouldBe(ModelEffortNames.Description(ModelEffort.High));
        ThumbShouldSitOn(strip, ModelEffort.High);
        // The pointer on the strip does not draw the row highlight over it.
        host.Main.Highlight!.CurrentOpacity.ShouldBe(0);
        // The same level again is reported without a change.
        host.Click(Cell(strip, ModelEffort.High));
        efforts.Count.ShouldBe(2);
        efforts[1].PreviousEffort.ShouldBe(ModelEffort.High);
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Choosing_A_Model_Snaps_The_Effort_To_A_Level_It_Offers()
    {
        using var host = Composer(m => m.Effort = ModelEffort.Max);
        var efforts = new List<ModelMenuEffortEventArgs>();
        host.Menu.EffortChosen += (_, e) => efforts.Add(e);
        host.Menu.Effort.ShouldBe(ModelEffort.Max);
        host.Open();
        host.Click(host.ModelRow(host.Main, "cobalt-atlas-2"));
        host.Menu.Effort.ShouldBe(ModelEffort.High);
        efforts.Single().IsSnap.ShouldBeTrue();
        host.Open();
        var strip = Strip(host);
        CellTexts(strip).ShouldBe(["Low", "Medium", "High"]);
        strip.Level.ShouldBe(ModelEffort.High);
        // A model without levels has no effort; back to one with levels, its default.
        host.Click(host.ModelRow(host.Main, "granite-70b"));
        host.Menu.Effort.ShouldBeNull();
        host.Open();
        host.Click(host.ModelRow(host.Main, "aurora-swift-3"));
        host.Menu.Effort.ShouldBe(ModelEffort.High);
    }

    #endregion

    #region Keyboard

    [AvaloniaFact]
    public void Up_And_Down_Walk_The_Models_And_The_Segmented_Row_As_One_List()
    {
        using var host = Composer();
        host.Anchor.Focus(NavigationMethod.Tab);
        host.Open(ModelMenuOpenFocus.Selected);
        var rows = host.Main.Rows;
        var strip = Strip(host);
        for (var i = 1; i <= 5; i++)
        {
            host.Key(PhysicalKey.ArrowDown);
            host.Main.Active.ShouldBe(rows[i]);
        }
        strip.IsFocused.ShouldBeTrue();
        strip.Classes.ShouldContain(":focus-visible");
        // The row highlight leaves for the strip: it shows its own ring around the selected level.
        host.Main.Highlight!.CurrentOpacity.ShouldBe(0);
        strip.Ring!.IsVisible.ShouldBeTrue();
        strip.Ring.CurrentRect.ShouldBe(CellRect(strip, ModelEffort.Medium).Inflate(1));
        strip.Ring.StrokeThickness.ShouldBe(2);
        strip.Ring.CornerRadius.ShouldBe(new CornerRadius(14));
        host.Key(PhysicalKey.ArrowDown);
        host.Main.Active.ShouldBe(rows[0]);
        strip.Ring.IsVisible.ShouldBeFalse();
        host.Key(PhysicalKey.ArrowUp);
        host.Main.Active.ShouldBe(strip);
        host.Key(PhysicalKey.Home);
        host.Main.Active.ShouldBe(rows[0]);
        host.Key(PhysicalKey.End);
        host.Main.Active.ShouldBe(strip);
        // A letter finds a model by its name, and the strip by its title (a pause starts a new search).
        var now = DateTime.UtcNow;
        host.Menu.Now = () => now;
        host.Key(PhysicalKey.H);
        host.Main.Active.ShouldBe(host.ModelRow(host.Main, "halcyon-wide-15"));
        now += TimeSpan.FromSeconds(2);
        host.Key(PhysicalKey.E);
        host.Main.Active.ShouldBe(strip);
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Left_And_Right_On_The_Segmented_Row_Change_The_Level_At_Once_And_Wrap()
    {
        using var host = Composer();
        var efforts = new List<ModelEffort?>();
        host.Menu.EffortChosen += (_, e) => efforts.Add(e.Effort);
        host.Open(ModelMenuOpenFocus.Last);
        var strip = Strip(host);
        host.Main.Active.ShouldBe(strip);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.Effort.ShouldBe(ModelEffort.High);
        host.Key(PhysicalKey.ArrowRight);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.Effort.ShouldBe(ModelEffort.Max);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.Effort.ShouldBe(ModelEffort.Low);
        host.Key(PhysicalKey.ArrowLeft);
        host.Menu.Effort.ShouldBe(ModelEffort.Max);
        efforts.ShouldBe([ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max, ModelEffort.Low, ModelEffort.Max]);
        host.Menu.IsOpen.ShouldBeTrue();
        strip.IsFocused.ShouldBeTrue();
        Header(host).Description.ShouldBe(ModelEffortNames.Description(ModelEffort.Max));
        strip.Ring!.CurrentRect.ShouldBe(CellRect(strip, ModelEffort.Max).Inflate(1));
        ThumbShouldSitOn(strip, ModelEffort.Max);
        // Left and Right on a model row do nothing.
        host.Key(PhysicalKey.Home);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.Effort.ShouldBe(ModelEffort.Max);
        host.Menu.IsOpen.ShouldBeTrue();
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
    }

    [AvaloniaFact]
    public void Enter_On_The_Segmented_Row_Reports_The_Level_And_Stays_Open_Escape_Closes_To_The_Chip()
    {
        using var host = Composer();
        var efforts = new List<ModelMenuEffortEventArgs>();
        host.Menu.EffortChosen += (_, e) => efforts.Add(e);
        host.Anchor.Focus(NavigationMethod.Tab);
        host.Open(ModelMenuOpenFocus.Last);
        host.Key(PhysicalKey.Enter);
        efforts.Single().Effort.ShouldBe(ModelEffort.Medium);
        efforts.Single().PreviousEffort.ShouldBe(ModelEffort.Medium);
        host.Key(PhysicalKey.Space);
        efforts.Count.ShouldBe(2);
        host.Menu.IsOpen.ShouldBeTrue();
        host.Key(PhysicalKey.Escape);
        host.Menu.IsOpen.ShouldBeFalse();
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void A_Pointer_On_The_Strip_Then_Keys_Keep_The_Strip()
    {
        using var host = Composer();
        host.Open();
        var strip = Strip(host);
        host.Move(Cell(strip, ModelEffort.Low));
        host.Main.Active.ShouldBe(strip);
        host.Main.Highlight!.CurrentOpacity.ShouldBe(0);
        strip.Ring!.IsVisible.ShouldBeFalse();
        host.Key(PhysicalKey.ArrowLeft);
        host.Menu.Effort.ShouldBe(ModelEffort.Low);
        // Back on a model row the highlight returns.
        host.Move(host.ModelRow(host.Main, "aurora-swift-3"));
        host.Main.Active.ShouldBe(host.ModelRow(host.Main, "aurora-swift-3"));
        host.Main.Highlight.CurrentOpacity.ShouldBe(1);
    }

    #endregion

    #region Combinations

    [AvaloniaFact]
    public void Without_More_Models_The_Picker_Layout_Lists_Every_Model_And_Keeps_The_Effort_Submenu()
    {
        using var host = ModelMenuHost.Show(ModelMenuSamples.Menu(m => m.ShowMoreModels = false));
        host.Open();
        var rows = host.Main.Rows;
        rows.OfType<ModelMenuModelRow>().Count().ShouldBe(ModelMenuSamples.Models().Length);
        rows.OfType<ModelMenuActionRow>().Select(r => r.Kind).ShouldBe([ModelMenuRowKind.Effort]);
        host.Main.IsInline.ShouldBeFalse();
        host.BoundsOf(host.Main).Width.ShouldBe(304);
        // Every model is a featured-style row here (54 high), the list scrolls past 420.
        rows.OfType<ModelMenuModelRow>().ShouldAllBe(r => !r.IsCompact && !r.IsInline);
        host.Main.Scroll!.Bounds.Height.ShouldBe(420, 0.5);
        host.Menu.FeaturedModels.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void The_Inline_Layout_With_More_Models_Keeps_The_More_Row_And_Its_Submenu()
    {
        using var host = ModelMenuHost.Show(ModelMenuSamples.Menu(m => m.EffortLayout = ModelMenuEffortLayout.Inline));
        host.Open(ModelMenuOpenFocus.Selected);
        var rows = host.Main.Rows;
        rows.OfType<ModelMenuModelRow>().Count().ShouldBe(4);
        rows[4].ShouldBeOfType<ModelMenuActionRow>().Kind.ShouldBe(ModelMenuRowKind.More);
        rows[5].ShouldBeOfType<ModelMenuEffortStrip>();
        host.InPanel<ModelMenuSeparator>(host.Main).Count().ShouldBe(2);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowUp);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.More);
        host.Menu.EffortPresence.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Switching_The_Layout_While_Open_Rebuilds_The_Menu()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Main.Rows.OfType<ModelMenuActionRow>().Count().ShouldBe(2);
        host.Menu.EffortLayout = ModelMenuEffortLayout.Inline;
        host.Menu.ShowMoreModels = false;
        host.Pump();
        host.Main.IsInline.ShouldBeTrue();
        host.Main.Rows.OfType<ModelMenuActionRow>().ShouldBeEmpty();
        host.Main.Rows.OfType<ModelMenuEffortStrip>().Count().ShouldBe(1);
        host.BoundsOf(host.Main).Width.ShouldBe(296);
        host.Menu.EffortLayout = ModelMenuEffortLayout.Submenu;
        host.Pump();
        host.Main.IsInline.ShouldBeFalse();
        host.Main.Rows.OfType<ModelMenuActionRow>().Single().Kind.ShouldBe(ModelMenuRowKind.Effort);
        host.BoundsOf(host.Main).Width.ShouldBe(304);
    }

    [AvaloniaFact]
    public void Labels_Name_The_Header_The_Cells_And_The_Strip()
    {
        using var host = Composer(m => m.Labels = new ModelMenuLabels
        {
            Effort = "Aufwand",
            EffortName = static e => e == ModelEffort.Medium ? "Mittel" : e.ToString(),
            EffortSegmentName = static e => e == ModelEffort.ExtraHigh ? "X-hoch" : e == ModelEffort.Medium ? "Mittel" : e.ToString(),
            EffortDescription = static e => $"Beschreibung {e}",
            EffortStrip = static (title, level) => $"{title}: {level}!"
        });
        host.Open();
        var strip = Strip(host);
        Header(host).Title.ShouldBe("Aufwand");
        Header(host).Description.ShouldBe("Beschreibung Medium");
        CellTexts(strip).ShouldBe(["Low", "Mittel", "High", "X-hoch", "Max"]);
        AutomationProperties.GetName(strip).ShouldBe("Aufwand: Mittel!");
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void The_Strip_Is_A_Menu_Item_Named_With_Its_Level_And_Its_Cells_Are_Radio_Buttons()
    {
        using var host = Composer();
        host.Open();
        var strip = Strip(host);
        AutomationProperties.GetName(strip).ShouldBe("Effort: Medium");
        var peer = ControlAutomationPeer.CreatePeerForElement(strip);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
        var cells = peer.GetChildren();
        cells.Count.ShouldBe(5);
        cells.ShouldAllBe(c => c.GetAutomationControlType() == AutomationControlType.RadioButton);
        cells[1].GetName().ShouldBe("Medium");
        cells[1].GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);
        cells[3].GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.Off);
        cells[3].GetProvider<IInvokeProvider>()!.Invoke();
        host.Pump();
        host.Menu.Effort.ShouldBe(ModelEffort.ExtraHigh);
        host.Menu.IsOpen.ShouldBeTrue();
        AutomationProperties.GetName(strip).ShouldBe("Effort: Extra high");
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void The_Thumb_Glides_On_The_Measured_Spring_And_The_Ring_Follows()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = Composer(level: ArcMotionLevel.Full);
        host.Open(ModelMenuOpenFocus.Last);
        CoreFrames.Run(clock, 60);
        host.Pump();
        var strip = Strip(host);
        var from = ThumbRect(strip).X;
        host.Key(PhysicalKey.ArrowRight);
        var to = CellRect(strip, ModelEffort.High).X + 0.5;
        // Arc's measured slider spring: FromVisual(0.35, 0.1).
        var spring = SpringSpec.FromVisual(0.35, 0.1);
        strip.ThumbSpring.ShouldBe(spring);
        var t = CoreFrames.Run(clock, 6);
        var x = ThumbRect(strip).X;
        x.ShouldBe(to + SpringSolver.Evaluate(spring, from - to, 0, t).Displacement, 0.01);
        x.ShouldBeGreaterThan(from + 1);
        x.ShouldBeLessThan(to - 1);
        strip.Ring!.CurrentRect.X.ShouldBe(x - 1.5, 0.01);
        CoreFrames.Run(clock, 60);
        ThumbRect(strip).X.ShouldBe(to, 0.01);
        strip.Ring.CurrentRect.X.ShouldBe(to - 1.5, 0.01);
    }

    [AvaloniaFact]
    public void The_Level_Text_Color_Changes_In_160ms_Under_Full_Motion()
    {
        using var host = Composer(level: ArcMotionLevel.Full);
        host.Open();
        host.Pump(10);
        var cell = Cell(Strip(host), ModelEffort.Low);
        cell.Classes.ShouldContain(":motion-full");
        cell.Transitions.ShouldNotBeNull().OfType<ArcBrushTransition>().Single(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty)
            .Duration.ShouldBe(TimeSpan.FromMilliseconds(160));
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Thumb()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = Composer(level: ArcMotionLevel.Reduced);
        host.Open(ModelMenuOpenFocus.Last);
        CoreFrames.Run(clock, 30);
        host.Pump();
        var strip = Strip(host);
        host.Key(PhysicalKey.ArrowRight);
        ThumbShouldSitOn(strip, ModelEffort.High);
    }

    [AvaloniaFact]
    public void The_Menu_Grows_From_Its_Bottom_Right_Corner_Toward_The_Chip()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = Composer(level: ArcMotionLevel.Full);
        host.Open();
        var presence = host.Menu.MainPresence!;
        presence.CurrentPose.ShouldBe(new Point(6, 0.97));
        var actor = host.Main.FindAncestorOfType<ArcPopupFrame>()!;
        var matrix = ((MatrixTransform)actor.RenderTransform!).Matrix;
        // Below its rest position (toward the chip under it), scaled about the bottom-right corner: that corner moves
        // only by the travel.
        var corner = host.Main.TranslatePoint(new Point(host.Main.Bounds.Width, host.Main.Bounds.Height), actor)!.Value;
        var moved = matrix.Transform(corner);
        moved.X.ShouldBe(corner.X, 0.01);
        moved.Y.ShouldBe(corner.Y + 6, 0.01);
        CoreFrames.Run(clock, 60);
        presence.CurrentPose.ShouldBe(new Point(0, 1));
    }

    #endregion
}
