namespace TKCode.Arc;

/// <summary>Invented quarterly goals <see cref="GoalTracker"/> shows while it has none of its own, dated around the period.</summary>
internal static class GoalTrackerSamples
{
    public const string Title = "Quarter goals";

    public static GoalTrackerGoal[] Goals(DateTime start, DateTime end)
    {
        var span = (end - start).TotalDays;
        DateTime At(double fraction) => start.AddDays(Math.Round(span * fraction));

        var rina = new ProjectPerson("Rina Kato");
        var samir = new ProjectPerson("Samir Aziz");
        var elena = new ProjectPerson("Elena Duarte");
        var kofi = new ProjectPerson("Kofi Mensah");
        var lise = new ProjectPerson("Lise Berg");

        GoalTrackerKeyResult Kr(string title, ProjectPerson owner, double s, double t, string? unit, GoalTrackerConfidence confidence,
            params (double Fraction, double Value, GoalTrackerConfidence Confidence, string Note)[] checkIns)
        {
            var kr = new GoalTrackerKeyResult(title, s, t, checkIns.Length > 0 ? checkIns[^1].Value : s, unit) { Owner = owner, Confidence = confidence };
            foreach (var c in checkIns)
            {
                kr.CheckIns.Add(new GoalTrackerCheckIn(At(c.Fraction), c.Value, c.Confidence, c.Note, owner.Name));
            }
            return kr;
        }

        return
        [
            new GoalTrackerGoal("Make onboarding feel effortless", "Growth", rina)
            {
                KeyResults =
                [
                    Kr("Teams that finish setup in a day", rina, 42, 70, "%", GoalTrackerConfidence.OnTrack,
                        (0.15, 46, GoalTrackerConfidence.OnTrack, "New checklist shipped"),
                        (0.35, 53, GoalTrackerConfidence.OnTrack, "Import step is faster"),
                        (0.55, 60, GoalTrackerConfidence.OnTrack, "Templates helped small teams")),
                    Kr("Support tickets in the first week", samir, 380, 200, "tickets", GoalTrackerConfidence.AtRisk,
                        (0.2, 360, GoalTrackerConfidence.OnTrack, "Help center rewrite started"),
                        (0.5, 330, GoalTrackerConfidence.AtRisk, "SSO questions still common")),
                ],
            },
            new GoalTrackerGoal("Grow weekly active teams", "Growth", elena)
            {
                KeyResults =
                [
                    Kr("Weekly active teams", elena, 410, 600, "teams", GoalTrackerConfidence.OnTrack,
                        (0.2, 440, GoalTrackerConfidence.OnTrack, "Campaign launched"),
                        (0.4, 482, GoalTrackerConfidence.OnTrack, "Referral loop working"),
                        (0.58, 521, GoalTrackerConfidence.OnTrack, "Ahead of plan")),
                    Kr("Teams on the mobile app", kofi, 90, 200, "teams", GoalTrackerConfidence.OffTrack,
                        (0.3, 98, GoalTrackerConfidence.AtRisk, "Release slipped two weeks"),
                        (0.55, 104, GoalTrackerConfidence.OffTrack, "Store review rejected the build")),
                ],
            },
            new GoalTrackerGoal("Ship a faster editor", "Platform", kofi)
            {
                KeyResults =
                [
                    Kr("Time to first paint", kofi, 1.8, 0.9, "s", GoalTrackerConfidence.AtRisk,
                        (0.25, 1.6, GoalTrackerConfidence.OnTrack, "Lazy loading the toolbar"),
                        (0.5, 1.45, GoalTrackerConfidence.AtRisk, "Fonts still block the first frame")),
                    Kr("Crash-free sessions", lise, 98.1, 99.5, "%", GoalTrackerConfidence.OnTrack,
                        (0.3, 98.6, GoalTrackerConfidence.OnTrack, "Fixed the paste crash"),
                        (0.56, 99.1, GoalTrackerConfidence.OnTrack, "Memory leak closed")),
                ],
            },
        ];
    }
}
