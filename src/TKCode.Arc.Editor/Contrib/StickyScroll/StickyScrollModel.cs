// Ported from vscode (src/vs/editor/contrib/stickyScroll/browser/stickyScrollElement.ts and stickyScrollModelProvider.ts,
// commit b57d5c9e), MIT license.
// Differences:
//  - the model provider takes the text model and the services, not the editor; it has no 300 ms Delayer of its own
//    (the CodeEditor's controller debounces its updates) and the providers run in order, each awaited;
//  - the folding providers use sticky scroll's own copy of the folding regions (StickyFoldingRanges.cs); the syntax
//    folding provider is chosen per update (upstream re-creates it on the registry's onDidChange);
//  - upstream's 'outlineModel' | 'foldingProviderModel' | 'indentationModel' is StickyScrollDefaultModel.

using TKCode.Arc.Editor.Contrib.DocumentSymbols;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.StickyScroll;

/// <summary>A range of lines of a scope: the line that stays and the last line (upstream's StickyRange).</summary>
internal readonly record struct StickyRange(int StartLineNumber, int EndLineNumber);

/// <summary>A scope and its nested scopes, sorted by start line (upstream's StickyElement).</summary>
internal sealed class StickyElement(StickyRange? range, List<StickyElement> children, StickyElement? parent)
{
    /// <summary>The lines the scope spans; null for the root.</summary>
    public StickyRange? Range { get; } = range;

    /// <summary>The nested scopes, by start line.</summary>
    public List<StickyElement> Children { get; } = children;

    public StickyElement? Parent { get; } = parent;
}

/// <summary>The scopes of a model version (upstream's StickyModel).</summary>
internal sealed record StickyModel(Uri? Uri, int Version, StickyElement? Element, string? OutlineProviderId);

/// <summary>Where the scopes come from first (upstream's stickyScroll.defaultModel option).</summary>
internal enum StickyScrollDefaultModel
{
    /// <summary>The document symbols, then the folding ranges, then indentation.</summary>
    OutlineModel,

    /// <summary>The folding range providers, then indentation.</summary>
    FoldingProviderModel,

    /// <summary>Indentation only.</summary>
    IndentationModel,
}

/// <summary>Computes the sticky model from the first provider that gives one (upstream's StickyModelProvider).</summary>
internal sealed class StickyModelProvider
{
    private readonly List<StickyModelCandidateProvider> _modelProviders = [];

    public StickyModelProvider(TextModel model, LanguageFeatures languageFeatures, LanguageConfigurationService languageConfigurationService, StickyScrollDefaultModel defaultModel)
    {
        switch (defaultModel)
        {
            case StickyScrollDefaultModel.OutlineModel:
                _modelProviders.Add(new StickyModelFromCandidateOutlineProvider(model, languageFeatures));
                _modelProviders.Add(new StickyModelFromCandidateSyntaxFoldingProvider(model, languageFeatures));
                _modelProviders.Add(new StickyModelFromCandidateIndentationFoldingProvider(model, languageConfigurationService));
                break;
            case StickyScrollDefaultModel.FoldingProviderModel:
                _modelProviders.Add(new StickyModelFromCandidateSyntaxFoldingProvider(model, languageFeatures));
                _modelProviders.Add(new StickyModelFromCandidateIndentationFoldingProvider(model, languageConfigurationService));
                break;
            default:
                _modelProviders.Add(new StickyModelFromCandidateIndentationFoldingProvider(model, languageConfigurationService));
                break;
        }
    }

