using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The root panel of an Arc field's template: the label (the first child) and the field's rows (the frame, then the
/// description and error rows). With the label on top (<see cref="FormLabelPlacement.Top"/>) it stacks every visible child
/// like a vertical <see cref="StackPanel"/>. With <see cref="Field.LabelPlacementProperty"/> Left the label takes a column
/// as tall as a field of the same <see cref="Size"/> (centred on it, top-aligned once it wraps taller), the other rows stack
/// in the second column, and the label's own margin (the gap to the editor below it) is left out.
/// </summary>
/// <remarks>
/// The placement and width come from the field that owns the template (its <c>TemplatedParent</c>); a <see cref="Field"/> that
/// is part of another control's template stands for that control (the pickers, TagInput, MoneyInput). A field inside a
/// <see cref="FormItem"/> keeps the top layout (the item draws the label), and a field inside another control's template
/// follows only a value set on itself, so a container's placement does not reach the editors inside other controls.
/// With the width NaN the column takes the widest label of the scope: the nearest element, from the field up, that sets the
/// placement or the width, or that is a <c>Grid.IsSharedSizeScope</c>.
/// </remarks>
internal sealed class FieldLayout : Panel
{
    /// <summary>Space between visible rows (as <see cref="StackPanel.Spacing"/>; with the label on the left, between the rows of the second column).</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        StackPanel.SpacingProperty.AddOwner<FieldLayout>();

    /// <summary>The field size: the height of the left label row (Small 36, Medium 44, Large 50).</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<FieldLayout, ControlSize>(nameof(Size), ControlSize.Medium);

    // Token values, bound only while the label is on the left.
    private static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<FieldLayout, double>("ColumnGap", 16);

    private static readonly StyledProperty<double> AutoLabelMaxWidthProperty =
        AvaloniaProperty.Register<FieldLayout, double>("AutoLabelMaxWidth", 240);

    private static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<FieldLayout, double>("RowHeight", 44);

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private IDisposable? _gapBinding;
    private IDisposable? _maxBinding;
    private IDisposable? _rowBinding;
    private FieldLabelColumn? _column;
    private double _labelX;
    private double _columnWidth;
    private double _gap;
    private double _rowHeight;

    static FieldLayout()
    {
        AffectsMeasure<FieldLayout>(SpacingProperty, ColumnGapProperty, AutoLabelMaxWidthProperty, RowHeightProperty);
    }

    /// <summary>Whether the last measure laid the label out on the left.</summary>
    internal bool IsLabelLeft { get; private set; }

    /// <summary>The width of the label column of the last measure (0 with the label on top).</summary>
    internal double LabelColumnWidth => IsLabelLeft ? _columnWidth : 0;

    /// <summary>
    /// The control the label belongs to: the templated parent, or the control a <see cref="Field"/> in its template stands
    /// for. A template that sets the placement on its field (ColorPicker: Top) keeps it.
    /// </summary>
    internal Control? Owner
    {
        get
        {
            var owner = TemplatedParent as Control;
            while (owner is Field and not FormItem && !owner.IsSet(Field.LabelPlacementProperty) && owner.TemplatedParent is Control parent)
            {
                owner = parent;
            }
            return owner;
        }
    }

    /// <summary>The placement that applies to <paramref name="owner"/> (see the remarks of <see cref="FieldLayout"/>).</summary>
    internal static FormLabelPlacement PlacementOf(Control? owner)
    {
        if (owner is null || FormFieldHost.IsHosted(owner))
        {
            return FormLabelPlacement.Top;
        }
        if (owner.TemplatedParent is not null && !owner.IsSet(Field.LabelPlacementProperty))
        {
            return FormLabelPlacement.Top;
        }
        return owner.GetValue(Field.LabelPlacementProperty);
    }

    private static double LabelWidthOf(Control owner)
        => owner.TemplatedParent is not null && !owner.IsSet(Field.LabelWidthProperty) ? double.NaN : owner.GetValue(Field.LabelWidthProperty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // All three are inherited from the field, so a change on it (or on a container above it) arrives here as well.
        if (change.Property == Field.LabelPlacementProperty || change.Property == Field.LabelWidthProperty
            || change.Property == FormFieldHost.StateProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == SizeProperty && _rowBinding is not null)
        {
            BindRowHeight();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LeaveColumn();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The scope may differ in the new place.
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        var left = Children.Count > 0 && PlacementOf(owner) == FormLabelPlacement.Left;
        SetLeft(left);
        if (!left)
        {
            LeaveColumn();
            return MeasureStack(0, availableSize);
        }

        var label = Children[0];
        var width = LabelWidthOf(owner!);
        double column;
        if (!double.IsNaN(width) && width >= 0)
        {
            LeaveColumn();
            column = width;
        }
        else
        {
            var own = 0.0;
            if (label.IsVisible)
            {
                var cap = Math.Min(GetValue(AutoLabelMaxWidthProperty), availableSize.Width);
                label.Measure(new Size(cap, double.PositiveInfinity));
                own = Math.Min(label.DesiredSize.Width - HorizontalMargin(label), cap);
            }
            column = JoinColumn(owner!).Report(this, Math.Max(0, own));
        }
        if (!double.IsInfinity(availableSize.Width))
        {
            column = Math.Min(column, availableSize.Width);
        }
        _columnWidth = column;
        // An empty column (no label anywhere in the scope) takes no gap either.
        _gap = column > 0 ? GetValue(ColumnGapProperty) : 0;
        _rowHeight = GetValue(RowHeightProperty);

        var labelHeight = 0.0;
        if (label.IsVisible)
        {
            var margin = label.Margin;
            label.Measure(new Size(column + margin.Left + margin.Right, double.PositiveInfinity));
            labelHeight = Math.Max(0, label.DesiredSize.Height - margin.Top - margin.Bottom);
        }
        var bodyWidth = Math.Max(0, availableSize.Width - column - _gap);
        var body = MeasureStack(1, availableSize.WithWidth(bodyWidth));
        return new Size(column + _gap + body.Width, Math.Max(Math.Max(labelHeight, _rowHeight), body.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!IsLabelLeft)
        {
            ArrangeStack(0, 0, finalSize.Width);
            return finalSize;
        }
        var label = Children[0];
        if (label.IsVisible)
        {
            // The label's margin (its gap to an editor below it) does not apply beside the editor: arrange it outside.
            var margin = label.Margin;
            var height = Math.Max(0, label.DesiredSize.Height - margin.Top - margin.Bottom);
            var y = height < _rowHeight ? (_rowHeight - height) / 2 : 0;
            var width = _columnWidth + margin.Left + margin.Right;
            label.Arrange(new Rect(-margin.Left, y - margin.Top, width, height + margin.Top + margin.Bottom));
        }
        _labelX = _columnWidth + _gap;
        ArrangeStack(1, _labelX, Math.Max(0, finalSize.Width - _labelX));
        return finalSize;
    }

    private Size MeasureStack(int start, Size available)
    {
        var spacing = Spacing;
        double width = 0, height = 0;
        var any = false;
        var children = Children;
        for (var i = start; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(available.Width, double.PositiveInfinity));
            if (any)
            {
                height += spacing;
            }
            any = true;
            height += child.DesiredSize.Height;
            width = Math.Max(width, child.DesiredSize.Width);
        }
        return new Size(width, height);
    }

    private void ArrangeStack(int start, double x, double width)
    {
        var spacing = Spacing;
        var y = 0.0;
        var any = false;
        var children = Children;
        for (var i = start; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            if (any)
            {
                y += spacing;
            }
            any = true;
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(x, y, width, height));
            y += height;
        }
    }

    private static double HorizontalMargin(Layoutable element) => element.Margin.Left + element.Margin.Right;

    private void SetLeft(bool left)
    {
        if (IsLabelLeft == left)
        {
            return;
        }
        IsLabelLeft = left;
        if (left)
        {
            // Only fields with the label on the left pay for these subscriptions.
            _gapBinding = this.Bind(ColumnGapProperty, this.GetResourceObservable(FormItemTokens.Key(FormItemTokenKind.LabelColumnGap)));
            _maxBinding = this.Bind(AutoLabelMaxWidthProperty, this.GetResourceObservable(FormTokens.Key(FormTokenKind.AutoLabelMaxWidth)));
            BindRowHeight();
        }
        else
        {
            _gapBinding?.Dispose();
            _maxBinding?.Dispose();
            _rowBinding?.Dispose();
            _gapBinding = _maxBinding = _rowBinding = null;
        }
    }

    private void BindRowHeight()
    {
        _rowBinding?.Dispose();
        var kind = Size switch
        {
            ControlSize.Small => ArcDesignTokenKind.ControlSizeSM,
            ControlSize.Large => ArcDesignTokenKind.ControlSizeLG,
            _ => ArcDesignTokenKind.ControlSizeMD
        };
        _rowBinding = this.Bind(RowHeightProperty, this.GetResourceObservable(ArcDesignTokens.Key(kind)));
    }

    private FieldLabelColumn JoinColumn(Control owner)
    {
        var scope = FieldLabelColumn.ScopeOf(owner);
        var column = FieldLabelColumn.Get(scope);
        if (!ReferenceEquals(column, _column))
        {
            LeaveColumn();
            _column = column;
        }
        return column;
    }

    private void LeaveColumn()
    {
        _column?.Remove(this);
        _column = null;
    }
}

/// <summary>
/// The automatic label column shared by the left labels of one scope (<see cref="FieldLayout"/>): every member reports the
/// natural width of its label and takes the widest one; when the widest changes the other members measure again.
/// </summary>
internal sealed class FieldLabelColumn
{
    private static readonly AttachedProperty<FieldLabelColumn?> ColumnProperty =
        AvaloniaProperty.RegisterAttached<FieldLabelColumn, Control, FieldLabelColumn?>("FieldLabelColumn");

    private readonly Dictionary<FieldLayout, double> _widths = new();
    private double _max;

    /// <summary>The members (tests).</summary>
    internal int Count => _widths.Count;

    /// <summary>The scope of <paramref name="owner"/>: the nearest element, from it up, that sets the placement or the width, or is a shared size scope.</summary>
    public static Control ScopeOf(Control owner)
    {
        for (Visual? visual = owner; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control control
                && (control.IsSet(Field.LabelPlacementProperty) || control.IsSet(Field.LabelWidthProperty) || Grid.GetIsSharedSizeScope(control)))
            {
                return control;
            }
        }
        return owner;
    }

    public static FieldLabelColumn Get(Control scope)
    {
        var column = scope.GetValue(ColumnProperty);
        if (column is null)
        {
            column = new FieldLabelColumn();
            scope.SetValue(ColumnProperty, column);
        }
        return column;
    }

    /// <summary>Records <paramref name="layout"/>'s label width and returns the column width.</summary>
    public double Report(FieldLayout layout, double width)
    {
        if (_widths.TryGetValue(layout, out var old) && old == width)
        {
            return _max;
        }
        _widths[layout] = width;
        Update(layout);
        return _max;
    }

    public void Remove(FieldLayout layout)
    {
        if (_widths.Remove(layout))
        {
            Update(null);
        }
    }

    private void Update(FieldLayout? source)
    {
        var max = 0.0;
        foreach (var width in _widths.Values)
        {
            max = Math.Max(max, width);
        }
        if (max == _max)
        {
            return;
        }
        _max = max;
        foreach (var member in _widths.Keys)
        {
            if (!ReferenceEquals(member, source))
            {
                member.InvalidateMeasure();
            }
        }
    }
}
