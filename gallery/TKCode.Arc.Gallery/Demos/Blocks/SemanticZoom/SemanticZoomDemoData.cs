using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// An invented city transit operator, "Harbor Line Transit": 5 teams, 22 projects, about 150 tasks and 24 people, built
/// from a seeded generator (mulberry32, seed 20261022, "today" 2026-09-21) so every run shows the same map. Two people in
/// three get a generated gradient picture, the rest show their initials.
/// </summary>
public static class SemanticZoomDemoData
{
    /// <summary>The generator's seed.</summary>
    public const uint Seed = 20261022;

    /// <summary>The day the data is generated against: done tasks are due before it, open ones after.</summary>
    public static readonly DateOnly Today = new(2026, 9, 21);

    private static SemanticZoomOrganization? s_shared;

    /// <summary>One shared organization for the docs page (built on first use).</summary>
    public static SemanticZoomOrganization Shared => s_shared ??= Create();

    private static readonly (string Team, string Description, string[] Roles, (string Name, string Pitch, string[] Nouns)[] Projects)[] Teams =
    [
        ("Operations", "Keeps buses, drivers and depots running to plan.",
            ["Operations lead", "Depot supervisor", "Duty manager", "Scheduler", "Control room analyst"],
            [
                ("Night bus pilot", "Three late routes run through the night for twelve weeks.",
                    ["route 14 timetable", "driver briefing pack", "stop signage", "fare gate settings", "pilot survey", "cost estimate", "late-shift rota", "safety walk", "rider notice"]),
                ("Driver roster tool", "Drivers see and swap their shifts on their phones.",
                    ["shift rules", "leave calendar", "swap requests", "payroll export", "rota templates", "union feedback", "login flow", "overtime report"]),
                ("Incident hotline", "One number for every disruption, answered around the clock.",
                    ["call scripts", "escalation tree", "on-call rota", "hotline number", "incident log", "response targets", "weekend cover", "training deck"]),
                ("Depot shift handover", "Nothing gets lost between the early and the late shift.",
                    ["handover checklist", "key cabinet", "vehicle status board", "defect notes", "handover times", "radio channel", "depot map", "shift summary"]),
                ("Weather response plan", "Service keeps moving through snow, floods and heat.",
                    ["snow routes", "gritting contract", "flood diversions", "heat checklist", "rider alerts", "depot sandbags", "storm briefing", "service levels"]),
                ("Fare inspection audits", "Inspections go where fare evasion actually happens.",
                    ["inspection rounds", "evasion sample", "ticket scanners", "audit report", "inspector rota", "appeal process", "fine notices", "hotspot map"])
            ]),
        ("Fleet", "Vehicles, charging and everything that wears out.",
            ["Fleet lead", "Fleet engineer", "Charging specialist", "Maintenance planner", "Workshop technician"],
            [
                ("Depot charging bays", "Forty electric buses charge overnight at the north depot.",
                    ["charger order", "grid connection", "bay layout", "cable trays", "charging schedule", "fire plan", "power meters", "site survey"]),
                ("Brake wear telemetry", "Brakes report their own wear before they need a workshop.",
                    ["sensor kit", "data feed", "wear thresholds", "alert rules", "pilot buses", "fleet dashboard", "workshop handbook", "vendor contract"]),
                ("Spare parts forecasting", "The right parts are on the shelf before a bus waits for them.",
                    ["parts catalogue", "demand model", "reorder points", "supplier list", "stock count", "lead times", "forecast report", "bin labels"]),
                ("Tram refurbishment", "The oldest trams get new interiors and another decade of service.",
                    ["seat covers", "interior lighting", "door seals", "paint scheme", "test run", "wheel profiles", "handrails", "floor panels"]),
                ("Cleaning cycle upgrade", "Every vehicle is cleaned on a cycle riders can notice.",
                    ["cleaning rota", "wash bay", "supplies order", "quality checks", "night crew", "chemical list", "deep clean plan", "checklist app"])
            ]),
        ("Stations", "Stops, platforms and the spaces around them.",
            ["Stations lead", "Station planner", "Accessibility advisor", "Facilities engineer", "Signage designer"],
            [
                ("Accessible boarding ramps", "Step-free boarding at the fifty busiest stops.",
                    ["ramp prototype", "kerb heights", "stop shortlist", "wheelchair trial", "ramp supplier", "install plan", "rider panel", "inspection form"]),
                ("Platform signage", "Riders find their platform without asking anyone.",
                    ["sign templates", "wayfinding audit", "braille tiles", "sign schedule", "print order", "platform numbers", "exit signs", "type sizes"]),
                ("Bike parking expansion", "Covered bike parking at every rail interchange.",
                    ["rack locations", "cycle counts", "shelter design", "planning permit", "rack order", "lighting plan", "lock-up survey", "launch notice"]),
                ("Lighting retrofit", "Brighter, cheaper station lighting on timers.",
                    ["fixture survey", "LED order", "timer settings", "power baseline", "night photos", "contractor brief", "install windows", "savings report"])
            ]),
        ("Rider experience", "Information riders see before and during a trip.",
            ["Rider experience lead", "Service designer", "Content writer", "Product designer", "Front-end engineer"],
            [
                ("Timetable redesign", "Printed and online timetables anyone can read in seconds.",
                    ["grid layout", "route colours", "print proofs", "rider testing", "stop index", "map legend", "online version", "holiday timetable"]),
                ("Live arrival boards", "Real arrival times on screens at the busiest stops.",
                    ["board hardware", "arrivals feed", "display rules", "outage message", "install sites", "brightness test", "maintenance plan", "accuracy check"]),
                ("Lost property desk", "Lost items find their owners within a week.",
                    ["intake form", "storage shelves", "item database", "return process", "desk hours", "disposal rules", "rider notice", "staff guide"]),
                ("Multilingual announcements", "Key announcements in the five most spoken languages.",
                    ["language list", "voice recordings", "translation glossary", "audio levels", "stop names", "recording booth", "rider feedback", "rollout plan"])
            ]),
        ("Planning", "Studies that decide where service goes next.",
            ["Planning lead", "Data analyst", "Transport modeller", "Survey researcher"],
            [
                ("Route 12 realignment", "Route 12 runs closer to the new hospital.",
                    ["route options", "stop changes", "travel times", "public meeting", "traffic study", "consultation report", "driver trial", "network map"]),
                ("Ridership survey", "A yearly picture of who rides, where and why.",
                    ["questionnaire", "sample plan", "survey tablets", "fieldwork rota", "data cleaning", "results deck", "open data export", "privacy notice"]),
                ("Transfer hub study", "Where a new hub would shorten the most journeys.",
                    ["site options", "walking times", "transfer counts", "cost model", "concept sketches", "stakeholder list", "hub shortlist", "final report"])
            ])
    ];

