using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// BillingOverview: status badges, the "expires soon" warning from the block's clock, newest-first invoices that fold
/// behind "Show all invoices", the download event, the action events, and updates from collections and models.
/// </summary>
public class BillingOverviewTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly TimeProvider Oct20 = new FixedClock(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

    private static BillingInvoice Invoice(string number, int month, decimal amount = 90,
                                          BillingInvoiceStatus status = BillingInvoiceStatus.Paid)
        => new() { Number = number, Date = new DateTime(2026, month, 1), Amount = amount, Status = status };

    /// <summary>The §1.6 sample: Studio, Mastercard 5108, six invoices (written out of order).</summary>
    private static BillingOverview Sample()
    {
        var block = new BillingOverview
        {
            Width = 720,
            Clock = Oct20,
            BillingEmail = "accounts@larkspur.co",
            Plan = new BillingPlan
            {
                Name = "Studio", Price = 90, Seats = 6, Status = BillingPlanStatus.Active, RenewsOn = new DateTime(2026, 11, 1)
            },
            PaymentMethod = new BillingPaymentMethod { Brand = "Mastercard", Last4 = "5108", ExpiryMonth = 4, ExpiryYear = 2029 }
        };
        block.Invoices.Add(Invoice("LS-2026-008", 8));
        block.Invoices.Add(Invoice("LS-2026-010", 10));
        block.Invoices.Add(Invoice("LS-2026-005", 5, 75));
        block.Invoices.Add(Invoice("LS-2026-009", 9));
        block.Invoices.Add(Invoice("LS-2026-007", 7, 75, BillingInvoiceStatus.Refunded));
        block.Invoices.Add(Invoice("LS-2026-006", 6, 75));
        return block;
    }

    private static Window ShowWide(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 800;
        window.Height = 1400;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Part<T>(Visual root, string name) where T : Control => TestHelpers.Descendant<T>(root, name);

    private static List<BillingOverviewInvoiceRow> Rows(BillingOverview block, string panel)
        => Part<Panel>(block, panel).Children.OfType<BillingOverviewInvoiceRow>().ToList();

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void The_Plan_Status_Sets_The_Badge_Tone_And_Text()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            var badge = Part<Badge>(block, BillingOverview.PlanStatusPart);
            var expected = new (BillingPlanStatus Status, BadgeTone Tone, string Text)[]
            {
                (BillingPlanStatus.Active, BadgeTone.Success, "Active"),
                (BillingPlanStatus.Trialing, BadgeTone.Info, "Trial"),
                (BillingPlanStatus.PastDue, BadgeTone.Danger, "Past due"),
                (BillingPlanStatus.Canceled, BadgeTone.Neutral, "Canceled")
            };
            foreach (var (status, tone, text) in expected)
            {
                // The plan is a live model: changing it after display updates the view.
                block.Plan!.Status = status;
                badge.Tone.ShouldBe(tone, status.ToString());
                badge.Content.ShouldBe(text);
            }
            var detail = Part<TextBlock>(block, BillingOverview.PlanDetailPart);
            detail.Text.ShouldBe("6 seats · Ends on Nov 1, 2026");
            Part<Button>(block, BillingOverview.CancelPlanPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Invoice_Statuses_Set_The_Badge_Tone_And_Refunds_Are_Muted()
    {
        var block = Sample();
        block.Invoices.Add(Invoice("LS-2026-011", 11, status: BillingInvoiceStatus.Open));
        block.Invoices.Add(new BillingInvoice
        {
            Number = "LS-2026-012", Date = new DateTime(2026, 11, 15), Amount = 90, Status = BillingInvoiceStatus.PastDue
        });
        block.Invoices.Add(new BillingInvoice
        {
            Number = "LS-2026-013", Date = new DateTime(2026, 11, 20), Amount = 90, Status = BillingInvoiceStatus.Void
        });
        block.VisibleInvoiceCount = 20;
        var window = ShowWide(block);
        try
        {
            var rows = Rows(block, BillingOverview.InvoiceRowsPart).ToDictionary(r => r.Invoice!.Number);
            BadgeTone ToneOf(string number) => Part<Badge>(rows[number], "PART_Status").Tone;
            ToneOf("LS-2026-010").ShouldBe(BadgeTone.Success);
            ToneOf("LS-2026-011").ShouldBe(BadgeTone.Neutral);
            ToneOf("LS-2026-012").ShouldBe(BadgeTone.Danger);
            ToneOf("LS-2026-007").ShouldBe(BadgeTone.Neutral);
            ToneOf("LS-2026-013").ShouldBe(BadgeTone.Neutral);
            rows["LS-2026-007"].Classes.Contains(BillingOverviewInvoiceRow.MutedPseudoClass).ShouldBeTrue();
            rows["LS-2026-013"].Classes.Contains(BillingOverviewInvoiceRow.MutedPseudoClass).ShouldBeTrue();
            rows["LS-2026-010"].Classes.Contains(BillingOverviewInvoiceRow.MutedPseudoClass).ShouldBeFalse();
            Part<Badge>(rows["LS-2026-012"], "PART_Status").Content.ShouldBe("Past due");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Card_Whose_Expiry_Month_Starts_Within_60_Days_Expires_Soon()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            var expiry = Part<TextBlock>(block, BillingOverview.CardExpiryPart);
            block.IsPaymentMethodExpiringSoon.ShouldBeFalse();
            expiry.Text.ShouldBe("Expires 04/2029");

            // Today is Oct 20, 2026: November and December start within 60 days, January does not.
            block.PaymentMethod!.ExpiryMonth = 11;
            block.PaymentMethod.ExpiryYear = 2026;
            block.IsPaymentMethodExpiringSoon.ShouldBeTrue();
            block.Classes.Contains(BillingOverview.CardExpiringPseudoClass).ShouldBeTrue();
            expiry.Text.ShouldBe("Expires soon · 11/2026");

            block.PaymentMethod.ExpiryMonth = 12;
            block.IsPaymentMethodExpiringSoon.ShouldBeTrue();

            block.PaymentMethod.ExpiryMonth = 1;
            block.PaymentMethod.ExpiryYear = 2027;
            block.IsPaymentMethodExpiringSoon.ShouldBeFalse();
            expiry.Text.ShouldBe("Expires 01/2027");

            block.PaymentMethod.ExpiryMonth = 9;
            block.PaymentMethod.ExpiryYear = 2026;
            block.IsPaymentMethodExpired.ShouldBeTrue();
            block.IsPaymentMethodExpiringSoon.ShouldBeFalse();
            block.Classes.Contains(BillingOverview.CardExpiredPseudoClass).ShouldBeTrue();
            expiry.Text.ShouldBe("Expired 09/2026");

            // The clock decides what "soon" means.
            block.PaymentMethod.ExpiryYear = 2027;
            block.Clock = new FixedClock(new DateTimeOffset(2027, 8, 1, 9, 0, 0, TimeSpan.Zero));
            block.IsPaymentMethodExpiringSoon.ShouldBeTrue();
            block.IsPaymentMethodExpired.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Row_Shows_A_Text_Brand_Mark()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            Part<TextBlock>(block, BillingOverview.CardBrandPart).Text.ShouldBe("MC");
            Part<TextBlock>(block, BillingOverview.CardTitlePart).Text.ShouldBe("Mastercard ending in 5108");
            block.PaymentMethod!.Brand = "Visa";
            Part<TextBlock>(block, BillingOverview.CardBrandPart).Text.ShouldBe("VISA");
            BillingOverview.MarkOf("American Express").ShouldBe("AMEX");

            block.PaymentMethod = null;
            Part<TextBlock>(block, BillingOverview.CardTitlePart).Text.ShouldBe("No payment method");
            Part<Button>(block, BillingOverview.UpdateCardPart).Content.ShouldBe("Add card");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Invoices_Show_Newest_First_And_Fold_Beyond_The_Visible_Count()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            Rows(block, BillingOverview.InvoiceRowsPart).Select(r => r.Invoice!.Number)
                .ShouldBe(["LS-2026-010", "LS-2026-009", "LS-2026-008", "LS-2026-007"]);
            Rows(block, BillingOverview.MoreRowsPart).Select(r => r.Invoice!.Number).ShouldBe(["LS-2026-006", "LS-2026-005"]);

            var more = Part<SpringSizeDecorator>(block, BillingOverview.MoreInvoicesPart);
            var moreRows = Part<Panel>(block, BillingOverview.MoreRowsPart);
            more.IsExpanded.ShouldBeFalse();
            moreRows.IsVisible.ShouldBeFalse();
            more.Bounds.Height.ShouldBe(0);

            var showAll = Part<Button>(block, BillingOverview.ShowAllPart);
            Part<ArcContentSwap>(block, BillingOverview.ShowAllTextPart).Content.ShouldBe("Show all invoices (6)");
            AutomationProperties.GetName(showAll).ShouldBe("Show all invoices (6)");

            Click(showAll);
            Dispatcher.UIThread.RunJobs();
            block.IsInvoiceListExpanded.ShouldBeTrue();
            block.Classes.Contains(BillingOverview.InvoicesExpandedPseudoClass).ShouldBeTrue();
            more.IsExpanded.ShouldBeTrue();
            moreRows.IsVisible.ShouldBeTrue();
            more.Bounds.Height.ShouldBeGreaterThan(90);
            Part<ArcContentSwap>(block, BillingOverview.ShowAllTextPart).Content.ShouldBe("Show fewer");

            Click(showAll);
            Dispatcher.UIThread.RunJobs();
            block.IsInvoiceListExpanded.ShouldBeFalse();
            more.IsExpanded.ShouldBeFalse();
            // Once folded, the hidden rows leave the tab order.
            moreRows.IsVisible.ShouldBeFalse();
            more.Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Show_All_Appears_Only_When_There_Are_More_Invoices_Than_Shown()
    {
        var block = Sample();
        block.VisibleInvoiceCount = 6;
        var window = ShowWide(block);
        try
        {
            var group = Part<SettingsGroup>(block, BillingOverview.InvoicesGroupPart);
            group.Footer.ShouldBeNull();
            Rows(block, BillingOverview.InvoiceRowsPart).Count.ShouldBe(6);

            block.Invoices.Add(Invoice("LS-2026-004", 4, 75));
            group.Footer.ShouldNotBeNull();
            Rows(block, BillingOverview.MoreRowsPart).Single().Invoice!.Number.ShouldBe("LS-2026-004");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Download_Button_Raises_The_Event_With_Its_Invoice()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            BillingInvoiceEventArgs? raised = null;
            block.InvoiceDownloadRequested += (_, e) => raised = e;

            var row = Rows(block, BillingOverview.InvoiceRowsPart)[1];
            var download = row.DownloadButton!;
            AutomationProperties.GetName(download).ShouldBe("Download LS-2026-009");
            var point = download.TranslatePoint(new Point(download.Bounds.Width / 2, download.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);

            raised.ShouldNotBeNull();
            raised.Invoice.Number.ShouldBe("LS-2026-009");
            raised.RoutedEvent.ShouldBe(BillingOverview.InvoiceDownloadRequestedEvent);
            raised.Source.ShouldBe(block);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Action_Raises_Its_Event()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            var log = new List<string>();
            block.ChangePlanRequested += (_, _) => log.Add("change");
            block.CancelPlanRequested += (_, _) => log.Add("cancel");
            block.UpdatePaymentMethodRequested += (_, _) => log.Add("card");
            block.EditBillingEmailRequested += (_, _) => log.Add("email");

            Click(Part<Button>(block, BillingOverview.ChangePlanPart));
            Click(Part<Button>(block, BillingOverview.CancelPlanPart));
            Click(Part<Button>(block, BillingOverview.UpdateCardPart));
            Click(Part<Button>(block, BillingOverview.EditEmailPart));
            log.ShouldBe(["change", "cancel", "card", "email"]);

            AutomationProperties.GetName(Part<Button>(block, BillingOverview.UpdateCardPart)).ShouldBe("Update payment method");
            AutomationProperties.GetName(Part<Button>(block, BillingOverview.EditEmailPart)).ShouldBe("Edit billing email");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Plan_Card_Shows_Price_And_Next_Invoice()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            Part<TextBlock>(block, BillingOverview.PlanNamePart).Text.ShouldBe("Studio");
            Part<TextBlock>(block, BillingOverview.PlanDetailPart).Text.ShouldBe("6 seats · Renews on Nov 1, 2026");
            var price = Part<TextBlock>(block, BillingOverview.PricePart);
            string.Concat(price.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)).ShouldBe("$90 per month");
            var next = Part<TextBlock>(block, BillingOverview.NextInvoicePart);
            AutomationProperties.GetName(next).ShouldBe("Next invoice $90.00 on Nov 1, 2026");

            block.NextInvoiceAmount = 72.5m;
            AutomationProperties.GetName(next).ShouldBe("Next invoice $72.50 on Nov 1, 2026");

            block.Plan!.Interval = BillingInterval.Year;
            block.Plan.Price = 864;
            string.Concat(price.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)).ShouldBe("$864 per year");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collection_And_Invoice_Changes_Update_The_Table_And_Reuse_Rows()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            var first = Rows(block, BillingOverview.InvoiceRowsPart)[0];
            first.Invoice!.Number.ShouldBe("LS-2026-010");

            // A change to an invoice updates its row in place.
            first.Invoice.Status = BillingInvoiceStatus.Refunded;
            Part<Badge>(first, "PART_Status").Content.ShouldBe("Refunded");

            // A newer invoice takes the top row; the same row controls are reused.
            block.Invoices.Add(Invoice("LS-2026-011", 11));
            var rows = Rows(block, BillingOverview.InvoiceRowsPart);
            rows[0].ShouldBeSameAs(first);
            rows[0].Invoice!.Number.ShouldBe("LS-2026-011");

            // A replaced (or bound) list is shown and watched.
            var replacement = new AvaloniaList<BillingInvoice> { Invoice("X-1", 3) };
            block.Invoices = replacement;
            Rows(block, BillingOverview.InvoiceRowsPart).Single().Invoice!.Number.ShouldBe("X-1");
            replacement.Add(Invoice("X-2", 4));
            Rows(block, BillingOverview.InvoiceRowsPart).Select(r => r.Invoice!.Number).ShouldBe(["X-2", "X-1"]);

            replacement.Clear();
            Part<Control>(block, BillingOverview.InvoiceTablePart).IsVisible.ShouldBeFalse();
            Part<Control>(block, BillingOverview.InvoicesEmptyPart).IsVisible.ShouldBeTrue();
            Part<TextBlock>(block, BillingOverview.EmptyTitlePart).Text.ShouldBe("No invoices yet");
            Part<SettingsGroup>(block, BillingOverview.InvoicesGroupPart).Footer.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Block_Hides_The_Invoice_Column_And_Moves_The_Price_Under_The_Name()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            block.IsNarrow.ShouldBeFalse();
            var row = Rows(block, BillingOverview.InvoiceRowsPart)[0];
            Part<TextBlock>(row, "PART_Number").IsVisible.ShouldBeTrue();
            Part<BillingOverviewSplitPanel>(block, "PART_PlanLayout").IsStacked.ShouldBeFalse();

            block.Width = 400;
            Dispatcher.UIThread.RunJobs();
            block.IsNarrow.ShouldBeTrue();
            block.Classes.Contains(BillingOverview.NarrowPseudoClass).ShouldBeTrue();
            row.Classes.Contains(BillingOverviewInvoiceRow.NarrowPseudoClass).ShouldBeTrue();
            Part<TextBlock>(row, "PART_Number").IsVisible.ShouldBeFalse();
            Part<TextBlock>(Part<BillingOverviewInvoiceRow>(block, BillingOverview.InvoiceHeaderPart), "PART_Number").IsVisible.ShouldBeFalse();
            Part<BillingOverviewSplitPanel>(block, "PART_PlanLayout").IsStacked.ShouldBeTrue();
            var name = Part<TextBlock>(block, BillingOverview.PlanNamePart);
            var price = Part<TextBlock>(block, BillingOverview.PricePart);
            price.TranslatePoint(default, block)!.Value.Y.ShouldBeGreaterThan(name.TranslatePoint(default, block)!.Value.Y);
            // The stacked email row's ghost button lines its label up with the text.
            Part<Button>(block, BillingOverview.EditEmailPart).Classes.Contains(BillingOverview.StackedClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_The_Built_In_Text()
    {
        var block = Sample();
        var window = ShowWide(block);
        try
        {
            block.Labels = new BillingOverviewLabels
            {
                ChangePlan = "Switch plan",
                Invoices = "Receipts",
                ShowAll = n => $"All {n}"
            };
            Part<Button>(block, BillingOverview.ChangePlanPart).Content.ShouldBe("Switch plan");
            Part<SettingsGroup>(block, BillingOverview.InvoicesGroupPart).Header.ShouldBe("Receipts");
            Part<ArcContentSwap>(block, BillingOverview.ShowAllTextPart).Content.ShouldBe("All 6");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Plan_The_Card_Offers_To_Choose_One()
    {
        var block = new BillingOverview { Width = 720 };
        var window = ShowWide(block);
        try
        {
            Part<TextBlock>(block, BillingOverview.PlanNamePart).Text.ShouldBe("No plan");
            Part<Badge>(block, BillingOverview.PlanStatusPart).IsVisible.ShouldBeFalse();
            Part<TextBlock>(block, BillingOverview.PricePart).IsVisible.ShouldBeFalse();
            Part<Button>(block, BillingOverview.ChangePlanPart).Content.ShouldBe("Choose a plan");
            Part<Button>(block, BillingOverview.CancelPlanPart).IsVisible.ShouldBeFalse();
            Part<SettingsRow>(block, BillingOverview.EmailRowPart).Description.ShouldBe("Invoices are not emailed yet.");
            Part<Control>(block, BillingOverview.InvoicesEmptyPart).IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
