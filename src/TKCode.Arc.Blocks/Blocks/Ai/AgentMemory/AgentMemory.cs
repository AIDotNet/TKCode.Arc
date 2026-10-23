using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A card that shows what an assistant remembers about the person: a capacity meter, two switches (use saved memories,
/// use chat history), filter pills by group, a search, and the memories themselves, which can be edited in place or
/// forgotten with a few seconds to undo. A memory the assistant has just noted shows as a pill that files it into its
/// group, and a field at the bottom adds one by hand and sorts it into a group.
/// <code>
/// &lt;arc:AgentMemory AssistantName="Orbit" Capacity="2000"&gt;
///     &lt;arc:AgentMemory.Memories&gt;
///         &lt;arc:MemoryItem Id="1" GroupKey="preferences" Text="Prefers tables to long bullet lists" /&gt;
///     &lt;/arc:AgentMemory.Memories&gt;
/// &lt;/arc:AgentMemory&gt;
/// </code>
/// Changes are applied to <see cref="Memories"/> and reported through <see cref="MemoryChanged"/>; set
/// <see cref="CommitAsync"/> to save them somewhere first (a failure shows in place and rolls the change back).
/// </summary>
public partial class AgentMemory : TemplatedControl
{
    #region Properties

    /// <summary>The memories to show and change, when you give them as a list to follow; it wins over <see cref="Memories"/> when set.</summary>
    public static readonly StyledProperty<IList<MemoryItem>?> MemoriesSourceProperty =
        AvaloniaProperty.Register<AgentMemory, IList<MemoryItem>?>(nameof(MemoriesSource));

    /// <summary>The memories the assistant has just noted, when you give them as a list to follow; it wins over <see cref="Captured"/> when set.</summary>
    public static readonly StyledProperty<IList<CapturedMemory>?> CapturedSourceProperty =
        AvaloniaProperty.Register<AgentMemory, IList<CapturedMemory>?>(nameof(CapturedSource));

    /// <summary>Whether the first switch ("Reference saved memories") is on (two-way). Off dims the list and pauses the captured pill.</summary>
    public static readonly StyledProperty<bool> IsSavedMemoriesEnabledProperty =
        AvaloniaProperty.Register<AgentMemory, bool>(nameof(IsSavedMemoriesEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the second switch ("Reference chat history") is on (two-way).</summary>
    public static readonly StyledProperty<bool> IsChatHistoryEnabledProperty =
        AvaloniaProperty.Register<AgentMemory, bool>(nameof(IsChatHistoryEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The capacity in characters of all the memories together; the meter shows the share used. Zero or less hides the meter and the limit.</summary>
    public static readonly StyledProperty<int> CapacityProperty =
        AvaloniaProperty.Register<AgentMemory, int>(nameof(Capacity), 2000);

    /// <summary>From this share of <see cref="Capacity"/> the meter turns to the warning color.</summary>
    public static readonly StyledProperty<double> WarningRatioProperty =
        AvaloniaProperty.Register<AgentMemory, double>(nameof(WarningRatio), 0.9);

    /// <summary>The longest a single memory may be, for editing and adding alike.</summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<AgentMemory, int>(nameof(MaxLength), 200);

    /// <summary>How long a forgotten memory can be brought back before it is really forgotten.</summary>
    public static readonly StyledProperty<TimeSpan> UndoDurationProperty =
        AvaloniaProperty.Register<AgentMemory, TimeSpan>(nameof(UndoDuration), TimeSpan.FromSeconds(5));

    /// <summary>The assistant's name, used in the subtitle and the switches' explanations.</summary>
    public static readonly StyledProperty<string> AssistantNameProperty =
        AvaloniaProperty.Register<AgentMemory, string>(nameof(AssistantName), "Assistant");

    /// <summary>The title; null shows <see cref="AgentMemoryLabels.Title"/>.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentMemory, string?>(nameof(Title));

    /// <summary>The line under the title; null shows <see cref="AgentMemoryLabels.Subtitle"/> with the assistant's name.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AgentMemory, string?>(nameof(Subtitle));

    /// <summary>The key of the group the list is filtered to (two-way); null shows all groups.</summary>
    public static readonly StyledProperty<string?> SelectedGroupKeyProperty =
        AvaloniaProperty.Register<AgentMemory, string?>(nameof(SelectedGroupKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The text of the search (two-way). Searching covers all groups whatever the filter is.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<AgentMemory, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the search box is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsSearchOpenProperty =
        AvaloniaProperty.Register<AgentMemory, bool>(nameof(IsSearchOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the field to add a memory by hand is shown.</summary>
    public static readonly StyledProperty<bool> IsAddEnabledProperty =
        AvaloniaProperty.Register<AgentMemory, bool>(nameof(IsAddEnabled), true);

    /// <summary>The list scrolls inside the card above this height.</summary>
    public static readonly StyledProperty<double> ListMaxHeightProperty =
        AvaloniaProperty.Register<AgentMemory, double>(nameof(ListMaxHeight), 380);

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<AgentMemoryLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentMemory, AgentMemoryLabels>(nameof(Labels), new AgentMemoryLabels());

    /// <summary>Run when the person presses a source with a link, with the <see cref="MemoryItem"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> SourceCommandProperty =
        AvaloniaProperty.Register<AgentMemory, ICommand?>(nameof(SourceCommand));

    #endregion

    #region Events

    /// <summary>Raised after a change is committed.</summary>
    public static readonly RoutedEvent<MemoryChangedEventArgs> MemoryChangedEvent =
        RoutedEvent.Register<AgentMemory, MemoryChangedEventArgs>(nameof(MemoryChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when one of the two switches changes.</summary>
    public static readonly RoutedEvent<MemorySettingToggledEventArgs> SettingToggledEvent =
        RoutedEvent.Register<AgentMemory, MemorySettingToggledEventArgs>(nameof(SettingToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses the source of a memory that has a <see cref="MemoryItem.SourceUri"/>.</summary>
    public static readonly RoutedEvent<MemorySourceRequestedEventArgs> SourceRequestedEvent =
        RoutedEvent.Register<AgentMemory, MemorySourceRequestedEventArgs>(nameof(SourceRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after a change is committed.</summary>
    public event EventHandler<MemoryChangedEventArgs>? MemoryChanged
    {
        add => AddHandler(MemoryChangedEvent, value);
        remove => RemoveHandler(MemoryChangedEvent, value);
    }

    /// <summary>Raised when one of the two switches changes.</summary>
    public event EventHandler<MemorySettingToggledEventArgs>? SettingToggled
    {
        add => AddHandler(SettingToggledEvent, value);
        remove => RemoveHandler(SettingToggledEvent, value);
    }

    /// <summary>Raised when the person presses the source of a memory that has a link.</summary>
    public event EventHandler<MemorySourceRequestedEventArgs>? SourceRequested
    {
        add => AddHandler(SourceRequestedEvent, value);
        remove => RemoveHandler(SourceRequestedEvent, value);
    }

    #endregion

    /// <summary>The memories, when no <see cref="MemoriesSource"/> is set. Changes are made on this list.</summary>
    public AvaloniaList<MemoryItem> Memories { get; } = new();

    /// <summary>The groups, in filter order. Empty means the four default groups with their <see cref="AgentMemoryLabels"/> texts.</summary>
    public AvaloniaList<MemoryGroup> Groups { get; } = new();

    /// <summary>The memories just noted by the assistant, when no <see cref="CapturedSource"/> is set. The first one not yet filed shows as the captured pill.</summary>
    public AvaloniaList<CapturedMemory> Captured { get; } = new();

    /// <summary>
    /// Saves a change before it takes effect. Capture, add and edit show a saving state while the task runs; an exception
    /// shows its message in place and the change is rolled back, a cancellation (the card left the window) rolls back
    /// silently. For an edit the change carries a copy of the memory with the new text: the memory itself changes only
    /// after the task succeeded. Forget is committed only once its undo window has ended; if it fails the memory comes
    /// back. Null commits at once (an edit still shows a short saving state).
    /// </summary>
    public Func<MemoryChange, CancellationToken, Task>? CommitAsync { get; set; }

    /// <summary>
    /// Returns the key of the group a typed memory belongs to. Null uses a built-in guess from keywords; the add field says
    /// which. An exception shows an error in the add field and keeps the text.
    /// </summary>
    public Func<string, CancellationToken, ValueTask<string>>? ClassifyAsync { get; set; }

    /// <inheritdoc cref="MemoriesSourceProperty"/>
    public IList<MemoryItem>? MemoriesSource
    {
        get => GetValue(MemoriesSourceProperty);
        set => SetValue(MemoriesSourceProperty, value);
    }

    /// <inheritdoc cref="CapturedSourceProperty"/>
    public IList<CapturedMemory>? CapturedSource
    {
        get => GetValue(CapturedSourceProperty);
        set => SetValue(CapturedSourceProperty, value);
    }

    /// <inheritdoc cref="IsSavedMemoriesEnabledProperty"/>
    public bool IsSavedMemoriesEnabled
    {
        get => GetValue(IsSavedMemoriesEnabledProperty);
        set => SetValue(IsSavedMemoriesEnabledProperty, value);
    }

    /// <inheritdoc cref="IsChatHistoryEnabledProperty"/>
    public bool IsChatHistoryEnabled
    {
        get => GetValue(IsChatHistoryEnabledProperty);
        set => SetValue(IsChatHistoryEnabledProperty, value);
    }

    /// <inheritdoc cref="CapacityProperty"/>
    public int Capacity
    {
        get => GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    /// <inheritdoc cref="WarningRatioProperty"/>
    public double WarningRatio
    {
        get => GetValue(WarningRatioProperty);
        set => SetValue(WarningRatioProperty, value);
    }

    /// <inheritdoc cref="MaxLengthProperty"/>
    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <inheritdoc cref="UndoDurationProperty"/>
    public TimeSpan UndoDuration
    {
        get => GetValue(UndoDurationProperty);
        set => SetValue(UndoDurationProperty, value);
    }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string AssistantName
    {
        get => GetValue(AssistantNameProperty);
        set => SetValue(AssistantNameProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <inheritdoc cref="SelectedGroupKeyProperty"/>
    public string? SelectedGroupKey
    {
        get => GetValue(SelectedGroupKeyProperty);
        set => SetValue(SelectedGroupKeyProperty, value);
    }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <inheritdoc cref="IsSearchOpenProperty"/>
    public bool IsSearchOpen
    {
        get => GetValue(IsSearchOpenProperty);
        set => SetValue(IsSearchOpenProperty, value);
    }

    /// <inheritdoc cref="IsAddEnabledProperty"/>
    public bool IsAddEnabled
    {
        get => GetValue(IsAddEnabledProperty);
        set => SetValue(IsAddEnabledProperty, value);
    }

    /// <inheritdoc cref="ListMaxHeightProperty"/>
    public double ListMaxHeight
    {
        get => GetValue(ListMaxHeightProperty);
        set => SetValue(ListMaxHeightProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentMemoryLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="SourceCommandProperty"/>
    public ICommand? SourceCommand
    {
        get => GetValue(SourceCommandProperty);
        set => SetValue(SourceCommandProperty, value);
    }
}
