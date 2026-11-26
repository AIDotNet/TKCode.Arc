using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Tests for <see cref="SwipeableRow"/>: opening and closing trays, invoking actions, sibling exclusion,
/// <see cref="ListGroup"/> native row hosting, horizontal swipe gestures and <see cref="NavigationStack"/> edge yield.
/// </summary>
public class SwipeableRowTests
{
    private sealed class TestCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);

        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingHaptics : IHapticsPlatform
    {
        public List<HapticKind> Calls { get; } = [];

        public void Trigger(HapticKind kind) => Calls.Add(kind);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Open_And_Close_Move_Row_To_Leading_And_Trailing_Tray_Widths()
    {
        var row = new SwipeableRow
        {
            Content = new ListItem { Content = "Message" },
            LeadingActions = { new SwipeRowAction { Label = "Pin", Role = SwipeRowActionRole.Primary } },
            TrailingActions =
            {
                new SwipeRowAction { Label = "Archive" },
                new SwipeRowAction { Label = "Delete", Role = SwipeRowActionRole.Destructive },
            },
        };
        var window = new Window { Content = row, Width = 390, Height = 120 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            row.OpenSide.ShouldBe(SwipeRowSide.None);
            row.IsOpen.ShouldBeFalse();
            row.SwipeOffset.ShouldBe(0);

            row.OpenTrailing();
            Settle(window);
            row.OpenSide.ShouldBe(SwipeRowSide.Trailing);
            row.IsOpen.ShouldBeTrue();
            row.SwipeOffset.ShouldBe(-152, 0.5);

            row.OpenLeading();
            Settle(window);
            row.OpenSide.ShouldBe(SwipeRowSide.Leading);
            row.IsOpen.ShouldBeTrue();
            row.SwipeOffset.ShouldBe(76, 0.5);

            row.IsOpen = false;
            Settle(window);
            row.OpenSide.ShouldBe(SwipeRowSide.None);
            row.SwipeOffset.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Action_Button_Raises_ActionInvoked_Runs_Command_Triggers_Haptic_And_Closes()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        var recorder = new RecordingHaptics();
        Haptics.Platform = recorder;
        Haptics.IsEnabled = true;

        object? commandArg = null;
        var delete = new SwipeRowAction
        {
            Label = "Delete",
            Role = SwipeRowActionRole.Destructive,
            Command = new TestCommand(p => commandArg = p),
            CommandParameter = "row-1",
        };
        var row = new SwipeableRow
        {
            Content = new ListItem { Content = "Draft" },
            TrailingActions = { delete },
        };
        SwipeRowActionEventArgs? raised = null;
        row.ActionInvoked += (_, e) => raised = e;

        var window = new Window { Content = row, Width = 390, Height = 120 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            row.OpenTrailing();
            Settle(window);

            var button = row.GetVisualDescendants().OfType<AvaloniaButton>().First(b => b.Classes.Contains("destructive"));
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            Settle(window);

            raised.ShouldNotBeNull();
            raised!.Action.ShouldBeSameAs(delete);
            raised.Side.ShouldBe(SwipeRowSide.Trailing);
            commandArg.ShouldBe("row-1");
            row.IsOpen.ShouldBeFalse();
            recorder.Calls.ShouldContain(HapticKind.Warning);
        }
        finally
        {
            window.Close();
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }

    [AvaloniaFact]
    public void Opening_One_Row_In_A_ListGroup_Closes_Open_Sibling_And_Does_Not_Double_Wrap()
    {
        var first = new SwipeableRow
        {
            Content = new ListItem { Content = "First" },
            TrailingActions = { new SwipeRowAction { Label = "Delete", Role = SwipeRowActionRole.Destructive } },
        };
        var second = new SwipeableRow
        {
            Content = new ListItem { Content = "Second" },
            TrailingActions = { new SwipeRowAction { Label = "Archive" } },
        };
        var group = new ListGroup { Items = { first, second } };
        var window = new Window { Content = group, Width = 390, Height = 300 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            // ListGroup uses SwipeableRow directly as the item container instead of wrapping it in a ListItem.
            group.ContainerFromIndex(0).ShouldBeSameAs(first);
            group.ContainerFromIndex(1).ShouldBeSameAs(second);

            first.OpenTrailing();
            Settle(window);
            first.IsOpen.ShouldBeTrue();

            second.OpenTrailing();
            Settle(window);
            second.IsOpen.ShouldBeTrue();
            first.IsOpen.ShouldBeFalse();
            first.OpenSide.ShouldBe(SwipeRowSide.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_And_Disabled_Actions_Are_Respected()
    {
        var hidden = new SwipeRowAction { Label = "Hidden", IsVisible = false };
        var disabled = new SwipeRowAction { Label = "Disabled", IsEnabled = false };
        var row = new SwipeableRow
        {
            Content = new ListItem { Content = "Row" },
            TrailingActions = { hidden, disabled },
        };
        var window = new Window { Content = row, Width = 390, Height = 120 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            row.OpenTrailing();
            Settle(window);
            // Only the one visible action contributes to the tray width (76px).
            row.SwipeOffset.ShouldBe(-76, 0.5);
            var buttons = row.GetVisualDescendants().OfType<AvaloniaButton>().Where(b => b.Classes.Contains("arc-swipe-action")).ToList();
            buttons.Count.ShouldBe(1);
            buttons[0].IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drag_From_Left_Edge_Inside_NavigationStack_Yields_To_SwipeBack()
    {
        var row = new SwipeableRow
        {
            Content = new ListItem { Content = "Swipe me" },
            LeadingActions = { new SwipeRowAction { Label = "Pin", Role = SwipeRowActionRole.Primary } },
        };
        var detail = new Page { Content = new ListGroup { Items = { row } } };
        var root = new Page { Content = new TextBlock { Text = "Root" } };
        var stack = new NavigationStack { Root = root };
        var window = new Window { Content = stack, Width = 390, Height = 600 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Full);
        window.Show();
        try
        {
            Settle(window);
            stack.Push(detail);
            Settle(window);

            var rowTop = row.TranslatePoint(new Point(0, row.Bounds.Height / 2), window)!.Value.Y;
            // Start inside NavigationStack.EdgeWidth (x = 8) and drag right: NavigationStack should handle it, not SwipeableRow.
            window.MouseDown(new Point(8, rowTop), MouseButton.Left);
            window.MouseMove(new Point(220, rowTop));
            window.MouseUp(new Point(220, rowTop), MouseButton.Left);
            Settle(window);

            row.OpenSide.ShouldBe(SwipeRowSide.None);
        }
        finally
        {
            window.Close();
        }
    }
}
