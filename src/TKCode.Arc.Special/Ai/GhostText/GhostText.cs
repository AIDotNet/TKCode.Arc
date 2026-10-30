using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Inline AI autocomplete for a text field (Arc Ghost text, design reference). When the person pauses, muted ghost text
/// continues their sentence right at the caret, streamed from your <see cref="Suggest"/> function. Tab accepts the whole
/// suggestion and it settles from ghost ink to text ink in place; Cmd+Right (Ctrl+Right, Option or Alt+Right too) accepts
/// one word at a time; Escape fades it away; typing the same characters as the ghost keeps it in place instead of asking
/// again. A small chip at the end shows the model's mark and name, a shimmering Thinking while it waits, a Tab key cap and
/// "to accept" when a suggestion is ready (a tap target on touch screens), Accepted with a check after Tab, and No suggestion
/// with Retry when your function throws.
/// <para>
/// The field is a real Avalonia <see cref="TextBox"/> (caret, selection, IME, undo, clipboard and spellcheck stay native);
/// the ghost is the muted tail of the same text layout, so text and ghost wrap identically at any width. A multi-line field
/// grows with its text from <see cref="Rows"/> to <see cref="MaxRows"/> lines on a 180ms tween and has a label row and a
/// footer row; a single-line field scrolls sideways and keeps the chip inline at its end. With no <see cref="Suggest"/> and
/// no text it is a plain field, or plays a <see cref="Simulation"/> you pass in that types, pauses, accepts and loops;
/// clicking it takes over.
/// </para>
/// </summary>
[TemplatePart(EditorPart, typeof(GhostTextEditor))]
[TemplatePart(HeightBoxPart, typeof(GhostTextSizeBox))]
[TemplatePart(LabelPart, typeof(ContentPresenter))]
[TemplatePart(FooterPart, typeof(ContentPresenter))]
[TemplatePart(HintsPart, typeof(Panel))]
[TemplatePart(WordKeyPart, typeof(GhostTextKey))]
[TemplatePart(EscapeKeyPart, typeof(GhostTextKey))]
[TemplatePart(ChipPart, typeof(GhostTextChip))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(SingleLinePseudoClass, PlainPseudoClass, FocusedPseudoClass, KeyboardFocusPseudoClass, NarrowPseudoClass,
    ReadyPseudoClass)]
public partial class GhostText : TemplatedControl
{
    internal const string EditorPart = "PART_Editor";
    internal const string HeightBoxPart = "PART_HeightBox";
    internal const string LabelPart = "PART_Label";
    internal const string FooterPart = "PART_Footer";
    internal const string HintsPart = "PART_Hints";
    internal const string WordKeyPart = "PART_WordKey";
    internal const string EscapeKeyPart = "PART_EscapeKey";
    internal const string ChipPart = "PART_Chip";
    internal const string WordHintPart = "PART_WordHint";
    internal const string DismissHintPart = "PART_DismissHint";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string SingleLinePseudoClass = ":single-line";
    internal const string PlainPseudoClass = ":plain";
    internal const string FocusedPseudoClass = ":field-focused";
    internal const string KeyboardFocusPseudoClass = ":keyboard-focus";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string ReadyPseudoClass = ":ready";

    /// <summary>The text (two-way). Programmatic changes run through the same logic as typing, so a parent can drive the field.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GhostText, string?>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Returns a continuation for the text before the caret: a string, a task of one, or a stream of chunks
    /// (<see cref="GhostTextResponse"/>); null skips without showing Thinking. Cancel your work when the token fires.
    /// </summary>
    public static readonly StyledProperty<GhostTextSuggest?> SuggestProperty =
        AvaloniaProperty.Register<GhostText, GhostTextSuggest?>(nameof(Suggest));

    /// <summary>The pause before <see cref="Suggest"/> is called (450ms). Every keystroke restarts it.</summary>
    public static readonly StyledProperty<TimeSpan> DelayProperty =
        AvaloniaProperty.Register<GhostText, TimeSpan>(nameof(Delay), TimeSpan.FromMilliseconds(450));

    /// <summary>Trimmed characters needed before suggestions start (3).</summary>
    public static readonly StyledProperty<int> MinLengthProperty =
        AvaloniaProperty.Register<GhostText, int>(nameof(MinLength), 3);

