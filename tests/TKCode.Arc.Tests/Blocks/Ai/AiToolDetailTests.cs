using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiToolDetail"/>: the panel geometry, the search, query, failed and custom layouts, the source rows (names, narrow
/// width, invoking by pointer and keys), following the tool's changes, and opening and closing at each motion level.
/// </summary>
public class AiToolDetailTests
{
    private static AiToolDetail Detail(AiChatTool tool, bool open = true, Action<AiToolDetail>? configure = null)
    {
        var detail = new AiToolDetail
        {
            Tool = tool,
            IsExpanded = open,
            SearchesName = "Searches",
            SourcesName = "Sources",
            ResultText = "Result",
            SourceLinkName = AiReplySamples.SourceLink
        };
        configure?.Invoke(detail);
        return detail;
    }

    private static List<AiToolSourceRow> Rows(Visual root) => root.GetVisualDescendants().OfType<AiToolSourceRow>().ToList();

    [AvaloniaFact]
    public void Panel_Sits_8px_Under_With_A_Subtle_Edge_Radius_16_And_Padding_12_14()
    {
        using var rig = new AiReplyRig(Detail(AiReplySamples.Search()));
        var detail = (AiToolDetail)rig.Content;
        var panel = TestHelpers.Descendant<Border>(detail, "PART_Panel");
        panel.TranslatePoint(default, detail)!.Value.ShouldBe(new Point(0, 8));
        panel.Padding.ShouldBe(new Thickness(14, 12));
        panel.CornerRadius.ShouldBe(new CornerRadius(16));
        panel.BorderThickness.ShouldBe(new Thickness(1));
        TestHelpers.ColorOf(panel.BorderBrush).ShouldBe(ArcTokens.GetColor(detail, ArcDesignTokenKind.BorderSubtle));
        detail.Bounds.Height.ShouldBe(8 + panel.Bounds.Height, 0.01);
    }

