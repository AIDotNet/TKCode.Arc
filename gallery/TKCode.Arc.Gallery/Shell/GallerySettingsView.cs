using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The desktop app's settings (<c>/settings/appearance</c>, <c>/settings/language</c>, <c>/settings/about</c>): a rail of
/// pages beside the open one. About holds the version, the update check and the open source project.
/// </summary>
internal sealed class GallerySettingsView : UserControl
{
    public const string AppearancePage = "appearance";
    public const string LanguagePage = "language";
    public const string AboutPage = "about";

    /// <summary>The settings pages, for the route.</summary>
    public static readonly string[] Pages = [AppearancePage, LanguagePage, AboutPage];

    private readonly GalleryShell _shell;
    private readonly Dictionary<string, (Button Button, Control Pane)> _pages = new();
    private readonly ArcGlyph _statusGlyph = new() { Size = 18, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _statusTitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
    private readonly TextBlock _statusHint = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 12.5);
    private readonly Button _updateButton = new() { Variant = ButtonVariant.Primary, Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _updateLabel = new() { VerticalAlignment = VerticalAlignment.Center };

    public GallerySettingsView(GalleryShell shell)
    {
        _shell = shell;
        var rail = new StackPanel { Spacing = 2 };
        var title = GallerySite.Text(GalleryShellLangResourceKind.Settings, ArcDesignTokenKind.Foreground, 22, FontWeight.Medium);
        title.Margin = new Thickness(12, 0, 12, 10);
        rail.Children.Add(title);

        var panes = new Panel();
        Add(rail, panes, AppearancePage, GalleryShellLangResourceKind.Appearance, ArcIcons.Palette, BuildAppearance());
        Add(rail, panes, LanguagePage, GalleryShellLangResourceKind.Language, ArcIcons.Languages, BuildLanguage());
        Add(rail, panes, AboutPage, GalleryShellLangResourceKind.SettingsAbout, ArcIcons.Info, BuildAbout());

        var back = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0), Content = GallerySite.Labelled(GalleryShellLangResourceKind.SettingsBackHome, null) };
        back.Click += (_, _) => _shell.Navigate(GalleryRoute.Home);
        rail.Children.Add(back);

        var layout = new GalleryTwoPane { StartWidth = 210, StackBelow = 720, Spacing = 32 };
        layout.Children.Add(rail);
        layout.Children.Add(panes);

