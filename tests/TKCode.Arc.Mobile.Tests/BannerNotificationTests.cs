using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile.Tests;

public class BannerNotificationTests
{
    [AvaloniaFact]
    public void Show_Opens_Banner_Respects_Top_SafeArea_And_Triggers_Tone_Haptic()
    {
        var recorded = new List<HapticKind>();
        var previous = Haptics.Platform;
        Haptics.Platform = new RecordingHaptics(recorded);
        try
        {
            var banner = new BannerNotification { Duration = TimeSpan.Zero };
            var page = new Page
            {
                UsePlatformSafeArea = false,
                SafeArea = new Thickness(8, 59, 8, 34),
                Content = new TextBlock { Text = "Body" },
                Overlay = banner,
            };
            var window = new Window { Width = 390, Height = 844, Content = page };
            window.Show();
            try
            {
                banner.Show("Synced", "All 14 notes are up to date.", BannerTone.Success);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                banner.IsOpen.ShouldBeTrue();
                banner.Classes.Contains(":success").ShouldBeTrue();
                recorded.ShouldContain(HapticKind.Success);

                var host = banner.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Host");
                host.Margin.Top.ShouldBeGreaterThanOrEqualTo(59 + 8 - 0.01);
                host.Margin.Left.ShouldBeGreaterThanOrEqualTo(8 + 12 - 0.01);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            Haptics.Platform = previous;
        }
    }

    [AvaloniaFact]
    public void Action_Button_Invokes_Command_Event_And_Dismisses()
    {
        var invoked = 0;
        var dismissed = 0;
        var commandRan = 0;
        var banner = new BannerNotification
        {
            Title = "Connection lost",
            Tone = BannerTone.Danger,
            Duration = TimeSpan.Zero,
            ActionText = "Retry",
            ActionCommand = new ActionCommand(() => commandRan++),
        };
        banner.ActionInvoked += (_, _) => invoked++;
        banner.Dismissed += (_, _) => dismissed++;

        var window = new Window { Width = 390, Height = 400, Content = banner };
        window.Show();
        try
        {
            banner.IsOpen = true;
            Dispatcher.UIThread.RunJobs();

            var button = banner.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action");
            button.IsVisible.ShouldBeTrue();
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

            invoked.ShouldBe(1);
            commandRan.ShouldBe(1);
            dismissed.ShouldBe(1);
            banner.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RecordingHaptics(List<HapticKind> sink) : IHapticsPlatform
    {
        public void Trigger(HapticKind kind) => sink.Add(kind);
    }

    private sealed class ActionCommand(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }
}
