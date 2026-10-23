using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ExtractionFieldRow"/> and its value box: the geometry (36 high, 88 | 1fr | 60, narrow 76 | 1fr | 54), the input
/// states of spec 3.4 (empty, empty under the pointer, filled, linked, focused, low confidence, invalid) and which wins,
/// the landing of a model's value, the reason opening and closing on the smooth spring, "Looks right", the person's typing
/// written to the value, the routed events, the tab order and what screen readers get. Values come from the invoice of
/// <see cref="ExtractionFixtures"/>.
/// </summary>
public class ExtractionFieldRowTests
{
    private const double Frame = 1.0 / 60;
    private const string DueNote = "The due date is not printed; it is the issue date plus 14 days.";

    private static ExtractionFieldRow Row(ExtractionValue? value = null, ExtractionFieldType type = ExtractionFieldType.Date, int tone = 1) => new()
    {
        Value = value ?? new ExtractionValue("dueDate"),
        Label = "Due date",
        Tone = tone,
        FieldType = type,
        TypeName = ExtractionFieldSamples.Labels.TypeName(type),
        EditedText = "Edited",
        LooksRightText = "Looks right",
        AutomationName = "Due date",
        ConfidenceAutomationText = "No value yet",
        Width = 410
    };

    /// <summary>The model's due date of the invoice: 0.66 with its note (below the 0.8 threshold).</summary>
    private static void Land(ExtractionValue value, string text = "2026-09-17", double confidence = 0.66, string? note = DueNote)
    {
        value.Text = text;
        value.Confidence = confidence;
        value.Note = note;
        value.Status = ExtractionValueStatus.Filled;
        value.NeedsCheck = confidence < 0.8;
    }

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static Color BackgroundOf(ExtractionInput input) => TestHelpers.ColorOf(input.Background);

    private static Color BorderOf(ExtractionInput input) => TestHelpers.ColorOf(input.BorderBrush);

    private static Rect BoundsIn(Visual visual, Visual relativeTo) =>
        new(visual.TranslatePoint(default, relativeTo)!.Value, visual.Bounds.Size);

