using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The looks of an <see cref="AgentMemoryButton"/>.</summary>
internal enum AgentMemoryButtonKind
{
    /// <summary>Edit / Forget on a row: 28px, 8px corners, a 15px glyph; the focus ring sits 2px outside.</summary>
    RowAction,

    /// <summary>The search toggle and the close button: 30px round, a 16px glyph; the focus ring sits inside.</summary>
    Search,

    /// <summary>"Clear search": an underlined text button.</summary>
    Link,

    /// <summary>The source of a memory with a link ("From a chat"), 12px like the line it sits in.</summary>
    Source
}

/// <summary>
/// The small buttons of the memory card, one look per <see cref="Kind"/>: an optional Lucide glyph or text content. Hover
/// uses <c>:hover</c>, set for mouse and pen only (Arc's <c>(hover: hover) and (pointer: fine)</c>).
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class AgentMemoryButton : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<AgentMemoryButtonKind> KindProperty =
        AvaloniaProperty.Register<AgentMemoryButton, AgentMemoryButtonKind>(nameof(Kind));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AgentMemoryButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AgentMemoryButton, double>(nameof(GlyphSize), 15);

    /// <summary>For a button that opens something (the search toggle): whether it is open (<c>aria-expanded</c>); null for others.</summary>
    public static readonly StyledProperty<bool?> ExpandedProperty =
        AvaloniaProperty.Register<AgentMemoryButton, bool?>(nameof(Expanded));

    public AgentMemoryButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public bool? Expanded { get => GetValue(ExpandedProperty); set => SetValue(ExpandedProperty, value); }

    protected override Type StyleKeyOverride => typeof(AgentMemoryButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ExpandedProperty && ControlAutomationPeer.FromElement(this) is ButtonPeer peer)
        {
            peer.RaiseExpanded(change.GetOldValue<bool?>() == true, change.GetNewValue<bool?>() == true);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonPeer(this);

    /// <summary>A button; the source of a memory is a link, and the search toggle expands and collapses.</summary>
    private sealed class ButtonPeer(AgentMemoryButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private AgentMemoryButton Button => (AgentMemoryButton)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Button.Expanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Button.Expanded == false)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
        }

        public void RaiseExpanded(bool was, bool now) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                was ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                now ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            Button.Kind is AgentMemoryButtonKind.Source ? AutomationControlType.Hyperlink : AutomationControlType.Button;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IExpandCollapseProvider) && Button.Expanded is null ? null : base.GetProviderCore(providerType);
    }
}

/// <summary>
/// The borderless one-line text box of the memory card (the search input and the add field): text on a transparent
/// background with a muted placeholder; the frame around it shows the focus (a border or a tint), not the box itself.
/// </summary>
[TemplatePart(TextPresenterPart, typeof(TextPresenter))]
internal sealed class AgentMemoryInput : AvaloniaTextBox
{
    public const string TextPresenterPart = "PART_TextPresenter";

    /// <summary>The presenter, whose pre-edit text tells whether an input method is composing.</summary>
    public TextPresenter? Presenter { get; private set; }

    /// <summary>True while an input method is composing text (Enter and Escape belong to it then).</summary>
    public bool IsComposing => !string.IsNullOrEmpty(Presenter?.PreeditText);

    protected override Type StyleKeyOverride => typeof(AgentMemoryInput);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Presenter = e.NameScope.Find<TextPresenter>(TextPresenterPart);
    }
}

