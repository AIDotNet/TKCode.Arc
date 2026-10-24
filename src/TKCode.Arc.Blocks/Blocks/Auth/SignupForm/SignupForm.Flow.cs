// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the form logic of signup-form.tsx.
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

public partial class SignupForm
{
    private enum Field
    {
        Name,
        Email,
        Password
    }

    private readonly record struct Errors(string Name, string Email, string Password)
    {
        public string For(Field field) => field switch
        {
            Field.Name => Name,
            Field.Email => Email,
            _ => Password
        };
    }

    // Parts.
    private SignupFormStage? _stage;
    private Input? _nameInput;
    private Input? _emailInput;
    private PasswordField? _passwordInput;
    private Checkbox? _updates;
    private Button? _submit;
    private Button? _startOver;
    private readonly Button?[] _providerButtons = new Button?[3];
    private Panel? _providersBlock;
    private TextBlock? _title;
    private TextBlock? _description;
    private TextBlock? _dividerText;
    private TextBlock? _legal;
    private TextBlock? _switchLine;
    private SignupFormSwap? _feedback;
    private SignupFormMeter? _meter;
    private SignupFormErrorRow? _errorRow;
    private SignupFormSwap? _statusLine;
    private SignupFormSuccessMark? _mark;
    private TextBlock? _doneTitle;
    private TextBlock? _doneDescription;
    private TextBlock? _methodLabel;
    private TextBlock? _methodValue;
    private TextBlock? _updatesLabel;
    private TextBlock? _updatesValue;
    private AuthAnnouncer? _announcer;
    private AuthAnnouncer? _alert;
    private SignupFormLinkButton? _terms;
    private SignupFormLinkButton? _privacy;
    private SignupFormLinkButton? _signIn;

    // Arc's state.
    private bool _touchedName;
    private bool _touchedEmail;
    private bool _touchedPassword;
    private bool _submitted;
    private string _submitError = string.Empty;
    private int _direction = 1;
    private int _run;
    private CancellationTokenSource? _cts;
    private bool _syncing;
    private string? _lastPasswordAlert;

    // What the completion panel shows (kept while it leaves after Start over, as AnimatePresence keeps the last render).
    private string _doneName = string.Empty;
    private string _doneEmail = string.Empty;
    private bool _doneUpdates;

    #region Validation (Arc's getErrors and passwordScore)

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")]
    private static partial Regex EmailPattern();

    /// <summary>Arc's address check on the trimmed text: something@something.tld with a top-level part of 2 or more.</summary>
    public static bool IsValidEmail(string? email) => EmailPattern().IsMatch((email ?? string.Empty).Trim());