        var column = new StackPanel { MaxWidth = 1040, HorizontalAlignment = HorizontalAlignment.Stretch };
        column.Children.Add(layout);
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(32, 32, 32, 64), Child = column }
        };

        _updateButton.Content = _updateLabel;
        _updateButton.Click += OnUpdateClick;
        // The state is shared with the toast and the home page; the text belongs to the language.
        AttachedToVisualTree += (_, _) => GalleryUpdates.Changed += SyncUpdate;
        DetachedFromVisualTree += (_, _) => GalleryUpdates.Changed -= SyncUpdate;
        GalleryText.Track(this, SyncUpdate);
        Show(AboutPage);
    }

    /// <summary>The page that is showing.</summary>
    public string CurrentPage { get; private set; } = AboutPage;

    /// <summary>The label of the update line: what the check found, in the current language.</summary>
    public string UpdateStatusText => _statusTitle.Text ?? string.Empty;

    /// <summary>The button of the update line.</summary>
    public Button UpdateButton => _updateButton;

    /// <summary>Shows <paramref name="page"/>.</summary>
    public void Show(string page)
    {
        CurrentPage = _pages.ContainsKey(page) ? page : AboutPage;
        foreach (var (name, (button, pane)) in _pages)
        {
            pane.IsVisible = name == CurrentPage;
            button.Variant = name == CurrentPage ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        }
    }

    private void Add(StackPanel rail, Panel panes, string page, GalleryShellLangResourceKind label, Geometry glyph, Control pane)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new ArcGlyph { Data = glyph, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(GallerySite.Text(label));
        var button = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Content = row };
        GalleryText.Bind(button, AutomationProperties.NameProperty, label);
        button.Click += (_, _) => _shell.Navigate(GalleryRoute.Settings(page));
        rail.Children.Add(button);
        panes.Children.Add(pane);
        _pages[page] = (button, pane);
    }

    private Control BuildAppearance()
    {
        var panel = new GalleryAppearancePanel(_shell.Appearance, _shell) { Width = double.NaN, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Left };
        return Pane(GalleryShellLangResourceKind.Appearance, panel);
    }

    private static Control BuildLanguage()
    {
        var names = GalleryLanguages.All.Select(language => language.Name).ToArray();
        var choice = new GallerySegmented(names) { HorizontalAlignment = HorizontalAlignment.Left };
        var syncing = false;
        GalleryText.Track(choice, () =>
        {
            syncing = true;
            var index = Array.FindIndex(GalleryLanguages.All.ToArray(), language => string.Equals(language.Tag, GalleryText.CurrentLanguage, StringComparison.OrdinalIgnoreCase));
            choice.SelectedIndex = Math.Max(0, index);
            syncing = false;
        });
        choice.SelectionChanged += index =>
        {
            if (!syncing)
            {
                GalleryLanguages.Choose(GalleryLanguages.All[index].Tag);
            }
        };
        return Pane(GalleryShellLangResourceKind.Language, choice);
    }

    private static StackPanel Pane(GalleryShellLangResourceKind title, Control content)
    {
        var pane = new StackPanel { Spacing = 16 };
        pane.Children.Add(GallerySite.Text(title, ArcDesignTokenKind.Foreground, 22, FontWeight.Medium));
        pane.Children.Add(content);
        return pane;
    }

    private Control BuildAbout()
    {
        var pane = new StackPanel { Spacing = 18 };
        pane.Children.Add(BuildIdentity());
        pane.Children.Add(BuildUpdateBox());

        pane.Children.Add(Group(GalleryShellLangResourceKind.AboutVersionGroup,
            Row(GalleryShellLangResourceKind.AboutVersion, null, Value(GalleryHost.Version)),
            Row(GalleryShellLangResourceKind.AboutRuntime, null, Value(GalleryHost.Runtime)),
            Row(GalleryShellLangResourceKind.AboutDiagnostics, GalleryShellLangResourceKind.AboutDiagnosticsHint, BuildCopyDiagnostics())));

        pane.Children.Add(Group(GalleryShellLangResourceKind.AboutOpenSource,
            Row(GalleryShellLangResourceKind.AboutLicense, null, Link(GalleryShellLangResourceKind.FooterLicense, new Uri($"{GalleryGitHub.RepositoryUrl}/blob/main/LICENSE"))),
            Row(GalleryShellLangResourceKind.AboutSource, null, Link(GalleryShellLangResourceKind.AboutOpenGitHub, GalleryGitHub.RepositoryUrl, ButtonVariant.Secondary)),
            Row(GalleryShellLangResourceKind.AboutIssues, GalleryShellLangResourceKind.AboutIssuesHint, Link(GalleryShellLangResourceKind.AboutOpenGitHub, new Uri($"{GalleryGitHub.RepositoryUrl}/issues"))),
            Row(GalleryShellLangResourceKind.AboutChangelog, null, GallerySite.Action(GalleryShellLangResourceKind.AboutChangelogOpen, ButtonVariant.Ghost, () => _shell.Navigate(GallerySite.Docs("DocsChangelog")), null, ControlSize.Small)),
            Row(GalleryShellLangResourceKind.AboutNotices, GalleryShellLangResourceKind.AboutNoticesHint, Link(GalleryShellLangResourceKind.AboutView, new Uri($"{GalleryGitHub.RepositoryUrl}/blob/main/THIRD-PARTY-NOTICES.md")))));

        var sponsor = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = "nextco.work" };
        sponsor.Click += (_, _) => GallerySite.Open(this, GalleryFriends.SponsorSite("about"));
        pane.Children.Add(Group(GalleryShellLangResourceKind.FooterSponsor, Row(GalleryShellLangResourceKind.AboutSponsor, null, sponsor)));
        return pane;
    }

    private static Control BuildIdentity()
    {
        var names = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.AboutAppName, ArcDesignTokenKind.Foreground, 24, FontWeight.Medium));
        names.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.AboutAppDescription, ArcDesignTokenKind.TextSecondary, 14));
        var badge = new Badge { Tone = BadgeTone.Warning, VerticalAlignment = VerticalAlignment.Center, IsVisible = GalleryHost.Version.Contains('-') };
        GalleryText.Bind(badge, ContentControl.ContentProperty, GalleryShellLangResourceKind.AboutPreview);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 16 };
        // The artwork's corners are not transparent, so the frame rounds them.
        var logo = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(16), ClipToBounds = true, Child = new Image { Source = GalleryBrand.CreateLogo() } };
        Grid.SetColumn(names, 1);
        Grid.SetColumn(badge, 2);
        row.Children.Add(logo);
        row.Children.Add(names);
        row.Children.Add(badge);
        return row;
    }

    private Control BuildUpdateBox()
    {
        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_statusTitle);
        text.Children.Add(_statusHint);
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(_updateButton, 2);
        line.Children.Add(_statusGlyph);
        line.Children.Add(text);
        line.Children.Add(_updateButton);

        var auto = new Switch { IsChecked = GalleryUpdates.AutoCheck, VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(auto, AutomationProperties.NameProperty, GalleryShellLangResourceKind.UpdateAutoCheck);
        auto.IsCheckedChanged += (_, _) => GalleryUpdates.AutoCheck = auto.IsChecked == true;
        var autoText = new StackPanel { Spacing = 1 };
        autoText.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.UpdateAutoCheck, ArcDesignTokenKind.Foreground, 14));
        autoText.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.UpdateAutoCheckHint, ArcDesignTokenKind.TextMuted, 12.5));
        var autoLine = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        Grid.SetColumn(auto, 1);
        autoLine.Children.Add(autoText);
        autoLine.Children.Add(auto);

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(line);
        stack.Children.Add(autoLine);
        return GallerySite.Panel(stack, ArcDesignTokenKind.SurfaceMuted, 18);
    }

    private void OnUpdateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (GalleryUpdates.Status == GalleryUpdateStatus.Available && GalleryUpdates.Available is { } release)
        {
            GallerySite.Open(this, release.DownloadUrl);
            return;
        }
        _ = GalleryUpdates.CheckAsync();
    }

    private void SyncUpdate()
    {
        var (glyph, title, hint) = GalleryUpdates.Status switch
        {
            GalleryUpdateStatus.Checking => (ArcIcons.RefreshCw, GalleryText.Get(GalleryShellLangResourceKind.UpdateChecking), GalleryText.Get(GalleryShellLangResourceKind.UpdateCheckingHint)),
            GalleryUpdateStatus.UpToDate => (ArcIcons.Check, GalleryText.Get(GalleryShellLangResourceKind.UpdateUpToDate), GalleryText.Format(GalleryShellLangResourceKind.UpdateUpToDateHint, GalleryHost.Version)),
            GalleryUpdateStatus.Available => (ArcIcons.Download, GalleryText.Format(GalleryShellLangResourceKind.UpdateAvailable, GalleryUpdates.Available!.Version), GalleryText.Format(GalleryShellLangResourceKind.UpdateAvailableHint, GalleryHost.Version)),
            GalleryUpdateStatus.Failed => (ArcIcons.TriangleAlert, GalleryText.Get(GalleryShellLangResourceKind.UpdateFailed), GalleryText.Get(GalleryShellLangResourceKind.UpdateFailedHint)),
            _ => (ArcIcons.Info, GalleryText.Format(GalleryShellLangResourceKind.UpdateIdle, GalleryHost.Version), GalleryText.Get(GalleryShellLangResourceKind.UpdateIdleHint))
        };
        _statusGlyph.Data = glyph;
        _statusTitle.Text = title;
        _statusHint.Text = hint;
        _updateButton.IsEnabled = GalleryUpdates.Status != GalleryUpdateStatus.Checking;
        _updateLabel.Text = GalleryText.Get(GalleryUpdates.Status == GalleryUpdateStatus.Available ? GalleryShellLangResourceKind.UpdateDownload : GalleryShellLangResourceKind.UpdateCheckNow);
    }

    private static Control BuildCopyDiagnostics()
    {
        var copy = new CopyButton { Value = $"TKCode Arc Gallery {GalleryHost.Version}\n{GalleryHost.Runtime}", Variant = CopyButtonVariant.Outline };
        GalleryText.Bind(copy, CopyButton.LabelProperty, GalleryShellLangResourceKind.AboutCopy);
        GalleryText.Bind(copy, CopyButton.CopiedLabelProperty, GalleryShellLangResourceKind.Copied);
        GalleryText.Bind(copy, CopyButton.FailedLabelProperty, GalleryShellLangResourceKind.Failed);
        return copy;
    }

    private static TextBlock Value(string text)
    {
        var value = GalleryUi.Mono(text, ArcDesignTokenKind.TextSecondary, 13);
        value.TextWrapping = TextWrapping.Wrap;
        value.TextAlignment = TextAlignment.Right;
        return value;
    }

    private Button Link(GalleryShellLangResourceKind label, Uri url, ButtonVariant variant = ButtonVariant.Ghost) =>
        GallerySite.Action(label, variant, () => GallerySite.Open(this, url), ArcIcons.ArrowUpRight, ControlSize.Small);

    private static SettingsRow Row(GalleryShellLangResourceKind title, GalleryShellLangResourceKind? hint, Control content)
    {
        var row = new SettingsRow { Content = content };
        GalleryText.Bind(row, SettingsRow.HeaderProperty, title);
        if (hint is { } description)
        {
            GalleryText.Bind(row, SettingsRow.DescriptionProperty, description);
        }
        return row;
    }

    private static SettingsGroup Group(GalleryShellLangResourceKind title, params SettingsRow[] rows)
    {
        var group = new SettingsGroup();
        GalleryText.Bind(group, Avalonia.Controls.Primitives.HeaderedItemsControl.HeaderProperty, title);
        foreach (var row in rows)
        {
            group.Items.Add(row);
        }
        return group;
    }
}
