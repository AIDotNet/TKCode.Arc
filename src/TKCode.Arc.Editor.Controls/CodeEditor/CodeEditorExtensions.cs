// Ported from vscode (src/vs/editor/browser/editorExtensions.ts: EditorContributionInstantiation, the editor
// contribution description, Command.register, EditorCommand, EditorAction and EditorCommand.bindToContribution;
// src/vs/editor/common/editorCommon.ts: IEditorContribution, IEditorAction; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - nothing is registered into process-wide registries: the built-in contributions and editor commands are the lists
//    in Contrib/CodeEditorContributions.cs, and each CodeEditor creates its contributions and registers the commands and
//    keybindings into its own CommandsRegistry and KeybindingsRegistry;
//  - a contribution is created by a factory that takes the CodeEditor (upstream: the instantiation service with the
//    editor as its first argument); the services a contribution needs are the editor's (LanguageFeatures,
//    MarkerService, ContextKeyService, ...);
//  - commands and actions run with the CodeEditor (upstream: a ServicesAccessor and the ICodeEditor) and return a Task;
//  - menus (menuOpts, contextMenuOpts), telemetry, MultiCommand / ProxyCommand and EditorAction2 are not ported.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>When an editor creates a contribution (upstream's EditorContributionInstantiation).</summary>
internal enum EditorContributionInstantiation
{
    /// <summary>Created with the editor (once it has a view model). Use for contributions that change what the editor shows at once.</summary>
    Eager,

    /// <summary>Created after the editor first renders, when the UI thread is idle.</summary>
    AfterFirstRender,

    /// <summary>Created before the first key or pointer event at the latest, sooner when the UI thread is idle.</summary>
    BeforeFirstInteraction,

    /// <summary>Created when the UI thread is idle (at the latest 5 s after the editor is created).</summary>
    Eventually,

    /// <summary>Created only when asked for (<see cref="CodeEditor.GetContribution{T}"/>).</summary>
    Lazy,
}

/// <summary>
/// A feature that lives as long as its editor (upstream's IEditorContribution): find, folding, hover, suggest, ...
/// It subscribes to the editor's events, adds decorations, widgets and gutter columns, and is disposed with the editor.
/// </summary>
internal interface ICodeEditorContribution : IDisposable
{
    /// <summary>State to keep when the editor's model is swapped (upstream's saveViewState); null for none.</summary>
    object? SaveViewState() => null;

    /// <summary>Restores what <see cref="SaveViewState"/> returned.</summary>
    void RestoreViewState(object? state)
    {
    }
}

/// <summary>How an editor creates one contribution (upstream's IEditorContributionDescription).</summary>
/// <param name="Id">The contribution's id (upstream's static ID of the controller, e.g. <c>editor.contrib.findController</c>).</param>
/// <param name="Create">Creates the contribution for an editor.</param>
/// <param name="Instantiation">When the editor creates it.</param>
internal sealed record EditorContributionDescription(
    string Id,
    Func<CodeEditor, ICodeEditorContribution> Create,
    EditorContributionInstantiation Instantiation = EditorContributionInstantiation.Eager);

/// <summary>
/// A keybinding of an editor command (upstream's ICommandKeybindingsOptions): key codes built with
/// <see cref="KeyMod"/> and <see cref="KeyCode"/> (<c>KeyMod.CtrlCmd | (int)KeyCode.KeyF</c>), optional
/// platform-specific ones, a <c>when</c> clause and a weight.
/// </summary>
internal sealed record EditorKeybinding(int Primary)
{
    public IReadOnlyList<int>? Secondary { get; init; }

    public PlatformKeybindings? Mac { get; init; }

    public PlatformKeybindings? Win { get; init; }

    public PlatformKeybindings? Linux { get; init; }

    /// <summary>When the keybinding applies (upstream's kbExpr); the command's precondition is added to it.</summary>
    public ContextKeyExpression? When { get; init; }

    /// <summary>The weight; contributions use <see cref="KeybindingWeight.EditorContrib"/> (upstream's default for them).</summary>
    public int Weight { get; init; } = KeybindingWeight.EditorContrib;

    /// <summary>The arguments the keybinding passes to the command.</summary>
    public object? Args { get; init; }
}

/// <summary>
/// A command that runs on an editor (upstream's EditorCommand): an id, a precondition checked against the editor's
/// context keys, and keybindings. Every editor registers the commands of <see cref="CodeEditorContributions.EditorCommands"/>
/// into its command and keybinding registries; a keybinding or <see cref="CodeEditor.Trigger"/> runs it.
/// </summary>
internal abstract class EditorCommand
{
    protected EditorCommand(string id, ContextKeyExpression? precondition, IReadOnlyList<EditorKeybinding>? keybindings)
    {
        Id = id;
        Precondition = precondition;
        Keybindings = keybindings ?? [];
    }

    /// <summary>The command's id.</summary>
    public string Id { get; }

    /// <summary>When the command can run, or null for always.</summary>
    public ContextKeyExpression? Precondition { get; }

    /// <summary>The keybindings that run the command.</summary>
    public IReadOnlyList<EditorKeybinding> Keybindings { get; }

    /// <summary>Runs the command on <paramref name="editor"/> (its precondition holds).</summary>
    public abstract Task RunEditorCommand(CodeEditor editor, object? args);

    /// <summary>A command that runs <paramref name="run"/>.</summary>
    public static EditorCommand Create(string id, ContextKeyExpression? precondition, Func<CodeEditor, object?, Task> run, params EditorKeybinding[] keybindings) =>
        new DelegateCommand(id, precondition, keybindings, run);

