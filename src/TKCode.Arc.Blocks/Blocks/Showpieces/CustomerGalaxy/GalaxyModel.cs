using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A snapshot of the accounts of a <see cref="CustomerGalaxy"/>, ordered by MRR (largest first), with everything the
/// scene needs per dot in flat arrays: world radius, health state, the overview's spiral coordinates and drift, and the
/// paint order (per health state, smallest first so big dots sit on top). Built once per data change.
/// </summary>
internal sealed class GalaxyModel
{
    /// <summary>The seed of every layout jitter (spec section 11).</summary>
    public const uint Seed = 1729;

    private readonly Dictionary<string, int> _byKey;
    private readonly Dictionary<GalaxyAccount, int> _byAccount;

    private GalaxyModel(GalaxyAccount[] accounts, GalaxyHealthState[] states)
    {
        var n = accounts.Length;
        Accounts = accounts;
        States = states;
        Mrr = new double[n];
        Radius = new double[n];
        Health = new int[n];
        Rho = new double[n];
        Theta = new double[n];
        DriftRadius = new double[n];
        DriftAngle = new double[n];
        DriftRate = new double[n];
        DriftPhase = new double[n];
        _byKey = new Dictionary<string, int>(n, StringComparer.Ordinal);
        _byAccount = new Dictionary<GalaxyAccount, int>(n, ReferenceEqualityComparer.Instance);
        DrawOrder = Array.Empty<int[]>();
        LayerOrder = Array.Empty<int>();
        HealthSet = null!;
    }

    public static GalaxyModel Empty { get; } = Build(Array.Empty<GalaxyAccount>(), Array.Empty<GalaxyHealthState>());

    /// <summary>Accounts by rank (largest MRR first; ties keep the source order).</summary>
    public GalaxyAccount[] Accounts { get; }

    public int Count => Accounts.Length;

    public GalaxyHealthState[] States { get; }

    public double[] Mrr { get; }

    /// <summary>World radius of each dot (no depth).</summary>
    public double[] Radius { get; }

    /// <summary>Index into <see cref="States"/> of each dot.</summary>
    public int[] Health { get; }

    /// <summary>State indices in paint order (lowest <see cref="GalaxyHealthState.Layer"/> first).</summary>
    public int[] LayerOrder { get; private set; }

    /// <summary>Per state: its dots, smallest MRR first (the paint order within a batch).</summary>
    public int[][] DrawOrder { get; private set; }

    public double[] Rho { get; }

    public double[] Theta { get; }

    public double[] DriftRadius { get; }

    public double[] DriftAngle { get; }

    public double[] DriftRate { get; }

    public double[] DriftPhase { get; }

    /// <summary>The overview core's radius (95 in the reference galaxy).</summary>
    public double CoreRadius { get; private set; } = GalaxyLayout.ReferenceCoreRadius;

    public double TotalMrr { get; private set; }

    public double GrowthMrr { get; private set; }

    public double AtRiskMrr { get; private set; }

    /// <summary>The health states as groups (largest MRR first): the overview's list and its row dimming.</summary>
    public GalaxyGroupSet HealthSet { get; private set; }

    /// <summary>The rank of an account (by reference, then by key), or −1.</summary>
    public int IndexOf(GalaxyAccount? account)
    {
        if (account is null)
        {
            return -1;
        }
        if (_byAccount.TryGetValue(account, out var index))
        {
            return index;
        }
        return _byKey.TryGetValue(account.Key, out index) ? index : -1;
    }

    /// <summary>The rank of the account with <paramref name="key"/>, or −1.</summary>
    public int IndexOfKey(string key) => _byKey.TryGetValue(key, out var index) ? index : -1;

