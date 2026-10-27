using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A key manager for an API's developer settings: a header, a toolbar with the number of active keys and a "Create key"
/// pill, and the keys as rows. The pill unfolds in place into a form (name, test or live, scopes); submitting runs
/// <see cref="GenerateAction"/> and the same panel then shows the new secret once, with a copy button, while the new row
/// slides in at the top, highlighted until the panel closes. A row opens into its details (creator, last request, request
/// count) and a hold-to-revoke button; a confirmed revoke strikes the row through, runs <see cref="RevokeAction"/> and
/// folds the row away.
/// <code>
/// &lt;arc:ApiKeys Title="API keys" Description="Keys let your backend call the API." /&gt;
/// </code>
/// </summary>
public partial class ApiKeys : TemplatedControl
{
    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ApiKeys, string?>(nameof(Title), "API keys");

    /// <summary>The line under the heading (at most 520 wide); null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ApiKeys, string?>(nameof(Description));

    /// <summary>The keys, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Keys"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> KeysSourceProperty =
        AvaloniaProperty.Register<ApiKeys, IEnumerable?>(nameof(KeysSource));

    /// <summary>The scopes the form offers, when you give them as a list; it wins over <see cref="Scopes"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ScopesSourceProperty =
        AvaloniaProperty.Register<ApiKeys, IEnumerable?>(nameof(ScopesSource));

