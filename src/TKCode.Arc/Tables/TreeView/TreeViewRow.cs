using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>A visible node of a <see cref="TreeView"/> (row model; the key is <see cref="Item"/>).</summary>
internal sealed class TreeViewNode
{
    public TreeViewNode(object item) => Item = item;

    public object Item { get; }
    public TreeViewNode? Parent { get; set; }
    public int Depth { get; set; } = 1;
    public int Position { get; set; } = 1;
    public int SetSize { get; set; } = 1;
    public string Label { get; set; } = string.Empty;
    public Geometry? Icon { get; set; }
    public bool HasChildren { get; set; }
    public bool IsExpanded { get; set; }
}

/// <summary>Resolved colors, pens and metrics shared by all rows of one tree.</summary>
internal sealed class TreeViewPalette
{
    private readonly Dictionary<(IBrush, double), IPen> _pens = new();
    private int _penVersion = -1;

    public int Version { get; set; }
    public Color Foreground { get; set; }
    public Color ActiveForeground { get; set; }
    public IBrush? HoverFill { get; set; }
    public IBrush? Muted { get; set; }
    public IBrush? Guide { get; set; }
    public IPen? FocusRing { get; set; }
    public IBrush? TypeScript { get; set; }
    public IBrush? Stylesheet { get; set; }
    public IBrush? Markdown { get; set; }
    public IBrush? Json { get; set; }
    public IBrush? FolderRest { get; set; }
    public IBrush? FolderActive { get; set; }
    public FontFamily FontFamily { get; set; } = FontFamily.Default;
    public double FontSize { get; set; } = 14;
    public double SlotHeight { get; set; } = 38;
    public double RowGap { get; set; } = 2;
    public double RowRadius { get; set; } = 10;
    public double Indent { get; set; } = 18;
    public double InlinePadding { get; set; } = 10;
    public double Gap { get; set; } = 8;
    public bool ShowGuides { get; set; } = true;
    public bool ShowIcons { get; set; } = true;

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

/// <summary>The TreeView's row engine: rows are <see cref="TreeViewRow"/>s keyed by their item.</summary>
internal sealed class TreeViewRowsPanel : TablesTreeRowsPanel
{
    public TreeViewRowsPanel()
    {
        Motion = new TablesTreeRowMotion
        {
            Stagger = true,
            EnterOffset = -6,
            ExitOffset = -4,
            EnterFade = ArcMotionPresets.Fast,
            EnterFadeEasing = ArcEasings.Enter,
            ExitFade = ArcMotionPresets.Instant,
            RevealDuration = ArcMotionPresets.Standard
        };
    }

    public TreeView? Owner { get; set; }

    protected override TablesTreeRow CreateRow() => new TreeViewRow(this);

    protected override object KeyOf(object model) => ((TreeViewNode)model).Item;
}

/// <summary>
/// One tree row, drawn directly: hover fill, branch line, chevron (turns on the snappy spring), folder or file
/// icon (the folder pops open), the label (ellipsis) and the focus ring. The selection fill is the panel's
/// gliding highlight underneath. Its motion (chevron spring, folder pop, hover and label color) uses
/// <see cref="MotionValue{T}"/> because the row draws these parts itself; there are no child properties for
/// ControlTheme transitions to target.
/// </summary>
internal sealed class TreeViewRow : TablesTreeRow
{
    private readonly TreeViewRowsPanel _panel;
    private readonly SolidColorBrush _labelBrush = new();
    private MotionValue<double>? _hover;
    private MotionValue<double>? _emphasis;
    private MotionValue<double>? _rotation;
    private MotionValue<double>? _swap;
    private bool _wasExpanded;
    private bool _pressed;
    private TextLayout? _layout;
    private string? _layoutText;
    private double _layoutWidth = double.NaN;
    private int _layoutVersion = -1;
    private double _naturalWidth = double.NaN;

