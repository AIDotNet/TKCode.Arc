using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A loading region whose skeleton blocks grow into the real content, block by block, instead of swapping (Arc Skeleton
/// morph, design reference). Wrap every visible piece of the loaded layout in a <see cref="MorphBlock"/>. While
/// <see cref="IsLoading"/> is true the blocks show size-matched skeletons that breathe together and the region reports
/// itself busy; when it turns false the blocks resolve in reading order, <see cref="Stagger"/> apart, each stretching
/// from its skeleton to the content's measured box. The region eases to its new height and clips while it does, so
/// its border never jumps; add the <c>card</c> style class to make the region the card. Going back to loading is
/// immediate for the blocks while the height still eases.
/// </summary>
[TemplatePart(StatusPart, typeof(Control))]
[PseudoClasses(LoadingPseudoClass, ResolvingPseudoClass)]
public class SkeletonMorph : ContentControl
{
    public const string StatusPart = "PART_Status";
    public const string LoadingPseudoClass = ":loading";
    public const string ResolvingPseudoClass = ":resolving";

    /// <summary>While true, blocks show their skeletons and the region is busy. Turning it false resolves the blocks.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<SkeletonMorph, bool>(nameof(IsLoading), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Time between blocks as they resolve in reading order (40 ms by default; zero resolves them together).</summary>
    public static readonly StyledProperty<TimeSpan> StaggerProperty =
        AvaloniaProperty.Register<SkeletonMorph, TimeSpan>(nameof(Stagger), TimeSpan.FromMilliseconds(40));

    /// <summary>Status text screen readers say while the region loads ("Loading" by default).</summary>
    public static readonly StyledProperty<string> LoadingLabelProperty =
        AvaloniaProperty.Register<SkeletonMorph, string>(nameof(LoadingLabel), "Loading");

    /// <summary>True from the moment loading ends until every block has resolved and its skeleton has faded.</summary>
    public static readonly DirectProperty<SkeletonMorph, bool> IsResolvingProperty =
        AvaloniaProperty.RegisterDirect<SkeletonMorph, bool>(nameof(IsResolving), o => o.IsResolving);

    /// <summary>Raised when the blocks have finished resolving after loading ended (also when there was nothing to animate).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ResolvedEvent =
        RoutedEvent.Register<SkeletonMorph, RoutedEventArgs>(nameof(Resolved), RoutingStrategies.Bubble);

    /// <summary>The low point of the pulse (theme: SkeletonMorphToken.PulseMinOpacity).</summary>
    internal static readonly StyledProperty<double> PulseMinOpacityProperty =
        AvaloniaProperty.Register<SkeletonMorph, double>(nameof(PulseMinOpacity), 0.55);

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <inheritdoc cref="StaggerProperty"/>
    public TimeSpan Stagger
    {
        get => GetValue(StaggerProperty);
        set => SetValue(StaggerProperty, value);
    }

    /// <inheritdoc cref="LoadingLabelProperty"/>
    public string LoadingLabel
    {
        get => GetValue(LoadingLabelProperty);
        set => SetValue(LoadingLabelProperty, value);
    }

    private bool _isResolving;

    /// <inheritdoc cref="IsResolvingProperty"/>
    public bool IsResolving
    {
        get => _isResolving;
        private set
        {
            if (SetAndRaise(IsResolvingProperty, ref _isResolving, value))
            {
                PseudoClasses.Set(ResolvingPseudoClass, value);
            }
        }
    }

    /// <inheritdoc cref="ResolvedEvent"/>
    public event EventHandler<RoutedEventArgs>? Resolved
    {
        add => AddHandler(ResolvedEvent, value);
        remove => RemoveHandler(ResolvedEvent, value);
    }

    internal double PulseMinOpacity
    {
        get => GetValue(PulseMinOpacityProperty);
        set => SetValue(PulseMinOpacityProperty, value);
    }

    private readonly List<MorphBlock> _blocks = new();
    private readonly List<MorphBlock> _order = new();
    private ExplainerAnnouncer? _status;
    private LiveLoop? _pulseLoop;
    private double _pulseTime;
    private double _pulse = 1;
    private int _generation;
    private int _pending;

    static SkeletonMorph()
    {
        FocusableProperty.OverrideDefaultValue<SkeletonMorph>(false);
        ClipToBoundsProperty.OverrideDefaultValue<SkeletonMorph>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SkeletonMorph>(AutomationControlType.Group);
    }

    /// <summary>The current breath of the skeletons (1 at rest and with motion off).</summary>
    internal double CurrentPulse => _pulse;

    /// <summary>True while the pulse loop asks for frames (tests read it).</summary>
    internal bool IsPulsing => _pulseLoop?.IsRunning == true;

    /// <summary>The blocks that belong to this region, in registration order.</summary>
    internal IReadOnlyList<MorphBlock> Blocks => _blocks;

    /// <summary>The live region that carries <see cref="LoadingLabel"/> while loading (tests read it).</summary>
    internal ExplainerAnnouncer? Status => _status;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _status = e.NameScope.Find<ExplainerAnnouncer>(StatusPart);
        UpdateStatus(announce: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsLoading)
        {
            StartPulse(restart: false);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLoadingProperty)
        {
            OnLoadingChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == LoadingLabelProperty)
        {
            UpdateStatus(announce: IsLoading);
        }
    }

    internal void Register(MorphBlock block)
    {
        if (!_blocks.Contains(block))
        {
            _blocks.Add(block);
        }
        if (IsLoading)
        {
            StartPulse(restart: false);
        }
    }

    internal void Unregister(MorphBlock block) => _blocks.Remove(block);

    private void OnLoadingChanged(bool loading)
    {
        _generation++;
        PseudoClasses.Set(LoadingPseudoClass, loading);
        UpdateStatus(announce: loading);
        if (loading)
        {
            IsResolving = false;
            _pending = 0;
            _pulseTime = 0;
            _pulse = 1;
            // Every block returns to its skeleton in the same frame; the shell's height spring eases the change.
            foreach (var block in _blocks)
            {
                block.ShowSkeleton(_pulse);
            }
            StartPulse(restart: true);
            return;
        }
        ResolveBlocks();
    }

    private void ResolveBlocks()
    {
        _order.Clear();
        CollectInReadingOrder(this, _order);
        var level = this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var pendingBlocks = 0;
        foreach (var block in _order)
        {
            if (!block.IsResolved)
            {
                pendingBlocks++;
            }
        }
        if (pendingBlocks == 0)
        {
            _order.Clear();
            RaiseEvent(new RoutedEventArgs(ResolvedEvent, this));
            return;
        }
        _pending = pendingBlocks;
        IsResolving = true;
        var generation = _generation;
        var index = 0;
        var stagger = Stagger;
        // Blocks report back (synchronously when nothing animates); a copy keeps the walk safe from re-entrant changes.
        var order = _order.ToArray();
        _order.Clear();
        foreach (var block in order)
        {
            if (block.IsResolved)
            {
                continue;
            }
            block.Resolve(SkeletonMorphMath.StaggerDelay(index++, stagger, level), level, generation);
            if (generation != _generation)
            {
                return;
            }
        }
    }

    /// <summary>Reading order: the visual tree in pre-order (document order), blocks of nested regions excluded.</summary>
    private void CollectInReadingOrder(Visual parent, List<MorphBlock> into)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is MorphBlock block)
            {
                if (ReferenceEquals(block.Owner, this))
                {
                    into.Add(block);
                }
                continue;
            }
            if (child is SkeletonMorph)
            {
                continue;
            }
            CollectInReadingOrder(child, into);
        }
    }

    internal void OnBlockSettled(int generation)
    {
        if (generation != _generation || _pending <= 0)
        {
            return;
        }
        _pending--;
        if (_pending == 0)
        {
            IsResolving = false;
            RaiseEvent(new RoutedEventArgs(ResolvedEvent, this));
        }
    }

    private void UpdateStatus(bool announce)
    {
        // The region is busy while loading: the hidden status says the label (Arc: aria-busy + role=status).
        AutomationProperties.SetItemStatus(this, IsLoading ? LoadingLabel : string.Empty);
        if (_status is null)
        {
            return;
        }
        if (IsLoading)
        {
            if (announce)
            {
                _status.Announce(LoadingLabel);
            }
        }
        else
        {
            _status.Clear();
        }
    }

    // ---- The pulse: one clock for every skeleton of the region, so they all breathe in phase ----

    private void StartPulse(bool restart)
    {
        if (restart)
        {
            _pulseTime = 0;
        }
        if (_pulseLoop is null)
        {
            // A LiveLoop, not a ControlTheme animation: the pulse drives a self-drawn layer in many blocks from one clock,
            // and must stop per block the moment that block mounts its content. It changes opacity only, which reduced
            // motion keeps, so it runs there too (a skeleton must still read as loading); motion off parks it.
            _pulseLoop = new LiveLoop(this, StepPulse) { MinimumLevel = ArcMotionLevel.Reduced };
            _pulseLoop.RunningChanged += OnPulseRunningChanged;
        }
        _pulseLoop.Start();
    }

    private bool StepPulse(double dt)
    {
        _pulseTime += dt;
        _pulse = SkeletonMorphMath.Pulse(_pulseTime, PulseMinOpacity);
        var any = false;
        foreach (var block in _blocks)
        {
            if (block.WantsPulse)
            {
                block.SetPulse(_pulse);
                any = true;
            }
        }
        if (!any)
        {
            // Nothing breathes (every block has mounted, or the region has no blocks yet): stop asking for frames.
            // Register restarts the loop when a block appears while loading; the phase carries on from _pulseTime.
            if (!IsLoading)
            {
                _pulse = 1;
            }
            return false;
        }
        return true;
    }

    private void OnPulseRunningChanged(bool running)
    {
        if (running || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            return;
        }
        // No motion: skeletons rest at full opacity.
        _pulse = 1;
        foreach (var block in _blocks)
        {
            block.SetPulse(1);
        }
    }
}