    private static readonly string[] People =
    [
        "Mara Ostrov", "Tobias Reyes", "Linh Pham", "Kofi Mensah", "Ines Carvalho",
        "Rustam Aliyev", "Hana Sato", "Noor Haddad", "Elio Bianchi", "Zainab Okoye",
        "Pavel Novak", "Sunniva Berg", "Mateus Lima", "Aroha Tane", "Yusuf Demir",
        "Greta Lindqvist", "Tariq Rahman", "Odile Martin", "Kenji Arai", "Priya Nair",
        "Lucas Weber", "Amara Diallo", "Soren Holm", "Camille Fournier"
    ];

    private static readonly string[] Verbs =
    [
        "Draft", "Review", "Measure", "Order", "Test", "Publish", "Survey", "Install", "Calibrate", "Schedule", "Document",
        "Train", "Prototype", "Audit", "Negotiate"
    ];

    private static readonly string[] ChecklistPool =
    [
        "Draft the brief", "Get sign-off", "Walk the route", "Check the budget", "Book the contractor", "Brief the drivers",
        "Test on one route", "Update the docs", "Share with riders", "Collect feedback", "Order materials", "Confirm the dates",
        "Review with the lead", "Fix open issues", "Publish the notice"
    ];

    private static readonly SemanticZoomTaskStatus[] Statuses =
    [
        SemanticZoomTaskStatus.Done, SemanticZoomTaskStatus.InProgress, SemanticZoomTaskStatus.InReview,
        SemanticZoomTaskStatus.Todo, SemanticZoomTaskStatus.Blocked
    ];