    /// <summary>
    /// A growing multi-line field with a label row and a footer row (true), or a single-line field with the chip inline at
    /// its end. Line breaks typed or pasted into a single-line field become spaces.
    /// </summary>
    public static readonly StyledProperty<bool> IsMultilineProperty =
        AvaloniaProperty.Register<GhostText, bool>(nameof(IsMultiline), true);

    /// <summary>A bordered field, or plain text that sits flush inside a card you already have.</summary>
    public static readonly StyledProperty<GhostTextVariant> VariantProperty =
        AvaloniaProperty.Register<GhostText, GhostTextVariant>(nameof(Variant));

    /// <summary>Minimum visible lines of a multi-line field (3).</summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<GhostText, int>(nameof(Rows), 3);

    /// <summary>Lines before a multi-line field stops growing and scrolls, keeping the caret in view (10).</summary>
    public static readonly StyledProperty<int> MaxRowsProperty =
        AvaloniaProperty.Register<GhostText, int>(nameof(MaxRows), 10);

    /// <summary>Hint shown while the field is empty.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<GhostText, string?>(nameof(Placeholder));

    /// <summary>A quiet row above the text, such as a field name with an icon; a string also names the field. Multi-line only.</summary>
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<GhostText, object?>(nameof(Label));

    /// <summary>Content at the start of the footer row, such as a character count, a note or a send button. Multi-line only.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<GhostText, object?>(nameof(Footer));

    /// <summary>The model behind the suggestions: its name shows in the idle chip and its mark at 12px in every chip state.</summary>
    public static readonly StyledProperty<AgentModel?> ModelProperty =
        AvaloniaProperty.Register<GhostText, AgentModel?>(nameof(Model));

    /// <summary>
    /// A simulated session to play while the field has no <see cref="Suggest"/> and no text (demos and prototypes): it
    /// types its script, lets the suggestions stream and accepts them, looping; clicking the field takes it over. Null
    /// leaves the field to the person.
    /// </summary>
    public static readonly StyledProperty<GhostTextSimulation?> SimulationProperty =
        AvaloniaProperty.Register<GhostText, GhostTextSimulation?>(nameof(Simulation));

    /// <summary>Shows the chip, the word and Escape key hints next to it on wide fields, and the keys description for screen readers.</summary>
    public static readonly StyledProperty<bool> IsHintVisibleProperty =
        AvaloniaProperty.Register<GhostText, bool>(nameof(IsHintVisible), true);

    /// <summary>A one-off message for screen readers, said through the field's own polite announcements.</summary>
    public static readonly StyledProperty<string?> NoticeProperty =
        AvaloniaProperty.Register<GhostText, string?>(nameof(Notice));

    /// <summary>Every word the field shows or says.</summary>
    public static readonly StyledProperty<GhostTextLabels> LabelsProperty =
        AvaloniaProperty.Register<GhostText, GhostTextLabels>(nameof(Labels), GhostTextLabels.Default);

    /// <summary>Where the suggestion is: idle, loading, ready or failed.</summary>
    public static readonly DirectProperty<GhostText, GhostTextStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<GhostText, GhostTextStatus>(nameof(Status), o => o.Status);

    /// <summary>The suggestion at the caret (what Tab would accept), or empty.</summary>
    public static readonly DirectProperty<GhostText, string> SuggestionProperty =
        AvaloniaProperty.RegisterDirect<GhostText, string>(nameof(Suggestion), o => o.Suggestion);

    /// <summary>True while a streamed suggestion is still arriving.</summary>
    public static readonly DirectProperty<GhostText, bool> IsStreamingProperty =
        AvaloniaProperty.RegisterDirect<GhostText, bool>(nameof(IsStreaming), o => o.IsStreaming);

    /// <summary>Raised with the text that was accepted: the whole suggestion or the next word. Bubbles.</summary>
    public static readonly RoutedEvent<GhostTextAcceptedEventArgs> AcceptedEvent =
        RoutedEvent.Register<GhostText, GhostTextAcceptedEventArgs>(nameof(Accepted), RoutingStrategies.Bubble);

    /// <summary>Raised with the suggestion the person dismissed with Escape. Bubbles.</summary>
    public static readonly RoutedEvent<GhostTextDismissedEventArgs> DismissedEvent =
        RoutedEvent.Register<GhostText, GhostTextDismissedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Status"/> changes. Bubbles.</summary>
    public static readonly RoutedEvent<GhostTextStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<GhostText, GhostTextStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    private GhostTextStatus _status;
    private string _suggestion = string.Empty;
    private bool _streaming;

