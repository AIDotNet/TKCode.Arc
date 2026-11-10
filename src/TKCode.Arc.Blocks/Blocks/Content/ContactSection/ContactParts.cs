// Ported from Arc's free contact-section block (MIT, Copyright (c) 2026 Elia Kuratli): TopicPicker, ConfirmAction, Offices and the channel
// button of contact-section.tsx.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Small text helpers for the code-built parts of the contact section.</summary>
internal static class ContactText
{
    public static TextBlock Line(string? text, ArcDesignTokenKind color, ArcDesignTokenKind size, bool isMedium = false)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        if (isMedium)
        {
            ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        return block;
    }
}

/// <summary>A topic of the form: a radio chip. The gliding highlight behind the chips marks the choice.</summary>
[PseudoClasses(CheckedPseudoClass)]
internal sealed class ContactTopicChip : AvaloniaButton
{
    public const string CheckedPseudoClass = ":checked";

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ContactTopicChip, bool>(nameof(IsChecked));

    protected override Type StyleKeyOverride => typeof(ContactTopicChip);

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, IsChecked);
            IsTabStop = IsChecked;
        }
    }
}

/// <summary>The form's topics: chips that form a radio group, where the arrow keys move the choice and wrap around.</summary>
internal sealed class ContactTopicPicker : Panel
{
    private readonly GlidingIndicator _highlight = new() { Spring = ArcSpring.Morph, IsHitTestVisible = false };
    private readonly WrapPanel _chips = new();
    private readonly List<ContactTopicChip> _items = new();
    private bool _placed;

    public ContactTopicPicker(IReadOnlyList<string> topics)
    {
        ArcTokens.Bind(_highlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.ControlOn);
        _highlight.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        Children.Add(_highlight);
        Children.Add(_chips);
        foreach (var topic in topics)
        {
            var chip = new ContactTopicChip { Content = topic, Margin = new Thickness(0, 0, 4, 4) };
            AutomationProperties.SetName(chip, topic);
            chip.Click += (_, _) => Select(topic);
            _items.Add(chip);
            _chips.Children.Add(chip);
        }
        AddHandler(KeyDownEvent, OnChipKeyDown);
        _chips.SizeChanged += (_, _) => Place(false);
        Selected = topics.Count > 0 ? topics[0] : string.Empty;
        Apply();
    }

    public string Selected { get; private set; }

    public IReadOnlyList<ContactTopicChip> Chips => _items;

    public void Select(string topic)
    {
        if (topic == Selected)
        {
            return;
        }
        Selected = topic;
        Apply();
        Place(ArcMotion.IsFull(this));
    }

    private void Apply()
    {
        foreach (var chip in _items)
        {
            chip.IsChecked = (string?)chip.Content == Selected;
        }
    }

    private void Place(bool animate)
    {
        var chip = _items.FirstOrDefault(c => c.IsChecked);
        if (chip is null || chip.Bounds.Width <= 0)
        {
            return;
        }
        _highlight.MoveTo(chip.Bounds, animate && _placed);
        _placed = true;
    }

    private void OnChipKeyDown(object? sender, KeyEventArgs e)
    {
        var delta = e.Key is Key.Right or Key.Down ? 1 : e.Key is Key.Left or Key.Up ? -1 : 0;
        if (delta == 0 || _items.Count == 0)
        {
            return;
        }
        var index = _items.FindIndex(c => c.IsChecked);
        var next = _items[(index + delta + _items.Count) % _items.Count];
        Select((string)next.Content!);
        next.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}

/// <summary>A way to reach you, as a tab of the channel list.</summary>
[PseudoClasses(SelectedPseudoClass, CompactPseudoClass)]
internal sealed class ContactChannelTab : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";
    public const string CompactPseudoClass = ":compact";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ContactChannelTab, string?>(nameof(Label));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<ContactChannelTab, string?>(nameof(Meta));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ContactChannelTab, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ContactChannelTab, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ContactChannelTab, bool>(nameof(IsCompact));

    public ContactChannelTab(ContactChannel channel)
    {
        Channel = channel;
        Label = channel.Label;
        Meta = channel.Meta;
        Icon = channel.Icon;
        AutomationProperties.SetName(this, channel.Label);
    }

    protected override Type StyleKeyOverride => typeof(ContactChannelTab);

    public ContactChannel Channel { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
            IsTabStop = IsSelected;
        }
        else if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
    }
}

/// <summary>An office's mail link.</summary>
internal sealed class ContactMailLink : AvaloniaButton
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ContactMailLink, string?>(nameof(Label));

    protected override Type StyleKeyOverride => typeof(ContactMailLink);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}

