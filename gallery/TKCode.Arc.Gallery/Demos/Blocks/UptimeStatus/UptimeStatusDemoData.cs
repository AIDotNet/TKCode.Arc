namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The UptimeStatus demos' data (all invented, seeded and deterministic): four services of a made-up platform with 48
/// half-hourly latency samples each, five incidents over the last 90 days, and a simulated incident on the Events service
/// that the Preview demo walks through (start, resolve, reset).
/// </summary>
public static class UptimeStatusDemoData
{
    /// <summary>The page's name.</summary>
    public const string PageName = "Larkspur status";

    /// <summary>The newest sample's time (and the page's "now"): Mar 14, 14:30 UTC.</summary>
    public static readonly DateTimeOffset Now = new(2027, 3, 14, 14, 30, 0, TimeSpan.Zero);

    /// <summary>When the page says it was updated.</summary>
    public static readonly DateTimeOffset UpdatedAt = new(2027, 3, 14, 14, 32, 0, TimeSpan.Zero);

    private static readonly string[] s_keys = ["edge", "console", "events", "identity"];

    /// <summary>The four services with their latency samples.</summary>
    public static UptimeService[] Services()
    {
        var random = new Mulberry32(20270314);
        return
        [
            Service(random, "edge", "Edge", "Routing and TLS", 350, 80, 210, 0.2),
            Service(random, "console", "Console", "console.larkspur.test", 900, 200, 520, 1.1),
            Service(random, "events", "Events", "Queue fan-out", 700, 120, 380, 2.3),
            Service(random, "identity", "Identity", "Login and API keys", 250, 45, 140, 3.4)
        ];
    }

    /// <summary>The five incidents of the last 90 days (the first one is open).</summary>
    public static UptimeIncident[] Incidents()
    {
        var today = new DateTimeOffset(2027, 3, 14, 0, 0, 0, TimeSpan.Zero);
        return
        [
            Incident("edge-5xx", "Elevated 5xx on Edge", ServiceStatus.Outage, today.AddDays(-22).AddHours(7).AddMinutes(40), 19, ["edge"],
                new UptimePerson("Noor Haddad"), expanded: true,
                ("Investigating", 0, "Some requests are answered with a 502 at the edge.", IncidentTone.Warning),
                ("Identified", 12, "A load balancer node failed to rotate its certificate.", IncidentTone.Warning),
                ("Monitoring", 24, "Traffic is moved off the node and the certificate is reissued; watching error rates.", IncidentTone.Warning),
                ("Resolved", 39, "Error rates have been at baseline for fifteen minutes.", IncidentTone.Success)),
            Incident("console-slow", "Console dashboards slow to load", ServiceStatus.Degraded, today.AddDays(-35).AddHours(16).AddMinutes(20), 61, ["console"],
                new UptimePerson("Ilse Brandt"), expanded: false,
                ("Investigating", 0, "Dashboards take over ten seconds to render for some workspaces.", IncidentTone.Warning),
                ("Resolved", 61, "A runaway query was stopped and the cache was warmed again.", IncidentTone.Success)),
            Incident("events-fanout", "Delayed event fan-out", ServiceStatus.Degraded, today.AddDays(-48).AddHours(2).AddMinutes(10), 29, ["events"],
                new UptimePerson("Tomas Ilves"), expanded: false,
                ("Investigating", 0, "Subscribers receive events several minutes late.", IncidentTone.Warning),
                ("Resolved", 29, "Consumers were scaled out and the backlog is clear.", IncidentTone.Success)),
            Incident("sso-login", "Login failures for SSO users", ServiceStatus.Outage, today.AddDays(-63).AddHours(12).AddMinutes(45), 14, ["identity"],
                new UptimePerson("Ravi Menon"), expanded: false,
                ("Investigating", 0, "Sign-in through single sign-on fails with a timeout.", IncidentTone.Danger),
                ("Resolved", 14, "The identity provider connection was restored.", IncidentTone.Success)),
            Incident("edge-events-latency", "Edge and Events latency increase", ServiceStatus.Degraded, today.AddDays(-81).AddHours(19).AddMinutes(5), 41, ["edge", "events"],
                new UptimePerson("Mara Voss"), expanded: false,
                ("Investigating", 0, "Response times are higher than usual at the edge and in event delivery.", IncidentTone.Warning),
                ("Resolved", 41, "A noisy neighbour was moved away; latency is back to normal.", IncidentTone.Success))
        ];
    }

