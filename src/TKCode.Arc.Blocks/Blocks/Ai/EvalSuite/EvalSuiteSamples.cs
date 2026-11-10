using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="EvalSuite"/> (its defaults): fourteen cases for the support replies of Larkway, a
/// made-up bike share, three prompt versions with every output already graded, and the simulated runner used when no
/// <see cref="EvalSuite.RunCaseAsync"/> is set.
/// </summary>
public static class EvalSuiteSamples
{
    /// <summary>The sample suite's title: "Larkway support replies".</summary>
    public const string Title = "Larkway support replies";

    /// <summary>The sample baseline: "v12".</summary>
    public const string BaselineId = "v12";

    /// <summary>The sample candidate: "v13".</summary>
    public const string CandidateId = "v13";

    /// <summary>How long a simulated case takes before its jitter: 420 ms.</summary>
    public static readonly TimeSpan SimulatedCaseTime = TimeSpan.FromMilliseconds(420);

    private sealed record Sample(string Id, string Title, string Tag, EvalCheckKind Check, string Expected, string Input,
        string V11, string V12, string V13, double S11 = 0, double S12 = 0, double S13 = 0);

    private static readonly Sample[] s_samples =
    [
        new("refund", "Refund window", "Policy", EvalCheckKind.Contains, "30 days",
            "I rented a bike 20 days ago and was charged twice. Can I still get my money back?",
            "Sorry about the double charge. You can ask for a refund within a month of the ride, and I've flagged this one for you.",
            "Sorry about the double charge! Refunds are possible within 30 days of a ride, so you're covered. I've sent the request to billing.",
            "Sorry for the double charge. You're within the 30 days for refunds, and I've sent the request to billing."),
        new("greeting", "Greets by name", "Tone", EvalCheckKind.Contains, "Mara",
            "Hi, it's Mara. The lock on bike 2207 won't open.",
            "Hi Mara, sorry about that! Hold the lock button for five seconds to reset it. If it still won't open, end the ride in the app and we'll waive the fee.",
            "Hi Mara, sorry about that! Hold the lock button for five seconds to reset it. If it still won't open, end the ride in the app and we'll waive the fee.",
            "Sorry about that. Hold the lock button for five seconds to reset it. If it stays shut, end the ride in the app and we'll waive the fee."),
        new("ride-id", "Asks for the ride ID", "Policy", EvalCheckKind.Regex, @"ride (ID|number)",
            "My ride yesterday cost way more than it should have.",
            "I'm sorry to hear that. Could you send me the ride ID from your receipt? I'll check the time and the price.",
            "I'm sorry to hear that. Could you send me the ride ID from your receipt? I'll check the time and the price.",
            "Sorry about that. Could you share the ride number from your receipt so I can check it?"),
        new("no-promise", "No restock promise", "Safety", EvalCheckKind.Rubric, "Does not promise a time for new bikes at a station",
            "When will you bring new bikes to the Harbor station?",
            "New bikes will be at Harbor by 3 pm today.",
            "I've asked the station team to restock Harbor. I can't promise a time, but the app shows bikes as soon as they're docked.",
            "The station team knows Harbor is empty. I can't give a time, but the app shows new bikes as soon as they arrive.",
            0.20, 0.90, 0.88),
        new("damage", "Reports a loose brake", "Policy", EvalCheckKind.Contains, "support team",
            "The front brake on my bike feels loose.",
            "Please stop riding and end the trip at the nearest station. I've reported the bike to our support team, and you won't be charged for this ride.",
            "Please stop riding and end the trip at the nearest station. I've reported the bike to our support team, and you won't be charged for this ride.",
            "Please stop riding and dock the bike at the nearest station. Our support team will check it, and this ride is free."),
        new("language", "Answers in Spanish", "Tone", EvalCheckKind.Contains, "pausar",
            "¿Puedo pausar mi plan mensual durante el verano?",
            "Yes, you can pause your monthly plan for up to three months in Settings → Plan.",
            "Yes, you can pause your monthly plan for up to three months in Settings → Plan.",
            "Sí, puedes pausar tu plan mensual hasta tres meses en Ajustes → Plan."),
        new("short", "Stays under 60 words", "Tone", EvalCheckKind.Rubric, "Answers in 60 words or fewer",
            "How do I unlock a bike?",
            "Great question! To unlock a bike, first make sure the app is open and Bluetooth is on. Then walk up to any bike with a green light, scan the QR code on the handlebar, and wait for the click. If the light is red, the bike is reserved or needs care, so try another one. Have a lovely ride, and let me know if anything else comes up!",
            "Great question! To unlock a bike, first make sure the app is open and Bluetooth is on. Then walk up to any bike with a green light, scan the QR code on the handlebar, and wait for the click. If the light is red, the bike is reserved or needs care, so try another one. Have a lovely ride, and let me know if anything else comes up!",
            "Open the app, scan the QR code on the handlebar and wait for the click. A red light means the bike is taken, so pick another.",
            0.55, 0.55, 0.92),
        new("price", "Quotes the day pass", "Facts", EvalCheckKind.Exact, "A day pass costs €12.",
            "How much is a day pass?",
            "A day pass costs €12.",
            "A day pass costs €12.",
            "A day pass is €12 and covers unlimited 45-minute rides."),
        new("hours", "Station hours", "Facts", EvalCheckKind.Contains, "6:00 to 23:00",
            "When is the Old Town station open?",
            "The Old Town station is open from 6:00 to 23:00 every day.",
            "The Old Town station is open from 6:00 to 23:00 every day.",
            "Old Town is open 6:00 to 23:00, every day of the week."),
        new("card", "Keeps card numbers private", "Safety", EvalCheckKind.Rubric, "Never repeats the card number back",
            "My card 4417 1234 5678 9113 was declined.",
            "Sorry about that. I can't see card details here, so please add the card again in Settings → Payment and try once more.",
            "Sorry about that. I can't see card details here, so please add the card again in Settings → Payment and try once more.",
            "Sorry about that. Please add the card again in Settings → Payment. For your safety, I never repeat card numbers.",
            0.95, 0.95, 0.97),
        new("pause", "Explains how to pause", "Facts", EvalCheckKind.Contains, "Settings → Plan",
            "Can I pause my membership while I'm away?",
            "Yes. Go to Settings → Plan and choose Pause. You can pause for up to three months.",
            "Yes. Go to Settings → Plan and choose Pause. You can pause for up to three months.",
            "Yes, in Settings → Plan choose Pause; it lasts up to three months."),
        new("apology", "Apologizes once", "Tone", EvalCheckKind.Rubric, "Apologizes once, then solves the problem",
            "The app crashed and charged me for 40 minutes.",
            "I'm so sorry! I'm really sorry for the trouble. I've refunded the 40 minutes, and I'm sorry again.",
            "I'm so sorry! I'm really sorry for the trouble. I've refunded the 40 minutes, and I'm sorry again.",
            "Sorry for the trouble! I'm sorry the app crashed. I've refunded the 40 minutes.",
            0.40, 0.40, 0.62),
        new("lost", "Points to the lost item form", "Policy", EvalCheckKind.Contains, "lost item form",
            "I left my jacket in a bike basket.",
            "Oh no! Tell me the station and I'll ask the team to look.",
            "Oh no! Tell me the station and I'll ask the team to look.",
            "Sorry to hear that. Fill in the lost item form in Help with the station and time, and we'll contact you if it turns up."),
        new("helmet", "Helmet advice, no legal claims", "Safety", EvalCheckKind.Rubric, "Gives safety advice without legal claims",
            "Do I have to wear a helmet?",
            "Yes, it's the law everywhere in the city.",
            "We strongly recommend a helmet on every ride. Rules differ by city, so check the local guidance.",
            "We recommend wearing a helmet on every ride. Rules vary by city, so check local guidance.",
            0.30, 0.86, 0.84),
    ];

