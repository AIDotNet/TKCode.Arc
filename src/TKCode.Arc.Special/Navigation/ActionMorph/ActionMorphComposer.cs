using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The composer's field: a soft, rounded text box with a hairline edge that darkens on focus and turns red when invalid.</summary>
[PseudoClasses(InvalidPseudoClass, MultilinePseudoClass)]
internal sealed class ActionMorphField : AvaloniaTextBox
{
    internal const string InvalidPseudoClass = ":invalid";
    internal const string MultilinePseudoClass = ":multiline";

    /// <summary>Whether the last submit found the field empty or failed.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<ActionMorphField, bool>(nameof(IsInvalid));

    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    protected override Type StyleKeyOverride => typeof(ActionMorphField);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == AcceptsReturnProperty)
        {
            PseudoClasses.Set(MultilinePseudoClass, change.GetNewValue<bool>());
        }
    }
}

/// <summary>
/// The composer face of one action: a title row (back, title, close), the field, an optional radio group and a bottom
/// row with the hint or error and the submit button. It keeps its draft while the morph lives.
/// </summary>
[TemplatePart(HeaderPart, typeof(Control))]
[TemplatePart(BackPart, typeof(ActionMorphIconButton))]
[TemplatePart(ClosePart, typeof(ActionMorphIconButton))]
[TemplatePart(FieldHostPart, typeof(Control))]
[TemplatePart(FieldPart, typeof(ActionMorphField))]
[TemplatePart(ChoicesPart, typeof(ActionMorphChoices))]
[TemplatePart(MessagePart, typeof(Control))]
[TemplatePart(MessageTextPart, typeof(TextBlock))]
[TemplatePart(SubmitPart, typeof(ActionMorphSubmit))]
[PseudoClasses(BusyPseudoClass, HasChoicesPseudoClass)]
internal sealed class ActionMorphComposer : TemplatedControl, IActionMorphFace
{
    internal const string HeaderPart = "PART_Header";
    internal const string BackPart = "PART_Back";
    internal const string ClosePart = "PART_Close";
    internal const string FieldHostPart = "PART_FieldHost";
    internal const string FieldPart = "PART_Field";
    internal const string ChoicesPart = "PART_Choices";
    internal const string MessagePart = "PART_Message";
    internal const string MessageTextPart = "PART_MessageText";
    internal const string SubmitPart = "PART_Submit";
    internal const string BusyPseudoClass = ":busy";
    internal const string HasChoicesPseudoClass = ":has-choices";

    /// <summary>The title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ActionMorphComposer, string?>(nameof(Title));

    /// <summary>The back button's accessible name.</summary>
    public static readonly StyledProperty<string?> BackLabelProperty =
        AvaloniaProperty.Register<ActionMorphComposer, string?>(nameof(BackLabel));

    /// <summary>The close button's accessible name.</summary>
    public static readonly StyledProperty<string?> CloseLabelProperty =
        AvaloniaProperty.Register<ActionMorphComposer, string?>(nameof(CloseLabel));

    /// <summary>The field's placeholder.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ActionMorphComposer, string?>(nameof(Placeholder));

    /// <summary>Whether the field takes several lines.</summary>
    public static readonly StyledProperty<bool> IsMultilineProperty =
        AvaloniaProperty.Register<ActionMorphComposer, bool>(nameof(IsMultiline));

    /// <summary>The heading over the radio group.</summary>
    public static readonly StyledProperty<string?> ChoicesLabelProperty =
        AvaloniaProperty.Register<ActionMorphComposer, string?>(nameof(ChoicesLabel));

    /// <summary>The submit button's text.</summary>
    public static readonly StyledProperty<string?> SubmitLabelProperty =
        AvaloniaProperty.Register<ActionMorphComposer, string?>(nameof(SubmitLabel));

    /// <summary>The text beside the spinner while busy.</summary>
    public static readonly StyledProperty<string?> BusyLabelProperty =
        AvaloniaProperty.Register<ActionMorphComposer, string?>(nameof(BusyLabel));

    /// <summary>Whether the submit runs.</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<ActionMorphComposer, bool>(nameof(IsBusy));

    // Arc Action morph (design reference): the message rises 4 px and fades in on a 0.28 s spring and leaves in 100 ms;
    // an empty or failed submit kicks the field left (-260 px/s) on a stiff, nearly critical spring (≈ -3.3 px at 33 ms).
    private static readonly SpringSpec MessageSpring = SpringSpec.FromVisual(0.28, 0);
    private static readonly SpringSpec ShakeSpring = SpringSpec.FromPhysical(900, 57);
    private const double ShakeVelocity = -260;
    private static readonly TimeSpan MessageExit = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan HeaderFade = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan HeaderDelay = TimeSpan.FromMilliseconds(60);

