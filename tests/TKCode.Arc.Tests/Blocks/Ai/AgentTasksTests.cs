using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AgentTasks"/>: columns, live changes, the agent filter, opening a run and the keyboard.</summary>
public class AgentTasksTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (AgentTasks Board, ObservableCollection<AgentTaskItem> Tasks, Window Window) Show(double width = 1000)
    {
        var tasks = new ObservableCollection<AgentTaskItem>(AgentTasksSamples.Tasks(Now));
        var board = new AgentTasks { Width = width, Tasks = tasks, Clock = () => Now };
        var window = TestHelpers.Show(board);
        window.Width = 1200;
        Settle(window);
        return (board, tasks, window);
    }

    [AvaloniaFact]
    public void Tasks_Sit_In_The_Column_Of_Their_Status()
    {
        var (board, _, _) = Show();
        board.ColumnAt(0)!.Count.ShouldBe(2);
        board.ColumnAt(1)!.Count.ShouldBe(3);
        board.ColumnAt(2)!.Count.ShouldBe(2); // needs input and failed
        board.ColumnAt(3)!.Count.ShouldBe(2);
        board.ColumnAt(2)!.Title.ShouldBe("Needs you");
    }

    [AvaloniaFact]
    public void A_Status_Change_Moves_The_Card_And_Is_Announced()
    {
        var (board, tasks, window) = Show();
        var task = tasks[0];
        task.Status = AgentTaskStatus.Done;
        Settle(window);
        board.ColumnAt(1)!.Count.ShouldBe(2);
        board.ColumnAt(3)!.Count.ShouldBe(3);
        board.Announcer!.Announcement.ShouldBe("Draft the March release notes: Done");
        tasks.Add(new AgentTaskItem { Title = "New one", Agent = "Quill" });
        Settle(window);
        board.ColumnAt(0)!.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Card_Reads_Its_Task_And_Opens_The_Run()
    {
        var (board, tasks, window) = Show();
        var card = board.CardOf(tasks[0])!;
        card.ProgressText.ShouldBe("62%");
        card.UpdatedText.ShouldBe("2m ago");
        AutomationProperties.GetName(card).ShouldBe("Draft the March release notes, Quill, Running, 62%, Writing the fixes section");
        AgentTaskEventArgs? opened = null;
        board.TaskOpened += (_, e) => opened = e;
        card.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);
        opened!.Task.ShouldBe(tasks[0]);
    }

    [AvaloniaFact]
    public void Agent_Filter_Keeps_One_Agent()
    {
        var (board, _, window) = Show();
        board.AgentFilter = "Ledger";
        Settle(window);
        Enumerable.Range(0, 4).Sum(i => board.ColumnAt(i)!.Count).ShouldBe(2);
        var chips = board.FilterHost!.Children.OfType<AgentTasksChip>().ToList();
        chips.Select(c => c.Content).ShouldBe(new object?[] { "All agents", "Ledger", "Patchwork", "Quill", "Scout" });
        chips[1].IsSelected.ShouldBeTrue();
        chips[0].IsSelected.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Within_And_Across_Columns()
    {
        var (board, _, window) = Show();
        var running = board.ColumnAt(1)!;
        running.CardAt(0)!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Settle(window);
        running.CardAt(1)!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Settle(window);
        board.ColumnAt(2)!.CardAt(1)!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Settle(window);
        board.ColumnAt(0)!.CardAt(1)!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Sample_Tasks_Progress_And_Finish_On_Their_Own()
    {
        var board = new AgentTasks { Width = 1000, Clock = () => Now };
        var window = TestHelpers.Show(board);
        Settle(window);
        board.IsSimulated.ShouldBeTrue();
        var task = board.ShownTasks.First(t => t.Status == AgentTaskStatus.Running);
        var before = task.Progress;
        board.Advance();
        task.Progress.ShouldBeGreaterThan(before);
        for (var i = 0; i < 200; i++)
        {
            board.Advance();
        }
        board.ShownTasks.ShouldNotContain(t => t.Status == AgentTaskStatus.Running && t.Progress >= 1);
        board.ShownTasks.Count(t => t.Status == AgentTaskStatus.Done).ShouldBeGreaterThan(2);
    }

    [AvaloniaFact]
    public void Narrow_Board_Stacks_Its_Columns()
    {
        var (board, _, _) = Show(600);
        board.IsCompact.ShouldBeTrue();
        Grid.GetRow(board.ColumnAt(3)!).ShouldBe(3);
    }
}
