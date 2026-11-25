// Ported from vscode (src/vs/editor/contrib/colorPicker/browser/color.ts and defaultDocumentColorProvider.ts, commit
// b57d5c9e), MIT license.
// Differences:
//  - the default provider computes the colors on the model directly (upstream asks the editor worker);
//  - upstream's 'auto' | 'always' | 'never' setting is DefaultColorDecoratorsEnablement;
//  - only the IColorData collector is ported (the extension host's IExtColorData and the presentations collector of the
//    _executeColorProvider commands are not);
//  - DefaultDocumentColorProviderFeature (registerEditorFeature) is the CodeEditor's color detector registering the
//    provider for '*'.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.ColorPicker;

/// <summary>When the default color decorators show (upstream's editor.defaultColorDecorators).</summary>
internal enum DefaultColorDecoratorsEnablement
{
    /// <summary>Only when no other provider gives colors.</summary>
    Auto,

    /// <summary>Always, with the other providers.</summary>
    Always,

    /// <summary>Never.</summary>
    Never,
}

/// <summary>A color of the document and the provider that found it (upstream's IColorData).</summary>
internal sealed record ColorData(ColorInformation ColorInfo, IDocumentColorProvider Provider);

/// <summary>Collects the colors of a document (upstream's color.ts).</summary>
internal static class DocumentColors
{
    /// <summary>
    /// The colors of the model from every provider (upstream's getColors): the default provider counts only when no other
    /// gave a list (<see cref="DefaultColorDecoratorsEnablement.Auto"/>), always, or never.
    /// </summary>
    public static async Task<IReadOnlyList<ColorData>> GetColors(
        LanguageFeatureRegistry<IDocumentColorProvider> colorProviderRegistry,
        TextModel model,
        CancellationToken token,
        DefaultColorDecoratorsEnablement defaultColorDecoratorsEnablement = DefaultColorDecoratorsEnablement.Auto)
    {
        var validDocumentColorProviderFound = false;
        DefaultDocumentColorProvider? defaultProvider = null;
        var colorData = new List<ColorData>();
        var documentColorProviders = colorProviderRegistry.Ordered(model);
        for (var i = documentColorProviders.Count - 1; i >= 0; i--)
        {
            var provider = documentColorProviders[i];
            if (defaultColorDecoratorsEnablement != DefaultColorDecoratorsEnablement.Always && provider is DefaultDocumentColorProvider defaultColorProvider)
            {
                defaultProvider = defaultColorProvider;
            }
            else
            {
                try
                {
                    if (await Compute(provider, model, token, colorData).ConfigureAwait(true))
                    {
                        validDocumentColorProviderFound = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    UnexpectedErrors.Report(e);
                }
            }
        }

        if (validDocumentColorProviderFound)
        {
            return colorData;
        }

        if (defaultProvider is not null && defaultColorDecoratorsEnablement != DefaultColorDecoratorsEnablement.Never)
        {
            await Compute(defaultProvider, model, token, colorData).ConfigureAwait(true);
            return colorData;
        }

        return [];
    }

    /// <summary>The ways the provider can write a color (upstream's getColorPresentations).</summary>
    public static Task<IReadOnlyList<ColorPresentation>?> GetColorPresentations(TextModel model, ColorInformation colorInfo, IDocumentColorProvider provider, CancellationToken token) =>
        provider.ProvideColorPresentations(model, colorInfo, token);

    // upstream's ColorDataCollector.compute
    private static async Task<bool> Compute(IDocumentColorProvider provider, TextModel model, CancellationToken token, List<ColorData> colors)
    {
        var documentColors = await provider.ProvideDocumentColors(model, token).ConfigureAwait(true);
        if (documentColors is null)
        {
            return false;
        }

        foreach (var colorInfo in documentColors)
        {
            colors.Add(new ColorData(colorInfo, provider));
        }

        return true;
    }
}

/// <summary>Finds CSS colors in any document and offers rgb, hsl and hex presentations (upstream's DefaultDocumentColorProvider).</summary>
internal sealed class DefaultDocumentColorProvider : IDocumentColorProvider
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ColorInformation>?> ProvideDocumentColors(TextModel model, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<ColorInformation>?>(DefaultDocumentColorsComputer.ComputeDefaultDocumentColors(model));

    /// <inheritdoc />
    public Task<IReadOnlyList<ColorPresentation>?> ProvideColorPresentations(TextModel model, ColorInformation colorInfo, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<ColorPresentation>?>(ProvideColorPresentations(colorInfo));

    /// <summary>The rgb, hsl and hex presentations of a color, replacing its range.</summary>
    public static IReadOnlyList<ColorPresentation> ProvideColorPresentations(ColorInformation colorInfo)
    {
        var range = colorInfo.Range;
        var color = CssColor.FromLanguageColor(colorInfo.Color);
        var rgb = CssColor.FormatRgb(color);
        var hsl = CssColor.FormatHsl(color);
        var hex = CssColor.FormatHexA(color, compact: true);

        return
        [
            new ColorPresentation(rgb) { TextEdit = new TextEdit(range, rgb) },
            new ColorPresentation(hsl) { TextEdit = new TextEdit(range, hsl) },
            new ColorPresentation(hex) { TextEdit = new TextEdit(range, hex) },
        ];
    }
}
