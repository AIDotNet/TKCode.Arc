// Ported from vscode (commit b57d5c9e), MIT license:
//  - src/vs/editor/contrib/inlayHints/browser/inlayHints.ts (InlayHintAnchor, InlayHintItem, InlayHintsFragments);
//  - the decoration part of src/vs/editor/contrib/inlayHints/browser/inlayHintsController.ts (_updateHintsDecorators,
//    _getHintsRanges, fixSpace's purpose, the render modes and the 'on' / 'onUnlessPressed' / 'offUnlessPressed' /
//    'off' setting) as InlayHintsDecorations;
//  - RenderedInlayHintLabelPart.
//
// Differences:
//  - the hint is a record, so a resolve replaces InlayHintItem.Hint instead of mutating it;
//  - the decorations carry class names (InlayHintsDecorations.*ClassName) that the view styles (foreground,
//    background, link underline) instead of dynamic CSS rules; there is no separate inlay hint font family or size;
//  - padding whitespace is an injected no-break space (upstream: an empty injected text a third of the font size wide);
//    label spaces are kept (upstream replaces them with no-break spaces against the DOM's whitespace collapsing);
//  - the fixed lengths that keep hints before the cursor stable while typing are not ported (the hints are redrawn when
//    the provider answers);
//  - the word range of the anchor uses the default word definition, the token fallback the model's line tokens.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.InlayHints;

/// <summary>Upstream's editor.inlayHints.enabled.</summary>
internal enum InlayHintsEnabled
{
    On,
    OnUnlessPressed,
    OffUnlessPressed,
    Off,
}

/// <summary>Where a hint is attached: before or after a range (upstream's InlayHintAnchor).</summary>
internal sealed record InlayHintAnchor(TextRange Range, InlayHintDirection Direction);

/// <summary>Which side of the anchor range the hint is drawn on.</summary>
internal enum InlayHintDirection
{
    Before,
    After,
}

/// <summary>A hint with its anchor and provider (upstream's InlayHintItem).</summary>
internal sealed class InlayHintItem
{
    private bool _isResolved;
    private Task? _currentResolve;

    public InlayHintItem(InlayHint hint, InlayHintAnchor anchor, IInlayHintsProvider provider)
    {
        Hint = hint;
        Anchor = anchor;
        Provider = provider;
    }

    /// <summary>The hint; a resolve replaces it with the resolved one.</summary>
    public InlayHint Hint { get; private set; }

    public InlayHintAnchor Anchor { get; }

    public IInlayHintsProvider Provider { get; }

    public InlayHintItem With(InlayHintAnchor anchor) => new(Hint, anchor, Provider)
    {
        _isResolved = _isResolved,
        _currentResolve = _currentResolve,
    };

    /// <summary>The label as parts (a string label is one part).</summary>
    public IReadOnlyList<InlayHintLabelPart> GetLabelParts() =>
        Hint.LabelParts is { Count: > 0 } parts ? parts : [new InlayHintLabelPart(Hint.Label ?? "")];

    public async Task Resolve(CancellationToken token)
    {
        if (!Provider.SupportsResolveInlayHint)
        {
            return;
        }

        if (_currentResolve is { } current)
        {
            // wait for an active resolve operation and try again when that's done.
            await current.ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await Resolve(token).ConfigureAwait(true);
            return;
        }

        if (!_isResolved)
        {
            _currentResolve = DoResolve(token);
            try
            {
                await _currentResolve.ConfigureAwait(true);
            }
            finally
            {
                _currentResolve = null;
            }
        }
    }

    private async Task DoResolve(CancellationToken token)
    {
        try
        {
            var newHint = await Provider.ResolveInlayHint(Hint, token).ConfigureAwait(true);
            Hint = Hint with
            {
                Tooltip = newHint?.Tooltip ?? Hint.Tooltip,
                Label = newHint is { } n && (n.Label is not null || n.LabelParts is not null) ? n.Label : Hint.Label,
                LabelParts = newHint is { } m && (m.Label is not null || m.LabelParts is not null) ? m.LabelParts : Hint.LabelParts,
                TextEdits = newHint?.TextEdits ?? Hint.TextEdits,
            };
            _isResolved = true;
        }
        catch (Exception err) when (err is not OperationCanceledException)
        {
            UnexpectedErrors.Report(err);
            _isResolved = false;
        }
    }
}

/// <summary>A label part as it is drawn: the item and the index of the part (upstream's RenderedInlayHintLabelPart).</summary>
internal sealed record RenderedInlayHintLabelPart(InlayHintItem Item, int Index)
{
    public InlayHintLabelPart Part => Item.GetLabelParts()[Index];
}

/// <summary>The hints of some ranges from all providers (upstream's InlayHintsFragments).</summary>
internal sealed class InlayHintsFragments : IDisposable
{
    private readonly List<Action> _disposables = new();

