// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): signup-form.tsx.
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// An account creation card (Arc's Sign up block): Google, Apple and GitHub buttons, then the full name, a work email and a
/// password with a four-bar strength meter, a product updates opt-in and Create account. When the account is made, the card
/// slides to a completion panel whose check draws itself in, with how the account was made and Start over; the card's
/// height glides between the two.
/// </summary>
/// <remarks>
/// <para>
/// Validation is local: a name of at least 2 characters, an address like name@example.com and a password of 8 or more
/// characters. A field is checked once it loses focus with text in it, and from then on with every keystroke; Create account
/// checks every field and focuses the first one that is wrong. The password's line says how strong it is (Weak, Fair, Good,
/// Strong: length 8 and 12, mixed case, a digit or symbol), or why it is not valid.
/// </para>
/// <para>
/// <see cref="SubmitAsync"/> creates the account: it receives the trimmed <see cref="SignupFormDetails"/> and a token that is
/// cancelled when the card is reset or removed; throw to show its message above the button (or <see cref="SignupFormLabels.SubmitFailed"/>
/// when it has none). The card stays busy for at least <see cref="SubmitMinimumDuration"/> (700 ms, Arc's pacing). A provider
/// button raises <see cref="ProviderSelected"/> and then runs <see cref="ProviderAsync"/>; without a handler it simulates the
/// redirect and completes after <see cref="ProviderSimulationDuration"/> (900 ms), filling in a sample name and address when
/// the fields are empty, as Arc's preview does. <see cref="ServerError"/> shows an error from your own API.
/// </para>
/// <para>
/// The card is at most 432 wide and follows its own width: at 380 and below the padding, heading and provider labels shrink;
/// below its full width it takes the smaller panel radius.
/// </para>
/// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli).
/// </remarks>
[TemplatePart(StagePart, typeof(SignupFormStage))]
[TemplatePart(NameInputPart, typeof(Input))]
[TemplatePart(EmailInputPart, typeof(Input))]
[TemplatePart(PasswordInputPart, typeof(PasswordField))]
[TemplatePart(SubmitPart, typeof(Button))]
[TemplatePart(StartOverPart, typeof(Button))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, BusyPseudoClass, DonePseudoClass, PasswordErrorPseudoClass)]
public partial class SignupForm : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string NameInputPart = "PART_NameInput";
    public const string EmailInputPart = "PART_EmailInput";
    public const string PasswordInputPart = "PART_PasswordInput";
    public const string SubmitPart = "PART_Submit";
    public const string StartOverPart = "PART_StartOver";

    /// <summary>Set while the card is at most 380 wide (Arc's container query): tighter padding, a 28px heading, smaller provider labels.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the card is narrower than its full 432: the smaller panel radius.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while an account is being created (<see cref="IsBusy"/>).</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>Set while the completion panel shows.</summary>
    public const string DonePseudoClass = ":done";

    /// <summary>Set while the password's line shows an error.</summary>
    public const string PasswordErrorPseudoClass = ":password-error";

    #region Form values

    /// <summary>The full name in the form (two-way).</summary>
    public static readonly StyledProperty<string> FullNameProperty =
        AvaloniaProperty.Register<SignupForm, string>(nameof(FullName), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => v ?? string.Empty);

    /// <summary>The work email in the form (two-way).</summary>
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<SignupForm, string>(nameof(Email), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => v ?? string.Empty);

    /// <summary>The password in the form (two-way).</summary>
    public static readonly StyledProperty<string> PasswordProperty =
        AvaloniaProperty.Register<SignupForm, string>(nameof(Password), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => v ?? string.Empty);

    /// <summary>Whether "Email me product updates" is checked (two-way).</summary>
    public static readonly StyledProperty<bool> ProductUpdatesProperty =
        AvaloniaProperty.Register<SignupForm, bool>(nameof(ProductUpdates), defaultBindingMode: BindingMode.TwoWay);

    #endregion

    #region Options

    /// <summary>An error from your API (Arc's <c>serverError</c>), shown above Create account; a submit error wins over it.</summary>
    public static readonly StyledProperty<string?> ServerErrorProperty =
        AvaloniaProperty.Register<SignupForm, string?>(nameof(ServerError));

    /// <summary>Whether the provider buttons and the "or" divider show (true).</summary>
    public static readonly StyledProperty<bool> AreProvidersVisibleProperty =
        AvaloniaProperty.Register<SignupForm, bool>(nameof(AreProvidersVisible), true);

    /// <summary>The least time Create account stays busy, even when <see cref="SubmitAsync"/> returns sooner (700 ms).</summary>
    public static readonly StyledProperty<TimeSpan> SubmitMinimumDurationProperty =
        AvaloniaProperty.Register<SignupForm, TimeSpan>(nameof(SubmitMinimumDuration), TimeSpan.FromMilliseconds(700));

    /// <summary>How long a simulated provider takes when <see cref="ProviderAsync"/> is null (900 ms).</summary>
    public static readonly StyledProperty<TimeSpan> ProviderSimulationDurationProperty =
        AvaloniaProperty.Register<SignupForm, TimeSpan>(nameof(ProviderSimulationDuration), TimeSpan.FromMilliseconds(900));

    /// <summary>Every user-visible string (Arc's English copy by default).</summary>
    public static readonly StyledProperty<SignupFormLabels> LabelsProperty =
        AvaloniaProperty.Register<SignupForm, SignupFormLabels>(nameof(Labels), new SignupFormLabels());

    #endregion

    #region State (read only)

    /// <summary>The view on screen: the form or the completion panel.</summary>
    public static readonly DirectProperty<SignupForm, SignupFormStep> StepProperty =
        AvaloniaProperty.RegisterDirect<SignupForm, SignupFormStep>(nameof(Step), o => o.Step);

    /// <summary>True while an account is being created, through the form or a provider.</summary>
    public static readonly DirectProperty<SignupForm, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<SignupForm, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>What is running while <see cref="IsBusy"/>: <see cref="SignupFormMethod.Email"/> for the form, otherwise the provider; null when idle.</summary>
    public static readonly DirectProperty<SignupForm, SignupFormMethod?> BusyMethodProperty =
        AvaloniaProperty.RegisterDirect<SignupForm, SignupFormMethod?>(nameof(BusyMethod), o => o.BusyMethod);

    /// <summary>How the last account was created (shown on the completion panel).</summary>
    public static readonly DirectProperty<SignupForm, SignupFormMethod> SignedUpWithProperty =
        AvaloniaProperty.RegisterDirect<SignupForm, SignupFormMethod>(nameof(SignedUpWith), o => o.SignedUpWith);

    /// <summary>The password's strength, 0 (empty or very weak) to 4 (strong): one point each for 8 and 12 characters, mixed case, and a digit or symbol.</summary>
    public static readonly DirectProperty<SignupForm, int> PasswordScoreProperty =
        AvaloniaProperty.RegisterDirect<SignupForm, int>(nameof(PasswordScore), o => o.PasswordScore);

    /// <summary>The line in the footer (also read by screen readers): what just happened, or empty.</summary>
    public static readonly DirectProperty<SignupForm, string> StatusProperty =
        AvaloniaProperty.RegisterDirect<SignupForm, string>(nameof(Status), o => o.Status);

    /// <summary>The error above Create account: the last submit error, else <see cref="ServerError"/>, else null.</summary>
    public static readonly DirectProperty<SignupForm, string?> ShownErrorProperty =
        AvaloniaProperty.RegisterDirect<SignupForm, string?>(nameof(ShownError), o => o.ShownError);

    #endregion

    #region Events and commands

    /// <summary>An account was created (through the form or a provider); the completion panel is about to show.</summary>
    public static readonly RoutedEvent<SignupFormCompletedEventArgs> AccountCreatedEvent =
        RoutedEvent.Register<SignupForm, SignupFormCompletedEventArgs>(nameof(AccountCreated), RoutingStrategies.Bubble);

    /// <summary>A provider button was pressed (Arc's <c>onProvider</c>): start your OAuth redirect here.</summary>
    public static readonly RoutedEvent<SignupFormProviderEventArgs> ProviderSelectedEvent =
        RoutedEvent.Register<SignupForm, SignupFormProviderEventArgs>(nameof(ProviderSelected), RoutingStrategies.Bubble);

    /// <summary>Terms, Privacy policy or Sign in was pressed. Mark it handled to stop the status line's preview text.</summary>
    public static readonly RoutedEvent<SignupFormNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<SignupForm, SignupFormNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    /// <summary>Runs after <see cref="AccountCreated"/>; its parameter is the event's <see cref="SignupFormCompletedEventArgs"/>.</summary>
    public static readonly StyledProperty<ICommand?> AccountCreatedCommandProperty =
        AvaloniaProperty.Register<SignupForm, ICommand?>(nameof(AccountCreatedCommand));

    /// <summary>Runs after <see cref="ProviderSelected"/>; its parameter is the <see cref="SignupFormMethod"/> of the provider.</summary>
    public static readonly StyledProperty<ICommand?> ProviderCommandProperty =
        AvaloniaProperty.Register<SignupForm, ICommand?>(nameof(ProviderCommand));

    /// <summary>Runs after <see cref="Navigate"/>; its parameter is the <see cref="SignupFormLink"/>.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<SignupForm, ICommand?>(nameof(NavigateCommand));

    #endregion

    private SignupFormStep _step;
    private bool _busy;
    private SignupFormMethod? _busyMethod;
    private SignupFormMethod _signedUpWith;
    private int _passwordScore;
    private string _status = string.Empty;
    private string? _shownError;

    /// <inheritdoc cref="FullNameProperty"/>
    public string FullName { get => GetValue(FullNameProperty); set => SetValue(FullNameProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="PasswordProperty"/>
    public string Password { get => GetValue(PasswordProperty); set => SetValue(PasswordProperty, value); }

    /// <inheritdoc cref="ProductUpdatesProperty"/>
    public bool ProductUpdates { get => GetValue(ProductUpdatesProperty); set => SetValue(ProductUpdatesProperty, value); }

    /// <inheritdoc cref="ServerErrorProperty"/>
    public string? ServerError { get => GetValue(ServerErrorProperty); set => SetValue(ServerErrorProperty, value); }

    /// <inheritdoc cref="AreProvidersVisibleProperty"/>
    public bool AreProvidersVisible { get => GetValue(AreProvidersVisibleProperty); set => SetValue(AreProvidersVisibleProperty, value); }

    /// <inheritdoc cref="SubmitMinimumDurationProperty"/>
    public TimeSpan SubmitMinimumDuration { get => GetValue(SubmitMinimumDurationProperty); set => SetValue(SubmitMinimumDurationProperty, value); }

    /// <inheritdoc cref="ProviderSimulationDurationProperty"/>
    public TimeSpan ProviderSimulationDuration { get => GetValue(ProviderSimulationDurationProperty); set => SetValue(ProviderSimulationDurationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SignupFormLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>
    /// Creates the account (Arc's <c>onSubmit</c>): called with valid, trimmed details and a token that is cancelled when the
    /// card is reset or removed. Return to show the completion panel; throw to show the exception's message above Create
    /// account. Null accepts every valid form.
    /// </summary>
    public Func<SignupFormDetails, CancellationToken, Task>? SubmitAsync { get; set; }

    /// <summary>
    /// Signs up with a provider after <see cref="ProviderSelected"/>: return to show the completion panel, throw to show the
    /// message above Create account. Null simulates the redirect for <see cref="ProviderSimulationDuration"/> (Arc's preview).
    /// </summary>
    public Func<SignupFormMethod, CancellationToken, Task>? ProviderAsync { get; set; }

    /// <inheritdoc cref="StepProperty"/>
    public SignupFormStep Step
    {
        get => _step;
        private set => SetAndRaise(StepProperty, ref _step, value);
    }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy
    {
        get => _busy;
        private set => SetAndRaise(IsBusyProperty, ref _busy, value);
    }

    /// <inheritdoc cref="BusyMethodProperty"/>
    public SignupFormMethod? BusyMethod
    {
        get => _busyMethod;
        private set => SetAndRaise(BusyMethodProperty, ref _busyMethod, value);
    }

    /// <inheritdoc cref="SignedUpWithProperty"/>
    public SignupFormMethod SignedUpWith
    {
        get => _signedUpWith;
        private set => SetAndRaise(SignedUpWithProperty, ref _signedUpWith, value);
    }

    /// <inheritdoc cref="PasswordScoreProperty"/>
    public int PasswordScore
    {
        get => _passwordScore;
        private set => SetAndRaise(PasswordScoreProperty, ref _passwordScore, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public string Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <inheritdoc cref="ShownErrorProperty"/>
    public string? ShownError
    {
        get => _shownError;
        private set => SetAndRaise(ShownErrorProperty, ref _shownError, value);
    }

    /// <inheritdoc cref="AccountCreatedEvent"/>
    public event EventHandler<SignupFormCompletedEventArgs>? AccountCreated
    {
        add => AddHandler(AccountCreatedEvent, value);
        remove => RemoveHandler(AccountCreatedEvent, value);
    }

    /// <inheritdoc cref="ProviderSelectedEvent"/>
    public event EventHandler<SignupFormProviderEventArgs>? ProviderSelected
    {
        add => AddHandler(ProviderSelectedEvent, value);
        remove => RemoveHandler(ProviderSelectedEvent, value);
    }

    /// <inheritdoc cref="NavigateEvent"/>
    public event EventHandler<SignupFormNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <inheritdoc cref="AccountCreatedCommandProperty"/>
    public ICommand? AccountCreatedCommand { get => GetValue(AccountCreatedCommandProperty); set => SetValue(AccountCreatedCommandProperty, value); }

    /// <inheritdoc cref="ProviderCommandProperty"/>
    public ICommand? ProviderCommand { get => GetValue(ProviderCommandProperty); set => SetValue(ProviderCommandProperty, value); }

    /// <inheritdoc cref="NavigateCommandProperty"/>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    static SignupForm()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<SignupForm>(AutomationLandmarkType.Region);
        FocusableProperty.OverrideDefaultValue<SignupForm>(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SignupFormPeer(this);

    private sealed class SignupFormPeer(SignupForm owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : ((SignupForm)Owner).Labels.RegionName;
    }
}
