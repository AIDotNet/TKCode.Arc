// The part of upstream's ICodeEditor (src/vs/editor/browser/editorBrowser.ts) and of the services that find uses
// (IStorageService, IClipboardService; src/vs/editor/common/config/editorOptions.ts: EditorFind / IEditorFindOptions),
// commit b57d5c9e, MIT license. The engine has no editor widget: the find model and controller run on this interface,
// which the CodeEditor control implements (and a test editor in the engine's tests).
// Differences from upstream:
//  - only what find calls; hasModel() is GetModel() != null;
//  - the cursor position event carries only the reason (all find reads);
//  - the storage is a per-host key/value store of booleans (upstream: the workspace storage), the clipboard the find
//    clipboard of macOS (readFindText / writeFindText); both optional.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using ICommand = TKCode.Arc.Editor.Commands.ICommand;

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>The <c>find.seedSearchStringFromSelection</c> option.</summary>
internal enum SeedSearchStringFromSelection
{
    /// <summary>Never seed the search string from the editor selection.</summary>
    Never,

    /// <summary>Seed from the selection, or from the word at the cursor when nothing is selected.</summary>
    Always,

    /// <summary>Seed only from a non-empty selection.</summary>
    Selection,
}

/// <summary>The <c>find.autoFindInSelection</c> option.</summary>
internal enum AutoFindInSelection
{
    /// <summary>Never turn on find in selection automatically.</summary>
    Never,

    /// <summary>Always turn it on when the selection is not empty.</summary>
    Always,

    /// <summary>Turn it on when several lines are selected.</summary>
    Multiline,
}

/// <summary>Monaco's <c>find</c> editor options with upstream's defaults (EditorFind).</summary>
internal sealed record EditorFindOptions
{
    public static EditorFindOptions Default { get; } = new();

    /// <summary>Whether the cursor jumps to the next match while typing the search string.</summary>
    public bool CursorMoveOnType { get; init; } = true;

    /// <summary>Whether the search runs while typing.</summary>
    public bool FindOnType { get; init; } = true;

    public SeedSearchStringFromSelection SeedSearchStringFromSelection { get; init; } = SeedSearchStringFromSelection.Always;

    public AutoFindInSelection AutoFindInSelection { get; init; } = AutoFindInSelection.Never;

    /// <summary>Whether the search string is shared through the find clipboard (macOS).</summary>
    public bool GlobalFindClipboard { get; init; }

    /// <summary>Whether the find widget adds space above the first line.</summary>
    public bool AddExtraSpaceOnTop { get; init; } = true;

    /// <summary>Whether next / previous wrap around at the end / start.</summary>
    public bool Loop { get; init; } = true;

    /// <summary>Whether navigating to a match from the find input closes the widget.</summary>
    public bool CloseOnResult { get; init; }
}

/// <summary>Where find keeps its toggles between sessions (upstream's IStorageService, workspace scope).</summary>
internal interface IFindStorage
{
    bool GetBoolean(string key, bool defaultValue);

    void Store(string key, bool value);
}

/// <summary>A find storage in memory.</summary>
internal sealed class InMemoryFindStorage : IFindStorage
{
    private readonly Dictionary<string, bool> _values = new(StringComparer.Ordinal);

    public bool GetBoolean(string key, bool defaultValue) => _values.TryGetValue(key, out var value) ? value : defaultValue;

    public void Store(string key, bool value) => _values[key] = value;
}

/// <summary>The find clipboard (upstream's IClipboardService.readFindText / writeFindText).</summary>
internal interface IFindClipboard
{
    Task<string> ReadFindText();

    void WriteFindText(string text);
}

/// <summary>The cursor moved (upstream's ICursorPositionChangedEvent, the part find reads).</summary>
internal readonly record struct FindCursorPositionChangedEvent(CursorChangeReason Reason);

/// <summary>The editor find runs on (the part of upstream's ICodeEditor find uses).</summary>
internal interface IFindEditor
{
    /// <summary>The editor's id: the owner of the decorations it adds.</summary>
    int EditorId { get; }

    /// <summary>The model, or null without one.</summary>
    TextModel? GetModel();

    TextSelection GetSelection();

    IReadOnlyList<TextSelection> GetSelections();

    TextPosition GetPosition();

    void SetSelection(TextSelection selection);

    void SetSelections(IReadOnlyList<TextSelection> selections);

    /// <summary>Upstream's revealRangeInCenterIfOutsideViewport (smooth).</summary>
    void RevealRangeInCenterIfOutsideViewport(TextRange range);

    bool PushUndoStop();

    void ExecuteCommand(string source, ICommand command);

    /// <summary>The height of the editor's layout in pixels (upstream's getLayoutInfo().height).</summary>
    double GetLayoutHeight();

    /// <summary>The find options.</summary>
    EditorFindOptions FindOptions { get; }

    /// <summary>The <c>wordSeparators</c> option.</summary>
    string WordSeparators { get; }

    /// <summary>The <c>readOnly</c> option.</summary>
    bool IsReadOnly { get; }

    /// <summary>The word at a position after the editor's word separators (upstream's getConfiguredWordAtPosition).</summary>
    WordAtPosition? GetConfiguredWordAtPosition(TextPosition position);

    /// <summary>Gives the editor text keyboard focus.</summary>
    void Focus();

    /// <summary>The context keys the find controller binds (findWidgetVisible) and reads (findInputFocussed).</summary>
    IContextKeyService ContextKeyService { get; }

    /// <summary>Runs the find model's delayed searches on the editor's thread.</summary>
    IdleScheduler Scheduler { get; }

    /// <summary>Where the toggles persist; null for nowhere.</summary>
    IFindStorage? Storage => null;

    /// <summary>The find clipboard; null for none.</summary>
    IFindClipboard? Clipboard => null;

    event Action<FindCursorPositionChangedEvent>? DidChangeCursorPosition;

    event Action<ModelContentChangedEvent>? DidChangeModelContent;

    /// <summary>Changes decorations owned by the editor (upstream's changeDecorations).</summary>
    T? ChangeDecorations<T>(Func<TextModel.ModelDecorationsChangeAccessor, T> callback) =>
        GetModel() is { } model ? model.ChangeDecorations(callback, EditorId) : default;

    /// <summary>Removes decorations owned by the editor (upstream's removeDecorations).</summary>
    void RemoveDecorations(IReadOnlyList<string> decorationIds)
    {
        if (decorationIds.Count > 0)
        {
            GetModel()?.DeltaDecorations(decorationIds, [], EditorId);
        }
    }
}
