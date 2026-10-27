using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which table <see cref="EnvVariables"/> shows.</summary>
public enum EnvVariablesView
{
    /// <summary>Every variable with its environments as a matrix of switches.</summary>
    Variables,

    /// <summary>Two environments side by side, by fingerprint.</summary>
    Compare
}

/// <summary>Where the redeploy banner of <see cref="EnvVariables"/> stands.</summary>
public enum EnvRedeployState
{
    /// <summary>Nothing waits for a deployment; no banner.</summary>
    None,

    /// <summary>Changes wait for a deployment; the banner offers to redeploy.</summary>
    Pending,

    /// <summary>A deployment runs (<see cref="EnvVariables.RedeployAction"/> is awaited).</summary>
    Running,

    /// <summary>The deployment finished; the banner confirms it, then closes.</summary>
    Done,

    /// <summary>The deployment failed; the changes are kept and the banner offers to try again.</summary>
    Failed
}

/// <summary>The two forms of the add panel of <see cref="EnvVariables"/>.</summary>
public enum EnvAddMode
{
    /// <summary>One key and one value.</summary>
    Single,

    /// <summary>A pasted .env file, parsed line by line.</summary>
    Dotenv
}

/// <summary>What changed the variables of <see cref="EnvVariables"/>.</summary>
public enum EnvVariablesChangeReason
{
    /// <summary>A variable was added from the add panel.</summary>
    Added,

    /// <summary>Variables were imported from a .env file.</summary>
    Imported,

    /// <summary>A value (or its sensitivity) was saved in the inline editor.</summary>
    Edited,

    /// <summary>A variable was removed.</summary>
    Removed,

    /// <summary>A variable was turned on or off for an environment.</summary>
    ScopeToggled,

    /// <summary>A value was copied to another environment from the compare view.</summary>
    CopiedAcross
}

/// <summary>An environment of <see cref="EnvVariables"/>: a column of the matrix and a choice in the pills and selects.</summary>
public sealed class EnvEnvironment : AvaloniaObject
{
    /// <summary>The stable identifier that <see cref="EnvVariable.Environments"/> refers to ("production").</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<EnvEnvironment, string>(nameof(Key), string.Empty);

    /// <summary>The name people read ("Production").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<EnvEnvironment, string>(nameof(Name), string.Empty);

    /// <summary>Creates an empty environment (set <see cref="Key"/> and <see cref="Name"/>).</summary>
    public EnvEnvironment()
    {
    }

    /// <summary>Creates an environment.</summary>
    public EnvEnvironment(string key, string name)
    {
        Key = key;
        Name = name;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <summary>The name, or the key when the name is empty.</summary>
    public override string ToString() => string.IsNullOrEmpty(Name) ? Key : Name;
}

/// <summary>
/// One row of <see cref="EnvVariables"/>: a value and the environments it applies to. A key can have several rows (one
/// value for development and staging, another for production), but each environment is covered by at most one row of a
/// key. The value stays out of the visual tree until someone reveals it; a sensitive value is never shown.
/// </summary>
public sealed class EnvVariable : AvaloniaObject
{
    /// <summary>A stable identifier (a new GUID by default).</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<EnvVariable, string>(nameof(Id), string.Empty);

    /// <summary>The variable's name (letters, digits and underscores, not starting with a digit).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<EnvVariable, string>(nameof(Key), string.Empty);

    /// <summary>
    /// The value. Shown only while revealed and copied only from there; a sensitive row may leave it null and give a
    /// <see cref="Fingerprint"/> instead. Fill it lazily in <see cref="EnvVariables.ValueRevealed"/> if you prefer.
    /// </summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<EnvVariable, string?>(nameof(Value));

    /// <summary>Whether the value is write-only: it can be replaced but never revealed, copied or made readable again.</summary>
    public static readonly StyledProperty<bool> IsSensitiveProperty =
        AvaloniaProperty.Register<EnvVariable, bool>(nameof(IsSensitive));

    /// <summary>When the value last changed (the second line reads "Updated Mar 4 by …").</summary>
    public static readonly StyledProperty<DateTimeOffset?> UpdatedAtProperty =
        AvaloniaProperty.Register<EnvVariable, DateTimeOffset?>(nameof(UpdatedAt));

    /// <summary>Who changed it last.</summary>
    public static readonly StyledProperty<string?> UpdatedByProperty =
        AvaloniaProperty.Register<EnvVariable, string?>(nameof(UpdatedBy));

    /// <summary>
    /// The short fingerprint the compare view shows instead of the value (equal values, equal fingerprints). Null computes
    /// it from <see cref="Value"/> (the first 6 hex digits of its SHA-256); give your own for values you do not load.
    /// </summary>
    public static readonly StyledProperty<string?> FingerprintProperty =
        AvaloniaProperty.Register<EnvVariable, string?>(nameof(Fingerprint));

