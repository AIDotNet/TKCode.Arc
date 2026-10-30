using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What a status label of <see cref="AgentHandoff"/> shows (its icon and color).</summary>
internal enum AgentHandoffStatusKind
{
    Assistant,
    Transferring,
    Connected,
    Resolved,
    Unavailable
}

/// <summary>
/// One row of the thread: the timeline gutter at the left (the hairline above and below the row's marker, or straight
/// through a row without one) and the row's content. The handoff card's row also carries the pending stretch with the
/// travel dot while it transfers.
/// </summary>
[PseudoClasses(LineAbovePseudoClass, LineBelowPseudoClass, CardPseudoClass, TravelPseudoClass, PendingPseudoClass)]
internal sealed class AgentHandoffRow : ContentControl
{
    internal const string LineAbovePseudoClass = ":line-above";
    internal const string LineBelowPseudoClass = ":line-below";
    internal const string CardPseudoClass = ":card";
    internal const string TravelPseudoClass = ":travel";
    internal const string PendingPseudoClass = ":pending";

    /// <summary>The marker in the gutter (a mark, the provider's mark or an avatar), or null for a row the line runs through.</summary>
    public static readonly StyledProperty<object?> MarkerProperty =
        AvaloniaProperty.Register<AgentHandoffRow, object?>(nameof(Marker));

    /// <summary>Whether the travel dot moves down the pending stretch.</summary>
    public static readonly StyledProperty<bool> IsTravellingProperty =
        AvaloniaProperty.Register<AgentHandoffRow, bool>(nameof(IsTravelling));

    public AgentHandoffRow(object key)
    {
        Key = key;
        Focusable = false;
    }

    /// <summary>What the row stands for (a message, a step, the card).</summary>
    public object Key { get; }

    public object? Marker { get => GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }

    public bool IsTravelling { get => GetValue(IsTravellingProperty); set => SetValue(IsTravellingProperty, value); }

    /// <summary>Whether the line runs above the marker (or through the row).</summary>
    public bool LineAbove { get; private set; }

    /// <summary>Whether the line runs below the marker (or through the row).</summary>
    public bool LineBelow { get; private set; }

    /// <summary>Whether the row shows the pending stretch below its marker.</summary>
    public bool IsPending { get; private set; }

    public void SetLines(bool above, bool below, bool pending, bool travel)
    {
        LineAbove = above;
        LineBelow = below;
        IsPending = pending;
        PseudoClasses.Set(LineAbovePseudoClass, above);
        PseudoClasses.Set(LineBelowPseudoClass, below);
        PseudoClasses.Set(PendingPseudoClass, pending);
        PseudoClasses.Set(TravelPseudoClass, travel);
        IsTravelling = travel;
    }

    public void SetCard(bool card) => PseudoClasses.Set(CardPseudoClass, card);
}

/// <summary>A round step marker: a stroked glyph on the surface, or an avatar.</summary>
[PseudoClasses(AvatarPseudoClass, AccentPseudoClass, SuccessPseudoClass)]
internal sealed class AgentHandoffMarker : TemplatedControl
{
    internal const string AvatarPseudoClass = ":avatar";
    internal const string AccentPseudoClass = ":accent";
    internal const string SuccessPseudoClass = ":success";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AgentHandoffMarker, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AgentHandoffMarker, double>(nameof(GlyphSize), 14);

    public static readonly StyledProperty<string?> PersonNameProperty =
        AvaloniaProperty.Register<AgentHandoffMarker, string?>(nameof(PersonName));

    public static readonly StyledProperty<ImageSource?> PhotoProperty =
        AvaloniaProperty.Register<AgentHandoffMarker, ImageSource?>(nameof(Photo));

    public AgentHandoffMarker()
    {
        Focusable = false;
        IsHitTestVisible = false;
        // Decorative next to the visible labels.
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public string? PersonName { get => GetValue(PersonNameProperty); set => SetValue(PersonNameProperty, value); }

    public ImageSource? Photo { get => GetValue(PhotoProperty); set => SetValue(PhotoProperty, value); }

    public static AgentHandoffMarker ForGlyph(Geometry? glyph, IBrush? brush = null, bool accent = false, bool success = false)
    {
        var marker = new AgentHandoffMarker { Glyph = glyph };
        if (brush is not null)
        {
            // The app's own color for its mark wins over the theme's (a local value).
            marker.Foreground = brush;
        }
        marker.PseudoClasses.Set(AccentPseudoClass, accent && brush is null);
        marker.PseudoClasses.Set(SuccessPseudoClass, success);
        return marker;
    }

    public static AgentHandoffMarker ForPerson(string name, ImageSource? photo)
    {
        var marker = new AgentHandoffMarker { PersonName = name, Photo = photo };
        marker.PseudoClasses.Set(AvatarPseudoClass, true);
        return marker;
    }

}

/// <summary>A step on the timeline: its title, then a muted meta part ("Assistant · the model name"), one line, truncated.</summary>
[TemplatePart(TextPart, typeof(TextBlock))]
internal sealed class AgentHandoffStep : TemplatedControl
{
    internal const string TextPart = "PART_Text";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentHandoffStep, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AgentHandoffStep, string?>(nameof(Meta));

