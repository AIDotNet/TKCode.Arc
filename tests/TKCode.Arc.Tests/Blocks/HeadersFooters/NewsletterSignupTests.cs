using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.HeadersFooters;

public class NewsletterSignupTests
{
    private static Window Show(Control control, double width = 1000, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static NewsletterEmailBox Input(NewsletterSignup s) => s.Form!.Input!;

    private static NewsletterSubmitButton Submit(NewsletterSignup s) => s.Form!.Submit!;

    private static NewsletterMessageLine Message(NewsletterSignup s) => s.Form!.Message!;

    private static void Type(NewsletterSignup s, string text)
    {
        Input(s).Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Enter(Window window, NewsletterSignup s)
    {
        Input(s).Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class Recorder : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_Show_Arcs_Preview()
    {
        var signup = new NewsletterSignup();
        var window = Show(signup);
        try
        {
            signup.Variant.ShouldBe(NewsletterVariant.Inline);
            signup.Phase.ShouldBe(NewsletterPhase.Idle);
            signup.DisplayTitle.ShouldBe("Five minutes on interface craft, every Friday");
            signup.ReaderCount.ShouldBe(12480);
            Input(signup).PlaceholderText.ShouldBe("you@company.com");
            Submit(signup).CurrentText.ShouldBe("Subscribe");
            Message(signup).VisibleText.ShouldBe("No tracking pixels. Unsubscribe with one click. Privacy policy");
            var cards = signup.Stack!.Cards;
            cards.Select(c => c.Issue!.Number).ShouldBe([148, 147, 146]);
            cards.Select(c => c.Depth).ShouldBe([0, 1, 2]);
            cards[0].IssueLabel.ShouldBe("Issue 148");
            signup.Readers!.DisplayCount.ShouldBe(12480);
            signup.Classes.Contains(":card").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Variant_Uses_Its_Copy_And_A_Tray()
    {
        var signup = new NewsletterSignup { Variant = NewsletterVariant.Card };
        var window = Show(signup, 600);
        try
        {
            signup.Classes.Contains(":card").ShouldBeTrue();
            signup.DisplayTitle.ShouldBe("Get the next issue on Friday");
            signup.Stack!.FillHeight.ShouldBeTrue();
            signup.Stack.OpacityMask.ShouldNotBeNull();
            TestHelpers.Descendant<Border>(signup, "PART_Tray").Bounds.Height.ShouldBe(248, 0.5);
            signup.TitleFontSize.ShouldBe(28);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1400, 52)]
    [InlineData(1000, 49.6)]
    [InlineData(700, 40)]
    [InlineData(400, 32)]
    public void Title_Size_Follows_The_Blocks_Own_Width(double width, double expected)
    {
        var signup = new NewsletterSignup();
        var window = Show(signup, width);
        try
        {
            signup.TitleFontSize.ShouldBe(expected, 0.05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1000, false, false, false, false)]
    [InlineData(860, true, false, false, false)]
    [InlineData(520, true, true, false, false)]
    [InlineData(400, true, true, true, false)]
    [InlineData(360, true, true, true, true)]
    public void Breakpoints_Set_Pseudo_Classes(double width, bool compact, bool narrow, bool tiny, bool micro)
    {
        var signup = new NewsletterSignup();
        var window = Show(signup, width);
        try
        {
            signup.Classes.Contains(":compact").ShouldBe(compact);
            signup.Classes.Contains(":narrow").ShouldBe(narrow);
            signup.Classes.Contains(":tiny").ShouldBe(tiny);
            signup.Classes.Contains(":micro").ShouldBe(micro);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Form_Narrower_Than_300_Stacks_Field_And_Button()
    {
        var signup = new NewsletterSignup { IsPublicationVisible = false };
        var window = Show(signup, 320);
        try
        {
            signup.Form!.IsStacked.ShouldBeTrue();
            var input = Input(signup);
            var button = Submit(signup);
            input.Bounds.Height.ShouldBe(52, 0.5);
            button.Bounds.Height.ShouldBe(48, 0.5);
            button.Bounds.Width.ShouldBe(input.Bounds.Width, 0.5);
            signup.Form.Pill!.Classes.Contains(":stacked").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        var wide = new NewsletterSignup { IsPublicationVisible = false };
        var window2 = Show(wide, 900);
        try
        {
            wide.Form!.IsStacked.ShouldBeFalse();
            wide.Form.Pill!.Bounds.Height.ShouldBe(58, 0.5);
        }
        finally
        {
            window2.Close();
        }
    }

    [AvaloniaFact]
    public async Task Empty_Submit_Shows_The_Empty_Message_And_Keeps_Focus()
    {
        var signup = new NewsletterSignup();
        var window = Show(signup);
        try
        {
            await Enter(window, signup);
            signup.ProblemText.ShouldBe("Enter your email address");
            Message(signup).Kind.ShouldBe(NewsletterMessageKind.Problem);
            signup.Form!.Pill!.IsInvalid.ShouldBeTrue();
            signup.Phase.ShouldBe(NewsletterPhase.Idle);
            signup.Alert!.History.ShouldContain("Enter your email address");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Validation_Follows_Arcs_Timing()
    {
        var signup = new NewsletterSignup();
        var window = Show(signup);
        try
        {
            // Not tried yet: typing never shows an error.
            Type(signup, "ada");
            signup.ProblemText.ShouldBeNull();
            // Leaving the field with text in it starts validation.
            Input(signup).Focus();
            Submit(signup).Focus();
            Dispatcher.UIThread.RunJobs();
            signup.ProblemText.ShouldBe("Enter an email like name@company.com");
            // From then on every keystroke is checked.
            Type(signup, "ada@example.com");
            signup.ProblemText.ShouldBeNull();
            Type(signup, "");
            signup.ProblemText.ShouldBe("Enter your email address");
            await Task.CompletedTask;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Typing_Before_The_First_Try_Clears_A_Failure()
    {
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => throw new InvalidOperationException("no") };
        var window = Show(signup);
        try
        {
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            signup.ProblemText.ShouldBe("That didn't go through. Your email is kept, so try again.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Successful_Send_Delivers_The_Next_Issue()
    {
        var gate = new TaskCompletionSource();
        var calls = new List<string>();
        var recorder = new Recorder();
        var signup = new NewsletterSignup
        {
            SubscribeAsync = async (email, _) =>
            {
                calls.Add(email);
                await gate.Task;
            },
            SubscribedCommand = recorder
        };
        string? raised = null;
        signup.Subscribed += (_, e) => raised = e.Email;
        var window = Show(signup);
        try
        {
            Type(signup, "  ada@example.com  ");
            await Enter(window, signup);
            signup.Phase.ShouldBe(NewsletterPhase.Sending);
            Input(signup).IsReadOnly.ShouldBeTrue();
            Submit(signup).State.ShouldBe(NewsletterButtonKey.Sending);
            // A second press while sending is ignored.
            await Enter(window, signup);
            calls.ShouldBe(["ada@example.com"]);

            gate.SetResult();
            await Settle();
            signup.Phase.ShouldBe(NewsletterPhase.Done);
            signup.SubscribedEmail.ShouldBe("ada@example.com");
            raised.ShouldBe("ada@example.com");
            recorder.Parameters.ShouldBe(["ada@example.com"]);
            Submit(signup).State.ShouldBe(NewsletterButtonKey.Done);
            Submit(signup).Focusable.ShouldBeFalse();
            signup.Form!.Pill!.IsDone.ShouldBeTrue();
            Message(signup).Kind.ShouldBe(NewsletterMessageKind.Done);
            signup.Announcer!.History.Last().ShouldContain("The link went to ada@example.com.");

            var cards = signup.Stack!.Cards;
            cards.Select(c => c.Issue!.Number).ShouldBe([149, 148, 147]);
            cards[0].RecipientText.ShouldBe("To ada@example.com");
            signup.Stack.ExitingCount.ShouldBe(0);
            signup.Readers!.DisplayCount.ShouldBe(12481);
            signup.Readers.YouHost!.IsExpanded.ShouldBeTrue();
            signup.Classes.Contains(":done").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Failed_Send_Keeps_The_Address_And_Offers_Another_Try()
    {
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => Task.FromException(new InvalidOperationException("down")) };
        var window = Show(signup);
        try
        {
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            signup.Phase.ShouldBe(NewsletterPhase.Idle);
            signup.EmailText.ShouldBe("ada@example.com");
            Input(signup).Text.ShouldBe("ada@example.com");
            Submit(signup).State.ShouldBe(NewsletterButtonKey.Retry);
            Submit(signup).CurrentText.ShouldBe("Try again");
            signup.ProblemText.ShouldBe("That didn't go through. Your email is kept, so try again.");
            signup.Form!.Pill!.IsInvalid.ShouldBeTrue();
            Input(signup).IsInvalid.ShouldBeFalse();
            signup.Stack!.Cards.Select(c => c.Issue!.Number).ShouldBe([148, 147, 146]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Reset_Starts_Over()
    {
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => Task.CompletedTask };
        var window = Show(signup);
        try
        {
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            signup.Phase.ShouldBe(NewsletterPhase.Done);
            signup.Reset();
            await Settle();
            signup.Phase.ShouldBe(NewsletterPhase.Idle);
            signup.SubscribedEmail.ShouldBeNull();
            Input(signup).Text.ShouldBeNullOrEmpty();
            Input(signup).IsReadOnly.ShouldBeFalse();
            signup.Stack!.Cards.Select(c => c.Issue!.Number).ShouldBe([148, 147, 146]);
            signup.Readers!.DisplayCount.ShouldBe(12480);
            Submit(signup).Focusable.ShouldBeTrue();
            Submit(signup).State.ShouldBe(NewsletterButtonKey.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Different_Email_Button_Resets()
    {
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => Task.CompletedTask };
        var window = Show(signup);
        try
        {
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            var again = Message(signup).DifferentEmailButton.ShouldNotBeNull();
            again.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            signup.Phase.ShouldBe(NewsletterPhase.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Button_Width_And_The_Message_Height_Never_Change_With_The_State()
    {
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => Task.CompletedTask };
        var window = Show(signup);
        try
        {
            var width = Submit(signup).Bounds.Width;
            var height = Message(signup).Bounds.Height;
            var formHeight = signup.Form!.Bounds.Height;
            Submit(signup).Sizers.Count.ShouldBe(4);
            Message(signup).Sizers.Count.ShouldBe(5);

            await Enter(window, signup);
            Dispatcher.UIThread.RunJobs();
            Message(signup).Bounds.Height.ShouldBe(height, 0.5);
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            Dispatcher.UIThread.RunJobs();
            Submit(signup).Bounds.Width.ShouldBe(width, 0.5);
            Message(signup).Bounds.Height.ShouldBe(height, 0.5);
            signup.Form.Bounds.Height.ShouldBe(formHeight, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Privacy_Link_Raises_Navigate_And_Runs_The_Command()
    {
        var recorder = new Recorder();
        var signup = new NewsletterSignup { PrivacyLinkHref = "#terms", NavigateCommand = recorder };
        string? href = null;
        signup.Navigate += (_, e) =>
        {
            href = e.Href;
            e.Handled = true;
        };
        var window = Show(signup);
        try
        {
            var link = Message(signup).Link.ShouldNotBeNull();
            link.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            href.ShouldBe("#terms");
            recorder.Parameters.ShouldBe(["#terms"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hiding_The_Stack_And_Readers_Leaves_One_Column()
    {
        var signup = new NewsletterSignup { IsPublicationVisible = false, IsReadersVisible = false, IsPrivacyLinkVisible = false };
        var window = Show(signup, 1000);
        try
        {
            signup.Classes.Contains(":no-aside").ShouldBeTrue();
            signup.Stack!.IsVisible.ShouldBeFalse();
            signup.Readers!.IsVisible.ShouldBeFalse();
            Message(signup).Link.ShouldBeNull();
            TestHelpers.Descendant<Border>(signup, "PART_Section").Bounds.Width.ShouldBe(720, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Stack_Places_Issues_On_Arcs_Steps()
    {
        var signup = new NewsletterSignup();
        var window = Show(signup);
        try
        {
            var stack = signup.Stack!;
            stack.TargetOf(148).ShouldBe((0.0, 1.0, 1.0));
            var second = stack.TargetOf(147).ShouldNotBeNull();
            second.Y.ShouldBe(-12);
            second.Scale.ShouldBe(0.955, 1e-6);
            var third = stack.TargetOf(146).ShouldNotBeNull();
            third.Y.ShouldBe(-24);
            third.Scale.ShouldBe(0.91, 1e-6);
            stack.Sizer.Opacity.ShouldBe(0);
            stack.Cards[0].Classes.Contains(":depth1").ShouldBeFalse();
            stack.Cards[1].Classes.Contains(":depth1").ShouldBeTrue();
            stack.Cards[2].Classes.Contains(":depth2").ShouldBeTrue();
            stack.Cards[0].ZIndex.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Delivery_Moves_The_Stack_On_The_Smooth_Spring_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => Task.CompletedTask };
        var window = Show(signup, 1000, ArcMotionLevel.Full);
        try
        {
            var stack = signup.Stack!;
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            signup.Phase.ShouldBe(NewsletterPhase.Done);
            // The upcoming issue starts below, invisible; the oldest one is on its way out.
            var arriving = stack.CurrentOf(149).ShouldNotBeNull();
            arriving.Y.ShouldBe(56, 0.5);
            arriving.Opacity.ShouldBe(0, 0.01);
            stack.ExitingCount.ShouldBe(1);
            stack.TargetOf(146).ShouldNotBeNull().Y.ShouldBe(-36, 0.01);
            stack.TargetOf(146).ShouldNotBeNull().Scale.ShouldBe(1 - 3 * 0.045, 1e-6);
            stack.TargetOf(149).ShouldNotBeNull().Y.ShouldBe(0, 0.01);
            stack.TargetOf(148).ShouldNotBeNull().Y.ShouldBe(-12, 0.01);

            clock.Advance(TimeSpan.FromMilliseconds(100));
            stack.CurrentOf(149)!.Value.Y.ShouldBeInRange(1, 55);
            stack.CurrentOf(149)!.Value.Opacity.ShouldBeInRange(0.2, 1);

            clock.Advance(TimeSpan.FromMilliseconds(1500));
            Dispatcher.UIThread.RunJobs();
            stack.CurrentOf(149)!.Value.Y.ShouldBe(0, 0.05);
            stack.CurrentOf(148)!.Value.Y.ShouldBe(-12, 0.05);
            stack.CurrentOf(148)!.Value.Scale.ShouldBe(0.955, 0.001);
            TestHelpers.ShouldDrain(() => stack.ExitingCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Delivery_Only_Fades_Under_Reduced_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => Task.CompletedTask };
        var window = Show(signup, 1000, ArcMotionLevel.Reduced);
        try
        {
            var stack = signup.Stack!;
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            stack.CurrentOf(149)!.Value.Y.ShouldBe(0, 0.01);
            stack.CurrentOf(149)!.Value.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            stack.CurrentOf(149)!.Value.Opacity.ShouldBeInRange(0.05, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            Dispatcher.UIThread.RunJobs();
            stack.CurrentOf(149)!.Value.Opacity.ShouldBe(1, 0.001);
            TestHelpers.ShouldDrain(() => stack.ExitingCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_Invalid_Submit_Shakes_The_Pill_At_Full_Motion_Only()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var signup = new NewsletterSignup();
        var window = Show(signup, 1000, ArcMotionLevel.Full);
        try
        {
            await Enter(window, signup);
            var pill = signup.Form!.Pill!;
            pill.IsShaking.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(72));
            pill.ShakeOffset.ShouldBeLessThan(-1);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            pill.ShakeOffset.ShouldBe(0, 0.01);
            pill.IsShaking.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }

        var reduced = new NewsletterSignup();
        var window2 = Show(reduced, 1000, ArcMotionLevel.Reduced);
        try
        {
            await Enter(window2, reduced);
            reduced.Form!.Pill!.IsShaking.ShouldBeFalse();
        }
        finally
        {
            window2.Close();
        }
    }

    [AvaloniaFact]
    public async Task Including_You_Fades_In_After_Its_Delay()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var signup = new NewsletterSignup { SubscribeAsync = (_, _) => Task.CompletedTask };
        var window = Show(signup, 1000, ArcMotionLevel.Full);
        try
        {
            Type(signup, "ada@example.com");
            await Enter(window, signup);
            await Settle();
            var you = signup.Readers!.You!;
            you.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            you.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(120));
            you.Opacity.ShouldBeGreaterThan(0.05);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            you.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Reader_Faces_Come_From_The_List()
    {
        var signup = new NewsletterSignup();
        var window = Show(signup);
        try
        {
            signup.Readers!.Classes.Contains(":no-faces").ShouldBeTrue();
            signup.ReaderFaces.Add(new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul));
            Dispatcher.UIThread.RunJobs();
            signup.Readers.Faces.ShouldBeSameAs(signup.ReaderFaces);
            signup.Readers.Classes.Contains(":no-faces").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Custom_Publication_Replaces_The_Sample()
    {
        var publication = new NewsletterPublication { Name = "Notes" };
        var issue = new NewsletterIssue { Number = 7, Date = "Monday", Subject = "Hello" };
        issue.Stories.Add(new NewsletterStory { Title = "One", Minutes = 2 });
        publication.Recent.Add(issue);
        var signup = new NewsletterSignup { Publication = publication };
        var window = Show(signup);
        try
        {
            var cards = signup.Stack!.Cards;
            cards.Count.ShouldBe(1);
            cards[0].PublicationName.ShouldBe("Notes");
            cards[0].Rows.Single().TitleText.ShouldBe("One");
            cards[0].Rows.Single().MinutesText.ShouldBe("2 min");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Block_Reports_The_Front_Issue_To_Screen_Readers_Only()
    {
        var signup = new NewsletterSignup();
        var window = Show(signup);
        try
        {
            var cards = signup.Stack!.Cards;
            Avalonia.Automation.AutomationProperties.GetName(cards[0]).ShouldBe("Margins, issue 148");
            Avalonia.Automation.AutomationProperties.GetAccessibilityView(cards[1]).ShouldBe(Avalonia.Automation.AccessibilityView.Raw);
            Avalonia.Automation.AutomationProperties.GetName(Input(signup)).ShouldBe("Email address");
        }
        finally
        {
            window.Close();
        }
    }
}
