using Avalonia.Controls;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Pages;

public partial class TimelinePage : UserControl
{
    public TimelinePage()
    {
        InitializeComponent();
    }

    private void OnTasksChanged(object? sender, GanttTasksChangedEventArgs e) => (DataContext as TimelinePageViewModel)?.Save(e.Tasks);
}