    public static readonly StyledProperty<IBrush?> MetaBrushProperty =
        AvaloniaProperty.Register<AgentHandoffStep, IBrush?>(nameof(MetaBrush));

    private readonly Run _title = new() { FontWeight = FontWeight.Medium };
    private readonly Run _separator = new(" · ");
    private readonly Run _meta = new();
    private TextBlock? _text;

    public AgentHandoffStep()
    {
        Focusable = false;
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    public IBrush? MetaBrush { get => GetValue(MetaBrushProperty); set => SetValue(MetaBrushProperty, value); }

    /// <summary>The line as read: title, then meta.</summary>
    public string Line => string.IsNullOrEmpty(Meta) ? Title ?? string.Empty : Title + " · " + Meta;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text?.Inlines?.Clear();
        _text = e.NameScope.Find<TextBlock>(TextPart);
        if (_text is not null)
        {
            _text.Inlines = [_title, _separator, _meta];
        }
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == MetaProperty || change.Property == MetaBrushProperty)
        {
            Sync();
        }
    }

    private void Sync()
    {
        _title.Text = Title;
        var hasMeta = !string.IsNullOrEmpty(Meta);
        _separator.Text = hasMeta ? " · " : string.Empty;
        _meta.Text = Meta;
        _separator.Foreground = MetaBrush;
        _meta.Foreground = MetaBrush;
        AutomationProperties.SetName(this, Line);
    }
}

/// <summary>
/// One message bubble: the customer's on the right in the accent, the assistant's and the person's on the left, at most
/// <see cref="Fraction"/> of the row wide (wider when the card is compact), with an optional time under it. The text
/// wraps; streamed chunks fade in.
/// </summary>
[TemplatePart(TextPart, typeof(AgentHandoffStreamText))]
[PseudoClasses(CustomerPseudoClass, AssistantPseudoClass, AgentPseudoClass)]
internal sealed class AgentHandoffBubble : TemplatedControl
{
    internal const string TextPart = "PART_Text";
    internal const string CustomerPseudoClass = ":customer";
    internal const string AssistantPseudoClass = ":assistant";
    internal const string AgentPseudoClass = ":agent";

    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<AgentHandoffBubble, double>(nameof(Fraction), 0.8);

    public static readonly StyledProperty<string?> TimeProperty =
        AvaloniaProperty.Register<AgentHandoffBubble, string?>(nameof(Time));

    public static readonly DirectProperty<AgentHandoffBubble, bool> HasTimeProperty =
        AvaloniaProperty.RegisterDirect<AgentHandoffBubble, bool>(nameof(HasTime), o => o.HasTime);

    private AgentHandoffStreamText? _text;
    private string? _pending;
    private bool _hasTime;

    static AgentHandoffBubble()
    {
        AffectsMeasure<AgentHandoffBubble>(FractionProperty);
    }

    public AgentHandoffBubble(AgentHandoffMessage message)
    {
        Message = message;
        Focusable = false;
        Sender = message.From;
        PseudoClasses.Set(CustomerPseudoClass, Sender == AgentHandoffSender.Customer);
        PseudoClasses.Set(AssistantPseudoClass, Sender == AgentHandoffSender.Assistant);
        PseudoClasses.Set(AgentPseudoClass, Sender == AgentHandoffSender.Agent);
        _pending = message.Text;
        Time = message.Time;
    }

    public AgentHandoffMessage Message { get; }

