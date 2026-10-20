using System.Collections.ObjectModel;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Pages;

public static class TimelinePage
{
    private static readonly string[] s_people = ["Ana Ruiz", "Theo Walsh", "Mina Park", "Kai Moreno", "Lee Chen", "Ivy Brooks"];

    private static readonly (string Title, string? Meta)[] s_updates =
    [
        ("merged Checkout redesign into main", "PR #212"),
        ("opened Pricing page copy review", "PR #219"),
        ("commented on Onboarding flow", "Issue #88"),
        ("approved Billing migration", "PR #207"),
        ("pushed 3 commits to feature/search", "a41c9e2"),
        ("closed Flaky e2e test on Safari", "Issue #91")
    ];

    public static Control Create() => GalleryUi.Column(20,
        GalleryUi.Card("Live deploy stream",
            "New updates slide in at the top while the rest glide down; relative times tick with Now. Activate the button to post an update.",
            LiveStream()),
        GalleryUi.Card("Expandable details",
            "Rows with a detail are buttons: ↑ ↓ Home End move between them, Enter or Space expands. Rows without detail are plain text.",
            Surface(new Timeline
            {
                Label = "Release history",
                Now = Now(),
                ItemsSource = Release(),
                ExpandedIds = ["build-418"]
            })),
        GalleryUi.Card("Scrolling feed with pinned days",
            "MaxHeight scrolls inside the feed: the current day's label stays pinned until the next day pushes it up, and the bottom edge feathers while more waits below.",
            Surface(new Timeline { Label = "Audit log", Now = Now(), ItemsSource = History(60, 140), MaxHeight = 380 })),
        GalleryUi.Card("A thousand updates",
            "The list is virtualized: only the rows in view exist, so long histories scroll smoothly.",
            Surface(new Timeline { Label = "Long history", Now = Now(), ItemsSource = History(1000, 23), MaxHeight = 420 })),
        GalleryUi.Card("Tones, icons and portraits",
            "People show their portrait; system events show an icon on a quiet node tinted by tone. Always say the outcome in the title too.",
            Surface(new Timeline { Label = "Pipeline", Now = Now(), ItemsSource = Tones() })));

    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
    }

    /// <summary>The feed sits on a surface card (Arc's preview), so its day labels blend with what scrolls under them.</summary>
    private static Border Surface(Control content)
    {
        var card = new Border
        {
            Padding = new Thickness(16, 12, 16, 4),
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(1),
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = content
        };
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return card;
    }

    private static Control LiveStream()
    {
        var now = Now();
        var events = new ObservableCollection<TimelineEvent>
        {
            new("live-1", now.AddMinutes(-3), "deployed web to production", "Ana Ruiz")
            {
                Meta = "Build #418 · 4m 12s",
                Avatar = "https://i.pravatar.cc/56?img=47",
                Detail = "All 312 checks passed. Rolled out to 100% of traffic in three waves."
            },
            new("live-2", now.AddMinutes(-41), "Preview environment is ready", null)
            {
                Meta = "feature/search",
                Icon = ArcIcons.Check,
                Tone = TimelineTone.Success
            },
            new("live-3", now.AddHours(-3), "merged Checkout redesign into main", "Theo Walsh")
            {
                Meta = "PR #212",
                Avatar = "https://i.pravatar.cc/56?img=12"
            },
            new("live-4", now.AddHours(-26), "Build failed on main", null)
            {
                Meta = "Step test:e2e exited with code 1",
                Icon = ArcIcons.X,
                Tone = TimelineTone.Danger,
                Detail = "Retried twice. The failing spec was quarantined in PR #215."
            },
            new("live-5", now.AddHours(-29), "requested changes on Pricing page", "Mina Park") { Meta = "PR #207" }
        };
        var timeline = new Timeline { Label = "Deployments", Now = now, ItemsSource = events };
        var count = 0;
        var post = new Button { Content = "Post an update", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        post.Click += (_, _) =>
        {
            count++;
            var (title, meta) = s_updates[count % s_updates.Length];
            var person = s_people[count % s_people.Length];
            events.Add(new TimelineEvent($"post-{count}", timeline.Now.AddSeconds(count), title, person) { Meta = meta });
        };

        // A ticking clock keeps relative times fresh (Arc suggests about once a minute; faster here to show it).
        var clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        clock.Tick += (_, _) => timeline.Now = timeline.Now.AddMinutes(1);
        timeline.AttachedToVisualTree += (_, _) => clock.Start();
        timeline.DetachedFromVisualTree += (_, _) => clock.Stop();

        var controls = GalleryUi.Row(post);
        controls.HorizontalAlignment = HorizontalAlignment.Left;
        return GalleryUi.Column(16, controls, Surface(timeline));
    }

    private static List<TimelineEvent> Release()
    {
        var now = Now();
        return
        [
            new("build-418", now.AddMinutes(-12), "deployed web to production", "Ana Ruiz")
            {
                Meta = "Build #418",
                Avatar = "https://i.pravatar.cc/56?img=47",
                Detail = "All 312 checks passed in 4m 12s. Rolled out to 100% of traffic in three waves, with no error-rate change."
            },
            new("pr-212", now.AddMinutes(-58), "merged Checkout redesign into main", "Theo Walsh")
            {
                Meta = "PR #212 · 14 files",
                Avatar = "https://i.pravatar.cc/56?img=12",
                Detail = "Squashed 9 commits. Reviewed by Mina Park and Lee Chen."
            },
            new("note", now.AddHours(-2), "renamed the project to Atlas", "Lee Chen") { Avatar = "https://i.pravatar.cc/56?img=33" },
            new("migrate", now.AddHours(-30), "Database migration completed", null)
            {
                Meta = "0042_add_invoices",
                Icon = ArcIcons.Check,
                Tone = TimelineTone.Success,
                Detail = "Applied in 38s on the primary; replicas caught up within a minute."
            }
        ];
    }

    private static List<TimelineEvent> History(int count, int minutesApart)
    {
        var now = Now();
        var list = new List<TimelineEvent>(count);
        for (var i = 0; i < count; i++)
        {
            var (title, meta) = s_updates[i % s_updates.Length];
            var system = i % 7 == 3;
            list.Add(new TimelineEvent($"h-{i}", now.AddMinutes(-(i * minutesApart + 4)), system ? $"Nightly backup {(i % 2 == 0 ? "completed" : "finished")}" : title,
                system ? null : s_people[i % s_people.Length])
            {
                Meta = system ? "Storage" : meta,
                Icon = system ? ArcIcons.Check : null,
                Tone = system ? TimelineTone.Success : TimelineTone.Neutral,
                Detail = i % 5 == 0 ? $"Update {i + 1} of {count}. Opened from the audit log." : null
            });
        }
        return list;
    }

    private static List<TimelineEvent> Tones()
    {
        var now = Now();
        return
        [
            new("t1", now.AddMinutes(-5), "Deploy succeeded", null) { Meta = "web · production", Icon = ArcIcons.Check, Tone = TimelineTone.Success },
            new("t2", now.AddMinutes(-18), "Deploy failed: health check timed out", null) { Meta = "api · staging", Icon = ArcIcons.X, Tone = TimelineTone.Danger },
            new("t3", now.AddMinutes(-26), "Rollback started", null) { Meta = "api · staging", Icon = ArcIcons.RotateCcw },
            new("t4", now.AddMinutes(-40), "approved the release", "Ivy Brooks") { Meta = "v2.8.0", Avatar = "https://i.pravatar.cc/56?img=45" },
            new("t5", now.AddMinutes(-52), "tagged v2.8.0", "Kai Moreno") { Avatar = "https://i.pravatar.cc/56?img=15" }
        ];
    }
}
