using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A company directory: a searchable list of people with team filters on one side and the profile of the person picked
/// on the other (bio, status, team, current work, location and an email address to copy). Search matches the name,
/// role, team and city as you type; the team buttons show fixed totals; the selection is kept by
/// <see cref="TeamDirectoryPerson.Id"/>, so when the picked person is filtered out the first match shows and the picked
/// person comes back when they match again. Rows that stay glide to their new places, rows that leave fade where they
/// were, and the profile cross-fades. Narrow blocks stack the profile under the list.
/// <code>
/// &lt;arc:TeamDirectory PersonSelected="OnPersonSelected"&gt;
///     &lt;arc:TeamDirectoryPerson Id="lena" Name="Lena Hartmann" Role="Interaction designer" Team="Design"
///                              City="Vienna" TimeZone="CET" Email="lena@northwind.test" /&gt;
/// &lt;/arc:TeamDirectory&gt;
/// </code>
/// </summary>
public partial class TeamDirectory : TemplatedControl
{
    #region Properties

    /// <summary>The people, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="People"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> PeopleSourceProperty =
        AvaloniaProperty.Register<TeamDirectory, IEnumerable?>(nameof(PeopleSource));

    /// <summary>The team shown (two-way); null shows every team. The search is kept when it changes.</summary>
    public static readonly StyledProperty<string?> SelectedTeamProperty =
        AvaloniaProperty.Register<TeamDirectory, string?>(nameof(SelectedTeam), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search text (two-way), applied on every keystroke; the team filter is kept when it changes.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<TeamDirectory, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The <see cref="TeamDirectoryPerson.Key"/> of the person picked (two-way); only a pick or code changes it. Null, or a
    /// person not among the matches, shows the first match (see <see cref="DisplayedPerson"/>) without changing it.
    /// </summary>
    public static readonly StyledProperty<string?> SelectedPersonIdProperty =
        AvaloniaProperty.Register<TeamDirectory, string?>(nameof(SelectedPersonId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Decides whether a person matches the search text (the untrimmed text as typed); null matches the trimmed text,
    /// ignoring case, as one piece of the name, role, team or city.
    /// </summary>
    public static readonly StyledProperty<Func<TeamDirectoryPerson, string, bool>?> SearchFilterProperty =
        AvaloniaProperty.Register<TeamDirectory, Func<TeamDirectoryPerson, string, bool>?>(nameof(SearchFilter));

    /// <summary>
    /// Copies an email address: awaited when the copy button is pressed, with the address and a token that is cancelled
    /// when the block leaves the window. An exception shows Retry; null copies to the system clipboard.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task>?> CopyAsyncProperty =
        AvaloniaProperty.Register<TeamDirectory, Func<string, CancellationToken, Task>?>(nameof(CopyAsync));

    /// <summary>How long the copy button shows Copied or Retry before it rests again.</summary>
    public static readonly StyledProperty<TimeSpan> CopyResetDelayProperty =
        AvaloniaProperty.Register<TeamDirectory, TimeSpan>(nameof(CopyResetDelay), TimeSpan.FromMilliseconds(1800));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<TeamDirectoryLabels> LabelsProperty =
        AvaloniaProperty.Register<TeamDirectory, TeamDirectoryLabels>(nameof(Labels), new TeamDirectoryLabels());

    /// <summary>Whether the header shows the search box.</summary>
    public static readonly StyledProperty<bool> IsSearchVisibleProperty =
        AvaloniaProperty.Register<TeamDirectory, bool>(nameof(IsSearchVisible), true);

    /// <summary>Whether the team buttons show under the header.</summary>
    public static readonly StyledProperty<bool> IsFilterVisibleProperty =
        AvaloniaProperty.Register<TeamDirectory, bool>(nameof(IsFilterVisible), true);

    /// <summary>Draws a bordered card with rounded corners (true); false lays the block flat at the container's width.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<TeamDirectory, bool>(nameof(IsFramed), true);

    /// <summary>Run when the person picks someone from the list, with the <see cref="TeamDirectoryPerson"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> PersonSelectedCommandProperty =
        AvaloniaProperty.Register<TeamDirectory, ICommand?>(nameof(PersonSelectedCommand));

    /// <summary>The person whose profile shows: the picked person while they match, otherwise the first match, otherwise null.</summary>
    public static readonly DirectProperty<TeamDirectory, TeamDirectoryPerson?> DisplayedPersonProperty =
        AvaloniaProperty.RegisterDirect<TeamDirectory, TeamDirectoryPerson?>(nameof(DisplayedPerson), o => o.DisplayedPerson);

    /// <summary>How many people match the team filter and the search.</summary>
    public static readonly DirectProperty<TeamDirectory, int> MatchCountProperty =
        AvaloniaProperty.RegisterDirect<TeamDirectory, int>(nameof(MatchCount), o => o.MatchCount);

    #endregion

    #region Events

    /// <summary>
    /// Raised when the person picks someone from the list (click, Enter or Space) and the pick changes. Not raised when the
    /// shown person changes because of the filter or the search, nor for <see cref="SelectPerson(string?)"/>.
    /// </summary>
    public static readonly RoutedEvent<TeamDirectoryPersonSelectedEventArgs> PersonSelectedEvent =
        RoutedEvent.Register<TeamDirectory, TeamDirectoryPersonSelectedEventArgs>(nameof(PersonSelected), RoutingStrategies.Bubble);

    /// <summary>Raised after the copy button tried to copy an email address, whether it worked or not.</summary>
    public static readonly RoutedEvent<TeamDirectoryEmailCopiedEventArgs> EmailCopiedEvent =
        RoutedEvent.Register<TeamDirectory, TeamDirectoryEmailCopiedEventArgs>(nameof(EmailCopied), RoutingStrategies.Bubble);

    /// <summary>Raised when the profile's email address is pressed; unless a handler sets Handled, a mailto: link opens.</summary>
    public static readonly RoutedEvent<TeamDirectoryPersonEventArgs> EmailLinkClickedEvent =
        RoutedEvent.Register<TeamDirectory, TeamDirectoryPersonEventArgs>(nameof(EmailLinkClicked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PersonSelectedEvent"/>
    public event EventHandler<TeamDirectoryPersonSelectedEventArgs>? PersonSelected
    {
        add => AddHandler(PersonSelectedEvent, value);
        remove => RemoveHandler(PersonSelectedEvent, value);
    }

    /// <inheritdoc cref="EmailCopiedEvent"/>
    public event EventHandler<TeamDirectoryEmailCopiedEventArgs>? EmailCopied
    {
        add => AddHandler(EmailCopiedEvent, value);
        remove => RemoveHandler(EmailCopiedEvent, value);
    }

    /// <inheritdoc cref="EmailLinkClickedEvent"/>
    public event EventHandler<TeamDirectoryPersonEventArgs>? EmailLinkClicked
    {
        add => AddHandler(EmailLinkClickedEvent, value);
        remove => RemoveHandler(EmailLinkClickedEvent, value);
    }

    #endregion

    private TeamDirectoryPerson? _displayedPerson;
    private int _matchCount;

    /// <summary>The people, in order, when no <see cref="PeopleSource"/> is set.</summary>
    [Content]
    public AvaloniaList<TeamDirectoryPerson> People { get; } = new();

    /// <summary>The teams of the filter, in order; while empty, every team found in the people, in order of first appearance. "All" always leads.</summary>
    public AvaloniaList<string> Teams { get; } = new();

    /// <inheritdoc cref="PeopleSourceProperty"/>
    public IEnumerable? PeopleSource { get => GetValue(PeopleSourceProperty); set => SetValue(PeopleSourceProperty, value); }

    /// <inheritdoc cref="SelectedTeamProperty"/>
    public string? SelectedTeam { get => GetValue(SelectedTeamProperty); set => SetValue(SelectedTeamProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="SelectedPersonIdProperty"/>
    public string? SelectedPersonId { get => GetValue(SelectedPersonIdProperty); set => SetValue(SelectedPersonIdProperty, value); }

    /// <inheritdoc cref="SearchFilterProperty"/>
    public Func<TeamDirectoryPerson, string, bool>? SearchFilter
    {
        get => GetValue(SearchFilterProperty);
        set => SetValue(SearchFilterProperty, value);
    }

    /// <inheritdoc cref="CopyAsyncProperty"/>
    public Func<string, CancellationToken, Task>? CopyAsync { get => GetValue(CopyAsyncProperty); set => SetValue(CopyAsyncProperty, value); }

    /// <inheritdoc cref="CopyResetDelayProperty"/>
    public TimeSpan CopyResetDelay { get => GetValue(CopyResetDelayProperty); set => SetValue(CopyResetDelayProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TeamDirectoryLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsSearchVisibleProperty"/>
    public bool IsSearchVisible { get => GetValue(IsSearchVisibleProperty); set => SetValue(IsSearchVisibleProperty, value); }

    /// <inheritdoc cref="IsFilterVisibleProperty"/>
    public bool IsFilterVisible { get => GetValue(IsFilterVisibleProperty); set => SetValue(IsFilterVisibleProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="PersonSelectedCommandProperty"/>
    public ICommand? PersonSelectedCommand
    {
        get => GetValue(PersonSelectedCommandProperty);
        set => SetValue(PersonSelectedCommandProperty, value);
    }

    /// <inheritdoc cref="DisplayedPersonProperty"/>
    public TeamDirectoryPerson? DisplayedPerson
    {
        get => _displayedPerson;
        private set => SetAndRaise(DisplayedPersonProperty, ref _displayedPerson, value);
    }

    /// <inheritdoc cref="MatchCountProperty"/>
    public int MatchCount { get => _matchCount; private set => SetAndRaise(MatchCountProperty, ref _matchCount, value); }
}

/// <summary>Arguments of an event about one person (<see cref="TeamDirectory.EmailLinkClicked"/>).</summary>
public sealed class TeamDirectoryPersonEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="person">The person.</param>
    public TeamDirectoryPersonEventArgs(RoutedEvent routedEvent, object source, TeamDirectoryPerson person)
        : base(routedEvent, source)
    {
        Person = person;
    }

    /// <summary>The person.</summary>
    public TeamDirectoryPerson Person { get; }
}
