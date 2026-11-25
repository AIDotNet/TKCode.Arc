// Ported from vscode (src/vs/editor/contrib/codeAction/browser/codeActionMenu.ts: the groups and toMenuItems; the list
// behavior of src/vs/platform/actionWidget/browser/actionList.ts and actionWidget.ts: headers, focus that skips headers
// and disabled rows, accept, hide; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the action widget is a content widget of the editor (below the position, else above) built from Avalonia controls on
//    a CodeEditorWidgetSurface, not a context view over the workbench; it does not take keyboard focus: the editor's
//    commands (selectNextCodeAction, ...) move through it while codeActionMenuVisible is set;
//  - no codicons: a group shows a small glyph drawn from a geometry; no preview of edits (previewSelectedCodeAction
//    accepts), no action bar (documentation, show disabled).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CodeAction;

namespace TKCode.Arc;

/// <summary>A group of the code action menu (upstream's ActionGroup).</summary>
internal sealed record CodeActionGroup(HierarchicalKind Kind, string Title);

/// <summary>A row of the code action menu: a header or an action (upstream's IActionListItem).</summary>
internal sealed record CodeActionMenuItem(CodeActionGroup Group, CodeActionItem? Item, string? Label, bool Disabled, string? Keybinding)
{
    public bool IsHeader => Item is null;
}

/// <summary>The rows of the code action menu (upstream's codeActionMenu.ts).</summary>
internal static class CodeActionMenu
{
    public static readonly CodeActionGroup UncategorizedGroup = new(HierarchicalKind.Empty, "More Actions...");

    public static readonly IReadOnlyList<CodeActionGroup> Groups =
    [
        new(CodeActionKind.QuickFix, "Quick Fix"),
        new(CodeActionKind.RefactorExtract, "Extract"),
        new(CodeActionKind.RefactorInline, "Inline"),
        new(CodeActionKind.RefactorRewrite, "Rewrite"),
        new(CodeActionKind.RefactorMove, "Move"),
        new(CodeActionKind.SurroundWith, "Surround With"),
        new(CodeActionKind.Source, "Source Action"),
        UncategorizedGroup,
    ];

    /// <summary>The menu rows of <paramref name="actions"/>: with headers, grouped by kind in the order of <see cref="Groups"/>.</summary>
    public static IReadOnlyList<CodeActionMenuItem> ToMenuItems(
        IReadOnlyList<CodeActionItem> actions,
        bool showHeaders,
        Func<Editor.Languages.CodeAction, ResolvedKeybinding?> keybindingResolver)
    {
        if (!showHeaders)
        {
            return [.. actions.Select(action => new CodeActionMenuItem(
                UncategorizedGroup, action, action.Action.Disabled ?? action.Action.Title, action.Action.Disabled is not null, null))];
        }

        // group code actions
        var menuEntries = Groups.Select(group => (Group: group, Actions: new List<CodeActionItem>())).ToList();
        foreach (var action in actions)
        {
            var kind = action.Action.Kind is { } k ? new HierarchicalKind(k) : HierarchicalKind.None;
            foreach (var entry in menuEntries)
            {
                if (entry.Group.Kind.Contains(kind))
                {
                    entry.Actions.Add(action);
                    break;
                }
            }
        }

        var allMenuItems = new List<CodeActionMenuItem>();
        foreach (var (group, groupActions) in menuEntries)
        {
            if (groupActions.Count == 0)
            {
                continue;
            }

            allMenuItems.Add(new CodeActionMenuItem(group, null, null, false, null));
            foreach (var action in groupActions)
            {
                allMenuItems.Add(new CodeActionMenuItem(
                    group, action, action.Action.Title, action.Action.Disabled is not null, keybindingResolver(action.Action)?.GetLabel()));
            }
        }

        return allMenuItems;
    }
}

/// <summary>The code action menu as a content widget (upstream's action widget with its action list).</summary>
internal sealed class CodeActionMenuWidget : ICodeEditorContentWidget
{
    // a light bulb and a wrench, in a 16 x 16 box
    private static readonly Geometry s_quickFixGlyph = Geometry.Parse("M8,1.5 C5.2,1.5 3.5,3.6 3.5,6 C3.5,7.7 4.4,8.8 5.3,9.7 C5.8,10.2 6,10.7 6,11.3 L6,12 L10,12 L10,11.3 C10,10.7 10.2,10.2 10.7,9.7 C11.6,8.8 12.5,7.7 12.5,6 C12.5,3.6 10.8,1.5 8,1.5 Z M6,13 L10,13 L10,13.6 C10,14.2 9.5,14.5 9,14.5 L7,14.5 C6.5,14.5 6,14.2 6,13.6 Z");
    private static readonly Geometry s_refactorGlyph = Geometry.Parse("M10.5,1.5 A4,4 0 0 0 6.7,6.8 L1.8,11.7 A1.6,1.6 0 0 0 4.3,14.2 L9.2,9.3 A4,4 0 0 0 14.5,5.5 L12,8 L9.5,7.5 L9,5 L11.5,2.5 A4,4 0 0 0 10.5,1.5 Z");

