// Accessibility of the editor. Behavior references (vscode, commit b57d5c9e, MIT license):
//  - src/vs/editor/browser/controller/editContext/textArea/textAreaEditContext.ts: what the hidden textarea exposes to
//    a screen reader: the page of lines around the selection (SimplePagedScreenReaderStrategy, ported in the engine's
//    Accessibility/ScreenReaderUtils.cs, accessibilityPageSize 500, long texts trimmed), so moving the caret reads the
//    line, the passed characters or the selection;
//  - src/vs/editor/browser/config/tabFocus.ts and editorOptions.ts (tabFocusMode): whether Tab moves the focus;
//  - src/vs/base/browser/ui/aria/aria.ts (CodeEditorAria.cs): alert and status messages.
// Differences from upstream:
//  - there is no textarea for the screen reader to read, and Avalonia 12 has no text pattern, so the editor announces
//    what a screen reader would read from the textarea after a caret move: the new line when the line changed, the
//    characters passed over on the same line, or the selected text with " selected" (as screen readers do); moves that
//    come with an edit (typing, deleting) are not announced, the screen reader echoes the keys itself. Announcements are
//    made only while the editor has an automation peer (assistive technology asked for one), once per dispatcher turn
//    for the last state, as a polite status;
//  - the tab focus mode is per editor (TabFocusMode), not process-wide; the editorTabMovesFocus context key follows it.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Accessibility;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    /// <summary>
    /// Whether Tab and Shift+Tab move the keyboard focus out of the editor instead of indenting, as Monaco's
    /// <c>tabFocusMode</c> option (default false). Ctrl+M (Ctrl+Shift+M on macOS) toggles it, so that keyboard users can
    /// leave the editor.
    /// </summary>
    public static readonly StyledProperty<bool> TabFocusModeProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(TabFocusMode));

    // the context key follows the property (upstream: EditorContextKeys.tabMovesFocus set from the computed option)
    private static readonly IDisposable s_tabFocusModeChanged =
        TabFocusModeProperty.Changed.AddClassHandler<CodeEditor>(static (editor, _) => editor.OnTabFocusModeChanged());

    /// <summary>Monaco's accessibilityPageSize default: the lines a screen reader can read at once.</summary>
    internal const int AccessibilityPageSize = 500;

    private const string Blank = "blank";

    private readonly CodeEditorAria _aria = new();
    private IContextKey<bool>? _tabMovesFocusKey;
    private CodeEditorAutomationPeer? _automationPeer;
    private TextSelection? _announcedSelection;
    private TextSelection? _pendingAnnouncement;
    private bool _announcementPosted;

    /// <summary>Whether Tab and Shift+Tab move the keyboard focus out of the editor instead of indenting.</summary>
    public bool TabFocusMode
    {
        get => GetValue(TabFocusModeProperty);
        set => SetValue(TabFocusModeProperty, value);
    }

    /// <summary>The editor's live regions (upstream's aria alert and status).</summary>
    internal CodeEditorAria Aria
    {
        get
        {
            _aria.AttachTo(WidgetHost);
            return _aria;
        }
    }

    /// <summary>The automation peer, once assistive technology asked for it (null before).</summary>
    internal CodeEditorAutomationPeer? AutomationPeer => _automationPeer;

    /// <summary>Reads a message out at once (upstream's alert).</summary>
    internal void Alert(string message) => Aria.Alert(message);

    /// <summary>Reads a message out when the screen reader is idle (upstream's status).</summary>
    internal void Status(string message) => Aria.Status(message);

    /// <summary>The cursor's line and column, as the item status says them ("Line 3, Column 5").</summary>
    internal string GetCursorStatusText()
    {
        if (GetSelection() is not { } selection)
        {
            return string.Empty;
        }

        var text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Line {selection.PositionLineNumber}, Column {selection.PositionColumn}");
        if (!selection.IsEmpty)
        {
            var length = Model.GetValueLengthInRange(selection.Range);
            text += string.Create(System.Globalization.CultureInfo.InvariantCulture, $" ({length} selected)");
        }

        return text;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        var peer = new CodeEditorAutomationPeer(this);
        if (_automationPeer is null)
        {
            DidChangeCursorSelection += OnCursorSelectionChangedForScreenReader;
        }

        _automationPeer = peer;
        _announcedSelection = GetSelection();
        return peer;
    }

    private void OnTabFocusModeChanged()
    {
        _tabMovesFocusKey ??= EditorContextKeys.TabMovesFocus.BindTo(ContextKeyService);
        _tabMovesFocusKey.Set(TabFocusMode);
    }

    // What a screen reader would read from upstream's textarea after the caret moved, once per dispatcher turn.
    private void OnCursorSelectionChangedForScreenReader(CursorStateChangedEvent e)
    {
        _automationPeer?.OnCursorChanged();
        if (e.Selections.Count == 0)
        {
            return;
        }

        if (e.OldModelVersionId != e.ModelVersionId)
        {
            // an edit moved the cursor: the screen reader echoes the typed keys itself
            _announcedSelection = e.Selections[0];
            _pendingAnnouncement = null;
            return;
        }

        _pendingAnnouncement = e.Selections[0];
        if (!_announcementPosted)
        {
            _announcementPosted = true;
            Dispatcher.UIThread.Post(AnnouncePendingSelection, DispatcherPriority.Background);
        }
    }

    /// <summary>Announces the cursor or selection change since the last announcement (posted; tests call it directly).</summary>
    internal void AnnouncePendingSelection()
    {
        _announcementPosted = false;
        if (_pendingAnnouncement is not { } selection)
        {
            return;
        }

        _pendingAnnouncement = null;
        var previous = _announcedSelection;
        _announcedSelection = selection;
        if (GetScreenReaderAnnouncement(previous, selection) is { Length: > 0 } message)
        {
            Status(message);
        }
    }

    /// <summary>
    /// What to read after the selection went from <paramref name="previous"/> to <paramref name="selection"/>: the
    /// selected text, the line the cursor moved to, or the characters it passed over; null when nothing changed.
    /// </summary>
    internal string? GetScreenReaderAnnouncement(TextSelection? previous, TextSelection selection)
    {
        if (previous is { } p && p == selection)
        {
            return null;
        }

        var model = Model;
        if (selection.StartLineNumber > model.GetLineCount())
        {
            return null;
        }

        if (!selection.IsEmpty)
        {
            var state = SimplePagedScreenReaderStrategy.FromEditorSelection(new TextModelScreenReaderModel(model), selection, AccessibilityPageSize, trimLongText: true);
            return state.SelectedText + " selected";
        }

        var position = selection.Position;
        var lineContent = model.GetLineContent(position.LineNumber);
        if (previous is not { } old || old.PositionLineNumber != position.LineNumber || !old.IsEmpty)
        {
            // a new line (or the end of a selection): the line, as a screen reader reads it
            return string.IsNullOrWhiteSpace(lineContent) ? Blank : lineContent;
        }

        // along the line: the characters passed over (one character, or a word for word moves)
        var from = Math.Min(old.PositionColumn, position.Column);
        var to = Math.Max(old.PositionColumn, position.Column);
        if (from == to)
        {
            return null;
        }

        var start = Math.Clamp(from - 1, 0, lineContent.Length);
        var end = Math.Clamp(to - 1, start, lineContent.Length);
        var passed = lineContent[start..end];
        return passed.Length switch
        {
            0 => null,
            1 => DescribeCharacter(passed[0]),
            _ => string.IsNullOrWhiteSpace(passed) ? Blank : passed,
        };
    }

    private static string DescribeCharacter(char c) => c switch
    {
        ' ' => "space",
        '\t' => "tab",
        _ => c.ToString(),
    };
}
