using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiRunMeta"/>: the parts and their dots, the narrow form without the name, missing figures, colors, tabular
/// figures, spacing, and the line's accessible name.
/// </summary>
public class AiRunMetaTests
{
    /// <summary>An invented neutral spark (24 × 24), standing in for a model's mark.</summary>
    private static readonly Geometry Spark = Geometry.Parse("M12 2 L14.2 9.8 L22 12 L14.2 14.2 L12 22 L9.8 14.2 L2 12 L9.8 9.8 Z");

    private static AiRunMeta Meta() => new()
    {
        ModelMark = Spark,
        ModelName = "Lumen Extract S",
        CostText = "$0.0003",
        ElapsedText = "3.2 s"
    };

    private static bool Visible(Control root, string name) => TestHelpers.Descendant<Control>(root, name).IsVisible;

    [AvaloniaFact]
    public void Mark_Name_Cost_And_Time_With_Dots_Between()
    {
        using var rig = new AiReplyRig(Meta());
        var meta = (AiRunMeta)rig.Content;
        Visible(meta, "PART_Mark").ShouldBeTrue();
        Visible(meta, "PART_Name").ShouldBeTrue();
        Visible(meta, "PART_CostDot").ShouldBeTrue();
        Visible(meta, "PART_Cost").ShouldBeTrue();
        Visible(meta, "PART_ElapsedDot").ShouldBeTrue();
        Visible(meta, "PART_Elapsed").ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(meta, "PART_Name").Text.ShouldBe("Lumen Extract S");
        TestHelpers.Descendant<TextBlock>(meta, "PART_Cost").Text.ShouldBe("$0.0003");
        TestHelpers.Descendant<TextBlock>(meta, "PART_Elapsed").Text.ShouldBe("3.2 s");
        TestHelpers.Descendant<ModelMark>(meta, "PART_Mark").Size.ShouldBe(14);
        TestHelpers.Descendant<StackPanel>(meta, "PART_Line").Spacing.ShouldBe(7);
        var dot = TestHelpers.Descendant<Ellipse>(meta, "PART_CostDot");
        dot.Bounds.Width.ShouldBe(3);
        dot.Bounds.Height.ShouldBe(3);
        dot.Opacity.ShouldBe(0.55);
        meta.Classes.ShouldNotContain(AiRunMeta.CompactPseudoClass);
        // The parts sit in order, 7px apart.
        var mark = TestHelpers.Descendant<Control>(meta, "PART_Mark").Bounds;
        var name = TestHelpers.Descendant<Control>(meta, "PART_Name").Bounds;
        name.X.ShouldBe(mark.Right + 7, 0.01);
        TestHelpers.Descendant<Control>(meta, "PART_CostDot").Bounds.X.ShouldBe(name.Right + 7, 0.01);
    }

