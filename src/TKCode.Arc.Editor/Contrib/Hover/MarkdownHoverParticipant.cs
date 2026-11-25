// Ported from vscode (src/vs/editor/contrib/hover/browser/markdownHoverParticipant.ts: MarkdownHover, HoverSource,
// MarkdownHoverParticipant's computeSync / computeAsync / createLoadingMessage and the verbosity request of
// MarkdownRenderedHoverParts._fetchHover, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - rendering (renderHoverParts, the verbosity buttons) is the controls' (ContentHoverRenderer); the participant keeps
//    the computing half and the verbosity request (FetchHover), which the rendered hover calls;
//  - the accessible content is the Markdown as plain text (HoverMarkdown.ToPlainText) instead of the rendered element's
//    innerText;
//  - the long line messages link to the command id but there is no "Don't Show Again" action behind it.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>Markdown in a hover (upstream's MarkdownHover).</summary>
internal sealed class MarkdownHover(
    IEditorHoverParticipant owner,
    TextRange range,
    IReadOnlyList<MarkdownString> contents,
    bool isBeforeContent,
    int ordinal,
    HoverSource? source = null,
    string? copyContent = null) : IHoverPart
{
    public IEditorHoverParticipant Owner { get; } = owner;

    public TextRange Range { get; } = range;

    public IReadOnlyList<MarkdownString> Contents { get; } = contents;

    public bool IsBeforeContent { get; } = isBeforeContent;

    /// <summary>The order among the Markdown parts (provider order; decorations from 1000; the loading message 2000).</summary>
    public int Ordinal { get; } = ordinal;

    /// <summary>The provider's hover it came from, or null for a decoration's hover message.</summary>
    public HoverSource? Source { get; } = source;

    public string? CopyContent { get; } = copyContent;

    public bool IsValidForHoverAnchor(HoverAnchor anchor) =>
        anchor.Type == HoverAnchorType.Range
        && Range.StartColumn <= anchor.Range.StartColumn
        && Range.EndColumn >= anchor.Range.EndColumn;
}

/// <summary>The provider and hover a Markdown part came from (upstream's HoverSource).</summary>
internal sealed record HoverSource(Languages.Hover Hover, IHoverProvider HoverProvider, TextPosition HoverPosition)
{
    public bool SupportsVerbosityAction(HoverVerbosityAction action) => action switch
    {
        HoverVerbosityAction.Increase => Hover.CanIncreaseVerbosity,
        HoverVerbosityAction.Decrease => Hover.CanDecreaseVerbosity,
        _ => false,
    };
}

/// <summary>The hovers of the hover providers and of decorations' hover messages (upstream's MarkdownHoverParticipant).</summary>
internal sealed class MarkdownHoverParticipant(IHoverEditor editor) : IEditorHoverParticipant
{
    public const string HideLongLineWarningHoverActionId = "editor.action.hideLongLineWarningHover";

    // the provider requests of a verbosity change, per provider (upstream: MarkdownRenderedHoverParts._ongoingHoverOperations)
    private readonly Dictionary<IHoverProvider, (int VerbosityDelta, CancellationTokenSource TokenSource)> _ongoingHoverOperations = [];

    public int HoverOrdinal => 3;

    public IHoverPart? CreateLoadingMessage(HoverAnchor anchor) =>
        new MarkdownHover(this, anchor.Range, [new MarkdownString(HoverMarkdown.EscapeMarkdownSyntaxTokens("Loading..."))], false, 2000);

    public IReadOnlyList<IHoverPart> ComputeSync(HoverAnchor anchor, IReadOnlyList<IModelDecoration> lineDecorations, HoverStartSource source)
    {
        var model = editor.Model;
        if (model is null || anchor.Type != HoverAnchorType.Range)
        {
            return [];
        }

        var options = editor.HoverOptions;
        var lineNumber = anchor.Range.StartLineNumber;
        var maxColumn = model.GetLineMaxColumn(lineNumber);
        var result = new List<IHoverPart>();

        var index = 1000;

        var lineLength = model.GetLineLength(lineNumber);
        var stopRenderingLineAfter = options.StopRenderingLineAfter;
        var maxTokenizationLineLength = options.MaxTokenizationLineLength;
        var showLongLineWarning = options.ShowLongLineWarning;
        var stopRenderingMessage = false;
        if (stopRenderingLineAfter >= 0 && lineLength > stopRenderingLineAfter && anchor.Range.StartColumn >= stopRenderingLineAfter)
        {
            stopRenderingMessage = true;
            if (showLongLineWarning)
            {
                result.Add(new MarkdownHover(this, anchor.Range,
                [
                    new MarkdownString($"Rendering paused for long line for performance reasons. This can be configured via `editor.stopRenderingLineAfter`. [Don't Show Again](command:{HideLongLineWarningHoverActionId})") { IsTrusted = true },
                ], false, index++));
            }
        }

        if (!stopRenderingMessage && maxTokenizationLineLength is { } max && lineLength >= max)
        {
            if (showLongLineWarning)
            {
                result.Add(new MarkdownHover(this, anchor.Range,
                [
                    new MarkdownString($"Tokenization is skipped for long lines for performance reasons. This can be configured via `editor.maxTokenizationLineLength`. [Don't Show Again](command:{HideLongLineWarningHoverActionId})") { IsTrusted = true },
                ], false, index++));
            }
        }

        var isBeforeContent = false;
        foreach (var d in lineDecorations)
        {
            var startColumn = d.Range.StartLineNumber == lineNumber ? d.Range.StartColumn : 1;
            var endColumn = d.Range.EndLineNumber == lineNumber ? d.Range.EndColumn : maxColumn;

            var hoverMessage = d.Options.HoverMessage;
            if (hoverMessage is null || HoverMarkdown.IsEmptyMarkdownString(hoverMessage))
            {
                continue;
            }

            if (d.Options.BeforeContentClassName is not null)
            {
                isBeforeContent = true;
            }

            var range = new TextRange(anchor.Range.StartLineNumber, startColumn, anchor.Range.StartLineNumber, endColumn);
            result.Add(new MarkdownHover(this, range, hoverMessage, isBeforeContent, index++, null, d.Options.HoverMessageCopyContent));
        }

        return result;
    }

