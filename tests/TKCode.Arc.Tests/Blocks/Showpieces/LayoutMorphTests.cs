using System.Collections.ObjectModel;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="LayoutMorph"/>: properties, views, keyboard model, opening, carousel and helix input, the list scroll,
/// the panel and the tag, size classes, motion levels and the idle loop, and the Gallery demos in light and dark.
/// </summary>
public class LayoutMorphTests
{
    private static (Window Window, LayoutMorph Morph) Show(LayoutMorph? morph = null, double width = 1200, double height = 1100,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        morph ??= Create();
        var window = TestHelpers.Show(morph, level);
        window.Width = width;
        window.Height = height;
        Pump();
        return (window, morph);
    }

    private static LayoutMorph Create()
    {
        var morph = new LayoutMorph { Title = "Studio", DefaultPanelTitle = "Shelf" };
        var items = LayoutMorphDemoData.CreateItems();
        morph.Items.AddRange(items);
        morph.DefaultStats.AddRange(LayoutMorphDemoData.CreateTotals(items));
        return morph;
    }

    private static void Pump(int n = 4)
    {
        for (var i = 0; i < n; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static Point WindowPoint(LayoutMorph morph, Point stagePoint) =>
        morph.Stage.TranslatePoint(stagePoint, TopLevel.GetTopLevel(morph)!)!.Value;

    private static Point CardCenter(LayoutMorph morph, int index) => WindowPoint(morph, morph.Stage.CardAt(index).ScreenCenter);

    // Defaults and structure -------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Defaults_match_the_arc_preview()
    {
        var morph = new LayoutMorph();
        morph.Layout.ShouldBe(LayoutMorphMode.Grid);
        morph.IsFramed.ShouldBeTrue();
        morph.IsPanelVisible.ShouldBeTrue();
        morph.IsHintVisible.ShouldBeTrue();
        morph.StaggerDuration.ShouldBe(TimeSpan.FromMilliseconds(330));
        double.IsNaN(morph.StageHeight).ShouldBeTrue();
        morph.ViewLabel.ShouldBe("Gallery view");
        morph.HelixLabel.ShouldBe("Helix");
        morph.DefaultPanelSubtitle.ShouldBe("Point at a card for details");
        new LayoutMorphItem().ActionText.ShouldBe("Save");
        new LayoutMorphItem().AspectRatio.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Builds_one_card_per_item_with_one_tab_stop_and_a_600_stage()
    {
        var (window, morph) = Show();
        try
        {
            morph.Stage.Count.ShouldBe(24);
            morph.Stage.Bounds.Height.ShouldBe(600);
            morph.Stage.Bounds.Width.ShouldBe(1200 - 2, 1);
            var stops = Enumerable.Range(0, 24).Where(i => KeyboardNavigation.GetIsTabStop(morph.Stage.CardAt(i))).ToList();
            stops.ShouldBe([2]);
            AutomationProperties.GetName(morph.Stage.CardAt(0)).ShouldBe("Slate & leather desk pad, By Gabriel Costa, $64");
            AutomationProperties.GetName(morph.Stage).ShouldStartWith("Studio in Grid view: 24 cards.");
            morph.Classes.ShouldContain(":framed");
            morph.Classes.ShouldNotContain(":narrow");
            var bar = TestHelpers.Descendant<LayoutMorphViewBar>(morph);
            bar.Options.Select(o => o.Label).ShouldBe(["Grid", "Masonry", "List", "Stack", "Carousel", "Helix"]);
            AutomationProperties.GetName(bar).ShouldBe("Gallery view");
            morph.HintSwap!.Content.ShouldBe("Select a card to open it");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Grid_cards_sit_where_the_engine_puts_them()
    {
        var (window, morph) = Show();
        try
        {
            var stage = morph.Stage;
            var w = stage.Bounds.Width;
            var shape = LayoutMorphEngine.GridShape(new LayoutMorphMetrics(w, 600), 24);
            shape.Columns.ShouldBe(8);
            var card = stage.CardAt(0);
            card.DrawWidth.ShouldBe(shape.CardWidth);
            card.ScreenMatrix.M13.ShouldBe(0, 1e-12);
            stage.IsMoving.ShouldBeFalse();
            stage.IsLoopRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // Views ----------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Changing_the_layout_moves_the_cards_announces_and_raises_LayoutChanged()
    {
        var (window, morph) = Show();
        try
        {
            LayoutMorphLayoutChangedEventArgs? args = null;
            morph.LayoutChanged += (_, e) => args = e;
            morph.Layout = LayoutMorphMode.List;
            Pump();
            args.ShouldNotBeNull();
            args.OldLayout.ShouldBe(LayoutMorphMode.Grid);
            args.NewLayout.ShouldBe(LayoutMorphMode.List);
            morph.LiveRegion!.LastAnnouncement.ShouldBe("List view");
            morph.HintSwap!.Content.ShouldBe("Maker and price on every row");
            TestHelpers.Descendant<LayoutMorphViewBar>(morph).SelectedMode.ShouldBe(LayoutMorphMode.List);
            morph.Stage.CardAt(0).DrawHeight.ShouldBe(56);
            morph.Stage.CardAt(0).Captions[(int)LayoutMorphCaption.Row].ShouldBe(1);
            morph.Stage.CardAt(0).Captions[(int)LayoutMorphCaption.Compact].ShouldBe(0);
            (AutomationProperties.GetName(morph.Stage) ?? string.Empty).ShouldContain("List view");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layouts_limits_and_orders_the_view_switch()
    {
        var morph = Create();
        morph.Layouts = [LayoutMorphMode.Carousel, LayoutMorphMode.Grid];
        var (window, _) = Show(morph);
        try
        {
            TestHelpers.Descendant<LayoutMorphViewBar>(morph).Options.Select(o => o.Mode)
                       .ShouldBe([LayoutMorphMode.Carousel, LayoutMorphMode.Grid]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_view_switch_wraps_with_the_arrows_and_jumps_with_Home_and_End()
    {
        var (window, morph) = Show();
        try
        {
            var bar = TestHelpers.Descendant<LayoutMorphViewBar>(morph);
            bar.Options[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Pump();
            morph.Layout.ShouldBe(LayoutMorphMode.Helix);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            morph.Layout.ShouldBe(LayoutMorphMode.Grid);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            morph.Layout.ShouldBe(LayoutMorphMode.Helix);
            bar.Options[5].IsFocused.ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(bar.Options[5]).ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(bar.Options[0]).ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            morph.Layout.ShouldBe(LayoutMorphMode.Grid);
        }
        finally
        {
            window.Close();
        }
    }

    // Card keyboard ----------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Cards_move_in_data_order_and_the_side_arrows_change_the_view_keeping_focus()
    {
        var (window, morph) = Show();
        try
        {
            var stage = morph.Stage;
            stage.CardAt(2).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            stage.CardAt(3).IsFocused.ShouldBeTrue();
            stage.TabStopIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            stage.CardAt(23).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            stage.CardAt(0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Pump();
            stage.CardAt(0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            morph.Layout.ShouldBe(LayoutMorphMode.Masonry);
            stage.CardAt(0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Pump();
            morph.Layout.ShouldBe(LayoutMorphMode.Helix);
            // Arrived on the helix from a card: that card is turned to the front.
            morph.FrontItem.ShouldBe(morph.EffectiveItems[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_opens_a_trapped_dialog_and_Escape_returns_focus_to_the_card()
    {
        var (window, morph) = Show();
        try
        {
            LayoutMorphItem? opened = null;
            morph.ItemOpened += (_, e) => opened = e.Item;
            var card = morph.Stage.CardAt(4);
            card.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            var item = morph.EffectiveItems[4];
            morph.OpenedItem.ShouldBe(item);
            opened.ShouldBe(item);
            morph.Classes.ShouldContain(":open");
            morph.LiveRegion!.LastAnnouncement.ShouldBe("Cork monitor riser opened");
            var dialog = morph.Dialog!;
            dialog.IsVisible.ShouldBeTrue();
            KeyboardNavigation.GetTabNavigation(dialog).ShouldBe(KeyboardNavigationMode.Cycle);
            AutomationProperties.GetName(dialog).ShouldBe("Cork monitor riser");
            var close = TestHelpers.Descendant<Avalonia.Controls.Button>(morph, "PART_Close");
            close.IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(close).ShouldBe("Close Cork monitor riser");
            // The other cards dim and leave the tab order; the open card is centered at its detail size.
            morph.Stage.CardAt(0).Opacity.ShouldBe(0.12, 1e-9);
            morph.Stage.CardAt(4).DrawWidth.ShouldBe(640);
            Enumerable.Range(0, 24).ShouldAllBe(i => !morph.Stage.CardAt(i).Focusable);
            // Tab stays inside: close → action → close.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump();
            TestHelpers.Descendant<ToggleButton>(morph, "PART_Action").IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump();
            close.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            morph.OpenedItem.ShouldBeNull();
            morph.Classes.ShouldNotContain(":open");
            card.IsFocused.ShouldBeTrue();
            morph.Stage.CardAt(0).Opacity.ShouldBe(1, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_detail_button_flips_IsActive_and_raises_ItemActionInvoked()
    {
        var (window, morph) = Show();
        try
        {
            var item = morph.EffectiveItems[1];
            LayoutMorphItem? acted = null;
            morph.ItemActionInvoked += (_, e) => acted = e.Item;
            morph.Open(item);
            Pump();
            var action = TestHelpers.Descendant<ToggleButton>(morph, "PART_Action");
            AutomationProperties.GetName(action).ShouldBe("Follow");
            action.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            item.IsActive.ShouldBeTrue();
            acted.ShouldBe(item);
            action.IsChecked.ShouldBe(true);
            AutomationProperties.GetName(action).ShouldBe("Following");
            morph.LiveRegion!.LastAnnouncement.ShouldBe("Priya Anand: Following");
            // The state survives closing and opening again.
            morph.Close();
            Pump();
            morph.Open(item);
            Pump();
            action.IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_handled_ItemInvoked_keeps_the_card_closed()
    {
        var (window, morph) = Show();
        try
        {
            morph.ItemInvoked += (_, e) => e.Handled = true;
            window.MouseDown(CardCenter(morph, 3), MouseButton.Left);
            window.MouseUp(CardCenter(morph, 3), MouseButton.Left);
            Pump();
            morph.OpenedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_opens_and_a_click_on_the_stage_outside_closes()
    {
        var (window, morph) = Show();
        try
        {
            window.MouseDown(CardCenter(morph, 3), MouseButton.Left);
            window.MouseUp(CardCenter(morph, 3), MouseButton.Left);
            Pump();
            morph.OpenedItem.ShouldBe(morph.EffectiveItems[3]);
            // The close button is not focus-visible after a pointer open.
            TestHelpers.Descendant<Avalonia.Controls.Button>(morph, "PART_Close").Classes.ShouldNotContain(":focus-visible");
            var corner = WindowPoint(morph, new Point(20, 20));
            window.MouseDown(corner, MouseButton.Left);
            window.MouseUp(corner, MouseButton.Left);
            Pump();
            morph.OpenedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    // Carousel and helix ----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Carousel_front_is_the_tab_stop_and_focusing_a_card_turns_it_to_the_front()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph);
        try
        {
            morph.FrontItem.ShouldBe(morph.EffectiveItems[2]);
            morph.Stage.TabStopIndex.ShouldBe(2);
            morph.PanelSubject.ShouldBe(morph.EffectiveItems[2]);
            morph.Stage.CardAt(2).ScreenCenter.X.ShouldBe(morph.Stage.Bounds.Width / 2, 0.5);
            // Behind the ring: invisible and not hit-testable.
            morph.Stage.CardAt(12).Opacity.ShouldBe(0);
            morph.Stage.CardAt(12).IsHitTestVisible.ShouldBeFalse();
            morph.FocusItem(morph.EffectiveItems[9]);
            Pump();
            morph.FrontItem.ShouldBe(morph.EffectiveItems[9]);
            morph.Stage.TabStopIndex.ShouldBe(9);
            morph.Stage.CardAt(9).IsFocused.ShouldBeTrue();
            morph.Stage.CardAtPoint(new Point(morph.Stage.Bounds.Width / 2, 300)).ShouldBe(morph.Stage.CardAt(9));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_the_carousel_by_one_card_lands_on_the_next()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph);
        try
        {
            var arc = LayoutMorphEngine.ArcPerCard(LayoutMorphMode.Carousel, morph.Stage.Metrics);
            var start = WindowPoint(morph, new Point(morph.Stage.Bounds.Width / 2, 300));
            window.MouseDown(start, MouseButton.Left);
            for (var i = 1; i <= 10; i++)
            {
                window.MouseMove(start - new Vector(arc * 0.9 * i / 10, 0), RawInputModifiers.LeftMouseButton);
                Pump(1);
            }
            morph.Classes.ShouldContain(":dragging");
            window.MouseUp(start - new Vector(arc * 0.9, 0), MouseButton.Left);
            Pump();
            morph.Classes.ShouldNotContain(":dragging");
            morph.Stage.Front.ShouldBe(Math.Round(morph.Stage.Front), 1e-9);
            morph.Stage.Front.ShouldBeGreaterThanOrEqualTo(3);
            morph.FrontItem.ShouldBe(morph.EffectiveItems[(int)morph.Stage.Front % 24]);
            morph.OpenedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_carousel_ends_at_its_first_and_last_cards()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph);
        try
        {
            var stage = morph.Stage;
            var arc = LayoutMorphEngine.ArcPerCard(LayoutMorphMode.Carousel, stage.Metrics);
            morph.FocusItem(morph.EffectiveItems[0]);
            Pump();
            morph.FrontItem.ShouldBe(morph.EffectiveItems[0]);
            stage.Front.ShouldBe(0, 1e-9);
            // Nothing comes round before the first card (Arc: empty space left of it).
            stage.CardAt(23).Opacity.ShouldBe(0);
            stage.CardAt(22).Opacity.ShouldBe(0);
            stage.CardAtPoint(new Point(stage.Bounds.Width / 2 - arc, 300)).ShouldBeNull();
            stage.CardAtPoint(new Point(stage.Bounds.Width / 2 + arc * 0.8, 300)).ShouldBe(stage.CardAt(1));
            // End turns the strip the whole way to the last card, with nothing after it.
            morph.FocusItem(morph.EffectiveItems[23]);
            Pump();
            stage.Front.ShouldBe(23, 1e-9);
            stage.CardAt(0).Opacity.ShouldBe(0);
            stage.CardAt(1).Opacity.ShouldBe(0);
            stage.CardAt(22).Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_beside_the_carousel_front_turns_that_card_to_the_front_and_the_front_card_opens()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph);
        try
        {
            var stage = morph.Stage;
            morph.FrontItem.ShouldBe(morph.EffectiveItems[2]);
            window.MouseDown(CardCenter(morph, 3), MouseButton.Left);
            window.MouseUp(CardCenter(morph, 3), MouseButton.Left);
            Pump();
            morph.OpenedItem.ShouldBeNull();
            morph.FrontItem.ShouldBe(morph.EffectiveItems[3]);
            stage.TabStopIndex.ShouldBe(3);
            window.MouseDown(CardCenter(morph, 3), MouseButton.Left);
            window.MouseUp(CardCenter(morph, 3), MouseButton.Left);
            Pump();
            morph.OpenedItem.ShouldBe(morph.EffectiveItems[3]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_on_a_helix_side_card_opens_it()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Helix;
        var (window, _) = Show(morph);
        try
        {
            morph.FrontItem.ShouldBe(morph.EffectiveItems[11]);
            window.MouseDown(CardCenter(morph, 12), MouseButton.Left);
            window.MouseUp(CardCenter(morph, 12), MouseButton.Left);
            Pump();
            morph.OpenedItem.ShouldBe(morph.EffectiveItems[12]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(LayoutMorphMode.Carousel)]
    [InlineData(LayoutMorphMode.Helix)]
    public void Dragging_past_the_first_card_stretches_and_springs_back(LayoutMorphMode mode)
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = mode;
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            morph.FocusItem(morph.EffectiveItems[0], animate: false);
            clock.Advance(TimeSpan.FromSeconds(1));
            Pump();
            stage.Front.ShouldBe(0, 1e-9);
            var start = WindowPoint(morph, new Point(stage.Bounds.Width * 0.25, 300));
            window.MouseDown(start, MouseButton.Left);
            for (var i = 1; i <= 20; i++)
            {
                window.MouseMove(start + new Vector(30 * i, 0), RawInputModifiers.LeftMouseButton);
                clock.Advance(TimeSpan.FromMilliseconds(16));
                Pump(1);
            }
            // 600px past the end shows about 200px of travel (not 600, and not a hard stop).
            var arc = LayoutMorphEngine.ArcPerCard(mode, stage.Metrics);
            (-stage.Front * arc).ShouldBe(206.5, 25);
            window.MouseUp(start + new Vector(600, 0), MouseButton.Left);
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            stage.Front.ShouldBe(0, 1e-6);
            morph.FrontItem.ShouldBe(morph.EffectiveItems[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_leaving_the_back_of_the_carousel_stay_dim_until_they_turn_round()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph, width: 1082, level: ArcMotionLevel.Full);
        try
        {
            var stage = morph.Stage;
            stage.Bounds.Width.ShouldBe(1080, 0.5);
            clock.Advance(TimeSpan.FromSeconds(1));
            stage.CardAt(12).Opacity.ShouldBe(0);
            morph.Layout = LayoutMorphMode.Grid;
            clock.Advance(TimeSpan.FromMilliseconds(17));
            // Arc: the hidden back cards show at 0.72 as bare bases from the first frame; front-facing ones at 1.
            stage.CardAt(12).Opacity.ShouldBe(LayoutMorphEngine.HelixBackOpacity, 1e-6);
            stage.CardAt(12).HidesContent.ShouldBeTrue();
            // Four places on (1.15 rad) the face already points away from the eye in perspective (Arc: 0.72 too).
            stage.CardAt(6).Opacity.ShouldBe(LayoutMorphEngine.HelixBackOpacity, 1e-6);
            stage.CardAt(2).Opacity.ShouldBe(1, 1e-6);
            stage.CardAt(23).Opacity.ShouldBe(1, 1e-6);
            clock.Advance(TimeSpan.FromSeconds(2.5));
            Enumerable.Range(0, 24).All(i => Math.Abs(stage.CardAt(i).Opacity - 1) < 1e-6 && !stage.CardAt(i).HidesContent).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_small_horizontal_wheel_nudge_settles_back_on_the_same_card()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Carousel;
        var (window, _) = Show(morph);
        try
        {
            window.MouseWheel(WindowPoint(morph, new Point(500, 300)), new Vector(-0.6, 0));
            Pump();
            morph.FrontItem.ShouldBe(morph.EffectiveItems[2]);
            morph.Stage.Front.ShouldBe(2, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Helix_starts_on_the_middle_card_with_back_faces_dimmed()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Helix;
        var (window, _) = Show(morph);
        try
        {
            morph.FrontItem.ShouldBe(morph.EffectiveItems[11]);
            var center = morph.Stage.CardAt(11).ScreenCenter;
            center.X.ShouldBe(morph.Stage.Bounds.Width / 2, 1);
            center.Y.ShouldBe(300, 1);
            morph.Stage.CardAt(14).Opacity.ShouldBe(LayoutMorphEngine.HelixBackOpacity, 1e-9);
            morph.Stage.CardAt(14).HidesContent.ShouldBeTrue();
            morph.Stage.CardAt(21).Opacity.ShouldBe(1, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    // List scroll ---------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_two_column_list_scrolls_with_the_wheel()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.List;
        var (window, _) = Show(morph, width: 900);
        try
        {
            morph.Stage.IsScrollable.ShouldBeTrue();
            morph.Classes.ShouldContain(":scroll");
            morph.Stage.ScrollRange.ShouldBe(760 - 552, 1);
            window.MouseWheel(WindowPoint(morph, new Point(300, 300)), new Vector(0, -1));
            Pump();
            morph.Stage.ScrollOffset.ShouldBe(-100, 0.5);
            for (var i = 0; i < 5; i++)
            {
                window.MouseWheel(WindowPoint(morph, new Point(300, 300)), new Vector(0, -1));
            }
            Pump();
            morph.Stage.ScrollOffset.ShouldBe(-morph.Stage.ScrollRange, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    // Pointing, panel and tag ------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Pointing_at_a_card_lifts_it_and_fills_the_panel_and_the_tag()
    {
        var morph = Create();
        morph.Layout = LayoutMorphMode.Masonry;
        var (window, _) = Show(morph);
        try
        {
            morph.PanelSubject.ShouldBeNull();
            window.MouseMove(CardCenter(morph, 5), RawInputModifiers.None);
            Pump();
            var item = morph.EffectiveItems[5];
            morph.Stage.HoverIndex.ShouldBe(5);
            morph.Stage.CardAt(5).Lift.ShouldBe(1);
            morph.PanelSubject.ShouldBe(item);
            morph.TagItem.ShouldBe(item);
            morph.TagCard!.IsOpen.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(morph, "PART_TagTitle").Text.ShouldBe(item.Title);
            // The grid shows captions on the cards, so it has no tag.
            morph.Layout = LayoutMorphMode.Grid;
            Pump();
            morph.TagCard.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panel_numbers_read_prefixes_decimals_and_units()
    {
        LayoutMorph.TryReadNumber(new LayoutMorphFact("Price", "$1,240"), out var value, out var decimals, out var prefix, out var suffix).ShouldBeTrue();
        (value, decimals, prefix, suffix).ShouldBe((1240.0, 0, "$", ""));
        LayoutMorph.TryReadNumber(new LayoutMorphFact("Rating", "4.8"), out value, out decimals, out prefix, out suffix).ShouldBeTrue();
        (value, decimals, prefix, suffix).ShouldBe((4.8, 1, "", ""));
        LayoutMorph.TryReadNumber(new LayoutMorphFact("Ships in", "2 days"), out value, out decimals, out prefix, out suffix).ShouldBeTrue();
        (value, decimals, prefix, suffix).ShouldBe((2.0, 0, "", " days"));
        LayoutMorph.TryReadNumber(new LayoutMorphFact("Since", "—"), out _, out _, out _, out _).ShouldBeFalse();
        LayoutMorph.TryReadNumber(new LayoutMorphFact { Label = "Score", Value = "high", NumericValue = 9.5, Decimals = 1 }, out value, out decimals, out _, out _).ShouldBeTrue();
        (value, decimals).ShouldBe((9.5, 1));
    }

    // Size classes ---------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Narrow_and_dense_containers()
    {
        var (window, morph) = Show(width: 620);
        try
        {
            morph.Classes.ShouldContain(":narrow");
            morph.Classes.ShouldNotContain(":dense");
            TestHelpers.Descendant<LayoutMorphViewBar>(morph).IsCompact.ShouldBeTrue();
            morph.Stage.Bounds.Height.ShouldBe(540);
            window.Width = 470;
            Pump();
            morph.Classes.ShouldContain(":dense");
            morph.Stage.Bounds.Height.ShouldBe(500);
            window.Width = 340;
            Pump();
            morph.Stage.Bounds.Height.ShouldBe(460);
            LayoutMorphEngine.GridShape(morph.Stage.Metrics, 24).Columns.ShouldBe(4);
            morph.StageHeight = 420;
            Pump();
            morph.Stage.Bounds.Height.ShouldBe(420);
        }
        finally
        {
            window.Close();
        }
    }

    // Data ---------------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Rebuilt_collections_keep_cards_by_key()
    {
        var source = new ObservableCollection<LayoutMorphItem>(LayoutMorphDemoData.CreatePieces());
        var morph = new LayoutMorph { ItemsSource = source };
        var (window, _) = Show(morph);
        try
        {
            morph.Stage.Count.ShouldBe(14);
            var card = morph.Stage.CardAt(3);
            var replacement = LayoutMorphDemoData.CreatePieces();
            source.Clear();
            foreach (var item in replacement)
            {
                source.Add(item);
            }
            Pump();
            morph.Stage.Count.ShouldBe(14);
            morph.Stage.CardAt(3).ShouldBeSameAs(card);
            card.Item.ShouldBeSameAs(replacement[3]);
            source.RemoveAt(0);
            Pump();
            morph.Stage.Count.ShouldBe(13);
            morph.Stage.CardAt(2).ShouldBeSameAs(card);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Added_cards_fly_in_from_a_neighbour_and_removed_cards_fade_out_in_place()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var source = new ObservableCollection<LayoutMorphItem>(LayoutMorphDemoData.CreateItems());
        var morph = new LayoutMorph { ItemsSource = source };
        var (window, _) = Show(morph, level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            var stage = morph.Stage;
            var removed = stage.CardAt(5);
            var place = removed.ScreenCenter;
            source.RemoveAt(5);
            morph.FlushItems();
            stage.Count.ShouldBe(23);
            stage.LeavingCards.ShouldBe([removed]);
            removed.IsHitTestVisible.ShouldBeFalse();
            removed.Focusable.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            removed.ScreenCenter.ShouldBe(place);
            removed.Opacity.ShouldBeInRange(0.05, 0.95);
            clock.Advance(TimeSpan.FromSeconds(1));
            stage.LeavingCards.ShouldBeEmpty();
            removed.GetVisualParent().ShouldBeNull();

            var added = new LayoutMorphItem { Key = "new-piece", Title = "Linen desk tidy", Value = "$22" };
            source.Insert(5, added);
            morph.FlushItems();
            var card = stage.CardAt(5);
            card.Item.ShouldBeSameAs(added);
            // Dealt from its neighbour: it starts on card 4, invisible, and flies to its slot while it fades in.
            var neighbour = stage.CardAt(4).ScreenCenter;
            new Vector(card.ScreenCenter.X - neighbour.X, card.ScreenCenter.Y - neighbour.Y).Length.ShouldBeLessThan(0.5);
            card.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromSeconds(2.5));
            stage.IsMoving.ShouldBeFalse();
            card.Opacity.ShouldBe(1, 0.01);
            new Vector(card.ScreenCenter.X - place.X, card.ScreenCenter.Y - place.Y).Length.ShouldBeLessThan(0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_the_open_item_closes_it_and_brings_the_world_back()
    {
        var source = new ObservableCollection<LayoutMorphItem>(LayoutMorphDemoData.CreateItems());
        var morph = new LayoutMorph { ItemsSource = source };
        var (window, _) = Show(morph);
        try
        {
            var item = source[3];
            morph.Open(item);
            Pump();
            morph.Stage.Recede.ShouldBe(LayoutMorphEngine.OpenRecede);
            morph.Stage.CardAt(0).Opacity.ShouldBe(LayoutMorphEngine.OpenDimOpacity, 0.01);
            var closed = 0;
            morph.ItemClosed += (_, _) => closed++;
            source.Remove(item);
            Pump();
            morph.OpenedItem.ShouldBeNull();
            closed.ShouldBe(1);
            morph.Classes.ShouldNotContain(":open");
            morph.Stage.OpenIndex.ShouldBe(-1);
            morph.Stage.Recede.ShouldBe(0);
            Enumerable.Range(0, morph.Stage.Count).All(i => Math.Abs(morph.Stage.CardAt(i).Opacity - 1) < 0.01).ShouldBeTrue();
            morph.Dialog!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_removed_focused_card_passes_the_focus_to_the_card_in_its_place()
    {
        var source = new ObservableCollection<LayoutMorphItem>(LayoutMorphDemoData.CreateItems());
        var morph = new LayoutMorph { ItemsSource = source };
        var (window, _) = Show(morph);
        try
        {
            morph.FocusItem(source[7]);
            Pump();
            morph.Stage.CardAt(7).IsFocused.ShouldBeTrue();
            var next = source[8];
            source.RemoveAt(7);
            Pump();
            var focused = morph.Stage.CardAt(7);
            focused.Item.ShouldBeSameAs(next);
            focused.IsFocused.ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(focused).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_stacked_detail_spans_its_button_and_follows_a_resize()
    {
        var (window, morph) = Show(width: 400, height: 1100);
        try
        {
            morph.Open(morph.EffectiveItems[1]);
            Pump();
            var dialog = morph.Dialog!;
            dialog.IsStacked.ShouldBeTrue();
            dialog.Classes.ShouldContain(":stacked");
            var action = dialog.Children.OfType<ToggleButton>().Single();
            // Full width, 12px in from both sides and the bottom (Arc 8.3).
            action.Bounds.Width.ShouldBe(dialog.Bounds.Width - 24, 0.5);
            action.Bounds.Bottom.ShouldBe(dialog.Bounds.Height - 12, 0.5);
            var stage = morph.Stage;
            dialog.Bounds.Width.ShouldBe(stage.Bounds.Width - 24, 0.5);

            window.Width = 1200;
            Pump();
            dialog.IsStacked.ShouldBeFalse();
            // Profile detail: 640 × 392, the button 160 wide, 20px from the bottom right.
            dialog.Bounds.Width.ShouldBe(640, 0.5);
            dialog.Bounds.Height.ShouldBe(392, 0.5);
            action.Bounds.Width.ShouldBe(160, 0.5);
            action.Bounds.Right.ShouldBe(640 - 20, 0.5);
            stage.CardAt(1).IsSheetNarrow.ShouldBeFalse();
            stage.CardAt(1).DrawWidth.ShouldBe(640, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    // Motion ----------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Full_motion_flies_on_springs_then_requests_no_frames()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, morph) = Show(level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            var stage = morph.Stage;
            var before = stage.CardAt(23).ScreenCenter;
            morph.Layout = LayoutMorphMode.List;
            clock.Advance(TimeSpan.FromMilliseconds(100));
            stage.IsMoving.ShouldBeTrue();
            // The old captions are already fading; the new layer waits until the card nearly arrived.
            stage.CardAt(0).Captions[(int)LayoutMorphCaption.Compact].ShouldBeLessThan(0.5);
            stage.CardAt(0).Captions[(int)LayoutMorphCaption.Row].ShouldBe(0);
            clock.Advance(TimeSpan.FromSeconds(2.5));
            stage.IsMoving.ShouldBeFalse();
            stage.CardAt(0).Captions[(int)LayoutMorphCaption.Row].ShouldBe(1);
            stage.CardAt(23).ScreenCenter.ShouldNotBe(before);
            Pump();
            var requests = clock.RequestCount;
            clock.Advance(TimeSpan.FromSeconds(1));
            clock.RequestCount.ShouldBe(requests);
            stage.IsLoopRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_farthest_card_starts_last()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, morph) = Show(level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            var stage = morph.Stage;
            var starts = Enumerable.Range(0, 24).Select(i => stage.CardAt(i).ScreenCenter).ToArray();
            // The masonry option sits top left: the bottom right card is the farthest.
            morph.Layout = LayoutMorphMode.Masonry;
            clock.Advance(TimeSpan.FromMilliseconds(120));
            stage.CardAt(23).ScreenCenter.ShouldBe(starts[23]);
            (stage.CardAt(0).ScreenCenter - starts[0]).X.ShouldNotBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Theme_changes_recolor_the_cards()
    {
        var (window, morph) = Show();
        try
        {
            var light = morph.Stage.Palette.CardFill;
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            Pump();
            morph.Stage.Palette.CardFill.ShouldNotBe(light);
            morph.Stage.Palette.IsDark.ShouldBeTrue();
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
            window.Close();
        }
    }

    // Theme and attachment ---------------------------------------------------------------------------------------------------
    // The stage resolves its card palette (colors and the body / display fonts) from the ArcDesign tokens. Font tokens are
    // read through the theme context, which only exists while the control is in a themed tree: a palette refresh posted by a
    // theme change (or by the resource change a detach raises) must not run on a detached stage, and a stage that missed a
    // theme change while detached must pick the new palette up when it comes back.

    private static Color ThemeColor(Control owner, TKCode.Arc.DesignTokens.ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    [AvaloniaFact]
    public void A_theme_change_then_a_detach_before_the_repaint_runs_does_not_throw()
    {
        var (window, morph) = Show();
        var layers = (Avalonia.Controls.Primitives.VisualLayerManager)window.Content!;
        try
        {
            // The theme variant changes (the stage posts its palette refresh), then the block leaves the window before
            // the posted job runs.
            window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            layers.Child = null;
            Should.NotThrow(() => Pump());
            TopLevel.GetTopLevel(morph.Stage).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detaching_and_changing_items_while_detached_does_not_throw()
    {
        var (window, morph) = Show();
        var layers = (Avalonia.Controls.Primitives.VisualLayerManager)window.Content!;
        try
        {
            // A detach raises a resource change (the stage marks its palette dirty); the posted refresh, a collection
            // rebuild and a size pass all run while the block is out of the tree.
            layers.Child = null;
            morph.Items.RemoveAt(0);
            morph.Items.Add(new LayoutMorphItem { Title = "Late arrival", Value = "$12" });
            Should.NotThrow(() => Pump());
            Should.NotThrow(() =>
            {
                morph.Measure(new Size(900, 1000));
                morph.Arrange(new Rect(0, 0, 900, 1000));
                using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(900, 1000));
                bitmap.Render(morph);
            });
            morph.Stage.Count.ShouldBe(24);

            // Back in the window: the cards paint with the window's palette again.
            layers.Child = morph;
            Pump();
            morph.Stage.Palette.CardFill.ShouldBe(ThemeColor(morph.Stage, TKCode.Arc.DesignTokens.ArcDesignTokenKind.SurfaceRaised));
            morph.Stage.Palette.CardFillBrush.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    private static List<LayoutMorphItem> PlainItems(int count, string prefix = "Item") =>
        Enumerable.Range(0, count).Select(i => new LayoutMorphItem { Title = $"{prefix} {i}", Value = $"${i}" }).ToList();

    [AvaloniaFact]
    public void A_morph_is_collected_while_its_items_live_on()
    {
        var source = new ObservableCollection<LayoutMorphItem>(PlainItems(6));
        var shared = PlainItems(5);
        var list = new Avalonia.Collections.AvaloniaList<LayoutMorphItem>(PlainItems(4));
        var withSource = TestHelpers.ShowAndClose(() => new LayoutMorph { ItemsSource = source });
        // The host's items in the morph's own list, and a list the host hands in.
        var withItems = TestHelpers.ShowAndClose(() =>
        {
            var morph = new LayoutMorph();
            morph.Items.AddRange(shared);
            return morph;
        });
        var withList = TestHelpers.ShowAndClose(() => new LayoutMorph { Items = list });
        TestHelpers.Collect(withSource, withItems, withList);
        withSource.IsAlive.ShouldBeFalse();
        withItems.IsAlive.ShouldBeFalse();
        withList.IsAlive.ShouldBeFalse();
        source.Add(new LayoutMorphItem { Title = "Later" });
        source[0].Title = "Renamed";
        shared[0].Title = "Renamed";
        list.Add(new LayoutMorphItem { Title = "Later" });
    }

    [AvaloniaFact]
    public void A_morph_put_back_shows_what_changed_while_it_was_away()
    {
        var source = new ObservableCollection<LayoutMorphItem>(PlainItems(6));
        var morph = new LayoutMorph { Title = "Studio", ItemsSource = source };
        var host = new Border { Child = morph };
        var window = TestHelpers.Show(host);
        window.Width = 1200;
        window.Height = 1100;
        Pump();
        try
        {
            morph.Stage.Count.ShouldBe(6);
            host.Child = null;
            Pump();
            source.Add(new LayoutMorphItem { Title = "Late arrival", Value = "$12" });
            source[0].Title = "Renamed";
            Pump();

            host.Child = morph;
            Pump();
            morph.Stage.Count.ShouldBe(7);
            AutomationProperties.GetName(morph.Stage.CardAt(0)).ShouldStartWith("Renamed");
            AutomationProperties.GetName(morph.Stage.CardAt(6)).ShouldStartWith("Late arrival");

            // Back in the window, it listens again.
            source[1].Title = "Retitled";
            source.RemoveAt(6);
            Pump();
            morph.Stage.Count.ShouldBe(6);
            AutomationProperties.GetName(morph.Stage.CardAt(1)).ShouldStartWith("Retitled");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_theme_switched_while_detached_is_picked_up_on_attach()
    {
        var (window, morph) = Show();
        var layers = (Avalonia.Controls.Primitives.VisualLayerManager)window.Content!;
        try
        {
            var light = morph.Stage.Palette.CardFill;
            layers.Child = null;
            Pump();
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            Pump();
            layers.Child = morph;
            Pump();
            var dark = ThemeColor(morph.Stage, TKCode.Arc.DesignTokens.ArcDesignTokenKind.SurfaceRaised);
            dark.ShouldNotBe(light);
            morph.Stage.Palette.CardFill.ShouldBe(dark);
            morph.Stage.Palette.IsDark.ShouldBeTrue();
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_block_created_before_the_theme_switch_and_attached_after_paints_with_the_new_theme()
    {
        // The window is up and the block exists (with items, never attached) when the theme switches to dark.
        var layers = new Avalonia.Controls.Primitives.VisualLayerManager();
        var window = new Window { Content = layers, Width = 1200, Height = 1100 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Full);
        window.Show();
        var morph = Create();
        try
        {
            Pump();
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            morph.Layout = LayoutMorphMode.Carousel;
            Should.NotThrow(() => Pump());
            layers.Child = morph;
            Should.NotThrow(() => Pump());
            var dark = ThemeColor(morph.Stage, TKCode.Arc.DesignTokens.ArcDesignTokenKind.SurfaceRaised);
            morph.Stage.Palette.CardFill.ShouldBe(dark);
            morph.Stage.Palette.IsDark.ShouldBeTrue();
            morph.Stage.CardAt(0).DrawWidth.ShouldBeGreaterThan(0);
            // A switch back while attached recolors at once (no detach in between).
            await TestHelpers.ApplyAsync(null);
            Pump();
            morph.Stage.Palette.IsDark.ShouldBeFalse();
            morph.Stage.Palette.CardFill.ShouldBe(ThemeColor(morph.Stage, TKCode.Arc.DesignTokens.ArcDesignTokenKind.SurfaceRaised));
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_the_window_mid_flight_and_pumping_does_not_throw()
    {
        var (window, morph) = Show(level: ArcMotionLevel.Full);
        morph.Layout = LayoutMorphMode.Helix;
        window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        window.Close();
        Should.NotThrow(() => Pump(8));
    }

    // Gallery demos ----------------------------------------------------------------------------------------------------------

    [AvaloniaTheory]
    [InlineData("Preview", 24, false)]
    [InlineData("Flat", 24, false)]
    [InlineData("Narrow", 24, false)]
    [InlineData("Team", 10, false)]
    [InlineData("Catalogue", 14, false)]
    [InlineData("Preview", 24, true)]
    [InlineData("Team", 10, true)]
    [InlineData("Catalogue", 14, true)]
    public async Task Demos_load_in_light_and_dark(string name, int cards, bool dark)
    {
        if (dark)
        {
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
        }
        try
        {
            Control demo = name switch
            {
                "Flat" => new LayoutMorphFlatDemo(),
                "Narrow" => new LayoutMorphNarrowDemo(),
                "Team" => new LayoutMorphTeamDemo(),
                "Catalogue" => new LayoutMorphCatalogueDemo(),
                _ => new LayoutMorphPreviewDemo()
            };
            var window = TestHelpers.Show(demo);
            window.Width = 1200;
            window.Height = 1100;
            Pump();
            var morph = demo.GetVisualDescendants().OfType<LayoutMorph>().Single();
            morph.Stage.Count.ShouldBe(cards);
            morph.Stage.Bounds.Height.ShouldBeGreaterThan(400);
            morph.Stage.CardAt(0).DrawWidth.ShouldBeGreaterThan(0);
            window.Close();
        }
        finally
        {
            if (dark)
            {
                await TestHelpers.ApplyAsync(null);
            }
        }
    }
}
