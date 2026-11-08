using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PricingCalculatorBackendDemo : UserControl
{
    private readonly List<string> _lines = new();

    public PricingCalculatorBackendDemo()
    {
        InitializeComponent();
        Calculator.SeatStops = PricingCalculatorDemoData.CrewStops;
        Calculator.PlansSource = PricingCalculatorDemoData.CrewPlans();
        Calculator.SetSeats(12);

        // Your backend: here a 900 ms wait that fails when the switch is on.
        Calculator.TrialHandler = async (request, cancellation) =>
        {
            Write($"Requesting a {request.Plan.Name} trial for {request.Seats} seats…");
            await Task.Delay(900, cancellation);
            if (FailNext.IsChecked == true)
            {
                FailNext.IsChecked = false;
                Write("The trial request failed.");
                return false;
            }
            return true;
        };
        Calculator.SeatsChanged += (_, e) => Write($"Seats {e.OldSeats} → {e.NewSeats}");
        Calculator.BillingChanged += (_, e) => Write($"Billing: {e.NewBilling}");
        Calculator.PlanPicked += (_, e) => Write($"Picked {e.Plan.Name} at {e.Seats} seats");
        Calculator.TrialStarted += (_, e) => Write($"Trial started: {e.Request.Plan.Name}, {e.Request.Seats} seats, {e.Request.Total:N0} a period");
    }

    private void Write(string line)
    {
        _lines.Add(line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(0);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
