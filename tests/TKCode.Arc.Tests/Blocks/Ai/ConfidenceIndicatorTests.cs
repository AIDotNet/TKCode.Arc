using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ConfidenceIndicator"/>: every state with its icon, text and colors, the percent rounding, the crossfade on a
/// manual clock at Full and Reduced motion (and none when Off or for the first state), the check that pops on the snappy
/// spring, and what screen readers get.
/// </summary>
public class ConfidenceIndicatorTests
{
    private const double Frame = 1.0 / 60;

    private static ConfidenceIndicator Indicator(ExtractionValue? value, string? typeName = "Date") => new()
    {
        Value = value,
        TypeName = typeName,
        EditedText = "Edited",
        Width = 60
    };

    private static ExtractionValue Filled(double confidence, bool low = false, string path = "dueDate") => new(path)
    {
        Text = "2026-09-17",
        Confidence = confidence,
        Status = ExtractionValueStatus.Filled,
        NeedsCheck = low
    };

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static ContentPresenter[] Layers(ConfidenceIndicator indicator) =>
        indicator.Swap.ShouldNotBeNull().GetVisualChildren().OfType<ContentPresenter>().ToArray();

    private static TextBlock FaceText(ConfidenceIndicatorFace face) => TestHelpers.Descendant<TextBlock>(face, "PART_Text");

    private static ArcGlyph FaceGlyph(ConfidenceIndicatorFace face) => TestHelpers.Descendant<ArcGlyph>(face, ConfidenceIndicatorFace.GlyphPart);

