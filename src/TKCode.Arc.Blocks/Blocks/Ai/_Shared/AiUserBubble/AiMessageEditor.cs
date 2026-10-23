using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>How an edit of a user message ended.</summary>
internal enum AiMessageEditOutcome
{
    /// <summary>The person sent a changed message.</summary>
    Submitted,

    /// <summary>The person cancelled, or sent the message unchanged.</summary>
    Cancelled
}

/// <summary>Reports how <see cref="AiMessageEditor"/> ended: the outcome, the text sent and whether the keyboard did it.</summary>
internal sealed class AiMessageEditEndedEventArgs(AiMessageEditOutcome outcome, string text, bool byKeyboard) : EventArgs
{
    public AiMessageEditOutcome Outcome { get; } = outcome;

    /// <summary>The text sent (trimmed), or the draft when cancelled.</summary>
    public string Text { get; } = text;

    /// <summary>True when a key ended it (Enter, Escape, or Enter / Space on a button): the focus that returns shows its ring.</summary>
    public bool ByKeyboard { get; } = byKeyboard;
}

/// <summary>
/// The form that replaces a user bubble while it is edited (Arc 3.5): as wide as the widest bubble (544, or 88% of a
/// narrow row) even for a short message, padding 10, 1px BorderStrong, radius 20 on Surface; the text area (16 / 24,
/// padding 2 6) shows the message's source Markdown and grows with it, and below it, 8px down and right-aligned 6px
/// apart, Cancel (ghost, small) and Send (primary, small).
/// </summary>
/// <remarks>
/// <para>
/// Keys: Enter sends, Shift+Enter adds a line, Escape cancels; none of them while an input method composes (the keys
/// belong to it then), and Escape only from the text area. The rules for sending: a draft that is empty or only white
/// space cannot be sent (Send is disabled and Enter does nothing); a draft that equals the original once both are
/// trimmed and their line endings normalized is not a change, so sending it ends the edit as a cancel (no new version);
/// anything else is sent trimmed.
/// </para>
/// <para>
/// The text area is the main package's <see cref="TokenTextEditor"/> with no triggers and no token styles, rather than a
/// plain TextBox: it already grows line by line on the chat's 24px rhythm, decides Enter against IME composition and
/// Shift, and is the text area the chat's own composer uses, so the edited text keeps the composer's metrics.
/// </para>
/// </remarks>
[TemplatePart(TextAreaPart, typeof(TokenTextEditor))]
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(SendPart, typeof(Button))]
[TemplatePart(FormPart, typeof(Border))]
internal sealed class AiMessageEditor : TemplatedControl
{
    public const string TextAreaPart = "PART_TextArea";
    public const string CancelPart = "PART_Cancel";
    public const string SendPart = "PART_Send";
    public const string FormPart = "PART_Form";

    /// <summary>The draft (two-way).</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiMessageEditor, string>(nameof(Text), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: (_, value) => value ?? string.Empty);

    /// <summary>The message as it was; sending the same text again ends the edit as a cancel.</summary>
    public static readonly StyledProperty<string> OriginalTextProperty =
        AvaloniaProperty.Register<AiMessageEditor, string>(nameof(OriginalText), string.Empty, coerce: (_, value) => value ?? string.Empty);

    /// <summary>A narrow chat: the form takes 88% of the row instead of min(82%, 544).</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<AiMessageEditor, bool>(nameof(IsNarrow));

    /// <summary>The label of the cancel button.</summary>
    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<AiMessageEditor, string?>(nameof(CancelText));

    /// <summary>The label of the send button.</summary>
    public static readonly StyledProperty<string?> SendTextProperty =
        AvaloniaProperty.Register<AiMessageEditor, string?>(nameof(SendText));

    /// <summary>The accessible name of the text area ("Edit message").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<AiMessageEditor, string?>(nameof(AccessibleName));

    /// <summary>True while the draft has something besides white space.</summary>
    public static readonly DirectProperty<AiMessageEditor, bool> CanSendProperty =
        AvaloniaProperty.RegisterDirect<AiMessageEditor, bool>(nameof(CanSend), o => o.CanSend);

    private TokenTextEditor? _area;
    private Button? _cancel;
    private Button? _send;
    private bool _canSend;
    private bool _pushing;
    private bool _focusPending;
    private bool _keyboard;
    private bool _ended;

    static AiMessageEditor()
    {
        FocusableProperty.OverrideDefaultValue<AiMessageEditor>(false);
    }

