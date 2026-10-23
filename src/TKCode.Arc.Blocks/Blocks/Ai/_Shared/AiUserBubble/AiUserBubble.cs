using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiUserBubble.CopyRequested"/>: the text to copy.</summary>
internal sealed class AiUserBubbleCopyEventArgs(RoutedEvent routedEvent, object source, string text) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The message's source text (Markdown subset, as typed).</summary>
    public string Text { get; } = text;
}

/// <summary>Arguments of <see cref="AiUserBubble.EditSubmitted"/>: the edited text.</summary>
internal sealed class AiUserBubbleEditEventArgs(RoutedEvent routedEvent, object source, string text) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The new text, trimmed; never empty and never the unchanged original.</summary>
    public string Text { get; } = text;
}

/// <summary>
/// The person's row of a chat turn (Arc 3.4): the bubble at the end of the row (padding 10 16, radius 20, SurfaceMuted,
/// text 16 / 24 with inline code chips, up to min(82%, 544), 88% in a narrow chat), the attached files listed above the
/// text, and Copy and Edit to its left. Copy and Edit show while the row is hovered with a mouse or pen, while the focus
/// is inside it, while the owner sets <see cref="IsRevealed"/>, and always with <see cref="AlwaysVisible"/> (touch).
/// </summary>
/// <remarks>
/// <para>
/// Copy raises <see cref="CopyRequested"/>; unless a handler marks it handled, the bubble writes the message's source
/// text to the clipboard itself. Either way the button shows a check for <see cref="CopiedDuration"/> (pressing again
/// restarts it).
/// </para>
/// <para>
/// Edit raises <see cref="EditRequested"/> and, unless it is handled, sets <see cref="IsEditing"/>: the bubble crossfades
/// into <see cref="AiMessageEditor"/> (out 100ms, in 160ms) while the row's height springs to the form's (anchored at the
/// bottom, so the form slides in from above), and the focus goes to the text area with the caret at the end. Sending a
/// changed text raises <see cref="EditSubmitted"/> with it; Cancel, Escape, or sending the text unchanged raise
/// <see cref="EditCancelled"/>. Both end the edit and give the focus back to Edit (with the keyboard ring when a key
/// ended it). <see cref="IsEditing"/> is two-way: the owner can open or close the editor itself, which raises no event.
/// </para>
/// <para>
/// Every word reaches the bubble through its properties (<see cref="CopyText"/>, <see cref="EditText"/>, …). The row and
/// the editor inside are made in code when shown: the outgoing one is still fading out in the crossfade, so they are
/// never reused.
/// </para>
/// </remarks>
[TemplatePart(SizerPart, typeof(AiUserBubbleSizer))]
[TemplatePart(SwapPart, typeof(ArcContentSwap))]
[PseudoClasses(EditingPseudoClass, RevealedPseudoClass, HoverPseudoClass, NarrowPseudoClass, CopiedPseudoClass)]
internal class AiUserBubble : TemplatedControl
{
    public const string SizerPart = "PART_Sizer";
    public const string SwapPart = "PART_Swap";

    /// <summary>Set while the editor shows.</summary>
    public const string EditingPseudoClass = ":editing";

    /// <summary>Set while Copy and Edit show.</summary>
    public const string RevealedPseudoClass = ":revealed";

    /// <summary>Set while a mouse or pen is over the row.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>Set while <see cref="IsNarrow"/>.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while Copy shows its check.</summary>
    public const string CopiedPseudoClass = ":copied";

    /// <summary>How long Copy shows its check (Arc 1.6 s).</summary>
    public static readonly TimeSpan CopiedDuration = TimeSpan.FromMilliseconds(1600);

    #region Properties

    /// <summary>The message shown; its text and attachments are followed as they change.</summary>
    public static readonly StyledProperty<AiChatUserMessage?> MessageProperty =
        AvaloniaProperty.Register<AiUserBubble, AiChatUserMessage?>(nameof(Message));