    public TreeViewRow(TreeViewRowsPanel panel)
    {
        _panel = panel;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    private TreeView? Owner => _panel.Owner;

    private TreeViewPalette? Palette => Owner?.Palette;

    public TreeViewNode? Node => Model as TreeViewNode;

    public bool IsSelected => Node is { } node && Owner is { } owner && Equals(owner.SelectedItem, node.Item);

    /// <summary>Current chevron angle in degrees (tests).</summary>
    internal double ChevronAngle => _rotation?.Value ?? (Node?.IsExpanded == true ? 90 : 0);

    protected override void OnBound(object? previous, bool sameKey)
    {
        var node = Node;
        if (node is null)
        {
            _layout = null;
            _layoutText = null;
            _hover?.Jump(0);
            _pressed = false;
            IsTabStop = false;
            return;
        }
        var owner = Owner;
        IsTabStop = owner?.IsTabStopKey(node.Item) == true && !IsGhost;
        var full = IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var angle = node.IsExpanded ? 90.0 : 0.0;
        if (sameKey && node.IsExpanded != _wasExpanded && full)
        {
            _rotation ??= CreateValue(_wasExpanded ? 90 : 0);
            _ = _rotation.AnimateTo(angle, MotionSpec.Spring(ArcSpring.Snappy));
            _swap ??= CreateValue(1);
            _swap.Jump(0);
            _ = _swap.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(300), ArcEasings.Enter));
        }
        else if (!sameKey || node.IsExpanded != _wasExpanded)
        {
            _rotation?.Jump(angle);
            _swap?.Jump(1);
        }
        _wasExpanded = node.IsExpanded;
        if (!sameKey)
        {
            _hover?.Jump(IsPointerOver ? 1 : 0);
            _naturalWidth = double.NaN;
        }
        var target = IsSelected || (_hover?.Target ?? 0) > 0.5 ? 1.0 : 0.0;
        if (!sameKey || !full)
        {
            _emphasis?.Jump(target);
        }
        else if (_emphasis is not null && Math.Abs(_emphasis.Target - target) > 0.001)
        {
            _ = _emphasis.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        if (!ReferenceEquals(_layoutText, node.Label))
        {
            _layout = null;
        }
        AutomationProperties.SetName(this, node.Label);
        AutomationProperties.SetPositionInSet(this, node.Position);
        AutomationProperties.SetSizeOfSet(this, node.SetSize);
        AutomationProperties.SetHelpText(this, string.Create(CultureInfo.InvariantCulture, $"level {node.Depth}"));
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    #region Input

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch && Node is not null && !IsGhost)
        {
            AnimateHover(1);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pressed = false;
        AnimateHover(0);
    }

    private void AnimateHover(double target)
    {
        _hover ??= CreateValue(0);
        _emphasis ??= CreateValue(IsSelected ? 1 : 0);
        var emphasis = Math.Max(target, IsSelected ? 1 : 0);
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _hover.Jump(target);
            _emphasis.Jump(emphasis);
            return;
        }
        var spec = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = _hover.AnimateTo(target, spec);
        _ = _emphasis.AnimateTo(emphasis, spec);
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
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner?.OnRowClicked(this);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnRowFocused(this);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TreeViewRowAutomationPeer(this);

    internal void ExpandFromAutomation(bool expand)
    {
        if (Node is { HasChildren: true } node && Owner is { } owner)
        {
            if (expand)
            {
                owner.Expand(node.Item);
            }
            else
            {
                owner.Collapse(node.Item);
            }
        }
    }

    internal void SelectFromAutomation()
    {
        if (Node is { } node && Owner is { } owner)
        {
            owner.SelectedItem = node.Item;
        }
    }

    #endregion

    #region Layout and rendering

    private double LabelX(TreeViewPalette p, TreeViewNode node)
    {
        var x = p.InlinePadding + (node.Depth - 1) * p.Indent + 14 + p.Gap;
        return p.ShowIcons ? x + 16 + p.Gap : x;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var p = Palette;
        var node = Node;
        var height = p?.SlotHeight ?? 38;
        if (p is null || node is null)
        {
            return new Size(0, height);
        }
        if (!double.IsInfinity(availableSize.Width))
        {
            return new Size(availableSize.Width, height);
        }
        if (double.IsNaN(_naturalWidth))
        {
            using var natural = CreateLayout(p, node.Label, double.PositiveInfinity);
            _naturalWidth = natural.WidthIncludingTrailingWhitespace;
        }
        return new Size(LabelX(p, node) + _naturalWidth + p.InlinePadding, height);
    }

    private TextLayout CreateLayout(TreeViewPalette p, string text, double maxWidth)
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
        var item = new Rect(0, gap, width, itemHeight);
        var centerY = gap + itemHeight / 2;

        var hover = _hover?.Value ?? 0;
        if (hover > 0.001 && p.HoverFill is { } hoverFill)
        {
            using (context.PushOpacity(hover))
            {
                context.DrawRectangle(hoverFill, null, new RoundedRect(item, p.RowRadius));
            }
        }

        var left = p.InlinePadding + (node.Depth - 1) * p.Indent;
        if (p.ShowGuides && node.Depth > 1 && p.PenFor(p.Guide, 1) is { } guide)
        {
            // Arc: a 1px line under the parent's chevron, inset 4px, growing from the top as the row enters.
            var x = p.InlinePadding + 7 + (node.Depth - 2) * p.Indent;
            var top = gap + 4;
            var length = (itemHeight - 8) * Reveal;
            if (length > 0.5)
            {
                using (context.PushOpacity(Reveal))
                {
                    context.DrawLine(guide, new Point(x, top), new Point(x, top + length));
                }
            }
        }

