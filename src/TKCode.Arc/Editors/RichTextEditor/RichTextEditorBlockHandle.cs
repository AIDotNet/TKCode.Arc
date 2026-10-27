using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The Docs profile's block handle: a "+" and a grip in the left gutter beside the hovered block. The grip drags the block
/// (its list children or table go with it) to a new place, and opens the block menu on a click; "+" adds a paragraph below and
/// opens the slash menu there. It lives in the editor's overlay, so it never takes focus from the writing surface.
/// </summary>
internal sealed class RichTextEditorBlockHandle
{
    private const double ButtonSize = 22;
    private const double DragThreshold = 4;
    private static readonly (string Label, RichTextBlockType Type)[] s_turnInto =
    {
        ("Text", RichTextBlockType.Paragraph), ("Heading 1", RichTextBlockType.Heading1), ("Heading 2", RichTextBlockType.Heading2),
        ("Heading 3", RichTextBlockType.Heading3), ("Heading 4", RichTextBlockType.Heading4), ("Heading 5", RichTextBlockType.Heading5),
        ("Heading 6", RichTextBlockType.Heading6), ("Bulleted list", RichTextBlockType.BulletedList), ("Numbered list", RichTextBlockType.NumberedList),
        ("To-do list", RichTextBlockType.TodoList), ("Toggle list", RichTextBlockType.Toggle), ("Quote", RichTextBlockType.Quote), ("Callout", RichTextBlockType.Callout),
        ("Code block", RichTextBlockType.CodeBlock)
    };

    private static readonly string[] s_calloutIcons = { "💡", "ℹ️", "✅", "⚠️", "🚫", "📌", "🔥", "❓" };

    private readonly record struct CalloutTone(RichTextColor Color);

    private readonly record struct LineSpacingChoice(RichTextLineSpacing Spacing);

    private readonly record struct CalloutIcon(string Icon);

