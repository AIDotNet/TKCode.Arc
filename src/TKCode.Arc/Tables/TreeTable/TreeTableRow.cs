using TKCode.Arc.DesignTokens;
using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

internal enum TreeTableLineKind
{
    Row,
    Loading,
    Error
}

/// <summary>Key of the loading / retry line under a parent that is still fetching (Arc: <c>`${id}:pending`</c>).</summary>
internal readonly record struct TreeTablePendingKey(object ParentKey);

/// <summary>One visible line of a <see cref="TreeTable"/>: a row, or the loading or retry line under a parent.</summary>
internal sealed class TreeTableLine
{
    public required TreeTableLineKind Kind { get; init; }
    public required object Key { get; init; }
    public object? Item { get; init; }
    public required int Depth { get; init; }
    public object? ParentKey { get; init; }
    public required IReadOnlyList<object> Ancestors { get; init; }
    public int Position { get; init; } = 1;
    public int Size { get; init; } = 1;
    public bool Expandable { get; init; }
    public bool Expanded { get; init; }
}

/// <summary>Draws the indent guides of a tree cell: one hairline through each ancestor's chevron column.</summary>
internal sealed class TreeTableGuides : Control
{
    private readonly TreeTable _owner;
    private IReadOnlyList<object> _ancestors = [];
    private int _levels;
    private object? _active;

    public TreeTableGuides(TreeTable owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
    }

    public void Update(IReadOnlyList<object> ancestors, int depth, object? activeBranch)
    {
        var levels = Math.Max(0, Math.Min(ancestors.Count, depth - 1));
        if (levels == _levels && ReferenceEquals(ancestors, _ancestors) && Equals(activeBranch, _active))
        {
            return;
        }
        _ancestors = ancestors;
        _levels = levels;
        _active = activeBranch;
        InvalidateVisual();
    }

    internal int Levels => _levels;

    internal bool IsLevelActive(int level) => level < _ancestors.Count && Equals(_ancestors[level], _active);

    public override void Render(DrawingContext context)
    {
        var palette = _owner.Palette;
        for (var level = 0; level < _levels; level++)
        {
            var pen = Equals(_ancestors[level], _active) ? palette.GuideActivePen : palette.GuidePen;
            var x = Math.Round(level * _owner.IndentWidth + _owner.ChevronExtent / 2) + 0.5;
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
        }
    }
}

/// <summary>
/// A visible row of a <see cref="TreeTable"/>: an optional checkbox cell, the tree cell (indent guides, chevron and
/// content) and one cell per other shown column on the table's shared tracks. Rows grow from zero height on a smooth
/// spring while their opacity fades in, and shrink away on exit; cells keep their full height so nothing re-centers
/// while a row opens.
/// </summary>
internal sealed class TreeTableRow : Grid
{
    internal const string SelectedPseudoClass = ":selected";
    internal const string PendingPseudoClass = ":pending";
    internal const string ExpandablePseudoClass = ":expandable";

    private readonly TreeTable _owner;
    private readonly List<Control> _cells = new();
    private readonly Border _divider = new();
    private TextBlock?[] _texts = [];
    private TreeTableGuides? _guides;
    private TreeTableGlyphButton? _chevron;
    private ArcSpinner? _spinner;
    private ContentPresenter? _treePresenter;
    private Checkbox? _check;
    private bool _syncingCheck;
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _shift;
    private object? _item;
    private bool _selected;
    private TreeTableLineKind _kind;
    private bool _exiting;
    private int _builtVersion = -1;

