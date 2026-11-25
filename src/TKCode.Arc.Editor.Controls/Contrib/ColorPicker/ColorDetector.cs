// Ported from vscode (src/vs/editor/contrib/colorPicker/browser/colorDetector.ts: ColorDetector and
// DecoratorLimitReporter; defaultDocumentColorProvider.ts: DefaultDocumentColorProviderFeature; commit b57d5c9e), MIT
// license.
// Differences:
//  - the option is the CodeEditor property ColorDecorators (upstream also reads the deprecated
//    [languageId].colorDecorators.enable); defaultColorDecorators is 'auto' and colorDecoratorsLimit 500, upstream's
//    defaults;
//  - each color's swatch is the injected text before the color (two no-break spaces, upstream's 0.8em box with 0.2em
//    margins in a 1.2em inline-block) whose inline class name has the color as Background, drawn as a box of
//    InlineBoxSize (CodeEditorView.InlineBoxes.cs) with the ColorDecoratorBorder theme color around it; upstream's
//    DynamicCssRules are style registrations, one per color, released when the colors are computed again;
//  - the recompute delay is upstream's minimum of 1000 ms (upstream learns it from how long the providers take);
//  - the default document color provider is registered for '*' by this contribution (upstream: an editor feature).

using Avalonia;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.ColorPicker;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

/// <summary>Finds the colors of the document and shows a swatch before each (upstream's ColorDetector).</summary>
internal sealed class ColorDetector : ICodeEditorContribution
{
    public const string Id = "editor.contrib.colorDetector";

    /// <summary>The delay before the colors are computed again after an edit (upstream's RECOMPUTE_TIME).</summary>
    public const int RecomputeTime = 1000;

    /// <summary>The most swatches shown (upstream's colorDecoratorsLimit default).</summary>
    public const int ColorDecoratorsLimit = 500;

    /// <summary>What the injected text of a swatch is (upstream: one no-break space 1.2em wide).</summary>
    public const string SwatchContent = "  ";

    /// <summary>The class name every swatch's injected text has (upstream's colorpicker-color-decoration).</summary>
    public const string ColorDecorationClassName = "colorpicker-color-decoration";