    /// <summary>Whether the row changed since the last deployment (a dot after the key and "not deployed yet").</summary>
    public static readonly StyledProperty<bool> IsPendingDeployProperty =
        AvaloniaProperty.Register<EnvVariable, bool>(nameof(IsPendingDeploy));

    private string? _computedFingerprint;
    private bool _fingerprintValid;

    /// <summary>Creates a row with a new <see cref="Id"/>.</summary>
    public EnvVariable()
    {
        Id = Guid.NewGuid().ToString("N");
        Environments = new AvaloniaList<string>();
    }

    /// <summary>Creates a row for <paramref name="key"/> with <paramref name="value"/> in <paramref name="environments"/>.</summary>
    public EnvVariable(string key, string? value, params string[] environments) : this()
    {
        Key = key;
        Value = value;
        Environments.AddRange(environments);
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="IsSensitiveProperty"/>
    public bool IsSensitive { get => GetValue(IsSensitiveProperty); set => SetValue(IsSensitiveProperty, value); }

    /// <inheritdoc cref="UpdatedAtProperty"/>
    public DateTimeOffset? UpdatedAt { get => GetValue(UpdatedAtProperty); set => SetValue(UpdatedAtProperty, value); }

    /// <inheritdoc cref="UpdatedByProperty"/>
    public string? UpdatedBy { get => GetValue(UpdatedByProperty); set => SetValue(UpdatedByProperty, value); }

    /// <inheritdoc cref="FingerprintProperty"/>
    public string? Fingerprint { get => GetValue(FingerprintProperty); set => SetValue(FingerprintProperty, value); }

    /// <inheritdoc cref="IsPendingDeployProperty"/>
    public bool IsPendingDeploy { get => GetValue(IsPendingDeployProperty); set => SetValue(IsPendingDeployProperty, value); }

    /// <summary>The keys of the environments this row applies to (<see cref="EnvEnvironment.Key"/>).</summary>
    public AvaloniaList<string> Environments { get; }

    /// <summary>
    /// <see cref="Fingerprint"/>, or the one computed from <see cref="Value"/> (cached; computed outside the visual tree);
    /// null when neither is known.
    /// </summary>
    public string? EffectiveFingerprint
    {
        get
        {
            if (Fingerprint is { Length: > 0 } given)
            {
                return given;
            }
            if (!_fingerprintValid)
            {
                _computedFingerprint = Value is null ? null : EnvVariableFingerprint.Compute(Value);
                _fingerprintValid = true;
            }
            return _computedFingerprint;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            _fingerprintValid = false;
            _computedFingerprint = null;
        }
    }

    /// <summary>The key (for debugging).</summary>
    public override string ToString() => Key;
}

/// <summary>What a redeploy covers, handed to <see cref="EnvVariables.RedeployAction"/> and the redeploy events.</summary>
public sealed class EnvRedeployRequest
{
    /// <summary>Creates a request.</summary>
    public EnvRedeployRequest(IReadOnlyList<string> environments, int changeCount, IReadOnlyList<EnvVariable> changed)
    {
        Environments = environments;
        ChangeCount = changeCount;
        Changed = changed;
    }

    /// <summary>The keys of the environments that keep old values until they are deployed, in column order.</summary>
    public IReadOnlyList<string> Environments { get; }

    /// <summary>How many changes the banner counted (every toggle, save, add, import, removal or copy counts once).</summary>
    public int ChangeCount { get; }

    /// <summary>The rows still present that changed (removed rows are not listed).</summary>
    public IReadOnlyList<EnvVariable> Changed { get; }
}

/// <summary>Arguments of <see cref="EnvVariables.VariablesChanged"/>: one change and every row after it.</summary>
public sealed class EnvVariablesChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnvVariablesChangedEventArgs(RoutedEvent routedEvent, EnvVariablesChangeReason reason,
        IReadOnlyList<EnvVariable> affected, IReadOnlyList<EnvVariable> snapshot) : base(routedEvent)
    {
        Reason = reason;
        Affected = affected;
        Snapshot = snapshot;
    }

    /// <summary>What happened.</summary>
    public EnvVariablesChangeReason Reason { get; }

    /// <summary>The rows the change touched (a removed row is listed although it is gone).</summary>
    public IReadOnlyList<EnvVariable> Affected { get; }

