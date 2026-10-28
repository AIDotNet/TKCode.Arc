using System.Globalization;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/status: the UptimeStatus block with "Halden status" (four services, five incidents over 90 days and a
/// button that walks a webhook incident through start, resolution and reset), then API health (latency or errors per
/// day) beside the month's error budget.
/// </summary>
public sealed class DemoStatusPage : DemoPageView
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    public DemoStatusPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        AddRow(DemoRowLayout.Stack, "uptime", DemoUi.Frame(BuildUptime()));
        AddRow(DemoRowLayout.MainSide, "traffic", BuildApiHealth(), BuildErrorBudget());
    }

    private static UptimeStatus BuildUptime()
    {
        var simulate = DemoUi.Button("Simulate incident");
        var status = new UptimeStatus
        {
            PageName = "Halden status",
            Today = DemoStatusData.Latest,
            UpdatedAt = DemoStatusData.UpdatedAt,
            ChartHeadroom = 1.08,
            ActionContent = simulate
        };
        status.Services.AddRange(DemoStatusData.Services());
        status.Incidents.AddRange(DemoStatusData.Incidents());
        var simulation = new DemoStatusSimulation(status);
        simulate.Click += (_, _) =>
        {
            simulation.Advance();
            simulate.Content = simulation.ActionText;
        };
        return status;
    }

    // Head with a Latency | Errors switch; the bars morph to the other measure (same keys: the days).
    private static Control BuildApiHealth()
    {
        var chart = new BarChart
        {
            PlotHeight = 220,
            Period = "Sep 9 – 22, 2026",
            AverageLabel = "Daily average",
            CategoryLabel = "Day"
        };
        var measure = new SegmentedControl();
        Avalonia.Automation.AutomationProperties.SetName(measure, "API health measure");
        measure.Items.Add(new SegmentedOption { Value = "latency", Label = "Latency" });
        measure.Items.Add(new SegmentedOption { Value = "errors", Label = "Errors" });
        measure.SelectedValue = "latency";

        void Show(bool latency)
        {
            chart.Label = latency ? "API latency at the 95th percentile" : "Failed requests";
            chart.ValueLabel = latency ? "Latency" : "Failed";
            chart.Unit = latency ? "ms" : string.Empty;
            var values = latency ? DemoStatusData.DailyLatency : DemoStatusData.DailyErrors;
            var first = new DateTime(2026, 9, 9);
            chart.ItemsSource = values.Select((value, i) =>
            {
                var day = first.AddDays(i);
                return new ChartPoint(day, value)
                {
                    Key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Label = day.ToString("ddd, MMM d", s_culture),
                    AxisLabel = day.ToString("ddd", s_culture)[..1]
                };
            }).ToArray();
        }

        Show(true);
        measure.SelectionChanged += (_, _) => Show(measure.SelectedValue as string != "errors");
        return DemoUi.Panel("API health", "Daily, the last 14 days", chart, measure);
    }

    private static Control BuildErrorBudget()
    {
        var meter = new UsageMeter
        {
            Label = "Downtime this month",
            Unit = "min",
            Decimals = 1,
            Limit = 43.2,
            FreeLabel = "Remaining"
        };
        meter.Segments.Add(new UsageSegment("sep-9", "Sep 9 · API latency", 12.4));
        meter.Segments.Add(new UsageSegment("sep-17", "Sep 17 · Webhook delays", 6.1));
        meter.Segments.Add(new UsageSegment("deploys", "Deploy restarts", 2.3));

        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(meter);
        body.Children.Add(Note("Half the budget is left with eight days to go. Releases stay unrestricted."));
        return DemoUi.Panel("Error budget", "99.9% availability target for September", body);
    }

    /// <summary>uiarc.dev's <c>panelNote</c>: a footnote under a hairline, in the secondary text color.</summary>
    private static Control Note(string text)
    {
        var note = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 12, 0, 0),
            Child = DemoUi.Text(text, ArcDesignTokenKind.TextSecondary, 12)
        };
        ArcTokens.Bind(note, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        note.HorizontalAlignment = HorizontalAlignment.Stretch;
        return note;
    }
}

