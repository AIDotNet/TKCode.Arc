using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Prompt studio's variable fields: one field per variable with its series color (dot, label, focus border), the values
/// both ways and the edit events, wide fields and the 4 / 2 / 1 column grid, focus and the active variable, the focus
/// method chips call, added and removed variables, the trimmed value away from focus and what screen readers get.
/// </summary>
public class PromptVariableFieldsTests
{
    private static PromptVariableFields Create(IList<string>? variables = null, AvaloniaDictionary<string, string>? values = null) => new()
    {
        Variables = variables ?? PromptRunSamples.Variables,
        Values = values ?? PromptRunSamples.Values(),
        EmptyText = "Empty",
        AccessibleName = "Variables"
    };

    private static Window Show(Control content, double width = 640, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } }, level);
        window.Width = width + 40;
        window.Height = 600;
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static Color Tone(Control owner, int tone) => PromptTone.SeriesColor(owner, tone);

    [AvaloniaFact]
    public void One_Field_Per_Variable_In_Order_With_Its_Name_And_Value()
    {
        var fields = Create(["buyer_name", "ticket_id", "buyer_name", "", "refund_rule", "agent_sign"]);
        var window = Show(fields);
        try
        {
            fields.Fields.Select(f => f.VariableName).ShouldBe(["buyer_name", "ticket_id", "refund_rule", "agent_sign"]);
            fields.Fields.Select(f => f.Text).ShouldBe(["Mira Okafor", "NK-3307", "Damaged parts are replaced free within 60 days.", ""]);
            fields.Fields.Select(f => f.Tone).ShouldBe([1, 2, 3, 4]);
            fields.Fields.ShouldAllBe(f => f.PlaceholderText == "Empty");
            TestHelpers.Descendant<TextBlock>(fields.Fields[0], "PART_Label").Text.ShouldBe("buyer_name");
            fields.FieldOf("ticket_id").ShouldBeSameAs(fields.Fields[1]);
            fields.FieldOf("nope").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Field_Takes_Its_Variables_Series_Color()
    {
        var fields = Create(["a", "b", "c", "d", "e"], new AvaloniaDictionary<string, string>());
        var window = Show(fields);
        try
        {
            var foreground = ArcTokens.GetColor(fields, ArcDesignTokenKind.Foreground);
            var border = ArcTokens.GetColor(fields, ArcDesignTokenKind.Border);
            for (var i = 0; i < 5; i++)
            {
                var field = fields.Fields[i];
                var tone = i % 4 + 1;
                field.Tone.ShouldBe(tone);
                var series = Tone(fields, tone);
                TestHelpers.ColorOf(field.DotBrush).ShouldBe(series);
                TestHelpers.ColorOf(field.LabelBrush).ShouldBe(PromptTone.Mix(foreground, series, 0.68));
                TestHelpers.ColorOf(field.FocusBorderBrush).ShouldBe(PromptTone.Mix(border, series, 0.65));
                TestHelpers.ColorOf(TestHelpers.Descendant<Ellipse>(field, "PART_Dot").Fill).ShouldBe(series);
                TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(field, "PART_Label").Foreground).ShouldBe(PromptTone.Mix(foreground, series, 0.68));
            }
            // The fifth variable takes the first color again.
            fields.Fields[4].Tone.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Field_Is_A_Label_Row_Over_A_36px_Rounded_Frame()
    {
        var fields = Create();
        var window = Show(fields);
        try
        {
            var field = fields.Fields[0];
            var row = TestHelpers.Descendant<StackPanel>(field, "PART_LabelRow");
            row.Bounds.Height.ShouldBe(16.8, 0.6);
            row.Margin.ShouldBe(new Thickness(12, 0, 0, 0));
            var dot = TestHelpers.Descendant<Ellipse>(field, "PART_Dot");
            dot.Bounds.Size.ShouldBe(new Size(6, 6));
            var label = TestHelpers.Descendant<TextBlock>(field, "PART_Label");
            label.FontSize.ShouldBe(12);
            label.Bounds.Left.ShouldBe(dot.Bounds.Right + 6, 0.01);

            var frame = TestHelpers.Descendant<Border>(field, "PART_Frame");
            frame.Bounds.Height.ShouldBe(36);
            frame.Bounds.Top.ShouldBe(row.Bounds.Bottom + 5, 0.6);
            frame.CornerRadius.ShouldBe(new CornerRadius(18));
            frame.BorderThickness.ShouldBe(new Thickness(1));
            frame.Padding.ShouldBe(new Thickness(12, 0));
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(fields, ArcDesignTokenKind.Surface));
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(fields, ArcDesignTokenKind.Border));
            field.FontSize.ShouldBe(14);
            TestHelpers.ColorOf(field.PlaceholderForeground).ShouldBe(ArcTokens.GetColor(fields, ArcDesignTokenKind.TextMuted));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Writes_The_Value_And_Raises_An_Edit_Per_Keystroke()
    {
        var values = PromptRunSamples.Values();
        var fields = Create(values: values);
        var window = Show(fields);
        try
        {
            var edits = new List<string>();
            fields.ValueEdited += (_, e) => edits.Add(e.Name);
            fields.FocusField("ticket_id").ShouldBeTrue();
            window.KeyTextInput("4");
            window.KeyTextInput("2");
            Settle(window);
            values["ticket_id"].ShouldBe("NK-330742");
            fields.Fields[1].Text.ShouldBe("NK-330742");
            edits.ShouldBe(["ticket_id", "ticket_id"]);

            // A variable without an entry gets one.
            fields.FocusField("agent_sign").ShouldBeTrue();
            window.KeyTextInput("K");
            values["agent_sign"].ShouldBe("K");
            edits[^1].ShouldBe("agent_sign");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Values_Changed_Elsewhere_Show_At_Once_Without_Edit_Events()
    {
        var values = PromptRunSamples.Values();
        var fields = Create(values: values);
        var window = Show(fields);
        try
        {
            var edits = 0;
            fields.ValueEdited += (_, _) => edits++;
            // A test case is loaded.
            foreach (var pair in PromptRunSamples.Cases()[1].Values)
            {
                values[pair.Key] = pair.Value;
            }
            fields.Fields[0].Text.ShouldBe("Tomas Lindqvist");
            fields.Fields[1].Text.ShouldBe("NK-3412");
            values.Remove("ticket_id");
            fields.Fields[1].Text.ShouldBe("");
            values.Clear();
            fields.Fields.ShouldAllBe(f => f.Text == "");

            // A new dictionary.
            var other = new AvaloniaDictionary<string, string> { ["buyer_name"] = "Ana Ferreira" };
            fields.Values = other;
            fields.Fields[0].Text.ShouldBe("Ana Ferreira");
            other["buyer_name"] = "Ana F.";
            fields.Fields[0].Text.ShouldBe("Ana F.");
            // The old dictionary is no longer followed.
            values["buyer_name"] = "Old";
            fields.Fields[0].Text.ShouldBe("Ana F.");
            edits.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Long_Value_Makes_Its_Field_Wide_And_Typing_Never_Changes_That()
    {
        var values = PromptRunSamples.Values();
        var fields = Create(values: values);
        var window = Show(fields);
        try
        {
            fields.Fields.Select(f => f.IsWide).ShouldBe([false, false, true, false]);
            fields.Fields[2].Classes.Contains(PromptVariableField.WidePseudoClass).ShouldBeTrue();

            // Cleared by the person: still wide (as in Arc).
            fields.FocusField("refund_rule");
            fields.Fields[2].SelectAll();
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Settle(window);
            values["refund_rule"].ShouldBe("");
            fields.Fields[2].IsWide.ShouldBeTrue();

            // A short value from a test case: narrow again; a long one: wide.
            values["refund_rule"] = "Short rule";
            fields.Fields[2].IsWide.ShouldBeFalse();
            values["buyer_name"] = "A name that is far longer than the threshold";
            fields.Fields[0].IsWide.ShouldBeTrue();

            fields.WideThreshold = 100;
            fields.Fields[0].IsWide.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [Theory]
    [InlineData(new[] { 1, 1, 2, 1 }, 4, "0:0:1 0:1:1 0:2:2 1:0:1")]
    [InlineData(new[] { 1, 1, 1, 2, 1 }, 4, "0:0:1 0:1:1 0:2:1 1:0:2 1:2:1")]
    [InlineData(new[] { 1, 2, 1 }, 2, "0:0:1 1:0:2 2:0:1")]
    [InlineData(new[] { 2, 1, 1 }, 1, "0:0:1 1:0:1 2:0:1")]
    public void Fields_Are_Placed_In_Order_And_A_Wide_One_Starts_A_Row_When_It_Does_Not_Fit(int[] spans, int columns, string expected)
    {
        var cells = new List<(int Row, int Column, int Span)>();
        PromptVariableFieldsPanel.Place(spans, columns, cells);
        string.Join(" ", cells.Select(c => $"{c.Row}:{c.Column}:{c.Span}")).ShouldBe(expected);
    }

    [AvaloniaFact]
    public void Four_Columns_Ten_Apart_With_Rows_Twelve_Apart()
    {
        var fields = Create();
        var window = Show(fields, width: 600);
        try
        {
            fields.ColumnCount.ShouldBe(4);
            fields.Classes.Contains(PromptVariableFields.TwoColumnPseudoClass).ShouldBeFalse();
            var column = (600 - 30) / 4.0;
            var f = fields.Fields;
            // Laid out to whole pixels: within a pixel of the exact grid.
            f[0].Bounds.X.ShouldBe(0);
            f[0].Bounds.Width.ShouldBe(column, 0.6);
            f[1].Bounds.X.ShouldBe(column + 10, 0.6);
            f[1].Bounds.Width.ShouldBe(column, 0.6);
            // refund_rule is wide: columns three and four.
            f[2].Bounds.X.ShouldBe(2 * (column + 10), 0.6);
            f[2].Bounds.Width.ShouldBe(2 * column + 10, 0.6);
            // agent_sign starts the second row.
            f[3].Bounds.X.ShouldBe(0);
            f[3].Bounds.Y.ShouldBe(f[0].Bounds.Bottom + 12, 0.6);
            fields.Bounds.Height.ShouldBe(f[3].Bounds.Bottom, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Grid_Has_Two_Columns_From_528_Down_And_One_From_316_Down()
    {
        PromptVariableFields.ColumnsFor(529, 528, 316).ShouldBe(4);
        PromptVariableFields.ColumnsFor(528, 528, 316).ShouldBe(2);
        PromptVariableFields.ColumnsFor(317, 528, 316).ShouldBe(2);
        PromptVariableFields.ColumnsFor(316, 528, 316).ShouldBe(1);
        PromptVariableFields.ColumnsFor(double.PositiveInfinity, 528, 316).ShouldBe(4);

        var fields = Create();
        var window = Show(fields, width: 500);
        try
        {
            fields.ColumnCount.ShouldBe(2);
            fields.Classes.Contains(PromptVariableFields.TwoColumnPseudoClass).ShouldBeTrue();
            var f = fields.Fields;
            f[1].Bounds.X.ShouldBe((500 - 10) / 2.0 + 10, 0.6);
            // The wide field takes a row of its own.
            f[2].Bounds.X.ShouldBe(0);
            f[2].Bounds.Width.ShouldBe(500, 0.6);
            f[3].Bounds.Y.ShouldBeGreaterThan(f[2].Bounds.Y);

            window.Width = 300 + 40;
            Settle(window);
            fields.ColumnCount.ShouldBe(1);
            fields.Classes.Contains(PromptVariableFields.OneColumnPseudoClass).ShouldBeTrue();
            fields.Classes.Contains(PromptVariableFields.TwoColumnPseudoClass).ShouldBeFalse();
            f.ShouldAllBe(field => Math.Abs(field.Bounds.Width - 300) < 0.6);

            // The breakpoints are properties.
            fields.OneColumnMaxWidth = 200;
            Settle(window);
            fields.ColumnCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Makes_A_Variable_Active_And_Leaving_The_Fields_Clears_It()
    {
        var after = new Button { Content = "after" };
        var fields = Create();
        var window = Show(new StackPanel { Children = { fields, after } });
        try
        {
            var focused = new List<string>();
            fields.FieldFocused += (_, e) => focused.Add(e.Name);
            fields.ActiveVariable.ShouldBeNull();
            fields.Fields[1].Focus(NavigationMethod.Tab);
            Settle(window);
            fields.ActiveVariable.ShouldBe("ticket_id");
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Settle(window);
            fields.ActiveVariable.ShouldBe("refund_rule");
            focused.ShouldBe(["ticket_id", "refund_rule"]);

            after.Focus(NavigationMethod.Tab);
            Settle(window);
            fields.ActiveVariable.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Label_Focuses_The_Field_Like_A_Label_Element()
    {
        var fields = Create();
        var window = Show(fields);
        try
        {
            var label = TestHelpers.Descendant<TextBlock>(fields.Fields[1], "PART_Label");
            var point = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle(window);
            fields.Fields[1].IsFocused.ShouldBeTrue();
            fields.ActiveVariable.ShouldBe("ticket_id");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Active_Variable_Clears_It()
    {
        var variables = new AvaloniaList<string>(PromptRunSamples.Variables);
        var fields = Create(variables);
        var window = Show(fields);
        try
        {
            fields.FocusField("refund_rule").ShouldBeTrue();
            Settle(window);
            fields.ActiveVariable.ShouldBe("refund_rule");
            variables.Remove("refund_rule");
            Settle(window);
            fields.ActiveVariable.ShouldBeNull();
            fields.FieldOf("refund_rule").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Field_Puts_The_Caret_At_The_End()
    {
        var fields = Create();
        var window = Show(fields);
        try
        {
            fields.FocusField("buyer_name").ShouldBeTrue();
            Settle(window);
            var field = fields.Fields[0];
            field.IsFocused.ShouldBeTrue();
            field.CaretIndex.ShouldBe("Mira Okafor".Length);
            field.SelectionStart.ShouldBe(field.SelectionEnd);
            fields.ActiveVariable.ShouldBe("buyer_name");
            fields.FocusField("missing").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Border_Is_Strong_Under_The_Pointer_And_The_Series_Mix_While_Focused()
    {
        var fields = Create();
        var window = Show(fields);
        try
        {
            var field = fields.Fields[1];
            var frame = TestHelpers.Descendant<Border>(field, "PART_Frame");
            var point = field.TranslatePoint(new Point(20, 40), window)!.Value;
            window.MouseMove(point);
            Settle(window);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(fields, ArcDesignTokenKind.BorderStrong));

            field.Focus(NavigationMethod.Pointer);
            Settle(window);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(TestHelpers.ColorOf(field.FocusBorderBrush));
            // No focus ring: the border is the focus.
            field.FocusAdorner.ShouldBeNull();

            window.MouseMove(new Point(1, 1));
            fields.Fields[0].Focus(NavigationMethod.Pointer);
            Settle(window);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(fields, ArcDesignTokenKind.Border));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Away_From_Focus_The_Value_Is_Shown_Trimmed()
    {
        var fields = Create();
        var window = Show(fields);
        try
        {
            var field = fields.Fields[2];
            var display = TestHelpers.Descendant<TextBlock>(field, "PART_Display");
            var scroller = TestHelpers.Descendant<ScrollViewer>(field, "PART_ScrollViewer");
            display.IsVisible.ShouldBeTrue();
            display.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
            display.Text.ShouldBe(field.Text);
            scroller.Opacity.ShouldBe(0);

            field.Focus(NavigationMethod.Pointer);
            Settle(window);
            display.IsVisible.ShouldBeFalse();
            scroller.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Added_Variables_Get_Fields_And_Removed_Ones_Lose_Them_While_The_Others_Stay()
    {
        var variables = new AvaloniaList<string>(PromptRunSamples.Variables);
        var fields = Create(variables);
        var window = Show(fields);
        try
        {
            var kept = fields.Fields[1];
            kept.Focus(NavigationMethod.Pointer);
            Settle(window);
            variables.Add("tracking_url");
            Settle(window);
            fields.Fields.Count.ShouldBe(5);
            fields.Fields[4].VariableName.ShouldBe("tracking_url");
            fields.Fields[4].Tone.ShouldBe(1);
            fields.Fields[1].ShouldBeSameAs(kept);
            kept.IsFocused.ShouldBeTrue();

            variables.RemoveAt(0);
            Settle(window);
            fields.Fields.Select(f => f.VariableName).ShouldBe(["ticket_id", "refund_rule", "agent_sign", "tracking_url"]);
            fields.Fields[0].ShouldBeSameAs(kept);
            // Colors follow the new order.
            fields.Fields.Select(f => f.Tone).ShouldBe([1, 2, 3, 4]);
            fields.Panel!.Children.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_A_New_Field_Fades_In_And_The_Height_Springs()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var variables = new AvaloniaList<string>(["a", "b", "c", "d"]);
        var fields = Create(variables, new AvaloniaDictionary<string, string>());
        var window = Show(fields, level: ArcMotionLevel.Full);
        try
        {
            fields.Fields.ShouldAllBe(f => f.Opacity == 1);
            var before = fields.Bounds.Height;
            variables.Add("e");
            Settle(window);
            var added = fields.Fields[4];
            added.Opacity.ShouldBeLessThan(0.01);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            Settle(window);
            added.Opacity.ShouldBeInRange(0.05, 0.99);
            fields.Bounds.Height.ShouldBeGreaterThan(before);
            fields.Bounds.Height.ShouldBeLessThan(before + 58 + 12);
            clock.Advance(TimeSpan.FromSeconds(1));
            Settle(window);
            added.Opacity.ShouldBe(1);
            fields.Bounds.Height.ShouldBe(before + 12 + added.Bounds.Height, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Named_Group_Of_Fields_Named_By_Their_Variables()
    {
        var fields = Create();
        var window = Show(fields);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(fields);
            peer.GetName().ShouldBe("Variables");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            var field = ControlAutomationPeer.CreatePeerForElement(fields.Fields[1]);
            field.GetName().ShouldBe("ticket_id");
            field.GetAutomationControlType().ShouldBe(AutomationControlType.Edit);
            AutomationProperties.GetPositionInSet(fields.Fields[1]).ShouldBe(2);
            AutomationProperties.GetSizeOfSet(fields.Fields[1]).ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }
}