    /// <summary>Marks the injected text of a swatch (upstream's ColorDecorationInjectedTextMarker).</summary>
    public static readonly object ColorDecorationInjectedTextMarker = new();

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new ColorDetector(editor), EditorContributionInstantiation.AfterFirstRender);

    // the swatch: 0.8em of a 1.35em line, upstream's colorDecoratorInnerWidthInEm
    private const double SwatchSizeOfLineHeight = 0.8 / 1.35;

    private static readonly ModelDecorationOptions Empty = new("color-detector-data");

    private readonly CodeEditor _editor;
    private readonly IDisposable _defaultProvider;
    private readonly Dictionary<string, ColorData> _colorDatas = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, (string ClassName, IDisposable Registration)> _colorStyles = [];
    private string[] _decorationsIds = [];
    private string[] _colorDecoratorIds = [];
    private DispatcherTimer? _timeoutTimer;
    private CancellationTokenSource? _computeCts;
    private bool _isColorDecoratorsEnabled;
    private bool _listening;

    private ColorDetector(CodeEditor editor)
    {
        _editor = editor;
        _defaultProvider = editor.LanguageFeatures.ColorProvider.Register("*", new DefaultDocumentColorProvider());
        editor.DidChangeModelLanguage += OnLanguageChanged;
        editor.LanguageFeatures.ColorProvider.DidChange += OnProvidersChanged;
        editor.PropertyChanged += OnEditorPropertyChanged;
        _isColorDecoratorsEnabled = editor.ColorDecorators;
        UpdateColors();
    }

    /// <summary>How many colors were found and how many swatches were left out (upstream's limitReporter).</summary>
    internal (int Computed, int? Limited) LimitReporter { get; private set; }

    /// <summary>Raised after the swatches changed (upstream's limitReporter.onDidChange).</summary>
    internal event Action? DidChangeColors;

    /// <summary>The computation that runs or ran last (for tests).</summary>
    internal Task LastCompute { get; private set; } = Task.CompletedTask;

    /// <summary>The colors found, with their providers.</summary>
    internal IReadOnlyCollection<ColorData> ColorDatas => _colorDatas.Values;

    public static ColorDetector? Get(CodeEditor editor) => editor.GetContribution<ColorDetector>(Id);

    public void Dispose()
    {
        Stop();
        RemoveAllDecorations();
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.LanguageFeatures.ColorProvider.DidChange -= OnProvidersChanged;
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        _defaultProvider.Dispose();
    }

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => UpdateColors();

    private void OnProvidersChanged(int count) => UpdateColors();

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != CodeEditor.ColorDecoratorsProperty)
        {
            return;
        }

        var prevIsEnabled = _isColorDecoratorsEnabled;
        _isColorDecoratorsEnabled = _editor.ColorDecorators;
        if (prevIsEnabled != _isColorDecoratorsEnabled)
        {
            if (_isColorDecoratorsEnabled)
            {
                UpdateColors();
            }
            else
            {
                RemoveAllDecorations();
            }
        }
    }

    private void UpdateColors()
    {
        Stop();
        if (!_isColorDecoratorsEnabled)
        {
            RemoveAllDecorations();
            return;
        }

        var model = _editor.Model;
        if (!_editor.LanguageFeatures.ColorProvider.Has(model))
        {
            return;
        }

        _editor.DidChangeModelContent += OnContentChanged;
        _listening = true;
        LastCompute = BeginCompute();
    }

    private void OnContentChanged(ModelContentChangedEvent e)
    {
        if (_timeoutTimer is null)
        {
            _timeoutTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(RecomputeTime), DispatcherPriority.Background, (_, _) =>
            {
                _timeoutTimer?.Stop();
                _timeoutTimer = null;
                LastCompute = BeginCompute();
            });
            _timeoutTimer.Start();
        }
    }

    private async Task BeginCompute()
    {
        _computeCts?.Cancel();
        var cts = _computeCts = new CancellationTokenSource();
        try
        {
            var colors = await DocumentColors.GetColors(_editor.LanguageFeatures.ColorProvider, _editor.Model, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested || _editor.Model.IsDisposed())
            {
                return;
            }

            UpdateDecorations(colors);
            UpdateColorDecorators(colors);
            if (_computeCts == cts)
            {
                _computeCts = null;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }

    private void Stop()
    {
        if (_timeoutTimer is not null)
        {
            _timeoutTimer.Stop();
            _timeoutTimer = null;
        }

        _computeCts?.Cancel();
        _computeCts = null;
        if (_listening)
        {
            _editor.DidChangeModelContent -= OnContentChanged;
            _listening = false;
        }
    }

    private void UpdateDecorations(IReadOnlyList<ColorData> colorDatas)
    {
        var decorations = colorDatas.Select(static c => new ModelDeltaDecoration(c.ColorInfo.Range, Empty)).ToList();
        _decorationsIds = _editor.Model.DeltaDecorations(_decorationsIds, decorations);
        _colorDatas.Clear();
        for (var i = 0; i < _decorationsIds.Length; i++)
        {
            _colorDatas[_decorationsIds[i]] = colorDatas[i];
        }
    }

    private void UpdateColorDecorators(IReadOnlyList<ColorData> colorData)
    {
        var decorations = new List<ModelDeltaDecoration>();
        var used = new HashSet<uint>();
        for (var i = 0; i < colorData.Count && decorations.Count < ColorDecoratorsLimit; i++)
        {
            var rgba = CssColor.FromLanguageColor(colorData[i].ColorInfo.Color);
            var className = GetColorClassName(rgba, used);
            decorations.Add(new ModelDeltaDecoration(colorData[i].ColorInfo.Range, new ModelDecorationOptions("colorDetector")
            {
                Before = new ModelDecorationInjectedTextOptions(SwatchContent)
                {
                    InlineClassName = className + " " + ColorDecorationClassName,
                    InlineClassNameAffectsLetterSpacing = true,
                    AttachedData = ColorDecorationInjectedTextMarker,
                },
            }));
        }

        // release the styles of colors no longer shown (upstream's _colorDecorationClassRefs.clear())
        foreach (var key in _colorStyles.Keys.Where(k => !used.Contains(k)).ToList())
        {
            _colorStyles[key].Registration.Dispose();
            _colorStyles.Remove(key);
        }

        var limited = ColorDecoratorsLimit < colorData.Count ? ColorDecoratorsLimit : (int?)null;
        var changed = LimitReporter != (colorData.Count, limited);
        LimitReporter = (colorData.Count, limited);
        _colorDecoratorIds = _editor.Model.DeltaDecorations(_colorDecoratorIds, decorations);
        if (changed || decorations.Count > 0)
        {
            DidChangeColors?.Invoke();
        }
    }

    // upstream's DynamicCssRules.createClassNameRef({ backgroundColor }): one style per color
    private string GetColorClassName(CssRgba rgba, HashSet<uint> used)
    {
        var alpha = (uint)Math.Round(rgba.A * 255);
        var key = ((uint)rgba.R << 24) | ((uint)rgba.G << 16) | ((uint)rgba.B << 8) | alpha;
        used.Add(key);
        if (_colorStyles.TryGetValue(key, out var existing))
        {
            return existing.ClassName;
        }

        var className = $"colorpicker-color-{key:x8}";
        var brush = new ImmutableSolidColorBrush(Avalonia.Media.Color.FromArgb((byte)alpha, (byte)rgba.R, (byte)rgba.G, (byte)rgba.B));
        var registration = _editor.DecorationStyles.Register(className, new CodeEditorDecorationStyle
        {
            Background = DecorationBrush.From(brush),
            Border = CodeEditorThemeColor.ColorDecoratorBorder,
            BorderThickness = 1,
            InlineBoxSize = SwatchSizeOfLineHeight,
        });
        _colorStyles[key] = (className, registration);
        return className;
    }

    private void RemoveAllDecorations()
    {
        if (!_editor.Model.IsDisposed())
        {
            _decorationsIds = _editor.Model.DeltaDecorations(_decorationsIds, []);
            _colorDecoratorIds = _editor.Model.DeltaDecorations(_colorDecoratorIds, []);
        }

        _decorationsIds = [];
        _colorDecoratorIds = [];
        _colorDatas.Clear();
        foreach (var (_, registration) in _colorStyles.Values)
        {
            registration.Dispose();
        }

        _colorStyles.Clear();
    }

    /// <summary>The color at a position (upstream's getColorData).</summary>
    public ColorData? GetColorData(TextPosition position)
    {
        foreach (var decoration in _editor.Model.GetDecorationsInRange(new TextRange(position, position)))
        {
            if (_colorDatas.TryGetValue(decoration.Id, out var data))
            {
                return data;
            }
        }

        return null;
    }

    /// <summary>The colors with the current range of each, in document order.</summary>
    public IReadOnlyList<(TextRange Range, ColorData Data)> GetColorRanges()
    {
        var result = new List<(TextRange Range, ColorData Data)>();
        foreach (var id in _decorationsIds)
        {
            if (_editor.Model.GetDecorationRange(id) is { } range && _colorDatas.TryGetValue(id, out var data))
            {
                result.Add((range, data));
            }
        }

        return result;
    }

    /// <summary>Whether a decoration is one of the swatches (upstream's isColorDecoration).</summary>
    public bool IsColorDecoration(ModelDecoration decoration) => _colorDecoratorIds.Contains(decoration.Id);
}
