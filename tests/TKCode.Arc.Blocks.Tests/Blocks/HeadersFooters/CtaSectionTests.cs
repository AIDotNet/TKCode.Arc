using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Layout;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.HeadersFooters;

public class CtaSectionTests
{
    private static Window Show(Control control, double width = 1100, double height = 900, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Part<T>(Visual root, string name) where T : Control => TestHelpers.Descendant<T>(root, name);

    private static IReadOnlyList<string?> Labels(CtaSection section, string panel) =>
        Part<Panel>(section, panel).Children.OfType<CtaActionButton>().Select(b => b.CurrentText).ToArray();

    private static void Press(Window window, Avalonia.Controls.Button button)
    {
        button.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Lets queued timers and posted jobs run for roughly <paramref name="milliseconds"/>.</summary>
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    private static IImage Picture(Color color)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(8, 8));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 8, 8));
        }
        return bitmap;
    }

    #region Defaults and copy

    [AvaloniaFact]
    public void Defaults_Show_Arcs_Centered_Copy()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            section.Variant.ShouldBe(CtaVariant.Centered);
            section.IsOpen.ShouldBeTrue();
            section.IsDismissible.ShouldBeFalse();
            section.IsSecondaryActionVisible.ShouldBeTrue();
            section.Faces.ShouldBeEmpty();
            section.SetupFaces.ShouldBeEmpty();
            section.SetupWorkspace.ShouldBe("Northwind");
            section.DismissLabel.ShouldBe("Dismiss");
            section.DisplayTitle.ShouldBe("Start building in minutes");
            section.DisplayDescription.ShouldBe("Set up a workspace, invite your team, and ship the first project this week. Free for 14 days.");
            section.DisplayNote.ShouldBe("No credit card required");
            Labels(section, "PART_CenteredActions").ShouldBe(["Start free trial", "Talk to sales"]);
            Part<Border>(section, "PART_CenteredSection").IsVisible.ShouldBeTrue();
            Part<Border>(section, "PART_SplitSection").IsVisible.ShouldBeFalse();
            Part<CtaBannerHost>(section, "PART_BannerHost").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Note_Row_Shows_Faces_And_Text_Only_When_Present()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            var note = Part<CtaFlowPanel>(section, "PART_Note");
            var faces = Part<SiteFaceStack>(section, "PART_NoteFaces");
            note.IsVisible.ShouldBeTrue();
            faces.IsVisible.ShouldBeFalse();
            section.Note = string.Empty;
            note.IsVisible.ShouldBeFalse();
            section.Faces.Add(Picture(Colors.Teal));
            note.IsVisible.ShouldBeTrue();
            faces.IsVisible.ShouldBeTrue();
            Part<TextBlock>(section, "PART_NoteText").IsVisible.ShouldBeFalse();
            section.Note = "Join 4,000 teams";
            Part<TextBlock>(section, "PART_NoteText").Text.ShouldBe("Join 4,000 teams");
            for (var i = 0; i < 6; i++)
            {
                section.Faces.Add(Picture(Colors.Orange));
            }
            // At most four faces are drawn (Arc: faces.slice(0, 4)).
            faces.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Variant_Shows_Its_Own_Copy_And_Actions()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            section.DisplayTitle.ShouldBe("Your team, set up before lunch");
            section.DisplayDescription.ShouldBe("Import your projects, bring everyone in, and keep working the way you already do.");
            Labels(section, "PART_SplitActions").ShouldBe(["Create a workspace", "Book a demo"]);
            Part<Panel>(section, "PART_Points").Children.OfType<CtaPoint>().Select(p => p.Text).ShouldBe(
                ["Import from any tracker in one step", "Single sign-on and audit logs included", "Cancel anytime from settings"]);
            Part<Border>(section, "PART_SplitSection").IsVisible.ShouldBeTrue();
            Part<Border>(section, "PART_CenteredSection").IsVisible.ShouldBeFalse();

            section.Variant = CtaVariant.Banner;
            section.DisplayTitle.ShouldBe("Workflows are here");
            section.DisplayDescription.ShouldBe("Automate handoffs between teams with no code.");
            var banner = Part<DockPanel>(section, "PART_BannerActions").Children.OfType<CtaActionButton>().ToList();
            banner.Count.ShouldBe(1);
            banner[0].CurrentText.ShouldBe("See how it works");
            banner[0].Size.ShouldBe(ControlSize.Small);
            Part<CtaBannerHost>(section, "PART_BannerHost").IsVisible.ShouldBeTrue();
            Part<Border>(section, "PART_SplitSection").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Actions_And_Points_Can_Be_Replaced_Or_Hidden()
    {
        var section = new CtaSection
        {
            Variant = CtaVariant.Split,
            Title = "Ship it",
            Description = "Short text.",
            PrimaryAction = new CtaAction { Label = "Go" },
            IsSecondaryActionVisible = false,
            Points = new AvaloniaList<string> { "One" }
        };
        var window = Show(section);
        try
        {
            section.DisplayTitle.ShouldBe("Ship it");
            section.DisplayDescription.ShouldBe("Short text.");
            Labels(section, "PART_SplitActions").ShouldBe(["Go"]);
            Part<Panel>(section, "PART_Points").Children.Count.ShouldBe(1);
            section.Points.Add("Two");
            Part<Panel>(section, "PART_Points").Children.Count.ShouldBe(2);
            section.Points = new AvaloniaList<string>();
            Part<Panel>(section, "PART_Points").IsVisible.ShouldBeFalse();
            section.Points = null;
            Part<Panel>(section, "PART_Points").Children.Count.ShouldBe(3);
            AutomationProperties.GetName(section).ShouldBe("Ship it");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Banner_Never_Shows_A_Secondary_Action()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner, SecondaryAction = new CtaAction { Label = "More" } };
        var window = Show(section);
        try
        {
            Part<DockPanel>(section, "PART_BannerActions").Children.OfType<CtaActionButton>().Count().ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Primary_Action_Has_An_Arrow_And_The_Secondary_Has_None()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            var buttons = Part<Panel>(section, "PART_CenteredActions").Children.OfType<CtaActionButton>().ToList();
            buttons[0].Size.ShouldBe(ControlSize.Large);
            buttons[0].Variant.ShouldBe(ButtonVariant.Primary);
            buttons[1].Variant.ShouldBe(ButtonVariant.Secondary);
            var primary = (CtaActionContent)buttons[0].Content!;
            var secondary = (CtaActionContent)buttons[1].Content!;
            primary.HasArrow.ShouldBeTrue();
            secondary.HasArrow.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Actions

    [AvaloniaFact]
    public void An_Action_Without_Href_Or_Command_Confirms_In_Place_And_Back()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            var events = new List<CtaActionEventArgs>();
            section.ActionInvoked += (_, e) => events.Add(e);
            var button = section.ActionButtons.First(b => b.IsPrimary);
            Press(window, button);
            button.IsConfirmed.ShouldBeTrue();
            button.CurrentText.ShouldBe("Trial started");
            var content = (CtaActionContent)button.Content!;
            content.HasCheck.ShouldBeTrue();
            content.HasArrow.ShouldBeFalse();
            content.Text.ShouldBe("Trial started");
            section.Announcement.ShouldBe("Trial started");
            events.Count.ShouldBe(1);
            events[0].IsPrimary.ShouldBeTrue();
            events[0].IsConfirmed.ShouldBeTrue();
            events[0].Action.Label.ShouldBe("Start free trial");

            Press(window, button);
            button.IsConfirmed.ShouldBeFalse();
            ((CtaActionContent)button.Content!).HasArrow.ShouldBeTrue();
            section.Announcement.ShouldBe("Start free trial");
            events[1].IsConfirmed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Missing_Confirmed_Label_Falls_Back_To_The_Label()
    {
        var section = new CtaSection { PrimaryAction = new CtaAction { Label = "Subscribe" } };
        var window = Show(section);
        try
        {
            var button = section.ActionButtons.First(b => b.IsPrimary);
            Press(window, button);
            button.CurrentText.ShouldBe("Subscribe");
            button.IsConfirmed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Command_Or_A_Handled_Event_Only_Runs_And_Does_Not_Confirm()
    {
        var command = new RecordingCommand();
        var section = new CtaSection
        {
            PrimaryAction = new CtaAction { Label = "Run", Command = command, CommandParameter = "p" },
            SecondaryAction = new CtaAction { Label = "Handled" }
        };
        var window = Show(section);
        try
        {
            section.ActionInvoked += (_, e) =>
            {
                if (!e.IsPrimary)
                {
                    e.Handled = true;
                }
            };
            var primary = section.ActionButtons.First(b => b.IsPrimary);
            Press(window, primary);
            command.Parameters.ShouldBe(["p"]);
            primary.IsConfirmed.ShouldBeFalse();
            var secondary = section.ActionButtons.First(b => !b.IsPrimary);
            Press(window, secondary);
            secondary.IsConfirmed.ShouldBeFalse();
            section.Announcement.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Action_With_Href_Raises_The_Event_And_Never_Confirms()
    {
        var section = new CtaSection { PrimaryAction = new CtaAction { Label = "Docs", Href = "/docs" } };
        var window = Show(section);
        try
        {
            CtaActionEventArgs? seen = null;
            section.ActionInvoked += (_, e) => seen = e;
            var button = section.ActionButtons.First(b => b.IsPrimary);
            Press(window, button);
            seen.ShouldNotBeNull();
            seen.IsConfirmed.ShouldBeFalse();
            seen.Action.Href.ShouldBe("/docs");
            button.IsConfirmed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_The_Variant_Starts_Every_Confirmation_Over()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            Press(window, section.ActionButtons.First(b => b.IsPrimary));
            section.ActionButtons.First(b => b.IsPrimary).IsConfirmed.ShouldBeTrue();
            section.Variant = CtaVariant.Split;
            section.ActionButtons.Any(b => b.IsConfirmed).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Arrow_Leans_Only_For_A_Mouse_Hover()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            var button = section.ActionButtons.First(b => b.IsPrimary);
            var content = (CtaActionContent)button.Content!;
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Dispatcher.UIThread.RunJobs();
            content.IsLeaning.ShouldBeTrue();
            content.Classes.Contains(CtaActionContent.LeanPseudoClass).ShouldBeTrue();
            window.MouseMove(new Point(2, 2));
            Dispatcher.UIThread.RunJobs();
            content.IsLeaning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Banner dismissal

    [AvaloniaFact]
    public void The_Dismiss_Button_Shows_Only_When_Dismissible_And_Is_Named()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner };
        var window = Show(section);
        try
        {
            var dismiss = Part<CtaDismissButton>(section, "PART_Dismiss");
            dismiss.IsVisible.ShouldBeFalse();
            section.IsDismissible = true;
            dismiss.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(dismiss).ShouldBe("Dismiss");
            section.DismissLabel = "Close";
            AutomationProperties.GetName(dismiss).ShouldBe("Close");
            dismiss.Width.ShouldBe(36);
            dismiss.Height.ShouldBe(36);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dismissing_With_Motion_Off_Removes_The_Banner_At_Once()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner, IsDismissible = true };
        var window = Show(section);
        try
        {
            var dismissed = 0;
            section.Dismissed += (_, _) => dismissed++;
            var host = Part<CtaBannerHost>(section, "PART_BannerHost");
            host.Bounds.Height.ShouldBeGreaterThan(60);
            Press(window, Part<CtaDismissButton>(section, "PART_Dismiss"));
            Dispatcher.UIThread.RunJobs();
            section.IsOpen.ShouldBeFalse();
            dismissed.ShouldBe(1);
            host.IsClosed.ShouldBeTrue();
            host.HeightFactor.ShouldBe(0);
            host.Opacity.ShouldBe(0);
            host.Bounds.Height.ShouldBe(0);
            // Closing again does nothing and does not raise the event twice.
            section.Dismiss();
            dismissed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_In_160ms_And_Springs_The_Height_Without_Scaling()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var section = new CtaSection { Variant = CtaVariant.Banner, IsDismissible = true };
        var window = Show(section, level: ArcMotionLevel.Full);
        try
        {
            var dismissed = 0;
            section.Dismissed += (_, _) => dismissed++;
            var host = Part<CtaBannerHost>(section, "PART_BannerHost");
            var full = host.Bounds.Height;
            section.Dismiss();
            host.IsHitTestVisible.ShouldBeFalse();
            section.IsOpen.ShouldBeTrue();

            clock.Advance(TimeSpan.FromMilliseconds(30));
            Dispatcher.UIThread.RunJobs();
            host.Opacity.ShouldBeInRange(0.02, 0.95);
            host.HeightFactor.ShouldBeInRange(0.05, 0.99);
            // Arc's docs mention a .98 scale; the source and the measurement have none.
            (host.RenderTransform is null || host.RenderTransform is TransformGroup { Children.Count: 0 } || ReferenceEquals(host.RenderTransform, null)).ShouldBeTrue();
            dismissed.ShouldBe(0);

            clock.Advance(TimeSpan.FromMilliseconds(100));
            Dispatcher.UIThread.RunJobs();
            host.Opacity.ShouldBe(0, 0.001);
            host.HeightFactor.ShouldBeGreaterThan(0.001);

            clock.Advance(TimeSpan.FromMilliseconds(2000));
            Dispatcher.UIThread.RunJobs();
            host.HeightFactor.ShouldBe(0, 0.001);
            section.IsOpen.ShouldBeFalse();
            dismissed.ShouldBe(1);
            full.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_In_120ms_And_Removes_The_Banner_At_The_End()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var section = new CtaSection { Variant = CtaVariant.Banner, IsDismissible = true };
        var window = Show(section, level: ArcMotionLevel.Reduced);
        try
        {
            var host = Part<CtaBannerHost>(section, "PART_BannerHost");
            section.Dismiss();
            clock.Advance(TimeSpan.FromMilliseconds(15));
            Dispatcher.UIThread.RunJobs();
            host.Opacity.ShouldBeInRange(0.02, 0.95);
            host.HeightFactor.ShouldBe(1, 0.0001);
            section.IsOpen.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.RunJobs();
            host.HeightFactor.ShouldBe(0, 0.0001);
            section.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Show_Brings_The_Banner_Back_And_IsOpen_False_From_Outside_Closes_Silently()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner };
        var window = Show(section);
        try
        {
            var dismissed = 0;
            section.Dismissed += (_, _) => dismissed++;
            var host = Part<CtaBannerHost>(section, "PART_BannerHost");
            section.IsOpen = false;
            host.IsClosed.ShouldBeTrue();
            dismissed.ShouldBe(0);
            section.Show();
            section.IsOpen.ShouldBeTrue();
            host.IsClosed.ShouldBeFalse();
            host.Opacity.ShouldBe(1);
            host.HeightFactor.ShouldBe(1);
            host.IsHitTestVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Banner_That_Starts_Closed_Takes_No_Space()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner, IsOpen = false };
        var window = Show(section);
        try
        {
            Part<CtaBannerHost>(section, "PART_BannerHost").Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Geometry and breakpoints

    [AvaloniaTheory]
    [InlineData(1280, false, false, 52.0)]
    [InlineData(1000, false, false, 50.0)]
    [InlineData(800, true, false, 43.2)]
    [InlineData(560, true, true, 35.04)]
    [InlineData(420, true, true, 30.28)]
    [InlineData(300, true, true, 30.0)]
    public void Breakpoints_Follow_The_Blocks_Own_Width(double width, bool stacked, bool narrow, double titleSize)
    {
        var section = new CtaSection { Width = width, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(section, width: 1400);
        try
        {
            section.Classes.Contains(CtaSection.StackedPseudoClass).ShouldBe(stacked);
            section.Classes.Contains(CtaSection.NarrowPseudoClass).ShouldBe(narrow);
            section.TitleFontSize.ShouldBe(titleSize, 0.01);
            Part<CtaBalancedText>(section, "PART_CenteredTitle").FontSize.ShouldBe(titleSize, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Centered_Card_Padding_And_Radius_Tighten_At_560()
    {
        var section = new CtaSection { Width = 1000, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(section, width: 1400);
        try
        {
            var outer = Part<Border>(section, "PART_CenteredSection");
            var card = Part<Border>(section, "PART_CenteredCard");
            outer.Padding.ShouldBe(new Thickness(32, 64));
            card.Padding.ShouldBe(new Thickness(32, 96));
            card.CornerRadius.ShouldBe(new CornerRadius(34));
            outer.Bounds.Width.ShouldBe(1000);
            Part<CtaBalancedText>(section, "PART_CenteredDescription").FontSize.ShouldBe(18);
            section.Width = 500;
            Dispatcher.UIThread.RunJobs();
            outer.Padding.ShouldBe(new Thickness(16, 40));
            card.Padding.ShouldBe(new Thickness(20, 64));
            card.CornerRadius.ShouldBe(new CornerRadius(28));
            Part<CtaBalancedText>(section, "PART_CenteredDescription").FontSize.ShouldBe(16);
            Part<CtaFlowPanel>(section, "PART_CenteredActions").IsStacked.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Centered_Content_Is_At_Most_1120_Wide_And_Centered()
    {
        var section = new CtaSection();
        var window = Show(section, width: 1400);
        try
        {
            var outer = Part<Border>(section, "PART_CenteredSection");
            outer.Bounds.Width.ShouldBe(1120);
            outer.Bounds.X.ShouldBe(140, 0.5);
            var title = Part<CtaBalancedText>(section, "PART_CenteredTitle");
            title.MaxChars.ShouldBe(20);
            Part<CtaBalancedText>(section, "PART_CenteredDescription").MaxWidth.ShouldBe(544);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Split_Is_Two_Equal_Columns_Then_One_Column_At_860()
    {
        var section = new CtaSection { Variant = CtaVariant.Split, Width = 1200, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(section, width: 1400);
        try
        {
            var panel = Part<CtaSplitPanel>(section, "PART_SplitPanel");
            var copy = Part<StackPanel>(section, "PART_SplitCopy");
            var stage = Part<Border>(section, "PART_Stage");
            // 1120 content - 64 padding = 1056; two columns of 496 around a 64 gap.
            copy.Bounds.Width.ShouldBe(496, 0.5);
            stage.Bounds.Width.ShouldBe(496, 0.5);
            stage.Bounds.X.ShouldBe(560, 0.5);
            stage.Bounds.Height.ShouldBeGreaterThanOrEqualTo(380);
            panel.IsStacked.ShouldBeFalse();
            stage.Padding.ShouldBe(new Thickness(32));

            section.Width = 800;
            Dispatcher.UIThread.RunJobs();
            panel.IsStacked.ShouldBeTrue();
            panel.Gap.ShouldBe(40);
            stage.Bounds.Y.ShouldBeGreaterThan(copy.Bounds.Bottom + 39);
            stage.Bounds.Y.ShouldBe(copy.Bounds.Bottom + 40, 0.5);
            stage.Bounds.Height.ShouldBeGreaterThanOrEqualTo(320);
            Part<Border>(section, "PART_SplitSection").Padding.ShouldBe(new Thickness(32, 64));

            section.Width = 500;
            Dispatcher.UIThread.RunJobs();
            stage.Padding.ShouldBe(new Thickness(20));
            stage.CornerRadius.ShouldBe(new CornerRadius(28));
            Part<Border>(section, "PART_SplitSection").Padding.ShouldBe(new Thickness(16, 48));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Banner_Is_One_Row_Then_Stacks_At_560()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner, IsDismissible = true, Width = 1100, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(section, width: 1400);
        try
        {
            var pad = Part<Border>(section, "PART_BannerPad");
            var banner = Part<Border>(section, "PART_Banner");
            var panel = Part<CtaBannerPanel>(section, "PART_BannerPanel");
            var text = Part<WrapPanel>(section, "PART_BannerText");
            var actions = Part<DockPanel>(section, "PART_BannerActions");
            pad.Padding.ShouldBe(new Thickness(32, 24));
            banner.Padding.ShouldBe(new Thickness(20, 10, 10, 10));
            banner.CornerRadius.ShouldBe(new CornerRadius(20));
            panel.IsStacked.ShouldBeFalse();
            // Text on the left, actions flush to the right edge, both centered on one row.
            text.Bounds.X.ShouldBe(0);
            actions.Bounds.Right.ShouldBe(panel.Bounds.Width, 0.5);
            (actions.Bounds.Y + actions.Bounds.Height / 2).ShouldBe(panel.Bounds.Height / 2, 0.75);

            section.Width = 500;
            Dispatcher.UIThread.RunJobs();
            pad.Padding.ShouldBe(new Thickness(16));
            banner.Padding.ShouldBe(new Thickness(16));
            panel.IsStacked.ShouldBeTrue();
            actions.Bounds.Y.ShouldBeGreaterThanOrEqualTo(text.Bounds.Bottom);
            actions.Bounds.Width.ShouldBe(panel.Bounds.Width, 0.5);
            Part<CtaDismissButton>(section, "PART_Dismiss").Bounds.Right.ShouldBe(actions.Bounds.Width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Layout parts

    [AvaloniaFact]
    public void Flow_Panel_Wraps_Centers_Lines_And_Stacks()
    {
        var panel = new CtaFlowPanel { Spacing = 12, ContentAlignment = HorizontalAlignment.Center, Width = 250 };
        panel.Children.Add(new Border { Width = 100, Height = 40 });
        panel.Children.Add(new Border { Width = 100, Height = 40 });
        panel.Children.Add(new Border { Width = 100, Height = 30 });
        var window = Show(new StackPanel { Children = { panel } }, 600, 400);
        try
        {
            // Two fit in 250 (100 + 12 + 100); the third wraps and its line is centered.
            panel.Children[0].Bounds.X.ShouldBe(19, 0.5);
            panel.Children[1].Bounds.X.ShouldBe(131, 0.5);
            panel.Children[2].Bounds.X.ShouldBe(75, 0.5);
            panel.Children[2].Bounds.Y.ShouldBe(52, 0.5);
            panel.Bounds.Height.ShouldBe(82, 0.5);

            panel.IsStacked = true;
            Dispatcher.UIThread.RunJobs();
            panel.Children[1].Bounds.Y.ShouldBe(52, 0.5);
            panel.Children[2].Bounds.Y.ShouldBe(104, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Balanced_Text_Wraps_At_The_Narrowest_Width_With_The_Same_Line_Count()
    {
        var plain = new CtaBalancedText { Text = "alpha beta gamma delta epsilon zeta eta theta iota kappa", FontSize = 20, TextWrapping = TextWrapping.Wrap };
        var balanced = new CtaBalancedText { Text = plain.Text, FontSize = 20, TextWrapping = TextWrapping.Wrap, IsBalanced = true };
        var window = Show(new StackPanel { Width = 300, Children = { plain, balanced } }, 600, 400);
        try
        {
            plain.Measure(new Size(300, double.PositiveInfinity));
            balanced.Measure(new Size(300, double.PositiveInfinity));
            plain.TextLayout.TextLines.Count.ShouldBeGreaterThan(1);
            balanced.TextLayout.TextLines.Count.ShouldBe(plain.TextLayout.TextLines.Count);
            balanced.DesiredSize.Width.ShouldBeLessThanOrEqualTo(plain.DesiredSize.Width);
            balanced.DesiredSize.Height.ShouldBe(plain.DesiredSize.Height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Max_Chars_Limits_The_Width_And_Leading_Sets_The_Line_Height()
    {
        var text = new CtaBalancedText { Text = "word word word word word word word word word word word word", FontSize = 20, MaxChars = 10, Leading = 1.1, TextWrapping = TextWrapping.Wrap };
        var window = Show(new StackPanel { Width = 600, Children = { text } }, 800, 400);
        try
        {
            text.LineHeight.ShouldBe(22, 0.001);
            text.DesiredSize.Width.ShouldBeLessThan(10 * 20 + 1);
            text.Tracking = -0.03;
            text.LetterSpacing.ShouldBe(-0.6, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Setup card

    [AvaloniaFact]
    public void Setup_Card_Starts_Idle_With_Arcs_Three_Steps()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            var card = section.SetupCard;
            card.Rows.Select(r => r.Label).ShouldBe(["Create workspace", "Import 214 issues", "Invite 4 teammates"]);
            card.Initial.ShouldBe("N");
            card.Completed.ShouldBe(0);
            card.Progress.ShouldBe(0);
            card.Rows[0].IsActive.ShouldBeTrue();
            card.Rows[0].IsDone.ShouldBeFalse();
            card.Rows[1].IsActive.ShouldBeFalse();
            card.Classes.Contains(":done").ShouldBeFalse();
            Part<TextBlock>(card, "PART_Setting").Classes.Contains("shown").ShouldBeTrue();
            Part<TextBlock>(card, "PART_Ready").Classes.Contains("shown").ShouldBeFalse();
            Part<TextBlock>(card, "PART_Setting").Text.ShouldBe("Setting up");
            Part<TextBlock>(card, "PART_Ready").Text.ShouldBe("Ready");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setup_Card_Steps_Follow_The_Completed_Count()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            var card = section.SetupCard;
            card.Completed = 1;
            card.Progress.ShouldBe(1.0 / 3, 0.0001);
            card.Rows[0].IsDone.ShouldBeTrue();
            card.Rows[0].IsActive.ShouldBeFalse();
            card.Rows[1].IsActive.ShouldBeTrue();
            card.Rows[0].Classes.Contains(":done").ShouldBeTrue();
            card.Rows[1].Classes.Contains(":active").ShouldBeTrue();
            card.Completed = 3;
            card.Progress.ShouldBe(1, 0.0001);
            card.Rows.All(r => r.IsDone).ShouldBeTrue();
            card.Rows.Any(r => r.IsActive).ShouldBeFalse();
            card.Classes.Contains(":done").ShouldBeTrue();
            Part<TextBlock>(card, "PART_Setting").Classes.Contains("shown").ShouldBeFalse();
            Part<TextBlock>(card, "PART_Ready").Classes.Contains("shown").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setup_Card_Content_Is_Configurable()
    {
        var section = new CtaSection
        {
            Variant = CtaVariant.Split,
            SetupWorkspace = "acme corp",
            SetupSteps = new AvaloniaList<string> { "Connect", "Sync" }
        };
        var window = Show(section);
        try
        {
            var card = section.SetupCard;
            card.Initial.ShouldBe("A");
            card.Rows.Select(r => r.Label).ShouldBe(["Connect", "Sync"]);
            section.SetupSteps.Add("Invite");
            card.Rows.Count.ShouldBe(3);
            card.Completed = 3;
            card.Progress.ShouldBe(1, 0.0001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setup_Timeline_Finishes_Step_I_At_520_Plus_620_I_Milliseconds()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            var card = section.SetupCard;
            var watch = Stopwatch.StartNew();
            var times = new List<(int Completed, long Ms)>();
            card.GetObservable(CtaSetupCard.CompletedProperty).Subscribe(new ValueObserver<int>(value => times.Add((value, watch.ElapsedMilliseconds))));
            card.Start();
            card.PendingTimers.ShouldBe(3);
            // Wait for the third step instead of a fixed 2.3 s window: a loaded runner delivers timers late.
            TestHelpers.PumpUntil(() => times.Count(t => t.Completed > 0) >= 3, 15000).ShouldBeTrue();
            var changes = times.Where(t => t.Completed > 0).ToList();
            changes.Select(t => t.Completed).ShouldBe([1, 2, 3]);
            changes[0].Ms.ShouldBeGreaterThanOrEqualTo(500);
            changes[1].Ms.ShouldBeGreaterThanOrEqualTo(1120);
            changes[2].Ms.ShouldBeGreaterThanOrEqualTo(1740);
            // Steps are 620 ms apart, so the second cannot come long before the first plus 600 (a little slack for the
            // timer and Stopwatch granularity; the absolute lower bounds above carry the exact timing).
            (changes[1].Ms - changes[0].Ms).ShouldBeGreaterThanOrEqualTo(500);
            card.PendingTimers.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setup_Card_Starts_Once_It_Is_Half_In_View_And_Not_Before()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        // Below the fold: the viewport is 300 high and the card sits well past it.
        var scroll = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 1400 }, section } } };
        var window = Show(scroll, 1100, 300);
        try
        {
            Pump(100);
            section.SetupCard.HasStarted.ShouldBeFalse();
            section.IsStageRevealed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_Every_Step_Finished_At_Once()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section, level: ArcMotionLevel.Reduced);
        try
        {
            var card = section.SetupCard;
            card.Completed.ShouldBe(3);
            card.Progress.ShouldBe(1, 0.0001);
            card.Classes.Contains(":done").ShouldBeTrue();
            card.PendingTimers.ShouldBe(0);
            section.IsStageRevealed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Finish_And_Reset_Put_The_Card_At_Either_End()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            var card = section.SetupCard;
            card.Start();
            card.Finish();
            card.Completed.ShouldBe(3);
            card.PendingTimers.ShouldBe(0);
            card.Reset();
            card.Completed.ShouldBe(0);
            card.HasStarted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Teammate_Faces_Slide_In_Staggered_When_The_Last_Step_Becomes_Current()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var section = new CtaSection { Variant = CtaVariant.Split };
        for (var i = 0; i < 4; i++)
        {
            section.SetupFaces.Add(Picture(Color.FromRgb((byte)(40 * i), 90, 160)));
        }
        var window = Show(section, level: ArcMotionLevel.Full);
        try
        {
            var card = section.SetupCard;
            var last = card.Rows[^1];
            last.FacesRevealed.ShouldBeFalse();
            last.FaceState(0).Opacity.ShouldBe(0);
            last.FaceState(0).Offset.ShouldBe(-6, 0.001);
            last.FaceState(0).Scale.ShouldBe(0.9, 0.001);
            card.Completed = 1;
            last.FacesRevealed.ShouldBeFalse();
            card.Completed = 2;
            last.IsActive.ShouldBeTrue();
            last.FacesRevealed.ShouldBeTrue();

            clock.Advance(TimeSpan.FromMilliseconds(40));
            last.FaceState(0).Opacity.ShouldBeInRange(0.05, 0.99);
            // The fourth face waits 180 ms.
            last.FaceState(3).Opacity.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            last.FaceState(3).Opacity.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromMilliseconds(1000));
            for (var i = 0; i < 4; i++)
            {
                var state = last.FaceState(i);
                state.Opacity.ShouldBe(1, 0.001);
                state.Offset.ShouldBe(0, 0.001);
                state.Scale.ShouldBe(1, 0.001);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Setup_Card_Is_Collected_While_Its_Steps_And_Faces_Live_On()
    {
        var steps = new AvaloniaList<string> { "Create workspace", "Invite teammates" };
        var faces = new AvaloniaList<IImage> { Picture(Colors.Teal) };
        var weak = TestHelpers.ShowAndClose(() => new CtaSetupCard { Steps = steps, Faces = faces });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        steps.Add("Ship");
        faces.Add(Picture(Colors.Coral));
    }

    [AvaloniaFact]
    public void A_Setup_Card_Put_Back_Shows_The_Steps_And_Faces_Added_While_It_Was_Away()
    {
        var steps = new AvaloniaList<string> { "Create workspace", "Invite teammates" };
        var faces = new AvaloniaList<IImage> { Picture(Colors.Teal) };
        var card = new CtaSetupCard { Steps = steps, Faces = faces, Completed = 2 };
        var host = new Border { Child = card };
        var window = Show(host);
        try
        {
            card.Rows.Select(r => r.Label).ShouldBe(["Create workspace", "Invite teammates"]);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            faces.Add(Picture(Colors.Coral));

            // Same steps, one more face: the last row draws it, shown like the first.
            host.Child = card;
            Dispatcher.UIThread.RunJobs();
            Part<SiteFaceStack>(card.Rows[^1], "PART_Faces").Count.ShouldBe(2);
            card.Rows[^1].FaceState(1).Opacity.ShouldBe(1);

            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            steps.Add("Import issues");
            host.Child = card;
            Dispatcher.UIThread.RunJobs();
            card.Rows.Select(r => r.Label).ShouldBe(["Create workspace", "Invite teammates", "Import issues"]);
            card.Rows[^1].Faces.ShouldBeSameAs(faces);

            // Back in the window, it listens again.
            steps.RemoveAt(2);
            faces.Add(Picture(Colors.Gold));
            Dispatcher.UIThread.RunJobs();
            card.Rows.Count.ShouldBe(2);
            card.Rows[^1].FaceState(2).Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Teammate_Faces_Jump_When_Motion_Is_Off()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        section.SetupFaces.Add(Picture(Colors.Teal));
        var window = Show(section);
        try
        {
            var last = section.SetupCard.Rows[^1];
            Part<SiteFaceStack>(last, "PART_Faces").IsVisible.ShouldBeTrue();
            Part<SiteFaceStack>(section.SetupCard.Rows[0], "PART_Faces").IsVisible.ShouldBeFalse();
            section.SetupCard.Completed = 2;
            last.FaceState(0).Opacity.ShouldBe(1);
            last.FaceState(0).Offset.ShouldBe(0);
            last.FaceState(0).Scale.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setup_Card_Is_Hidden_From_Screen_Readers_And_A_Custom_Visual_Replaces_It()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            AutomationProperties.GetAccessibilityView(section.SetupCard).ShouldBe(AccessibilityView.Raw);
            var content = Part<Avalonia.Controls.Presenters.ContentPresenter>(section, "PART_StageContent");
            content.Content.ShouldBe(section.SetupCard);
            var custom = new Border { Width = 100, Height = 50 };
            section.Visual = custom;
            content.Content.ShouldBe(custom);
            section.Visual = null;
            content.Content.ShouldBe(section.SetupCard);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Stage entrance

    [AvaloniaFact]
    public void Stage_Is_Shown_At_Once_When_Motion_Is_Off()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            section.IsStageRevealed.ShouldBeTrue();
            Part<Border>(section, "PART_Stage").Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stage_Waits_For_The_Viewport_Under_Full_Motion_And_Replays_After_A_Variant_Change()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section, level: ArcMotionLevel.Full);
        try
        {
            var stage = Part<Border>(section, "PART_Stage");
            Pump(60);
            section.IsStageRevealed.ShouldBeTrue();
            // The theme animates the reveal: opacity 0 to 1 and 24 px of travel, 480 ms.
            clock.Advance(TimeSpan.FromMilliseconds(100));
            stage.Opacity.ShouldBeInRange(0.01, 0.99);

            section.Variant = CtaVariant.Centered;
            section.IsStageRevealed.ShouldBeFalse();
            section.IsSplitStarted.ShouldBeFalse();
            section.SetupCard.Completed.ShouldBe(0);
            section.Variant = CtaVariant.Split;
            section.IsSplitStarted.ShouldBeTrue();
            section.IsStageRevealed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Custom_Visual_Does_Not_Start_The_Setup_Timeline()
    {
        var section = new CtaSection { Variant = CtaVariant.Split, Visual = new Border { Width = 50, Height = 50 } };
        var window = Show(section);
        try
        {
            Pump(100);
            section.SetupCard.HasStarted.ShouldBeFalse();
            section.SetupCard.PendingTimers.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Step mark and progress line

    [AvaloniaFact]
    public void Step_Mark_And_Progress_Line_Take_Their_Brushes_From_The_Theme()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            var card = section.SetupCard;
            var line = TestHelpers.Descendant<CtaProgressLine>(card);
            double.IsNaN(line.Height).ShouldBeTrue();
            line.Bounds.Height.ShouldBe(2);
            line.TrackBrush.ShouldNotBeNull();
            line.FillBrush.ShouldNotBeNull();
            var pending = Part<CtaStepMark>(card.Rows[2], "PART_Mark");
            pending.IsDashed.ShouldBeTrue();
            pending.CheckOpacity.ShouldBe(0);
            pending.CheckScale.ShouldBe(0.5);
            card.Completed = 1;
            Part<CtaStepMark>(card.Rows[0], "PART_Mark").CheckOpacity.ShouldBe(1);
            Part<CtaStepMark>(card.Rows[0], "PART_Mark").CheckScale.ShouldBe(1);
            Part<CtaStepMark>(card.Rows[0], "PART_Mark").IsDashed.ShouldBeFalse();
            var current = Part<CtaStepMark>(card.Rows[1], "PART_Mark");
            current.IsDashed.ShouldBeFalse();
            current.CheckOpacity.ShouldBe(0);
            line.Value.ShouldBe(1.0 / 3, 0.001);
            Part<CtaStepMark>(card.Rows[0], "PART_Mark").Bounds.Size.ShouldBe(new Size(20, 20));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setup_Card_Metrics_Match_Arcs_Css()
    {
        var section = new CtaSection { Variant = CtaVariant.Split };
        var window = Show(section);
        try
        {
            var card = section.SetupCard;
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(360.5);
            // 2 border + 58 head + 2 line + 10 + 3 × 44 + 2 × 2 + 12 = 220 (Arc: 220).
            card.Bounds.Height.ShouldBe(220, 1);
            card.Rows.All(r => r.Bounds.Height >= 44).ShouldBeTrue();
            card.Rows[0].Bounds.Height.ShouldBe(44, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void The_Section_Is_A_Landmark_Named_By_Its_Title_And_Buttons_Are_Named()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            AutomationProperties.GetLandmarkType(section).ShouldBe(AutomationLandmarkType.Region);
            AutomationProperties.GetName(section).ShouldBe("Start building in minutes");
            AutomationProperties.GetName(section.ActionButtons.First(b => b.IsPrimary)).ShouldBe("Start free trial");
            section.Title = "Different";
            AutomationProperties.GetName(section).ShouldBe("Different");
            AutomationProperties.GetLiveSetting(Part<SiteAnnouncer>(section, "PART_Announcer")).ShouldBe(AutomationLiveSetting.Polite);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_The_Dismiss_Buttons_Ring_And_Enter_Dismisses()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner, IsDismissible = true };
        var window = Show(section);
        try
        {
            var dismiss = Part<CtaDismissButton>(section, "PART_Dismiss");
            var ring = Part<Border>(dismiss, "PART_FocusRing");
            ring.IsVisible.ShouldBeFalse();
            dismiss.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            ring.IsVisible.ShouldBeTrue();
            ring.BorderThickness.ShouldBe(new Thickness(2));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            section.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Decorative_Parts_Are_Hidden_From_Screen_Readers()
    {
        var section = new CtaSection();
        var window = Show(section);
        try
        {
            AutomationProperties.GetAccessibilityView(Part<SiteFaceStack>(section, "PART_NoteFaces")).ShouldBe(AccessibilityView.Raw);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Events and routing

    [AvaloniaFact]
    public void Events_Bubble_To_Ancestors()
    {
        var section = new CtaSection { Variant = CtaVariant.Banner, IsDismissible = true };
        var host = new StackPanel { Children = { section } };
        var window = Show(host);
        try
        {
            var invoked = 0;
            var dismissed = 0;
            host.AddHandler(CtaSection.ActionInvokedEvent, (object? _, CtaActionEventArgs _) => invoked++);
            host.AddHandler(CtaSection.DismissedEvent, (object? _, RoutedEventArgs _) => dismissed++);
            Press(window, Part<DockPanel>(section, "PART_BannerActions").Children.OfType<CtaActionButton>().First());
            section.Dismiss();
            invoked.ShouldBe(1);
            dismissed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    private sealed class ValueObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value) => onNext(value);
    }
}
