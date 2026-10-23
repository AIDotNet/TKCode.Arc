using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiStatusNote"/>: the stopped line, the error box (token layers, padding, title and reason, the Retry action) and
/// the inline error, the action event, the accessible name, and appearing and leaving at each motion level.
/// </summary>
public class AiStatusNoteTests
{
    private static AiStatusNote ErrorCard() => new()
    {
        Tone = AiStatusNoteTone.Error,
        Layout = AiStatusNoteLayout.Card,
        Title = "Reply failed.",
        Text = "ci-runner-7 did not answer within 8 seconds.",
        ActionText = "Retry"
    };

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    [AvaloniaFact]
    public void Stopped_Is_A_Muted_Line_After_An_11px_Square()
    {
        using var rig = new AiReplyRig(new AiStatusNote { Tone = AiStatusNoteTone.Stopped, Text = "Stopped" });
        var note = (AiStatusNote)rig.Content;
        var square = TestHelpers.Descendant<Border>(note, "PART_Square");
        square.IsVisible.ShouldBeTrue();
        square.Bounds.Size.ShouldBe(new Size(11, 11));
        TestHelpers.ColorOf(square.Background).ShouldBe(Token(note, ArcDesignTokenKind.TextMuted));
        TestHelpers.Descendant<ArcGlyph>(note, "PART_Alert").IsVisible.ShouldBeFalse();
        var text = TestHelpers.Descendant<TextBlock>(note, "PART_Text");
        TestHelpers.ColorOf(note.Foreground).ShouldBe(Token(note, ArcDesignTokenKind.TextMuted));
        text.FontSize.ShouldBe(14);
        text.LineHeight.ShouldBe(19.6);
        // 8px between the square and the text; no box, no action.
        text.TranslatePoint(default, note)!.Value.X.ShouldBe(11 + 8, 0.01);
        TestHelpers.Descendant<Border>(note, "PART_Fill").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Border>(note, "PART_Edge").IsVisible.ShouldBeFalse();
        note.ActionButton.ShouldNotBeNull().IsVisible.ShouldBeFalse();
        note.ReadText.ShouldBe("Stopped");
        AutomationProperties.GetName(note).ShouldBe("Stopped");
    }

