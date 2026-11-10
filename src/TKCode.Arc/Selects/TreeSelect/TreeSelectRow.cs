using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// A node of a <see cref="TreeSelect"/>'s data, built once per data load. Only leaves store a check; a parent's state
/// is derived from its children (see <see cref="TreeSelect"/>).
/// </summary>
internal sealed class TreeSelectNode
{
    public TreeSelectNode(object item) => Item = item;

    public object Item { get; }
    public TreeSelectNode? Parent { get; set; }
    public List<TreeSelectNode>? Children { get; set; }
    public int Depth { get; set; } = 1;
    public int Position { get; set; } = 1;
    public int SetSize { get; set; } = 1;
    public string Label { get; set; } = string.Empty;
    public object? Value { get; set; }
    public Geometry? Icon { get; set; }
    public bool IsDisabled { get; set; }

    /// <summary>The stored check of a leaf.</summary>
    public bool IsChecked { get; set; }

    /// <summary>Derived three-state check (a leaf mirrors <see cref="IsChecked"/>).</summary>
    public CheckboxState State { get; set; }

    /// <summary>Whether the row shows its children right now (set on every rebuild of the visible rows).</summary>
    public bool IsExpanded { get; set; }

    /// <summary>The matched part of <see cref="Label"/> while searching, or -1.</summary>
    public int MatchStart { get; set; } = -1;
    public int MatchLength { get; set; }

    public bool HasChildren => Children is { Count: > 0 };
}

/// <summary>Resolved colors, pens and metrics shared by every row of one menu.</summary>
internal sealed class TreeSelectPalette
{
    private readonly Dictionary<(IBrush, double), IPen> _pens = new();
    private int _penVersion = -1;

    public int Version { get; set; }
    public IBrush? Foreground { get; set; }
    public IBrush? Muted { get; set; }
    public IBrush? Icon { get; set; }
    public IBrush? CheckOn { get; set; }
    public IBrush? CheckGlyph { get; set; }
    public IBrush? CheckBorder { get; set; }
    public IBrush? CheckSurface { get; set; }
    public IBrush? Match { get; set; }
    public FontFamily FontFamily { get; set; } = FontFamily.Default;
    public double FontSize { get; set; } = 14;
    public double SlotHeight { get; set; } = 36;
    public double RowGap { get; set; } = 2;
    public double Indent { get; set; } = 18;
    public double InlinePadding { get; set; } = 8;
    public double Gap { get; set; } = 8;
    public double CheckboxSize { get; set; } = 16;
    public double CheckboxRadius { get; set; } = 4.5;
    public double CheckStroke { get; set; } = 1.75;
    public double IconSize { get; set; } = 16;
    public double DisabledOpacity { get; set; } = 0.45;
    public double MatchRadius { get; set; } = 3;
    public bool ShowCheckboxes { get; set; }
    public bool ShowIcons { get; set; }

    public IPen? PenFor(IBrush? brush, double thickness)
    {
        if (brush is null)
        {
            return null;
        }
        if (_penVersion != Version)
        {
            _pens.Clear();
            _penVersion = Version;
        }
        if (!_pens.TryGetValue((brush, thickness), out var pen))
        {
            pen = new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _pens[(brush, thickness)] = pen;
        }
        return pen;
    }
}

/// <summary>The TreeSelect's row engine: the shared tree rows panel of TreeView, with rows keyed by their item.</summary>
internal sealed class TreeSelectRowsPanel : TablesTreeRowsPanel
{
    public TreeSelectRowsPanel()
    {
        Motion = new TablesTreeRowMotion
        {
            Stagger = true,
            EnterOffset = -6,
            ExitOffset = -4,
            EnterFade = ArcMotionPresets.Fast,
            EnterFadeEasing = ArcEasings.Enter,
            ExitFade = ArcMotionPresets.Instant,
            RevealDuration = ArcMotionPresets.Standard,
            // Filtering replaces most rows at once; past a few screens it applies instantly.
            BulkLimit = 400
        };
    }

    public TreeSelect? Owner { get; set; }

    protected override TablesTreeRow CreateRow() => new TreeSelectRow(this);

    protected override object KeyOf(object model) => ((TreeSelectNode)model).Item;

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Owner?.OnRowsPointerExited();
    }
}

