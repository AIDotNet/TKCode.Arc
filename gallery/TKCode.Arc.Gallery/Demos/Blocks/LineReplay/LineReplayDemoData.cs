namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The LineReplay demos' data (all invented): people active in a fictional app, Fernly, every five minutes on the day
/// version 2.0 shipped, against a typical Wednesday, with six key moments; and shoppers on a fictional store's site
/// every two minutes on a sale morning, without a comparison. The curves are closed-form (a daily shape, a rise after
/// each moment and a little deterministic wobble), so every run draws the same line. Not real traffic.
/// </summary>
public static class LineReplayDemoData
{
    public const string Title = "Release day";

    public const string Description = "People active in Fernly every five minutes on Wednesday 14 October 2026, Pacific time.";

    /// <summary>288 points, midnight to 11:55 pm, with the typical Wednesday as the baseline.</summary>
    public static LineReplayPoint[] ReleaseDay()
    {
        var points = new LineReplayPoint[288];
        for (var i = 0; i < points.Length; i++)
        {
            var m = i * 5.0;
            points[i] = new LineReplayPoint(m, Math.Round(ReleaseValue(m)), Math.Round(Typical(m)));
        }
        return points;
    }

    public static LineReplayMoment[] ReleaseMoments() =>
    [
        new("notes", 390, "Release notes published", "Changelog, blog and in-app banner together", LineReplayMomentKind.Launch),
        new("newsletter", 490, "Featured in a tech newsletter", "Lead story for about 120,000 readers", LineReplayMomentKind.Press),
        new("demo", 605, "Live demo on the community stream", "Forty minutes with questions from viewers", LineReplayMomentKind.Launch),
        new("queue", 705, "Sign-up queue backed up", "Workers saturated until autoscaling caught up", LineReplayMomentKind.Incident),
        new("email", 840, "Customer email sent", "61,500 customers, 38% opened in the first hour", LineReplayMomentKind.Email),
        new("workspaces", 1170, "10,000th workspace created", "Up from 7,200 the evening before", LineReplayMomentKind.Milestone),
    ];

    private static double Typical(double m) => 120 + 830 * Math.Exp(-Math.Pow((m - 780) / 260, 2));

    private static double ReleaseValue(double m)
    {
        var v = Typical(m) * 0.85
                + Rise(m, 390, 12, 900, 420)
                + Rise(m, 490, 15, 1100, 300)
                + Rise(m, 605, 18, 4300, 150)
                + Rise(m, 840, 10, 1500, 130)
                + Rise(m, 1170, 8, 650, 110);
        if (m is >= 705 and <= 765)
        {
            v *= 1 - 0.3 * Math.Sin(Math.PI * (m - 705) / 60);
        }
        return v * (1 + 0.03 * Math.Sin(m * 0.21) + 0.02 * Math.Sin(m * 0.57 + 1));
    }

    /// <summary>A rise that starts at <paramref name="at"/>, climbs over about <paramref name="climb"/> minutes and fades over <paramref name="fade"/>.</summary>
    private static double Rise(double m, double at, double climb, double height, double fade)
    {
        if (m < at)
        {
            return 0;
        }
        var d = m - at;
        return height * (1 - Math.Exp(-d / climb)) * Math.Exp(-d / fade);
    }

    /// <summary>Shoppers on site every two minutes from 5 am to noon (no baseline).</summary>
    public static LineReplayPoint[] SaleMorning()
    {
        var points = new List<LineReplayPoint>();
        for (var m = 300.0; m <= 720; m += 2)
        {
            var v = 300 + 900 * (1 - Math.Exp(-(m - 300) / 60))
                    + Rise(m, 360, 6, 1400, 200)
                    + Rise(m, 435, 10, 2200, 160)
                    + Rise(m, 570, 8, 1600, 120)
                    + Rise(m, 660, 5, 500, 80);
            if (m is >= 520 and <= 560)
            {
                v *= 1 - 0.45 * Math.Sin(Math.PI * (m - 520) / 40);
            }
            v *= 1 + 0.025 * Math.Sin(m * 0.37) + 0.015 * Math.Sin(m * 0.91 + 2);
            points.Add(new LineReplayPoint(m, Math.Round(v)));
        }
        return points.ToArray();
    }

    public static LineReplayMoment[] SaleMoments() =>
    [
        new("open", 360, "Sale opened", "Every store and the app at once", LineReplayMomentKind.Launch),
        new("email", 435, "Early-access email", "Sent to members who opted in", LineReplayMomentKind.Email),
        new("outage", 520, "Payment provider slowed", "Card checks timed out for a few minutes", LineReplayMomentKind.Incident),
        new("post", 570, "Picked up by a deals site", "Front page for most of the hour", LineReplayMomentKind.Press),
        new("record", 660, "Best morning on record", "Orders passed last year's full day", LineReplayMomentKind.Milestone),
    ];
}
