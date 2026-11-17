using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiRunStatusLine"/>: every kind (icon, colors, count, detail, pill), the geometry, the crossfade and the height
/// spring at each motion level, the count changing in place, the action event, the edge and automation.
/// </summary>
public class AiRunStatusLineTests
{
    private const string Detail = "The values found so far are kept. Extract again to start over.";

    private static AiRunStatusLine Line(AiRunStatusKind kind = AiRunStatusKind.Idle) => new()
    {
        Kind = kind,
        Text = TextOf(kind),
        CountText = kind == AiRunStatusKind.Running ? "3 of 10" : null,
        DetailText = kind is AiRunStatusKind.Stopped or AiRunStatusKind.Failed ? Detail : null,
        ActionText = "Review"
    };

    private static string TextOf(AiRunStatusKind kind) => kind switch
    {
        AiRunStatusKind.Idle => "10 fields to fill from this document",
        AiRunStatusKind.Running => "Extracting fields",
        AiRunStatusKind.NeedsCheck => "2 fields need a check",
        AiRunStatusKind.Done => "Found 10 of 10 fields in 3.1 s",
        AiRunStatusKind.Accepted => "All fields accepted and saved",
        AiRunStatusKind.Stopped => "Stopped after 5 of 10 fields",
        _ => "Extraction failed"
    };

    private static AiRunStatusFace Face(AiRunStatusLine line) => line.Face.ShouldNotBeNull();

    private static string? VisibleIcon(AiRunStatusFace face)
    {
        var cell = TestHelpers.Descendant<Panel>(face, "PART_IconCell");
        return cell.IsVisible ? cell.Children.Single(c => c.IsVisible).Name : null;
    }

    private static void Set(AiRunStatusLine line, AiRunStatusKind kind)
    {
        line.Kind = kind;
        line.Text = TextOf(kind);
        line.CountText = kind == AiRunStatusKind.Running ? "3 of 10" : null;
        line.DetailText = kind is AiRunStatusKind.Stopped or AiRunStatusKind.Failed ? Detail : null;
    }

    [AvaloniaFact]
    public void Every_Kind_Has_Its_Icon_Colors_And_Parts()
    {
        foreach (var kind in Enum.GetValues<AiRunStatusKind>())
        {
            using var rig = new AiReplyRig(Line(kind), width: 420);
            var line = (AiRunStatusLine)rig.Content;
            var face = Face(line);
            Color Token(ArcDesignTokenKind token) => ArcTokens.GetColor(line, token);
            face.Kind.ShouldBe(kind);
            face.Text.ShouldBe(TextOf(kind));
            VisibleIcon(face).ShouldBe(kind switch
            {
                AiRunStatusKind.NeedsCheck => "PART_Warning",
                AiRunStatusKind.Done or AiRunStatusKind.Accepted => "PART_Check",
                AiRunStatusKind.Stopped => "PART_Square",
                AiRunStatusKind.Failed => "PART_Alert",
                _ => null
            });
            var running = kind == AiRunStatusKind.Running;
            TestHelpers.Descendant<StackPanel>(face, "PART_Running").IsVisible.ShouldBe(running);
            TestHelpers.Descendant<TextBlock>(face, "PART_Text").IsVisible.ShouldBe(!running);
            TestHelpers.Descendant<TextBlock>(face, "PART_Count").IsVisible.ShouldBe(running);
            var hasDetail = kind is AiRunStatusKind.Stopped or AiRunStatusKind.Failed;
            TestHelpers.Descendant<TextBlock>(face, "PART_Detail").IsVisible.ShouldBe(hasDetail);
            line.ShowsAction.ShouldBe(kind == AiRunStatusKind.NeedsCheck);
            line.Classes.Contains(AiRunStatusLine.ActionPseudoClass).ShouldBe(kind == AiRunStatusKind.NeedsCheck);
            TestHelpers.Descendant<PresenceHost>(line, "PART_ActionPresence").IsPresent.ShouldBe(kind == AiRunStatusKind.NeedsCheck);
            TestHelpers.ColorOf(face.Foreground).ShouldBe(Token(kind switch
            {
                AiRunStatusKind.Idle => ArcDesignTokenKind.TextMuted,
                AiRunStatusKind.NeedsCheck or AiRunStatusKind.Failed => ArcDesignTokenKind.Foreground,
                _ => ArcDesignTokenKind.TextSecondary
            }));
            if (running)
            {
                var shimmer = TestHelpers.Descendant<TextShimmer>(face, "PART_Shimmer");
                shimmer.Text.ShouldBe("Extracting fields");
                shimmer.IsActive.ShouldBeTrue();
                TestHelpers.ColorOf(shimmer.Foreground).ShouldBe(Token(ArcDesignTokenKind.Foreground));
                var count = TestHelpers.Descendant<TextBlock>(face, "PART_Count");
                count.Text.ShouldBe("3 of 10");
                TestHelpers.ColorOf(count.Foreground).ShouldBe(Token(ArcDesignTokenKind.TextMuted));
                count.FontFeatures!.Select(f => f.Tag).ShouldContain("tnum");
            }
            if (hasDetail)
            {
                var detail = TestHelpers.Descendant<TextBlock>(face, "PART_Detail");
                detail.Text.ShouldBe(Detail);
                detail.FontSize.ShouldBe(12);
                detail.LineHeight.ShouldBe(16.8);
                detail.Margin.ShouldBe(new Thickness(24, 0, 0, 4));
                TestHelpers.ColorOf(detail.Foreground).ShouldBe(Token(ArcDesignTokenKind.TextMuted));
            }
        }
    }

