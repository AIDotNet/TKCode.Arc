using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Buttons;

public class CopyButtonTests
{
    [AvaloniaFact]
    public void Missing_Value_Reports_Failure_Without_Changing_Width()
    {
        var button = new CopyButton { Value = null };
        var window = TestHelpers.Show(button);
        try
        {
            var width = button.Bounds.Width;
            button.State.ShouldBe(CopyState.Idle);
            button.Focus();
            window.KeyPressQwerty(Avalonia.Input.PhysicalKey.Enter, Avalonia.Input.RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            button.State.ShouldBe(CopyState.Error);
            button.Classes.ShouldContain(":error");
            Dispatcher.UIThread.RunJobs();
            button.Bounds.Width.ShouldBe(width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Icon_Only_Hides_The_Label()
    {
        var button = new CopyButton { Value = "x", IsIconOnly = true };
        var window = TestHelpers.Show(button);
        try
        {
            button.Classes.ShouldContain(":icon-only");
            button.Bounds.Width.ShouldBe(36, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
