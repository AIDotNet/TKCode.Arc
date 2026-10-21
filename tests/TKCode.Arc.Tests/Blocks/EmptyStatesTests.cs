using System.Windows.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks;

public class EmptyStatesTests
{
    private static Window Show(Control control, double width = 800, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 720;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string? Title(EmptyStates states) => TestHelpers.Descendant<EmptyStatesTextSwap>(states, "PART_Title").CurrentBlock?.Text;

    private static string? Description(EmptyStates states) => TestHelpers.Descendant<EmptyStatesTextSwap>(states, "PART_Description").CurrentBlock?.Text;

    private static Button Action(EmptyStates states) => TestHelpers.Descendant<Button>(states, "PART_Action");

    private static IReadOnlyList<EmptyStatesTab> Tabs(EmptyStates states) => states.TabPanel!.Tabs;

    private static void Near(Thickness actual, Thickness expected)
    {
        actual.Left.ShouldBe(expected.Left, 0.01);
        actual.Top.ShouldBe(expected.Top, 0.01);
        actual.Right.ShouldBe(expected.Right, 0.01);
        actual.Bottom.ShouldBe(expected.Bottom, 0.01);
    }

    private static void Press(Window window, Avalonia.Controls.Button button)
    {
        button.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_Show_Arcs_Four_Scenes_And_Copy()
    {
        var states = new EmptyStates();
        var window = Show(states);
        try
        {
            states.Items.ShouldBeEmpty();
            Tabs(states).Select(t => t.Content).ShouldBe(["No results", "Offline", "Caught up", "Not found"]);
            states.SelectedIndex.ShouldBe(0);
            states.SelectedItem.ShouldNotBeNull().Scene.ShouldBe(EmptyStatesScene.NoResults);
            states.Phase.ShouldBe(EmptyStatesPhase.Idle);
            Title(states).ShouldBe("No results for “Q3 roadmap”");
            Description(states).ShouldBe("Two filters are on: owner is Emma Collins and status is archived.");
            Action(states).Content.ShouldBe("Clear filters");
            Action(states).Size.ShouldBe(ControlSize.Small);
            Action(states).Variant.ShouldBe(ButtonVariant.Primary);

            AutomationProperties.GetName(states).ShouldBe("Empty states");
            AutomationProperties.GetLandmarkType(states).ShouldBe(AutomationLandmarkType.Region);
            AutomationProperties.GetName(states.TabPanel!).ShouldBe("Empty state");
            AutomationProperties.GetName(states.Art!).ShouldBe("A magnifying glass over three empty result rows");
            ControlAutomationPeer.CreatePeerForElement(states.Art!).GetAutomationControlType().ShouldBe(AutomationControlType.Image);
            ControlAutomationPeer.CreatePeerForElement(Tabs(states)[0]).GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);

            var card = TestHelpers.Descendant<Border>(states, "PART_Card");
            card.CornerRadius.ShouldBe(new CornerRadius(20));
            card.BorderThickness.ShouldBe(new Thickness(1));
            var track = TestHelpers.Descendant<Border>(states, "PART_TabTrack");
            track.IsVisible.ShouldBeTrue();
            track.Bounds.Width.ShouldBe(460, 0.5);
            track.CornerRadius.ShouldBe(new CornerRadius(12));
            Tabs(states).ShouldAllBe(t => Math.Abs(t.Bounds.Height - 30) < 0.5);
            states.Art!.Bounds.Width.ShouldBe(360, 0.5);
            states.Art.Bounds.Height.ShouldBe(243, 0.5);
            TestHelpers.Descendant<GlidingIndicator>(states, "PART_Highlight").CurrentRect.ShouldBe(Tabs(states)[0].Bounds.Deflate(0.5));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Action_Raises_The_Event_And_Runs_The_Command_With_Item_And_Phase()
    {
        var command = new RecordingCommand();
        var states = new EmptyStates { ActionCommand = command };
        var raised = new List<EmptyStatesActionEventArgs>();
        states.ActionInvoked += (_, e) => raised.Add(e);
        var window = Show(states);
        try
        {
            Press(window, Action(states));
            raised.Count.ShouldBe(1);
            raised[0].Item.ShouldBeSameAs(states.SelectedItem);
            raised[0].Phase.ShouldBe(EmptyStatesPhase.Idle);
            raised[0].Source.ShouldBe(states);
            command.Parameters.ShouldBe([raised[0]]);
            // The control does no work of its own: the phase only changes when the app sets it.
            states.Phase.ShouldBe(EmptyStatesPhase.Idle);

            states.Phase = EmptyStatesPhase.Done;
            Dispatcher.UIThread.RunJobs();
            Title(states).ShouldBe("12 results for “Q3 roadmap”");
            Action(states).Content.ShouldBe("Restore filters");
            Press(window, Action(states));
            raised.Count.ShouldBe(2);
            raised[1].Phase.ShouldBe(EmptyStatesPhase.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Keeps_The_Title_Shows_The_Loading_Line_And_Ignores_Presses()
    {
        var states = new EmptyStates { SelectedIndex = 1 };
        var raised = 0;
        states.ActionInvoked += (_, _) => raised++;
        var window = Show(states);
        try
        {
            states.Phase = EmptyStatesPhase.Loading;
            Dispatcher.UIThread.RunJobs();
            Title(states).ShouldBe("You are offline");
            Description(states).ShouldBe("Reaching sync.northwind.example…");
            Action(states).IsLoading.ShouldBeTrue();
            Action(states).Content.ShouldBe("Try again");
            Press(window, Action(states));
            raised.ShouldBe(0);

            states.Phase = EmptyStatesPhase.Done;
            Dispatcher.UIThread.RunJobs();
            Title(states).ShouldBe("Back online");
            Description(states).ShouldBe("Three edits synced to the Northwind workspace just now.");
            Action(states).IsLoading.ShouldBeFalse();
            AutomationProperties.GetName(states.Art!).ShouldBe("A cloud joined to a device by an unbroken line, with a check on the device");

            // Loading from Done keeps the Done title and label.
            states.Phase = EmptyStatesPhase.Loading;
            Dispatcher.UIThread.RunJobs();
            Title(states).ShouldBe("Back online");
            Action(states).Content.ShouldBe("Go offline");
            Description(states).ShouldBe("Reaching sync.northwind.example…");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Scene_Without_Loading_Copy_Keeps_Its_Description_While_Loading()
    {
        var states = new EmptyStates();
        var window = Show(states);
        try
        {
            states.Phase = EmptyStatesPhase.Loading;
            Dispatcher.UIThread.RunJobs();
            Description(states).ShouldBe("Two filters are on: owner is Emma Collins and status is archived.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Tabs_Resets_The_Phase_And_Keeps_Index_And_Item_In_Step()
    {
        var states = new EmptyStates();
        var window = Show(states);
        try
        {
            states.Phase = EmptyStatesPhase.Done;
            states.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            states.Phase.ShouldBe(EmptyStatesPhase.Idle);
            states.SelectedItem!.Scene.ShouldBe(EmptyStatesScene.CaughtUp);
            Title(states).ShouldBe("All caught up");
            Tabs(states).Select(t => t.IsSelected).ShouldBe([false, false, true, false]);
            Tabs(states).Select(KeyboardNavigation.GetIsTabStop).ShouldBe([false, false, true, false]);

            var last = states.SelectedItem;
            states.SelectedIndex = 3;
            states.SelectedItem!.Scene.ShouldBe(EmptyStatesScene.NotFound);
            states.Phase = EmptyStatesPhase.Done;
            states.SelectedItem = last;
            states.SelectedIndex.ShouldBe(2);
            states.Phase.ShouldBe(EmptyStatesPhase.Idle);
            Title(states).ShouldBe("All caught up");
            Tabs(states)[2].IsSelected.ShouldBeTrue();

            // Out of range indexes clamp; items that are not in the list are refused.
            states.SelectedIndex = 9;
            states.SelectedIndex.ShouldBe(3);
            states.SelectedItem = new EmptyStatesItem(EmptyStatesScene.Offline);
            states.SelectedIndex.ShouldBe(3);
            states.SelectedItem!.Scene.ShouldBe(EmptyStatesScene.NotFound);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Wrap_Home_And_End_Select_And_Focus_Tabs()
    {
        var states = new EmptyStates();
        var window = Show(states);
        try
        {
            Tabs(states)[0].Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            states.SelectedIndex.ShouldBe(1);
            Tabs(states)[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            states.SelectedIndex.ShouldBe(3);
            Tabs(states)[3].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            states.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            states.SelectedIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            states.SelectedIndex.ShouldBe(0);
            Tabs(states)[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_A_Tab_Selects_It()
    {
        var states = new EmptyStates();
        var window = Show(states);
        try
        {
            Press(window, Tabs(states)[2]);
            states.SelectedIndex.ShouldBe(2);
            Title(states).ShouldBe("All caught up");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Items_Replace_The_Defaults_And_Fall_Back_To_Arcs_Copy()
    {
        var states = new EmptyStates
        {
            Items =
            {
                new EmptyStatesItem(EmptyStatesScene.Offline) { Header = "Sync", Title = "Sync is paused", ActionLabel = "Resume" },
                new EmptyStatesItem(EmptyStatesScene.NotFound)
            }
        };
        var window = Show(states);
        try
        {
            Tabs(states).Select(t => t.Content).ShouldBe(["Sync", "Not found"]);
            Title(states).ShouldBe("Sync is paused");
            Description(states).ShouldBe("Edits are saved on this device and sync when the connection returns.");
            Action(states).Content.ShouldBe("Resume");
            states.Art!.Frame.ShouldBeSameAs(EmptyStatesArtFrames.Get(EmptyStatesScene.Offline, EmptyStatesPhase.Idle));
            states.SelectedIndex = 1;
            Title(states).ShouldBe("Page not found");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Item_Changes_Update_The_Tabs_And_Keep_Or_Clamp_The_Selection()
    {
        var offline = new EmptyStatesItem(EmptyStatesScene.Offline);
        var inbox = new EmptyStatesItem(EmptyStatesScene.CaughtUp);
        var map = new EmptyStatesItem(EmptyStatesScene.NotFound);
        var states = new EmptyStates { Items = { offline, inbox, map }, SelectedIndex = 2 };
        var window = Show(states);
        try
        {
            states.SelectedItem.ShouldBeSameAs(map);
            states.Items.Insert(0, new EmptyStatesItem(EmptyStatesScene.NoResults));
            Dispatcher.UIThread.RunJobs();
            Tabs(states).Count.ShouldBe(4);
            states.SelectedItem.ShouldBeSameAs(map);
            states.SelectedIndex.ShouldBe(3);

            states.Phase = EmptyStatesPhase.Done;
            states.Items.Remove(map);
            Dispatcher.UIThread.RunJobs();
            Tabs(states).Count.ShouldBe(3);
            states.SelectedIndex.ShouldBe(2);
            states.SelectedItem.ShouldBeSameAs(inbox);
            states.Phase.ShouldBe(EmptyStatesPhase.Idle);

            states.Items.Clear();
            Dispatcher.UIThread.RunJobs();
            Tabs(states).Select(t => t.Content).ShouldBe(["No results", "Offline", "Caught up", "Not found"]);
            states.SelectedItem!.Scene.ShouldBe(EmptyStatesScene.CaughtUp);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void One_Item_Hides_The_Tab_Row()
    {
        var states = new EmptyStates { Items = { new EmptyStatesItem(EmptyStatesScene.CaughtUp) } };
        var window = Show(states);
        try
        {
            TestHelpers.Descendant<Border>(states, "PART_TabTrack").IsVisible.ShouldBeFalse();
            Title(states).ShouldBe("All caught up");
            states.Items.Add(new EmptyStatesItem(EmptyStatesScene.Offline));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Border>(states, "PART_TabTrack").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editing_The_Selected_Item_Updates_The_View_In_Place()
    {
        var item = new EmptyStatesItem(EmptyStatesScene.NoResults);
        var states = new EmptyStates { Items = { item, new EmptyStatesItem(EmptyStatesScene.Offline) } };
        var window = Show(states);
        try
        {
            item.Title = "Nothing matches “invoices”";
            item.Header = "Search";
            item.IllustrationLabel = "Empty search results";
            Dispatcher.UIThread.RunJobs();
            Title(states).ShouldBe("Nothing matches “invoices”");
            Tabs(states)[0].Content.ShouldBe("Search");
            AutomationProperties.GetName(states.Art!).ShouldBe("Empty search results");
            item.Scene = EmptyStatesScene.NotFound;
            Dispatcher.UIThread.RunJobs();
            states.Art!.Frame.ShouldBeSameAs(EmptyStatesArtFrames.Get(EmptyStatesScene.NotFound, EmptyStatesPhase.Idle));
            Description(states).ShouldBe("The link to /projects/atlas-2023 is broken, or the page has moved.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Selection_Made_Before_The_Items_Arrive_Is_Kept()
    {
        // XAML sets attributes before it adds the content items.
        var states = new EmptyStates();
        states.BeginInit();
        states.SelectedIndex = 1;
        states.Phase = EmptyStatesPhase.Done;
        states.Items.Add(new EmptyStatesItem(EmptyStatesScene.CaughtUp));
        states.Items.Add(new EmptyStatesItem(EmptyStatesScene.NotFound));
        states.EndInit();
        var window = Show(states);
        try
        {
            states.SelectedIndex.ShouldBe(1);
            states.SelectedItem!.Scene.ShouldBe(EmptyStatesScene.NotFound);
            states.Phase.ShouldBe(EmptyStatesPhase.Done);
            Title(states).ShouldBe("Found it in Archive");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Phase_Changes_Are_Announced_And_Switching_Tabs_Clears_The_Announcement()
    {
        var states = new EmptyStates { SelectedIndex = 2 };
        var window = Show(states);
        try
        {
            states.Announcement.ShouldBeNull();
            states.Phase = EmptyStatesPhase.Loading;
            states.Announcement.ShouldBe("Checking for new mail…");
            states.Phase = EmptyStatesPhase.Done;
            states.Announcement.ShouldBe("Still all caught up. Checked just now. Nothing new since 8:30 this morning.");
            var announcer = TestHelpers.Descendant<EmptyStatesAnnouncer>(states);
            AutomationProperties.GetLiveSetting(announcer).ShouldBe(AutomationLiveSetting.Polite);
            states.SelectedIndex = 0;
            states.Announcement.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Width_Switches_Between_Narrow_And_Wide_Layouts()
    {
        var states = new EmptyStates();
        var window = Show(states, width: 380);
        try
        {
            states.Classes.ShouldContain(EmptyStates.NarrowPseudoClass);
            states.Classes.ShouldNotContain(EmptyStates.WidePseudoClass);
            Tabs(states)[0].FontSize.ShouldBe(12);
            Tabs(states)[0].Padding.ShouldBe(new Thickness(6, 0));
            TestHelpers.Descendant<EmptyStatesTextSwap>(states, "PART_Title").FontSize.ShouldBe(18);
            Near(TestHelpers.Descendant<Border>(states, "PART_Card").Padding, new Thickness(16, 19));

            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            states.Classes.ShouldNotContain(EmptyStates.NarrowPseudoClass);
            states.Classes.ShouldContain(EmptyStates.WidePseudoClass);
            Tabs(states)[0].FontSize.ShouldBe(14);
            Tabs(states)[0].Padding.ShouldBe(new Thickness(14, 0));
            TestHelpers.Descendant<EmptyStatesTextSwap>(states, "PART_Title").FontSize.ShouldBe(22);
            Near(TestHelpers.Descendant<Border>(states, "PART_Card").Padding, new Thickness(32, 40));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabs_Share_The_Row_Equally_But_Never_Squeeze_A_Label()
    {
        var states = new EmptyStates
        {
            Items =
            {
                new EmptyStatesItem(EmptyStatesScene.NoResults) { Header = "A" },
                new EmptyStatesItem(EmptyStatesScene.Offline) { Header = "B" },
                new EmptyStatesItem(EmptyStatesScene.CaughtUp) { Header = "C" },
                new EmptyStatesItem(EmptyStatesScene.NotFound) { Header = "D" }
            }
        };
        var window = Show(states);
        try
        {
            // Four equal columns: (460 − 2 border − 6 padding − 3 × 2 gap) / 4 (layout rounding moves them by half a pixel).
            Tabs(states).ShouldAllBe(t => Math.Abs(t.Bounds.Width - 111.5) <= 0.5);
            Tabs(states)[1].Bounds.X.ShouldBe(113.5, 0.5);
            states.TabPanel!.ClipToBounds.ShouldBeFalse();

            // A label wider than its share keeps its width; the others share what is left.
            states.Items[3].Header = "A much longer label";
            Dispatcher.UIThread.RunJobs();
            var tabs = Tabs(states);
            var natural = tabs[3].DesiredSize.Width;
            natural.ShouldBeGreaterThan(111.5);
            tabs[3].Bounds.Width.ShouldBe(natural, 0.5);
            tabs[0].Bounds.Width.ShouldBe((446 - natural) / 3, 0.5);
            tabs[0].Bounds.Width.ShouldBe(tabs[2].Bounds.Width, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabs_That_Do_Not_Fit_Slide_The_Selected_Tab_Into_View()
    {
        var states = new EmptyStates
        {
            Items =
            {
                new EmptyStatesItem(EmptyStatesScene.NoResults) { Header = "Nothing in this folder yet" },
                new EmptyStatesItem(EmptyStatesScene.Offline) { Header = "The connection dropped" },
                new EmptyStatesItem(EmptyStatesScene.CaughtUp) { Header = "Every message is read" },
                new EmptyStatesItem(EmptyStatesScene.NotFound) { Header = "This page does not exist" }
            }
        };
        var window = Show(states, width: 380);
        try
        {
            var panel = states.TabPanel!;
            panel.ClipToBounds.ShouldBeTrue();
            states.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            var last = Tabs(states)[3];
            last.Bounds.Right.ShouldBeLessThanOrEqualTo(panel.Bounds.Width + 0.5);
            last.Bounds.Left.ShouldBeGreaterThanOrEqualTo(-0.5);
            states.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Tabs(states)[0].Bounds.Left.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Frames_Use_Arcs_Sampling_And_Are_Centered_In_The_View_Box()
    {
        foreach (var scene in Enum.GetValues<EmptyStatesScene>())
        {
            foreach (var phase in Enum.GetValues<EmptyStatesPhase>())
            {
                var frame = EmptyStatesArtFrames.Get(scene, phase);
                frame.Prims.Length.ShouldBe(10);
                frame.Label.ShouldNotBeNullOrWhiteSpace();
                for (var i = 0; i < 10; i++)
                {
                    var prim = frame.Prims[i];
                    prim.Kind.ShouldBe(EmptyStatesArtFrames.Kinds[i]);
                    prim.Geo.Length.ShouldBe(prim.Kind switch
                    {
                        EmptyStatesPrimKind.Tile => 8,
                        EmptyStatesPrimKind.Body => 176 * 2 + 1,
                        EmptyStatesPrimKind.Stroke => 40 * 2 + 3,
                        _ => 12 * 4 + 3
                    });
                    prim.Geo.ShouldAllBe(v => double.IsFinite(v));
                }
            }
            // The idle drawing's visible bounds are centered on (160, 108).
            var (x0, y0, x1, y1) = Bounds(EmptyStatesArtFrames.Get(scene, EmptyStatesPhase.Idle));
            ((x0 + x1) / 2).ShouldBe(160, 1e-6);
            ((y0 + y1) / 2).ShouldBe(108, 1e-6);
        }
        // The magnifier's lens: a 60px disc, offset with the rest of the scene.
        var lens = EmptyStatesArtFrames.Get(EmptyStatesScene.NoResults, EmptyStatesPhase.Idle).Prims[6].Geo;
        lens[2].ShouldBe(60);
        lens[4].ShouldBe(30);
        lens[7].ShouldBe(1.6);
    }

    private static (double, double, double, double) Bounds(EmptyStatesFrame frame)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var p in frame.Prims)
        {
            if (p.Fx[0] < .01)
            {
                continue;
            }
            if (p.Kind == EmptyStatesPrimKind.Tile)
            {
                x0 = Math.Min(x0, p.Geo[0] - p.Geo[2] / 2);
                x1 = Math.Max(x1, p.Geo[0] + p.Geo[2] / 2);
                y0 = Math.Min(y0, p.Geo[1] - p.Geo[3] / 2);
                y1 = Math.Max(y1, p.Geo[1] + p.Geo[3] / 2);
                continue;
            }
            if (p.Kind != EmptyStatesPrimKind.Body && p.Fx[1] < .01)
            {
                continue;
            }
            for (var i = 0; i < p.Geo.Length - (p.Kind == EmptyStatesPrimKind.Body ? 1 : 3); i += 2)
            {
                x0 = Math.Min(x0, p.Geo[i]);
                x1 = Math.Max(x1, p.Geo[i]);
                y0 = Math.Min(y0, p.Geo[i + 1]);
                y1 = Math.Max(y1, p.Geo[i + 1]);
            }
        }
        return (x0, y0, x1, y1);
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_Every_Shape_To_The_New_Scene_And_Then_Sleeps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var states = new EmptyStates();
        var window = Show(states, level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.5));
            var art = states.Art!;
            art.IsBusy.ShouldBeFalse();
            var from = EmptyStatesArtFrames.Get(EmptyStatesScene.NoResults, EmptyStatesPhase.Idle);
            var to = EmptyStatesArtFrames.Get(EmptyStatesScene.NotFound, EmptyStatesPhase.Idle);

            states.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            art.IsBusy.ShouldBeTrue();
            art.GeometryOf(2).ShouldBe(from.Prims[2].Geo);
            clock.Advance(TimeSpan.FromSeconds(0.2));
            // The body has left the card but has not reached the map yet; the route redraws from nothing.
            art.GeometryOf(2)[0].ShouldNotBe(from.Prims[2].Geo[0]);
            art.GeometryOf(2)[0].ShouldNotBe(to.Prims[2].Geo[0]);
            art.DrawnOf(7).ShouldBeLessThan(0.5);

            clock.Advance(TimeSpan.FromSeconds(2));
            art.IsBusy.ShouldBeFalse();
            for (var i = 0; i < 10; i++)
            {
                art.GeometryOf(i).ShouldBe(to.Prims[i].Geo);
                art.OpacityOf(i).ShouldBe(to.Prims[i].Fx[0]);
                art.DrawnOf(i).ShouldBe(to.Prims[i].Fx[1]);
            }

            // Done: the pin turns success green after its 480ms color cross-over.
            states.Phase = EmptyStatesPhase.Done;
            Dispatcher.UIThread.RunJobs();
            var success = ArcTokens.GetColor(states, ArcDesignTokenKind.Success);
            clock.Advance(TimeSpan.FromSeconds(0.2));
            art.StrokeOf(5).ShouldNotBe(success);
            clock.Advance(TimeSpan.FromSeconds(2));
            art.StrokeOf(5).ShouldBe(success);
            art.IsBusy.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Parallax_Follows_The_Pointer_Under_Full_Motion_Only()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var states = new EmptyStates();
        var window = Show(states, level: ArcMotionLevel.Full);
        try
        {
            var art = states.Art!;
            art.PointAt(1, -1);
            clock.Advance(TimeSpan.FromSeconds(2));
            art.Sway.X.ShouldBe(1, 0.01);
            art.Sway.Y.ShouldBe(-1, 0.01);
            art.IsBusy.ShouldBeFalse();

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            states.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            art.Sway.ShouldBe(default);
            art.PointAt(1, 1);
            art.Sway.ShouldBe(default);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_Without_Travel()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var states = new EmptyStates();
        var window = Show(states, level: ArcMotionLevel.Reduced);
        try
        {
            var art = states.Art!;
            states.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            var to = EmptyStatesArtFrames.Get(EmptyStatesScene.Offline, EmptyStatesPhase.Idle);
            art.GeometryOf(2).ShouldBe(to.Prims[2].Geo);
            art.IsCrossfading.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(0.4));
            art.IsCrossfading.ShouldBeFalse();
            art.IsBusy.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Switches_At_Once()
    {
        var states = new EmptyStates();
        var window = Show(states);
        try
        {
            var art = states.Art!;
            states.SelectedIndex = 2;
            states.Phase = EmptyStatesPhase.Loading;
            Dispatcher.UIThread.RunJobs();
            var to = EmptyStatesArtFrames.Get(EmptyStatesScene.CaughtUp, EmptyStatesPhase.Loading);
            for (var i = 0; i < 10; i++)
            {
                art.GeometryOf(i).ShouldBe(to.Prims[i].Geo);
            }
            art.IsBusy.ShouldBeFalse();
            art.IsCrossfading.ShouldBeFalse();
            art.StrokeOf(6).ShouldBe(ArcTokens.GetColor(states, ArcDesignTokenKind.TextMuted));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Runs_Real_Frames_Without_Throwing()
    {
        var states = new EmptyStates();
        var window = Show(states, level: ArcMotionLevel.Full);
        try
        {
            for (var step = 0; step < 24; step++)
            {
                if (step % 6 == 0)
                {
                    states.SelectedIndex = (states.SelectedIndex + 1) % 4;
                }
                else if (step % 6 == 3)
                {
                    states.Phase = EmptyStatesPhase.Loading;
                }
                else if (step % 6 == 4)
                {
                    states.Phase = EmptyStatesPhase.Done;
                }
                states.Art!.PointAt(step % 2 == 0 ? 0.5 : -0.5, 0.3);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            states.Art!.Frame.ShouldBeSameAs(EmptyStatesArtFrames.Get(states.SelectedItem!.Scene, states.Phase));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Gallery_Has_Its_English_Page_And_Demos()
    {
        var block = BlockCatalog.Blocks.Single(b => b.ClassName == "EmptyStates");
        GalleryDocs.Has("EmptyStates").ShouldBeTrue();
        GalleryDocs.HasDemos("EmptyStates").ShouldBeTrue();
        GalleryDocs.PackageOf("EmptyStates").ShouldBe("TKCode.Arc.Blocks");
        GalleryDocs.Summary("EmptyStates", "en-US").ShouldBe(block.Description);
        var doc = GalleryDocs.Get("EmptyStates", "en-US").ShouldNotBeNull();
        doc.Title.ShouldBe(block.Title);
        doc.Markdown.ShouldContain("### EmptyStates");
        doc.Markdown.ShouldContain("### EmptyStatesItem");
    }

    [AvaloniaFact]
    public void Color_Mix_Fades_Through_Premultiplied_Alpha()
    {
        var red = Color.FromRgb(255, 0, 0);
        var transparent = Color.FromArgb(0, 0, 0, 0);
        var half = EmptyStatesArt.Mix(red, transparent, 0.5);
        half.A.ShouldBe((byte)128);
        half.R.ShouldBe((byte)255);
        EmptyStatesArt.Mix(red, Color.FromRgb(0, 0, 255), 1).ShouldBe(Color.FromRgb(0, 0, 255));
        EmptyStatesArt.Mix(red, transparent, 1).A.ShouldBe((byte)0);
    }
}
