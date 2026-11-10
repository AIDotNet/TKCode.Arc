using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// Two-factor setup: the QR encoder and the time-based codes against published vectors, the steps for both methods,
/// wrong and right codes, the backup codes gate and the keyboard.
/// </summary>
public class TwoFactorSetupTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static (TwoFactorSetup Setup, Window Window) Show()
    {
        var setup = new TwoFactorSetup { Clock = () => Now };
        var window = TestHelpers.Show(setup);
        window.Width = 700;
        window.Height = 900;
        Settle();
        return (setup, window);
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(TwoFactorSetup setup, string part)
    {
        TestHelpers.Descendant<AvaloniaButton>(setup, part).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Settle();
    }

    [Fact]
    public void Totp_Matches_The_Rfc_6238_Sha1_Vectors()
    {
        // RFC 6238 appendix B: the ASCII secret "12345678901234567890", last six digits of the eight-digit codes.
        var secret = TwoFactorSetupTotp.Encode(Encoding.ASCII.GetBytes("12345678901234567890"));
        TwoFactorSetup.ComputeCode(secret, DateTimeOffset.FromUnixTimeSeconds(59)).ShouldBe("287082");
        TwoFactorSetup.ComputeCode(secret, DateTimeOffset.FromUnixTimeSeconds(1111111109)).ShouldBe("081804");
        TwoFactorSetup.ComputeCode(secret, DateTimeOffset.FromUnixTimeSeconds(2000000000)).ShouldBe("279037");
    }

    [Fact]
    public void Base32_Round_Trips_And_Rejects_Other_Characters()
    {
        var bytes = new byte[] { 1, 2, 3, 250, 99, 17, 0, 44, 8, 128 };
        TwoFactorSetupTotp.Decode(TwoFactorSetupTotp.Encode(bytes)).ShouldBe(bytes);
        TwoFactorSetupTotp.Normalize("qf7m 4kzt-w2nb x6rd").ShouldBe("QF7M4KZTW2NBX6RD");
        TwoFactorSetupTotp.Normalize("QF7M 1KZT").ShouldBeNull();
        TwoFactorSetup.GenerateSecret().Length.ShouldBe(32);
        TwoFactorSetup.GenerateBackupCodes(4).ShouldAllBe(c => c.Length == 9 && c[4] == '-');
    }

    [Fact]
    public void Qr_Encoder_Picks_The_Smallest_Version_And_Keeps_The_Fixed_Patterns()
    {
        var small = TwoFactorSetupQrEncoder.Encode("HELLO")!;
        small.Version.ShouldBe(1);
        small.Size.ShouldBe(21);
        var uri = new TwoFactorSetup().ProvisioningUri;
        var matrix = TwoFactorSetupQrEncoder.Encode(uri)!;
        matrix.Version.ShouldBeInRange(6, TwoFactorSetupQrEncoder.MaxVersion);
        matrix.Size.ShouldBe(matrix.Version * 4 + 17);
        // The three finder patterns: dark 7x7 rings with a dark 3x3 centre, and the dark module beside the lower one.
        foreach (var (x, y) in new[] { (0, 0), (matrix.Size - 7, 0), (0, matrix.Size - 7) })
        {
            matrix[x, y].ShouldBeTrue();
            matrix[x + 6, y + 6].ShouldBeTrue();
            matrix[x + 1, y + 1].ShouldBeFalse();
            matrix[x + 3, y + 3].ShouldBeTrue();
        }
        matrix[8, matrix.Size - 8].ShouldBeTrue();
        // Timing pattern between the finders alternates.
        for (var i = 8; i < matrix.Size - 8; i++)
        {
            matrix[i, 6].ShouldBe(i % 2 == 0);
        }
        TwoFactorSetupQrEncoder.Encode(new string('x', 400)).ShouldBeNull();
    }

    [Fact]
    public void Qr_Encoder_Matches_A_Reference_Symbol()
    {
        // "HELLO" in byte mode, level M, mask 2: checked against an independent encoder.
        TwoFactorSetupQrEncoder.Encode("HELLO", TwoFactorSetupQrLevel.Medium, 2)!.ToString().ShouldBe(HelloMask2);
    }

    // Generated with python-qrcode 7 (version 1, ERROR_CORRECT_M, mask_pattern=2, byte mode), border removed.
    private const string HelloMask2 =
        "#######....#..#######\n" +
        "#.....#...#.#.#.....#\n" +
        "#.###.#.##....#.###.#\n" +
        "#.###.#.#.#.#.#.###.#\n" +
        "#.###.#.##..#.#.###.#\n" +
        "#.....#.####..#.....#\n" +
        "#######.#.#.#.#######\n" +
        "........##...........\n" +
        "#.#####...##..#####..\n" +
        ".##.##.#.######..##..\n" +
        "..#####.#...#.##.###.\n" +
        ".##.#....######..##..\n" +
        ".#.######...#..#..#.#\n" +
        "........#.#.#..#.#...\n" +
        "#######..###.#..#.##.\n" +
        "#.....#.#.#....#####.\n" +
        "#.###.#.##.#.#..#.##.\n" +
        "#.###.#.##.#####.#...\n" +
        "#.###.#.##..#.##..#..\n" +
        "#.....#..######.###..\n" +
        "#######.##..#...#.##.\n";

    [AvaloniaFact]
    public void Authenticator_Path_Scans_Verifies_And_Turns_On()
    {
        var (setup, window) = Show();
        try
        {
            TwoFactorSetupCompletedEventArgs? completed = null;
            setup.Completed += (_, e) => completed = e;
            setup.Step.ShouldBe(TwoFactorSetupStep.Method);
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.Connect);
            var qr = TestHelpers.Descendant<TwoFactorSetupQrCode>(setup, TwoFactorSetup.QrPart);
            qr.Matrix.ShouldNotBeNull();
            qr.Text.ShouldBe(setup.ProvisioningUri);
            setup.ProvisioningUri.ShouldContain("secret=QF7M4KZTW2NBX6RD");
            AutomationProperties.GetName(qr)!.ShouldContain("Lumenfield");
            TestHelpers.Descendant<SelectableTextBlock>(setup, TwoFactorSetup.KeyPart).Text.ShouldBe("QF7M 4KZT W2NB X6RD");

            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.Verify);
            var code = TestHelpers.Descendant<OtpInput>(setup, TwoFactorSetup.CodePart);

            code.Value = "000000" == TwoFactorSetup.ComputeCode(setup.Secret, Now) ? "111111" : "000000";
            Settle();
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.Verify);
            code.IsInvalid.ShouldBeTrue();

            code.Value = TwoFactorSetup.ComputeCode(setup.Secret, Now - TimeSpan.FromSeconds(30));
            Settle();
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.BackupCodes);

            var finish = TestHelpers.Descendant<AvaloniaButton>(setup, TwoFactorSetup.PrimaryButtonPart);
            finish.IsEnabled.ShouldBeFalse();
            TestHelpers.Descendant<ItemsControl>(setup, TwoFactorSetup.CodesPart).ItemCount.ShouldBe(10);
            TestHelpers.Descendant<Checkbox>(setup, TwoFactorSetup.SavedCheckPart).IsChecked = true;
            Settle();
            finish.IsEnabled.ShouldBeTrue();
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.Done);
            completed.ShouldNotBeNull();
            completed!.Method.ShouldBe(TwoFactorSetupMethod.AuthenticatorApp);
            completed.BackupCodes.Count.ShouldBe(10);
            completed.Secret.ShouldBe(TwoFactorSetup.SampleSecret);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Message_Path_Checks_The_Number_And_Uses_The_Verifier()
    {
        var (setup, window) = Show();
        try
        {
            var sent = new List<TwoFactorSetupTextMessageEventArgs>();
            setup.TextMessageRequested += (_, e) => sent.Add(e);
            TwoFactorSetupVerifyRequest? request = null;
            setup.CodeVerifier = r =>
            {
                request = r;
                return Task.FromResult(r.Code == "424242");
            };
            setup.Method = TwoFactorSetupMethod.TextMessage;
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.Connect);

            var phone = TestHelpers.Descendant<Input>(setup, TwoFactorSetup.PhonePart);
            phone.Text = "555 01";
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.Connect);
            phone.IsInvalid.ShouldBeTrue();
            sent.ShouldBeEmpty();

            phone.Text = "+1 555 010 0199";
            setup.PhoneNumber.ShouldBe("+1 555 010 0199");
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            setup.Step.ShouldBe(TwoFactorSetupStep.Verify);
            sent.Single().PhoneNumber.ShouldBe("+1 555 010 0199");
            TwoFactorSetup.MaskPhone(setup.PhoneNumber).ShouldBe("•••• 0199");

            Click(setup, TwoFactorSetup.ResendButtonPart);
            sent.Count.ShouldBe(2);
            sent[1].IsResend.ShouldBeTrue();

            TestHelpers.Descendant<OtpInput>(setup, TwoFactorSetup.CodePart).Value = "424242";
            Settle();
            Click(setup, TwoFactorSetup.PrimaryButtonPart);
            request.ShouldNotBeNull();
            request!.PhoneNumber.ShouldBe("+1 555 010 0199");
            setup.Step.ShouldBe(TwoFactorSetupStep.BackupCodes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alt_Left_Goes_Back_And_Text_Messages_Can_Be_Turned_Off()
    {
        var (setup, window) = Show();
        try
        {
            setup.Continue();
            Settle();
            setup.Step.ShouldBe(TwoFactorSetupStep.Connect);
            TestHelpers.Descendant<TextBlock>(setup, "PART_ScanTitle").Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Alt);
            Settle();
            setup.Step.ShouldBe(TwoFactorSetupStep.Method);
            setup.GoBack().ShouldBeFalse();

            setup.Method = TwoFactorSetupMethod.TextMessage;
            setup.IsTextMessageAvailable = false;
            setup.Method.ShouldBe(TwoFactorSetupMethod.AuthenticatorApp);
            TestHelpers.Descendant<RadioCards>(setup, TwoFactorSetup.MethodsPart).ItemCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
