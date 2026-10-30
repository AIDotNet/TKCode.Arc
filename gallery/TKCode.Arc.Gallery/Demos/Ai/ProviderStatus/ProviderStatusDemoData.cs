namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Sample status data for the ProviderStatus demos. The provider names, the models and the status page components come
/// from <see cref="AiModelCatalog"/>; every incident, latency and uptime figure is invented and labeled as sample data, never
/// any provider's real history. Incident titles start with "Sample". Marks are monograms.
/// </summary>
internal static class ProviderStatusDemoData
{
    private const int WindowDays = 30;

    /// <summary>The footnote under every sample card.</summary>
    public const string Footnote =
        "Sample data: the providers and their status page components are real; every incident, latency and uptime figure is invented, not their history.";

    /// <summary>
    /// The story the preview plays, one step per simulated check, built relative to the moment it is read: an outage of the
    /// first provider and a slowdown of the second resolve, the third has a short incident, and the tenth check fails to
    /// show the error state. Then it starts over.
    /// </summary>
    public static IReadOnlyList<ProviderStatusSimulationStep> Story
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            var steps = new List<ProviderStatusSimulationStep>(StoryLength);
            for (var step = 0; step < StoryLength; step++)
            {
                steps.Add(step == FailingStep
                    ? new ProviderStatusSimulationStep { Fails = true }
                    : new ProviderStatusSimulationStep { Providers = StoryAt(step, now) });
            }
            return steps;
        }
    }

    /// <summary>The id of the row whose incident the preview opens.</summary>
    public static string OutageId => AiModelCatalog.Anthropic.Key;

    /// <summary>Three healthy providers for the states demo.</summary>
    public static IReadOnlyList<ProviderStatusItem> Healthy()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return
        [
            HealthyItem(today, AiModelCatalog.Anthropic, Detail(AiModelCatalog.Anthropic, 2, AiModelCatalog.ClaudeSonnet55), 402),
            HealthyItem(today, AiModelCatalog.OpenAI, Detail(AiModelCatalog.OpenAI, 0, AiModelCatalog.Gpt61Terra), 236),
            HealthyItem(today, AiModelCatalog.Google, Detail(AiModelCatalog.Google, 0, AiModelCatalog.Gemini38Flash), 1180),
        ];
    }

    /// <summary>
    /// The row's line under the name: the model you call and, where the catalog lists them, the status page component
    /// the row follows.
    /// </summary>
    public static string Detail(AiProviderInfo provider, int? component, params AiModelInfo[] models)
    {
        var detail = string.Join(" · ", models.Select(m => m.Name));
        if (component is { } index && index < provider.StatusComponents.Count)
        {
            detail += " · " + provider.StatusComponents[index];
        }
        return detail;
    }

    /// <summary>Thirty days of invented history ending today, with one event on <paramref name="eventDay"/>.</summary>
    public static List<ProviderStatusDay> History(DateOnly today, int eventDay, double eventUptime, string note)
    {
        var days = new List<ProviderStatusDay>();
        for (var d = 0; d < WindowDays; d++)
        {
            days.Add(new ProviderStatusDay
            {
                Date = today.AddDays(d - (WindowDays - 1)),
                Uptime = d == eventDay ? eventUptime : d % 7 == 3 ? 99.96 : 100,
                Note = d == eventDay ? note : null,
            });
        }
        return days;
    }

    private const int StoryLength = 11;

    private const int FailingStep = 9;

    private static ProviderStatusItem HealthyItem(DateOnly today, AiProviderInfo provider, string detail, double latency) => new()
    {
        Id = provider.Key,
        Name = provider.Name,
        Detail = detail,
        LatencyMs = latency,
        Days = Enumerable.Range(0, WindowDays)
            .Select(d => new ProviderStatusDay { Date = today.AddDays(d - (WindowDays - 1)), Uptime = d % 9 == 4 ? 99.95 : 100 })
            .ToList(),
    };

    /// <summary>The six providers at <paramref name="step"/> of the story, with times relative to <paramref name="now"/>.</summary>
    private static IReadOnlyList<ProviderStatusItem> StoryAt(int step, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        // Incident times are whole minutes before now, so they read like a status page.
        var minute = new DateTimeOffset(now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day, now.UtcDateTime.Hour,
            now.UtcDateTime.Minute, 0, TimeSpan.Zero);

        var first = new ProviderStatusItem
        {
            Id = AiModelCatalog.Anthropic.Key,
            Name = AiModelCatalog.Anthropic.Name,
            Detail = Detail(AiModelCatalog.Anthropic, 2, AiModelCatalog.ClaudeSonnet55),
            Health = step switch
            {
                <= 1 => ProviderStatusHealth.Outage,
                <= 3 => ProviderStatusHealth.Degraded,
                _ => ProviderStatusHealth.Operational
            },
            LatencyMs = Latency(412, step, 0, step <= 1 ? 4.6 : step <= 3 ? 1.6 : 1),
            Days = StoryHistory(0, today, step <= 5 ? (98.62, "Sample: elevated error rates since this morning.") : (98.9, "Sample: elevated error rates for 38 minutes."),
                [(6, 99.82, "Sample: slower responses in one region."), (17, 98.4, "Sample: API errors for 52 minutes.")]),
            Incident = step > 5 ? null : Incident("sample-errors", "Sample incident: elevated error rates", minute.AddMinutes(-14),
            [
                (ProviderStatusStage.Investigating, 0, "Elevated error rates are being investigated."),
                (ProviderStatusStage.Identified, 6, "The cause is identified and a fix is rolling out."),
                (ProviderStatusStage.Monitoring, 11, "Error rates are back to normal. Monitoring."),
                (ProviderStatusStage.Resolved, 14, "Resolved. Requests made during the incident can be retried.")
            ], step switch
            {
                0 => 1,
                1 => 2,
                <= 3 => 3,
                _ => 4
            })
        };

        var second = new ProviderStatusItem
        {
            Id = AiModelCatalog.OpenAI.Key,
            Name = AiModelCatalog.OpenAI.Name,
            Detail = Detail(AiModelCatalog.OpenAI, 0, AiModelCatalog.Gpt61Sol),
            Health = step <= 1 ? ProviderStatusHealth.Degraded : ProviderStatusHealth.Operational,
            LatencyMs = Latency(620, step, 1, step <= 1 ? 3.1 : step == 2 ? 1.3 : 1),
            Days = StoryHistory(1, today, (99.74, "Sample: slower responses for about 40 minutes."),
                [(11, 99.86, "Sample: image uploads slower than usual."), (23, 99.5, "Sample: some requests timed out.")]),
            Incident = step > 3 ? null : Incident("sample-slow", "Sample incident: slower responses", minute.AddMinutes(-9),
            [
                (ProviderStatusStage.Investigating, 0, "Responses are slower than usual. Investigating."),
                (ProviderStatusStage.Identified, 4, "Capacity in one region is short; more is coming online."),
                (ProviderStatusStage.Monitoring, 7, "Response times are recovering. Monitoring."),
                (ProviderStatusStage.Resolved, 9, "Response times have been normal for 15 minutes. Resolved.")
            ], step + 1)
        };

        var thirdOpen = step is >= 6 and <= 8;
        var third = new ProviderStatusItem
        {
            Id = AiModelCatalog.Google.Key,
            Name = AiModelCatalog.Google.Name,
            Detail = Detail(AiModelCatalog.Google, 0, AiModelCatalog.Gemini38Flash),
            Health = step == 6 ? ProviderStatusHealth.Degraded : ProviderStatusHealth.Operational,
            LatencyMs = Latency(280, step, 2, step == 6 ? 2.2 : 1),
            Days = StoryHistory(2, today, step >= 6 ? (99.88, "Sample: a short rise in errors.") : (100, null),
                [(3, 99.93, null), (20, 98.7, "Sample: streaming responses stopped early for an hour.")]),
            Incident = !thirdOpen ? null : Incident("sample-streams", "Sample incident: streams ending early", minute.AddMinutes(-5),
            [
                (ProviderStatusStage.Investigating, 0, "Some streaming responses end early. Investigating."),
                (ProviderStatusStage.Monitoring, 3, "A fix is out and streams complete again. Monitoring."),
                (ProviderStatusStage.Resolved, 5, "Resolved.")
            ], step - 5)
        };

        var fourth = new ProviderStatusItem
        {
            Id = AiModelCatalog.MistralAI.Key,
            Name = AiModelCatalog.MistralAI.Name,
            Detail = Detail(AiModelCatalog.MistralAI, 0, AiModelCatalog.MistralMedium35),
            Health = ProviderStatusHealth.Operational,
            LatencyMs = Latency(455, step, 3, 1),
            Days = StoryHistory(3, today, (100, null), [(9, 99.81, "Sample: elevated latency after a network change.")])
        };

        var fifth = new ProviderStatusItem
        {
            Id = AiModelCatalog.DeepSeek.Key,
            Name = AiModelCatalog.DeepSeek.Name,
            Detail = Detail(AiModelCatalog.DeepSeek, 0, AiModelCatalog.DeepSeekV4Pro),
            Health = ProviderStatusHealth.Operational,
            LatencyMs = Latency(1340, step, 4, 1),
            // Monitoring started 24 days ago: the bar pads the older days as No data.
            Days = StoryHistory(4, today, (100, null), [(14, 99.4, "Sample: requests queued longer than usual.")], startDay: 6)
        };

        var sixth = new ProviderStatusItem
        {
            Id = AiModelCatalog.XAi.Key,
            Name = AiModelCatalog.XAi.Name,
            Detail = Detail(AiModelCatalog.XAi, null, AiModelCatalog.Grok47),
            Health = ProviderStatusHealth.Operational,
            LatencyMs = Latency(190, step, 5, 1),
            Days = StoryHistory(5, today, (100, null), [(19, null, "Sample: checks were paused for maintenance.")])
        };

        return [first, second, third, fourth, fifth, sixth];
    }

    /// <summary>A plausible latency: the base, a ±8% wobble per check and a factor while slow.</summary>
    private static double Latency(double baseMs, int check, int provider, double factor)
    {
        var wobble = (Noise(provider * 131 + check * 17) - 0.5) * 0.16;
        return Math.Round(baseMs * factor * (1 + wobble));
    }

    /// <summary>A repeatable number in [0, 1).</summary>
    private static double Noise(int seed)
    {
        unchecked
        {
            var x = (uint)seed * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (x & 0xFFFFFF) / (double)0x1000000;
        }
    }

    /// <summary>
    /// Thirty days ending today: mostly 100% with a few near misses, the listed <paramref name="events"/> (days counted from
    /// the oldest) and today's value. Days before <paramref name="startDay"/> are left out.
    /// </summary>
    private static List<ProviderStatusDay> StoryHistory(int provider, DateOnly today, (double? Uptime, string? Note) todayValue,
        (int Day, double? Uptime, string? Note)[] events, int startDay = 0)
    {
        var days = new List<ProviderStatusDay>(WindowDays);
        var first = today.AddDays(-(WindowDays - 1));
        for (var d = startDay; d < WindowDays; d++)
        {
            double? uptime = 100;
            string? note = null;
            var noise = Noise(provider * 977 + d * 31);
            if (noise < 0.22)
            {
                uptime = Math.Round(99.94 + noise * 0.25, 2);
            }
            foreach (var e in events)
            {
                if (e.Day == d)
                {
                    uptime = e.Uptime;
                    note = e.Note;
                }
            }
            if (d == WindowDays - 1)
            {
                uptime = todayValue.Uptime;
                note = todayValue.Note;
            }
            days.Add(new ProviderStatusDay { Date = first.AddDays(d), Uptime = uptime, Note = note });
        }
        return days;
    }

    private static ProviderStatusIncident Incident(string id, string title, DateTimeOffset start,
        (ProviderStatusStage Stage, int Minutes, string Message)[] script, int posted)
    {
        var updates = new List<ProviderStatusUpdate>();
        for (var i = 0; i < Math.Clamp(posted, 0, script.Length); i++)
        {
            updates.Add(new ProviderStatusUpdate { Stage = script[i].Stage, At = start.AddMinutes(script[i].Minutes), Message = script[i].Message });
        }
        return new ProviderStatusIncident { Id = id, Title = title, Updates = updates };
    }
}
