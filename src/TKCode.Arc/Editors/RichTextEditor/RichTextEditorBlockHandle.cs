using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The Docs profile's block handle in the left gutter beside the hovered block: a chip showing the block's type and a grip,
/// or "+" on an empty paragraph. The chip drags the block (its list children or table go with it) to a new place, tints the
/// block while the pointer is on it, and opens the block menu on a click; "+" opens the slash menu on the empty line. It glides
/// from block to block and lives in the editor's overlay, so it never takes focus from the writing surface.
/// </summary>
internal sealed class RichTextEditorBlockHandle
{
    private const double ButtonSize = 24;
    private const double GripWidth = 40;
    private const double Gap = 8;
    private const double DragThreshold = 4;
    // The pointer counts as heading for the handle when its path, measured from a sample this far back, points at it.
    private const double AimSample = 8;
    private const double AimSlack = 24;
    // Only the last stretch toward the handle holds it; anywhere else in the text the handle follows the pointer at once.
    private const double AimReach = 140;
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
    private readonly ArcGlyph _kind = new() { Data = ArcIcons.Type, Size = 16, StrokeThickness = 2 };
    private readonly Border _indicator;
    private MenuSurface? _menu;
    private int _block = -1;
    private bool _over;
    private Point? _press;
    private bool _dragging;
    private int _gap = -1;
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    // While the pointer crosses other blocks on its way to the handle, the handle holds; it follows again once the pointer rests.
    private readonly DispatcherTimer _aimTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private Point? _aimFrom;
    private Point? _aimOlder;
    private Point _aimPending;
    // Code-driven: the target comes from block layout on every pointer move, which a ControlTheme transition cannot follow.
    private readonly MotionValue<Point> _position;

