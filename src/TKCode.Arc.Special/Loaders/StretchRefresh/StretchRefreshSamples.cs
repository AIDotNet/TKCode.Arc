using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TKCode.Arc;

/// <summary>
/// Invented sample content for <see cref="StretchRefresh"/> (a fictional company's activity feed): seven entries from
/// newest to oldest and three batches of new entries for the first three refreshes (3, 1 and 2 entries; then none),
/// in the shapes the design uses: an avatar row, an avatar row with a quote, a status glyph row and a photo row.
/// </summary>
public static class StretchRefreshSamples
{
    /// <summary>The sample feed's heading.</summary>
    public const string Title = "Northbeam activity";

    /// <summary>The subtitle before the first refresh.</summary>
    public const string InitialSubtitle = "Updated 12 min ago";

    /// <summary>The subtitle after a refresh.</summary>
    public const string RefreshedSubtitle = "Updated just now";

    /// <summary>The refresh button's accessible name in the samples.</summary>
    public const string RefreshLabel = "Refresh activity";

    /// <summary>The result when a refresh brings nothing new.</summary>
    public const string CaughtUp = "You’re all caught up";

    /// <summary>The seven entries the feed starts with, newest first (new instances on every call).</summary>
    public static IReadOnlyList<StretchRefreshActivity> CreateInitial() =>
    [
        new()
        {
            Id = "a1", Actor = "Priya Okafor", Action = "merged", Target = "Fix duplicate invoice emails",
            Meta = "Billing · 12 min ago", AvatarSeed = 1
        },
        new()
        {
            Id = "a2", Actor = "Build 1307", Action = "passed on", Target = "preview",
            Meta = "Deploys · 25 min ago", Tone = StretchRefreshActivityTone.Success
        },
        new()
        {
            Id = "a3", Actor = "Tomas Lindqvist", Action = "commented on", Target = "Onboarding v2",
            Quote = "Could the import step ship first? Teams keep asking.", Meta = "Planning · 41 min ago", AvatarSeed = 2
        },
        new()
        {
            Id = "a4", Actor = "Pinecrest Labs", Action = "paid invoice", Target = "INV-3318",
            Meta = "$2,375.00 · 1 hr ago", Tone = StretchRefreshActivityTone.Neutral
        },
        new()
        {
            Id = "a5", Actor = "Ada Moreau", Action = "added a photo to", Target = "Autumn moodboard",
            Meta = "Brand · 2 hr ago", AvatarSeed = 3, PhotoSeed = 4
        },
        new()
        {
            Id = "a6", Actor = "Kenji Watanabe", Action = "closed", Target = "Tooltip flickers on scroll",
            Meta = "Web app · 3 hr ago", AvatarSeed = 4
        },
        new()
        {
            Id = "a7", Actor = "Lena Fischer", Action = "published", Target = "October changelog",
            Meta = "Docs · Yesterday", AvatarSeed = 14
        }
    ];

    /// <summary>The new entries of the first three refreshes (3, 1 and 2), newest first within each batch; unread.</summary>
    public static IReadOnlyList<IReadOnlyList<StretchRefreshActivity>> CreateBatches() =>
    [
        [
            new()
            {
                Id = "n1", Actor = "Kenji Watanabe", Action = "replied on", Target = "Fix duplicate invoice emails",
                Quote = "Merging after standup.", Meta = "Billing · Just now", AvatarSeed = 4, IsUnread = true
            },
            new()
            {
                Id = "n2", Actor = "Build 1308", Action = "failed on", Target = "main",
                Meta = "3 billing tests · Just now", Tone = StretchRefreshActivityTone.Danger, IsUnread = true
            },
            new()
            {
                Id = "n3", Actor = "Lena Fischer", Action = "requested your review on", Target = "Saved views",
                Meta = "Web app · Just now", AvatarSeed = 14, IsUnread = true
            }
        ],
        [
            new()
            {
                Id = "n4", Actor = "Ada Moreau", Action = "added a photo to", Target = "Lisbon retreat",
                Meta = "Events · Just now", AvatarSeed = 3, PhotoSeed = 9, IsUnread = true
            }
        ],
        [
            new()
            {
                Id = "n5", Actor = "Harbor & Finch", Action = "paid invoice", Target = "INV-3324",
                Meta = "$980.00 · Just now", Tone = StretchRefreshActivityTone.Neutral, IsUnread = true
            },
            new()
            {
                Id = "n6", Actor = "Tomas Lindqvist", Action = "assigned you", Target = "Error page copy",
                Meta = "Planning · Just now", AvatarSeed = 2, IsUnread = true
            }
        ]
    ];

