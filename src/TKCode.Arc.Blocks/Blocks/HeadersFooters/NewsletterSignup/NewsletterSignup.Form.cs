// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): the form logic of newsletter-signup.tsx.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class NewsletterSignup
{
    private readonly record struct Problem(bool IsInvalid, string Text);

    private NewsletterForm? _form;
    private NewsletterIssueStack? _stack;
    private NewsletterReaders? _readers;
    private SiteAnnouncer? _announcer;
    private SiteAnnouncer? _alert;
    private NewsletterEmailBox? _wiredInput;
    private NewsletterSubmitButton? _wiredSubmit;
    private NewsletterMessageLine? _wiredMessage;
    private NewsletterPublication? _defaultPublication;
    private string _email = string.Empty;
    private bool _tried;
    private Problem? _problem;
    private int _run;
    private CancellationTokenSource? _cts;
    private bool _syncingText;
    private bool _refocus;

    /// <summary>
    /// Puts the block back to its start: the address and any error are cleared, the phase is Idle (the next issue leaves
    /// the stack again), a send in flight is abandoned and the focus returns to the field. This is what "Use a different
    /// email" does.
    /// </summary>
    public void Reset()
    {
        _run++;
        _cts?.Cancel();
        _cts = null;
        _email = string.Empty;
        _tried = false;
        _problem = null;
        _refocus = true;
        SetPhase(NewsletterPhase.Idle, null);
        SyncAll();
        FocusInputSoon();
    }

    /// <summary>The text of the email field (tests read it).</summary>
    internal string EmailText => _email;

    /// <summary>The error on screen, or null (tests read it).</summary>
    internal string? ProblemText => _problem?.Text;

    internal NewsletterForm? Form => _form;

    internal NewsletterIssueStack? Stack => _stack;

    internal NewsletterReaders? Readers => _readers;

    internal SiteAnnouncer? Announcer => _announcer;

    internal SiteAnnouncer? Alert => _alert;

    private NewsletterPublication? EffectivePublication => IsPublicationVisible ? Publication ?? (_defaultPublication ??= NewsletterSignupSamples.CreatePublication()) : null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unwire();
        if (_form is not null)
        {
            _form.PartsReady -= OnFormReady;
        }
        _form = e.NameScope.Find<NewsletterForm>(FormPart);
        _stack = e.NameScope.Find<NewsletterIssueStack>(StackPart);
        _readers = e.NameScope.Find<NewsletterReaders>(ReadersPart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        _alert = e.NameScope.Find<SiteAnnouncer>(AlertPart);
        if (_form is not null)
        {
            _form.PartsReady += OnFormReady;
            // The form's own template is applied now (not at its first measure) so the parts exist for the first sync.
            _form.ApplyTemplate();
        }
        SyncAll();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Abandon a send in flight: the token is cancelled and its result is ignored.
        _run++;
        _cts?.Cancel();
        _cts = null;
    }

    private void OnFormReady(object? sender, EventArgs e)
    {
        Unwire();
        if (_form is null)
        {
            return;
        }
        _wiredInput = _form.Input;
        _wiredSubmit = _form.Submit;
        _wiredMessage = _form.Message;
        if (_wiredInput is not null)
        {
            _wiredInput.TextChanged += OnInputTextChanged;
            _wiredInput.LostFocus += OnInputLostFocus;
            _wiredInput.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            TextInputOptions.SetContentType(_wiredInput, TextInputContentType.Email);
            TextInputOptions.SetReturnKeyType(_wiredInput, TextInputReturnKeyType.Send);
            TextInputOptions.SetAutoCapitalization(_wiredInput, false);
        }
        if (_wiredSubmit is not null)
        {
            _wiredSubmit.Click += OnSubmitClick;
        }
        if (_wiredMessage is not null)
        {
            _wiredMessage.LinkInvoked += OnPrivacyLink;
            _wiredMessage.DifferentEmailInvoked += OnDifferentEmail;
        }
        SyncForm();
    }

    private void Unwire()
    {
        if (_wiredInput is not null)
        {
            _wiredInput.TextChanged -= OnInputTextChanged;
            _wiredInput.LostFocus -= OnInputLostFocus;
            _wiredInput.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        }
        if (_wiredSubmit is not null)
        {
            _wiredSubmit.Click -= OnSubmitClick;
        }
        if (_wiredMessage is not null)
        {
            _wiredMessage.LinkInvoked -= OnPrivacyLink;
            _wiredMessage.DifferentEmailInvoked -= OnDifferentEmail;
        }
        _wiredInput = null;
        _wiredSubmit = null;
        _wiredMessage = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == VariantProperty)
        {
            PseudoClasses.Set(CardPseudoClass, Variant == NewsletterVariant.Card);
            RaisePropertyChanged(DisplayTitleProperty, null, DisplayTitle);
            RaisePropertyChanged(DisplayDescriptionProperty, null, DisplayDescription);
            UpdateTitleSize(Bounds.Width);
            SyncAll();
        }
        else if (property == TitleProperty)
        {
            RaisePropertyChanged(DisplayTitleProperty, null, DisplayTitle);
            UpdateName();
        }
        else if (property == DescriptionProperty)
        {
            RaisePropertyChanged(DisplayDescriptionProperty, null, DisplayDescription);
        }
        else if (property == IsPublicationVisibleProperty || property == IsReadersVisibleProperty || property == PublicationProperty)
        {
            SyncAll();
        }
        else if (property == PlaceholderProperty || property == ButtonLabelProperty || property == PrivacyNoteProperty ||
                 property == PrivacyLinkLabelProperty || property == IsPrivacyLinkVisibleProperty || property == EmailLabelProperty ||
                 property == RetryLabelProperty || property == SendingLabelProperty || property == DoneLabelProperty ||
                 property == EmptyMessageProperty || property == FormatMessageProperty || property == FailedMessageProperty ||
                 property == DoneMessageProperty || property == DifferentEmailLabelProperty)
        {
            SyncForm();
        }
        else if (property == ReaderCountProperty || property == RecipientFormatProperty || property == IssueFormatProperty ||
                 property == MinutesFormatProperty || property == IssueNameFormatProperty || property == ReadersWordProperty ||
                 property == IncludingYouProperty)
        {
            SyncAside();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var width = e.NewSize.Width;
        PseudoClasses.Set(CompactPseudoClass, width <= 860);
        PseudoClasses.Set(NarrowPseudoClass, width <= 520);
        PseudoClasses.Set(TinyPseudoClass, width <= 400);
        PseudoClasses.Set(MicroPseudoClass, width <= 360);
        UpdateTitleSize(width);
        SyncAside();
    }

    /// <summary>The heading's size: clamp(32, 17.6 + 0.032 × the block's width, 52); a card's heading is 28.</summary>
    private void UpdateTitleSize(double width)
    {
        var size = Variant == NewsletterVariant.Card ? 28 : Math.Clamp(17.6 + 0.032 * width, 32, 52);
        if (Math.Abs(size - _titleFontSize) < 0.001)
        {
            return;
        }
        var old = _titleFontSize;
        _titleFontSize = size;
        RaisePropertyChanged(TitleFontSizeProperty, old, size);
        RaisePropertyChanged(TitleLineHeightProperty, old * 1.1, size * 1.1);
        RaisePropertyChanged(TitleLetterSpacingProperty, old * -0.03, size * -0.03);
    }

    private void UpdateName()
    {
        AutomationProperties.SetName(this, DisplayTitle);
    }

    private void SyncAll()
    {
        var publication = EffectivePublication;
        PseudoClasses.Set(CardPseudoClass, Variant == NewsletterVariant.Card);
        PseudoClasses.Set(NoPublicationPseudoClass, publication is null);
        PseudoClasses.Set(NoReadersPseudoClass, !IsReadersVisible);
        PseudoClasses.Set(NoAsidePseudoClass, publication is null && !IsReadersVisible);
        UpdateName();
        SyncForm();
        SyncAside();
    }

    private void SyncAside()
    {
        var width = Bounds.Width;
        if (_stack is not null)
        {
            _stack.IsTray = Variant == NewsletterVariant.Card;
            _stack.IsNarrow = width <= 520;
            _stack.IsTiny = width <= 400;
            _stack.IssueFormat = IssueFormat;
            _stack.MinutesFormat = MinutesFormat;
            _stack.RecipientFormat = RecipientFormat;
            _stack.IssueNameFormat = IssueNameFormat;
            _stack.Publication = EffectivePublication;
            _stack.Recipient = _subscribedEmail;
            _stack.IsDelivered = _phase == NewsletterPhase.Done;
        }
        if (_form is not null)
        {
            _form.IsNarrow = width <= 520;
        }
        if (_readers is not null)
        {
            _readers.Count = ReaderCount;
            _readers.Faces = _readerFaces;
            _readers.ReadersWord = ReadersWord;
            _readers.IncludingYou = IncludingYou;
            _readers.IsTiny = width <= 400;
            _readers.IsMicro = width <= 360;
            _readers.IsDone = _phase == NewsletterPhase.Done;
        }
    }

    private NewsletterButtonKey ButtonKey => _phase == NewsletterPhase.Done
        ? NewsletterButtonKey.Done
        : _phase == NewsletterPhase.Sending
            ? NewsletterButtonKey.Sending
            : _problem is { IsInvalid: false } ? NewsletterButtonKey.Retry : NewsletterButtonKey.Idle;

    private void SyncForm()
    {
        if (_form is null)
        {
            return;
        }
        var done = _phase == NewsletterPhase.Done;
        if (_form.Input is { } input)
        {
            input.PlaceholderText = Placeholder;
            input.IsReadOnly = _phase != NewsletterPhase.Idle;
            input.IsInvalid = _problem is { IsInvalid: true };
            input.IsDone = done;
            AutomationProperties.SetName(input, EmailLabel);
            if (input.Text != _email)
            {
                _syncingText = true;
                input.Text = _email;
                _syncingText = false;
            }
        }
        if (_form.Pill is { } pill)
        {
            // Any problem, including a failed send, turns the border to the danger color.
            pill.IsInvalid = _problem is not null;
            pill.IsDone = done;
        }
        if (_form.Submit is { } submit)
        {
            submit.IdleLabel = ButtonLabel;
            submit.RetryLabel = RetryLabel;
            submit.SendingLabel = SendingLabel;
            submit.DoneLabel = DoneLabel;
            submit.State = ButtonKey;
        }
        if (_form.Message is { } message)
        {
            message.NoteText = PrivacyNote;
            message.LinkLabel = PrivacyLinkLabel;
            message.IsLinkVisible = IsPrivacyLinkVisible;
            message.DoneText = DoneMessage;
            message.DifferentEmailLabel = DifferentEmailLabel;
            message.EmptyMessage = EmptyMessage;
            message.FormatMessage = FormatMessage;
            message.FailedMessage = FailedMessage;
            message.Show(_problem is { } problem ? NewsletterMessageKind.Problem : done ? NewsletterMessageKind.Done : NewsletterMessageKind.Note, _problem?.Text);
            if (_form.Input is { } described)
            {
                AutomationProperties.SetHelpText(described, message.VisibleText);
            }
        }
    }

    private void SetPhase(NewsletterPhase phase, string? email)
    {
        var oldPhase = _phase;
        var oldEmail = _subscribedEmail;
        _phase = phase;
        _subscribedEmail = email;
        PseudoClasses.Set(SendingPseudoClass, phase == NewsletterPhase.Sending);
        PseudoClasses.Set(DonePseudoClass, phase == NewsletterPhase.Done);
        RaisePropertyChanged(PhaseProperty, oldPhase, phase);
        RaisePropertyChanged(SubscribedEmailProperty, oldEmail, email);
    }

    private Problem? Validate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Problem(true, EmptyMessage);
        }
        return SiteEmail.IsValid(text) ? null : new Problem(true, FormatMessage);
    }

    private void SetProblem(Problem? problem)
    {
        var changed = problem != _problem;
        _problem = problem;
        if (changed && problem is { } found)
        {
            // Arc's role="alert": errors interrupt.
            _alert?.Announce(found.Text);
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingText || sender is not NewsletterEmailBox input)
        {
            return;
        }
        _email = input.Text ?? string.Empty;
        if (_tried)
        {
            SetProblem(Validate(_email));
        }
        else if (_problem is not null)
        {
            SetProblem(null);
        }
        SyncForm();
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_email) && !_tried && _phase == NewsletterPhase.Idle)
        {
            _tried = true;
            SetProblem(Validate(_email));
            SyncForm();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && sender is NewsletterEmailBox { IsComposing: false })
        {
            e.Handled = true;
            _ = SubmitAsync();
        }
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SubmitAsync();
    }

    private void OnDifferentEmail(object? sender, EventArgs e) => Reset();

    private void OnPrivacyLink(object? sender, EventArgs e)
    {
        var args = new NewsletterNavigateEventArgs(NavigateEvent, this, PrivacyLinkHref);
        RaiseEvent(args);
        if (NavigateCommand is { } command && command.CanExecute(PrivacyLinkHref))
        {
            command.Execute(PrivacyLinkHref);
        }
        if (!args.Handled)
        {
            SiteLinks.TryLaunch(this, PrivacyLinkHref);
        }
    }

    /// <summary>
    /// What Enter and the button do: check the address, then send it. A press while sending or done is ignored.
    /// </summary>
    internal async Task SubmitAsync()
    {
        if (_phase != NewsletterPhase.Idle)
        {
            return;
        }
        _tried = true;
        var found = Validate(_email);
        SetProblem(found);
        if (found is not null)
        {
            SyncForm();
            _form?.Pill?.Shake();
            _form?.Input?.Focus();
            return;
        }
        var value = _email.Trim();
        var run = ++_run;
        var cts = _cts = new CancellationTokenSource();
        SetPhase(NewsletterPhase.Sending, null);
        SyncAll();
        try
        {
            if (SubscribeAsync is { } handler)
            {
                await handler(value, cts.Token);
            }
            else
            {
                await Task.Delay(1100, cts.Token);
            }
            if (run != _run)
            {
                return;
            }
            SetPhase(NewsletterPhase.Done, value);
            SyncAll();
            RaiseEvent(new NewsletterSubscribedEventArgs(SubscribedEvent, this, value));
            if (SubscribedCommand is { } command && command.CanExecute(value))
            {
                command.Execute(value);
            }
            _announcer?.Announce($"{DoneMessage}{string.Format(CultureInfo.CurrentCulture, SentToFormat, value)} {DifferentEmailLabel}");
            FocusDifferentSoon();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Reset or removed while sending: nothing to show.
        }
        catch (Exception) when (run == _run)
        {
            // Any other exception is a failed send: the address stays, the button offers another try and the pill shakes.
            SetPhase(NewsletterPhase.Idle, null);
            SetProblem(new Problem(false, FailedMessage));
            SyncAll();
            _form?.Pill?.Shake();
        }
    }

    private void FocusInputSoon()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_refocus && _phase == NewsletterPhase.Idle)
            {
                _refocus = false;
                _form?.Input?.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    private void FocusDifferentSoon()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_phase == NewsletterPhase.Done)
            {
                _form?.Message?.DifferentEmailButton?.Focus();
            }
        }, DispatcherPriority.Loaded);
    }
}
