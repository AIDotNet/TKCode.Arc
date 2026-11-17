using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ArenaIdentity"/>: the blind name, the reveal (mark and name), hiding again, the privacy walk over the visual,
/// logical and automation trees, same-turn model changes, the width spring and the motion levels.
/// </summary>
public class ArenaIdentityTests
{
    private static ArenaIdentityFace Face(ArenaIdentity identity) => identity.Face.ShouldNotBeNull();

    private static TextBlock Text(ArenaIdentityFace face) => TestHelpers.Descendant<TextBlock>(face, "PART_Text");

    /// <summary>
    /// Everything the trees hold that could name or show a model: texts, marks, automation names and help texts, tooltips,
    /// across the visual and logical descendants of <paramref name="root"/> and its automation peers.
    /// </summary>
    private static List<string> Traces(Control root)
    {
        var found = new List<string>();
        var visuals = root.GetVisualDescendants().Prepend(root).ToList();
        var logicals = root.GetLogicalDescendants().OfType<Visual>().ToList();
        foreach (var visual in visuals.Concat(logicals).Distinct())
        {
            switch (visual)
            {
                case TextBlock text when !string.IsNullOrEmpty(text.Text):
                    found.Add("text:" + text.Text);
                    break;
                case ModelMark { Mark: not null } mark:
                    found.Add("mark:" + mark.Mark);
                    break;
                case ArenaIdentityFace face:
                    found.Add("face:" + face.Text + (face.Mark is null ? "" : "+mark"));
                    break;
                case ContentPresenter { Content: string content }:
                    found.Add("content:" + content);
                    break;
            }
            if (visual is Control control)
            {
                if (AutomationProperties.GetName(control) is { Length: > 0 } name)
                {
                    found.Add("name:" + name);
                }
                if (AutomationProperties.GetHelpText(control) is { Length: > 0 } help)
                {
                    found.Add("help:" + help);
                }
                if (ToolTip.GetTip(control) is { } tip)
                {
                    found.Add("tip:" + tip);
                }
            }
        }
        var peer = ControlAutomationPeer.CreatePeerForElement(root);
        found.Add("peer:" + peer.GetName());
        foreach (var child in peer.GetChildren())
        {
            found.Add("peer-child:" + child.GetName());
        }
        return found;
    }

    private static void ShouldNotLeak(Control root, AiModel model)
    {
        foreach (var trace in Traces(root))
        {
            trace.ShouldNotContain(model.Name, Case.Insensitive);
            if (!string.IsNullOrEmpty(model.ShortName))
            {
                trace.ShouldNotContain(model.ShortName, Case.Insensitive);
            }
        }
        // No mark of the model and no face that holds it, visible or not.
        var visuals = root.GetVisualDescendants().Concat(root.GetLogicalDescendants().OfType<Visual>()).ToList();
        visuals.OfType<ModelMark>().ShouldAllBe(m => !ReferenceEquals(m.Mark, model.Mark));
        visuals.OfType<ArenaIdentityFace>().ShouldAllBe(f => !ReferenceEquals(f.Model, model));
    }

    [AvaloniaFact]
    public void Blind_Shows_The_Blind_Name_In_14_Medium_Foreground()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", ArenaStatusSamples.Lumen()));
        var identity = (ArenaIdentity)rig.Content;
        var face = Face(identity);
        face.ShowsModel.ShouldBeFalse();
        Text(face).Text.ShouldBe("Model A");
        identity.FontSize.ShouldBe(14);
        identity.FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(identity.Foreground).ShouldBe(ArcTokens.GetColor(identity, ArcDesignTokenKind.Foreground));
        TestHelpers.Descendant<ModelMark>(face, "PART_Mark").IsVisible.ShouldBeFalse();
        identity.Classes.ShouldNotContain(ArenaIdentity.RevealedPseudoClass);
        AutomationProperties.GetName(identity).ShouldBe("Model A");
        identity.ShowsModel.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Revealed_Shows_A_16px_Mark_8px_Before_The_Name()
    {
        var model = ArenaStatusSamples.Lumen();
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", model, revealed: true));
        var identity = (ArenaIdentity)rig.Content;
        var face = Face(identity);
        face.ShowsModel.ShouldBeTrue();
        var mark = TestHelpers.Descendant<ModelMark>(face, "PART_Mark");
        mark.IsVisible.ShouldBeTrue();
        mark.Size.ShouldBe(16);
        mark.Mark.ShouldBeSameAs(model.Mark);
        mark.MarkColor.ShouldBeSameAs(model.MarkColor);
        var text = Text(face);
        text.Text.ShouldBe("Lumen 3 Pro");
        (text.TranslatePoint(default, mark)!.Value.X - mark.Bounds.Width).ShouldBe(8, 0.01);
        identity.Classes.ShouldContain(ArenaIdentity.RevealedPseudoClass);
        AutomationProperties.GetName(identity).ShouldBe("Model A, Lumen 3 Pro");
        ControlAutomationPeer.CreatePeerForElement(identity).GetName().ShouldBe("Model A, Lumen 3 Pro");
    }

