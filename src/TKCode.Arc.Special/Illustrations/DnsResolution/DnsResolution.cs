using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An animated explainer of how a domain name is resolved: the browser asks its recursive resolver, the resolver finds
/// nothing in its cache and walks the name servers from the top (a root server refers it to the top-level domain's
/// servers, those refer it to the domain's own name servers, which answer with the address), stores the answer for its
/// time to live and hands it back; asked again, it answers from the cache. Packets travel between the parts, a message
/// log names each exchange, and two readouts count the round trips and the lookup time. Play, pause, step, restart and
/// a 0.5× / 1× / 2× speed control drive it; under reduced motion it opens on its end and steps apply in place. Timings
/// and addresses are illustrative.
/// </summary>
[TemplatePart(StagePart, typeof(DnsResolutionStage))]
[TemplatePart(ExplainerChapterKit.StatusPart, typeof(ExplainerStatus))]
[TemplatePart(ExplainerChapterKit.CaptionPart, typeof(ArcContentSwap))]
[TemplatePart(ExplainerChapterKit.SpeedPart, typeof(SegmentedControl))]
public class DnsResolution : Explainer
{
    /// <summary>The drawn diagram, readouts and message log.</summary>
    public const string StagePart = "PART_Stage";

    /// <summary>The name being looked up (default an invented "docs.lumen-garden.dev"). Use a name with at least two labels.</summary>
    public static readonly StyledProperty<string> DomainProperty =
        AvaloniaProperty.Register<DnsResolution, string>(nameof(Domain), "docs.lumen-garden.dev");

    /// <summary>The address the authoritative server answers with (default 203.0.113.42, a documentation address).</summary>
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<DnsResolution, string>(nameof(Address), "203.0.113.42");

    /// <summary>How long, in seconds, the resolver may keep the answer (default 300).</summary>
    public static readonly StyledProperty<int> TimeToLiveProperty =
        AvaloniaProperty.Register<DnsResolution, int>(nameof(TimeToLive), 300, coerce: static (_, v) => Math.Clamp(v, 0, 604800));

    /// <summary>Ends the story with a second lookup answered from the cache (default true).</summary>
    public static readonly StyledProperty<bool> IsRepeatLookupShownProperty =
        AvaloniaProperty.Register<DnsResolution, bool>(nameof(IsRepeatLookupShown), true);

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<DnsResolutionLabels> LabelsProperty =
        AvaloniaProperty.Register<DnsResolution, DnsResolutionLabels>(nameof(Labels), new DnsResolutionLabels());

    /// <summary>The chapter the story is in.</summary>
    public static readonly DirectProperty<DnsResolution, DnsResolutionStep> StepProperty =
        AvaloniaProperty.RegisterDirect<DnsResolution, DnsResolutionStep>(nameof(Step), o => o.Step);

    /// <summary>Raised when the story enters a chapter (also at the end, with <see cref="DnsResolutionStep.Done"/>).</summary>
    public static readonly RoutedEvent<DnsResolutionStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<DnsResolution, DnsResolutionStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<Thickness> BodyPaddingProperty =
        AvaloniaProperty.Register<DnsResolution, Thickness>(nameof(BodyPadding), new Thickness(24, 24, 24, 0));

    internal static readonly StyledProperty<Thickness> FooterPaddingProperty =
        AvaloniaProperty.Register<DnsResolution, Thickness>(nameof(FooterPadding), new Thickness(24, 16, 24, 24));

    /// <summary>Seconds a packet takes between two parts at speed 1.</summary>
    internal const double Travel = 0.9;

    /// <summary>Illustrative round-trip times in ms: browser to resolver, root, top-level domain, authoritative.</summary>
    internal static readonly int[] RoundTripMs = [4, 24, 31, 38];

    private readonly DnsResolutionScene _scene = new();
    private readonly ExplainerChapterKit _kit;
    private DnsResolutionStage? _stage;
    private DnsResolutionStep _step;
    private string? _sentence;
    private bool _rebuildPending;
    private bool _pauseCheckPending;

