using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Where an integration stands in <see cref="Integrations"/>.</summary>
public enum IntegrationStatus
{
    /// <summary>Not connected: the row offers Connect.</summary>
    Idle,

    /// <summary>Waiting for the other service to grant access (the connect action is running).</summary>
    Connecting,

    /// <summary>Connected: the row offers its settings and the drawer can disconnect it.</summary>
    Connected,

    /// <summary>The last connect attempt failed: the row offers Retry and the drawer shows why.</summary>
    Error
}

/// <summary>What a permission lets the integration do; it picks the permission's icon.</summary>
public enum IntegrationScopeKind
{
    /// <summary>Reads data (an eye).</summary>
    Read,

    /// <summary>Creates or changes data (a pen).</summary>
    Write,

    /// <summary>Keeps data in step on a schedule (two turning arrows).</summary>
    Sync,

    /// <summary>Manages settings or people (a shield).</summary>
    Admin
}

/// <summary>
/// One app in <see cref="Integrations"/>: its name, category, one-line description, mark, permissions and connection.
/// <see cref="Status"/>, <see cref="SyncFrequency"/> and the connection details are written by the block as the person
/// connects, configures and disconnects it; write them yourself to reflect changes made elsewhere.
/// </summary>
public class IntegrationItem : AvaloniaObject
{
    /// <summary>A stable key (events, automation ids); defaults to the name when empty.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<IntegrationItem, string>(nameof(Key), string.Empty);

    /// <summary>The app's name: the row's title, the drawer's title and part of every accessible name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<IntegrationItem, string>(nameof(Name), string.Empty);

    /// <summary>The key of its <see cref="IntegrationCategory"/> (the filter it belongs to); null puts it in none.</summary>
    public static readonly StyledProperty<string?> CategoryProperty =
        AvaloniaProperty.Register<IntegrationItem, string?>(nameof(Category));

    /// <summary>One sentence on what it does: one line in the row (two when narrow) and the drawer's description.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<IntegrationItem, string?>(nameof(Description));

    /// <summary>
    /// The app's mark, drawn 20×20 with no plate: an <see cref="IImage"/>, a <see cref="Geometry"/> (filled with
    /// <see cref="MarkBrush"/>) or any control. Null draws a generated mark: <see cref="Monogram"/> on a rounded tile.
    /// </summary>
    public static readonly StyledProperty<object?> LogoProperty =
        AvaloniaProperty.Register<IntegrationItem, object?>(nameof(Logo));

    /// <summary>The letters of the generated mark (one or two); null uses the first letter of the name.</summary>
    public static readonly StyledProperty<string?> MonogramProperty =
        AvaloniaProperty.Register<IntegrationItem, string?>(nameof(Monogram));

    /// <summary>The generated tile's fill and a geometry logo's fill; null picks a chart series color from the key.</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<IntegrationItem, IBrush?>(nameof(MarkBrush));

