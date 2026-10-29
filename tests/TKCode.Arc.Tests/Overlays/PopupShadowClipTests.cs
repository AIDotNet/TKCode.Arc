using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class PopupShadowClipTests
{
    [AvaloniaFact]
    public void Nothing_Between_The_Surface_And_The_Overlay_Layer_Clips_The_Shadow()
    {
        // The overlay host is exactly as large as the surface and the floating shadow is painted past it; a clipping
        // host (Avalonia's default for OverlayPopupHost and LayoutTransformControl) cut the shadow to a rectangle, so
        // the surface's rounded corners looked square.
        var menu = new DropdownMenu { Label = "Actions" };
        menu.Items.Add(new MenuEntry { Header = "Rename" });
        menu.Items.Add(new MenuEntry { Header = "Duplicate" });
        menu.Items.Add(new MenuEntry { Header = "Delete", IsDestructive = true, HasSeparatorBefore = true });
        var window = TestHelpers.Show(new Border { Padding = new Thickness(40), Child = menu });
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var frame = window.GetVisualDescendants().OfType<ArcPopupFrame>().ShouldHaveSingleItem();
            frame.BoxShadow.Count.ShouldBeGreaterThan(0);
            var host = frame.FindAncestorOfType<OverlayPopupHost>().ShouldNotBeNull();
            host.Bounds.Size.ShouldBe(frame.Bounds.Size);
            var layer = host.GetVisualParent().ShouldNotBeNull();
            for (Visual? visual = frame; visual is not null && !ReferenceEquals(visual, layer); visual = visual.GetVisualParent())
            {
                visual.ClipToBounds.ShouldBeFalse(visual.GetType().Name);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