    private GhostTextEditor? _editor;
    private GhostTextSizeBox? _heightBox;
    private ContentPresenter? _label;
    private ContentPresenter? _footer;
    private Panel? _hints;
    private GhostTextKey? _wordKey;
    private GhostTextKey? _escapeKey;
    private GhostTextChip? _chip;
    private TextBlock? _wordHint;
    private TextBlock? _dismissHint;
    private ExplainerAnnouncer? _announcer;

    private bool _syncingText;
    private bool _applying;
    private bool _touch;
    private bool _showingAccepted;
    private DispatcherTimer? _acceptedTimer;

    static GhostText()
    {
        FocusableProperty.OverrideDefaultValue<GhostText>(true);
        // Focus() lands on the field; Tab goes straight to it.
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<GhostText>(false);
    }

    /// <summary>Creates the field.</summary>
    public GhostText()
    {
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>A press on the field's padding, label or footer (not on a control of its own) puts the caret at the end.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || _editor is not { } editor || editor.IsFocused || !IsEffectivelyEnabled)
        {
            return;
        }
        editor.Focus(NavigationMethod.Pointer);
        editor.CaretToEnd();
        e.Handled = true;
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SuggestProperty"/>
    public GhostTextSuggest? Suggest { get => GetValue(SuggestProperty); set => SetValue(SuggestProperty, value); }

    /// <inheritdoc cref="DelayProperty"/>
    public TimeSpan Delay { get => GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <inheritdoc cref="MinLengthProperty"/>
    public int MinLength { get => GetValue(MinLengthProperty); set => SetValue(MinLengthProperty, value); }

    /// <inheritdoc cref="IsMultilineProperty"/>
    public bool IsMultiline { get => GetValue(IsMultilineProperty); set => SetValue(IsMultilineProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public GhostTextVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="RowsProperty"/>
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    /// <inheritdoc cref="MaxRowsProperty"/>
    public int MaxRows { get => GetValue(MaxRowsProperty); set => SetValue(MaxRowsProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AgentModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public GhostTextSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="IsHintVisibleProperty"/>
    public bool IsHintVisible { get => GetValue(IsHintVisibleProperty); set => SetValue(IsHintVisibleProperty, value); }

    /// <inheritdoc cref="NoticeProperty"/>
    public string? Notice { get => GetValue(NoticeProperty); set => SetValue(NoticeProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public GhostTextLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public GhostTextStatus Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <inheritdoc cref="SuggestionProperty"/>
    public string Suggestion
    {
        get => _suggestion;
        private set => SetAndRaise(SuggestionProperty, ref _suggestion, value);
    }

    /// <inheritdoc cref="IsStreamingProperty"/>
    public bool IsStreaming
    {
        get => _streaming;
        private set => SetAndRaise(IsStreamingProperty, ref _streaming, value);
    }

    /// <inheritdoc cref="AcceptedEvent"/>
    public event EventHandler<GhostTextAcceptedEventArgs>? Accepted
    {
        add => AddHandler(AcceptedEvent, value);
        remove => RemoveHandler(AcceptedEvent, value);
    }

    /// <inheritdoc cref="DismissedEvent"/>
    public event EventHandler<GhostTextDismissedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<GhostTextStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    internal GhostTextEditor? Editor => _editor;

    internal GhostTextChip? Chip => _chip;

    internal GhostTextSizeBox? HeightBox => _heightBox;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal GhostTextKey? WordKey => _wordKey;

    internal GhostTextKey? EscapeKey => _escapeKey;

    internal Panel? Hints => _hints;

    /// <summary>True while a suggestion shows at the caret (not while it fades out).</summary>
    internal bool HasSuggestion => _editor is { } editor && editor.Ghost.Length > 0 && !editor.IsGhostLeaving && Status == GhostTextStatus.Ready;

    // ----- Handle ----------------------------------------------------------------------------------------------------

    /// <summary>Moves keyboard focus into the field.</summary>
    public bool FocusField() => _editor?.Focus(NavigationMethod.Pointer) ?? Focus();

    /// <summary>Accepts the whole suggestion (what Tab does). Returns false when none shows.</summary>
    public bool AcceptAll() => AcceptAllCore();

    /// <summary>Accepts the next word of the suggestion (what Cmd/Ctrl+Right does). Returns false when none shows.</summary>
    public bool AcceptWord() => AcceptWordCore();

    /// <summary>Dismisses the suggestion (what Escape does). Returns false when none shows.</summary>
    public bool Dismiss() => DismissCore();

    /// <summary>Asks for a suggestion now, without waiting for a pause. Returns false when nothing was asked.</summary>
    public bool Request()
    {
        _debounce?.Stop();
        return StartRequest();
    }

    // ----- Template ------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_editor is not null)
        {
            _editor.Owner = null;
        }
        if (_chip is not null)
        {
            _chip.Click -= OnChipClick;
        }
        _editor = e.NameScope.Find<GhostTextEditor>(EditorPart);
        _heightBox = e.NameScope.Find<GhostTextSizeBox>(HeightBoxPart);
        _label = e.NameScope.Find<ContentPresenter>(LabelPart);
        _footer = e.NameScope.Find<ContentPresenter>(FooterPart);
        _hints = e.NameScope.Find<Panel>(HintsPart);
        _wordKey = e.NameScope.Find<GhostTextKey>(WordKeyPart);
        _escapeKey = e.NameScope.Find<GhostTextKey>(EscapeKeyPart);
        _chip = e.NameScope.Find<GhostTextChip>(ChipPart);
        _wordHint = e.NameScope.Find<TextBlock>(WordHintPart);
        _dismissHint = e.NameScope.Find<TextBlock>(DismissHintPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_chip is not null)
        {
            _chip.Click += OnChipClick;
        }
        if (_editor is not null)
        {
            _editor.Owner = this;
            _syncingText = true;
            try
            {
                _editor.Text = DisplayedText();
            }
            finally
            {
                _syncingText = false;
            }
            _editor.CaretToEnd();
            _editor.AddHandler(TextBox.PastingFromClipboardEvent, OnEditorPasting);
            _editor.PropertyChanged += OnEditorPropertyChanged;
        }
        UpdateMode();
        UpdatePseudoClasses();
        UpdateParts();
        UpdateChip(force: true);
        UpdateHints();
        UpdateSimulation();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this) && _editor is not null)
        {
            _editor.Focus(e.NavigationMethod);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            if (!_syncingText && _editor is not null && (_editor.Text ?? string.Empty) != (Text ?? string.Empty))
            {
                StopSimulation(takeOver: false);
                _editor.Text = IsMultiline ? Text ?? string.Empty : SingleLine(Text ?? string.Empty);
            }
        }
        else if (change.Property == IsMultilineProperty || change.Property == RowsProperty || change.Property == MaxRowsProperty)
        {
            UpdateMode();
            UpdatePseudoClasses();
        }
        else if (change.Property == VariantProperty)
        {
            UpdatePseudoClasses();
            UpdateMode();
        }
        else if (change.Property == LabelProperty || change.Property == FooterProperty || change.Property == PlaceholderProperty)
        {
            UpdateParts();
        }
        else if (change.Property == ModelProperty || change.Property == LabelsProperty)
        {
            UpdateParts();
            UpdateChip(force: true);
        }
        else if (change.Property == IsHintVisibleProperty)
        {
            UpdateParts();
            UpdateHints();
        }
        else if (change.Property == NoticeProperty)
        {
            if (!string.IsNullOrEmpty(Notice))
            {
                _announcer?.Announce(Notice);
            }
        }
        else if (change.Property == SuggestProperty)
        {
            CancelRequest();
            ClearSuggestion(fade: false);
            UpdateSimulation();
        }
        else if (change.Property == SimulationProperty)
        {
            UpdateSimulation();
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
            {
                _debounce?.Stop();
                CancelRequest();
                ClearSuggestion(fade: false);
            }
        }
        else if (change.Property == StatusProperty)
        {
            var old = change.GetOldValue<GhostTextStatus>();
            var now = change.GetNewValue<GhostTextStatus>();
            if (now is GhostTextStatus.Loading or GhostTextStatus.Ready or GhostTextStatus.Error)
            {
                HideAccepted();
            }
            PseudoClasses.Set(ReadyPseudoClass, now == GhostTextStatus.Ready);
            UpdateChip(force: false);
            UpdateHints();
            RaiseEvent(new GhostTextStatusChangedEventArgs(StatusChangedEvent, old, now));
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < GhostTextMotion.NarrowWidth;
        if (PseudoClasses.Contains(NarrowPseudoClass) != narrow)
        {
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            UpdateHints();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSimulation();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _debounce?.Stop();
        _acceptedTimer?.Stop();
        CancelRequest();
        _simulation?.Stop();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(SingleLinePseudoClass, !IsMultiline);
        PseudoClasses.Set(PlainPseudoClass, Variant == GhostTextVariant.Plain);
        PseudoClasses.Set(ReadyPseudoClass, Status == GhostTextStatus.Ready);
    }

    /// <summary>The minimum and maximum height of a multi-line field from its rows; a single-line field is one line.</summary>
    private void UpdateMode()
    {
        if (_editor is null)
        {
            return;
        }
        if (IsMultiline)
        {
            var line = double.IsNaN(_editor.LineHeight) || _editor.LineHeight <= 0 ? _editor.FontSize * 1.5 : _editor.LineHeight;
            var padding = _editor.Padding.Top + _editor.Padding.Bottom;
            var rows = Math.Max(1, Rows);
            var maxRows = Math.Max(rows, MaxRows);
            _editor.MinHeight = rows * line + padding;
            _editor.MaxHeight = maxRows * line + padding;
        }
        else
        {
            _editor.ClearValue(MinHeightProperty);
            _editor.ClearValue(MaxHeightProperty);
            var text = _editor.Text ?? string.Empty;
            if (text.Contains('\n') || text.Contains('\r'))
            {
                _editor.Text = SingleLine(text);
            }
        }
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.LineHeightProperty || e.Property == PaddingProperty || e.Property == TextBox.FontSizeProperty)
        {
            UpdateMode();
        }
    }

    /// <summary>A footer note that truncates with an ellipsis when the field is narrow.</summary>
    private static TextBlock Note(string note)
    {
        var text = new TextBlock { Text = note, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return text;
    }

    /// <summary>The label, footer, placeholder and accessible name and description of the field.</summary>
    private void UpdateParts()
    {
        if (_label is not null)
        {
            var label = Label;
            _label.Content = label;
            _label.IsVisible = label is not null && IsMultiline;
        }
        if (_footer is not null)
        {
            _footer.Content = Footer is string note ? Note(note) : Footer;
        }
        if (_editor is not null)
        {
            _editor.PlaceholderText = Placeholder;
            var name = AutomationProperties.GetName(this);
            if (string.IsNullOrEmpty(name))
            {
                name = Label as string;
            }
            AutomationProperties.SetName(_editor, name ?? string.Empty);
            AutomationProperties.SetHelpText(_editor, IsHintVisible ? Labels.KeysDescription : string.Empty);
        }
        if (_chip is not null)
        {
            _chip.IsVisible = IsHintVisible;
        }
    }

    /// <summary>The word and Escape hints show next to the chip on wide fields while a suggestion is ready.</summary>
    private void UpdateHints()
    {
        if (_hints is null)
        {
            return;
        }
        _hints.IsVisible = IsHintVisible && !PseudoClasses.Contains(NarrowPseudoClass) && Status == GhostTextStatus.Ready && !_touch;
        if (_wordKey is not null)
        {
            var modifier = OperatingSystem.IsMacOS() ? Labels.MacWordModifier : Labels.WordModifier;
            _wordKey.Text = modifier + " →";
        }
        if (_escapeKey is not null)
        {
            _escapeKey.Text = Labels.EscapeKey;
        }
        if (_wordHint is not null)
        {
            _wordHint.Text = Labels.WordHint;
        }
        if (_dismissHint is not null)
        {
            _dismissHint.Text = Labels.DismissHint;
        }
    }

    // ----- Editor callbacks ------------------------------------------------------------------------------------------

    /// <summary>Tab, Cmd/Ctrl/Alt+Right and Escape act on a showing suggestion; otherwise the TextBox gets them.</summary>
    internal bool HandleEditorKey(KeyEventArgs e)
    {
        if (!HasSuggestion)
        {
            return false;
        }
        var modifiers = e.KeyModifiers;
        if (e.Key == Key.Tab && modifiers == KeyModifiers.None)
        {
            return AcceptAllCore();
        }
        if (e.Key == Key.Escape && modifiers == KeyModifiers.None)
        {
            return DismissCore();
        }
        if (e.Key == Key.Right && (modifiers & (KeyModifiers.Meta | KeyModifiers.Control | KeyModifiers.Alt)) != 0 &&
            (modifiers & KeyModifiers.Shift) == 0)
        {
            return AcceptWordCore();
        }
        return false;
    }

    internal void OnEditorTextChanged(string oldText, string newText)
    {
        if (!_syncingText && (Text ?? string.Empty) != newText)
        {
            _syncingText = true;
            try
            {
                SetCurrentValue(TextProperty, newText);
            }
            finally
            {
                _syncingText = false;
            }
        }
        if (_applying || _editor is not { } editor)
        {
            return;
        }
        editor.EndMerges();
        if (!editor.InOperation && newText.StartsWith(oldText, StringComparison.Ordinal) && editor.CaretIndex == oldText.Length)
        {
            // A parent drove the field from the end: the caret follows, as when typing.
            _applying = true;
            try
            {
                editor.CaretToEnd();
            }
            finally
            {
                _applying = false;
            }
        }
        var ghost = editor.Ghost;
        var added = newText.Length - oldText.Length;
        if (HasSuggestion && added > 0 && newText.StartsWith(oldText, StringComparison.Ordinal) &&
            ghost.AsSpan().StartsWith(newText.AsSpan(oldText.Length), StringComparison.Ordinal))
        {
            // Typing through the suggestion consumes it in place, with no new request.
            editor.ConsumeGhost(added);
            Suggestion = editor.Ghost;
            if (editor.Ghost.Length == 0 && !IsStreaming)
            {
                ClearSuggestion(fade: false);
                ScheduleAfterOperation();
            }
            return;
        }
        // Anything else clears the ghost at once and asks again after the next pause.
        CancelRequest();
        ClearSuggestion(fade: false);
        if (Status == GhostTextStatus.Error)
        {
            Status = GhostTextStatus.Idle;
        }
        ScheduleAfterOperation();
    }

    /// <summary>Restarts the pause now, or once the TextBox has finished the keystroke (and moved the caret).</summary>
    private void ScheduleAfterOperation()
    {
        if (_editor?.InOperation == true)
        {
            _scheduleWhenDone = true;
            return;
        }
        ScheduleRequest();
    }

    private bool _scheduleWhenDone;

    internal void OnEditorSelectionChanged()
    {
        if (_applying || _editor is not { } editor || editor.InOperation)
        {
            return;
        }
        CheckCaret();
    }

    internal void OnEditorOperationEnded()
    {
        CheckCaret();
        if (_scheduleWhenDone)
        {
            _scheduleWhenDone = false;
            ScheduleRequest();
        }
    }

    /// <summary>Suggestions only continue from the end: a caret moved away (or a selection) clears the ghost and the request.</summary>
    private void CheckCaret()
    {
        if (_editor is not { } editor || editor.CaretAtEnd)
        {
            return;
        }
        _debounce?.Stop();
        if (Status is GhostTextStatus.Loading or GhostTextStatus.Ready)
        {
            CancelRequest();
            ClearSuggestion(fade: false);
        }
    }

    internal void OnEditorFocusChanged(bool focused, NavigationMethod method)
    {
        PseudoClasses.Set(FocusedPseudoClass, focused);
        PseudoClasses.Set(KeyboardFocusPseudoClass, focused && method is NavigationMethod.Tab or NavigationMethod.Directional);
        if (focused)
        {
            StopSimulation(takeOver: true);
            return;
        }
        if (_simulation?.IsRunning == true)
        {
            return;
        }
        // Leaving the field aborts the request and fades the ghost out; a failed request keeps Retry reachable.
        _debounce?.Stop();
        if (Status is GhostTextStatus.Loading or GhostTextStatus.Ready)
        {
            CancelRequest();
            ClearSuggestion(fade: true);
        }
    }

    internal void OnEditorPointer(PointerType type) => SetTouch(type == PointerType.Touch);

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SetTouch(e.Pointer.Type == PointerType.Touch);
        StopSimulation(takeOver: true);
    }

    private void SetTouch(bool touch)
    {
        if (_touch == touch)
        {
            return;
        }
        _touch = touch;
        UpdateChip(force: true);
        UpdateHints();
    }

    /// <summary>A single-line field reads the clipboard itself: Avalonia keeps only the first line, Arc turns breaks into spaces.</summary>
    private async void OnEditorPasting(object? sender, RoutedEventArgs e)
    {
        if (IsMultiline || _editor is not { } editor || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        e.Handled = true;
        string? text;
        try
        {
            text = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard);
        }
        catch (Exception)
        {
            text = null;
        }
        if (!string.IsNullOrEmpty(text))
        {
            editor.TypeText(SingleLine(text));
        }
    }

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (Status == GhostTextStatus.Error)
        {
            Request();
            _editor?.Focus(NavigationMethod.Pointer);
        }
        else if (HasSuggestion)
        {
            AcceptAllCore();
        }
        else
        {
            _editor?.Focus(NavigationMethod.Pointer);
        }
    }

    // ----- Accept and dismiss ----------------------------------------------------------------------------------------

    private bool AcceptAllCore()
    {
        if (!HasSuggestion || _editor is not { } editor)
        {
            return false;
        }
        var accepted = editor.Ghost;
        var start = (editor.Text ?? string.Empty).Length;
        _debounce?.Stop();
        CancelRequest();
        _applying = true;
        try
        {
            editor.ClearGhost();
            editor.CaretToEnd();
            editor.TypeText(accepted);
            editor.CaretToEnd();
        }
        finally
        {
            _applying = false;
        }
        editor.MergeInk(start, accepted.Length);
        Suggestion = string.Empty;
        Status = GhostTextStatus.Idle;
        ShowAccepted();
        _chip?.TabKey?.Press();
        _announcer?.Announce(Labels.AcceptedAnnouncement);
        RaiseEvent(new GhostTextAcceptedEventArgs(AcceptedEvent, accepted, GhostTextAcceptMode.All));
        return true;
    }

    private bool AcceptWordCore()
    {
        if (!HasSuggestion || _editor is not { } editor)
        {
            return false;
        }
        var ghost = editor.Ghost;
        var word = NextWord(ghost);
        if (word.Length == 0)
        {
            return false;
        }
        var start = (editor.Text ?? string.Empty).Length;
        _debounce?.Stop();
        _applying = true;
        try
        {
            editor.CaretToEnd();
            editor.TypeText(word);
            editor.CaretToEnd();
            editor.ConsumeGhost(word.Length);
        }
        finally
        {
            _applying = false;
        }
        editor.MergeInk(start, word.Length);
        _wordKey?.Press();
        var rest = editor.Ghost;
        if (rest.Trim().Length == 0 && !IsStreaming)
        {
            CancelRequest();
            editor.ClearGhost();
            Suggestion = string.Empty;
            Status = GhostTextStatus.Idle;
            ShowAccepted();
            _announcer?.Announce(Labels.AcceptedAnnouncement);
        }
        else
        {
            Suggestion = rest;
        }
        RaiseEvent(new GhostTextAcceptedEventArgs(AcceptedEvent, word, GhostTextAcceptMode.Word));
        return true;
    }

    private bool DismissCore()
    {
        if (!HasSuggestion || _editor is not { } editor)
        {
            return false;
        }
        var suggestion = editor.Ghost;
        CancelRequest();
        ClearSuggestion(fade: true);
        _escapeKey?.Press();
        _announcer?.Announce(Labels.DismissedAnnouncement);
        RaiseEvent(new GhostTextDismissedEventArgs(DismissedEvent, suggestion));
        return true;
    }

    /// <summary>Takes the suggestion away (fading it out for Escape and blur) and returns to idle.</summary>
    private void ClearSuggestion(bool fade)
    {
        if (_editor is { } editor)
        {
            if (fade)
            {
                editor.FadeOutGhost();
            }
            else
            {
                editor.ClearGhost();
            }
        }
        Suggestion = string.Empty;
        IsStreaming = false;
        if (Status == GhostTextStatus.Ready)
        {
            Status = GhostTextStatus.Idle;
        }
    }

    private void ShowAccepted()
    {
        _showingAccepted = true;
        UpdateChip(force: false);
        _acceptedTimer ??= new DispatcherTimer(GhostTextMotion.AcceptedHold, DispatcherPriority.Normal, (_, _) => HideAccepted());
        _acceptedTimer.Stop();
        _acceptedTimer.Start();
    }

    private void HideAccepted()
    {
        _acceptedTimer?.Stop();
        if (_showingAccepted)
        {
            _showingAccepted = false;
            UpdateChip(force: false);
        }
    }

    /// <summary>Ends the "Accepted" chip at once (tests).</summary>
    internal void EndAcceptedHold() => HideAccepted();

    // ----- Chip ------------------------------------------------------------------------------------------------------

    internal GhostTextChipState ChipState => Status switch
    {
        GhostTextStatus.Loading => GhostTextChipState.Thinking,
        GhostTextStatus.Ready => GhostTextChipState.Ready,
        GhostTextStatus.Error => GhostTextChipState.Error,
        _ => _showingAccepted ? GhostTextChipState.Accepted : GhostTextChipState.Idle,
    };

    private void UpdateChip(bool force)
    {
        if (_chip is null)
        {
            return;
        }
        var state = ChipState;
        if (!force && _chip.State == state)
        {
            return;
        }
        var labels = Labels;
        var model = Model;
        var name = string.IsNullOrEmpty(model?.Name) ? labels.DefaultModelName : model!.Name;
        const double font = 12;
        const double mark = 12;
        const double gap = 5;
        GhostTextKey? tab = null;
        Control content;
        string accessibleName;
        switch (state)
        {
            case GhostTextChipState.Thinking:
            {
                var shimmer = new TextShimmer { Text = labels.Thinking, IsActive = true, FontSize = font, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                ArcTokens.Bind(shimmer, TextShimmer.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
                ArcTokens.Bind(shimmer, TextShimmer.BaseForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
                content = GhostTextChip.Row(model?.Mark, model?.MarkBrush, mark, gap, shimmer);
                accessibleName = labels.Thinking;
                break;
            }
            case GhostTextChipState.Ready:
                if (_touch)
                {
                    content = GhostTextChip.Row(model?.Mark, model?.MarkBrush, mark, gap,
                        GhostTextChip.Text(labels.TapToAccept, DesignTokens.ArcDesignTokenKind.TextSecondary, font));
                }
                else
                {
                    tab = new GhostTextKey { Text = labels.TabKey };
                    content = GhostTextChip.Row(model?.Mark, model?.MarkBrush, mark, gap, tab,
                        GhostTextChip.Text(labels.ToAccept, DesignTokens.ArcDesignTokenKind.TextSecondary, font));
                }
                accessibleName = labels.AcceptSuggestion;
                break;
            case GhostTextChipState.Accepted:
                content = GhostTextChip.Row(model?.Mark, model?.MarkBrush, mark, gap,
                    GhostTextChip.Glyph(ArcIcons.Check, DesignTokens.ArcDesignTokenKind.Success, mark),
                    GhostTextChip.Text(labels.Accepted, DesignTokens.ArcDesignTokenKind.TextSecondary, font));
                accessibleName = labels.Accepted;
                break;
            case GhostTextChipState.Error:
                content = GhostTextChip.Row(model?.Mark, model?.MarkBrush, mark, gap,
                    GhostTextChip.Glyph(ArcIcons.CircleAlert, DesignTokens.ArcDesignTokenKind.Danger, mark),
                    GhostTextChip.Text(labels.NoSuggestion, DesignTokens.ArcDesignTokenKind.TextSecondary, font),
                    GhostTextChip.Text(labels.Retry, DesignTokens.ArcDesignTokenKind.Foreground, font, medium: true));
                accessibleName = labels.RetrySuggestion;
                break;
            default:
                content = GhostTextChip.Row(model?.Mark, model?.MarkBrush, mark, gap,
                    GhostTextChip.Text(name, DesignTokens.ArcDesignTokenKind.TextMuted, font));
                accessibleName = labels.ModelChip(name);
                break;
        }
        _chip.Show(state, content, tab);
        AutomationProperties.SetName(_chip, accessibleName);
    }

    // ----- Pure helpers ----------------------------------------------------------------------------------------------

    /// <summary>Line breaks become spaces (a single-line field).</summary>
    internal static string SingleLine(string text) => text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

    /// <summary>The next word of <paramref name="ghost"/>: its leading spaces, then everything up to the next space.</summary>
    internal static string NextWord(string ghost)
    {
        var i = 0;
        while (i < ghost.Length && char.IsWhiteSpace(ghost[i]))
        {
            i++;
        }
        while (i < ghost.Length && !char.IsWhiteSpace(ghost[i]))
        {
            i++;
        }
        return ghost[..i];
    }

    /// <summary>The text the field starts with: <see cref="Text"/>.</summary>
    private string DisplayedText() => IsMultiline ? Text ?? string.Empty : SingleLine(Text ?? string.Empty);
}