    /// <summary>Every row after the change, in display order.</summary>
    public IReadOnlyList<EnvVariable> Snapshot { get; }
}

/// <summary>Arguments of <see cref="EnvVariables.VariableAdded"/>, <see cref="EnvVariables.VariableSaved"/> and <see cref="EnvVariables.VariableRemoved"/>.</summary>
public sealed class EnvVariableEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnvVariableEventArgs(RoutedEvent routedEvent, EnvVariable variable, int index, bool valueChanged = false,
        bool becameSensitive = false) : base(routedEvent)
    {
        Variable = variable;
        Index = index;
        ValueChanged = valueChanged;
        BecameSensitive = becameSensitive;
    }

    /// <summary>The row.</summary>
    public EnvVariable Variable { get; }

    /// <summary>Its index in <see cref="EnvVariables.Variables"/> (before a removal; after an add).</summary>
    public int Index { get; }

    /// <summary>Saved only: whether the value changed (the old value is never passed on).</summary>
    public bool ValueChanged { get; }

    /// <summary>Saved only: whether the save made the value write-only.</summary>
    public bool BecameSensitive { get; }
}

/// <summary>Arguments of <see cref="EnvVariables.ScopeToggled"/> and <see cref="EnvVariables.ScopeConflict"/>.</summary>
public sealed class EnvScopeEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnvScopeEventArgs(RoutedEvent routedEvent, EnvVariable variable, string environment, bool isOn,
        EnvVariable? blockingVariable = null) : base(routedEvent)
    {
        Variable = variable;
        Environment = environment;
        IsOn = isOn;
        BlockingVariable = blockingVariable;
    }

    /// <summary>The row whose switch was pressed.</summary>
    public EnvVariable Variable { get; }

    /// <summary>The environment's key.</summary>
    public string Environment { get; }

    /// <summary>Toggled: the new state. Conflict: the state that was asked for (always true).</summary>
    public bool IsOn { get; }

    /// <summary>Conflict only: the other row of the same key that already covers the environment.</summary>
    public EnvVariable? BlockingVariable { get; }
}

/// <summary>Arguments of <see cref="EnvVariables.ValueRevealed"/>.</summary>
public sealed class EnvValueRevealedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnvValueRevealedEventArgs(RoutedEvent routedEvent, EnvVariable variable, bool isRevealed) : base(routedEvent)
    {
        Variable = variable;
        IsRevealed = isRevealed;
    }

    /// <summary>The row.</summary>
    public EnvVariable Variable { get; }

    /// <summary>True when the value is about to show (set <see cref="EnvVariable.Value"/> now to load it lazily); false when it hid.</summary>
    public bool IsRevealed { get; }
}

/// <summary>Arguments of <see cref="EnvVariables.VariablesImported"/>.</summary>
public sealed class EnvVariablesImportedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnvVariablesImportedEventArgs(RoutedEvent routedEvent, IReadOnlyList<EnvVariable> added,
        IReadOnlyList<EnvVariable> replaced, IReadOnlyList<string> environments, IReadOnlyList<int> skippedLines) : base(routedEvent)
    {
        Added = added;
        Replaced = replaced;
        Environments = environments;
        SkippedLines = skippedLines;
    }

    /// <summary>The new rows.</summary>
    public IReadOnlyList<EnvVariable> Added { get; }

    /// <summary>Existing rows that gave up the imported environments (a row left with none is removed).</summary>
    public IReadOnlyList<EnvVariable> Replaced { get; }

    /// <summary>The environments imported into.</summary>
    public IReadOnlyList<string> Environments { get; }

    /// <summary>The 1-based numbers of the lines that were skipped (invalid, or set again later).</summary>
    public IReadOnlyList<int> SkippedLines { get; }
}

/// <summary>Arguments of <see cref="EnvVariables.ValueCopied"/>.</summary>
public sealed class EnvValueCopiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnvValueCopiedEventArgs(RoutedEvent routedEvent, EnvVariable variable, string from, string to) : base(routedEvent)
    {
        Variable = variable;
        From = from;
        To = to;
    }

    /// <summary>The row that now also covers <see cref="To"/>.</summary>
    public EnvVariable Variable { get; }

    /// <summary>The environment the value came from.</summary>
    public string From { get; }

    /// <summary>The environment it was copied to.</summary>
    public string To { get; }
}

/// <summary>Arguments of <see cref="EnvVariables.RedeployRequested"/>, <see cref="EnvVariables.Redeployed"/> and <see cref="EnvVariables.RedeployFailed"/>.</summary>
public sealed class EnvRedeployEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public EnvRedeployEventArgs(RoutedEvent routedEvent, EnvRedeployRequest request, Exception? exception = null) : base(routedEvent)
    {
        Request = request;
        Exception = exception;
    }

    /// <summary>What the deployment covers.</summary>
    public EnvRedeployRequest Request { get; }

    /// <summary>Failed only: what went wrong.</summary>
    public Exception? Exception { get; }

    /// <summary>Requested only: set to true to stop the deployment before it starts.</summary>
    public bool Cancel { get; set; }
}