    /// <summary>Whether the editor replaces the bubble (two-way).</summary>
    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<AiUserBubble, bool>(nameof(IsEditing), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows Copy and Edit regardless of hover and focus (the owner's own reveal rule).</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<AiUserBubble, bool>(nameof(IsRevealed));

    /// <summary>Always shows Copy and Edit (touch screens, where nothing hovers).</summary>
    public static readonly StyledProperty<bool> AlwaysVisibleProperty =
        AvaloniaProperty.Register<AiUserBubble, bool>(nameof(AlwaysVisible));

    /// <summary>A narrow chat (Arc: card ≤ 480): the bubble and the editor take up to 88% of the row instead of min(82%, 544).</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<AiUserBubble, bool>(nameof(IsNarrow));

    /// <summary>The tooltip and accessible name of Copy ("Copy").</summary>
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<AiUserBubble, string?>(nameof(CopyText));

    /// <summary>The tooltip and accessible name of Copy while it shows the check ("Copied"); null keeps <see cref="CopyText"/>.</summary>
    public static readonly StyledProperty<string?> CopiedTextProperty =
        AvaloniaProperty.Register<AiUserBubble, string?>(nameof(CopiedText));

    /// <summary>The tooltip and accessible name of Edit ("Edit").</summary>
    public static readonly StyledProperty<string?> EditTextProperty =
        AvaloniaProperty.Register<AiUserBubble, string?>(nameof(EditText));

    /// <summary>The accessible name of the editor's text area ("Edit message").</summary>
    public static readonly StyledProperty<string?> EditMessageTextProperty =
        AvaloniaProperty.Register<AiUserBubble, string?>(nameof(EditMessageText));

    /// <summary>The editor's cancel button ("Cancel").</summary>
    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<AiUserBubble, string?>(nameof(CancelText));

    /// <summary>The editor's send button ("Send").</summary>
    public static readonly StyledProperty<string?> SendTextProperty =
        AvaloniaProperty.Register<AiUserBubble, string?>(nameof(SendText));

    /// <summary>The accessible name of the list of attached files ("Attachments").</summary>
    public static readonly StyledProperty<string?> AttachmentsTextProperty =
        AvaloniaProperty.Register<AiUserBubble, string?>(nameof(AttachmentsText));

    /// <summary>Whether Copy and Edit show now (read-only).</summary>
    public static readonly DirectProperty<AiUserBubble, bool> ShowsActionsProperty =
        AvaloniaProperty.RegisterDirect<AiUserBubble, bool>(nameof(ShowsActions), o => o.ShowsActions);

    /// <summary>Whether Copy shows its check (read-only).</summary>
    public static readonly DirectProperty<AiUserBubble, bool> IsCopiedProperty =
        AvaloniaProperty.RegisterDirect<AiUserBubble, bool>(nameof(IsCopied), o => o.IsCopied);

    /// <summary>Raised when Copy is pressed; handle it to copy yourself (otherwise the bubble copies the source text).</summary>
    public static readonly RoutedEvent<AiUserBubbleCopyEventArgs> CopyRequestedEvent =
        RoutedEvent.Register<AiUserBubble, AiUserBubbleCopyEventArgs>(nameof(CopyRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Edit is pressed; handle it to keep the editor closed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> EditRequestedEvent =
        RoutedEvent.Register<AiUserBubble, RoutedEventArgs>(nameof(EditRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after the person sent a changed text from the editor (the editor has closed).</summary>
    public static readonly RoutedEvent<AiUserBubbleEditEventArgs> EditSubmittedEvent =
        RoutedEvent.Register<AiUserBubble, AiUserBubbleEditEventArgs>(nameof(EditSubmitted), RoutingStrategies.Bubble);

    /// <summary>Raised after the person cancelled the edit, or sent the text unchanged (the editor has closed).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> EditCancelledEvent =
        RoutedEvent.Register<AiUserBubble, RoutedEventArgs>(nameof(EditCancelled), RoutingStrategies.Bubble);

    /// <inheritdoc cref="MessageProperty"/>
    public AiChatUserMessage? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="IsEditingProperty"/>
    public bool IsEditing { get => GetValue(IsEditingProperty); set => SetValue(IsEditingProperty, value); }

    /// <inheritdoc cref="IsRevealedProperty"/>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <inheritdoc cref="AlwaysVisibleProperty"/>
    public bool AlwaysVisible { get => GetValue(AlwaysVisibleProperty); set => SetValue(AlwaysVisibleProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    /// <inheritdoc cref="CopiedTextProperty"/>
    public string? CopiedText { get => GetValue(CopiedTextProperty); set => SetValue(CopiedTextProperty, value); }

    /// <inheritdoc cref="EditTextProperty"/>
    public string? EditText { get => GetValue(EditTextProperty); set => SetValue(EditTextProperty, value); }

    /// <inheritdoc cref="EditMessageTextProperty"/>
    public string? EditMessageText { get => GetValue(EditMessageTextProperty); set => SetValue(EditMessageTextProperty, value); }

    /// <inheritdoc cref="CancelTextProperty"/>
    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <inheritdoc cref="SendTextProperty"/>
    public string? SendText { get => GetValue(SendTextProperty); set => SetValue(SendTextProperty, value); }

    /// <inheritdoc cref="AttachmentsTextProperty"/>
    public string? AttachmentsText { get => GetValue(AttachmentsTextProperty); set => SetValue(AttachmentsTextProperty, value); }

    /// <inheritdoc cref="ShowsActionsProperty"/>
    public bool ShowsActions { get => _showsActions; private set => SetAndRaise(ShowsActionsProperty, ref _showsActions, value); }

    /// <inheritdoc cref="IsCopiedProperty"/>
    public bool IsCopied { get => _isCopied; private set => SetAndRaise(IsCopiedProperty, ref _isCopied, value); }

    /// <inheritdoc cref="CopyRequestedEvent"/>
    public event EventHandler<AiUserBubbleCopyEventArgs>? CopyRequested
    {
        add => AddHandler(CopyRequestedEvent, value);
        remove => RemoveHandler(CopyRequestedEvent, value);
    }

    /// <inheritdoc cref="EditRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? EditRequested
    {
        add => AddHandler(EditRequestedEvent, value);
        remove => RemoveHandler(EditRequestedEvent, value);
    }

    /// <inheritdoc cref="EditSubmittedEvent"/>
    public event EventHandler<AiUserBubbleEditEventArgs>? EditSubmitted
    {
        add => AddHandler(EditSubmittedEvent, value);
        remove => RemoveHandler(EditSubmittedEvent, value);
    }

    /// <inheritdoc cref="EditCancelledEvent"/>
    public event EventHandler<RoutedEventArgs>? EditCancelled
    {
        add => AddHandler(EditCancelledEvent, value);
        remove => RemoveHandler(EditCancelledEvent, value);
    }

    #endregion

    private bool _showsActions;
    private bool _isCopied;
    private bool _hover;
    private AiUserBubbleSizer? _sizer;
    private ArcContentSwap? _swap;
    private AiUserBubbleRow? _row;
    private AiMessageEditor? _editor;
    private IDisposable? _copyReset;
    private NavigationMethod? _returnFocus;

    static AiUserBubble()
    {
        FocusableProperty.OverrideDefaultValue<AiUserBubble>(false);
    }

    /// <summary>
    /// Schedules the end of the copied check: called with the delay and the action, returns what cancels it. Tests
    /// replace it to move time by hand; the default is a one-shot dispatcher timer.
    /// </summary>
    internal Func<TimeSpan, Action, IDisposable> ScheduleCopyReset { get; set; } =
        static (delay, action) => DispatcherTimer.RunOnce(action, delay);

    /// <summary>The row shown while not editing (null while editing or before the template).</summary>
    internal AiUserBubbleRow? Row => _row;

    /// <summary>The editor shown while editing.</summary>
    internal AiMessageEditor? Editor => _editor;

    internal AiUserBubbleSizer? Sizer => _sizer;

    internal ArcContentSwap? Swap => _swap;

    #region Template and state

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _sizer = e.NameScope.Find<AiUserBubbleSizer>(SizerPart);
        _swap = e.NameScope.Find<ArcContentSwap>(SwapPart);
        // The first content of a new host never animates.
        Show(IsEditing ? CreateEditor() : CreateRow(), arm: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEditingProperty)
        {
            var editing = change.GetNewValue<bool>();
            PseudoClasses.Set(EditingPseudoClass, editing);
            if (_swap is not null)
            {
                if (editing)
                {
                    Show(CreateEditor(), arm: true);
                }
                else
                {
                    var focusInside = _editor?.IsKeyboardFocusWithin == true;
                    var method = _returnFocus ?? (focusInside ? NavigationMethod.Unspecified : (NavigationMethod?)null);
                    _returnFocus = null;
                    var row = CreateRow();
                    Show(row, arm: true);
                    if (method is { } focus)
                    {
                        row.FocusEditButton(focus);
                    }
                }
            }
        }
        else if (change.Property == IsRevealedProperty || change.Property == AlwaysVisibleProperty ||
                 change.Property == IsKeyboardFocusWithinProperty)
        {
            UpdateReveal();
        }
        else if (change.Property == IsNarrowProperty)
        {
            var narrow = change.GetNewValue<bool>();
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            _row?.SetCurrentValue(AiUserBubbleRow.IsNarrowProperty, narrow);
            _editor?.SetCurrentValue(AiMessageEditor.IsNarrowProperty, narrow);
        }
        else if (change.Property == MessageProperty)
        {
            _row?.SetCurrentValue(AiUserBubbleRow.MessageProperty, change.GetNewValue<AiChatUserMessage?>());
        }
        else if (change.Property == CopyTextProperty || change.Property == CopiedTextProperty || change.Property == EditTextProperty ||
                 change.Property == AttachmentsTextProperty)
        {
            PushRowText();
        }
        else if (change.Property == EditMessageTextProperty || change.Property == CancelTextProperty || change.Property == SendTextProperty)
        {
            PushEditorText();
        }
        else if (change.Property == IsCopiedProperty)
        {
            PseudoClasses.Set(CopiedPseudoClass, change.GetNewValue<bool>());
            _row?.SetCurrentValue(AiUserBubbleRow.IsCopiedProperty, change.GetNewValue<bool>());
        }
        else if (change.Property == ShowsActionsProperty)
        {
            PseudoClasses.Set(RevealedPseudoClass, change.GetNewValue<bool>());
            _row?.SetCurrentValue(AiUserBubbleRow.IsRevealedProperty, change.GetNewValue<bool>());
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _copyReset?.Dispose();
        _copyReset = null;
        IsCopied = false;
        _hover = false;
        PseudoClasses.Set(HoverPseudoClass, false);
        UpdateReveal();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc reveals on (hover: hover) only: a touch does not hover (AlwaysVisible covers touch screens).
        _hover = e.Pointer.Type != PointerType.Touch;
        PseudoClasses.Set(HoverPseudoClass, _hover);
        UpdateReveal();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        PseudoClasses.Set(HoverPseudoClass, false);
        UpdateReveal();
    }

    private void UpdateReveal() => ShowsActions = IsRevealed || AlwaysVisible || _hover || IsKeyboardFocusWithin;

    private void Show(Control content, bool arm)
    {
        if (_swap is null)
        {
            return;
        }
        if (arm)
        {
            _sizer?.Arm();
        }
        _swap.Content = content;
    }

    #endregion

    #region Row

    private AiUserBubbleRow CreateRow()
    {
        DropRow();
        DropEditor();
        var row = new AiUserBubbleRow
        {
            Message = Message,
            IsNarrow = IsNarrow,
            IsRevealed = ShowsActions,
            IsCopied = IsCopied
        };
        row.CopyClicked += OnCopyClicked;
        row.EditClicked += OnEditClicked;
        _row = row;
        PushRowText();
        return row;
    }

    private void DropRow()
    {
        if (_row is not null)
        {
            _row.CopyClicked -= OnCopyClicked;
            _row.EditClicked -= OnEditClicked;
            _row = null;
        }
    }

    private void PushRowText()
    {
        if (_row is not { } row)
        {
            return;
        }
        row.CopyText = CopyText;
        row.CopiedText = CopiedText;
        row.EditText = EditText;
        row.AttachmentsText = AttachmentsText;
    }

    private void OnCopyClicked(object? sender, EventArgs e) => Copy();

    private void OnEditClicked(object? sender, EventArgs e) => RequestEdit();

    /// <summary>
    /// Copies the message (raises <see cref="CopyRequested"/>; copies the source text itself when nobody handles it) and
    /// shows the check for <see cref="CopiedDuration"/>, restarting it when already shown.
    /// </summary>
    internal void Copy()
    {
        var text = Message?.Text ?? string.Empty;
        var args = new AiUserBubbleCopyEventArgs(CopyRequestedEvent, this, text);
        RaiseEvent(args);
        if (!args.Handled)
        {
            _ = ArcClipboard.CopyTextAsync(this, text);
        }
        _copyReset?.Dispose();
        IsCopied = true;
        _copyReset = ScheduleCopyReset(CopiedDuration, () =>
        {
            _copyReset = null;
            IsCopied = false;
        });
    }

    /// <summary>Raises <see cref="EditRequested"/> and opens the editor unless a handler handled it. Returns whether it opened.</summary>
    internal bool RequestEdit()
    {
        if (IsEditing)
        {
            return false;
        }
        var args = new RoutedEventArgs(EditRequestedEvent, this);
        RaiseEvent(args);
        if (args.Handled)
        {
            return false;
        }
        SetCurrentValue(IsEditingProperty, true);
        return true;
    }

    #endregion

    #region Editor

    private AiMessageEditor CreateEditor()
    {
        DropEditor();
        DropRow();
        var text = Message?.Text ?? string.Empty;
        var editor = new AiMessageEditor
        {
            Text = text,
            OriginalText = text,
            IsNarrow = IsNarrow
        };
        editor.Ended += OnEditorEnded;
        _editor = editor;
        PushEditorText();
        editor.FocusTextArea();
        return editor;
    }

    private void DropEditor()
    {
        if (_editor is not null)
        {
            _editor.Ended -= OnEditorEnded;
            _editor = null;
        }
    }

    private void PushEditorText()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        editor.AccessibleName = EditMessageText;
        editor.CancelText = CancelText;
        editor.SendText = SendText;
    }

    private void OnEditorEnded(object? sender, AiMessageEditEndedEventArgs e)
    {
        if (!ReferenceEquals(sender, _editor))
        {
            return;
        }
        // The edit ends first, so handlers see the bubble back (and may set a new message for it).
        _returnFocus = e.ByKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
        SetCurrentValue(IsEditingProperty, false);
        _returnFocus = null;
        if (e.Outcome == AiMessageEditOutcome.Submitted)
        {
            RaiseEvent(new AiUserBubbleEditEventArgs(EditSubmittedEvent, this, e.Text));
        }
        else
        {
            RaiseEvent(new RoutedEventArgs(EditCancelledEvent, this));
        }
    }

    /// <summary>Opens the editor (no <see cref="EditRequested"/>); the focus goes to its text area.</summary>
    public void BeginEdit() => SetCurrentValue(IsEditingProperty, true);

    /// <summary>Closes the editor without raising an event, keeping the message as it is.</summary>
    public void CancelEdit() => SetCurrentValue(IsEditingProperty, false);

    #endregion
}