    public TreeTableRow(TreeTable owner, object key)
    {
        _owner = owner;
        Key = key;
        ClipToBounds = true;
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Arrow);
        RowDefinitions.Add(new RowDefinition(owner.RowHeight, GridUnitType.Pixel));
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
    }

    public object Key { get; }

    public TreeTableLine Line { get; private set; } = null!;

    public bool IsExiting => _exiting;

    /// <summary>The presence height factor, 0 (gone) to 1 (full), as drawn now.</summary>
    internal double Presence => _height?.Value ?? 1;

    internal double Opacity01 => _fade?.Value ?? 1;

    internal TreeTableGuides? Guides => _guides;

    internal TreeTableGlyphButton? Chevron => _chevron;

    internal Checkbox? Check => _check;

    internal object? Item => _item;

    internal Button? RetryButton { get; private set; }

    // Layout: the row's own height follows the presence value; the single grid row stays at its full height.
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width, Math.Max(0, Math.Round(_owner.RowHeight * Presence * 100) / 100));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(new Size(finalSize.Width, _owner.RowHeight));
        return finalSize;
    }

    /// <summary>Sets the content for a line. Cells are rebuilt only when the column set changed.</summary>
    public void Bind(TreeTableLine line, bool rebuild)
    {
        Line = line;
        _kind = line.Kind;
        _exiting = false;
        var item = line.Item;
        var itemChanged = !ReferenceEquals(item, _item);
        _item = item;
        if (rebuild || _builtVersion != _owner.ColumnVersion)
        {
            BuildCells();
        }
        else if (itemChanged)
        {
            RefreshValues();
        }
        PseudoClasses.Set(PendingPseudoClass, line.Kind != TreeTableLineKind.Row);
        PseudoClasses.Set(ExpandablePseudoClass, line.Expandable);
        UpdateTreeCell();
        AutomationProperties.SetName(this, line.Kind == TreeTableLineKind.Row
            ? _owner.LabelOf(line.Item!)
            : line.Kind == TreeTableLineKind.Loading ? "Loading" : "Couldn't load");
        if (line.Kind == TreeTableLineKind.Row)
        {
            AutomationProperties.SetPositionInSet(this, line.Position);
            AutomationProperties.SetSizeOfSet(this, line.Size);
            AutomationProperties.SetItemStatus(this, line.Expandable ? line.Expanded ? "expanded" : "collapsed" : string.Empty);
        }
    }

    /// <summary>Updates selection, check state, guides and the chevron without touching the cells.</summary>
    public void Sync(bool selected, CheckboxState check, object? activeBranch, bool loading, bool animate)
    {
        if (_selected != selected)
        {
            _selected = selected;
        }
        PseudoClasses.Set(SelectedPseudoClass, selected);
        if (_check is not null)
        {
            _syncingCheck = true;
            _check.CheckedState = check;
            _syncingCheck = false;
            AutomationProperties.SetName(_check, _owner.RowLabelFor(this));
        }
        _guides?.Update(Line.Ancestors, Line.Depth, activeBranch);
        if (_chevron is not null && Line.Kind == TreeTableLineKind.Row)
        {
            _chevron.IsVisible = Line.Expandable && !loading;
            _chevron.SetAngle(Line.Expanded ? 90 : 0, animate);
            _chevron.GlyphBrush = Line.Expanded ? _owner.Palette.Foreground : null;
        }
        if (_spinner is not null)
        {
            _spinner.IsVisible = loading;
        }
        PseudoClasses.Set(":busy", loading);
    }

    private void BuildCells()
    {
        _builtVersion = _owner.ColumnVersion;
        Children.Clear();
        ColumnDefinitions.Clear();
        _cells.Clear();
        _guides = null;
        _chevron = null;
        _spinner = null;
        _treePresenter = null;
        _check = null;
        RetryButton = null;
        var shown = _owner.ShownColumns;
        _texts = new TextBlock?[shown.Count];
        var column = 0;
        if (_owner.IsSelectable)
        {
            ColumnDefinitions.Add(new ColumnDefinition(_owner.CheckColumnWidth, GridUnitType.Pixel));
            if (_kind == TreeTableLineKind.Row)
            {
                _check = new Checkbox
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    IsTabStop = false,
                    Focusable = false
                };
                _check.CheckedStateChanged += OnCheckChanged;
                _check.PointerPressed += (_, e) => e.Handled = true;
                SetColumn(_check, 0);
                Children.Add(_check);
            }
            column = 1;
        }
        for (var i = 0; i < shown.Count; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition(shown[i].Width) { MinWidth = 0 });
            Control cell = i == 0 ? BuildTreeCell(shown[i]) : BuildCell(i, shown[i]);
            SetColumn(cell, column + i);
            Children.Add(cell);
            _cells.Add(cell);
        }
        _divider.Height = 1;
        _divider.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        _divider.IsHitTestVisible = false;
        _divider.Background = _owner.Palette.Divider;
        SetColumnSpan(_divider, Math.Max(1, ColumnDefinitions.Count));
        Children.Add(_divider);
        RefreshValues();
    }

    private Control BuildTreeCell(TreeTableColumn column)
    {
        var host = new Grid { Margin = new Thickness(_owner.TreeCellMargin, 0, 0, 0), Height = _owner.RowHeight, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        _guides = new TreeTableGuides(_owner);
        host.Children.Add(_guides);
        var inner = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness((Line.Depth - 1) * _owner.IndentWidth, 0, 12, 0)
        };
        if (_kind == TreeTableLineKind.Row)
        {
            var slot = new Grid { Width = _owner.ChevronExtent, Height = _owner.ChevronExtent, Margin = new Thickness(0, 0, 4, 0) };
            _chevron = new TreeTableGlyphButton(_owner, _owner.ChevronExtent, 8, isTabStop: false);
            _chevron.SetGlyph(ArcIcons.ChevronRight, animate: false);
            _chevron.Clicked += () => _owner.ToggleFromChevron(this);
            _spinner = new ArcSpinner { Width = 16, Height = 16, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, IsVisible = false };
            slot.Children.Add(_chevron);
            slot.Children.Add(_spinner);
            DockPanel.SetDock(slot, Dock.Left);
            inner.Children.Add(slot);
            _treePresenter = null;
            inner.Children.Add(BuildContent(0, column, treeColumn: true));
        }
        else
        {
            inner.Children.Add(BuildPending());
        }
        host.Children.Add(inner);
        return host;
    }

    private Control BuildPending()
    {
        var pad = _owner.ChevronExtent + 4;
        var stack = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(pad, 0, 0, 0)
        };
        if (_kind == TreeTableLineKind.Loading)
        {
            stack.Children.Add(new ArcSpinner { Width = 16, Height = 16, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            stack.Children.Add(Text("Loading", _owner.Palette.TextMuted));
        }
        else
        {
            stack.Children.Add(Text("Couldn't load", _owner.Palette.Danger));
            var retry = new Button
            {
                Content = "Retry",
                Variant = ButtonVariant.Secondary,
                Size = ControlSize.Small,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            retry.Click += (_, _) => _owner.Retry(this);
            RetryButton = retry;
            AutomationProperties.SetName(retry, "Retry");
            stack.Children.Add(retry);
        }
        return stack;
    }

    private TextBlock Text(string text, IBrush? brush) => new()
    {
        Text = text,
        Foreground = brush,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap
    };

    private Control BuildCell(int index, TreeTableColumn column)
    {
        if (_kind != TreeTableLineKind.Row)
        {
            // A quiet skeleton bar stands in for the values that are still loading.
            if (_kind == TreeTableLineKind.Loading)
            {
                return new Border
                {
                    Height = 10,
                    Width = 72,
                    Background = _owner.Palette.Skeleton,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = column.IsEndAligned ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left,
                    Margin = new Thickness(_owner.CellInlinePadding, 0)
                }.BindCornerRadius(ArcDesignTokenKind.RadiusStep5);
            }
            return new Border();
        }
        var content = BuildContent(index, column, treeColumn: false);
        content.Margin = new Thickness(_owner.CellInlinePadding, 0);
        content.HorizontalAlignment = column.IsEndAligned ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        var host = new Grid { Height = _owner.RowHeight, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, ClipToBounds = true };
        host.Children.Add(content);
        return host;
    }

    private Control BuildContent(int index, TreeTableColumn column, bool treeColumn)
    {
        if (column.CellTemplate is { } template)
        {
            var presenter = new ContentPresenter
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = treeColumn ? Avalonia.Layout.HorizontalAlignment.Stretch : Avalonia.Layout.HorizontalAlignment.Left,
                Content = _item,
                ContentTemplate = template
            };
            if (treeColumn)
            {
                _treePresenter = presenter;
            }
            else
            {
                _presenters[index] = presenter;
            }
            return presenter;
        }
        var text = new TextBlock
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = treeColumn ? _owner.Palette.Foreground : _owner.Palette.TextSecondary
        };
        if (column.IsEndAligned)
        {
            text.FontFeatures = TreeTablePalette.TabularNumbers;
            text.TextAlignment = TextAlignment.Right;
        }
        _texts[index] = text;
        return text;
    }

    private readonly Dictionary<int, ContentPresenter> _presenters = new();

    private void RefreshValues()
    {
        if (_kind != TreeTableLineKind.Row || _item is null)
        {
            return;
        }
        var shown = _owner.ShownColumns;
        for (var i = 0; i < shown.Count; i++)
        {
            if (shown[i].CellTemplate is not null)
            {
                var presenter = i == 0 ? _treePresenter : _presenters.GetValueOrDefault(i);
                if (presenter is not null)
                {
                    // Setting the same item again must still rebuild the template's content for a changed value.
                    presenter.Content = null;
                    presenter.Content = _item;
                }
            }
            else if (i < _texts.Length && _texts[i] is { } text)
            {
                text.Text = _owner.CellText(shown[i], _item);
            }
        }
    }

    private void UpdateTreeCell()
    {
        // Tree indent depends on the depth, which is fixed per row key (a row never changes level).
        if (_cells.Count > 0 && _cells[0] is Grid host && host.Children.Count > 1 && host.Children[1] is DockPanel inner)
        {
            inner.Margin = new Thickness((Line.Depth - 1) * _owner.IndentWidth, 0, 12, 0);
        }
    }

    private void OnCheckChanged(object? sender, CheckboxStateChangedEventArgs e)
    {
        if (!_syncingCheck && Line.Kind == TreeTableLineKind.Row)
        {
            _owner.ToggleSelect(Key);
        }
    }

    internal void RefreshPalette()
    {
        _divider.Background = _owner.Palette.Divider;
        _builtVersion = -1;
    }

    #region Presence

    /// <summary>Starts the row's enter: height from zero (or full when it takes over a loading line) and a fade, staggered by position.</summary>
    public void Enter(bool animate, bool takeOver, int position)
    {
        _exiting = false;
        var full = ArcMotion.IsFull(this);
        if (!animate || !this.IsAttachedToVisualTree() || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _height?.Jump(1);
            _fade?.Jump(1);
            Opacity = 1;
            InvalidateMeasure();
            return;
        }
        var delay = full ? TimeSpan.FromMilliseconds(Math.Min(position - 1, 8) * 18) : TimeSpan.Zero;
        EnsureMotion(full ? takeOver ? 1 : 0 : 1, 0);
        Opacity = _fade!.Value;
        if (full)
        {
            _ = _height!.AnimateTo(1, MotionSpec.Spring(ArcMotionPresets.Smooth));
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
        }
        else
        {
            // Reduced motion: no height spring, no stagger; rows appear with a short fade.
            _height!.Jump(1);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
    }

    /// <summary>A row that was leaving comes back (its parent opened again): it grows from where it is.</summary>
    public void Revive(bool animate)
    {
        _exiting = false;
        IsHitTestVisible = true;
        if (_height is null || !animate || !ArcMotion.IsFull(this))
        {
            _height?.Jump(1);
            _fade?.Jump(1);
            Opacity = 1;
            InvalidateMeasure();
            return;
        }
        _ = _height.AnimateTo(1, MotionSpec.Spring(ArcMotionPresets.Smooth));
        _ = _fade!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    /// <summary>Starts the row's exit; <paramref name="removed"/> runs when it is gone.</summary>
    public void Exit(bool immediate, Action<TreeTableRow> removed)
    {
        _exiting = true;
        IsHitTestVisible = false;
        if (immediate || !this.IsAttachedToVisualTree() || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            removed(this);
            return;
        }
        EnsureMotion(_height?.Value ?? 1, _fade?.Value ?? 1);
        if (ArcMotion.IsFull(this))
        {
            _ = _fade!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            var done = _height!.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Smooth));
            done.ContinueWith(task =>
            {
                if (task.Result && _exiting)
                {
                    removed(this);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        else
        {
            _height!.Jump(1);
            _ = _fade!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard))
                .ContinueWith(task =>
                {
                    if (task.Result && _exiting)
                    {
                        removed(this);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    private void EnsureMotion(double height, double fade)
    {
        if (_height is null)
        {
            _height = new MotionValue<double>(this, height, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
            _fade = new MotionValue<double>(this, fade, MotionVectors.Double);
            _fade.Changed += value => Opacity = Math.Clamp(value, 0, 1);
        }
        else
        {
            _height.Jump(height);
            _fade!.Jump(fade);
        }
        Opacity = Math.Clamp(_fade!.Value, 0, 1);
    }

    /// <summary>FLIP after a sort: the row starts <paramref name="delta"/> px away from its new place and springs home.</summary>
    public void Slide(double delta)
    {
        if (Math.Abs(delta) < 0.5 || !ArcMotion.IsFull(this) || !this.IsAttachedToVisualTree())
        {
            return;
        }
        if (_shift is null)
        {
            _shift = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shift.Changed += value => RenderTransform = new TranslateTransform(0, value);
        }
        _shift.Jump(delta);
        RenderTransform = new TranslateTransform(0, delta);
        _ = _shift.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Morph));
    }

    #endregion

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && Line.Kind == TreeTableLineKind.Row)
        {
            Focus(NavigationMethod.Pointer);
            _owner.OnRowClicked(this);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this))
        {
            _owner.OnRowFocused(this);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _owner.OnRowHover(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner.OnRowHover(this, false);
    }
}
