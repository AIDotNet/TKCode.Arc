using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The content of a <see cref="ShareSheet"/>, the same in a popover and in a bottom sheet: a header, the link with its
/// copy button, a strip of people, a strip of destinations and the action rows. Each strip and the row list is one
/// stop in the tab order; the arrow keys move inside it.
/// </summary>
[TemplatePart(PeoplePart, typeof(Panel))]
[TemplatePart(DestinationsPart, typeof(Panel))]
[TemplatePart(ActionsPart, typeof(Panel))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[PseudoClasses(SheetPseudoClass)]
internal sealed class ShareSheetPanel : TemplatedControl
{
    internal const string PeoplePart = "PART_People";
    internal const string DestinationsPart = "PART_Destinations";
    internal const string ActionsPart = "PART_Actions";
    internal const string CopyPart = "PART_Copy";
    internal const string SheetPseudoClass = ":sheet";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ShareSheetPanel, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ShareSheetPanel, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> LinkProperty =
        AvaloniaProperty.Register<ShareSheetPanel, string?>(nameof(Link));

    public static readonly StyledProperty<string?> PeopleHeaderProperty =
        AvaloniaProperty.Register<ShareSheetPanel, string?>(nameof(PeopleHeader));

    public static readonly StyledProperty<string?> DestinationsHeaderProperty =
        AvaloniaProperty.Register<ShareSheetPanel, string?>(nameof(DestinationsHeader));

    public static readonly StyledProperty<string> CopyLinkLabelProperty =
        AvaloniaProperty.Register<ShareSheetPanel, string>(nameof(CopyLinkLabel), "Copy link");

    public static readonly StyledProperty<string> CopiedLabelProperty =
        AvaloniaProperty.Register<ShareSheetPanel, string>(nameof(CopiedLabel), "Copied");

    public static readonly StyledProperty<bool> HasPeopleProperty =
        AvaloniaProperty.Register<ShareSheetPanel, bool>(nameof(HasPeople));

    public static readonly StyledProperty<bool> HasDestinationsProperty =
        AvaloniaProperty.Register<ShareSheetPanel, bool>(nameof(HasDestinations));

    public static readonly StyledProperty<bool> HasActionsProperty =
        AvaloniaProperty.Register<ShareSheetPanel, bool>(nameof(HasActions));

    private static readonly ArcDesignTokenKind[] s_tints =
        [ArcDesignTokenKind.Series1, ArcDesignTokenKind.Series2, ArcDesignTokenKind.Series3, ArcDesignTokenKind.Series4];

    private readonly List<ShareSheetTile> _people = new();
    private readonly List<ShareSheetTile> _destinations = new();
    private readonly List<ShareSheetActionRow> _actions = new();
    private Panel? _peopleHost;
    private Panel? _destinationsHost;
    private Panel? _actionsHost;
    private CopyButton? _copy;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    public string? Link { get => GetValue(LinkProperty); set => SetValue(LinkProperty, value); }

    public string? PeopleHeader { get => GetValue(PeopleHeaderProperty); set => SetValue(PeopleHeaderProperty, value); }

    public string? DestinationsHeader { get => GetValue(DestinationsHeaderProperty); set => SetValue(DestinationsHeaderProperty, value); }

    public string CopyLinkLabel { get => GetValue(CopyLinkLabelProperty); set => SetValue(CopyLinkLabelProperty, value); }

    public string CopiedLabel { get => GetValue(CopiedLabelProperty); set => SetValue(CopiedLabelProperty, value); }

    public bool HasPeople { get => GetValue(HasPeopleProperty); set => SetValue(HasPeopleProperty, value); }

    public bool HasDestinations { get => GetValue(HasDestinationsProperty); set => SetValue(HasDestinationsProperty, value); }

    public bool HasActions { get => GetValue(HasActionsProperty); set => SetValue(HasActionsProperty, value); }

    /// <summary>Called when a tile is chosen.</summary>
    public Action<ShareSheetTile>? TileInvoked { get; set; }

    /// <summary>Called when an action row is chosen.</summary>
    public Action<ShareSheetActionRow>? RowInvoked { get; set; }

    /// <summary>Called when the link was copied.</summary>
    public Action? Copied { get; set; }

    public IReadOnlyList<ShareSheetTile> People => _people;

    public IReadOnlyList<ShareSheetTile> Destinations => _destinations;

    public IReadOnlyList<ShareSheetActionRow> Actions => _actions;

    /// <summary>Shows the header (popover) or leaves it to the sheet's own title (bottom sheet).</summary>
    public void SetSheet(bool sheet) => PseudoClasses.Set(SheetPseudoClass, sheet);

    /// <summary>Rebuilds the tiles and rows from the owner's lists.</summary>
    public void SetItems(IEnumerable<ShareSheetTarget> people, IEnumerable<ShareSheetTarget> destinations, IEnumerable<ShareSheetAction> actions)
    {
        _people.Clear();
        _destinations.Clear();
        _actions.Clear();
        foreach (var person in people)
        {
            var tile = new ShareSheetTile(person, isPerson: true) { Invoked = OnTileInvoked };
            ArcTokens.Bind(tile, ShareSheetTile.AvatarBrushProperty, s_tints[TintOf(person.Name)]);
            _people.Add(tile);
        }
        foreach (var destination in destinations)
        {
            _destinations.Add(new ShareSheetTile(destination, isPerson: false) { Invoked = OnTileInvoked });
        }
        foreach (var action in actions)
        {
            _actions.Add(new ShareSheetActionRow(action) { Invoked = OnRowInvoked });
        }
        HasPeople = _people.Count > 0;
        HasDestinations = _destinations.Count > 0;
        HasActions = _actions.Count > 0;
        Fill();
    }

    /// <summary>A stable tint for a name, so a person keeps their color from one opening to the next.</summary>
    internal static int TintOf(string? name)
    {
        var hash = 0;
        foreach (var c in name ?? string.Empty)
        {
            hash = unchecked(hash * 31 + c);
        }
        return (int)((uint)hash % (uint)s_tints.Length);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        _peopleHost?.Children.Clear();
        _destinationsHost?.Children.Clear();
        _actionsHost?.Children.Clear();
        _peopleHost = e.NameScope.Find<Panel>(PeoplePart);
        _destinationsHost = e.NameScope.Find<Panel>(DestinationsPart);
        _actionsHost = e.NameScope.Find<Panel>(ActionsPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
        }
        Fill();
    }

    private void Fill()
    {
        Fill(_peopleHost, _people);
        Fill(_destinationsHost, _destinations);
        Fill(_actionsHost, _actions);
    }

    private static void Fill<T>(Panel? host, List<T> items) where T : ShareSheetPressable
    {
        if (host is null)
        {
            return;
        }
        host.Children.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            // One tab stop per group; the arrows move inside it.
            items[i].IsTabStop = i == 0;
            host.Children.Add(items[i]);
        }
    }

    private void OnCopied(object? sender, EventArgs e) => Copied?.Invoke();

    private void OnTileInvoked(ShareSheetPressable tile) => TileInvoked?.Invoke((ShareSheetTile)tile);

    private void OnRowInvoked(ShareSheetPressable row) => RowInvoked?.Invoke((ShareSheetActionRow)row);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not ShareSheetPressable item)
        {
            base.OnKeyDown(e);
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            item.Invoke();
            return;
        }
        var group = GroupOf(item);
        var index = group?.IndexOf(item) ?? -1;
        if (group is null || index < 0)
        {
            base.OnKeyDown(e);
            return;
        }
        var horizontal = item is ShareSheetTile;
        var target = e.Key switch
        {
            Key.Right when horizontal => Math.Min(index + 1, group.Count - 1),
            Key.Left when horizontal => Math.Max(index - 1, 0),
            Key.Down when !horizontal => Math.Min(index + 1, group.Count - 1),
            Key.Up when !horizontal => Math.Max(index - 1, 0),
            Key.Home => 0,
            Key.End => group.Count - 1,
            _ => -1
        };
        if (target < 0)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        for (var i = 0; i < group.Count; i++)
        {
            group[i].IsTabStop = i == target;
        }
        group[target].Focus(NavigationMethod.Directional);
    }

    private List<ShareSheetPressable>? GroupOf(ShareSheetPressable item)
    {
        if (item is ShareSheetTile tile)
        {
            return (tile.IsPerson ? _people : _destinations).Cast<ShareSheetPressable>().ToList();
        }
        return item is ShareSheetActionRow ? _actions.Cast<ShareSheetPressable>().ToList() : null;
    }

    /// <summary>The first element to focus when the panel opens: the copy button, else the first tile or row.</summary>
    public Control? FirstFocusTarget()
    {
        if (_copy is { IsEffectivelyVisible: true })
        {
            return _copy;
        }
        return _people.FirstOrDefault() as Control ?? _destinations.FirstOrDefault() as Control ?? _actions.FirstOrDefault();
    }
}