    public static GalaxyModel Build(IReadOnlyList<GalaxyAccount> source, IReadOnlyList<GalaxyHealthState> stateList)
    {
        var states = stateList.ToArray();
        var order = new int[source.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        var mrrs = new double[source.Count];
        for (var i = 0; i < mrrs.Length; i++)
        {
            var mrr = source[i].Mrr;
            mrrs[i] = double.IsFinite(mrr) ? Math.Max(0, mrr) : 0;
        }
        // Largest MRR first; stable for ties.
        Array.Sort(order, (a, b) =>
        {
            var c = mrrs[b].CompareTo(mrrs[a]);
            return c != 0 ? c : a.CompareTo(b);
        });
        var accounts = new GalaxyAccount[order.Length];
        for (var i = 0; i < order.Length; i++)
        {
            accounts[i] = source[order[i]];
        }
        var model = new GalaxyModel(accounts, states);
        model.Fill(mrrs, order);
        return model;
    }

    private void Fill(double[] mrrs, int[] order)
    {
        var n = Count;
        var stateIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var s = 0; s < States.Length; s++)
        {
            stateIndex.TryAdd(States[s].Key, s);
        }
        // Accounts without a known state draw with the first neutral (non-growth, non-risk) state.
        var fallback = 0;
        for (var s = 0; s < States.Length; s++)
        {
            if (!States[s].IsGrowth && !States[s].IsRisk)
            {
                fallback = s;
                break;
            }
        }
        for (var i = 0; i < n; i++)
        {
            var account = Accounts[i];
            Mrr[i] = mrrs[order[i]];
            Radius[i] = GalaxyLayout.DotRadius(Mrr[i]);
            Health[i] = States.Length == 0 ? 0 : account.Health is { } h && stateIndex.TryGetValue(h, out var s) ? s : fallback;
            _byAccount.TryAdd(account, i);
            _byKey.TryAdd(account.Key, i);
            TotalMrr += Mrr[i];
            if (States.Length > 0)
            {
                if (States[Health[i]].IsGrowth)
                {
                    GrowthMrr += Mrr[i];
                }
                if (States[Health[i]].IsRisk)
                {
                    AtRiskMrr += Mrr[i];
                }
            }
        }
        LayerOrder = Enumerable.Range(0, States.Length).OrderBy(s => States[s].Layer).ThenBy(s => s).ToArray();
        var draw = new List<int>[Math.Max(1, States.Length)];
        for (var s = 0; s < draw.Length; s++)
        {
            draw[s] = new List<int>();
        }
        for (var i = n - 1; i >= 0; i--)
        {
            draw[States.Length == 0 ? 0 : Health[i]].Add(i);
        }
        DrawOrder = draw.Select(l => l.ToArray()).ToArray();
        CoreRadius = GalaxyLayout.Spiral(Radius, new ShowpieceSeed(Seed), Rho, Theta, DriftRadius, DriftAngle, DriftRate, DriftPhase);
        HealthSet = BuildHealthSet();
    }

    private GalaxyGroupSet BuildHealthSet()
    {
        var keys = new string[States.Length];
        var labels = new string[States.Length];
        for (var s = 0; s < States.Length; s++)
        {
            keys[s] = States[s].Key;
            labels[s] = States[s].DisplayLabel;
        }
        var groupOf = new int[Count];
        Array.Copy(Health, groupOf, Count);
        return GalaxyGroupSet.Create(this, GalaxyGrouping.HealthKey, keys, labels, groupOf);
    }

    /// <summary>
    /// The clusters of a grouping: <paramref name="definitions"/> first (in their order), then values found on the
    /// accounts in the order they first appear (largest MRR first); accounts without a value go to "<paramref name="otherLabel"/>".
    /// </summary>
    public GalaxyGroupSet BuildSet(string key, IReadOnlyList<GalaxyGroupDefinition> definitions, string otherLabel)
    {
        if (string.Equals(key, GalaxyGrouping.HealthKey, StringComparison.OrdinalIgnoreCase) && definitions.Count == 0)
        {
            var health = BuildHealthSet();
            return health;
        }
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var keys = new List<string>();
        var labels = new List<string>();
        foreach (var definition in definitions)
        {
            if (index.TryAdd(definition.Key, keys.Count))
            {
                keys.Add(definition.Key);
                labels.Add(string.IsNullOrEmpty(definition.Label) ? definition.Key : definition.Label!);
            }
        }
        var isHealth = string.Equals(key, GalaxyGrouping.HealthKey, StringComparison.OrdinalIgnoreCase);
        var groupOf = new int[Count];
        for (var i = 0; i < Count; i++)
        {
            var value = isHealth ? (States.Length > 0 ? States[Health[i]].Key : null) : Accounts[i].GroupValue(key);
            value ??= string.Empty;
            if (!index.TryGetValue(value, out var g))
            {
                g = keys.Count;
                index[value] = g;
                keys.Add(value);
                labels.Add(value.Length == 0 ? otherLabel : isHealth ? HealthLabel(value) : value);
            }
            groupOf[i] = g;
        }
        return GalaxyGroupSet.Create(this, key, keys.ToArray(), labels.ToArray(), groupOf);
    }

