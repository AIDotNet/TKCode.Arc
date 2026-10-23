// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): newsletter-signup-data.ts and the props of newsletter-signup.tsx.
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How a <see cref="NewsletterSignup"/> is laid out.</summary>
public enum NewsletterVariant
{
    /// <summary>A page section: the copy and the form on one side, the stack of issues and the readers on the other.</summary>
    Inline,

    /// <summary>A self-contained card: the stack of issues sits in a tray on top, cropped where the card body begins.</summary>
    Card
}

/// <summary>Where a <see cref="NewsletterSignup"/> stands in a subscription.</summary>
public enum NewsletterPhase
{
    /// <summary>Waiting for an address (or for another try after a failure).</summary>
    Idle,

    /// <summary>The address is being sent; the field is read-only and the button reports the work.</summary>
    Sending,

    /// <summary>The address was accepted: the next issue, addressed to it, sits on the front of the stack.</summary>
    Done
}

/// <summary>One story teased in an issue: a title, a small square thumbnail and a reading time.</summary>
/// <remarks>Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
public class NewsletterStory : AvaloniaObject
{
    /// <summary>The story title (at most two lines are shown).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<NewsletterStory, string?>(nameof(Title));

    /// <summary>The thumbnail, drawn as a 44 px rounded square (the longer side is cropped). Null leaves the muted tile.</summary>
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<NewsletterStory, IImage?>(nameof(Image));

    /// <summary>The reading time in minutes, shown as "5 min" (see <see cref="NewsletterSignup.MinutesFormat"/>).</summary>
    public static readonly StyledProperty<int> MinutesProperty =
        AvaloniaProperty.Register<NewsletterStory, int>(nameof(Minutes));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ImageProperty"/>
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <inheritdoc cref="MinutesProperty"/>
    public int Minutes { get => GetValue(MinutesProperty); set => SetValue(MinutesProperty, value); }
}

/// <summary>One issue of the newsletter as it appears on the stack. The first three <see cref="Stories"/> are shown.</summary>
/// <remarks>Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
public class NewsletterIssue : AvaloniaObject
{
    /// <summary>The issue number. It is the identity of the issue on the stack, so give every issue its own.</summary>
    public static readonly StyledProperty<int> NumberProperty =
        AvaloniaProperty.Register<NewsletterIssue, int>(nameof(Number));

    /// <summary>The send date as it should read, such as "Friday, September 25".</summary>
    public static readonly StyledProperty<string?> DateProperty =
        AvaloniaProperty.Register<NewsletterIssue, string?>(nameof(Date));

    /// <summary>The subject line, shown large under the masthead.</summary>
    public static readonly StyledProperty<string?> SubjectProperty =
        AvaloniaProperty.Register<NewsletterIssue, string?>(nameof(Subject));

    private readonly AvaloniaList<NewsletterStory> _stories = new();

    /// <inheritdoc cref="NumberProperty"/>
    public int Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    /// <inheritdoc cref="DateProperty"/>
    public string? Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <inheritdoc cref="SubjectProperty"/>
    public string? Subject { get => GetValue(SubjectProperty); set => SetValue(SubjectProperty, value); }

    /// <summary>The stories teased in the issue; at most three are shown.</summary>
    [Content]
    public AvaloniaList<NewsletterStory> Stories => _stories;
}

/// <summary>
/// The publication shown beside the form: its name, the next issue and the recent ones. When someone subscribes the
/// <see cref="Upcoming"/> issue, addressed to the new reader, lands on the front of the stack and the oldest issue drops
/// away.
/// </summary>
/// <remarks>Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
public class NewsletterPublication : AvaloniaObject
{
    /// <summary>The publication name, the masthead of every issue.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<NewsletterPublication, string?>(nameof(Name));

    /// <summary>The next issue. It joins the front of the stack once a reader has subscribed.</summary>
    public static readonly StyledProperty<NewsletterIssue?> UpcomingProperty =
        AvaloniaProperty.Register<NewsletterPublication, NewsletterIssue?>(nameof(Upcoming));

    private readonly AvaloniaList<NewsletterIssue> _recent = new();

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="UpcomingProperty"/>
    public NewsletterIssue? Upcoming { get => GetValue(UpcomingProperty); set => SetValue(UpcomingProperty, value); }

    /// <summary>Recent issues, newest first. The first three make the stack.</summary>
    [Content]
    public AvaloniaList<NewsletterIssue> Recent => _recent;

    /// <summary>Raised when the name, the upcoming issue or the list of recent issues changes.</summary>
    internal event EventHandler? Changed;

    public NewsletterPublication()
    {
        _recent.CollectionChanged += OnRecentChanged;
    }

    private void OnRecentChanged(object? sender, NotifyCollectionChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NameProperty || change.Property == UpcomingProperty)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>Arguments of <see cref="NewsletterSignup.Subscribed"/>.</summary>
public class NewsletterSubscribedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NewsletterSubscribedEventArgs(RoutedEvent routedEvent, object source, string email) : base(routedEvent, source)
    {
        Email = email;
    }

    /// <summary>The address that was subscribed (trimmed).</summary>
    public string Email { get; }
}

/// <summary>Arguments of <see cref="NewsletterSignup.Navigate"/>.</summary>
public class NewsletterNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NewsletterNavigateEventArgs(RoutedEvent routedEvent, object source, string? href) : base(routedEvent, source)
    {
        Href = href;
    }

    /// <summary>The destination of the privacy link.</summary>
    public string? Href { get; }
}
