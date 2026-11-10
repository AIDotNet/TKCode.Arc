using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityFeedPreviewDemo : UserControl
{
    private static readonly (string Actor, string Action, ActivityKind Kind)[] s_next =
    [
        ("Rosa Lind", "commented on Pricing page", ActivityKind.Comment),
        ("Dev Patel", "moved Onboarding emails to Done", ActivityKind.Change),
        ("Yuki Sato", "replied to Launch checklist", ActivityKind.Comment),
        ("Amir Haddad", "created Holiday campaign", ActivityKind.Change),
    ];

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private int _count;

    public ActivityFeedPreviewDemo()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Arrive();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _timer.Start();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _timer.Stop();
    }

    // Every few seconds someone does something, or a deploy finishes.
    private void Arrive()
    {
        _count++;
        if (_count % 3 == 0)
        {
            Feed.Push(new ActivityFeedItem($"live{_count}", DateTimeOffset.Now, null, "Deploy to production succeeded", ActivityKind.Deploy)
            {
                Target = $"web · build {1482 + _count}",
                Tone = TimelineTone.Success,
            });
            return;
        }
        var (actor, action, kind) = s_next[_count % s_next.Length];
        Feed.Push(new ActivityFeedItem($"live{_count}", DateTimeOffset.Now, actor, action, kind));
    }
}