/// <summary>
/// The status page's simulation, driven by one button: the first press starts a webhook incident (Webhooks degraded and
/// selected, its last two latency samples spike), the next resolves it, the next removes it again.
/// </summary>
internal sealed class DemoStatusSimulation
{
    private readonly UptimeStatus _block;
    private readonly UptimeService _webhooks;
    private readonly LatencySample[] _calm;
    private UptimeIncident? _incident;

    public DemoStatusSimulation(UptimeStatus block)
    {
        _block = block;
        _webhooks = block.Services.First(service => service.Key == "webhooks");
        _calm = _webhooks.Latency.ToArray();
    }

    /// <summary>0 operational, 1 degraded, 2 recovered.</summary>
    public int Phase { get; private set; }

    /// <summary>The button's label for the next press.</summary>
    public string ActionText => Phase switch { 0 => "Simulate incident", 1 => "Simulate resolution", _ => "Reset demo" };

    public void Advance()
    {
        switch (Phase)
        {
            case 0:
                Start();
                break;
            case 1:
                Resolve();
                break;
            default:
                Reset();
                break;
        }
        Phase = (Phase + 1) % 3;
    }

    private void Start()
    {
        _incident = DemoStatusData.SimulatedIncident();
        _webhooks.Latency.Clear();
        _webhooks.Latency.AddRange(DemoStatusData.WebhooksSpike(_calm));
        _block.Incidents.Insert(0, _incident);
        _block.SelectedService = _webhooks;
        _block.StatusSummary = "Webhook deliveries slowed at 13:58 UTC";
    }

    private void Resolve()
    {
        if (_incident is null)
        {
            return;
        }
        _incident.Updates.Add(new IncidentUpdate("Resolved", DemoStatusData.At(89, 14, 51),
            "The delivery queue was scaled out and the backlog has drained. Late events arrived in order.", IncidentTone.Success));
        _incident.Duration = TimeSpan.FromMinutes(53);
        _incident.IsResolved = true;
        _block.StatusSummary = "Webhooks recovered at 14:51 UTC";
    }

    private void Reset()
    {
        if (_incident is not null)
        {
            _block.Incidents.Remove(_incident);
            _incident = null;
        }
        _webhooks.Latency.Clear();
        _webhooks.Latency.AddRange(_calm);
        _block.StatusSummary = null;
    }
}

