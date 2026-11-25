// Ported from vscode (src/vs/editor/common/coordinatesConverter.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the optional parameters have the defaults upstream's implementations give them: no affinity for a position, Left
//    for a range (ViewModelLinesFromProjectedModel.convertModelRangeToViewRange), false for the two flags;
//  - IdentityCoordinatesConverter works on the TextModel (upstream's ITextModel).

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>Converts positions and ranges between the model and the view.</summary>
internal interface ICoordinatesConverter
{
    // View -> Model conversion and related methods
    TextPosition ConvertViewPositionToModelPosition(TextPosition viewPosition);

    TextRange ConvertViewRangeToModelRange(TextRange viewRange);

    TextPosition ValidateViewPosition(TextPosition viewPosition, TextPosition expectedModelPosition);

    TextRange ValidateViewRange(TextRange viewRange, TextRange expectedModelRange);

    // Model -> View conversion and related methods

    /// <param name="modelPosition">The model position.</param>
    /// <param name="affinity">Which side the position sticks to.</param>
    /// <param name="allowZeroLineNumber">Should it return 0 when there are hidden lines at the top and the position is in the hidden area?</param>
    /// <param name="belowHiddenRanges">When the model position is in a hidden area, should it return the first view position after or before?</param>
    TextPosition ConvertModelPositionToViewPosition(
        TextPosition modelPosition,
        PositionAffinity affinity = PositionAffinity.None,
        bool allowZeroLineNumber = false,
        bool belowHiddenRanges = false);

    /// <param name="modelRange">The model range.</param>
    /// <param name="affinity">Only has an effect if the range is empty.</param>
    TextRange ConvertModelRangeToViewRange(TextRange modelRange, PositionAffinity affinity = PositionAffinity.Left);

    bool ModelPositionIsVisible(TextPosition modelPosition);

    int GetModelLineViewLineCount(int modelLineNumber);

    int GetViewLineNumberOfModelPosition(int modelLineNumber, int modelColumn);
}

/// <summary>The coordinates converter of a view that shows the model as it is.</summary>
internal sealed class IdentityCoordinatesConverter : ICoordinatesConverter
{
    private readonly TextModel _model;

    public IdentityCoordinatesConverter(TextModel model)
    {
        _model = model;
    }

    private TextPosition ValidPosition(TextPosition pos) => _model.ValidatePosition(pos);

    private TextRange ValidRange(TextRange range) => _model.ValidateRange(range);

    // View -> Model conversion and related methods

    public TextPosition ConvertViewPositionToModelPosition(TextPosition viewPosition) => ValidPosition(viewPosition);

    public TextRange ConvertViewRangeToModelRange(TextRange viewRange) => ValidRange(viewRange);

    public TextPosition ValidateViewPosition(TextPosition viewPosition, TextPosition expectedModelPosition) => ValidPosition(expectedModelPosition);

    public TextRange ValidateViewRange(TextRange viewRange, TextRange expectedModelRange) => ValidRange(expectedModelRange);

    // Model -> View conversion and related methods

    public TextPosition ConvertModelPositionToViewPosition(
        TextPosition modelPosition,
        PositionAffinity affinity = PositionAffinity.None,
        bool allowZeroLineNumber = false,
        bool belowHiddenRanges = false) => ValidPosition(modelPosition);

    public TextRange ConvertModelRangeToViewRange(TextRange modelRange, PositionAffinity affinity = PositionAffinity.Left) => ValidRange(modelRange);

    public bool ModelPositionIsVisible(TextPosition modelPosition)
    {
        var lineCount = _model.GetLineCount();
        if (modelPosition.LineNumber < 1 || modelPosition.LineNumber > lineCount)
        {
            // invalid arguments
            return false;
        }

        return true;
    }

    public bool ModelRangeIsVisible(TextRange modelRange)
    {
        var lineCount = _model.GetLineCount();
        if (modelRange.StartLineNumber < 1 || modelRange.StartLineNumber > lineCount)
        {
            // invalid arguments
            return false;
        }

        if (modelRange.EndLineNumber < 1 || modelRange.EndLineNumber > lineCount)
        {
            // invalid arguments
            return false;
        }

        return true;
    }

    public int GetModelLineViewLineCount(int modelLineNumber) => 1;

    public int GetViewLineNumberOfModelPosition(int modelLineNumber, int modelColumn) => modelLineNumber;
}