    [AvaloniaFact]
    public void Icons_Have_Their_Colors()
    {
        using var rig = new AiReplyRig(Line(AiRunStatusKind.NeedsCheck));
        var line = (AiRunStatusLine)rig.Content;
        var face = Face(line);
        Color Token(ArcDesignTokenKind token) => ArcTokens.GetColor(line, token);
        TestHelpers.ColorOf(TestHelpers.Descendant<ArcGlyph>(face, "PART_Warning").Foreground).ShouldBe(Token(ArcDesignTokenKind.Warning));
        TestHelpers.ColorOf(TestHelpers.Descendant<ArcGlyph>(face, "PART_Check").Foreground).ShouldBe(Token(ArcDesignTokenKind.Success));
        TestHelpers.ColorOf(TestHelpers.Descendant<ArcGlyph>(face, "PART_Alert").Foreground).ShouldBe(Token(ArcDesignTokenKind.Danger));
        var square = TestHelpers.Descendant<Border>(face, "PART_Square");
        TestHelpers.ColorOf(square.Background).ShouldBe(Token(ArcDesignTokenKind.TextMuted));
        square.Width.ShouldBe(9);
        TestHelpers.Descendant<ArcGlyph>(face, "PART_Warning").Size.ShouldBe(16);
    }

    [AvaloniaFact]
    public void Geometry_Matches_Arc()
    {
        using var rig = new AiReplyRig(Line(), width: 700);
        var line = (AiRunStatusLine)rig.Content;
        // Padding 10 12 8 14 around a row at least 30 high, then the 1px edge: 49.
        line.Bounds.Height.ShouldBe(49, 0.01);
        line.Padding.ShouldBe(new Thickness(14, 10, 12, 8));
        line.BorderThickness.ShouldBe(new Thickness(0, 0, 0, 1));
        TestHelpers.ColorOf(line.Background).ShouldBe(ArcTokens.GetColor(line, ArcDesignTokenKind.Surface));
        var text = TestHelpers.Descendant<TextBlock>(Face(line), "PART_Text");
        text.TranslatePoint(default, line)!.Value.X.ShouldBe(14, 0.01);
        text.FontSize.ShouldBe(14);
        text.LineHeight.ShouldBe(19.6);

        // With an icon, the text starts 24 in (16 + 8); the pill sits at the right, 12 from the edge, centered on the row.
        Set(line, AiRunStatusKind.NeedsCheck);
        rig.Settle();
        text = TestHelpers.Descendant<TextBlock>(Face(line), "PART_Text");
        text.TranslatePoint(default, line)!.Value.X.ShouldBe(14 + 24, 0.01);
        var pill = line.Action.ShouldNotBeNull();
        pill.Bounds.Height.ShouldBe(28);
        var pillOrigin = pill.TranslatePoint(default, line)!.Value;
        (pillOrigin.X + pill.Bounds.Width).ShouldBe(line.Bounds.Width - 12, 0.01);
        pillOrigin.Y.ShouldBe(10 + 1, 0.01);
        line.Bounds.Height.ShouldBe(49, 0.01);
    }

