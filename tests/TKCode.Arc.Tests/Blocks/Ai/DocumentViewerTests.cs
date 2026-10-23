using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The document viewer: toolbar, fit and zoom steps, the zoom spring around the center, spans, scrolling, menu, scan.</summary>
public class DocumentViewerTests
{
    #region Rig

    /// <summary>Sample words (not the defaults), so every name is shown to come from the labels.</summary>
    private static readonly DocumentExtractionLabels s_labels = new()
    {
        Document = "Doc pane",
        CanvasName = (name, zoom) => $"{name} at {zoom}",
        SwitchDocument = name => $"{name} (switch)",
        PageOf = (page, count) => $"{page}-{count}",
        PageOfName = (page, count) => $"page {page}/{count}",
        Zoom = "Zoom group",
        ZoomOut = "Smaller",
        ZoomIn = "Bigger",
        ZoomReset = percent => $"Reset from {percent}",
        Fit = "Fit it",
        FitTip = "Fit it to the width"
    };

    private static readonly DocumentSpanPaint s_linked = new(DocumentSpanState.Linked, 1, false);

    private static (Window Window, DocumentViewer Viewer) Show(
        Action<DocumentViewer>? configure = null, double width = 641, double height = 600, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var viewer = new DocumentViewer { Document = ExtractionFixtures.Invoice(), Labels = s_labels, Width = width, Height = height };
        configure?.Invoke(viewer);
        var host = new Border
        {
            Child = viewer,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var window = TestHelpers.Show(host, level);
        window.Width = 900;
        window.Height = 900;
        Settle(window);
        return (window, viewer);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static void Frames(ManualFrameClock clock, Window window, int milliseconds)
    {
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.RunJobs();
        _ = window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Settle(window);
    }

    private static T Part<T>(DocumentViewer viewer, string name) where T : Control => TestHelpers.Descendant<T>(viewer, name);

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    private static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));

    /// <summary>The page point (page units) under the canvas center.</summary>
    private static Point CenterOnPage(DocumentViewer viewer)
    {
        var canvas = viewer.Canvas!;
        var viewport = canvas.Viewport;
        var toPage = canvas.TransformToVisual(viewer.Page!)!.Value;
        return new Point(viewport.Width / 2, viewport.Height / 2).Transform(toPage);
    }

    private static Rect PageUnion(DocumentViewer viewer, string id)
    {
        viewer.Page!.TryGetSpanRects(id, out var rects).ShouldBeTrue();
        var union = rects[0];
        foreach (var rect in rects)
        {
            union = union.Union(rect);
        }
        return union;
    }

    #endregion

    #region Fit and steps

