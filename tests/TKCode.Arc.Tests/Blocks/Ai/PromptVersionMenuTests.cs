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
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The prompt studio's version menu: rows, selection, actions, the production marker, Undo, keys, focus and motion.</summary>
public class PromptVersionMenuTests
{
    private static PromptVersion V(PromptMenuHost host, string label) => host.VersionMenu.Rows.First(r => r.Version.Label == label).Version;

    private static PromptVersionRow Row(PromptMenuHost host, string label) => host.VersionMenu.Rows.First(r => r.Version.Label == label);

    private static PromptMenuHost Open(PromptVersionMenu? menu = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var host = PromptMenuHost.ShowVersions(menu, level);
        host.Click(host.Pill);
        return host;
    }

    #region Opening

    [AvaloniaFact]
    public void A_Press_On_The_Pill_Opens_The_Menu_352_Wide_Left_Aligned_6px_Below()
    {
        using var host = Open();
        var menu = host.VersionMenu;
        menu.IsOpen.ShouldBeTrue();
        host.Pill.IsOpen.ShouldBeTrue();
        var surface = menu.Surface!;
        surface.Bounds.Width.ShouldBe(352);
        var pill = host.BoundsOf(host.Pill);
        var box = host.BoundsOf(surface);
        box.X.ShouldBe(pill.X, 0.5);
        box.Y.ShouldBe(pill.Bottom + 6, 0.5);
        surface.Padding.ShouldBe(new Thickness(6));
        surface.CornerRadius.ShouldBe(new CornerRadius(26));
        TestHelpers.ColorOf(surface.Background).ShouldBe(ArcTokens.GetColor(surface, ArcDesignTokenKind.Surface));
        AutomationProperties.GetName(surface).ShouldBe("Versions");
        ControlAutomationPeer.CreatePeerForElement(surface).GetAutomationControlType().ShouldBe(AutomationControlType.Pane);
    }