    public AgentHandoffSender Sender { get; }

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }

    public string? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    public bool HasTime => _hasTime;

    /// <summary>The text part (null before the template applies).</summary>
    public AgentHandoffStreamText? TextView => _text;

    /// <summary>The text on screen, or the text that will show once the template applies.</summary>
    public string ShownText => _text?.Shown ?? _pending ?? string.Empty;

    /// <summary>Shows the message's current text; appended text fades in while it streams.</summary>
    public void Update(bool fade)
    {
        Time = Message.Time;
        if (_text is null)
        {
            _pending = Message.Text;
            return;
        }
        _text.Show(Message.Text, fade);
        AutomationProperties.SetName(this, Message.Text);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<AgentHandoffStreamText>(TextPart);
        _text?.Show(_pending ?? Message.Text, fade: false);
        AutomationProperties.SetName(this, Message.Text);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TimeProperty)
        {
            SetAndRaise(HasTimeProperty, ref _hasTime, !string.IsNullOrEmpty(Time));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // A bubble takes at most its fraction of the row and wraps inside it; it never widens the thread.
        var width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width * Fraction) : availableSize.Width;
        return base.MeasureOverride(new Size(width, availableSize.Height));
    }
}

/// <summary>Who is typing: the assistant's short shimmering Thinking line, or a person's typing dots in a bubble.</summary>
[PseudoClasses(AgentPseudoClass)]
internal sealed class AgentHandoffTypingView : TemplatedControl
{
    internal const string AgentPseudoClass = ":agent";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentHandoffTypingView, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsAgentProperty =
        AvaloniaProperty.Register<AgentHandoffTypingView, bool>(nameof(IsAgent));

    public AgentHandoffTypingView()
    {
        Focusable = false;
    }

    /// <summary>The Thinking text, or the dots' name ("Jordan is typing").</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsAgent { get => GetValue(IsAgentProperty); set => SetValue(IsAgentProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAgentProperty)
        {
            PseudoClasses.Set(AgentPseudoClass, IsAgent);
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }
}

/// <summary>The Talk to a person quick reply under the assistant's last answer: a pill with a person glyph.</summary>
internal sealed class AgentHandoffSuggestion : AvaloniaButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentHandoffSuggestion, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override Type StyleKeyOverride => typeof(AgentHandoffSuggestion);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }
}

/// <summary>A status with its icon: a spinner while transferring, a person, a check or an alert. Color is never the only signal.</summary>
[TemplatePart(GlyphPart, typeof(ArcGlyph))]
[PseudoClasses(":assistant", ":transferring", ":connected", ":resolved", ":unavailable")]
internal sealed class AgentHandoffStatusLabel : TemplatedControl
{
    internal const string GlyphPart = "PART_Glyph";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentHandoffStatusLabel, string?>(nameof(Text));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AgentHandoffStatusLabel, double>(nameof(GlyphSize), 14);

    public AgentHandoffStatusLabel(AgentHandoffStatusKind kind, string text)
    {
        Kind = kind;
        Text = text;
        Focusable = false;
        IsHitTestVisible = false;
        PseudoClasses.Set(":" + kind.ToString().ToLowerInvariant(), true);
        AutomationProperties.SetName(this, text);
    }

    public AgentHandoffStatusKind Kind { get; }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <summary>The glyph of a kind (the spinner stands in for it while transferring).</summary>
    public static Geometry GlyphOf(AgentHandoffStatusKind kind) => kind switch
    {
        AgentHandoffStatusKind.Assistant => ArcIcons.Sparkles,
        AgentHandoffStatusKind.Transferring => ArcIcons.LoaderCircle,
        AgentHandoffStatusKind.Connected => ArcIcons.User,
        AgentHandoffStatusKind.Resolved => ArcIcons.CircleCheck,
        _ => ArcIcons.CircleAlert,
    };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<ArcGlyph>(GlyphPart) is { } glyph)
        {
            glyph.Data = GlyphOf(Kind);
        }
    }
}

/// <summary>The summary toggle of the handoff card: the label and a chevron; it reports expanded or collapsed.</summary>
internal sealed class AgentHandoffSummaryToggle : ToolCallDisclosureButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentHandoffSummaryToggle, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override Type StyleKeyOverride => typeof(AgentHandoffSummaryToggle);
}

/// <summary>One fact of the summary: a muted label column and the value.</summary>
internal sealed class AgentHandoffFieldRow : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AgentHandoffFieldRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<AgentHandoffFieldRow, string?>(nameof(Value));

    public AgentHandoffFieldRow()
    {
        Focusable = false;
    }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