/// <summary>
/// One group of the list: its heading ("Preferences 3") above the rows and, while it has none, its own empty sentence.
/// The heading shows in the All view and in search results; a single-group view shows the rows alone. Heading and empty
/// sentence come and go with the same presence as the rows; the owner fills <see cref="Rows"/>.
/// </summary>
[TemplatePart(HeadingHostPart, typeof(AgentMemoryPresence))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[TemplatePart(EmptyHostPart, typeof(AgentMemoryPresence))]
[PseudoClasses(FirstPseudoClass)]
internal sealed class AgentMemoryGroupView : TemplatedControl
{
    public const string HeadingHostPart = "PART_HeadingHost";
    public const string RowsPart = "PART_Rows";
    public const string EmptyHostPart = "PART_EmptyHost";

    /// <summary>The first group on screen: its heading sits closer to the top (8 instead of 14).</summary>
    public const string FirstPseudoClass = ":first";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AgentMemoryGroupView, string?>(nameof(Label));

    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<AgentMemoryGroupView, string?>(nameof(CountText));

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<AgentMemoryGroupView, string?>(nameof(EmptyText));

    private readonly StackPanel _detachedRows = new();
    private AgentMemoryPresence? _headingHost;
    private AgentMemoryPresence? _emptyHost;
    private StackPanel? _rows;
    private bool _headingShown = true;
    private bool _emptyShown;

    public AgentMemoryGroupView(string key)
    {
        Key = key;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
    }

    static AgentMemoryGroupView()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemoryGroupView>(false);
    }

    /// <summary>The key of the group shown.</summary>
    public string Key { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <summary>The rows' panel (Arc's <c>ul</c>); usable before the template is applied.</summary>
    public StackPanel Rows => _rows ?? _detachedRows;

    /// <summary>The presence that wraps this group in the list (set by the owner).</summary>
    public AgentMemoryPresence? Host { get; set; }

    internal bool IsHeadingShown => _headingShown;

    internal bool IsEmptyShown => _emptyShown;

    internal AgentMemoryPresence? HeadingHost => _headingHost;

    internal AgentMemoryPresence? EmptyHost => _emptyHost;

    /// <summary>The row of <paramref name="memory"/> in this group, shown or leaving.</summary>
    public AgentMemoryRow? RowOf(MemoryItem memory)
    {
        foreach (var child in Rows.Children)
        {
            if (child is AgentMemoryRow row && ReferenceEquals(row.Memory, memory))
            {
                return row;
            }
        }
        return null;
    }

    public void SetFirst(bool first) => PseudoClasses.Set(FirstPseudoClass, first);

    public void SetHeading(bool shown, bool animate)
    {
        _headingShown = shown;
        Apply(_headingHost, shown, animate);
    }

    public void SetEmpty(bool shown, bool animate)
    {
        _emptyShown = shown;
        Apply(_emptyHost, shown, animate);
    }

    private static void Apply(AgentMemoryPresence? host, bool shown, bool animate)
    {
        if (host is null)
        {
            return;
        }
        if (!animate)
        {
            host.Set(shown);
        }
        else if (shown)
        {
            host.Show(animate: true);
        }
        else
        {
            host.Hide(animate: true);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var rows = e.NameScope.Find<StackPanel>(RowsPart);
        var previous = _rows ?? _detachedRows;
        if (rows is not null && !ReferenceEquals(rows, previous))
        {
            // Rows added before the template existed move into the template's panel.
            var moving = previous.Children.ToArray();
            previous.Children.Clear();
            rows.Children.AddRange(moving);
            AutomationProperties.SetControlTypeOverride(rows, AutomationControlType.List);
        }
        _rows = rows;
        _headingHost = e.NameScope.Find<AgentMemoryPresence>(HeadingHostPart);
        _emptyHost = e.NameScope.Find<AgentMemoryPresence>(EmptyHostPart);
        _headingHost?.Set(_headingShown);
        _emptyHost?.Set(_emptyShown);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }
}

/// <summary>
/// The reading state of a memory row: the sentence (with the search marks), the source line ("From a chat · Sep 8") and
/// the Edit and Forget buttons. Source line and buttons are invisible at rest but always take their space (the text never
/// moves); hover, keyboard focus inside the row and touch devices (<see cref="IsRevealed"/>) bring them in with a soft
/// tint behind the row. A memory about a person starts with a 22px avatar.
/// </summary>
[TemplatePart(TextPart, typeof(HighlightedText))]
[TemplatePart(EditPart, typeof(AgentMemoryButton))]
[TemplatePart(ForgetPart, typeof(AgentMemoryButton))]
[TemplatePart(SourcePart, typeof(AgentMemoryButton))]
[PseudoClasses(HoverPseudoClass, RevealPseudoClass)]
internal sealed class AgentMemoryRowView : TemplatedControl
{
    public const string TextPart = "PART_Text";
    public const string EditPart = "PART_Edit";
    public const string ForgetPart = "PART_Forget";
    public const string SourcePart = "PART_Source";

    /// <summary>Mouse or pen over the row.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The source line and the buttons show at rest (touch: there is no hover to reveal them).</summary>
    public const string RevealPseudoClass = ":reveal";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(Text));

    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(Query));

    public static readonly StyledProperty<string?> PersonNameProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(PersonName));

    public static readonly StyledProperty<IImage?> PersonImageProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, IImage?>(nameof(PersonImage));

    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(SourceText));

    public static readonly StyledProperty<string?> DateTextProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(DateText));

    public static readonly StyledProperty<bool> IsSourceLinkProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, bool>(nameof(IsSourceLink));

    public static readonly StyledProperty<string?> EditTipProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(EditTip));

    public static readonly StyledProperty<string?> ForgetTipProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(ForgetTip));

    public static readonly StyledProperty<string?> EditNameProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(EditName));

    public static readonly StyledProperty<string?> ForgetNameProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, string?>(nameof(ForgetName));

    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<AgentMemoryRowView, bool>(nameof(IsRevealed));

    private AgentMemoryButton? _edit;
    private AgentMemoryButton? _forget;
    private AgentMemoryButton? _source;
    private HighlightedText? _text;

    public AgentMemoryRowView(MemoryItem memory)
    {
        Memory = memory;
    }

    static AgentMemoryRowView()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemoryRowView>(false);
    }

    public MemoryItem Memory { get; }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public string? PersonName { get => GetValue(PersonNameProperty); set => SetValue(PersonNameProperty, value); }
    public IImage? PersonImage { get => GetValue(PersonImageProperty); set => SetValue(PersonImageProperty, value); }
    public string? SourceText { get => GetValue(SourceTextProperty); set => SetValue(SourceTextProperty, value); }
    public string? DateText { get => GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }
    public bool IsSourceLink { get => GetValue(IsSourceLinkProperty); set => SetValue(IsSourceLinkProperty, value); }
    public string? EditTip { get => GetValue(EditTipProperty); set => SetValue(EditTipProperty, value); }
    public string? ForgetTip { get => GetValue(ForgetTipProperty); set => SetValue(ForgetTipProperty, value); }
    public string? EditName { get => GetValue(EditNameProperty); set => SetValue(EditNameProperty, value); }
    public string? ForgetName { get => GetValue(ForgetNameProperty); set => SetValue(ForgetNameProperty, value); }
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <summary>The Edit button (template part).</summary>
    public AgentMemoryButton? EditButton => _edit;

    /// <summary>The Forget button (template part).</summary>
    public AgentMemoryButton? ForgetButton => _forget;

    /// <summary>The source link (template part; shown only with a link).</summary>
    public AgentMemoryButton? SourceButton => _source;

    /// <summary>The sentence (template part).</summary>
    public HighlightedText? TextBlock => _text;

    /// <summary>Edit was pressed.</summary>
    public event EventHandler<RoutedEventArgs>? EditRequested;

    /// <summary>Forget was pressed.</summary>
    public event EventHandler<RoutedEventArgs>? ForgetRequested;

    /// <summary>The source link was pressed.</summary>
    public event EventHandler<RoutedEventArgs>? SourceRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_edit is not null)
        {
            _edit.Click -= OnEditClick;
            _edit.GotFocus -= OnButtonGotFocus;
            _edit.LostFocus -= OnButtonLostFocus;
        }
        if (_forget is not null)
        {
            _forget.Click -= OnForgetClick;
            _forget.GotFocus -= OnButtonGotFocus;
            _forget.LostFocus -= OnButtonLostFocus;
        }
        if (_source is not null)
        {
            _source.Click -= OnSourceClick;
        }
        base.OnApplyTemplate(e);
        _edit = e.NameScope.Find<AgentMemoryButton>(EditPart);
        _forget = e.NameScope.Find<AgentMemoryButton>(ForgetPart);
        _source = e.NameScope.Find<AgentMemoryButton>(SourcePart);
        _text = e.NameScope.Find<HighlightedText>(TextPart);
        if (_edit is not null)
        {
            _edit.Click += OnEditClick;
            _edit.GotFocus += OnButtonGotFocus;
            _edit.LostFocus += OnButtonLostFocus;
        }
        if (_forget is not null)
        {
            _forget.Click += OnForgetClick;
            _forget.GotFocus += OnButtonGotFocus;
            _forget.LostFocus += OnButtonLostFocus;
        }
        if (_source is not null)
        {
            _source.Click += OnSourceClick;
        }
        UpdateNames();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EditNameProperty || change.Property == ForgetNameProperty || change.Property == EditTipProperty ||
            change.Property == ForgetTipProperty)
        {
            UpdateNames();
        }
        else if (change.Property == IsRevealedProperty)
        {
            PseudoClasses.Set(RevealPseudoClass, change.GetNewValue<bool>());
        }
    }

    private void UpdateNames()
    {
        if (_edit is not null)
        {
            AutomationProperties.SetName(_edit, EditName);
            Tooltip.SetTip(_edit, EditTip);
        }
        if (_forget is not null)
        {
            AutomationProperties.SetName(_forget, ForgetName);
            Tooltip.SetTip(_forget, ForgetTip);
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        EditRequested?.Invoke(this, e);
    }

    private void OnForgetClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ForgetRequested?.Invoke(this, e);
    }

    private void OnSourceClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SourceRequested?.Invoke(this, e);
    }

    /// <summary>Keyboard focus on a row button shows its tooltip at once (hover waits); Arc does the same.</summary>
    private static void OnButtonGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is Control button && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            Tooltip.SetIsOpen(button, true);
        }
    }

    private static void OnButtonLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control button)
        {
            Tooltip.SetIsOpen(button, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>The row reads as its sentence; the buttons inside keep their own names.</summary>
    private sealed class RowPeer(AgentMemoryRowView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((AgentMemoryRowView)Owner).Text;
    }
}

/// <summary>
/// The bar that replaces a forgotten memory for the undo window: "Forgot “…”" on one line (cut with an ellipsis; the
/// word in the secondary color, the quoted sentence muted) and the Undo button with its countdown. It reads as a group
/// named "Forgotten memory".
/// </summary>
[TemplatePart(UndoPart, typeof(UndoCountdownButton))]
internal sealed class AgentMemoryUndoBar : TemplatedControl
{
    public const string UndoPart = "PART_Undo";

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<AgentMemoryUndoBar, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> QuoteProperty =
        AvaloniaProperty.Register<AgentMemoryUndoBar, string?>(nameof(Quote));

    public static readonly StyledProperty<string?> UndoTextProperty =
        AvaloniaProperty.Register<AgentMemoryUndoBar, string?>(nameof(UndoText));

    private UndoCountdownButton? _undo;

    public AgentMemoryUndoBar(MemoryItem memory, UndoCountdownButton undo)
    {
        Memory = memory;
        // The button is made by the owner (it holds the countdown); the template only places it.
        _undo = undo;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
    }

    static AgentMemoryUndoBar()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemoryUndoBar>(false);
    }

    public MemoryItem Memory { get; }

    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    public string? Quote { get => GetValue(QuoteProperty); set => SetValue(QuoteProperty, value); }

    public string? UndoText { get => GetValue(UndoTextProperty); set => SetValue(UndoTextProperty, value); }

    /// <summary>The Undo button with the countdown.</summary>
    public UndoCountdownButton Undo => _undo!;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<ContentPresenter>("PART_UndoHost") is { } host)
        {
            host.Content = _undo;
        }
    }

    /// <summary>
    /// Splits a formatted "Forgot “sentence”" around the sentence: the words before the opening quote, and the quoted
    /// sentence with its quote marks. Without the sentence in it, the whole text is the prefix.
    /// </summary>
    internal static (string Prefix, string Quote) Split(string formatted, string sentence)
    {
        var at = string.IsNullOrEmpty(sentence) ? -1 : formatted.IndexOf(sentence, StringComparison.Ordinal);
        if (at < 0)
        {
            return (formatted, string.Empty);
        }
        var start = at;
        while (start > 0 && !char.IsWhiteSpace(formatted[start - 1]) && !char.IsLetterOrDigit(formatted[start - 1]))
        {
            start--;
        }
        return (formatted[..start], formatted[start..]);
    }
}

