using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="CustomerGalaxy"/>: defaults, totals and list, groupings, focus, selection, keyboard, hit testing, the
/// hover card, container widths, motion levels, theme colors, commands and screen-reader text.
/// </summary>
public class CustomerGalaxyTests
{
    private static void Settle(int ticks = 3)
    {
        for (var i = 0; i < ticks; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static (CustomerGalaxy Galaxy, Window Window) Show(double width = 664, ArcMotionLevel level = ArcMotionLevel.Off,
                                                                IReadOnlyList<GalaxyAccount>? accounts = null, bool groupings = true)
    {
        var galaxy = new CustomerGalaxy();
        if (groupings)
        {
            foreach (var grouping in CustomerGalaxyDemoData.Groupings())
            {
                galaxy.Groupings.Add(grouping);
            }
        }
        galaxy.ItemsSource = accounts ?? CustomerGalaxyDemoData.Accounts;
        var window = TestHelpers.Show(galaxy, level);
        window.Width = width;
        window.Height = 1200;
        Settle();
        return (galaxy, window);
    }

    private static GalaxyStage Stage(CustomerGalaxy galaxy) => galaxy.Stage.ShouldNotBeNull();

    private static Point OnWindow(Window window, Visual visual, Point local) => visual.TranslatePoint(local, window)!.Value;

    [AvaloniaFact]
    public void Defaults_Match_The_Arc_Preview()
    {
        var galaxy = new CustomerGalaxy();
        galaxy.MaxZoom.ShouldBe(9);
        galaxy.ZoomStep.ShouldBe(1.6);
        galaxy.WheelZoomSensitivity.ShouldBe(0.0022);
        galaxy.RotationSpeed.ShouldBe(1.6);
        galaxy.IsAutoRotate.ShouldBeTrue();
        galaxy.TotalsPlacement.ShouldBe(GalaxyTotalsPlacement.Auto);
        galaxy.GroupSort.ShouldBe(GalaxyGroupSort.Mrr);
        galaxy.SelectedGroupingKey.ShouldBeNull();
        galaxy.Groupings.ShouldBeEmpty();
        galaxy.ShowLegend.ShouldBeTrue();
        galaxy.ShowTotals.ShouldBeTrue();
        galaxy.ShowHint.ShouldBeTrue();
        galaxy.ShowZoomControls.ShouldBeTrue();
        galaxy.HealthStates.Select(s => s.Tone).ShouldBe([GalaxyTone.Success, GalaxyTone.Neutral, GalaxyTone.Warning, GalaxyTone.Danger]);
        galaxy.HealthStates.Select(s => s.DotOpacity).ShouldBe([0.95, 0.72, 0.95, 0.95]);
        // Paint order: neutral first, risk on top.
        galaxy.HealthStates.OrderBy(s => s.Layer).Select(s => s.Key).ShouldBe(["steady", "growing", "cooling", "at-risk"]);
        galaxy.HealthStates.Single(s => s.IsGrowth).Key.ShouldBe("growing");
        galaxy.HealthStates.Single(s => s.IsRisk).Key.ShouldBe("at-risk");
    }

    [AvaloniaFact]
    public void The_Overview_Totals_Everything_And_Lists_Health_By_Mrr()
    {
        var (galaxy, window) = Show();
        try
        {
            var accounts = CustomerGalaxyDemoData.Accounts;
            galaxy.Summary.AccountCount.ShouldBe(2400);
            galaxy.Summary.TotalMrr.ShouldBe(accounts.Sum(a => a.Mrr), 0.01);
            galaxy.Summary.GrowthMrr.ShouldBe(accounts.Where(a => a.Health == "growing").Sum(a => a.Mrr), 0.01);
            galaxy.Summary.AtRiskMrr.ShouldBe(accounts.Where(a => a.Health == "at-risk").Sum(a => a.Mrr), 0.01);
            var rows = galaxy.Rows;
            rows.Count.ShouldBe(4);
            rows[0].Key.ShouldBe("steady");
            rows.Select(r => galaxy.Summary.Groups.Single(g => g.Key == r.Key).Mrr).ShouldBeInOrder(SortDirection.Descending);
            rows[0].Caption.ShouldBe($"{accounts.Count(a => a.Health == "steady"):N0} accounts");
            TestHelpers.Descendant<TextBlock>(galaxy, CustomerGalaxy.ListTitlePart).Text.ShouldBe("By health");
            TestHelpers.Descendant<ShowpieceNumber>(galaxy, CustomerGalaxy.MrrPart).DisplayText
                .ShouldBe(ShowpieceFormat.CompactCurrency(galaxy.Summary.TotalMrr));
            TestHelpers.Descendant<ShowpieceNumber>(galaxy, CustomerGalaxy.AccountsPart).DisplayText.ShouldBe("2,400");
            galaxy.ClusterLabels.ShouldBeEmpty();
            galaxy.Classes.ShouldNotContain(":grouped");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Grouping_Clusters_The_Dots_Labels_Them_And_Lists_The_Clusters()
    {
        var (galaxy, window) = Show();
        try
        {
            var changes = new List<(string? Old, string? New)>();
            galaxy.GroupingChanged += (_, e) => changes.Add((e.OldKey, e.NewKey));
            galaxy.SelectedGroupingKey = "plan";
            Settle();
            changes.ShouldBe([(null, "plan")]);
            galaxy.LayoutSet.ShouldNotBeNull().Key.ShouldBe("plan");
            galaxy.ClusterLabels.Select(l => l.Text).ShouldBe(["Corporate", "Business", "Team", "Solo"]);
            galaxy.ClusterLabels[0].Detail.ShouldBe(" · 70 accounts");
            TestHelpers.Descendant<TextBlock>(galaxy, CustomerGalaxy.ListTitlePart).Text.ShouldBe("By plan");
            galaxy.Rows.Select(r => r.Key).ShouldBe(["corporate", "business", "team", "solo"]);
            galaxy.Rows[0].Caption.ShouldEndWith("% at risk");
            galaxy.Table.ShouldNotBeNull().Rows.Count.ShouldBe(4);
            galaxy.LastAnnouncement.ShouldNotBeNull().ShouldStartWith("4 clusters by plan.");
            Stage(galaxy).View.IsAtFit.ShouldBeTrue();
            // The tab strip follows.
            TestHelpers.Descendant<GalaxyTabStrip>(galaxy).SelectedIndex.ShouldBe(1);
            // An unknown grouping shows the overview.
            galaxy.SelectedGroupingKey = "colour";
            Settle();
            galaxy.SelectedGroupingKey.ShouldBeNull();
            galaxy.LayoutSet.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focusing_A_Cluster_Flies_In_Dims_The_Rest_And_Switches_The_Totals()
    {
        var (galaxy, window) = Show();
        try
        {
            galaxy.SelectedGroupingKey = "plan";
            Settle();
            var focused = new List<string?>();
            galaxy.GroupFocused += (_, e) => focused.Add(e.GroupKey);
            galaxy.FocusedGroupKey = "corporate";
            Settle();
            focused.ShouldBe(["corporate"]);
            galaxy.Classes.ShouldContain(":cluster-focused");
            var stage = Stage(galaxy);
            var layout = galaxy.LayoutSet!;
            var index = layout.IndexOf("corporate");
            stage.FocusGroup.ShouldBe(index);
            // The disc and its label fill 75% of the height, the disc centered 23.7 px below the stage center.
            var camera = stage.View;
            var center = camera.WorldToScreen(layout.Centers[index]);
            center.X.ShouldBe(stage.Bounds.Width / 2, 0.5);
            center.Y.ShouldBe(stage.Bounds.Height / 2 + 23.7, 0.5);
            (2 * layout.Radii[index] * camera.Scale + 47.4).ShouldBe(0.75 * stage.Bounds.Height, 0.5);
            // Every other cluster at the lowest level, the focused one full.
            for (var g = 0; g < layout.Count; g++)
            {
                stage.Field.DimLevels[g].ShouldBe(g == index ? GalaxyLayout.DimLevels - 1 : 0);
                galaxy.ClusterLabels[g].IsDimmed.ShouldBe(g != index);
            }
            galaxy.Rows.Single(r => r.Key == "corporate").IsChecked.ShouldBe(true);
            galaxy.Rows.Where(r => r.Key != "corporate").ShouldAllBe(r => r.IsChecked == false);
            TestHelpers.Descendant<ShowpieceNumber>(galaxy, CustomerGalaxy.MrrPart).DisplayText
                .ShouldBe(ShowpieceFormat.CompactCurrency(layout.Groups[index].Mrr));
            TestHelpers.Descendant<ShowpieceNumber>(galaxy, CustomerGalaxy.AccountsPart).DisplayText.ShouldBe("70");
            TestHelpers.Descendant<PresenceHost>(galaxy, CustomerGalaxy.ShowAllHostPart).IsPresent.ShouldBeTrue();
            TestHelpers.Descendant<ShowpieceZoomControls>(galaxy).CanFit.ShouldBeTrue();
            galaxy.LastAnnouncement.ShouldNotBeNull().ShouldStartWith("Corporate, 70 accounts");

            galaxy.ResetView();
            Settle();
            galaxy.FocusedGroupKey.ShouldBeNull();
            focused[^1].ShouldBeNull();
            camera.IsAtFit.ShouldBeTrue();
            stage.Field.DimLevels.ShouldAllBe(level => level == GalaxyLayout.DimLevels - 1);
            TestHelpers.Descendant<PresenceHost>(galaxy, CustomerGalaxy.ShowAllHostPart).IsPresent.ShouldBeFalse();
            TestHelpers.Descendant<ShowpieceZoomControls>(galaxy).CanFit.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Fly_Between_Clusters_Step_Through_Accounts_Zoom_And_Go_Back()
    {
        var (galaxy, window) = Show();
        try
        {
            var stage = Stage(galaxy);
            stage.Focus();
            // The overview has no clusters: arrows are not handled.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            galaxy.FocusedGroupKey.ShouldBeNull();

            galaxy.SelectedGroupingKey = "plan";
            Settle();
            stage.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            galaxy.FocusedGroupKey.ShouldBe("corporate");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            galaxy.FocusedGroupKey.ShouldBe("business");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            galaxy.FocusedGroupKey.ShouldBe("solo", "← wraps to the last cluster");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            galaxy.FocusedGroupKey.ShouldBe("corporate");

            var members = galaxy.LayoutSet!.Members[0];
            var model = galaxy.Model;
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            galaxy.SelectedAccount.ShouldBe(model.Accounts[members[0]]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            galaxy.SelectedAccount.ShouldBe(model.Accounts[members[1]]);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            galaxy.SelectedAccount.ShouldBe(model.Accounts[members[0]], "↑ stays on the first account");
            galaxy.LastAnnouncement.ShouldNotBeNull().ShouldStartWith(model.Accounts[members[0]].Name!);
            Settle();
            // The card is pinned beside the selected dot.
            var card = galaxy.Card.ShouldNotBeNull();
            card.IsOpen.ShouldBeTrue();
            var (dot, radius) = stage.DotOnScreen(members[0]);
            card.Anchor.ShouldBe(new Point(dot.X + radius, dot.Y));
            card.Placement.Gap.ShouldBe(12);

            window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.None);
            stage.View.TargetZoom.ShouldBe(stage.View.Zoom, 1e-6);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            galaxy.SelectedAccount.ShouldBeNull("Escape clears the selection first");
            galaxy.FocusedGroupKey.ShouldBe("corporate");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            galaxy.FocusedGroupKey.ShouldBeNull("then returns to every cluster");
            stage.View.IsAtFit.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.None);
            stage.View.TargetZoom.ShouldBe(1.6, 1e-6);
            window.KeyPressQwerty(PhysicalKey.Minus, RawInputModifiers.None);
            stage.View.TargetZoom.ShouldBe(1, 1e-6);
            window.KeyPressQwerty(PhysicalKey.Minus, RawInputModifiers.None);
            stage.View.TargetZoom.ShouldBe(1, 1e-6, "nothing below fit");
            window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.None);
            stage.View.IsAtFit.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    private static IReadOnlyList<GalaxyAccount> TwoAccounts() =>
    [
        new GalaxyAccount { Id = "big", Name = "Big", Mrr = 90000, Health = "growing", Owner = "Ada", Note = "Seats up" },
        new GalaxyAccount { Id = "small", Name = "Small", Mrr = 100, Health = "steady" }
    ];

    [AvaloniaFact]
    public void A_Dot_Is_Hit_Within_Its_Radius_Plus_Ten_Pixels()
    {
        var (galaxy, window) = Show(accounts: TwoAccounts(), groupings: false);
        try
        {
            var stage = Stage(galaxy);
            var (center, radius) = stage.DotOnScreen(0);
            var (other, _) = stage.DotOnScreen(1);
            // Away from the small dot.
            var away = new Vector(center.X - other.X, center.Y - other.Y);
            away /= Math.Sqrt(away.X * away.X + away.Y * away.Y);
            stage.HitIndex(center).ShouldBe(0);
            stage.HitIndex(center + away * (radius + 9)).ShouldBe(0);
            stage.HitIndex(center + away * (radius + 11)).ShouldBe(-1);
            stage.HitIndex(other).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_Shows_The_Card_At_The_Pointer_And_A_Click_Selects()
    {
        var (galaxy, window) = Show(accounts: TwoAccounts(), groupings: false);
        try
        {
            var stage = Stage(galaxy);
            var (center, radius) = stage.DotOnScreen(0);
            var pointer = center + new Point(2, 0);
            window.MouseMove(OnWindow(window, stage, pointer));
            Settle();
            galaxy.HoveredAccount.ShouldNotBeNull().Name.ShouldBe("Big");
            stage.HoveredIndex.ShouldBe(0);
            var card = galaxy.Card.ShouldNotBeNull();
            card.IsOpen.ShouldBeTrue();
            card.Anchor.ShouldBe(pointer);
            card.Placement.ShouldBe(ShowpieceCardPlacement.Galaxy);
            var content = card.Content.ShouldBeOfType<GalaxyAccountCard>();
            content.Title.ShouldBe("Big");
            content.MrrValue.ShouldBe("$90,000");
            content.HealthValue.ShouldBe("Growing");
            content.OwnerValue.ShouldBe("Ada");
            content.Note.ShouldBe("Seats up");

            GalaxyAccount? selected = null;
            galaxy.AccountSelected += (_, e) => selected = e.Account;
            window.MouseDown(OnWindow(window, stage, pointer), MouseButton.Left);
            window.MouseUp(OnWindow(window, stage, pointer), MouseButton.Left);
            Settle();
            galaxy.SelectedAccount.ShouldNotBeNull().Name.ShouldBe("Big");
            selected.ShouldBe(galaxy.SelectedAccount);
            stage.SelectedIndex.ShouldBe(0);

            // The pointer leaves the stage: the card stays, pinned 12 px right of the dot.
            window.MouseMove(new Point(window.Width - 2, window.Height - 2));
            Settle();
            galaxy.HoveredAccount.ShouldBeNull();
            card.IsOpen.ShouldBeTrue();
            card.Anchor.ShouldBe(new Point(center.X + radius, center.Y));

            // A click on empty space clears the selection and the card.
            var empty = new Point(10, 10);
            window.MouseMove(OnWindow(window, stage, empty));
            window.MouseDown(OnWindow(window, stage, empty), MouseButton.Left);
            window.MouseUp(OnWindow(window, stage, empty), MouseButton.Left);
            Settle();
            galaxy.SelectedAccount.ShouldBeNull();
            selected.ShouldBeNull();
            card.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Card_Template_Is_Only_Built_With_An_Account()
    {
        var built = new List<GalaxyAccount?>();
        var galaxy = new CustomerGalaxy
        {
            ItemsSource = TwoAccounts(),
            AccountTooltipTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GalaxyAccount>((account, _) =>
            {
                built.Add(account);
                return new TextBlock { Text = account.Name };
            })
        };
        var window = TestHelpers.Show(galaxy);
        try
        {
            window.Width = 664;
            window.Height = 900;
            Settle();
            var stage = Stage(galaxy);
            var (big, _) = stage.DotOnScreen(0);
            var (small, _) = stage.DotOnScreen(1);
            window.MouseMove(OnWindow(window, stage, big));
            Settle();
            window.MouseMove(OnWindow(window, stage, small));
            Settle();
            window.MouseDown(OnWindow(window, stage, small), MouseButton.Left);
            window.MouseUp(OnWindow(window, stage, small), MouseButton.Left);
            window.MouseMove(new Point(window.Width - 2, window.Height - 2));
            Settle();
            galaxy.SelectedAccount = null;
            galaxy.AccountTooltipTemplate = null;
            Settle();
            built.ShouldNotBeEmpty();
            built.ShouldAllBe(account => account != null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Double_Click_On_A_Cluster_Flies_Into_It()
    {
        var (galaxy, window) = Show();
        try
        {
            galaxy.SelectedGroupingKey = "plan";
            Settle();
            var stage = Stage(galaxy);
            var layout = galaxy.LayoutSet!;
            var team = layout.IndexOf("team");
            var point = OnWindow(window, stage, stage.View.WorldToScreen(layout.Centers[team]));
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            galaxy.FocusedGroupKey.ShouldBe("team");
            // Its label also counts.
            var label = stage.LabelRect(layout.IndexOf("solo"));
            stage.ClusterAt(label.Center).ShouldBe(layout.IndexOf("solo"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Wheel_Zooms_About_The_Pointer_Within_Fit_And_Nine_Times()
    {
        // Full motion: the bounds wait for the gesture to end, so the anchor holds (at reduced motion they apply at once).
        var (galaxy, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var stage = Stage(galaxy);
            var camera = stage.View;
            camera.Stop();
            var anchor = new Point(200, 120);
            var world = camera.ScreenToWorld(anchor);
            window.MouseWheel(OnWindow(window, stage, anchor), new Vector(0, 1));
            camera.TargetZoom.ShouldBe(Math.Exp(0.22), 1e-6);
            var size = stage.Bounds.Size;
            var target = new Point((world.X - camera.TargetCenter.X) * camera.TargetScale + size.Width / 2,
                (world.Y - camera.TargetCenter.Y) * camera.TargetScale + size.Height / 2);
            target.X.ShouldBe(anchor.X, 0.01);
            target.Y.ShouldBe(anchor.Y, 0.01);
            for (var i = 0; i < 20; i++)
            {
                window.MouseWheel(OnWindow(window, stage, anchor), new Vector(0, 1));
            }
            camera.TargetZoom.ShouldBe(9, 1e-6);
            for (var i = 0; i < 20; i++)
            {
                window.MouseWheel(OnWindow(window, stage, anchor), new Vector(0, -1));
            }
            camera.TargetZoom.ShouldBe(1, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Zoom_Buttons_Step_By_1_6_And_Fit_Enables_Away_From_Fit()
    {
        var (galaxy, window) = Show();
        try
        {
            var zoom = TestHelpers.Descendant<ShowpieceZoomControls>(galaxy);
            zoom.CanFit.ShouldBeFalse();
            zoom.FitText.ShouldBe("Show every account");
            zoom.Request(ShowpieceZoomAction.ZoomIn);
            Settle();
            Stage(galaxy).View.TargetZoom.ShouldBe(1.6, 1e-6);
            zoom.CanFit.ShouldBeTrue();
            zoom.Request(ShowpieceZoomAction.Fit);
            Settle();
            Stage(galaxy).View.IsAtFit.ShouldBeTrue();
            zoom.CanFit.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void List_Rows_Fly_To_Their_Cluster_And_Hovering_One_Dims_The_Others()
    {
        var (galaxy, window) = Show();
        try
        {
            // In the overview a row groups by health first.
            var risk = galaxy.Rows.Single(r => r.Key == "at-risk");
            var point = OnWindow(window, risk, new Point(30, 20));
            window.MouseMove(point);
            Settle();
            galaxy.Classes.ShouldContain(":row-hover");
            var stage = Stage(galaxy);
            var health = galaxy.Model.HealthSet;
            stage.Field.DimLevels[health.IndexOf("steady")].ShouldBe(0);
            stage.Field.DimLevels[health.IndexOf("at-risk")].ShouldBe(GalaxyLayout.DimLevels - 1);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            galaxy.SelectedGroupingKey.ShouldBe("health");
            galaxy.FocusedGroupKey.ShouldBe("at-risk");

            galaxy.SelectedGroupingKey = "plan";
            Settle();
            var team = galaxy.Rows.Single(r => r.Key == "team");
            window.MouseMove(OnWindow(window, team, new Point(30, 20)));
            Settle();
            var layout = galaxy.LayoutSet!;
            for (var g = 0; g < layout.Count; g++)
            {
                galaxy.ClusterLabels[g].IsDimmed.ShouldBe(layout.Groups[g].Key != "team");
            }
            galaxy.FocusedGroupKey.ShouldBeNull("hovering a row does not move the camera");
            window.MouseMove(new Point(2, 2));
            Settle();
            galaxy.Classes.ShouldNotContain(":row-hover");
            galaxy.ClusterLabels.ShouldAllBe(l => !l.IsDimmed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sorting_By_Accounts_Orders_And_Measures_Rows_By_Count()
    {
        var (galaxy, window) = Show();
        try
        {
            galaxy.SelectedGroupingKey = "region";
            Settle();
            galaxy.GroupSort = GalaxyGroupSort.Accounts;
            Settle();
            var counts = galaxy.Rows.Select(r => galaxy.Summary.Groups.Single(g => g.Key == r.Key).AccountCount).ToList();
            counts.ShouldBeInOrder(SortDirection.Descending);
            var first = galaxy.Rows[0];
            first.ValueNumber.ShouldNotBeNull().DisplayText.ShouldBe(counts[0].ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
            first.Bar.ShouldNotBeNull().Fraction.ShouldBe(1);
            TestHelpers.Descendant<GalaxySortButton>(galaxy, CustomerGalaxy.SortAccountsPart).IsChecked.ShouldBe(true);
            TestHelpers.Descendant<GalaxySortButton>(galaxy, CustomerGalaxy.SortMrrPart).IsChecked.ShouldBe(false);
            // Stacked health segments weigh each state's MRR.
            var group = galaxy.Summary.Groups.Single(g => g.Key == first.Key);
            first.Bar!.Weights.ShouldBe(group.HealthMrr);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Container_Width_Picks_The_Layout()
    {
        var (galaxy, window) = Show(width: 1312);
        try
        {
            var body = TestHelpers.Descendant<GalaxyBodyPanel>(galaxy);
            var totals = TestHelpers.Descendant<GalaxyTotalsPanel>(galaxy);
            body.IsSide.ShouldBeTrue();
            galaxy.Classes.ShouldContain(":side");
            Stage(galaxy).Bounds.Width.ShouldBe(1312 - 2 - 288, 0.5);
            // 16:9 capped at 560, unless the panel is taller (its text measures a little differently in the test host).
            var panel = ((Control)totals.GetVisualParent()!).DesiredSize.Height;
            Stage(galaxy).Bounds.Height.ShouldBe(GalaxyLayout.SideStageHeight(1022, panel), 1.01);
            Stage(galaxy).Bounds.Height.ShouldBeInRange(559.5, 575);
            // 288 with the 1 px divider on its left.
            ((Control)totals.GetVisualParent()!).Bounds.Width.ShouldBe(288, 1.01);
            totals.IsSplit.ShouldBeFalse();

            window.Width = 664;
            Settle();
            body.IsSide.ShouldBeFalse();
            Stage(galaxy).Bounds.Height.ShouldBe(360, 0.5);
            totals.IsSplit.ShouldBeTrue();
            TestHelpers.Descendant<GalaxyToolbarPanel>(galaxy).IsOneRow.ShouldBeFalse("the legend moves under the tabs at 664");

            window.Width = 380;
            Settle();
            Stage(galaxy).Bounds.Height.ShouldBe(380, 0.5);
            totals.IsSplit.ShouldBeFalse();
            // The camera refits on every resize.
            Stage(galaxy).View.IsAtFit.ShouldBeTrue();
            Stage(galaxy).FitScale.ShouldBe(GalaxyLayout.AllFit(Stage(galaxy).Bounds.Size, galaxy.Model.CoreRadius), 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Legend_Drops_Its_Divider_When_The_Size_Entry_Wraps()
    {
        var legend = new GalaxyLegendPanel { ItemGap = 16, LineGap = 6 };
        for (var i = 0; i < 4; i++)
        {
            legend.Children.Add(new Border { Width = 60, Height = 17 });
        }
        legend.Children.Add(new GalaxyLegendDivider { Width = 1 });
        legend.Children.Add(new Border { Width = 100, Height = 17 });
        // 4 × 60 + 3 × 16 = 288, then 16 + 1 + 16 + 100 = 421 on one line.
        legend.Measure(new Size(421, double.PositiveInfinity));
        legend.Arrange(new Rect(legend.DesiredSize));
        legend.IsDividerShown.ShouldBeTrue();
        legend.DesiredSize.Height.ShouldBe(17);
        legend.Children[4].Bounds.X.ShouldBe(288 + 16);
        legend.Children[5].Bounds.X.ShouldBe(288 + 33);
        legend.Measure(new Size(420, double.PositiveInfinity));
        legend.Arrange(new Rect(new Size(420, legend.DesiredSize.Height)));
        legend.IsDividerShown.ShouldBeFalse();
        legend.DesiredSize.Height.ShouldBe(17 + 6 + 17);
        legend.Children[5].Bounds.Position.ShouldBe(new Point(0, 23));
    }

    [AvaloniaFact]
    public void Off_And_Reduced_Motion_Draw_Still_Frames_And_Regroup_At_Once()
    {
        var (galaxy, window) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            var stage = Stage(galaxy);
            var angle = stage.Field.Angle;
            for (var i = 0; i < 30; i++)
            {
                stage.Step(1 / 60.0).ShouldBeFalse("no rotation, no drift at reduced motion");
            }
            stage.Field.Angle.ShouldBe(angle);
            galaxy.SelectedGroupingKey = "plan";
            Settle();
            stage.Field.IsMorphing.ShouldBeFalse();
            stage.View.IsAtFit.ShouldBeTrue();
            galaxy.ClusterLabels.ShouldAllBe(l => l.EnterProgress == 1);
            galaxy.FocusedGroupKey = "team";
            stage.View.IsMoving.ShouldBeFalse("flights land at once");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Turns_The_Galaxy_Until_The_Pointer_Rests_On_It()
    {
        var (galaxy, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var stage = Stage(galaxy);
            stage.IsLoopRunning.ShouldBeTrue();
            var angle = stage.Field.Angle;
            for (var i = 0; i < 10; i++)
            {
                stage.Step(0.1).ShouldBeTrue();
            }
            stage.Field.Angle.ShouldBe(angle + GalaxyLayout.RotationSpeed, 2e-4);
            window.MouseMove(OnWindow(window, stage, new Point(20, 20)));
            Settle();
            stage.IsPointerOver.ShouldBeTrue();
            var moving = true;
            for (var i = 0; i < 400 && moving; i++)
            {
                moving = stage.Step(1 / 60.0);
            }
            moving.ShouldBeFalse("idle with the pointer on the stage: no frame is requested");
            galaxy.IsAutoRotate = false;
            window.MouseMove(new Point(window.Width - 2, window.Height - 2));
            Settle();
            stage.Step(1 / 60.0).ShouldBeFalse("auto-rotation off");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Colors_Come_From_The_Theme_Tokens_And_Follow_It()
    {
        var (galaxy, window) = Show();
        try
        {
            var success = galaxy.HealthStates.Single(s => s.Tone == GalaxyTone.Success);
            var light = galaxy.ResolveColor(success);
            light.ShouldBe(ArcTokens.GetColor(galaxy, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Success));
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            Settle();
            var dark = galaxy.ResolveColor(success);
            dark.ShouldNotBe(light);
            var legend = TestHelpers.Descendant<GalaxyLegendPanel>(galaxy).Children.OfType<GalaxyLegendItem>().First();
            TestHelpers.ColorOf(legend.DotBrush).ShouldBe(dark);
            // A fixed color does not follow the theme.
            success.Color = Avalonia.Media.Colors.Teal;
            galaxy.ResolveColor(success).ShouldBe(Avalonia.Media.Colors.Teal);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Colors_Are_Read_Again_When_The_Block_Returns_After_A_Theme_Switch()
    {
        // The theme changed while the block was out of the window: no resource change reaches it on the way back, and a
        // refresh that ran while it was away read transparent colors.
        var (galaxy, window) = Show();
        try
        {
            var layers = (Avalonia.Controls.Primitives.VisualLayerManager)window.Content!;
            layers.Child = null;
            Settle();
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            layers.Child = galaxy;
            Settle();
            var dark = ArcTokens.GetColor(galaxy, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Success);
            dark.A.ShouldBeGreaterThan((byte)0);
            var legend = TestHelpers.Descendant<GalaxyLegendPanel>(galaxy).Children.OfType<GalaxyLegendItem>().First();
            TestHelpers.ColorOf(legend.DotBrush).ShouldBe(dark);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void Without_Accounts_The_Stage_Is_Empty_And_The_Numbers_Show_A_Dash()
    {
        var (galaxy, window) = Show(accounts: Array.Empty<GalaxyAccount>());
        try
        {
            galaxy.Classes.ShouldContain(":empty");
            TestHelpers.Descendant<ShowpieceNumber>(galaxy, CustomerGalaxy.MrrPart).DisplayText.ShouldBe("—");
            galaxy.Rows.ShouldBeEmpty();
            Stage(galaxy).Field.Count.ShouldBe(0);
            Stage(galaxy).HitIndex(new Point(100, 100)).ShouldBe(-1);
            galaxy.SelectedGroupingKey = "plan";
            Settle();
            galaxy.ClusterLabels.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Commands_Methods_And_Data_Changes_Keep_Selection_And_Focus()
    {
        var accounts = CustomerGalaxyDemoData.Create().ToList();
        var source = new System.Collections.ObjectModel.ObservableCollection<GalaxyAccount>(accounts);
        var (galaxy, window) = Show(accounts: accounts);
        try
        {
            galaxy.ItemsSource = source;
            Settle();
            galaxy.SelectedGroupingKey = "region";
            Settle();
            galaxy.FlyToGroupCommand.CanExecute("aurora").ShouldBeTrue();
            galaxy.FlyToGroupCommand.Execute("aurora");
            galaxy.FocusedGroupKey.ShouldBe("aurora");
            galaxy.SelectedAccount = source[5];
            source.Add(new GalaxyAccount { Id = "new", Name = "Newcomer", Mrr = 500, Health = "growing", Groups = { ["region"] = "aurora" } });
            Settle();
            galaxy.Summary.AccountCount.ShouldBe(2401);
            galaxy.SelectedAccount.ShouldBe(source[5]);
            galaxy.FocusedGroupKey.ShouldBe("aurora");
            galaxy.ResetViewCommand.Execute(null);
            galaxy.FocusedGroupKey.ShouldBeNull();
            // Selecting an account the galaxy does not hold clears it.
            galaxy.SelectedAccount = new GalaxyAccount { Id = "stranger" };
            galaxy.SelectedAccount.ShouldBeNull();
            galaxy.FocusNextGroup().ShouldBeTrue();
            galaxy.FocusedGroupKey.ShouldBe(galaxy.LayoutSet!.Groups[0].Key);
            galaxy.SelectNextAccount().ShouldBeTrue();
            galaxy.SelectedAccount.ShouldBe(galaxy.Model.Accounts[galaxy.LayoutSet.Members[0][0]]);
            galaxy.FlyToAccount(source[100]);
            galaxy.SelectedAccount.ShouldBe(source[100]);
            Stage(galaxy).View.TargetZoom.ShouldBeGreaterThanOrEqualTo(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Galaxy_Is_Collected_While_Its_Accounts_Live_On()
    {
        var source = new System.Collections.ObjectModel.ObservableCollection<GalaxyAccount>(TwoAccounts());
        var weak = TestHelpers.ShowAndClose(() => new CustomerGalaxy { ItemsSource = source });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        source.Add(new GalaxyAccount { Id = "later", Name = "Later", Mrr = 10, Health = "growing" });
    }

    [AvaloniaFact]
    public void A_Galaxy_Put_Back_Shows_Accounts_Added_While_It_Was_Away_And_Otherwise_Keeps_Its_View()
    {
        var source = new System.Collections.ObjectModel.ObservableCollection<GalaxyAccount>(CustomerGalaxyDemoData.Create());
        var galaxy = new CustomerGalaxy { ItemsSource = source };
        var host = new Border { Child = galaxy };
        var window = TestHelpers.Show(host);
        window.Width = 664;
        window.Height = 1200;
        Settle();
        try
        {
            galaxy.Summary.AccountCount.ShouldBe(2400);
            galaxy.FlyToAccount(source[100]);
            Settle();
            Stage(galaxy).View.TargetZoom.ShouldBeGreaterThanOrEqualTo(3);

            // Nothing changed while it was away: the view stays where it was.
            host.Child = null;
            Settle();
            host.Child = galaxy;
            Settle();
            galaxy.SelectedAccount.ShouldBe(source[100]);
            Stage(galaxy).View.TargetZoom.ShouldBeGreaterThanOrEqualTo(3);

            host.Child = null;
            Settle();
            source.Add(new GalaxyAccount { Id = "away", Name = "Arrived away", Mrr = 500, Health = "growing" });
            host.Child = galaxy;
            Settle();
            galaxy.Summary.AccountCount.ShouldBe(2401);
            galaxy.SelectedAccount.ShouldBe(source[100]);

            // Back in the window, it listens again.
            source.Add(new GalaxyAccount { Id = "back", Name = "Arrived back", Mrr = 500, Health = "steady" });
            Settle();
            galaxy.Summary.AccountCount.ShouldBe(2402);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Summary_Keyboard_Help_And_A_Table()
    {
        var (galaxy, window) = Show();
        try
        {
            var stage = Stage(galaxy);
            AutomationProperties.GetName(stage).ShouldNotBeNull().ShouldStartWith("Customer galaxy of 2,400 accounts and ");
            AutomationProperties.GetName(stage).ShouldNotBeNull().ShouldContain("in one spiral");
            AutomationProperties.GetHelpText(stage).ShouldBe(galaxy.KeyboardHelpText);
            galaxy.Table.ShouldNotBeNull().Rows.Count.ShouldBe(4);
            galaxy.SelectedGroupingKey = "industry";
            Settle();
            AutomationProperties.GetName(stage).ShouldNotBeNull().ShouldContain("grouped by industry");
            galaxy.Table!.Rows.Count.ShouldBe(6);
            galaxy.Table.Rows[0].ShouldContain("accounts");
            var row = galaxy.Rows[0];
            AutomationProperties.GetName(row).ShouldNotBeNull().ShouldContain("MRR");
            AutomationProperties.GetHelpText(row).ShouldBe("Fly to this cluster");
            AutomationProperties.GetName(TestHelpers.Descendant<GalaxyTabStrip>(galaxy)).ShouldBe("Group accounts by");
        }
        finally
        {
            window.Close();
        }
    }
}
