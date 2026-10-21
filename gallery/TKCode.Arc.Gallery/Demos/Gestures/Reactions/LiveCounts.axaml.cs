using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReactionsLiveCountsDemo : UserControl
{
    private readonly Random _random = new(7);

    public ReactionsLiveCountsDemo()
    {
        InitializeComponent();
        Bar.Value = [new Reaction("like", 98), new Reaction("love", 9, true)];
    }

    private void OnSomeoneReactsClick(object? sender, RoutedEventArgs e)
    {
        var current = Bar.Value ?? [];
        var id = _random.Next(3) switch { 0 => "like", 1 => "love", _ => "celebrate" };
        var existing = current.FirstOrDefault(r => r.Id == id);
        Bar.Value = existing is null
            ? [.. current, new Reaction(id, 1)]
            : current.Select(r => r.Id == id ? r with { Count = r.Count + 1 } : r).ToArray();
    }

    private void OnSomeoneLeavesClick(object? sender, RoutedEventArgs e)
    {
        var current = Bar.Value ?? [];
        var target = current.Where(r => r.Count > (r.Reacted ? 1 : 0)).OrderBy(_ => _random.Next()).FirstOrDefault();
        if (target is not null)
        {
            Bar.Value = current.Select(r => r == target ? r with { Count = r.Count - 1 } : r).Where(r => r.Count > 0).ToArray();
        }
    }
}
