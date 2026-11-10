using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class PresenceStackLiveViewersDemo : UserControl
{
    private static readonly (string Name, string Detail)[] s_people =
    [
        ("Ada Brook", "Editing the intro"),
        ("Ben Ortiz", "Reading"),
        ("Chen Wei", "Commenting on slide 4"),
        ("Dana Ruiz", "Reading"),
        ("Eli Novak", "On mobile"),
        ("Fay Lindqvist", "Editing the budget"),
        ("Gus Amari", "Reading"),
        ("Hana Sato", "Presenting")
    ];

    private readonly ObservableCollection<PresenceStackViewer> _viewers = new();
    private int _next;

    public PresenceStackLiveViewersDemo()
    {
        InitializeComponent();
        for (var i = 0; i < 3; i++)
        {
            Join();
        }
        Stack.ItemsSource = _viewers;
    }

    private void Join()
    {
        var (name, detail) = s_people[_next % s_people.Length];
        _next++;
        // Newcomers go at the end of the list; the stack shows the most recent first.
        _viewers.Add(new PresenceStackViewer(name, detail: detail));
    }

    private void OnJoin(object? sender, RoutedEventArgs e)
    {
        if (_viewers.Count < s_people.Length)
        {
            Join();
        }
    }

    private void OnLeave(object? sender, RoutedEventArgs e)
    {
        if (_viewers.Count > 0)
        {
            _viewers.RemoveAt(0);
        }
    }

    private void OnIdle(object? sender, RoutedEventArgs e)
    {
        if (_viewers.Count > 0)
        {
            var viewer = _viewers[^1];
            viewer.IsIdle = !viewer.IsIdle;
        }
    }
}
