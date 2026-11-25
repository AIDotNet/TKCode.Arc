// Ported from vscode (src/vs/editor/common/languages.ts: HoverProvider, SignatureHelpProvider, CodeLensProvider,
// InlayHintsProvider, DocumentSemanticTokensProvider, DocumentRangeSemanticTokensProvider and their types, commit
// b57d5c9e), MIT license. The general differences are listed in LanguageFeatures.Types.cs. Here also:
//  - SignatureHelpResult and CodeLensList / InlayHintList (upstream: IDisposable) carry an optional Dispose callback;
//  - ParameterInformation.label (string | [start, end]) is Label or LabelOffsets;
//  - semantic token data (Uint32Array) is uint[].

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Information about a symbol or word, shown in a tooltip-like widget (upstream's Hover).</summary>
internal sealed class Hover(IReadOnlyList<MarkdownString> contents)
{
    public IReadOnlyList<MarkdownString> Contents { get; } = contents;

    /// <summary>The range the hover applies to; null uses the word at the position.</summary>
    public TextRange? Range { get; init; }

    public bool CanIncreaseVerbosity { get; init; }

    public bool CanDecreaseVerbosity { get; init; }
}

/// <summary>Asks for a more or less verbose hover (upstream's HoverVerbosityRequest).</summary>
internal sealed record HoverVerbosityRequest(int VerbosityDelta, Hover PreviousHover);

/// <summary>The context a hover provider is called in (upstream's HoverContext).</summary>
internal sealed record HoverContext
{
    public HoverVerbosityRequest? VerbosityRequest { get; init; }
}

internal enum HoverVerbosityAction
{
    Increase,
    Decrease,
}

/// <summary>Provides hovers (upstream's HoverProvider).</summary>
internal interface IHoverProvider
{
    /// <summary>A hover for the position; several providers' hovers at the same position are merged.</summary>
    Task<Hover?> ProvideHover(TextModel model, TextPosition position, CancellationToken token, HoverContext? context = null);
}

/// <summary>A parameter of a callable-signature (upstream's ParameterInformation).</summary>
internal sealed record ParameterInformation
{
    /// <summary>The label: a substring of the signature's label.</summary>
    public string? Label { get; init; }

    /// <summary>The label as start and end offsets in the signature's label (exclusive end).</summary>
    public (int Start, int End)? LabelOffsets { get; init; }

    public StringOrMarkdown? Documentation { get; init; }
}

/// <summary>The signature of something callable (upstream's SignatureInformation).</summary>
internal sealed record SignatureInformation(string Label, IReadOnlyList<ParameterInformation> Parameters)
{

    public StringOrMarkdown? Documentation { get; init; }

    /// <summary>The index of the active parameter; overrides <see cref="SignatureHelp.ActiveParameter"/>.</summary>
    public int? ActiveParameter { get; init; }
}

/// <summary>The signatures at a position and the active one (upstream's SignatureHelp).</summary>
internal sealed record SignatureHelp(IReadOnlyList<SignatureInformation> Signatures, int ActiveSignature, int ActiveParameter);

/// <summary>A signature help result (upstream's SignatureHelpResult).</summary>
internal sealed record SignatureHelpResult(SignatureHelp Value)
{
    public Action? Dispose { get; init; }
}

internal enum SignatureHelpTriggerKind
{
    Invoke = 1,
    TriggerCharacter = 2,
    ContentChange = 3,
}

/// <summary>The context a signature help provider is called in.</summary>
internal sealed record SignatureHelpContext(SignatureHelpTriggerKind TriggerKind, bool IsRetrigger)
{
    public string? TriggerCharacter { get; init; }

    public SignatureHelp? ActiveSignatureHelp { get; init; }
}

/// <summary>Provides parameter hints (upstream's SignatureHelpProvider).</summary>
internal interface ISignatureHelpProvider
{
    IReadOnlyList<string>? SignatureHelpTriggerCharacters => null;

    IReadOnlyList<string>? SignatureHelpRetriggerCharacters => null;

    Task<SignatureHelpResult?> ProvideSignatureHelp(TextModel model, TextPosition position, CancellationToken token, SignatureHelpContext context);
}

/// <summary>A command shown above a line (upstream's CodeLens).</summary>
internal sealed record CodeLens(TextRange Range)
{
    public string? Id { get; init; }

    public LanguageCommand? Command { get; init; }
}

/// <summary>The code lenses of a document (upstream's CodeLensList).</summary>
internal sealed class CodeLensList(IReadOnlyList<CodeLens> lenses)
{
    public IReadOnlyList<CodeLens> Lenses { get; } = lenses;

    public Action? Dispose { get; init; }
}

/// <summary>Provides code lenses (upstream's CodeLensProvider).</summary>
internal interface ICodeLensProvider
{
    /// <summary>Raised when the lenses should be asked for again.</summary>
    event Action<ICodeLensProvider>? DidChange
    {
        add
        {
        }

        remove
        {
        }
    }

    Task<CodeLensList?> ProvideCodeLenses(TextModel model, CancellationToken token);

    /// <summary>Whether <see cref="ResolveCodeLens"/> is implemented.</summary>
    bool SupportsResolveCodeLens => false;

    /// <summary>Fills in the command of a lens.</summary>
    Task<CodeLens?> ResolveCodeLens(TextModel model, CodeLens codeLens, CancellationToken token) => Task.FromResult<CodeLens?>(null);
}

internal enum InlayHintKind
{
    Type = 1,
    Parameter = 2,
}

/// <summary>A part of an inlay hint's label (upstream's InlayHintLabelPart).</summary>
internal sealed record InlayHintLabelPart(string Label)
{
    public StringOrMarkdown? Tooltip { get; init; }

    public LanguageCommand? Command { get; init; }

    public Location? Location { get; init; }
}

/// <summary>Text shown inline between characters (upstream's InlayHint).</summary>
internal sealed record InlayHint(TextPosition Position)
{
    /// <summary>The label as one string; null when <see cref="LabelParts"/> is set.</summary>
    public string? Label { get; init; }

    /// <summary>The label as parts with their own tooltips, commands and locations.</summary>
    public IReadOnlyList<InlayHintLabelPart>? LabelParts { get; init; }

    public StringOrMarkdown? Tooltip { get; init; }

    public IReadOnlyList<TextEdit>? TextEdits { get; init; }

    public InlayHintKind? Kind { get; init; }

    public bool PaddingLeft { get; init; }

    public bool PaddingRight { get; init; }
}

/// <summary>The inlay hints of a range (upstream's InlayHintList).</summary>
internal sealed class InlayHintList(IReadOnlyList<InlayHint> hints)
{
    public IReadOnlyList<InlayHint> Hints { get; } = hints;

    public Action? Dispose { get; init; }
}

/// <summary>Provides inlay hints (upstream's InlayHintsProvider).</summary>
internal interface IInlayHintsProvider
{
    string? DisplayName => null;

    /// <summary>Raised when the hints should be asked for again.</summary>
    event Action? DidChangeInlayHints
    {
        add
        {
        }

        remove
        {
        }
    }

    Task<InlayHintList?> ProvideInlayHints(TextModel model, TextRange range, CancellationToken token);

    /// <summary>Whether <see cref="ResolveInlayHint"/> is implemented.</summary>
    bool SupportsResolveInlayHint => false;

    Task<InlayHint?> ResolveInlayHint(InlayHint hint, CancellationToken token) => Task.FromResult<InlayHint?>(null);
}

/// <summary>The token types and modifiers semantic token data indexes (upstream's SemanticTokensLegend).</summary>
internal sealed class SemanticTokensLegend(IReadOnlyList<string> tokenTypes, IReadOnlyList<string> tokenModifiers)
{
    public IReadOnlyList<string> TokenTypes { get; } = tokenTypes;
    public IReadOnlyList<string> TokenModifiers { get; } = tokenModifiers;
}

/// <summary>
/// Semantic tokens: five numbers per token (delta line, delta start character, length, token type, modifier bits),
/// as upstream's SemanticTokens.
/// </summary>
internal sealed class SemanticTokens(uint[] data)
{
    public uint[] Data { get; } = data;

    public string? ResultId { get; init; }
}

/// <summary>A change of the previous result's data (upstream's SemanticTokensEdit).</summary>
internal sealed record SemanticTokensEdit(int Start, int DeleteCount)
{
    public uint[]? Data { get; init; }
}

/// <summary>Changes to the previous result (upstream's SemanticTokensEdits).</summary>
internal sealed class SemanticTokensEdits(IReadOnlyList<SemanticTokensEdit> edits)
{
    public IReadOnlyList<SemanticTokensEdit> Edits { get; } = edits;

    public string? ResultId { get; init; }
}

/// <summary>Semantic tokens or edits to the previous ones.</summary>
internal union SemanticTokensOrEdits(SemanticTokens, SemanticTokensEdits)
{
    public SemanticTokens? Tokens => this is SemanticTokens tokens ? tokens : null;

    public SemanticTokensEdits? Edits => this is SemanticTokensEdits edits ? edits : null;
}

/// <summary>Provides the semantic tokens of a whole document (upstream's DocumentSemanticTokensProvider).</summary>
internal interface IDocumentSemanticTokensProvider
{
    event Action? DidChange
    {
        add
        {
        }

        remove
        {
        }
    }

    SemanticTokensLegend GetLegend();

    Task<SemanticTokensOrEdits?> ProvideDocumentSemanticTokens(TextModel model, string? lastResultId, CancellationToken token);

    void ReleaseDocumentSemanticTokens(string? resultId);
}

/// <summary>Provides the semantic tokens of a range (upstream's DocumentRangeSemanticTokensProvider).</summary>
internal interface IDocumentRangeSemanticTokensProvider
{
    event Action? DidChange
    {
        add
        {
        }

        remove
        {
        }
    }

    SemanticTokensLegend GetLegend();

    Task<SemanticTokens?> ProvideDocumentRangeSemanticTokens(TextModel model, TextRange range, CancellationToken token);
}
