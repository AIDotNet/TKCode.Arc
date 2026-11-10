using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Finance;

/// <summary><see cref="WalletCard"/>: masking, freezing through the handler, details, the activity rows and the layout.</summary>
public class WalletCardTests
{
    private static (WalletCard Card, Window Window) Show(double width = 900, Action<WalletCard>? configure = null)
    {
        var card = new WalletCard { Today = new DateTime(2026, 11, 9), DetailsTimeout = TimeSpan.Zero };
        WalletCardSamples.Apply(card);
        card.Transactions.Clear();
        card.Transactions.AddRange(WalletCardSamples.CreateTransactions(new DateTimeOffset(new DateTime(2026, 11, 9, 12, 0, 0))));
        configure?.Invoke(card);
        var window = TestHelpers.Show(new Border { Width = width, Child = card });
        Pump();
        return (card, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static string? Text(WalletCard card, string part) => TestHelpers.Descendant<TextBlock>(card, part).Text;

    [AvaloniaFact]
    public void Number_Is_Masked_To_The_Last_Four_Until_Details_Are_Shown()
    {
        var (card, window) = Show();
        try
        {
            card.LastFour.ShouldBe("4821");
            Text(card, "PART_Masked").ShouldBe("•••• •••• •••• 4821");
            Text(card, "PART_Full").ShouldBe("4929 1820 5531 4821");
            card.Classes.Contains(":details").ShouldBeFalse();
            TestHelpers.Descendant<CopyButton>(card, "PART_Copy").IsVisible.ShouldBeFalse();

            card.ToggleDetails();
            Pump();
            card.IsDetailsVisible.ShouldBeTrue();
            card.Classes.Contains(":details").ShouldBeTrue();
            TestHelpers.Descendant<CopyButton>(card, "PART_Copy").Value.ShouldBe("4929182055314821");
            card.LastAnnouncement.ShouldBe("Card details shown");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Hides_The_Details()
    {
        var (card, window) = Show(configure: c => c.IsDetailsVisible = true);
        try
        {
            var details = TestHelpers.Descendant<Button>(card, "PART_Details");
            details.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            card.IsDetailsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Freeze_Waits_For_The_Handler_And_Reverts_On_Failure()
    {
        var (card, window) = Show();
        try
        {
            var gate = new TaskCompletionSource<bool>();
            card.SetFrozenAsync = (_, _) => gate.Task;
            var events = new List<bool>();
            card.FrozenChanged += (_, e) => events.Add(e.IsFrozen);

            var pending = card.RequestFrozenAsync(true);
            card.IsBusy.ShouldBeTrue();
            TestHelpers.Descendant<Button>(card, "PART_Freeze").IsLoading.ShouldBeTrue();
            card.IsFrozen.ShouldBeFalse();
            gate.SetResult(false);
            (await pending).ShouldBeFalse();
            card.IsFrozen.ShouldBeFalse();
            card.IsBusy.ShouldBeFalse();
            card.LastAnnouncement.ShouldBe("The card could not be changed. Try again.");
            events.ShouldBeEmpty();

            card.SetFrozenAsync = (_, _) => Task.FromResult(true);
            (await card.RequestFrozenAsync(true)).ShouldBeTrue();
            Pump();
            card.IsFrozen.ShouldBeTrue();
            card.Classes.Contains(":frozen").ShouldBeTrue();
            events.ShouldBe([true]);
            TestHelpers.Descendant<Border>(card, "PART_FrozenNote").IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Button>(card, "PART_Freeze").Content.ShouldBe("Unfreeze");
            AutomationProperties.GetName(TestHelpers.Descendant<WalletCardFacePanel>(card)).ShouldEndWith("ending in 4821, frozen");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Show_Signed_Amounts_Status_And_Move_With_Arrows()
    {
        var (card, window) = Show();
        try
        {
            var rows = card.GetVisualDescendants().OfType<WalletCardRow>().ToList();
            rows.Count.ShouldBe(5);
            AutomationProperties.GetName(rows[0]).ShouldBe("Juniper Grocers, −$42.10, Today, Pending");
            AutomationProperties.GetName(rows[3]).ShouldBe("Pinecrest Outfitters, +$64.00, Nov 7");

            WalletTransaction? invoked = null;
            card.TransactionInvoked += (_, e) => invoked = e.Transaction;
            rows[0].Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            rows[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            rows[^1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            invoked.ShouldBe(rows[^1].Transaction);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Limit_Meter_Follows_Spending_And_Turns_Over()
    {
        var (card, window) = Show();
        try
        {
            var meter = TestHelpers.Descendant<WalletCardMeter>(card);
            meter.Fraction.ShouldBe(1240.18 / 3000, 1e-6);
            meter.IsOver.ShouldBeFalse();
            Text(card, "PART_LimitLine").ShouldBe("$1,240.18 of $3,000.00 monthly limit");
            card.SpentThisMonth = 3100;
            meter.IsOver.ShouldBeTrue();
            meter.Fraction.ShouldBe(1);
            card.MonthlyLimit = 0;
            meter.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Block_Stacks_The_Face_Above_The_Balance()
    {
        var (card, window) = Show(width: 420);
        try
        {
            card.IsCompact.ShouldBeTrue();
            var face = TestHelpers.Descendant<WalletCardFacePanel>(card);
            (face.Bounds.Width / face.Bounds.Height).ShouldBe(WalletCardFacePanel.Ratio, 0.01);
            face.Bounds.Width.ShouldBeLessThanOrEqualTo(360.5);
        }
        finally
        {
            window.Close();
        }
    }
}