    static DnsResolution()
    {
        TitleProperty.OverrideDefaultValue<DnsResolution>("DNS resolution");
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<DnsResolution>(AutomationControlType.Group);
    }

    /// <summary>Creates the explainer with the invented sample domain.</summary>
    public DnsResolution()
    {
        _kit = new ExplainerChapterKit(this);
        AutomationProperties.SetName(this, Title);
    }

    /// <summary>The name being looked up (default an invented "docs.lumen-garden.dev"). Use a name with at least two labels.</summary>
    public string Domain { get => GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    /// <summary>The address the authoritative server answers with (default 203.0.113.42, a documentation address).</summary>
    public string Address { get => GetValue(AddressProperty); set => SetValue(AddressProperty, value); }

    /// <summary>How long, in seconds, the resolver may keep the answer (default 300).</summary>
    public int TimeToLive { get => GetValue(TimeToLiveProperty); set => SetValue(TimeToLiveProperty, value); }

    /// <summary>Ends the story with a second lookup answered from the cache (default true).</summary>
    public bool IsRepeatLookupShown { get => GetValue(IsRepeatLookupShownProperty); set => SetValue(IsRepeatLookupShownProperty, value); }

    /// <summary>Every word the card shows or announces.</summary>
    public DnsResolutionLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The chapter the story is in.</summary>
    public DnsResolutionStep Step { get => _step; private set => SetAndRaise(StepProperty, ref _step, value); }

    /// <summary>Raised when the story enters a chapter (also at the end, with <see cref="DnsResolutionStep.Done"/>).</summary>
    public event EventHandler<DnsResolutionStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    internal Thickness BodyPadding { get => GetValue(BodyPaddingProperty); set => SetValue(BodyPaddingProperty, value); }

    internal Thickness FooterPadding { get => GetValue(FooterPaddingProperty); set => SetValue(FooterPaddingProperty, value); }

    /// <summary>What the story has shown so far (stage and tests).</summary>
    internal DnsResolutionScene Scene => _scene;

    /// <summary>The story time in seconds.</summary>
    internal double StoryTime => Now;

    /// <summary>True when changes travel and fade (full motion); otherwise they apply in place.</summary>
    internal bool Animates => IsTravelEnabled;

    /// <summary>The caption shown (tests).</summary>
    internal string? CaptionText => _kit.CaptionText;

    /// <summary>The status words shown (tests).</summary>
    internal string? StatusText => _kit.StatusText;

    /// <summary>The top-level domain with its dot (".dev").</summary>
    internal string TldText
    {
        get
        {
            var labels = DomainLabels();
            return "." + labels[^1];
        }
    }

    /// <summary>The domain's zone: its last two labels ("lumen-garden.dev").</summary>
    internal string ZoneText
    {
        get
        {
            var labels = DomainLabels();
            return labels.Length >= 2 ? labels[^2] + "." + labels[^1] : labels[^1];
        }
    }

    /// <summary>The zone's first name server, as the referral names it ("ns1.lumen-garden.dev").</summary>
    internal string NameServerText => "ns1." + ZoneText;

    /// <summary>The first lookup's time in ms (all four round trips).</summary>
    internal static int FirstLookupMs => RoundTripMs.Sum();

    private protected override double StepSpeed => 2 * Math.Max(1, Speed);

    private string[] DomainLabels()
    {
        var parts = (Domain ?? string.Empty).Trim().TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? ["example"] : parts;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _kit.Apply(e.NameScope);
        _stage = e.NameScope.Find<DnsResolutionStage>(StagePart);
        _stage?.Attach(this);
        _kit.SetSpeedLabel(Labels.Speed);
        UpdateStatus(force: true);
        UpdateCaption(animate: false);
        UpdateSizing(Bounds.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DomainProperty || change.Property == AddressProperty || change.Property == TimeToLiveProperty ||
            change.Property == IsRepeatLookupShownProperty)
        {
            ScheduleRebuild();
        }
        else if (change.Property == LabelsProperty)
        {
            _kit.SetSpeedLabel(Labels.Speed);
            UpdateStatus(force: true);
            UpdateCaption(animate: false);
            _stage?.OnLabelsChanged();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (change.Property == SpeedProperty)
        {
            _kit.UpdateSpeed();
        }
        else if (change.Property == IsSteppingProperty || change.Property == IsFinishedProperty)
        {
            UpdateStatus();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateSizing(change.GetNewValue<Rect>().Width);
        }
    }

    private protected override void BuildStory(ExplainerScript script)
    {
        var t = 0.4;
        // 1. The browser asks; the resolver's cache has nothing.
        script.At(t, () => EnterStep(DnsResolutionStep.Query));
        Send(script, t, DnsResolutionNode.Client, DnsResolutionNode.Resolver, DnsResolutionPacketKind.Query, () => Labels.Ask(Domain));
        t += Travel;
        script.At(t + 0.2, () => CacheCheck(hit: false));
        script.At(t + 0.7, AnnounceSentence, isStop: true);
        t += 0.8;
        // 2–4. The resolver walks the servers from the top.
        t = Visit(script, t, DnsResolutionStep.Root, DnsResolutionNode.Root, DnsResolutionPacketKind.Referral,
            () => Labels.ReferToTld(TldText), 1);
        t = Visit(script, t, DnsResolutionStep.Tld, DnsResolutionNode.Tld, DnsResolutionPacketKind.Referral,
            () => Labels.ReferToZone(NameServerText), 2);
        t = Visit(script, t, DnsResolutionStep.Authoritative, DnsResolutionNode.Authoritative, DnsResolutionPacketKind.Answer,
            () => Labels.Answer(Address, TimeToLive), 3);
        // 5. The resolver caches the answer and hands it back.
        script.At(t, () =>
        {
            EnterStep(DnsResolutionStep.Answer);
            _scene.CachedAt = Now;
            _scene.HasCacheEntry = true;
            _stage?.Changed();
        });
        Send(script, t + 0.2, DnsResolutionNode.Resolver, DnsResolutionNode.Client, DnsResolutionPacketKind.Answer, () => Address);
        t += 0.2 + Travel;
        script.At(t, () =>
        {
            _scene.ClientAddressAt = Now;
            _scene.HasClientAddress = true;
            AddTrip(0);
        });
        script.At(t + 0.5, AnnounceSentence, isStop: true);
        t += 0.6;
        if (IsRepeatLookupShown)
        {
            // 6. The same question again, answered from the cache.
            t += 0.6;
            script.At(t, () =>
            {
                EnterStep(DnsResolutionStep.Cached);
                _scene.HasClientAddress = false;
                _scene.Lookups = 2;
                _scene.RoundTrips = 0;
                _scene.ElapsedMs.Jump(0);
                _scene.ElapsedTarget = 0;
                _stage?.Changed();
            });
            Send(script, t, DnsResolutionNode.Client, DnsResolutionNode.Resolver, DnsResolutionPacketKind.Query, () => Labels.Ask(Domain));
            t += Travel;
            script.At(t + 0.15, () => CacheCheck(hit: true));
            Send(script, t + 0.35, DnsResolutionNode.Resolver, DnsResolutionNode.Client, DnsResolutionPacketKind.Answer, () => Address);
            t += 0.35 + Travel;
            script.At(t, () =>
            {
                _scene.ClientAddressAt = Now;
                _scene.HasClientAddress = true;
                AddTrip(0);
            });
            script.At(t + 0.4, AnnounceSentence, isStop: true);
            t += 0.5;
        }
        script.At(t + 0.3, Finish, isStop: true);
        script.Length = t + 0.3;
    }

    /// <summary>One server: the query goes out, the server lights, its reply comes back. Returns the time after it.</summary>
    private double Visit(ExplainerScript script, double t, DnsResolutionStep step, DnsResolutionNode server, DnsResolutionPacketKind reply,
        Func<string> replyText, int trip)
    {
        script.At(t, () => EnterStep(step));
        Send(script, t, DnsResolutionNode.Resolver, server, DnsResolutionPacketKind.Query, () => Labels.Ask(Domain));
        t += Travel + 0.35;
        Send(script, t, server, DnsResolutionNode.Resolver, reply, replyText);
        t += Travel;
        script.At(t, () => AddTrip(trip));
        script.At(t + 0.25, AnnounceSentence, isStop: true);
        return t + 0.35;
    }

    private void Send(ExplainerScript script, double at, DnsResolutionNode from, DnsResolutionNode to, DnsResolutionPacketKind kind, Func<string> text)
    {
        script.At(at, () =>
        {
            var message = text();
            _scene.Packet = new DnsResolutionPacket(from, to, kind, Now, Travel);
            _scene.Light(from, Now);
            _scene.AddLine(from, to, kind, message, Now);
            _stage?.Changed();
            Beat();
        });
        script.At(at + Travel, () =>
        {
            _scene.Light(to, Now);
            _stage?.Changed();
            Beat();
        });
    }

    private void CacheCheck(bool hit)
    {
        _scene.CacheFlash = hit ? DnsResolutionCacheFlash.Hit : DnsResolutionCacheFlash.Miss;
        _scene.CacheFlashAt = Now;
        _scene.AddLine(DnsResolutionNode.Resolver, DnsResolutionNode.Resolver,
            hit ? DnsResolutionPacketKind.Answer : DnsResolutionPacketKind.Query, hit ? Labels.FoundInCache : Labels.NotCached, Now);
        _stage?.Changed();
        Beat();
    }

    private void AddTrip(int index)
    {
        _scene.RoundTrips++;
        _scene.ElapsedTarget += RoundTripMs[index];
        if (Animates)
        {
            _scene.ElapsedMs.Tween(_scene.ElapsedTarget, Now, 0.5, ArcEasings.Standard);
        }
        else
        {
            _scene.ElapsedMs.Jump(_scene.ElapsedTarget);
        }
        _stage?.Changed();
        Beat();
    }

    private void EnterStep(DnsResolutionStep step)
    {
        Step = step;
        _sentence = CaptionFor(step);
        UpdateCaption(Animates);
        UpdateStatus();
        RaiseEvent(new DnsResolutionStepChangedEventArgs(StepChangedEvent, this, step));
    }

    private void Finish()
    {
        _scene.Packet = null;
        _scene.Finished = true;
        EnterStep(DnsResolutionStep.Done);
        _stage?.Changed();
    }

    private void AnnounceSentence() => AnnounceBeat(_sentence);

    private void Beat()
    {
        if (Animates)
        {
            RequestFrames(1.0);
        }
    }

    internal string CaptionFor(DnsResolutionStep step)
    {
        var labels = Labels;
        return step switch
        {
            DnsResolutionStep.Query => labels.QueryCaption(Domain),
            DnsResolutionStep.Root => labels.RootCaption(TldText),
            DnsResolutionStep.Tld => labels.TldCaption(TldText, ZoneText),
            DnsResolutionStep.Authoritative => labels.AuthoritativeCaption(Domain, Address),
            DnsResolutionStep.Answer => labels.AnswerCaption(Address, TimeToLive),
            DnsResolutionStep.Cached => labels.CachedCaption,
            DnsResolutionStep.Done => labels.DoneCaption(FirstLookupMs, IsRepeatLookupShown ? RoundTripMs[0] : 0),
            _ => labels.ReadyCaption(Domain)
        };
    }

    private protected override void ResetScene()
    {
        var hadContent = _scene.HasStarted;
        _scene.Reset();
        _sentence = null;
        Step = DnsResolutionStep.Ready;
        _stage?.Changed();
        UpdateStatus();
        UpdateCaption(Animates && hadContent);
        if (hadContent)
        {
            RequestFrames(0.6);
        }
    }

    private protected override void OnTimeChanged()
    {
        _stage?.Tick();
        UpdateStatus();
    }

    private protected override void OnFinished()
    {
        UpdateStatus();
        Announce(CaptionFor(DnsResolutionStep.Done));
    }

    private protected override void OnPlaybackChanged()
    {
        UpdateStatus();
        if (!IsPlaying && !_pauseCheckPending)
        {
            _pauseCheckPending = true;
            Dispatcher.UIThread.Post(AnnouncePause, DispatcherPriority.Background);
        }
    }

    private void AnnouncePause()
    {
        _pauseCheckPending = false;
        if (!IsPlaying && !IsStepping && !IsFinished && _scene.HasStarted && IsReaderDriven)
        {
            Announce(_sentence);
        }
    }

    private void ScheduleRebuild()
    {
        if (_rebuildPending)
        {
            return;
        }
        _rebuildPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildPending = false;
            Rebuild();
        }, DispatcherPriority.Background);
    }

