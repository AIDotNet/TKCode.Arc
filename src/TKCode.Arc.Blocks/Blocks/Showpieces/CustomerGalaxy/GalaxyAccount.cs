using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One customer account of a <see cref="CustomerGalaxy"/>: a dot whose area follows <see cref="Mrr"/> and whose color
/// follows <see cref="Health"/>. The values the groupings read (plan, region, industry …) go into <see cref="Groups"/>
/// under the grouping's <see cref="GalaxyGrouping.Key"/>.
/// </summary>
public class GalaxyAccount : AvaloniaObject
{
    /// <summary>Stable id (selection and incremental updates match accounts by it); the name is used when it is empty.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<GalaxyAccount, string?>(nameof(Id));

    /// <summary>The account's name, shown at the top of its card and announced when it is selected.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<GalaxyAccount, string?>(nameof(Name));

    /// <summary>Monthly recurring revenue; the dot's area grows with it and every total adds it up.</summary>
    public static readonly StyledProperty<double> MrrProperty =
        AvaloniaProperty.Register<GalaxyAccount, double>(nameof(Mrr));

    /// <summary>The <see cref="GalaxyHealthState.Key"/> of the account's health; it picks the dot color.</summary>
    public static readonly StyledProperty<string?> HealthProperty =
        AvaloniaProperty.Register<GalaxyAccount, string?>(nameof(Health));

    /// <summary>Who owns the account ("Self-serve" for accounts without one); shown on the card.</summary>
    public static readonly StyledProperty<string?> OwnerProperty =
        AvaloniaProperty.Register<GalaxyAccount, string?>(nameof(Owner));

    /// <summary>One short line about the account, shown at the bottom of its card ("Renewal call booked").</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<GalaxyAccount, string?>(nameof(Note));

    /// <summary>Any value you want back from <see cref="CustomerGalaxy.AccountSelected"/>.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<GalaxyAccount, object?>(nameof(Tag));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="MrrProperty"/>
    public double Mrr { get => GetValue(MrrProperty); set => SetValue(MrrProperty, value); }

    /// <inheritdoc cref="HealthProperty"/>
    public string? Health { get => GetValue(HealthProperty); set => SetValue(HealthProperty, value); }

