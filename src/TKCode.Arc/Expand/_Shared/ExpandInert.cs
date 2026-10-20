using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// Avalonia has no <c>inert</c>: a collapsed region of the Expand controls stops taking the pointer, drops out of
/// the Tab order and leaves the accessibility tree, while staying mounted so a reversal can animate from where it is.
/// </summary>
internal static class ExpandInert
{
    public static void Apply(Control? region, bool inert)
    {
        if (region is null)
        {
            return;
        }
        region.IsHitTestVisible = !inert;
        KeyboardNavigation.SetTabNavigation(region, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        AutomationProperties.SetAccessibilityView(region, inert ? AccessibilityView.Raw : AccessibilityView.Default);
    }
}
