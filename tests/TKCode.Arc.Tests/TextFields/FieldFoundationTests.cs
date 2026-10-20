using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

public class FieldFoundationTests
{
    private static FieldFrame FrameOf(Visual root) => root.GetVisualDescendants().OfType<FieldFrame>().First();

    private static FieldMessage Message(Visual root, string name) =>
        root.GetVisualDescendants().OfType<FieldMessage>().First(m => m.Name == name);

    [AvaloniaFact]
    public void Input_Shows_Label_And_Placeholder_With_The_Arc_Frame()
    {
        var input = new Input { Label = "Email", Placeholder = "you@example.com", Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            input.PlaceholderText.ShouldBe("you@example.com");
            var label = input.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Label");
            label.Text.ShouldBe("Email");
            label.IsEffectivelyVisible.ShouldBeTrue();
            var frame = FrameOf(input);
            frame.Bounds.Height.ShouldBe(44, 0.5);
            frame.CornerRadius.TopLeft.ShouldBe(18);
            frame.BorderThickness.Left.ShouldBe(1);
            AutomationProperties.GetName(input).ShouldBe("Email");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ControlSize.Small, 36)]
    [InlineData(ControlSize.Medium, 44)]
    [InlineData(ControlSize.Large, 50)]
    public void Size_Maps_To_Control_Heights(ControlSize size, double height)
    {
        var input = new Input { Label = "Size", Size = size, Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            FrameOf(input).Bounds.Height.ShouldBe(height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Message_Makes_The_Field_Invalid_And_Opens_The_Error_Row()
    {
        var input = new Input { Label = "Website", Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            var error = Message(input, "PART_Error");
            error.IsOpen.ShouldBeFalse();
            error.Bounds.Height.ShouldBe(0);
            input.IsInvalid.ShouldBeFalse();

            input.ErrorMessage = "Enter a valid URL.";
            Dispatcher.UIThread.RunJobs();
            input.IsInvalid.ShouldBeTrue();
            input.Classes.ShouldContain(":invalid");
            FrameOf(input).Classes.ShouldContain(":invalid");
            error.IsOpen.ShouldBeTrue();
            error.Bounds.Height.ShouldBeGreaterThan(8);
            AutomationProperties.GetLiveSetting(error).ShouldBe(AutomationLiveSetting.Assertive);
            AutomationProperties.GetHelpText(input).ShouldBe("Enter a valid URL.");

            input.ErrorMessage = null;
            Dispatcher.UIThread.RunJobs();
            input.IsInvalid.ShouldBeFalse();
            error.Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Description_Rewrites_Only_Changed_Words()
    {
        var input = new Input { Label = "Username", Description = "3 to 20 characters. 20 left.", Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            var description = Message(input, "PART_Description");
            description.LiveWords.ShouldBe(new[] { "3", "to", "20", "characters.", "20", "left." });
            input.Description = "3 to 20 characters. 17 left.";
            Dispatcher.UIThread.RunJobs();
            description.LiveWords.ShouldBe(new[] { "3", "to", "20", "characters.", "17", "left." });
            AutomationProperties.GetName(description).ShouldBe("3 to 20 characters. 17 left.");
            AutomationProperties.GetHelpText(input).ShouldBe("3 to 20 characters. 17 left.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Message_Row_Animates_Open_And_Closed_Under_Full_Motion()
    {
        var message = new FieldMessage { Width = 300 };
        var window = TestHelpers.Show(message, Motion.ArcMotionLevel.Full);
        try
        {
            message.Text = "Something went wrong";
            Dispatcher.UIThread.RunJobs();
            message.IsOpen.ShouldBeTrue();
            message.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            message.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Validation_Error_From_A_Binding_Shows_Unless_An_Explicit_Error_Is_Set()
    {
        var model = new ValidatedModel();
        var input = new Input { Label = "Age", Width = 320 };
        input.Bind(TextBox.TextProperty, new Binding(nameof(ValidatedModel.Age)) { Source = model, Mode = BindingMode.TwoWay });
        var window = TestHelpers.Show(input);
        try
        {
            input.Text = "abc";
            Dispatcher.UIThread.RunJobs();
            input.EffectiveErrorMessage.ShouldBe("Enter a whole number.");
            input.IsInvalid.ShouldBeTrue();

            input.ErrorMessage = "Explicit wins.";
            Dispatcher.UIThread.RunJobs();
            input.EffectiveErrorMessage.ShouldBe("Explicit wins.");

            input.ErrorMessage = null;
            input.Text = "42";
            Dispatcher.UIThread.RunJobs();
            input.EffectiveErrorMessage.ShouldBeNull();
            input.IsInvalid.ShouldBeFalse();
            model.Age.ShouldBe("42");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Darkens_The_Frame_Through_Focus_Active()
    {
        var input = new Input { Label = "Name", Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            var frame = FrameOf(input);
            frame.Classes.ShouldNotContain(":focus-active");
            input.Focus();
            Dispatcher.UIThread.RunJobs();
            frame.Classes.ShouldContain(":focus-active");
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(frame, DesignTokens.ArcDesignTokenKind.Foreground));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_And_Disabled_States_Reach_The_Frame()
    {
        var readOnly = new Input { Label = "Id", Text = "ws_1", IsReadOnly = true };
        var disabled = new Input { Label = "Off", IsEnabled = false };
        var window = TestHelpers.Show(new StackPanel { Width = 320, Children = { readOnly, disabled } });
        try
        {
            FrameOf(readOnly).Classes.ShouldContain(":readonly");
            FrameOf(disabled).Opacity.ShouldBe(0.5, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Prefix_And_Suffix_Sit_Inside_The_Frame_And_Receive_The_Template_Parent()
    {
        var prefix = new TextBlock { Text = "$" };
        var suffix = new TextBlock { Text = "USD" };
        var input = new Input { Label = "Price", Prefix = prefix, Suffix = suffix, Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            var frame = FrameOf(input);
            prefix.GetVisualAncestors().ShouldContain(frame);
            suffix.GetVisualAncestors().ShouldContain(frame);
            prefix.Bounds.Width.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Standalone_Field_Names_Its_Content_And_Follows_Its_Error()
    {
        var content = new FieldFrame { Content = new TextBlock { Text = "Pick a date" } };
        var field = new Field { Label = "Due date", Description = "Optional", Content = content, Width = 320 };
        var window = TestHelpers.Show(field);
        try
        {
            AutomationProperties.GetName(content).ShouldBe("Due date");
            AutomationProperties.GetHelpText(content).ShouldBe("Optional");
            field.IsInvalid.ShouldBeFalse();
            field.ErrorMessage = "Pick a day after today.";
            field.IsInvalid.ShouldBeTrue();
            field.Classes.ShouldContain(":invalid");
            AutomationProperties.GetHelpText(content).ShouldBe("Optional Pick a day after today.");
            field.ErrorMessage = null;
            field.IsInvalid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Field_Message_Number_Detection_Matches_Arc()
    {
        FieldDrawing.IsNumber("12").ShouldBeTrue();
        FieldDrawing.IsNumber("1,204").ShouldBeTrue();
        FieldDrawing.IsNumber("3/5").ShouldBeTrue();
        FieldDrawing.IsNumber("50%").ShouldBeTrue();
        FieldDrawing.IsNumber("v2").ShouldBeFalse();
        FieldDrawing.IsNumber("12px").ShouldBeFalse();
        FieldDrawing.LeadingNumber("1,204").ShouldBe(1204);
        FieldDrawing.LeadingNumber("3:45").ShouldBe(3);
    }

    private sealed class ValidatedModel
    {
        private string _age = string.Empty;

        public string Age
        {
            get => _age;
            set
            {
                if (value.Length > 0 && !int.TryParse(value, out _))
                {
                    throw new ArgumentException("Enter a whole number.");
                }
                _age = value;
            }
        }
    }
}
