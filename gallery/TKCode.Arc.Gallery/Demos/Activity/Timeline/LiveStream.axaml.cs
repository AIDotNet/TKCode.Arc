using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class TimelineLiveStreamDemo : UserControl
{
    private readonly ObservableCollection<TimelineEvent> _events;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(20) };
    private int _count;

    public TimelineLiveStreamDemo()
    {
        InitializeComponent();
        var now = TimelineDemoData.Now();
        _events =
        [
            new("live-1", now.AddMinutes(-3), "deployed web to production", "Ana Ruiz")
            {
                Meta = "Build #418 · 4m 12s",
                Avatar = "https://i.pravatar.cc/56?img=47",
                Detail = "All 312 checks passed. Rolled out to 100% of traffic in three waves."
            },
            new("live-2", now.AddMinutes(-41), "Preview environment is ready")
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
            new("live-4", now.AddHours(-26), "Build failed on main")
            {
                Meta = "Step test:e2e exited with code 1",
                Icon = ArcIcons.X,
                Tone = TimelineTone.Danger,
                Detail = "Retried twice. The failing spec was quarantined in PR #215."
            },
            new("live-5", now.AddHours(-29), "requested changes on Pricing page", "Mina Park") { Meta = "PR #207" }
        ];
        Feed.Now = now;
        Feed.ItemsSource = _events;

        // A ticking clock keeps relative times fresh (Arc suggests about once a minute; faster here to show it).
        _clock.Tick += (_, _) => Feed.Now = Feed.Now.AddMinutes(1);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _clock.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPostClick(object? sender, RoutedEventArgs e)
    {
        _count++;
        var (actor, title, meta) = TimelineDemoData.Update(_count);
        _events.Add(new TimelineEvent($"post-{_count}", Feed.Now.AddSeconds(_count), title, actor) { Meta = meta });
    }
}
