using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A browsable gallery of ready-made agents: a heading with a search field, category pills, a count with a sort switch,
/// and a responsive grid of cards (mark, name, maker, one-line pitch, rating, installs, tags and an install button).
/// A card opens a detail view in place of the grid with the description, what the agent can do, what it needs access
/// to and prompts to try, where it can be installed or removed. Without data it shows an invented sample catalog.
/// <para>
/// Installing goes through <see cref="InstallAsync"/> (a simulated 1.2 second install when it is null): the button
/// shows progress, then the agent's id joins the two-way <see cref="InstalledIds"/> and <see cref="AgentInstalled"/>
/// is raised; a failure shows its message with a retry and raises <see cref="InstallFailed"/>.
/// </para>
/// </summary>
/// <example><code>
/// &lt;arc:AgentGallery Agents="{Binding Catalog}" Categories="{Binding Categories}"
///                   InstalledIds="{Binding InstalledAgentIds}" /&gt;
/// </code></example>
[TemplatePart(HostPart, typeof(Panel))]
[TemplatePart(SearchPart, typeof(SearchField))]
[TemplatePart(CategoriesPart, typeof(PillFilterBar))]
[TemplatePart(SortPart, typeof(SegmentedControl))]
[TemplatePart(GridPart, typeof(AgentGalleryGrid))]
[TemplatePart(BackPart, typeof(Button))]
[TemplatePart(DetailInstallPart, typeof(Button))]
[TemplatePart(UninstallPart, typeof(Button))]
[PseudoClasses(CompactPseudoClass, DetailPseudoClass, EmptyPseudoClass)]
public partial class AgentGallery : TemplatedControl
{
    public const string HostPart = "PART_Host";
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string SearchPart = "PART_Search";
    public const string BrowsePart = "PART_Browse";
    public const string CategoriesPart = "PART_Categories";
    public const string CountPart = "PART_Count";
    public const string SortPart = "PART_Sort";
    public const string GridPart = "PART_Grid";
    public const string EmptyPart = "PART_Empty";
    public const string EmptyTitlePart = "PART_EmptyTitle";
    public const string EmptyHintPart = "PART_EmptyHint";
    public const string ClearSearchPart = "PART_ClearSearch";
    public const string ShowAllPart = "PART_ShowAll";
    public const string DetailPart = "PART_Detail";
    public const string BackPart = "PART_Back";
    public const string DetailMarkPart = "PART_DetailMark";
    public const string DetailNamePart = "PART_DetailName";
    public const string DetailMakerPart = "PART_DetailMaker";
    public const string DetailVerifiedPart = "PART_DetailVerified";
    public const string DetailStatsPart = "PART_DetailStats";
    public const string DetailInstallPart = "PART_DetailInstall";
    public const string DetailInstalledPart = "PART_DetailInstalled";
    public const string UninstallPart = "PART_Uninstall";
    public const string DetailErrorPart = "PART_DetailError";
    public const string DetailErrorTextPart = "PART_DetailErrorText";
    public const string DescriptionPart = "PART_Description";
    public const string AbilitiesHeadingPart = "PART_AbilitiesHeading";
    public const string AbilitiesPart = "PART_Abilities";
    public const string PermissionsHeadingPart = "PART_PermissionsHeading";
    public const string PermissionsPart = "PART_Permissions";
    public const string PromptsHeadingPart = "PART_PromptsHeading";
    public const string PromptsPart = "PART_Prompts";

    /// <summary>Set while the gallery is narrower than its compact width (600): the search moves under the title and the paddings tighten.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the detail view of an agent is shown in place of the grid.</summary>
    public const string DetailPseudoClass = ":detail";

    /// <summary>Set while no card matches the category and the search.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>
    /// The agents. A list that reports its changes (an <c>ObservableCollection</c>) is followed. Default: fourteen
    /// invented sample agents (<see cref="AgentGallerySamples.Agents"/>).
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<AgentGalleryItem>> AgentsProperty =
        AvaloniaProperty.Register<AgentGallery, IReadOnlyList<AgentGalleryItem>>(nameof(Agents), AgentGallerySamples.Agents());