    [AvaloniaFact]
    public void A_Pending_Value_Shows_Its_Type_Name_In_TextMuted()
    {
        using var rig = new ExtractionFieldRig(Indicator(new ExtractionValue("dueDate")));
        var indicator = (ConfidenceIndicator)rig.Content;
        indicator.Kind.ShouldBe(ConfidenceKind.Type);
        indicator.Text.ShouldBe("Date");
        var face = indicator.Face.ShouldNotBeNull();
        face.Classes.ShouldContain(ConfidenceIndicatorFace.TypePseudoClass);
        FaceText(face).Text.ShouldBe("Date");
        TestHelpers.ColorOf(FaceText(face).Foreground).ShouldBe(Token(indicator, ArcDesignTokenKind.TextMuted));
        FaceGlyph(face).IsVisible.ShouldBeFalse();
        // No value at all reads the same; no type name shows nothing (a table without values).
        indicator.Value = null;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Type);
        indicator.TypeName = null;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.None);
        indicator.Text.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Every_State_Has_Its_Icon_Text_And_Colors()
    {
        var value = new ExtractionValue("dueDate");
        using var rig = new ExtractionFieldRig(Indicator(value));
        var indicator = (ConfidenceIndicator)rig.Content;

        // Filled: "99%", TextMuted, no icon.
        value.Text = "2026-09-17";
        value.Confidence = 0.99;
        value.Status = ExtractionValueStatus.Filled;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Percent);
        var face = indicator.Face.ShouldNotBeNull();
        FaceText(face).Text.ShouldBe("99%");
        FaceGlyph(face).IsVisible.ShouldBeFalse();
        TestHelpers.ColorOf(FaceText(face).Foreground).ShouldBe(Token(indicator, ArcDesignTokenKind.TextMuted));
        FaceText(face).FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");

        // Low: a Warning triangle and the percent in Foreground.
        value.Confidence = 0.64;
        value.NeedsCheck = true;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Low);
        face = indicator.Face.ShouldNotBeNull();
        FaceText(face).Text.ShouldBe("64%");
        FaceGlyph(face).IsVisible.ShouldBeTrue();
        FaceGlyph(face).Data.ShouldBe(ArcIcons.TriangleAlert);
        FaceGlyph(face).Size.ShouldBe(13);
        TestHelpers.ColorOf(FaceGlyph(face).Foreground).ShouldBe(Token(indicator, ArcDesignTokenKind.Warning));
        TestHelpers.ColorOf(FaceText(face).Foreground).ShouldBe(Token(indicator, ArcDesignTokenKind.Foreground));

        // Checked: a Success check and the percent in TextMuted.
        value.Status = ExtractionValueStatus.Checked;
        value.NeedsCheck = false;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Checked);
        face = indicator.Face.ShouldNotBeNull();
        FaceText(face).Text.ShouldBe("64%");
        FaceGlyph(face).Data.ShouldBe(ArcIcons.Check);
        TestHelpers.ColorOf(FaceGlyph(face).Foreground).ShouldBe(Token(indicator, ArcDesignTokenKind.Success));
        TestHelpers.ColorOf(FaceText(face).Foreground).ShouldBe(Token(indicator, ArcDesignTokenKind.TextMuted));

        // Accepted (by the owner's flag or the status): the same look.
        indicator.IsAccepted = true;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Accepted);
        FaceGlyph(indicator.Face!).Data.ShouldBe(ArcIcons.Check);
        indicator.IsAccepted = false;
        value.Status = ExtractionValueStatus.Accepted;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Accepted);
        FaceText(indicator.Face!).Text.ShouldBe("64%");

        // Edited: a TextMuted pencil and "Edited"; edited and accepted: the check and "Edited".
        value.Status = ExtractionValueStatus.Edited;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Edited);
        face = indicator.Face.ShouldNotBeNull();
        FaceText(face).Text.ShouldBe("Edited");
        FaceGlyph(face).Data.ShouldBe(ConfidenceIndicatorFace.PencilLine);
        TestHelpers.ColorOf(FaceGlyph(face).Foreground).ShouldBe(Token(indicator, ArcDesignTokenKind.TextMuted));
        indicator.IsAccepted = true;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.EditedAccepted);
        FaceText(indicator.Face!).Text.ShouldBe("Edited");
        FaceGlyph(indicator.Face!).Data.ShouldBe(ArcIcons.Check);
    }

    [AvaloniaFact]
    public void Percents_Are_Whole_And_Rounded_Half_Up()
    {
        ConfidenceIndicator.Percent(0.99).ShouldBe("99%");
        ConfidenceIndicator.Percent(0.935).ShouldBe("94%");
        ConfidenceIndicator.Percent(0.9349).ShouldBe("93%");
        ConfidenceIndicator.Percent(0.6649).ShouldBe("66%");
        ConfidenceIndicator.Percent(1).ShouldBe("100%");
        ConfidenceIndicator.Percent(1.4).ShouldBe("100%");
        ConfidenceIndicator.Percent(-1).ShouldBe("0%");
        ConfidenceIndicator.Percent(double.NaN).ShouldBe("0%");
    }

    [AvaloniaFact]
    public void The_Column_Is_Right_Aligned_At_Twelve_Pixels()
    {
        using var rig = new ExtractionFieldRig(Indicator(Filled(0.99)));
        var indicator = (ConfidenceIndicator)rig.Content;
        var face = indicator.Face.ShouldNotBeNull();
        FaceText(face).FontSize.ShouldBe(12);
        FaceText(face).LineHeight.ShouldBe(16.8);
        var right = face.TranslatePoint(new Point(face.Bounds.Width, 0), indicator)!.Value.X;
        right.ShouldBe(60, 0.5);
        // The icon sits 4 before the text.
        indicator.Value!.NeedsCheck = true;
        rig.Settle();
        face = indicator.Face!;
        var glyph = FaceGlyph(face);
        var text = FaceText(face);
        (text.TranslatePoint(default, face)!.Value.X - glyph.TranslatePoint(new Point(13, 0), face)!.Value.X).ShouldBe(4, 0.01);
    }

    [AvaloniaFact]
    public void The_First_State_And_Every_State_Without_Motion_Show_At_Once()
    {
        using var rig = new ExtractionFieldRig(Indicator(Filled(0.97)));
        var indicator = (ConfidenceIndicator)rig.Content;
        Layers(indicator).Length.ShouldBe(1);
        indicator.Value!.Status = ExtractionValueStatus.Checked;
        rig.Settle();
        Layers(indicator).Length.ShouldBe(1);
        Layers(indicator)[0].Opacity.ShouldBe(1);
        indicator.Face!.GlyphScale.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Change_Crossfades_Old_Out_In_100ms_And_New_In_Over_160ms_Rising_2px()
    {
        var value = new ExtractionValue("dueDate");
        using var rig = new ExtractionFieldRig(Indicator(value), ArcMotionLevel.Full);
        var indicator = (ConfidenceIndicator)rig.Content;
        Layers(indicator).Length.ShouldBe(1);

        value.Text = "2026-09-17";
        value.Confidence = 0.97;
        value.Status = ExtractionValueStatus.Filled;
        rig.Settle();
        var layers = Layers(indicator);
        layers.Length.ShouldBe(2);
        var (outgoing, incoming) = (layers[0], layers[1]);
        incoming.Opacity.ShouldBe(0, 0.001);
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(2, 0.001);
        outgoing.Opacity.ShouldBe(1, 0.001);

        rig.Frames(50);
        outgoing.Opacity.ShouldBeInRange(0.01, 0.6);
        incoming.Opacity.ShouldBeInRange(0.2, 0.95);
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBeInRange(0.05, 1.6);

        rig.Frames(60);
        outgoing.Opacity.ShouldBe(0, 0.001);
        rig.Drain(() => Layers(indicator).Length == 1).ShouldBeTrue();

        rig.Frames(70);
        incoming.Opacity.ShouldBe(1, 0.001);
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0, 0.001);
        FaceText(indicator.Face!).Text.ShouldBe("97%");
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_In_120ms_Without_The_Rise_Or_The_Pop()
    {
        var value = Filled(0.64, low: true);
        using var rig = new ExtractionFieldRig(Indicator(value), ArcMotionLevel.Reduced);
        var indicator = (ConfidenceIndicator)rig.Content;
        value.Status = ExtractionValueStatus.Checked;
        value.NeedsCheck = false;
        rig.Settle();
        var layers = Layers(indicator);
        layers.Length.ShouldBe(2);
        var (outgoing, incoming) = (layers[0], layers[1]);
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0, 0.001);
        indicator.Face!.GlyphScale.ShouldBe(1);
        indicator.Face.IsPopping.ShouldBeFalse();
        rig.Frames(60);
        // Linear: about half way at 60ms.
        incoming.Opacity.ShouldBeInRange(0.35, 0.65);
        outgoing.Opacity.ShouldBeInRange(0.35, 0.65);
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0, 0.001);
        rig.Frames(70);
        incoming.Opacity.ShouldBe(1, 0.001);
        rig.Drain(() => Layers(indicator).Length == 1).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Check_Pops_From_Point_Four_On_The_Snappy_Spring()
    {
        var value = Filled(0.64, low: true);
        using var rig = new ExtractionFieldRig(Indicator(value), ArcMotionLevel.Full);
        var indicator = (ConfidenceIndicator)rig.Content;
        value.Status = ExtractionValueStatus.Checked;
        value.NeedsCheck = false;
        rig.Settle();
        var face = indicator.Face.ShouldNotBeNull();
        face.Pops.ShouldBeTrue();
        face.GlyphScale.ShouldBe(ConfidenceIndicatorFace.PopFrom, 0.001);
        rig.Frames(6 * 1000.0 / 60);
        var (expected, _) = SpringSolver.Evaluate(ArcMotionPresets.Snappy, ConfidenceIndicatorFace.PopFrom - 1, 0, 6 * Frame);
        face.GlyphScale.ShouldBe(1 + expected, 0.01);
        face.GlyphScale.ShouldBeInRange(0.6, 0.99);
        rig.Frames(500);
        face.GlyphScale.ShouldBe(1, 0.001);
        face.IsPopping.ShouldBeFalse();

        // Accept all reaches the checked value: the face swaps (the cascade's crossfade) and the check pops again.
        indicator.IsAccepted = true;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Accepted);
        indicator.Face!.Pops.ShouldBeTrue();
        rig.Frames(500);
        // A percent that changes next to a check keeps it still.
        indicator.Value!.Confidence = 0.7;
        rig.Settle();
        FaceText(indicator.Face!).Text.ShouldBe("70%");
        indicator.Face!.Pops.ShouldBeFalse();
        indicator.Face.GlyphScale.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Accepting_A_Plain_Value_Pops_Its_New_Check()
    {
        using var rig = new ExtractionFieldRig(Indicator(Filled(0.99)), ArcMotionLevel.Full);
        var indicator = (ConfidenceIndicator)rig.Content;
        indicator.IsAccepted = true;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Accepted);
        indicator.Face!.Pops.ShouldBeTrue();
        indicator.Face.GlyphScale.ShouldBe(ConfidenceIndicatorFace.PopFrom, 0.001);
        rig.Frames(500);
        indicator.Face.GlyphScale.ShouldBe(1, 0.001);
    }

    [AvaloniaFact]
    public void The_Same_Kind_And_Text_Keeps_The_Face()
    {
        using var rig = new ExtractionFieldRig(Indicator(Filled(0.97)), ArcMotionLevel.Full);
        var indicator = (ConfidenceIndicator)rig.Content;
        var face = indicator.Face;
        indicator.Value!.Confidence = 0.971;
        indicator.Value.Text = "2026-09-18";
        rig.Settle();
        indicator.Face.ShouldBeSameAs(face);
        Layers(indicator).Length.ShouldBe(1);
        indicator.Value.Confidence = 0.5;
        rig.Settle();
        indicator.Face.ShouldNotBeSameAs(face);
        FaceText(indicator.Face!).Text.ShouldBe("50%");
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_The_Owners_Sentence_And_Not_The_Visible_Text()
    {
        var indicator = Indicator(Filled(0.64, low: true));
        indicator.AutomationText = "Low confidence, 64 percent, check this value";
        using var rig = new ExtractionFieldRig(indicator);
        AutomationProperties.GetName(indicator).ShouldBe("Low confidence, 64 percent, check this value");
        var peer = ControlAutomationPeer.CreatePeerForElement(indicator);
        peer.GetName().ShouldBe("Low confidence, 64 percent, check this value");
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        peer.GetChildren().ShouldBeEmpty();
        indicator.AutomationText = "Checked, 64 percent";
        peer.GetName().ShouldBe("Checked, 64 percent");
        indicator.Focusable.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Value_That_Outlives_The_Indicator_Is_Let_Go()
    {
        var value = Filled(0.9);
        var indicator = Indicator(value);
        var host = new Border { Child = indicator };
        using var rig = new ExtractionFieldRig(host);
        host.Child = null;
        rig.Settle();
        value.Status = ExtractionValueStatus.Checked;
        // Not observed while away: nothing changed on it, and it catches up once back.
        indicator.Kind.ShouldBe(ConfidenceKind.Percent);
        host.Child = indicator;
        rig.Settle();
        indicator.Kind.ShouldBe(ConfidenceKind.Checked);
    }
}
