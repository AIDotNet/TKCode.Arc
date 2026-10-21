using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which illustration an <see cref="EmptyStatesItem"/> shows (Arc's four empty states).</summary>
public enum EmptyStatesScene
{
    /// <summary>A search or filter found nothing: a magnifier over empty rows.</summary>
    NoResults,

    /// <summary>No connection: a cloud and a device joined by a broken line.</summary>
    Offline,

    /// <summary>Nothing left to read: a stacked inbox with a check mark.</summary>
    CaughtUp,

    /// <summary>A missing page: a folded map whose route ends in an x.</summary>
    NotFound
}

/// <summary>Where the action of the selected <see cref="EmptyStatesItem"/> stands.</summary>
public enum EmptyStatesPhase
{
    /// <summary>The empty state itself.</summary>
    Idle,

    /// <summary>The action is running: the button shows a spinner and the description says what is happening.</summary>
    Loading,

    /// <summary>The action finished: the illustration and copy show the result.</summary>
    Done
}

/// <summary>
/// One tab of an <see cref="EmptyStates"/> block: the scene its illustration shows and its copy for each phase. Text
/// left null uses Arc's copy for <see cref="Scene"/>.
/// </summary>
/// <remarks>Ported from Arc's free empty-states block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
public class EmptyStatesItem : AvaloniaObject
{
    /// <summary>The illustration: NoResults, Offline, CaughtUp or NotFound.</summary>
    public static readonly StyledProperty<EmptyStatesScene> SceneProperty =
        AvaloniaProperty.Register<EmptyStatesItem, EmptyStatesScene>(nameof(Scene));

    /// <summary>The tab label. Null uses Arc's label for the scene ("No results", "Offline", "Caught up", "Not found").</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(Header));

    /// <summary>The title in the Idle phase. Null uses Arc's copy for the scene.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(Title));

    /// <summary>The description in the Idle phase. Null uses Arc's copy for the scene.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(Description));

    /// <summary>The button label in the Idle phase. Null uses Arc's copy for the scene.</summary>
    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(ActionLabel));

    /// <summary>The title in the Done phase. Null uses Arc's copy for the scene.</summary>
    public static readonly StyledProperty<string?> DoneTitleProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(DoneTitle));

    /// <summary>The description in the Done phase. Null uses Arc's copy for the scene.</summary>
    public static readonly StyledProperty<string?> DoneDescriptionProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(DoneDescription));

    /// <summary>The button label in the Done phase. Null uses Arc's copy for the scene.</summary>
    public static readonly StyledProperty<string?> DoneActionLabelProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(DoneActionLabel));

    /// <summary>
    /// The description while Loading; the title and button label stay those of the phase the action started from. Null
    /// uses Arc's copy for the scene; an empty text keeps the description of that phase.
    /// </summary>
    public static readonly StyledProperty<string?> LoadingDescriptionProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(LoadingDescription));

    /// <summary>The accessible description of the illustration in the Idle phase. Null uses Arc's description of the drawing.</summary>
    public static readonly StyledProperty<string?> IllustrationLabelProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(IllustrationLabel));

    /// <summary>The accessible description of the illustration while Loading. Null uses Arc's description of the drawing.</summary>
    public static readonly StyledProperty<string?> LoadingIllustrationLabelProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(LoadingIllustrationLabel));

    /// <summary>The accessible description of the illustration in the Done phase. Null uses Arc's description of the drawing.</summary>
    public static readonly StyledProperty<string?> DoneIllustrationLabelProperty =
        AvaloniaProperty.Register<EmptyStatesItem, string?>(nameof(DoneIllustrationLabel));

    /// <summary>Any value you want back in <see cref="EmptyStates.ActionInvoked"/>.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<EmptyStatesItem, object?>(nameof(Tag));

    /// <summary>Creates an item for the NoResults scene with Arc's copy.</summary>
    public EmptyStatesItem()
    {
    }

    /// <summary>Creates an item for <paramref name="scene"/> with Arc's copy.</summary>
    public EmptyStatesItem(EmptyStatesScene scene)
    {
        Scene = scene;
    }

    /// <inheritdoc cref="SceneProperty"/>
    public EmptyStatesScene Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ActionLabelProperty"/>
    public string? ActionLabel { get => GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }

    /// <inheritdoc cref="DoneTitleProperty"/>
    public string? DoneTitle { get => GetValue(DoneTitleProperty); set => SetValue(DoneTitleProperty, value); }

    /// <inheritdoc cref="DoneDescriptionProperty"/>
    public string? DoneDescription { get => GetValue(DoneDescriptionProperty); set => SetValue(DoneDescriptionProperty, value); }

    /// <inheritdoc cref="DoneActionLabelProperty"/>
    public string? DoneActionLabel { get => GetValue(DoneActionLabelProperty); set => SetValue(DoneActionLabelProperty, value); }

    /// <inheritdoc cref="LoadingDescriptionProperty"/>
    public string? LoadingDescription { get => GetValue(LoadingDescriptionProperty); set => SetValue(LoadingDescriptionProperty, value); }

    /// <inheritdoc cref="IllustrationLabelProperty"/>
    public string? IllustrationLabel { get => GetValue(IllustrationLabelProperty); set => SetValue(IllustrationLabelProperty, value); }

    /// <inheritdoc cref="LoadingIllustrationLabelProperty"/>
    public string? LoadingIllustrationLabel { get => GetValue(LoadingIllustrationLabelProperty); set => SetValue(LoadingIllustrationLabelProperty, value); }

    /// <inheritdoc cref="DoneIllustrationLabelProperty"/>
    public string? DoneIllustrationLabel { get => GetValue(DoneIllustrationLabelProperty); set => SetValue(DoneIllustrationLabelProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    private EmptyStatesCopy Copy => EmptyStatesCopy.For(Scene);

    internal string EffectiveHeader => Header ?? Copy.Header;

    /// <summary>The copy shown in <paramref name="phase"/>; Loading keeps the title and label of <paramref name="from"/>.</summary>
    internal (string Title, string Description, string ActionLabel) CopyFor(EmptyStatesPhase phase, EmptyStatesPhase from)
    {
        var copy = Copy;
        var done = phase == EmptyStatesPhase.Done || (phase == EmptyStatesPhase.Loading && from == EmptyStatesPhase.Done);
        var title = done ? DoneTitle ?? copy.DoneTitle : Title ?? copy.Title;
        var description = done ? DoneDescription ?? copy.DoneDescription : Description ?? copy.Description;
        var label = done ? DoneActionLabel ?? copy.DoneActionLabel : ActionLabel ?? copy.ActionLabel;
        if (phase == EmptyStatesPhase.Loading && (LoadingDescription ?? copy.LoadingDescription) is { Length: > 0 } loading)
        {
            description = loading;
        }
        return (title, description, label);
    }

    /// <summary>The accessible description of the drawing in <paramref name="phase"/>.</summary>
    internal string IllustrationLabelFor(EmptyStatesPhase phase) => phase switch
    {
        EmptyStatesPhase.Loading => LoadingIllustrationLabel,
        EmptyStatesPhase.Done => DoneIllustrationLabel,
        _ => IllustrationLabel
    } ?? EmptyStatesArtFrames.Get(Scene, phase).Label;

    /// <inheritdoc />
    public override string ToString() => EffectiveHeader;
}

/// <summary>Arguments of <see cref="EmptyStates.ActionInvoked"/>.</summary>
public sealed class EmptyStatesActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>, whose action was pressed in <paramref name="phase"/>.</summary>
    public EmptyStatesActionEventArgs(RoutedEvent routedEvent, EmptyStatesItem item, EmptyStatesPhase phase)
        : base(routedEvent)
    {
        Item = item;
        Phase = phase;
    }

    /// <summary>The selected item whose action was pressed.</summary>
    public EmptyStatesItem Item { get; }

    /// <summary>The phase the action was pressed in (Idle or Done; the button ignores presses while Loading).</summary>
    public EmptyStatesPhase Phase { get; }
}

