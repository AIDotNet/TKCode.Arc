using TKCode.Arc.Primitives;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Every visible word of a <see cref="MultiRegionFailover"/> (override for another language or domain).</summary>
public sealed class MultiRegionFailoverLabels
{
    public string Users { get; init; } = "Users";
    public string UsersDetail { get; init; } = "worldwide";
    public string Balancer { get; init; } = "Load balancer";
    public string BalancerDetail { get; init; } = "health checks";
    public string App { get; init; } = "App";
    public string AppDetail { get; init; } = "3 instances";
    public string Database { get; init; } = "Database";
    public string Primary { get; init; } = "primary";
    public string Replica { get; init; } = "replica";
    public string CatchingUp { get; init; } = "catching up";
    public string Unavailable { get; init; } = "unavailable";
    public string Healthy { get; init; } = "healthy";
    public string Degraded { get; init; } = "degraded";
    public string Down { get; init; } = "down";
    public string Recovering { get; init; } = "recovering";
    public string FailingOver { get; init; } = "Failing over";
    public string Promoted { get; init; } = "Promoted";
    public Func<string, string> ServedBy { get; init; } = static region => $"→ {region}";
    public Func<double, string> FailoverTime { get; init; } = static seconds => $"failover in {seconds:0.0} s";
    public Func<string, string> Steady { get; init; } = static region => $"Traffic is served from {region}; data replicates to the standby regions";
    public Func<string, string> Failing { get; init; } = static region => $"{region} fails its health checks";
    public Func<string, string> Shifted { get; init; } = static region => $"The load balancer shifts traffic to {region}";
    public Func<string, string> PromotedStep { get; init; } = static region => $"The {region} database is promoted to primary";
    public Func<string, string> Recovered { get; init; } = static region => $"{region} recovers and catches up as a replica";
    public Func<string, string> Failback { get; init; } = static region => $"Planned failback to {region}";
    public string Summary { get; init; } = "A global load balancer serves users from a healthy region and fails over to a standby region, whose database is promoted, when the primary region goes down";
}

/// <summary>
/// An animated multi-region deployment: a global load balancer sends users to the primary region while its database
/// replicates to standby regions. When a region fails its health checks traffic shifts to the next healthy region and,
/// if it held the primary database, a replica is promoted; a recovered region catches up as a replica.
/// </summary>
/// <remarks>
/// With <see cref="IsAutomatic"/> the story plays by itself (fail, fail over, recover, fail back). Set it to false and
/// drive the same state machine with <see cref="FailRegion"/> and <see cref="RecoverRegion"/>.
/// </remarks>
public class MultiRegionFailover : Illustration
{
    /// <summary>The regions (2 or 3 names); defaults to us-east-1, eu-west-1 and ap-southeast-1.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> RegionsProperty =
        AvaloniaProperty.Register<MultiRegionFailover, IReadOnlyList<string>?>(nameof(Regions));

    /// <summary>Plays the failure and recovery story by itself (default); false waits for <see cref="FailRegion"/>.</summary>
    public static readonly StyledProperty<bool> IsAutomaticProperty =
        AvaloniaProperty.Register<MultiRegionFailover, bool>(nameof(IsAutomatic), true);

    public static readonly StyledProperty<MultiRegionFailoverLabels> LabelsProperty =
        AvaloniaProperty.Register<MultiRegionFailover, MultiRegionFailoverLabels>(nameof(Labels), new MultiRegionFailoverLabels());

    public IReadOnlyList<string>? Regions { get => GetValue(RegionsProperty); set => SetValue(RegionsProperty, value); }

    public bool IsAutomatic { get => GetValue(IsAutomaticProperty); set => SetValue(IsAutomaticProperty, value); }

    public MultiRegionFailoverLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The regions shown when <see cref="Regions"/> is not set.</summary>
    public static IReadOnlyList<string> DefaultRegions { get; } = ["us-east-1", "eu-west-1", "ap-southeast-1"];

    /// <summary>The region whose database is the primary.</summary>
    public int PrimaryRegion => _primary;

    /// <summary>The region the load balancer sends users to.</summary>
    public int TrafficRegion => _traffic;

    /// <summary>Raised when another region's database becomes the primary.</summary>
    public event EventHandler<int>? PrimaryRegionChanged;

    public override int StepCount => 6;

    private const double SceneW = 844;
    private const double SceneH = 356;
    private const double ReplicationHop = 0.7;

    private enum Health
    {
        Healthy,
        Degraded,
        Down,
        Recovering
    }