    /// <summary>The same five incidents, newest first (as the block lists them).</summary>
    public static UptimeIncident[] AllClear() => Incidents();

    /// <summary>Services and no incidents at all (an all-green status page).</summary>
    public static UptimeIncident[] NoIncidents() => [];

    /// <summary>A service whose current outage is still going.</summary>
    public static UptimeIncident OngoingOutage()
    {
        var start = Now.AddMinutes(-47);
        var incident = new UptimeIncident
        {
            Key = "console-down",
            Title = "Console is not loading",
            Severity = ServiceStatus.Outage,
            Start = start,
            Duration = TimeSpan.FromMinutes(47),
            IsResolved = false,
            Lead = new UptimePerson("Ilse Brandt"),
            IsExpanded = true
        };
        incident.ServiceKeys.Add("console");
        incident.Updates.Add(new IncidentUpdate("Investigating", start, "Visitors get an error page when they open the console.", IncidentTone.Danger));
        incident.Updates.Add(new IncidentUpdate("Identified", start.AddMinutes(18), "A bad deploy is the cause; we are rolling it back.", IncidentTone.Danger));
        return incident;
    }

    /// <summary>The latency of the Events service while the simulated incident is open (the last four samples jump).</summary>
    public static LatencySample[] EventsDuringIncident(IReadOnlyList<LatencySample> calm)
    {
        var samples = calm.ToArray();
        double[] p95 = [1.6, 2.8, 4.4, 5.1];
        double[] p50 = [1.5, 2.1, 2.8, 3.2];
        for (var i = 0; i < 4; i++)
        {
            var k = samples.Length - 4 + i;
            // Relative to the service's baseline (not the noisy sample), so the chart's domain lands at 0 to 2,500.
            samples[k] = new LatencySample(samples[k].Time, Math.Round(120 * p50[i]), Math.Round(380 * p95[i]));
        }
        return samples;
    }

    /// <summary>The simulated incident (Events, degraded, begun 34 minutes ago).</summary>
    public static UptimeIncident SimulatedIncident()
    {
        var start = UpdatedAt.AddMinutes(-34);
        var incident = new UptimeIncident
        {
            Key = "sim-events",
            Title = "Event delivery delayed",
            Severity = ServiceStatus.Degraded,
            Start = start,
            Duration = TimeSpan.FromMinutes(34),
            IsResolved = false,
            Lead = new UptimePerson("Tomas Ilves"),
            IsExpanded = true
        };
        incident.ServiceKeys.Add("events");
        incident.Updates.Add(new IncidentUpdate("Investigating", start.AddMinutes(7), "Some subscribers receive events up to five minutes late; none are lost.", IncidentTone.Warning));
        return incident;
    }

    /// <summary>The update that closes the simulated incident.</summary>
    public static IncidentUpdate ResolutionUpdate() =>
        new("Resolved", UpdatedAt.AddMinutes(19), "Consumers were scaled out and the backlog is clear.", IncidentTone.Success);

    private static UptimeService Service(Mulberry32 random, string key, string name, string description, double target, double p50Base, double p95Base, double phase)
    {
        const int count = 48;
        var first = Now.AddMinutes(-30 * (count - 1));
        var samples = new List<LatencySample>(count);
        var untilSpike = 9 + random.Next(6);
        for (var i = 0; i < count; i++)
        {
            var angle = 2 * Math.PI * i / count + phase;
            var p95 = p95Base * (1 + 0.18 * Math.Sin(angle) + 0.08 * random.Gaussian(0, 1));
            var p50 = p50Base * (1 + 0.10 * Math.Sin(angle) + 0.03 * random.Gaussian(0, 1));
            if (--untilSpike <= 0)
            {
                p95 *= 1 + 0.25 + 0.15 * random.NextDouble();
                untilSpike = 9 + random.Next(6);
            }
            p95 = Math.Round(p95);
            p50 = Math.Round(Math.Min(p50, 0.55 * p95));
            samples.Add(new LatencySample(first.AddMinutes(30 * i), p50, p95));
        }
        return new UptimeService(key, name, description, target, samples);
    }