    [AvaloniaFact]
    public void Error_Card_Mixes_Danger_Into_Surface_And_Border_With_Layers()
    {
        using var rig = new AiReplyRig(ErrorCard());
        var note = (AiStatusNote)rig.Content;
        var fill = TestHelpers.Descendant<Border>(note, "PART_Fill");
        var tint = TestHelpers.Descendant<Border>(note, "PART_Tint");
        var edge = TestHelpers.Descendant<Border>(note, "PART_Edge");
        var dangerEdge = TestHelpers.Descendant<Border>(note, "PART_DangerEdge");
        foreach (var layer in new[] { fill, tint, edge, dangerEdge })
        {
            layer.IsVisible.ShouldBeTrue();
            layer.CornerRadius.ShouldBe(new CornerRadius(16));
        }
        TestHelpers.ColorOf(fill.Background).ShouldBe(Token(note, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(tint.Background).ShouldBe(Token(note, ArcDesignTokenKind.Danger));
        tint.Opacity.ShouldBe(0.05);
        TestHelpers.ColorOf(edge.BorderBrush).ShouldBe(Token(note, ArcDesignTokenKind.Border));
        TestHelpers.ColorOf(dangerEdge.BorderBrush).ShouldBe(Token(note, ArcDesignTokenKind.Danger));
        dangerEdge.Opacity.ShouldBe(0.26);
        dangerEdge.BorderThickness.ShouldBe(new Thickness(1));

        var alert = TestHelpers.Descendant<ArcGlyph>(note, "PART_Alert");
        alert.IsVisible.ShouldBeTrue();
        alert.Size.ShouldBe(16);
        TestHelpers.ColorOf(alert.Foreground).ShouldBe(Token(note, ArcDesignTokenKind.Danger));
        // Padding 8 8 8 14, parts 10px apart.
        alert.TranslatePoint(default, note)!.Value.X.ShouldBe(14, 0.01);
        var text = TestHelpers.Descendant<TextBlock>(note, "PART_Text");
        text.TranslatePoint(default, note)!.Value.X.ShouldBe(14 + 16 + 10, 0.01);
        text.LineHeight.ShouldBe(20.3);
        TestHelpers.ColorOf(note.Foreground).ShouldBe(Token(note, ArcDesignTokenKind.TextSecondary));
        var action = note.ActionButton.ShouldNotBeNull();
        action.IsVisible.ShouldBeTrue();
        action.Variant.ShouldBe(ButtonVariant.Secondary);
        action.Size.ShouldBe(ControlSize.Small);
        action.Content.ShouldBe("Retry");
        (action.TranslatePoint(default, note)!.Value.X + action.Bounds.Width).ShouldBe(note.Bounds.Width - 8, 0.01);
        // A row at least 36px high, 8px from the top and bottom, the action centered on it (headless text may wrap).
        note.Bounds.Height.ShouldBe(8 + Math.Max(36, text.Bounds.Height) + 8, 0.5);
        (action.TranslatePoint(default, note)!.Value.Y + action.Bounds.Height / 2).ShouldBe(note.Bounds.Height / 2, 0.5);
    }

    [AvaloniaFact]
    public void Title_Is_Medium_Foreground_Before_The_Reason()
    {
        using var rig = new AiReplyRig(ErrorCard());
        var note = (AiStatusNote)rig.Content;
        var runs = TestHelpers.Descendant<TextBlock>(note, "PART_Text").Inlines!.OfType<Run>().ToList();
        runs.Select(r => r.Text).ShouldBe(["Reply failed.", " ", "ci-runner-7 did not answer within 8 seconds."]);
        runs[0].FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(runs[0].Foreground).ShouldBe(Token(note, ArcDesignTokenKind.Foreground));
        AutomationProperties.GetName(note).ShouldBe("Reply failed. ci-runner-7 did not answer within 8 seconds.");
        AutomationProperties.GetName(note.ActionButton!).ShouldBe("Retry");

        note.Title = null;
        rig.Settle();
        runs.Select(r => r.Text).ShouldBe(["", "", "ci-runner-7 did not answer within 8 seconds."]);
    }

    [AvaloniaFact]
    public void Retry_Raises_ActionRequested_And_No_Action_Hides_The_Button()
    {
        var note = ErrorCard();
        var requests = 0;
        note.ActionRequested += (_, _) => requests++;
        using var rig = new AiReplyRig(note);
        rig.Click(note.ActionButton!);
        note.ActionButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        requests.ShouldBe(2);
        note.ActionText = null;
        rig.Settle();
        note.ActionButton!.IsVisible.ShouldBeFalse();
        note.Classes.ShouldNotContain(AiStatusNote.ActionPseudoClass);
    }

    [AvaloniaFact]
    public void Inline_Error_Has_The_Alert_And_No_Box()
    {
        using var rig = new AiReplyRig(new AiStatusNote { Tone = AiStatusNoteTone.Error, Title = "Reply failed.", Text = "The model is busy." });
        var note = (AiStatusNote)rig.Content;
        TestHelpers.Descendant<ArcGlyph>(note, "PART_Alert").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<Border>(note, "PART_Fill").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Border>(note, "PART_Tint").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<TextBlock>(note, "PART_Text").TranslatePoint(default, note)!.Value.X.ShouldBe(16 + 8, 0.01);
    }

    [AvaloniaFact]
    public void A_Stopped_Card_Keeps_The_Plain_Box()
    {
        using var rig = new AiReplyRig(new AiStatusNote { Tone = AiStatusNoteTone.Stopped, Layout = AiStatusNoteLayout.Card, Text = "Stopped" });
        var note = (AiStatusNote)rig.Content;
        TestHelpers.Descendant<Border>(note, "PART_Fill").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<Border>(note, "PART_Edge").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<Border>(note, "PART_Tint").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Border>(note, "PART_DangerEdge").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void IsActive_Off_Hides_It_At_Once_Without_Motion()
    {
        using var rig = new AiReplyRig(ErrorCard());
        var note = (AiStatusNote)rig.Content;
        note.Bounds.Height.ShouldBeGreaterThan(0);
        note.IsActive = false;
        rig.Settle();
        note.Bounds.Height.ShouldBe(0);
        note.IsActive = true;
        rig.Settle();
        note.Bounds.Height.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_In_And_Out()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var note = ErrorCard();
        note.IsActive = false;
        using var rig = new AiReplyRig(note, ArcMotionLevel.Full, clock);
        var presence = TestHelpers.Descendant<PresenceHost>(note, "PART_Presence");
        note.Bounds.Height.ShouldBe(0);
        note.IsActive = true;
        rig.Settle();
        rig.Frames(50);
        presence.Opacity.ShouldBeGreaterThan(0);
        presence.Opacity.ShouldBeLessThan(1);
        note.Bounds.Height.ShouldBeGreaterThan(0);
        rig.Frames(300);
        presence.Opacity.ShouldBe(1);
        note.IsActive = false;
        rig.Settle();
        rig.Frames(50);
        presence.Opacity.ShouldBeLessThan(1);
        note.Bounds.Height.ShouldBeGreaterThan(0);
        rig.Frames(300);
        note.Bounds.Height.ShouldBe(0);
    }
}