    /// <inheritdoc cref="OwnerProperty"/>
    public string? Owner { get => GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>
    /// The account's value for each grouping, by <see cref="GalaxyGrouping.Key"/> ("plan" → "Business"). Health is read
    /// from <see cref="Health"/>, not from here. Change them before the account is added, or call
    /// <see cref="CustomerGalaxy.Refresh"/>.
    /// </summary>
    public AvaloniaDictionary<string, string> Groups { get; } = new();

    /// <summary>The key selection and updates match by: <see cref="Id"/>, else <see cref="Name"/>.</summary>
    internal string Key => string.IsNullOrEmpty(Id) ? Name ?? string.Empty : Id;

    /// <summary>The value the grouping <paramref name="key"/> reads (<see cref="Health"/> for the health grouping).</summary>
    public string? GroupValue(string key)
    {
        if (string.Equals(key, GalaxyGrouping.HealthKey, StringComparison.OrdinalIgnoreCase))
        {
            return Health;
        }
        return Groups.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>The name.</summary>
    public override string ToString() => Name ?? Id ?? string.Empty;
}

/// <summary>
/// One way to group the accounts of a <see cref="CustomerGalaxy"/> into clusters (a tab of its grouping switch): the
/// accounts' <see cref="GalaxyAccount.Groups"/> value under <see cref="Key"/> picks their cluster. The key
/// <see cref="HealthKey"/> groups by <see cref="GalaxyAccount.Health"/> and takes its clusters from
/// <see cref="CustomerGalaxy.HealthStates"/>.
/// </summary>
public class GalaxyGrouping : AvaloniaObject
{
    /// <summary>The key of the built-in health grouping (reads <see cref="GalaxyAccount.Health"/>).</summary>
    public const string HealthKey = "health";

    /// <summary>The <see cref="GalaxyAccount.Groups"/> key this grouping reads ("plan", "region"), or <see cref="HealthKey"/>.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<GalaxyGrouping, string>(nameof(Key), string.Empty);

    /// <summary>The tab text ("Plan"); <see cref="Key"/> when null.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<GalaxyGrouping, string?>(nameof(Header));

    /// <summary>The title of the totals list while this grouping is shown ("By plan"); built from <see cref="CustomerGalaxy.ListTitleFormat"/> when null.</summary>
    public static readonly StyledProperty<string?> ListTitleProperty =
        AvaloniaProperty.Register<GalaxyGrouping, string?>(nameof(ListTitle));

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="ListTitleProperty"/>
    public string? ListTitle { get => GetValue(ListTitleProperty); set => SetValue(ListTitleProperty, value); }

    /// <summary>
    /// The clusters and their labels, in the order used to break ties. Leave it empty to take the values the accounts
    /// carry, in the order they first appear; values that are not listed are added after the listed ones.
    /// </summary>
    [Content]
    public AvaloniaList<GalaxyGroupDefinition> Groups { get; } = new();

    internal string DisplayHeader => string.IsNullOrEmpty(Header) ? Key : Header!;

    internal bool IsHealth => string.Equals(Key, HealthKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>The header.</summary>
    public override string ToString() => DisplayHeader;
}

/// <summary>One cluster of a <see cref="GalaxyGrouping"/>: the account value it collects and the label it shows.</summary>
public class GalaxyGroupDefinition : AvaloniaObject
{
    /// <summary>The <see cref="GalaxyAccount.Groups"/> value of the accounts in this cluster ("business").</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<GalaxyGroupDefinition, string>(nameof(Key), string.Empty);

    /// <summary>The label above the cluster and in the totals list ("Business"); <see cref="Key"/> when null.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<GalaxyGroupDefinition, string?>(nameof(Label));

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The label.</summary>
    public override string ToString() => Label ?? Key;
}

/// <summary>The ArcDesign color a <see cref="GalaxyHealthState"/> draws with (resolved again on every theme change).</summary>
public enum GalaxyTone
{
    /// <summary>The muted text color (<c>TextMuted</c>): the steady majority.</summary>
    Neutral,

    /// <summary><c>Success</c>: accounts that grow.</summary>
    Success,

    /// <summary><c>Warning</c>: accounts to watch.</summary>
    Warning,

    /// <summary><c>Danger</c>: accounts at risk.</summary>
    Danger,

    /// <summary>The accent color (<c>Accent</c>).</summary>
    Accent,

    /// <summary>Chart series color 1 (<c>Series1</c>).</summary>
    Series1,

    /// <summary>Chart series color 2 (<c>Series2</c>).</summary>
    Series2,

    /// <summary>Chart series color 3 (<c>Series3</c>).</summary>
    Series3,

    /// <summary>Chart series color 4 (<c>Series4</c>).</summary>
    Series4
}

/// <summary>
/// A health state of a <see cref="CustomerGalaxy"/>: its key, legend label and color, how strongly its dots are drawn,
/// and whether it counts as growth or risk in the totals.
/// </summary>
public class GalaxyHealthState : AvaloniaObject
{
    /// <summary>The value of <see cref="GalaxyAccount.Health"/> that selects this state ("at-risk").</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<GalaxyHealthState, string>(nameof(Key), string.Empty);

    /// <summary>The legend, card and list label ("At risk"); <see cref="Key"/> when null.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<GalaxyHealthState, string?>(nameof(Label));

    /// <summary>The ArcDesign color of its dots, legend dot and bar segments (default <see cref="GalaxyTone.Neutral"/>).</summary>
    public static readonly StyledProperty<GalaxyTone> ToneProperty =
        AvaloniaProperty.Register<GalaxyHealthState, GalaxyTone>(nameof(Tone));

    /// <summary>A fixed color instead of <see cref="Tone"/> (it does not follow the theme).</summary>
    public static readonly StyledProperty<Color?> ColorProperty =
        AvaloniaProperty.Register<GalaxyHealthState, Color?>(nameof(Color));

    /// <summary>Opacity of its dots and legend dot (default 0.95; the neutral state 0.72).</summary>
    public static readonly StyledProperty<double> DotOpacityProperty =
        AvaloniaProperty.Register<GalaxyHealthState, double>(nameof(DotOpacity), 0.95);

    /// <summary>Opacity of its bar segments in the totals list (default 1; the neutral state 0.5).</summary>
    public static readonly StyledProperty<double> BarOpacityProperty =
        AvaloniaProperty.Register<GalaxyHealthState, double>(nameof(BarOpacity), 1.0);

    /// <summary>Paint order: lower layers are drawn first, so higher ones sit on top (neutral 0, risk last).</summary>
    public static readonly StyledProperty<int> LayerProperty =
        AvaloniaProperty.Register<GalaxyHealthState, int>(nameof(Layer));

    /// <summary>Its MRR is the "growing" subtotal of the totals panel.</summary>
    public static readonly StyledProperty<bool> IsGrowthProperty =
        AvaloniaProperty.Register<GalaxyHealthState, bool>(nameof(IsGrowth));

    /// <summary>Its MRR is the "at risk" subtotal and the risk share of every cluster.</summary>
    public static readonly StyledProperty<bool> IsRiskProperty =
        AvaloniaProperty.Register<GalaxyHealthState, bool>(nameof(IsRisk));

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public GalaxyTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="ColorProperty"/>
    public Color? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <inheritdoc cref="DotOpacityProperty"/>
    public double DotOpacity { get => GetValue(DotOpacityProperty); set => SetValue(DotOpacityProperty, value); }

    /// <inheritdoc cref="BarOpacityProperty"/>
    public double BarOpacity { get => GetValue(BarOpacityProperty); set => SetValue(BarOpacityProperty, value); }

    /// <inheritdoc cref="LayerProperty"/>
    public int Layer { get => GetValue(LayerProperty); set => SetValue(LayerProperty, value); }

    /// <inheritdoc cref="IsGrowthProperty"/>
    public bool IsGrowth { get => GetValue(IsGrowthProperty); set => SetValue(IsGrowthProperty, value); }

    /// <inheritdoc cref="IsRiskProperty"/>
    public bool IsRisk { get => GetValue(IsRiskProperty); set => SetValue(IsRiskProperty, value); }

    internal string DisplayLabel => string.IsNullOrEmpty(Label) ? Key : Label!;

    /// <summary>
    /// The four default states, in legend order: Growing (<c>Success</c>), Steady (<c>TextMuted</c> at 72%, drawn
    /// first), Cooling (<c>Warning</c>) and At risk (<c>Danger</c>, drawn last).
    /// </summary>
    public static IEnumerable<GalaxyHealthState> CreateDefaults()
    {
        yield return new GalaxyHealthState { Key = "growing", Label = "Growing", Tone = GalaxyTone.Success, Layer = 1, IsGrowth = true };
        yield return new GalaxyHealthState
        {
            Key = "steady",
            Label = "Steady",
            Tone = GalaxyTone.Neutral,
            DotOpacity = 0.72,
            BarOpacity = 0.5,
            Layer = 0
        };
        yield return new GalaxyHealthState { Key = "cooling", Label = "Cooling", Tone = GalaxyTone.Warning, Layer = 2 };
        yield return new GalaxyHealthState { Key = "at-risk", Label = "At risk", Tone = GalaxyTone.Danger, Layer = 3, IsRisk = true };
    }

    /// <summary>The label.</summary>
    public override string ToString() => DisplayLabel;
}

/// <summary>Where the totals panel of a <see cref="CustomerGalaxy"/> goes.</summary>
public enum GalaxyTotalsPlacement
{
    /// <summary>Beside the stage when the control is at least 880 px wide, under it otherwise.</summary>
    Auto,

    /// <summary>Always beside the stage (288 px wide).</summary>
    End,

    /// <summary>Always under the stage.</summary>
    Bottom
}

/// <summary>How the rows of the totals list are ordered and measured.</summary>
public enum GalaxyGroupSort
{
    /// <summary>By monthly recurring revenue: the row shows the MRR and its bar is the share of the largest MRR.</summary>
    Mrr,

    /// <summary>By number of accounts: the row shows the count and its bar is the share of the largest count.</summary>
    Accounts
}

/// <summary>Arguments of <see cref="CustomerGalaxy.AccountSelected"/>.</summary>
public sealed class GalaxyAccountEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public GalaxyAccountEventArgs(RoutedEvent routedEvent, GalaxyAccount? account)
        : base(routedEvent)
    {
        Account = account;
    }

    /// <summary>The selected account, or null when the selection was cleared.</summary>
    public GalaxyAccount? Account { get; }
}

/// <summary>Arguments of <see cref="CustomerGalaxy.GroupFocused"/>.</summary>
public sealed class GalaxyGroupEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public GalaxyGroupEventArgs(RoutedEvent routedEvent, string? groupingKey, string? groupKey, GalaxyGroupSummary? group)
        : base(routedEvent)
    {
        GroupingKey = groupingKey;
        GroupKey = groupKey;
        Group = group;
    }

    /// <summary>The grouping the cluster belongs to.</summary>
    public string? GroupingKey { get; }

    /// <summary>The focused cluster's key, or null when the view returned to every cluster.</summary>
    public string? GroupKey { get; }

    /// <summary>The focused cluster's totals, or null.</summary>
    public GalaxyGroupSummary? Group { get; }
}

/// <summary>Arguments of <see cref="CustomerGalaxy.GroupingChanged"/>.</summary>
public sealed class GalaxyGroupingChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public GalaxyGroupingChangedEventArgs(RoutedEvent routedEvent, string? oldKey, string? newKey)
        : base(routedEvent)
    {
        OldKey = oldKey;
        NewKey = newKey;
    }

