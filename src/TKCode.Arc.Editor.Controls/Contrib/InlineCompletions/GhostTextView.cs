// Ported from vscode (src/vs/editor/contrib/inlineCompletions/browser/view/ghostText/ghostTextView.ts and
// ghostTextView.css: GhostTextView, computeGhostTextViewData, AdditionalLinesWidget; commit b57d5c9e), MIT license.
//
// Differences: the inline parts are injected text decorations as upstream's; the text after an insertion that
// continues on more lines is hidden with opacity 0 (upstream also shrinks it to font-size 0, so here it keeps its
// space on the first line) and drawn again at the end of the last additional line; the additional lines are a view
// zone whose control draws them in the editor's font (GhostTextLinesControl), without syntax colors; no hover on the
// ghost text, no "clickable" ghost text, no warning style. Every ghost text of the state is drawn (upstream: one view
// per ghost text).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.InlineSuggestions;
using TKCode.Arc.Editor.Model;
using TokenFontStyle = TKCode.Arc.Editor.Tokens.FontStyle;

namespace TKCode.Arc;

/// <summary>Draws the ghost texts of an inline completions state into an editor.</summary>
internal sealed class GhostTextView : IDisposable
{
    public const string GhostTextClassName = "ghost-text-decoration";
    public const string PreviewClassName = "ghost-text-decoration-preview";
    public const string HiddenClassName = "ghost-text-hidden";

    private readonly CodeEditor _editor;
    private readonly List<IDisposable> _styles = new();
    private readonly List<string> _zoneIds = new();
    private string[] _decorationIds = [];

    public GhostTextView(CodeEditor editor)
    {
        _editor = editor;
        // upstream's ghostTextView.css: the editorGhostText foreground, italic
        _styles.Add(editor.DecorationStyles.Register(GhostTextClassName, new CodeEditorDecorationStyle { Foreground = CodeEditorThemeColor.Muted, FontStyle = TokenFontStyle.Italic }));
        _styles.Add(editor.DecorationStyles.Register(PreviewClassName, new CodeEditorDecorationStyle { Foreground = CodeEditorThemeColor.Muted, FontStyle = TokenFontStyle.Italic }));
        _styles.Add(editor.DecorationStyles.Register(HiddenClassName, new CodeEditorDecorationStyle { Opacity = 0 }));
    }

    /// <summary>The additional lines shown below each ghost text's line (for tests).</summary>
    internal List<GhostTextLinesControl> LineControls { get; } = new();

    /// <summary>The model decorations of the ghost texts (for tests).</summary>
    internal IReadOnlyList<string> DecorationIds => _decorationIds;

    /// <summary>The view zones of the additional lines (for tests).</summary>
    internal IReadOnlyList<string> ZoneIds => _zoneIds;

    public void Update(InlineCompletionsState? state)
    {
        var model = _editor.Model;
        var decorations = new List<ModelDeltaDecoration>();
        var zones = new List<(int LineNumber, GhostTextLinesControl Control)>();
        foreach (var ghostText in state?.GhostTexts ?? [])
        {
            var data = ComputeGhostTextViewData(ghostText, model);
            foreach (var p in data.InlineTexts)
            {
                decorations.Add(new ModelDeltaDecoration(
                    new TextRange(ghostText.LineNumber, p.Column, ghostText.LineNumber, p.Column),
                    new ModelDecorationOptions("ghost-text-decoration")
                    {
                        After = new ModelDecorationInjectedTextOptions(p.Text)
                        {
                            InlineClassName = p.Preview ? PreviewClassName : GhostTextClassName,
                            CursorStops = InjectedTextCursorStops.Left,
                        },
                        ShowIfCollapsed = true,
                    }));
            }

            if (data.HiddenStartColumn is { } hidden)
            {
                decorations.Add(new ModelDeltaDecoration(
                    new TextRange(ghostText.LineNumber, hidden, ghostText.LineNumber, model.GetLineMaxColumn(ghostText.LineNumber)),
                    new ModelDecorationOptions("ghost-text-hidden") { InlineClassName = HiddenClassName }));
            }

            if (data.AdditionalLines.Count > 0)
            {
                zones.Add((ghostText.LineNumber, new GhostTextLinesControl(_editor, data.AdditionalLines, data.OriginalSuffix)));
            }
        }

        _decorationIds = model.DeltaDecorations(_decorationIds, decorations);
        _editor.ChangeViewZones(accessor =>
        {
            foreach (var id in _zoneIds)
            {
                accessor.RemoveZone(id);
            }

            _zoneIds.Clear();
            LineControls.Clear();
            foreach (var (lineNumber, control) in zones)
            {
                _zoneIds.Add(accessor.AddZone(new CodeEditorViewZone
                {
                    AfterLineNumber = lineNumber,
                    HeightInLines = control.Lines.Count,
                    Control = control,
                    ShowInHiddenAreas = true,
                }));
                LineControls.Add(control);
            }
        });
    }