    /// <summary>The result line for <paramref name="count"/> new entries: "3 new updates", "1 new update", or <see cref="CaughtUp"/>.</summary>
    public static string FormatResult(int count) => count switch
    {
        <= 0 => CaughtUp,
        1 => "1 new update",
        _ => $"{count} new updates"
    };
}

/// <summary>
/// The simulated backend of the <see cref="StretchRefresh"/> samples, replaying the reference preview: every refresh
/// takes <see cref="Latency"/> (1150 ms), marks the entries shown so far as read, puts the next batch of
/// <see cref="StretchRefreshSamples.CreateBatches"/> on top (3, 1, 2, then nothing), sets the subtitle to
/// <see cref="StretchRefreshSamples.RefreshedSubtitle"/> and returns the result line. Pass its <see cref="RefreshAsync"/>
/// as <see cref="StretchRefresh.RefreshHandler"/>; replace it with real work in an app. Time comes from a
/// <see cref="TimeProvider"/>, so tests can step it.
/// </summary>
public sealed class StretchRefreshSampleFeed : INotifyPropertyChanged
{
    private readonly TimeProvider _time;
    private IReadOnlyList<IReadOnlyList<StretchRefreshActivity>> _batches;
    private int _next;
    private string _subtitle = StretchRefreshSamples.InitialSubtitle;
    private bool _isChanged;

    /// <summary>Creates the feed with the seven initial entries.</summary>
    /// <param name="timeProvider">The clock of the simulated latency (null: the system clock).</param>
    public StretchRefreshSampleFeed(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _batches = StretchRefreshSamples.CreateBatches();
        Items = new ObservableCollection<StretchRefreshActivity>(StretchRefreshSamples.CreateInitial());
    }

    /// <summary>Raised when <see cref="Subtitle"/> or <see cref="IsChanged"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The entries, newest first; bind <see cref="StretchRefresh.ItemsSource"/> to it.</summary>
    public ObservableCollection<StretchRefreshActivity> Items { get; }

    /// <summary>"Updated 12 min ago" until the first refresh, then "Updated just now".</summary>
    public string Subtitle
    {
        get => _subtitle;
        private set
        {
            if (_subtitle != value)
            {
                _subtitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
            }
        }
    }

    /// <summary>True once a refresh has changed the feed (the preview's Reset button is enabled then).</summary>
    public bool IsChanged
    {
        get => _isChanged;
        private set
        {
            if (_isChanged != value)
            {
                _isChanged = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChanged)));
            }
        }
    }

    /// <summary>How long a simulated refresh takes (1150 ms, as measured on the reference preview).</summary>
    public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(1150);

    /// <summary>How many refreshes have completed.</summary>
    public int RefreshCount => _next;

    /// <summary>The simulated refresh; returns the result line ("3 new updates", "1 new update", "You’re all caught up").</summary>
    public async Task<string?> RefreshAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Latency, _time, cancellationToken);
        var batch = _next < _batches.Count ? _batches[_next] : Array.Empty<StretchRefreshActivity>();
        _next++;
        foreach (var item in Items)
        {
            item.IsUnread = false;
        }
        for (var i = batch.Count - 1; i >= 0; i--)
        {
            Items.Insert(0, batch[i]);
        }
        Subtitle = StretchRefreshSamples.RefreshedSubtitle;
        IsChanged = true;
        return StretchRefreshSamples.FormatResult(batch.Count);
    }

    /// <summary>Back to the seven initial entries and the first subtitle.</summary>
    public void Reset()
    {
        _batches = StretchRefreshSamples.CreateBatches();
        _next = 0;
        Items.Clear();
        foreach (var item in StretchRefreshSamples.CreateInitial())
        {
            Items.Add(item);
        }
        Subtitle = StretchRefreshSamples.InitialSubtitle;
        IsChanged = false;
    }
}
