using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

public class PasswordFieldTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Clicking_Reveal_Changes_The_Text_Actually_Laid_Out(bool strength, bool initiallyHidden)
    {
        const string password = "Sample-password-123";
        PasswordField field = strength ? new PasswordStrength() : new PasswordField();
        field.IsRevealed = false;
        field.IsVisible = !initiallyHidden;
        field.IsUndoEnabled = false;
        field.Size = ControlSize.Large;
        field.Width = 320;
        var window = TestHelpers.Show(field);
        try
        {
            field.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            field.Focus();
            window.KeyTextInput(password);
            Dispatcher.UIThread.RunJobs();
            var presenter = TestHelpers.Descendant<TextPresenter>(field);
            var toggle = TestHelpers.Descendant<FieldIconButton>(field);
            string DisplayedText() => string.Concat(presenter.TextLayout.TextLines.SelectMany(line => line.TextRuns)
                .Select(run => run.Text.ToString()));
            DisplayedText().ShouldBe(new string('•', password.Length));
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            var point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            field.IsRevealed.ShouldBeTrue();
            field.RevealPassword.ShouldBeTrue();
            presenter.RevealPassword.ShouldBeTrue();
            DisplayedText().ShouldBe(password);
            // Returning from the reveal button must not silently remask the editor.
            field.Focus();
            field.RevealPassword.ShouldBeTrue("returning to the editor preserves the selected reveal state");
            window.KeyTextInput("-more");
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe(password + "-more");
            presenter.RevealPassword.ShouldBeTrue();
            DisplayedText().ShouldBe(password + "-more");
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            field.IsRevealed.ShouldBeFalse();
            presenter.RevealPassword.ShouldBeFalse();
            DisplayedText().ShouldBe(new string('•', field.Text!.Length));
        }
        finally
        {
            window.Close();
        }
    }

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
