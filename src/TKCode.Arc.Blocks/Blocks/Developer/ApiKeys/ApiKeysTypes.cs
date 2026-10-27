using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where an API key works: against sandbox data or against real data.</summary>
public enum ApiKeyEnvironment
{
    /// <summary>A test key: sandbox data only (neutral badge).</summary>
    Test,

    /// <summary>A live key: real data (success badge).</summary>
    Live
}

/// <summary>What the "Create key" panel of <see cref="ApiKeys"/> shows.</summary>
public enum ApiKeysPanelState
{
    /// <summary>The panel is the "Create key" pill.</summary>
    Closed,

    /// <summary>The creation form is open.</summary>
    Form,

    /// <summary>The form was submitted and <see cref="ApiKeys.GenerateAction"/> is running.</summary>
    Generating,

    /// <summary>The new key's secret is shown, once.</summary>
    Reveal
}

/// <summary>Why the "Create key" panel of <see cref="ApiKeys"/> closed.</summary>
public enum ApiKeysCloseReason
{
    /// <summary>The form's Cancel button.</summary>
    Cancel,

    /// <summary>The reveal's Done button.</summary>
    Done,

    /// <summary>The close (×) toggle on the panel's face.</summary>
    Toggle,

    /// <summary>Escape inside the panel.</summary>
    Escape,

    /// <summary>Code: <see cref="ApiKeys.CloseCreate"/> or <see cref="ApiKeys.IsCreateOpen"/> set to false.</summary>
    Programmatic
}

/// <summary>
/// One API key in an <see cref="ApiKeys"/> list. It never holds the secret itself: only the prefix and the last four
/// characters that identify it.
/// </summary>
public class ApiKeyItem : AvaloniaObject
{
    /// <summary>A stable identifier (events, automation ids).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<ApiKeyItem, string>(nameof(Key), string.Empty);

    /// <summary>The name people gave the key; the row's title.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ApiKeyItem, string>(nameof(Name), string.Empty);

    /// <summary>Test or live; shown as a badge next to the name.</summary>
    public static readonly StyledProperty<ApiKeyEnvironment> EnvironmentProperty =
        AvaloniaProperty.Register<ApiKeyItem, ApiKeyEnvironment>(nameof(Environment));

    /// <summary>The visible start of the secret ("rk_live_").</summary>
    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<ApiKeyItem, string>(nameof(Prefix), string.Empty);

    /// <summary>The last four characters of the secret.</summary>
    public static readonly StyledProperty<string> LastFourProperty =
        AvaloniaProperty.Register<ApiKeyItem, string>(nameof(LastFour), string.Empty);

    /// <summary>When the key last signed a request; null means it was never used.</summary>
    public static readonly StyledProperty<DateTimeOffset?> LastUsedAtProperty =
        AvaloniaProperty.Register<ApiKeyItem, DateTimeOffset?>(nameof(LastUsedAt));

    /// <summary>Replaces the generated "Used …" line when set.</summary>
    public static readonly StyledProperty<string?> LastUsedTextProperty =
        AvaloniaProperty.Register<ApiKeyItem, string?>(nameof(LastUsedText));

    /// <summary>When the key was created.</summary>
    public static readonly StyledProperty<DateTimeOffset> CreatedAtProperty =
        AvaloniaProperty.Register<ApiKeyItem, DateTimeOffset>(nameof(CreatedAt));

    /// <summary>The name of the person who created the key (detail, next to an avatar with their initials).</summary>
    public static readonly StyledProperty<string?> CreatedByProperty =
        AvaloniaProperty.Register<ApiKeyItem, string?>(nameof(CreatedBy));

    /// <summary>The "Last request" fact (when and where from); null derives it from <see cref="LastUsedAt"/>.</summary>
    public static readonly StyledProperty<string?> LastRequestTextProperty =
        AvaloniaProperty.Register<ApiKeyItem, string?>(nameof(LastRequestText));

    /// <summary>How many requests the key signed in the last 30 days.</summary>
    public static readonly StyledProperty<long> RequestsLast30DaysProperty =
        AvaloniaProperty.Register<ApiKeyItem, long>(nameof(RequestsLast30Days));

    /// <summary>Anything the app wants to keep with the key.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<ApiKeyItem, object?>(nameof(Tag));

    /// <summary>The prefix, four dots and the last four characters ("rk_live_••••7c3e").</summary>
    public static readonly DirectProperty<ApiKeyItem, string> MaskedTextProperty =
        AvaloniaProperty.RegisterDirect<ApiKeyItem, string>(nameof(MaskedText), o => o.MaskedText);

