// Ported from Arc's free site-footer block (MIT, Copyright (c) 2026 Elia Kuratli): the Newsletter component of site-footer.tsx.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where the newsletter's signup stands.</summary>
internal enum SiteFooterNewsletterState
{
    /// <summary>Waiting for an address.</summary>
    Idle,

    /// <summary>The address is being sent: the button spins and the input is read only.</summary>
    Loading,

    /// <summary>Subscribed: the button says so and cannot be pressed again.</summary>
    Done
}

/// <summary>
/// The footer's email signup: validates in place, keeps its width while the button morphs, and confirms without a toast.
/// The input row carries the validation state with its border only; one line under it swaps between the hint, the error
/// and the confirmation (rising out of a blur).
/// </summary>
[TemplatePart(InputPart, typeof(SiteFooterInput))]
[TemplatePart(SubmitPart, typeof(Button))]
[TemplatePart(MessagePart, typeof(SiteSwap))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(InvalidPseudoClass, LoadingPseudoClass, DonePseudoClass)]
internal sealed class SiteFooterNewsletter : TemplatedControl
{
    public const string InputPart = "PART_Input";
    public const string SubmitPart = "PART_Submit";
    public const string MessagePart = "PART_Message";
    public const string AnnouncerPart = "PART_Announcer";
    public const string InvalidPseudoClass = ":invalid";
    public const string LoadingPseudoClass = ":loading";
    public const string DonePseudoClass = ":done";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string?>(nameof(Description));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string> EmailLabelProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(EmailLabel), "Email address");

    public static readonly StyledProperty<string> SubscribeLabelProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(SubscribeLabel), "Subscribe");

    public static readonly StyledProperty<string> SubscribedLabelProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(SubscribedLabel), "Subscribed");

    public static readonly StyledProperty<string> EmptyEmailMessageProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(EmptyEmailMessage), "Enter your email address");

    public static readonly StyledProperty<string> InvalidEmailMessageProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(InvalidEmailMessage), "That email doesn't look right");

    public static readonly StyledProperty<string> FailedMessageProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(FailedMessage), "We couldn't subscribe you. Try again in a moment");

    public static readonly StyledProperty<string> ConfirmFormatProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(ConfirmFormat), "Check {0} to confirm");

    public static readonly StyledProperty<Func<string, CancellationToken, Task>?> SubscribeHandlerProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, Func<string, CancellationToken, Task>?>(nameof(SubscribeHandler));

    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, string>(nameof(Email), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>True lifts the 400px width limit (narrow footers).</summary>
    public static readonly StyledProperty<bool> IsUnboundedProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, bool>(nameof(IsUnbounded));

    /// <summary>The widest the form gets (400) unless <see cref="IsUnbounded"/>.</summary>
    public static readonly StyledProperty<double> MaximumWidthProperty =
        AvaloniaProperty.Register<SiteFooterNewsletter, double>(nameof(MaximumWidth), 400);

    public static readonly DirectProperty<SiteFooterNewsletter, SiteFooterNewsletterState> StateProperty =
        AvaloniaProperty.RegisterDirect<SiteFooterNewsletter, SiteFooterNewsletterState>(nameof(State), o => o.State);

    public static readonly DirectProperty<SiteFooterNewsletter, string?> ErrorProperty =
        AvaloniaProperty.RegisterDirect<SiteFooterNewsletter, string?>(nameof(Error), o => o.Error);

    public static readonly DirectProperty<SiteFooterNewsletter, bool> HasTitleProperty =
        AvaloniaProperty.RegisterDirect<SiteFooterNewsletter, bool>(nameof(HasTitle), o => o.HasTitle);

    private SiteFooterNewsletterState _state;
    private string? _error;
    private bool _touched;
    private bool _hasTitle;
    private SiteFooterMessage? _message;
    private SiteFooterInput? _input;
    private Button? _submit;
    private SiteSwap? _swap;
    private SiteAnnouncer? _announcer;
    private CancellationTokenSource? _cts;
    private bool _messageReady;

    static SiteFooterNewsletter()
    {
        FocusableProperty.OverrideDefaultValue<SiteFooterNewsletter>(false);
        AffectsMeasure<SiteFooterNewsletter>(MaximumWidthProperty, IsUnboundedProperty);
    }

    /// <summary>
    /// A block of up to <see cref="MaximumWidth"/> at the start of its slot: Avalonia centers a stretched control that has a
    /// maximum width, CSS does not, so the width is taken here and the control aligns left.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = IsUnbounded ? availableSize.Width : Math.Min(availableSize.Width, MaximumWidth);
        var size = base.MeasureOverride(new Size(width, availableSize.Height));
        return new Size(double.IsInfinity(width) ? size.Width : width, size.Height);
    }

    public SiteFooterNewsletter()
    {
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Form);
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public string EmailLabel { get => GetValue(EmailLabelProperty); set => SetValue(EmailLabelProperty, value); }

    public string SubscribeLabel { get => GetValue(SubscribeLabelProperty); set => SetValue(SubscribeLabelProperty, value); }

    public string SubscribedLabel { get => GetValue(SubscribedLabelProperty); set => SetValue(SubscribedLabelProperty, value); }

    public string EmptyEmailMessage { get => GetValue(EmptyEmailMessageProperty); set => SetValue(EmptyEmailMessageProperty, value); }

    public string InvalidEmailMessage { get => GetValue(InvalidEmailMessageProperty); set => SetValue(InvalidEmailMessageProperty, value); }

    public string FailedMessage { get => GetValue(FailedMessageProperty); set => SetValue(FailedMessageProperty, value); }

    public string ConfirmFormat { get => GetValue(ConfirmFormatProperty); set => SetValue(ConfirmFormatProperty, value); }

    /// <summary>Called with a valid address; an exception means failure. Null simulates a 900 ms success.</summary>
    public Func<string, CancellationToken, Task>? SubscribeHandler { get => GetValue(SubscribeHandlerProperty); set => SetValue(SubscribeHandlerProperty, value); }

    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    public bool IsUnbounded { get => GetValue(IsUnboundedProperty); set => SetValue(IsUnboundedProperty, value); }

    public double MaximumWidth { get => GetValue(MaximumWidthProperty); set => SetValue(MaximumWidthProperty, value); }

    public SiteFooterNewsletterState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <summary>The validation or sending error, or null.</summary>
    public string? Error { get => _error; private set => SetAndRaise(ErrorProperty, ref _error, value); }

    public bool HasTitle { get => _hasTitle; private set => SetAndRaise(HasTitleProperty, ref _hasTitle, value); }

    /// <summary>The line under the input right now: confirmation, error or hint (null when there is none).</summary>
    public SiteFooterMessage? Message => _message;

    /// <summary>The input of the template (tests read it).</summary>
    public SiteFooterInput? Input => _input;

    /// <summary>The submit button of the template (tests read it).</summary>
    public Button? SubmitButton => _submit;

    /// <summary>The announcer (tests read it).</summary>
    public SiteAnnouncer? Announcer => _announcer;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.KeyDown -= OnInputKeyDown;
            _input.LostFocus -= OnInputLostFocus;
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        _input = e.NameScope.Find<SiteFooterInput>(InputPart);
        _submit = e.NameScope.Find<Button>(SubmitPart);
        _swap = e.NameScope.Find<SiteSwap>(MessagePart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        if (_input is not null)
        {
            _input.KeyDown += OnInputKeyDown;
            _input.LostFocus += OnInputLostFocus;
            AutomationProperties.SetName(_input, EmailLabel);
        }
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
        if (_swap is not null)
        {
            // The line under the input: Arc's message swap (in: y 4, blur 2, 240 ms enter; out: y -4, blur 2, 120 ms), fade only when reduced.
            _swap.EnterOffset = 4;
            _swap.ExitOffset = -4;
            _swap.ExitDuration = ArcMotionPresets.Instant;
            _swap.ReducedExitDuration = TimeSpan.Zero;
            _swap.HorizontalContentAlignment = HorizontalAlignment.Left;
            _swap.VerticalContentAlignment = VerticalAlignment.Top;
            _swap.ContentTemplate = new FuncDataTemplate<SiteFooterMessage>(
                (message, _) => message is null ? null : new SiteFooterLabel { Kind = SiteFooterLabelKind.Message, Tone = message.Tone, Text = message.Text });
        }
        // The message may have been computed before the template existed: hand it to the new swap.
        _messageReady = false;
        _message = null;
        UpdateMessage();
        _messageReady = true;
        UpdateSubmitContent();
        UpdateStatePseudoClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TitleProperty)
        {
            HasTitle = !string.IsNullOrEmpty(Title);
            AutomationProperties.SetName(this, Title ?? string.Empty);
        }
        else if (property == EmailProperty)
        {
            if (_touched && State == SiteFooterNewsletterState.Idle)
            {
                Error = Validate(Email);
            }
        }
        else if (property == ErrorProperty || property == StateProperty || property == DescriptionProperty
                 || property == ConfirmFormatProperty || property == EmptyEmailMessageProperty
                 || property == InvalidEmailMessageProperty)
        {
            if (property == EmptyEmailMessageProperty || property == InvalidEmailMessageProperty)
            {
                if (_touched && State == SiteFooterNewsletterState.Idle && Error is not null)
                {
                    Error = Validate(Email);
                }
            }
            UpdateMessage();
            if (property == StateProperty)
            {
                UpdateSubmitContent();
            }
            UpdateStatePseudoClasses();
        }
        else if (property == SubscribeLabelProperty || property == SubscribedLabelProperty)
        {
            UpdateSubmitContent();
        }
        else if (property == EmailLabelProperty && _input is not null)
        {
            AutomationProperties.SetName(_input, EmailLabel);
        }
    }

    private void UpdateStatePseudoClasses()
    {
        PseudoClasses.Set(InvalidPseudoClass, Error is not null);
        PseudoClasses.Set(LoadingPseudoClass, State == SiteFooterNewsletterState.Loading);
        PseudoClasses.Set(DonePseudoClass, State == SiteFooterNewsletterState.Done);
        if (_input is not null)
        {
            _input.IsReadOnly = State != SiteFooterNewsletterState.Idle;
            _input.IsDone = State == SiteFooterNewsletterState.Done;
        }
        if (_submit is not null)
        {
            _submit.IsLoading = State == SiteFooterNewsletterState.Loading;
        }
    }

    private void UpdateSubmitContent()
    {
        if (_submit is null)
        {
            return;
        }
        if (State == SiteFooterNewsletterState.Done)
        {
            // A fresh panel each time: the old one may still be fading out inside the button.
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new ArcGlyph { Data = ArcIcons.Check, Size = 15, StrokeThickness = 2.25, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = SubscribedLabel, VerticalAlignment = VerticalAlignment.Center });
            _submit.Content = panel;
        }
        else
        {
            _submit.Content = SubscribeLabel;
        }
    }

    private void UpdateMessage()
    {
        SiteFooterMessage? next;
        if (State == SiteFooterNewsletterState.Done)
        {
            next = new SiteFooterMessage(ConfirmFormat.Replace("{0}", Email.Trim(), StringComparison.Ordinal), SiteFooterMessageTone.Success);
        }
        else if (Error is not null)
        {
            next = new SiteFooterMessage(Error, SiteFooterMessageTone.Error);
        }
        else
        {
            next = string.IsNullOrEmpty(Description) ? null : new SiteFooterMessage(Description, SiteFooterMessageTone.Hint);
        }
        if (Equals(next, _message))
        {
            return;
        }
        _message = next;
        if (_swap is not null)
        {
            _swap.Content = next;
        }
        if (_messageReady && next is not null && _announcer is not null)
        {
            // Errors are alerts (assertive), the rest is polite (aria-live on the slot).
            _announcer.IsAssertive = next.Tone == SiteFooterMessageTone.Error;
            _announcer.Announce(next.Text);
        }
    }

    private string? Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? EmptyEmailMessage : SiteEmail.IsValid(value) ? null : InvalidEmailMessage;

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && _input is { IsComposing: false })
        {
            e.Handled = true;
            _ = SubmitAsync();
        }
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        // Typing starts validating only once the field was left with something in it.
        if (State == SiteFooterNewsletterState.Idle && !string.IsNullOrEmpty(Email))
        {
            _touched = true;
            Error = Validate(Email);
        }
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e) => _ = SubmitAsync();

    /// <summary>Sends the address in the input: validates, then loads, then confirms or fails. Ignored unless idle.</summary>
    public async Task SubmitAsync()
    {
        if (State != SiteFooterNewsletterState.Idle)
        {
            return;
        }
        _touched = true;
        var problem = Validate(Email);
        Error = problem;
        if (problem is not null)
        {
            return;
        }
        var address = Email.Trim();
        State = SiteFooterNewsletterState.Loading;
        _cts?.Cancel();
        _cts?.Dispose();
        var cts = _cts = new CancellationTokenSource();
        try
        {
            if (SubscribeHandler is { } handler)
            {
                await handler(address, cts.Token);
            }
            else
            {
                await Task.Delay(900, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Detached while sending: nothing to show.
            return;
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_cts, cts))
            {
                return;
            }
            State = SiteFooterNewsletterState.Idle;
            Error = FailedMessage;
            return;
        }
        if (!ReferenceEquals(_cts, cts))
        {
            return;
        }
        State = SiteFooterNewsletterState.Done;
        RaiseEvent(new SiteFooterSubscribedEventArgs(SiteFooter.SubscribedEvent, this, address));
    }

    /// <summary>Back to an empty, idle form.</summary>
    public void Reset()
    {
        _cts?.Cancel();
        _touched = false;
        Error = null;
        State = SiteFooterNewsletterState.Idle;
        Email = string.Empty;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        if (State == SiteFooterNewsletterState.Loading)
        {
            State = SiteFooterNewsletterState.Idle;
        }
    }
}