    private string HealthLabel(string key)
    {
        foreach (var state in States)
        {
            if (string.Equals(state.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return state.DisplayLabel;
            }
        }
        return key;
    }
}

/// <summary>
/// The accounts of a <see cref="GalaxyModel"/> split into groups (the clusters of a grouping, or the health states of the
/// overview): totals per group, largest MRR first, which group every dot belongs to, and, once
/// <see cref="EnsureGeometry"/> ran, the cluster layout (centers, radii, and every dot's offset in its cluster).
/// </summary>
internal sealed class GalaxyGroupSet
{
    private GalaxyGroupSet(string key, GalaxyGroupSummary[] groups, int[] groupOf, int[][] members)
    {
        Key = key;
        Groups = groups;
        GroupOf = groupOf;
        Members = members;
        Centers = new Point[groups.Length];
        Radii = new double[groups.Length];
        LocalX = new double[groupOf.Length];
        LocalY = new double[groupOf.Length];
    }

    public string Key { get; }

    /// <summary>Groups, largest MRR first (ties: definition order).</summary>
    public GalaxyGroupSummary[] Groups { get; }

    public int Count => Groups.Length;

    /// <summary>The group index (into <see cref="Groups"/>) of every dot.</summary>
    public int[] GroupOf { get; }

    /// <summary>Per group: its dots, largest MRR first.</summary>
    public int[][] Members { get; }

    public bool HasGeometry { get; private set; }

    /// <summary>Cluster centers (world).</summary>
    public Point[] Centers { get; }

    /// <summary>Cluster outer radii (world).</summary>
    public double[] Radii { get; }

    /// <summary>Every dot's offset from its cluster's center (before the cluster's rotation).</summary>
    public double[] LocalX { get; }

    public double[] LocalY { get; }

    /// <summary>Label sizes (px) the clusters were arranged with.</summary>
    public Size[] LabelSizes { get; private set; } = Array.Empty<Size>();