    /// <summary>Where it stands (two-way); the block moves it through Connecting to Connected or Error.</summary>
    public static readonly StyledProperty<IntegrationStatus> StatusProperty =
        AvaloniaProperty.Register<IntegrationItem, IntegrationStatus>(nameof(Status), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Who connected it (a display name; the drawer shows a generated avatar); null when unknown.</summary>
    public static readonly StyledProperty<string?> ConnectedByProperty =
        AvaloniaProperty.Register<IntegrationItem, string?>(nameof(ConnectedBy));

    /// <summary>When it was connected; null leaves the date out.</summary>
    public static readonly StyledProperty<DateTimeOffset?> ConnectedAtProperty =
        AvaloniaProperty.Register<IntegrationItem, DateTimeOffset?>(nameof(ConnectedAt));

    /// <summary>True when the person using the block connected it in this session ("you, just now").</summary>
    public static readonly StyledProperty<bool> IsConnectedByCurrentUserProperty =
        AvaloniaProperty.Register<IntegrationItem, bool>(nameof(IsConnectedByCurrentUser));

    /// <summary>The key of the chosen <see cref="IntegrationSyncOption"/> (two-way); null uses the first option.</summary>
    public static readonly StyledProperty<string?> SyncFrequencyProperty =
        AvaloniaProperty.Register<IntegrationItem, string?>(nameof(SyncFrequency), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Why the last connect attempt failed (shown in the drawer while <see cref="Status"/> is Error).</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<IntegrationItem, string?>(nameof(ErrorText));

    /// <summary>Anything you want to keep with the item.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<IntegrationItem, object?>(nameof(Tag));

    /// <summary>What it may do once connected, in order (the drawer's permission list).</summary>
    [Content]
    public AvaloniaList<IntegrationScope> Scopes { get; } = new();

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="CategoryProperty"/>
    public string? Category { get => GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="LogoProperty"/>
    public object? Logo { get => GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    /// <inheritdoc cref="MonogramProperty"/>
    public string? Monogram { get => GetValue(MonogramProperty); set => SetValue(MonogramProperty, value); }

    /// <inheritdoc cref="MarkBrushProperty"/>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public IntegrationStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ConnectedByProperty"/>
    public string? ConnectedBy { get => GetValue(ConnectedByProperty); set => SetValue(ConnectedByProperty, value); }

    /// <inheritdoc cref="ConnectedAtProperty"/>
    public DateTimeOffset? ConnectedAt { get => GetValue(ConnectedAtProperty); set => SetValue(ConnectedAtProperty, value); }

    /// <inheritdoc cref="IsConnectedByCurrentUserProperty"/>
    public bool IsConnectedByCurrentUser
    {
        get => GetValue(IsConnectedByCurrentUserProperty);
        set => SetValue(IsConnectedByCurrentUserProperty, value);
    }

    /// <inheritdoc cref="SyncFrequencyProperty"/>
    public string? SyncFrequency { get => GetValue(SyncFrequencyProperty); set => SetValue(SyncFrequencyProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The key, or the name when the key is empty.</summary>
    public string EffectiveKey => string.IsNullOrEmpty(Key) ? Name : Key;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>One permission of an <see cref="IntegrationItem"/>: a short phrase and the kind that picks its icon.</summary>
public class IntegrationScope : AvaloniaObject
{
    /// <summary>What it lets the app do; picks the icon unless <see cref="Icon"/> is set.</summary>
    public static readonly StyledProperty<IntegrationScopeKind> KindProperty =
        AvaloniaProperty.Register<IntegrationScope, IntegrationScopeKind>(nameof(Kind));

    /// <summary>The phrase ("View rooms and their members"); it completes the drawer's lead ("Chirp can").</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<IntegrationScope, string>(nameof(Text), string.Empty);

    /// <summary>A 24×24 stroke icon replacing the one <see cref="Kind"/> picks.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<IntegrationScope, Geometry?>(nameof(Icon));

    /// <summary>Creates an empty permission (XAML).</summary>
    public IntegrationScope()
    {
    }

    /// <summary>Creates a permission.</summary>
    public IntegrationScope(IntegrationScopeKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    /// <inheritdoc cref="KindProperty"/>
    public IntegrationScopeKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>A filter of <see cref="Integrations"/>: the items whose <see cref="IntegrationItem.Category"/> is its key.</summary>
public class IntegrationCategory : AvaloniaObject
{
    /// <summary>The key items use in <see cref="IntegrationItem.Category"/> (and <see cref="Integrations.SelectedFilter"/>).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<IntegrationCategory, string>(nameof(Key), string.Empty);

    /// <summary>The filter's text and the category shown next to an app's name.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<IntegrationCategory, string>(nameof(Label), string.Empty);

    /// <summary>Creates an empty category (XAML).</summary>
    public IntegrationCategory()
    {
    }

    /// <summary>Creates a category.</summary>
    public IntegrationCategory(string key, string label)
    {
        Key = key;
        Label = label;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}

/// <summary>A choice of the drawer's sync frequency menu.</summary>
public class IntegrationSyncOption : AvaloniaObject
{
    /// <summary>The key stored in <see cref="IntegrationItem.SyncFrequency"/>.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<IntegrationSyncOption, string>(nameof(Key), string.Empty);

    /// <summary>The menu text ("Every 30 minutes").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<IntegrationSyncOption, string>(nameof(Label), string.Empty);

    /// <summary>The line under the menu while it is chosen ("Updates arrive in half-hour batches.").</summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<IntegrationSyncOption, string?>(nameof(Hint));

    /// <summary>The phrase screen readers hear after a change ("every 30 minutes"); null uses the label.</summary>
    public static readonly StyledProperty<string?> PhraseProperty =
        AvaloniaProperty.Register<IntegrationSyncOption, string?>(nameof(Phrase));

    /// <summary>Creates an empty option (XAML).</summary>
    public IntegrationSyncOption()
    {
    }

    /// <summary>Creates an option.</summary>
    public IntegrationSyncOption(string key, string label, string? hint = null, string? phrase = null)
    {
        Key = key;
        Label = label;
        Hint = hint;
        Phrase = phrase;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="HintProperty"/>
    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <inheritdoc cref="PhraseProperty"/>
    public string? Phrase { get => GetValue(PhraseProperty); set => SetValue(PhraseProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>What <see cref="Integrations.ConnectAction"/> is asked to do: connect <see cref="Item"/>.</summary>
public sealed class IntegrationConnectRequest
{
    /// <summary>Creates a request.</summary>
    public IntegrationConnectRequest(IntegrationItem item, int attempt)
    {
        Item = item;
        Attempt = attempt;
    }

    /// <summary>The app to connect.</summary>
    public IntegrationItem Item { get; }

    /// <summary>1 for the first attempt on this app since the block was created, 2 for the next (a retry), and so on.</summary>
    public int Attempt { get; }
}

/// <summary>
/// The outcome of <see cref="Integrations.ConnectAction"/>: <see cref="Connected"/> with who and when, or
/// <see cref="Failed"/> with the reason shown in the drawer. Throwing from the action fails it too.
/// </summary>
public sealed class IntegrationConnectResult
{
    private IntegrationConnectResult(bool isSuccess, string? connectedBy, DateTimeOffset? connectedAt, string? errorText)
    {
        IsSuccess = isSuccess;
        ConnectedBy = connectedBy;
        ConnectedAt = connectedAt;
        ErrorText = errorText;
    }

    /// <summary>True when the app is now connected.</summary>
    public bool IsSuccess { get; }

    /// <summary>Who connected it; null means the person using the block ("you, just now").</summary>
    public string? ConnectedBy { get; }

    /// <summary>When it was connected; null uses the time the action finished.</summary>
    public DateTimeOffset? ConnectedAt { get; }

    /// <summary>Why it failed (null uses the labels' generic failure line).</summary>
    public string? ErrorText { get; }

    /// <summary>A successful connection.</summary>
    public static IntegrationConnectResult Connected(string? connectedBy = null, DateTimeOffset? connectedAt = null) =>
        new(true, connectedBy, connectedAt, null);

    /// <summary>A refused or failed connection with the reason to show.</summary>
    public static IntegrationConnectResult Failed(string? errorText = null) => new(false, null, null, errorText);
}