    /// <summary>The sticky model of the first provider that has a valid one; null when cancelled or none has (upstream's update).</summary>
    public async Task<StickyModel?> Update(CancellationToken token)
    {
        try
        {
            foreach (var modelProvider in _modelProviders)
            {
                var status = await modelProvider.ComputeStickyModel(token).ConfigureAwait(true);
                switch (status)
                {
                    case StickyModelStatus.Canceled:
                        return null;
                    case StickyModelStatus.Valid:
                        return modelProvider.StickyModel;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
            return null;
        }
    }
}

/// <summary>upstream's Status.</summary>
internal enum StickyModelStatus
{
    Valid,
    Invalid,
    Canceled,
}

/// <summary>One source of scopes (upstream's StickyModelCandidateProvider).</summary>
internal abstract class StickyModelCandidateProvider
{
    protected StickyModelCandidateProvider(TextModel model)
    {
        Model = model;
    }

    protected TextModel Model { get; }

    /// <summary>The last model computed, or null.</summary>
    public StickyModel? StickyModel { get; protected set; }

    /// <summary>Computes the sticky model and says whether it is valid (upstream's computeStickyModel).</summary>
    public async Task<StickyModelStatus> ComputeStickyModel(CancellationToken token)
    {
        if (token.IsCancellationRequested || !IsProviderValid())
        {
            StickyModel = null;
            return StickyModelStatus.Invalid;
        }

        try
        {
            var providerModel = await CreateModelFromProvider(token).ConfigureAwait(true);
            if (!IsModelValid(providerModel))
            {
                StickyModel = null;
                return StickyModelStatus.Invalid;
            }

            if (token.IsCancellationRequested)
            {
                return StickyModelStatus.Canceled;
            }

            StickyModel = CreateStickyModel(providerModel!);
            return StickyModelStatus.Valid;
        }
        catch (OperationCanceledException)
        {
            return StickyModelStatus.Canceled;
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
            return StickyModelStatus.Canceled;
        }
    }

    protected virtual bool IsProviderValid() => true;

    protected abstract bool IsModelValid(object? model);

    protected abstract Task<object?> CreateModelFromProvider(CancellationToken token);

    protected abstract StickyModel CreateStickyModel(object model);

    protected static int Comparator(StickyRange range1, StickyRange range2) =>
        range1.StartLineNumber != range2.StartLineNumber
            ? range1.StartLineNumber - range2.StartLineNumber
            : range2.EndLineNumber - range1.EndLineNumber;
}

/// <summary>Scopes from the document symbols (upstream's StickyModelFromCandidateOutlineProvider).</summary>
internal sealed class StickyModelFromCandidateOutlineProvider(TextModel model, LanguageFeatures languageFeatures) : StickyModelCandidateProvider(model)
{
    protected override async Task<object?> CreateModelFromProvider(CancellationToken token) =>
        await OutlineModel.Create(languageFeatures.DocumentSymbolProvider, Model, token).ConfigureAwait(true);

    protected override bool IsModelValid(object? model) => model is OutlineModel { Children.Count: > 0 };

    protected override StickyModel CreateStickyModel(object model)
    {
        var (stickyOutlineElement, providerId) = StickyModelFromOutlineModel((OutlineModel)model, StickyModel?.OutlineProviderId);
        return new StickyModel(Model.Uri, Model.GetVersionId(), stickyOutlineElement, providerId);
    }

    private static (StickyElement Element, string? ProviderId) StickyModelFromOutlineModel(OutlineModel outlineModel, string? preferredProvider)
    {
        IEnumerable<OutlineElement> outlineElements;
        // When several possible outline providers
        if (outlineModel.Children.Values.FirstOrDefault() is OutlineGroup)
        {
            var provider = outlineModel.Children.Values.OfType<OutlineGroup>().FirstOrDefault(group => group.Id == preferredProvider);
            if (provider is not null)
            {
                outlineElements = provider.Children.Values;
            }
            else
            {
                var tempId = string.Empty;
                var maxTotalSumOfRanges = -1;
                OutlineGroup? optimalOutlineGroup = null;
                foreach (var outlineGroup in outlineModel.Children.Values.OfType<OutlineGroup>())
                {
                    var totalSumRanges = FindSumOfRangesOfGroup(outlineGroup);
                    if (totalSumRanges > maxTotalSumOfRanges)
                    {
                        optimalOutlineGroup = outlineGroup;
                        maxTotalSumOfRanges = totalSumRanges;
                        tempId = outlineGroup.Id;
                    }
                }

                preferredProvider = tempId;
                outlineElements = optimalOutlineGroup!.Children.Values;
            }
        }
        else
        {
            outlineElements = outlineModel.Children.Values.OfType<OutlineElement>();
        }

        var outlineElementsArray = outlineElements.ToList();
        outlineElementsArray.Sort((element1, element2) => Comparator(
            new StickyRange(element1.Symbol.Range.StartLineNumber, element1.Symbol.Range.EndLineNumber),
            new StickyRange(element2.Symbol.Range.StartLineNumber, element2.Symbol.Range.EndLineNumber)));
        var stickyChildren = new List<StickyElement>();
        foreach (var outlineElement in outlineElementsArray)
        {
            stickyChildren.Add(StickyModelFromOutlineElement(outlineElement, outlineElement.Symbol.SelectionRange.StartLineNumber));
        }

        return (new StickyElement(null, stickyChildren, null), preferredProvider);
    }

    private static StickyElement StickyModelFromOutlineElement(OutlineElement outlineElement, int previousStartLine)
    {
        var children = new List<StickyElement>();
        foreach (var child in outlineElement.Children.Values)
        {
            if (child.Symbol.SelectionRange.StartLineNumber != child.Symbol.Range.EndLineNumber)
            {
                if (child.Symbol.SelectionRange.StartLineNumber != previousStartLine)
                {
                    children.Add(StickyModelFromOutlineElement(child, child.Symbol.SelectionRange.StartLineNumber));
                }
                else
                {
                    foreach (var subchild in child.Children.Values)
                    {
                        children.Add(StickyModelFromOutlineElement(subchild, child.Symbol.SelectionRange.StartLineNumber));
                    }
                }
            }
        }

        children.Sort(static (child1, child2) => Comparator(child1.Range!.Value, child2.Range!.Value));
        var range = new StickyRange(outlineElement.Symbol.SelectionRange.StartLineNumber, outlineElement.Symbol.Range.EndLineNumber);
        return new StickyElement(range, children, null);
    }

    private static int FindSumOfRangesOfGroup(TreeElement outline)
    {
        var res = 0;
        foreach (var child in outline.ChildElements)
        {
            res += FindSumOfRangesOfGroup(child);
        }

        return outline is OutlineElement element
            ? res + element.Symbol.Range.EndLineNumber - element.Symbol.SelectionRange.StartLineNumber
            : res;
    }
}

/// <summary>Scopes from folding regions (upstream's StickyModelFromCandidateFoldingProvider).</summary>
internal abstract class StickyModelFromCandidateFoldingProvider(TextModel model) : StickyModelCandidateProvider(model)
{
    protected override bool IsModelValid(object? model) => model is StickyFoldingRegions;

    protected override StickyModel CreateStickyModel(object model) =>
        new(Model.Uri, Model.GetVersionId(), FromFoldingRegions((StickyFoldingRegions)model), null);

    private static StickyElement FromFoldingRegions(StickyFoldingRegions foldingRegions)
    {
        var length = foldingRegions.Length;
        var orderedStickyElements = new List<StickyElement>();

        // The root sticky outline element
        var stickyOutlineElement = new StickyElement(null, [], null);

        for (var i = 0; i < length; i++)
        {
            // Finding the parent index of the current range
            var parentIndex = foldingRegions.GetParentIndex(i);

            // Access the reference of the parent node, or the root
            var parentNode = parentIndex != -1 ? orderedStickyElements[parentIndex] : stickyOutlineElement;

            var child = new StickyElement(
                new StickyRange(foldingRegions.GetStartLineNumber(i), foldingRegions.GetEndLineNumber(i) + 1),
                [],
                parentNode);
            parentNode.Children.Add(child);
            orderedStickyElements.Add(child);
        }

        return stickyOutlineElement;
    }
}

/// <summary>Scopes from indentation (upstream's StickyModelFromCandidateIndentationFoldingProvider).</summary>
internal sealed class StickyModelFromCandidateIndentationFoldingProvider(TextModel model, LanguageConfigurationService languageConfigurationService)
    : StickyModelFromCandidateFoldingProvider(model)
{
    protected override Task<object?> CreateModelFromProvider(CancellationToken token) =>
        Task.FromResult<object?>(StickyIndentRanges.Compute(Model, languageConfigurationService));
}

/// <summary>Scopes from the folding range providers (upstream's StickyModelFromCandidateSyntaxFoldingProvider).</summary>
internal sealed class StickyModelFromCandidateSyntaxFoldingProvider(TextModel model, LanguageFeatures languageFeatures)
    : StickyModelFromCandidateFoldingProvider(model)
{
    protected override bool IsProviderValid() => languageFeatures.FoldingRangeProvider.Has(Model);

    protected override async Task<object?> CreateModelFromProvider(CancellationToken token) =>
        await StickySyntaxRanges.Compute(languageFeatures.FoldingRangeProvider.Ordered(Model), Model, token).ConfigureAwait(true);
}
