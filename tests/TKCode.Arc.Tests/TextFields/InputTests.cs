using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.TextFields;

public class InputTests
{
    [AvaloniaFact]
    public void Typing_Updates_Two_Way_Text()
    {
        var input = new Input { Label = "Name", Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            input.Focus();
            window.KeyTextInput("Ada");
            Dispatcher.UIThread.RunJobs();
            input.Text.ShouldBe("Ada");
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            input.Text.ShouldBe("Ad");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Is_Two_Way_By_Default()
    {
        TextBox.TextProperty.GetMetadata(typeof(Input)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
    }

    [AvaloniaFact]
    public void Hidden_Label_Still_Names_The_Field()
    {
        var input = new Input { Label = "Search", IsLabelVisible = false, Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            Avalonia.Automation.AutomationProperties.GetName(input).ShouldBe("Search");
            var label = TestHelpers.Descendant<TextBlock>(input);
            label.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
