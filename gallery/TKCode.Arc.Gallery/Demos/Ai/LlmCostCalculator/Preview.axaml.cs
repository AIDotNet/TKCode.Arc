using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: real list prices, then the loading, error and empty states.</summary>
public partial class LlmCostCalculatorPreviewDemo : UserControl
{
    private readonly DispatcherTimer _load = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private readonly bool _ready;

    public LlmCostCalculatorPreviewDemo()
    {
        InitializeComponent();
        _load.Tick += (_, _) =>
        {
            _load.Stop();
            Calculator.IsLoading = false;
        };
        Calculator.RetryRequested += OnRetryRequested;
        DetachedFromVisualTree += (_, _) => _load.Stop();
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The segmented control reports its first selection while the XAML loads, before the constructor has finished.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "prices");
        }
    }

    private void Show(string scenario)
    {
        _load.Stop();
        Calculator.IsLoading = scenario == "loading";
        Calculator.ErrorText = scenario == "error" ? "The price service answered 503. Try again in a moment." : null;
        Calculator.Models = scenario == "empty" ? null : LlmCostCalculatorDemoData.Models;
    }

    private void OnRetryRequested(object? sender, RoutedEventArgs e)
    {
        // A pretend refetch: placeholder rows for a moment, then the prices.
        Calculator.ErrorText = null;
        Calculator.IsLoading = true;
        _load.Start();
    }
}