    /// <summary>True from the moment a revoke is confirmed until the row leaves (or the revoke fails).</summary>
    public static readonly DirectProperty<ApiKeyItem, bool> IsRevokedProperty =
        AvaloniaProperty.RegisterDirect<ApiKeyItem, bool>(nameof(IsRevoked), o => o.IsRevoked);

    /// <summary>True for a key just created, until its one-time secret panel closes (the row stays highlighted).</summary>
    public static readonly DirectProperty<ApiKeyItem, bool> IsFreshProperty =
        AvaloniaProperty.RegisterDirect<ApiKeyItem, bool>(nameof(IsFresh), o => o.IsFresh);

    private string _maskedText = string.Empty;
    private bool _isRevoked;
    private bool _isFresh;

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="EnvironmentProperty"/>
    public ApiKeyEnvironment Environment { get => GetValue(EnvironmentProperty); set => SetValue(EnvironmentProperty, value); }

    /// <inheritdoc cref="PrefixProperty"/>
    public string Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <inheritdoc cref="LastFourProperty"/>
    public string LastFour { get => GetValue(LastFourProperty); set => SetValue(LastFourProperty, value); }

    /// <inheritdoc cref="LastUsedAtProperty"/>
    public DateTimeOffset? LastUsedAt { get => GetValue(LastUsedAtProperty); set => SetValue(LastUsedAtProperty, value); }

    /// <inheritdoc cref="LastUsedTextProperty"/>
    public string? LastUsedText { get => GetValue(LastUsedTextProperty); set => SetValue(LastUsedTextProperty, value); }

    /// <inheritdoc cref="CreatedAtProperty"/>
    public DateTimeOffset CreatedAt { get => GetValue(CreatedAtProperty); set => SetValue(CreatedAtProperty, value); }

    /// <inheritdoc cref="CreatedByProperty"/>
    public string? CreatedBy { get => GetValue(CreatedByProperty); set => SetValue(CreatedByProperty, value); }

    /// <inheritdoc cref="LastRequestTextProperty"/>
    public string? LastRequestText { get => GetValue(LastRequestTextProperty); set => SetValue(LastRequestTextProperty, value); }

