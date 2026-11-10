// Ported from Arc's free contact-section block (MIT, Copyright (c) 2026 Elia Kuratli): contact-section.tsx and
// contact-section.module.css. The sample channels, offices and copy are this library's own.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A contact section in three layouts: a validated form whose card springs into a confirmation, a list of ways to reach you
/// beside a panel that morphs to each one, and office cards with live local times and open status. Connect
/// <see cref="SubmitHandler"/> in an app: without one the form only pretends to send.
/// </summary>
/// <remarks>Ported from Arc's free contact-section block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(HostPart, typeof(Panel))]
[PseudoClasses(StackedPseudoClass, CompactPseudoClass)]
public class ContactSection : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set while the control is at most 820 wide: the form stacks and the channel list runs sideways.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the control is at most 520 wide: field rows stack and the send button fills the width.</summary>
    public const string CompactPseudoClass = ":compact";

    #region Public properties

    /// <summary>The layout: Form (default), Channels or Offices.</summary>
    public static readonly StyledProperty<ContactSectionVariant> VariantProperty =
        AvaloniaProperty.Register<ContactSection, ContactSectionVariant>(nameof(Variant));

    /// <summary>The section heading; null shows the layout's default.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ContactSection, string?>(nameof(Title));

    /// <summary>One line under the heading; null shows the layout's default.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ContactSection, string?>(nameof(Description));

    /// <summary>The topics offered in the form; while the list is empty it offers Sales, Support, Partnerships and Press.</summary>
    public static readonly DirectProperty<ContactSection, AvaloniaList<string>> TopicsProperty =
        AvaloniaProperty.RegisterDirect<ContactSection, AvaloniaList<string>>(nameof(Topics), o => o.Topics);

    /// <summary>The ways to reach you; while the list is empty the section shows sample channels.</summary>
    public static readonly DirectProperty<ContactSection, AvaloniaList<ContactChannel>> ChannelsProperty =
        AvaloniaProperty.RegisterDirect<ContactSection, AvaloniaList<ContactChannel>>(nameof(Channels), o => o.Channels);

    /// <summary>The value of the chosen channel (two way); null chooses the first.</summary>
    public static readonly StyledProperty<string?> SelectedChannelProperty =
        AvaloniaProperty.Register<ContactSection, string?>(nameof(SelectedChannel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The offices; while the list is empty the section shows sample offices.</summary>
    public static readonly DirectProperty<ContactSection, AvaloniaList<ContactOffice>> OfficesProperty =
        AvaloniaProperty.RegisterDirect<ContactSection, AvaloniaList<ContactOffice>>(nameof(Offices), o => o.Offices);

    /// <summary>Sends a valid message. A faulted task keeps the form and shows an error; a completed one shows the confirmation. Null only pretends to send, after 1.1 seconds.</summary>
    public static readonly StyledProperty<Func<ContactMessage, Task>?> SubmitHandlerProperty =
        AvaloniaProperty.Register<ContactSection, Func<ContactMessage, Task>?>(nameof(SubmitHandler));

    /// <summary>The fact about reply times beside the form.</summary>
    public static readonly StyledProperty<string> ResponseNoteProperty =
        AvaloniaProperty.Register<ContactSection, string>(nameof(ResponseNote), "Replies within one business day");

    /// <summary>The email address beside the form, with a copy button.</summary>
    public static readonly StyledProperty<string> ContactEmailProperty =
        AvaloniaProperty.Register<ContactSection, string>(nameof(ContactEmail), "hello@example.com");

    private readonly AvaloniaList<string> _topics = new();
    private readonly AvaloniaList<ContactChannel> _channels = new();
    private readonly AvaloniaList<ContactOffice> _offices = new();

    /// <inheritdoc cref="VariantProperty"/>
    public ContactSectionVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="TopicsProperty"/>
    public AvaloniaList<string> Topics => _topics;

    /// <inheritdoc cref="ChannelsProperty"/>
    public AvaloniaList<ContactChannel> Channels => _channels;

    /// <inheritdoc cref="SelectedChannelProperty"/>
    public string? SelectedChannel { get => GetValue(SelectedChannelProperty); set => SetValue(SelectedChannelProperty, value); }

    /// <inheritdoc cref="OfficesProperty"/>
    public AvaloniaList<ContactOffice> Offices => _offices;

    /// <inheritdoc cref="SubmitHandlerProperty"/>
    public Func<ContactMessage, Task>? SubmitHandler { get => GetValue(SubmitHandlerProperty); set => SetValue(SubmitHandlerProperty, value); }

    /// <inheritdoc cref="ResponseNoteProperty"/>
    public string ResponseNote { get => GetValue(ResponseNoteProperty); set => SetValue(ResponseNoteProperty, value); }

    /// <inheritdoc cref="ContactEmailProperty"/>
    public string ContactEmail { get => GetValue(ContactEmailProperty); set => SetValue(ContactEmailProperty, value); }

    #endregion

    private static readonly TimeSpan s_clockInterval = TimeSpan.FromSeconds(15);

    private readonly List<ContactChannelTab> _tabs = new();
    private readonly List<ContactOfficeCard> _officeCards = new();
    private readonly Dictionary<ContactChannel, Control> _channelFaces = new();
    private Panel? _host;
    private Border? _body;
    private Grid? _layout;
    private Control? _intro;
    private Control? _main;
    private CtaBalancedText? _titleBlock;
    private TextBlock? _descriptionBlock;
    private ContactFormView? _form;
    private ContentFaceSwap? _channelPanel;
    private GlidingIndicator? _tabHighlight;
    private StackPanel? _tabStack;
    private ScrollViewer? _tabScroller;
    private UniformGrid? _officesGrid;
    private ContactChannelTab? _lastTab;
    private DispatcherTimer? _clock;
    private bool _tabPlaced;
    private bool _rebuilding;

    public ContactSection()
    {
        _topics.CollectionChanged += (_, _) => Rebuild();
        _channels.CollectionChanged += (_, _) => Rebuild();
        _offices.CollectionChanged += (_, _) => Rebuild();
    }

    /// <summary>What "now" is for the office cards; tests replace it.</summary>
    internal Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    internal ContactFormView? Form => _form;

    internal IReadOnlyList<ContactChannelTab> Tabs => _tabs;

    internal ContentFaceSwap? ChannelPanel => _channelPanel;

    internal IReadOnlyList<ContactOfficeCard> OfficeCards => _officeCards;

    internal int OfficeColumns => _officesGrid?.Columns ?? 0;

    internal bool IsLayoutStacked => _layout is not null && _main is not null && Grid.GetRow(_main) == 1;

    private IReadOnlyList<ContactChannel>? _sampleChannels;

    private IReadOnlyList<ContactChannel> EffectiveChannels => _channels.Count > 0 ? _channels : _sampleChannels ??= ContactSamples.Channels();

    private ContactChannel? ActiveChannel
    {
        get
        {
            var channels = EffectiveChannels;
            return channels.FirstOrDefault(c => c.Value == SelectedChannel) ?? channels.FirstOrDefault();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>(HostPart);
        _body = e.NameScope.Find<Border>("PART_Body");
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateClock();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock?.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
        {
            Rebuild();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateWidthState(((Rect)change.NewValue!).Width);
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty)
        {
            UpdateHeading();
        }
        else if (change.Property == SelectedChannelProperty)
        {
            ShowChannel();
        }
    }

    private (string Title, string Description) DefaultCopy => Variant switch
    {
        ContactSectionVariant.Channels => ("Reach us your way", "Choose whatever suits the question. Every route ends with the same small team."),
        ContactSectionVariant.Offices => ("Come and see us", "Three offices across three time zones, so someone is usually at a desk. Tell us when you are stopping by."),
        _ => ("Write to our team", "Tell us what you are working on, or where you are stuck. Every message is read by a person, not a queue.")
    };

    private void UpdateHeading()
    {
        var copy = DefaultCopy;
        var title = Title ?? copy.Title;
        if (_titleBlock is not null)
        {
            _titleBlock.Text = title;
        }
        if (_descriptionBlock is not null)
        {
            _descriptionBlock.Text = Description ?? copy.Description;
        }
        AutomationProperties.SetName(this, title);
    }

    private void Rebuild()
    {
        if (_host is null || _rebuilding)
        {
            return;
        }
        _rebuilding = true;
        _host.Children.Clear();
        _tabs.Clear();
        _officeCards.Clear();
        _channelFaces.Clear();
        _form = null;
        _channelPanel = null;
        _tabHighlight = null;
        _tabStack = null;
        _tabScroller = null;
        _officesGrid = null;
        _lastTab = null;
        _tabPlaced = false;

        _intro = BuildIntro();
        _main = Variant switch
        {
            ContactSectionVariant.Channels => BuildChannels(),
            ContactSectionVariant.Offices => BuildOffices(),
            _ => BuildForm()
        };
        _layout = new Grid { Children = { _intro, _main } };
        _host.Children.Add(_layout);
        _rebuilding = false;
        UpdateHeading();
        ShowChannel();
        UpdateClock();
        UpdateWidthState(Bounds.Width);
    }

    private Control BuildIntro()
    {
        _titleBlock = new CtaBalancedText { TextWrapping = TextWrapping.WrapWithOverflow, IsBalanced = true, HorizontalAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(_titleBlock, CtaBalancedText.LeadingProperty, ArcDesignTokenKind.LeadingDisplay);
        ArcTokens.Bind(_titleBlock, CtaBalancedText.TrackingProperty, ArcDesignTokenKind.TrackingDisplay);
        ArcTokens.Bind(_titleBlock, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_titleBlock, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_titleBlock, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetHeadingLevel(_titleBlock, 2);
        _descriptionBlock = ContactText.Line(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeBase);
        _descriptionBlock.LineHeight = 24;
        var intro = new StackPanel { Spacing = 12, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left, Children = { _titleBlock, _descriptionBlock } };
        if (Variant == ContactSectionVariant.Form)
        {
            var clock = new ArcGlyph { Data = ArcIcons.Clock, Size = 16, StrokeThickness = 1.75 };
            ArcTokens.Bind(clock, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            var mail = new ArcGlyph { Data = ArcIcons.Mail, Size = 16, StrokeThickness = 1.75 };
            ArcTokens.Bind(mail, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            var response = ContactText.Line(ResponseNote, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);
            response.VerticalAlignment = VerticalAlignment.Center;
            ArcShrinkRow.SetIsShrinkable(response, true);
            var address = ContactText.Line(ContactEmail, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);
            address.VerticalAlignment = VerticalAlignment.Center;
            ArcShrinkRow.SetIsShrinkable(address, true);
            var copy = new CopyButton { Value = ContactEmail, Label = "Copy email", IsIconOnly = true, Variant = CopyButtonVariant.Plain };
            intro.Children.Add(new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new ArcShrinkRow { Spacing = 8, MinHeight = 32, Children = { clock, response } },
                    new ArcShrinkRow { Spacing = 8, MinHeight = 32, Children = { mail, address, copy } }
                }
            });
        }
        return intro;
    }

    private Control BuildForm()
    {
        _form = new ContactFormView(this, _topics.Count > 0 ? _topics.ToArray() : ["Sales", "Support", "Partnerships", "Press"]);
        return _form;
    }

    private Control BuildChannels()
    {
        _tabHighlight = new GlidingIndicator { Spring = ArcSpring.Morph, IsHitTestVisible = false };
        ArcTokens.Bind(_tabHighlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.SurfaceMuted);
        _tabHighlight.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        _tabStack = new StackPanel { Spacing = 2 };
        foreach (var channel in EffectiveChannels)
        {
            var tab = new ContactChannelTab(channel);
            tab.Click += (_, _) => Choose(channel);
            _tabs.Add(tab);
            _tabStack.Children.Add(tab);
        }
        AutomationProperties.SetName(_tabStack, "Ways to reach us");
        var rail = new Panel { Children = { _tabHighlight, _tabStack } };
        rail.SizeChanged += (_, _) => PlaceTabHighlight(false);
        rail.AddHandler(KeyDownEvent, OnTabKeyDown);
        _tabScroller = new ScrollViewer
        {
            Content = rail,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _channelPanel = new ContentFaceSwap { SpringsHeight = true, ClipToBounds = true };
        var card = new Border { BorderThickness = new Thickness(1), Child = _channelPanel, ClipToBounds = true }.BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        card.VerticalAlignment = VerticalAlignment.Top;
        var grid = new Grid { Children = { _tabScroller, card } };
        grid.Tag = card;
        return grid;
    }

    private void Choose(ContactChannel channel)
    {
        if (channel.Value != ActiveChannel?.Value)
        {
            SetCurrentValue(SelectedChannelProperty, channel.Value);
        }
    }

    private void ShowChannel()
    {
        if (_channelPanel is null || ActiveChannel is not { } active)
        {
            return;
        }
        var channels = EffectiveChannels.ToList();
        var nextIndex = channels.IndexOf(active);
        var previousIndex = _lastTab is null ? nextIndex : channels.IndexOf(_lastTab.Channel);
        foreach (var tab in _tabs)
        {
            tab.IsSelected = ReferenceEquals(tab.Channel, active);
        }
        _lastTab = _tabs.FirstOrDefault(t => t.IsSelected);
        if (!_channelFaces.TryGetValue(active, out var face))
        {
            face = new ContentPresenter { Content = active.Detail, Padding = new Thickness(Bounds.Width > 0 && Bounds.Width <= 520 ? 20 : 24) };
            _channelFaces[active] = face;
        }
        var direction = Math.Sign(nextIndex - previousIndex);
        _channelPanel.Swap(face, direction == 0 ? 1 : direction);
        AutomationProperties.SetName(_channelPanel, active.Label);
        PlaceTabHighlight(_tabPlaced);
    }

    private void PlaceTabHighlight(bool animate)
    {
        var tab = _tabs.FirstOrDefault(t => t.IsSelected);
        if (_tabHighlight is null || tab is null || tab.Bounds.Width <= 0)
        {
            return;
        }
        _tabHighlight.MoveTo(tab.Bounds, animate && ArcMotion.IsFull(this));
        _tabPlaced = true;
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs e)
    {
        var channels = EffectiveChannels.ToList();
        if (ActiveChannel is not { } active || channels.Count == 0)
        {
            return;
        }
        var delta = e.Key is Key.Down or Key.Right ? 1 : e.Key is Key.Up or Key.Left ? -1 : 0;
        var edge = e.Key == Key.Home ? 0 : e.Key == Key.End ? channels.Count - 1 : -1;
        if (delta == 0 && edge < 0)
        {
            return;
        }
        var next = edge >= 0 ? edge : (channels.IndexOf(active) + delta + channels.Count) % channels.Count;
        Choose(channels[next]);
        _tabs[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private Control BuildOffices()
    {
        IReadOnlyList<ContactOffice> offices = _offices.Count > 0 ? _offices : ContactSamples.Offices();
        _officesGrid = new UniformGrid { ColumnSpacing = 16, RowSpacing = 16 };
        foreach (var office in offices)
        {
            var card = new ContactOfficeCard(office);
            card.Refresh(Now());
            _officeCards.Add(card);
            _officesGrid.Children.Add(card);
        }
        return _officesGrid;
    }

    private void UpdateClock()
    {
        var wanted = VisualRoot is not null && Variant == ContactSectionVariant.Offices;
        if (!wanted)
        {
            _clock?.Stop();
            return;
        }
        _clock ??= CreateClock();
        _clock.Start();
        RefreshOffices();
    }

    private DispatcherTimer CreateClock()
    {
        var timer = new DispatcherTimer { Interval = s_clockInterval };
        timer.Tick += (_, _) => RefreshOffices();
        return timer;
    }

    internal void RefreshOffices()
    {
        var now = Now();
        foreach (var card in _officeCards)
        {
            card.Refresh(now);
        }
    }

    private void UpdateWidthState(double width)
    {
        var stacked = width <= 820;
        var compact = width <= 520;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_body is not null)
        {
            _body.Padding = compact ? new Thickness(16, 48) : new Thickness(32, 80);
        }
        if (_titleBlock is not null)
        {
            HeroText.SetTitleSize(_titleBlock, Math.Clamp(16 + 0.03 * width, 28, ArcTokens.Get<double>(this, ArcDesignTokenKind.Type4XL)));
        }
        if (_layout is null || _intro is null || _main is null)
        {
            return;
        }
        var form = Variant == ContactSectionVariant.Form;
        if (form && !stacked)
        {
            _layout.ColumnDefinitions = new ColumnDefinitions("0.85*,1.15*");
            _layout.RowDefinitions = new RowDefinitions("Auto");
            _layout.ColumnSpacing = 48;
            _layout.RowSpacing = 0;
            Grid.SetColumn(_main, 1);
            Grid.SetRow(_main, 0);
        }
        else
        {
            _layout.ColumnDefinitions = new ColumnDefinitions("*");
            _layout.RowDefinitions = new RowDefinitions("Auto,Auto");
            _layout.ColumnSpacing = 0;
            _layout.RowSpacing = form ? 32 : 40;
            Grid.SetColumn(_main, 0);
            Grid.SetRow(_main, 1);
        }
        _form?.SetCompact(compact);
        if (_main is Grid channels && _tabScroller is not null && channels.Tag is Control card)
        {
            channels.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("Auto,*");
            channels.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            channels.ColumnSpacing = stacked ? 0 : 24;
            channels.RowSpacing = stacked ? 24 : 0;
            Grid.SetColumn(card, stacked ? 0 : 1);
            Grid.SetRow(card, stacked ? 1 : 0);
            _tabScroller.Width = stacked ? double.NaN : Math.Clamp(width * 0.3, 220, 300);
            _tabScroller.HorizontalScrollBarVisibility = stacked ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
            if (_tabStack is not null)
            {
                _tabStack.Orientation = stacked ? Orientation.Horizontal : Orientation.Vertical;
            }
            foreach (var tab in _tabs)
            {
                tab.IsCompact = stacked;
            }
        }
        if (_officesGrid is not null)
        {
            // grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)).
            var inner = Math.Max(0, Math.Min(width, 1120) - (compact ? 32 : 64));
            var fit = (int)Math.Floor((inner + 16) / (240 + 16));
            _officesGrid.Columns = Math.Clamp(fit, 1, Math.Max(1, _officeCards.Count));
        }
    }
}

/// <summary>The form card: the fields, then the confirmation it springs into. Its faces are the morph of Arc's ContactForm.</summary>
internal sealed class ContactFormView : Border
{
    private readonly ContactSection _owner;
    private readonly IReadOnlyList<string> _topics;
    private readonly ContentFaceSwap _swap = new() { SpringsHeight = true, ClipToBounds = true };
    private readonly Control _formFace;
    private readonly Input _name = new() { Label = "Name", Placeholder = "Ada Moreno" };
    private readonly Input _email = new() { Label = "Work email", Placeholder = "ada@company.example" };
    private readonly Textarea _message;
    private readonly ContactTopicPicker _picker;
    private readonly SpringSizeDecorator _failureHost;
    private readonly TextBlock _failure;
    private readonly Button _submit = new() { Variant = ButtonVariant.Primary, Content = "Send message" };
    private readonly UniformGrid _fieldRow = new() { Columns = 2, ColumnSpacing = 16, RowSpacing = 16 };
    private readonly DockPanel _submitRow = new();
    private readonly TextBlock _fine;
    private bool _submitted;
    private bool _isCompact;
    private Control? _sentFace;
    private MotionValue<double>? _checkDraw;

    public ContactFormView(ContactSection owner, IReadOnlyList<string> topics)
    {
        _owner = owner;
        _topics = topics;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        VerticalAlignment = VerticalAlignment.Top;
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);

        _message = new Textarea
        {
            Label = "Message",
            Placeholder = "What are you working on?",
            MaxLength = ContactRules.MaxMessage,
            Description = $"{ContactRules.MaxMessage} characters left",
            FrameHeight = 124
        };
        _picker = new ContactTopicPicker(topics);
        _fieldRow.Children.Add(_name);
        _fieldRow.Children.Add(_email);
        var topicLabel = ContactText.Line("Topic", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, true);
        topicLabel.Margin = new Thickness(0, 0, 0, 8);
        AutomationProperties.SetName(_picker, "Topic");

        _failure = ContactText.Line(null, ArcDesignTokenKind.Danger, ArcDesignTokenKind.TypeSM);
        AutomationProperties.SetLiveSetting(_failure, AutomationLiveSetting.Assertive);
        _failureHost = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, IsExpanded = false, Child = _failure };

        _fine = ContactText.Line("We reply within one business day.", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM);
        _fine.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_submit, Dock.Right);
        _submitRow.Children.Add(_submit);
        _submitRow.Children.Add(_fine);

        _formFace = new StackPanel
        {
            Spacing = 20,
            Margin = new Thickness(24),
            Children =
            {
                _fieldRow,
                new StackPanel { Children = { topicLabel, _picker } },
                _message,
                _failureHost,
                _submitRow
            }
        };
        AutomationProperties.SetName(_formFace, "Contact form");
        Child = _swap;
        _swap.Swap(_formFace);

        _name.TextChanged += (_, _) => OnEdited();
        _email.TextChanged += (_, _) => OnEdited();
        _message.TextChanged += (_, _) => OnEdited();
        _name.KeyDown += OnFieldKeyDown;
        _email.KeyDown += OnFieldKeyDown;
        _submit.Click += (_, _) => _ = SubmitAsync();
    }

    internal Input NameField => _name;

    internal Input EmailField => _email;

    internal Textarea MessageField => _message;

    internal ContactTopicPicker Picker => _picker;

    internal Button SubmitButton => _submit;

    internal TextBlock FailureLine => _failure;

    internal bool IsSent { get; private set; }

    internal bool IsSending { get; private set; }

    internal ContentFaceSwap Swap => _swap;

    internal string? SuccessTitle { get; private set; }

    internal string? SuccessText { get; private set; }

    public void SetCompact(bool compact)
    {
        _isCompact = compact;
        _fieldRow.Columns = compact ? 1 : 2;
        _formFace.Margin = new Thickness(compact ? 20 : 24);
        _sentFace?.SetValue(MarginProperty, new Thickness(compact ? 20 : 24));
        DockPanel.SetDock(_submit, compact ? Dock.Bottom : Dock.Right);
        _submit.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _fine.Margin = compact ? new Thickness(0, 0, 0, 12) : default;
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = SubmitAsync();
        }
    }

    private void OnEdited()
    {
        _message.Description = $"{ContactRules.MaxMessage - (_message.Text?.Length ?? 0)} characters left";
        if (_submitted)
        {
            ShowErrors();
        }
    }

    private (string? Name, string? Email, string? Message) ShowErrors()
    {
        var name = ContactRules.ValidateName(_name.Text ?? string.Empty);
        var email = ContactRules.ValidateEmail(_email.Text ?? string.Empty);
        var message = ContactRules.ValidateMessage(_message.Text ?? string.Empty);
        _name.ErrorMessage = name;
        _email.ErrorMessage = email;
        _message.ErrorMessage = message;
        return (name, email, message);
    }

    /// <summary>Validates and sends: focus goes to the first wrong field, a failure keeps the form, success shows the confirmation.</summary>
    internal async Task SubmitAsync()
    {
        if (IsSending || IsSent)
        {
            return;
        }
        _submitted = true;
        _failureHost.IsExpanded = false;
        var errors = ShowErrors();
        if (errors.Name is not null)
        {
            _name.Focus();
            return;
        }
        if (errors.Email is not null)
        {
            _email.Focus();
            return;
        }
        if (errors.Message is not null)
        {
            _message.Focus();
            return;
        }
        SetSending(true);
        var payload = new ContactMessage
        {
            Name = _name.Text!.Trim(),
            Email = _email.Text!.Trim(),
            Topic = _picker.Selected,
            Message = _message.Text!.Trim()
        };
        try
        {
            if (_owner.SubmitHandler is { } handler)
            {
                await handler(payload);
            }
            else
            {
                await Task.Delay(1100);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            SetSending(false);
            _failure.Text = "We couldn't send your message. Check your connection and try again.";
            _failureHost.IsExpanded = true;
            return;
        }
        SetSending(false);
        ShowSent(payload);
    }

    private void SetSending(bool sending)
    {
        IsSending = sending;
        _submit.IsLoading = sending;
        _name.IsReadOnly = sending;
        _email.IsReadOnly = sending;
        _message.IsReadOnly = sending;
    }

    private void ShowSent(ContactMessage payload)
    {
        IsSent = true;
        var firstName = payload.Name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        SuccessTitle = $"Thanks, {firstName}";
        SuccessText = $"Your message is with our {payload.Topic.ToLowerInvariant()} team. We'll reply to {payload.Email} within one business day.";
        _sentFace = BuildSent(out var heading);
        _swap.Swap(_sentFace, 1);
        Dispatcher.UIThread.Post(() => heading.Focus(), DispatcherPriority.Loaded);
    }

    private Control BuildSent(out TextBlock heading)
    {
        var glyph = new StrokeDrawGlyph
        {
            Data = ArcIcons.Check,
            Size = 26,
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(glyph, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        var tint = new Border { Opacity = 0.13 }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(tint, BackgroundProperty, ArcDesignTokenKind.Success);
        var mark = new Panel
        {
            Width = 52,
            Height = 52,
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { tint, glyph }
        };
        // The check draws itself after the card has started to move, so the delay and curve are set here.
        if (ArcMotion.IsFull(this))
        {
            glyph.Progress = 0;
            _checkDraw = new MotionValue<double>(glyph, 0, MotionVectors.Double);
            _checkDraw.Changed += value => glyph.Progress = value;
            _ = _checkDraw.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(420), ArcEasings.Enter, TimeSpan.FromMilliseconds(160)));
        }
        else
        {
            glyph.Progress = 1;
        }

        heading = new TextBlock { Text = SuccessTitle, Focusable = true, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FocusAdorner = null };
        ArcTokens.Bind(heading, TextBlock.FontSizeProperty, ArcDesignTokenKind.Type2XL);
        ArcTokens.Bind(heading, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(heading, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetHeadingLevel(heading, 3);
        var text = ContactText.Line(SuccessText, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);
        text.MaxWidth = 360;
        text.TextAlignment = TextAlignment.Center;
        text.LineHeight = 21.7;
        text.Margin = new Thickness(0, 0, 0, 12);
        var again = new Button { Variant = ButtonVariant.Secondary, Content = "Send another message", HorizontalAlignment = HorizontalAlignment.Center };
        again.Click += (_, _) => Reset();
        var panel = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(_isCompact ? 20 : 24),
            Children = { mark, heading, text, again }
        };
        AutomationProperties.SetLiveSetting(panel, AutomationLiveSetting.Polite);
        return new Border { Padding = new Thickness(8, 40), Child = panel };
    }

    private void Reset()
    {
        IsSent = false;
        _submitted = false;
        _name.Text = string.Empty;
        _email.Text = string.Empty;
        _message.Text = string.Empty;
        _name.ErrorMessage = null;
        _email.ErrorMessage = null;
        _message.ErrorMessage = null;
        _failureHost.IsExpanded = false;
        _swap.Swap(_formFace, -1);
        Dispatcher.UIThread.Post(() => _name.Focus(), DispatcherPriority.Loaded);
    }
}