    /// <summary>The categories, in pill order after "All". Default: the six sample categories.</summary>
    public static readonly StyledProperty<IReadOnlyList<AgentGalleryCategory>> CategoriesProperty =
        AvaloniaProperty.Register<AgentGallery, IReadOnlyList<AgentGalleryCategory>>(nameof(Categories), AgentGallerySamples.Categories());

    /// <summary>The ids of the installed agents (two-way: an install adds its id, an uninstall removes it).</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> InstalledIdsProperty =
        AvaloniaProperty.Register<AgentGallery, IReadOnlyList<string>>(nameof(InstalledIds), Array.Empty<string>(),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the category shown (two-way); null shows every category.</summary>
    public static readonly StyledProperty<string?> SelectedCategoryKeyProperty =
        AvaloniaProperty.Register<AgentGallery, string?>(nameof(SelectedCategoryKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search text (two-way). It matches names, makers, pitches, tags and category names, ignoring case.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<AgentGallery, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The order of the cards (two-way; Popular by default).</summary>
    public static readonly StyledProperty<AgentGallerySort> SortProperty =
        AvaloniaProperty.Register<AgentGallery, AgentGallerySort>(nameof(Sort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the agent whose detail view is shown (two-way); null shows the grid.</summary>
    public static readonly StyledProperty<string?> OpenAgentIdProperty =
        AvaloniaProperty.Register<AgentGallery, string?>(nameof(OpenAgentId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The heading; null uses <see cref="AgentGalleryLabels.Title"/>.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentGallery, string?>(nameof(Title));

    /// <summary>The line under the heading; null uses <see cref="AgentGalleryLabels.Subtitle"/>, empty hides it.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AgentGallery, string?>(nameof(Subtitle));

    /// <summary>Culture of every count and date (default "en-US").</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<AgentGallery, string>(nameof(CultureName), "en-US");

    /// <summary>The built-in text; replace the object to reword or translate the gallery.</summary>
    public static readonly StyledProperty<AgentGalleryLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentGallery, AgentGalleryLabels>(nameof(Labels), AgentGalleryLabels.Default);

    /// <summary>How many agents the category and the search leave on screen.</summary>
    public static readonly DirectProperty<AgentGallery, int> ResultCountProperty =
        AvaloniaProperty.RegisterDirect<AgentGallery, int>(nameof(ResultCount), o => o.ResultCount);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentGallery, double>(nameof(CompactWidth), 600);

    internal static readonly StyledProperty<double> ResultsFadeFromProperty =
        AvaloniaProperty.Register<AgentGallery, double>(nameof(ResultsFadeFrom), 0.25);

    /// <summary>Raised when the detail view of an agent opens (a card, the keyboard or <see cref="Open"/>).</summary>
    public static readonly RoutedEvent<AgentGalleryAgentEventArgs> AgentOpenedEvent =
        RoutedEvent.Register<AgentGallery, AgentGalleryAgentEventArgs>(nameof(AgentOpened), RoutingStrategies.Bubble);

    /// <summary>Raised after an install finished and the id joined <see cref="InstalledIds"/>.</summary>
    public static readonly RoutedEvent<AgentGalleryAgentEventArgs> AgentInstalledEvent =
        RoutedEvent.Register<AgentGallery, AgentGalleryAgentEventArgs>(nameof(AgentInstalled), RoutingStrategies.Bubble);

    /// <summary>Raised after an agent was removed and its id left <see cref="InstalledIds"/>.</summary>
    public static readonly RoutedEvent<AgentGalleryAgentEventArgs> AgentUninstalledEvent =
        RoutedEvent.Register<AgentGallery, AgentGalleryAgentEventArgs>(nameof(AgentUninstalled), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="InstallAsync"/> threw, with the message shown.</summary>
    public static readonly RoutedEvent<AgentGalleryInstallFailedEventArgs> InstallFailedEvent =
        RoutedEvent.Register<AgentGallery, AgentGalleryInstallFailedEventArgs>(nameof(InstallFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when a sample prompt in the detail view is pressed (start a chat with the agent and the prompt).</summary>
    public static readonly RoutedEvent<AgentGalleryPromptEventArgs> PromptChosenEvent =
        RoutedEvent.Register<AgentGallery, AgentGalleryPromptEventArgs>(nameof(PromptChosen), RoutingStrategies.Bubble);

    /// <inheritdoc cref="AgentsProperty"/>
    public IReadOnlyList<AgentGalleryItem> Agents { get => GetValue(AgentsProperty); set => SetValue(AgentsProperty, value); }

    /// <inheritdoc cref="CategoriesProperty"/>
    public IReadOnlyList<AgentGalleryCategory> Categories { get => GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }

    /// <inheritdoc cref="InstalledIdsProperty"/>
    public IReadOnlyList<string> InstalledIds { get => GetValue(InstalledIdsProperty); set => SetValue(InstalledIdsProperty, value); }

    /// <inheritdoc cref="SelectedCategoryKeyProperty"/>
    public string? SelectedCategoryKey { get => GetValue(SelectedCategoryKeyProperty); set => SetValue(SelectedCategoryKeyProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="SortProperty"/>
    public AgentGallerySort Sort { get => GetValue(SortProperty); set => SetValue(SortProperty, value); }

    /// <inheritdoc cref="OpenAgentIdProperty"/>
    public string? OpenAgentId { get => GetValue(OpenAgentIdProperty); set => SetValue(OpenAgentIdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentGalleryLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ResultCountProperty"/>
    public int ResultCount { get => _resultCount; private set => SetAndRaise(ResultCountProperty, ref _resultCount, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double ResultsFadeFrom { get => GetValue(ResultsFadeFromProperty); set => SetValue(ResultsFadeFromProperty, value); }

    /// <summary>
    /// Installs an agent through your backend: resolve once it is installed; throw to show the exception's message with a
    /// retry. The token is cancelled when the gallery leaves the visual tree. Null (the default) simulates a 1.2 second
    /// install.
    /// </summary>
    public Func<AgentGalleryItem, CancellationToken, Task>? InstallAsync { get; set; }

    /// <summary>
    /// Removes an agent through your backend: resolve once it is removed; throw to keep it installed and show the message.
    /// Null (the default) removes it at once.
    /// </summary>
    public Func<AgentGalleryItem, CancellationToken, Task>? UninstallAsync { get; set; }

    /// <summary>The waits of the gallery (the simulated install); tests replace it to step time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (time, token) => Task.Delay(time, token);

    /// <inheritdoc cref="AgentOpenedEvent"/>
    public event EventHandler<AgentGalleryAgentEventArgs>? AgentOpened
    {
        add => AddHandler(AgentOpenedEvent, value);
        remove => RemoveHandler(AgentOpenedEvent, value);
    }

    /// <inheritdoc cref="AgentInstalledEvent"/>
    public event EventHandler<AgentGalleryAgentEventArgs>? AgentInstalled
    {
        add => AddHandler(AgentInstalledEvent, value);
        remove => RemoveHandler(AgentInstalledEvent, value);
    }

    /// <inheritdoc cref="AgentUninstalledEvent"/>
    public event EventHandler<AgentGalleryAgentEventArgs>? AgentUninstalled
    {
        add => AddHandler(AgentUninstalledEvent, value);
        remove => RemoveHandler(AgentUninstalledEvent, value);
    }

    /// <inheritdoc cref="InstallFailedEvent"/>
    public event EventHandler<AgentGalleryInstallFailedEventArgs>? InstallFailed
    {
        add => AddHandler(InstallFailedEvent, value);
        remove => RemoveHandler(InstallFailedEvent, value);
    }

    /// <inheritdoc cref="PromptChosenEvent"/>
    public event EventHandler<AgentGalleryPromptEventArgs>? PromptChosen
    {
        add => AddHandler(PromptChosenEvent, value);
        remove => RemoveHandler(PromptChosenEvent, value);
    }

    #endregion

    private int _resultCount;

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }

    /// <summary>Creates a gallery showing the sample catalog.</summary>
    public AgentGallery()
    {
        ObserveAgents(null, Agents);
        Refresh(announce: false);
    }
}