    private static UptimeIncident Incident(string key, string title, ServiceStatus severity, DateTimeOffset start, int minutes, string[] services,
        UptimePerson lead, bool expanded, params (string Phase, int Offset, string Body, IncidentTone Tone)[] updates)
    {
        var incident = new UptimeIncident
        {
            Key = key,
            Title = title,
            Severity = severity,
            Start = start,
            Duration = TimeSpan.FromMinutes(minutes),
            IsResolved = true,
            Lead = lead,
            IsExpanded = expanded
        };
        incident.ServiceKeys.AddRange(services);
        foreach (var (phase, offset, body, tone) in updates)
        {
            incident.Updates.Add(new IncidentUpdate(phase, start.AddMinutes(offset), body, tone));
        }
        return incident;
    }

    /// <summary>Keys of the services, in order.</summary>
    public static IReadOnlyList<string> Keys => s_keys;

    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;
        private bool _hasSpare;
        private double _spare;

        public double NextDouble()
        {
            _state += 0x6D2B79F5;
            var t = _state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return ((t ^ (t >> 14)) / 4294967296.0);
        }

        public int Next(int n) => (int)(NextDouble() * n);

        public double Gaussian(double mean, double sd)
        {
            if (_hasSpare)
            {
                _hasSpare = false;
                return mean + sd * _spare;
            }
            double u;
            double v;
            double s;
            do
            {
                u = NextDouble() * 2 - 1;
                v = NextDouble() * 2 - 1;
                s = u * u + v * v;
            }
            while (s >= 1 || s == 0);
            var m = Math.Sqrt(-2 * Math.Log(s) / s);
            _spare = v * m;
            _hasSpare = true;
            return mean + sd * u * m;
        }
    }
}

/// <summary>
/// The Preview demo's simulation, driven by one button: <see cref="Advance"/> starts an incident on the Events service, the
/// next call resolves it, the next removes it again. Each step changes the block's data and its header text the way a
/// monitoring feed would; the block itself does the rest (colours, numbers, chart, list).
/// </summary>
public sealed class UptimeIncidentSimulation
{
    private readonly UptimeStatus _block;
    private readonly UptimeService _events;
    private readonly LatencySample[] _calm;
    private UptimeIncident? _incident;

    /// <summary>Wraps a block whose services include "events".</summary>
    public UptimeIncidentSimulation(UptimeStatus block)
    {
        _block = block;
        _events = block.Services.First(s => s.Key == "events");
        _calm = _events.Latency.ToArray();
    }

    /// <summary>The phase the next press performs: 0 start, 1 resolve, 2 reset.</summary>
    public int Phase { get; private set; }

    /// <summary>The button's label for the next press.</summary>
    public string ActionText => Phase switch { 0 => "Simulate incident", 1 => "Simulate resolution", _ => "Reset demo" };

    /// <summary>Performs the next step.</summary>
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
        _incident = UptimeStatusDemoData.SimulatedIncident();
        _events.Latency.Clear();
        _events.Latency.AddRange(UptimeStatusDemoData.EventsDuringIncident(_calm));
        _block.Incidents.Add(_incident);
        _block.SelectedService = _events;
        _block.StatusSummary = "Events delayed since 13:58 UTC";
    }

    private void Resolve()
    {
        if (_incident is null)
        {
            return;
        }
        _incident.Updates.Add(UptimeStatusDemoData.ResolutionUpdate());
        _incident.Duration = TimeSpan.FromMinutes(53);
        _incident.IsResolved = true;
        _block.StatusSummary = "Events recovered at 14:51 UTC";
    }

    private void Reset()
    {
        if (_incident is not null)
        {
            _block.Incidents.Remove(_incident);
            _incident = null;
        }
        _events.Latency.Clear();
        _events.Latency.AddRange(_calm);
        _block.StatusSummary = null;
    }
}