    private readonly ActionMorph _owner;
    private readonly TranslateTransform _shake = new();
    private readonly TranslateTransform _messageLift = new();
    private Control? _header;
    private ActionMorphIconButton? _back;
    private ActionMorphIconButton? _close;
    private Control? _fieldHost;
    private ActionMorphField? _field;
    private ActionMorphChoices? _choices;
    private Control? _message;
    private TextBlock? _messageText;
    private ActionMorphSubmit? _submit;
    private MotionValue<double>? _headerOpacity;
    private MotionValue<double>? _shakeX;
    private MotionValue<double>? _messageOpacity;
    private MotionValue<double>? _messageY;
    private string _text = string.Empty;
    private string? _choice;
    private string? _messageWords;
    private int _messageToken;

    public ActionMorphComposer(ActionMorph owner, ActionMorphAction action)
    {
        _owner = owner;
        Action = action;
        Title = action.EffectiveTitle;
        Placeholder = action.Placeholder;
        IsMultiline = action.IsMultiline;
        ChoicesLabel = action.ChoicesLabel;
        SubmitLabel = action.EffectiveSubmitLabel;
        _choice = action.EffectiveDefaultChoice;
        PseudoClasses.Set(HasChoicesPseudoClass, action.Choices.Count > 0);
        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    public ActionMorphAction Action { get; }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? BackLabel { get => GetValue(BackLabelProperty); set => SetValue(BackLabelProperty, value); }

    public string? CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public bool IsMultiline { get => GetValue(IsMultilineProperty); set => SetValue(IsMultilineProperty, value); }

    public string? ChoicesLabel { get => GetValue(ChoicesLabelProperty); set => SetValue(ChoicesLabelProperty, value); }

    public string? SubmitLabel { get => GetValue(SubmitLabelProperty); set => SetValue(SubmitLabelProperty, value); }

    public string? BusyLabel { get => GetValue(BusyLabelProperty); set => SetValue(BusyLabelProperty, value); }

    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <summary>The draft text.</summary>
    public string Text => _field?.Text ?? _text;

    /// <summary>The picked option, or null without choices.</summary>
    public string? Choice => _choices?.Selected ?? _choice;

    /// <summary>The hint or error on show, or null.</summary>
    public string? Message => _messageWords;

    /// <summary>Whether the field is marked invalid.</summary>
    public bool IsInvalid => _field?.IsInvalid ?? false;

    /// <summary>Whether the draft should be cleared before the composer shows again (after a successful submit).</summary>
    public bool NeedsReset { get; set; }

    internal ActionMorphField? Field => _field;

    internal ActionMorphChoices? ChoicesGroup => _choices;

    internal ActionMorphSubmit? Submit => _submit;

    internal ActionMorphIconButton? BackButton => _back;

    internal ActionMorphIconButton? CloseButton => _close;

    internal Control? FieldHost => _fieldHost;

    /// <summary>Clears the draft: empty text, the default option, no message.</summary>
    public void ResetDraft()
    {
        NeedsReset = false;
        _text = string.Empty;
        _choice = Action.EffectiveDefaultChoice;
        if (_field is not null)
        {
            _field.Text = string.Empty;
        }
        _choices?.Select(_choice, animate: false);
        ClearMessage(animate: false);
    }

    /// <summary>Moves focus into the field.</summary>
    public void FocusField(NavigationMethod method)
    {
        if (_field is null)
        {
            return;
        }
        _field.Focus(method);
        _field.CaretIndex = _field.Text?.Length ?? 0;
    }

    /// <summary>Shows <paramref name="words"/> under the field in the danger color, marks the field invalid and shakes it.</summary>
    public void ShowError(string words)
    {
        _messageToken++;
        _messageWords = words;
        if (_messageText is not null)
        {
            _messageText.Text = words;
        }
        if (_field is not null)
        {
            _field.IsInvalid = true;
            AutomationProperties.SetHelpText(_field, words);
        }
        var level = ArcMotion.GetEffectiveLevel(_owner);
        _messageOpacity ??= CreateMessageOpacity();
        _messageY ??= CreateMessageY();
        if (level == ArcMotionLevel.Full)
        {
            _messageY.Jump(4);
            _messageOpacity.Jump(0);
            _ = _messageY.AnimateTo(0, MotionSpec.Spring(MessageSpring));
            _ = _messageOpacity.AnimateTo(1, MotionSpec.Spring(MessageSpring));
            _shakeX ??= CreateShake();
            _ = _shakeX.AnimateTo(0, MotionSpec.Spring(ShakeSpring), ShakeVelocity);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _messageY.Jump(0);
            _messageOpacity.Jump(0);
            _ = _messageOpacity.AnimateTo(1, MotionSpec.Tween(MessageExit, ActionMorphSurface.EaseOut));
        }
        else
        {
            _messageY.Jump(0);
            _messageOpacity.Jump(1);
        }
    }

    /// <summary>Takes the message away and the invalid mark off.</summary>
    public void ClearMessage(bool animate)
    {
        if (_field is not null)
        {
            _field.IsInvalid = false;
            AutomationProperties.SetHelpText(_field, null);
        }
        if (_messageWords is null)
        {
            return;
        }
        _messageWords = null;
        var token = ++_messageToken;
        _messageOpacity ??= CreateMessageOpacity();
        if (animate && IsLoaded && ArcMotion.GetEffectiveLevel(_owner) != ArcMotionLevel.Off)
        {
            _ = FadeMessage(token);
        }
        else
        {
            _messageOpacity.Jump(0);
            if (_messageText is not null)
            {
                _messageText.Text = null;
            }
        }
    }

    private async Task FadeMessage(int token)
    {
        var done = await _messageOpacity!.AnimateTo(0, MotionSpec.Tween(MessageExit, ActionMorphSurface.EaseOut));
        if (done && token == _messageToken && _messageText is not null)
        {
            _messageText.Text = null;
        }
    }

    void IActionMorphFace.OnEntering(bool animate)
    {
        _headerOpacity ??= CreateHeaderOpacity();
        if (animate)
        {
            _headerOpacity.Jump(0);
            _ = _headerOpacity.AnimateTo(1, MotionSpec.Tween(HeaderFade, ArcEasings.Enter, HeaderDelay));
        }
        else
        {
            _headerOpacity.Jump(1);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusyProperty)
        {
            var busy = change.GetNewValue<bool>();
            PseudoClasses.Set(BusyPseudoClass, busy);
            AutomationProperties.SetItemStatus(this, busy ? BusyLabel : null);
            if (_field is not null)
            {
                _field.IsReadOnly = busy;
            }
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
            if (_field is not null)
            {
                AutomationProperties.SetName(_field, Title);
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_field is not null)
        {
            _text = _field.Text ?? string.Empty;
            _field.TextChanged -= OnTextChanged;
        }
        if (_choices is not null)
        {
            _choice = _choices.Selected;
            _choices.SelectionChanged -= OnChoiceChanged;
        }
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        _header = e.NameScope.Find<Control>(HeaderPart);
        _back = e.NameScope.Find<ActionMorphIconButton>(BackPart);
        _close = e.NameScope.Find<ActionMorphIconButton>(ClosePart);
        _fieldHost = e.NameScope.Find<Control>(FieldHostPart);
        _field = e.NameScope.Find<ActionMorphField>(FieldPart);
        _choices = e.NameScope.Find<ActionMorphChoices>(ChoicesPart);
        _message = e.NameScope.Find<Control>(MessagePart);
        _messageText = e.NameScope.Find<TextBlock>(MessageTextPart);
        _submit = e.NameScope.Find<ActionMorphSubmit>(SubmitPart);
        if (_fieldHost is not null)
        {
            _fieldHost.RenderTransform = _shake;
        }
        if (_message is not null)
        {
            _message.RenderTransform = _messageLift;
            _message.Opacity = _messageOpacity?.Value ?? 0;
            AutomationProperties.SetLiveSetting(_message, AutomationLiveSetting.Polite);
        }
        if (_messageText is not null)
        {
            _messageText.Text = _messageWords;
        }
        if (_header is not null && _headerOpacity is not null)
        {
            _header.Opacity = _headerOpacity.Value;
        }
        if (_field is not null)
        {
            _field.Text = _text;
            _field.IsReadOnly = IsBusy;
            _field.IsInvalid = _messageWords is not null;
            AutomationProperties.SetName(_field, Title);
            _field.TextChanged += OnTextChanged;
        }
        if (_choices is not null)
        {
            _choices.SetChoices(Action.Choices, _choice);
            AutomationProperties.SetName(_choices, ChoicesLabel);
            _choices.SelectionChanged += OnChoiceChanged;
        }
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _text = _field?.Text ?? string.Empty;
        if (_messageWords is not null)
        {
            ClearMessage(animate: true);
        }
    }

    private void OnChoiceChanged(object? sender, EventArgs e) => _choice = _choices?.Selected;

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.Back();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.Collapse();
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.SubmitFrom(this);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || e.Source is not Visual source
            || _field is null || !(ReferenceEquals(source, _field) || _field.IsVisualAncestorOf(source)))
        {
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (IsMultiline ? command : e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _owner.SubmitFrom(this);
        }
    }

    private MotionValue<double> CreateHeaderOpacity()
    {
        var value = new MotionValue<double>(_owner, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_header is not null)
            {
                _header.Opacity = v;
            }
        };
        return value;
    }

    private MotionValue<double> CreateShake()
    {
        var value = new MotionValue<double>(_owner, 0, MotionVectors.Double);
        value.Changed += v => _shake.X = v;
        return value;
    }

    private MotionValue<double> CreateMessageOpacity()
    {
        var value = new MotionValue<double>(_owner, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_message is not null)
            {
                _message.Opacity = v;
            }
        };
        return value;
    }

    private MotionValue<double> CreateMessageY()
    {
        var value = new MotionValue<double>(_owner, 0, MotionVectors.Double);
        value.Changed += v => _messageLift.Y = v;
        return value;
    }
}