    private sealed class Region(string name, IllustrationGroup group, IllustrationNode app, IllustrationNode db, IllustrationEdge fromBalancer,
                                IllustrationEdge appToDb)
    {
        public string Name { get; } = name;
        public IllustrationGroup Group { get; } = group;
        public IllustrationNode App { get; } = app;
        public IllustrationNode Db { get; } = db;
        public IllustrationEdge FromBalancer { get; } = fromBalancer;
        public IllustrationEdge AppToDb { get; } = appToDb;
        public Health Health { get; set; }
    }

    private readonly List<Region> _regions = new();
    private readonly List<IllustrationEdge> _links = new();
    private IllustrationNode _users = null!;
    private IllustrationNode _balancer = null!;
    private IllustrationEdge _toBalancer = null!;
    private IllustrationLabel _served = null!;
    private IllustrationLabel _failover = null!;
    private int _primary;
    private int _traffic;
    private double _trafficClock;
    private double _replicationClock;
    private double _failingSince = double.NaN;
    private int _requests;

    static MultiRegionFailover()
    {
        LabelProperty.OverrideDefaultValue<MultiRegionFailover>("Multi-region failover illustration");
    }

    private protected override double SceneWidth => SceneW;

    private protected override double SceneHeight => SceneH;

    private IReadOnlyList<string> EffectiveRegions
    {
        get
        {
            var regions = Regions is { Count: >= 2 } list ? list : DefaultRegions;
            return regions.Count > 3 ? regions.Take(3).ToArray() : regions;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RegionsProperty || change.Property == LabelsProperty || change.Property == IsAutomaticProperty)
        {
            Rebuild();
        }
    }

    private protected override void BuildScene()
    {
        var labels = Labels;
        var names = EffectiveRegions;
        _regions.Clear();
        _links.Clear();
        const double w = 150;
        const double h = 52;
        _users = AddNode(new IllustrationNode("users", new Rect(14, 20, w, h), ArcIcons.Users, labels.Users, labels.UsersDetail));
        _balancer = AddNode(new IllustrationNode("balancer", new Rect(SceneW / 2 - 85, 20, 170, h), ArcIcons.Globe, labels.Balancer, labels.BalancerDetail));
        _toBalancer = AddEdge(new IllustrationEdge(_users, _balancer, IllustrationEdge.Curve(_users.Right, _balancer.Left), curved: true));
        _served = AddLabel(new IllustrationLabel(new Point(_balancer.Bounds.Right + 16, _balancer.Center.Y - 16), string.Empty, IllustrationInk.Secondary)
        {
            IsMono = true
        });
        _failover = AddLabel(new IllustrationLabel(new Point(_balancer.Bounds.Right + 16, _balancer.Center.Y + 2), string.Empty, IllustrationInk.Accent)
        {
            IsMono = true
        });
        const double top = 104;
        const double gap = 16;
        var frameWidth = (SceneW - 28 - gap * (names.Count - 1)) / names.Count;
        for (var i = 0; i < names.Count; i++)
        {
            var frame = new Rect(14 + i * (frameWidth + gap), top, frameWidth, SceneH - top - 12);
            var group = AddGroup(new IllustrationGroup(frame, names[i]));
            var app = AddNode(new IllustrationNode($"app{i}", new Rect(frame.Center.X - w / 2, frame.Y + 44, w, h), ArcIcons.Server,
                labels.App, labels.AppDetail));
            var db = AddNode(new IllustrationNode($"db{i}", new Rect(frame.Center.X - w / 2, frame.Y + 134, w, h), ArcIcons.Database,
                labels.Database, labels.Replica));
            var fromBalancer = AddEdge(new IllustrationEdge(_balancer, app, IllustrationEdge.Curve(_balancer.Bottom, app.Top, vertical: true), curved: true));
            var appToDb = AddEdge(new IllustrationEdge(app, db, IllustrationEdge.Curve(app.Bottom, db.Top, vertical: true), curved: true));
            _regions.Add(new Region(names[i], group, app, db, fromBalancer, appToDb));
        }
        for (var i = 0; i < _regions.Count - 1; i++)
        {
            var a = _regions[i].Db;
            var b = _regions[i + 1].Db;
            _links.Add(AddEdge(new IllustrationEdge(a, b, IllustrationEdge.Curve(a.Right, b.Left), curved: true, dashed: true)));
        }
    }

    private protected override void BuildScript(IllustrationScript script)
    {
        script.At(0, () => SetStep(0, Labels.Steady(_regions[_traffic].Name)));
        if (!IsAutomatic)
        {
            return;
        }
        script.At(3.0, () => FailRegion(0));
        script.At(8.6, () => RecoverRegion(0));
        script.At(11.8, () => FailBack(0));
        script.At(14.4, () => { });
        script.Length = 14.4;
    }

