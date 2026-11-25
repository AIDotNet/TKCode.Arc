// Ported from vscode (src/vs/editor/common/viewModel/viewModelDecorations.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - upstream reads filterValidationDecorations(options) and filterFontDecorations(options) from the editor
//    configuration at every query; the editor configuration is not ported, so the constructor takes the two as
//    functions that the view model computes from its options (renderValidationDecorations with readOnly, and
//    effectiveAllowVariableFonts), called at every query as upstream;
//  - the model is the TextModel (upstream's ITextModel); the computer's context is a private class;
//  - IViewDecorationsCollection is ViewDecorationsCollection (InlineDecorations.cs).

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The decorations of the view lines, with a cache of the last viewport's decorations.</summary>
internal sealed class ViewModelDecorations : IDisposable
{
    private readonly int _editorId;
    private readonly IViewModelLines _linesCollection;
    private readonly Func<bool> _filterValidationDecorations;
    private readonly Func<bool> _filterFontDecorations;

    private readonly InlineModelDecorationsComputer _inlineDecorationsComputer;

    private ViewDecorationsCollection? _cachedModelDecorationsResolver;
    private TextRange? _cachedModelDecorationsResolverViewRange;

    /// <param name="editorId">The owner id whose decorations (and the ones of no owner) are shown.</param>
    /// <param name="model">The text model.</param>
    /// <param name="filterValidationDecorations">Whether validation decorations are left out (upstream's filterValidationDecorations(options)).</param>
    /// <param name="filterFontDecorations">Whether font decorations are left out (upstream's filterFontDecorations(options)).</param>
    /// <param name="linesCollection">The view lines.</param>
    /// <param name="coordinatesConverter">Converts between model and view coordinates.</param>
    public ViewModelDecorations(
        int editorId,
        TextModel model,
        Func<bool> filterValidationDecorations,
        Func<bool> filterFontDecorations,
        IViewModelLines linesCollection,
        ICoordinatesConverter coordinatesConverter)
    {
        _editorId = editorId;
        _filterValidationDecorations = filterValidationDecorations;
        _filterFontDecorations = filterFontDecorations;
        _linesCollection = linesCollection;
        _inlineDecorationsComputer = new InlineModelDecorationsComputer(new Context(this), model, coordinatesConverter);
        _cachedModelDecorationsResolver = null;
        _cachedModelDecorationsResolverViewRange = null;
    }

    private void ClearCachedModelDecorationsResolver()
    {
        _cachedModelDecorationsResolver = null;
        _cachedModelDecorationsResolverViewRange = null;
    }

    public void Dispose()
    {
        _inlineDecorationsComputer.Reset();
        ClearCachedModelDecorationsResolver();
    }

    public void Reset()
    {
        _inlineDecorationsComputer.Reset();
        ClearCachedModelDecorationsResolver();
    }

    public void OnModelDecorationsChanged()
    {
        _inlineDecorationsComputer.OnModelDecorationsChanged();
        ClearCachedModelDecorationsResolver();
    }

    public void OnLineMappingChanged()
    {
        _inlineDecorationsComputer.OnLineMappingChanged();

        ClearCachedModelDecorationsResolver();
    }

    public IReadOnlyList<ViewModelDecoration> GetMinimapDecorationsInRange(TextRange range)
    {
        return _inlineDecorationsComputer.GetDecorations(range, true, false).Decorations;
    }

    public ViewDecorationsCollection GetDecorationsViewportData(TextRange viewRange)
    {
        var cacheIsValid = _cachedModelDecorationsResolver is not null;
        cacheIsValid = cacheIsValid && _cachedModelDecorationsResolverViewRange == viewRange;
        if (!cacheIsValid)
        {
            _cachedModelDecorationsResolver = _inlineDecorationsComputer.GetDecorations(viewRange, false, false);
            _cachedModelDecorationsResolverViewRange = viewRange;
        }

        return _cachedModelDecorationsResolver!;
    }

    public ViewDecorationsCollection GetDecorationsOnLine(int lineNumber, bool onlyMinimapDecorations = false, bool onlyMarginDecorations = false)
    {
        var range = new TextRange(lineNumber, _linesCollection.GetViewLineMinColumn(lineNumber), lineNumber, _linesCollection.GetViewLineMaxColumn(lineNumber));
        return _inlineDecorationsComputer.GetDecorations(range, onlyMinimapDecorations, onlyMarginDecorations);
    }

    private sealed class Context(ViewModelDecorations owner) : IInlineModelDecorationsComputerContext
    {
        public IReadOnlyList<IModelDecoration> GetModelDecorations(TextRange viewRange, bool onlyMinimapDecorations, bool onlyMarginDecorations) =>
            owner._linesCollection.GetDecorationsInRange(
                viewRange,
                owner._editorId,
                owner._filterValidationDecorations(),
                owner._filterFontDecorations(),
                onlyMinimapDecorations,
                onlyMarginDecorations);
    }
}