    public int IndexOf(string? key)
    {
        if (key is null)
        {
            return -1;
        }
        for (var g = 0; g < Groups.Length; g++)
        {
            if (string.Equals(Groups[g].Key, key, StringComparison.Ordinal))
            {
                return g;
            }
        }
        for (var g = 0; g < Groups.Length; g++)
        {
            if (string.Equals(Groups[g].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return g;
            }
        }
        return -1;
    }

    public static GalaxyGroupSet Create(GalaxyModel model, string key, string[] keys, string[] labels, int[] groupOf)
    {
        var states = model.States.Length;
        var raw = new GalaxyGroupSummary[keys.Length];
        for (var g = 0; g < keys.Length; g++)
        {
            raw[g] = new GalaxyGroupSummary(keys[g], labels[g], states);
        }
        for (var i = 0; i < model.Count; i++)
        {
            var summary = raw[groupOf[i]];
            var mrr = model.Mrr[i];
            summary.Mrr += mrr;
            summary.AccountCount++;
            if (states > 0)
            {
                var h = model.Health[i];
                summary.HealthMrrValues[h] += mrr;
                summary.HealthCountValues[h]++;
                if (model.States[h].IsGrowth)
                {
                    summary.GrowthMrr += mrr;
                }
                if (model.States[h].IsRisk)
                {
                    summary.AtRiskMrr += mrr;
                }
            }
        }
        // Empty groups (a listed definition no account uses) are dropped; the rest sort by MRR, ties by definition order.
        var used = Enumerable.Range(0, raw.Length).Where(g => raw[g].AccountCount > 0)
                             .OrderByDescending(g => raw[g].Mrr).ThenBy(g => g).ToArray();
        var remap = new int[raw.Length];
        for (var g = 0; g < used.Length; g++)
        {
            remap[used[g]] = g;
        }
        var groups = used.Select(g => raw[g]).ToArray();
        var finalOf = new int[groupOf.Length];
        var members = new List<int>[groups.Length];
        for (var g = 0; g < groups.Length; g++)
        {
            members[g] = new List<int>(groups[g].AccountCount);
        }
        for (var i = 0; i < groupOf.Length; i++)
        {
            finalOf[i] = remap[groupOf[i]];
            members[finalOf[i]].Add(i);
        }
        return new GalaxyGroupSet(key, groups, finalOf, members.Select(m => m.ToArray()).ToArray());
    }

    /// <summary>
    /// Packs every cluster and arranges them (largest in the middle), with labels of <paramref name="labelSizes"/> px.
    /// Runs once per set (and again when the labels change size).
    /// </summary>
    public void EnsureGeometry(GalaxyModel model, IReadOnlyList<Size> labelSizes)
    {
        if (HasGeometry && SameSizes(labelSizes))
        {
            return;
        }
        LabelSizes = labelSizes.ToArray();
        var seed = new ShowpieceSeed(GalaxyModel.Seed ^ (uint)StableHash(Key));
        var radii = new double[Count];
        for (var g = 0; g < Count; g++)
        {
            var members = Members[g];
            var r = new double[members.Length];
            var x = new double[members.Length];
            var y = new double[members.Length];
            for (var m = 0; m < members.Length; m++)
            {
                r[m] = model.Radius[members[m]];
            }
            radii[g] = GalaxyLayout.Pack(r, seed, x, y);
            for (var m = 0; m < members.Length; m++)
            {
                LocalX[members[m]] = x[m];
                LocalY[members[m]] = y[m];
            }
        }
        // Labels keep their pixel size while the clusters scale; they are turned into world units at one typical scale
        // (arranging at the actual fit runs away: wider gaps lower the fit, which widens the gaps again).
        var labels = new Size[Count];
        for (var g = 0; g < Count; g++)
        {
            var px = g < LabelSizes.Length ? LabelSizes[g] : new Size(120, GalaxyLayout.LabelHeight);
            labels[g] = new Size(px.Width / GalaxyLayout.ArrangeScale, px.Height / GalaxyLayout.ArrangeScale);
        }
        var centers = new Point[Count];
        GalaxyLayout.Arrange(radii, labels, GalaxyLayout.LabelGap / GalaxyLayout.ArrangeScale,
            GalaxyLayout.ClusterGap / GalaxyLayout.ArrangeScale, GalaxyLayout.LabelDiscGap / GalaxyLayout.ArrangeScale, centers);
        Array.Copy(radii, Radii, Count);
        Array.Copy(centers, Centers, Count);
        HasGeometry = true;
    }

    private bool SameSizes(IReadOnlyList<Size> sizes)
    {
        if (sizes.Count != LabelSizes.Length)
        {
            return false;
        }
        for (var i = 0; i < sizes.Count; i++)
        {
            if (Math.Abs(sizes[i].Width - LabelSizes[i].Width) > 0.5 || Math.Abs(sizes[i].Height - LabelSizes[i].Height) > 0.5)
            {
                return false;
            }
        }
        return true;
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in text)
            {
                hash = (hash ^ char.ToLowerInvariant(c)) * 16777619;
            }
            return hash;
        }
    }

    /// <summary>The fit scale and camera content of the arranged clusters on a stage.</summary>
    public double Fit(Size stage, out Rect content) =>
        GalaxyLayout.GroupFit(Centers, Radii, LabelSizes.Length == Count ? LabelSizes : DefaultLabels(), stage, out content);

    private Size[] DefaultLabels()
    {
        var labels = new Size[Count];
        for (var g = 0; g < Count; g++)
        {
            labels[g] = new Size(120, GalaxyLayout.LabelHeight);
        }
        return labels;
    }
}