    public IAsyncEnumerable<IHoverPart>? ComputeAsync(HoverAnchor anchor, IReadOnlyList<IModelDecoration> lineDecorations, HoverStartSource source, CancellationToken token)
    {
        var model = editor.Model;
        if (model is null || anchor.Type != HoverAnchorType.Range)
        {
            return null;
        }

        var registry = editor.LanguageFeatures.HoverProvider;
        if (!registry.Has(model))
        {
            return null;
        }

        return GetMarkdownHovers(registry, model, anchor, token);
    }

    private async IAsyncEnumerable<IHoverPart> GetMarkdownHovers(
        LanguageFeatureRegistry<IHoverProvider> registry,
        TextModel model,
        HoverAnchor anchor,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var position = anchor.Range.Start;
        await foreach (var item in GetHover.GetHoverProviderResultsAsAsyncIterable(registry, model, position, token).ConfigureAwait(true))
        {
            if (!HoverMarkdown.IsEmptyMarkdownString(item.Hover.Contents))
            {
                var range = item.Hover.Range ?? anchor.Range;
                var hoverSource = new HoverSource(item.Hover, item.Provider, position);
                yield return new MarkdownHover(this, range, item.Hover.Contents, false, item.Ordinal, hoverSource);
            }
        }
    }

    public string GetAccessibleContent(IHoverPart hoverPart) =>
        hoverPart is MarkdownHover markdown
            ? string.Join("\n", markdown.Contents.Select(c => HoverMarkdown.ToPlainText(c.Value)).Where(t => t.Length > 0))
            : string.Empty;

    /// <summary>What a copy button copies (upstream's getCopyContent).</summary>
    public string GetCopyContent(MarkdownHover hoverPart) => hoverPart.CopyContent ?? GetAccessibleContent(hoverPart);

    /// <summary>
    /// The hover of the part's provider one level more or less verbose (upstream's _fetchHover), or null; a new request
    /// for the same provider cancels the running one and adds up the deltas.
    /// </summary>
    public async Task<MarkdownHover?> UpdateMarkdownHoverVerbosityLevel(MarkdownHover hoverPart, HoverVerbosityAction action)
    {
        var model = editor.Model;
        var hoverSource = hoverPart.Source;
        if (model is null || hoverSource is null || !hoverSource.SupportsVerbosityAction(action))
        {
            return null;
        }

        var verbosityDelta = action == HoverVerbosityAction.Increase ? 1 : -1;
        var provider = hoverSource.HoverProvider;
        if (_ongoingHoverOperations.TryGetValue(provider, out var ongoing))
        {
            ongoing.TokenSource.Cancel();
            verbosityDelta += ongoing.VerbosityDelta;
        }

        var tokenSource = new CancellationTokenSource();
        _ongoingHoverOperations[provider] = (verbosityDelta, tokenSource);
        var context = new HoverContext { VerbosityRequest = new HoverVerbosityRequest(verbosityDelta, hoverSource.Hover) };
        Languages.Hover? hover = null;
        try
        {
            hover = await provider.ProvideHover(model, hoverSource.HoverPosition, tokenSource.Token, context).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }

        if (_ongoingHoverOperations.TryGetValue(provider, out var current) && ReferenceEquals(current.TokenSource, tokenSource))
        {
            _ongoingHoverOperations.Remove(provider);
        }

        var cancelled = tokenSource.IsCancellationRequested;
        tokenSource.Dispose();
        if (hover is null || cancelled)
        {
            return null;
        }

        var newHoverSource = new HoverSource(hover, hoverSource.HoverProvider, hoverSource.HoverPosition);
        return new MarkdownHover(this, hoverPart.Range, hover.Contents, hoverPart.IsBeforeContent, hoverPart.Ordinal, newHoverSource);
    }

    /// <summary>Cancels the running verbosity requests (when the rendered hover goes away).</summary>
    public void CancelVerbosityRequests()
    {
        foreach (var (_, operation) in _ongoingHoverOperations)
        {
            operation.TokenSource.Cancel();
        }

        _ongoingHoverOperations.Clear();
    }
}