/// <summary>
/// The handoff card in the thread, from transferring on: its status title (crossfading), a detail line (the typical
/// wait, the provider, or what to do when nobody is available), Cancel or Try again, the person once they have joined,
/// and the summary the assistant passed on behind a toggle.
/// </summary>
[TemplatePart(TitlePart, typeof(ArcContentSwap))]
[TemplatePart(DetailPart, typeof(TextBlock))]
[TemplatePart(ActionPart, typeof(AvaloniaButton))]
[TemplatePart(TogglePart, typeof(AgentHandoffSummaryToggle))]
[TemplatePart(SummaryPart, typeof(AiHeightBox))]
[TemplatePart(IssuePart, typeof(TextBlock))]
[TemplatePart(FieldsPart, typeof(StackPanel))]
[PseudoClasses(ExpandedPseudoClass, PersonPseudoClass, SummaryPseudoClass)]
internal sealed class AgentHandoffCard : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string DetailPart = "PART_Detail";
    internal const string ActionPart = "PART_Action";
    internal const string TogglePart = "PART_SummaryToggle";
    internal const string SummaryPart = "PART_Summary";
    internal const string IssuePart = "PART_Issue";
    internal const string FieldsPart = "PART_Fields";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string PersonPseudoClass = ":person";
    internal const string SummaryPseudoClass = ":summary";

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AgentHandoffCard, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> PersonNameProperty =
        AvaloniaProperty.Register<AgentHandoffCard, string?>(nameof(PersonName));

    public static readonly StyledProperty<string?> PersonRoleProperty =
        AvaloniaProperty.Register<AgentHandoffCard, string?>(nameof(PersonRole));

    public static readonly StyledProperty<ImageSource?> PersonPhotoProperty =
        AvaloniaProperty.Register<AgentHandoffCard, ImageSource?>(nameof(PersonPhoto));

    public static readonly StyledProperty<string?> ToggleTextProperty =
        AvaloniaProperty.Register<AgentHandoffCard, string?>(nameof(ToggleText));

    private ArcContentSwap? _title;
    private TextBlock? _detail;
    private AvaloniaButton? _action;
    private AgentHandoffSummaryToggle? _toggle;
    private AiHeightBox? _summary;
    private TextBlock? _issue;
    private StackPanel? _fields;
    private AgentHandoffStatusKind? _titleKind;
    private string? _titleText;
    private string? _actionText;
    private bool _expanded = true;
    private string _showName = string.Empty;
    private string _hideName = string.Empty;
    private AgentHandoffSummary? _shownSummary;
    private readonly List<(string, string)> _shownFields = [];

    public AgentHandoffCard()
    {
        Focusable = false;
    }

    /// <summary>Raised when Cancel or Try again is pressed.</summary>
    public event EventHandler? ActionInvoked;

    /// <summary>Raised when the summary toggle is pressed.</summary>
    public event EventHandler? ToggleInvoked;

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public string? PersonName { get => GetValue(PersonNameProperty); set => SetValue(PersonNameProperty, value); }

    public string? PersonRole { get => GetValue(PersonRoleProperty); set => SetValue(PersonRoleProperty, value); }

    public ImageSource? PersonPhoto { get => GetValue(PersonPhotoProperty); set => SetValue(PersonPhotoProperty, value); }

    public string? ToggleText { get => GetValue(ToggleTextProperty); set => SetValue(ToggleTextProperty, value); }

    /// <summary>The title on screen (tests).</summary>
    public string? TitleText => _titleText;

    /// <summary>The action's text, or null when the card has none.</summary>
    public string? ActionText => _actionText;

    public bool IsSummaryExpanded => _expanded;

    internal AvaloniaButton? ActionButton => _action;

    internal AgentHandoffSummaryToggle? Toggle => _toggle;

    internal AiHeightBox? SummaryBox => _summary;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnAction;
        }
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggle;
        }
        _title = e.NameScope.Find<ArcContentSwap>(TitlePart);
        _detail = e.NameScope.Find<TextBlock>(DetailPart);
        _action = e.NameScope.Find<AvaloniaButton>(ActionPart);
        _toggle = e.NameScope.Find<AgentHandoffSummaryToggle>(TogglePart);
        _summary = e.NameScope.Find<AiHeightBox>(SummaryPart);
        _issue = e.NameScope.Find<TextBlock>(IssuePart);
        _fields = e.NameScope.Find<StackPanel>(FieldsPart);
        if (_action is not null)
        {
            _action.Click += OnAction;
        }
        if (_toggle is not null)
        {
            _toggle.Click += OnToggle;
        }
        var kind = _titleKind;
        var text = _titleText;
        _titleKind = null;
        _titleText = null;
        if (kind is { } k && text is not null)
        {
            SetTitle(k, text);
        }
        SetAction(_actionText);
        var summary = _shownSummary;
        _shownSummary = null;
        _shownFields.Clear();
        SetSummary(summary, _expanded, _showName, _hideName, instant: true);
    }

    private void OnAction(object? sender, RoutedEventArgs e) => ActionInvoked?.Invoke(this, EventArgs.Empty);

    private void OnToggle(object? sender, RoutedEventArgs e) => ToggleInvoked?.Invoke(this, EventArgs.Empty);

    /// <summary>Sets the status title; a new status crossfades (Arc's label swap: 100ms out, 160ms in, 2px rise).</summary>
    public void SetTitle(AgentHandoffStatusKind kind, string text)
    {
        if (_titleKind == kind && _titleText == text)
        {
            return;
        }
        _titleKind = kind;
        _titleText = text;
        if (_title is not null)
        {
            _title.Content = new AgentHandoffStatusLabel(kind, text) { GlyphSize = 16 };
        }
    }

    /// <summary>Shows Cancel or Try again, or no button for null.</summary>
    public void SetAction(string? text)
    {
        _actionText = text;
        if (_action is null)
        {
            return;
        }
        _action.IsVisible = text is not null;
        if (text is not null && !Equals(_action.Content, text))
        {
            _action.Content = text;
        }
    }

    /// <summary>Shows the summary (or hides the toggle without one), open or closed.</summary>
    public void SetSummary(AgentHandoffSummary? summary, bool expanded, string showName, string hideName, bool instant)
    {
        _expanded = expanded;
        _showName = showName;
        _hideName = hideName;
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        PseudoClasses.Set(SummaryPseudoClass, summary is not null);
        if (_toggle is not null)
        {
            _toggle.IsVisible = summary is not null;
            _toggle.IsExpanded = expanded;
            AutomationProperties.SetName(_toggle, (ToggleText ?? string.Empty) + ", " + (expanded ? hideName : showName));
            AutomationProperties.SetHelpText(_toggle, expanded ? hideName : showName);
        }
        if (_issue is not null && _issue.Text != summary?.Issue)
        {
            _issue.Text = summary?.Issue;
        }
        if (_issue is not null)
        {
            _issue.IsVisible = !string.IsNullOrEmpty(summary?.Issue);
        }
        SyncFields(summary);
        _shownSummary = summary;
        if (_summary is not null)
        {
            var open = summary is not null && expanded;
            if (_summary.IsExpanded != open)
            {
                if (instant && open)
                {
                    _summary.OpenInstantly();
                }
                else
                {
                    _summary.IsExpanded = open;
                }
            }
        }
    }

    private void SyncFields(AgentHandoffSummary? summary)
    {
        if (_fields is null)
        {
            return;
        }
        var fields = summary?.Fields ?? (IReadOnlyList<AgentHandoffField>)Array.Empty<AgentHandoffField>();
        var same = fields.Count == _shownFields.Count;
        for (var i = 0; same && i < fields.Count; i++)
        {
            same = _shownFields[i] == (fields[i].Label, fields[i].Value);
        }
        if (same)
        {
            return;
        }
        _shownFields.Clear();
        _fields.Children.Clear();
        foreach (var field in fields)
        {
            _shownFields.Add((field.Label, field.Value));
            _fields.Children.Add(new AgentHandoffFieldRow { Label = field.Label, Value = field.Value });
        }
        _fields.IsVisible = fields.Count > 0;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PersonNameProperty)
        {
            PseudoClasses.Set(PersonPseudoClass, !string.IsNullOrEmpty(PersonName));
        }
    }
}

