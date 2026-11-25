// Ported from vscode (src/vs/editor/common/cursor/cursorContext.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the model is the TextModel (upstream's ITextModel); there is no brand field.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>What the cursors work on: the model, the view model, the converter between the two and the configuration.</summary>
internal sealed class CursorContext
{
    public CursorContext(TextModel model, ICursorSimpleModel viewModel, ICoordinatesConverter coordinatesConverter, CursorConfiguration cursorConfig)
    {
        Model = model;
        ViewModel = viewModel;
        CoordinatesConverter = coordinatesConverter;
        CursorConfig = cursorConfig;
    }

    public TextModel Model { get; }

    public ICursorSimpleModel ViewModel { get; }

    public ICoordinatesConverter CoordinatesConverter { get; }

    public CursorConfiguration CursorConfig { get; }
}