    [AvaloniaFact]
    public void Colors_And_Tabular_Figures()
    {
        using var rig = new AiReplyRig(Meta());
        var meta = (AiRunMeta)rig.Content;
        Color Token(ArcDesignTokenKind kind) => ArcTokens.GetColor(meta, kind);
        meta.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(meta, "PART_Name").Foreground).ShouldBe(Token(ArcDesignTokenKind.TextSecondary));
        foreach (var part in new[] { "PART_Cost", "PART_Elapsed" })
        {
            var text = TestHelpers.Descendant<TextBlock>(meta, part);
            TestHelpers.ColorOf(text.Foreground).ShouldBe(Token(ArcDesignTokenKind.TextMuted));
            text.FontFeatures.ShouldNotBeNull();
            text.FontFeatures!.Select(f => f.Tag).ShouldContain("tnum");
            text.LineHeight.ShouldBe(16.8);
        }
        TestHelpers.ColorOf(TestHelpers.Descendant<Ellipse>(meta, "PART_ElapsedDot").Fill).ShouldBe(Token(ArcDesignTokenKind.TextMuted));
    }

    [AvaloniaFact]
    public void The_Narrow_Footer_Hides_The_Name_And_Keeps_The_Mark()
    {
        using var rig = new AiReplyRig(Meta());
        var meta = (AiRunMeta)rig.Content;
        meta.ShowsModel = false;
        rig.Settle();
        meta.Classes.ShouldContain(AiRunMeta.CompactPseudoClass);
        Visible(meta, "PART_Mark").ShouldBeTrue();
        Visible(meta, "PART_Name").ShouldBeFalse();
        // "✦ $0.0003 · 3.2 s": no dot between the mark and the cost.
        Visible(meta, "PART_CostDot").ShouldBeFalse();
        Visible(meta, "PART_ElapsedDot").ShouldBeTrue();
        var mark = TestHelpers.Descendant<Control>(meta, "PART_Mark").Bounds;
        TestHelpers.Descendant<Control>(meta, "PART_Cost").Bounds.X.ShouldBe(mark.Right + 7, 0.01);
        meta.SpokenText.ShouldBe("$0.0003, 3.2 s");
    }

    [AvaloniaFact]
    public void A_Missing_Figure_Leaves_With_Its_Dot()
    {
        using var rig = new AiReplyRig(Meta());
        var meta = (AiRunMeta)rig.Content;
        meta.CostText = null;
        rig.Settle();
        Visible(meta, "PART_Cost").ShouldBeFalse();
        Visible(meta, "PART_CostDot").ShouldBeFalse();
        Visible(meta, "PART_ElapsedDot").ShouldBeTrue();
        meta.ElapsedText = "";
        rig.Settle();
        Visible(meta, "PART_Elapsed").ShouldBeFalse();
        Visible(meta, "PART_ElapsedDot").ShouldBeFalse();
        // Without a name or a cost, the time is not led by a dot.
        meta.ElapsedText = "0.4 s";
        meta.ShowsModel = false;
        rig.Settle();
        Visible(meta, "PART_Elapsed").ShouldBeTrue();
        Visible(meta, "PART_ElapsedDot").ShouldBeFalse();
        // No mark: nothing before the figures.
        meta.ModelMark = null;
        rig.Settle();
        Visible(meta, "PART_Mark").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Figures_Change_In_Place()
    {
        using var rig = new AiReplyRig(Meta());
        var meta = (AiRunMeta)rig.Content;
        var elapsed = TestHelpers.Descendant<TextBlock>(meta, "PART_Elapsed");
        meta.ElapsedText = "3.3 s";
        rig.Settle();
        TestHelpers.Descendant<TextBlock>(meta, "PART_Elapsed").ShouldBeSameAs(elapsed);
        elapsed.Text.ShouldBe("3.3 s");
        meta.SpokenText.ShouldBe("Lumen Extract S, $0.0003, 3.3 s");
    }

    [AvaloniaFact]
    public void Reads_As_One_Line_Named_By_The_Owner_Or_By_What_It_Shows()
    {
        using var rig = new AiReplyRig(Meta());
        var meta = (AiRunMeta)rig.Content;
        AutomationProperties.GetName(meta).ShouldBe("Lumen Extract S, $0.0003, 3.2 s");
        var peer = ControlAutomationPeer.CreatePeerForElement(meta);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        peer.GetName().ShouldBe("Lumen Extract S, $0.0003, 3.2 s");
        peer.GetChildren().ShouldBeEmpty();
        meta.AutomationName = "Lumen Extract S, cost $0.0003, 3.2 seconds";
        AutomationProperties.GetName(meta).ShouldBe("Lumen Extract S, cost $0.0003, 3.2 seconds");
        peer.GetName().ShouldBe("Lumen Extract S, cost $0.0003, 3.2 seconds");
        meta.AutomationName = null;
        peer.GetName().ShouldBe("Lumen Extract S, $0.0003, 3.2 s");
        meta.Focusable.ShouldBeFalse();
    }
}