    /// <summary>The fourteen sample cases (policy, tone, facts and safety checks of every kind).</summary>
    public static IReadOnlyList<EvalCase> Cases()
    {
        var cases = new EvalCase[s_samples.Length];
        for (var i = 0; i < s_samples.Length; i++)
        {
            var s = s_samples[i];
            cases[i] = new EvalCase { Id = s.Id, Title = s.Title, Tag = s.Tag, Check = s.Check, Expected = s.Expected, Input = s.Input };
        }
        return cases;
    }

    /// <summary>Three prompt versions: v11 (long prompt), v12 (in production) and v13 (shorter prompt).</summary>
    public static IReadOnlyList<EvalVersion> Versions() =>
    [
        new EvalVersion { Id = "v11", Name = "v11", Note = "Long prompt" },
        new EvalVersion { Id = "v12", Name = "v12", Note = "In production" },
        new EvalVersion { Id = "v13", Name = "v13", Note = "Shorter prompt" },
    ];

    /// <summary>
    /// Graded results of every case on all three versions. v12 passes 10 of 14, v13 passes 11: two regressions, three
    /// fixes and one case that fails on both; v11 passes 7.
    /// </summary>
    public static IReadOnlyList<EvalResult> Results()
    {
        var cases = Cases();
        var list = new List<EvalResult>(cases.Count * 3);
        foreach (var version in new[] { "v11", "v12", "v13" })
        {
            foreach (var evalCase in cases)
            {
                if (Output(evalCase.Id, version) is { } output)
                {
                    list.Add(EvalSuiteGrader.Grade(evalCase, version, output.Text, output.Score, DurationOf(output.Text),
                        EvalSuiteLabels.Default, CultureInfo.InvariantCulture));
                }
            }
        }
        return list;
    }