    private void UpdateStatus(bool force = false)
    {
        var phase = _kit.PhaseOf(_scene.HasStarted);
        var labels = Labels;
        var text = phase switch
        {
            ExplainerChapterPhase.Playing => labels.Resolving,
            ExplainerChapterPhase.Paused => labels.Paused,
            ExplainerChapterPhase.Done => labels.Resolved,
            _ => labels.Ready
        };
        _kit.SetStatus(phase, text, force);
    }

    private void UpdateCaption(bool animate) => _kit.SetCaption(CaptionFor(Step), animate);

    private void UpdateSizing(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var padding = ExplainerChapterKit.CardPadding(width);
        var body = new Thickness(padding, padding, padding, AreControlsVisible ? 0 : padding);
        if (body != BodyPadding)
        {
            SetCurrentValue(BodyPaddingProperty, body);
        }
        var footer = new Thickness(padding, 16, padding, padding);
        if (footer != FooterPadding)
        {
            SetCurrentValue(FooterPaddingProperty, footer);
        }
        _kit.SetSpeedLabelVisible(width >= 420);
    }
}

/// <summary>What a packet carries, which sets its color.</summary>
internal enum DnsResolutionPacketKind
{
    Query,
    Referral,
    Answer
}

/// <summary>The cache box's last check.</summary>
internal enum DnsResolutionCacheFlash
{
    None,
    Miss,
    Hit
}

