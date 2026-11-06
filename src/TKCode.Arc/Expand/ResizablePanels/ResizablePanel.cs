using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// One pane of a <see cref="ResizablePanels"/> group. It carries the pane's configuration (label, default share,
/// limits, collapsibility) and shows its content. Below its minimum the content keeps its minimum size along the
/// group's axis (width, or height in a vertical group), stays anchored to the moving edge and fades, so a closing pane slides away like a drawer instead of being crushed.
/// </summary>
public class ResizablePanel : ContentControl
{
    /// <summary>Stable id (dividers reference panes by id).</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<ResizablePanel, string?>(nameof(Id));

    /// <summary>Names the pane for its divider, for example "Folders".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ResizablePanel, string?>(nameof(Label));

    /// <summary>Starting share of the group; shares are relative and need not add up to 100.</summary>
    public static readonly StyledProperty<double> DefaultSizeProperty =
        AvaloniaProperty.Register<ResizablePanel, double>(nameof(DefaultSize), 1);

    /// <summary>Size in px where the pane starts to resist (Arc: 80): its width, or its height in a vertical group. Its content never reflows below it.</summary>
    public static readonly StyledProperty<double> MinSizeProperty =
        AvaloniaProperty.Register<ResizablePanel, double>(nameof(MinSize), ResizablePanelsMath.DefaultMin);

    /// <summary>Maximum size in px along the group's axis: width, or height in a vertical group (infinite by default).</summary>
    public static readonly StyledProperty<double> MaxSizeProperty =
        AvaloniaProperty.Register<ResizablePanel, double>(nameof(MaxSize), double.PositiveInfinity);

    /// <summary>Dragging well past the minimum, or flicking toward the edge, snaps the pane closed.</summary>
    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<ResizablePanel, bool>(nameof(IsCollapsible));

    /// <summary>Whether the pane is hidden. Two-way; the initial value is Arc's defaultCollapsed (needs <see cref="IsCollapsible"/>).</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<ResizablePanel, bool>(nameof(IsCollapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DefaultSizeProperty"/>
    public double DefaultSize { get => GetValue(DefaultSizeProperty); set => SetValue(DefaultSizeProperty, value); }

    /// <inheritdoc cref="MinSizeProperty"/>
    public double MinSize { get => GetValue(MinSizeProperty); set => SetValue(MinSizeProperty, value); }

    /// <inheritdoc cref="MaxSizeProperty"/>
    public double MaxSize { get => GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }

    /// <inheritdoc cref="IsCollapsibleProperty"/>
    public bool IsCollapsible { get => GetValue(IsCollapsibleProperty); set => SetValue(IsCollapsibleProperty, value); }

    /// <inheritdoc cref="IsCollapsedProperty"/>
    public bool IsCollapsed { get => GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }

    /// <summary>Set by the group: the content keeps at least this size along the axis (the effective minimum).</summary>
    internal double ContentMin { get; set; }

    /// <summary>Set by the group: the first pane anchors its content to its trailing (or bottom) edge.</summary>
    internal bool AnchorEnd { get; set; }

    /// <summary>Set by the group: the axis the pane is sized along.</summary>
    internal ResizablePanelsAxis Axis { get; set; }

    /// <summary>Raised when a configuration property the group depends on changes.</summary>
    internal event Action<ResizablePanel, AvaloniaProperty>? ConfigChanged;

    public ResizablePanel()
    {
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        if (change.Property == DefaultSizeProperty || change.Property == MinSizeProperty || change.Property == MaxSizeProperty ||
            change.Property == IsCollapsibleProperty || change.Property == IsCollapsedProperty)
        {
            ConfigChanged?.Invoke(this, change.Property);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var axis = Axis;
        var available = axis.Main(availableSize);
        var main = double.IsInfinity(available) ? available : Math.Max(available, ContentMin);
        var size = base.MeasureOverride(axis.Size(main, axis.Cross(availableSize)));
        return axis.Size(Math.Min(axis.Main(size), available), axis.Cross(size));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var axis = Axis;
        var length = axis.Main(finalSize);
        var main = Math.Max(length, ContentMin);
        var offset = AnchorEnd ? length - main : 0;
        foreach (var child in VisualChildren)
        {
            if (child is Avalonia.Layout.Layoutable layoutable)
            {
                layoutable.Arrange(axis.Rect(offset, 0, main, axis.Cross(finalSize)));
            }
        }
        return finalSize;
    }
}