    /// <summary>Whether the "Create key" panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsCreateOpenProperty =
        AvaloniaProperty.Register<ApiKeys, bool>(nameof(IsCreateOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The environment a fresh form starts with.</summary>
    public static readonly StyledProperty<ApiKeyEnvironment> DefaultEnvironmentProperty =
        AvaloniaProperty.Register<ApiKeys, ApiKeyEnvironment>(nameof(DefaultEnvironment));

    /// <summary>The longest name the form accepts (40).</summary>
    public static readonly StyledProperty<int> NameMaxLengthProperty =
        AvaloniaProperty.Register<ApiKeys, int>(nameof(NameMaxLength), 40);

    /// <summary>
    /// Creates the key on your server: awaited when the form is submitted, with what the person asked for and a token
    /// that Escape, closing the panel or the block leaving the window cancels. Return the new row and its one-time secret.
    /// An exception keeps the form open with the error in it; a cancellation returns to the form quietly. Null simulates
    /// a server (about 730ms, a random 40-character secret).
    /// </summary>
    public static readonly StyledProperty<Func<ApiKeyRequest, CancellationToken, Task<ApiKeyCreated>>?> GenerateActionProperty =
        AvaloniaProperty.Register<ApiKeys, Func<ApiKeyRequest, CancellationToken, Task<ApiKeyCreated>>?>(nameof(GenerateAction));

    /// <summary>
    /// Revokes the key on your server: awaited once the hold is confirmed, while the row shows it struck through. An
    /// exception restores the row and announces the error. Null revokes at once.
    /// </summary>
    public static readonly StyledProperty<Func<ApiKeyItem, CancellationToken, Task>?> RevokeActionProperty =
        AvaloniaProperty.Register<ApiKeys, Func<ApiKeyItem, CancellationToken, Task>?>(nameof(RevokeAction));

    /// <summary>Run with the new <see cref="ApiKeyItem"/> after a key was created.</summary>
    public static readonly StyledProperty<ICommand?> KeyCreatedCommandProperty =
        AvaloniaProperty.Register<ApiKeys, ICommand?>(nameof(KeyCreatedCommand));

    /// <summary>Run with the <see cref="ApiKeyItem"/> after a key was revoked and left the list.</summary>
    public static readonly StyledProperty<ICommand?> KeyRevokedCommandProperty =
        AvaloniaProperty.Register<ApiKeys, ICommand?>(nameof(KeyRevokedCommand));

    /// <summary>The prefix of simulated secrets; {0} is "test" or "live" (used only while <see cref="GenerateAction"/> is null).</summary>
    public static readonly StyledProperty<string> SimulatedPrefixFormatProperty =
        AvaloniaProperty.Register<ApiKeys, string>(nameof(SimulatedPrefixFormat), "rk_{0}_");

    /// <summary>The creator recorded on simulated keys (used only while <see cref="GenerateAction"/> is null).</summary>
    public static readonly StyledProperty<string?> CurrentUserNameProperty =
        AvaloniaProperty.Register<ApiKeys, string?>(nameof(CurrentUserName));

    /// <summary>"Now" for the relative dates and the stale check; null reads the clock.</summary>
    public static readonly StyledProperty<DateTimeOffset?> ReferenceTimeProperty =
        AvaloniaProperty.Register<ApiKeys, DateTimeOffset?>(nameof(ReferenceTime));

    /// <summary>A key whose last request is older than this is marked as idle (30 days).</summary>
    public static readonly StyledProperty<TimeSpan> StaleAfterProperty =
        AvaloniaProperty.Register<ApiKeys, TimeSpan>(nameof(StaleAfter), TimeSpan.FromDays(30));

    /// <summary>How long the revoke button must be held (1.2 s).</summary>
    public static readonly StyledProperty<TimeSpan> RevokeHoldDurationProperty =
        AvaloniaProperty.Register<ApiKeys, TimeSpan>(nameof(RevokeHoldDuration), TimeSpan.FromMilliseconds(1200));

    /// <summary>How long a revoked row stays struck through before it folds away (0.9 s; longer when the server is slower).</summary>
    public static readonly StyledProperty<TimeSpan> RevokeExitDelayProperty =
        AvaloniaProperty.Register<ApiKeys, TimeSpan>(nameof(RevokeExitDelay), TimeSpan.FromMilliseconds(900));

    /// <summary>The button text of the empty state; null shows no button.</summary>
    public static readonly StyledProperty<string?> EmptyActionTextProperty =
        AvaloniaProperty.Register<ApiKeys, string?>(nameof(EmptyActionText));

    /// <summary>Run when the empty state's button is pressed; when null and <see cref="EmptyActionInvoked"/> is not handled, the form opens.</summary>
    public static readonly StyledProperty<ICommand?> EmptyActionCommandProperty =
        AvaloniaProperty.Register<ApiKeys, ICommand?>(nameof(EmptyActionCommand));

    /// <summary>The key whose details are open (two-way); null when every row is closed.</summary>
    public static readonly StyledProperty<ApiKeyItem?> ExpandedKeyProperty =
        AvaloniaProperty.Register<ApiKeys, ApiKeyItem?>(nameof(ExpandedKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Draws a bordered card with rounded corners (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<ApiKeys, bool>(nameof(IsFramed), true);

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<ApiKeyLabels> LabelsProperty =
        AvaloniaProperty.Register<ApiKeys, ApiKeyLabels>(nameof(Labels), new ApiKeyLabels());

    /// <summary>How many keys are listed (a revoked key counts until its row has left).</summary>
    public static readonly DirectProperty<ApiKeys, int> ActiveCountProperty =
        AvaloniaProperty.RegisterDirect<ApiKeys, int>(nameof(ActiveCount), o => o.ActiveCount);

    /// <summary>What the "Create key" panel shows.</summary>
    public static readonly DirectProperty<ApiKeys, ApiKeysPanelState> PanelStateProperty =
        AvaloniaProperty.RegisterDirect<ApiKeys, ApiKeysPanelState>(nameof(PanelState), o => o.PanelState);

    /// <summary>The error the form shows after a failed generate; null otherwise.</summary>
    public static readonly DirectProperty<ApiKeys, string?> GenerateErrorProperty =
        AvaloniaProperty.RegisterDirect<ApiKeys, string?>(nameof(GenerateError), o => o.GenerateError);

    #endregion

    #region Events

    /// <summary>Raised when a key was created and listed, before its secret shows.</summary>
    public static readonly RoutedEvent<ApiKeyCreatedEventArgs> KeyCreatedEvent =
        RoutedEvent.Register<ApiKeys, ApiKeyCreatedEventArgs>(nameof(KeyCreated), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="GenerateAction"/> threw; the form stays open with the error.</summary>
    public static readonly RoutedEvent<ApiKeyCreateFailedEventArgs> KeyCreateFailedEvent =
        RoutedEvent.Register<ApiKeys, ApiKeyCreateFailedEventArgs>(nameof(KeyCreateFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when the one-time secret was copied.</summary>
    public static readonly RoutedEvent<ApiKeySecretCopiedEventArgs> SecretCopiedEvent =
        RoutedEvent.Register<ApiKeys, ApiKeySecretCopiedEventArgs>(nameof(SecretCopied), RoutingStrategies.Bubble);

    /// <summary>Raised when a revoke is confirmed, before anything changes; set <see cref="ApiKeyRevokingEventArgs.Cancel"/> to keep the key.</summary>
    public static readonly RoutedEvent<ApiKeyRevokingEventArgs> KeyRevokingEvent =
        RoutedEvent.Register<ApiKeys, ApiKeyRevokingEventArgs>(nameof(KeyRevoking), RoutingStrategies.Bubble);

    /// <summary>Raised when a key was revoked and its row left the list.</summary>
    public static readonly RoutedEvent<ApiKeyRevokedEventArgs> KeyRevokedEvent =
        RoutedEvent.Register<ApiKeys, ApiKeyRevokedEventArgs>(nameof(KeyRevoked), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="RevokeAction"/> threw; the row is restored.</summary>
    public static readonly RoutedEvent<ApiKeyRevokeFailedEventArgs> KeyRevokeFailedEvent =
        RoutedEvent.Register<ApiKeys, ApiKeyRevokeFailedEventArgs>(nameof(KeyRevokeFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when a row's details open or close.</summary>
    public static readonly RoutedEvent<ApiKeyExpandedEventArgs> ExpandedKeyChangedEvent =
        RoutedEvent.Register<ApiKeys, ApiKeyExpandedEventArgs>(nameof(ExpandedKeyChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="PanelState"/> changes.</summary>
    public static readonly RoutedEvent<ApiKeysPanelStateChangedEventArgs> PanelStateChangedEvent =
        RoutedEvent.Register<ApiKeys, ApiKeysPanelStateChangedEventArgs>(nameof(PanelStateChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the empty state's button is pressed; mark it handled to keep the form closed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> EmptyActionInvokedEvent =
        RoutedEvent.Register<ApiKeys, RoutedEventArgs>(nameof(EmptyActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a key was created and listed, before its secret shows.</summary>
    public event EventHandler<ApiKeyCreatedEventArgs>? KeyCreated
    {
        add => AddHandler(KeyCreatedEvent, value);
        remove => RemoveHandler(KeyCreatedEvent, value);
    }

    /// <summary>Raised when <see cref="GenerateAction"/> threw; the form stays open with the error.</summary>
    public event EventHandler<ApiKeyCreateFailedEventArgs>? KeyCreateFailed
    {
        add => AddHandler(KeyCreateFailedEvent, value);
        remove => RemoveHandler(KeyCreateFailedEvent, value);
    }

    /// <summary>Raised when the one-time secret was copied.</summary>
    public event EventHandler<ApiKeySecretCopiedEventArgs>? SecretCopied
    {
        add => AddHandler(SecretCopiedEvent, value);
        remove => RemoveHandler(SecretCopiedEvent, value);
    }

    /// <summary>Raised when a revoke is confirmed, before anything changes; set <see cref="ApiKeyRevokingEventArgs.Cancel"/> to keep the key.</summary>
    public event EventHandler<ApiKeyRevokingEventArgs>? KeyRevoking
    {
        add => AddHandler(KeyRevokingEvent, value);
        remove => RemoveHandler(KeyRevokingEvent, value);
    }

    /// <summary>Raised when a key was revoked and its row left the list.</summary>
    public event EventHandler<ApiKeyRevokedEventArgs>? KeyRevoked
    {
        add => AddHandler(KeyRevokedEvent, value);
        remove => RemoveHandler(KeyRevokedEvent, value);
    }

    /// <summary>Raised when <see cref="RevokeAction"/> threw; the row is restored.</summary>
    public event EventHandler<ApiKeyRevokeFailedEventArgs>? KeyRevokeFailed
    {
        add => AddHandler(KeyRevokeFailedEvent, value);
        remove => RemoveHandler(KeyRevokeFailedEvent, value);
    }

    /// <summary>Raised when a row's details open or close.</summary>
    public event EventHandler<ApiKeyExpandedEventArgs>? ExpandedKeyChanged
    {
        add => AddHandler(ExpandedKeyChangedEvent, value);
        remove => RemoveHandler(ExpandedKeyChangedEvent, value);
    }

    /// <summary>Raised when <see cref="PanelState"/> changes.</summary>
    public event EventHandler<ApiKeysPanelStateChangedEventArgs>? PanelStateChanged
    {
        add => AddHandler(PanelStateChangedEvent, value);
        remove => RemoveHandler(PanelStateChangedEvent, value);
    }

    /// <summary>Raised when the empty state's button is pressed; mark it handled to keep the form closed.</summary>
    public event EventHandler<RoutedEventArgs>? EmptyActionInvoked
    {
        add => AddHandler(EmptyActionInvokedEvent, value);
        remove => RemoveHandler(EmptyActionInvokedEvent, value);
    }

    #endregion

    private int _activeCount;
    private ApiKeysPanelState _panelState;
    private string? _generateError;

    /// <summary>The keys, newest first, when no <see cref="KeysSource"/> is set. Created keys are inserted at the top and revoked ones removed.</summary>
    [Content]
    public AvaloniaList<ApiKeyItem> Keys { get; } = new();

    /// <summary>The scopes the form offers, when no <see cref="ScopesSource"/> is set.</summary>
    public AvaloniaList<ApiKeyScope> Scopes { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="KeysSourceProperty"/>
    public IEnumerable? KeysSource { get => GetValue(KeysSourceProperty); set => SetValue(KeysSourceProperty, value); }

    /// <inheritdoc cref="ScopesSourceProperty"/>
    public IEnumerable? ScopesSource { get => GetValue(ScopesSourceProperty); set => SetValue(ScopesSourceProperty, value); }

    /// <inheritdoc cref="IsCreateOpenProperty"/>
    public bool IsCreateOpen { get => GetValue(IsCreateOpenProperty); set => SetValue(IsCreateOpenProperty, value); }

    /// <inheritdoc cref="DefaultEnvironmentProperty"/>
    public ApiKeyEnvironment DefaultEnvironment
    {
        get => GetValue(DefaultEnvironmentProperty);
        set => SetValue(DefaultEnvironmentProperty, value);
    }

    /// <inheritdoc cref="NameMaxLengthProperty"/>
    public int NameMaxLength { get => GetValue(NameMaxLengthProperty); set => SetValue(NameMaxLengthProperty, value); }

    /// <inheritdoc cref="GenerateActionProperty"/>
    public Func<ApiKeyRequest, CancellationToken, Task<ApiKeyCreated>>? GenerateAction
    {
        get => GetValue(GenerateActionProperty);
        set => SetValue(GenerateActionProperty, value);
    }

    /// <inheritdoc cref="RevokeActionProperty"/>
    public Func<ApiKeyItem, CancellationToken, Task>? RevokeAction
    {
        get => GetValue(RevokeActionProperty);
        set => SetValue(RevokeActionProperty, value);
    }

    /// <inheritdoc cref="KeyCreatedCommandProperty"/>
    public ICommand? KeyCreatedCommand { get => GetValue(KeyCreatedCommandProperty); set => SetValue(KeyCreatedCommandProperty, value); }

    /// <inheritdoc cref="KeyRevokedCommandProperty"/>
    public ICommand? KeyRevokedCommand { get => GetValue(KeyRevokedCommandProperty); set => SetValue(KeyRevokedCommandProperty, value); }

    /// <inheritdoc cref="SimulatedPrefixFormatProperty"/>
    public string SimulatedPrefixFormat
    {
        get => GetValue(SimulatedPrefixFormatProperty);
        set => SetValue(SimulatedPrefixFormatProperty, value);
    }

    /// <inheritdoc cref="CurrentUserNameProperty"/>
    public string? CurrentUserName { get => GetValue(CurrentUserNameProperty); set => SetValue(CurrentUserNameProperty, value); }

    /// <inheritdoc cref="ReferenceTimeProperty"/>
    public DateTimeOffset? ReferenceTime { get => GetValue(ReferenceTimeProperty); set => SetValue(ReferenceTimeProperty, value); }

    /// <inheritdoc cref="StaleAfterProperty"/>
    public TimeSpan StaleAfter { get => GetValue(StaleAfterProperty); set => SetValue(StaleAfterProperty, value); }

    /// <inheritdoc cref="RevokeHoldDurationProperty"/>
    public TimeSpan RevokeHoldDuration
    {
        get => GetValue(RevokeHoldDurationProperty);
        set => SetValue(RevokeHoldDurationProperty, value);
    }

    /// <inheritdoc cref="RevokeExitDelayProperty"/>
    public TimeSpan RevokeExitDelay { get => GetValue(RevokeExitDelayProperty); set => SetValue(RevokeExitDelayProperty, value); }

    /// <inheritdoc cref="EmptyActionTextProperty"/>
    public string? EmptyActionText { get => GetValue(EmptyActionTextProperty); set => SetValue(EmptyActionTextProperty, value); }

    /// <inheritdoc cref="EmptyActionCommandProperty"/>
    public ICommand? EmptyActionCommand { get => GetValue(EmptyActionCommandProperty); set => SetValue(EmptyActionCommandProperty, value); }

    /// <inheritdoc cref="ExpandedKeyProperty"/>
    public ApiKeyItem? ExpandedKey { get => GetValue(ExpandedKeyProperty); set => SetValue(ExpandedKeyProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ApiKeyLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ActiveCountProperty"/>
    public int ActiveCount { get => _activeCount; private set => SetAndRaise(ActiveCountProperty, ref _activeCount, value); }

    /// <inheritdoc cref="PanelStateProperty"/>
    public ApiKeysPanelState PanelState { get => _panelState; private set => SetAndRaise(PanelStateProperty, ref _panelState, value); }

    /// <inheritdoc cref="GenerateErrorProperty"/>
    public string? GenerateError { get => _generateError; private set => SetAndRaise(GenerateErrorProperty, ref _generateError, value); }
}