    private static readonly double[] StatusWeights = [0.44, 0.20, 0.10, 0.18, 0.06];

    /// <summary>Sort order of tasks on a card: done, in review, in progress, blocked, to do.</summary>
    private static int Rank(SemanticZoomTaskStatus status) => status switch
    {
        SemanticZoomTaskStatus.Done => 0,
        SemanticZoomTaskStatus.InReview => 1,
        SemanticZoomTaskStatus.InProgress => 2,
        SemanticZoomTaskStatus.Blocked => 3,
        _ => 4
    };

    /// <summary>Builds a fresh organization (the same one on every call).</summary>
    public static SemanticZoomOrganization Create()
    {
        var seed = new Mulberry32(Seed);
        var counts = TaskCounts();
        var projectIndex = 0;
        var org = new SemanticZoomOrganization { Title = "Harbor Line Transit", Key = "harbor-line" };
        var personIndex = 0;
        foreach (var (teamName, teamDescription, roles, projects) in Teams)
        {
            // The team's people: its lead first.
            var pool = new List<SemanticZoomPerson>();
            for (var i = 0; i < roles.Length; i++)
            {
                var name = People[personIndex++];
                var person = new SemanticZoomPerson(name, roles[i]);
                if (personIndex % 3 != 0)
                {
                    person.Avatar = GradientAvatar(name);
                }
                pool.Add(person);
            }
            var team = new SemanticZoomTeam { Title = teamName, Description = teamDescription, Lead = pool[0] };
            foreach (var (projectName, pitch, nouns) in projects)
            {
                var project = new SemanticZoomProject
                {
                    Title = projectName,
                    Description = pitch,
                    Lead = pool[seed.Next(pool.Count)]
                };
                var count = counts[projectIndex++];
                var tasks = new List<SemanticZoomTask>(count);
                var usedNouns = new HashSet<string>();
                var usedTitles = new HashSet<string>();
                var blocked = 0;
                DateOnly? latestOpen = null;
                DateOnly? latestDone = null;
                for (var i = 0; i < count; i++)
                {
                    var status = Statuses[seed.Weighted(StatusWeights)];
                    if (status == SemanticZoomTaskStatus.Blocked && ++blocked > 2)
                    {
                        status = SemanticZoomTaskStatus.Todo;
                    }
                    // A fresh noun while there are any, and never the same title twice in a project.
                    string noun;
                    do
                    {
                        noun = nouns[seed.Next(nouns.Length)];
                    }
                    while (!usedNouns.Add(noun) && usedNouns.Count < nouns.Length);
                    string verb;
                    do
                    {
                        verb = Verbs[seed.Next(Verbs.Length)];
                    }
                    while (!usedTitles.Add($"{verb} {noun}"));
                    var due = status == SemanticZoomTaskStatus.Done
                        ? new DateOnly(2026, 8, 28).AddDays(seed.Next(24))
                        : Today.AddDays(1 + seed.Next(45));
                    var task = new SemanticZoomTask
                    {
                        Title = $"{verb} {noun}",
                        Status = status,
                        Assignee = pool[seed.Next(pool.Count)],
                        Reviewer = project.Lead,
                        Due = due,
                        Points = seed.Pick([1, 2, 3, 5, 8]),
                        CommentCount = seed.Next(12)
                    };
                    var items = 3 + seed.Next(3);
                    var ticked = status switch
                    {
                        SemanticZoomTaskStatus.Done => items,
                        SemanticZoomTaskStatus.InReview => items - 1,
                        SemanticZoomTaskStatus.InProgress => 1 + seed.Next(Math.Max(1, items - 2)),
                        SemanticZoomTaskStatus.Blocked => 1,
                        _ => 0
                    };
                    var start = seed.Next(ChecklistPool.Length);
                    for (var c = 0; c < items; c++)
                    {
                        task.Checklist.Add(new SemanticZoomChecklistItem(ChecklistPool[(start + c * 4) % ChecklistPool.Length], c < ticked));
                    }
                    if (status == SemanticZoomTaskStatus.Done)
                    {
                        latestDone = latestDone is null || due > latestDone ? due : latestDone;
                    }
                    else
                    {
                        latestOpen = latestOpen is null || due > latestOpen ? due : latestOpen;
                    }
                    tasks.Add(task);
                }
                // Done first, then in review, in progress, blocked, to do (stable within a status).
                foreach (var task in tasks.Select((t, i) => (t, i)).OrderBy(p => Rank(p.t.Status)).ThenBy(p => p.i).Select(p => p.t))
                {
                    project.Tasks.Add(task);
                }
                project.Due = latestOpen ?? latestDone;
                project.Health = blocked switch
                {
                    0 => SemanticZoomHealth.OnTrack,
                    1 => SemanticZoomHealth.AtRisk,
                    _ => SemanticZoomHealth.OffTrack
                };
                team.Projects.Add(project);
            }
            org.Teams.Add(team);
        }
        return org;
    }