    /// <summary>A command that runs <paramref name="run"/> synchronously.</summary>
    public static EditorCommand Create(string id, ContextKeyExpression? precondition, Action<CodeEditor, object?> run, params EditorKeybinding[] keybindings) =>
        new DelegateCommand(id, precondition, keybindings, (editor, args) =>
        {
            run(editor, args);
            return Task.CompletedTask;
        });

    /// <summary>
    /// A command that calls <paramref name="handler"/> on the editor's contribution <paramref name="contributionId"/>
    /// (upstream's EditorCommand.bindToContribution); nothing when the editor has no such contribution.
    /// </summary>
    public static EditorCommand BindToContribution<T>(
        string contributionId,
        string id,
        ContextKeyExpression? precondition,
        Action<T, object?> handler,
        params EditorKeybinding[] keybindings)
        where T : class, ICodeEditorContribution =>
        new DelegateCommand(id, precondition, keybindings, (editor, args) =>
        {
            if (editor.GetContribution<T>(contributionId) is { } controller)
            {
                handler(controller, args);
            }

            return Task.CompletedTask;
        });

    private sealed class DelegateCommand(string id, ContextKeyExpression? precondition, IReadOnlyList<EditorKeybinding> keybindings, Func<CodeEditor, object?, Task> run)
        : EditorCommand(id, precondition, keybindings)
    {
        public override Task RunEditorCommand(CodeEditor editor, object? args) => run(editor, args);
    }
}

/// <summary>
/// An editor command with a label, which <see cref="CodeEditor.GetAction"/> finds (upstream's EditorAction): what a
/// command palette or context menu lists.
/// </summary>
internal abstract class EditorAction : EditorCommand
{
    protected EditorAction(string id, string label, string? alias, ContextKeyExpression? precondition, IReadOnlyList<EditorKeybinding>? keybindings)
        : base(id, precondition, keybindings)
    {
        Label = label;
        Alias = alias ?? label;
    }

    /// <summary>The label shown to the user.</summary>
    public string Label { get; }

    /// <summary>The label in English (upstream's alias).</summary>
    public string Alias { get; }

    /// <inheritdoc />
    public sealed override Task RunEditorCommand(CodeEditor editor, object? args) => Run(editor, args);

    /// <summary>Runs the action on <paramref name="editor"/>.</summary>
    public abstract Task Run(CodeEditor editor, object? args);

    /// <summary>An action that runs <paramref name="run"/>.</summary>
    public static EditorAction Create(string id, string label, ContextKeyExpression? precondition, Func<CodeEditor, object?, Task> run, params EditorKeybinding[] keybindings) =>
        new DelegateAction(id, label, precondition, keybindings, run);

    /// <summary>An action that runs <paramref name="run"/> synchronously.</summary>
    public static EditorAction Create(string id, string label, ContextKeyExpression? precondition, Action<CodeEditor, object?> run, params EditorKeybinding[] keybindings) =>
        new DelegateAction(id, label, precondition, keybindings, (editor, args) =>
        {
            run(editor, args);
            return Task.CompletedTask;
        });

    private sealed class DelegateAction(string id, string label, ContextKeyExpression? precondition, IReadOnlyList<EditorKeybinding> keybindings, Func<CodeEditor, object?, Task> run)
        : EditorAction(id, label, null, precondition, keybindings)
    {
        public override Task Run(CodeEditor editor, object? args) => run(editor, args);
    }
}

/// <summary>An editor's action, bound to it (upstream's InternalEditorAction / IEditorAction).</summary>
internal sealed class CodeEditorAction
{
    private readonly CodeEditor _editor;
    private readonly EditorAction _action;

    internal CodeEditorAction(CodeEditor editor, EditorAction action)
    {
        _editor = editor;
        _action = action;
    }

    public string Id => _action.Id;

    public string Label => _action.Label;

    public string Alias => _action.Alias;

    public ContextKeyExpression? Precondition => _action.Precondition;

    /// <summary>Whether the precondition holds now.</summary>
    public bool IsSupported() => _editor.ContextKeyService.ContextMatchesRules(_action.Precondition);

    /// <summary>Runs the action if it is supported (upstream's InternalEditorAction.run).</summary>
    public Task Run(object? args = null)
    {
        if (!IsSupported())
        {
            return Task.CompletedTask;
        }

        return _action.RunEditorCommand(_editor, args);
    }
}

/// <summary>The built-in handler ids <see cref="CodeEditor.Trigger"/> knows besides commands (upstream's editorCommon.Handler).</summary>
internal static class EditorHandler
{
    public const string CompositionStart = "compositionStart";
    public const string CompositionEnd = "compositionEnd";
    public const string Type = "type";
    public const string ReplacePreviousChar = "replacePreviousChar";
    public const string CompositionType = "compositionType";
    public const string Paste = "paste";
    public const string Cut = "cut";
}

/// <summary>The payload of <see cref="EditorHandler.Type"/> (upstream's TypePayload).</summary>
internal sealed record TypePayload(string Text);

/// <summary>The payload of <see cref="EditorHandler.ReplacePreviousChar"/> (upstream's ReplacePreviousCharPayload).</summary>
internal sealed record ReplacePreviousCharPayload(string Text, int ReplaceCharCnt);

/// <summary>The payload of <see cref="EditorHandler.CompositionType"/> (upstream's CompositionTypePayload).</summary>
internal sealed record CompositionTypePayload(string Text, int ReplacePrevCharCnt, int ReplaceNextCharCnt, int PositionDelta);

/// <summary>The payload of <see cref="EditorHandler.Paste"/> (upstream's PastePayload).</summary>
internal sealed record PastePayload(string Text)
{
    public bool PasteOnNewLine { get; init; }

    public IReadOnlyList<string>? MulticursorText { get; init; }

    /// <summary>Whether the text was copied from a column selection (upstream's isBlock clipboard metadata).</summary>
    public bool IsBlock { get; init; }
}
