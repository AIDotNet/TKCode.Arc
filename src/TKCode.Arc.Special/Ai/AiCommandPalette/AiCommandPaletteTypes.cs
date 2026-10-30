using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What an <see cref="AiCommandPalette"/> is doing: searching pages and actions, or asking the model.</summary>
public enum AiCommandPaletteMode
{
    /// <summary>The query filters the pages and actions.</summary>
    Search,

    /// <summary>The query is a question for the model; the answer streams in the palette.</summary>
    Ask
}

/// <summary>Where a suggested <see cref="AiCommandPaletteAction"/> is.</summary>
public enum AiCommandPaletteActionState
{
    /// <summary>Not run yet.</summary>
    Idle,

    /// <summary>Running: a spinner and the running label.</summary>
    Running,

    /// <summary>Finished: a check and the done label.</summary>
    Done,

    /// <summary>Failed: an alert and the error label; Enter runs it again.</summary>
    Failed
}

/// <summary>
/// A page or an action an <see cref="AiCommandPalette"/> searches: a label, an optional group, a 16px icon, quiet meta text,
/// a shortcut and extra keywords. Rows marked <see cref="IsSuggested"/> show while the query is empty.
/// </summary>
public sealed class AiCommandPaletteItem : AvaloniaObject
{
    /// <summary>A stable id (a route, a command name).</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, string>(nameof(Id), string.Empty);

    /// <summary>The row label, kept short ("Billing").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, string>(nameof(Label), string.Empty);

    /// <summary>The section the row is listed under ("Pages", "Actions"); rows without one share an untitled section.</summary>
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, string?>(nameof(Group));

    /// <summary>The 16px icon, a 24 × 24 stroked glyph (<see cref="ArcIcons"/>); null shows a neutral dot.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, Geometry?>(nameof(Icon));

    /// <summary>Quiet detail after the label ("Settings", "12 unread"); it truncates before the label does.</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, string?>(nameof(Meta));

    /// <summary>A shortcut shown as key caps at the end of the row, keys separated by spaces ("G I").</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, string?>(nameof(Shortcut));

    /// <summary>More words that find the row, separated by spaces or commas ("invoice payment card").</summary>
    public static readonly StyledProperty<string?> KeywordsProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, string?>(nameof(Keywords));

    /// <summary>Lists the row while the query is empty.</summary>
    public static readonly StyledProperty<bool> IsSuggestedProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, bool>(nameof(IsSuggested));

    /// <summary>Runs when the row is chosen, with <see cref="CommandParameter"/> (or the item when that is null).</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<AiCommandPaletteItem, object?>(nameof(CommandParameter));

    /// <summary>Creates an empty item.</summary>
    public AiCommandPaletteItem()
    {
    }