    /// <summary>
    /// Tasks per project: round(triangular(4, 7, 10)) from their own stream (so the other draws cannot shift them), then
    /// nudged one task at a time (largest projects first down, smallest first up) until the total is 148–156.
    /// </summary>
    private static int[] TaskCounts()
    {
        var random = new Mulberry32(Seed ^ 0x9E3779B9u);
        var counts = new int[Teams.Sum(t => t.Projects.Length)];
        for (var i = 0; i < counts.Length; i++)
        {
            counts[i] = Math.Clamp((int)Math.Round(random.Triangular(4, 7, 10)), 4, 10);
        }
        while (counts.Sum() > 156)
        {
            counts[Array.IndexOf(counts, counts.Max())]--;
        }
        while (counts.Sum() < 148)
        {
            counts[Array.IndexOf(counts, counts.Min())]++;
        }
        return counts;
    }

    /// <summary>A generated picture: a diagonal two-tone gradient whose hue comes from the name.</summary>
    public static IImage GradientAvatar(string name)
    {
        uint hash = 2166136261;
        foreach (var c in name)
        {
            hash = (hash ^ c) * 16777619;
        }
        var hue = hash % 360;
        var light = new HslColor(1, hue, 0.55, 0.70).ToRgb();
        var deep = new HslColor(1, (hue + 38) % 360, 0.50, 0.42).ToRgb();
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(light, 0), new GradientStop(deep, 1) }
        };
        return new DrawingImage(new GeometryDrawing { Geometry = new RectangleGeometry(new Rect(0, 0, 64, 64)), Brush = brush });
    }

    /// <summary>Mulberry32, the generator of the Showpiece data recipes (matches its JavaScript original bit for bit).</summary>
    private sealed class Mulberry32
    {
        private uint _state;

        public Mulberry32(uint seed) => _state = seed;

        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int Next(int count) => Math.Min(count - 1, (int)(NextDouble() * count));

        public T Pick<T>(T[] values) => values[Next(values.Length)];

        public int Weighted(double[] weights)
        {
            var total = weights.Sum();
            var r = NextDouble() * total;
            for (var i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r < 0)
                {
                    return i;
                }
            }
            return weights.Length - 1;
        }

        public double Triangular(double min, double mode, double max)
        {
            var u = NextDouble();
            var f = (mode - min) / (max - min);
            return u < f
                ? min + Math.Sqrt(u * (max - min) * (mode - min))
                : max - Math.Sqrt((1 - u) * (max - min) * (max - mode));
        }
    }
}
