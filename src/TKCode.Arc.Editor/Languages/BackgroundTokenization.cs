// Ported from vscode (src/vs/editor/common/languages.ts: ITokenizationSupport.createBackgroundTokenizer,
// IBackgroundTokenizer and IBackgroundTokenizationStore, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - createBackgroundTokenizer, an optional member of ITokenizationSupport upstream, is the interface
//    IBackgroundTokenizerFactory, which a tokenization support may also implement (Tokenization.cs keeps the contract
//    of the support itself);
//  - backgroundTokenizerShouldOnlyVerifyTokens and reportMismatchingTokens (a debugging aid that runs a second
//    tokenizer and compares) are left out;
//  - IBackgroundTokenizationStore has no setFontInfo: font tokens come only from tree-sitter and the experimental font
//    decorations, which are not ported.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>
/// A tokenization support that tokenizes a model in the background itself (TextMate does it in a web worker upstream)
/// instead of letting the model's default background tokenizer call it line by line on the owning thread.
/// </summary>
internal interface IBackgroundTokenizerFactory
{
    /// <summary>
    /// Creates the background tokenizer of a model; null to use the default one. It runs on the model's owning thread and
    /// must call <paramref name="store"/> there.
    /// </summary>
    IBackgroundTokenizer? CreateBackgroundTokenizer(TextModel textModel, IBackgroundTokenizationStore store);
}

/// <summary>A model's background tokenizer; disposed when the model's language or tokenizer changes.</summary>
internal interface IBackgroundTokenizer : IDisposable
{
    /// <summary>
    /// Instructs the background tokenizer to set the tokens for the given range again. This might be necessary if the
    /// renderer overwrote those tokens with heuristically computed ones for some viewport, when the change does not even
    /// propagate to that viewport.
    /// </summary>
    void RequestTokens(int startLineNumber, int endLineNumberExclusive);
}

/// <summary>Where a background tokenizer puts what it computed.</summary>
internal interface IBackgroundTokenizationStore
{
    void SetTokens(IReadOnlyList<ContiguousMultilineTokens> tokens);

    void SetEndState(int lineNumber, IState state);

    /// <summary>
    /// Should be called to indicate that the background tokenization has finished for now. (This triggers bracket pair
    /// colorization to re-parse the bracket pairs with token information.)
    /// </summary>
    void BackgroundTokenizationFinished();
}