    [AvaloniaFact]
    public void A_Second_Press_Closes_It_And_Escape_Closes_It_With_Focus_Back_On_The_Pill()
    {
        using var host = Open();
        host.Click(host.Pill);
        host.VersionMenu.IsOpen.ShouldBeFalse();
        host.Pill.IsOpen.ShouldBeFalse();
        host.VersionMenu.Presence!.Popup.IsOpen.ShouldBeFalse();

        host.OpenWithKeys();
        host.VersionMenu.IsOpen.ShouldBeTrue();
        host.Key(PhysicalKey.Escape);
        host.VersionMenu.IsOpen.ShouldBeFalse();
        host.Focused.ShouldBe(host.Pill);
        host.HasFocusRing(host.Pill).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Press_Outside_Closes_It_And_Focus_Inside_Goes_Back_To_The_Pill_Without_A_Ring()
    {
        using var host = Open();
        var closed = 0;
        host.VersionMenu.Closed += (_, _) => closed++;
        host.Click(new Point(700, 560));
        host.VersionMenu.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        // Focus was on a row, which leaves with the popover: it goes back to the pill rather than to nothing.
        host.Focused.ShouldBe(host.Pill);
        host.HasFocusRing(host.Pill).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Press_Outside_With_Focus_Elsewhere_Leaves_Focus_Alone()
    {
        var menu = PromptMenuSamples.VersionMenu();
        var pill = PromptMenuSamples.VersionPill();
        var other = new TextBox { Width = 120 };
        PromptMenuSamples.Wire(pill, menu);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(24), Children = { pill, menu, other } });
        window.Width = 760;
        window.Height = 600;
        menu.IsOpen = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        other.Focus();
        window.MouseDown(new Point(700, 560), MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(new Point(700, 560), MouseButton.Left, RawInputModifiers.None);
        menu.IsOpen.ShouldBeFalse();
        TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement().ShouldBe(other);
        window.Close();
    }

    [AvaloniaFact]
    public void Without_A_Pill_Nothing_Opens()
    {
        var menu = PromptMenuSamples.VersionMenu();
        var window = TestHelpers.Show(new StackPanel { Children = { menu } });
        menu.Open();
        menu.IsOpen.ShouldBeFalse();
        window.Close();
    }

    #endregion

    #region Rows

    [AvaloniaFact]
    public void Each_Version_Is_A_Radio_Row_With_Its_Author_Message_And_Meta()
    {
        using var host = Open();
        var rows = host.VersionMenu.Rows;
        rows.Select(r => r.Message).ShouldBe(["Name the rule before the apology", "Trim to one paragraph", "Add sign-off variable", "Initial draft from help macros"]);
        rows.Select(r => r.MetaText).ShouldBe(["v4 · Rui Tanaka · 2 days ago", "v3 · Lena Brooks · last week", "v2 · Omar Haddad · 2 weeks ago", "v1 · Rui Tanaka · 4 weeks ago"]);
        var row = rows[1];
        // 62.4 (8 + 22.4 + 2 + 22 + 8), rounded to whole pixels by layout rounding at scale 1.
        row.Bounds.Height.ShouldBe(62.4, 0.65);
        row.Bounds.Width.ShouldBe(352 - 2 - 12, 0.01);
        host.BoundsOf(rows[2]).Y.ShouldBe(host.BoundsOf(rows[1]).Bottom + 2, 0.01);
        // The label is medium in the secondary color; the rest muted.
        var inlines = row.MetaBlock!.Inlines!.OfType<Run>().ToList();
        inlines.Select(r => r.Text).ShouldBe(["v3", " · Lena Brooks · last week"]);
        inlines[0].FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(inlines[0].Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.TextSecondary));
        TestHelpers.ColorOf(row.MetaBlock.Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.TextMuted));
        var avatar = TestHelpers.Descendant<PromptVersionAvatar>(row);
        avatar.Bounds.Size.ShouldBe(new Size(28, 28));
        avatar.AuthorName.ShouldBe("Lena Brooks");
        TestHelpers.Descendant<Avatar>(avatar).Initials.ShouldBe("LB");
        ControlAutomationPeer.CreatePeerForElement(avatar).GetAutomationControlType().ShouldBe(AutomationControlType.Image);
        AutomationProperties.GetName(avatar).ShouldBe("Lena Brooks");
    }

    [AvaloniaFact]
    public void Assistive_Technology_Sees_A_Named_Radio_Group()
    {
        using var host = Open();
        var group = host.VersionMenu.RowsHost!;
        AutomationProperties.GetName(group).ShouldBe("Pick a version to compare or promote");
        ControlAutomationPeer.CreatePeerForElement(group).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        var v4 = Row(host, "v4");
        var v3 = Row(host, "v3");
        AutomationProperties.GetName(v4).ShouldBe("Name the rule before the apology, v4 · Rui Tanaka · 2 days ago, Production");
        AutomationProperties.GetName(v3).ShouldBe("Trim to one paragraph, v3 · Lena Brooks · last week");
        var peer = ControlAutomationPeer.CreatePeerForElement(v3);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.RadioButton);
        peer.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);
        var v2 = ControlAutomationPeer.CreatePeerForElement(Row(host, "v2"));
        v2.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.Off);
        v2.GetProvider<IToggleProvider>()!.Toggle();
        host.VersionMenu.SelectedVersion.ShouldBe(V(host, "v2"));
    }

    [AvaloniaFact]
    public void A_Picture_Shows_As_A_Circle_Over_The_Initials()
    {
        var versions = PromptMenuSamples.Versions();
        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96));
        versions[1].Author = new PromptAuthor { Name = "Lena Brooks", Avatar = bitmap };
        using var host = Open(PromptMenuSamples.VersionMenu(versions));
        var picture = TestHelpers.Descendant<Border>(TestHelpers.Descendant<PromptVersionAvatar>(Row(host, "v3")), "PART_Picture");
        picture.IsVisible.ShouldBeTrue();
        picture.Bounds.Size.ShouldBe(new Size(26, 26));
        TestHelpers.Descendant<Border>(TestHelpers.Descendant<PromptVersionAvatar>(Row(host, "v2")), "PART_Picture").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Pointer_Tints_A_Row_At_60_Percent_And_The_Selected_Row_Fully()
    {
        using var host = Open();
        var v2 = Row(host, "v2");
        var tint = TestHelpers.Descendant<Border>(v2, "PART_Tint");
        tint.Opacity.ShouldBe(0);
        host.Move(v2);
        tint.Opacity.ShouldBe(0.6);
        TestHelpers.Descendant<Border>(Row(host, "v3"), "PART_Tint").Opacity.ShouldBe(1);
        TestHelpers.ColorOf(tint.Background).ShouldBe(ArcTokens.GetColor(v2, ArcDesignTokenKind.SurfaceMuted));
    }

    [AvaloniaFact]
    public void Versions_Added_Or_Edited_While_Open_Show_At_Once()
    {
        var versions = new AvaloniaList<PromptVersion>(PromptMenuSamples.Versions());
        var menu = PromptMenuSamples.VersionMenu(versions.ToArray());
        menu.Versions = versions;
        using var host = Open(menu);
        versions.Insert(0, new PromptVersion { Id = "v5", Label = "v5", Message = "Shorter sign-off", Author = PromptMenuSamples.Rui(), CreatedAt = PromptMenuSamples.Now });
        host.Pump();
        host.VersionMenu.Rows.Count.ShouldBe(5);
        host.VersionMenu.Rows[0].MetaText.ShouldBe("v5 · Rui Tanaka · today");
        versions[2].Message = "Trim it to one paragraph";
        Row(host, "v3").Message.ShouldBe("Trim it to one paragraph");
    }

    #endregion

    #region Selection and actions

    [AvaloniaFact]
    public void The_Version_After_The_Open_One_Is_Selected_By_Default()
    {
        using var host = Open();
        host.VersionMenu.SelectedVersion.ShouldBe(V(host, "v3"));
        Row(host, "v3").IsChecked.ShouldBeTrue();
        Row(host, "v3").Classes.Contains(PromptVersionRow.CheckedPseudoClass).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void When_The_Oldest_Is_Open_The_One_Before_It_Is_Selected()
    {
        var versions = PromptMenuSamples.Versions();
        var menu = PromptMenuSamples.VersionMenu(versions);
        menu.OpenVersion = versions[3];
        using var host = Open(menu);
        menu.SelectedVersion.ShouldBe(versions[2]);
    }

    [AvaloniaFact]
    public void A_Selection_Is_Kept_Between_Opens()
    {
        using var host = Open();
        host.Click(Row(host, "v1"));
        host.Click(host.Pill);
        host.Click(host.Pill);
        host.VersionMenu.SelectedVersion.ShouldBe(V(host, "v1"));
        Row(host, "v1").IsChecked.ShouldBeTrue();
        host.VersionMenu.CompareAction!.Text.ShouldBe("Compare with v1");
    }

    [AvaloniaFact]
    public void The_Actions_Act_On_The_Selected_Row()
    {
        using var host = Open();
        var compare = host.VersionMenu.CompareAction!;
        var promote = host.VersionMenu.PromoteAction!;
        compare.Text.ShouldBe("Compare with v3");
        promote.Text.ShouldBe("Promote to production");
        compare.IsEnabled.ShouldBeTrue();
        promote.IsEnabled.ShouldBeTrue();
        compare.Bounds.Height.ShouldBe(36);
        TestHelpers.Descendant<ArcGlyph>(compare).Data.ShouldBe(ArcIcons.GitCompareArrows);
        TestHelpers.Descendant<ArcGlyph>(promote).Data.ShouldBe(ArcIcons.ArrowUpFromLine);

        // The open version, also in production: nothing to compare or promote (Arc's v4 shot).
        host.Click(Row(host, "v4"));
        compare.Text.ShouldBe("Compare with v4");
        compare.IsEnabled.ShouldBeFalse();
        promote.IsEnabled.ShouldBeFalse();
        TestHelpers.ColorOf(compare.Foreground).ShouldBe(ArcTokens.GetColor(compare, ArcDesignTokenKind.TextMuted));

        // In production but not open: compare yes, promote no (Arc's promoted shot).
        host.VersionMenu.ProductionVersion = V(host, "v3");
        host.Click(Row(host, "v3"));
        compare.IsEnabled.ShouldBeTrue();
        promote.IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Compare_Closes_The_Menu_Then_Reports_The_Selected_Version()
    {
        using var host = Open();
        var reported = new List<(PromptVersion Version, bool Open)>();
        host.VersionMenu.CompareRequested += (_, e) => reported.Add((e.Version, host.VersionMenu.IsOpen));
        host.Click(host.VersionMenu.CompareAction!);
        reported.ShouldHaveSingleItem();
        reported[0].Version.ShouldBe(V(host, "v3"));
        reported[0].Open.ShouldBeFalse();
        host.Focused.ShouldBe(host.Pill);
        host.HasFocusRing(host.Pill).ShouldBeFalse();
    }

    #endregion

    #region Promote and undo

    [AvaloniaFact]
    public void Promote_Keeps_The_Menu_Open_Moves_The_Marker_And_Offers_Undo()
    {
        using var host = Open();
        var menu = host.VersionMenu;
        var requested = new List<PromptVersion>();
        menu.PromoteRequested += (_, e) => requested.Add(e.Version);
        menu.Status!.Text.ShouldBe("Production serves v4");
        menu.Status.ShowsUndo.ShouldBeFalse();
        menu.MarkerVersion.ShouldBe(V(host, "v4"));

        host.Click(menu.PromoteAction!);
        requested.ShouldBe([V(host, "v3")]);
        menu.IsOpen.ShouldBeTrue();
        menu.ProductionVersion.ShouldBe(V(host, "v3"));
        menu.MarkerVersion.ShouldBe(V(host, "v3"));
        menu.IsUndoAvailable.ShouldBeTrue();
        menu.Status!.Text.ShouldBe("v3 is in production");
        menu.Status.ShowsUndo.ShouldBeTrue();
        menu.Status.UndoButton!.IsEffectivelyVisible.ShouldBeTrue();
        menu.PromoteAction!.IsEnabled.ShouldBeFalse();
        ((PromptVersionPill)host.Pill).IsProduction.ShouldBeFalse();
        // Focus does not fall to the page: it moves on to Undo.
        host.Focused.ShouldBe(menu.Status.UndoButton);
        AutomationProperties.GetName(menu.StatusSwap!).ShouldBe("v3 is in production");
        AutomationProperties.GetLiveSetting(menu.StatusSwap!).ShouldBe(AutomationLiveSetting.Polite);
    }

    [AvaloniaFact]
    public void Undo_Puts_Production_Back_And_Focus_On_The_Selected_Row()
    {
        using var host = Open();
        var menu = host.VersionMenu;
        var undone = 0;
        menu.UndoPromoteRequested += (_, _) => undone++;
        host.Click(menu.PromoteAction!);
        host.Click(menu.Status!.UndoButton!);
        undone.ShouldBe(1);
        menu.ProductionVersion.ShouldBe(V(host, "v4"));
        menu.MarkerVersion.ShouldBe(V(host, "v4"));
        menu.IsUndoAvailable.ShouldBeFalse();
        menu.Status!.Text.ShouldBe("Production serves v4");
        menu.Status.ShowsUndo.ShouldBeFalse();
        menu.PromoteAction!.IsEnabled.ShouldBeTrue();
        host.Focused.ShouldBe(Row(host, "v3"));
        ((PromptVersionPill)host.Pill).IsProduction.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Promotion_The_Studio_Turns_Down_Offers_No_Undo()
    {
        var menu = PromptMenuSamples.VersionMenu();
        var host = PromptMenuHost.Show(PromptMenuSamples.VersionPill(), menu);
        using var _ = host;
        // The studio vetoes: production goes straight back.
        menu.PropertyChanged += (_, e) =>
        {
            if (e.Property == PromptVersionMenu.ProductionVersionProperty && !ReferenceEquals(menu.ProductionVersion, menu.OpenVersion))
            {
                menu.ProductionVersion = menu.OpenVersion;
            }
        };
        host.Click(host.Pill);
        host.Click(menu.PromoteAction!);
        menu.ProductionVersion.ShouldBe(menu.OpenVersion);
        menu.IsUndoAvailable.ShouldBeFalse();
        menu.Status!.Text.ShouldBe("Production serves v4");
    }

    [AvaloniaFact]
    public void Production_Moved_Elsewhere_Takes_Undo_Away()
    {
        using var host = Open();
        var menu = host.VersionMenu;
        host.Click(menu.PromoteAction!);
        menu.IsUndoAvailable.ShouldBeTrue();
        menu.ProductionVersion = V(host, "v1");
        host.Pump();
        menu.IsUndoAvailable.ShouldBeFalse();
        menu.Status!.Text.ShouldBe("Production serves v1");
        menu.MarkerVersion.ShouldBe(V(host, "v1"));
    }

    [AvaloniaFact]
    public void The_Marker_Is_A_Bordered_Pill_At_The_End_Of_The_Production_Row()
    {
        using var host = Open();
        var menu = host.VersionMenu;
        var marker = menu.Marker!;
        var row = Row(host, "v4");
        var line = row.MetaLine!;
        var rect = marker.CurrentRect.Inflate(PromptProductionMarker.StrokeInset);
        var lineBox = new Rect(line.TranslatePoint(default, marker)!.Value, line.Bounds.Size);
        rect.Right.ShouldBe(lineBox.Right, 0.01);
        rect.Height.ShouldBe(22);
        rect.Y.ShouldBe(lineBox.Y, 0.01);
        rect.Width.ShouldBe(marker.PillWidth, 0.01);
        marker.CurrentOpacity.ShouldBe(1);
        // The meta text keeps clear of it.
        row.MetaBlock!.Margin.Right.ShouldBe(marker.PillWidth + 8, 0.01);
        Row(host, "v3").MetaBlock!.Margin.Right.ShouldBe(0);
        var (fill, stroke, dot, text) = marker.DrawColors;
        fill.ShouldBe(ArcTokens.GetColor(marker, ArcDesignTokenKind.Surface));
        dot.ShouldBe(ArcTokens.GetColor(marker, ArcDesignTokenKind.Success));
        text.ShouldBe(ArcTokens.GetColor(marker, ArcDesignTokenKind.TextSecondary));
        stroke.ShouldBe(PromptTone.Mix(ArcTokens.GetColor(marker, ArcDesignTokenKind.Border), dot, 0.26));
    }

    [AvaloniaFact]
    public void Reopened_The_Marker_Is_On_Its_Row_At_Once()
    {
        using var host = Open();
        var menu = host.VersionMenu;
        var first = menu.Marker!.CurrentRect;
        host.Click(host.Pill);
        host.Click(host.Pill);
        menu.Marker!.CurrentOpacity.ShouldBe(1);
        menu.Marker.CurrentRect.ShouldBe(first);
        // Production moved while closed: the marker lands on the new row without gliding.
        host.Click(host.Pill);
        menu.ProductionVersion = V(host, "v2");
        host.Click(host.Pill);
        menu.MarkerVersion.ShouldBe(V(host, "v2"));
        var line = Row(host, "v2").MetaLine!;
        menu.Marker.CurrentRect.Y.ShouldBe(line.TranslatePoint(default, menu.Marker)!.Value.Y + PromptProductionMarker.StrokeInset, 0.01);
    }

    [AvaloniaFact]
    public void The_Marker_Glides_To_The_Promoted_Row_On_The_Smooth_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = PromptMenuHost.ShowVersions(level: ArcMotionLevel.Full);
        host.Click(host.Pill);
        CoreFrames.Run(clock, 60);
        host.Pump();
        var menu = host.VersionMenu;
        var marker = menu.Marker!;
        var start = marker.CurrentRect.Y;
        host.Click(menu.PromoteAction!);
        host.Pump();
        var target = marker.CurrentRect.Y;
        // The target moved one row plus the 2px gap down; the indicator starts from where it was.
        var line = Row(host, "v3").MetaLine!;
        var pitch = Row(host, "v3").Bounds.Y - Row(host, "v4").Bounds.Y;
        pitch.ShouldBe(64.4, 0.65);
        var expected = line.TranslatePoint(default, marker)!.Value.Y + PromptProductionMarker.StrokeInset;
        expected.ShouldBe(start + pitch, 0.01);
        target.ShouldBe(start, 0.01);
        var t = CoreFrames.Run(clock, 8);
        var y = marker.CurrentRect.Y;
        var spring = expected + SpringSolver.Evaluate(ArcMotionPresets.Smooth, start - expected, 0, t).Displacement;
        y.ShouldBe(spring, 0.05);
        y.ShouldBeGreaterThan(start + 20);
        y.ShouldBeLessThan(expected - 5);
        CoreFrames.Run(clock, 60);
        marker.CurrentRect.Y.ShouldBe(expected, 0.01);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Marker()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = PromptMenuHost.ShowVersions(level: ArcMotionLevel.Reduced);
        host.Click(host.Pill);
        CoreFrames.Run(clock, 30);
        host.Pump();
        var marker = host.VersionMenu.Marker!;
        var start = marker.CurrentRect.Y;
        host.Click(host.VersionMenu.PromoteAction!);
        host.Pump();
        var pitch = Row(host, "v3").Bounds.Y - Row(host, "v4").Bounds.Y;
        marker.CurrentRect.Y.ShouldBe(start + pitch, 0.01);
    }

    [AvaloniaFact]
    public void The_Last_Line_Crossfades_As_A_Label()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = PromptMenuHost.ShowVersions(level: ArcMotionLevel.Full);
        host.Click(host.Pill);
        CoreFrames.Run(clock, 60);
        host.Pump();
        var swap = host.VersionMenu.StatusSwap!;
        swap.Mode.ShouldBe(ContentSwapMode.Label);
        host.Click(host.VersionMenu.PromoteAction!);
        PromptVersionPillTests.Layers(swap).ShouldBe(2);
        CoreFrames.Run(clock, 30);
        PromptVersionPillTests.WaitFor(() => PromptVersionPillTests.Layers(swap) == 1).ShouldBeTrue();
    }

    #endregion

    #region Keys and focus

    [AvaloniaFact]
    public void Opened_With_Keys_Focus_Lands_On_The_Selected_Row_With_Its_Ring()
    {
        using var host = PromptMenuHost.ShowVersions();
        host.OpenWithKeys();
        var v3 = Row(host, "v3");
        host.Focused.ShouldBe(v3);
        host.HasFocusRing(v3).ShouldBeTrue();
        TestHelpers.Descendant<Border>(v3, "PART_FocusRing").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<Border>(v3, "PART_FocusRing").Margin.ShouldBe(default);
    }

    [AvaloniaFact]
    public void Opened_With_The_Pointer_Focus_Lands_On_The_Selected_Row_Without_A_Ring()
    {
        using var host = Open();
        var v3 = Row(host, "v3");
        host.Focused.ShouldBe(v3);
        host.HasFocusRing(v3).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Arrows_Move_And_Select_Wrapping_Home_And_End_Go_To_The_Ends()
    {
        using var host = PromptMenuHost.ShowVersions();
        host.OpenWithKeys();
        var menu = host.VersionMenu;
        host.Key(PhysicalKey.ArrowDown);
        menu.SelectedVersion.ShouldBe(V(host, "v2"));
        host.Focused.ShouldBe(Row(host, "v2"));
        host.Key(PhysicalKey.ArrowDown);
        host.Key(PhysicalKey.ArrowDown);
        menu.SelectedVersion.ShouldBe(V(host, "v4"));
        host.Key(PhysicalKey.ArrowUp);
        menu.SelectedVersion.ShouldBe(V(host, "v1"));
        host.Key(PhysicalKey.Home);
        menu.SelectedVersion.ShouldBe(V(host, "v4"));
        host.Key(PhysicalKey.End);
        menu.SelectedVersion.ShouldBe(V(host, "v1"));
        host.Key(PhysicalKey.ArrowLeft);
        menu.SelectedVersion.ShouldBe(V(host, "v2"));
        host.Key(PhysicalKey.ArrowRight);
        menu.SelectedVersion.ShouldBe(V(host, "v1"));
        menu.CompareAction!.Text.ShouldBe("Compare with v1");
    }

    [AvaloniaFact]
    public void Enter_On_A_Row_Does_Nothing()
    {
        using var host = PromptMenuHost.ShowVersions();
        host.OpenWithKeys();
        var compared = 0;
        host.VersionMenu.CompareRequested += (_, _) => compared++;
        host.Key(PhysicalKey.Enter);
        host.VersionMenu.IsOpen.ShouldBeTrue();
        host.VersionMenu.SelectedVersion.ShouldBe(V(host, "v3"));
        compared.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Tab_Cycles_The_Selected_Row_The_Enabled_Actions_And_Undo()
    {
        using var host = PromptMenuHost.ShowVersions();
        host.OpenWithKeys();
        var menu = host.VersionMenu;
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(menu.CompareAction);
        host.HasFocusRing(menu.CompareAction!).ShouldBeTrue();
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(menu.PromoteAction);
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(Row(host, "v3"));
        host.Key(PhysicalKey.Tab, RawInputModifiers.Shift);
        host.Focused.ShouldBe(menu.PromoteAction);

        // Promote with Enter: focus goes to Undo (with its ring), which is the last stop now; Promote is skipped.
        host.Key(PhysicalKey.Enter);
        var undo = menu.Status!.UndoButton!;
        host.Focused.ShouldBe(undo);
        host.HasFocusRing(undo).ShouldBeTrue();
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(Row(host, "v3"));
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(menu.CompareAction);
        host.Key(PhysicalKey.Tab);
        host.Focused.ShouldBe(undo);
        menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Arrows_On_An_Action_Do_Not_Move_The_Selection()
    {
        using var host = PromptMenuHost.ShowVersions();
        host.OpenWithKeys();
        host.Key(PhysicalKey.Tab);
        host.Key(PhysicalKey.ArrowDown);
        host.VersionMenu.SelectedVersion.ShouldBe(V(host, "v3"));
        host.Focused.ShouldBe(host.VersionMenu.CompareAction);
    }

    [AvaloniaFact]
    public void Compare_With_The_Keys_Gives_Focus_Back_To_The_Pill_With_Its_Ring()
    {
        using var host = PromptMenuHost.ShowVersions();
        host.OpenWithKeys();
        PromptVersion? compared = null;
        host.VersionMenu.CompareRequested += (_, e) => compared = e.Version;
        host.Key(PhysicalKey.Tab);
        host.Key(PhysicalKey.Enter);
        compared.ShouldBe(V(host, "v3"));
        host.Focused.ShouldBe(host.Pill);
        host.HasFocusRing(host.Pill).ShouldBeTrue();
    }

    #endregion

    #region Motion of the popover

    [AvaloniaFact]
    public void The_Popover_Fades_In_From_5px_Above_At_97_Percent_About_Its_Top_Left_Corner_Then_Leaves_In_130ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = PromptMenuHost.ShowVersions(level: ArcMotionLevel.Full);
        var closed = 0;
        host.VersionMenu.Closed += (_, _) => closed++;
        host.Click(host.Pill);
        var presence = host.VersionMenu.Presence!;
        presence.CurrentOpacity.ShouldBe(0);
        presence.CurrentPose.ShouldBe(new Point(5, 0.97));
        var matrix = presence.CurrentMatrix;
        // Scaled about the top-left corner and moved up toward the pill: the corner stays, only shifted up by 5.
        var surface = host.VersionMenu.Surface!;
        var actor = surface.FindAncestorOfType<ArcPopupFrame>()!;
        var corner = surface.TranslatePoint(default, actor)!.Value;
        matrix.Transform(corner).ShouldBe(new Point(corner.X, corner.Y - 5));

        var t = CoreFrames.Run(clock, 5);
        presence.CurrentOpacity.ShouldBe(ArcEasings.Enter.Ease(Math.Min(1, t / 0.16)), 0.001);
        presence.CurrentPose.X.ShouldBe(5 * (1 - ArcEasings.CssSpring.Ease(t / 0.58)), 0.001);
        CoreFrames.Run(clock, 40);
        presence.CurrentPose.ShouldBe(new Point(0, 1));
        presence.CurrentOpacity.ShouldBe(1);

        host.Key(PhysicalKey.Escape);
        presence.Popup.IsOpen.ShouldBeTrue();
        t = CoreFrames.Run(clock, 4);
        presence.CurrentOpacity.ShouldBe(1 - ArcEasings.Standard.Ease(t / 0.13), 0.001);
        presence.CurrentPose.X.ShouldBe(2.5 * ArcEasings.Standard.Ease(t / 0.13), 0.001);
        presence.CurrentPose.Y.ShouldBe(1 - 0.02 * ArcEasings.Standard.Ease(t / 0.13), 0.0001);
        CoreFrames.Run(clock, 6);
        host.Pump();
        presence.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Only_Fades_The_Popover_In_120ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = PromptMenuHost.ShowVersions(level: ArcMotionLevel.Reduced);
        host.Click(host.Pill);
        var presence = host.VersionMenu.Presence!;
        presence.CurrentPose.ShouldBe(new Point(0, 1));
        var t = CoreFrames.Run(clock, 3);
        presence.CurrentOpacity.ShouldBe(t / 0.12, 0.001);
        CoreFrames.Run(clock, 5);
        presence.CurrentOpacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Motion_Off_Opens_And_Closes_At_Once()
    {
        using var host = Open();
        var presence = host.VersionMenu.Presence!;
        presence.CurrentOpacity.ShouldBe(1);
        presence.CurrentPose.ShouldBe(new Point(0, 1));
        host.Key(PhysicalKey.Escape);
        presence.Popup.IsOpen.ShouldBeFalse();
    }

    #endregion

    [AvaloniaFact]
    public void The_Menu_Holds_No_Words_Of_Its_Own()
    {
        var versions = PromptMenuSamples.Versions();
        var menu = new PromptVersionMenu { Versions = versions, OpenVersion = versions[0], ProductionVersion = versions[0] };
        using var host = PromptMenuHost.Show(PromptMenuSamples.VersionPill(), menu);
        host.Click(host.Pill);
        menu.Rows.Count.ShouldBe(4);
        menu.Rows[0].MetaText.ShouldBe(string.Empty);
        menu.CompareAction!.Text.ShouldBeNull();
        menu.Status!.Text.ShouldBe(string.Empty);
        // No word, no marker.
        menu.MarkerVersion.ShouldBeNull();
    }
}
