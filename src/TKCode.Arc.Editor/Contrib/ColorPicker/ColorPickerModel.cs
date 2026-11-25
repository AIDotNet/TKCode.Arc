// Ported from vscode (src/vs/editor/contrib/colorPicker/browser/colorPickerModel.ts, and createColorHover /
// updateColorPresentations of colorPickerParticipantUtils.ts; commit b57d5c9e), MIT license.
// Differences: the colors are CssRgba values (upstream's Color); the events are .NET events; createColorHover is
// ColorPickerModel.Create, which returns the model with the presentations of the color.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.ColorPicker;

/// <summary>The state of a color picker: the original and current color and how the color is written (upstream's ColorPickerModel).</summary>
internal sealed class ColorPickerModel
{
    private CssRgba _color;
    private IReadOnlyList<ColorPresentation> _colorPresentations;
    private int _presentationIndex;

    public ColorPickerModel(CssRgba color, IReadOnlyList<ColorPresentation> availableColorPresentations, int presentationIndex)
    {
        OriginalColor = color;
        _color = color;
        _colorPresentations = availableColorPresentations;
        _presentationIndex = presentationIndex;
    }

    /// <summary>Raised when the color is pushed to the document (upstream's onColorFlushed).</summary>
    public event Action<CssRgba>? ColorFlushed;

    /// <summary>Raised when the color changed (upstream's onDidChangeColor).</summary>
    public event Action<CssRgba>? DidChangeColor;

    /// <summary>Raised when the presentation changed (upstream's onDidChangePresentation).</summary>
    public event Action<ColorPresentation?>? DidChangePresentation;

    /// <summary>The color when the picker opened.</summary>
    public CssRgba OriginalColor { get; }

    /// <summary>The color now.</summary>
    public CssRgba Color
    {
        get => _color;
        set
        {
            if (_color == value)
            {
                return;
            }

            _color = value;
            DidChangeColor?.Invoke(value);
        }
    }

    /// <summary>The presentation in use, or null when the provider gave none.</summary>
    public ColorPresentation? Presentation =>
        _presentationIndex >= 0 && _presentationIndex < _colorPresentations.Count ? _colorPresentations[_presentationIndex] : null;

    /// <summary>The index of the presentation in use.</summary>
    public int PresentationIndex => _presentationIndex;

    /// <summary>The ways the provider writes the current color.</summary>
    public IReadOnlyList<ColorPresentation> ColorPresentations
    {
        get => _colorPresentations;
        set
        {
            _colorPresentations = value;
            if (_presentationIndex > value.Count - 1)
            {
                _presentationIndex = 0;
            }

            DidChangePresentation?.Invoke(Presentation);
        }
    }

    /// <summary>Uses the next presentation, cycling (upstream's selectNextColorPresentation).</summary>
    public void SelectNextColorPresentation()
    {
        if (_colorPresentations.Count == 0)
        {
            return;
        }

        _presentationIndex = (_presentationIndex + 1) % _colorPresentations.Count;
        FlushColor();
        DidChangePresentation?.Invoke(Presentation);
    }

    /// <summary>
    /// Uses the presentation the document already writes the color with: the same text, else the same function name
    /// (upstream's guessColorPresentation).
    /// </summary>
    public void GuessColorPresentation(string originalText)
    {
        var presentationIndex = -1;
        for (var i = 0; i < _colorPresentations.Count; i++)
        {
            if (originalText.ToLowerInvariant() == _colorPresentations[i].Label)
            {
                presentationIndex = i;
                break;
            }
        }

        if (presentationIndex == -1)
        {
            // check which color presentation text has same prefix as original text's prefix
            var originalTextPrefix = originalText.Split('(')[0].ToLowerInvariant();
            for (var i = 0; i < _colorPresentations.Count; i++)
            {
                if (_colorPresentations[i].Label.ToLowerInvariant().StartsWith(originalTextPrefix, StringComparison.Ordinal))
                {
                    presentationIndex = i;
                    break;
                }
            }
        }

        if (presentationIndex != -1 && presentationIndex != _presentationIndex)
        {
            _presentationIndex = presentationIndex;
            DidChangePresentation?.Invoke(Presentation);
        }
    }

    /// <summary>Pushes the color to the document (upstream's flushColor).</summary>
    public void FlushColor() => ColorFlushed?.Invoke(_color);

    /// <summary>
    /// The picker model of a color in the document (upstream's createColorHover): its presentations from the provider,
    /// the one the document uses selected.
    /// </summary>
    public static async Task<ColorPickerModel> Create(TextModel editorModel, ColorInformation colorInfo, IDocumentColorProvider provider, CancellationToken token)
    {
        var originalText = editorModel.GetValueInRange(colorInfo.Range);
        var color = CssColor.FromLanguageColor(colorInfo.Color);
        var colorPresentations = await DocumentColors.GetColorPresentations(editorModel, colorInfo, provider, token).ConfigureAwait(true);
        var model = new ColorPickerModel(color, [], 0)
        {
            ColorPresentations = colorPresentations ?? [],
        };
        model.GuessColorPresentation(originalText);
        return model;
    }

    /// <summary>Asks the provider how to write <paramref name="color"/> at <paramref name="range"/> (upstream's updateColorPresentations).</summary>
    public async Task UpdateColorPresentations(TextModel editorModel, CssRgba color, TextRange range, IDocumentColorProvider provider, CancellationToken token)
    {
        var colorPresentations = await DocumentColors.GetColorPresentations(
            editorModel,
            new ColorInformation(range, ToLanguageColor(color)),
            provider,
            token).ConfigureAwait(true);
        ColorPresentations = colorPresentations ?? [];
    }

    /// <summary>A CSS color as a language color (channels 0..1).</summary>
    public static LanguageColor ToLanguageColor(CssRgba color) => new(color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A);
}
