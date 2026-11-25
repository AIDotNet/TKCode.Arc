using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model.BracketPairs;
using TKCode.Arc.Editor.Standalone;
using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

// Syntax colors: the text layer draws each view line's tokens in the colors of the token theme's color map (upstream's
// ViewLines with the theme's generated token CSS), and tells the model which lines are on screen so that they are
// tokenized first (upstream: viewModel.visibleLinesStabilized after a render). Colorized brackets are inline
// decorations whose class names (bracket-highlighting-N, unexpected-closing-bracket) take the theme's
// editorBracketHighlight colors (upstream: the theming participant of colorizedBracketPairsDecorationProvider.ts); their
// brushes follow the color map's in the palette.
internal sealed partial class CodeEditorView
{
    private StandaloneThemeService? _themeService;
    private (int Start, int End) _stabilizedLines = (-1, -1);
    private bool _stabilizePending;

    // palette indices of the bracket colors: the levels' (the visible ones, in order), then the unexpected bracket's
    internal int BracketColorBase => _bracketColorBase;

    internal int BracketColorCount => _bracketColorCount;

    private int _bracketColorBase = -1;
    private int _bracketColorCount;

    private void AttachTokenTheme()
    {
        DetachTokenTheme();
        _themeService = _editor?.LanguageServices.ThemeService;
        if (_themeService is not null)
        {
            _themeService.DidColorThemeChange += OnColorThemeChanged;
        }

        UpdatePalette();
    }

    private void DetachTokenTheme()
    {
        if (_themeService is not null)
        {
            _themeService.DidColorThemeChange -= OnColorThemeChanged;
            _themeService = null;
        }
    }

    private void OnColorThemeChanged(StandaloneTheme theme)
    {
        UpdatePalette();
        InvalidateLayers(CodeEditorLayers.Text);
    }

    /// <summary>Brushes for the color map (index = color id); id 1, the default foreground, follows the view's foreground.</summary>
    private void UpdatePalette()
    {
        if (_textCache is null)
        {
            return;
        }

        if (_themeService is null)
        {
            _decorationColorBase = -1;
            _textCache.SetPalette(null);
            return;
        }

        var colorMap = _themeService.GetColorMap();
        var palette = new List<IBrush>(colorMap.Length + 7);
        foreach (var c in colorMap)
        {
            palette.Add(ToBrush(c));
        }

        _bracketColorBase = palette.Count;
        _bracketColorCount = 0;
        if (_themeService.GetColorTheme() is StandaloneTheme theme)
        {
            // upstream drops the transparent ones, so the levels cycle through the colors a theme shows
            foreach (var id in EditorColorRegistry.BracketHighlightingForegrounds)
            {
                if (theme.GetColorOrDefault(id) is { A: > 0 } color)
                {
                    palette.Add(ToBrush(color));
                    _bracketColorCount++;
                }
            }

            var unexpected = theme.GetColorOrDefault(EditorColorRegistry.EditorBracketHighlightingUnexpectedBracketForeground);
            palette.Add(unexpected is { } u ? ToBrush(u) : palette[(int)ColorId.DefaultForeground]);
        }
        else
        {
            _bracketColorBase = -1;
        }

        AppendDecorationPalette(palette);
        _textCache.SetPalette([.. palette]);

        static IBrush ToBrush(RgbaColor c) => new ImmutableSolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
    }

    /// <summary>
    /// The token colors of a view line, with its colorized brackets and inline decorations (CodeEditorView.Decorations.cs);
    /// null when it is one token in the default color.
    /// </summary>
    internal CodeEditorLineColors? GetLineColors(int viewLineNumber, TextRange visibleRange)
    {
        var data = _viewModel!.GetViewportViewLineRenderingData(visibleRange, viewLineNumber);
        var overrides = GetInlineOverrides(viewLineNumber, data, GetBracketColors(viewLineNumber, data));
        return CodeEditorLineColors.From(_viewModel.GetViewLineData(viewLineNumber).Tokens, overrides);
    }

    private List<ColorOverride>? GetBracketColors(int viewLineNumber, ViewLineRenderingData data)
    {
        if (_bracketColorBase < 0)
        {
            return null;
        }

        List<ColorOverride>? result = null;
        var maxColumn = data.MaxColumn;
        foreach (var decoration in data.InlineDecorations)
        {
            var className = decoration.InlineClassName;
            int color;
            if (className == ColorProvider.UnexpectedClosingBracketClassName)
            {
                color = _bracketColorBase + _bracketColorCount;
            }
            else if (_bracketColorCount > 0
                && className.StartsWith(ColorProvider.LevelClassNamePrefix, StringComparison.Ordinal)
                && int.TryParse(className.AsSpan(ColorProvider.LevelClassNamePrefix.Length), out var level))
            {
                color = _bracketColorBase + (level % _bracketColorCount);
            }
            else
            {
                continue;
            }

            var range = decoration.Range;
            var startColumn = range.StartLineNumber < viewLineNumber ? 1 : range.StartColumn;
            var endColumn = range.EndLineNumber > viewLineNumber ? maxColumn : range.EndColumn;
            if (endColumn <= startColumn)
            {
                continue;
            }

            (result ??= []).Add(new ColorOverride(startColumn - 1, endColumn - 1, color));
        }

        result?.Sort((a, b) => a.Start.CompareTo(b.Start));
        return result;
    }

    // After a frame shows other lines, the model tokenizes them now (heuristically, from a nearby state) instead of
    // waiting for the background; posted, because the tokens it sets repaint the text layer.
    private void ReportVisibleLines(int startLine, int endLine)
    {
        if (_stabilizedLines == (startLine, endLine) || _stabilizePending)
        {
            return;
        }

        _stabilizePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _stabilizePending = false;
            if (_viewModel is null)
            {
                return;
            }

            _stabilizedLines = GetVisibleLines();
            _viewModel.VisibleLinesStabilized();
        }, DispatcherPriority.Background);
    }
}
