using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Workflow;

public class WorkflowCommandTests
{
    private static (WorkflowCanvas Canvas, Window Window, WorkflowNode A, WorkflowNode B) Show(bool editable = true)
    {
        var graph = new WorkflowGraph();
        var a = new WorkflowNode("a", "A") { Position = new Point(0, 0) };
        var b = new WorkflowNode("b", "B") { Position = new Point(0, 110) };
        graph.Nodes.Add(a);
        graph.Nodes.Add(b);
        var canvas = new WorkflowCanvas { Graph = graph, IsEditable = editable, AutoFit = WorkflowAutoFit.None };
        var window = TestHelpers.Show(canvas);
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (canvas, window, a, b);
    }

    [AvaloniaFact]
    public void Commands_Follow_The_History_The_Selection_And_The_Zoom()
    {
        var (canvas, window, a, b) = Show();
        try
        {
            var changes = 0;
            canvas.UndoCommand.CanExecuteChanged += (_, _) => changes++;
            canvas.UndoCommand.CanExecute(null).ShouldBeFalse();
            canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeFalse();
            canvas.CopyCommand.CanExecute(null).ShouldBeFalse();
            canvas.PasteCommand.CanExecute(null).ShouldBeTrue();

            // A bound button (in a window: a button asks its command once it is shown) follows its command.
            var delete = new Button { Command = canvas.DeleteSelectionCommand, Content = "Delete" };
            var toolbar = TestHelpers.Show(delete);
            delete.IsEffectivelyEnabled.ShouldBeFalse();
            canvas.Select([b]);
            delete.IsEffectivelyEnabled.ShouldBeTrue();
            toolbar.Close();
            canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeTrue();
            canvas.CopyCommand.CanExecute(null).ShouldBeTrue();
            canvas.DuplicateCommand.CanExecute(null).ShouldBeTrue();

            canvas.DeleteSelectionCommand.Execute(null);
            canvas.Graph!.Nodes.ShouldBe([a]);
            canvas.UndoCommand.CanExecute(null).ShouldBeTrue();
            changes.ShouldBeGreaterThan(0);
            canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeFalse();

            canvas.UndoCommand.Execute(null);
            canvas.Graph.Nodes.ShouldBe([a, b]);
            canvas.RedoCommand.CanExecute(null).ShouldBeTrue();

            canvas.ZoomTo(canvas.MaxZoom, animate: false);
            canvas.ZoomInCommand.CanExecute(null).ShouldBeFalse();
            canvas.ZoomOutCommand.CanExecute(null).ShouldBeTrue();
            canvas.ZoomOutCommand.Execute(null);
            canvas.ZoomInCommand.CanExecute(null).ShouldBeTrue();
            canvas.FitCommand.CanExecute(null).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Read_Only_Canvas_Only_Copies_And_Zooms()
    {
        var (canvas, window, a, _) = Show(editable: false);
        try
        {
            canvas.Select([a]);
            canvas.CopyCommand.CanExecute(null).ShouldBeTrue();
            canvas.CutCommand.CanExecute(null).ShouldBeFalse();
            canvas.PasteCommand.CanExecute(null).ShouldBeFalse();
            canvas.DuplicateCommand.CanExecute(null).ShouldBeFalse();
            canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeFalse();
            canvas.UndoCommand.CanExecute(null).ShouldBeFalse();

            // Turning editing on lets them run, and says so.
            var told = false;
            canvas.CutCommand.CanExecuteChanged += (_, _) => told = true;
            canvas.IsEditable = true;
            told.ShouldBeTrue();
            canvas.CutCommand.CanExecute(null).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