    /// <summary>Creates an item with an id, a label and an optional group, icon and meta text.</summary>
    public AiCommandPaletteItem(string id, string label, string? group = null, Geometry? icon = null, string? meta = null)
    {
        Id = id;
        Label = label;
        Group = group;
        Icon = icon;
        Meta = meta;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="GroupProperty"/>
    public string? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <inheritdoc cref="KeywordsProperty"/>
    public string? Keywords { get => GetValue(KeywordsProperty); set => SetValue(KeywordsProperty, value); }

    /// <inheritdoc cref="IsSuggestedProperty"/>
    public bool IsSuggested { get => GetValue(IsSuggestedProperty); set => SetValue(IsSuggestedProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
}

/// <summary>A document an answer cites as [1], [2] in its text, shown as a numbered chip above the answer.</summary>
public sealed class AiCommandPaletteSource : AvaloniaObject
{
    /// <summary>A stable id.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiCommandPaletteSource, string>(nameof(Id), string.Empty);

    /// <summary>The short chip title ("billing-api #1893").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AiCommandPaletteSource, string>(nameof(Title), string.Empty);

    /// <summary>Longer text for the tooltip and the accessible name of the chip and its citations.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AiCommandPaletteSource, string?>(nameof(Description));

    /// <summary>The 14px mark in the chip, a 24 × 24 stroked glyph; null shows a document glyph.</summary>
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<AiCommandPaletteSource, Geometry?>(nameof(Mark));

    /// <summary>An address opened with the system launcher when the source is chosen (unless the event is handled).</summary>
    public static readonly StyledProperty<Uri?> HrefProperty =
        AvaloniaProperty.Register<AiCommandPaletteSource, Uri?>(nameof(Href));

    /// <summary>Creates an empty source.</summary>
    public AiCommandPaletteSource()
    {
    }

    /// <summary>Creates a source with an id, a title and optional description, mark and address.</summary>
    public AiCommandPaletteSource(string id, string title, string? description = null, Geometry? mark = null, Uri? href = null)
    {
        Id = id;
        Title = title;
        Description = description;
        Mark = mark;
        Href = href;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="HrefProperty"/>
    public Uri? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }
}

/// <summary>
/// A suggested next step returned with an answer and run in place: a row with a mark, a label and quiet meta text that turns
/// into a spinner while <see cref="Run"/> works, then a check and the done label (or an alert, and Enter retries).
/// </summary>
public sealed class AiCommandPaletteAction : AvaloniaObject
{
    /// <summary>A stable id.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, string>(nameof(Id), string.Empty);

    /// <summary>The row label ("Create issue in Linear").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, string>(nameof(Label), string.Empty);

    /// <summary>Quiet detail after the label ("ENG · Billing").</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, string?>(nameof(Meta));

    /// <summary>The 16px mark of the tool it runs in, a 24 × 24 stroked glyph; null shows an arrow.</summary>
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, Geometry?>(nameof(Mark));

    /// <summary>
    /// Does the work. Resolve with a done label ("Created ENG-2481") or null; throw to show the failed state (Enter retries).
    /// The token is cancelled when the person asks again, goes back to search or the palette leaves the window.
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, Task<string?>>?> RunProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, Func<CancellationToken, Task<string?>>?>(nameof(Run));

    /// <summary>The label while it runs; null uses <see cref="AiCommandPaletteLabels.Running"/>.</summary>
    public static readonly StyledProperty<string?> RunningLabelProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, string?>(nameof(RunningLabel));

    /// <summary>The label once done when <see cref="Run"/> returns nothing; null uses <see cref="AiCommandPaletteLabels.Done"/>.</summary>
    public static readonly StyledProperty<string?> DoneLabelProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, string?>(nameof(DoneLabel));

    /// <summary>The label after a failure; null uses <see cref="AiCommandPaletteLabels.ActionFailed"/>.</summary>
    public static readonly StyledProperty<string?> ErrorLabelProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, string?>(nameof(ErrorLabel));

    /// <summary>The meta text once done; a simulated action says here that nothing was sent.</summary>
    public static readonly StyledProperty<string?> DoneMetaProperty =
        AvaloniaProperty.Register<AiCommandPaletteAction, string?>(nameof(DoneMeta));

    /// <summary>Creates an empty action.</summary>
    public AiCommandPaletteAction()
    {
    }

    /// <summary>Creates an action with an id, a label, optional meta text and mark, and the work it does.</summary>
    public AiCommandPaletteAction(string id, string label, string? meta = null, Geometry? mark = null,
        Func<CancellationToken, Task<string?>>? run = null)
    {
        Id = id;
        Label = label;
        Meta = meta;
        Mark = mark;
        Run = run;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="RunProperty"/>
    public Func<CancellationToken, Task<string?>>? Run { get => GetValue(RunProperty); set => SetValue(RunProperty, value); }

    /// <inheritdoc cref="RunningLabelProperty"/>
    public string? RunningLabel { get => GetValue(RunningLabelProperty); set => SetValue(RunningLabelProperty, value); }

    /// <inheritdoc cref="DoneLabelProperty"/>
    public string? DoneLabel { get => GetValue(DoneLabelProperty); set => SetValue(DoneLabelProperty, value); }

    /// <inheritdoc cref="ErrorLabelProperty"/>
    public string? ErrorLabel { get => GetValue(ErrorLabelProperty); set => SetValue(ErrorLabelProperty, value); }

    /// <inheritdoc cref="DoneMetaProperty"/>
    public string? DoneMeta { get => GetValue(DoneMetaProperty); set => SetValue(DoneMetaProperty, value); }
}

/// <summary>
/// What a <see cref="AiCommandPalette.Responder"/> resolves with: the whole text (when it did not stream it with
/// <see cref="AiCommandPaletteAskContext.Append"/>), the sources (when it did not set them early) and the suggested actions.
/// </summary>
public sealed class AiCommandPaletteAnswer
{
    /// <summary>The answer in plain text with [n] citations; used only when nothing was appended.</summary>
    public string? Text { get; init; }

    /// <summary>The cited sources; used only when none were set with <see cref="AiCommandPaletteAskContext.SetSources"/>.</summary>
    public IReadOnlyList<AiCommandPaletteSource>? Sources { get; init; }

    /// <summary>The next steps listed under the answer.</summary>
    public IReadOnlyList<AiCommandPaletteAction>? Actions { get; init; }
}

/// <summary>
/// The question a <see cref="AiCommandPalette.Responder"/> answers, with a token that is cancelled when the person asks again
/// or goes back, and the two ways to fill the answer while it works: <see cref="Append"/> streams text,
/// <see cref="SetSources"/> lands the source chips before the text.
/// </summary>
public sealed class AiCommandPaletteAskContext
{
    private readonly Action<string> _append;
    private readonly Action<IReadOnlyList<AiCommandPaletteSource>> _setSources;

    /// <summary>Creates a context (the palette makes one per question; tests and adapters can make their own).</summary>
    public AiCommandPaletteAskContext(string question, CancellationToken cancellationToken, Action<string> append,
        Action<IReadOnlyList<AiCommandPaletteSource>> setSources)
    {
        Question = question;
        CancellationToken = cancellationToken;
        _append = append;
        _setSources = setSources;
    }

    /// <summary>The question.</summary>
    public string Question { get; }

    /// <summary>Cancelled when the person asks again, goes back to search, or the palette leaves the window.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Streams more of the answer (any size: a token, a word, a sentence). Call it on the UI thread.</summary>
    public void Append(string text) => _append(text);

    /// <summary>Shows the source chips, ideally before the text that cites them. Call it on the UI thread.</summary>
    public void SetSources(IReadOnlyList<AiCommandPaletteSource> sources) => _setSources(sources);
}

/// <summary>Arguments of <see cref="AiCommandPalette.ItemSelected"/>: the page or action that was chosen.</summary>
public class AiCommandPaletteItemEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiCommandPaletteItemEventArgs(RoutedEvent routedEvent, AiCommandPaletteItem item) : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The chosen item.</summary>
    public AiCommandPaletteItem Item { get; }
}

/// <summary>Arguments of <see cref="AiCommandPalette.ActionCompleted"/>: the action and the label it ended on.</summary>
public class AiCommandPaletteActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiCommandPaletteActionEventArgs(RoutedEvent routedEvent, AiCommandPaletteAction action, string result) : base(routedEvent)
    {
        Action = action;
        Result = result;
    }

    /// <summary>The action that finished.</summary>
    public AiCommandPaletteAction Action { get; }

    /// <summary>The label it ended on ("Created ENG-2481").</summary>
    public string Result { get; }
}

/// <summary>
/// Arguments of <see cref="AiCommandPalette.SourceSelected"/>: the source and whether a citation in the text was chosen rather
/// than its chip. Set <see cref="RoutedEventArgs.Handled"/> to stop the palette from opening <see cref="AiCommandPaletteSource.Href"/>.
/// </summary>
public class AiCommandPaletteSourceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiCommandPaletteSourceEventArgs(RoutedEvent routedEvent, AiCommandPaletteSource source, bool fromCitation) : base(routedEvent)
    {
        Item = source;
        FromCitation = fromCitation;
    }

    /// <summary>The chosen source.</summary>
    public AiCommandPaletteSource Item { get; }

    /// <summary>True when a citation in the answer was chosen, false for a chip.</summary>
    public bool FromCitation { get; }
}

/// <summary>Arguments of <see cref="AiCommandPalette.ModeChanged"/>.</summary>
public class AiCommandPaletteModeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiCommandPaletteModeChangedEventArgs(RoutedEvent routedEvent, AiCommandPaletteMode mode) : base(routedEvent)
    {
        Mode = mode;
    }

    /// <summary>The mode the palette switched to.</summary>
    public AiCommandPaletteMode Mode { get; }
}
