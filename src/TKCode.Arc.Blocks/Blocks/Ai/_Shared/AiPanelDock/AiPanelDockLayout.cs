using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Places the parts of an <see cref="AiPanelDock"/> (found by name among its children): the page, the reopen pill, the scrim
/// and the panel host. Docked, the page column is the width left of the dock (<see cref="DockWidth"/>, which the dock
/// animates) and the host keeps <see cref="PanelWidth"/> with its left edge on the dock's edge, so it slides out past the
/// right edge (the dock clips); the pill sits 14px from the page column's top right. As a sheet the page takes the whole
/// width, the scrim covers it and the host is the bottom <see cref="SheetHeightRatio"/> of the height (the dock moves it
/// down with a render transform).
/// </summary>
internal sealed class AiPanelDockLayout : Panel
{
    /// <summary>The pill's distance from the page column's top and right edges.</summary>
    public const double ReopenInset = 14;

    private double _dockWidth;
    private double _panelWidth = 360;
    private double _sheetHeightRatio = 0.74;
    private bool _isSheet;

    /// <summary>The docked width of the panel area (0 closed, <see cref="PanelWidth"/> open).</summary>
    public double DockWidth
    {
        get => _dockWidth;
        set
        {
            if (Math.Abs(_dockWidth - value) > 0.001)
            {
                _dockWidth = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The width of the panel itself.</summary>
    public double PanelWidth
    {
        get => _panelWidth;
        set
        {
            if (Math.Abs(_panelWidth - value) > 0.001)
            {
                _panelWidth = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The share of the height the sheet takes.</summary>
    public double SheetHeightRatio
    {
        get => _sheetHeightRatio;
        set
        {
            if (Math.Abs(_sheetHeightRatio - value) > 0.0001)
            {
                _sheetHeightRatio = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>Whether the panel is a bottom sheet.</summary>
    public bool IsSheet
    {
        get => _isSheet;
        set
        {
            if (_isSheet != value)
            {
                _isSheet = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The sheet's height for a layout <paramref name="height"/> tall.</summary>
    public double SheetHeight(double height) => Math.Max(0, height * Math.Clamp(SheetHeightRatio, 0, 1));

    private Control? Part(string name)
    {
        foreach (var child in Children)
        {
            if (child.Name == name)
            {
                return child;
            }
        }
        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var page = Part(AiPanelDock.PagePart);
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var dock = IsSheet ? 0 : Math.Clamp(DockWidth, 0, PanelWidth);
        page?.Measure(new Size(Math.Max(0, width - dock), availableSize.Height));
        Part(AiPanelDock.ReopenPart)?.Measure(Size.Infinity);
        Part(AiPanelDock.ScrimPart)?.Measure(availableSize);
        var host = Part(AiPanelDock.PanelHostPart);
        host?.Measure(IsSheet ? new Size(width, SheetHeight(height)) : new Size(PanelWidth, availableSize.Height));
        if (!double.IsFinite(availableSize.Width))
        {
            width = (page?.DesiredSize.Width ?? 0) + dock;
        }
        if (!double.IsFinite(availableSize.Height))
        {
            height = Math.Max(page?.DesiredSize.Height ?? 0, host?.DesiredSize.Height ?? 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (width, height) = (finalSize.Width, finalSize.Height);
        var dock = IsSheet ? 0 : Math.Clamp(DockWidth, 0, PanelWidth);
        var column = Math.Max(0, width - dock);
        Part(AiPanelDock.PagePart)?.Arrange(new Rect(0, 0, column, height));
        if (Part(AiPanelDock.ReopenPart) is { } reopen)
        {
            var size = reopen.DesiredSize;
            reopen.Arrange(new Rect(column - ReopenInset - size.Width, ReopenInset, size.Width, size.Height));
        }
        Part(AiPanelDock.ScrimPart)?.Arrange(new Rect(0, 0, width, height));
        if (Part(AiPanelDock.PanelHostPart) is { } host)
        {
            var sheet = SheetHeight(height);
            host.Arrange(IsSheet ? new Rect(0, height - sheet, width, sheet) : new Rect(column, 0, PanelWidth, height));
        }
        return finalSize;
    }
}