/// <summary>A packet in flight on the story clock.</summary>
internal sealed record DnsResolutionPacket(DnsResolutionNode From, DnsResolutionNode To, DnsResolutionPacketKind Kind, double Start, double Duration);

/// <summary>One line of the message log.</summary>
internal sealed record DnsResolutionLogLine(DnsResolutionNode From, DnsResolutionNode To, DnsResolutionPacketKind Kind, string Text, double At);

/// <summary>What the DNS story has shown so far; the stage draws it at the current story time.</summary>
internal sealed class DnsResolutionScene
{
    /// <summary>When each part last lit up (NaN never).</summary>
    public readonly double[] LitAt = new double[5];

    public DnsResolutionPacket? Packet { get; set; }

    public List<DnsResolutionLogLine> Log { get; } = new();

    public bool HasCacheEntry { get; set; }

    public double CachedAt { get; set; }

    public DnsResolutionCacheFlash CacheFlash { get; set; }

    public double CacheFlashAt { get; set; }

    public bool HasClientAddress { get; set; }

    public double ClientAddressAt { get; set; }

    public int RoundTrips { get; set; }

    public int Lookups { get; set; } = 1;

    public ExplainerValue ElapsedMs;

    public double ElapsedTarget { get; set; }

    public bool Finished { get; set; }

    /// <summary>The part that lit last (the one the story is at), or -1.</summary>
    public int Current { get; private set; } = -1;

    public bool HasStarted => Log.Count > 0 || Finished;

    public DnsResolutionScene()
    {
        Reset();
    }

    public void Light(DnsResolutionNode node, double at)
    {
        LitAt[(int)node] = at;
        Current = (int)node;
    }

    public void AddLine(DnsResolutionNode from, DnsResolutionNode to, DnsResolutionPacketKind kind, string text, double at)
        => Log.Add(new DnsResolutionLogLine(from, to, kind, text, at));

    public void Reset()
    {
        Array.Fill(LitAt, double.NaN);
        Packet = null;
        Log.Clear();
        HasCacheEntry = false;
        CacheFlash = DnsResolutionCacheFlash.None;
        HasClientAddress = false;
        RoundTrips = 0;
        Lookups = 1;
        ElapsedMs = new ExplainerValue(0);
        ElapsedTarget = 0;
        Finished = false;
        Current = -1;
    }
}
