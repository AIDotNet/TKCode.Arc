using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/billing: three stacked rows. "overview" is uiarc.dev's billing overview of Fieldnote Labs
/// (DemoBillingOverview), "usage" the UsageForecast block with Northwind Labs' September, and "cancel" a panel whose
/// button opens a dialog with the CancelFlow block for Halcyon Studio's Team plan (one offer per reason).
/// </summary>
public sealed class DemoBillingPage : DemoPageView
{
    private readonly Dialog _cancelDialog;
    private readonly CancelFlow _flow;

    public DemoBillingPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        AddRow(DemoRowLayout.Stack, "overview", new DemoBillingOverview());
        AddRow(DemoRowLayout.Stack, "usage", BuildForecast());

        _flow = BuildFlow();
        _cancelDialog = new Dialog
        {
            Title = "Cancel subscription",
            Description = "Team plan · Halcyon Studio · 8 seats · $96 a month",
            Size = ControlSize.Large,
            Content = _flow
        };
        // Every opening starts from the first step, unless the plan was just cancelled.
        _cancelDialog.Opened += (_, _) =>
        {
            if (_flow.Step != CancelFlowStep.Canceled)
            {
                _flow.Reset();
            }
        };
        AddRow(DemoRowLayout.Stack, "cancel", BuildCancelPanel());
    }

    private static UsageForecast BuildForecast()
    {
        var forecast = new UsageForecast
        {
            Title = "Usage forecast",
            Subtitle = "Northwind Labs · Scale plan · Renews Oct 1",
            PeriodStart = DemoBillingData.PeriodStart,
            PeriodDays = 30,
            TodayIndex = 22,
            Labels = DemoBillingData.ForecastLabels()
        };
        forecast.Metrics.AddRange(DemoBillingData.Metrics());
        return forecast;
    }

    private CancelFlow BuildFlow()
    {
        var flow = new CancelFlow
        {
            Appearance = CancelFlowAppearance.Plain,
            PlanName = "Team",
            AccessEndsOn = new DateTime(2026, 10, 14),
            Labels = DemoBillingData.CancelLabels
        };
        foreach (var reason in DemoBillingData.Reasons())
        {
            flow.Reasons.Add(reason);
        }
        foreach (var loss in DemoBillingData.Losses())
        {
            flow.Losses.Add(loss);
        }
        flow.KeepPlanRequested += (_, _) =>
        {
            var retained = flow.Step == CancelFlowStep.Retained;
            _cancelDialog.Close();
            if (!retained)
            {
                Host.Toast(ToastKind.Info, "Nothing changed. Your Team plan stays active.");
            }
        };
        flow.OfferAccepted += (_, e) => Host.Toast(ToastKind.Success, e.Offer.AcceptedTitle ?? e.Offer.Title);
        flow.ReactivateRequested += (_, _) =>
        {
            _cancelDialog.Close();
            Host.Toast(ToastKind.Success, "Plan reactivated. It renews on Oct 14.");
        };
        return flow;
    }

    /// <summary>uiarc.dev's <c>cancelPanel</c>: the heading and its line beside the button (stacked below 560 wide).</summary>
    private Control BuildCancelPanel()
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(DemoUi.Text("Cancel subscription", ArcDesignTokenKind.Foreground, 14, FontWeight.Medium));
        text.Children.Add(DemoUi.Text("The workspace stays open until the end of the billing period, and you can export everything first.",
            ArcDesignTokenKind.TextMuted, 12));
        var open = DemoUi.Button("Cancel subscription");
        open.VerticalAlignment = VerticalAlignment.Center;
        open.Click += (_, _) => _cancelDialog.Open();

        var grid = new Grid();
        grid.Children.Add(text);
        grid.Children.Add(open);
        grid.Children.Add(_cancelDialog);
        var card = DemoUi.Card(grid, new Thickness(20, 16, 20, 20));
        card.SizeChanged += (_, e) =>
        {
            var stacked = e.NewSize.Width <= 560;
            card.Padding = stacked ? new Thickness(16) : new Thickness(20, 16, 20, 20);
            grid.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("*,Auto");
            grid.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            Grid.SetColumn(open, stacked ? 0 : 1);
            Grid.SetRow(open, stacked ? 1 : 0);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            open.Margin = stacked ? new Thickness(0, 12, 0, 0) : new Thickness(16, 0, 0, 0);
        };
        grid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
        Grid.SetColumn(open, 1);
        open.Margin = new Thickness(16, 0, 0, 0);
        return card;
    }
}