    [AvaloniaFact]
    public void The_Row_Is_36_High_On_A_Grid_Of_88_1fr_60()
    {
        using var rig = new ExtractionFieldRig(Row());
        var row = (ExtractionFieldRow)rig.Content;
        row.Bounds.Height.ShouldBe(36, 0.01);
        row.CornerRadius.ShouldBe(new CornerRadius(12));
        var label = TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.LabelPart);
        BoundsIn(label, row).X.ShouldBe(8, 0.01);
        label.Bounds.Width.ShouldBe(88, 0.01);
        label.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(label.Foreground).ShouldBe(Token(row, ArcDesignTokenKind.TextSecondary));
        var input = row.Input.ShouldNotBeNull();
        var box = BoundsIn(input, row);
        box.X.ShouldBe(8 + 88 + 8, 0.01);
        box.Y.ShouldBe(2, 0.01);
        box.Height.ShouldBe(32, 0.01);
        input.CornerRadius.ShouldBe(new CornerRadius(9));
        input.Padding.ShouldBe(new Thickness(8, 0));
        input.BorderThickness.ShouldBe(new Thickness(1));
        input.FontSize.ShouldBe(14);
        var confidence = TestHelpers.Descendant<ConfidenceIndicator>(row, ExtractionFieldRow.ConfidencePart);
        var column = BoundsIn(confidence, row);
        column.Width.ShouldBe(60, 0.01);
        column.Right.ShouldBe(410 - 4, 0.01);
        box.Right.ShouldBe(column.X - 8, 0.01);
        confidence.Text.ShouldBe("Date");
    }

    [AvaloniaFact]
    public void Narrow_Rows_Are_76_1fr_54()
    {
        var row = Row();
        row.IsNarrow = true;
        row.Width = 332;
        using var rig = new ExtractionFieldRig(row);
        row.Classes.ShouldContain(ExtractionFieldRow.NarrowPseudoClass);
        TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.LabelPart).Bounds.Width.ShouldBe(76, 0.01);
        TestHelpers.Descendant<ConfidenceIndicator>(row, ExtractionFieldRow.ConfidencePart).Bounds.Width.ShouldBe(54, 0.01);
        BoundsIn(row.Input!, row).X.ShouldBe(8 + 76 + 8, 0.01);
        BoundsIn(row.Input!, row).Width.ShouldBe(332 - 8 - 4 - 76 - 54 - 16, 0.01);
    }

    [AvaloniaFact]
    public void An_Empty_Box_Is_Four_Percent_Foreground_And_Shows_Its_Border_Under_The_Pointer()
    {
        using var rig = new ExtractionFieldRig(Row());
        var row = (ExtractionFieldRow)rig.Content;
        var input = row.Input!;
        var foreground = Token(row, ArcDesignTokenKind.Foreground);
        input.Look.ShouldBe(ExtractionInputLook.Empty);
        input.Classes.ShouldContain(ExtractionInput.EmptyPseudoClass);
        BackgroundOf(input).ShouldBe(Oklch.WithAlpha(foreground, foreground.A / 255.0 * 0.04));
        BorderOf(input).A.ShouldBe((byte)0);
        rig.Hover(input);
        input.Look.ShouldBe(ExtractionInputLook.EmptyHover);
        BackgroundOf(input).ShouldBe(Oklch.WithAlpha(foreground, foreground.A / 255.0 * 0.04));
        BorderOf(input).ShouldBe(Token(row, ArcDesignTokenKind.Border));
        rig.Leave();
        input.Look.ShouldBe(ExtractionInputLook.Empty);
        BorderOf(input).A.ShouldBe((byte)0);
    }

    [AvaloniaFact]
    public void A_Filled_Box_Reads_Like_Text()
    {
        var value = new ExtractionValue("vendor");
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text));
        var input = ((ExtractionFieldRow)rig.Content).Input!;
        Land(value, "Oakridge Ceramics Ltd", 0.99, null);
        rig.Settle();
        input.Text.ShouldBe("Oakridge Ceramics Ltd");
        input.Look.ShouldBe(ExtractionInputLook.Filled);
        input.Classes.ShouldNotContain(ExtractionInput.EmptyPseudoClass);
        BackgroundOf(input).A.ShouldBe((byte)0);
        BorderOf(input).A.ShouldBe((byte)0);
        // A filled box under the pointer stays text: only the owner's link lights it.
        rig.Hover(input);
        input.Look.ShouldBe(ExtractionInputLook.Filled);
    }

    [AvaloniaFact]
    public void A_Linked_Row_Takes_Its_Group_Color_At_8_Percent_And_The_Box_Sits_On_Surface()
    {
        var value = new ExtractionValue("vendor");
        Land(value, "Oakridge Ceramics Ltd", 0.99, null);
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text, tone: 2));
        var row = (ExtractionFieldRow)rig.Content;
        TestHelpers.ColorOf(row.Background).A.ShouldBe((byte)0);
        row.IsLinked = true;
        rig.Settle();
        row.Classes.ShouldContain(ExtractionFieldRow.LinkedPseudoClass);
        var series = Token(row, ArcDesignTokenKind.Series2);
        TestHelpers.ColorOf(row.Background).ShouldBe(Oklch.WithAlpha(series, series.A / 255.0 * 0.08));
        TestHelpers.ColorOf(TestHelpers.Descendant<Border>(row, "PART_Row").Background).ShouldBe(TestHelpers.ColorOf(row.Background));
        var input = row.Input!;
        input.Look.ShouldBe(ExtractionInputLook.Linked);
        BackgroundOf(input).ShouldBe(Token(row, ArcDesignTokenKind.Surface));
        BorderOf(input).ShouldBe(Token(row, ArcDesignTokenKind.Border));
        row.IsLinked = false;
        rig.Settle();
        TestHelpers.ColorOf(row.Background).A.ShouldBe((byte)0);
        input.Look.ShouldBe(ExtractionInputLook.Filled);
    }

    [AvaloniaFact]
    public void A_Focused_Box_Is_Surface_With_BorderStrong_And_No_Ring()
    {
        var value = new ExtractionValue("vendor");
        Land(value, "Oakridge Ceramics Ltd", 0.99, null);
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text));
        var row = (ExtractionFieldRow)rig.Content;
        row.FocusInput(NavigationMethod.Tab).ShouldBeTrue();
        rig.Settle();
        var input = row.Input!;
        input.IsFocused.ShouldBeTrue();
        input.Look.ShouldBe(ExtractionInputLook.Focused);
        BackgroundOf(input).ShouldBe(Token(row, ArcDesignTokenKind.Surface));
        BorderOf(input).ShouldBe(Token(row, ArcDesignTokenKind.BorderStrong));
        input.FocusAdorner.ShouldBeNull();
        // Focus wins over the link.
        row.IsLinked = true;
        rig.Settle();
        input.Look.ShouldBe(ExtractionInputLook.Focused);
        // The caret is after the text.
        input.CaretIndex.ShouldBe("Oakridge Ceramics Ltd".Length);
    }

    [AvaloniaFact]
    public void A_Low_Confidence_Box_Is_Six_Percent_Warning_With_A_Half_Warning_Border()
    {
        var value = new ExtractionValue("dueDate");
        using var rig = new ExtractionFieldRig(Row(value));
        var row = (ExtractionFieldRow)rig.Content;
        Land(value);
        rig.Settle();
        var input = row.Input!;
        input.Look.ShouldBe(ExtractionInputLook.Low);
        input.Classes.ShouldContain(ExtractionInput.LowPseudoClass);
        var surface = Token(row, ArcDesignTokenKind.Surface);
        var border = Token(row, ArcDesignTokenKind.Border);
        var warning = Token(row, ArcDesignTokenKind.Warning);
        BackgroundOf(input).ShouldBe(Oklch.MixOklab(surface, warning, 0.06));
        BorderOf(input).ShouldBe(Oklch.MixOklab(border, warning, 0.5));
        // Low keeps its look under focus and the link.
        row.FocusInput(NavigationMethod.Tab);
        row.IsLinked = true;
        rig.Settle();
        input.Look.ShouldBe(ExtractionInputLook.Low);
    }

    [AvaloniaFact]
    public void An_Invalid_Box_Is_Surface_With_A_Danger_Border_And_Its_Error_Below()
    {
        var value = new ExtractionValue("issueDate");
        Land(value, "2026-09-03", 0.97, null);
        using var rig = new ExtractionFieldRig(Row(value));
        var row = (ExtractionFieldRow)rig.Content;
        var error = TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.ErrorPart);
        error.IsVisible.ShouldBeFalse();
        row.Bounds.Height.ShouldBe(36, 0.01);
        value.Text = "3 Sept 2026";
        value.Status = ExtractionValueStatus.Edited;
        value.Error = "Use YYYY-MM-DD";
        rig.Settle();
        var input = row.Input!;
        input.Look.ShouldBe(ExtractionInputLook.Invalid);
        input.Classes.ShouldContain(ExtractionInput.InvalidPseudoClass);
        row.Classes.ShouldContain(ExtractionFieldRow.InvalidPseudoClass);
        BackgroundOf(input).ShouldBe(Token(row, ArcDesignTokenKind.Surface));
        BorderOf(input).ShouldBe(Oklch.MixOklab(Token(row, ArcDesignTokenKind.Border), Token(row, ArcDesignTokenKind.Danger), 0.6));
        row.ErrorText.ShouldBe("Use YYYY-MM-DD");
        error.IsVisible.ShouldBeTrue();
        error.Text.ShouldBe("Use YYYY-MM-DD");
        error.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(error.Foreground).ShouldBe(Token(row, ArcDesignTokenKind.Danger));
        // Under the box, its text 8 in (padding 4 0 6 8).
        var errorBounds = BoundsIn(error, row);
        errorBounds.X.ShouldBe(BoundsIn(input, row).X + 8, 0.01);
        errorBounds.Y.ShouldBe(2 + 32 + 4, 0.01);
        row.Bounds.Height.ShouldBe(2 + 32 + 4 + errorBounds.Height + 6 + 2, 0.01);
        AutomationProperties.GetHelpText(input).ShouldBe("Use YYYY-MM-DD");
        // Invalid wins over low.
        value.NeedsCheck = true;
        rig.Settle();
        input.Look.ShouldBe(ExtractionInputLook.Invalid);
        value.Error = null;
        rig.Settle();
        error.IsVisible.ShouldBeFalse();
        input.Look.ShouldBe(ExtractionInputLook.Low);
    }

    [AvaloniaFact]
    public void A_Models_Value_Lands_And_The_Persons_Typing_Does_Not()
    {
        var value = new ExtractionValue("vendor");
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text), ArcMotionLevel.Full);
        var input = ((ExtractionFieldRow)rig.Content).Input!;
        input.Classes.ShouldNotContain(ExtractionInput.LandedPseudoClass);
        Land(value, "Oakridge Ceramics Ltd", 0.99, null);
        rig.Settle();
        input.Classes.ShouldContain(ExtractionInput.LandedPseudoClass);
        // The value empties (Extract again): no longer landed; the person types into the empty box: no landing.
        value.Text = string.Empty;
        rig.Settle();
        input.Classes.ShouldNotContain(ExtractionInput.LandedPseudoClass);
        input.Focus(NavigationMethod.Tab);
        rig.Type("Oak");
        value.Text.ShouldBe("Oak");
        input.Classes.ShouldNotContain(ExtractionInput.LandedPseudoClass);
        // A model value that replaces text is not a landing either (only empty → filled).
        value.Text = "Oakridge";
        rig.Settle();
        input.Text.ShouldBe("Oakridge");
        input.Classes.ShouldNotContain(ExtractionInput.LandedPseudoClass);
    }

    [AvaloniaFact]
    public void Nothing_Lands_Without_Motion()
    {
        var value = new ExtractionValue("vendor");
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text));
        var input = ((ExtractionFieldRow)rig.Content).Input!;
        Land(value, "Oakridge Ceramics Ltd", 0.99, null);
        rig.Settle();
        input.Text.ShouldBe("Oakridge Ceramics Ltd");
        input.Classes.ShouldNotContain(ExtractionInput.LandedPseudoClass);
        TestHelpers.Descendant<Panel>(input, "PART_Content").Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Landing_Fades_The_Text_In_And_The_Fill_Out()
    {
        var value = new ExtractionValue("vendor");
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text), ArcMotionLevel.Full);
        var input = ((ExtractionFieldRow)rig.Content).Input!;
        var content = TestHelpers.Descendant<Panel>(input, "PART_Content");
        Land(value, "Oakridge Ceramics Ltd", 0.99, null);
        rig.Settle();
        input.Classes.ShouldContain(ExtractionInput.LandedPseudoClass);
        // The fade runs on Avalonia's animation clock (real time, frames on request).
        for (var i = 0; i < 100 && content.Opacity < 1; i++)
        {
            Thread.Sleep(5);
            rig.Window.InvalidateVisual();
            rig.Settle();
        }
        // The fill's transition is declared in the theme (160ms) and the text's fade is the :landed animation.
        var transitions = input.Transitions.ShouldNotBeNull().OfType<ArcBrushTransition>().ToList();
        transitions.Single(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty).Duration.ShouldBe(ArcMotionPresets.Fast);
        transitions.Single(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty).Duration.ShouldBe(ArcMotionPresets.Fast);
        input.Look.ShouldBe(ExtractionInputLook.Filled);
        content.Opacity.ShouldBe(1, 0.001);
        BackgroundOf(input).A.ShouldBe((byte)0);
    }

    [AvaloniaFact]
    public void With_Full_Motion_The_Colors_Arrive_After_Their_Transitions()
    {
        var value = new ExtractionValue("dueDate");
        using var rig = new ExtractionFieldRig(Row(value), ArcMotionLevel.Full);
        var row = (ExtractionFieldRow)rig.Content;
        var input = row.Input!;
        Land(value);
        rig.Settle();
        input.Look.ShouldBe(ExtractionInputLook.Low);
        // Several changes within one transition (filled, then low) end on the last one.
        for (var i = 0; i < 60; i++)
        {
            Thread.Sleep(5);
            rig.Window.InvalidateVisual();
            rig.Settle();
        }
        var warning = Token(row, ArcDesignTokenKind.Warning);
        BackgroundOf(input).ShouldBe(Oklch.MixOklab(Token(row, ArcDesignTokenKind.Surface), warning, 0.06));
        BorderOf(input).ShouldBe(Oklch.MixOklab(Token(row, ArcDesignTokenKind.Border), warning, 0.5));
    }

    [AvaloniaFact]
    public void The_Reason_Opens_Under_A_Low_Value_With_A_Note_And_Closes_When_It_Is_Checked()
    {
        var value = new ExtractionValue("dueDate");
        using var rig = new ExtractionFieldRig(Row(value));
        var row = (ExtractionFieldRow)rig.Content;
        var reason = row.Reason!;
        row.IsReasonOpen.ShouldBeFalse();
        reason.IsMounted.ShouldBeFalse();
        // A closed reason is out of the visual tree: its pill is no tab stop.
        TopLevel.GetTopLevel(row.LooksRightButton!).ShouldBeNull();
        Land(value);
        rig.Settle();
        row.IsReasonOpen.ShouldBeTrue();
        row.ReasonText.ShouldBe(DueNote);
        row.Classes.ShouldContain(ExtractionFieldRow.ReasonPseudoClass);
        reason.IsMounted.ShouldBeTrue();
        var text = TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.ReasonTextPart);
        text.Text.ShouldBe(DueNote);
        text.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(text.Foreground).ShouldBe(Token(row, ArcDesignTokenKind.TextSecondary));
        // From the box's column: the text 8 in, 6 below the box; the pill 10 after it, 3 up, on the right.
        BoundsIn(text, row).X.ShouldBe(BoundsIn(row.Input!, row).X + 8, 0.01);
        BoundsIn(text, row).Y.ShouldBe(2 + 32 + 6, 0.01);
        var pill = row.LooksRightButton!;
        TopLevel.GetTopLevel(pill).ShouldNotBeNull();
        BoundsIn(pill, row).Right.ShouldBe(410 - 4, 0.01);
        BoundsIn(pill, row).X.ShouldBe(BoundsIn(text, row).Right + 10, 0.01);
        BoundsIn(pill, row).Y.ShouldBe(2 + 32 + 6 - 3, 0.01);
        reason.Bounds.Height.ShouldBe(6 + text.Bounds.Height + 6, 0.01);
        row.Bounds.Height.ShouldBe(2 + 32 + reason.Bounds.Height + 2, 0.01);
        AutomationProperties.GetHelpText(row.Input!).ShouldBe(DueNote);

        // A value without a note needs a check but shows no reason.
        value.Note = null;
        rig.Settle();
        row.IsReasonOpen.ShouldBeFalse();
        value.Note = DueNote;
        rig.Settle();
        row.IsReasonOpen.ShouldBeTrue();

        value.Status = ExtractionValueStatus.Checked;
        value.NeedsCheck = false;
        rig.Settle();
        row.IsReasonOpen.ShouldBeFalse();
        reason.IsMounted.ShouldBeFalse();
        row.ReasonText.ShouldBeNull();
        row.Bounds.Height.ShouldBe(36, 0.01);
        AutomationProperties.GetHelpText(row.Input!).ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Reason_Opens_On_The_Smooth_Spring_With_A_160ms_Fade_And_Closes_After_A_100ms_Fade()
    {
        var value = new ExtractionValue("dueDate");
        using var rig = new ExtractionFieldRig(Row(value), ArcMotionLevel.Full);
        var row = (ExtractionFieldRow)rig.Content;
        var reason = row.Reason!;
        Land(value);
        rig.Settle();
        reason.IsMounted.ShouldBeTrue();
        rig.Frames(6 * 1000.0 / 60);
        var natural = reason.Content!.DesiredSize.Height;
        natural.ShouldBeGreaterThan(20);
        var (displacement, _) = SpringSolver.Evaluate(ArcMotionPresets.Smooth, -natural, 0, 6 * Frame);
        reason.DisplayedHeight.ShouldBe(natural + displacement, 0.05);
        reason.DisplayedHeight.ShouldBeInRange(natural * 0.2, natural * 0.9);
        reason.DisplayedOpacity.ShouldBeInRange(0.3, 0.99);
        reason.Clip.ShouldNotBeNull();
        rig.Frames(100);
        reason.DisplayedOpacity.ShouldBe(1, 0.001);
        rig.Frames(600);
        reason.DisplayedHeight.ShouldBe(natural, 0.01);
        reason.Clip.ShouldBeNull();

        value.Status = ExtractionValueStatus.Checked;
        value.NeedsCheck = false;
        rig.Settle();
        reason.IsMounted.ShouldBeTrue();
        rig.Frames(50);
        reason.DisplayedOpacity.ShouldBeInRange(0.01, 0.7);
        reason.DisplayedHeight.ShouldBeInRange(1, natural - 1);
        rig.Frames(60);
        reason.DisplayedOpacity.ShouldBe(0, 0.001);
        reason.IsMounted.ShouldBeTrue();
        rig.Frames(600);
        rig.Drain(() => !reason.IsMounted).ShouldBeTrue();
        row.Bounds.Height.ShouldBe(36, 0.01);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_The_Reason_At_Full_Height_And_Fades_It_In_120ms()
    {
        var value = new ExtractionValue("dueDate");
        using var rig = new ExtractionFieldRig(Row(value), ArcMotionLevel.Reduced);
        var row = (ExtractionFieldRow)rig.Content;
        var reason = row.Reason!;
        Land(value);
        rig.Settle();
        var natural = reason.Content!.DesiredSize.Height;
        reason.DisplayedHeight.ShouldBe(natural, 0.01);
        rig.Frames(60);
        reason.DisplayedOpacity.ShouldBeInRange(0.35, 0.65);
        rig.Frames(70);
        reason.DisplayedOpacity.ShouldBe(1, 0.001);
        value.NeedsCheck = false;
        rig.Settle();
        rig.Frames(60);
        reason.DisplayedHeight.ShouldBe(natural, 0.01);
        reason.DisplayedOpacity.ShouldBeInRange(0.35, 0.65);
        rig.Frames(80);
        rig.Drain(() => !reason.IsMounted).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Looks_Right_Only_Asks()
    {
        var value = new ExtractionValue("dueDate");
        Land(value);
        using var rig = new ExtractionFieldRig(Row(value));
        var row = (ExtractionFieldRow)rig.Content;
        var asked = new List<string?>();
        row.LooksRightRequested += (_, e) => asked.Add(e.Path);
        var pill = row.LooksRightButton!;
        pill.Text.ShouldBe("Looks right");
        AutomationProperties.GetName(pill).ShouldBe("Looks right");
        rig.Click(pill);
        asked.ShouldBe(["dueDate"]);
        value.Status.ShouldBe(ExtractionValueStatus.Filled);
        value.NeedsCheck.ShouldBeTrue();
        row.IsReasonOpen.ShouldBeTrue();
        // Enter on the focused pill asks too.
        pill.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        asked.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void The_Pill_Is_26_High_With_A_Check_And_Lights_Under_The_Pointer()
    {
        var value = new ExtractionValue("dueDate");
        Land(value);
        using var rig = new ExtractionFieldRig(Row(value));
        var row = (ExtractionFieldRow)rig.Content;
        var pill = row.LooksRightButton!;
        pill.Bounds.Height.ShouldBe(26, 0.01);
        pill.Padding.ShouldBe(new Thickness(8, 0, 10, 0));
        pill.FontSize.ShouldBe(12);
        pill.FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(pill.Background).ShouldBe(Token(row, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(Token(row, ArcDesignTokenKind.Border));
        TestHelpers.Descendant<ArcGlyph>(pill).Data.ShouldBe(ArcIcons.Check);
        TestHelpers.Descendant<ArcGlyph>(pill).Size.ShouldBe(13);
        rig.Hover(pill);
        pill.Classes.ShouldContain(ExtractionPillButton.HoverPseudoClass);
        TestHelpers.ColorOf(pill.Background).ShouldBe(Token(row, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(Token(row, ArcDesignTokenKind.BorderStrong));
        // The keyboard ring shows only for keyboard focus.
        var ring = TestHelpers.Descendant<Border>(pill, "PART_FocusRing");
        pill.Focus(NavigationMethod.Pointer);
        rig.Settle();
        ring.IsVisible.ShouldBeFalse();
        row.FocusInput(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBe(pill);
        ring.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void In_A_Narrow_Row_The_Pill_Goes_Under_The_Reason_On_The_Left()
    {
        var value = new ExtractionValue("dueDate");
        Land(value);
        var row = Row(value);
        row.IsNarrow = true;
        row.Width = 332;
        using var rig = new ExtractionFieldRig(row);
        var text = TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.ReasonTextPart);
        var pill = row.LooksRightButton!;
        BoundsIn(pill, row).X.ShouldBe(BoundsIn(text, row).X, 0.01);
        BoundsIn(pill, row).Y.ShouldBe(BoundsIn(text, row).Bottom + 10, 0.01);
        // The text takes the whole width of the box's column and the confidence column.
        BoundsIn(text, row).Right.ShouldBe(332 - 4, 0.5);
    }

    [AvaloniaFact]
    public void Typing_Writes_The_Value_And_Raises_ValueEdited()
    {
        var value = new ExtractionValue("vendor");
        Land(value, "Oakridge Ceramics", 0.99, null);
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text));
        var row = (ExtractionFieldRow)rig.Content;
        var edits = new List<ExtractionValueChangedEventArgs>();
        row.ValueEdited += (_, e) => edits.Add(e);
        row.FocusInput(NavigationMethod.Pointer);
        rig.Type(" Ltd");
        value.Text.ShouldBe("Oakridge Ceramics Ltd");
        edits.Count.ShouldBe(1);
        edits[0].Path.ShouldBe("vendor");
        edits[0].OldText.ShouldBe("Oakridge Ceramics");
        edits[0].NewText.ShouldBe("Oakridge Ceramics Ltd");
        edits[0].Origin.ShouldBe(ExtractionOrigin.User);
        edits[0].RoutedEvent.ShouldBe(ExtractionFieldRow.ValueEditedEvent);
        rig.Key(PhysicalKey.Backspace);
        value.Text.ShouldBe("Oakridge Ceramics Lt");
        edits.Count.ShouldBe(2);
        // The status is the owner's: the row did not touch it.
        value.Status.ShouldBe(ExtractionValueStatus.Filled);
        // The model's (or the owner's) changes are not edits.
        value.Text = "Oakridge";
        rig.Settle();
        row.Input!.Text.ShouldBe("Oakridge");
        edits.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void The_Pointer_Over_The_Row_Raises_PathEntered_And_PathExited()
    {
        var value = new ExtractionValue("vendor");
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text));
        var row = (ExtractionFieldRow)rig.Content;
        var log = new List<string>();
        row.PathEntered += (_, e) => log.Add("enter " + e.Path);
        row.PathExited += (_, e) => log.Add("exit " + e.Path);
        rig.Hover(TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.LabelPart));
        rig.Hover(row.Input!);
        rig.Hover(TestHelpers.Descendant<ConfidenceIndicator>(row, ExtractionFieldRow.ConfidencePart));
        rig.Leave();
        log.ShouldBe(["enter vendor", "exit vendor"]);
        // The row does not light itself: the owner does.
        row.IsLinked.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Focusing_The_Box_Raises_PathFocused_And_Tab_Goes_Box_Then_Pill()
    {
        var value = new ExtractionValue("dueDate");
        Land(value);
        var before = new Button { Content = "Before" };
        var row = Row(value);
        var panel = new StackPanel { Width = 410, Children = { before, row } };
        using var rig = new ExtractionFieldRig(panel);
        var focused = new List<string?>();
        row.PathFocused += (_, e) => focused.Add(e.Path);
        before.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBe(row.Input);
        focused.ShouldBe(["dueDate"]);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBe(row.LooksRightButton);
        focused.Count.ShouldBe(1);
        rig.Key(PhysicalKey.Tab, RawInputModifiers.Shift);
        rig.Focused.ShouldBe(row.Input);
        focused.Count.ShouldBe(2);
        // The label and the confidence column are not tab stops.
        row.Focusable.ShouldBeFalse();
        TestHelpers.Descendant<ConfidenceIndicator>(row).Focusable.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void When_The_Reason_Closes_Under_The_Focused_Pill_The_Focus_Goes_To_The_Box()
    {
        var value = new ExtractionValue("dueDate");
        Land(value);
        using var rig = new ExtractionFieldRig(Row(value));
        var row = (ExtractionFieldRow)rig.Content;
        row.LooksRightRequested += (_, _) =>
        {
            value.Status = ExtractionValueStatus.Checked;
            value.NeedsCheck = false;
        };
        row.LooksRightButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        row.IsReasonOpen.ShouldBeFalse();
        rig.Focused.ShouldBe(row.Input);
    }

    [AvaloniaFact]
    public void Clicking_The_Label_Focuses_The_Box()
    {
        var value = new ExtractionValue("vendor");
        Land(value, "Oakridge Ceramics Ltd", 0.99, null);
        using var rig = new ExtractionFieldRig(Row(value, ExtractionFieldType.Text));
        var row = (ExtractionFieldRow)rig.Content;
        var focused = new List<string?>();
        row.PathFocused += (_, e) => focused.Add(e.Path);
        rig.Click(TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.LabelPart));
        rig.Focused.ShouldBe(row.Input);
        focused.ShouldBe(["vendor"]);
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_The_Box_By_Name_And_The_Confidence_As_A_Sentence()
    {
        var value = new ExtractionValue("dueDate");
        Land(value);
        var row = Row(value);
        row.ConfidenceAutomationText = "Low confidence, 66 percent, check this value";
        using var rig = new ExtractionFieldRig(row);
        AutomationProperties.GetName(row.Input!).ShouldBe("Due date");
        row.AutomationName = "Due date, invoice";
        AutomationProperties.GetName(row.Input!).ShouldBe("Due date, invoice");
        var confidence = TestHelpers.Descendant<ConfidenceIndicator>(row);
        AutomationProperties.GetName(confidence).ShouldBe("Low confidence, 66 percent, check this value");
        AutomationProperties.GetAccessibilityView(TestHelpers.Descendant<TextBlock>(row, ExtractionFieldRow.LabelPart))
            .ShouldBe(AccessibilityView.Raw);
    }

    [AvaloniaFact]
    public void A_New_Value_Is_Followed_And_The_Old_One_Let_Go()
    {
        var first = new ExtractionValue("vendor");
        Land(first, "Oakridge Ceramics Ltd", 0.99, null);
        using var rig = new ExtractionFieldRig(Row(first, ExtractionFieldType.Text));
        var row = (ExtractionFieldRow)rig.Content;
        var second = new ExtractionValue("vendor");
        row.Value = second;
        rig.Settle();
        row.Input!.Text.ShouldBe(string.Empty);
        row.Input.Look.ShouldBe(ExtractionInputLook.Empty);
        first.Error = "Changed after";
        first.Text = "Elsewhere";
        rig.Settle();
        row.Input.Text.ShouldBe(string.Empty);
        row.ErrorText.ShouldBeNull();
        Land(second, "Bluefin Logistics BV", 0.95, null);
        rig.Settle();
        row.Input.Text.ShouldBe("Bluefin Logistics BV");
    }
}
