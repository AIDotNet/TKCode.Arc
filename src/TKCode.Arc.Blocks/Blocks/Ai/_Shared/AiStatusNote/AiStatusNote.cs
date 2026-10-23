using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What an <see cref="AiStatusNote"/> reports.</summary>
internal enum AiStatusNoteTone
{
    /// <summary>The person stopped the reply: a small solid square and muted text.</summary>
    Stopped,

    /// <summary>The reply failed: a Danger alert glyph, a bold title and the reason.</summary>
    Error
}

/// <summary>How an <see cref="AiStatusNote"/> is laid out.</summary>
internal enum AiStatusNoteLayout
{
    /// <summary>One quiet line under the reply.</summary>
    Inline,

    /// <summary>A rounded box (Arc's error box) with the action at its right.</summary>
    Card
}

/// <summary>
/// The note at the end of an AI reply that did not finish (Arc chat, answer, side panel): "■ Stopped" as one muted line, or
/// the error box: radius 16, 8px around and 14px on the left, a fill of Danger at 5% over Surface and an edge of Danger at
/// 26% over Border, a 16px Danger CircleAlert, the bold <see cref="Title"/> ("Reply failed.") and the reason in secondary text
/// (14 / 20.3), and at the right the action (Arc's secondary small button, "Retry") raising <see cref="ActionRequested"/>.
/// <see cref="IsActive"/> fades it in and out (a <see cref="PresenceHost"/>: 240ms in, 160ms out; reduced motion 120ms; Off
/// at once); the first state shows as it is. Every word comes from the owner.
/// </summary>
[TemplatePart(TextPart, typeof(TextBlock))]
[TemplatePart(ActionPart, typeof(Button))]
[TemplatePart(PresencePart, typeof(PresenceHost))]
[PseudoClasses(StoppedPseudoClass, ErrorPseudoClass, InlinePseudoClass, CardPseudoClass, ActionPseudoClass)]
internal class AiStatusNote : TemplatedControl
{
    public const string TextPart = "PART_Text";
    public const string ActionPart = "PART_Action";
    public const string PresencePart = "PART_Presence";

    public const string StoppedPseudoClass = ":stopped";
    public const string ErrorPseudoClass = ":error";
    public const string InlinePseudoClass = ":inline";
    public const string CardPseudoClass = ":card";

    /// <summary>Set while the note has an action to show.</summary>
    public const string ActionPseudoClass = ":action";

    #region Properties

    /// <summary>Stopped or Error.</summary>
    public static readonly StyledProperty<AiStatusNoteTone> ToneProperty =
        AvaloniaProperty.Register<AiStatusNote, AiStatusNoteTone>(nameof(Tone));

    /// <summary>One line or a box.</summary>
    public static readonly StyledProperty<AiStatusNoteLayout> LayoutProperty =
        AvaloniaProperty.Register<AiStatusNote, AiStatusNoteLayout>(nameof(Layout));

    /// <summary>The bold start ("Reply failed."); empty leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiStatusNote, string?>(nameof(Title));

    /// <summary>The note itself ("Stopped", the reason a reply failed).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiStatusNote, string?>(nameof(Text));

    /// <summary>The action's label ("Retry"); empty hides the action.</summary>
    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<AiStatusNote, string?>(nameof(ActionText));

    /// <summary>Whether the note is shown; a change fades it in or out.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AiStatusNote, bool>(nameof(IsActive), true);

    /// <summary>Raised (bubbling) when the person presses the action.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActionRequestedEvent =
        RoutedEvent.Register<AiStatusNote, RoutedEventArgs>(nameof(ActionRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ToneProperty"/>
    public AiStatusNoteTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="LayoutProperty"/>
    public AiStatusNoteLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ActionTextProperty"/>
    public string? ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    /// <inheritdoc cref="ActionRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ActionRequested
    {
        add => AddHandler(ActionRequestedEvent, value);
        remove => RemoveHandler(ActionRequestedEvent, value);
    }

    #endregion

    private TextBlock? _text;
    private Button? _action;
    private readonly Run _titleRun = new();
    private readonly Run _gapRun = new(" ");
    private readonly Run _bodyRun = new();

    public AiStatusNote()
    {
        // The title is Foreground at weight 500 whatever the line's color (Arc's error box prefix).
        _titleRun.FontWeight = FontWeight.Medium;
        ArcTokens.Bind(_titleRun, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        UpdateClasses();
    }

    /// <summary>The note as read (title and text).</summary>
    internal string ReadText => string.IsNullOrEmpty(Title) ? Text ?? string.Empty
        : string.IsNullOrEmpty(Text) ? Title : Title + " " + Text;

    /// <summary>The action button.</summary>
    internal Button? ActionButton => _action;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        _text?.Inlines?.Clear();
        _text = e.NameScope.Find<TextBlock>(TextPart);
        _action = e.NameScope.Find<Button>(ActionPart);
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_text is not null)
        {
            _text.Inlines ??= new InlineCollection();
            _text.Inlines.Clear();
            _text.Inlines.Add(_titleRun);
            _text.Inlines.Add(_gapRun);
            _text.Inlines.Add(_bodyRun);
        }
        UpdateTexts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty || change.Property == LayoutProperty)
        {
            UpdateClasses();
        }
        else if (change.Property == TitleProperty || change.Property == TextProperty || change.Property == ActionTextProperty)
        {
            UpdateTexts();
        }
    }

    private void UpdateClasses()
    {
        PseudoClasses.Set(StoppedPseudoClass, Tone == AiStatusNoteTone.Stopped);
        PseudoClasses.Set(ErrorPseudoClass, Tone == AiStatusNoteTone.Error);
        PseudoClasses.Set(InlinePseudoClass, Layout == AiStatusNoteLayout.Inline);
        PseudoClasses.Set(CardPseudoClass, Layout == AiStatusNoteLayout.Card);
    }

    private void UpdateTexts()
    {
        var title = Title;
        var text = Text;
        var hasTitle = !string.IsNullOrEmpty(title);
        _titleRun.Text = hasTitle ? title : string.Empty;
        _gapRun.Text = hasTitle && !string.IsNullOrEmpty(text) ? " " : string.Empty;
        _bodyRun.Text = text ?? string.Empty;
        _text?.InvalidateMeasure();
        AutomationProperties.SetName(this, ReadText);
        var action = ActionText;
        PseudoClasses.Set(ActionPseudoClass, !string.IsNullOrEmpty(action));
        if (_action is not null)
        {
            _action.Content = action;
            AutomationProperties.SetName(_action, action ?? string.Empty);
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ActionRequestedEvent, this));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiStatusNoteAutomationPeer(this);

    /// <summary>A status text named by its title and text; the action button stays its own element.</summary>
    private sealed class AiStatusNoteAutomationPeer(AiStatusNote owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