    [AvaloniaFact]
    public void A_Model_Without_A_Mark_Shows_Its_Name_Alone()
    {
        var model = ArenaStatusSamples.Kite();
        model.Mark = null;
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("B", model, revealed: true));
        var face = Face((ArenaIdentity)rig.Content);
        TestHelpers.Descendant<ModelMark>(face, "PART_Mark").IsVisible.ShouldBeFalse();
        Text(face).Text.ShouldBe("Kite 5");
    }

    [AvaloniaFact]
    public void Without_A_Revealed_Name_The_Accessible_Name_Is_The_Model_Name()
    {
        var identity = new ArenaIdentity { BlindText = "Model A", Model = ArenaStatusSamples.Lumen(), IsRevealed = true };
        using var rig = new AiReplyRig(identity);
        AutomationProperties.GetName(identity).ShouldBe("Lumen 3 Pro");
        identity.RevealedAutomationName = "Model A, Lumen 3 Pro";
        AutomationProperties.GetName(identity).ShouldBe("Model A, Lumen 3 Pro");
    }

    [AvaloniaFact]
    public void Short_Name_Shows_The_Display_Name()
    {
        var identity = ArenaStatusSamples.Identity("A", ArenaStatusSamples.Lumen(), revealed: true);
        identity.ShowsShortName = true;
        using var rig = new AiReplyRig(identity);
        Text(Face(identity)).Text.ShouldBe("Lumen Pro");
        var kite = ArenaStatusSamples.Kite();
        identity.Model = kite;
        rig.Settle();
        Text(Face(identity)).Text.ShouldBe("Kite 5");
    }

    [AvaloniaFact]
    public void Blind_Trees_Carry_No_Trace_Of_The_Model()
    {
        var model = ArenaStatusSamples.Lumen();
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", model));
        var identity = (ArenaIdentity)rig.Content;
        ShouldNotLeak(identity, model);
        // The blind name is there.
        Traces(identity).ShouldContain("text:Model A");
    }

    [AvaloniaFact]
    public void Reveal_Then_Hide_Again_Leaves_No_Trace()
    {
        var model = ArenaStatusSamples.Lumen();
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", model));
        var identity = (ArenaIdentity)rig.Content;
        identity.IsRevealed = true;
        rig.Settle();
        Text(Face(identity)).Text.ShouldBe("Lumen 3 Pro");
        Traces(identity).ShouldContain("text:Lumen 3 Pro");

        identity.IsRevealed = false;
        // Hiding takes effect at once, before any dispatcher job runs.
        identity.ShowsModel.ShouldBeFalse();
        AutomationProperties.GetName(identity).ShouldBe("Model A");
        ShouldNotLeak(identity, model);
        rig.Settle();
        ShouldNotLeak(identity, model);
        Text(Face(identity)).Text.ShouldBe("Model A");
    }

    [AvaloniaFact]
    public void Hiding_At_Full_Motion_Fades_The_Shown_Model_Out_Of_The_Automation_Tree()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var model = ArenaStatusSamples.Lumen();
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", model, revealed: true), ArcMotionLevel.Full, clock);
        var identity = (ArenaIdentity)rig.Content;
        var shown = Face(identity);
        identity.IsRevealed = false;
        rig.Settle();
        // The model that was shown fades out (100ms) but is no longer read: raw, under a peer without children.
        AutomationProperties.GetAccessibilityView(shown).ShouldBe(AccessibilityView.Raw);
        AutomationProperties.GetName(identity).ShouldBe("Model A");
        var peer = ControlAutomationPeer.CreatePeerForElement(identity);
        peer.GetName().ShouldBe("Model A");
        peer.GetChildren().ShouldBeEmpty();
        Text(Face(identity)).Text.ShouldBe("Model A");
        rig.Frames(150);
        var swap = TestHelpers.Descendant<ArcContentSwap>(identity, "PART_Face");
        rig.Drain(() => swap.GetVisualChildren().Count() == 1).ShouldBeTrue();
        ShouldNotLeak(identity, model);
        rig.Frames(600);
        identity.Bounds.Width.ShouldBe(Face(identity).DesiredSize.Width, 0.6);
    }

    [AvaloniaFact]
    public void A_New_Model_And_A_Hide_In_One_Turn_Never_Show_The_New_Model()
    {
        var first = ArenaStatusSamples.Lumen();
        var next = ArenaStatusSamples.Kite();
        // Both orders: the next round's model first, then the hide; and the hide first.
        foreach (var modelFirst in new[] { true, false })
        {
            var identity = ArenaStatusSamples.Identity("A", first, revealed: true);
            using var rig = new AiReplyRig(identity, ArcMotionLevel.Full);
            Text(Face(identity)).Text.ShouldBe("Lumen 3 Pro");
            var seen = new List<string?>();
            identity.PropertyChanged += (_, _) => seen.Add(identity.Face?.Text);
            if (modelFirst)
            {
                identity.Model = next;
                identity.IsRevealed = false;
            }
            else
            {
                identity.IsRevealed = false;
                identity.Model = next;
            }
            ShouldNotLeak(identity, next);
            rig.Settle();
            rig.Frames(300);
            ShouldNotLeak(identity, next);
            seen.ShouldNotContain("Kite 5");
            Text(Face(identity)).Text.ShouldBe("Model A");
        }
    }

    [AvaloniaFact]
    public void Changing_The_Model_While_Hidden_Changes_Nothing_On_Screen()
    {
        var identity = ArenaStatusSamples.Identity("A", ArenaStatusSamples.Lumen());
        using var rig = new AiReplyRig(identity);
        var face = Face(identity);
        var next = ArenaStatusSamples.Kite();
        identity.Model = next;
        rig.Settle();
        Face(identity).ShouldBeSameAs(face);
        ShouldNotLeak(identity, next);
    }

    [AvaloniaFact]
    public void Revealed_Follows_A_Renamed_Model_In_Place_And_Another_Model_With_A_Swap()
    {
        var model = ArenaStatusSamples.Lumen();
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", model, revealed: true));
        var identity = (ArenaIdentity)rig.Content;
        var face = Face(identity);
        model.Name = "Lumen 3.1 Pro";
        rig.Settle();
        Face(identity).ShouldBeSameAs(face);
        Text(face).Text.ShouldBe("Lumen 3.1 Pro");

        identity.Model = ArenaStatusSamples.Kite();
        // A new model waits for the end of the dispatcher turn.
        Face(identity).ShouldBeSameAs(face);
        rig.Settle();
        Face(identity).ShouldNotBeSameAs(face);
        Text(Face(identity)).Text.ShouldBe("Kite 5");
    }

    [AvaloniaFact]
    public void Blind_Name_Changes_In_Place()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A"));
        var identity = (ArenaIdentity)rig.Content;
        var face = Face(identity);
        identity.BlindText = "Answer 1";
        rig.Settle();
        Face(identity).ShouldBeSameAs(face);
        Text(face).Text.ShouldBe("Answer 1");
        AutomationProperties.GetName(identity).ShouldBe("Answer 1");
    }

    [AvaloniaFact]
    public void A_Long_Name_Ends_In_An_Ellipsis_In_A_Narrow_Slot()
    {
        var identity = ArenaStatusSamples.Identity("A", ArenaStatusSamples.Lumen(), revealed: true);
        using var rig = new AiReplyRig(new Border { Width = 70, Child = identity, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
        identity.Bounds.Width.ShouldBeLessThanOrEqualTo(70.01);
        var text = Text(Face(identity));
        text.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
        (text.Bounds.Width + 24).ShouldBeLessThanOrEqualTo(70.01);
    }

    [AvaloniaFact]
    public void Full_Motion_Swaps_And_Springs_The_Width_To_The_Revealed_Name()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", ArenaStatusSamples.Lumen()), ArcMotionLevel.Full, clock);
        var identity = (ArenaIdentity)rig.Content;
        var blindWidth = identity.Bounds.Width;
        var blindFace = Face(identity);
        identity.IsRevealed = true;
        rig.Settle();
        var swap = TestHelpers.Descendant<ArcContentSwap>(identity, "PART_Face");
        var size = TestHelpers.Descendant<SpringSizeDecorator>(identity, "PART_Size");
        // Both faces are there: the blind one leaving (out of layout), the revealed one arriving.
        swap.GetVisualChildren().Count().ShouldBe(2);
        swap.DesiredSize.Width.ShouldBe(Face(identity).DesiredSize.Width, 0.01);
        rig.Frames(50);
        size.IsMoving.ShouldBeTrue();
        identity.Bounds.Width.ShouldBeGreaterThan(blindWidth);
        identity.Bounds.Width.ShouldBeLessThan(Face(identity).DesiredSize.Width);
        rig.Frames(150);
        // The blind face is gone after its 100ms.
        rig.Drain(() => swap.GetVisualChildren().Count() == 1).ShouldBeTrue();
        TopLevel.GetTopLevel(blindFace).ShouldBeNull();
        rig.Frames(600);
        size.IsMoving.ShouldBeFalse();
        identity.Bounds.Width.ShouldBe(Face(identity).DesiredSize.Width, 0.6);
        size.Clip.ShouldBeNull();
    }

    [AvaloniaFact]
    public void In_A_Pane_Head_The_Revealed_Name_Settles_Unclipped()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var identity = ArenaStatusSamples.Identity("A", ArenaStatusSamples.Lumen());
        var pill = ArenaStatusSamples.Pill(ArenaPaneStatus.Writing);
        using var rig = new AiReplyRig(ArenaStatusSamples.Pane(identity, pill, ArenaStatusSamples.Metrics()), ArcMotionLevel.Full, clock);
        pill.Status = ArenaPaneStatus.FinishedFirst;
        rig.Frames(600);
        identity.IsRevealed = true;
        rig.Settle();
        rig.Frames(800);
        var size = TestHelpers.Descendant<SpringSizeDecorator>(identity, "PART_Size");
        size.IsMoving.ShouldBeFalse();
        size.Clip.ShouldBeNull();
        identity.Bounds.Width.ShouldBe(Face(identity).DesiredSize.Width, 0.6);
        Text(Face(identity)).Bounds.Width.ShouldBe(Text(Face(identity)).DesiredSize.Width, 0.6);
    }

    [AvaloniaFact]
    public void Off_And_Reduced_Jump_The_Width()
    {
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Reduced })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            using var rig = new AiReplyRig(ArenaStatusSamples.Identity("A", ArenaStatusSamples.Lumen()), level, clock);
            var identity = (ArenaIdentity)rig.Content;
            identity.IsRevealed = true;
            rig.Settle();
            rig.Frames(17);
            TestHelpers.Descendant<SpringSizeDecorator>(identity, "PART_Size").IsMoving.ShouldBeFalse();
            identity.Bounds.Width.ShouldBe(Face(identity).DesiredSize.Width, 0.6);
            var swap = TestHelpers.Descendant<ArcContentSwap>(identity, "PART_Face");
            if (level == ArcMotionLevel.Off)
            {
                swap.GetVisualChildren().Count().ShouldBe(1);
            }
            else
            {
                // Reduced: a 120ms crossfade without a rise.
                var incoming = (ContentPresenter)swap.GetVisualChildren().Last();
                ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0);
                rig.Frames(150);
                rig.Drain(() => swap.GetVisualChildren().Count() == 1).ShouldBeTrue();
            }
        }
    }
}