    public AiMessageEditor()
    {
        // Which device acted last decides whether the focus that returns to Edit shows its keyboard ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboard = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboard = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string OriginalText { get => GetValue(OriginalTextProperty); set => SetValue(OriginalTextProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    public string? SendText { get => GetValue(SendTextProperty); set => SetValue(SendTextProperty, value); }

    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    public bool CanSend { get => _canSend; private set => SetAndRaise(CanSendProperty, ref _canSend, value); }

    /// <summary>Raised once, when the edit ends (sent, cancelled, or sent unchanged, which counts as cancelled).</summary>
    public event EventHandler<AiMessageEditEndedEventArgs>? Ended;

    internal TokenTextEditor? TextArea => _area;

    internal Button? CancelButton => _cancel;

    internal Button? SendButton => _send;

    internal Control? Form { get; private set; }

    /// <summary>True when the draft is the original text (trimmed, line endings normalized).</summary>
    internal bool IsUnchanged => Normalize(Text) == Normalize(OriginalText);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_area is not null)
        {
            _area.TextChanged -= OnAreaTextChanged;
            _area.SubmitRequested -= OnAreaSubmitRequested;
            _area.RemoveHandler(KeyDownEvent, OnAreaKeyDown);
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        base.OnApplyTemplate(e);
        _area = e.NameScope.Find<TokenTextEditor>(TextAreaPart);
        _cancel = e.NameScope.Find<Button>(CancelPart);
        _send = e.NameScope.Find<Button>(SendPart);
        Form = e.NameScope.Find<Control>(FormPart);
        if (_area is not null)
        {
            PushText();
            _area.TextChanged += OnAreaTextChanged;
            _area.SubmitRequested += OnAreaSubmitRequested;
            // Tunnel: Escape is decided before the text box sees it.
            _area.AddHandler(KeyDownEvent, OnAreaKeyDown, RoutingStrategies.Tunnel);
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        UpdateCanSend();
        UpdateAutomation();
        TryFocus();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        TryFocus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            PushText();
            UpdateCanSend();
        }
        else if (change.Property == AccessibleNameProperty || change.Property == CancelTextProperty || change.Property == SendTextProperty)
        {
            UpdateAutomation();
        }
    }

    private void PushText()
    {
        if (_area is null || _area.Text == Text)
        {
            return;
        }
        _pushing = true;
        try
        {
            _area.Text = Text;
        }
        finally
        {
            _pushing = false;
        }
    }

    private void OnAreaTextChanged(object? sender, TokenTextChangedEventArgs e)
    {
        if (_pushing || _area is null)
        {
            return;
        }
        SetCurrentValue(TextProperty, _area.Text);
    }

    private void UpdateCanSend()
    {
        CanSend = !string.IsNullOrWhiteSpace(Text);
        if (_send is not null)
        {
            _send.IsEnabled = CanSend;
        }
    }

    private void UpdateAutomation()
    {
        if (_area is not null)
        {
            AutomationProperties.SetName(_area, AccessibleName);
        }
        if (_cancel is not null)
        {
            AutomationProperties.SetName(_cancel, CancelText);
        }
        if (_send is not null)
        {
            AutomationProperties.SetName(_send, SendText);
        }
    }

    private void OnAreaKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || _area is { IsComposing: true })
        {
            return;
        }
        e.Handled = true;
        Cancel(byKeyboard: true);
    }

    private void OnAreaSubmitRequested(object? sender, TokenTextSubmitRequestedEventArgs e) => Submit(byKeyboard: true);

    private void OnSendClick(object? sender, RoutedEventArgs e) => Submit(_keyboard);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel(_keyboard);

    /// <summary>
    /// Sends the draft: nothing when it is empty or white space; a cancel when it is unchanged; otherwise
    /// <see cref="Ended"/> with the trimmed text. Returns whether the edit ended.
    /// </summary>
    internal bool Submit(bool byKeyboard)
    {
        if (_ended || !CanSend)
        {
            return false;
        }
        if (IsUnchanged)
        {
            return Cancel(byKeyboard);
        }
        _ended = true;
        Ended?.Invoke(this, new AiMessageEditEndedEventArgs(AiMessageEditOutcome.Submitted, Text.Trim(), byKeyboard));
        return true;
    }

    /// <summary>Ends the edit without sending. Returns whether the edit ended (false when it already had).</summary>
    internal bool Cancel(bool byKeyboard)
    {
        if (_ended)
        {
            return false;
        }
        _ended = true;
        Ended?.Invoke(this, new AiMessageEditEndedEventArgs(AiMessageEditOutcome.Cancelled, Text, byKeyboard));
        return true;
    }

    /// <summary>Focuses the text area with the caret after the last character, as soon as it can take the focus.</summary>
    public void FocusTextArea()
    {
        _focusPending = true;
        TryFocus();
    }

    private void TryFocus()
    {
        if (!_focusPending || _area is null || !_area.IsAttachedToVisualTree() || !_area.IsEffectivelyVisible)
        {
            return;
        }
        // The area hands the focus to its text box, which exists only once its template is applied.
        _area.ApplyTemplate();
        if (!_area.Focus() || !_area.IsKeyboardFocusWithin || ReferenceEquals(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement(), _area))
        {
            return;
        }
        _focusPending = false;
        var end = _area.Text.Length;
        _area.Select(end, end);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
}
