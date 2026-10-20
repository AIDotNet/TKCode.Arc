using TKCode.Arc.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

public class SearchFieldTests
{
    [AvaloniaFact]
    public void Escape_Clears_And_Enter_Submits()
    {
        var field = new SearchField { Label = "Search", Width = 320 };
        var window = TestHelpers.Show(field);
        try
        {
            string? submitted = null;
            field.SearchSubmitted += (_, e) => submitted = e.Query;
            field.Focus();
            window.KeyTextInput("roadmap");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            submitted.ShouldBe("roadmap");
            field.Classes.ShouldContain(":filled");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe(string.Empty);
            field.Classes.ShouldNotContain(":filled");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_Button_Appears_In_A_Reserved_Slot_And_Returns_Focus()
    {
        var field = new SearchField { Label = "Search", Width = 320 };
        var window = TestHelpers.Show(field);
        try
        {
            var presence = field.GetVisualDescendants().OfType<PresenceHost>().First();
            var frame = field.GetVisualDescendants().OfType<FieldFrame>().First();
            var width = frame.Bounds.Width;
            presence.IsPresent.ShouldBeFalse();
            field.Text = "q3";
            Dispatcher.UIThread.RunJobs();
            presence.IsPresent.ShouldBeTrue();
            frame.Bounds.Width.ShouldBe(width);

            var clear = field.GetVisualDescendants().OfType<FieldIconButton>().First();
            Avalonia.Automation.AutomationProperties.GetName(clear).ShouldBe("Clear search");
            clear.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe(string.Empty);
            field.IsFocused.ShouldBeTrue();
            presence.IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
