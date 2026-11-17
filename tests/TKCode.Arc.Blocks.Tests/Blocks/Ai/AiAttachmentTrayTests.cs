using System.Collections.ObjectModel;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.Blocks.Ai.AiAttachmentSamples;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiAttachmentTray"/>: geometry (60px row, chips 8 apart, the viewport 4px outside), following the collection
/// (add, insert, remove, replace, move, reset, a new source, changes while away), texts forwarded to the chips, requests
/// bubbling through, focus after a removal, scrolling without jumping to the newest chip, the empty fold, and the
/// enter / leave / slide motion at full, reduced and no motion.
/// </summary>
public class AiAttachmentTrayTests
{
    private static AvaloniaList<AiAttachment> Attachments(int count)
    {
        var names = new[] { "whiteboard.png", "sprint-15-review.pdf", "usage-oct.csv", "export-worker.ts", "tester-welcome.docx", "headcount-plan.xlsx" };
        var list = new AvaloniaList<AiAttachment>();
        for (var i = 0; i < count; i++)
        {
            list.Add(Sample(names[i % names.Length], 1_000 * (i + 1)));
        }
        return list;
    }

    private static AiAttachmentTray Create(IEnumerable<AiAttachment>? items) => new()
    {
        ItemsSource = items,
        RemoveText = n => $"Take out {n}",
        CancelText = n => $"Stop uploading {n}",
        UploadingText = p => $"{p}% uploaded",
        ErrorText = "Could not upload",
        ImageText = "Picture",
        LinesText = n => $"{n} lines of text"
    };

    private sealed class Rig
    {
        public Rig(IEnumerable<AiAttachment>? items, ArcMotionLevel level = ArcMotionLevel.Off, double width = 360, Control? before = null)
        {
            Tray = Create(items);
            Below = new Border { Height = 40 };
            var stack = new StackPanel { Width = width, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            if (before is not null)
            {
                stack.Children.Add(before);
            }
            stack.Children.Add(Tray);
            stack.Children.Add(Below);
            Window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = stack }, level);
            Window.Width = width + 40;
            Pump();
        }

        public AiAttachmentTray Tray { get; }

        public Border Below { get; }

        public Window Window { get; }

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public List<AiAttachmentTrayItem> Items => Tray.Items.ToList();

        public List<string> Names => Tray.LiveItems.Select(i => i.Attachment.Name).ToList();