    [AvaloniaFact]
    public void The_Action_Pill_Raises_ActionRequested()
    {
        using var rig = new AiReplyRig(Line(AiRunStatusKind.NeedsCheck));
        var line = (AiRunStatusLine)rig.Content;
        var raised = 0;
        line.ActionRequested += (_, e) =>
        {
            e.Source.ShouldBe(line);
            raised++;
        };
        var pill = line.Action.ShouldNotBeNull();
        TestHelpers.Descendant<ContentPresenter>(pill, "PART_Label").Content.ShouldBe("Review");
        pill.Height.ShouldBe(28);
        pill.Padding.ShouldBe(new Thickness(11, 0));
        pill.FontSize.ShouldBe(12);
        pill.Focus(NavigationMethod.Tab);
        rig.Settle();
        TestHelpers.Descendant<Border>(pill, "PART_FocusRing").IsVisible.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        raised.ShouldBe(1);
        rig.Click(pill);
        raised.ShouldBe(2);
        // The pill is outside the face: a new text keeps it (and its focus).
        line.Text = "1 field needs a check";
        rig.Settle();
        line.Action.ShouldBeSameAs(pill);
        pill.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Pill_Hovers_With_The_Mouse_Only()
    {
        using var rig = new AiReplyRig(Line(AiRunStatusKind.NeedsCheck));
        var line = (AiRunStatusLine)rig.Content;
        var pill = line.Action.ShouldNotBeNull();
        Color Token(ArcDesignTokenKind token) => ArcTokens.GetColor(line, token);
        TestHelpers.ColorOf(pill.Background).ShouldBe(Token(ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(Token(ArcDesignTokenKind.Border));
        rig.Hover(pill);
        pill.Classes.ShouldContain(AiRunStatusAction.HoverPseudoClass);
        TestHelpers.ColorOf(pill.Background).ShouldBe(Token(ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(Token(ArcDesignTokenKind.BorderStrong));
    }

    [AvaloniaFact]
    public void A_New_Count_Changes_In_Place_And_A_New_Text_Is_A_New_Face()
    {
        using var rig = new AiReplyRig(Line(AiRunStatusKind.Running));
        var line = (AiRunStatusLine)rig.Content;
        var face = Face(line);
        var shimmer = TestHelpers.Descendant<TextShimmer>(face, "PART_Shimmer");
        line.CountText = "4 of 10";
        rig.Settle();
        Face(line).ShouldBeSameAs(face);
        TestHelpers.Descendant<TextBlock>(face, "PART_Count").Text.ShouldBe("4 of 10");
        TestHelpers.Descendant<TextShimmer>(face, "PART_Shimmer").ShouldBeSameAs(shimmer);
        line.IsShimmering = false;
        rig.Settle();
        Face(line).ShouldBeSameAs(face);
        shimmer.IsActive.ShouldBeFalse();
        line.IsShimmering = true;
        line.Text = "Reading the document";
        rig.Settle();
        Face(line).ShouldNotBeSameAs(face);
        Face(line).CountText.ShouldBe("4 of 10");
        TestHelpers.Descendant<TextShimmer>(Face(line), "PART_Shimmer").IsActive.ShouldBeTrue();
        // The same kind, text and detail set again keeps the face.
        var current = Face(line);
        line.Kind = AiRunStatusKind.Running;
        line.DetailText = "";
        rig.Settle();
        Face(line).ShouldBeSameAs(current);
    }

    [AvaloniaFact]
    public void Full_Motion_Crossfades_The_Face_And_Springs_The_Height()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new AiReplyRig(Line(AiRunStatusKind.Running), ArcMotionLevel.Full, clock, 420);
        var line = (AiRunStatusLine)rig.Content;
        var before = line.Bounds.Height;
        before.ShouldBe(49, 0.01);
        var old = Face(line);
        Set(line, AiRunStatusKind.Stopped);
        rig.Settle();
        var swap = TestHelpers.Descendant<ArcContentSwap>(line, "PART_Face");
        var size = TestHelpers.Descendant<SpringSizeDecorator>(line, "PART_Size");
        var presenters = swap.GetVisualChildren().OfType<ContentPresenter>().ToList();
        presenters.Count.ShouldBe(2);
        // The old face is out of layout: only the new one sizes the swap.
        swap.DesiredSize.Height.ShouldBe(Face(line).DesiredSize.Height, 0.01);
        var incoming = presenters.Single(p => ReferenceEquals(p.Content, Face(line)));
        var outgoing = presenters.Single(p => ReferenceEquals(p.Content, old));
        rig.Frames(50);
        incoming.Opacity.ShouldBeInRange(0.05, 0.95);
        outgoing.Opacity.ShouldBeLessThan(1);
        ((TranslateTransform?)incoming.RenderTransform)?.Y.ShouldBe(0, 0.001);
        size.IsMoving.ShouldBeTrue();
        line.Bounds.Height.ShouldBeGreaterThan(before);
        var target = 10 + Face(line).DesiredSize.Height + 8 + 1;
        target.ShouldBeGreaterThan(before + 20);
        line.Bounds.Height.ShouldBeLessThan(target);
        rig.Frames(150);
        rig.Drain(() => swap.GetVisualChildren().Count() == 1).ShouldBeTrue();
        incoming.Opacity.ShouldBe(1, 0.001);
        rig.Frames(700);
        size.IsMoving.ShouldBeFalse();
        line.Bounds.Height.ShouldBe(target, 0.6);
    }

    [AvaloniaFact]
    public void Reduced_Crossfades_In_120ms_And_Jumps_The_Height()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new AiReplyRig(Line(AiRunStatusKind.Running), ArcMotionLevel.Reduced, clock, 420);
        var line = (AiRunStatusLine)rig.Content;
        Set(line, AiRunStatusKind.Failed);
        rig.Settle();
        var swap = TestHelpers.Descendant<ArcContentSwap>(line, "PART_Face");
        TestHelpers.Descendant<SpringSizeDecorator>(line, "PART_Size").IsMoving.ShouldBeFalse();
        line.Bounds.Height.ShouldBe(10 + Face(line).DesiredSize.Height + 8 + 1, 0.6);
        var incoming = swap.GetVisualChildren().OfType<ContentPresenter>().Single(p => ReferenceEquals(p.Content, Face(line)));
        rig.Frames(50);
        incoming.Opacity.ShouldBeInRange(0.2, 0.8);
        rig.Frames(100);
        rig.Drain(() => swap.GetVisualChildren().Count() == 1).ShouldBeTrue();
        incoming.Opacity.ShouldBe(1, 0.001);
    }

    [AvaloniaFact]
    public void Off_Jumps()
    {
        using var rig = new AiReplyRig(Line(AiRunStatusKind.Running), width: 420);
        var line = (AiRunStatusLine)rig.Content;
        Set(line, AiRunStatusKind.Stopped);
        rig.Settle();
        var swap = TestHelpers.Descendant<ArcContentSwap>(line, "PART_Face");
        swap.GetVisualChildren().Count().ShouldBe(1);
        ((ContentPresenter)swap.GetVisualChildren().Single()).Opacity.ShouldBe(1);
        TestHelpers.Descendant<SpringSizeDecorator>(line, "PART_Size").IsMoving.ShouldBeFalse();
        line.Bounds.Height.ShouldBe(10 + Face(line).DesiredSize.Height + 8 + 1, 0.6);
        line.Bounds.Height.ShouldBeGreaterThan(49 + 15);
    }

    [AvaloniaFact]
    public void The_Edge_Shows_After_The_Form_Scrolled()
    {
        using var rig = new AiReplyRig(Line(AiRunStatusKind.NeedsCheck));
        var line = (AiRunStatusLine)rig.Content;
        TestHelpers.ColorOf(line.BorderBrush).A.ShouldBe((byte)0);
        line.Classes.ShouldNotContain(AiRunStatusLine.EdgePseudoClass);
        line.ShowsEdge = true;
        rig.Settle();
        line.Classes.ShouldContain(AiRunStatusLine.EdgePseudoClass);
        TestHelpers.ColorOf(line.BorderBrush).ShouldBe(ArcTokens.GetColor(line, ArcDesignTokenKind.BorderSubtle));
        // The edge is drawn by the frame's bottom border.
        TestHelpers.Descendant<Border>(line, "PART_Frame").BorderThickness.ShouldBe(new Thickness(0, 0, 0, 1));
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_The_Edge_In_160ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new AiReplyRig(Line(AiRunStatusKind.NeedsCheck), ArcMotionLevel.Full, clock);
        var line = (AiRunStatusLine)rig.Content;
        line.Transitions.ShouldNotBeNull();
        line.Transitions!.OfType<ArcBrushTransition>().Single().Property.ShouldBe(TemplatedControl.BorderBrushProperty);
    }

    [AvaloniaFact]
    public void Reads_As_Its_Text_And_A_Failure_Is_An_Alert()
    {
        using var rig = new AiReplyRig(Line(AiRunStatusKind.Running));
        var line = (AiRunStatusLine)rig.Content;
        var peer = ControlAutomationPeer.CreatePeerForElement(line);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        peer.GetName().ShouldBe("Extracting fields 3 of 10");
        AutomationProperties.GetItemStatus(line).ShouldBe("Busy");
        AutomationProperties.GetLiveSetting(line).ShouldBe(AutomationLiveSetting.Off);
        peer.GetChildren().ShouldBeEmpty();

        Set(line, AiRunStatusKind.Failed);
        rig.Settle();
        peer.GetName().ShouldBe("Extraction failed. " + Detail);
        AutomationProperties.GetLiveSetting(line).ShouldBe(AutomationLiveSetting.Assertive);
        AutomationProperties.GetItemStatus(line).ShouldBeNull();

        Set(line, AiRunStatusKind.NeedsCheck);
        rig.Settle();
        peer.GetName().ShouldBe("2 fields need a check");
        AutomationProperties.GetLiveSetting(line).ShouldBe(AutomationLiveSetting.Off);
        // The pill is the line's one child, a button named by its word.
        var child = peer.GetChildren().ShouldHaveSingleItem();
        child.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        child.GetName().ShouldBe("Review");
    }

    [AvaloniaFact]
    public void Without_An_Action_Word_No_Pill_Shows()
    {
        var line = Line(AiRunStatusKind.NeedsCheck);
        line.ActionText = null;
        using var rig = new AiReplyRig(line);
        line.ShowsAction.ShouldBeFalse();
        TestHelpers.Descendant<PresenceHost>(line, "PART_ActionPresence").IsPresent.ShouldBeFalse();
        line.ActionText = "Review";
        rig.Settle();
        line.ShowsAction.ShouldBeTrue();
    }
}
