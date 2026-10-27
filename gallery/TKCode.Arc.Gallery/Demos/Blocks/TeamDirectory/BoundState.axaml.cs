using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamDirectoryBoundStateDemo : UserControl
{
    private readonly ObservableCollection<string> _log = new();

    public TeamDirectoryBoundStateDemo()
    {
        InitializeComponent();
        Directory.People.AddRange(TeamDirectoryDemoData.People());
        Log.ItemsSource = _log;

        // Picks from the list arrive as PersonSelected; the buttons below drive the same state from code without it.
        Directory.PersonSelected += (_, e) => Add($"Picked {e.Person.Name} (was {e.PreviousPerson?.Name ?? "nobody"})");
        Directory.EmailCopied += (_, e) => Add(e.Succeeded ? $"Copied {e.Email}" : $"Could not copy {e.Email}");
        Directory.PropertyChanged += (_, e) =>
        {
            if (e.Property == TeamDirectory.SelectedTeamProperty || e.Property == TeamDirectory.SearchTextProperty
                || e.Property == TeamDirectory.DisplayedPersonProperty || e.Property == TeamDirectory.SelectedPersonIdProperty)
            {
                ShowState();
            }
        };

        ShowFiona.Click += (_, _) => Directory.SelectPerson("fiona");
        ShowEngineering.Click += (_, _) => Directory.SelectedTeam = "Engineering";
        SearchMunich.Click += (_, _) => Directory.SearchText = "munich";
        Reset.Click += (_, _) => Directory.ClearFilters();
        ShowState();
    }

    private void ShowState()
    {
        var team = Directory.SelectedTeam ?? "all teams";
        var search = string.IsNullOrEmpty(Directory.SearchText) ? "no search" : $"search “{Directory.SearchText}”";
        var shown = Directory.DisplayedPerson?.Name ?? "nobody";
        var picked = Directory.SelectedPersonId ?? "none yet";
        State.Text = $"{team} · {search} · {Directory.MatchCount} shown · profile: {shown} · picked id: {picked}";
    }

    private void Add(string line)
    {
        _log.Insert(0, line);
        while (_log.Count > 5)
        {
            _log.RemoveAt(_log.Count - 1);
        }
    }
}
