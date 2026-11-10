using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

/// <summary>
/// Floating surfaces draw <c>ShadowFloating</c> (offset downwards) past their bounds. Anything that clips on the way cuts
/// the shadow to a hard rectangle, which shows as dark square corners below the rounded ones (issue #75).
/// </summary>
public class FloatingShadowClipTests
{
    public static TheoryData<string> Surfaces => new() { nameof(DialogSurface), nameof(DrawerSurface), nameof(BottomSheetSurface) };

    private static Control CreateSurface(string name) => name switch
    {
        nameof(DialogSurface) => new DialogSurface { Title = "Title", Content = "Body" },
        nameof(DrawerSurface) => new DrawerSurface { Title = "Title", Content = "Body" },
        _ => new BottomSheetSurface { Title = "Title", Content = "Body" }
    };

    [AvaloniaTheory]
    [MemberData(nameof(Surfaces))]
    public void Modal_Surface_Does_Not_Clip_Its_Own_Shadow(string name)
    {
        var surface = CreateSurface(name);
        var window = TestHelpers.Show(surface);
        try
        {
            var shadow = surface.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Shadow");
            shadow.BoxShadow.Count.ShouldBeGreaterThan(0);
            surface.ClipToBounds.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Popup_Shadow_Is_Not_Clipped_By_A_Clipping_Overlay_Host_Template()
    {
        // FluentTheme and SimpleTheme replace Arc's base OverlayPopupHost theme; their template is a LayoutTransformControl,
        // which clips by default. This theme stands in for theirs.
        var clippingHost = new ControlTheme(typeof(OverlayPopupHost))
        {
            Setters =
            {
                new Setter(Visual.ClipToBoundsProperty, true),
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<OverlayPopupHost>((_, scope) =>
                    new LayoutTransformControl
                    {
                        Child = new ContentPresenter
                        {
                            Name = "PART_ContentPresenter",
                            [!ContentPresenter.ContentProperty] = new TemplateBinding(ContentControl.ContentProperty)
                        }.RegisterInNameScope(scope)
                    }))
            }
        };
        var probe = new Border { Width = 120, Height = 40 };
        var popover = new Popover { Trigger = new Button { Content = "Open" }, Content = probe };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { popover } });
        window.Resources[typeof(OverlayPopupHost)] = clippingHost;
        try
        {
            popover.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var frame = probe.FindAncestorOfType<ArcPopupFrame>();
            frame.ShouldNotBeNull();
            frame.BoxShadow.Count.ShouldBeGreaterThan(0);
            var host = frame.FindAncestorOfType<OverlayPopupHost>();
            host.ShouldNotBeNull();
            host.GetVisualDescendants().OfType<LayoutTransformControl>().ShouldNotBeEmpty();
            foreach (var visual in frame.GetVisualAncestors())
            {
                visual.ClipToBounds.ShouldBeFalse($"{visual.GetType().Name} clips the popup shadow");
                if (ReferenceEquals(visual, host))
                {
                    break;
                }
            }
        }
        finally
        {
            window.Close();
        }
    }
}
