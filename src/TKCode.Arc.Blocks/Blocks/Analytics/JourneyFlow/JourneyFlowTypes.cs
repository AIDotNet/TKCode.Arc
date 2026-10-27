using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>What a <see cref="JourneyNode"/> stands for, which sets its colour and the colour of the flows into it.</summary>
public enum JourneyNodeKind
{
    /// <summary>A step on the main path: AccentStrong bar, flows into it in Accent at 10%.</summary>
    Step,

    /// <summary>People leaving the journey: TextMuted bar, flows into it in Accent at 4%; listed as a drop-off.</summary>
    Exit,

    /// <summary>The outcome you want: Success bar, flows into it in Success at 16%; counted as the goal.</summary>
    Goal
}

/// <summary>
/// One stage (column) of a <see cref="JourneyFlow"/>, left to right: its name under the chart and the words the tooltip
/// uses for the share of the stage.
/// </summary>
public class JourneyStage : AvaloniaObject
{
    /// <summary>Identifies the stage (<see cref="JourneyNode.StageKey"/>); defaults to the header.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<JourneyStage, string?>(nameof(Key));

    /// <summary>The stage name under the chart ("Trial").</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<JourneyStage, string?>(nameof(Header));

    /// <summary>A short name for narrow charts ("Wk 2"); null keeps <see cref="Header"/>.</summary>
    public static readonly StyledProperty<string?> ShortHeaderProperty =
        AvaloniaProperty.Register<JourneyStage, string?>(nameof(ShortHeader));

    /// <summary>
    /// What a node's share of this stage is a share of, in the tooltip's "Share of …" row ("trial starts"). Null uses the
    /// nearest earlier node whose count equals the stage's total (the step everyone in this stage came through).
    /// </summary>
    public static readonly StyledProperty<string?> ShareBaseLabelProperty =
        AvaloniaProperty.Register<JourneyStage, string?>(nameof(ShareBaseLabel));

    /// <summary>Creates an empty stage.</summary>
    public JourneyStage()
    {
    }

