using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The signed-in Northstar app of uiarc.dev/demo: the sidebar (232px, a 64px rail when collapsed, a drawer below 1100px
/// wide), the 52px top bar (breadcrumb, workspace search, notifications, account menu) and the open page, with the
/// toasts at the bottom. <c>[</c> collapses the sidebar.
/// </summary>
public sealed class DemoFrame : Grid, IDemoHost
{
    private const double DrawerBelow = 1100;
    private const double CrumbRootBelow = 720;

    private readonly DemoAppView _app;
    private readonly Border _sidebar = new() { Width = DemoNav.ExpandedWidth, ClipToBounds = true, BorderThickness = new Thickness(0, 0, 1, 0) };
    private readonly DemoNav _nav = new(isRail: true);
    private readonly DemoNav _drawerNav = new(isRail: false);
    private readonly Drawer _drawer;
    private readonly Button _menuButton;
    private readonly StackPanel _crumbRoot = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextMorph _crumb = new() { FontSize = 13, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
    private readonly ExpandingSearch _search;
    private readonly UserMenu _account;
    private readonly ContentControl _pageHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly ToastStack _toasts = new() { Position = ToastStackPosition.BottomCenter, IsContained = true };
    private readonly Dictionary<string, DemoPageView> _pages = new();
    private readonly Dialog _newWorkspace;
    private bool _isCollapsed;

    public DemoFrame(DemoAppView app)
    {
        _app = app;
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        Avalonia.Automation.AutomationProperties.SetName(_toasts, "Workspace notifications");

        // Sidebar: foreground mixed 2.2% into the background, with a subtle right edge; its width follows a spring.
        _sidebar.Child = _nav;
        ArcTokens.Bind(_sidebar, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(_sidebar, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        _sidebar.Transitions = [new DoubleTransition { Property = WidthProperty, Duration = TimeSpan.FromMilliseconds(400), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }];
        WireNav(_nav);
        _nav.CollapseToggled += ToggleCollapsed;
        Children.Add(_sidebar);

        _drawerNav.Margin = new Thickness(0);
        WireNav(_drawerNav);
        _drawer = new Drawer
        {
            Side = DrawerSide.Left,
            Title = DemoData.StudioName,
            Description = DemoData.StudioTagline,
            MaxWidth = 320,
            Content = _drawerNav
        };

        _menuButton = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Width = 36,
            MinWidth = 36,
            Padding = new Thickness(0),
            Margin = new Thickness(-8, 0, 0, 0),
            IsVisible = false,
            Content = DemoUi.Glyph(ArcIcons.Menu, 18, ArcDesignTokenKind.Foreground)
        };
        Avalonia.Automation.AutomationProperties.SetName(_menuButton, "Open navigation");
        _menuButton.Click += (_, _) => _drawer.IsOpen = true;

        _search = BuildSearch();
        _account = BuildAccount();
        _newWorkspace = BuildNewWorkspace();

        var column = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        column.Children.Add(BuildTopBar());
        Grid.SetRow(_pageHost, 1);
        column.Children.Add(_pageHost);
        Grid.SetRow(_toasts, 1);
        column.Children.Add(_toasts);
        column.Children.Add(_drawer);
        column.Children.Add(_newWorkspace);
        Grid.SetColumn(column, 1);
        Children.Add(column);

        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
        Focusable = true;
    }

    public DemoAccount Account => DemoSession.Account ?? DemoSession.Emma;

    /// <summary>The page that is open.</summary>
    public DemoPageView? Current => _pageHost.Content as DemoPageView;

    public bool IsSidebarCollapsed => _isCollapsed;

    /// <summary>Opens <paramref name="page"/>; it is built the first time and kept, like a visited route.</summary>
    public void Show(DemoPage page)
    {
        if (!_pages.TryGetValue(page.Slug, out var view))
        {
            view = DemoPageFactory.Create(page, this);
            _pages[page.Slug] = view;
        }
        _pageHost.Content = view;
        _nav.Selected = page.Slug;
        _drawerNav.Selected = page.Slug;
        _crumb.Text = page.Title;
        SyncAccount();
    }

    public void Navigate(DemoIntent intent)
    {
        _drawer.IsOpen = false;
        _app.Open(intent.Page);
        if (_pages.TryGetValue(intent.Page, out var page) && (intent.View is not null || intent.Project is not null))
        {
            // The page may have just been built: let it lay out before it scrolls or opens something.
            Dispatcher.UIThread.Post(() => page.ApplyIntent(intent), DispatcherPriority.Loaded);
        }
    }

    public string Toast(ToastOptions options) => _toasts.Show(options);

    public void UpdateToast(string id, ToastUpdate update) => _toasts.Update(id, update);

    public void ToggleSidebar() => ToggleCollapsed();

    public Task SignOutAsync() => _app.SignOutAsync();

    public GalleryTheme Appearance => _app.Shell.Appearance;

    public void ToggleCollapsed()
    {
        _isCollapsed = !_isCollapsed;
        _sidebar.Width = _isCollapsed ? DemoNav.CollapsedWidth : DemoNav.ExpandedWidth;
        _nav.IsCollapsed = _isCollapsed;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // "[" anywhere but in a text field, and not with Cmd, Ctrl or Alt.
        if (!e.Handled && e.Key == Key.OemOpenBrackets && (e.KeyModifiers & (KeyModifiers.Meta | KeyModifiers.Control | KeyModifiers.Alt)) == 0
            && e.Source is not TextBox && _sidebar.IsVisible)
        {
            ToggleCollapsed();
            e.Handled = true;
        }
    }

    private void WireNav(DemoNav nav)
    {
        nav.PageChosen += page => Navigate(new DemoIntent(page.Slug));
        nav.InviteRequested += CopyInvite;
        nav.BackRequested += () =>
        {
            _drawer.IsOpen = false;
            _app.Shell.Navigate(GalleryRoute.Home);
        };
    }

    private void ApplyWidth(double width)
    {
        var narrow = width <= DrawerBelow - 1;
        _sidebar.IsVisible = !narrow;
        _menuButton.IsVisible = narrow;
        if (!narrow)
        {
            _drawer.IsOpen = false;
        }
        _crumbRoot.IsVisible = width > CrumbRootBelow;
    }

    private async void CopyInvite()
    {
        _drawer.IsOpen = false;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        try
        {
            if (clipboard is null)
            {
                throw new InvalidOperationException("No clipboard.");
            }
            await clipboard.SetTextAsync(DemoData.InviteLink);
            this.Toast(ToastKind.Success, "Invite link copied", "Anyone with the link can ask to join.");
        }
        catch (Exception)
        {
            this.Toast(ToastKind.Error, "Couldn't copy the invite link", "Your browser blocked clipboard access. Try again from the address bar.");
        }
    }

    #region Top bar

    private Control BuildTopBar()
    {
        var chevron = DemoUi.Glyph(ArcIcons.ChevronRight, 14, ArcDesignTokenKind.BorderStrong);
        _crumbRoot.Children.Add(DemoUi.Line(DemoData.StudioName, ArcDesignTokenKind.TextMuted));
        _crumbRoot.Children.Add(chevron);
        _crumbRoot.VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(_crumb, TextMorph.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var crumbs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        crumbs.Children.Add(_crumbRoot);
        crumbs.Children.Add(_crumb);

        var bell = new NotificationCenter { Label = "Notifications", VerticalAlignment = VerticalAlignment.Center };
        bell.Items.AddRange(DemoData.Notifications());

        var lane = new Panel { Width = 280, Children = { _search } };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(lane);
        actions.Children.Add(bell);
        actions.Children.Add(_account);

        var row = new DockPanel();
        DockPanel.SetDock(_menuButton, Dock.Left);
        DockPanel.SetDock(actions, Dock.Right);
        _menuButton.Margin = new Thickness(-8, 0, 4, 0);
        row.Children.Add(_menuButton);
        row.Children.Add(actions);
        row.Children.Add(crumbs);

        var bar = new Border { Height = 52, BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
        ArcTokens.Bind(bar, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(bar, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        bar.SizeChanged += (_, e) =>
        {
            var gutter = DemoPageView.Gutter(e.NewSize.Width + (_sidebar.IsVisible ? _sidebar.Bounds.Width : 0));
            bar.Padding = new Thickness(gutter, 0);
            lane.Width = Math.Clamp(e.NewSize.Width - 2 * gutter - 220, 36, 280);
        };
        return bar;
    }

    private ExpandingSearch BuildSearch()
    {
        var items = new List<ExpandingSearchItem>();
        foreach (var page in DemoPages.App)
        {
            items.Add(Item($"page:{page.Slug}", page.Title, "Page", "Pages", page.Icon, new DemoIntent(page.Slug)));
        }
        (string Title, string Meta, string Page, string View, Geometry? Icon)[] views =
        [
            ("Project board", "Projects", "projects", "board", DemoIcons.FolderKanban),
            ("Cohort retention", "Analytics", "analytics", "movement", null),
            ("Journeys to a paid plan", "Analytics", "analytics", "journeys", null),
            ("Roles and permissions", "Team", "team", "roles", null),
            ("API keys", "Developers", "developers", "keys", null),
            ("Webhooks", "Developers", "developers", "webhooks", null),
            ("Usage forecast", "Billing", "billing", "usage", null),
            ("Cancel subscription", "Billing", "billing", "cancel", null),
            ("Security", "Settings", "settings", "security", null),
            ("Appearance", "Settings · Preferences", "settings", "preferences", ArcIcons.SlidersHorizontal)
        ];
        foreach (var view in views)
        {
            items.Add(Item($"view:{view.Page}/{view.View}", view.Title, view.Meta, "Pages", view.Icon ?? ArcIcons.Hash, new DemoIntent(view.Page, view.View)));
        }
        foreach (var project in DemoData.Projects)
        {
            var item = Item($"project:{project.Id}", project.Name, $"{project.Client} · {DemoData.Label(project.Status)}", "Projects",
                DemoIcons.FolderKanban, new DemoIntent("projects", "list", project.Id));
            item.Keywords = [project.Client];
            items.Add(item);
        }
        foreach (var person in DemoData.People)
        {
            items.Add(Item($"person:{person.Id}", person.Name, person.Role, "People", ArcIcons.User, new DemoIntent("team", "members")));
        }

        var search = new ExpandingSearch
        {
            Label = "Search the workspace",
            PlaceholderText = "Search pages, projects, people",
            EmptyHint = "Try a page, a project, or a teammate's name.",
            SuggestionsLabel = "Suggested",
            ExpandedWidth = 280,
            MaxResults = 6,
            Anchor = ExpandingSearchAnchor.End,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = items
        };
        search.Suggestions = items.Where(item => item.Id is "project:northwind" or "view:developers/keys" or "view:settings/preferences").ToList();
        search.ItemSelected += (_, e) =>
        {
            if (e.Item.Tag is DemoIntent intent)
            {
                Navigate(intent);
            }
        };
        return search;

        static ExpandingSearchItem Item(string id, string title, string meta, string group, Geometry icon, DemoIntent intent)
            => new() { Id = id, Title = title, Meta = meta, Group = group, Icon = icon, Tag = intent };
    }

    private UserMenu BuildAccount()
    {
        var menu = new UserMenu { Plan = DemoData.Plan, VerticalAlignment = VerticalAlignment.Center };
        menu.Items.Add(new MenuEntry { Header = "Settings", Icon = ArcIcons.Settings });
        menu.Items.Add(new MenuEntry { Header = "Billing", Icon = ArcIcons.CreditCard });
        menu.Items.Add(new MenuEntry { Header = "New workspace", Icon = ArcIcons.Plus });
        menu.Items.Add(new MenuEntry { Header = "Arc library", Icon = ArcIcons.ArrowUpRight });
        menu.ItemClicked += (_, e) =>
        {
            switch (e.Entry.Header as string)
            {
                case "Settings":
                    Navigate(new DemoIntent("settings", "account"));
                    break;
                case "Billing":
                    Navigate(new DemoIntent("billing", "overview"));
                    break;
                case "New workspace":
                    DispatcherTimer.RunOnce(() => _newWorkspace.IsOpen = true, TimeSpan.FromMilliseconds(60));
                    break;
                case "Arc library":
                    _app.Shell.Navigate(GalleryRoute.Home);
                    break;
            }
        };
        menu.StatusChanged += (_, e) =>
        {
            var (title, description) = e.Status switch
            {
                UserMenuStatus.Busy => ("You're busy", "Notifications pause until 5 PM."),
                UserMenuStatus.Away => ("You're away", "Your team sees your new status."),
                _ => ("You're available", "Your team sees your new status.")
            };
            this.Toast(ToastKind.Info, title, description, id: "status");
        };
        menu.ThemePreferenceChanged += async (_, e) => await DemoAppearance.SetAsync(Appearance, e.ThemePreference switch
        {
            UserMenuThemePreference.Dark => DemoAppearance.Dark,
            UserMenuThemePreference.System => DemoAppearance.System,
            _ => DemoAppearance.Light
        });
        // The row spins for 700ms, then the whole app fades out and the sign-in page opens.
        menu.SignOutAction = async () =>
        {
            await Task.Delay(700);
            await _app.SignOutAsync();
        };
        return menu;
    }

    private void SyncAccount()
    {
        _account.DisplayName = Account.Name;
        _account.Email = Account.Email;
        _account.ThemePreference = DemoAppearance.Current(Appearance) switch
        {
            DemoAppearance.System => UserMenuThemePreference.System,
            DemoAppearance.Dark => UserMenuThemePreference.Dark,
            _ => UserMenuThemePreference.Light
        };
    }

    private Dialog BuildNewWorkspace()
    {
        var name = new Input { Label = "Workspace name", PlaceholderText = "Design team", Description = "The address is made from the name." };
        name.PropertyChanged += (_, e) =>
        {
            if (e.Property == Input.TextProperty)
            {
                var slug = Slug(name.Text);
                name.Description = slug.Length == 0 ? "The address is made from the name." : $"Your address will be {slug}.arc.app";
            }
        };
        var plan = new RadioGroup { Header = "Plan" };
        plan.Items.Add(new RadioOption { Value = "free", Label = "Free", Description = "Up to 3 people." });
        plan.Items.Add(new RadioOption { Value = "team", Label = "Team", Description = "Unlimited people and shared history." });
        plan.SelectedValue = "team";
        var review = DemoUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);

        var form = new MultiStepForm { NextLabel = "Continue", CompleteLabel = "Create workspace" };
        form.Steps.Add(new MultiStepFormStep { Title = "Name", Description = "Pick something your team will recognize.", Content = name });
        form.Steps.Add(new MultiStepFormStep { Title = "Plan", Description = "You can change this later in billing.", Content = plan });
        form.Steps.Add(new MultiStepFormStep { Title = "Review", Description = "Check the details before you create it.", Content = review });
        form.StepChanged += (_, _) =>
        {
            var workspace = string.IsNullOrWhiteSpace(name.Text) ? "Untitled workspace" : name.Text!.Trim();
            review.Text = $"{workspace} on the {(plan.SelectedValue as string == "free" ? "Free" : "Team")} plan.";
        };
        return new Dialog { Title = "New workspace", MaxWidth = 720, Content = form };

        static string Slug(string? text) => string.Join('-', System.Text.RegularExpressions.Regex.Split((text ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+")
            .Where(part => part.Length > 0));
    }

    #endregion
}