/// <summary>
/// One menu row, drawn directly: indent, chevron (turns on the snappy spring), checkbox (Multiple; fill and mark as
/// Arc's <see cref="Checkbox"/>, the check draws itself and morphs into the dash), optional icon, the label with the
/// search match marked, and the check of the selected node (Single). The hover / keyboard highlight is the panel's
/// gliding indicator underneath. Its motion uses <see cref="MotionValue{T}"/> because the row draws these parts
/// itself; there are no child properties for ControlTheme transitions to target. Rows never take focus: the field
/// (or its search input) keeps it and drives the highlight.
/// </summary>
internal sealed class TreeSelectRow : TablesTreeRow
{
    private static readonly Cursor s_hand = new(StandardCursorType.Hand);
    private readonly TreeSelectRowsPanel _panel;
    private readonly SolidColorBrush _labelBrush = new();
    private readonly List<TextRangeDecoration> _decorations = new(1);
    private readonly TextRangeDecoration _match = new();
    private MotionValue<double>? _rotation;
    private MotionValue<double>? _fill;
    private MotionValue<double>? _draw;
    private MotionValue<double>? _morph;
    private bool _wasExpanded;
    private CheckboxState _wasState;
    private bool _pressed;
    private TextLayout? _layout;
    private string? _layoutText;
    private double _layoutWidth = double.NaN;
    private int _layoutVersion = -1;

    public TreeSelectRow(TreeSelectRowsPanel panel)
    {
        _panel = panel;
        Cursor = s_hand;
        _decorations.Add(_match);
    }

    internal TreeSelect? Owner => _panel.Owner;

    private TreeSelectPalette? Palette => Owner?.Palette;

    public TreeSelectNode? Node => Model as TreeSelectNode;

    /// <summary>The selected node of a single-select field.</summary>
    public bool IsSelected => Node is { } node && Owner is { } owner && owner.IsSelectedNode(node);

    /// <summary>Current chevron angle in degrees (tests).</summary>
    internal double ChevronAngle => _rotation?.Value ?? (Node?.IsExpanded == true ? 90 : 0);

    /// <summary>The checkbox state drawn now (tests).</summary>
    internal CheckboxState CheckState => Node?.State ?? CheckboxState.Unchecked;

    protected override void OnBound(object? previous, bool sameKey)
    {
        // The field keeps focus; a row must not take it on click.
        Focusable = false;
        IsTabStop = false;
        var node = Node;
        if (node is null)
        {
            _layout = null;
            _layoutText = null;
            _pressed = false;
            return;
        }
        Cursor = node.IsDisabled && !node.HasChildren ? null : s_hand;
        var full = IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var angle = node.IsExpanded ? 90.0 : 0.0;
        if (sameKey && node.IsExpanded != _wasExpanded && full)
        {
            _rotation ??= CreateValue(_wasExpanded ? 90 : 0);
            _ = _rotation.AnimateTo(angle, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _rotation?.Jump(angle);
        }
        _wasExpanded = node.IsExpanded;
        ApplyCheck(node.State, sameKey && node.State != _wasState && full);
        _wasState = node.State;
        if (!ReferenceEquals(_layoutText, node.Label))
        {
            _layout = null;
        }
        AutomationProperties.SetName(this, node.Label);
        AutomationProperties.SetPositionInSet(this, node.Position);
        AutomationProperties.SetSizeOfSet(this, node.SetSize);
        AutomationProperties.SetHelpText(this, TreeSelectLangResources.Format(TreeSelectLangResourceKind.Level, node.Depth));
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>The checkbox follows <see cref="Checkbox"/>: the fill grows from 0.6, the mark draws itself and morphs into the dash.</summary>
    private void ApplyCheck(CheckboxState state, bool animate)
    {
        var on = state != CheckboxState.Unchecked;
        var fill = on ? 1.0 : 0.0;
        var morph = state == CheckboxState.Indeterminate ? 1.0 : 0.0;
        if (!animate)
        {
            _fill?.Jump(fill);
            _draw?.Jump(fill);
            _morph?.Jump(morph);
            return;
        }
        var wasOn = _wasState != CheckboxState.Unchecked ? 1.0 : 0.0;
        _fill ??= CreateValue(wasOn);
        _draw ??= CreateValue(wasOn);
        _morph ??= CreateValue(_wasState == CheckboxState.Indeterminate ? 1 : 0);
        _ = _fill.AnimateTo(fill, MotionSpec.Spring(ArcSpring.Snappy));
        _ = _draw.AnimateTo(fill, MotionSpec.Spring(ArcSpring.Snappy));
        _ = _morph.AnimateTo(morph, MotionSpec.Spring(ArcSpring.Morph));
    }

    #region Input

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch && Node is not null && !IsGhost)
        {
            Owner?.OnRowHovered(this);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pressed = false;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Node is null || IsGhost || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        var point = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(point))
        {
            e.Handled = true;
            Owner?.OnRowClicked(this, IsOnChevron(point.X));
        }
    }

