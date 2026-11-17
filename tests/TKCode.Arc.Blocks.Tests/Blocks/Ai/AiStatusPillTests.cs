using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiStatusPill"/>: the six statuses and idle (icons, words, colors, pseudo-classes), the geometry of the normal and
/// compact pills, the word swap and the width spring at each motion level, the spinner and the shimmer, and automation.
/// </summary>
public class AiStatusPillTests
{
    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static AiStatusPillFace Face(AiStatusPill pill) => pill.Face.ShouldNotBeNull();

    private static readonly (ArenaPaneStatus Status, string Word, string PseudoClass)[] s_statuses =
    [
        (ArenaPaneStatus.Thinking, "Thinking", AiStatusPill.ThinkingPseudoClass),
        (ArenaPaneStatus.Writing, "Writing", AiStatusPill.WritingPseudoClass),
        (ArenaPaneStatus.Done, "Done", AiStatusPill.DonePseudoClass),
        (ArenaPaneStatus.FinishedFirst, "Finished first", AiStatusPill.FinishedFirstPseudoClass),
        (ArenaPaneStatus.Stopped, "Stopped", AiStatusPill.StoppedPseudoClass),
        (ArenaPaneStatus.Failed, "Failed", AiStatusPill.FailedPseudoClass)
    ];

    /// <summary>The icon parts a face can show, by name; exactly one is visible for a status.</summary>
    private static string VisibleIcon(AiStatusPillFace face)
    {
        var cell = TestHelpers.Descendant<Panel>(face, "PART_IconCell");
        return cell.Children.Single(c => c.IsVisible).Name!;
    }

    [AvaloniaFact]
    public void Every_Status_Has_Its_Word_Icon_And_Pseudo_Classes()
    {
        foreach (var (status, word, pseudoClass) in s_statuses)
        {
            using var rig = new AiReplyRig(ArenaStatusSamples.Pill(status));
            var pill = (AiStatusPill)rig.Content;
            pill.Label.ShouldBe(word);
            pill.Classes.ShouldContain(pseudoClass);
            pill.Classes.Count(c => c.StartsWith(':') && s_statuses.Any(s => s.PseudoClass == c)).ShouldBe(1);
            pill.Classes.ShouldNotContain(AiStatusPill.IdlePseudoClass);
            var face = Face(pill);
            face.Status.ShouldBe(status);
            face.Text.ShouldBe(word);
            face.Classes.ShouldContain(pseudoClass);
            VisibleIcon(face).ShouldBe(status switch
            {
                ArenaPaneStatus.Thinking or ArenaPaneStatus.Writing => "PART_Spinner",
                ArenaPaneStatus.Done => "PART_Check",
                ArenaPaneStatus.FinishedFirst => "PART_Flag",
                ArenaPaneStatus.Stopped => "PART_Square",
                _ => "PART_Alert"
            });
            var busy = status is ArenaPaneStatus.Thinking or ArenaPaneStatus.Writing;
            var shimmer = TestHelpers.Descendant<TextShimmer>(face, "PART_Shimmer");
            shimmer.IsVisible.ShouldBe(busy);
            shimmer.IsActive.ShouldBe(busy);
            var text = TestHelpers.Descendant<TextBlock>(face, "PART_Word");
            text.IsVisible.ShouldBe(!busy);
            (busy ? shimmer.Text : text.Text).ShouldBe(word);
            AutomationProperties.GetName(pill).ShouldBe(word);
            AutomationProperties.GetItemStatus(pill).ShouldBe(busy ? "Busy" : null);
        }
    }

