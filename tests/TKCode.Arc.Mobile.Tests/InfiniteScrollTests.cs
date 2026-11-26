using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Mobile.Tests;

public class InfiniteScrollTests
{
    [AvaloniaFact]
    public void Scroll_Near_Bottom_Triggers_LoadMore_And_Deferral_Clears_IsLoading()
    {
        var items = new StackPanel();
        for (var i = 0; i < 30; i++)
        {
            items.Children.Add(new Border { Height = 40 });
        }

        var scroll = new InfiniteScroll { Content = items, Threshold = 80 };
        IDisposable? deferral = null;
        var requests = 0;
        scroll.LoadMoreRequested += (_, e) =>
        {
            requests++;
            deferral = e.GetDeferral();
        };

        var window = new Window { Width = 360, Height = 300, Content = scroll };
        window.Show();
        try
        {
            window.UpdateLayout();
            var inner = scroll.InnerScrollViewer.ShouldNotBeNull();
            // Scroll close to the bottom (extent ~1200, viewport 300 -> max offset ~900).
            inner.Offset = new Vector(0, Math.Max(10, inner.Extent.Height - inner.Viewport.Height - 40));
            Dispatcher.UIThread.RunJobs();

            requests.ShouldBe(1);
            scroll.IsLoading.ShouldBeTrue();
            scroll.Classes.Contains(":loading").ShouldBeTrue();

            deferral.ShouldNotBeNull();
            deferral!.Dispose();
            Dispatcher.UIThread.RunJobs();

            scroll.IsLoading.ShouldBeFalse();
            scroll.Classes.Contains(":loading").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Retry_And_Finished_States_Work_With_Command()
    {
        var executed = 0;
        var scroll = new InfiniteScroll
        {
            Command = new ActionCommand(() => executed++),
            Content = new Border { Height = 200 },
        };
        var window = new Window { Width = 360, Height = 300, Content = scroll };
        window.Show();
        try
        {
            scroll.HasError = true;
            scroll.Classes.Contains(":error").ShouldBeTrue();
            scroll.TryLoadMore().ShouldBeFalse();

            scroll.Retry();
            scroll.HasError.ShouldBeFalse();
            scroll.IsLoading.ShouldBeTrue();
            executed.ShouldBe(1);

            scroll.IsLoading = false;
            scroll.HasMore = false;
            scroll.Classes.Contains(":finished").ShouldBeTrue();
            scroll.TryLoadMore().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class ActionCommand(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }
}