    public RichTextEditorBlockHandle(RichTextEditor owner, Panel overlay)
    {
        _owner = owner;
        _add = Button(ArcIcons.Plus, "Add block", "Insert a block");
        _add.Width = ButtonSize;
        _grip = Button(ArcIcons.GripVertical, "Block actions", "Drag to move, click for actions");
        _grip.Width = GripWidth;
        _grip.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _kind, new ArcGlyph { Data = ArcIcons.GripVertical, Size = 14, StrokeThickness = 2 } }
        };
        _indicator = new Border { Height = 2, CornerRadius = new CornerRadius(1), IsHitTestVisible = false, IsVisible = false };
        ArcTokens.Bind(_indicator, Border.BackgroundProperty, DesignTokens.ArcDesignTokenKind.Accent);
        ArcTokens.Bind(_kind, ArcGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Accent, Avalonia.Data.BindingPriority.LocalValue);
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
                if (button == _grip)
                {
                    Highlight(true);
                }
            };
            button.PointerExited += (_, _) =>
            {
                _over = false;
                Highlight(false);
                ScheduleHide();
            };
        }
        _position = new MotionValue<Point>(owner, default, MotionVectors.Point);
        // The value is the handle's right edge and top; "+" and the chip both end there.
        _position.Changed += point =>
        {
            Move(_add, point.X - ButtonSize, point.Y);
            Move(_grip, point.X - GripWidth, point.Y);
        };
        _aimTimer.Tick += (_, _) =>
        {
            _aimTimer.Stop();
            _aimFrom = _aimOlder = null;
            Track(_aimPending);
        };
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
        var button = new RichTextEditorToolButton(icon, name) { Height = ButtonSize, Classes = { "handle" } }.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusStep6);
        Tooltip.SetTip(button, tip);
        return button;
    }

    /// <summary>The glyph the chip shows for a block's type, so the handle says what the block is.</summary>
    private static Avalonia.Media.Geometry KindIcon(RichTextEditorBlock block) => block.Type switch
    {
        RichTextBlockType.Heading1 => ArcIcons.Heading1,
        RichTextBlockType.Heading2 => ArcIcons.Heading2,
        RichTextBlockType.Heading3 => ArcIcons.Heading3,
        RichTextBlockType.Heading4 => ArcIcons.Heading4,
        RichTextBlockType.Heading5 => ArcIcons.Heading5,
        RichTextBlockType.Heading6 => ArcIcons.Heading6,
        RichTextBlockType.BulletedList => ArcIcons.List,
        RichTextBlockType.NumberedList => ArcIcons.ListOrdered,
        RichTextBlockType.TodoList => ArcIcons.ListTodo,
        RichTextBlockType.Toggle => ArcIcons.ChevronRight,
        RichTextBlockType.Quote => ArcIcons.Quote,
        RichTextBlockType.Callout => ArcIcons.Lightbulb,
        RichTextBlockType.CodeBlock => ArcIcons.Code,
        RichTextBlockType.Divider => ArcIcons.Minus,
        RichTextBlockType.Image => ArcIcons.Image,
        RichTextBlockType.TableCell => ArcIcons.Table,
        _ => ArcIcons.Type
    };

    /// <summary>An empty paragraph gets "+" instead of the chip: there is nothing to move yet, only something to insert.</summary>
    private static bool IsBlank(RichTextEditorBlock block) => block.Type == RichTextBlockType.Paragraph && block.IsEmpty;

    /// <summary>Tints the block the chip acts on while the pointer is on the chip (a drag or the open menu keeps its own tint).</summary>
    private void Highlight(bool on)
    {
        if (_dragging || _press is not null || _menu is { IsOpen: true } || Surface is not { } surface)
        {
            return;
        }
        surface.ActiveUnit = on && _block >= 0 ? RichTextEditorEngine.UnitExtent(Engine.Blocks, _block) : null;
    }

    private RichTextEditorSurface? Surface => _owner.Surface;

    private RichTextEditorEngine Engine => _owner.EngineForHandle;

    /// <summary>Follows the pointer over the editor: the handle moves to the block under <paramref name="surfacePoint"/>.</summary>
    public void Track(Point surfacePoint)
    {
        if (_dragging || _press is not null || _menu is { IsOpen: true } || _over || Surface is not { } surface || surface.IsDragging || surface.IsComposing || _owner.IsReadOnly)
        {
            return;
        }
        _hideTimer.Stop();
        if (surfacePoint.Y < 0 || surfacePoint.Y > surface.Bounds.Height)
        {
            Hide();
            return;
        }
        var block = UnitStart(surface.BlockIndexAtY(surfacePoint.Y));
        var point = surface.TranslatePoint(surfacePoint, _owner) ?? surfacePoint;
        if (block != _block && _block >= 0 && _aimOlder is { } from && AimsAtHandle(from, point))
        {
            _aimPending = surfacePoint;
            _aimTimer.Stop();
            _aimTimer.Start();
            return;
        }
        _aimTimer.Stop();
        if (_aimFrom is not { } last || Distance(last, point) >= AimSample)
        {
            _aimOlder = _aimFrom ?? point;
            _aimFrom = point;
        }
        Place(block);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// True when the pointer, moving from <paramref name="from"/> to <paramref name="point"/> (editor coordinates), is heading for
    /// the handle: it is within <see cref="AimReach"/> right of the handle and inside the triangle from <paramref name="from"/> to its right edge.
    /// </summary>
    private bool AimsAtHandle(Point from, Point point)
    {
        var at = _position.Target;
        var right = at.X;
        if (point.X <= right || point.X >= from.X || point.X - right > AimReach)
        {
            return false;
        }
        var top = new Point(right, at.Y - AimSlack);
        var bottom = new Point(right, at.Y + ButtonSize + AimSlack);
        static double Side(Point p, Point a, Point b) => (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
        var d1 = Side(point, from, top);
        var d2 = Side(point, top, bottom);
        var d3 = Side(point, bottom, from);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
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
        var target = new Point(origin.X + surface.HandleX(block) - Gap, origin.Y + surface.HandleY(block) - ButtonSize / 2);
        var shown = _add.IsVisible || _grip.IsVisible;
        // Gliding to another block keeps the eye on the handle; showing it, or following a scroll or reflow, puts it there at once.
        if (_block >= 0 && shown && (block != _block || _position.IsAnimating))
        {
            if (_position.Target != target)
            {
                _ = _position.AnimateTo(target, MotionSpec.Spring(ArcSpring.Responsive));
            }
        }
        else
        {
            _position.Jump(target);
        }
        _block = block;
        var blank = IsBlank(Engine.Blocks[block]);
        _kind.Data = KindIcon(Engine.Blocks[block]);
        _add.IsVisible = blank;
        _grip.IsVisible = !blank;
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
        _aimTimer.Stop();
        _aimFrom = _aimOlder = null;
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
        _menu.Items.Add(new MenuEntry { Header = "Add block below", Tag = "add", Icon = ArcIcons.Plus, HasSeparatorBefore = _menu.Items.Count > 0 });
        _menu.Items.Add(new MenuEntry { Header = "Duplicate", Tag = "duplicate", Icon = ArcIcons.Copy });
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
            case "add":
                // After the menu has closed and handed focus back, so the slash menu opens in the editor.
                Dispatcher.UIThread.Post(AddBelow);
                return;
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