    /// <summary>
    /// Arc's strength score: 0 for an empty password, else one point each for 8 or more characters, 12 or more, both a
    /// lowercase and an uppercase letter (a–z, A–Z), and a digit or a symbol.
    /// </summary>
    public static int ScorePassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 0;
        }
        var lower = false;
        var upper = false;
        var digitOrSymbol = false;
        foreach (var c in password)
        {
            if (c is >= 'a' and <= 'z')
            {
                lower = true;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                upper = true;
            }
            else if (c is >= '0' and <= '9' || (c != '_' && !char.IsWhiteSpace(c)))
            {
                // JavaScript's /\d|[^\w\s]/: an ASCII digit, or anything that is not a word character or a space.
                digitOrSymbol = true;
            }
        }
        var score = 0;
        score += password.Length >= 8 ? 1 : 0;
        score += password.Length >= 12 ? 1 : 0;
        score += lower && upper ? 1 : 0;
        score += digitOrSymbol ? 1 : 0;
        return score;
    }

    private Errors Validate()
    {
        var labels = Labels;
        return new Errors(
            FullName.Trim().Length < 2 ? labels.NameError : string.Empty,
            !IsValidEmail(Email) ? labels.EmailError : string.Empty,
            Password.Length < 8 ? labels.PasswordError : string.Empty);
    }

    private string VisibleError(Errors errors, Field field)
    {
        var touched = field switch
        {
            Field.Name => _touchedName,
            Field.Email => _touchedEmail,
            _ => _touchedPassword
        };
        return touched || _submitted ? errors.For(field) : string.Empty;
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Unwire();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _stage = scope.Find<SignupFormStage>(StagePart);
        _nameInput = scope.Find<Input>(NameInputPart);
        _emailInput = scope.Find<Input>(EmailInputPart);
        _passwordInput = scope.Find<PasswordField>(PasswordInputPart);
        _updates = scope.Find<Checkbox>("PART_Updates");
        _submit = scope.Find<Button>(SubmitPart);
        _startOver = scope.Find<Button>(StartOverPart);
        _providerButtons[0] = scope.Find<Button>("PART_Google");
        _providerButtons[1] = scope.Find<Button>("PART_Apple");
        _providerButtons[2] = scope.Find<Button>("PART_GitHub");
        _providersBlock = scope.Find<Panel>("PART_ProvidersBlock");
        _title = scope.Find<TextBlock>("PART_Title");
        _description = scope.Find<TextBlock>("PART_Description");
        _dividerText = scope.Find<TextBlock>("PART_DividerText");
        _legal = scope.Find<TextBlock>("PART_Legal");
        _switchLine = scope.Find<TextBlock>("PART_SwitchLine");
        _feedback = scope.Find<SignupFormSwap>("PART_FeedbackText");
        _meter = scope.Find<SignupFormMeter>("PART_Meter");
        _errorRow = scope.Find<SignupFormErrorRow>("PART_SubmitError");
        _statusLine = scope.Find<SignupFormSwap>("PART_Status");
        _mark = scope.Find<SignupFormSuccessMark>("PART_Mark");
        _doneTitle = scope.Find<TextBlock>("PART_DoneTitle");
        _doneDescription = scope.Find<TextBlock>("PART_DoneDescription");
        _methodLabel = scope.Find<TextBlock>("PART_MethodLabel");
        _methodValue = scope.Find<TextBlock>("PART_MethodValue");
        _updatesLabel = scope.Find<TextBlock>("PART_UpdatesLabel");
        _updatesValue = scope.Find<TextBlock>("PART_UpdatesValue");
        _announcer = scope.Find<AuthAnnouncer>("PART_Announcer");
        _alert = scope.Find<AuthAnnouncer>("PART_Alert");

        _terms = new SignupFormLinkButton();
        _privacy = new SignupFormLinkButton();
        _signIn = new SignupFormLinkButton { IsStrong = true };
        Wire();
        ApplyLabels();
        _stage?.Show(Step == SignupFormStep.Done ? 1 : 0, 1);
        if (Step == SignupFormStep.Done)
        {
            _mark?.Show();
        }
        _statusLine?.Show(Status, Status);
        SyncFields();
        SyncBusy();
        Revalidate(announce: false);
        UpdateShownError();
    }

    private void Wire()
    {
        if (_nameInput is not null)
        {
            _nameInput.TextChanged += OnFieldTextChanged;
            _nameInput.LostFocus += OnFieldLostFocus;
            _nameInput.AddHandler(KeyDownEvent, OnFieldKeyDown, RoutingStrategies.Tunnel);
            TextInputOptions.SetContentType(_nameInput, TextInputContentType.Name);
        }
        if (_emailInput is not null)
        {
            _emailInput.TextChanged += OnFieldTextChanged;
            _emailInput.LostFocus += OnFieldLostFocus;
            _emailInput.AddHandler(KeyDownEvent, OnFieldKeyDown, RoutingStrategies.Tunnel);
            TextInputOptions.SetContentType(_emailInput, TextInputContentType.Email);
            TextInputOptions.SetAutoCapitalization(_emailInput, false);
        }
        if (_passwordInput is not null)
        {
            _passwordInput.TextChanged += OnFieldTextChanged;
            _passwordInput.LostFocus += OnFieldLostFocus;
            _passwordInput.AddHandler(KeyDownEvent, OnFieldKeyDown, RoutingStrategies.Tunnel);
            TextInputOptions.SetContentType(_passwordInput, TextInputContentType.Password);
        }
        if (_updates is not null)
        {
            _updates.IsCheckedChanged += OnUpdatesChanged;
        }
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
        if (_startOver is not null)
        {
            _startOver.Click += OnStartOverClick;
        }
        foreach (var button in _providerButtons)
        {
            if (button is not null)
            {
                button.Click += OnProviderClick;
            }
        }
        if (_terms is not null)
        {
            _terms.Click += OnLinkClick;
        }
        if (_privacy is not null)
        {
            _privacy.Click += OnLinkClick;
        }
        if (_signIn is not null)
        {
            _signIn.Click += OnLinkClick;
        }
    }

    private void Unwire()
    {
        foreach (var input in new FieldTextBox?[] { _nameInput, _emailInput, _passwordInput })
        {
            if (input is not null)
            {
                input.TextChanged -= OnFieldTextChanged;
                input.LostFocus -= OnFieldLostFocus;
                input.RemoveHandler(KeyDownEvent, OnFieldKeyDown);
            }
        }
        if (_updates is not null)
        {
            _updates.IsCheckedChanged -= OnUpdatesChanged;
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        if (_startOver is not null)
        {
            _startOver.Click -= OnStartOverClick;
        }
        foreach (var button in _providerButtons)
        {
            if (button is not null)
            {
                button.Click -= OnProviderClick;
            }
        }
        foreach (var link in new[] { _terms, _privacy, _signIn })
        {
            if (link is not null)
            {
                link.Click -= OnLinkClick;
            }
        }
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        if (string.IsNullOrEmpty(AutomationProperties.GetName(this)) || AutomationProperties.GetName(this) == _appliedRegionName)
        {
            _appliedRegionName = labels.RegionName;
            AutomationProperties.SetName(this, labels.RegionName);
        }
        SetText(_title, labels.Title);
        SetText(_description, labels.Description);
        SetText(_dividerText, labels.Divider);
        if (_providersBlock is not null)
        {
            var group = _providersBlock.Children.OfType<Grid>().FirstOrDefault();
            if (group is not null)
            {
                AutomationProperties.SetName(group, labels.ProvidersName);
            }
        }
        for (var i = 0; i < _providerButtons.Length; i++)
        {
            if (_providerButtons[i] is { } button)
            {
                AutomationProperties.SetName(button, labels.ProviderButtonName(labels.MethodName(ProviderAt(i))));
            }
        }
        if (_nameInput is not null)
        {
            _nameInput.Label = labels.NameLabel;
            _nameInput.Placeholder = labels.NamePlaceholder;
        }
        if (_emailInput is not null)
        {
            _emailInput.Label = labels.EmailLabel;
            _emailInput.Placeholder = labels.EmailPlaceholder;
        }
        if (_passwordInput is not null)
        {
            _passwordInput.Label = labels.PasswordLabel;
        }
        if (_updates is not null)
        {
            _updates.Content = labels.ProductUpdatesLabel;
        }
        if (_submit is not null)
        {
            _submit.Content = labels.Submit;
        }
        if (_startOver is not null)
        {
            _startOver.Content = labels.StartOver;
        }
        if (_terms is not null)
        {
            _terms.Content = labels.Terms;
            AutomationProperties.SetName(_terms, labels.Terms);
        }
        if (_privacy is not null)
        {
            _privacy.Content = labels.Privacy;
            AutomationProperties.SetName(_privacy, labels.Privacy);
        }
        if (_signIn is not null)
        {
            _signIn.Content = labels.SignIn;
            AutomationProperties.SetName(_signIn, labels.SignIn);
        }
        BuildLegal(labels);
        BuildSwitchLine(labels);
        SetText(_methodLabel, labels.SignedUpWith);
        SetText(_updatesLabel, labels.ProductUpdates);
        UpdateDoneText();
        UpdateFeedback(announce: false);
    }

    private string? _appliedRegionName;

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    private void BuildLegal(SignupFormLabels labels)
    {
        if (_legal is null || _terms is null || _privacy is null)
        {
            return;
        }
        DetachInline(_terms);
        DetachInline(_privacy);
        var inlines = _legal.Inlines ??= new InlineCollection();
        inlines.Clear();
        inlines.Add(new Run(labels.LegalLead));
        inlines.Add(new InlineUIContainer(_terms) { BaselineAlignment = BaselineAlignment.Baseline });
        inlines.Add(new Run(labels.LegalJoin));
        inlines.Add(new InlineUIContainer(_privacy) { BaselineAlignment = BaselineAlignment.Baseline });
        inlines.Add(new Run(labels.LegalEnd));
    }

    private void BuildSwitchLine(SignupFormLabels labels)
    {
        if (_switchLine is null || _signIn is null)
        {
            return;
        }
        DetachInline(_signIn);
        var inlines = _switchLine.Inlines ??= new InlineCollection();
        inlines.Clear();
        inlines.Add(new Run(labels.HaveAccount + " "));
        inlines.Add(new InlineUIContainer(_signIn) { BaselineAlignment = BaselineAlignment.Baseline });
    }

    private static void DetachInline(Control link)
    {
        // A link moves to a fresh container when the line is rebuilt; it may sit in only one at a time.
        if (link.Parent is InlineUIContainer container)
        {
            container.Child = null!;
        }
    }

    private static SignupFormMethod ProviderAt(int index) => index switch
    {
        0 => SignupFormMethod.Google,
        1 => SignupFormMethod.Apple,
        _ => SignupFormMethod.GitHub
    };

    private static int IndexOf(SignupFormMethod provider) => provider switch
    {
        SignupFormMethod.Google => 0,
        SignupFormMethod.Apple => 1,
        SignupFormMethod.GitHub => 2,
        _ => -1
    };

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FullNameProperty || change.Property == EmailProperty || change.Property == PasswordProperty)
        {
            SyncFields();
            Revalidate(announce: true);
        }
        else if (change.Property == ProductUpdatesProperty)
        {
            SyncFields();
        }
        else if (change.Property == ServerErrorProperty)
        {
            UpdateShownError();
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            Revalidate(announce: false);
        }
        else if (change.Property == AreProvidersVisibleProperty)
        {
            if (_providersBlock is not null)
            {
                _providersBlock.IsVisible = AreProvidersVisible;
            }
        }
        else if (change.Property == BoundsProperty)
        {
            var width = Bounds.Width;
            PseudoClasses.Set(NarrowPseudoClass, width > 0 && width <= 380.5);
            PseudoClasses.Set(CompactPseudoClass, width > 0 && width < 431.5);
        }
        else if (change.Property == IsBusyProperty)
        {
            PseudoClasses.Set(BusyPseudoClass, IsBusy);
        }
        else if (change.Property == StepProperty)
        {
            PseudoClasses.Set(DonePseudoClass, Step == SignupFormStep.Done);
        }
    }

    private void SyncFields()
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        try
        {
            if (_nameInput is not null && _nameInput.Text != FullName)
            {
                _nameInput.Text = FullName;
            }
            if (_emailInput is not null && _emailInput.Text != Email)
            {
                _emailInput.Text = Email;
            }
            if (_passwordInput is not null && _passwordInput.Text != Password)
            {
                _passwordInput.Text = Password;
            }
            if (_updates is not null && _updates.IsChecked != ProductUpdates)
            {
                _updates.IsChecked = ProductUpdates;
            }
            if (_providersBlock is not null)
            {
                _providersBlock.IsVisible = AreProvidersVisible;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnFieldTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is FieldTextBox box)
        {
            PullText(box);
        }
    }

    /// <summary>Copies a field's text into its property (Arc's onChange) and re-checks.</summary>
    private void PullText(FieldTextBox box)
    {
        if (_syncing)
        {
            return;
        }
        var text = box.Text ?? string.Empty;
        _syncing = true;
        try
        {
            if (ReferenceEquals(box, _nameInput))
            {
                SetCurrentValue(FullNameProperty, text);
            }
            else if (ReferenceEquals(box, _emailInput))
            {
                SetCurrentValue(EmailProperty, text);
            }
            else if (ReferenceEquals(box, _passwordInput))
            {
                SetCurrentValue(PasswordProperty, text);
            }
        }
        finally
        {
            _syncing = false;
        }
        Revalidate(announce: true);
    }

    private void OnUpdatesChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _updates is null)
        {
            return;
        }
        SetCurrentValue(ProductUpdatesProperty, _updates.IsChecked == true);
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not FieldTextBox box)
        {
            return;
        }
        // Take the field's text now: its TextChanged may not have arrived yet.
        PullText(box);
        // Arc's onBlur: a field is checked once it is left with text in it.
        var filled = !string.IsNullOrEmpty(box.Text);
        if (ReferenceEquals(box, _nameInput) && filled)
        {
            _touchedName = true;
        }
        else if (ReferenceEquals(box, _emailInput) && filled)
        {
            _touchedEmail = true;
        }
        else if (ReferenceEquals(box, _passwordInput) && filled)
        {
            _touchedPassword = true;
        }
        Revalidate(announce: true);
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter in a field submits the form (an HTML form's implicit submission).
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _ = SubmitCore();
        }
    }

    /// <summary>Recomputes the visible errors, the strength and the password's line.</summary>
    private void Revalidate(bool announce)
    {
        var errors = Validate();
        if (_nameInput is not null)
        {
            _nameInput.ErrorMessage = NullIfEmpty(VisibleError(errors, Field.Name));
        }
        if (_emailInput is not null)
        {
            _emailInput.ErrorMessage = NullIfEmpty(VisibleError(errors, Field.Email));
        }
        PasswordScore = ScorePassword(Password);
        if (_meter is not null)
        {
            _meter.Level = PasswordScore;
        }
        UpdateFeedback(announce);
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    /// <summary>The password's line: the error, else "{Strength} password", else the hint (Arc's <c>feedback</c>).</summary>
    private void UpdateFeedback(bool announce)
    {
        var labels = Labels;
        var passwordError = VisibleError(Validate(), Field.Password);
        string key;
        string text;
        if (passwordError.Length > 0)
        {
            key = "error";
            text = passwordError;
        }
        else if (Password.Length > 0)
        {
            key = "s" + PasswordScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
            text = labels.StrengthLine(labels.StrengthWord(Math.Max(PasswordScore, 1)));
        }
        else
        {
            key = "hint";
            text = labels.PasswordHint;
        }
        PseudoClasses.Set(PasswordErrorPseudoClass, passwordError.Length > 0);
        if (_passwordInput is not null)
        {
            _passwordInput.IsInvalid = passwordError.Length > 0;
            AutomationProperties.SetHelpText(_passwordInput, text);
        }
        _feedback?.Show(key, text);
        // role="alert" on the line while it shows the error.
        if (passwordError.Length == 0)
        {
            _lastPasswordAlert = null;
        }
        else if (announce && passwordError != _lastPasswordAlert)
        {
            _lastPasswordAlert = passwordError;
            _alert?.Announce(passwordError);
        }
    }

    private void UpdateShownError()
    {
        var shown = _submitError.Length > 0 ? _submitError : string.IsNullOrEmpty(ServerError) ? null : ServerError;
        var changed = shown != ShownError;
        ShownError = shown;
        if (_errorRow is not null)
        {
            _errorRow.Text = shown;
        }
        if (changed && shown is not null && IsLoaded)
        {
            _alert?.Announce(shown);
        }
    }

    #endregion

    #region Actions

    private void OnSubmitClick(object? sender, RoutedEventArgs e) => _ = SubmitCore();

    private void OnStartOverClick(object? sender, RoutedEventArgs e) => Reset();

    private void OnProviderClick(object? sender, RoutedEventArgs e)
    {
        var index = Array.IndexOf(_providerButtons, sender as Button);
        if (index >= 0)
        {
            _ = ContinueWithCore(ProviderAt(index));
        }
    }

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        var link = ReferenceEquals(sender, _terms) ? SignupFormLink.Terms
            : ReferenceEquals(sender, _privacy) ? SignupFormLink.Privacy
            : SignupFormLink.SignIn;
        OpenLink(link);
    }

    /// <summary>
    /// Activates a text link as pressing it does: raises <see cref="Navigate"/> and runs <see cref="NavigateCommand"/>; when
    /// nobody handles the event, the status line says where the link would go ("Terms open in your app").
    /// </summary>
    public void OpenLink(SignupFormLink link)
    {
        var args = new SignupFormNavigateEventArgs(NavigateEvent, link);
        RaiseEvent(args);
        if (NavigateCommand is { } command && command.CanExecute(link))
        {
            command.Execute(link);
        }
        if (!args.Handled)
        {
            var labels = Labels;
            SetStatus(link switch
            {
                SignupFormLink.Terms => labels.StatusTerms,
                SignupFormLink.Privacy => labels.StatusPrivacy,
                _ => labels.StatusSignIn
            });
        }
    }

    /// <summary>
    /// Presses Create account: checks every field (the first wrong one gets the focus) and, when all are valid, runs
    /// <see cref="SubmitAsync"/> for at least <see cref="SubmitMinimumDuration"/>, then shows the completion panel or the
    /// error. Ignored while busy or on the completion panel. Returns when the attempt is over.
    /// </summary>
    public Task Submit() => SubmitCore();

    private async Task SubmitCore()
    {
        if (IsBusy || Step != SignupFormStep.Form)
        {
            return;
        }
        _submitted = true;
        _submitError = string.Empty;
        UpdateShownError();
        var errors = Validate();
        Revalidate(announce: false);
        var first = errors.Name.Length > 0 ? (Control?)_nameInput
            : errors.Email.Length > 0 ? _emailInput
            : errors.Password.Length > 0 ? _passwordInput
            : null;
        if (errors.Name.Length > 0 || errors.Email.Length > 0 || errors.Password.Length > 0)
        {
            first?.Focus(NavigationMethod.Unspecified);
            if (errors.Password.Length > 0)
            {
                _lastPasswordAlert = errors.Password;
                _alert?.Announce(errors.Password);
            }
            return;
        }
        var run = ++_run;
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        SetBusy(SignupFormMethod.Email);
        SetStatus(Labels.StatusCreating);
        var details = new SignupFormDetails(FullName.Trim(), Email.Trim(), Password, ProductUpdates);
        try
        {
            var handler = SubmitAsync;
            var work = handler is null ? Task.CompletedTask : handler(details, cts.Token);
            var minimum = SubmitMinimumDuration > TimeSpan.Zero ? Task.Delay(SubmitMinimumDuration, cts.Token) : Task.CompletedTask;
            await Task.WhenAll(work, minimum);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested || run != _run)
        {
            return;
        }
        catch (Exception exception)
        {
            if (run != _run)
            {
                return;
            }
            SetBusy(null);
            _submitError = string.IsNullOrWhiteSpace(exception.Message) ? Labels.SubmitFailed : exception.Message;
            UpdateShownError();
            SetStatus(Labels.StatusFailed);
            return;
        }
        if (run != _run)
        {
            return;
        }
        Finish(SignupFormMethod.Email);
    }

    /// <summary>
    /// Signs up with a provider as its button does: raises <see cref="ProviderSelected"/>, then runs <see cref="ProviderAsync"/>
    /// (or simulates the redirect) and shows the completion panel. Ignored while busy, on the completion panel, or for
    /// <see cref="SignupFormMethod.Email"/>.
    /// </summary>
    public Task ContinueWith(SignupFormMethod provider) => ContinueWithCore(provider);

    private async Task ContinueWithCore(SignupFormMethod provider)
    {
        if (IsBusy || Step != SignupFormStep.Form || provider == SignupFormMethod.Email)
        {
            return;
        }
        var labels = Labels;
        var run = ++_run;
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        SetBusy(provider);
        SetStatus(labels.StatusOpening(labels.MethodName(provider)));
        RaiseEvent(new SignupFormProviderEventArgs(ProviderSelectedEvent, provider));
        if (ProviderCommand is { } command && command.CanExecute(provider))
        {
            command.Execute(provider);
        }
        var handler = ProviderAsync;
        try
        {
            if (handler is not null)
            {
                await handler(provider, cts.Token);
            }
            else if (ProviderSimulationDuration > TimeSpan.Zero)
            {
                await Task.Delay(ProviderSimulationDuration, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested || run != _run)
        {
            return;
        }
        catch (Exception exception)
        {
            if (run != _run)
            {
                return;
            }
            SetBusy(null);
            _submitError = string.IsNullOrWhiteSpace(exception.Message) ? labels.SubmitFailed : exception.Message;
            UpdateShownError();
            SetStatus(labels.StatusFailed);
            return;
        }
        if (run != _run)
        {
            return;
        }
        if (handler is null)
        {
            // The simulated provider knows who you are: Arc fills an empty name and address with its sample person.
            if (FullName.Trim().Length == 0)
            {
                SetCurrentValue(FullNameProperty, labels.SampleName);
            }
            if (Email.Trim().Length == 0)
            {
                SetCurrentValue(EmailProperty, labels.SampleEmail);
            }
        }
        Finish(provider);
    }

    private void Finish(SignupFormMethod how)
    {
        SetBusy(null);
        SignedUpWith = how;
        _direction = 1;
        _doneName = FullName.Trim();
        _doneEmail = Email.Trim();
        _doneUpdates = ProductUpdates;
        UpdateDoneText();
        var args = new SignupFormCompletedEventArgs(AccountCreatedEvent, how, _doneName, _doneEmail, _doneUpdates);
        GoTo(SignupFormStep.Done);
        SetStatus(Labels.StatusCreated(how));
        RaiseEvent(args);
        if (AccountCreatedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    /// <summary>
    /// Starts over as the Start over button does: empties the form (fields, errors, opt-in), goes back to the form and
    /// focuses the name. A request in flight is abandoned (its token is cancelled).
    /// </summary>
    public void Reset()
    {
        _run++;
        _cts?.Cancel();
        _cts = null;
        SetBusy(null);
        _touchedName = false;
        _touchedEmail = false;
        _touchedPassword = false;
        _submitted = false;
        _submitError = string.Empty;
        _lastPasswordAlert = null;
        if (_passwordInput is not null)
        {
            _passwordInput.IsRevealed = false;
        }
        SetCurrentValue(FullNameProperty, string.Empty);
        SetCurrentValue(EmailProperty, string.Empty);
        SetCurrentValue(PasswordProperty, string.Empty);
        SetCurrentValue(ProductUpdatesProperty, false);
        UpdateShownError();
        Revalidate(announce: false);
        _direction = -1;
        GoTo(SignupFormStep.Form);
        SetStatus(Labels.StatusStartOver);
        _ = FocusNameSoon(_run);
    }

    private async Task FocusNameSoon(int run)
    {
        // Arc focuses the name 60 ms after Start over, once the form is back. Resumes on the UI thread.
        await Task.Delay(60);
        if (run == _run && Step == SignupFormStep.Form)
        {
            FocusName();
        }
    }

    /// <summary>Moves the keyboard focus to the full name field.</summary>
    public bool FocusName() => _nameInput?.Focus(NavigationMethod.Unspecified) == true;

    private void GoTo(SignupFormStep step)
    {
        if (Step == step)
        {
            return;
        }
        Step = step;
        _stage?.Show(step == SignupFormStep.Done ? 1 : 0, _direction);
        if (step == SignupFormStep.Done)
        {
            _mark?.Play();
            // Arc focuses the completion heading (tabIndex -1) so screen readers land on it.
            _doneTitle?.Focus(NavigationMethod.Unspecified);
        }
    }

    private void SetBusy(SignupFormMethod? method)
    {
        BusyMethod = method;
        IsBusy = method is not null;
        SyncBusy();
    }

    private void SyncBusy()
    {
        var busy = BusyMethod;
        foreach (var input in new FieldTextBox?[] { _nameInput, _emailInput, _passwordInput })
        {
            if (input is not null)
            {
                input.IsReadOnly = busy is not null;
            }
        }
        if (_updates is not null)
        {
            _updates.IsEnabled = busy is null;
        }
        if (_submit is not null)
        {
            _submit.IsLoading = busy == SignupFormMethod.Email;
        }
        for (var i = 0; i < _providerButtons.Length; i++)
        {
            if (_providerButtons[i] is { } button)
            {
                var mine = busy == ProviderAt(i);
                button.IsLoading = mine;
                // The others are aria-disabled: they look the same, do not press in, and ignore presses.
                button.IsPressScaleEnabled = busy is null;
            }
        }
    }

    private void SetStatus(string text)
    {
        if (text == Status)
        {
            return;
        }
        Status = text;
        _statusLine?.Show(text, text);
        _announcer?.Announce(text);
    }

    private void UpdateDoneText()
    {
        var labels = Labels;
        var first = _doneName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        SetText(_doneTitle, labels.Welcome(string.IsNullOrEmpty(first) ? labels.NoName : first));
        if (_doneDescription is not null)
        {
            var inlines = _doneDescription.Inlines ??= new InlineCollection();
            inlines.Clear();
            inlines.Add(new Run(labels.ConfirmationLead));
            var address = new Run(_doneEmail);
            ArcTokens.Bind(address, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
            inlines.Add(address);
            inlines.Add(new Run(labels.ConfirmationEnd));
        }
        SetText(_methodValue, labels.MethodName(SignedUpWith));
        SetText(_updatesValue, _doneUpdates ? labels.On : labels.Off);
    }

    #endregion

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Abandon a request in flight: its token is cancelled and its result is ignored.
        if (IsBusy)
        {
            _run++;
            _cts?.Cancel();
            _cts = null;
            SetBusy(null);
        }
    }

    // Test hooks.
    internal SignupFormStage? Stage => _stage;
    internal SignupFormSwap? FeedbackLine => _feedback;
    internal SignupFormSwap? StatusLine => _statusLine;
    internal SignupFormMeter? Meter => _meter;
    internal SignupFormErrorRow? ErrorRow => _errorRow;
    internal SignupFormSuccessMark? Mark => _mark;
    internal AuthAnnouncer? Announcer => _announcer;
    internal AuthAnnouncer? Alert => _alert;
    internal Input? NameInput => _nameInput;
    internal Input? EmailInput => _emailInput;
    internal PasswordField? PasswordInput => _passwordInput;
    internal Checkbox? UpdatesBox => _updates;
    internal Button? SubmitButton => _submit;
    internal Button? StartOverButton => _startOver;
    internal Button? ProviderButton(SignupFormMethod provider) => IndexOf(provider) is var i and >= 0 ? _providerButtons[i] : null;
    internal TextBlock? DoneTitle => _doneTitle;
    internal TextBlock? DoneDescription => _doneDescription;
    internal string? MethodValueText => _methodValue?.Text;
    internal string? UpdatesValueText => _updatesValue?.Text;
    internal SignupFormLinkButton? TermsLink => _terms;
    internal SignupFormLinkButton? PrivacyLink => _privacy;
    internal SignupFormLinkButton? SignInLink => _signIn;
    internal Panel? ProvidersBlock => _providersBlock;
}
