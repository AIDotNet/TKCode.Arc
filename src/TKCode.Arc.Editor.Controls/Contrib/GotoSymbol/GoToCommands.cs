// Ported from vscode (src/vs/editor/contrib/gotoSymbol/browser/goToCommands.ts: SymbolNavigationAction, DefinitionAction,
// DeclarationAction, TypeDefinitionAction, ImplementationAction, ReferencesAction and their registered variants;
// commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the gotoLocation options are upstream's defaults: multiple results open the peek view ('peek'), the alternative of
//    go to definition / declaration / type definition / implementation is go to references, of go to references none;
//  - another document opens through the host (CodeEditor.OpenLocation; side by side asks the host too); the jump
//    highlight is a 'symbolHighlight' decoration (RangeHighlight color) for 350 ms;
//  - the set of running alternative commands is per editor (ReferencesController), not static;
//  - the symbol navigation service (F12 again to go to the next result) and the generic goToLocations command are not
//    ported; Peek References has Shift+Alt+F12 (upstream: none, the workbench's Find All References has it);
//  - menus, Go menu titles and the web-only Ctrl+F12 variant are not ported.

using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.GotoSymbol;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>How a symbol navigation behaves (upstream's SymbolNavigationActionConfig).</summary>
internal sealed record SymbolNavigationActionConfig(bool OpenToSide, bool OpenInPeek, bool MuteMessage);

/// <summary>What a symbol navigation asks for and how it reports nothing found.</summary>
internal sealed record SymbolNavigationKind(
    Func<CodeEditor, TextModel, TextPosition, CancellationToken, Task<ReferencesModel>> GetLocationModel,
    Func<WordAtPosition?, string> GetNoResultFoundMessage,
    string? AlternativeCommand);

/// <summary>Goes to, or peeks, the results of a location provider (upstream's SymbolNavigationAction).</summary>
internal sealed class SymbolNavigationAction : EditorAction
{
    public const string SymbolHighlightClassName = "symbolHighlight";

    private readonly SymbolNavigationKind _kind;

    public SymbolNavigationAction(
        string id,
        string label,
        ContextKeyExpression? precondition,
        SymbolNavigationActionConfig configuration,
        SymbolNavigationKind kind,
        params EditorKeybinding[] keybindings)
        : base(id, label, null, precondition, keybindings)
    {
        Configuration = configuration;
        _kind = kind;
    }

    public SymbolNavigationActionConfig Configuration { get; }

    /// <inheritdoc />
    public override Task Run(CodeEditor editor, object? args) => Run(editor, null);

    /// <summary>Runs at the cursor; <paramref name="range"/> is where the peek view goes (the cursor's line by default).</summary>
    public async Task Run(CodeEditor editor, TextRange? range)
    {
        if (editor.GetPosition() is not { } position)
        {
            return;
        }

        var model = editor.Model;
        var references = default(ReferencesModel);
        using (var cts = new EditorStateCancellationTokenSource(editor, CodeEditorStateFlag.Value | CodeEditorStateFlag.Position))
        {
            try
            {
                references = await _kind.GetLocationModel(editor, model, position, cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception error)
            {
                MessageController.Get(editor)?.ShowMessage(error.Message, position);
                return;
            }

            if (cts.Token.IsCancellationRequested)
            {
                references.Dispose();
                return;
            }
        }

        var controller = ReferencesController.Get(editor);
        SymbolNavigationAction? altAction = null;
        if (references.ReferenceAt(model.Uri, position) is not null
            && _kind.AlternativeCommand is { } altActionId
            && controller is not null
            && !controller.ActiveAlternativeCommands.Contains(altActionId)
            && GoToCommands.All.TryGetValue(altActionId, out var alt))
        {
            altAction = alt;
        }

        var referenceCount = references.References.Count;
        if (referenceCount == 0)
        {
            // no result: tell the user
            if (!Configuration.MuteMessage)
            {
                var info = ModelWords.GetWordAtPosition(model, position, editor.LanguageConfigurationService);
                MessageController.Get(editor)?.ShowMessage(_kind.GetNoResultFoundMessage(info), position);
            }

            references.Dispose();
        }
        else if (referenceCount == 1 && altAction is not null && controller is not null)
        {
            // already at the only result: the alternative (go to references from a definition)
            references.Dispose();
            controller.ActiveAlternativeCommands.Add(Id);
            try
            {
                await altAction.Run(editor, range).ConfigureAwait(true);
            }
            finally
            {
                controller.ActiveAlternativeCommands.Remove(Id);
            }
        }
        else
        {
            await OnResult(editor, references, range).ConfigureAwait(true);
        }
    }

    private async Task OnResult(CodeEditor editor, ReferencesModel model, TextRange? range)
    {
        // gotoLocation 'peek' (the default): several results open the peek view
        if (Configuration.OpenInPeek || model.References.Count > 1)
        {
            OpenInPeek(editor, model, range);
            return;
        }

        var next = model.FirstReference()!;
        await OpenReference(editor, next.Link, Configuration.OpenToSide, highlight: true).ConfigureAwait(true);
        model.Dispose();
    }

    private static async Task OpenReference(CodeEditor editor, LocationLink reference, bool sideBySide, bool highlight)
    {
        var range = reference.TargetSelectionRange ?? reference.Range;
        var shownHere = await editor.OpenLocation(reference.Uri, new TextRange(range.Start), sideBySide).ConfigureAwait(true);
        if (highlight && shownHere)
        {
            var model = editor.Model;
            var decorations = model.DeltaDecorations([], [new ModelDeltaDecoration(range, new ModelDecorationOptions("symbol-navigate-action-highlight") { ClassName = SymbolHighlightClassName })]);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (!model.IsDisposed())
                {
                    model.DeltaDecorations(decorations, []);
                }
            };
            timer.Start();
        }
    }

    private void OpenInPeek(CodeEditor target, ReferencesModel model, TextRange? range)
    {
        if (ReferencesController.Get(target) is { } controller)
        {
            controller.ToggleWidget(range ?? target.GetSelection()!.Value.Range, Task.FromResult(model), null, Configuration.OpenInPeek);
        }
        else
        {
            model.Dispose();
        }
    }
}

/// <summary>The go to and peek actions (upstream's registerAction2 calls in goToCommands.ts).</summary>
internal static class GoToCommands
{
    private const string GoToReferencesId = "editor.action.goToReferences";

    internal static readonly SymbolNavigationKind DefinitionsKind = new(
        static async (editor, model, position, token) => new ReferencesModel(
            await GoToSymbol.GetDefinitionsAtPosition(editor.LanguageFeatures.DefinitionProvider, model, position, false, token).ConfigureAwait(true), "Definitions"),
        static info => info is { Word.Length: > 0 } ? $"No definition found for '{info.Word}'" : "No definition found",
        GoToReferencesId);

    private static readonly SymbolNavigationKind s_declarations = new(
        static async (editor, model, position, token) => new ReferencesModel(
            await GoToSymbol.GetDeclarationsAtPosition(editor.LanguageFeatures.DeclarationProvider, model, position, false, token).ConfigureAwait(true), "Declarations"),
        static info => info is { Word.Length: > 0 } ? $"No declaration found for '{info.Word}'" : "No declaration found",
        GoToReferencesId);

    private static readonly SymbolNavigationKind s_typeDefinitions = new(
        static async (editor, model, position, token) => new ReferencesModel(
            await GoToSymbol.GetTypeDefinitionsAtPosition(editor.LanguageFeatures.TypeDefinitionProvider, model, position, false, token).ConfigureAwait(true), "Type Definitions"),
        static info => info is { Word.Length: > 0 } ? $"No type definition found for '{info.Word}'" : "No type definition found",
        GoToReferencesId);

    private static readonly SymbolNavigationKind s_implementations = new(
        static async (editor, model, position, token) => new ReferencesModel(
            await GoToSymbol.GetImplementationsAtPosition(editor.LanguageFeatures.ImplementationProvider, model, position, false, token).ConfigureAwait(true), "Implementations"),
        static info => info is { Word.Length: > 0 } ? $"No implementation found for '{info.Word}'" : "No implementation found",
        GoToReferencesId);

    private static readonly SymbolNavigationKind s_references = new(
        static async (editor, model, position, token) => new ReferencesModel(
            await GoToSymbol.GetReferencesAtPosition(editor.LanguageFeatures.ReferenceProvider, model, position, true, false, token).ConfigureAwait(true), "References"),
        static info => info is { Word.Length: > 0 } ? $"No references found for '{info.Word}'" : "No references found",
        null);

    private static readonly SymbolNavigationKind s_peekReferences = s_references with
    {
        GetLocationModel = static async (editor, model, position, token) => new ReferencesModel(
            await GoToSymbol.GetReferencesAtPosition(editor.LanguageFeatures.ReferenceProvider, model, position, false, false, token).ConfigureAwait(true), "References"),
    };

    private static readonly ContextKeyExpression? s_notInPeek = PeekContext.NotInPeekEditor;

    public static readonly SymbolNavigationAction GoToDefinition = new(
        "editor.action.revealDefinition", "Go to Definition", EditorContextKeys.HasDefinitionProvider,
        new(false, false, false), DefinitionsKind,
        new EditorKeybinding((int)KeyCode.F12) { When = EditorContextKeys.EditorTextFocus });

    public static readonly SymbolNavigationAction OpenDefinitionToSide = new(
        "editor.action.revealDefinitionAside", "Open Definition to the Side",
        ContextKeyExpr.And(EditorContextKeys.HasDefinitionProvider, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(true, false, false), DefinitionsKind,
        new EditorKeybinding(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, (int)KeyCode.F12)) { When = EditorContextKeys.EditorTextFocus });

    public static readonly SymbolNavigationAction PeekDefinition = new(
        "editor.action.peekDefinition", "Peek Definition",
        ContextKeyExpr.And(EditorContextKeys.HasDefinitionProvider, s_notInPeek, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(false, true, false), DefinitionsKind,
        new EditorKeybinding(KeyMod.Alt | (int)KeyCode.F12)
        {
            When = EditorContextKeys.EditorTextFocus,
            Linux = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.F10 },
        });

    public static readonly SymbolNavigationAction GoToDeclaration = new(
        "editor.action.revealDeclaration", "Go to Declaration",
        ContextKeyExpr.And(EditorContextKeys.HasDeclarationProvider, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(false, false, false), s_declarations);

    public static readonly SymbolNavigationAction PeekDeclaration = new(
        "editor.action.peekDeclaration", "Peek Declaration",
        ContextKeyExpr.And(EditorContextKeys.HasDeclarationProvider, s_notInPeek, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(false, true, false), s_declarations);

    public static readonly SymbolNavigationAction GoToTypeDefinition = new(
        "editor.action.goToTypeDefinition", "Go to Type Definition", EditorContextKeys.HasTypeDefinitionProvider,
        new(false, false, false), s_typeDefinitions);

    public static readonly SymbolNavigationAction PeekTypeDefinition = new(
        "editor.action.peekTypeDefinition", "Peek Type Definition",
        ContextKeyExpr.And(EditorContextKeys.HasTypeDefinitionProvider, s_notInPeek, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(false, true, false), s_typeDefinitions);

    public static readonly SymbolNavigationAction GoToImplementation = new(
        "editor.action.goToImplementation", "Go to Implementations", EditorContextKeys.HasImplementationProvider,
        new(false, false, false), s_implementations,
        new EditorKeybinding(KeyMod.CtrlCmd | (int)KeyCode.F12) { When = EditorContextKeys.EditorTextFocus });

    public static readonly SymbolNavigationAction PeekImplementation = new(
        "editor.action.peekImplementation", "Peek Implementations",
        ContextKeyExpr.And(EditorContextKeys.HasImplementationProvider, s_notInPeek, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(false, true, false), s_implementations,
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.F12) { When = EditorContextKeys.EditorTextFocus });

    public static readonly SymbolNavigationAction GoToReferences = new(
        GoToReferencesId, "Go to References",
        ContextKeyExpr.And(EditorContextKeys.HasReferenceProvider, s_notInPeek, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(false, false, false), s_references,
        new EditorKeybinding(KeyMod.Shift | (int)KeyCode.F12) { When = EditorContextKeys.EditorTextFocus });

    public static readonly SymbolNavigationAction PeekReferences = new(
        "editor.action.referenceSearch.trigger", "Peek References",
        ContextKeyExpr.And(EditorContextKeys.HasReferenceProvider, s_notInPeek, EditorContextKeys.IsInEmbeddedEditor.ToNegated()),
        new(false, true, false), s_peekReferences,
        new EditorKeybinding(KeyMod.Shift | KeyMod.Alt | (int)KeyCode.F12) { When = EditorContextKeys.EditorTextFocus });

    /// <summary>The actions by id (upstream's SymbolNavigationAction._allSymbolNavigationCommands); immutable.</summary>
    public static readonly IReadOnlyDictionary<string, SymbolNavigationAction> All = new[]
    {
        GoToDefinition, OpenDefinitionToSide, PeekDefinition, GoToDeclaration, PeekDeclaration, GoToTypeDefinition,
        PeekTypeDefinition, GoToImplementation, PeekImplementation, GoToReferences, PeekReferences,
    }.ToDictionary(static a => a.Id, StringComparer.Ordinal);
}
