using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Auth;

/// <summary>Magic link sign-in: address checks, the sent state and its countdown, resend, a failing sender and the expired state.</summary>
public class MagicLinkSignInTests
{
    private static DateTimeOffset s_now;

    private static (MagicLinkSignIn SignIn, Window Window) Show()
    {
        s_now = new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
        var signIn = new MagicLinkSignIn { Clock = () => s_now };
        var window = TestHelpers.Show(signIn);
        Settle();
        return (signIn, window);
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(MagicLinkSignIn signIn, string part)
    {
        TestHelpers.Descendant<AvaloniaButton>(signIn, part).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Settle();
    }

    [Theory]
    [InlineData("mara@lumenfield.example", true)]
    [InlineData(" mara@lumenfield.example ", true)]
    [InlineData("mara@", false)]
    [InlineData("mara@lumenfield", false)]
    [InlineData("@lumenfield.example", false)]
    [InlineData("ma ra@lumenfield.example", false)]
    [InlineData("a@b@c.example", false)]
    public void Validates_Addresses(string email, bool valid) => MagicLinkSignIn.IsValidEmail(email).ShouldBe(valid);

    [AvaloniaFact]
    public void Invalid_Address_Shows_An_Error_And_Sends_Nothing()
    {
        var (signIn, window) = Show();
        try
        {
            var requests = 0;
            signIn.LinkRequested += (_, _) => requests++;
            signIn.Email = "mara@";
            Click(signIn, MagicLinkSignIn.SendButtonPart);
            requests.ShouldBe(0);
            signIn.State.ShouldBe(MagicLinkSignInState.Email);
            TestHelpers.Descendant<Input>(signIn, MagicLinkSignIn.EmailPart).IsInvalid.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sending_Shows_The_Inbox_State_With_A_Countdown_Then_Resend_Works()
    {
        var (signIn, window) = Show();
        try
        {
            var requests = new List<MagicLinkSignInLinkEventArgs>();
            signIn.LinkRequested += (_, e) => requests.Add(e);
            signIn.Email = "mara@lumenfield.example";
            TestHelpers.Descendant<Input>(signIn, MagicLinkSignIn.EmailPart).Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            signIn.State.ShouldBe(MagicLinkSignInState.Sent);
            requests.Single().IsResend.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(signIn, MagicLinkSignIn.SentDescriptionPart).Inlines!.Count.ShouldBe(3);

            var resend = TestHelpers.Descendant<AvaloniaButton>(signIn, MagicLinkSignIn.ResendButtonPart);
            resend.IsEnabled.ShouldBeFalse();
            signIn.ResendRemaining.ShouldBe(TimeSpan.FromSeconds(30));
            TestHelpers.Descendant<TextBlock>(signIn, MagicLinkSignIn.ResendTextPart).Text.ShouldBe("Resend in 0:30");

            s_now += TimeSpan.FromSeconds(12);
            signIn.UpdateResend();
            TestHelpers.Descendant<TextBlock>(signIn, MagicLinkSignIn.ResendTextPart).Text.ShouldBe("Resend in 0:18");
            signIn.RequestLinkAsync().Result.ShouldBeFalse();

            s_now += TimeSpan.FromSeconds(20);
            signIn.UpdateResend();
            resend.IsEnabled.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(signIn, MagicLinkSignIn.ResendTextPart).Text.ShouldBe("Resend link");
            Click(signIn, MagicLinkSignIn.ResendButtonPart);
            requests.Count.ShouldBe(2);
            requests[1].IsResend.ShouldBeTrue();
            requests[1].Email.ShouldBe("mara@lumenfield.example");
            resend.IsEnabled.ShouldBeFalse();

            Click(signIn, MagicLinkSignIn.ChangeButtonPart);
            signIn.State.ShouldBe(MagicLinkSignInState.Email);
            signIn.Email.ShouldBe("mara@lumenfield.example");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failing_Sender_Keeps_The_Form_And_Says_So()
    {
        var (signIn, window) = Show();
        try
        {
            var source = new TaskCompletionSource<bool>();
            signIn.LinkSender = _ => source.Task;
            signIn.Email = "mara@lumenfield.example";
            Click(signIn, MagicLinkSignIn.SendButtonPart);
            signIn.IsSending.ShouldBeTrue();
            ((Button)TestHelpers.Descendant<AvaloniaButton>(signIn, MagicLinkSignIn.SendButtonPart)).IsLoading.ShouldBeTrue();
            source.SetResult(false);
            Settle();
            signIn.IsSending.ShouldBeFalse();
            signIn.State.ShouldBe(MagicLinkSignInState.Email);
            TestHelpers.Descendant<Input>(signIn, MagicLinkSignIn.EmailPart).ErrorMessage.ShouldBe(signIn.Labels.SendFailed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expired_State_Sends_A_Fresh_Link()
    {
        var (signIn, window) = Show();
        try
        {
            var states = new List<MagicLinkSignInState>();
            signIn.StateChanged += (_, e) => states.Add(e.NewState);
            signIn.Email = "mara@lumenfield.example";
            signIn.ShowExpired();
            Settle();
            signIn.State.ShouldBe(MagicLinkSignInState.Expired);
            MagicLinkSignInLinkEventArgs? request = null;
            signIn.LinkRequested += (_, e) => request = e;
            Click(signIn, MagicLinkSignIn.SendNewButtonPart);
            request.ShouldNotBeNull();
            request!.IsResend.ShouldBeTrue();
            signIn.State.ShouldBe(MagicLinkSignInState.Sent);
            states.ShouldBe([MagicLinkSignInState.Expired, MagicLinkSignInState.Sent]);
        }
        finally
        {
            window.Close();
        }
    }
}