/// <summary>
/// A switch row of the memory card: the title (pressing it toggles too, like a <c>label for</c>) and a one-line
/// explanation under it that crossfades when it changes, with the switch at the end. The switch is named by the title
/// and described by the explanation. <see cref="Toggled"/> reports the person's toggles only.
/// </summary>
[TemplatePart(SwitchPart, typeof(Switch))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(ExplanationPart, typeof(ArcContentSwap))]
internal sealed class AgentMemorySettingRow : TemplatedControl
{
    public const string SwitchPart = "PART_Switch";
    public const string TitlePart = "PART_Title";
    public const string ExplanationPart = "PART_Explanation";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentMemorySettingRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> ExplanationProperty =
        AvaloniaProperty.Register<AgentMemorySettingRow, string?>(nameof(Explanation));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<AgentMemorySettingRow, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    private Switch? _switch;
    private TextBlock? _title;
    private ArcContentSwap? _explanation;
    private bool _pushing;

    static AgentMemorySettingRow()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemorySettingRow>(false);
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Explanation { get => GetValue(ExplanationProperty); set => SetValue(ExplanationProperty, value); }

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>The switch (template part).</summary>
    public Switch? Switch => _switch;

    /// <summary>The person toggled the switch (or pressed the title); the argument is the new state.</summary>
    public event EventHandler<bool>? Toggled;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_switch is not null)
        {
            _switch.IsCheckedChanged -= OnSwitchChanged;
        }
        if (_title is not null)
        {
            _title.PointerPressed -= OnTitlePressed;
        }
        base.OnApplyTemplate(e);
        _switch = e.NameScope.Find<Switch>(SwitchPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _explanation = e.NameScope.Find<ArcContentSwap>(ExplanationPart);
        if (_switch is not null)
        {
            PushChecked();
            _switch.IsCheckedChanged += OnSwitchChanged;
        }
        if (_title is not null)
        {
            _title.PointerPressed += OnTitlePressed;
        }
        if (_explanation is not null)
        {
            // A new host starts with the current text (its first content never animates).
            _explanation.Content = Explanation;
        }
        UpdateAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PushChecked();
        }
        else if (change.Property == ExplanationProperty)
        {
            if (_explanation is not null)
            {
                _explanation.Content = change.GetNewValue<string?>();
            }
            UpdateAutomation();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateAutomation();
        }
    }

    private void PushChecked()
    {
        if (_switch is null || _switch.IsChecked == IsChecked)
        {
            return;
        }
        _pushing = true;
        try
        {
            _switch.IsChecked = IsChecked;
        }
        finally
        {
            _pushing = false;
        }
    }

    private void OnSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (_pushing || _switch is null)
        {
            return;
        }
        var on = _switch.IsChecked == true;
        SetCurrentValue(IsCheckedProperty, on);
        Toggled?.Invoke(this, on);
    }

    private void OnTitlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_switch is null || !_switch.IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        _switch.IsChecked = _switch.IsChecked != true;
    }

    private void UpdateAutomation()
    {
        if (_switch is null)
        {
            return;
        }
        AutomationProperties.SetName(_switch, Title);
        AutomationProperties.SetHelpText(_switch, Explanation);
    }
}

/// <summary>Lucide glyphs the memory card needs that <see cref="ArcIcons"/> does not include.</summary>
internal static class AgentMemoryGlyphs
{
    /// <summary>Lucide <c>search-x</c>: the search without matches.</summary>
    public static Geometry SearchX { get; } =
        ArcIcons.Parse("M13.5 8.5l-5 5 M8.5 8.5l5 5 M3 11a8 8 0 1 0 16 0a8 8 0 1 0 -16 0z M21 21l-4.3-4.3");
}