    private readonly RichTextEditor _owner;
    private readonly RichTextEditorToolButton _add;
    private readonly RichTextEditorToolButton _grip;
    private readonly Border _indicator;
    private MenuSurface? _menu;
    private int _block = -1;
    private bool _over;
    private Point? _press;
    private bool _dragging;
    private int _gap = -1;
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };

    public RichTextEditorBlockHandle(RichTextEditor owner, Panel overlay)
    {
        _owner = owner;
        _add = Button(ArcIcons.Plus, "Add block", "Add a block below");
        _grip = Button(ArcIcons.GripVertical, "Block actions", "Drag to move, click for actions");
        _indicator = new Border { Height = 2, CornerRadius = new CornerRadius(1), IsHitTestVisible = false, IsVisible = false };
        ArcTokens.Bind(_indicator, Border.BackgroundProperty, DesignTokens.ArcDesignTokenKind.Accent);
        overlay.Children.Add(_indicator);
        overlay.Children.Add(_add);
        overlay.Children.Add(_grip);
        _add.Click += (_, _) => AddBelow();
        _grip.Click += (_, _) =>
        {
            if (!_dragging)
            {
                OpenMenu();
            }
        };
        _grip.AddHandler(InputElement.PointerPressedEvent, OnGripPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        _grip.AddHandler(InputElement.PointerMovedEvent, OnGripMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        _grip.AddHandler(InputElement.PointerReleasedEvent, OnGripReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        foreach (var button in new[] { _add, _grip })
        {
            button.PointerEntered += (_, _) =>
            {
                _over = true;
                _hideTimer.Stop();
            };
            button.PointerExited += (_, _) =>
            {
                _over = false;
                ScheduleHide();
            };
        }
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!Engaged)
            {
                Hide();
            }
        };
        Hide();
    }

    /// <summary>Hides the handle after a short grace period, so the pointer can cross the gap between the text and the handle.</summary>
    private void ScheduleHide()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    /// <summary>True while the pointer is on the handle, a press or drag started on it, or its menu is open: the handle must stay.</summary>
    private bool Engaged => _over || _grip.IsPointerOver || _add.IsPointerOver || _press is not null || _dragging || _menu is { IsOpen: true };

    /// <summary>The grip, for tests and keyboard focus.</summary>
    public RichTextEditorToolButton Grip => _grip;

    public RichTextEditorToolButton Add => _add;

    public Border Indicator => _indicator;

    public MenuSurface? Menu => _menu;

    /// <summary>The block the handle stands beside, or -1 while hidden.</summary>
    public int Block => _block;

    public bool IsDragging => _dragging;

    private static RichTextEditorToolButton Button(Avalonia.Media.Geometry icon, string name, string tip)
    {
        var button = new RichTextEditorToolButton(icon, name) { Width = ButtonSize, Height = ButtonSize };
        Tooltip.SetTip(button, tip);
        return button;
    }

    private RichTextEditorSurface? Surface => _owner.Surface;

    private RichTextEditorEngine Engine => _owner.EngineForHandle;

    /// <summary>Follows the pointer over the editor: the handle moves to the block under <paramref name="surfacePoint"/>.</summary>
    public void Track(Point surfacePoint)
    {
        if (_dragging || _press is not null || _menu is { IsOpen: true } || Surface is not { } surface || surface.IsDragging || surface.IsComposing || _owner.IsReadOnly)
        {
            return;
        }
        _hideTimer.Stop();
        if (surfacePoint.Y < 0 || surfacePoint.Y > surface.Bounds.Height)
        {
            Hide();
            return;
        }
        Place(UnitStart(surface.BlockIndexAtY(surfacePoint.Y)));
    }

    private int UnitStart(int block)
    {
        block = Math.Clamp(block, 0, Engine.Blocks.Count - 1);
        return Engine.Blocks[block].IsCell ? RichTextEditorEngine.UnitExtent(Engine.Blocks, block).Start : block;
    }

    /// <summary>Puts the handle beside <paramref name="block"/>.</summary>
    private void Place(int block)
    {
        if (Surface is not { } surface || block < 0 || block >= Engine.Blocks.Count)
        {
            Hide();
            return;
        }
        var origin = surface.TranslatePoint(default, _owner) ?? default;
        var y = origin.Y + surface.HandleY(block) - ButtonSize / 2;
        var x = origin.X - ButtonSize * 2 - 6;
        _block = block;
        Move(_add, x, y);
        Move(_grip, x + ButtonSize + 2, y);
        _add.IsVisible = true;
        _grip.IsVisible = true;
    }

    private static void Move(Control control, double x, double y) => control.RenderTransform = new TranslateTransform(x, y);

    /// <summary>Re-places the handle after the document changed (the block may have moved or gone).</summary>
    public void Refresh()
    {
        if (_block < 0)
        {
            return;
        }
        if (_block >= Engine.Blocks.Count || _owner.IsReadOnly)
        {
            Hide();
            return;
        }
        Place(_block);
    }

    public void Hide()
    {
        if (_dragging || _menu is { IsOpen: true })
        {
            return;
        }
        _block = -1;
        _add.IsVisible = false;
        _grip.IsVisible = false;
        _indicator.IsVisible = false;
    }

    /// <summary>Called when the pointer leaves the editor: the handle stays only while the pointer is on it.</summary>
    public void PointerLeftEditor() => ScheduleHide();

    /* ----------------------------------------------------------------------------------------------------------
     * Drag
     * -------------------------------------------------------------------------------------------------------- */

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_grip).Properties.IsLeftButtonPressed && _block >= 0)
        {
            // A drag whose release was lost (the window lost the pointer) must not leak into this press.
            EndDrag(commit: false);
            _press = e.GetPosition(_owner);
        }
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_press is not { } start || Surface is not { } surface)
        {
            return;
        }
        var point = e.GetPosition(_owner);
        if (!_dragging)
        {
            if (Math.Abs(point.Y - start.Y) < DragThreshold && Math.Abs(point.X - start.X) < DragThreshold)
            {
                return;
            }
            _dragging = true;
            var (from, to) = RichTextEditorEngine.UnitExtent(Engine.Blocks, _block);
            surface.ActiveUnit = (from, to);
        }
        var origin = surface.TranslatePoint(default, _owner) ?? default;
        _gap = surface.DropGapAt(point.Y - origin.Y, Engine.Blocks);
        var y = origin.Y + surface.GapY(_gap);
        _indicator.Width = Math.Max(0, surface.Bounds.Width);
        Move(_indicator, origin.X, y - 1);
        _indicator.IsVisible = true;
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging)
        {
            EndDrag(commit: true);
        }
        _press = null;
    }

    private void EndDrag(bool commit)
    {
        if (!_dragging)
        {
            _press = null;
            return;
        }
        var block = _block;
        var gap = _gap;
        _dragging = false;
        _press = null;
        _gap = -1;
        _indicator.IsVisible = false;
        if (Surface is { } surface)
        {
            surface.ActiveUnit = null;
        }
        if (commit && block >= 0 && gap >= 0 && Engine.MoveUnit(block, gap))
        {
            _owner.Announce("Block moved");
            _block = -1;
        }
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Actions
     * -------------------------------------------------------------------------------------------------------- */

    private void AddBelow()
    {
        if (_block < 0 || _owner.IsReadOnly)
        {
            return;
        }
        var blocks = Engine.Blocks;
        var block = blocks[_block];
        var index = block.Type == RichTextBlockType.Paragraph && block.IsEmpty
            ? _block
            : Engine.InsertParagraphAfterUnit(_block);
        Engine.SetSelection(RichTextEditorSelection.Caret(index, 0));
        _owner.FocusEditor();
        _owner.HandleTextInput("/", false);
        Hide();
    }

    private void OpenMenu()
    {
        if (_block < 0 || _owner.IsReadOnly || Surface is not { } surface)
        {
            return;
        }
        var block = Engine.Blocks[_block];
        var (from, to) = RichTextEditorEngine.UnitExtent(Engine.Blocks, _block);
        surface.ActiveUnit = (from, to);
        if (_menu is null)
        {
            _menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Start, AnchorGap = 4 };
            AutomationProperties.SetName(_menu, "Block actions");
            _menu.ItemClicked += (_, args) => OnMenuChosen(args.Entry.Tag);
            _menu.Closed += (_, _) =>
            {
                if (Surface is { } s)
                {
                    s.ActiveUnit = null;
                }
            };
        }
        _menu.Items.Clear();
        if (block.IsTextual && !block.IsCell)
        {
            foreach (var (label, type) in s_turnInto)
            {
                _menu.Items.Add(new MenuEntry
                {
                    Header = label, Tag = type, ToggleType = MenuEntryToggleType.Radio, GroupName = "turn-into",
                    IsChecked = block.Type == type, HasSeparatorBefore = false
                });
            }
        }
        if (block.IsCallout)
        {
            foreach (var color in Enum.GetValues<RichTextColor>())
            {
                _menu.Items.Add(new MenuEntry
                {
                    Header = "Color: " + (color == RichTextColor.Default ? "None" : color.ToString()), Tag = new CalloutTone(color), ToggleType = MenuEntryToggleType.Radio,
                    GroupName = "callout-color", IsChecked = block.Tone == color, HasSeparatorBefore = color == RichTextColor.Default
                });
            }
            foreach (var icon in s_calloutIcons)
            {
                _menu.Items.Add(new MenuEntry
                {
                    Header = "Icon: " + icon, Tag = new CalloutIcon(icon), ToggleType = MenuEntryToggleType.Radio, GroupName = "callout-icon",
                    IsChecked = block.Icon == icon, HasSeparatorBefore = icon == s_calloutIcons[0]
                });
            }
        }
        if (block.IsTextual && !block.IsCell && !block.IsCode)
        {
            foreach (var spacing in Enum.GetValues<RichTextLineSpacing>())
            {
                _menu.Items.Add(new MenuEntry
                {
                    Header = "Line spacing: " + spacing, Tag = new LineSpacingChoice(spacing), ToggleType = MenuEntryToggleType.Radio,
                    GroupName = "line-spacing", IsChecked = block.Spacing == spacing, HasSeparatorBefore = spacing == RichTextLineSpacing.Default
                });
            }
        }
        _menu.Items.Add(new MenuEntry { Header = "Duplicate", Tag = "duplicate", Icon = ArcIcons.Copy, HasSeparatorBefore = _menu.Items.Count > 0 });
        _menu.Items.Add(new MenuEntry { Header = "Delete", Tag = "delete", Icon = ArcIcons.Trash2, IsDestructive = true });
        _menu.Open(_grip);
    }

    private void OnMenuChosen(object? tag)
    {
        var block = _block;
        if (block < 0)
        {
            return;
        }
        switch (tag)
        {
            case RichTextBlockType type:
                Engine.SetBlockTypeAt(block, type);
                break;
            case CalloutTone tone:
                Engine.SetCallout(block, tone.Color, null);
                break;
            case CalloutIcon icon:
                Engine.SetCallout(block, null, icon.Icon);
                break;
            case LineSpacingChoice choice:
                Engine.SetLineSpacingAt(block, choice.Spacing);
                break;
            case "duplicate":
                Engine.DuplicateUnit(block);
                _owner.Announce("Block duplicated");
                break;
            case "delete":
                Engine.DeleteUnit(block);
                _owner.Announce("Block deleted");
                break;
        }
        _block = -1;
        Hide();
        _owner.FocusEditor();
    }
}