    /// <summary>Whether <paramref name="x"/> falls on the chevron slot (a click there only opens or closes).</summary>
    internal bool IsOnChevron(double x)
    {
        if (Palette is not { } p || Node is not { HasChildren: true } node)
        {
            return false;
        }
        var left = ChevronLeft(p, node);
        return x >= left - p.Gap / 2 && x <= left + 14 + p.Gap / 2;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TreeSelectRowAutomationPeer(this);

    #endregion

    #region Layout and rendering

    private static double ChevronLeft(TreeSelectPalette p, TreeSelectNode node) => p.InlinePadding + (node.Depth - 1) * p.Indent;

    private static double CheckboxLeft(TreeSelectPalette p, TreeSelectNode node) => ChevronLeft(p, node) + 14 + p.Gap;

    private static double IconLeft(TreeSelectPalette p, TreeSelectNode node) =>
        CheckboxLeft(p, node) + (p.ShowCheckboxes ? p.CheckboxSize + p.Gap : 0);

    private static double LabelX(TreeSelectPalette p, TreeSelectNode node) =>
        IconLeft(p, node) + (p.ShowIcons && node.Icon is not null ? p.IconSize + p.Gap : 0);

    /// <summary>Left edge of the label (tests).</summary>
    internal double LabelLeft => Palette is { } p && Node is { } node ? LabelX(p, node) : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = Palette?.SlotHeight ?? 36;
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    private TextLayout CreateLayout(TreeSelectPalette p, string text, double maxWidth)
    {
        var typeface = new Typeface(p.FontFamily, FontStyle.Normal, FontWeight.Normal);
        return new TextLayout(text, typeface, p.FontSize, _labelBrush, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(0, maxWidth), double.PositiveInfinity,
            p.FontSize * 1.45);
    }

    public override void Render(DrawingContext context)
    {
        var p = Palette;
        var node = Node;
        if (p is null || node is null)
        {
            return;
        }
        var width = Bounds.Width;
        // A transparent fill makes the whole slot hit-testable, not only the drawn glyphs.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var gap = p.RowGap;
        var itemHeight = p.SlotHeight - gap;
        var centerY = gap + itemHeight / 2;
        using var dim = context.PushOpacity(node.IsDisabled ? p.DisabledOpacity : 1);

        var left = ChevronLeft(p, node);
        if (node.HasChildren && p.PenFor(p.Muted, 1.9) is { } chevronPen)
        {
            DrawGlyph(context, ArcIcons.ChevronRight, chevronPen, new Point(left + 7, centerY), 14, ChevronAngle);
        }

        if (p.ShowCheckboxes)
        {
            DrawCheckbox(context, p, new Point(CheckboxLeft(p, node), centerY - p.CheckboxSize / 2));
        }

        if (p.ShowIcons && node.Icon is { } icon && p.PenFor(p.Icon, 1.7) is { } iconPen)
        {
            DrawGlyph(context, icon, iconPen, new Point(IconLeft(p, node) + p.IconSize / 2, centerY), p.IconSize, 0);
        }

        var selected = IsSelected;
        var labelX = LabelX(p, node);
        var right = p.InlinePadding + (selected ? 16 + p.Gap : 0);
        var maxWidth = width - labelX - right;
        if (p.Foreground is ISolidColorBrush fg && _labelBrush.Color != fg.Color)
        {
            _labelBrush.Color = fg.Color;
        }
        if (_layout is null || _layoutVersion != p.Version || Math.Abs(_layoutWidth - maxWidth) > 0.5 || !ReferenceEquals(_layoutText, node.Label))
        {
            _layout?.Dispose();
            _layout = CreateLayout(p, node.Label, maxWidth);
            _layoutText = node.Label;
            _layoutWidth = maxWidth;
            _layoutVersion = p.Version;
        }
        var origin = new Point(labelX, centerY - _layout.Height / 2);
        if (node.MatchStart >= 0 && node.MatchLength > 0 && node.MatchStart + node.MatchLength <= node.Label.Length)
        {
            _match.Start = node.MatchStart;
            _match.Length = node.MatchLength;
            _match.Background = p.Match;
            _match.CornerRadius = p.MatchRadius;
            _match.Outset = new Thickness(1, 0);
            TextRangeRenderer.DrawBackgrounds(context, _layout, _decorations, origin);
        }
        _layout.Draw(context, origin);

        if (selected && p.PenFor(p.Foreground, 2) is { } checkPen)
        {
            DrawGlyph(context, ArcIcons.Check, checkPen, new Point(width - p.InlinePadding - 8, centerY), 16, 0);
        }
    }

    private void DrawCheckbox(DrawingContext context, TreeSelectPalette p, Point topLeft)
    {
        var size = p.CheckboxSize;
        var box = new Rect(topLeft, new Size(size, size));
        var fill = Math.Clamp(_fill?.Value ?? (Node?.State is CheckboxState.Unchecked or null ? 0 : 1), 0, 1.2);
        // The square: hairline border on the surface, as Checkbox's PART_Square.
        if (fill < 0.999)
        {
            context.DrawRectangle(p.CheckSurface, p.PenFor(p.CheckBorder, 1), new RoundedRect(box.Deflate(0.5), Math.Max(0, p.CheckboxRadius - 0.5)));
        }
        if (fill <= 0.001)
        {
            return;
        }
        // The ControlOn fill grows from 0.6 while it fades in (CheckboxMark).
        var scale = 0.6 + 0.4 * fill;
        var edge = size * scale;
        var offset = (size - edge) / 2;
        using (context.PushOpacity(Math.Clamp(fill, 0, 1)))
        {
            context.DrawRectangle(p.CheckOn, null, new RoundedRect(new Rect(topLeft.X + offset, topLeft.Y + offset, edge, edge), p.CheckboxRadius * scale));
            if (p.PenFor(p.CheckGlyph, p.CheckStroke * CheckboxMark.BoxSize / size) is { } pen)
            {
                var s = size / CheckboxMark.BoxSize;
                using (context.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(topLeft.X, topLeft.Y)))
                {
                    var morph = _morph?.Value ?? (Node?.State == CheckboxState.Indeterminate ? 1 : 0);
                    CheckboxPathMorph.DrawPartial(context, pen, Math.Clamp(morph, 0, 1), Math.Clamp(_draw?.Value ?? 1, 0, 1));
                }
            }
        }
    }

