using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The face of one <see cref="InboxTriage"/> row: an accent bar while it is the keyboard's current item, the sender's
/// avatar with an unread dot, the sender, labels, subject and preview, and the time with the assignee. It follows the
/// item's changes while it is on screen.
/// </summary>
internal sealed class InboxTriageCard : Border
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    private readonly InboxTriage _owner;
    private readonly Border _cursor;
    private readonly Avatar _avatar;
    private readonly Control _unread;
    private readonly TextBlock _sender;
    private readonly WrapPanel _labels;
    private readonly TextBlock _subject;
    private readonly TextBlock _preview;
    private readonly TextBlock _time;

    public InboxTriageCard(InboxTriage owner, InboxTriageItem item)
    {
        _owner = owner;
        Item = item;
        Padding = new Thickness(4, 2);
        _cursor = new Border { Width = 3, Margin = new Thickness(-4, 4, 0, 4), HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false }
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(_cursor, BackgroundProperty, ArcDesignTokenKind.Accent);
        _avatar = new Avatar { Size = AvatarSize.Medium, VerticalAlignment = VerticalAlignment.Top };
        _unread = SupportKit.Dot(ArcDesignTokenKind.Accent, 8);
        _unread.HorizontalAlignment = HorizontalAlignment.Right;
        _unread.VerticalAlignment = VerticalAlignment.Top;
        _unread.Margin = new Thickness(0, -1, -1, 0);
        var face = new Panel { Children = { _avatar, _unread }, VerticalAlignment = VerticalAlignment.Top };

        _sender = SupportKit.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, trim: true);
        _labels = new WrapPanel { ItemSpacing = 4, LineSpacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var top = new ArcShrinkRow { Spacing = 8 };
        ArcShrinkRow.SetIsShrinkable(_sender, true);
        top.Children.Add(_sender);
        top.Children.Add(_labels);
        _subject = SupportKit.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, trim: true);
        _preview = SupportKit.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, trim: true);
        var text = new StackPanel { Spacing = 1, Children = { top, _subject, _preview } };

        _time = SupportKit.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        _time.VerticalAlignment = VerticalAlignment.Top;
        _time.HorizontalAlignment = HorizontalAlignment.Right;

        var grid = SupportKit.Grid("Auto,*,Auto", columnSpacing: 12);
        grid.Children.Add(face.At(0));
        grid.Children.Add(text.At(1));
        grid.Children.Add(_time.At(2));
        Child = new Panel { Children = { grid, _cursor } };
        Update();
    }

    public InboxTriageItem Item { get; }

    public bool IsCurrent => _cursor.IsVisible;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Item.PropertyChanged += OnItemChanged;
        Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Item.PropertyChanged -= OnItemChanged;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => Update();

    public void Update()
    {
        var item = Item;
        _cursor.IsVisible = ReferenceEquals(_owner.CurrentItem, item);
        _avatar.DisplayName = item.Sender;
        _unread.IsVisible = item.IsUnread;
        _sender.Text = item.Sender;
        _sender.FontWeight = item.IsUnread ? FontWeight.SemiBold : FontWeight.Normal;
        _subject.Text = item.Subject;
        _preview.Text = item.Preview;
        _preview.IsVisible = !string.IsNullOrEmpty(item.Preview);
        _time.Text = SupportKit.Ago(item.ReceivedAt, _owner.Clock(), s_culture);
        _labels.Children.Clear();
        foreach (var label in item.Labels)
        {
            _labels.Children.Add(SupportKit.Pill(label, ArcDesignTokenKind.AccentSubtle, ArcDesignTokenKind.AccentStrong));
        }
        if (item.Assignee is { } assignee)
        {
            _labels.Children.Add(SupportKit.Pill(assignee, ArcDesignTokenKind.SurfaceMuted, ArcDesignTokenKind.TextSecondary, ArcIcons.User));
        }
    }
}

/// <summary>The sample inbox of an <see cref="InboxTriage"/> that has no items when it first loads.</summary>
internal static class InboxTriageSamples
{
    public static readonly string[] Teammates = ["Ines Duarte", "Kofi Mensah", "Lena Park", "Tomas Reyes"];

    public static readonly string[] Labels = ["Billing", "Bug", "Feature request", "Urgent", "VIP"];

    public static IEnumerable<InboxTriageItem> Items(DateTimeOffset now) =>
    [
        new("s1", "Mara Lindqvist", "Charged twice for the March invoice", "Hi, I see two identical charges on my card from yesterday.", now.AddMinutes(-4)) { Labels = ["Billing"] },
        new("s2", "Felix Okafor", "Export to CSV drops the last column", "When I export the project board, the Owner column is missing.", now.AddMinutes(-26)) { Labels = ["Bug"] },
        new("s3", "Hanna Weiss", "Can we get SSO for the whole team?", "We are moving 40 seats over next month and need single sign-on.", now.AddHours(-2)),
        new("s4", "Rui Tanaka", "Password reset link expired", "The link in the reset email says it has expired right away.", now.AddHours(-3)) { IsUnread = false },
        new("s5", "Sofia Brandt", "Love the new timeline view", "Just wanted to say the new view saved me an hour this week.", now.AddHours(-20)) { IsUnread = false },
        new("s6", "Omar Haddad", "Webhook retries are not showing", "Failed deliveries no longer appear in the log since Monday.", now.AddDays(-2)) { Labels = ["Bug", "Urgent"] },
    ];
}
