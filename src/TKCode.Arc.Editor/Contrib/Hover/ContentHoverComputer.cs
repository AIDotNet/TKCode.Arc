// Ported from vscode (src/vs/editor/contrib/hover/browser/contentHoverComputer.ts and contentHoverTypes.ts, commit
// b57d5c9e), MIT license.
// Differences from upstream: the editor is an IHoverEditor; AsyncIterableProducer.merge is AsyncEnumerables.Merge
// (items in the order they arrive); coalesce drops null parts.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>What a content hover computes (upstream's ContentHoverComputerOptions).</summary>
internal sealed record ContentHoverComputerOptions(HoverAnchor Anchor, HoverStartSource Source, bool ShouldFocus, bool InsistOnKeepingHoverVisible);

/// <summary>Asks every participant for its parts at an anchor (upstream's ContentHoverComputer).</summary>
internal sealed class ContentHoverComputer(IHoverEditor editor, IReadOnlyList<IEditorHoverParticipant> participants)
    : IHoverComputer<ContentHoverComputerOptions, IHoverPart>
{
    /// <summary>The decorations of the anchor's line that touch the anchor (upstream's _getLineDecorations).</summary>
    public static IReadOnlyList<IModelDecoration> GetLineDecorations(IHoverEditor editor, HoverAnchor anchor)
    {
        if (anchor.Type != HoverAnchorType.Range && !anchor.SupportsMarkerHover)
        {
            return [];
        }

        var model = editor.Model;
        if (model is null)
        {
            return [];
        }

        var lineNumber = anchor.Range.StartLineNumber;
        if (lineNumber > model.GetLineCount())
        {
            // invalid line
            return [];
        }

        var maxColumn = model.GetLineMaxColumn(lineNumber);
        var result = new List<IModelDecoration>();
        foreach (var d in editor.GetLineDecorations(lineNumber))
        {
            if (d.Options.IsWholeLine)
            {
                result.Add(d);
                continue;
            }

            var startColumn = d.Range.StartLineNumber == lineNumber ? d.Range.StartColumn : 1;
            var endColumn = d.Range.EndLineNumber == lineNumber ? d.Range.EndColumn : maxColumn;
            if (d.Options.ShowIfCollapsed)
            {
                // relax the check around showIfCollapsed decorations to also include +/- 1 character
                if (startColumn > anchor.Range.StartColumn + 1 || anchor.Range.EndColumn - 1 > endColumn)
                {
                    continue;
                }
            }
            else if (startColumn > anchor.Range.StartColumn || anchor.Range.EndColumn > endColumn)
            {
                continue;
            }

            result.Add(d);
        }

        return result;
    }

    public IAsyncEnumerable<IHoverPart>? ComputeAsync(ContentHoverComputerOptions options, CancellationToken token)
    {
        var anchor = options.Anchor;
        if (editor.Model is null)
        {
            return null;
        }

        var lineDecorations = GetLineDecorations(editor, anchor);
        var sources = new List<IAsyncEnumerable<IHoverPart>>();
        foreach (var participant in participants)
        {
            if (participant.ComputeAsync(anchor, lineDecorations, options.Source, token) is { } items)
            {
                sources.Add(items);
            }
        }

        return sources.Count == 0 ? null : AsyncEnumerables.Merge(sources, token);
    }

    public IReadOnlyList<IHoverPart> ComputeSync(ContentHoverComputerOptions options)
    {
        if (editor.Model is null)
        {
            return [];
        }

        var anchor = options.Anchor;
        var lineDecorations = GetLineDecorations(editor, anchor);
        var result = new List<IHoverPart>();
        foreach (var participant in participants)
        {
            foreach (var part in participant.ComputeSync(anchor, lineDecorations, options.Source))
            {
                if (part is not null)
                {
                    result.Add(part);
                }
            }
        }

        return result;
    }
}

/// <summary>The parts of a content hover (upstream's ContentHoverResult).</summary>
internal class ContentHoverResult(IReadOnlyList<IHoverPart> hoverParts, bool isComplete, ContentHoverComputerOptions options)
{
    public IReadOnlyList<IHoverPart> HoverParts { get; } = hoverParts;

    public bool IsComplete { get; } = isComplete;

    public ContentHoverComputerOptions Options { get; } = options;

    /// <summary>The parts still valid for <paramref name="anchor"/> (this instance when all are).</summary>
    public virtual ContentHoverResult Filter(HoverAnchor anchor)
    {
        var filtered = HoverParts.Where(m => m.IsValidForHoverAnchor(anchor)).ToList();
        if (filtered.Count == HoverParts.Count)
        {
            return this;
        }

        return new FilteredContentHoverResult(this, filtered, IsComplete, Options);
    }
}

/// <summary>A filtered result that filters its original again (upstream's FilteredContentHoverResult).</summary>
internal sealed class FilteredContentHoverResult(ContentHoverResult original, IReadOnlyList<IHoverPart> messages, bool isComplete, ContentHoverComputerOptions options)
    : ContentHoverResult(messages, isComplete, options)
{
    public override ContentHoverResult Filter(HoverAnchor anchor) => original.Filter(anchor);
}

/// <summary>Merging async sequences (upstream's AsyncIterableProducer.merge and fromPromisesResolveOrder).</summary>
internal static class AsyncEnumerables
{
    /// <summary>The items of all sources, in the order they arrive (a source that completes synchronously goes first).</summary>
    public static async IAsyncEnumerable<T> Merge<T>(
        IReadOnlyList<IAsyncEnumerable<T>> sources,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
    {
        if (sources.Count == 1)
        {
            await foreach (var item in sources[0].WithCancellation(token))
            {
                yield return item;
            }

            yield break;
        }

        var enumerators = new List<IAsyncEnumerator<T>>();
        var pending = new List<Task<bool>>();
        try
        {
            foreach (var source in sources)
            {
                var enumerator = source.GetAsyncEnumerator(token);
                enumerators.Add(enumerator);
                pending.Add(enumerator.MoveNextAsync().AsTask());
            }

            var active = enumerators.Count;
            while (active > 0)
            {
                var done = await Task.WhenAny(pending.Where(t => t is not null)).ConfigureAwait(true);
                var index = pending.IndexOf(done);
                if (await done.ConfigureAwait(true))
                {
                    var item = enumerators[index].Current;
                    pending[index] = enumerators[index].MoveNextAsync().AsTask();
                    yield return item;
                }
                else
                {
                    pending[index] = null!;
                    active--;
                }
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                await enumerator.DisposeAsync().ConfigureAwait(true);
            }
        }
    }

    /// <summary>The results of the tasks in the order they complete, without the nulls (upstream's fromPromisesResolveOrder(...).coalesce()).</summary>
    public static async IAsyncEnumerable<T> FromTasksResolveOrder<T>(IReadOnlyList<Task<T?>> tasks)
        where T : class
    {
        var pending = tasks.ToList();
        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending).ConfigureAwait(true);
            pending.Remove(done);
            if (await done.ConfigureAwait(true) is { } value)
            {
                yield return value;
            }
        }
    }
}
