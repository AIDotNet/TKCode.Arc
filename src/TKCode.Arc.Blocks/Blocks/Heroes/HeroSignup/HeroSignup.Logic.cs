using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class HeroSignup
{
    private Input? _email;
    private Button? _submit;
    private Button? _reset;
    private PresenceHost? _form;
    private PresenceHost? _done;
    private SiteAnnouncer? _announcer;
    private HeroSignupFaces? _faces;
    private CancellationTokenSource? _sending;
    private bool _triedSubmit;
    private string? _checkedText;

    internal Input? EmailBox => _email;

    internal Button? SubmitButton => _submit;

    internal Button? ResetButton => _reset;

    internal SiteAnnouncer? Announcer => _announcer;

    internal HeroSignupFaces? Faces => _faces;

    /// <summary>The address in the field.</summary>
    public string Email
    {
        get => _email?.Text ?? _pendingEmail ?? string.Empty;
        set
        {
            _pendingEmail = value;
            if (_email is not null)
            {
                _email.Text = value;
            }
        }
    }

    private string? _pendingEmail;

    /// <summary>Checks the address and, when it is valid, sends it (the button and Enter in the field call this).</summary>
    public async Task Submit()
    {
        if (Phase != HeroSignupPhase.Idle)
        {
            return;
        }
        _triedSubmit = true;
        _checkedText = Email;
        var email = Email.Trim();
        if (Validate(email) is { } error)
        {
            ShowError(error);
            _email?.Focus(NavigationMethod.Tab);
            return;
        }
        ShowError(null);
        _sending?.Cancel();
        var cts = new CancellationTokenSource();
        _sending = cts;
        SetPhase(HeroSignupPhase.Sending);
        try
        {
            var send = SubmitAsync ?? Simulate;
            await send(email, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_sending, cts))
            {
                return;
            }
            SetPhase(HeroSignupPhase.Idle);
            ShowError(FailedMessage);
            _email?.Focus(NavigationMethod.Tab);
            return;
        }
        if (!ReferenceEquals(_sending, cts) || cts.IsCancellationRequested)
        {
            return;
        }
        _sending = null;
        SubmittedEmail = email;
        DoneText = string.Format(CultureInfo.CurrentCulture, DoneFormat, email);
        SetPhase(HeroSignupPhase.Done);
        _announcer?.Announce(DoneTitle + ". " + DoneText);
        RaiseEvent(new HeroSignupSubmittedEventArgs(SubmittedEvent, email));
        if (SubmitCommand is { } command && command.CanExecute(email))
        {
            command.Execute(email);
        }
        // The form that had focus is going away: the reset button takes it, so the keyboard stays in the section.
        if (_email?.IsKeyboardFocusWithin == true || _submit?.IsKeyboardFocusWithin == true)
        {
            Dispatcher.UIThread.Post(() => _reset?.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
        }
    }

    /// <summary>Brings back the empty form: cancels a send in progress, clears the address and the error.</summary>
    public void Reset()
    {
        _sending?.Cancel();
        _sending = null;
        _triedSubmit = false;
        SubmittedEmail = null;
        DoneText = null;
        var hadFocus = _reset?.IsKeyboardFocusWithin == true;
        SetPhase(HeroSignupPhase.Idle);
        Email = string.Empty;
        ShowError(null);
        if (hadFocus)
        {
            Dispatcher.UIThread.Post(() => _email?.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
        }
    }

    /// <summary>Moves keyboard focus to the address field.</summary>
    public void FocusEmail() => _email?.Focus(NavigationMethod.Tab);

    /// <summary>The error for <paramref name="email"/>, or null when it is a valid address.</summary>
    internal string? Validate(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return EmptyMessage;
        }
        return SiteEmail.IsValid(email) ? null : FormatMessage;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_email is not null)
        {
            _pendingEmail = _email.Text;
            _email.RemoveHandler(KeyDownEvent, OnEmailKeyDown);
            _email.TextChanged -= OnEmailTextChanged;
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        _email = e.NameScope.Find<Input>(EmailPart);
        _submit = e.NameScope.Find<Button>(SubmitPart);
        _reset = e.NameScope.Find<Button>(ResetPart);
        _form = e.NameScope.Find<PresenceHost>(FormPart);
        _done = e.NameScope.Find<PresenceHost>(DonePart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        _faces = e.NameScope.Find<HeroSignupFaces>("PART_Faces");
        if (_email is not null)
        {
            _email.Text = _pendingEmail;
            _email.AddHandler(KeyDownEvent, OnEmailKeyDown, RoutingStrategies.Tunnel);
            _email.TextChanged += OnEmailTextChanged;
        }
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }
        UpdateFaces();
        ApplyPhase();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProofNamesProperty || change.Property == ProofTextProperty)
        {
            UpdateFaces();
        }
        else if (change.Property == AlignmentProperty)
        {
            PseudoClasses.Set(StartPseudoClass, Alignment == HeroSignupAlignment.Start);
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty || change.Property == NarrowWidthProperty)
        {
            var width = Bounds.Width;
            PseudoClasses.Set(CompactPseudoClass, width > 0 && width < CompactWidth);
            PseudoClasses.Set(NarrowPseudoClass, width > 0 && width < NarrowWidth);
        }
        else if (change.Property == DoneFormatProperty && SubmittedEmail is { } email)
        {
            DoneText = string.Format(CultureInfo.CurrentCulture, DoneFormat, email);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_sending is { } sending)
        {
            sending.Cancel();
            _sending = null;
            SetPhase(HeroSignupPhase.Idle);
        }
    }

    private static async Task Simulate(string email, CancellationToken token) => await Task.Delay(900, token);

    private void OnSubmitClick(object? sender, RoutedEventArgs e) => _ = Submit();

    private void OnResetClick(object? sender, RoutedEventArgs e) => Reset();

    private void OnEmailKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _ = Submit();
        }
    }

    private void OnEmailTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Phase != HeroSignupPhase.Idle || _email is null)
        {
            return;
        }
        if (string.Equals(Email, _checkedText, StringComparison.Ordinal))
        {
            // The change notice of text that was already checked (it can arrive after the submit that checked it).
            return;
        }
        _checkedText = null;
        if (_triedSubmit)
        {
            ShowError(Validate(Email.Trim()));
        }
        else if (_email.IsInvalid)
        {
            ShowError(null);
        }
    }

    private void ShowError(string? message)
    {
        if (_email is null)
        {
            return;
        }
        var changed = !string.Equals(_email.ErrorMessage, message, StringComparison.Ordinal);
        _email.ErrorMessage = message;
        _email.IsInvalid = message is not null;
        if (message is not null && changed)
        {
            _announcer?.Announce(message);
        }
    }

    private void SetPhase(HeroSignupPhase phase)
    {
        Phase = phase;
        ApplyPhase();
    }

    private void ApplyPhase()
    {
        PseudoClasses.Set(SendingPseudoClass, Phase == HeroSignupPhase.Sending);
        PseudoClasses.Set(DonePseudoClass, Phase == HeroSignupPhase.Done);
        if (_email is not null)
        {
            _email.IsReadOnly = Phase != HeroSignupPhase.Idle;
        }
        if (_submit is not null)
        {
            _submit.IsLoading = Phase == HeroSignupPhase.Sending;
            _submit.Content = Phase == HeroSignupPhase.Sending ? SendingText : ButtonText;
        }
        if (_form is not null)
        {
            _form.IsPresent = Phase != HeroSignupPhase.Done;
            _form.IsHitTestVisible = Phase != HeroSignupPhase.Done;
        }
        if (_done is not null)
        {
            _done.IsPresent = Phase == HeroSignupPhase.Done;
            _done.IsHitTestVisible = Phase == HeroSignupPhase.Done;
        }
        if (_reset is not null)
        {
            _reset.IsTabStop = Phase == HeroSignupPhase.Done;
        }
        if (_email is not null)
        {
            _email.IsTabStop = Phase != HeroSignupPhase.Done;
        }
        if (_submit is not null)
        {
            _submit.IsTabStop = Phase != HeroSignupPhase.Done;
        }
    }

    private void UpdateFaces()
    {
        var show = !string.IsNullOrEmpty(ProofText);
        PseudoClasses.Set(NoProofPseudoClass, !show);
        _faces?.SetNames(show ? EffectiveProofNames : []);
    }
}