    [AvaloniaFact]
    public void Colors_Follow_The_Status()
    {
        foreach (var (status, _, _) in s_statuses)
        {
            using var rig = new AiReplyRig(ArenaStatusSamples.Pill(status));
            var pill = (AiStatusPill)rig.Content;
            var face = Face(pill);
            var frame = TestHelpers.Descendant<Border>(pill, "PART_Frame");
            if (status == ArenaPaneStatus.FinishedFirst)
            {
                TestHelpers.ColorOf(frame.Background).ShouldBe(Token(pill, ArcDesignTokenKind.AccentSubtle));
                TestHelpers.ColorOf(face.Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.AccentStrong));
                TestHelpers.ColorOf(TestHelpers.Descendant<ArcGlyph>(face, "PART_Flag").Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.AccentStrong));
            }
            else
            {
                TestHelpers.ColorOf(frame.Background).A.ShouldBe((byte)0);
                TestHelpers.ColorOf(face.Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.TextSecondary));
            }
            TestHelpers.ColorOf(TestHelpers.Descendant<ArcGlyph>(face, "PART_Check").Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.Success));
            TestHelpers.ColorOf(TestHelpers.Descendant<ArcGlyph>(face, "PART_Alert").Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.Danger));
            TestHelpers.ColorOf(TestHelpers.Descendant<Border>(face, "PART_Square").Background).ShouldBe(TestHelpers.ColorOf(face.Foreground));
            TestHelpers.ColorOf(TestHelpers.Descendant<AiToolSpinner>(face, "PART_Spinner").Foreground).ShouldBe(TestHelpers.ColorOf(face.Foreground));
        }
    }

    [AvaloniaFact]
    public void Pill_Is_24_High_With_A_14px_Icon_6px_Before_The_Word_And_8px_Inside()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Done));
        var pill = (AiStatusPill)rig.Content;
        var face = Face(pill);
        pill.Bounds.Height.ShouldBe(24);
        pill.FontSize.ShouldBe(12);
        pill.CornerRadius.TopLeft.ShouldBeGreaterThanOrEqualTo(12);
        var cell = TestHelpers.Descendant<Panel>(face, "PART_IconCell");
        cell.Bounds.Width.ShouldBe(14);
        cell.Bounds.Height.ShouldBe(14);
        cell.TranslatePoint(default, pill)!.Value.X.ShouldBe(8, 0.01);
        TestHelpers.Descendant<ArcGlyph>(face, "PART_Check").Size.ShouldBe(14);
        TestHelpers.Descendant<ArcGlyph>(face, "PART_Check").StrokeThickness.ShouldBe(1.75);
        var word = TestHelpers.Descendant<TextBlock>(face, "PART_Word");
        word.TranslatePoint(default, pill)!.Value.X.ShouldBe(28, 0.01);
        pill.Bounds.Width.ShouldBe(8 + 14 + 6 + word.Bounds.Width + 8, 0.6);
        // The stopped square: about 8px, filled, centered in the cell.
        using var stopped = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Stopped));
        var square = TestHelpers.Descendant<Border>(Face((AiStatusPill)stopped.Content), "PART_Square");
        square.Bounds.Size.ShouldBe(new Size(8, 8));
        square.Bounds.X.ShouldBe(3, 0.01);
    }

    [AvaloniaFact]
    public void Compact_Pill_Is_20_High_With_A_12px_Icon_And_The_Short_Word()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.FinishedFirst, compact: true));
        var pill = (AiStatusPill)rig.Content;
        var face = Face(pill);
        pill.Classes.ShouldContain(AiStatusPill.CompactPseudoClass);
        face.Classes.ShouldContain(AiStatusPillFace.CompactPseudoClass);
        pill.Bounds.Height.ShouldBe(20);
        pill.Label.ShouldBe("First");
        face.Text.ShouldBe("First");
        var cell = TestHelpers.Descendant<Panel>(face, "PART_IconCell");
        cell.Bounds.Width.ShouldBe(12);
        cell.TranslatePoint(default, pill)!.Value.X.ShouldBe(7, 0.01);
        TestHelpers.Descendant<ArcGlyph>(face, "PART_Flag").Size.ShouldBe(12);
        var word = TestHelpers.Descendant<TextBlock>(face, "PART_Word");
        pill.Bounds.Width.ShouldBe(7 + 12 + 6 + word.Bounds.Width + 7, 0.6);

        // Back to the full pill: the long word, at once (a change of layout, not of status).
        pill.IsCompact = false;
        rig.Settle();
        pill.Label.ShouldBe("Finished first");
        pill.Bounds.Height.ShouldBe(24);
        TestHelpers.Descendant<ArcContentSwap>(pill, "PART_Face").GetVisualChildren().Count().ShouldBe(1);
    }

    [AvaloniaFact]
    public void Compact_Without_A_Short_Word_Uses_The_Long_One()
    {
        var pill = ArenaStatusSamples.Pill(ArenaPaneStatus.FinishedFirst, compact: true);
        pill.FinishedFirstShortText = null;
        using var rig = new AiReplyRig(pill);
        pill.Label.ShouldBe("Finished first");
    }

    [AvaloniaFact]
    public void Idle_Shows_Nothing_And_Leaves_The_Automation_Tree()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill());
        var pill = (AiStatusPill)rig.Content;
        pill.Classes.ShouldContain(AiStatusPill.IdlePseudoClass);
        pill.Face.ShouldBeNull();
        pill.Label.ShouldBe(string.Empty);
        pill.Bounds.Width.ShouldBe(0);
        var peer = ControlAutomationPeer.CreatePeerForElement(pill);
        peer.IsContentElement().ShouldBeFalse();
        peer.IsControlElement().ShouldBeFalse();

        pill.Status = ArenaPaneStatus.Thinking;
        rig.Settle();
        pill.Label.ShouldBe("Thinking");
        pill.Bounds.Width.ShouldBeGreaterThan(60);
        peer.IsContentElement().ShouldBeTrue();
        peer.GetName().ShouldBe("Thinking");
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        peer.GetChildren().ShouldBeEmpty();

        pill.Status = ArenaPaneStatus.Idle;
        rig.Settle();
        pill.Face.ShouldBeNull();
        pill.Bounds.Width.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_New_Word_From_The_Owner_Replaces_The_Old_One()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Done));
        var pill = (AiStatusPill)rig.Content;
        pill.DoneText = "Complete";
        rig.Settle();
        pill.Label.ShouldBe("Complete");
        Face(pill).Text.ShouldBe("Complete");
        AutomationProperties.GetName(pill).ShouldBe("Complete");
        // A word for another status changes nothing on screen.
        var face = Face(pill);
        pill.FailedText = "Error";
        rig.Settle();
        Face(pill).ShouldBeSameAs(face);
    }

    [AvaloniaFact]
    public void Every_Word_Is_A_New_Face_So_The_Shimmer_Starts_Over()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Thinking), ArcMotionLevel.Full, clock);
        var pill = (AiStatusPill)rig.Content;
        var thinking = TestHelpers.Descendant<TextShimmer>(Face(pill), "PART_Shimmer");
        rig.Frames(900);
        thinking.Sweep.ShouldBeGreaterThan(0.2);
        pill.Status = ArenaPaneStatus.Writing;
        rig.Settle();
        var writing = TestHelpers.Descendant<TextShimmer>(Face(pill), "PART_Shimmer");
        writing.ShouldNotBeSameAs(thinking);
        writing.Sweep.ShouldBe(0);
        writing.IsSweeping.ShouldBeTrue();
        // About 150ms of rest, then the band starts from the left.
        rig.Frames(100);
        writing.Sweep.ShouldBe(0);
        rig.Frames(300);
        writing.Sweep.ShouldBeGreaterThan(0);
        writing.Sweep.ShouldBeLessThan(0.3);
    }

    [AvaloniaFact]
    public void Spinner_Turns_At_Full_Motion_Only()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using (var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Writing), ArcMotionLevel.Full, clock))
        {
            var spinner = TestHelpers.Descendant<AiToolSpinner>(rig.Content, "PART_Spinner");
            spinner.IsSpinning.ShouldBeTrue();
            var before = spinner.Angle;
            rig.Frames(450);
            ((spinner.Angle - before + 360) % 360).ShouldBe(180, 12);
        }
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Thinking), level, clock);
            var spinner = TestHelpers.Descendant<AiToolSpinner>(rig.Content, "PART_Spinner");
            rig.Frames(200);
            spinner.IsSpinning.ShouldBeFalse();
            spinner.Angle.ShouldBe(0);
            var shimmer = TestHelpers.Descendant<TextShimmer>(rig.Content, "PART_Shimmer");
            shimmer.IsSweeping.ShouldBeFalse();
            shimmer.Sweep.ShouldBe(0);
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_The_Old_Word_Out_Of_Layout_And_Springs_The_Width()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Writing), ArcMotionLevel.Full, clock);
        var pill = (AiStatusPill)rig.Content;
        var before = pill.Bounds.Width;
        var old = Face(pill);
        pill.Status = ArenaPaneStatus.FinishedFirst;
        rig.Settle();
        var swap = TestHelpers.Descendant<ArcContentSwap>(pill, "PART_Face");
        var size = TestHelpers.Descendant<SpringSizeDecorator>(pill, "PART_Size");
        var presenters = swap.GetVisualChildren().OfType<ContentPresenter>().ToList();
        presenters.Count.ShouldBe(2);
        // The old word is out of layout: only the new face sizes the swap.
        swap.DesiredSize.Width.ShouldBe(Face(pill).DesiredSize.Width, 0.01);
        var incoming = presenters.Single(p => ReferenceEquals(p.Content, Face(pill)));
        var outgoing = presenters.Single(p => ReferenceEquals(p.Content, old));
        rig.Frames(17);
        // The new word rises from 2px and fades in; the old one fades without moving.
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBeInRange(0.05, 2);
        incoming.Opacity.ShouldBeInRange(0.01, 0.9);
        ((TranslateTransform)outgoing.RenderTransform!).Y.ShouldBe(0);
        outgoing.Opacity.ShouldBeLessThan(1);
        size.IsMoving.ShouldBeTrue();
        pill.Bounds.Width.ShouldBeGreaterThan(before);
        pill.Bounds.Width.ShouldBeLessThan(Face(pill).DesiredSize.Width);
        // Clipped at the moving edge while it grows (and only then).
        size.Clip.ShouldNotBeNull();
        size.ClipOverflow.ShouldBe(0);
        size.ClipToBounds.ShouldBeFalse();
        rig.Frames(150);
        rig.Drain(() => swap.GetVisualChildren().Count() == 1).ShouldBeTrue();
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0, 0.01);
        incoming.Opacity.ShouldBe(1, 0.001);
        rig.Frames(600);
        size.IsMoving.ShouldBeFalse();
        size.Clip.ShouldBeNull();
        pill.Bounds.Width.ShouldBe(Face(pill).DesiredSize.Width, 0.6);
    }

    [AvaloniaFact]
    public void Reduced_Crossfades_In_120ms_Without_A_Rise_And_Jumps_The_Width()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Thinking), ArcMotionLevel.Reduced, clock);
        var pill = (AiStatusPill)rig.Content;
        pill.Status = ArenaPaneStatus.Done;
        rig.Settle();
        var swap = TestHelpers.Descendant<ArcContentSwap>(pill, "PART_Face");
        var size = TestHelpers.Descendant<SpringSizeDecorator>(pill, "PART_Size");
        size.IsMoving.ShouldBeFalse();
        // The width jumped: a longer word that leaves is cut at the pill's edge while it fades.
        size.ClipToBounds.ShouldBeTrue();
        pill.Bounds.Width.ShouldBe(Face(pill).DesiredSize.Width, 0.6);
        var incoming = swap.GetVisualChildren().OfType<ContentPresenter>().Single(p => ReferenceEquals(p.Content, Face(pill)));
        rig.Frames(50);
        ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0);
        incoming.Opacity.ShouldBeInRange(0.2, 0.8);
        rig.Frames(100);
        rig.Drain(() => swap.GetVisualChildren().Count() == 1).ShouldBeTrue();
        incoming.Opacity.ShouldBe(1, 0.001);
    }

    [AvaloniaFact]
    public void Off_Jumps()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Pill(ArenaPaneStatus.Thinking));
        var pill = (AiStatusPill)rig.Content;
        pill.Status = ArenaPaneStatus.Failed;
        rig.Settle();
        var swap = TestHelpers.Descendant<ArcContentSwap>(pill, "PART_Face");
        swap.GetVisualChildren().Count().ShouldBe(1);
        ((ContentPresenter)swap.GetVisualChildren().Single()).Opacity.ShouldBe(1);
        TestHelpers.Descendant<SpringSizeDecorator>(pill, "PART_Size").IsMoving.ShouldBeFalse();
        pill.Bounds.Width.ShouldBe(Face(pill).DesiredSize.Width, 0.6);
    }
}