    private InlayHintsFragments(IReadOnlyList<TextRange> ranges, List<(InlayHintList List, IInlayHintsProvider Provider)> data, TextModel model)
    {
        Ranges = ranges;
        var providers = new HashSet<IInlayHintsProvider>(ReferenceEqualityComparer.Instance);
        var items = new List<InlayHintItem>();
        foreach (var (list, provider) in data)
        {
            if (list.Dispose is { } dispose)
            {
                _disposables.Add(dispose);
            }

            providers.Add(provider);
            foreach (var hint in list.Hints)
            {
                // compute the range to which the item should be attached to
                var position = model.ValidatePosition(hint.Position);
                var wordRange = GetRangeAtPosition(model, position);
                TextRange range;
                InlayHintDirection direction;
                if (wordRange.Start.IsBefore(position))
                {
                    range = new TextRange(wordRange.Start, position);
                    direction = InlayHintDirection.After;
                }
                else
                {
                    range = new TextRange(position, wordRange.End);
                    direction = InlayHintDirection.Before;
                }

                items.Add(new InlayHintItem(hint, new InlayHintAnchor(range, direction), provider));
            }
        }

        // a stable sort by position (upstream's Array.prototype.sort is stable)
        Items = items.Select((item, index) => (item, index))
            .OrderBy(static p => p.item.Hint.Position)
            .ThenBy(static p => p.index)
            .Select(static p => p.item)
            .ToList();
        Providers = providers;
    }

    public IReadOnlyList<InlayHintItem> Items { get; }

    public IReadOnlyList<TextRange> Ranges { get; }

    public IReadOnlySet<IInlayHintsProvider> Providers { get; }