    /// <summary>What a ghost text draws (upstream's computeGhostTextViewData).</summary>
    internal sealed record GhostTextViewData(
        IReadOnlyList<(int Column, string Text, bool Preview)> InlineTexts,
        IReadOnlyList<string> AdditionalLines,
        int? HiddenStartColumn,
        string? OriginalSuffix);

    internal static GhostTextViewData ComputeGhostTextViewData(GhostText ghostText, TextModel textModel)
    {
        var inlineTexts = new List<(int, string, bool)>();
        var additionalLines = new List<string>();

        void AddToAdditionalLines(IReadOnlyList<string> ghLines)
        {
            var start = 0;
            if (additionalLines.Count > 0)
            {
                additionalLines[^1] += ghLines[0];
                start = 1;
            }

            for (var i = start; i < ghLines.Count; i++)
            {
                additionalLines.Add(ghLines[i]);
            }
        }

        var textBufferLine = textModel.GetLineContent(ghostText.LineNumber);
        int? hiddenTextStartColumn = null;
        var lastIdx = 0;
        foreach (var part in ghostText.Parts)
        {
            IReadOnlyList<string> ghLines = part.Lines;
            if (hiddenTextStartColumn is null)
            {
                inlineTexts.Add((part.Column, ghLines[0], part.Preview));
                ghLines = ghLines.Skip(1).ToList();
            }
            else
            {
                AddToAdditionalLines([textBufferLine[lastIdx..Math.Min(textBufferLine.Length, part.Column - 1)]]);
            }

            if (ghLines.Count > 0)
            {
                AddToAdditionalLines(ghLines);
                if (hiddenTextStartColumn is null && part.Column <= textBufferLine.Length)
                {
                    hiddenTextStartColumn = part.Column;
                }
            }

            lastIdx = part.Column - 1;
        }

        string? originalSuffix = null;
        if (hiddenTextStartColumn is not null)
        {
            originalSuffix = textBufferLine[Math.Min(lastIdx, textBufferLine.Length)..];
        }

        return new GhostTextViewData(inlineTexts, additionalLines, hiddenTextStartColumn, originalSuffix);
    }

    public void Dispose()
    {
        if (!_editor.Model.IsDisposed())
        {
            _decorationIds = _editor.Model.DeltaDecorations(_decorationIds, []);
        }

        _editor.ChangeViewZones(accessor =>
        {
            foreach (var id in _zoneIds)
            {
                accessor.RemoveZone(id);
            }
        });
        _zoneIds.Clear();
        foreach (var style in _styles)
        {
            style.Dispose();
        }

        _styles.Clear();
    }
}

/// <summary>
/// The lines of a ghost text below its line (upstream's AdditionalLinesWidget): the suggestion in the ghost text style,
/// then the rest of the original line in the text color. Drawn in the editor's font with the view's brushes.
/// </summary>
internal sealed class GhostTextLinesControl : Control
{
    private readonly CodeEditor _editor;

    public GhostTextLinesControl(CodeEditor editor, IReadOnlyList<string> lines, string? originalSuffix)
    {
        _editor = editor;
        Lines = lines;
        OriginalSuffix = originalSuffix;
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    public IReadOnlyList<string> Lines { get; }

    public string? OriginalSuffix { get; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_editor.View is not { } view)
        {
            return;
        }

        var muted = view.GetThemeBrush(CodeEditorThemeColor.Muted) ?? _editor.Foreground;
        var foreground = _editor.Foreground;
        var tabSize = _editor.Model.GetOptions().TabSize;
        var lineHeight = view.LineHeight;
        var x0 = -view.ScrollLeft;
        var italic = new Typeface(_editor.FontFamily, FontStyle.Italic);
        var normal = new Typeface(_editor.FontFamily);
        for (var i = 0; i < Lines.Count; i++)
        {
            var y = i * lineHeight;
            var text = ExpandTabs(Lines[i], tabSize);
            var ghost = Format(text, italic, muted);
            context.DrawText(ghost, new Point(x0, y + ((lineHeight - ghost.Height) / 2)));
            if (i == Lines.Count - 1 && !string.IsNullOrEmpty(OriginalSuffix))
            {
                // the rest of the line the suggestion was inserted into, after the suggestion
                var suffix = Format(ExpandTabs(OriginalSuffix, tabSize), normal, foreground);
                context.DrawText(suffix, new Point(x0 + (text.Length * view.CharWidth), y + ((lineHeight - suffix.Height) / 2)));
            }
        }
    }

    private FormattedText Format(string text, Typeface typeface, IBrush? brush) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, _editor.FontSize, brush);

    private static string ExpandTabs(string text, int tabSize)
    {
        if (!text.Contains('\t', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (ch == '\t')
            {
                builder.Append(' ', tabSize - (builder.Length % tabSize));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