    /// <summary>The previous grouping (null: all accounts in one galaxy).</summary>
    public string? OldKey { get; }

    /// <summary>The new grouping (null: all accounts in one galaxy).</summary>
    public string? NewKey { get; }
}

/// <summary>Totals of one cluster (or of one health state in the overview).</summary>
public sealed class GalaxyGroupSummary
{
    internal GalaxyGroupSummary(string key, string label, int healthCount)
    {
        Key = key;
        Label = label;
        HealthMrrValues = new double[healthCount];
        HealthCountValues = new int[healthCount];
    }

    internal readonly double[] HealthMrrValues;
    internal readonly int[] HealthCountValues;

    /// <summary>The cluster's key (the accounts' value).</summary>
    public string Key { get; }

    /// <summary>The cluster's label.</summary>
    public string Label { get; internal set; }

    /// <summary>Total monthly recurring revenue.</summary>
    public double Mrr { get; internal set; }

    /// <summary>Number of accounts.</summary>
    public int AccountCount { get; internal set; }

    /// <summary>MRR of the accounts whose health state <see cref="GalaxyHealthState.IsGrowth"/>.</summary>
    public double GrowthMrr { get; internal set; }

    /// <summary>MRR of the accounts whose health state <see cref="GalaxyHealthState.IsRisk"/>.</summary>
    public double AtRiskMrr { get; internal set; }