    /// <summary>Creates a stage with a key and a header.</summary>
    public JourneyStage(string key, string header, string? shortHeader = null)
    {
        Key = key;
        Header = header;
        ShortHeader = shortHeader;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="ShortHeaderProperty"/>
    public string? ShortHeader { get => GetValue(ShortHeaderProperty); set => SetValue(ShortHeaderProperty, value); }

    /// <inheritdoc cref="ShareBaseLabelProperty"/>
    public string? ShareBaseLabel { get => GetValue(ShareBaseLabelProperty); set => SetValue(ShareBaseLabelProperty, value); }

    /// <summary>The key used by nodes: <see cref="Key"/>, or the header when no key is set.</summary>
    public string? EffectiveKey => Key ?? Header;

    /// <summary>Raised when any property changes.</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One node of a <see cref="JourneyFlow"/>: a group of people at one stage (signed up, left, subscribed). Nodes stack in
/// their stage in the order of <see cref="JourneyFlow.Nodes"/>; put steps first, exits after them and the goal first in
/// the last stage.
/// </summary>
public class JourneyNode : AvaloniaObject
{
    /// <summary>Identifies the node (links, <see cref="JourneyFlow.SelectNode"/>); defaults to the header.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<JourneyNode, string?>(nameof(Key));

    /// <summary>The <see cref="JourneyStage.Key"/> of the stage the node is in.</summary>
    public static readonly StyledProperty<string?> StageKeyProperty =
        AvaloniaProperty.Register<JourneyNode, string?>(nameof(StageKey));

    /// <summary>The node's name on the chart, in the tooltip and in the side panel ("Started trial").</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<JourneyNode, string?>(nameof(Header));

    /// <summary>One sentence on who is in the node, shown under its name in the side panel when it is selected.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<JourneyNode, string?>(nameof(Description));

    /// <summary>Step, exit or goal. Default <see cref="JourneyNodeKind.Step"/>.</summary>
    public static readonly StyledProperty<JourneyNodeKind> KindProperty =
        AvaloniaProperty.Register<JourneyNode, JourneyNodeKind>(nameof(Kind));

    /// <summary>How the overview's drop-off list names this exit ("Left before the trial"); null uses the header.</summary>
    public static readonly StyledProperty<string?> DropOffLabelProperty =
        AvaloniaProperty.Register<JourneyNode, string?>(nameof(DropOffLabel));

    /// <summary>
    /// The node as the base of a share in the tooltip ("Share of trial starts"); null uses the header with a lower-case
    /// first letter.
    /// </summary>
    public static readonly StyledProperty<string?> ShareLabelProperty =
        AvaloniaProperty.Register<JourneyNode, string?>(nameof(ShareLabel));

    /// <summary>Creates an empty node.</summary>
    public JourneyNode()
    {
    }

    /// <summary>Creates a node with a key, its stage, a header and a kind.</summary>
    public JourneyNode(string key, string stageKey, string header, JourneyNodeKind kind = JourneyNodeKind.Step, string? description = null)
    {
        Key = key;
        StageKey = stageKey;
        Header = header;
        Kind = kind;
        Description = description;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="StageKeyProperty"/>
    public string? StageKey { get => GetValue(StageKeyProperty); set => SetValue(StageKeyProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public JourneyNodeKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="DropOffLabelProperty"/>
    public string? DropOffLabel { get => GetValue(DropOffLabelProperty); set => SetValue(DropOffLabelProperty, value); }

    /// <inheritdoc cref="ShareLabelProperty"/>
    public string? ShareLabel { get => GetValue(ShareLabelProperty); set => SetValue(ShareLabelProperty, value); }

    /// <summary>The key used by links: <see cref="Key"/>, or the header when no key is set.</summary>
    public string? EffectiveKey => Key ?? Header;

    /// <summary>Raised when any property changes.</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// How many people went from one node to a node in the next stage, in one <see cref="JourneySegment"/>. Links between
/// stages that are not next to each other are ignored.
/// </summary>
public class JourneyLink : AvaloniaObject
{
    /// <summary>The <see cref="JourneyNode.Key"/> the people came from.</summary>
    public static readonly StyledProperty<string?> SourceKeyProperty =
        AvaloniaProperty.Register<JourneyLink, string?>(nameof(SourceKey));

    /// <summary>The <see cref="JourneyNode.Key"/> they went to (a node in the next stage).</summary>
    public static readonly StyledProperty<string?> TargetKeyProperty =
        AvaloniaProperty.Register<JourneyLink, string?>(nameof(TargetKey));

    /// <summary>How many people (0 or more; other values count as 0).</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<JourneyLink, double>(nameof(Value));

    /// <summary>Creates an empty link.</summary>
    public JourneyLink()
    {
    }

    /// <summary>Creates a link.</summary>
    public JourneyLink(string sourceKey, string targetKey, double value)
    {
        SourceKey = sourceKey;
        TargetKey = targetKey;
        Value = value;
    }

    /// <inheritdoc cref="SourceKeyProperty"/>
    public string? SourceKey { get => GetValue(SourceKeyProperty); set => SetValue(SourceKeyProperty, value); }

    /// <inheritdoc cref="TargetKeyProperty"/>
    public string? TargetKey { get => GetValue(TargetKeyProperty); set => SetValue(TargetKeyProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Raised when any property changes.</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One data set of a <see cref="JourneyFlow"/> (a channel, a cohort, a month): the same nodes with their own link
/// counts. With two or more segments a switch over the chart picks one, and the chart morphs to it.
/// </summary>
public class JourneySegment : AvaloniaObject
{
    /// <summary>Identifies the segment; defaults to the header.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<JourneySegment, string?>(nameof(Key));

    /// <summary>The segment's name on the switch and as the side panel's overview title ("Search").</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<JourneySegment, string?>(nameof(Header));

    /// <summary>Creates an empty segment.</summary>
    public JourneySegment()
    {
        Links.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (JourneyLink link in e.OldItems)
                {
                    link.Changed -= OnLinkChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (JourneyLink link in e.NewItems)
                {
                    link.Changed += OnLinkChanged;
                }
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                foreach (var link in Links)
                {
                    link.Changed -= OnLinkChanged;
                    link.Changed += OnLinkChanged;
                }
            }
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Creates a segment with a key, a header and its links.</summary>
    public JourneySegment(string key, string header, IEnumerable<JourneyLink>? links = null) : this()
    {
        Key = key;
        Header = header;
        if (links is not null)
        {
            Links.AddRange(links);
        }
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>The flows of this segment, each from a node to a node in the next stage.</summary>
    [Content]
    public AvaloniaList<JourneyLink> Links { get; } = new();

    /// <summary>The key used to select the segment: <see cref="Key"/>, or the header when no key is set.</summary>
    public string? EffectiveKey => Key ?? Header;

    /// <summary>Raised when any property or link changes.</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnLinkChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Arguments of <see cref="JourneyFlow.SelectedNodeChanged"/>.</summary>
public class JourneyNodeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public JourneyNodeChangedEventArgs(RoutedEvent routedEvent, JourneyNode? oldNode, JourneyNode? newNode) : base(routedEvent)
    {
        OldNode = oldNode;
        NewNode = newNode;
    }

    /// <summary>The node selected before (null for none).</summary>
    public JourneyNode? OldNode { get; }

    /// <summary>The node selected now (null after the selection was cleared).</summary>
    public JourneyNode? NewNode { get; }
}

/// <summary>Arguments of <see cref="JourneyFlow.SelectedSegmentChanged"/>.</summary>
public class JourneySegmentChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public JourneySegmentChangedEventArgs(RoutedEvent routedEvent, JourneySegment? oldSegment, JourneySegment? newSegment) : base(routedEvent)
    {
        OldSegment = oldSegment;
        NewSegment = newSegment;
    }

    /// <summary>The segment shown before.</summary>
    public JourneySegment? OldSegment { get; }

    /// <summary>The segment shown now.</summary>
    public JourneySegment? NewSegment { get; }
}

/// <summary>
/// Arguments of <see cref="JourneyFlow.TraceChanged"/>: the node or link whose people are traced through the journey
/// (pointer over it, or keyboard focus on a node), or neither.
/// </summary>
public class JourneyTraceChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public JourneyTraceChangedEventArgs(RoutedEvent routedEvent, JourneyNode? node, JourneyLink? link) : base(routedEvent)
    {
        Node = node;
        Link = link;
    }

    /// <summary>The traced node (null when a link or nothing is traced).</summary>
    public JourneyNode? Node { get; }

    /// <summary>The traced link of the shown segment (null when a node or nothing is traced).</summary>
    public JourneyLink? Link { get; }
}