        if (node.HasChildren && p.PenFor(p.Muted, 1.9) is { } chevronPen)
        {
            DrawGlyph(context, ArcIcons.ChevronRight, chevronPen, new Point(left + 7, centerY), 14, ChevronAngle, 1, 1);
        }

        var labelX = LabelX(p, node);
        if (p.ShowIcons)
        {
            DrawIcon(context, p, node, new Point(left + 14 + p.Gap + 8, centerY));
        }

        var emphasis = _emphasis?.Value ?? (IsSelected ? 1 : 0);
        _labelBrush.Color = Lerp(p.Foreground, p.ActiveForeground, Math.Clamp(emphasis, 0, 1));
        var maxWidth = width - labelX - p.InlinePadding;
        if (_layout is null || _layoutVersion != p.Version || Math.Abs(_layoutWidth - maxWidth) > 0.5 || !ReferenceEquals(_layoutText, node.Label))
        {
            _layout?.Dispose();
            _layout = CreateLayout(p, node.Label, maxWidth);
            _layoutText = node.Label;
            _layoutWidth = maxWidth;
            _layoutVersion = p.Version;
        }
        _layout.Draw(context, new Point(labelX, centerY - _layout.Height / 2));

        if (IsFocused && p.FocusRing is { } ring && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, ring, new RoundedRect(item.Deflate(1), Math.Max(0, p.RowRadius - 1)));
        }
    }

    private void DrawIcon(DrawingContext context, TreeViewPalette p, TreeViewNode node, Point center)
    {
        if (node.Icon is { } custom)
        {
            var brush = node.HasChildren ? (node.IsExpanded || IsSelected ? p.FolderActive : p.FolderRest) : ToneOf(p, node.Label);
            if (p.PenFor(brush, 1.7) is { } pen)
            {
                DrawGlyph(context, custom, pen, center, 16, 0, 1, 1);
            }
            return;
        }
        if (!node.HasChildren)
        {
            if (p.PenFor(ToneOf(p, node.Label), 1.7) is { } filePen)
            {
                DrawGlyph(context, ArcIcons.File, filePen, center, 16, 0, 1, 1);
            }
            return;
        }
        var active = node.IsExpanded || IsSelected;
        if (p.PenFor(active ? p.FolderActive : p.FolderRest, 1.7) is not { } folderPen)
        {
            return;
        }
        var swap = Math.Clamp(_swap?.Value ?? 1, 0, 1);
        var current = node.IsExpanded ? ArcIcons.FolderOpen : ArcIcons.Folder;
        if (swap >= 0.999)
        {
            DrawGlyph(context, current, folderPen, center, 16, 0, 1, 1);
            return;
        }
        // Pop: the new glyph grows from 0.6 while fading in; the old one shrinks away faster.
        var outgoing = node.IsExpanded ? ArcIcons.Folder : ArcIcons.FolderOpen;
        var exit = Math.Clamp(swap / 0.4, 0, 1);
        DrawGlyph(context, outgoing, folderPen, center, 16, 0, 1 - 0.4 * exit, 1 - exit);
        DrawGlyph(context, current, folderPen, center, 16, 0, 0.6 + 0.4 * swap, Math.Clamp(swap / 0.55, 0, 1));
    }

    private static IBrush? ToneOf(TreeViewPalette p, string label)
    {
        if (label.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) || label.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            return p.TypeScript;
        }
        if (label.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            return p.Stylesheet;
        }
        if (label.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return p.Markdown;
        }
        if (label.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return p.Json;
        }
        return p.Muted;
    }

    private static void DrawGlyph(DrawingContext context, Geometry geometry, IPen pen, Point center, double size, double degrees,
                                  double scale, double opacity)
    {
        if (opacity <= 0.001 || scale <= 0.001)
        {
            return;
        }
        var s = size / 24.0 * scale;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(s, s);
        if (Math.Abs(degrees) > 0.01)
        {
            matrix *= Matrix.CreateRotation(degrees * Math.PI / 180);
        }
        matrix *= Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    #endregion

    private sealed class TreeViewRowAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider
    {
        public TreeViewRowAutomationPeer(TreeViewRow owner) : base(owner)
        {
        }

        private TreeViewRow Row => (TreeViewRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

        protected override string? GetNameCore() => Row.Node?.Label;

        public ExpandCollapseState ExpandCollapseState => Row.Node is not { HasChildren: true } node
            ? ExpandCollapseState.LeafNode
            : node.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => Row.ExpandFromAutomation(true);

        public void Collapse() => Row.ExpandFromAutomation(false);

        public bool IsSelected => Row.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Row.SelectFromAutomation();

        public void RemoveFromSelection()
        {
        }

        public void Select() => Row.SelectFromAutomation();
    }
}