    [AvaloniaFact]
    public void Search_Lists_The_Terms_Then_The_Sources()
    {
        using var rig = new AiReplyRig(Detail(AiReplySamples.Search()));
        var detail = (AiToolDetail)rig.Content;
        detail.Classes.ShouldContain(AiToolDetail.SearchPseudoClass);
        var queries = TestHelpers.Descendant<ItemsControl>(detail, "PART_Queries");
        queries.IsVisible.ShouldBeTrue();
        AutomationProperties.GetName(queries).ShouldBe("Searches");
        var terms = queries.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        terms.ShouldBe(["build cache key changed lock file", "restore cache miss slow compile"]);
        var glyph = queries.GetVisualDescendants().OfType<ArcGlyph>().First();
        glyph.Size.ShouldBe(13);
        TestHelpers.ColorOf(glyph.Foreground).ShouldBe(ArcTokens.GetColor(detail, ArcDesignTokenKind.TextMuted));

        var sources = TestHelpers.Descendant<ItemsControl>(detail, "PART_Sources");
        AutomationProperties.GetName(sources).ShouldBe("Sources");
        var rows = Rows(detail);
        rows.Count.ShouldBe(4);
        rows[0].Bounds.Height.ShouldBe(32);
        // Rows reach 8px past the text on both sides.
        var panel = TestHelpers.Descendant<Border>(detail, "PART_Panel");
        rows[0].TranslatePoint(default, panel)!.Value.X.ShouldBe(1 + 14 - 8, 0.01);
        rows[0].Bounds.Width.ShouldBe(panel.Bounds.Width - 2 - 28 + 16, 0.01);
        var mark = TestHelpers.Descendant<AiToolSourceMark>(rows[0], "PART_Mark");
        mark.Size.ShouldBe(14);
        var title = TestHelpers.Descendant<TextBlock>(rows[0], "PART_Title");
        title.Text.ShouldBe("Caching dependencies in pipelines");
        title.TranslatePoint(default, rows[0])!.Value.X.ShouldBe(8 + 14 + 10, 0.01);
        var domain = TestHelpers.Descendant<TextBlock>(rows[0], "PART_Domain");
        domain.Text.ShouldBe("docs.example.org");
        domain.IsVisible.ShouldBeTrue();
        domain.FontSize.ShouldBe(12);
        (domain.TranslatePoint(default, rows[0])!.Value.X + domain.Bounds.Width).ShouldBe(rows[0].Bounds.Width - 8, 0.01);
        AutomationProperties.GetName(rows[1]).ShouldBe("Source 2: Why our cache hit rate fell to zero, blog.example.dev");
        ControlAutomationPeer.CreatePeerForElement(rows[1]).GetAutomationControlType().ShouldBe(AutomationControlType.Hyperlink);
        // A search has no statement or result.
        TestHelpers.Descendant<Control>(detail, "PART_InputScroll").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Control>(detail, "PART_ResultRow").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Narrow_Hides_The_Domains_And_Titles_Trim()
    {
        var tool = AiReplySamples.Search();
        tool.Sources[0].Title = string.Concat(Enumerable.Repeat("A very long source title ", 12));
        using var rig = new AiReplyRig(Detail(tool, configure: d => d.IsNarrow = true), width: 340);
        var detail = (AiToolDetail)rig.Content;
        var rows = Rows(detail);
        rows.ShouldAllBe(r => r.IsNarrow);
        TestHelpers.Descendant<TextBlock>(rows[0], "PART_Domain").IsVisible.ShouldBeFalse();
        var title = TestHelpers.Descendant<TextBlock>(rows[0], "PART_Title");
        title.TextTrimming.ShouldBe(Avalonia.Media.TextTrimming.CharacterEllipsis);
        (title.TranslatePoint(default, rows[0])!.Value.X + title.Bounds.Width).ShouldBeLessThanOrEqualTo(rows[0].Bounds.Width - 8 + 0.01);
        detail.IsNarrow = false;
        rig.Settle();
        TestHelpers.Descendant<TextBlock>(rows[0], "PART_Domain").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Source_Row_Raises_SourceInvoked_On_Press_Enter_And_Space()
    {
        var detail = Detail(AiReplySamples.Search());
        var invoked = new List<AiSource>();
        detail.SourceInvoked += (_, e) => invoked.Add(e.Item);
        using var rig = new AiReplyRig(detail);
        var rows = Rows(detail);
        rig.Click(rows[2]);
        rows[1].Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Space);
        invoked.Select(s => s.Index).ShouldBe([3, 2, 2]);
        rows[1].Classes.ShouldContain(":focus-visible");
        rig.Hover(rows[0]);
        rows[0].Classes.ShouldContain(AiToolSourceRow.HoverPseudoClass);
        TestHelpers.ColorOf(rows[0].Background).ShouldBe(ArcTokens.GetColor(detail, ArcDesignTokenKind.SurfaceMuted));
    }

    [AvaloniaFact]
    public void Query_Shows_The_Statement_And_The_Result()
    {
        var tool = AiReplySamples.Query();
        using var rig = new AiReplyRig(Detail(tool));
        var detail = (AiToolDetail)rig.Content;
        TestHelpers.Descendant<ItemsControl>(detail, "PART_Queries").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<ItemsControl>(detail, "PART_Sources").IsVisible.ShouldBeFalse();
        var scroll = TestHelpers.Descendant<ScrollArea>(detail, "PART_InputScroll");
        scroll.IsVisible.ShouldBeTrue();
        scroll.Orientation.ShouldBe(ScrollAreaOrientation.Horizontal);
        var input = TestHelpers.Descendant<SelectableTextBlock>(detail, "PART_Input");
        input.Text.ShouldBe(tool.Input);
        input.FontSize.ShouldBe(12);
        input.LineHeight.ShouldBe(19.2);
        input.TextWrapping.ShouldBe(Avalonia.Media.TextWrapping.NoWrap);
        var label = TestHelpers.Descendant<TextBlock>(detail, "PART_ResultLabel");
        label.Text.ShouldBe("Result");
        TestHelpers.ColorOf(label.Foreground).ShouldBe(ArcTokens.GetColor(detail, ArcDesignTokenKind.TextMuted));
        var output = TestHelpers.Descendant<SelectableTextBlock>(detail, "PART_Output");
        output.Text.ShouldBe("restore 4.1 s, compile 19.6 min");
        TestHelpers.Descendant<StackPanel>(detail, "PART_ResultRow").Spacing.ShouldBe(10);

        tool.OutputLabel = "Averages";
        rig.Settle();
        label.Text.ShouldBe("Averages");
    }

    [AvaloniaFact]
    public void A_Failed_Query_Shows_Only_The_Statement()
    {
        using var rig = new AiReplyRig(Detail(AiReplySamples.Query(AiChatToolStatus.Error)));
        var detail = (AiToolDetail)rig.Content;
        detail.Classes.ShouldContain(AiToolDetail.ErrorPseudoClass);
        TestHelpers.Descendant<Control>(detail, "PART_InputScroll").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<Control>(detail, "PART_ResultRow").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Custom_Tools_Use_The_Owner_Template_With_The_Tool_As_Data_And_Never_Null()
    {
        var probe = new NullTemplateProbe<AiChatTool>();
        var tool = new AiChatTool { Kind = AiChatToolKind.Custom, Status = AiChatToolStatus.Done, DoneLabel = "Opened the runbook" };
        var detail = Detail(tool, configure: d => d.DetailTemplate = probe.Template);
        using var rig = new AiReplyRig(detail);
        probe.Built.ShouldBe([tool]);
        TestHelpers.Descendant<ContentPresenter>(detail, "PART_Custom").IsVisible.ShouldBeTrue();
        // Switching the tool away and back, and dropping the template, never builds it with null.
        detail.Tool = AiReplySamples.Query();
        rig.Settle();
        detail.Tool = tool;
        rig.Settle();
        detail.DetailTemplate = null;
        rig.Settle();
        probe.NullBuilds.ShouldBe(0);
        TestHelpers.Descendant<ContentPresenter>(detail, "PART_Custom").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Panel_Follows_The_Tool_Lists()
    {
        var tool = AiReplySamples.Search(AiChatToolStatus.Running);
        using var rig = new AiReplyRig(Detail(tool));
        var detail = (AiToolDetail)rig.Content;
        Rows(detail).ShouldBeEmpty();
        TestHelpers.Descendant<ItemsControl>(detail, "PART_Sources").IsVisible.ShouldBeFalse();
        tool.Sources.Add(AiReplySamples.Sources()[0]);
        tool.Queries.Add("third term");
        rig.Settle();
        Rows(detail).Count.ShouldBe(1);
        TestHelpers.Descendant<ItemsControl>(detail, "PART_Queries").GetVisualDescendants().OfType<TextBlock>().Count().ShouldBe(3);
        tool.Sources[0].Title = "Renamed source";
        rig.Settle();
        TestHelpers.Descendant<TextBlock>(Rows(detail)[0], "PART_Title").Text.ShouldBe("Renamed source");
        AutomationProperties.GetName(Rows(detail)[0]).ShouldBe("Source 1: Renamed source, docs.example.org");
    }

    [AvaloniaFact]
    public void Closed_Panels_Are_Unmounted_And_Take_No_Space()
    {
        using var rig = new AiReplyRig(Detail(AiReplySamples.Search(), open: false));
        var detail = (AiToolDetail)rig.Content;
        detail.Body.ShouldNotBeNull().IsMounted.ShouldBeFalse();
        detail.Bounds.Height.ShouldBe(0);
        Rows(detail).ShouldBeEmpty();
        detail.IsExpanded = true;
        rig.Settle();
        detail.Body!.IsMounted.ShouldBeTrue();
        Rows(detail).Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Full_Motion_Opens_On_A_Spring_With_The_Gap_At_Once_And_Unmounts_After_Closing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(Detail(AiReplySamples.Query(), open: false), ArcMotionLevel.Full, clock);
        var detail = (AiToolDetail)rig.Content;
        var body = detail.Body.ShouldNotBeNull();
        detail.IsExpanded = true;
        rig.Settle();
        rig.Frames(34);
        body.IsMounted.ShouldBeTrue();
        var panel = TestHelpers.Descendant<Border>(detail, "PART_Panel");
        body.DisplayedHeight.ShouldBeGreaterThan(0);
        body.DisplayedHeight.ShouldBeLessThan(panel.Bounds.Height);
        detail.Bounds.Height.ShouldBe(8 + body.DisplayedHeight, 1);
        rig.Frames(800);
        body.DisplayedHeight.ShouldBe(panel.Bounds.Height, 0.5);
        body.IsMoving.ShouldBeFalse();
        detail.IsExpanded = false;
        rig.Settle();
        rig.Frames(900);
        body.IsMounted.ShouldBeFalse();
        detail.Bounds.Height.ShouldBe(0);
    }
}
