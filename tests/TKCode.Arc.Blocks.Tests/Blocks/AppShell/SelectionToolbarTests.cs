using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.AppShell;

/// <summary>Selection toolbar: shown only with a selection, the count and select all, actions, clearing and the keyboard.</summary>
public class SelectionToolbarTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Shows_Count_Actions_And_Clears()
    {
        var list = new Border { Focusable = true, Height = 300 };
        var toolbar = new SelectionToolbar { Content = list, TotalCount = 48, Width = 800 };
        var window = TestHelpers.Show(toolbar);
        try
        {
            Settle();
            toolbar.IsBarPresent.ShouldBeFalse();
            toolbar.SelectedCount = 3;
            Settle();
            toolbar.IsBarPresent.ShouldBeTrue();
            TestHelpers.Descendant<ArcContentSwap>(toolbar, SelectionToolbar.CountPart).Content.ShouldBe("3 selected");
            var selectAll = TestHelpers.Descendant<AvaloniaButton>(toolbar, SelectionToolbar.SelectAllButtonPart);
            selectAll.IsVisible.ShouldBeTrue();
            selectAll.Content.ShouldBe("Select all 48");

            // Default actions, destructive last.
            toolbar.ShownActions.Select(a => a.Key).ShouldBe(["archive", "move", "share", "delete"]);
            var delete = toolbar.ShownActions.Single(a => a.IsDestructive);
            SelectionToolbarActionEventArgs? invoked = null;
            toolbar.ActionInvoked += (_, e) => invoked = e;
            toolbar.ButtonOf(delete)!.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            invoked!.Action.ShouldBe(delete);
            invoked.SelectedCount.ShouldBe(3);

            var selectAllRequested = false;
            toolbar.SelectAllRequested += (_, _) => selectAllRequested = true;
            selectAll.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            selectAllRequested.ShouldBeTrue();
            toolbar.SelectedCount = 48;
            Settle();
            selectAll.IsVisible.ShouldBeFalse();

            var cleared = 0;
            toolbar.Cleared += (_, _) => cleared++;
            list.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            cleared.ShouldBe(1);
            toolbar.SelectedCount.ShouldBe(0);
            toolbar.IsBarPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Actions_Run_Their_Command_And_Go_Icon_Only_When_Narrow()
    {
        var ran = -1;
        var action = new SelectionToolbarAction("tag", "Add tag", ArcIcons.Tag) { Command = new RelayCommand(p => ran = (int)p!) };
        var toolbar = new SelectionToolbar { Content = new Border(), Width = 400, SelectedCount = 2 };
        toolbar.Actions.Add(action);
        var window = TestHelpers.Show(toolbar);
        try
        {
            Settle();
            toolbar.ShownActions.ShouldBe([action]);
            var button = toolbar.ButtonOf(action)!;
            button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            ran.ShouldBe(2);
            AutomationProperties.GetName(button).ShouldBe("Add tag");
            toolbar.Classes.Contains(":compact").ShouldBeTrue();
            Tooltip.GetTip(button).ShouldBe("Add tag");
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand(Action<object?> run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run(parameter);
    }

    [AvaloniaFact]
    public void Actions_Follow_IsEnabled_And_IsVisible_Live()
    {
        var archive = new SelectionToolbarAction { Key = "archive", Label = "Archive" };
        var move = new SelectionToolbarAction { Key = "move", Label = "Move" };
        var toolbar = new SelectionToolbar { Content = new Border { Height = 300 }, TotalCount = 10, SelectedCount = 2, Width = 800 };
        toolbar.Actions.AddRange([archive, move]);
        var window = TestHelpers.Show(toolbar);
        try
        {
            Settle();
            var buttons = TestHelpers.Descendant<Panel>(toolbar, SelectionToolbar.ActionsPart).Children.OfType<AvaloniaButton>().ToList();
            buttons.Count.ShouldBe(2);
            buttons[1].IsVisible.ShouldBeTrue();
            buttons[1].IsEnabled.ShouldBeTrue();

            move.IsEnabled = false;
            buttons[1].IsEnabled.ShouldBeFalse();
            buttons[0].IsEnabled.ShouldBeTrue();
            move.IsEnabled = true;
            buttons[1].IsEnabled.ShouldBeTrue();

            move.IsVisible = false;
            buttons[1].IsVisible.ShouldBeFalse();
            buttons[0].IsVisible.ShouldBeTrue();
            move.IsVisible = true;
            buttons[1].IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