/// <summary>Arc's copy for each scene (empty-states.tsx: SCENES and COPY).</summary>
/// <remarks>Ported from Arc's free empty-states block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
internal sealed record EmptyStatesCopy(
    string Header,
    string Title,
    string Description,
    string ActionLabel,
    string DoneTitle,
    string DoneDescription,
    string DoneActionLabel,
    string LoadingDescription)
{
    private static readonly EmptyStatesCopy s_search = new("No results",
        "No results for “Q3 roadmap”", "Two filters are on: owner is Emma Collins and status is archived.", "Clear filters",
        "12 results for “Q3 roadmap”", "Filters cleared. Showing matches from every owner and status.", "Restore filters",
        "");

    private static readonly EmptyStatesCopy s_offline = new("Offline",
        "You are offline", "Edits are saved on this device and sync when the connection returns.", "Try again",
        "Back online", "Three edits synced to the Northwind workspace just now.", "Go offline",
        "Reaching sync.northwind.example…");

    private static readonly EmptyStatesCopy s_inbox = new("Caught up",
        "All caught up", "You have read every message in Inbox. New mail lands here first.", "Check for mail",
        "Still all caught up", "Checked just now. Nothing new since 8:30 this morning.", "Check again",
        "Checking for new mail…");

    private static readonly EmptyStatesCopy s_map = new("Not found",
        "Page not found", "The link to /projects/atlas-2023 is broken, or the page has moved.", "Find the page",
        "Found it in Archive", "Atlas 2023 roadmap moved to /archive/atlas-2023 in March.", "Show broken link",
        "Searching the workspace for Atlas…");

    public static EmptyStatesCopy For(EmptyStatesScene scene) => scene switch
    {
        EmptyStatesScene.Offline => s_offline,
        EmptyStatesScene.CaughtUp => s_inbox,
        EmptyStatesScene.NotFound => s_map,
        _ => s_search
    };
}
