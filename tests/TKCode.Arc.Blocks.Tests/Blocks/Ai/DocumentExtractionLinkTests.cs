using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The document extraction block: linking the form and the document, following, keyboard, layout and names.</summary>
public class DocumentExtractionLinkTests
{
    [AvaloniaFact]
    public void Pointing_At_A_Row_Lights_Its_Text_And_Draws_The_Link()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        Assert.Equal(DocumentSpanState.Linked, x.SpanPaintOf("vendor")!.Value.State);
        Assert.False(x.Links!.IsShown);

        rig.Move(rig.Row("vendor"));
        Assert.Equal("vendor", x.HoverPath);
        Assert.True(rig.Row("vendor").IsLinked);
        Assert.Equal(DocumentSpanState.Lit, x.SpanPaintOf("vendor")!.Value.State);
        Assert.Equal(1, x.SpanPaintOf("vendor")!.Value.Tone);
        Assert.True(x.Links.IsShown);

        rig.Move(x.AcceptAllButton!);
        Assert.Null(x.HoverPath);
        Assert.False(rig.Row("vendor").IsLinked);
        Assert.Equal(DocumentSpanState.Linked, x.SpanPaintOf("vendor")!.Value.State);
        Assert.False(x.Links.IsShown);
    }

    [AvaloniaFact]
    public void A_Landing_Value_Tints_Its_Text_Deeply_For_A_Moment()
    {
        using var rig = new DocumentExtractionRig(ArcMotionLevel.Full);
        var x = rig.Extraction;
        _ = rig.Start();
        rig.Step(2);
        Assert.Equal(DocumentSpanState.Fresh, x.SpanPaintOf("vendor")!.Value.State);
        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(DocumentSpanState.Fresh, x.SpanPaintOf("vendor")!.Value.State);
        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(DocumentSpanState.Linked, x.SpanPaintOf("vendor")!.Value.State);

        rig.Step(3);
        // The due date is below the threshold: its cited text ("14 days net") is underlined.
        Assert.True(x.SpanPaintOf("terms")!.Value.NeedsCheck);
        Assert.False(x.SpanPaintOf("vendor")!.Value.NeedsCheck);
        Assert.Null(x.SpanPaintOf("total"));
    }

    [AvaloniaFact]
    public void Reduced_Motion_Has_No_Fresh_Tint()
    {
        using var rig = new DocumentExtractionRig(ArcMotionLevel.Reduced);
        var x = rig.Extraction;
        _ = rig.Start();
        rig.Step(2);
        Assert.Equal(DocumentSpanState.Linked, x.SpanPaintOf("vendor")!.Value.State);
    }

    [AvaloniaFact]
    public void Selecting_A_Path_Links_Both_Ends()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var changes = new List<string?>();
        x.SelectedPathChanged += (_, e) => changes.Add(e.Path);
        x.SelectedPath = "iban";
        rig.Pump();
        Assert.Equal(new string?[] { "iban" }, changes);
        Assert.True(rig.Row("iban").IsLinked);
        Assert.Equal(DocumentSpanState.Lit, x.SpanPaintOf("iban")!.Value.State);
        Assert.Equal(4, x.SpanPaintOf("iban")!.Value.Tone);
        Assert.True(x.Links!.IsShown);

        var end = x.Links.Current!.End()!.Value;
        var paneLeft = x.FormPane!.TranslatePoint(default, x.Links)!.Value.X;
        // The end is the value box's line (the reason below it does not move it).
        var box = rig.Row("iban").Input!;
        Assert.Equal(paneLeft, end.Bounds.X, 3);
        Assert.Equal(box.TranslatePoint(default, x.Links)!.Value.Y, end.Bounds.Y, 3);
        Assert.Equal(box.Bounds.Height, end.Bounds.Height, 3);
        Assert.True(end.IsVisible);
        Assert.NotNull(x.Links.Current.Start());

        x.SelectedPath = null;
        rig.Pump();
        Assert.False(x.Links.IsShown);
        Assert.Equal(DocumentSpanState.Linked, x.SpanPaintOf("iban")!.Value.State);
    }

    [AvaloniaFact]
    public void The_Pointer_Links_Over_The_Selection_For_A_While()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        x.SelectedPath = "vendor";
        rig.Pump();
        rig.Move(rig.Row("invoiceNumber"));
        Assert.False(rig.Row("vendor").IsLinked);
        Assert.True(rig.Row("invoiceNumber").IsLinked);
        Assert.Equal(DocumentSpanState.Linked, x.SpanPaintOf("vendor")!.Value.State);
        rig.Move(x.AcceptAllButton!);
        Assert.True(rig.Row("vendor").IsLinked);
        Assert.Equal(DocumentSpanState.Lit, x.SpanPaintOf("vendor")!.Value.State);
    }

    [AvaloniaFact]
    public void A_Cell_Links_To_Its_Table_Row()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        x.SelectedPath = "lineItems.1.amount";
        rig.Pump();
        var table = x.TableOf("lineItems")!;
        Assert.Equal("lineItems.1.amount", table.LinkedPath);
        Assert.Equal(DocumentSpanState.Lit, x.SpanPaintOf("li1a")!.Value.State);
        Assert.Equal(2, x.SpanPaintOf("li1a")!.Value.Tone);
        var end = x.Links!.Current!.End()!.Value;
        var tableRow = table.FindRow(1)!;
        Assert.Equal(tableRow.TranslatePoint(default, x.Links)!.Value.Y, end.Bounds.Y, 3);
        Assert.Equal(tableRow.Bounds.Height, end.Bounds.Height, 3);
    }

    [AvaloniaFact]
    public void Clicking_Cited_Text_Selects_And_Focuses_Its_Field()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        Assert.True(x.Viewer!.TryGetSpanBounds("invoiceNumber", rig.Window, out var bounds, out var visible));
        Assert.True(visible);
        var pressed = new List<string>();
        x.Viewer.SpanPressed += (_, e) => pressed.Add(e.SpanId);
        // The headless stub font wraps the span: its first line starts at the top left of the union.
        var point = new Point(bounds.X + 4, bounds.Y + 4);
        rig.Move(point);
        Assert.Equal("invoiceNumber", x.HoverPath);
        Assert.Equal(DocumentSpanState.Lit, x.SpanPaintOf("invoiceNumber")!.Value.State);
        rig.Click(point);
        Assert.Equal(new[] { "invoiceNumber" }, pressed);
        Assert.Equal("invoiceNumber", x.SelectedPath);
        Assert.True(rig.FocusIsIn(rig.Row("invoiceNumber")));
    }

    [AvaloniaFact]
    public void Json_Lines_Link_And_The_Schema_Tab_Does_Not()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        x.SelectedTab = ExtractionTab.Json;
        rig.Pump();
        Assert.True(x.JsonScroll!.IsVisible);
        Assert.False(x.FieldsScroll!.IsVisible);
        x.SelectedPath = "total";
        rig.Pump();
        Assert.Equal("total", x.JsonView!.LinkedPath);
        Assert.True(x.Links!.IsShown);
        Assert.True(x.JsonView.TryGetLineBounds("total", x.Links, out var line));
        var end = x.Links.Current!.End()!.Value;
        Assert.Equal(line.Y, end.Bounds.Y, 3);

        x.SelectedTab = ExtractionTab.Schema;
        rig.Pump();
        Assert.False(x.Links.IsShown);
        Assert.Null(x.JsonView.LinkedPath);
    }

    [AvaloniaFact]
    public void Stacked_There_Is_No_Link_But_The_Text_Is_Lit()
    {
        using var rig = new DocumentExtractionRig(width: 700, instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        Assert.True(x.IsStackedLayout);
        x.SelectedPath = "vendor";
        rig.Pump();
        Assert.False(x.Links!.IsShown);
        Assert.False(x.Links.IsVisible);
        Assert.Equal(DocumentSpanState.Lit, x.SpanPaintOf("vendor")!.Value.State);
    }

    [AvaloniaFact]
    public void Focusing_A_Field_Selects_It_And_Escape_Clears_The_Selection()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var changes = new List<string?>();
        x.SelectedPathChanged += (_, e) => changes.Add(e.Path);
        rig.Row("subtotal").FocusInput(NavigationMethod.Tab);
        rig.Pump();
        Assert.Equal("subtotal", x.SelectedPath);
        Assert.Equal(new string?[] { "subtotal" }, changes);
        rig.Key(PhysicalKey.Escape);
        Assert.Null(x.SelectedPath);
        Assert.False(rig.Row("subtotal").IsLinked);
        Assert.True(rig.FocusIsIn(rig.Row("subtotal")));
    }

    [AvaloniaFact]
    public void Tab_Goes_Tabs_Action_Panel_Review_Then_The_Fields()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        Assert.True(x.TabStrip!.FocusSelected());
        rig.Pump();
        rig.Key(PhysicalKey.Tab);
        Assert.Same(x.ActionButton, rig.Focused);
        rig.Key(PhysicalKey.Tab);
        Assert.Same(x.FieldsScroll, rig.Focused);
        Assert.True(x.PanelRing!.IsVisible);
        rig.Key(PhysicalKey.Tab);
        Assert.True(rig.FocusIsIn(x.StatusLine!.Action));
        Assert.False(x.PanelRing.IsVisible);
        rig.Key(PhysicalKey.Tab);
        Assert.True(rig.FocusIsIn(rig.Row("vendor")));
        Assert.Equal("vendor", x.SelectedPath);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_The_Tabs()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        x.TabStrip!.FocusSelected();
        rig.Pump();
        rig.Key(PhysicalKey.ArrowRight);
        Assert.Equal(ExtractionTab.Json, x.SelectedTab);
        Assert.True(x.JsonScroll!.IsVisible);
        Assert.Same(x.TabStrip.ButtonOf(ExtractionTab.Json), rig.Focused);
        rig.Key(PhysicalKey.End);
        Assert.Equal(ExtractionTab.Schema, x.SelectedTab);
        rig.Key(PhysicalKey.ArrowRight);
        Assert.Equal(ExtractionTab.Fields, x.SelectedTab);
        // (The wide stub font pushes the last tab under the button; the second one is clear of it.)
        rig.Click(x.TabStrip.ButtonOf(ExtractionTab.Json)!);
        Assert.Equal(ExtractionTab.Json, x.SelectedTab);
        Assert.True(x.JsonScroll!.IsVisible);
        Assert.False(x.SchemaScroll!.IsVisible);
    }

    [AvaloniaFact]
    public void A_Run_Follows_Each_Value_Until_The_Person_Scrolls()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        _ = rig.Start();
        rig.Step(1);
        for (var i = 0; i < 26 && x.State is ExtractionState.Reading or ExtractionState.Extracting; i++)
        {
            rig.Step(1);
            rig.Frame(1);
        }
        rig.Pump();
        var scroll = x.FieldsScroll!;
        Assert.True(scroll.Offset.Y > 0, "the form followed the values down");
        Assert.False(x.IsFollowPaused);

        _ = rig.Start();
        rig.Step(1);
        Assert.Equal(0, scroll.Offset.Y, 3);
        rig.Step(1);
        rig.Frame(1);
        rig.Window.MouseWheel(rig.CenterOf(scroll), new Vector(0, -1), RawInputModifiers.None);
        rig.Pump();
        Assert.True(x.IsFollowPaused);
        var offset = scroll.Offset.Y;
        for (var i = 0; i < 26 && x.State is ExtractionState.Reading or ExtractionState.Extracting; i++)
        {
            rig.Step(1);
            rig.Frame(1);
        }
        Assert.Equal(offset, scroll.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void The_Status_Line_Sticks_And_Shows_Its_Edge_Once_Scrolled()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        var scroll = x.FieldsScroll!;
        Assert.False(x.StatusLine!.ShowsEdge);
        scroll.Offset = new Vector(0, 120);
        rig.Pump();
        Assert.True(x.StatusLine.ShowsEdge);
        Assert.Equal(0, x.StatusLine.TranslatePoint(default, scroll)!.Value.Y, 1);
    }

    [AvaloniaFact]
    public void The_Layout_Classes_Follow_The_Width_With_Hysteresis()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        Assert.False(x.IsStackedLayout);
        Assert.Equal(436, x.FormPane!.Bounds.Width, 1);
        Assert.Equal(600, x.FormPane.Bounds.Height, 1);
        Assert.Equal(ExtractionFooterMode.Row, x.Footer!.Mode);

        rig.Host.Width = 810;
        rig.Pump();
        // 45% of the inner 808 (363.6), on the pixel grid.
        Assert.InRange(x.FormPane.Bounds.Width, 363, 365);

        rig.Host.Width = 759;
        rig.Pump();
        Assert.True(x.IsStackedLayout);
        Assert.Contains(DocumentExtraction.StackedPseudoClass, x.Classes);
        Assert.Equal(478, x.FormPane.Bounds.Height, 1);
        Assert.Equal(379, x.Viewer!.Bounds.Height, 1);
        Assert.True(x.Viewer.IsStacked);
        rig.Host.Width = 760;
        rig.Pump();
        Assert.True(x.IsStackedLayout);
        rig.Host.Width = 761;
        rig.Pump();
        Assert.False(x.IsStackedLayout);

        rig.Host.Width = 690;
        rig.Pump();
        Assert.True(x.IsFooterWrapLayout);
        Assert.Contains(DocumentExtraction.FooterWrapPseudoClass, x.Classes);
        Assert.Equal(ExtractionFooterMode.Wrap, x.Footer.Mode);
        Assert.False(x.IsNarrowLayout);

        rig.Host.Width = 519;
        rig.Pump();
        Assert.True(x.IsNarrowLayout);
        Assert.Contains(DocumentExtraction.NarrowPseudoClass, x.Classes);
        rig.Host.Width = 520;
        rig.Pump();
        Assert.True(x.IsNarrowLayout);
        rig.Host.Width = 521;
        rig.Pump();
        Assert.False(x.IsNarrowLayout);
    }

    [AvaloniaFact]
    public void Narrow_Tightens_The_Rows_Footer_And_Button()
    {
        using var rig = new DocumentExtractionRig(width: 400, instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        Assert.True(x.IsNarrowLayout);
        Assert.True(rig.Row("vendor").IsNarrow);
        Assert.True(x.TableOf("lineItems")!.IsNarrow);
        Assert.False(x.Meta!.ShowsModel);
        Assert.True(x.Viewer!.IsNarrow);
        Assert.Equal(ExtractionFooterMode.Narrow, x.Footer!.Mode);
        // "Extract again" shows only its first word (no icon); the name stays whole.
        Assert.Equal("Extract", x.ActionButton!.Content);
        Assert.Equal("Extract again", AutomationProperties.GetName(x.ActionButton));
        Assert.Equal(x.Footer.Bounds.Width, x.AcceptAllButton!.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void Extract_Again_Shortens_When_The_Head_Has_No_Room()
    {
        using var rig = new DocumentExtractionRig(instant: true);
        var x = rig.Extraction;
        rig.FullRun();
        // At full width the label is the long one or, with the wide headless font, already the short one: both are right.
        // What must hold is the narrow case below.
        rig.Host.Width = 761;
        rig.Pump();
        rig.Pump();
        // A 341px pane side by side has no room for "Extract again" beside the tabs.
        Assert.False(x.IsNarrowLayout);
        Assert.Equal("Extract", x.ActionButton!.Content);
        Assert.Equal("Extract again", AutomationProperties.GetName(x.ActionButton));
    }

    [AvaloniaFact]
    public void Names_Read_The_Form()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        var strip = ControlAutomationPeer.CreatePeerForElement(x.TabStrip!);
        Assert.Equal("Output", strip.GetName());
        Assert.Equal(AutomationControlType.Tab, strip.GetAutomationControlType());
        var tab = ControlAutomationPeer.CreatePeerForElement(x.TabStrip!.ButtonOf(ExtractionTab.Json)!);
        Assert.Equal(AutomationControlType.TabItem, tab.GetAutomationControlType());
        Assert.Equal("Typed output as JSON", x.JsonView!.AutomationName);
        Assert.Equal("JSON schema", x.SchemaView!.AutomationName);
        var group = (Control)x.FormPanel!.Children[0];
        Assert.Equal("Invoice", AutomationProperties.GetName(group));
        Assert.Equal("Vendor", rig.Row("vendor").AutomationName);
        Assert.Equal("Extract", AutomationProperties.GetName(x.ActionButton!));
        Assert.Equal("Accept all", AutomationProperties.GetName(x.AcceptAllButton!));
        // The table group has no title of its own: the table's header carries the dot.
        var tableGroup = (Panel)x.FormPanel.Children[1];
        Assert.IsType<ExtractionTableGrid>(tableGroup.Children[0]);
    }

    [AvaloniaFact]
    public void Zoom_Fit_And_Tab_Are_Two_Way()
    {
        using var rig = new DocumentExtractionRig();
        var x = rig.Extraction;
        x.Zoom = 1.25;
        x.IsFitWidth = false;
        rig.Pump();
        Assert.Equal(1.25, x.Viewer!.Zoom);
        Assert.False(x.Viewer.IsFitWidth);
        x.Viewer.SetCurrentValue(DocumentViewer.ZoomProperty, 0.75);
        rig.Pump();
        Assert.Equal(0.75, x.Zoom);
        Assert.False(x.IsFitWidth);
        // With Fit on the viewer sets the zoom from the canvas width, and the control follows.
        x.Viewer.SetCurrentValue(DocumentViewer.IsFitWidthProperty, true);
        rig.Pump();
        Assert.True(x.IsFitWidth);
        x.SelectedTab = ExtractionTab.Schema;
        rig.Pump();
        Assert.Equal(ExtractionTab.Schema, x.TabStrip!.SelectedTab);
        x.TabStrip.Select(ExtractionTab.Fields);
        rig.Pump();
        Assert.Equal(ExtractionTab.Fields, x.SelectedTab);
    }
}