    /// <summary>The sample output and rubric score of a case on a version, or null when there is none.</summary>
    /// <param name="caseId">A sample case id.</param>
    /// <param name="versionId">"v11", "v12" or "v13".</param>
    public static (string Text, double? Score)? Output(string caseId, string versionId)
    {
        foreach (var s in s_samples)
        {
            if (s.Id != caseId)
            {
                continue;
            }
            double? score(double value) => s.Check == EvalCheckKind.Rubric ? value : null;
            return versionId switch
            {
                "v11" => (s.V11, score(s.S11)),
                "v12" => (s.V12, score(s.S12)),
                "v13" => (s.V13, score(s.S13)),
                _ => null
            };
        }
        return null;
    }

    /// <summary>
    /// A runner that waits like a model would and answers from the samples: a known case and version gives its sample
    /// output, anything else echoes the expected text (a rubric case scores 0.8). Graded with the default labels.
    /// </summary>
    /// <param name="delay">The wait; receives the time and the cancellation token.</param>
    public static Func<EvalCase, EvalVersion, CancellationToken, Task<EvalResult>> SimulatedRunner(
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        return async (evalCase, version, cancellation) =>
        {
            var output = Output(evalCase.Id, version.Id) ?? (evalCase.Expected, evalCase.Check == EvalCheckKind.Rubric ? 0.8 : null);
            var time = SimulatedCaseTime + TimeSpan.FromMilliseconds(Jitter(evalCase.Id + version.Id));
            await delay(time, cancellation);
            return EvalSuiteGrader.Grade(evalCase, version.Id, output.Text, output.Score, DurationOf(output.Text),
                EvalSuiteLabels.Default, CultureInfo.InvariantCulture);
        };
    }

    private static TimeSpan DurationOf(string output) => TimeSpan.FromMilliseconds(640 + output.Length * 9);

    private static int Jitter(string key)
    {
        var hash = 17;
        foreach (var c in key)
        {
            hash = unchecked(hash * 31 + c);
        }
        return (int)((uint)hash % 380);
    }
}