    private protected override void OnRestart()
    {
        _primary = 0;
        _traffic = 0;
        _trafficClock = 0;
        _replicationClock = 0;
        _failingSince = double.NaN;
        _requests = 0;
        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            region.Health = Health.Healthy;
            region.Group.Status = Labels.Healthy;
            region.Db.Subtitle = i == _primary ? Labels.Primary : Labels.Replica;
        }
        foreach (var link in _links)
        {
            link.ActiveTone = IllustrationTone.Accent;
            link.Active.Jump(0.35);
        }
        ApplyTraffic(animate: false);
        _failover.Text = string.Empty;
    }

    private protected override void ApplyPoster()
    {
        SetStep(0, Labels.Summary);
        _toBalancer.Active.Jump(0.7);
        Spawn(_toBalancer, 1, delay: -0.5);
        Spawn(_regions[0].FromBalancer, 1, delay: -0.5);
        foreach (var link in _links)
        {
            Spawn(link, 1, delay: -0.5);
        }
        ShowBadge(_regions[0].Db, Labels.Primary, IllustrationTone.Success, ArcIcons.Check, below: true);
    }

    #region State machine

    /// <summary>Takes region <paramref name="index"/> down: health checks fail, traffic and the primary move away.</summary>
    public void FailRegion(int index)
    {
        if (index < 0 || index >= _regions.Count || _regions[index].Health is Health.Down or Health.Degraded)
        {
            return;
        }
        var region = _regions[index];
        var labels = Labels;
        region.Health = Health.Degraded;
        region.Group.SetTone(IllustrationTone.Warning, Now);
        region.Group.Status = labels.Degraded;
        _failingSince = Now;
        SetStep(1, labels.Failing(region.Name));
        Then(0.8, () =>
        {
            region.Health = Health.Down;
            region.Group.SetTone(IllustrationTone.Danger, Now);
            region.Group.Status = labels.Down;
            region.Group.Alarm.Tween(1, Now, 0.3);
            region.App.Muted.Tween(1, Now, 0.3);
            region.Db.Muted.Tween(1, Now, 0.3);
            region.Db.Subtitle = labels.Unavailable;
            HideBadge(region.Db);
            DropPackets(region.FromBalancer);
            DropPackets(region.AppToDb);
            foreach (var link in _links)
            {
                if (ReferenceEquals(link.From, region.Db) || ReferenceEquals(link.To, region.Db))
                {
                    DropPackets(link);
                    link.Dim.Tween(1, Now, 0.3);
                }
            }
            if (_traffic != index && _primary != index)
            {
                return;
            }
            var next = NextHealthy(index);
            if (next < 0)
            {
                return;
            }
            ShowBadge(_balancer, labels.FailingOver, IllustrationTone.Accent, ArcIcons.RefreshCw, below: true);
            Then(0.5, () =>
            {
                if (_traffic == index)
                {
                    _traffic = next;
                    ApplyTraffic(animate: true);
                    _failover.Text = labels.FailoverTime(double.IsNaN(_failingSince) ? 0 : Now - _failingSince);
                    SetStep(2, labels.Shifted(_regions[next].Name));
                }
                Then(0.7, () =>
                {
                    HideBadge(_balancer);
                    if (_primary == index)
                    {
                        Promote(next, labels.Promoted);
                        SetStep(3, labels.PromotedStep(_regions[next].Name));
                    }
                });
            });
        });
    }

    /// <summary>Brings region <paramref name="index"/> back; it catches up as a replica of the current primary.</summary>
    public void RecoverRegion(int index)
    {
        if (index < 0 || index >= _regions.Count || _regions[index].Health is Health.Healthy or Health.Recovering)
        {
            return;
        }
        var region = _regions[index];
        var labels = Labels;
        region.Health = Health.Recovering;
        region.Group.SetTone(IllustrationTone.Warning, Now);
        region.Group.Status = labels.Recovering;
        region.Group.Alarm.Tween(0, Now, 0.4);
        region.App.Muted.Tween(0, Now, 0.4);
        region.Db.Muted.Tween(0, Now, 0.4);
        region.Db.Subtitle = $"{labels.Replica} · {labels.CatchingUp}";
        foreach (var link in _links)
        {
            if (ReferenceEquals(link.From, region.Db) || ReferenceEquals(link.To, region.Db))
            {
                link.Dim.Tween(0, Now, 0.4);
            }
        }
        SetStep(4, labels.Recovered(region.Name));
        // Catch-up: a burst of changes from the primary.
        for (var k = 0; k < 5; k++)
        {
            Replicate(_primary, index, delay: k * 0.16, force: true);
        }
        Then(1.6, () =>
        {
            region.Health = Health.Healthy;
            region.Group.SetTone(IllustrationTone.Success, Now);
            region.Group.Status = labels.Healthy;
            region.Db.Subtitle = index == _primary ? labels.Primary : labels.Replica;
        });
    }

    /// <summary>Moves traffic and the primary back to <paramref name="index"/> (a planned failback).</summary>
    private void FailBack(int index)
    {
        if (_regions[index].Health != Health.Healthy)
        {
            return;
        }
        var labels = Labels;
        SetStep(5, labels.Failback(_regions[index].Name));
        _traffic = index;
        ApplyTraffic(animate: true);
        _failover.Text = string.Empty;
        if (_primary != index)
        {
            Promote(index, labels.Primary);
        }
    }

    private void Promote(int index, string badge)
    {
        var labels = Labels;
        if (_primary >= 0 && _primary < _regions.Count && _primary != index)
        {
            var old = _regions[_primary];
            old.Db.Subtitle = old.Health == Health.Down ? labels.Unavailable : labels.Replica;
            HideBadge(old.Db);
        }
        _primary = index;
        _regions[index].Db.Subtitle = labels.Primary;
        _regions[index].Db.SetTone(IllustrationTone.Success, Now);
        ShowBadge(_regions[index].Db, badge, IllustrationTone.Success, ArcIcons.Check, below: true);
        Then(1.6, () =>
        {
            foreach (var region in _regions)
            {
                region.Db.SetTone(IllustrationTone.Neutral, Now);
            }
        });
        PrimaryRegionChanged?.Invoke(this, index);
    }

    private int NextHealthy(int from)
    {
        for (var step = 1; step < _regions.Count; step++)
        {
            var candidate = (from + step) % _regions.Count;
            if (_regions[candidate].Health == Health.Healthy)
            {
                return candidate;
            }
        }
        return -1;
    }

    private void ApplyTraffic(bool animate)
    {
        for (var i = 0; i < _regions.Count; i++)
        {
            var edge = _regions[i].FromBalancer;
            var target = i == _traffic ? 0.8 : 0;
            var dim = _regions[i].Health == Health.Down ? 1 : 0;
            if (animate)
            {
                edge.Active.Tween(target, Now, 0.4);
                edge.Dim.Tween(dim, Now, 0.4);
            }
            else
            {
                edge.Active.Jump(target);
                edge.Dim.Jump(dim);
            }
            _regions[i].AppToDb.Active.Jump(i == _traffic ? 0.5 : 0);
        }
        _toBalancer.Active.Jump(0.7);
        _served.Text = Labels.ServedBy(_regions[_traffic].Name);
    }

    /// <summary>Runs <paramref name="action"/> after a delay, or at once in the still poster frame.</summary>
    private void Then(double seconds, Action action)
    {
        if (IsPoster)
        {
            action();
        }
        else
        {
            After(seconds, action);
        }
    }

    #endregion

    #region Traffic and replication

    private protected override void OnTick(double dt)
    {
        _trafficClock += dt;
        while (_trafficClock >= 0.3)
        {
            _trafficClock -= 0.3;
            Spawn(_toBalancer, 0.6, tag: 1);
        }
        _replicationClock += dt;
        if (_replicationClock >= 0.9)
        {
            _replicationClock -= 0.9;
            for (var i = 0; i < _regions.Count; i++)
            {
                if (i != _primary && _regions[i].Health == Health.Healthy)
                {
                    Replicate(_primary, i, delay: 0, force: false);
                }
            }
        }
    }

    /// <summary>Sends one change from database <paramref name="from"/> to <paramref name="to"/> along the replication links.</summary>
    private void Replicate(int from, int to, double delay, bool force)
    {
        if (from == to || _regions[from].Health == Health.Down)
        {
            return;
        }
        var step = to > from ? 1 : -1;
        var hop = 0;
        for (var i = from; i != to; i += step)
        {
            var next = i + step;
            if (!force && next != to && _regions[next].Health != Health.Healthy)
            {
                return;
            }
            var link = _links[Math.Min(i, next)];
            Spawn(link, ReplicationHop, IllustrationTone.Success, reverse: step < 0, delay: delay + hop * ReplicationHop);
            hop++;
        }
    }

    private protected override void OnPacketArrived(IllustrationEdge edge, int tag, IllustrationTone tone)
    {
        if (ReferenceEquals(edge, _toBalancer))
        {
            var target = _regions[_traffic];
            if (target.Health != Health.Down)
            {
                Spawn(target.FromBalancer, 0.7, tag: 2);
            }
        }
        else if (tag == 2)
        {
            _requests++;
            foreach (var region in _regions)
            {
                if (ReferenceEquals(region.FromBalancer, edge) && region.Health != Health.Down && _requests % 2 == 0)
                {
                    Spawn(region.AppToDb, 0.35);
                }
            }
        }
    }

    #endregion
}
