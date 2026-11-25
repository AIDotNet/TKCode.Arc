// Ported from vscode (src/vs/editor/common/languages.ts: CodeActionProvider, RenameProvider,
// DocumentFormattingEditProvider, DocumentRangeFormattingEditProvider, OnTypeFormattingEditProvider,
// DocumentColorProvider and their types, commit b57d5c9e), MIT license. The general differences are listed in
// LanguageFeatures.Types.cs. Here also:
//  - CodeActionList (upstream: IDisposable) carries an optional Dispose callback;
//  - `WorkspaceEdit & Rejection` and `RenameLocation & Rejection` are WorkspaceEdit / RenameLocation with a
//    RejectReason (RenameEdits, RenameLocationResult);
//  - code action documentation and _getAdditionalMenuItems are not ported.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Languages;

/// <summary>A command or an edit that fixes or refactors code (upstream's CodeAction).</summary>
internal sealed record CodeAction(string Title)
{
    public LanguageCommand? Command { get; init; }

    public WorkspaceEdit? Edit { get; init; }

    /// <summary>The diagnostics the action resolves.</summary>
    public IReadOnlyList<MarkerData>? Diagnostics { get; init; }

    /// <summary>A hierarchical kind such as <c>quickfix</c> or <c>refactor.extract</c>.</summary>
    public string? Kind { get; init; }

    public bool IsPreferred { get; init; }

    public bool IsAI { get; init; }

    /// <summary>Why the action cannot be applied now, or null.</summary>
    public string? Disabled { get; init; }

    public IReadOnlyList<TextRange>? Ranges { get; init; }
}

internal enum CodeActionTriggerType
{
    Invoke = 1,
    Auto = 2,
}

/// <summary>The context of a code action request (upstream's CodeActionContext).</summary>
internal sealed record CodeActionContext(CodeActionTriggerType Trigger)
{
    /// <summary>Only actions of this kind are asked for, or null.</summary>
    public string? Only { get; init; }
}

/// <summary>The code actions of one request (upstream's CodeActionList).</summary>
internal sealed record CodeActionList(IReadOnlyList<CodeAction> Actions)
{
    public Action? Dispose { get; init; }
}

/// <summary>Provides code actions (upstream's CodeActionProvider).</summary>
internal interface ICodeActionProvider
{
    string? DisplayName => null;

    string? ExtensionId => null;

    /// <summary>The kinds of actions the provider may return, or null.</summary>
    IReadOnlyList<string>? ProvidedCodeActionKinds => null;

    /// <summary>Actions for a range or selection.</summary>
    Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token);

    /// <summary>Whether <see cref="ResolveCodeAction"/> is implemented.</summary>
    bool SupportsResolveCodeAction => false;

    /// <summary>Fills in the edit of an action.</summary>
    Task<CodeAction?> ResolveCodeAction(CodeAction codeAction, CancellationToken token) => Task.FromResult<CodeAction?>(null);
}

/// <summary>The range and text a rename starts with (upstream's RenameLocation &amp; Rejection).</summary>
internal sealed record RenameLocationResult(TextRange Range, string Text)
{
    /// <summary>Why the symbol cannot be renamed; then range and text are not used.</summary>
    public string? RejectReason { get; init; }
}

/// <summary>The edits of a rename (upstream's WorkspaceEdit &amp; Rejection).</summary>
internal sealed record RenameEdits(WorkspaceEdit Edit)
{
    /// <summary>Why the rename failed; then the edit is not used.</summary>
    public string? RejectReason { get; init; }
}

/// <summary>Provides renames (upstream's RenameProvider).</summary>
internal interface IRenameProvider
{
    Task<RenameEdits?> ProvideRenameEdits(TextModel model, TextPosition position, string newName, CancellationToken token);

    /// <summary>Whether <see cref="ResolveRenameLocation"/> is implemented (else the word at the position is renamed).</summary>
    bool SupportsResolveRenameLocation => false;

    Task<RenameLocationResult?> ResolveRenameLocation(TextModel model, TextPosition position, CancellationToken token) =>
        Task.FromResult<RenameLocationResult?>(null);
}

/// <summary>Options for formatting (upstream's FormattingOptions).</summary>
internal sealed record FormattingOptions(int TabSize, bool InsertSpaces);

/// <summary>Formats whole documents (upstream's DocumentFormattingEditProvider).</summary>
internal interface IDocumentFormattingEditProvider
{
    string? ExtensionId => null;

    string? DisplayName => null;

    Task<IReadOnlyList<TextEdit>?> ProvideDocumentFormattingEdits(TextModel model, FormattingOptions options, CancellationToken token);
}

/// <summary>Formats ranges (upstream's DocumentRangeFormattingEditProvider).</summary>
internal interface IDocumentRangeFormattingEditProvider
{
    string? ExtensionId => null;

    string? DisplayName => null;

    Task<IReadOnlyList<TextEdit>?> ProvideDocumentRangeFormattingEdits(TextModel model, TextRange range, FormattingOptions options, CancellationToken token);

    /// <summary>Whether <see cref="ProvideDocumentRangesFormattingEdits"/> is implemented.</summary>
    bool SupportsRangesFormatting => false;

    Task<IReadOnlyList<TextEdit>?> ProvideDocumentRangesFormattingEdits(TextModel model, IReadOnlyList<TextRange> ranges, FormattingOptions options, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<TextEdit>?>(null);
}

/// <summary>Formats as the user types (upstream's OnTypeFormattingEditProvider).</summary>
internal interface IOnTypeFormattingEditProvider
{
    string? ExtensionId => null;

    /// <summary>The characters that trigger formatting.</summary>
    IReadOnlyList<string> AutoFormatTriggerCharacters { get; }

    Task<IReadOnlyList<TextEdit>?> ProvideOnTypeFormattingEdits(TextModel model, TextPosition position, string ch, FormattingOptions options, CancellationToken token);
}

/// <summary>A color in RGBA, each part from 0 to 1 (upstream's IColor).</summary>
internal readonly record struct LanguageColor(double Red, double Green, double Blue, double Alpha);

/// <summary>A way to write a color (upstream's IColorPresentation).</summary>
internal sealed record ColorPresentation(string Label)
{
    public TextEdit? TextEdit { get; init; }

    public IReadOnlyList<TextEdit>? AdditionalTextEdits { get; init; }
}

/// <summary>A color and where it is written (upstream's IColorInformation).</summary>
internal sealed record ColorInformation(TextRange Range, LanguageColor Color);

/// <summary>Provides the colors of a document and their presentations (upstream's DocumentColorProvider).</summary>
internal interface IDocumentColorProvider
{
    Task<IReadOnlyList<ColorInformation>?> ProvideDocumentColors(TextModel model, CancellationToken token);

    Task<IReadOnlyList<ColorPresentation>?> ProvideColorPresentations(TextModel model, ColorInformation colorInfo, CancellationToken token);
}