/// <summary>The Status page's data, as uiarc.dev's "Halden status": services, latency, incidents and the daily API figures.</summary>
internal static class DemoStatusData
{
    /// <summary>Day 0 of the 90-day window: Jun 25, 2026 (day 89 is today, Sep 22).</summary>
    private static readonly DateTimeOffset s_dayZero = new(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The newest latency sample: Sep 22, 14:30 UTC.</summary>
    public static readonly DateTimeOffset Latest = new(2026, 9, 22, 14, 30, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset UpdatedAt = new(2026, 9, 22, 14, 32, 0, TimeSpan.Zero);

    public static readonly double[] DailyLatency = [398, 186, 179, 183, 171, 168, 181, 188, 342, 184, 176, 170, 178, 173];

    public static readonly double[] DailyErrors = [2140, 402, 388, 395, 355, 348, 401, 392, 1260, 410, 362, 340, 395, 377];

    /// <summary>A time on day <paramref name="day"/> of the window.</summary>
    public static DateTimeOffset At(int day, int hour, int minute) => s_dayZero.AddDays(day).AddHours(hour).AddMinutes(minute);

    public static UptimeService[] Services() =>
    [
        Service("api", "API", "REST and GraphQL", 400,
            [101, 99, 99, 97, 99, 101, 94, 96, 96, 92, 92, 88, 86, 81, 83, 82, 76, 74, 77, 76, 78, 73, 70, 72, 73, 70, 70, 76, 74, 77, 79, 77, 79, 78, 76, 83, 87, 89, 94, 90, 88, 92, 90, 92, 94, 100, 96, 97],
            [265, 262, 250, 253, 266, 244, 240, 224, 236, 237, 233, 209, 190, 192, 178, 197, 229, 160, 212, 163, 168, 169, 173, 158, 164, 174, 152, 168, 161, 183, 188, 191, 181, 187, 183, 194, 205, 264, 216, 229, 207, 217, 225, 235, 225, 241, 232, 244]),
        Service("dashboard", "Dashboard", "app.halden.example", 1000,
            [249, 257, 254, 246, 244, 246, 236, 232, 233, 234, 227, 224, 227, 208, 199, 188, 188, 180, 176, 188, 186, 175, 187, 190, 193, 195, 182, 180, 183, 179, 176, 185, 187, 205, 206, 206, 213, 208, 214, 226, 232, 228, 239, 245, 243, 248, 237, 231],
            [546, 581, 612, 603, 581, 560, 537, 546, 474, 504, 494, 438, 455, 398, 388, 344, 363, 334, 331, 408, 484, 368, 390, 385, 498, 398, 352, 394, 350, 387, 364, 378, 495, 514, 398, 414, 384, 404, 414, 456, 479, 541, 524, 511, 594, 545, 536, 554]),
        Service("webhooks", "Webhooks", "Event delivery", 750,
            [141, 144, 145, 149, 146, 140, 141, 141, 138, 134, 131, 127, 125, 125, 127, 117, 111, 106, 105, 108, 106, 112, 117, 118, 120, 116, 116, 110, 113, 119, 123, 128, 122, 128, 130, 129, 135, 140, 144, 136, 134, 141, 145, 146, 147, 146, 144, 144],
            [413, 399, 384, 441, 429, 399, 364, 377, 342, 347, 324, 340, 328, 391, 316, 278, 259, 283, 265, 353, 246, 290, 287, 298, 305, 292, 286, 265, 273, 299, 311, 306, 397, 294, 320, 400, 356, 362, 370, 381, 336, 366, 405, 389, 395, 383, 420, 400]),
        Service("auth", "Auth", "Sign-in, SSO and tokens", 300,
            [65, 67, 67, 65, 64, 62, 63, 61, 59, 61, 58, 56, 55, 56, 55, 53, 55, 55, 52, 54, 52, 49, 49, 51, 52, 51, 51, 49, 53, 53, 54, 55, 56, 57, 59, 59, 58, 59, 61, 64, 65, 67, 66, 65, 64, 64, 62, 63],
            [150, 166, 153, 157, 164, 154, 147, 141, 138, 137, 120, 112, 118, 107, 113, 103, 156, 119, 108, 106, 105, 107, 108, 109, 114, 116, 107, 134, 119, 102, 121, 105, 121, 121, 130, 119, 124, 131, 142, 131, 136, 148, 165, 152, 151, 162, 156, 157])
    ];

    /// <summary>The webhooks samples while the simulated incident is open or just recovered: the last two spike.</summary>
    public static LatencySample[] WebhooksSpike(IReadOnlyList<LatencySample> calm)
    {
        var samples = calm.ToArray();
        samples[46] = samples[46] with { P50 = 389, P95 = 1680 };
        samples[47] = samples[47] with { P50 = 490, P95 = 2040 };
        return samples;
    }

    public static UptimeIncident[] Incidents() =>
    [
        Incident("api-errors", "Elevated error rates on the API", ServiceStatus.Outage, 84, 9, 12, 23, "Ava Mitchell", true, ["api"],
            ("Resolved", 9, 58, "Error rates have been normal for 20 minutes. Pool limits are raised on every cluster and saturation now pages on call."),
            ("Monitoring", 9, 35, "A fix is deployed and errors are falling. We are watching the us-east-1 cluster."),
            ("Identified", 9, 21, "A connection pool limit on the primary database rejected new requests during a traffic peak."),
            ("Investigating", 9, 12, "Some API requests in us-east-1 return 503 errors.")),
        Incident("dashboard-slow", "Dashboard charts loading slowly", ServiceStatus.Degraded, 70, 14, 5, 72, "Marcus Johnson", false, ["dashboard"],
            ("Resolved", 15, 17, "Chart queries are back to their usual speed after the reporting index finished rebuilding."),
            ("Identified", 14, 40, "A reporting index was rebuilt during business hours, which slowed chart queries."),
            ("Investigating", 14, 5, "Analytics charts take up to 20 seconds to load.")),
        Incident("webhook-retries", "Webhook retries paused", ServiceStatus.Degraded, 57, 3, 40, 38, "Sofia Ramirez", false, ["webhooks"],
            ("Resolved", 4, 18, "Every queued retry has been delivered. No events were lost."),
            ("Identified", 3, 52, "A stuck worker stopped the retry queue. Retries resume after a restart."),
            ("Investigating", 3, 40, "Failed deliveries are not being retried.")),
        Incident("saml-failures", "SAML sign-in failures", ServiceStatus.Outage, 42, 11, 26, 17, "Jasmine Brooks", false, ["auth"],
            ("Resolved", 11, 43, "Sign-in works again for every identity provider after we rolled back a certificate change."),
            ("Investigating", 11, 26, "People who sign in with SAML see an invalid signature error.")),
        Incident("api-latency", "Increased API and webhook latency", ServiceStatus.Degraded, 14, 18, 2, 46, "Emma Collins", false, ["api", "webhooks"],
            ("Resolved", 18, 48, "Latency is back within target. The tenant now has its own cache partition."),
            ("Identified", 18, 15, "One tenant saturated the shared cache in eu-west-1."),
            ("Investigating", 18, 2, "API responses and webhook deliveries are slower than usual."))
    ];

    /// <summary>The simulated incident: webhooks degraded since 13:58 today, 34 minutes so far, open.</summary>
    public static UptimeIncident SimulatedIncident()
    {
        var incident = new UptimeIncident
        {
            Key = "sim-webhooks",
            Title = "Delayed webhook deliveries",
            Severity = ServiceStatus.Degraded,
            Start = At(89, 13, 58),
            Duration = TimeSpan.FromMinutes(34),
            IsResolved = false,
            Lead = new UptimePerson("Emma Collins"),
            IsExpanded = true
        };
        incident.ServiceKeys.Add("webhooks");
        incident.Updates.Add(new IncidentUpdate("Investigating", At(89, 14, 32),
            "Deliveries to endpoints in eu-west-1 arrive up to 6 minutes late. No events have been dropped.", IncidentTone.Warning));
        return incident;
    }

    private static UptimeService Service(string key, string name, string description, double target, double[] p50, double[] p95)
    {
        // 48 half-hour samples from Sep 21, 15:00 to Sep 22, 14:30 UTC.
        var first = Latest.AddMinutes(-30 * 47);
        var samples = new LatencySample[p50.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = new LatencySample(first.AddMinutes(30 * i), p50[i], p95[i]);
        }
        return new UptimeService(key, name, description, target, samples);
    }

    // Updates are given newest first, as uiarc.dev lists them; the newest one's dot carries the outcome.
    private static UptimeIncident Incident(string key, string title, ServiceStatus severity, int day, int hour, int minute, int minutes,
                                           string lead, bool expanded, string[] services,
                                           params (string Phase, int Hour, int Minute, string Body)[] updates)
    {
        var incident = new UptimeIncident
        {
            Key = key,
            Title = title,
            Severity = severity,
            Start = At(day, hour, minute),
            Duration = TimeSpan.FromMinutes(minutes),
            IsResolved = true,
            Lead = new UptimePerson(lead),
            IsExpanded = expanded
        };
        incident.ServiceKeys.AddRange(services);
        var ongoingTone = severity == ServiceStatus.Outage ? IncidentTone.Danger : IncidentTone.Warning;
        foreach (var (phase, h, m, body) in updates)
        {
            incident.Updates.Add(new IncidentUpdate(phase, At(day, h, m), body, phase == "Resolved" ? IncidentTone.Success : ongoingTone));
        }
        return incident;
    }
}
