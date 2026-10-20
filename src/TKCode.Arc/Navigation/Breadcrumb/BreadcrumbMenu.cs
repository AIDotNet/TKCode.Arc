using Avalonia.Controls;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Surface of the breadcrumb overflow menu (Arc dropdown menu look).</summary>
internal sealed class BreadcrumbMenu : ContentControl
{
    protected override Type StyleKeyOverride => typeof(BreadcrumbMenu);
}

/// <summary>One collapsed crumb in the overflow menu.</summary>
internal sealed class BreadcrumbMenuItem : AvaloniaButton
{
    public int Index { get; init; }

    protected override Type StyleKeyOverride => typeof(BreadcrumbMenuItem);
}