    /// <inheritdoc cref="RequestsLast30DaysProperty"/>
    public long RequestsLast30Days { get => GetValue(RequestsLast30DaysProperty); set => SetValue(RequestsLast30DaysProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The scopes the key was granted, in order (shown as chips).</summary>
    public AvaloniaList<string> Scopes { get; } = new();

    /// <inheritdoc cref="MaskedTextProperty"/>
    public string MaskedText { get => _maskedText; private set => SetAndRaise(MaskedTextProperty, ref _maskedText, value); }

    /// <inheritdoc cref="IsRevokedProperty"/>
    public bool IsRevoked { get => _isRevoked; internal set => SetAndRaise(IsRevokedProperty, ref _isRevoked, value); }

    /// <inheritdoc cref="IsFreshProperty"/>
    public bool IsFresh { get => _isFresh; internal set => SetAndRaise(IsFreshProperty, ref _isFresh, value); }

    /// <summary>Raised when the scopes change (the row redraws its chips).</summary>
    internal event EventHandler? ScopesChanged;

    /// <summary>Creates an empty key.</summary>
    public ApiKeyItem()
    {
        Scopes.CollectionChanged += (_, _) => ScopesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PrefixProperty || change.Property == LastFourProperty)
        {
            MaskedText = DeveloperText.Mask(Prefix, LastFour);
        }
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>A scope that the "Create key" form of <see cref="ApiKeys"/> offers.</summary>
public class ApiKeyScope : AvaloniaObject
{
    /// <summary>The scope's identifier, shown in a monospaced face ("orders:write").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ApiKeyScope, string>(nameof(Name), string.Empty);

    /// <summary>One line on what the scope allows.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ApiKeyScope, string?>(nameof(Description));

    /// <summary>Whether a fresh form starts with this scope ticked.</summary>
    public static readonly StyledProperty<bool> IsDefaultProperty =
        AvaloniaProperty.Register<ApiKeyScope, bool>(nameof(IsDefault));

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsDefaultProperty"/>
    public bool IsDefault { get => GetValue(IsDefaultProperty); set => SetValue(IsDefaultProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>What the person asked for in the "Create key" form: passed to <see cref="ApiKeys.GenerateAction"/>.</summary>
public sealed class ApiKeyRequest
{
    /// <summary>Creates a request.</summary>
    public ApiKeyRequest(string name, ApiKeyEnvironment environment, IReadOnlyList<string> scopes, int attempt)
    {
        Name = name;
        Environment = environment;
        Scopes = scopes;
        Attempt = attempt;
    }

    /// <summary>The key's name, trimmed.</summary>
    public string Name { get; }

    /// <summary>Test or live.</summary>
    public ApiKeyEnvironment Environment { get; }

    /// <summary>The ticked scopes, in the form's order.</summary>
    public IReadOnlyList<string> Scopes { get; }

    /// <summary>1 for the first submit of an opened form, 2 for a retry after a failure, and so on.</summary>
    public int Attempt { get; }
}

/// <summary>A key the server created: the row to add and the secret to show once.</summary>
public sealed class ApiKeyCreated
{
    /// <summary>Creates a result.</summary>
    public ApiKeyCreated(ApiKeyItem item, string secret)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Secret = secret ?? throw new ArgumentNullException(nameof(secret));
    }

    /// <summary>The new key's row. Fill <see cref="ApiKeyItem.Prefix"/> and <see cref="ApiKeyItem.LastFour"/> from the secret.</summary>
    public ApiKeyItem Item { get; }

    /// <summary>The full secret. The block shows it once and drops it when the panel closes.</summary>
    public string Secret { get; }
}

/// <summary>Arguments of <see cref="ApiKeys.KeyCreated"/>.</summary>
public sealed class ApiKeyCreatedEventArgs(RoutedEvent routedEvent, object source, ApiKeyItem item, ApiKeyRequest request)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The new key (already in the list).</summary>
    public ApiKeyItem Item { get; } = item;

    /// <summary>What was asked for.</summary>
    public ApiKeyRequest Request { get; } = request;
}

/// <summary>Arguments of <see cref="ApiKeys.KeyCreateFailed"/>.</summary>
public sealed class ApiKeyCreateFailedEventArgs(RoutedEvent routedEvent, object source, ApiKeyRequest request, Exception exception)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>What was asked for.</summary>
    public ApiKeyRequest Request { get; } = request;

    /// <summary>What <see cref="ApiKeys.GenerateAction"/> threw.</summary>
    public Exception Exception { get; } = exception;
}

/// <summary>Arguments of <see cref="ApiKeys.KeyRevoking"/>: set <see cref="Cancel"/> to keep the key.</summary>
public sealed class ApiKeyRevokingEventArgs(RoutedEvent routedEvent, object source, ApiKeyItem item)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The key about to be revoked.</summary>
    public ApiKeyItem Item { get; } = item;

    /// <summary>Set to true to keep the key; the hold button resets.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <see cref="ApiKeys.KeyRevoked"/>.</summary>
public sealed class ApiKeyRevokedEventArgs(RoutedEvent routedEvent, object source, ApiKeyItem item, int index)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The revoked key (no longer listed).</summary>
    public ApiKeyItem Item { get; } = item;

    /// <summary>Where the key was in the list.</summary>
    public int Index { get; } = index;
}

/// <summary>Arguments of <see cref="ApiKeys.KeyRevokeFailed"/>.</summary>
public sealed class ApiKeyRevokeFailedEventArgs(RoutedEvent routedEvent, object source, ApiKeyItem item, Exception exception)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The key that is still active.</summary>
    public ApiKeyItem Item { get; } = item;

    /// <summary>What <see cref="ApiKeys.RevokeAction"/> threw.</summary>
    public Exception Exception { get; } = exception;
}

/// <summary>Arguments of <see cref="ApiKeys.SecretCopied"/>.</summary>
public sealed class ApiKeySecretCopiedEventArgs(RoutedEvent routedEvent, object source, ApiKeyItem item)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The key whose secret was copied.</summary>
    public ApiKeyItem Item { get; } = item;
}

/// <summary>Arguments of <see cref="ApiKeys.ExpandedKeyChanged"/>.</summary>
public sealed class ApiKeyExpandedEventArgs(RoutedEvent routedEvent, object source, ApiKeyItem? oldItem, ApiKeyItem? newItem)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The key whose details were open before; null for none.</summary>
    public ApiKeyItem? OldItem { get; } = oldItem;

    /// <summary>The key whose details are open now; null for none.</summary>
    public ApiKeyItem? NewItem { get; } = newItem;
}

/// <summary>Arguments of <see cref="ApiKeys.PanelStateChanged"/>.</summary>
public sealed class ApiKeysPanelStateChangedEventArgs(
    RoutedEvent routedEvent,
    object source,
    ApiKeysPanelState oldState,
    ApiKeysPanelState newState,
    ApiKeysCloseReason? closeReason)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>What the panel showed before.</summary>
    public ApiKeysPanelState OldState { get; } = oldState;

    /// <summary>What it shows now.</summary>
    public ApiKeysPanelState NewState { get; } = newState;

    /// <summary>Why it closed, when <see cref="NewState"/> is <see cref="ApiKeysPanelState.Closed"/>; otherwise null.</summary>
    public ApiKeysCloseReason? CloseReason { get; } = closeReason;
}
