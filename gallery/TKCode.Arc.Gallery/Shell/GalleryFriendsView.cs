using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The Friends page: a header, the sponsors (or slots that invite one) and the friends, each as a <see cref="Card"/>
/// with a drawn mark, a description and a link. The sites come from <see cref="GalleryFriends"/>.
/// </summary>
public sealed class GalleryFriendsView : UserControl
{
    private const double ContentMaxWidth = 1040;
    private const double OneColumnBelow = 720;
    private const double ThreeColumnsFrom = 980;

    public GalleryFriendsView()
    {
        var column = new StackPanel { Spacing = 48, MaxWidth = ContentMaxWidth, HorizontalAlignment = HorizontalAlignment.Stretch };
        column.Children.Add(BuildHeader());
        column.Children.Add(BuildSection(GalleryShellLangResourceKind.FriendsSponsors, GalleryShellLangResourceKind.FriendsSponsorsHint, BuildSponsors()));
        column.Children.Add(BuildSection(GalleryShellLangResourceKind.FriendsFriends, GalleryShellLangResourceKind.FriendsFriendsHint, BuildFriends()));
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(24, 40, 24, 56), Child = column }
        };
    }

    private Control BuildHeader()
    {
        var badge = new Badge { Tone = BadgeTone.Info, Icon = new ArcGlyph { Data = ArcIcons.Heart, Size = 12 }, HorizontalAlignment = HorizontalAlignment.Left };
        GalleryText.Bind(badge, ContentControl.ContentProperty, GalleryShellLangResourceKind.FriendsSponsors);
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 40, FontWeight.Medium);
        GalleryText.Bind(title, TextBlock.TextProperty, GalleryShellLangResourceKind.FriendsTitle);
        var subtitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 16);
        subtitle.MaxWidth = 560;
        GalleryText.Bind(subtitle, TextBlock.TextProperty, GalleryShellLangResourceKind.FriendsSubtitle);

        var stack = new StackPanel { Spacing = 14, Margin = new Thickness(36, 40) };
        stack.Children.Add(badge);
        stack.Children.Add(title);
        stack.Children.Add(subtitle);
        var surface = new AuroraSurface();
        surface.Children.Add(stack);
        var frame = new Border { CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), ClipToBounds = true, Child = surface };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return frame;
    }

    private static Control BuildSection(GalleryShellLangResourceKind title, GalleryShellLangResourceKind hint, Control body)
    {
        var heading = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 24, FontWeight.Medium);
        GalleryText.Bind(heading, TextBlock.TextProperty, title);
        var caption = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);
        GalleryText.Bind(caption, TextBlock.TextProperty, hint);
        var section = new StackPanel { Spacing = 18 };
        section.Children.Add(new StackPanel { Spacing = 4, Children = { heading, caption } });
        section.Children.Add(body);
        return section;
    }

    private Control BuildSponsors()
    {
        var grid = ResponsiveGrid();
        foreach (var sponsor in GalleryFriends.Sponsors)
        {
            grid.Children.Add(SiteCard(sponsor));
        }
        if (grid.Children.Count == 0)
        {
            grid.Children.Add(SlotCard());
        }
        return grid;
    }

    private Control BuildFriends()
    {
        var grid = ResponsiveGrid();
        foreach (var friend in GalleryFriends.Friends)
        {
            grid.Children.Add(SiteCard(friend));
        }
        return grid;
    }

    private static UniformGrid ResponsiveGrid()
    {
        var grid = new UniformGrid { Columns = 3 };
        grid.SizeChanged += (_, e) => grid.Columns = e.NewSize.Width < OneColumnBelow ? 1 : e.NewSize.Width < ThreeColumnsFrom ? 2 : 3;
        return grid;
    }

    private Control SiteCard(GalleryFriend site)
    {
        var visit = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = LabelWithGlyph(GalleryShellLangResourceKind.FriendsVisit, ArcIcons.ArrowUpRight) };
        visit.Click += (_, _) => Launch(site.Url);
        Avalonia.Automation.AutomationProperties.SetName(visit, site.Name);
        var card = new Card
        {
            Title = site.Name,
            Media = new FriendMark(site, 64, 112),
            Action = visit,
            Margin = new Thickness(0, 0, 16, 16)
        };
        GalleryText.Bind(card, Card.DescriptionProperty, site.Description);
        return card;
    }

    private Control SlotCard()
    {
        var become = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = LabelWithGlyph(GalleryShellLangResourceKind.FriendsBecome, ArcIcons.Heart) };
        become.Click += (_, _) => Launch(GalleryFriends.SponsorUrl);
        var card = new Card
        {
            Media = new FriendMark(new GalleryFriend("+", GalleryFriends.SponsorUrl, GalleryShellLangResourceKind.FriendsSlotHint, "+", 45), 64, 112),
            Action = become,
            Margin = new Thickness(0, 0, 16, 16)
        };
        GalleryText.Bind(card, Card.TitleProperty, GalleryShellLangResourceKind.FriendsSlotTitle);
        GalleryText.Bind(card, Card.DescriptionProperty, GalleryShellLangResourceKind.FriendsSlotHint);
        return card;
    }

    internal static Control LabelWithGlyph(GalleryShellLangResourceKind label, Geometry glyph)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(text, TextBlock.TextProperty, label);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(text);
        row.Children.Add(new ArcGlyph { Data = glyph, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private void Launch(Uri url) => _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(url);
}
