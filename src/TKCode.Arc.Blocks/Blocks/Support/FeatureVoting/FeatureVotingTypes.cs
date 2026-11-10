using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One proposed feature in a <see cref="FeatureVoting"/> widget. Change <see cref="Votes"/> in place (live results from
/// your server) and the count rolls to the new value.
/// </summary>
public sealed class FeatureVotingOption : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string? _description;
    private int _votes;

    /// <summary>Creates an empty option (XAML).</summary>
    public FeatureVotingOption()
    {
    }

    /// <summary>Creates an option.</summary>
    public FeatureVotingOption(string id, string title, string? description = null, int votes = 0)
    {
        Id = id;
        _title = title;
        _description = description;
        _votes = votes;
    }

    /// <summary>Raised when a property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable identity, reported back when people vote.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The feature's name, such as "Dark mode for reports".</summary>
    public string Title { get => _title; set => Set(ref _title, value); }

    /// <summary>One line under the title; null shows none.</summary>
    public string? Description { get => _description; set => Set(ref _description, value); }

    /// <summary>Votes cast by other people. The widget adds the viewer's own vote on top while the option is chosen.</summary>
    public int Votes { get => _votes; set => Set(ref _votes, Math.Max(0, value)); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Arguments of <see cref="FeatureVoting.Voted"/>.</summary>
public sealed class FeatureVotingVotedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FeatureVotingVotedEventArgs(RoutedEvent routedEvent, IReadOnlyList<FeatureVotingOption> options)
        : base(routedEvent)
    {
        Options = options;
    }

    /// <summary>The options the viewer voted for, in list order.</summary>
    public IReadOnlyList<FeatureVotingOption> Options { get; }
}