/// <summary>An office: the city and its live time, the open status written out, the address, a mail link and a copy button.</summary>
[PseudoClasses(OpenPseudoClass)]
[TemplatePart(MailPart, typeof(AvaloniaButton))]
internal sealed class ContactOfficeCard : TemplatedControl
{
    public const string OpenPseudoClass = ":open";
    public const string MailPart = "PART_Mail";

    public static readonly StyledProperty<string?> CityProperty =
        AvaloniaProperty.Register<ContactOfficeCard, string?>(nameof(City));

    public static readonly StyledProperty<string?> TimeProperty =
        AvaloniaProperty.Register<ContactOfficeCard, string?>(nameof(Time));

    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<ContactOfficeCard, string?>(nameof(Status));

    public static readonly StyledProperty<string?> AddressTextProperty =
        AvaloniaProperty.Register<ContactOfficeCard, string?>(nameof(AddressText));

    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<ContactOfficeCard, string?>(nameof(Email));

    public static readonly StyledProperty<string?> CopyValueProperty =
        AvaloniaProperty.Register<ContactOfficeCard, string?>(nameof(CopyValue));

    private AvaloniaButton? _mail;

    public ContactOfficeCard(ContactOffice office)
    {
        Office = office;
        City = office.City;
        Email = office.Email;
        AddressText = string.Join("\n", office.Address);
        CopyValue = string.Join(", ", office.Address);
        AutomationProperties.SetName(this, office.City);
    }

    public ContactOffice Office { get; }

    public string? City { get => GetValue(CityProperty); set => SetValue(CityProperty, value); }

    public string? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    public string? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public string? AddressText { get => GetValue(AddressTextProperty); set => SetValue(AddressTextProperty, value); }

    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    public string? CopyValue { get => GetValue(CopyValueProperty); set => SetValue(CopyValueProperty, value); }

    internal bool IsOpen => PseudoClasses.Contains(OpenPseudoClass);

    /// <summary>Refreshes the time and the status for <paramref name="now"/>.</summary>
    public void Refresh(DateTimeOffset now)
    {
        var (time, open) = ContactRules.OfficeState(Office, now);
        Time = time;
        Status = open switch
        {
            null => "Checking hours",
            true => $"Open until {ContactRules.HourLabel(Office.CloseHour)}",
            false => "Closed now"
        };
        PseudoClasses.Set(OpenPseudoClass, open == true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_mail is not null)
        {
            _mail.Click -= OnMailClick;
        }
        _mail = e.NameScope.Find<AvaloniaButton>(MailPart);
        if (_mail is not null)
        {
            _mail.Click += OnMailClick;
            _mail.IsVisible = !string.IsNullOrEmpty(Email);
        }
    }

    private void OnMailClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SiteLinks.TryLaunch(this, "mailto:" + Email);
}

/// <summary>
/// A sample channel's action that confirms in place: the label changes, the button goes quiet, and the result stays visible
/// below it. Pressing it again resets it.
/// </summary>
internal sealed class ContactConfirmAction : StackPanel
{
    private static readonly TimeSpan s_busy = TimeSpan.FromMilliseconds(800);

    private readonly Button _button = new() { Variant = ButtonVariant.Primary };
    private readonly SpringSizeDecorator _noteHost;
    private readonly string _idle;
    private readonly string _done;
    private readonly bool _isBusyFirst;
    private DispatcherTimer? _timer;

    public ContactConfirmAction(string idle, string done, Control? note, bool isBusyFirst)
    {
        _idle = idle;
        _done = done;
        _isBusyFirst = isBusyFirst;
        Spacing = 8;
        HorizontalAlignment = HorizontalAlignment.Left;
        _button.HorizontalAlignment = HorizontalAlignment.Left;
        _button.Content = idle;
        AutomationProperties.SetName(_button, idle);
        _button.Click += (_, _) => Press();
        _noteHost = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Height,
            Spring = ArcSpring.Smooth,
            IsExpanded = false,
            Child = note
        };
        AutomationProperties.SetLiveSetting(_noteHost, AutomationLiveSetting.Polite);
        Children.Add(_button);
        Children.Add(_noteHost);
    }

    internal bool IsDone { get; private set; }

    internal Button ActionButton => _button;

    private void Press()
    {
        if (_timer is not null || IsDone)
        {
            _timer?.Stop();
            _timer = null;
            _button.IsLoading = false;
            Set(false);
            return;
        }
        if (!_isBusyFirst)
        {
            Set(true);
            return;
        }
        _button.IsLoading = true;
        _timer = new DispatcherTimer { Interval = s_busy };
        _timer.Tick += (_, _) =>
        {
            _timer?.Stop();
            _timer = null;
            _button.IsLoading = false;
            Set(true);
        };
        _timer.Start();
    }

    private void Set(bool done)
    {
        IsDone = done;
        _button.Variant = done ? ButtonVariant.Secondary : ButtonVariant.Primary;
        _button.Content = done ? new CtaActionContent { Text = _done, HasCheck = true } : _idle;
        AutomationProperties.SetName(_button, done ? _done : _idle);
        _noteHost.IsExpanded = done;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }
}

