using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The screen a <see cref="SupportWidget"/> shows while open.</summary>
public enum SupportWidgetScreen
{
    /// <summary>Greeting, search, recent articles and "Send us a message".</summary>
    Home,

    /// <summary>The visitor's past conversations.</summary>
    Messages,

    /// <summary>One conversation.</summary>
    Chat,

    /// <summary>One article.</summary>
    Article
}

/// <summary>
/// The floating help widget a customer opens from the corner of a site: a round launcher that turns into a panel with a
/// greeting, a search over help articles, the most recent articles, a way to start a chat with the team (with a
/// typing indicator and the reply), and the history of earlier conversations.
/// <code>
/// &lt;Panel&gt;
///     &lt;!-- the page --&gt;
///     &lt;arc:SupportWidget TeamName="Lumen support" /&gt;
/// &lt;/Panel&gt;
/// </code>
/// It sits in the bottom-right corner of the space it is given, so put it over the page. Messages the visitor sends are
/// added to the conversation and raise <see cref="MessageSent"/>; add the team's replies to
/// <see cref="SupportWidgetConversation.Messages"/>. Without your data it shows a sample and answers on its own.
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(OpenPseudoClass)]
public class SupportWidget : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set while the panel is open.</summary>
    public const string OpenPseudoClass = ":open";

    #region Properties

    /// <summary>Whether the panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<SupportWidget, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The team's name in the panel header ("Support").</summary>
    public static readonly StyledProperty<string> TeamNameProperty =
        AvaloniaProperty.Register<SupportWidget, string>(nameof(TeamName), "Support");

    /// <summary>The greeting on the home screen ("Hi there").</summary>
    public static readonly StyledProperty<string> GreetingProperty =
        AvaloniaProperty.Register<SupportWidget, string>(nameof(Greeting), "Hi there");

    /// <summary>The line under the greeting ("How can we help?").</summary>
    public static readonly StyledProperty<string> PromptProperty =
        AvaloniaProperty.Register<SupportWidget, string>(nameof(Prompt), "How can we help?");

    /// <summary>How fast the team answers ("We usually reply in a few minutes").</summary>
    public static readonly StyledProperty<string> ReplyTimeProperty =
        AvaloniaProperty.Register<SupportWidget, string>(nameof(ReplyTime), "We usually reply in a few minutes");

    /// <summary>The <see cref="ChatParticipant.Id"/> of the visitor ("visitor").</summary>
    public static readonly StyledProperty<string> VisitorIdProperty =
        AvaloniaProperty.Register<SupportWidget, string>(nameof(VisitorId), "visitor");

    /// <summary>Whether the widget answers the visitor itself (default true): the sample team types, then replies. Turn it off when your team answers.</summary>
    public static readonly StyledProperty<bool> SimulatesRepliesProperty =
        AvaloniaProperty.Register<SupportWidget, bool>(nameof(SimulatesReplies), true);

    /// <summary>Whether a widget with no articles when it first loads gets the sample (default true).</summary>
    public static readonly StyledProperty<bool> UsesSampleDataProperty =
        AvaloniaProperty.Register<SupportWidget, bool>(nameof(UsesSampleData), true);

    /// <summary>The screen showing now.</summary>
    public static readonly DirectProperty<SupportWidget, SupportWidgetScreen> ScreenProperty =
        AvaloniaProperty.RegisterDirect<SupportWidget, SupportWidgetScreen>(nameof(Screen), o => o.Screen);

    /// <summary>Raised after the visitor sent a message.</summary>
    public static readonly RoutedEvent<SupportWidgetMessageEventArgs> MessageSentEvent =
        RoutedEvent.Register<SupportWidget, SupportWidgetMessageEventArgs>(nameof(MessageSent), RoutingStrategies.Bubble);

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="TeamNameProperty"/>
    public string TeamName { get => GetValue(TeamNameProperty); set => SetValue(TeamNameProperty, value); }

    /// <inheritdoc cref="GreetingProperty"/>
    public string Greeting { get => GetValue(GreetingProperty); set => SetValue(GreetingProperty, value); }

    /// <inheritdoc cref="PromptProperty"/>
    public string Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    /// <inheritdoc cref="ReplyTimeProperty"/>
    public string ReplyTime { get => GetValue(ReplyTimeProperty); set => SetValue(ReplyTimeProperty, value); }

    /// <inheritdoc cref="VisitorIdProperty"/>
    public string VisitorId { get => GetValue(VisitorIdProperty); set => SetValue(VisitorIdProperty, value); }

    /// <inheritdoc cref="SimulatesRepliesProperty"/>
    public bool SimulatesReplies { get => GetValue(SimulatesRepliesProperty); set => SetValue(SimulatesRepliesProperty, value); }

    /// <inheritdoc cref="UsesSampleDataProperty"/>
    public bool UsesSampleData { get => GetValue(UsesSampleDataProperty); set => SetValue(UsesSampleDataProperty, value); }

    /// <inheritdoc cref="ScreenProperty"/>
    public SupportWidgetScreen Screen { get => _screen; private set => SetAndRaise(ScreenProperty, ref _screen, value); }

    /// <summary>The help articles, most recent first.</summary>
    public AvaloniaList<SupportWidgetArticle> Articles { get; } = new();

    /// <summary>The visitor's conversations, newest first.</summary>
    public AvaloniaList<SupportWidgetConversation> Conversations { get; } = new();

    /// <summary>The team members who answer; their faces show on the home screen.</summary>
    public AvaloniaList<ChatParticipant> Agents { get; } = new();

    /// <inheritdoc cref="MessageSentEvent"/>
    public event EventHandler<SupportWidgetMessageEventArgs>? MessageSent
    {
        add => AddHandler(MessageSentEvent, value);
        remove => RemoveHandler(MessageSentEvent, value);
    }

    #endregion

    internal static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<SupportWidget, double>(nameof(PanelWidth), 380);

    internal static readonly StyledProperty<double> PanelHeightProperty =
        AvaloniaProperty.Register<SupportWidget, double>(nameof(PanelHeight), 560);

    internal static readonly StyledProperty<double> LauncherSizeProperty =
        AvaloniaProperty.Register<SupportWidget, double>(nameof(LauncherSize), 56);

    internal double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    internal double PanelHeight { get => GetValue(PanelHeightProperty); set => SetValue(PanelHeightProperty, value); }

    internal double LauncherSize { get => GetValue(LauncherSizeProperty); set => SetValue(LauncherSizeProperty, value); }

    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private SupportWidgetScreen _screen;
    private readonly StackPanel _root;
    private readonly PresenceHost _panelHost;
    private readonly Border _panel;
    private readonly ArcContentSwap _content;
    private readonly Grid _nav;
    private readonly SupportKitChip _homeTab;
    private readonly SupportKitChip _messagesTab;
    private readonly Button _launcher;
    private readonly ArcContentSwap _launcherIcon;
    private readonly ArcGlyph _openGlyph;
    private readonly ArcGlyph _closeGlyph;
    private readonly SupportKitAnnouncer _announcer = new();
    private readonly ObservableCollection<string> _typing = new();
    private Decorator? _host;
    private SearchField? _search;
    private StackPanel? _homeList;
    private ChatThread? _chat;
    private SupportWidgetConversation? _conversation;
    private SupportWidgetArticle? _article;
    private SupportWidgetScreen _returnTo;
    private bool _loaded;
    private int _replyIndex;

    /// <summary>Creates the widget.</summary>
    public SupportWidget()
    {
        BlockFocusModality.Watch();
        _content = new ArcContentSwap { Mode = ContentSwapMode.Slide, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        _homeTab = Tab("Home", ArcIcons.House, SupportWidgetScreen.Home);
        _messagesTab = Tab("Messages", ArcIcons.MessageSquare, SupportWidgetScreen.Messages);
        _nav = SupportKit.Grid("*,*", columnSpacing: 6);
        _nav.Children.Add(_homeTab.At(0));
        _nav.Children.Add(_messagesTab.At(1));
        var navFrame = new Border { Child = _nav, Padding = new Thickness(10, 8) };
        ArcTokens.Bind(navFrame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(navFrame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        NavFrame = navFrame;
        var layout = SupportKit.Grid("*", "*,Auto");
        layout.Children.Add(_content.At(0, 0));
        layout.Children.Add(navFrame.At(0, 1));
        _panel = new Border { Child = layout, ClipToBounds = true }.BindCornerRadius(ArcDesignTokenKind.RadiusStep20);
        ArcTokens.Bind(_panel, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(_panel, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_panel, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        AutomationProperties.SetName(_panel, "Help");
        _panel.AddHandler(KeyDownEvent, OnPanelKeyDown, RoutingStrategies.Bubble);
        var shadow = new Border { Child = _panel }.BindCornerRadius(ArcDesignTokenKind.RadiusStep20);
        ArcTokens.Bind(shadow, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);
        _panelHost = new PresenceHost
        {
            Mode = PresenceMode.Scale,
            Origin = new RelativePoint(1, 1, RelativeUnit.Relative),
            Child = shadow,
            IsPresent = false,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        _openGlyph = new ArcGlyph { Data = ArcIcons.MessageCircle, Size = 24 };
        _closeGlyph = new ArcGlyph { Data = ArcIcons.ChevronDown, Size = 24 };
        _launcherIcon = new ArcContentSwap { Mode = ContentSwapMode.Pop, Content = _openGlyph };
        _launcher = new Button { Variant = ButtonVariant.Primary, Content = _launcherIcon, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right }
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        AutomationProperties.SetName(_launcher, "Open help");
        _launcher.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);

        _root = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Children = { _panelHost, _launcher, _announcer } };
        Agents.CollectionChanged += (_, _) => RefreshScreen();
        Articles.CollectionChanged += (_, _) => RefreshScreen();
        Conversations.CollectionChanged += (_, _) => RefreshScreen();
        ApplySizes();
        Show(SupportWidgetScreen.Home, 0);
    }

    internal Border NavFrame { get; }

    #region Internals for tests

    internal Button Launcher => _launcher;

    internal bool IsPanelShown => _panelHost.IsPresent;

    internal object? Page => _content.Content;

    internal SearchField? Search => _search;

    internal IReadOnlyList<SupportKitRow> HomeRows => _homeList?.Children.OfType<SupportKitRow>().ToList() ?? [];

    internal ChatThread? Chat => _chat;

    internal SupportWidgetConversation? OpenConversation => _conversation;

    internal IReadOnlyCollection<string> Typing => _typing;

    internal string? LastAnnouncement => _announcer.Announcement;

    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>Runs the simulated reply after a delay (tests run it at once).</summary>
    internal Action<TimeSpan, Action> Schedule { get; set; } = static (delay, action) => DispatcherTimer.RunOnce(action, delay);

    #endregion

    #region Public actions

    /// <summary>Opens the panel on the home screen.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the panel; focus returns to the launcher.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Opens a new, empty conversation and focuses its composer.</summary>
    public SupportWidgetConversation StartConversation(string? title = null)
    {
        var conversation = new SupportWidgetConversation($"c{Guid.NewGuid():N}", title ?? "New conversation");
        var agent = Agents.FirstOrDefault();
        if (agent is not null)
        {
            conversation.Messages.Add(new ChatMessage($"w{Guid.NewGuid():N}", agent.Id, $"{Greeting}! {Prompt} Ask us anything.", Clock()));
        }
        Conversations.Insert(0, conversation);
        OpenChat(conversation);
        return conversation;
    }

    /// <summary>Shows <paramref name="conversation"/> (opening the panel if needed).</summary>
    public void OpenChat(SupportWidgetConversation conversation)
    {
        conversation.IsUnread = false;
        _conversation = conversation;
        if (Screen is SupportWidgetScreen.Home or SupportWidgetScreen.Messages)
        {
            _returnTo = Screen;
        }
        Open();
        Show(SupportWidgetScreen.Chat, 1);
        Dispatcher.UIThread.Post(() => _chat?.FocusComposer(), DispatcherPriority.Loaded);
    }

    /// <summary>Shows <paramref name="article"/> (opening the panel if needed).</summary>
    public void OpenArticle(SupportWidgetArticle article)
    {
        _article = article;
        if (Screen is SupportWidgetScreen.Home or SupportWidgetScreen.Messages)
        {
            _returnTo = Screen;
        }
        Open();
        Show(SupportWidgetScreen.Article, 1);
    }

    /// <summary>Goes back from a chat or an article to the screen it was opened from.</summary>
    public void GoBack()
    {
        if (Screen is SupportWidgetScreen.Chat or SupportWidgetScreen.Article)
        {
            Show(_returnTo, -1);
        }
    }

    /// <summary>Sends <paramref name="text"/> as the visitor in the open conversation (a new one when none is open).</summary>
    public void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        var conversation = _conversation ?? StartConversation(text.Length > 40 ? text[..40] + "…" : text);
        if (conversation.Title == "New conversation")
        {
            conversation.Title = text.Length > 40 ? text[..40].TrimEnd() + "…" : text.Trim();
        }
        var message = new ChatMessage($"v{Guid.NewGuid():N}", VisitorId, text.Trim(), Clock()) { Status = ChatMessageStatus.Sent };
        conversation.Messages.Add(message);
        RaiseEvent(new SupportWidgetMessageEventArgs(MessageSentEvent, conversation, message));
        if (SimulatesReplies && Agents.FirstOrDefault() is { } agent)
        {
            SimulateReply(conversation, message, agent);
        }
    }

    #endregion

    private void SimulateReply(SupportWidgetConversation conversation, ChatMessage message, ChatParticipant agent)
    {
        Schedule(TimeSpan.FromMilliseconds(500), () =>
        {
            message.Status = ChatMessageStatus.Read;
            if (ReferenceEquals(_conversation, conversation))
            {
                _typing.Clear();
                _typing.Add(agent.Id);
            }
            Schedule(TimeSpan.FromMilliseconds(1600), () =>
            {
                _typing.Clear();
                var replies = SupportWidgetSamples.Replies;
                var text = replies[_replyIndex++ % replies.Length];
                conversation.Messages.Add(new ChatMessage($"r{Guid.NewGuid():N}", agent.Id, text, Clock()));
                if (!ReferenceEquals(_conversation, conversation) || Screen != SupportWidgetScreen.Chat || !IsOpen)
                {
                    conversation.IsUnread = true;
                }
                _announcer.Announce($"{agent.Name} replied.");
            });
        });
    }

    #region Template and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Decorator>(HostPart);
        SupportKit.Mount(_host, _root);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        if (UsesSampleData && Articles.Count == 0)
        {
            SupportWidgetSamples.Fill(this, Clock());
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            var within = _panel.IsKeyboardFocusWithin;
            _panelHost.IsPresent = open;
            _launcherIcon.Content = open ? _closeGlyph : _openGlyph;
            AutomationProperties.SetName(_launcher, open ? "Close help" : "Open help");
            _announcer.Announce(open ? $"{TeamName} help opened." : null);
            if (!open && within)
            {
                _launcher.Focus(BlockFocusModality.For(this));
            }
            else if (open && Screen == SupportWidgetScreen.Home)
            {
                Dispatcher.UIThread.Post(() => _search?.Focus(BlockFocusModality.For(this)), DispatcherPriority.Loaded);
            }
        }
        else if (property == TeamNameProperty || property == GreetingProperty || property == PromptProperty || property == ReplyTimeProperty)
        {
            RefreshScreen();
        }
        else if (property == PanelWidthProperty || property == PanelHeightProperty || property == LauncherSizeProperty)
        {
            ApplySizes();
        }
    }

    private void ApplySizes()
    {
        _panel.Width = PanelWidth;
        _panel.Height = PanelHeight;
        _launcher.Width = LauncherSize;
        _launcher.Height = LauncherSize;
        _launcher.MinHeight = LauncherSize;
    }

    #endregion

    #region Screens

    private void RefreshScreen() => Show(Screen, 0);

    private void Show(SupportWidgetScreen screen, int direction)
    {
        var keepSearch = screen == SupportWidgetScreen.Home && Screen == SupportWidgetScreen.Home ? _search?.Text : null;
        Screen = screen;
        _content.Direction = direction < 0 ? -1 : 1;
        var mode = _content.Mode;
        if (direction == 0)
        {
            _content.Mode = ContentSwapMode.None;
        }
        _chat = null;
        _content.Content = screen switch
        {
            SupportWidgetScreen.Messages => MessagesScreen(),
            SupportWidgetScreen.Chat when _conversation is not null => ChatScreen(_conversation),
            SupportWidgetScreen.Article when _article is not null => ArticleScreen(_article),
            _ => HomeScreen(keepSearch),
        };
        _content.Mode = mode;
        NavFrame.IsVisible = screen is SupportWidgetScreen.Home or SupportWidgetScreen.Messages;
        _homeTab.IsChecked = screen == SupportWidgetScreen.Home;
        _messagesTab.IsChecked = screen == SupportWidgetScreen.Messages;
        var unread = Conversations.Count(c => c.IsUnread);
        AutomationProperties.SetName(_messagesTab, unread > 0 ? $"Messages, {unread} unread" : "Messages");
    }

    private SupportKitChip Tab(string label, Geometry icon, SupportWidgetScreen screen)
    {
        var tab = new SupportKitChip
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new ArcGlyph { Data = icon, Size = 15 }, new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center } } },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(0),
            MinHeight = 36,
        };
        AutomationProperties.SetName(tab, label);
        tab.Click += (_, _) => Show(screen, screen == SupportWidgetScreen.Home ? -1 : 1);
        return tab;
    }

    private Control HomeScreen(string? search)
    {
        // The header band: the team, the greeting and the faces of who answers.
        var faces = new StackPanel { Orientation = Orientation.Horizontal, Spacing = -8 };
        foreach (var agent in Agents.Take(3))
        {
            faces.Children.Add(new Avatar { DisplayName = agent.Name, Size = AvatarSize.Small, Source = agent.Avatar is { } url ? ImageSource.Parse(url) : null });
        }
        var team = SupportKit.Text(TeamName, ArcDesignTokenKind.AccentForeground, ArcDesignTokenKind.TypeXS, FontWeight.SemiBold);
        var top = SupportKit.Grid("*,Auto");
        top.Children.Add(team.At(0));
        top.Children.Add(faces.At(1));
        var greeting = SupportKit.Text(Greeting, ArcDesignTokenKind.AccentForeground, ArcDesignTokenKind.Type2XL, FontWeight.SemiBold, wrap: true);
        ArcTokens.Bind(greeting, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        AutomationProperties.SetHeadingLevel(greeting, 2);
        var prompt = SupportKit.Text(Prompt, ArcDesignTokenKind.AccentForeground, ArcDesignTokenKind.TypeLG, wrap: true);
        prompt.Opacity = 0.85;
        var band = new Border { Padding = new Thickness(20, 18, 20, 44), Child = new StackPanel { Spacing = 4, Children = { top, greeting, prompt } } };
        top.Margin = new Thickness(0, 0, 0, 14);
        ArcTokens.Bind(band, Border.BackgroundProperty, ArcDesignTokenKind.Accent);

        // "Send us a message" floats over the band's lower edge.
        var startText = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                SupportKit.Text("Send us a message", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.SemiBold),
                SupportKit.Text(ReplyTime, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true),
            },
        };
        var startGrid = SupportKit.Grid("*,Auto", columnSpacing: 10);
        startGrid.Children.Add(startText.At(0));
        startGrid.Children.Add(SupportKit.Glyph(ArcIcons.SendHorizontal, 18, ArcDesignTokenKind.Accent).At(1));
        var start = new SupportKitRow { Content = startGrid, Padding = new Thickness(16, 12) };
        ArcTokens.Bind(start, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(start, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(start, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        AutomationProperties.SetName(start, $"Send us a message. {ReplyTime}");
        start.Click += (_, _) => StartConversation();

        _search = new SearchField { Placeholder = "Search for help", Text = search };
        AutomationProperties.SetName(_search, "Search for help");
        _homeList = new StackPanel { Spacing = 2 };
        var listTitle = SupportKit.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.SemiBold);
        listTitle.Margin = new Thickness(6, 4, 0, 2);
        void Fill()
        {
            _homeList!.Children.Clear();
            var query = _search!.Text ?? string.Empty;
            var searching = !string.IsNullOrWhiteSpace(query);
            var found = searching
                ? Articles.Where(a => SupportKit.Matches(a.Title, query) || SupportKit.Matches(a.Summary, query) || SupportKit.Matches(a.Body, query)).Take(6).ToList()
                : Articles.Take(4).ToList();
            listTitle.Text = searching ? SupportKit.Count(found.Count, "result", "results") : "Recent articles";
            foreach (var article in found)
            {
                _homeList.Children.Add(ArticleRow(article));
            }
            if (searching && found.Count == 0)
            {
                var ask = SupportKit.Button("Ask the team instead", ButtonVariant.Secondary, ArcIcons.MessageCircle);
                ask.HorizontalAlignment = HorizontalAlignment.Left;
                ask.Margin = new Thickness(6, 4);
                ask.Click += (_, _) =>
                {
                    var question = query;
                    StartConversation(question);
                };
                _homeList.Children.Add(SupportKit.Text($"No articles about “{query.Trim()}”.", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, wrap: true));
                ((Control)_homeList.Children[^1]).Margin = new Thickness(6, 2);
                _homeList.Children.Add(ask);
            }
        }
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Fill();
            }
        };
        Fill();
        var help = SupportKit.Panel(new StackPanel { Spacing = 8, Children = { _search, listTitle, _homeList } }, ArcDesignTokenKind.RadiusStep16, ArcDesignTokenKind.Surface, new Thickness(10));
        var below = new StackPanel { Spacing = 12, Margin = new Thickness(14, -30, 14, 14), Children = { start, help } };
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel { Children = { band, below } },
        };
    }

    private SupportKitRow ArticleRow(SupportWidgetArticle article)
    {
        var text = new StackPanel { Spacing = 1 };
        text.Children.Add(SupportKit.Text(article.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium, wrap: true));
        if (!string.IsNullOrEmpty(article.Summary))
        {
            var summary = SupportKit.Text(article.Summary, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, trim: true);
            text.Children.Add(summary);
        }
        var grid = SupportKit.Grid("*,Auto", columnSpacing: 8);
        grid.Children.Add(text.At(0));
        grid.Children.Add(SupportKit.Glyph(ArcIcons.ChevronRight, 15, ArcDesignTokenKind.TextMuted).At(1));
        var row = new SupportKitRow { Content = grid, Item = article, Padding = new Thickness(8, 8) };
        AutomationProperties.SetName(row, article.Title);
        row.Click += (_, _) => OpenArticle(article);
        return row;
    }

    private Control MessagesScreen()
    {
        var header = TopBar("Messages", back: false);
        var list = new StackPanel { Spacing = 2, Margin = new Thickness(8) };
        AutomationProperties.SetName(list, "Conversations");
        foreach (var conversation in Conversations)
        {
            var last = conversation.Messages.LastOrDefault();
            var title = SupportKit.Text(conversation.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM,
                conversation.IsUnread ? FontWeight.SemiBold : FontWeight.Medium, trim: true);
            var preview = SupportKit.Text(last?.Text, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, trim: true);
            var time = SupportKit.Text(conversation.UpdatedAt is { } at ? SupportKit.Ago(at, Clock(), Culture) : null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
            var dot = SupportKit.Dot(ArcDesignTokenKind.Accent, 8);
            dot.IsVisible = conversation.IsUnread;
            var side = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right, Children = { time, dot } };
            dot.HorizontalAlignment = HorizontalAlignment.Right;
            var author = last is null ? null : Agents.FirstOrDefault(a => a.Id == last.AuthorId);
            var face = new Avatar { DisplayName = author?.Name ?? TeamName, Size = AvatarSize.Medium, VerticalAlignment = VerticalAlignment.Top };
            var grid = SupportKit.Grid("Auto,*,Auto", columnSpacing: 10);
            grid.Children.Add(face.At(0));
            grid.Children.Add(new StackPanel { Spacing = 2, Children = { title, preview } }.At(1));
            grid.Children.Add(side.At(2));
            var row = new SupportKitRow { Content = grid, Item = conversation, Padding = new Thickness(10) };
            AutomationProperties.SetName(row, $"{conversation.Title}{(conversation.IsUnread ? ", unread" : string.Empty)}");
            row.Click += (_, _) => OpenChat(conversation);
            list.Children.Add(row);
        }
        if (Conversations.Count == 0)
        {
            var none = SupportKit.Text("No conversations yet.", ArcDesignTokenKind.TextMuted, wrap: true);
            none.Margin = new Thickness(12, 16);
            list.Children.Add(none);
        }
        var start = SupportKit.Button("Send us a message", ButtonVariant.Primary, ArcIcons.SendHorizontal, ControlSize.Medium);
        start.HorizontalAlignment = HorizontalAlignment.Center;
        start.Margin = new Thickness(0, 8, 0, 14);
        start.Click += (_, _) => StartConversation();
        var layout = SupportKit.Grid("*", "Auto,*,Auto");
        layout.Children.Add(header.At(0, 0));
        layout.Children.Add(new ScrollViewer { Content = list, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }.At(0, 1));
        layout.Children.Add(start.At(0, 2));
        return layout;
    }

    private Control ChatScreen(SupportWidgetConversation conversation)
    {
        var participants = new List<ChatParticipant>(Agents) { new(VisitorId, "You") };
        _typing.Clear();
        _chat = new ChatThread
        {
            ItemsSource = conversation.Messages,
            Participants = participants,
            CurrentUserId = VisitorId,
            TypingParticipantIds = _typing,
            Label = conversation.Title,
            Placeholder = "Write a message…",
        };
        _chat.AddHandler(ChatThread.SendRequestedEvent, (_, e) =>
        {
            e.Handled = true;
            SendMessage(e.Draft.Text);
        });
        var layout = SupportKit.Grid("*", "Auto,*");
        layout.Children.Add(TopBar(TeamName, back: true, ReplyTime).At(0, 0));
        layout.Children.Add(_chat.At(0, 1));
        return layout;
    }

    private Control ArticleScreen(SupportWidgetArticle article)
    {
        var body = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(20, 8, 20, 20),
            Children = { SupportKit.Heading(article.Title, ArcDesignTokenKind.TypeLG), new Markdown { Text = article.Body } },
        };
        var layout = SupportKit.Grid("*", "Auto,*");
        layout.Children.Add(TopBar("Article", back: true).At(0, 0));
        layout.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }.At(0, 1));
        return layout;
    }

    private Control TopBar(string title, bool back, string? subtitle = null)
    {
        var grid = SupportKit.Grid("Auto,*,Auto", columnSpacing: 6);
        if (back)
        {
            var button = SupportKit.IconButton(ArcIcons.ArrowLeft, "Back");
            button.Click += (_, _) => GoBack();
            grid.Children.Add(button.At(0));
        }
        var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(back ? 0 : 8, 0, 0, 0) };
        var heading = SupportKit.Text(title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.SemiBold, trim: true);
        AutomationProperties.SetHeadingLevel(heading, 2);
        text.Children.Add(heading);
        if (subtitle is not null)
        {
            text.Children.Add(SupportKit.Text(subtitle, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, trim: true));
        }
        grid.Children.Add(text.At(1));
        var close = SupportKit.IconButton(ArcIcons.X, "Close help");
        close.Click += (_, _) => Close();
        grid.Children.Add(close.At(2));
        var bar = new Border { Child = grid, Padding = new Thickness(8, 8) };
        ArcTokens.Bind(bar, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(bar, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessBottom);
        return bar;
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape)
        {
            return;
        }
        if (Screen is SupportWidgetScreen.Chat or SupportWidgetScreen.Article)
        {
            GoBack();
        }
        else
        {
            Close();
        }
        e.Handled = true;
    }

    #endregion
}
