using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvatarGroupTeamDemo : UserControl
{
    private static readonly string[] Names =
    [
        "Maya Chen", "Noah Park", "Leah Fox", "Omar Haddad", "Ines Duarte", "Kenji Sato", "Ava Brooks", "Luca Romano", "Zara Ali"
    ];

    private readonly AvaloniaList<AvatarGroupMember> _team = new()
    {
        new("Maya Chen", status: AvatarStatus.Online),
        new("Noah Park"),
        new("Leah Fox", status: AvatarStatus.Offline),
        new("Omar Haddad"),
        new("Ines Duarte"),
        new("Kenji Sato")
    };

    private int _next = 6;

    public AvatarGroupTeamDemo()
    {
        InitializeComponent();
        Group.ItemsSource = _team;
    }

    // Joining and leaving animate the slot; the list is changed in place.
    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        _team.Insert(1, new AvatarGroupMember(Names[_next++ % Names.Length]));
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (_team.Count > 0)
        {
            _team.RemoveAt(Math.Min(1, _team.Count - 1));
        }
    }
}
