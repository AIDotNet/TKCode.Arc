using System.Collections.Specialized;
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
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A support agent's view of one customer conversation: the messages (a <see cref="ChatThread"/>), a composer that
/// writes either a reply or an internal note, saved replies one press away (or type "/" in an empty composer), the
/// status (open, pending, resolved), and a side panel with the customer's details, tags and the team's notes.
/// <code>
/// &lt;arc:SupportConversation Subject="Charged twice in March" AgentId="agent" Status="Open" /&gt;
/// </code>
/// Replies are added to <see cref="Messages"/> and notes to <see cref="Notes"/>; both raise <see cref="ReplySent"/> so
/// your server can deliver them. A conversation with no messages when it first loads shows a sample.
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(NarrowPseudoClass, NotePseudoClass)]
public class SupportConversation : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set below the narrow width: the details replace the conversation behind a toggle.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the composer writes an internal note.</summary>
    public const string NotePseudoClass = ":note";

    #region Properties

    /// <summary>What the conversation is about, shown in the header.</summary>
    public static readonly StyledProperty<string?> SubjectProperty =
        AvaloniaProperty.Register<SupportConversation, string?>(nameof(Subject));

    /// <summary>The customer; null shows the sample customer while sample data is used, else a blank panel.</summary>
    public static readonly StyledProperty<SupportCustomer?> CustomerProperty =
        AvaloniaProperty.Register<SupportConversation, SupportCustomer?>(nameof(Customer));

    /// <summary>The <see cref="ChatParticipant.Id"/> of the agent using the view; their messages sit on the right.</summary>
    public static readonly StyledProperty<string> AgentIdProperty =
        AvaloniaProperty.Register<SupportConversation, string>(nameof(AgentId), "agent");

    /// <summary>The name notes are signed with ("You").</summary>
    public static readonly StyledProperty<string> AgentNameProperty =
        AvaloniaProperty.Register<SupportConversation, string>(nameof(AgentName), "You");

    /// <summary>The status (two-way).</summary>
    public static readonly StyledProperty<SupportConversationStatus> StatusProperty =
        AvaloniaProperty.Register<SupportConversation, SupportConversationStatus>(nameof(Status), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the composer writes a reply or an internal note (two-way).</summary>
    public static readonly StyledProperty<SupportComposerMode> ComposerModeProperty =
        AvaloniaProperty.Register<SupportConversation, SupportComposerMode>(nameof(ComposerMode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether a conversation with no messages when it first loads gets the sample (default true).</summary>
    public static readonly StyledProperty<bool> UsesSampleDataProperty =
        AvaloniaProperty.Register<SupportConversation, bool>(nameof(UsesSampleData), true);

    /// <summary>Raised after a reply or a note was added.</summary>
    public static readonly RoutedEvent<SupportReplyEventArgs> ReplySentEvent =
        RoutedEvent.Register<SupportConversation, SupportReplyEventArgs>(nameof(ReplySent), RoutingStrategies.Bubble);

    /// <summary>Raised when the status changes.</summary>
    public static readonly RoutedEvent<SupportStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<SupportConversation, SupportStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SubjectProperty"/>
    public string? Subject { get => GetValue(SubjectProperty); set => SetValue(SubjectProperty, value); }

    /// <inheritdoc cref="CustomerProperty"/>
    public SupportCustomer? Customer { get => GetValue(CustomerProperty); set => SetValue(CustomerProperty, value); }

    /// <inheritdoc cref="AgentIdProperty"/>
    public string AgentId { get => GetValue(AgentIdProperty); set => SetValue(AgentIdProperty, value); }

    /// <inheritdoc cref="AgentNameProperty"/>
    public string AgentName { get => GetValue(AgentNameProperty); set => SetValue(AgentNameProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public SupportConversationStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ComposerModeProperty"/>
    public SupportComposerMode ComposerMode { get => GetValue(ComposerModeProperty); set => SetValue(ComposerModeProperty, value); }

    /// <inheritdoc cref="UsesSampleDataProperty"/>
    public bool UsesSampleData { get => GetValue(UsesSampleDataProperty); set => SetValue(UsesSampleDataProperty, value); }

    /// <summary>The messages, oldest first.</summary>
    public AvaloniaList<ChatMessage> Messages { get; } = new();

    /// <summary>The people in the conversation (the customer and the agents).</summary>
    public AvaloniaList<ChatParticipant> Participants { get; } = new();

    /// <summary>The team's internal notes, oldest first (the panel shows the newest first).</summary>
    public AvaloniaList<SupportNote> Notes { get; } = new();

    /// <summary>The saved replies offered in the composer.</summary>
    public AvaloniaList<SupportCannedReply> CannedReplies { get; } = new();

    /// <inheritdoc cref="ReplySentEvent"/>
    public event EventHandler<SupportReplyEventArgs>? ReplySent
    {
        add => AddHandler(ReplySentEvent, value);
        remove => RemoveHandler(ReplySentEvent, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<SupportStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    #endregion

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<SupportConversation, double>(nameof(NarrowWidth), 820);

    internal static readonly StyledProperty<double> SidePanelWidthProperty =
        AvaloniaProperty.Register<SupportConversation, double>(nameof(SidePanelWidth), 300);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double SidePanelWidth { get => GetValue(SidePanelWidthProperty); set => SetValue(SidePanelWidthProperty, value); }

    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private readonly Grid _root;
    private readonly Avatar _avatar;
    private readonly TextBlock _nameText;
    private readonly TextBlock _subjectText;
    private readonly SegmentedControl _status;
    private readonly Button _detailsToggle;
    private readonly ChatThread _thread;
    private readonly SegmentedControl _mode;
    private readonly Textarea _composer;
    private readonly Button _canned;
    private readonly Button _send;
    private readonly Border _composerFrame;
    private readonly Grid _main;
    private readonly ScrollViewer _side;
    private readonly SupportConversationPanel _panel;
    private readonly SupportKitPicker _picker = new();
    private readonly SupportKitAnnouncer _announcer = new();
    private readonly SupportKitBindings _tints = new();
    private Decorator? _host;
    private bool _loaded;
    private bool _narrow;
    private bool _showDetails;
    private bool _syncing;

    /// <summary>Creates the view.</summary>
    public SupportConversation()
    {
        BlockFocusModality.Watch();
        _avatar = new Avatar { Size = AvatarSize.Medium };
        _nameText = SupportKit.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.SemiBold, trim: true);
        _subjectText = SupportKit.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, trim: true);
        AutomationProperties.SetHeadingLevel(_nameText, 2);
        var who = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { _nameText, _subjectText } };
        _status = new SegmentedControl
        {
            ItemsSource = new[]
            {
                new SegmentedOption(SupportConversationStatus.Open, "Open"),
                new SegmentedOption(SupportConversationStatus.Pending, "Pending"),
                new SegmentedOption(SupportConversationStatus.Resolved, "Resolved"),
            },
            SelectedIndex = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_status, "Status");
        _status.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _status.SelectedItem is SegmentedOption { Value: SupportConversationStatus status })
            {
                SetCurrentValue(StatusProperty, status);
            }
        };
        _detailsToggle = SupportKit.IconButton(ArcIcons.PanelRight, "Customer details");
        _detailsToggle.Click += (_, _) => ToggleDetails();
        var header = SupportKit.Grid("Auto,*,Auto,Auto", columnSpacing: 12);
        header.Children.Add(_avatar.At(0));
        header.Children.Add(who.At(1));
        header.Children.Add(_status.At(2));
        header.Children.Add(_detailsToggle.At(3));
        var headerFrame = new Border { Child = header, Padding = new Thickness(16, 12) };
        ArcTokens.Bind(headerFrame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(headerFrame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessBottom);

        _thread = new ChatThread { IsComposerVisible = false, ItemsSource = Messages, Participants = Participants, Label = "Conversation" };

        _mode = new SegmentedControl
        {
            ItemsSource = new[]
            {
                new SegmentedOption(SupportComposerMode.Reply, "Reply"),
                new SegmentedOption(SupportComposerMode.Note, "Internal note"),
            },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(_mode, "Write a");
        _mode.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _mode.SelectedItem is SegmentedOption { Value: SupportComposerMode mode })
            {
                SetCurrentValue(ComposerModeProperty, mode);
            }
        };
        _composer = new Textarea { FrameHeight = 76, Placeholder = "Write a reply… Type / for saved replies" };
        AutomationProperties.SetName(_composer, "Reply");
        _composer.PropertyChanged += OnComposerPropertyChanged;
        _composer.AddHandler(KeyDownEvent, OnComposerKeyDown, RoutingStrategies.Tunnel);
        _canned = SupportKit.Button("Saved replies", ButtonVariant.Ghost, ArcIcons.Bookmark);
        _canned.Click += (_, _) => OpenCannedReplies();
        _send = SupportKit.Button("Send reply", ButtonVariant.Primary, ArcIcons.SendHorizontal);
        _send.Click += (_, _) => Send();
        var hint = SupportKit.Text("Ctrl+Enter to send", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        var tools = SupportKit.Grid("Auto,*,Auto", columnSpacing: 8);
        tools.Children.Add(_canned.At(0));
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        tools.Children.Add(hint.At(1));
        tools.Children.Add(_send.At(2));
        _composerFrame = new Border { Child = new StackPanel { Spacing = 10, Children = { _mode, _composer, tools } }, Padding = new Thickness(16, 12, 16, 16) };
        ArcTokens.Bind(_composerFrame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_composerFrame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);

        _main = SupportKit.Grid("*", "Auto,*,Auto");
        _main.Children.Add(headerFrame.At(0, 0));
        _main.Children.Add(_thread.At(0, 1));
        _main.Children.Add(_composerFrame.At(0, 2));

        _panel = new SupportConversationPanel(this);
        _side = new ScrollViewer { Content = _panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var sideFrame = new Border { Child = _side };
        ArcTokens.Bind(sideFrame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(sideFrame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(sideFrame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        SideFrame = sideFrame;

        _root = SupportKit.Grid("*,Auto");
        _root.Children.Add(_main.At(0));
        _root.Children.Add(sideFrame.At(1));
        _root.Children.Add(_announcer.At(0));

        Messages.CollectionChanged += (_, _) => _panel.Refresh();
        Notes.CollectionChanged += (_, _) => _panel.Refresh();
        _send.IsEnabled = false;
        UpdateMode();
        UpdateLayoutMode();
    }

    internal Border SideFrame { get; }

    #region Internals for tests

    internal ChatThread Thread => _thread;

    internal Textarea Composer => _composer;

    internal Button SendButton => _send;

    internal SupportKitPicker Picker => _picker;

    internal SupportConversationPanel Panel => _panel;

    internal bool IsNarrow => _narrow;

    internal bool IsDetailsShown => _side.IsEffectivelyVisible;

    internal string? LastAnnouncement => _announcer.Announcement;

    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    #endregion

    #region Public actions

    /// <summary>
    /// Sends what the composer holds: a reply joins <see cref="Messages"/>, a note joins <see cref="Notes"/>; the composer
    /// empties and <see cref="ReplySent"/> is raised. Does nothing when the composer is empty.
    /// </summary>
    public bool Send()
    {
        var text = _composer.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        var now = Clock();
        object item;
        if (ComposerMode == SupportComposerMode.Note)
        {
            var note = new SupportNote($"n{Guid.NewGuid():N}", AgentName, text, now);
            Notes.Add(note);
            item = note;
            _announcer.Announce("Internal note added. Only your team can see it.");
        }
        else
        {
            var message = new ChatMessage($"m{Guid.NewGuid():N}", AgentId, text, now) { Status = ChatMessageStatus.Sent };
            Messages.Add(message);
            item = message;
            _announcer.Announce("Reply sent.");
            if (Status == SupportConversationStatus.Open)
            {
                // Answering hands the conversation back to the customer.
                SetCurrentValue(StatusProperty, SupportConversationStatus.Pending);
            }
        }
        _composer.Text = string.Empty;
        RaiseEvent(new SupportReplyEventArgs(ReplySentEvent, text, ComposerMode, item));
        return true;
    }

    /// <summary>Puts <paramref name="reply"/> into the composer (its <c>{name}</c> becomes the customer's first name).</summary>
    public void InsertCannedReply(SupportCannedReply reply)
    {
        var first = (EffectiveCustomer?.Name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
        var text = reply.Text.Replace("{name}", first, StringComparison.Ordinal);
        var current = _composer.Text;
        _composer.Text = string.IsNullOrWhiteSpace(current) ? text : current.TrimEnd() + "\n\n" + text;
        _composer.CaretIndex = _composer.Text.Length;
        _composer.Focus(NavigationMethod.Tab);
    }

    /// <summary>Opens the saved replies under the composer's button.</summary>
    public void OpenCannedReplies()
    {
        if (CannedReplies.Count == 0)
        {
            return;
        }
        _picker.Open(_canned, "Saved replies", CannedReplies.Select(r => (r.Title, (string?)r.Text)), null, title =>
        {
            if (CannedReplies.FirstOrDefault(r => r.Title == title) is { } reply)
            {
                InsertCannedReply(reply);
            }
        });
    }

    #endregion

    internal SupportCustomer? EffectiveCustomer => Customer;

    #region Template, layout and properties

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
        if (UsesSampleData && Messages.Count == 0)
        {
            SupportConversationSamples.Fill(this, Clock());
        }
        _thread.CurrentUserId = AgentId;
        UpdateHeader();
        _panel.Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _picker.Close();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < NarrowWidth;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            UpdateLayoutMode();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SubjectProperty || property == CustomerProperty)
        {
            UpdateHeader();
            _panel.Refresh();
        }
        else if (property == AgentIdProperty)
        {
            _thread.CurrentUserId = AgentId;
        }
        else if (property == StatusProperty)
        {
            var old = change.GetOldValue<SupportConversationStatus>();
            var status = change.GetNewValue<SupportConversationStatus>();
            _syncing = true;
            _status.SelectedIndex = (int)status;
            _syncing = false;
            _announcer.Announce($"Status: {status}.");
            RaiseEvent(new SupportStatusChangedEventArgs(StatusChangedEvent, old, status));
        }
        else if (property == ComposerModeProperty)
        {
            UpdateMode();
        }
        else if (property == SidePanelWidthProperty)
        {
            UpdateLayoutMode();
        }
    }

    private void UpdateHeader()
    {
        var customer = EffectiveCustomer;
        _avatar.DisplayName = customer?.Name;
        _avatar.Source = customer?.Avatar is { } url ? ImageSource.Parse(url) : null;
        _nameText.Text = customer?.Name ?? "Customer";
        _subjectText.Text = Subject;
        _subjectText.IsVisible = !string.IsNullOrEmpty(Subject);
    }

    private void UpdateMode()
    {
        var note = ComposerMode == SupportComposerMode.Note;
        PseudoClasses.Set(NotePseudoClass, note);
        _syncing = true;
        _mode.SelectedIndex = note ? 1 : 0;
        _syncing = false;
        _composer.Placeholder = note ? "Write a note for your team… Only teammates can see it" : "Write a reply… Type / for saved replies";
        AutomationProperties.SetName(_composer, note ? "Internal note" : "Reply");
        if (_send.Content is StackPanel { Children: [_, TextBlock label] })
        {
            label.Text = note ? "Add note" : "Send reply";
        }
        AutomationProperties.SetName(_send, note ? "Add note" : "Send reply");
        // A note is written on a warm tint so it is never mistaken for a reply.
        _tints.Rebind(_composerFrame, Border.BackgroundProperty, note ? ArcDesignTokenKind.WarningSubtle : ArcDesignTokenKind.Surface);
    }

    private void ToggleDetails()
    {
        _showDetails = !_showDetails;
        UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        PseudoClasses.Set(NarrowPseudoClass, _narrow);
        _detailsToggle.IsVisible = _narrow;
        if (_narrow)
        {
            Grid.SetColumn(SideFrame, 0);
            SideFrame.Width = double.NaN;
            _root.ColumnDefinitions[1].Width = new GridLength(0);
            SideFrame.IsVisible = _showDetails;
            _main.IsVisible = true;
            // The details cover the messages; the header stays so the toggle can close them.
            Grid.SetRow(SideFrame, 0);
            SideFrame.Margin = new Thickness(0, 64, 0, 0);
            AutomationProperties.SetName(_detailsToggle, _showDetails ? "Hide customer details" : "Customer details");
        }
        else
        {
            Grid.SetColumn(SideFrame, 1);
            SideFrame.Width = SidePanelWidth;
            SideFrame.Margin = default;
            _root.ColumnDefinitions[1].Width = GridLength.Auto;
            SideFrame.IsVisible = true;
        }
    }

    #endregion

    #region Composer

    private void OnComposerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && _composer.Text == "/" && CannedReplies.Count > 0)
        {
            _composer.Text = string.Empty;
            OpenCannedReplies();
        }
        if (e.Property == TextBox.TextProperty)
        {
            _send.IsEnabled = !string.IsNullOrWhiteSpace(_composer.Text);
        }
    }

    private void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            Send();
            e.Handled = true;
        }
    }

    #endregion
}