    [AvaloniaFact]
    public void Fit_Uses_The_Canvas_Width_Less_The_Padding_And_Three_Pixels()
    {
        var (window, viewer) = Show();
        try
        {
            // Spec 3.3: 641 → 110%, 553 → 94%, 445 → 74%; stacked (padding 14) 720 → 128%, 358 → 61%.
            DocumentViewer.PercentOf(viewer.FitZoom(641)).ShouldBe(110);
            DocumentViewer.PercentOf(viewer.FitZoom(553)).ShouldBe(94);
            DocumentViewer.PercentOf(viewer.FitZoom(445)).ShouldBe(74);
            viewer.FitZoom(641).ShouldBe((641 - 44 - 3) / 540.0, 1e-9);
            viewer.IsStacked = true;
            Settle(window);
            DocumentViewer.PercentOf(viewer.FitZoom(720)).ShouldBe(128);
            DocumentViewer.PercentOf(viewer.FitZoom(358)).ShouldBe(61);
            // Within the steps' range.
            viewer.FitZoom(100).ShouldBe(0.4);
            viewer.FitZoom(5000).ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Fitted_Page_Follows_The_Width_At_Once_And_Draws_At_The_Zoom()
    {
        var (window, viewer) = Show(level: ArcMotionLevel.Full);
        try
        {
            viewer.IsFitWidth.ShouldBeTrue();
            viewer.Zoom.ShouldBe(594 / 540.0, 1e-9);
            viewer.DrawnZoom.ShouldBe(viewer.Zoom, 1e-9);
            // The sizer reports the scaled page (rounded to whole pixels by the layout).
            viewer.Sizer!.Bounds.Width.ShouldBe(594, 1);
            viewer.Sizer.Bounds.Height.ShouldBe(764 * viewer.Zoom, 1);
            Part<TextBlock>(viewer, DocumentViewer.PercentPart).Text.ShouldBe("110%");
            Part<DocumentViewerButton>(viewer, DocumentViewer.FitPart).IsOn.ShouldBeTrue();

            // A resize follows without a spring, even at full motion.
            viewer.Width = 445;
            Settle(window);
            DocumentViewer.PercentOf(viewer.Zoom).ShouldBe(74);
            viewer.DrawnZoom.ShouldBe(viewer.Zoom, 1e-9);
            viewer.IsZooming.ShouldBeFalse();

            // Stacking changes the padding and the fit with it.
            viewer.Width = 720;
            viewer.IsStacked = true;
            Settle(window);
            DocumentViewer.PercentOf(viewer.Zoom).ShouldBe(128);
            viewer.Sizer.Margin.Left.ShouldBe(14);
            viewer.IsStacked = false;
            Settle(window);
            viewer.Sizer.Margin.Left.ShouldBe(22);
            DocumentViewer.PercentOf(viewer.Zoom).ShouldBe(125);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Buttons_Step_Through_The_Steps_And_Turn_Fit_Off()
    {
        var (window, viewer) = Show();
        try
        {
            var zoomIn = Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomInPart);
            var zoomOut = Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomOutPart);
            var reset = Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomResetPart);
            var fit = Part<DocumentViewerButton>(viewer, DocumentViewer.FitPart);
            var percent = Part<TextBlock>(viewer, DocumentViewer.PercentPart);

            // Fitted at 110.2%: the step in is 125%, the step out is 100% (110% is the same percentage).
            Click(zoomIn);
            Settle(window);
            viewer.Zoom.ShouldBe(1.25);
            viewer.IsFitWidth.ShouldBeFalse();
            fit.IsOn.ShouldBeFalse();
            percent.Text.ShouldBe("125%");
            viewer.IsFitWidth = true;
            Settle(window);
            Click(zoomOut);
            Settle(window);
            viewer.Zoom.ShouldBe(1.0);

            // The ends disable their button.
            viewer.Zoom = 0.4;
            Settle(window);
            zoomOut.IsEnabled.ShouldBeFalse();
            zoomIn.IsEnabled.ShouldBeTrue();
            viewer.StepZoom(-1);
            viewer.Zoom.ShouldBe(0.4);
            viewer.Zoom = 2;
            Settle(window);
            zoomIn.IsEnabled.ShouldBeFalse();
            zoomOut.IsEnabled.ShouldBeTrue();

            // The zoom stays within the steps' range.
            viewer.Zoom = 5;
            viewer.Zoom.ShouldBe(2);
            viewer.Zoom = 0.1;
            viewer.Zoom.ShouldBe(0.4);

            // The percentage resets to 100%; Fit toggles and fits again.
            Click(reset);
            Settle(window);
            viewer.Zoom.ShouldBe(1);
            viewer.IsFitWidth.ShouldBeFalse();
            Click(fit);
            Settle(window);
            viewer.IsFitWidth.ShouldBeTrue();
            fit.IsOn.ShouldBeTrue();
            viewer.Zoom.ShouldBe(594 / 540.0, 1e-9);
            Click(fit);
            Settle(window);
            viewer.IsFitWidth.ShouldBeFalse();
            viewer.Zoom.ShouldBe(594 / 540.0, 1e-9);

            // A zoom set by the owner while fitted leaves fit.
            viewer.IsFitWidth = true;
            Settle(window);
            viewer.Zoom = 1.5;
            viewer.IsFitWidth.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Or_Cmd_With_Plus_Minus_And_Zero_Zoom_The_Focused_Canvas_And_Arrows_Scroll_It()
    {
        var (window, viewer) = Show();
        try
        {
            viewer.Canvas!.Focus(NavigationMethod.Tab);
            Settle(window);
            Press(window, PhysicalKey.Equal, RawInputModifiers.Control);
            viewer.Zoom.ShouldBe(1.25);
            viewer.IsFitWidth.ShouldBeFalse();
            Press(window, PhysicalKey.Equal, RawInputModifiers.Meta);
            viewer.Zoom.ShouldBe(1.5);
            Press(window, PhysicalKey.Minus, RawInputModifiers.Control);
            viewer.Zoom.ShouldBe(1.25);
            Press(window, PhysicalKey.NumPadAdd, RawInputModifiers.Control);
            viewer.Zoom.ShouldBe(1.5);
            Press(window, PhysicalKey.Digit0, RawInputModifiers.Control);
            viewer.IsFitWidth.ShouldBeTrue();
            viewer.Zoom.ShouldBe(594 / 540.0, 1e-9);

            // Without the modifier the keys do nothing to the zoom; the arrows scroll.
            Press(window, PhysicalKey.Equal);
            viewer.Zoom.ShouldBe(594 / 540.0, 1e-9);
            Press(window, PhysicalKey.ArrowDown);
            viewer.Canvas.Offset.Y.ShouldBeGreaterThan(0);

            // The keys belong to the canvas: on the file button they do nothing.
            var before = viewer.Zoom;
            Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomOutPart).Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Equal, RawInputModifiers.Control);
            viewer.Zoom.ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Zoom spring

    [AvaloniaFact]
    public void Zoom_Springs_Around_The_Canvas_Center_While_The_Percentage_Changes_At_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, viewer) = Show(v =>
        {
            v.IsFitWidth = false;
            v.Zoom = 1.1;
        }, level: ArcMotionLevel.Full);
        try
        {
            var canvas = viewer.Canvas!;
            canvas.Offset = new Vector(0, 300);
            Settle(window);
            var center = CenterOnPage(viewer);
            viewer.Page!.TryGetSpanRects("vendor", out var vendorBefore).ShouldBeTrue();

            viewer.StepZoom(1);
            Part<TextBlock>(viewer, DocumentViewer.PercentPart).Text.ShouldBe("125%");
            viewer.DrawnZoom.ShouldBe(1.1, 1e-9);
            viewer.IsZooming.ShouldBeTrue();

            Frames(clock, window, 100);
            viewer.DrawnZoom.ShouldBeInRange(1.11, 1.245);
            var during = CenterOnPage(viewer);
            during.X.ShouldBe(center.X, 0.5);
            during.Y.ShouldBe(center.Y, 0.5);

            Frames(clock, window, 1200);
            viewer.IsZooming.ShouldBeFalse();
            viewer.DrawnZoom.ShouldBe(1.25, 1e-9);
            var after = CenterOnPage(viewer);
            after.X.ShouldBe(center.X, 0.5);
            after.Y.ShouldBe(center.Y, 0.5);
            // The page grew past the canvas width, so the canvas scrolled sideways to keep the center.
            canvas.Offset.X.ShouldBeGreaterThan(1);

            // The page was scaled, not laid out again: its span rectangles are the same cached list.
            viewer.Page.TryGetSpanRects("vendor", out var vendorAfter).ShouldBeTrue();
            vendorAfter.ShouldBeSameAs(vendorBefore);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Zoom_In_One_Step()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, viewer) = Show(v => v.IsFitWidth = false, level: ArcMotionLevel.Reduced);
        try
        {
            viewer.StepZoom(1);
            viewer.DrawnZoom.ShouldBe(viewer.Zoom, 1e-9);
            viewer.IsZooming.ShouldBeFalse();
            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            viewer.StepZoom(1);
            viewer.DrawnZoom.ShouldBe(viewer.Zoom, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Spans and scrolling

    [AvaloniaFact]
    public void Span_Bounds_Follow_The_Zoom_And_The_Scroll()
    {
        var (window, viewer) = Show(v =>
        {
            v.IsFitWidth = false;
            v.Zoom = 1.25;
        });
        try
        {
            var canvas = viewer.Canvas!;
            canvas.Offset = new Vector(20, 150);
            Settle(window);
            var union = PageUnion(viewer, "total");
            viewer.TryGetSpanBounds("total", canvas, out var bounds, out _).ShouldBeTrue();
            // The content is 675 + 2 × 22 wide (wider than the 641 canvas), so the page starts 22 in, less the scroll.
            bounds.X.ShouldBe(22 - 20 + union.X * 1.25, 0.01);
            bounds.Y.ShouldBe(22 - 150 + union.Y * 1.25, 0.01);
            bounds.Width.ShouldBe(union.Width * 1.25, 0.01);
            bounds.Height.ShouldBe(union.Height * 1.25, 0.01);

            // Any visual: the window sees the same rectangle moved by the canvas' place.
            viewer.TryGetSpanBounds("total", window, out var inWindow, out _).ShouldBeTrue();
            var canvasOrigin = canvas.TranslatePoint(default, window)!.Value;
            inWindow.X.ShouldBe(bounds.X + canvasOrigin.X, 0.01);
            inWindow.Y.ShouldBe(bounds.Y + canvasOrigin.Y, 0.01);

            // Visibility is measured against the canvas viewport.
            canvas.Offset = default;
            Settle(window);
            viewer.TryGetSpanBounds("vendor", canvas, out _, out var vendorVisible).ShouldBeTrue();
            vendorVisible.ShouldBeTrue();
            viewer.TryGetSpanBounds("iban", canvas, out var iban, out var ibanVisible).ShouldBeTrue();
            iban.Top.ShouldBeGreaterThan(canvas.Viewport.Height);
            ibanVisible.ShouldBeFalse();

            viewer.TryGetSpanBounds("nope", canvas, out _, out _).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrollToSpan_Leaves_Visible_Spans_Alone_And_Brings_Others_56_From_The_Nearest_Edge()
    {
        var (window, viewer) = Show();
        try
        {
            var canvas = viewer.Canvas!;
            var height = canvas.Viewport.Height;
            viewer.ScrollToSpan("vendor");
            canvas.Offset.ShouldBe(default);

            // Below: its bottom comes to 56 above the lower edge.
            viewer.ScrollToSpan("iban");
            Settle(window);
            canvas.Offset.Y.ShouldBeGreaterThan(0);
            viewer.TryGetSpanBounds("iban", canvas, out var iban, out var visible).ShouldBeTrue();
            visible.ShouldBeTrue();
            iban.Bottom.ShouldBe(height - DocumentViewer.ScrollMargin, 0.5);

            // Above: its top comes to 56 below the upper edge.
            viewer.ScrollToSpan("vendor");
            Settle(window);
            viewer.TryGetSpanBounds("vendor", canvas, out var vendor, out _).ShouldBeTrue();
            vendor.Top.ShouldBe(DocumentViewer.ScrollMargin, 0.5);

            // Nearest per axis: a fully visible span does not move the canvas.
            var offset = canvas.Offset;
            viewer.ScrollToSpan("vendor");
            canvas.Offset.ShouldBe(offset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Nearest_Scroll_Uses_The_Margin_And_Shows_The_Start_Of_Large_Ranges()
    {
        DocumentViewer.NearestDelta(10, 40, 500).ShouldBe(0);
        DocumentViewer.NearestDelta(-30, -10, 500).ShouldBe(-86);
        DocumentViewer.NearestDelta(520, 540, 500).ShouldBe(96);
        // Barely fits: the margin shrinks to center it.
        DocumentViewer.NearestDelta(480, 880, 500).ShouldBe(430);
        // Larger than the viewport: its start is shown.
        DocumentViewer.NearestDelta(600, 1200, 500).ShouldBe(600);
    }

    [AvaloniaFact]
    public void ScrollToSpan_Eases_At_Full_Motion_And_Gives_Way_To_The_Wheel()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, viewer) = Show(level: ArcMotionLevel.Full);
        try
        {
            var canvas = viewer.Canvas!;
            viewer.ScrollToSpan("iban");
            viewer.IsScrollingToSpan.ShouldBeTrue();
            canvas.Offset.Y.ShouldBe(0);
            Frames(clock, window, 300);
            viewer.IsScrollingToSpan.ShouldBeFalse();
            var target = canvas.Offset.Y;
            target.ShouldBeGreaterThan(0);

            canvas.Offset = default;
            Settle(window);
            viewer.ScrollToSpan("iban");
            Frames(clock, window, 60);
            var part = canvas.Offset.Y;
            part.ShouldBeInRange(1, target - 1);
            // A wheel over the canvas stops the eased scroll where it is.
            window.MouseWheel(canvas.TranslatePoint(new Point(100, 100), window)!.Value, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            viewer.IsScrollingToSpan.ShouldBeFalse();
            var afterWheel = canvas.Offset.Y;
            Frames(clock, window, 300);
            canvas.Offset.Y.ShouldBe(afterWheel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Span_Events_Bubble_Through_The_Viewer_Once_And_The_Hover_Is_Mirrored()
    {
        var (window, viewer) = Show();
        try
        {
            var log = new List<string>();
            viewer.SpanPointerEntered += (_, e) => log.Add("viewer enter " + e.SpanId);
            viewer.SpanPressed += (_, e) => log.Add("viewer press " + e.SpanId);
            ((Control)viewer.Parent!).AddHandler(DocumentViewer.SpanPressedEvent, (_, e) => log.Add("host press " + e.SpanId));
            viewer.SetSpan("vendor", s_linked);
            viewer.Page!.GetSpanPaint("vendor").ShouldBe(s_linked);
            viewer.TryGetSpanBounds("vendor", window, out var bounds, out _).ShouldBeTrue();
            window.MouseMove(bounds.Center, RawInputModifiers.None);
            Settle(window);
            viewer.HoveredSpanId.ShouldBe("vendor");
            window.MouseDown(bounds.Center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(bounds.Center, MouseButton.Left, RawInputModifiers.None);
            Settle(window);
            log.ShouldBe(new[] { "viewer enter vendor", "viewer press vendor", "host press vendor" });

            viewer.ClearSpans();
            viewer.Page.GetSpanPaint("vendor").ShouldBeNull();
            viewer.HoveredSpanId.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ViewChanged_Reports_Scrolls_Zooms_And_Resizes()
    {
        var (window, viewer) = Show();
        try
        {
            var count = 0;
            viewer.ViewChanged += (_, _) => count++;
            viewer.Canvas!.Offset = new Vector(0, 40);
            count.ShouldBeGreaterThan(0);
            count = 0;
            viewer.StepZoom(1);
            Settle(window);
            count.ShouldBeGreaterThan(0);
            count = 0;
            viewer.Height = 500;
            Settle(window);
            count.ShouldBeGreaterThan(0);
            viewer.CanvasViewport.ShouldBeSameAs(viewer.Canvas);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Toolbar, menu and names

    [AvaloniaFact]
    public void Names_And_Texts_Come_From_The_Labels()
    {
        var (window, viewer) = Show();
        try
        {
            Avalonia.Automation.AutomationProperties.GetName(viewer).ShouldBe("Doc pane");
            viewer.Canvas!.Label.ShouldBe("oakridge-invoice-1187.pdf at 110");
            Part<TextBlock>(viewer, DocumentViewer.FileNamePart).Text.ShouldBe("oakridge-invoice-1187.pdf");
            var pageCount = Part<TextBlock>(viewer, DocumentViewer.PageCountPart);
            pageCount.Text.ShouldBe("1-1");
            Avalonia.Automation.AutomationProperties.GetName(pageCount).ShouldBe("page 1/1");
            Avalonia.Automation.AutomationProperties.GetName(Part<Control>(viewer, DocumentViewer.ZoomGroupPart)).ShouldBe("Zoom group");
            var zoomOut = Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomOutPart);
            Avalonia.Automation.AutomationProperties.GetName(zoomOut).ShouldBe("Smaller");
            Tooltip.GetTip(zoomOut).ShouldBe("Smaller");
            Tooltip.GetTip(Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomInPart)).ShouldBe("Bigger");
            Avalonia.Automation.AutomationProperties.GetName(Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomResetPart)).ShouldBe("Reset from 110");
            var fit = Part<DocumentViewerButton>(viewer, DocumentViewer.FitPart);
            fit.Content.ShouldBe("Fit it");
            Tooltip.GetTip(fit).ShouldBe("Fit it to the width");
            var peer = (IToggleProvider)ControlAutomationPeer.CreatePeerForElement(fit).GetProvider<IToggleProvider>()!;
            peer.ToggleState.ShouldBe(ToggleState.On);
            viewer.IsFitWidth = false;
            peer.ToggleState.ShouldBe(ToggleState.Off);

            viewer.Zoom = 1.5;
            Settle(window);
            viewer.Canvas.Label.ShouldBe("oakridge-invoice-1187.pdf at 150");
            viewer.Page!.ArticleName.ShouldBe("oakridge-invoice-1187.pdf");
            // One document: the file button is only a label.
            viewer.FileButton!.Focusable.ShouldBeFalse();
            Part<ArcGlyph>(viewer, "PART_Chevron").IsVisible.ShouldBeFalse();
            Avalonia.Automation.AutomationProperties.GetName(viewer.FileButton).ShouldBe("oakridge-invoice-1187.pdf");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Email_And_A_Narrow_Viewer_Hide_The_Page_Count()
    {
        var (window, viewer) = Show();
        try
        {
            var pageCount = Part<TextBlock>(viewer, DocumentViewer.PageCountPart);
            var mark = Part<DocumentFileMark>(viewer, DocumentViewer.FileMarkPart);
            pageCount.IsVisible.ShouldBeTrue();
            mark.Kind.ShouldBe(DocumentKind.Pdf);
            mark.DesiredSize.ShouldBe(new Size(15, 19));

            viewer.Document = ExtractionFixtures.Email();
            Settle(window);
            pageCount.IsVisible.ShouldBeFalse();
            mark.Kind.ShouldBe(DocumentKind.Email);
            mark.DesiredSize.ShouldBe(new Size(16, 16));
            viewer.Page!.IsPdf.ShouldBeFalse();

            viewer.Document = ExtractionFixtures.Invoice();
            viewer.IsNarrow = true;
            Settle(window);
            pageCount.IsVisible.ShouldBeFalse();
            viewer.IsNarrow = false;
            Settle(window);
            pageCount.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Toolbar_Is_Tabbed_In_Order_And_Ends_On_The_Canvas()
    {
        var invoice = ExtractionFixtures.Invoice();
        var (window, viewer) = Show(v =>
        {
            v.Document = invoice;
            v.Documents = [invoice, ExtractionFixtures.Email()];
        });
        try
        {
            viewer.FileButton!.Focus(NavigationMethod.Tab);
            var order = new List<object?> { Focused(window) };
            for (var i = 0; i < 5; i++)
            {
                Press(window, PhysicalKey.Tab);
                order.Add(Focused(window));
            }
            order.ShouldBe(new object?[]
            {
                viewer.FileButton,
                Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomOutPart),
                Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomResetPart),
                Part<DocumentViewerButton>(viewer, DocumentViewer.ZoomInPart),
                Part<DocumentViewerButton>(viewer, DocumentViewer.FitPart),
                viewer.Canvas
            });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Document_Menu_Opens_On_The_Chosen_Document_With_A_Visible_Highlight_And_Returns_Focus()
    {
        var invoice = ExtractionFixtures.Invoice();
        var email = ExtractionFixtures.Email();
        var (window, viewer) = Show(v =>
        {
            v.Document = invoice;
            v.Documents = [invoice, email];
        });
        try
        {
            var button = viewer.FileButton!;
            button.HasMenu.ShouldBeTrue();
            button.Focusable.ShouldBeTrue();
            Part<ArcGlyph>(viewer, "PART_Chevron").IsVisible.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(button).ShouldBe("oakridge-invoice-1187.pdf (switch)");

            button.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            var menu = viewer.Menu!;
            menu.IsOpen.ShouldBeTrue();
            button.IsOn.ShouldBeTrue();
            var entries = menu.Items.OfType<MenuEntry>().ToList();
            entries.Count.ShouldBe(2);
            entries.Select(e => e.IsChecked).ShouldBe(new[] { true, false });
            entries[0].ToggleType.ShouldBe(MenuEntryToggleType.Radio);
            entries[1].Icon.ShouldBeOfType<DocumentFileMark>().Kind.ShouldBe(DocumentKind.Email);
            // The keyboard highlight starts on the chosen document and is visible: Foreground at 5%, the rows' radius.
            menu.HighlightedEntry.ShouldBe(entries[0]);
            var highlight = menu.Highlight!;
            highlight.CornerRadius.ShouldBe(new CornerRadius(DocumentViewer.MenuRowRadius));
            var foreground = ArcTokens.GetColor(viewer, ArcDesignTokenKind.Foreground);
            TestHelpers.ColorOf(highlight.Fill).ShouldBe(Oklch.WithAlpha(foreground, DocumentViewer.ForegroundTint));
            highlight.CurrentOpacity.ShouldBeGreaterThan(0);

            Press(window, PhysicalKey.ArrowDown);
            menu.HighlightedEntry.ShouldBe(entries[1]);
            Press(window, PhysicalKey.Enter);
            viewer.SelectedDocument.ShouldBeSameAs(email);
            menu.IsOpen.ShouldBeFalse();
            button.IsOn.ShouldBeFalse();
            Focused(window).ShouldBe(button);
            entries.Select(e => e.IsChecked).ShouldBe(new[] { false, true });

            // Two-way: the owner's choice moves the check; Escape closes and gives focus back.
            viewer.SelectedDocument = invoice;
            entries.Select(e => e.IsChecked).ShouldBe(new[] { true, false });
            Press(window, PhysicalKey.Enter);
            menu.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            menu.IsOpen.ShouldBeFalse();
            Focused(window).ShouldBe(button);

            // A click opens it without a keyboard highlight.
            window.MouseDown(button.TranslatePoint(new Point(20, 16), window)!.Value, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(button.TranslatePoint(new Point(20, 16), window)!.Value, MouseButton.Left, RawInputModifiers.None);
            Settle(window);
            menu.IsOpen.ShouldBeTrue();
            menu.HighlightedEntry.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Theme

    [AvaloniaFact]
    public async Task The_Canvas_Is_A_Little_Deeper_Than_The_Surface_In_Both_Themes()
    {
        var (window, viewer) = Show();
        try
        {
            var surface = ArcTokens.GetColor(viewer, ArcDesignTokenKind.Surface);
            var foreground = ArcTokens.GetColor(viewer, ArcDesignTokenKind.Foreground);
            TestHelpers.ColorOf(viewer.CanvasBrush).ShouldBe(Oklch.MixOklab(surface, foreground, 0.05));
            Oklch.FromColor(TestHelpers.ColorOf(viewer.CanvasBrush)).L.ShouldBe(0.9575, 0.01);
            TestHelpers.ColorOf(Part<Border>(viewer, "PART_CanvasHost").Background).ShouldBe(TestHelpers.ColorOf(viewer.CanvasBrush));
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            try
            {
                Settle(window);
                TestHelpers.ColorOf(viewer.CanvasBrush).ShouldBe(ArcTokens.GetColor(viewer, ArcDesignTokenKind.Background));
            }
            finally
            {
                await TestHelpers.ApplyAsync(new ThemeConfigBuilder().Build());
            }
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Scan

    [AvaloniaFact]
    public void The_Scan_Sweeps_Once_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, viewer) = Show(level: ArcMotionLevel.Full);
        try
        {
            viewer.StartScan();
            var scan = viewer.Scan.ShouldNotBeNull();
            scan.GetVisualParent().ShouldBe(Part<Panel>(viewer, DocumentViewer.StagePart));
            Settle(window);
            scan.Bounds.Size.ShouldBe(viewer.Page!.Bounds.Size);
            Frames(clock, window, 750);
            scan.Progress.ShouldBeInRange(0.45, 0.55);
            scan.IsSweeping.ShouldBeTrue();
            Frames(clock, window, 800);
            Dispatcher.UIThread.RunJobs();
            viewer.Scan.ShouldBeNull();
            viewer.GetVisualDescendants().OfType<ScanBeam>().ShouldBeEmpty();
            Frames(clock, window, 2000);
            viewer.GetVisualDescendants().OfType<ScanBeam>().ShouldBeEmpty();

            // A stop removes it at once.
            viewer.StartScan();
            viewer.Scan.ShouldNotBeNull();
            viewer.StopScan();
            viewer.GetVisualDescendants().OfType<ScanBeam>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void There_Is_No_Scan_Under_Reduced_Motion_Or_Off()
    {
        var (window, viewer) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            viewer.StartScan();
            viewer.Scan.ShouldBeNull();
            viewer.GetVisualDescendants().OfType<ScanBeam>().ShouldBeEmpty();
            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            viewer.StartScan();
            viewer.GetVisualDescendants().OfType<ScanBeam>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Scan_Keyframes_Follow_The_Spec()
    {
        ScanBeam.Duration.ShouldBe(TimeSpan.FromMilliseconds(1500));
        ScanBeam.PositionAt(0).ShouldBe(0);
        ScanBeam.PositionAt(0.5).ShouldBe(0.5, 1e-3);
        ScanBeam.PositionAt(1).ShouldBe(1);
        ScanBeam.OpacityAt(0).ShouldBe(0);
        ScanBeam.OpacityAt(0.06).ShouldBe(0.5, 1e-3);
        ScanBeam.OpacityAt(0.12).ShouldBe(1);
        ScanBeam.OpacityAt(0.5).ShouldBe(1);
        ScanBeam.OpacityAt(0.78).ShouldBe(1);
        ScanBeam.OpacityAt(0.89).ShouldBe(0.5, 1e-3);
        ScanBeam.OpacityAt(1).ShouldBe(0);
    }

    #endregion
}
