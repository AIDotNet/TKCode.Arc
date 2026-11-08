using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LiveCheckoutHandlersDemo : UserControl
{
    private readonly List<string> _log = [];

    public LiveCheckoutHandlersDemo()
    {
        InitializeComponent();
        Checkout.Plans =
        [
            new LiveCheckoutPlan { Id = "sketch", Name = "Sketch", MonthlySeatPrice = 9m, Blurb = "Boards and comments" },
            new LiveCheckoutPlan { Id = "studio", Name = "Studio", MonthlySeatPrice = 19m, Blurb = "Shared libraries" },
            new LiveCheckoutPlan { Id = "atelier", Name = "Atelier", MonthlySeatPrice = 34m, Blurb = "Roles and audit trail" }
        ];
        Checkout.Addons =
        [
            new LiveCheckoutAddon { Id = "fonts", Name = "Type library", Description = "1,200 licensed families", MonthlyPrice = 12m },
            new LiveCheckoutAddon { Id = "review", Name = "Client review", Description = "Guest links with approvals", MonthlyPrice = 29m }
        ];
        Checkout.SelectedPlanId = "studio";
        Checkout.SelectedAddonIds = ["review"];

        // Your server checks the code; here STUDIO25 takes a quarter off after a short wait.
        Checkout.ValidateCouponAsync = async (code, cancellation) =>
        {
            await Task.Delay(400, cancellation);
            return code == "STUDIO25"
                ? LiveCheckoutCouponResult.Accept(code, 25)
                : LiveCheckoutCouponResult.Reject(code, $"{code} has expired or never existed.");
        };

        // Your server charges the order; a throw shows its message above the pay button.
        Checkout.PayAsync = async (order, cancellation) =>
        {
            Write($"Charging {order.Totals.Total:0.00} {order.Currency} for {order.Seats} seats of {order.PlanId}…");
            await Task.Delay(1200, cancellation);
            if (FailNext.IsChecked == true)
            {
                FailNext.IsChecked = false;
                throw new PaymentException("The bank asked for another check. Try again in a minute.");
            }
            return new LiveCheckoutReceipt { OrderNumber = "PW-" + (order.Seats * 1000 + 417) };
        };

        Checkout.TotalsChanged += (_, e) => Write($"Total {e.Totals.Total:0.00} (tax {e.Totals.Tax:0.00})");
        Checkout.CouponApplied += (_, e) => Write($"Coupon {e.Code} applied ({e.Percent}% off)");
        Checkout.CouponRemoved += (_, e) => Write($"Coupon {e.Code} removed");
        Checkout.PaymentFailed += (_, e) => Write($"Payment failed: {e.Message}");
        Checkout.Paid += (_, e) => Write($"Paid: order {e.Receipt.OrderNumber}, {e.Receipt.PaymentText}");
        Checkout.ReceiptRequested += (_, e) => Write($"Receipt requested for {e.Receipt.OrderNumber}");
        Checkout.Restarted += (_, _) => Write("Started over");
    }

    private void Write(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 6)
        {
            _log.RemoveAt(_log.Count - 1);
        }
        Log.Text = string.Join("\n", _log);
    }

    private sealed class PaymentException(string message) : System.Exception(message);
}
