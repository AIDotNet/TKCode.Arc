using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// SecuritySettings: the protection score follows the safeguards, the password form succeeds and rejects, sessions sign
/// out (one, or every other one) and leave the list, and every time reads "now" from the block's clock.
/// </summary>
public class SecuritySettingsTests
{
    /// <summary>Local noon on Oct 20, 2026, so "2 hours ago" stays on the same local day in every time zone.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0,
        TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Unspecified)));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }

    private static SecuritySession Current() => new()
    {
        Device = "MacBook Air", Client = "Firefox", Location = "Rotterdam, NL", Kind = SecurityDeviceKind.Laptop,
        LastActive = Now, IsCurrent = true
    };

    private static SecuritySession[] Others() =>
    [
        new() { Device = "ThinkPad X1", Client = "Edge", Location = "Utrecht, NL", Kind = SecurityDeviceKind.Laptop, LastActive = Now.AddDays(-4) },
        new() { Device = "Pixel 9", Client = "Larkspur app", Location = "Rotterdam, NL", Kind = SecurityDeviceKind.Phone, LastActive = Now.AddHours(-2) },
        new() { Device = "iPad mini", Client = "Safari", Location = "Ghent, BE", Kind = SecurityDeviceKind.Tablet, LastActive = Now.AddDays(-21) }
    ];

    private static SecuritySettings Create(FixedClock? clock = null, bool withSessions = true)
    {
        var security = new SecuritySettings { Clock = clock ?? new FixedClock(Now), PasswordChangedAt = Now.AddMonths(-3) };
        if (withSessions)
        {
            foreach (var session in Others())
            {
                security.Sessions.Add(session);
            }
            security.Sessions.Insert(1, Current());
        }
        return security;
    }

    private static Window ShowWide(Control content, double width = 900)
    {
        var window = TestHelpers.Show(content);
        window.Width = width;
        window.Height = 1800;
        Settle();
        return window;
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static T Part<T>(Visual root, string name) where T : Control => TestHelpers.Descendant<T>(root, name);

    private static List<SettingsRow> SessionRows(SecuritySettings security) =>
        Part<SettingsGroup>(security, SecuritySettings.SessionsGroupPart).GetVisualDescendants()
            .OfType<SettingsRow>().Where(r => r.Parent is PresenceHost { IsPresent: true }).ToList();

    private static AvaloniaButton SignOutButtonOf(SecuritySettings security, string device) =>
        SessionRows(security).Single(r => r.Header!.StartsWith(device, StringComparison.Ordinal)).Content.ShouldBeAssignableTo<AvaloniaButton>()!;

    private static string? Shown(ArcContentSwap swap) => swap.Content as string;

    [AvaloniaFact]
    public void Protection_Score_Counts_The_Four_Safeguards_And_Follows_The_Switches()
    {
        var security = Create(withSessions: false);
        var window = ShowWide(security);
        try
        {
            // Only the password: warning tone, one segment.
            security.ProtectionScore.ShouldBe(1);
            security.Classes.Contains(SecuritySettings.ToneWarningPseudoClass).ShouldBeTrue();
            security.Classes.Contains(SecuritySettings.PasswordOnPseudoClass).ShouldBeTrue();
            Shown(Part<ArcContentSwap>(security, "PART_SummaryTitle")).ShouldBe("Add another safeguard");
            Shown(Part<ArcContentSwap>(security, "PART_SummaryCount")).ShouldBe("1 of 4 safeguards on");
            Part<Border>(security, "PART_SegmentPassword").Opacity.ShouldBe(1);
            Part<Border>(security, "PART_SegmentTwoStep").Opacity.ShouldBe(0);

            // The authenticator switch turns two-step verification on (two-way).
            var authenticator = Part<ToggleButton>(security, SecuritySettings.AuthenticatorSwitchPart);
            authenticator.IsChecked = true;
            security.IsAuthenticatorEnabled.ShouldBeTrue();
            security.ProtectionScore.ShouldBe(2);
            security.Classes.Contains(SecuritySettings.ToneAccentPseudoClass).ShouldBeTrue();
            Part<Border>(security, "PART_SegmentTwoStep").Opacity.ShouldBe(1);

            security.PasskeyCount = 1;
            security.RecoveryCodesCreatedAt = Now.AddDays(-30);
            security.ProtectionScore.ShouldBe(4);
            security.Classes.Contains(SecuritySettings.ToneSuccessPseudoClass).ShouldBeTrue();
            Shown(Part<ArcContentSwap>(security, "PART_SummaryTitle")).ShouldBe("Your account is well protected");
            AutomationProperties.GetName(Part<StackPanel>(security, "PART_Meter")).ShouldBe("4 of 4 safeguards on");

            // Either two-step method counts; turning both off drops the score again.
            security.IsTextMessageEnabled = true;
            security.ProtectionScore.ShouldBe(4);
            authenticator.IsChecked = false;
            security.ProtectionScore.ShouldBe(4);
            security.IsTextMessageEnabled = false;
            security.ProtectionScore.ShouldBe(3);
            security.HasPassword = false;
            security.ProtectionScore.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Messages_Need_A_Phone_Number_And_Show_Only_Its_Last_Four_Digits()
    {
        var security = Create(withSessions: false);
        var window = ShowWide(security);
        try
        {
            var row = Part<SettingsRow>(security, "PART_TextMessageRow");
            var toggle = Part<ToggleButton>(security, SecuritySettings.TextMessageSwitchPart);
            row.Description.ShouldBe("Add a phone number first");
            toggle.IsEnabled.ShouldBeFalse();

            security.PhoneNumber = "+31 6 2045 4821";
            row.Description.ShouldBe("Codes go to •••• 4821");
            toggle.IsEnabled.ShouldBeTrue();
            SecuritySettings.MaskPhone("(555) 01").ShouldBe("•••• 5501");
            SecuritySettings.MaskPhone("12").ShouldBe("•••• 12");
            SecuritySettings.MaskPhone("none").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Two_Step_Rows_Describe_Passkeys_And_Recovery_Codes_And_Raise_Requests()
    {
        var security = Create(withSessions: false);
        var passkeyRequests = 0;
        var codeRequests = 0;
        security.AddPasskeyRequested += (_, _) => passkeyRequests++;
        security.RecoveryCodesRequested += (_, _) => codeRequests++;
        var window = ShowWide(security);
        try
        {
            Part<SettingsRow>(security, "PART_PasskeysRow").Description.ShouldBe("No passkeys yet");
            Part<SettingsRow>(security, "PART_RecoveryRow").Description.ShouldBe("Not created yet");
            var codes = Part<AvaloniaButton>(security, SecuritySettings.RecoveryCodesPart);
            codes.Content.ShouldBe("Create codes");

            security.PasskeyCount = 2;
            security.RecoveryCodesCreatedAt = new DateTimeOffset(2026, 6, 2, 12, 0, 0, Now.Offset);
            Part<SettingsRow>(security, "PART_PasskeysRow").Description.ShouldBe("2 passkeys");
            Part<SettingsRow>(security, "PART_RecoveryRow").Description.ShouldBe("Created Jun 2, 2026");
            codes.Content.ShouldBe("View codes");

            Part<AvaloniaButton>(security, SecuritySettings.AddPasskeyPart).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            codes.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            passkeyRequests.ShouldBe(1);
            codeRequests.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Password_Row_Reads_The_Change_Date_From_The_Clock()
    {
        var clock = new FixedClock(Now);
        var security = Create(clock, withSessions: false);
        var window = ShowWide(security);
        try
        {
            var row = Part<SettingsRow>(security, SecuritySettings.PasswordRowPart);
            row.Description.ShouldBe("Last changed 3 months ago");
            security.PasswordChangedAt = Now.AddDays(-3);
            row.Description.ShouldBe("Last changed 3 days ago");
            security.PasswordChangedAt = Now.AddDays(-1);
            row.Description.ShouldBe("Last changed yesterday");
            security.PasswordChangedAt = Now.AddSeconds(-20);
            row.Description.ShouldBe("Updated just now");

            // A later clock moves the text on.
            clock.Now = Now.AddMinutes(5);
            security.Clock = clock;
            row.Description.ShouldBe("Last changed 5 min ago");

            security.PasswordChangedAt = null;
            row.Description.ShouldBe("Never changed");
            security.HasPassword = false;
            row.Description.ShouldBe("Not set. You sign in with a passkey or an email link.");
            Part<AvaloniaButton>(security, SecuritySettings.ChangePasswordPart).Content.ShouldBe("Set password");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Password_Submits_With_Enter_Closes_The_Form_And_Stamps_The_Clock()
    {
        var security = Create(withSessions: false);
        SecurityPasswordChangeEventArgs? request = null;
        security.PasswordChangeRequested += (_, e) => request = e;
        var window = ShowWide(security);
        try
        {
            var change = Part<AvaloniaButton>(security, SecuritySettings.ChangePasswordPart);
            change.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);

            security.IsPasswordFormOpen.ShouldBeTrue();
            Part<SpringSizeDecorator>(security, SecuritySettings.PasswordFormHostPart).IsExpanded.ShouldBeTrue();
            var current = Part<PasswordField>(security, SecuritySettings.CurrentPasswordPart);
            var next = Part<PasswordStrength>(security, SecuritySettings.NewPasswordPart);
            var update = Part<AvaloniaButton>(security, SecuritySettings.UpdatePasswordPart);
            current.IsFocused.ShouldBeTrue();
            change.IsEnabled.ShouldBeFalse();
            update.IsEnabled.ShouldBeFalse();

            current.Text = "correct horse";
            next.Text = "Larkspur-2026!";
            Dispatcher.UIThread.RunJobs();
            next.Level.ShouldBeGreaterThanOrEqualTo(SecuritySettings.MinimumPasswordLevel);
            update.IsEnabled.ShouldBeTrue();

            current.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);

            request.ShouldNotBeNull();
            request.CurrentPassword.ShouldBe("correct horse");
            request.NewPassword.ShouldBe("Larkspur-2026!");
            security.IsPasswordFormOpen.ShouldBeFalse();
            security.PasswordChangedAt.ShouldBe(Now);
            Part<SettingsRow>(security, SecuritySettings.PasswordRowPart).Description.ShouldBe("Updated just now");
            change.IsEnabled.ShouldBeTrue();
            change.IsFocused.ShouldBeTrue();
            Settle();
            Part<Control>(security, SecuritySettings.PasswordFormPart).IsVisible.ShouldBeFalse();
            current.Text.ShouldBeNullOrEmpty();
            next.Text.ShouldBeNullOrEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Rejected_Change_Shows_The_Error_Under_The_Current_Password_And_Keeps_The_Form_Open()
    {
        var security = Create(withSessions: false);
        var changedAt = security.PasswordChangedAt;
        security.PasswordChangeRequested += (_, e) =>
        {
            if (e.CurrentPassword != "correct horse")
            {
                e.ErrorMessage = "That isn't your current password.";
            }
        };
        var window = ShowWide(security);
        try
        {
            security.OpenPasswordForm();
            var current = Part<PasswordField>(security, SecuritySettings.CurrentPasswordPart);
            var next = Part<PasswordStrength>(security, SecuritySettings.NewPasswordPart);
            current.Text = "wrong horse";
            next.Text = "Larkspur-2026!";
            Dispatcher.UIThread.RunJobs();
            Part<AvaloniaButton>(security, SecuritySettings.UpdatePasswordPart).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));

            security.IsPasswordFormOpen.ShouldBeTrue();
            current.ErrorMessage.ShouldBe("That isn't your current password.");
            current.IsFocused.ShouldBeTrue();
            security.PasswordChangedAt.ShouldBe(changedAt);

            // Typing again clears the error; the right password then goes through.
            current.Text = "correct horse";
            current.ErrorMessage.ShouldBeNull();
            security.SubmitPasswordChange();
            security.IsPasswordFormOpen.ShouldBeFalse();
            security.PasswordChangedAt.ShouldBe(Now);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Update_Needs_The_Current_Password_And_A_New_One_Of_At_Least_Fair_Strength()
    {
        var security = Create(withSessions: false);
        var requests = 0;
        security.PasswordChangeRequested += (_, _) => requests++;
        var window = ShowWide(security);
        try
        {
            security.OpenPasswordForm();
            var current = Part<PasswordField>(security, SecuritySettings.CurrentPasswordPart);
            var next = Part<PasswordStrength>(security, SecuritySettings.NewPasswordPart);
            var update = Part<AvaloniaButton>(security, SecuritySettings.UpdatePasswordPart);

            next.Text = "Larkspur-2026!";
            Dispatcher.UIThread.RunJobs();
            update.IsEnabled.ShouldBeFalse();

            current.Text = "correct horse";
            next.Text = "larkspur";
            Dispatcher.UIThread.RunJobs();
            next.Level.ShouldBeLessThan(SecuritySettings.MinimumPasswordLevel);
            update.IsEnabled.ShouldBeFalse();
            security.SubmitPasswordChange();
            requests.ShouldBe(0);

            // Without a password only the new one is asked for.
            security.HasPassword = false;
            current.IsVisible.ShouldBeFalse();
            next.Text = "Larkspur-2026!";
            Dispatcher.UIThread.RunJobs();
            update.IsEnabled.ShouldBeTrue();
            update.Content.ShouldBe("Set password");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Form_Without_Saving_And_Focus_Returns_To_Change_Password()
    {
        var security = Create(withSessions: false);
        var requests = 0;
        security.PasswordChangeRequested += (_, _) => requests++;
        var window = ShowWide(security);
        try
        {
            security.OpenPasswordForm();
            var current = Part<PasswordField>(security, SecuritySettings.CurrentPasswordPart);
            current.Text = "correct horse";
            current.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);

            security.IsPasswordFormOpen.ShouldBeFalse();
            requests.ShouldBe(0);
            Part<AvaloniaButton>(security, SecuritySettings.ChangePasswordPart).IsFocused.ShouldBeTrue();
            current.Text.ShouldBeNullOrEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sessions_List_The_Current_Device_First_Then_The_Most_Recent_With_Times_From_The_Clock()
    {
        var security = Create();
        var window = ShowWide(security);
        try
        {
            var rows = SessionRows(security);
            rows.Select(r => r.Header).ShouldBe(["MacBook Air · Firefox", "Pixel 9 · Larkspur app", "ThinkPad X1 · Edge", "iPad mini · Safari"]);
            rows.Select(r => r.Description!.Replace(' ', ' '))
                .ShouldBe(["Rotterdam, NL · Active now", "Rotterdam, NL · 2 hours ago", "Utrecht, NL · 4 days ago", "Ghent, BE · 3 weeks ago"]);
            rows[0].Content.ShouldBeOfType<Badge>().Content.ShouldBe("This device");
            AutomationProperties.GetName(SignOutButtonOf(security, "ThinkPad X1")).ShouldBe("Sign out ThinkPad X1");
            rows.Select(r => r.Icon).ShouldBe([ArcIcons.Laptop, ArcIcons.Smartphone, ArcIcons.Laptop, SecuritySettingsGlyphs.Tablet]);

            // Only the first row drops its hairline, though the rows sit in presence hosts.
            TestHelpers.Descendant<Border>(rows[0], "PART_Divider").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Border>(rows[1], "PART_Divider").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Signing_Out_A_Session_Removes_Its_Row_And_The_Session_And_Raises_The_Event()
    {
        var security = Create();
        var signedOut = new List<SecuritySession>();
        security.SessionSignedOut += (_, e) => signedOut.Add(e.Session);
        var window = ShowWide(security);
        try
        {
            var thinkPad = security.Sessions.Single(s => s.Device == "ThinkPad X1");
            var button = SignOutButtonOf(security, "ThinkPad X1");
            button.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            Settle();

            signedOut.ShouldBe([thinkPad]);
            security.Sessions.ShouldNotContain(thinkPad);
            security.Sessions.Count.ShouldBe(3);
            SessionRows(security).Select(r => r.Header).ShouldBe(["MacBook Air · Firefox", "Pixel 9 · Larkspur app", "iPad mini · Safari"]);
            // Focus stays in the list.
            SignOutButtonOf(security, "iPad mini").IsFocused.ShouldBeTrue();

            // The current session cannot be signed out here.
            security.SignOut(security.Sessions.Single(s => s.IsCurrent));
            security.Sessions.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Confirming_Sign_Out_Other_Sessions_Leaves_Only_This_Device()
    {
        var security = Create();
        SecuritySessionsEventArgs? signedOut = null;
        var single = 0;
        security.OtherSessionsSignedOut += (_, e) => signedOut = e;
        security.SessionSignedOut += (_, _) => single++;
        var window = ShowWide(security);
        try
        {
            var group = Part<SettingsGroup>(security, SecuritySettings.SessionsGroupPart);
            var morph = group.HeaderActions.ShouldBeOfType<ConfirmMorph>();
            morph.Label.ShouldBe("Sign out other sessions");
            morph.Prompt.ShouldBe("Sign out 3 sessions?");
            morph.IsEnabled.ShouldBeTrue();

            morph.Ask();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(ConfirmMorph.ArmDelay + TimeSpan.FromMilliseconds(40));
            TestHelpers.Descendant<AvaloniaButton>(morph, "PART_Confirm").RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Settle();

            signedOut.ShouldNotBeNull();
            signedOut.Sessions.Select(s => s.Device).ShouldBe(["Pixel 9", "ThinkPad X1", "iPad mini"]);
            single.ShouldBe(0);
            security.Sessions.ShouldHaveSingleItem().IsCurrent.ShouldBeTrue();
            SessionRows(security).Count.ShouldBe(1);
            group.Description.ShouldBe("Only this device is signed in.");
            morph.State.ShouldBe(ConfirmMorphState.Done);
            morph.IsEnabled.ShouldBeTrue();

            // Once it rests again there is nothing left to sign out.
            morph.State = ConfirmMorphState.Idle;
            morph.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_Full_Motion_Rows_Leave_First_And_Leave_The_List_After_Their_Exit()
    {
        var security = Create();
        SecuritySessionsEventArgs? others = null;
        security.OtherSessionsSignedOut += (_, e) => others = e;
        var window = TestHelpers.Show(security, global::TKCode.Arc.Motion.ArcMotionLevel.Full);
        try
        {
            window.Width = 900;
            window.Height = 1800;
            WaitFor(() => security.Classes.Contains(":motion-full"), 1000);
            var pixel = security.Sessions.Single(s => s.Device == "Pixel 9");
            var host = (PresenceHost)SessionRows(security).Single(r => r.Header!.StartsWith("Pixel", StringComparison.Ordinal)).Parent!;
            security.SignOut(pixel);
            // The row is still listed while it fades and collapses.
            host.IsPresent.ShouldBeFalse();
            security.Sessions.ShouldContain(pixel);
            WaitFor(() => !security.Sessions.Contains(pixel)).ShouldBeTrue();

            security.SignOutOtherSessions();
            WaitFor(() => others is not null).ShouldBeTrue();
            others!.Sessions.Select(s => s.Device).ShouldBe(["ThinkPad X1", "iPad mini"]);
            security.Sessions.ShouldHaveSingleItem().IsCurrent.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Runs frames (render ticks, jobs and dispatcher timers) until <paramref name="condition"/> holds.</summary>
    private static bool WaitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return true;
            }
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var frame = new DispatcherFrame();
            DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.PushFrame(frame);
        }
        Dispatcher.UIThread.RunJobs();
        return condition();
    }

    [AvaloniaFact]
    public void A_Read_Only_Session_List_Keeps_Its_Items_But_Hides_The_Signed_Out_Row()
    {
        var sessions = Others();
        var security = new SecuritySettings { Clock = new FixedClock(Now), Sessions = sessions };
        SecuritySession? signedOut = null;
        security.SessionSignedOut += (_, e) => signedOut = e.Session;
        var window = ShowWide(security);
        try
        {
            security.SignOut(sessions[0]);
            Settle();
            signedOut.ShouldBe(sessions[0]);
            sessions.Length.ShouldBe(3);
            SessionRows(security).Count.ShouldBe(2);

            // A new list starts fresh.
            security.Sessions = new List<SecuritySession>(sessions);
            Settle();
            SessionRows(security).Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Security_Settings_Are_Collected_While_Their_Sessions_Live_On()
    {
        var sessions = new System.Collections.ObjectModel.ObservableCollection<SecuritySession>(Others());
        var weak = TestHelpers.ShowAndClose(() => new SecuritySettings { Clock = new FixedClock(Now), Sessions = sessions });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        sessions.Add(Current());
    }

    [AvaloniaFact]
    public void Security_Settings_Put_Back_List_The_Sessions_That_Changed_While_They_Were_Away()
    {
        var sessions = new System.Collections.ObjectModel.ObservableCollection<SecuritySession>(Others());
        var security = new SecuritySettings { Clock = new FixedClock(Now), PasswordChangedAt = Now.AddMonths(-3), Sessions = sessions };
        var host = new Border { Child = security };
        var window = ShowWide(host);
        try
        {
            SessionRows(security).Count.ShouldBe(3);
            host.Child = null;
            Settle();
            var iPad = sessions.Single(s => s.Device == "iPad mini");
            sessions.Remove(iPad);
            sessions.Add(Current());

            host.Child = security;
            Settle();
            SessionRows(security).Select(r => r.Header).ShouldBe(["MacBook Air · Firefox", "Pixel 9 · Larkspur app", "ThinkPad X1 · Edge"]);

            // Back in the window, it listens again.
            sessions.Add(iPad);
            Settle();
            SessionRows(security).Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Session_List_Shows_A_Note()
    {
        var security = Create(withSessions: false);
        var window = ShowWide(security);
        try
        {
            var group = Part<SettingsGroup>(security, SecuritySettings.SessionsGroupPart);
            group.Items.ShouldHaveSingleItem().ShouldBeOfType<SettingsRow>().Description.ShouldBe("No sessions to show.");
            Part<SettingsGroup>(security, SecuritySettings.SessionsGroupPart).HeaderActions.ShouldBeOfType<ConfirmMorph>().IsEnabled.ShouldBeFalse();

            security.Sessions.Add(Current());
            Settle();
            group.Items.ShouldHaveSingleItem().ShouldBeOfType<PresenceHost>();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Page_Moves_The_Meter_Under_The_Text_And_Sign_Out_Others_Under_The_List()
    {
        var security = Create();
        var window = ShowWide(security);
        try
        {
            var group = Part<SettingsGroup>(security, SecuritySettings.SessionsGroupPart);
            security.Classes.Contains(SecuritySettings.CompactPseudoClass).ShouldBeFalse();
            group.HeaderActions.ShouldBeOfType<ConfirmMorph>();
            Grid.GetColumn(Part<StackPanel>(security, "PART_Meter")).ShouldBe(2);

            window.Width = 420;
            Settle();
            security.Classes.Contains(SecuritySettings.CompactPseudoClass).ShouldBeTrue();
            group.HeaderActions.ShouldBeNull();
            group.Footer.ShouldBeOfType<ConfirmMorph>().Prompt.ShouldBe("Sign out all?");
            Grid.GetRow(Part<StackPanel>(security, "PART_Meter")).ShouldBe(1);
            // Rows with a small control keep it beside the text; rows with a button stack.
            SessionRows(security).ShouldAllBe(r => !r.IsStacked);
            Part<SettingsRow>(security, "PART_PasskeysRow").IsStacked.ShouldBeTrue();
            security.Classes.Contains(SecuritySettings.RowsStackedPseudoClass).ShouldBeTrue();

            window.Width = 900;
            Settle();
            group.HeaderActions.ShouldBeOfType<ConfirmMorph>().Prompt.ShouldBe("Sign out 3 sessions?");
            group.Footer.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sections_Choose_The_Cards_And_Labels_Replace_Every_Text()
    {
        var security = Create();
        var window = ShowWide(security);
        try
        {
            security.Sections = SecuritySettingsSections.Sessions;
            Part<SettingsGroup>(security, SecuritySettings.SummaryGroupPart).IsVisible.ShouldBeFalse();
            Part<SettingsGroup>(security, SecuritySettings.TwoStepGroupPart).IsVisible.ShouldBeFalse();
            Part<SettingsGroup>(security, SecuritySettings.SessionsGroupPart).IsVisible.ShouldBeTrue();
            security.Sections = SecuritySettingsSections.All;
            security.IsSummaryVisible = false;
            Part<SettingsGroup>(security, SecuritySettings.SummaryGroupPart).IsVisible.ShouldBeFalse();

            security.Labels = new SecuritySettingsLabels
            {
                PasswordHeader = "Wachtwoord",
                SessionsHeader = "Waar je bent ingelogd",
                SignOut = "Afmelden",
                SignOutSession = device => $"{device} afmelden",
                SummaryCount = count => $"{count} van 4"
            };
            Part<SettingsRow>(security, SecuritySettings.PasswordRowPart).Header.ShouldBe("Wachtwoord");
            Part<SettingsGroup>(security, SecuritySettings.SessionsGroupPart).Header.ShouldBe("Waar je bent ingelogd");
            var button = SignOutButtonOf(security, "Pixel 9");
            button.Content.ShouldBe("Afmelden");
            AutomationProperties.GetName(button).ShouldBe("Pixel 9 afmelden");
            Shown(Part<ArcContentSwap>(security, "PART_SummaryCount")).ShouldBe("1 van 4");
        }
        finally
        {
            window.Close();
        }
    }
}
