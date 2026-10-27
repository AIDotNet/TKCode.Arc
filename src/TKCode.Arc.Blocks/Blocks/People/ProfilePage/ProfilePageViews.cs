using System.ComponentModel;
using AtomUI.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The Activity tab (internal): one heading and list per group, in the order the groups first appear. A row is the kind's
/// icon, "{first name} {action} " in the secondary color followed by the target, an optional detail line and the time.
/// </summary>
internal sealed class ProfilePageActivityView : StackPanel
{
    private static readonly Geometry s_rocket = ArcIcons.Parse(
        "M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5 M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09 M9 12a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.4 22.4 0 0 1-4 2z M9 12H4s.55-3.03 2-4c1.62-1.08 5 .05 5 .05"); // Lucide rocket

    private readonly List<(Run Prefix, ProfileActivity Item)> _prefixes = new();
    private string _firstName = string.Empty;

    public ProfilePageActivityView()
    {
        Spacing = 20;
    }

    /// <summary>Gap between groups.</summary>
    public double GroupGap { get => Spacing; set => Spacing = value; }

    /// <summary>The rows built (for tests).</summary>
    public int RowCount => _prefixes.Count;

    public void Build(IReadOnlyList<ProfileActivity> items, string firstName)
    {
        Children.Clear();
        _prefixes.Clear();
        _firstName = firstName;
        var groups = new List<(string Name, List<ProfileActivity> Rows)>();
        foreach (var item in items)
        {
            var group = groups.FindIndex(g => g.Name == item.Group);
            if (group < 0)
            {
                groups.Add((item.Group, new List<ProfileActivity>()));
                group = groups.Count - 1;
            }
            groups[group].Rows.Add(item);
        }
        foreach (var (name, rows) in groups)
        {
            var section = new StackPanel();
            if (!string.IsNullOrEmpty(name))
            {
                var heading = new TextBlock
                {
                    Text = name,
                    FontSize = 12,
                    LineHeight = 13.2,
                    LetterSpacing = -0.36,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                heading.FontWeight = FontWeight.Medium;
                ArcTokens.Bind(heading, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
                ArcTokens.Bind(heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                AutomationProperties.SetHeadingLevel(heading, 3);
                section.Children.Add(heading);
            }
            for (var i = 0; i < rows.Count; i++)
            {
                section.Children.Add(BuildRow(rows[i], i > 0));
            }
            Children.Add(section);
        }
    }

    /// <summary>The profile's name changed: the rows show its new first word.</summary>
    public void UpdateName(string firstName)
    {
        _firstName = firstName;
        foreach (var (prefix, item) in _prefixes)
        {
            prefix.Text = PrefixOf(item);
        }
    }

    private string PrefixOf(ProfileActivity item) =>
        string.IsNullOrEmpty(_firstName) ? $"{item.Action} " : $"{_firstName} {item.Action} ";

    private Control BuildRow(ProfileActivity item, bool hasLine)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("20,*,Auto"),
            ColumnSpacing = 12,
        };
        var (glyph, color) = item.Kind switch
        {
            ProfileActivityKind.Shipped => (s_rocket, ArcDesignTokenKind.Success),
            ProfileActivityKind.Commented => (ArcIcons.MessageSquare, ArcDesignTokenKind.TextMuted),
            ProfileActivityKind.Created => (ArcIcons.Plus, ArcDesignTokenKind.TextMuted),
            ProfileActivityKind.Reviewed => (ArcIcons.ThumbsUp, ArcDesignTokenKind.TextMuted),
            _ => (ArcIcons.Star, ArcDesignTokenKind.Warning),
        };
        var glyphView = new ArcGlyph
        {
            Data = glyph,
            Size = 15,
            StrokeThickness = 1.75,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        ArcTokens.Bind(glyphView, ArcGlyph.ForegroundProperty, color);
        var icon = new Border { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Top, Child = glyphView };
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        grid.Children.Add(icon);

        var text = new StackPanel { Spacing = 2 };
        var prefix = new Run(PrefixOf(item));
        ArcTokens.Bind(prefix, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _prefixes.Add((prefix, item));
        var main = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 19.6 };
        main.Inlines!.Add(prefix);
        main.Inlines.Add(new Run(item.Target));
        text.Children.Add(main);
        if (!string.IsNullOrEmpty(item.Detail))
        {
            var detail = new TextBlock { Text = item.Detail, TextWrapping = TextWrapping.Wrap, LineHeight = 19.6 };
            ArcTokens.Bind(detail, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            text.Children.Add(detail);
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var time = new TextBlock
        {
            Text = item.TimeText,
            FontSize = 12,
            LineHeight = 16.8,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.NoWrap,
        };
        ArcTokens.Bind(time, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        Grid.SetColumn(time, 2);
        grid.Children.Add(time);

        var row = new Border
        {
            Padding = new Thickness(0, 10),
            BorderThickness = new Thickness(0, hasLine ? 1 : 0, 0, 0),
            Child = grid,
        };
        ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return row;
    }
}

/// <summary>
/// The Projects tab (internal): project cards in two columns (one on a narrow page). Each card has a 96px cover, the title
/// and visibility, the description, overlapping collaborator faces, the update text and the star button.
/// </summary>
internal sealed class ProfilePageProjectsView : ProfilePageColumns
{
    private readonly List<(ProfileProject Project, ProfilePageStarButton Star, TextBlock Visibility)> _cards = new();
    private ProfilePage? _owner;

    public IReadOnlyList<ProfilePageStarButton> StarButtons => _cards.Select(c => c.Star).ToList();

    public void Build(IReadOnlyList<ProfileProject> projects, ProfilePage owner)
    {
        Detach();
        _owner = owner;
        Children.Clear();
        foreach (var project in projects)
        {
            Children.Add(BuildCard(project));
        }
    }

    public void Detach()
    {
        foreach (var (project, _, _) in _cards)
        {
            project.PropertyChanged -= OnProjectChanged;
        }
        _cards.Clear();
    }

    public void RefreshLabels()
    {
        foreach (var (project, star, visibility) in _cards)
        {
            UpdateStar(project, star);
            visibility.Text = VisibilityText(project);
        }
    }

    private string VisibilityText(ProfileProject project)
    {
        var labels = _owner?.Labels ?? new ProfilePageLabels();
        return project.Visibility == ProfileProjectVisibility.Private ? labels.Private : labels.Public;
    }

    private Control BuildCard(ProfileProject project)
    {
        var coverFill = new Border();
        if (project.CoverBrush is { } brush)
        {
            coverFill.Background = brush;
        }
        else
        {
            ArcTokens.Bind(coverFill, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        }
        var cover = new Panel { Height = 96 };
        cover.Children.Add(coverFill);
        if (project.Cover is { } source)
        {
            cover.Children.Add(new AsyncImage { Source = source, Stretch = Stretch.UniformToFill });
        }
        AutomationProperties.SetAccessibilityView(cover, AccessibilityView.Raw);
        DockPanel.SetDock(cover, Dock.Top);

        var visibility = new TextBlock { Text = VisibilityText(project), FontSize = 12, LineHeight = 16.8, VerticalAlignment = VerticalAlignment.Bottom };
        ArcTokens.Bind(visibility, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        DockPanel.SetDock(visibility, Dock.Right);
        var title = new TextBlock
        {
            Text = project.Title,
            FontWeight = FontWeight.Medium,
            LineHeight = 15.4,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        AutomationProperties.SetHeadingLevel(title, 4);
        var head = new DockPanel();
        head.Children.Add(visibility);
        head.Children.Add(title);

        var description = new TextBlock { Text = project.Description, TextWrapping = TextWrapping.Wrap, LineHeight = 19.6 };
        ArcTokens.Bind(description, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);

        var faces = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var first = true;
        foreach (var person in project.Collaborators)
        {
            var face = new Avatar
            {
                DisplayName = person.Name,
                Source = person.Avatar,
                Size = AvatarSize.Small,
                Width = 22,
                Height = 22,
                FontSize = 9,
                BorderThickness = new Thickness(2),
                Margin = first ? default : new Thickness(-7, 0, 0, 0),
            };
            ArcTokens.Bind(face, Avatar.BorderBrushProperty, ArcDesignTokenKind.Surface);
            faces.Children.Add(face);
            first = false;
        }
        if (project.Collaborators.Count > 0)
        {
            AutomationProperties.SetName(faces, string.Join(", ", project.Collaborators.Select(c => c.Name)));
        }
        DockPanel.SetDock(faces, Dock.Left);

        var star = new ProfilePageStarButton { VerticalAlignment = VerticalAlignment.Center, Tag = project };
        star.Click += OnStarClick;
        DockPanel.SetDock(star, Dock.Right);
        UpdateStar(project, star);
        _cards.Add((project, star, visibility));
        project.PropertyChanged += OnProjectChanged;

        var updated = new TextBlock
        {
            Text = project.UpdatedText,
            FontSize = 12,
            LineHeight = 16.8,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(project.Collaborators.Count > 0 ? 10 : 0, 0, 10, 0),
        };
        ArcTokens.Bind(updated, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);

        var foot = new DockPanel { Height = 28, Margin = new Thickness(0, 4, 0, 0) };
        foot.Children.Add(faces);
        foot.Children.Add(star);
        foot.Children.Add(updated);

        var body = new StackPanel { Margin = new Thickness(14, 12), Spacing = 6 };
        body.Children.Add(head);
        body.Children.Add(description);
        body.Children.Add(foot);

        var dock = new DockPanel();
        dock.Children.Add(cover);
        dock.Children.Add(body);

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = dock,
            Tag = project,
        };
        card.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        return card;
    }

    private void UpdateStar(ProfileProject project, ProfilePageStarButton star)
    {
        var labels = _owner?.Labels ?? new ProfilePageLabels();
        star.IsStarred = project.IsStarred;
        star.CountText = project.StarCount.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        AutomationProperties.SetName(star, project.IsStarred
            ? labels.UnstarName(project.Title, project.StarCount)
            : labels.StarName(project.Title, project.StarCount));
    }

    private void OnProjectChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is ProfileProject project &&
            (e.Property == ProfileProject.IsStarredProperty || e.Property == ProfileProject.StarCountProperty ||
             e.Property == ProfileProject.TitleProperty))
        {
            foreach (var (p, star, _) in _cards)
            {
                if (ReferenceEquals(p, project))
                {
                    UpdateStar(p, star);
                }
            }
        }
    }

    private void OnStarClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ProfilePageStarButton { Tag: ProfileProject project } star)
        {
            _owner?.ToggleStar(project, star);
        }
    }
}
