// Ported from vscode (src/vs/editor/contrib/unicodeHighlighter/browser/unicodeHighlighter.ts: UnicodeHighlighter,
// resolveOptions, DocumentUnicodeHighlighter, ViewportUnicodeHighlighter and Decorations; commit b57d5c9e), MIT
// license. The characters come from the engine (UnicodeTextModelHighlighter).
// Differences:
//  - the document highlighter computes on the UI thread after its 250 ms delay (upstream: in the editor worker);
//    models too large for syncing use the viewport highlighter, as upstream does when the worker cannot compute;
//  - the workspace is trusted (the standalone editor's default), so the 'inUntrustedWorkspace' defaults resolve to
//    false; '_os' and '_vscode' locales are the current culture and UI culture;
//  - no banner when a document has too many characters (the highlights are left out as upstream does), no hover part
//    and no disable / allow actions (they change user settings upstream).

using System.Globalization;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Boxes invisible characters and characters that look like ASCII ones (upstream's UnicodeHighlighter).</summary>
internal sealed class UnicodeHighlighterContribution : ICodeEditorContribution
{
    public const string Id = "editor.contrib.unicodeHighlighter";

    /// <summary>The class name of a highlighted character.</summary>
    public const string ClassName = "unicode-highlight";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new UnicodeHighlighterContribution(editor), EditorContributionInstantiation.AfterFirstRender);

    private static readonly ModelDecorationOptions[] s_decorations =
    [
        CreateDecoration(false, false),
        CreateDecoration(false, true),
        CreateDecoration(true, false),
        CreateDecoration(true, true),
    ];

    private readonly CodeEditor _editor;
    private readonly IDisposable _style;
    private UnicodeHighlighterOptions _options = new()
    {
        NonBasicAscii = false,
        InvisibleCharacters = true,
        AmbiguousCharacters = true,
        IncludeComments = false,
        IncludeStrings = true,
        AllowedCodePoints = [],
        AllowedLocales = [CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name],
    };

    private IDisposable? _updateSoon;
    private bool _viewport;
    private string[] _decorationIds = [];
    private bool _disposed;

    private UnicodeHighlighterContribution(CodeEditor editor)
    {
        _editor = editor;
        _style = editor.DecorationStyles.Register(ClassName, new CodeEditorDecorationStyle { Border = CodeEditorThemeColor.Warning });
        editor.DidChangeModelContent += OnModelContent;
        editor.DidScrollChange += OnScroll;
        editor.DidLayoutChange += OnLayout;
        editor.DidChangeHiddenAreas += OnHiddenAreas;
        UpdateHighlighter();
    }

    /// <summary>The highlighter's options (upstream's unicodeHighlight.* settings, resolved for a trusted workspace).</summary>
    public UnicodeHighlighterOptions Options
    {
        get => _options;
        set
        {
            _options = value;
            UpdateHighlighter();
        }
    }

    /// <summary>The last result, for tests (upstream's _updateState argument).</summary>
    internal UnicodeHighlightsResult? State { get; private set; }

    /// <summary>The highlighted ranges, in order (tests).</summary>
    internal IReadOnlyList<TextRange> HighlightedRanges =>
        _decorationIds.Select(id => _editor.Model.GetDecorationRange(id)).OfType<TextRange>().ToList();

    public static UnicodeHighlighterContribution? Get(CodeEditor editor) => editor.GetContribution<UnicodeHighlighterContribution>(Id);

    private static ModelDecorationOptions CreateDecoration(bool hideInComments, bool hideInStrings) =>
        new("unicode-highlight")
        {
            Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
            ClassName = ClassName,
            ShowIfCollapsed = true,
            HideInCommentTokens = hideInComments,
            HideInStringTokens = hideInStrings,
        };

    private ModelDecorationOptions GetDecorationFromOptions() =>
        s_decorations[(_options.IncludeComments ? 0 : 2) + (_options.IncludeStrings ? 0 : 1)];

    private void UpdateHighlighter()
    {
        State = null;
        ClearDecorations();
        _updateSoon?.Dispose();
        _updateSoon = null;
        if (!_options.NonBasicAscii && !_options.AmbiguousCharacters && !_options.InvisibleCharacters)
        {
            // Don't do anything if the feature is fully disabled
            return;
        }

        _viewport = _editor.Model.IsTooLargeForSyncing();
        Schedule();
    }

    private void Schedule()
    {
        _updateSoon?.Dispose();
        _updateSoon = CodeEditorMultiCursorAdapter.RunAfter(TimeSpan.FromMilliseconds(250), Update);
    }

    private bool IsActive => !_disposed && (_options.NonBasicAscii || _options.AmbiguousCharacters || _options.InvisibleCharacters);

    private void OnModelContent(ModelContentChangedEvent e)
    {
        if (IsActive)
        {
            Schedule();
        }
    }

    private void OnScroll(ScrollChangedEvent e)
    {
        if (IsActive && _viewport)
        {
            Schedule();
        }
    }

    private void OnLayout(Avalonia.Size size)
    {
        if (IsActive && _viewport)
        {
            Schedule();
        }
    }

    private void OnHiddenAreas(object? e)
    {
        if (IsActive && _viewport)
        {
            Schedule();
        }
    }

    /// <summary>Computes the highlights now (the schedulers' _update).</summary>
    internal void Update()
    {
        _updateSoon = null;
        var model = _editor.Model;
        if (_disposed || model.IsDisposed())
        {
            return;
        }

        if (!model.MightContainNonBasicASCII())
        {
            ClearDecorations();
            return;
        }

        var target = new TextModelUnicodeTarget(model);
        UnicodeHighlightsResult result;
        if (_viewport)
        {
            if (_editor.ViewModel is not { } viewModel)
            {
                return;
            }

            var ranges = new List<TextRange>();
            var hasMore = false;
            foreach (var range in viewModel.GetVisibleRanges())
            {
                var part = UnicodeTextModelHighlighter.ComputeUnicodeHighlights(target, _options, range);
                ranges.AddRange(part.Ranges);
                hasMore = hasMore || part.HasMore;
            }

            // upstream adds the totals to themselves here (they stay 0)
            result = new UnicodeHighlightsResult(ranges, hasMore, 0, 0, 0);
        }
        else
        {
            result = UnicodeTextModelHighlighter.ComputeUnicodeHighlights(target, _options);
        }

        State = result;
        var decorations = new List<ModelDeltaDecoration>();
        if (!result.HasMore)
        {
            // Don't show decorations if there are too many (upstream shows a banner instead).
            var options = GetDecorationFromOptions();
            foreach (var range in result.Ranges)
            {
                decorations.Add(new ModelDeltaDecoration(range, options));
            }
        }

        _decorationIds = model.DeltaDecorations(_decorationIds, decorations);
    }

    /// <summary>Why the character a decoration covers is highlighted (upstream's getDecorationInfo, without comment / string info).</summary>
    internal UnicodeHighlighterReason? GetDecorationReason(string decorationId)
    {
        if (!_decorationIds.Contains(decorationId) || _editor.Model.GetDecorationRange(decorationId) is not { } range)
        {
            return null;
        }

        return UnicodeTextModelHighlighter.ComputeUnicodeHighlightReason(_editor.Model.GetValueInRange(range), _options);
    }

    private void ClearDecorations()
    {
        if (_decorationIds.Length > 0 && !_editor.Model.IsDisposed())
        {
            _decorationIds = _editor.Model.DeltaDecorations(_decorationIds, []);
        }

        _decorationIds = [];
    }

    public void Dispose()
    {
        _disposed = true;
        _updateSoon?.Dispose();
        ClearDecorations();
        _editor.DidChangeModelContent -= OnModelContent;
        _editor.DidScrollChange -= OnScroll;
        _editor.DidLayoutChange -= OnLayout;
        _editor.DidChangeHiddenAreas -= OnHiddenAreas;
        _style.Dispose();
    }
}