    /// <summary><see cref="AtRiskMrr"/> as a share of <see cref="Mrr"/> (0 when the cluster has no MRR).</summary>
    public double AtRiskShare => Mrr > 0 ? AtRiskMrr / Mrr : 0;

    /// <summary>MRR per health state, in the order of <see cref="CustomerGalaxy.HealthStates"/>.</summary>
    public IReadOnlyList<double> HealthMrr => HealthMrrValues;

    /// <summary>Accounts per health state, in the order of <see cref="CustomerGalaxy.HealthStates"/>.</summary>
    public IReadOnlyList<int> HealthCounts => HealthCountValues;

    /// <summary>The label.</summary>
    public override string ToString() => Label;
}

/// <summary>The totals of a <see cref="CustomerGalaxy"/>: everything, per health state and per cluster of the current grouping.</summary>
public sealed class GalaxySummary
{
    internal GalaxySummary(double totalMrr, int accountCount, double growthMrr, double atRiskMrr,
                           IReadOnlyList<GalaxyGroupSummary> health, IReadOnlyList<GalaxyGroupSummary> groups)
    {
        TotalMrr = totalMrr;
        AccountCount = accountCount;
        GrowthMrr = growthMrr;
        AtRiskMrr = atRiskMrr;
        Health = health;
        Groups = groups;
    }

    /// <summary>No accounts.</summary>
    public static GalaxySummary Empty { get; } = new(0, 0, 0, 0, Array.Empty<GalaxyGroupSummary>(), Array.Empty<GalaxyGroupSummary>());

    /// <summary>Total monthly recurring revenue.</summary>
    public double TotalMrr { get; }

    /// <summary>Number of accounts.</summary>
    public int AccountCount { get; }

    /// <summary>MRR of the growing accounts.</summary>
    public double GrowthMrr { get; }

    /// <summary>MRR of the accounts at risk.</summary>
    public double AtRiskMrr { get; }

    /// <summary>One entry per health state, largest MRR first.</summary>
    public IReadOnlyList<GalaxyGroupSummary> Health { get; }

    /// <summary>One entry per cluster of the current grouping, largest MRR first (the health states in the overview).</summary>
    public IReadOnlyList<GalaxyGroupSummary> Groups { get; }
}