    private readonly CodeEditorWidgetSurface _surface;
    private readonly StackPanel _rows;
    private readonly ScrollViewer _scroll;
    private readonly List<(CodeActionMenuItem Item, Border Row)> _entries = new();
    private readonly Action<CodeActionItem> _onSelect;
    private readonly Action<CodeActionItem?> _onFocus;
    private int _focused = -1;
    private IDisposable? _focusBinding;

    public CodeActionMenuWidget(TextPosition position, IReadOnlyList<CodeActionMenuItem> items, Action<CodeActionItem> onSelect, Action<CodeActionItem?> onFocus)
    {
        Position = position;
        Items = items;
        _onSelect = onSelect;
        _onFocus = onFocus;
        _rows = new StackPanel { Orientation = Orientation.Vertical };
        _scroll = new ScrollViewer
        {
            Content = _rows,
            MaxHeight = 300,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        _surface = new CodeEditorWidgetSurface
        {
            Padding = new Thickness(4),
            MinWidth = 160,
            MaxWidth = 480,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _scroll,
        };

        foreach (var item in items)
        {
            var row = item.IsHeader ? CreateHeader(item) : CreateRow(item);
            _entries.Add((item, row));
            _rows.Children.Add(row);
        }

        FocusFirst();
    }

    public string Id => "editor.contrib.codeActionMenu";

    public Control Control => _surface;

    public TextPosition Position { get; }

    public IReadOnlyList<CodeActionMenuItem> Items { get; }

    /// <summary>The focused action row, or null.</summary>
    public CodeActionItem? FocusedItem => _focused >= 0 ? _entries[_focused].Item.Item : null;

    public ContentWidgetPosition? GetPosition() =>
        new(Position, [ContentWidgetPositionPreference.Below, ContentWidgetPositionPreference.Above]);

    /// <summary>Moves the focus to the next action that can be chosen (upstream's focusNext), wrapping around.</summary>
    public void FocusNext() => MoveFocus(+1);

    /// <summary>Moves the focus to the previous action that can be chosen (upstream's focusPrevious), wrapping around.</summary>
    public void FocusPrevious() => MoveFocus(-1);

    /// <summary>Chooses the focused action (upstream's acceptSelected).</summary>
    public void AcceptSelected()
    {
        if (FocusedItem is { } item)
        {
            _onSelect(item);
        }
    }

    private void FocusFirst()
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (CanFocus(i))
            {
                SetFocus(i);
                return;
            }
        }
    }

    private bool CanFocus(int index) => !_entries[index].Item.IsHeader && !_entries[index].Item.Disabled;

    private void MoveFocus(int delta)
    {
        if (_entries.Count == 0)
        {
            return;
        }

        var start = _focused < 0 ? (delta > 0 ? -1 : 0) : _focused;
        for (var step = 1; step <= _entries.Count; step++)
        {
            var index = ((start + (delta * step)) % _entries.Count + _entries.Count) % _entries.Count;
            if (CanFocus(index))
            {
                SetFocus(index);
                return;
            }
        }
    }

    private void SetFocus(int index)
    {
        if (_focused >= 0)
        {
            ApplyRowBackground(_focused, focused: false);
        }

        _focused = index;
        ApplyRowBackground(index, focused: true);
        _entries[index].Row.BringIntoView();
        _onFocus(FocusedItem);
    }

    private void ApplyRowBackground(int index, bool focused)
    {
        _focusBinding?.Dispose();
        _focusBinding = focused
            ? _surface.BindBrush(_entries[index].Row, Border.BackgroundProperty, CodeEditorWidgetSurface.SelectionBackgroundProperty)
            : null;
    }

    private Border CreateHeader(CodeActionMenuItem item)
    {
        var text = new TextBlock { Text = item.Group.Title, FontSize = 11, Margin = new Thickness(8, 6, 8, 2) };
        _surface.BindBrush(text, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);
        return new Border { Child = text };
    }

    private Border CreateRow(CodeActionMenuItem item)
    {
        var glyph = new Avalonia.Controls.Shapes.Path
        {
            Data = item.Group.Kind.Contains(CodeActionKind.QuickFix) || item.Group == CodeActionMenu.UncategorizedGroup ? s_quickFixGlyph : s_refactorGlyph,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        _surface.BindBrush(glyph, Shape.FillProperty, item.Disabled ? CodeEditorWidgetSurface.MutedForegroundProperty : CodeEditorWidgetSurface.AccentForegroundProperty);

        var label = new TextBlock
        {
            Text = item.Label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (item.Disabled)
        {
            _surface.BindBrush(label, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(glyph);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        if (item.Keybinding is { } keybinding)
        {
            var key = new TextBlock { Text = keybinding, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _surface.BindBrush(key, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);
            Grid.SetColumn(key, 2);
            grid.Children.Add(key);
        }

        var row = new Border
        {
            Child = grid,
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(4),
            Cursor = item.Disabled ? null : new Cursor(StandardCursorType.Hand),
        };
        if (!item.Disabled && item.Item is { } action)
        {
            row.PointerEntered += (_, _) =>
            {
                var index = _entries.FindIndex(e => ReferenceEquals(e.Row, row));
                if (index >= 0 && index != _focused)
                {
                    SetFocus(index);
                }
            };
            row.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                _onSelect(action);
            };
        }

        return row;
    }
}