/// <summary>The sample data of the channels and offices layouts: a fictional design tool.</summary>
internal static class ContactSamples
{
    private static Control Row(string text, string value, string copyLabel)
    {
        var copy = new CopyButton { Value = value, Label = copyLabel, Variant = CopyButtonVariant.Plain };
        DockPanel.SetDock(copy, Dock.Right);
        var line = ContactText.Line(text, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM);
        line.TextWrapping = TextWrapping.NoWrap;
        line.TextTrimming = TextTrimming.CharacterEllipsis;
        line.VerticalAlignment = VerticalAlignment.Center;
        var border = new Border
        {
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 4, 4, 4),
            Child = new DockPanel { Children = { copy, line } }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        ArcTokens.Bind(border, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return border;
    }

    private static Control Detail(string title, string body, params Control[] extras)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(ContactText.Line(title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeLG, true));
        var paragraph = ContactText.Line(body, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);
        paragraph.MaxWidth = 480;
        paragraph.HorizontalAlignment = HorizontalAlignment.Left;
        paragraph.LineHeight = 22.4;
        paragraph.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(paragraph);
        foreach (var extra in extras)
        {
            panel.Children.Add(extra);
        }
        return panel;
    }

    private static Control Note(string text) => ContactText.Line(text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);

    private static Control AgentNote()
    {
        var avatar = new Avatar { DisplayName = "Mina Okafor", Size = AvatarSize.Small, Status = AvatarStatus.Online };
        var text = ContactText.Line("Mina Okafor joined the chat. Say hello.", ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = new ArcShrinkRow { Spacing = 8, Margin = new Thickness(0, 4, 0, 0), Children = { avatar, text } };
        ArcShrinkRow.SetIsShrinkable(text, true);
        return row;
    }

    public static ContactChannel[] Channels() =>
    [
        new()
        {
            Value = "chat", Label = "Live chat", Meta = "Usually under five minutes", Icon = ArcIcons.MessageCircle,
            Detail = Detail("Live chat", "Two people from the support team are on now. Chat suits short questions about a file, an export or a setting that seems wrong.",
                new ContactConfirmAction("Open a chat", "Chat opened", AgentNote(), true))
        },
        new()
        {
            Value = "email", Label = "Email the team", Meta = "One working day", Icon = ArcIcons.Mail,
            Detail = Detail("Email the team", "Attach files, links or a short recording. A person reads every message and answers within one working day.",
                Row("help@example.com", "help@example.com", "Copy email"))
        },
        new()
        {
            Value = "sales", Label = "Speak with sales", Meta = "Monday to Friday, 9am to 5pm CET", Icon = ArcIcons.Phone,
            Detail = Detail("Speak with sales", "Volume pricing, purchase orders or a security questionnaire. Book 20 minutes and we will go through it together.",
                Row("+1 (555) 010-0142", "+15550100142", "Copy number"),
                new ContactConfirmAction("Request a call", "Call requested", Note("We will email you three times that work this week."), true))
        },
        new()
        {
            Value = "community", Label = "Community forum", Meta = "3,100 members", Icon = ArcIcons.Users,
            Detail = Detail("Community forum", "Other customers trade templates and answer each other's questions, often within the hour.",
                new ContactConfirmAction("Visit the forum", "Forum opened", Note("In your app, this button opens your forum."), false))
        }
    ];

    public static ContactOffice[] Offices() =>
    [
        new() { City = "Toronto", TimeZone = "America/Toronto", Address = new(["42 Sample Road, Unit 7", "Toronto, ON M0M 0M0"]), Email = "toronto@example.com" },
        new() { City = "Berlin", TimeZone = "Europe/Berlin", Address = new(["Beispielstraße 8, 2. OG", "10000 Berlin, Germany"]), Email = "berlin@example.com" },
        new() { City = "Singapore", TimeZone = "Asia/Singapore", Address = new(["9 Example Quay, #05-01", "Singapore 000000"]), Email = "singapore@example.com" }
    ];
}