    public static async Task<InlayHintsFragments> Create(LanguageFeatureRegistry<IInlayHintsProvider> registry, TextModel model, IReadOnlyList<TextRange> ranges, CancellationToken token)
    {
        var data = new List<(InlayHintList, IInlayHintsProvider)>();
        var ordered = registry.Ordered(model).Reverse().ToList();
        var tasks = new List<Task>();
        foreach (var provider in ordered)
        {
            foreach (var range in ranges)
            {
                tasks.Add(Provide(provider, range));
            }
        }

        async Task Provide(IInlayHintsProvider provider, TextRange range)
        {
            try
            {
                var result = await provider.ProvideInlayHints(model, range, token).ConfigureAwait(true);
                if (result is { Hints.Count: > 0 } || ProvidesChangeEvents(provider))
                {
                    data.Add((result ?? new InlayHintList([]), provider));
                }
            }
            catch (Exception err) when (err is not OperationCanceledException)
            {
                UnexpectedErrors.Report(err);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(true);

        if (token.IsCancellationRequested || model.IsDisposed())
        {
            foreach (var (list, _) in data)
            {
                list.Dispose?.Invoke();
            }

            throw new OperationCanceledException();
        }

        return new InlayHintsFragments(ranges, data, model);
    }

    // upstream keeps an empty list of a provider that has onDidChangeInlayHints; here every provider may raise the event
    private static bool ProvidesChangeEvents(IInlayHintsProvider provider) => true;

    public void Dispose()
    {
        foreach (var dispose in _disposables)
        {
            dispose();
        }

        _disposables.Clear();
    }

    private static TextRange GetRangeAtPosition(TextModel model, TextPosition position)
    {
        var line = position.LineNumber;
        var word = WordHelper.GetWordAtText(position.Column, WordHelper.DefaultWordRegExp, model.GetLineContent(line), 0);
        if (word is not null)
        {
            // always prefer the word range
            return new TextRange(line, word.StartColumn, line, word.EndColumn);
        }

        model.Tokenization.TokenizeIfCheap(line);
        var tokens = model.Tokenization.GetLineTokens(line);
        var offset = position.Column - 1;
        var idx = tokens.FindTokenIndexAtOffset(offset);

        var start = tokens.GetStartOffset(idx);
        var end = tokens.GetEndOffset(idx);

        if (end - start == 1)
        {
            // single character token, when at its end try leading/trailing token instead
            if (start == offset && idx > 1)
            {
                // leading token
                start = tokens.GetStartOffset(idx - 1);
                end = tokens.GetEndOffset(idx - 1);
            }
            else if (end == offset && idx < tokens.GetCount() - 1)
            {
                // trailing token
                start = tokens.GetStartOffset(idx + 1);
                end = tokens.GetEndOffset(idx + 1);
            }
        }

        return new TextRange(line, start + 1, line, end + 1);
    }
}

/// <summary>A decoration of a hint and what it draws (upstream's InlayHintDecorationRenderInfo).</summary>
internal sealed record InlayHintDecoration(InlayHintItem Item, ModelDeltaDecoration Decoration);

/// <summary>Turns hints into injected text decorations (upstream's _updateHintsDecorators).</summary>
internal static class InlayHintsDecorations
{
    public const int MaxDecorators = 1500;

    /// <summary>Every hint label part.</summary>
    public const string ClassName = "inlayHint";

    /// <summary>Parameter hints (with <see cref="ClassName"/>).</summary>
    public const string ParameterClassName = "inlayHintParameter";

    /// <summary>Type hints (with <see cref="ClassName"/>).</summary>
    public const string TypeClassName = "inlayHintType";

    /// <summary>A part with a command or location under the pointer.</summary>
    public const string ActiveLinkClassName = "inlayHintActiveLink";

    /// <summary>The whitespace before or after a padded hint.</summary>
    public const string PaddingClassName = "inlayHintPadding";

    /// <summary>The marker of the whitespace's injected text (upstream's _whitespaceData).</summary>
    public static readonly object WhitespaceData = new();

    /// <summary>Upstream's _getHintsRanges: the visible ranges grown by 30 lines, merged when they touch.</summary>
    public static List<TextRange> GetHintsRanges(TextModel model, IEnumerable<TextRange> visibleRanges)
    {
        const int extra = 30;
        var result = new List<TextRange>();
        foreach (var range in visibleRanges.OrderBy(static r => r.Start))
        {
            var extendedRange = model.ValidateRange(new TextRange(range.StartLineNumber - extra, range.StartColumn, range.EndLineNumber + extra, range.EndColumn));
            if (result.Count == 0 || !TextRange.AreIntersectingOrTouching(result[^1], extendedRange))
            {
                result.Add(extendedRange);
            }
            else
            {
                result[^1] = result[^1].PlusRange(extendedRange);
            }
        }

        return result;
    }

    /// <summary>
    /// The decorations of <paramref name="items"/>: padding whitespace, one injected text per label part (cut at
    /// <paramref name="maximumLength"/> characters per line), none when <paramref name="visible"/> is false.
    /// </summary>
    public static List<InlayHintDecoration> Create(IReadOnlyList<InlayHintItem> items, bool visible, int maximumLength, RenderedInlayHintLabelPart? activePart)
    {
        var newDecorationsData = new List<InlayHintDecoration>();

        void AddInjectedText(InlayHintItem item, string className, string content, InjectedTextCursorStops cursorStops, object? attachedData)
        {
            var opts = new ModelDecorationInjectedTextOptions(content)
            {
                InlineClassNameAffectsLetterSpacing = true,
                InlineClassName = className,
                CursorStops = cursorStops,
                AttachedData = attachedData,
            };
            var range = item.Anchor.Range;
            var options = new ModelDecorationOptions("InlayHint")
            {
                ShowIfCollapsed = range.IsEmpty,
                CollapseOnReplaceEdit = !range.IsEmpty,
                Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
                Before = visible && item.Anchor.Direction == InlayHintDirection.Before ? opts : null,
                After = visible && item.Anchor.Direction == InlayHintDirection.After ? opts : null,
            };
            newDecorationsData.Add(new InlayHintDecoration(item, new ModelDeltaDecoration(range, options)));
        }

        void AddInjectedWhitespace(InlayHintItem item, bool isLast) =>
            AddInjectedText(item, PaddingClassName, " ", isLast ? InjectedTextCursorStops.Right : InjectedTextCursorStops.None, WhitespaceData);

        var currentLine = 0;
        var currentTotalLen = 0;
        foreach (var item in items)
        {
            if (currentLine != item.Anchor.Range.StartLineNumber)
            {
                currentLine = item.Anchor.Range.StartLineNumber;
                currentTotalLen = 0;
            }

            if (maximumLength > 0 && currentTotalLen > maximumLength)
            {
                continue;
            }

            // whitespace leading the actual label
            if (item.Hint.PaddingLeft)
            {
                AddInjectedWhitespace(item, false);
            }

            // the label with its parts
            var parts = item.GetLabelParts();
            var kindClassName = item.Hint.Kind switch
            {
                InlayHintKind.Parameter => " " + ParameterClassName,
                InlayHintKind.Type => " " + TypeClassName,
                _ => "",
            };
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var isLast = i == parts.Count - 1;

                var className = ClassName + kindClassName;
                if ((part.Command is not null || part.Location is not null) && activePart is { } active && ReferenceEquals(active.Item, item) && active.Index == i)
                {
                    // active link!
                    className += " " + ActiveLinkClassName;
                }

                var textLabel = part.Label;
                currentTotalLen += textLabel.Length;
                var tooLong = false;
                var over = maximumLength != 0 ? currentTotalLen - maximumLength : 0;
                if (over > 0)
                {
                    textLabel = textLabel[..Math.Max(0, textLabel.Length - over)] + "…";
                    tooLong = true;
                }

                AddInjectedText(
                    item,
                    className,
                    textLabel,
                    isLast && !item.Hint.PaddingRight ? InjectedTextCursorStops.Right : InjectedTextCursorStops.None,
                    new RenderedInlayHintLabelPart(item, i));

                if (tooLong)
                {
                    break;
                }
            }

            // whitespace trailing the actual label
            if (item.Hint.PaddingRight)
            {
                AddInjectedWhitespace(item, true);
            }

            if (newDecorationsData.Count > MaxDecorators)
            {
                break;
            }
        }

        return newDecorationsData;
    }
}
