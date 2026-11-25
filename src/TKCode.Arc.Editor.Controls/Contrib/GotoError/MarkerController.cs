// Ported from vscode (src/vs/editor/contrib/gotoError/browser/gotoError.ts: MarkerController, MarkerNavigationAction,
// NextMarkerAction, PrevMarkerAction, NextMarkerInFilesAction, PrevMarkerInFilesAction and closeMarkersNavigation,
// commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the marker list is the engine's MarkerList over the editor's marker service (no marker navigation service);
//  - there is one editor and no editor service: the "in files" actions list the markers of the editor's document
//    only (upstream opens the next file's editor); related information in another resource is not opened;
//  - the widget is a view zone (MarkerNavigationWidget) instead of a peek zone widget.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.GotoError;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.Platform.Markers;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>Next / previous problem (F8) and the problem widget (upstream's MarkerController).</summary>
internal sealed class MarkerController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.markerController";
    public const string NextMarkerActionId = "editor.action.marker.next";
    public const string PrevMarkerActionId = "editor.action.marker.prev";
    public const string NextMarkerInFilesActionId = "editor.action.marker.nextInFiles";
    public const string PrevMarkerInFilesActionId = "editor.action.marker.prevInFiles";
    public const string CloseMarkersNavigationCommandId = "closeMarkersNavigation";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new MarkerController(editor), EditorContributionInstantiation.Lazy);

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _widgetVisible;
    private MarkerList? _model;
    private MarkerNavigationWidget? _widget;
    private bool _disposed;

    private MarkerController(CodeEditor editor)
    {
        _editor = editor;
        _widgetVisible = editor.CreateContextKey(CodeEditorContextKeys.MarkersNavigationVisible);
    }

    /// <summary>The controller of <paramref name="editor"/> (created on first use).</summary>
    public static MarkerController? Get(CodeEditor editor) => editor.GetContribution<MarkerController>(Id);

    /// <summary>Upstream's problems.sortOrder.</summary>
    public MarkerSortOrder SortOrder { get; set; } = MarkerSortOrder.Severity;

    /// <summary>The widget while it shows.</summary>
    internal MarkerNavigationWidget? Widget => _widget;

    /// <summary>The marker list of the running session.</summary>
    internal MarkerList? MarkerList => _model;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CleanUp();
    }

    private void CleanUp()
    {
        _widgetVisible.Reset();
        _editor.DidChangeCursorSelection -= OnCursorChanged;
        if (_model is { } model)
        {
            model.OnDidChange -= OnMarkersChanged;
            model.Dispose();
        }

        if (_widget is { } widget)
        {
            widget.NavigateRequested -= OnNavigateRequested;
            widget.CloseRequested -= OnCloseRequested;
            widget.Dispose();
        }

        _widget = null;
        _model = null;
    }

    private MarkerList GetOrCreateModel(Uri? uri)
    {
        if (_model is { } existing && existing.Matches(uri))
        {
            return existing;
        }

        var reusePosition = false;
        if (_model is not null)
        {
            reusePosition = true;
            CleanUp();
        }

        var model = uri is null
            ? new MarkerList(_ => true, _editor.MarkerService, _editor.MarkerDecorationsService.GetWordAtPosition, SortOrder)
            : new MarkerList(uri, _editor.MarkerService, _editor.MarkerDecorationsService.GetWordAtPosition, SortOrder);
        _model = model;
        if (reusePosition && _editor.GetPosition() is { } position)
        {
            model.Move(true, _editor.Model, position);
        }

        _widget = new MarkerNavigationWidget(_editor);
        _widget.NavigateRequested += OnNavigateRequested;
        _widget.CloseRequested += OnCloseRequested;
        _widgetVisible.Set(true);

        // follow the cursor, update the markers
        _editor.DidChangeCursorSelection += OnCursorChanged;
        model.OnDidChange += OnMarkersChanged;
        return model;
    }

    private void OnCursorChanged(TKCode.Arc.Editor.ViewModel.CursorStateChangedEvent e)
    {
        var position = e.Selections.Count > 0 ? e.Selections[0].Position : (TextPosition?)null;
        if (_model?.Selected is not { } selected || position is not { } p || !selected.Marker.Range.ContainsPosition(p))
        {
            _model?.ResetIndex();
        }
    }

    private void OnMarkersChanged()
    {
        if (_widget is not { Position: { } position } widget || _model is not { } model)
        {
            return;
        }

        if (model.Find(_editor.Model.Uri, position) is { } info)
        {
            widget.UpdateMarker(info.Marker);
        }
        else
        {
            widget.ShowStale();
        }
    }

    private void OnNavigateRequested(bool next) => Navigate(next, multiFile: false);

    private void OnCloseRequested() => Close();

    /// <summary>Hides the widget and ends the session (upstream's close).</summary>
    public void Close(bool focusEditor = true)
    {
        CleanUp();
        if (focusEditor)
        {
            _editor.Focus();
        }
    }

    /// <summary>Shows the widget at a marker (upstream's showAtMarker; the hover's "View Problem").</summary>
    public void ShowAtMarker(Marker marker)
    {
        var model = GetOrCreateModel(_editor.Model.Uri);
        model.ResetIndex();
        model.Move(true, _editor.Model, new TextPosition(marker.StartLineNumber, marker.StartColumn));
        if (model.Selected is { } selected)
        {
            _widget!.ShowAtMarker(selected.Marker, selected.Index, selected.Total);
        }
    }

    /// <summary>Shows the next or previous problem from the cursor (upstream's navigate).</summary>
    public void Navigate(bool next, bool multiFile)
    {
        if (_editor.GetPosition() is not { } position)
        {
            return;
        }

        var model = GetOrCreateModel(_editor.Model.Uri);
        model.Move(next, _editor.Model, position);
        if (model.Selected is not { } selected)
        {
            return;
        }

        _widget!.ShowAtMarker(selected.Marker, selected.Index, selected.Total);
    }

    // ---- actions

    public static readonly EditorAction NextMarkerAction = MarkerNavigationAction(
        NextMarkerActionId, "Go to Next Problem (Error, Warning, Info)", true, false, KeyMod.Alt | (int)EditorKeyCode.F8);

    public static readonly EditorAction PrevMarkerAction = MarkerNavigationAction(
        PrevMarkerActionId, "Go to Previous Problem (Error, Warning, Info)", false, false, KeyMod.Shift | KeyMod.Alt | (int)EditorKeyCode.F8);

    public static readonly EditorAction NextMarkerInFilesAction = MarkerNavigationAction(
        NextMarkerInFilesActionId, "Go to Next Problem in Files (Error, Warning, Info)", true, true, (int)EditorKeyCode.F8);

    public static readonly EditorAction PrevMarkerInFilesAction = MarkerNavigationAction(
        PrevMarkerInFilesActionId, "Go to Previous Problem in Files (Error, Warning, Info)", false, true, KeyMod.Shift | (int)EditorKeyCode.F8);

    /// <summary>Upstream's closeMarkersNavigation: Escape closes the widget.</summary>
    public static readonly EditorCommand CloseMarkersNavigationCommand = EditorCommand.BindToContribution<MarkerController>(
        Id, CloseMarkersNavigationCommandId, CodeEditorContextKeys.MarkersNavigationVisible, (controller, _) => controller.Close(),
        new EditorKeybinding((int)EditorKeyCode.Escape)
        {
            Weight = KeybindingWeight.EditorContrib + 50,
            When = EditorContextKeys.Focus,
            Secondary = [KeyMod.Shift | (int)EditorKeyCode.Escape],
        });

    // the "in files" actions stay in this editor's document (there is no editor service to open another one)
    private static EditorAction MarkerNavigationAction(string id, string label, bool next, bool multiFile, int primary) =>
        EditorAction.Create(id, label, null, (editor, _) => Get(editor)?.Navigate(next, multiFile),
            new EditorKeybinding(primary) { When = EditorContextKeys.Focus, Weight = KeybindingWeight.EditorContrib });
}