/// <summary>Watches an <see cref="AvaloniaList{T}"/> of messages and every message in it, calling back on any change.</summary>
internal sealed class AgentHandoffMessageWatch
{
    private readonly Action<AgentHandoffMessage?> _changed;
    private AvaloniaList<AgentHandoffMessage>? _list;
    private readonly List<AgentHandoffMessage> _watched = [];

    public AgentHandoffMessageWatch(Action<AgentHandoffMessage?> changed)
    {
        _changed = changed;
    }

    public void Watch(AvaloniaList<AgentHandoffMessage>? list)
    {
        if (ReferenceEquals(list, _list))
        {
            return;
        }
        if (_list is not null)
        {
            _list.CollectionChanged -= OnCollectionChanged;
        }
        foreach (var message in _watched)
        {
            message.PropertyChanged -= OnMessageChanged;
        }
        _watched.Clear();
        _list = list;
        if (list is not null)
        {
            list.CollectionChanged += OnCollectionChanged;
            Track();
        }
    }

    private void Track()
    {
        foreach (var message in _watched)
        {
            message.PropertyChanged -= OnMessageChanged;
        }
        _watched.Clear();
        if (_list is null)
        {
            return;
        }
        foreach (var message in _list)
        {
            message.PropertyChanged += OnMessageChanged;
            _watched.Add(message);
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Track();
        _changed(null);
    }

    private void OnMessageChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is AgentHandoffMessage message)
        {
            _changed(message);
        }
    }
}
