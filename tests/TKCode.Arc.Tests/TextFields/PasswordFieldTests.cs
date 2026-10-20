using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

public class PasswordFieldTests
{
    [AvaloniaFact]
    public void Masks_By_Default_And_Reveal_Toggles_With_The_Keyboard()
    {
        var field = new PasswordField { Label = "Password", Text = "hunter2", Width = 320 };
        var window = TestHelpers.Show(field);
        try
        {
            field.PasswordChar.ShouldBe('•');
            field.RevealPassword.ShouldBeFalse();
            var toggle = field.GetVisualDescendants().OfType<FieldIconButton>().First();
            AutomationProperties.GetName(toggle).ShouldBe("Show password");
            toggle.IsToggle.ShouldBeTrue();
            toggle.IsOn.ShouldBeFalse();

            toggle.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.IsRevealed.ShouldBeTrue();
            field.RevealPassword.ShouldBeTrue();
            toggle.IsOn.ShouldBeTrue();
            AutomationProperties.GetName(toggle).ShouldBe("Hide password");

            toggle.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            toggle.IsPressed.ShouldBeTrue("after down");
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe("hunter2");
            field.IsRevealed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Eye_Slash_Follows_The_Revealed_State()
    {
        var field = new PasswordField { Label = "Password", IsRevealed = true, Width = 320 };
        var window = TestHelpers.Show(field);
        try
        {
            var eye = field.GetVisualDescendants().OfType<PasswordEyeGlyph>().First();
            eye.IsSlashed.ShouldBeTrue();
            eye.SlashLength.ShouldBe(1);
            field.IsRevealed = false;
            Dispatcher.UIThread.RunJobs();
            eye.SlashLength.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reveal_Button_Inset_Is_Even()
    {
        var field = new PasswordField { Label = "Password", Width = 320 };
        var window = TestHelpers.Show(field);
        try
        {
            var frame = field.GetVisualDescendants().OfType<FieldFrame>().First();
            var toggle = field.GetVisualDescendants().OfType<FieldIconButton>().First();
            toggle.Bounds.Width.ShouldBe(30);
            var origin = toggle.TranslatePoint(default, frame)!.Value;
            var right = frame.Bounds.Width - origin.X - toggle.Bounds.Width;
            origin.Y.ShouldBe(7, 0.5);
            right.ShouldBe(7, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