        public double X(AiAttachmentTrayItem item) => item.TranslatePoint(default, Tray)!.Value.X;
    }

    #region Geometry

    [AvaloniaFact]
    public void A_Row_Of_Chips_Is_60_High_With_Chips_8_Apart()
    {
        var rig = new Rig(Attachments(2));
        var tray = rig.Tray;
        tray.Bounds.Height.ShouldBe(60);
        tray.HasItems.ShouldBeTrue();
        tray.Classes.ShouldNotContain(AiAttachmentTray.EmptyPseudoClass);
        var items = rig.Items;
        items.Count.ShouldBe(2);
        items[0].Chip.TranslatePoint(default, tray)!.Value.ShouldBe(new Point(0, 0));
        items[1].Chip.TranslatePoint(default, tray)!.Value.ShouldBe(new Point(212 + 8, 0));
        // The next element starts 8 below the chips.
        rig.Below.TranslatePoint(default, tray)!.Value.Y.ShouldBe(60);
        // The viewport reaches 4px past the left, top and right edges.
        var scroller = tray.Scroller!;
        scroller.TranslatePoint(default, tray)!.Value.ShouldBe(new Point(-4, -4));
        scroller.Bounds.Width.ShouldBe(tray.Bounds.Width + 8);
        scroller.Bounds.Height.ShouldBe(64);
        scroller.HorizontalScrollBarVisibility.ShouldBe(ScrollBarVisibility.Hidden);
        scroller.VerticalScrollBarVisibility.ShouldBe(ScrollBarVisibility.Disabled);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void An_Empty_Tray_Takes_No_Space_And_Holds_Nothing_To_Focus()
    {
        var rig = new Rig(new AvaloniaList<AiAttachment>());
        var tray = rig.Tray;
        tray.Bounds.Height.ShouldBe(0);
        tray.HasItems.ShouldBeFalse();
        tray.Classes.ShouldContain(AiAttachmentTray.EmptyPseudoClass);
        tray.Scroller!.Opacity.ShouldBe(0);
        rig.Below.TranslatePoint(default, tray)!.Value.Y.ShouldBe(0);
        tray.Focusable.ShouldBeFalse();
        tray.Scroller.Focusable.ShouldBeFalse();
        tray.GetVisualDescendants().OfType<InputElement>().Any(e => e.Focusable && e.IsEffectivelyVisible).ShouldBeFalse();

        // No source at all is the same.
        var none = new Rig(null);
        none.Tray.Bounds.Height.ShouldBe(0);
        none.Tray.HasItems.ShouldBeFalse();
        rig.Window.Close();
        none.Window.Close();
    }

    [AvaloniaFact]
    public void Many_Chips_Scroll_Sideways_Without_Jumping_To_The_Newest()
    {
        var items = Attachments(2);
        var rig = new Rig(items, width: 360);
        var scroller = rig.Tray.Scroller!;
        items.Add(Sample("tester-welcome.docx", 29 * 1024));
        items.Add(Sample("headcount-plan.xlsx", 88 * 1024));
        items.Add(Sample("deck.pptx", 2_400_000));
        rig.Pump();
        scroller.Extent.Width.ShouldBe(5 * 212 + 4 * 8 + 8);
        scroller.Viewport.Width.ShouldBe(368);
        scroller.Offset.X.ShouldBe(0);
        // It can be scrolled by hand.
        scroller.Offset = new Vector(300, 0);
        rig.Pump();
        scroller.Offset.X.ShouldBe(300);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Tabbing_Into_A_Chip_Scrolls_It_Into_View()
    {
        var rig = new Rig(Attachments(4), width: 360);
        var scroller = rig.Tray.Scroller!;
        rig.Items[3].Chip.ActionButton!.Focus(NavigationMethod.Tab);
        rig.Pump();
        scroller.Offset.X.ShouldBeGreaterThan(0);
        rig.Window.Close();
    }

    #endregion

    #region Following the collection

    [AvaloniaFact]
    public void Adds_Inserts_Removes_Replaces_And_Moves_Follow_The_Collection()
    {
        var items = Attachments(3);
        var rig = new Rig(items);
        rig.Names.ShouldBe(["whiteboard.png", "sprint-15-review.pdf", "usage-oct.csv"]);

        items.Insert(1, Sample("tester-welcome.docx", 1));
        rig.Pump();
        rig.Names.ShouldBe(["whiteboard.png", "tester-welcome.docx", "sprint-15-review.pdf", "usage-oct.csv"]);
        rig.Items.Select(i => i.Attachment.Name).ShouldBe(rig.Names);

        items.RemoveAt(0);
        rig.Pump();
        rig.Names.ShouldBe(["tester-welcome.docx", "sprint-15-review.pdf", "usage-oct.csv"]);
        rig.Items.Count.ShouldBe(3);

        items[2] = Sample("deck.pptx", 1);
        rig.Pump();
        rig.Names.ShouldBe(["tester-welcome.docx", "sprint-15-review.pdf", "deck.pptx"]);

        items.Move(0, 2);
        rig.Pump();
        rig.Names.ShouldBe(["sprint-15-review.pdf", "deck.pptx", "tester-welcome.docx"]);
        rig.Items.Select(i => i.Attachment.Name).ShouldBe(rig.Names);
        rig.X(rig.Items[2]).ShouldBe(2 * 220);

        items.Clear();
        rig.Pump();
        rig.Names.ShouldBeEmpty();
        rig.Items.ShouldBeEmpty();
        rig.Tray.HasItems.ShouldBeFalse();
        rig.Tray.Bounds.Height.ShouldBe(0);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void An_ObservableCollection_And_A_Plain_List_Work_Too()
    {
        var observable = new ObservableCollection<AiAttachment>(Attachments(1));
        var rig = new Rig(observable);
        observable.Add(Sample("tester-welcome.docx", 1));
        rig.Pump();
        rig.Names.Count.ShouldBe(2);

        var plain = new Rig(new List<AiAttachment>(Attachments(2)));
        plain.Names.Count.ShouldBe(2);
        rig.Window.Close();
        plain.Window.Close();
    }

    [AvaloniaFact]
    public void The_Same_Attachment_Twice_Gets_Two_Chips_And_Leaves_One_At_A_Time()
    {
        var one = Sample("tester-welcome.docx", 1);
        var items = new AvaloniaList<AiAttachment> { one, Sample("deck.pptx", 1), one };
        var rig = new Rig(items);
        rig.Items.Count.ShouldBe(3);
        items.RemoveAt(2);
        rig.Pump();
        rig.Names.ShouldBe(["tester-welcome.docx", "deck.pptx"]);
        rig.Tray.LiveItems[0].Attachment.ShouldBeSameAs(one);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void A_New_Source_Rebuilds_And_The_Old_One_Is_Let_Go()
    {
        var first = Attachments(2);
        var rig = new Rig(first);
        var second = Attachments(4);
        rig.Tray.ItemsSource = second;
        rig.Pump();
        rig.Names.Count.ShouldBe(4);
        first.Add(Sample("late.txt", 1));
        rig.Pump();
        rig.Names.Count.ShouldBe(4);
        second.RemoveAt(0);
        rig.Pump();
        rig.Names.Count.ShouldBe(3);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Changes_While_Away_Show_On_Return()
    {
        var items = Attachments(2);
        var rig = new Rig(items);
        var parent = (StackPanel)rig.Tray.Parent!;
        parent.Children.Remove(rig.Tray);
        rig.Pump();
        items.Add(Sample("tester-welcome.docx", 1));
        items.RemoveAt(0);
        parent.Children.Insert(0, rig.Tray);
        rig.Pump();
        rig.Names.ShouldBe(["sprint-15-review.pdf", "tester-welcome.docx"]);
        rig.Items.Count.ShouldBe(2);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Texts_Reach_Every_Chip_And_Follow_Changes()
    {
        var items = Attachments(1);
        items[0].Status = AiAttachmentStatus.Uploading;
        items[0].Progress = 0.5;
        var rig = new Rig(items);
        var chip = rig.Items[0].Chip;
        chip.MetaText.ShouldBe("50% uploaded");
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Stop uploading whiteboard.png");
        chip.ImageText.ShouldBe("Picture");
        chip.LinesText!(3).ShouldBe("3 lines of text");
        chip.ErrorText.ShouldBe("Could not upload");

        rig.Tray.UploadingText = p => $"{p}% sent";
        chip.MetaText.ShouldBe("50% sent");
        items.Add(Sample("deck.pptx", 1));
        rig.Pump();
        rig.Items[1].Chip.UploadingText!(7).ShouldBe("7% sent");
        rig.Tray.RemoveText = n => $"Drop {n}";
        AutomationProperties.GetName(rig.Items[1].Chip.ActionButton!).ShouldBe("Drop deck.pptx");
        rig.Window.Close();
    }

    #endregion

    #region Requests, focus, accessibility

    [AvaloniaFact]
    public void Requests_Bubble_Through_The_Tray_With_The_Attachment()
    {
        var items = Attachments(2);
        items[1].Status = AiAttachmentStatus.Uploading;
        var rig = new Rig(items);
        var removed = new List<AiAttachment>();
        var cancelled = new List<AiAttachment>();
        rig.Tray.RemoveRequested += (_, e) => removed.Add(e.Attachment);
        rig.Tray.CancelRequested += (_, e) => cancelled.Add(e.Attachment);
        foreach (var item in rig.Items)
        {
            item.Chip.ActionButton!.Focus(NavigationMethod.Tab);
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
        removed.ShouldBe([items[0]]);
        cancelled.ShouldBe([items[1]]);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void An_Owner_That_Removes_On_Request_Closes_The_Loop()
    {
        var items = Attachments(3);
        var rig = new Rig(items);
        rig.Tray.RemoveRequested += (_, e) => items.Remove(e.Attachment);
        var middle = rig.Items[1].Chip.ActionButton!;
        middle.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        rig.Names.ShouldBe(["whiteboard.png", "usage-oct.csv"]);
        // Focus moved to the chip now in its place, still with the keyboard ring.
        var next = rig.Items[1].Chip.ActionButton!;
        next.IsFocused.ShouldBeTrue();
        next.Classes.ShouldContain(":focus-visible");
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Focus_Moves_To_The_Previous_Chip_After_The_Last_One_Leaves()
    {
        var items = Attachments(2);
        var rig = new Rig(items);
        rig.Items[1].Chip.ActionButton!.Focus(NavigationMethod.Pointer);
        items.RemoveAt(1);
        rig.Pump();
        var previous = rig.Items[0].Chip.ActionButton!;
        previous.IsFocused.ShouldBeTrue();
        previous.Classes.ShouldNotContain(":focus-visible");

        // With no chip left the owner decides: the tray does not move focus anywhere.
        items.RemoveAt(0);
        rig.Pump();
        rig.Items.ShouldBeEmpty();
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Focus_Elsewhere_Stays_Where_It_Is()
    {
        var before = new Button { Content = "Before" };
        var items = Attachments(2);
        var rig = new Rig(items, before: before);
        before.Focus(NavigationMethod.Tab);
        items.RemoveAt(0);
        rig.Pump();
        before.IsFocused.ShouldBeTrue();
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Tab_Goes_Through_The_Chip_Buttons_In_Order()
    {
        var before = new Button { Content = "Before" };
        var rig = new Rig(Attachments(2), before: before);
        before.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        rig.Items[0].Chip.ActionButton!.IsFocused.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        rig.Items[1].Chip.ActionButton!.IsFocused.ShouldBeTrue();
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void The_Tray_Is_A_List_Of_Chips()
    {
        var rig = new Rig(Attachments(2));
        AutomationProperties.SetName(rig.Tray, "Attachments");
        var peer = ControlAutomationPeer.CreatePeerForElement(rig.Tray);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.List);
        peer.GetName().ShouldBe("Attachments");
        var chips = peer.GetChildren().SelectMany(Flatten).Where(p => p.GetAutomationControlType() == AutomationControlType.ListItem).ToList();
        chips.Select(p => p.GetName()).ShouldBe(["whiteboard.png", "sprint-15-review.pdf"]);
        rig.Window.Close();

        static IEnumerable<AutomationPeer> Flatten(AutomationPeer peer) =>
            peer.GetAutomationControlType() == AutomationControlType.ListItem ? [peer] : peer.GetChildren().SelectMany(Flatten);
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void At_Full_Motion_A_New_Chip_Grows_From_0_92_And_Fades_In()
    {
        var items = Attachments(1);
        var rig = new Rig(items, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        items.Add(Sample("tester-welcome.docx", 1));
        rig.Pump();
        var item = rig.Items[1];
        item.Presence.Mode.ShouldBe(PresenceMode.Scale);
        item.Presence.EnterScale.ShouldBe(0.92);
        item.Presence.Spring.ShouldBe(ArcSpring.Settle);
        item.Presence.IsPresent.ShouldBeTrue();
        item.Presence.Opacity.ShouldBe(0);
        CoreFrames.Run(clock, 3);
        item.Presence.Opacity.ShouldBeInRange(0.05, 0.95);
        var scale = ((Avalonia.Media.ScaleTransform)((Avalonia.Media.TransformGroup)item.Presence.RenderTransform!).Children[0]).ScaleX;
        scale.ShouldBeInRange(0.92, 1);
        item.Chip.Effect.ShouldBeNull();
        clock.Advance(TimeSpan.FromSeconds(1));
        item.Presence.Opacity.ShouldBe(1);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void At_Full_Motion_A_Removed_Chip_Leaves_First_Then_The_Rest_Slide_Over()
    {
        var items = Attachments(3);
        var rig = new Rig(items, ArcMotionLevel.Full, width: 760);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var leaving = rig.Items[0];
        var second = rig.Items[1];
        var third = rig.Items[2];
        items.RemoveAt(0);
        rig.Pump();
        // While it leaves it keeps its slot and takes no input.
        leaving.IsLeaving.ShouldBeTrue();
        leaving.IsHitTestVisible.ShouldBeFalse();
        KeyboardNavigation.GetIsTabStop(leaving.Chip.ActionButton!).ShouldBeFalse();
        rig.Items.Count.ShouldBe(3);
        rig.X(second).ShouldBe(220);
        rig.Tray.LiveItems.Count.ShouldBe(2);
        CoreFrames.Run(clock, 3);
        leaving.Presence.Opacity.ShouldBeInRange(0.05, 0.95);
        rig.X(second).ShouldBe(220);

        // The exit is over: the slot goes, and the others start where they were.
        clock.Advance(TimeSpan.FromMilliseconds(200));
        rig.Pump();
        rig.Items.ShouldBe([second, third]);
        second.LastX.ShouldBe(0);
        second.Offset.ShouldBe(220, 1);
        third.Offset.ShouldBe(220, 1);
        CoreFrames.Run(clock, 6);
        second.Offset.ShouldBeInRange(1, 219);
        clock.Advance(TimeSpan.FromSeconds(1.2));
        second.Offset.ShouldBe(0, 0.01);
        third.Offset.ShouldBe(0, 0.01);
        rig.X(second).ShouldBe(0, 0.01);
        rig.X(third).ShouldBe(220, 0.01);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void At_Full_Motion_An_Inserted_Chip_Pushes_The_Rest_Over_On_A_Spring()
    {
        var items = Attachments(2);
        var rig = new Rig(items, ArcMotionLevel.Full, width: 760);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var second = rig.Items[1];
        items.Insert(1, Sample("tester-welcome.docx", 1));
        rig.Pump();
        second.LastX.ShouldBe(440);
        second.Offset.ShouldBe(-220, 1);
        clock.Advance(TimeSpan.FromSeconds(1.2));
        second.Offset.ShouldBe(0, 0.01);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Row_Folds_Away_On_A_Spring_When_The_Last_Chip_Goes()
    {
        var items = Attachments(1);
        var rig = new Rig(items, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var size = TestHelpers.Descendant<SpringSizeDecorator>(rig.Tray, AiAttachmentTray.SizePart);
        size.Spring.ShouldBe(ArcSpring.Smooth);
        items.RemoveAt(0);
        rig.Pump();
        rig.Tray.Classes.ShouldContain(AiAttachmentTray.EmptyPseudoClass);
        CoreFrames.Run(clock, 6);
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBeInRange(1, 59);
        size.IsMoving.ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(1.5));
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBe(0);
        rig.Items.ShouldBeEmpty();

        // And unfolds for the next one.
        items.Add(Sample("tester-welcome.docx", 1));
        rig.Pump();
        CoreFrames.Run(clock, 6);
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBeInRange(1, 59);
        clock.Advance(TimeSpan.FromSeconds(1.5));
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBe(60);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Chips_Without_Scale_Or_Slide()
    {
        var items = Attachments(2);
        var rig = new Rig(items, ArcMotionLevel.Reduced, width: 760);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        items.Add(Sample("tester-welcome.docx", 1));
        rig.Pump();
        var added = rig.Items[2];
        CoreFrames.Run(clock, 1);
        added.Presence.Opacity.ShouldBeInRange(0.05, 0.95);
        var scale = ((Avalonia.Media.ScaleTransform)((Avalonia.Media.TransformGroup)added.Presence.RenderTransform!).Children[0]).ScaleX;
        scale.ShouldBe(1);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        added.Presence.Opacity.ShouldBe(1);

        var second = rig.Items[1];
        items.RemoveAt(0);
        rig.Pump();
        clock.Advance(TimeSpan.FromMilliseconds(200));
        rig.Pump();
        rig.Items.Count.ShouldBe(2);
        second.Offset.ShouldBe(0);
        rig.X(second).ShouldBe(0);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Without_Motion_Chips_Come_And_Go_At_Once()
    {
        var items = Attachments(2);
        var rig = new Rig(items);
        items.Add(Sample("tester-welcome.docx", 1));
        rig.Pump();
        rig.Items[2].Presence.Opacity.ShouldBe(1);
        var second = rig.Items[1];
        items.RemoveAt(0);
        rig.Pump();
        rig.Items.Count.ShouldBe(2);
        second.Offset.ShouldBe(0);
        rig.X(second).ShouldBe(0);
        items.Clear();
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBe(0);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void Chips_There_From_The_Start_Do_Not_Animate()
    {
        var rig = new Rig(Attachments(2), ArcMotionLevel.Full);
        foreach (var item in rig.Items)
        {
            item.Presence.Opacity.ShouldBe(1);
            item.Offset.ShouldBe(0);
        }
        rig.Tray.Bounds.Height.ShouldBe(60);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void The_Row_Fade_Is_Declared_Per_Motion_Level()
    {
        var full = new Rig(Attachments(1), ArcMotionLevel.Full);
        var reduced = new Rig(Attachments(1), ArcMotionLevel.Reduced);
        var off = new Rig(Attachments(1));
        full.Tray.Scroller!.Transitions!.Single().ShouldBeOfType<SpringDoubleTransition>();
        reduced.Tray.Scroller!.Transitions!.Single().ShouldBeOfType<ArcDoubleTransition>();
        (off.Tray.Scroller!.Transitions is null || off.Tray.Scroller.Transitions.Count == 0).ShouldBeTrue();
        full.Window.Close();
        reduced.Window.Close();
        off.Window.Close();
    }

    #endregion

    #region Compact (the AI chat's row)

    [AvaloniaFact]
    public void A_Compact_Row_Is_40_High_With_Pills_6_Apart_2_In_From_The_Edges()
    {
        var rig = new Rig(Attachments(2), width: 600);
        var tray = rig.Tray;
        tray.ChipAppearance = AiAttachmentChipAppearance.Compact;
        rig.Pump();
        tray.Classes.ShouldContain(AiAttachmentTray.CompactPseudoClass);
        tray.Bounds.Height.ShouldBe(40);
        var items = rig.Items;
        items.ShouldAllBe(i => i.Chip.Appearance == AiAttachmentChipAppearance.Compact);
        items[0].Chip.Bounds.Height.ShouldBe(32);
        items[0].Chip.TranslatePoint(default, tray)!.Value.ShouldBe(new Point(2, 2));
        items[1].Chip.TranslatePoint(default, tray)!.Value.ShouldBe(new Point(2 + items[0].Chip.Bounds.Width + 6, 2));
        // The text row starts 6 below the pills.
        rig.Below.TranslatePoint(default, tray)!.Value.Y.ShouldBe(40);
        TestHelpers.Descendant<SpringSizeDecorator>(tray, AiAttachmentTray.SizePart).Spring.ShouldBe(ArcSpring.Settle);

        // New chips come in compact; back to cards, every chip follows.
        rig.Tray.ItemsSource!.ShouldBeOfType<AvaloniaList<AiAttachment>>().Add(Sample("usage-oct.csv", 3));
        rig.Pump();
        rig.Items[2].Chip.Appearance.ShouldBe(AiAttachmentChipAppearance.Compact);
        tray.ChipAppearance = AiAttachmentChipAppearance.Card;
        rig.Pump();
        rig.Items.ShouldAllBe(i => i.Chip.Appearance == AiAttachmentChipAppearance.Card);
        tray.Bounds.Height.ShouldBe(60);
        rig.Window.Close();
    }

    [AvaloniaFact]
    public void At_Full_Motion_A_Compact_Row_Opens_On_The_Settle_Spring_With_The_Pill_In_Place()
    {
        // The manual clock goes in before the window: switching the appearance of the shown (empty) tray already starts
        // a spring (the size decorator follows the row's new width), and a frame requested from the real clock is only
        // served by a render tick this rig never runs, so every later motion of the window would wait behind it.
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new AvaloniaList<AiAttachment>();
        var rig = new Rig(items, ArcMotionLevel.Full, width: 600);
        rig.Tray.ChipAppearance = AiAttachmentChipAppearance.Compact;
        rig.Pump();
        clock.Advance(TimeSpan.FromSeconds(1));
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBe(0);
        items.Add(Sample("stage-timings.csv", 1229));
        rig.Pump();
        CoreFrames.Run(clock, 6);
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBeInRange(1, 39);
        // The pill takes its place at once (a 32px slot); only its scale and fade move.
        rig.Items[0].Bounds.Height.ShouldBe(32);
        clock.Advance(TimeSpan.FromSeconds(1.5));
        rig.Pump();
        rig.Tray.Bounds.Height.ShouldBe(40);
        rig.Items[0].Bounds.Width.ShouldBeGreaterThan(24);
        rig.Window.Close();
    }

    #endregion
}