    private static void DrawGlyph(DrawingContext context, Geometry geometry, IPen pen, Point center, double size, double degrees)
    {
        var s = size / 24.0;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(s, s);
        if (Math.Abs(degrees) > 0.01)
        {
            matrix *= Matrix.CreateRotation(degrees * Math.PI / 180);
        }
        matrix *= Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    #endregion

    private sealed class TreeSelectRowAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider, IToggleProvider
    {
        public TreeSelectRowAutomationPeer(TreeSelectRow owner) : base(owner)
        {
        }

        private TreeSelectRow Row => (TreeSelectRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

        protected override string? GetNameCore() => Row.Node?.Label;

        protected override bool IsEnabledCore() => Row.Node is not { IsDisabled: true } && base.IsEnabledCore();

        protected override bool IsKeyboardFocusableCore() => false;

        public ExpandCollapseState ExpandCollapseState => Row.Node is not { HasChildren: true } node
            ? ExpandCollapseState.LeafNode
            : node.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Row.Node is { } node)
            {
                Row.Owner?.SetNodeExpanded(node, true);
            }
        }

        public void Collapse()
        {
            if (Row.Node is { } node)
            {
                Row.Owner?.SetNodeExpanded(node, false);
            }
        }

        public bool IsSelected => Row.Owner?.SelectionMode == TreeSelectMode.Multiple
            ? Row.Node?.State == CheckboxState.Checked
            : Row.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
            if (Row.Owner?.SelectionMode == TreeSelectMode.Multiple && Row.Node is { State: not CheckboxState.Unchecked } node)
            {
                Row.Owner.ToggleNode(node);
            }
        }

        public void Select()
        {
            if (Row.Node is not { } node || Row.Owner is not { } owner)
            {
                return;
            }
            if (owner.SelectionMode == TreeSelectMode.Multiple)
            {
                if (node.State != CheckboxState.Checked)
                {
                    owner.ToggleNode(node);
                }
            }
            else
            {
                owner.PickNode(node);
            }
        }

        public ToggleState ToggleState => Row.Node?.State switch
        {
            CheckboxState.Checked => ToggleState.On,
            CheckboxState.Indeterminate => ToggleState.Indeterminate,
            _ => ToggleState.Off
        };

        public void Toggle()
        {
            if (Row.Node is { } node && Row.Owner is { SelectionMode: TreeSelectMode.Multiple } owner)
            {
                owner.ToggleNode(node);
            }
        }
    }
}

internal static class TreeSelectText
{
    public static string StateOf(CheckboxState state) => state switch
    {
        CheckboxState.Checked => TreeSelectLangResources.Get(TreeSelectLangResourceKind.Checked),
        CheckboxState.Indeterminate => TreeSelectLangResources.Get(TreeSelectLangResourceKind.PartiallyChecked),
        _ => TreeSelectLangResources.Get(TreeSelectLangResourceKind.NotChecked)
    };

    public static string Level(int depth) => TreeSelectLangResources.Format(TreeSelectLangResourceKind.Level, depth);

    public static string Count(int count) => TreeSelectLangResources.Format(TreeSelectLangResourceKind.SelectedCount, count);
}
