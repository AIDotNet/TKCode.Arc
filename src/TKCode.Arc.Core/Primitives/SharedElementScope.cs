using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P8 shared-element flight (Motion's <c>layoutId</c> with <c>layoutCrossfade={false}</c>): the keyed pieces of a
/// <see cref="Target"/> container (a quick look on the overlay layer) fly between the boxes of their counterparts in a
/// <see cref="Source"/> container (the card in the page) and their own layout, on one spring. Pieces are marked with
/// <see cref="SharedElement.KeyProperty"/>; the target root itself can be keyed too, and when it is a
/// <see cref="SharedElementFrame"/> its corners morph from the source's radius and stay circular under the stretch.
/// <para>
/// Nothing is re-parented or snapshotted and nothing is laid out per frame: every target piece gets a render transform
/// (origin top-left) computed from its live root-space boxes, children corrected for their flying parents. While the
/// target stands in, the source pieces are hidden (animation-priority opacity), so one solid surface moves instead of
/// two translucent copies. Works with <see cref="ModalPresence"/> (<see cref="ModalEntrance.Custom"/>): both containers
/// live in the same window, the target on its overlay layer.
/// </para>
/// <para>
/// <see cref="Progress"/> 0 puts the target pieces on their source counterparts, 1 at their own layout. A new
/// <see cref="FlyAsync"/> retargets the running spring and keeps its velocity, so an open and a close reverse each other
/// mid-flight. Motion is code-driven because the boxes are read from two separate trees every frame.
/// </para>
/// <code>
/// var flight = new SharedElementScope(card, quickLook) { RevealsSourceOnArrival = false };
/// await flight.FlyAsync(1, MotionSpec.Spring(SpringSpec.FromVisual(0.3, 0)));   // grow out of the card
/// if (await flight.FlyAsync(0, MotionSpec.Spring(SpringSpec.FromVisual(0.26, 0)))) { flight.Reset(); } // land and hand back
/// </code>
/// </summary>
public sealed class SharedElementScope : IDisposable
{
    private sealed class Pair
    {
        public required string Key { get; init; }
        public required Visual Source { get; init; }
        public required Visual Target { get; init; }
        public required SharedElementMode Mode { get; init; }
        public required CornerRadius SourceRadius { get; init; }
        public required int Depth { get; init; }
        public MatrixTransform Transform { get; } = new();
        public IDisposable? TransformToken { get; set; }
        public IDisposable? OriginToken { get; set; }
    }

    private static readonly RelativePoint s_topLeft = new(0, 0, RelativeUnit.Absolute);

    private readonly MotionValue<double> _progress;
    private readonly List<Pair> _pairs = new();
    private readonly HashSet<Visual> _movers = new();
    private readonly List<IDisposable> _hidden = new();
    private Visual? _root;
    private bool _active;
    private int _generation;

    /// <param name="source">Container that stays in place (the card); its keyed pieces are hidden while the target stands in.</param>
    /// <param name="target">Container whose keyed pieces fly (the quick look); laid out at its own position.</param>
    /// <param name="progress">Initial progress (0 = on the source).</param>
    public SharedElementScope(Visual source, Visual target, double progress = 0)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _progress = new MotionValue<double>(source, Math.Clamp(progress, 0, 1), MotionVectors.Double);
        _progress.Changed += Apply;
    }

    /// <summary>The container that stays where it is (its keyed pieces hide while the target stands in).</summary>
    public Visual Source { get; }

    /// <summary>The container whose keyed pieces fly.</summary>
    public Visual Target { get; }

    /// <summary>0 = target pieces sit on their source counterparts; 1 = target pieces at their own layout.</summary>
    public double Progress => _progress.Value;

    /// <summary>Velocity of <see cref="Progress"/> per second.</summary>
    public double Velocity => _progress.Velocity;

    /// <summary>True while the target pieces carry flight transforms.</summary>
    public bool IsActive => _active;

    /// <summary>True while the source pieces are hidden.</summary>
    public bool IsSourceHidden => _hidden.Count > 0;

    /// <summary>
    /// When a flight lands on the source (progress 0), show the source and drop the transforms at once (default true).
    /// Set false to hand content back first and call <see cref="Reset"/> yourself in the same frame.
    /// </summary>
    public bool RevealsSourceOnArrival { get; set; } = true;

    /// <summary>The keys paired by the last flight (both sides present, visible and sized), parents first.</summary>
    public IReadOnlyList<string> PairedKeys
    {
        get
        {
            var keys = new string[_pairs.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = _pairs[i].Key;
            }
            return keys;
        }
    }

    /// <summary>Raised after the pieces were placed for a new progress value.</summary>
    public event Action<double>? ProgressChanged;

    /// <summary>
    /// Flies to <paramref name="progress"/> (1 = target layout, 0 = on the source) with <paramref name="spec"/>, keeping
    /// the current velocity. Completes with true when it arrived (false when interrupted). Arriving at 1 drops the
    /// transforms and keeps the source hidden (the target stands in); arriving at 0 reveals the source unless
    /// <see cref="RevealsSourceOnArrival"/> is false. Honors the motion level of <see cref="Source"/> like
    /// <see cref="MotionValue{T}"/> (Off and Reduced jump).
    /// </summary>
    public async Task<bool> FlyAsync(double progress, MotionSpec spec)
    {
        progress = Math.Clamp(progress, 0, 1);
        Begin();
        var generation = ++_generation;
        var arrived = await _progress.AnimateTo(progress, spec);
        if (!arrived || generation != _generation)
        {
            return false;
        }
        Arrive(progress);
        return true;
    }

    /// <summary>Jumps to <paramref name="progress"/> (pieces placed at once) with the same arrival rules as <see cref="FlyAsync"/>.</summary>
    public void Jump(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        Begin();
        _generation++;
        _progress.Jump(progress);
        Arrive(progress);
    }

    /// <summary>Shows the source pieces again.</summary>
    public void RevealSource()
    {
        foreach (var token in _hidden)
        {
            token.Dispose();
        }
        _hidden.Clear();
    }

    /// <summary>Hides the source pieces (the target stands in for them) without flying.</summary>
    public void HideSource()
    {
        if (_pairs.Count == 0)
        {
            Collect();
        }
        HideSourceCore();
    }

    /// <summary>Stops any flight, drops every transform and shows the source.</summary>
    public void Reset()
    {
        _generation++;
        _progress.Stop();
        ClearTransforms();
        RevealSource();
        _pairs.Clear();
    }

    public void Dispose()
    {
        Reset();
        _progress.Dispose();
    }

    private void Begin()
    {
        if (_active)
        {
            return;
        }
        var root = TopLevel.GetTopLevel(Target) as Visual ?? TopLevel.GetTopLevel(Source);
        if (root is null || TopLevel.GetTopLevel(Source) is null)
        {
            return;
        }
        _root = root;
        Collect();
        HideSourceCore();
        foreach (var pair in _pairs)
        {
            _movers.Add(pair.Target);
            pair.OriginToken = pair.Target.SetValue(Visual.RenderTransformOriginProperty, s_topLeft, BindingPriority.Animation);
            pair.TransformToken = pair.Target.SetValue(Visual.RenderTransformProperty, pair.Transform, BindingPriority.Animation);
        }
        _active = _pairs.Count > 0;
        Apply(_progress.Value);
    }

    private void Arrive(double progress)
    {
        if (progress >= 1)
        {
            ClearTransforms();
        }
        else if (progress <= 0 && RevealsSourceOnArrival)
        {
            ClearTransforms();
            RevealSource();
        }
    }

    private void Collect()
    {
        _pairs.Clear();
        var root = _root ?? TopLevel.GetTopLevel(Target);
        var sources = new Dictionary<string, Visual>(StringComparer.Ordinal);
        foreach (var visual in Source.GetSelfAndVisualDescendants())
        {
            if (SharedElement.GetKey(visual) is { Length: > 0 } key && !sources.ContainsKey(key) && IsShown(visual))
            {
                sources[key] = visual;
            }
        }
        foreach (var visual in Target.GetSelfAndVisualDescendants())
        {
            if (SharedElement.GetKey(visual) is not { Length: > 0 } key || !sources.Remove(key, out var source) || !IsShown(visual))
            {
                continue;
            }
            _pairs.Add(new Pair
            {
                Key = key,
                Source = source,
                Target = visual,
                Mode = SharedElement.GetMode(visual),
                SourceRadius = RadiusOf(source),
                Depth = DepthOf(visual, root)
            });
        }
        // Parents are placed before their children, so a child reads its parent's transform of this frame.
        _pairs.Sort(static (a, b) => a.Depth.CompareTo(b.Depth));
    }

    private static bool IsShown(Visual visual) => visual.IsEffectivelyVisible && visual.Bounds.Width > 0 && visual.Bounds.Height > 0;

    private static int DepthOf(Visual visual, Visual? root)
    {
        var depth = 0;
        for (var current = visual; current is not null && !ReferenceEquals(current, root); current = current.GetVisualParent())
        {
            depth++;
        }
        return depth;
    }

    private static CornerRadius RadiusOf(Visual visual) => visual switch
    {
        SharedElementFrame frame => frame.CurrentRadius,
        Border border => border.CornerRadius,
        TemplatedControl control => control.CornerRadius,
        _ => default
    };

    private void HideSourceCore()
    {
        if (_hidden.Count > 0)
        {
            return;
        }
        foreach (var pair in _pairs)
        {
            if (pair.Source.SetValue(Visual.OpacityProperty, 0d, BindingPriority.Animation) is { } token)
            {
                _hidden.Add(token);
            }
        }
    }

    private void ClearTransforms()
    {
        foreach (var pair in _pairs)
        {
            pair.TransformToken?.Dispose();
            pair.OriginToken?.Dispose();
            pair.TransformToken = null;
            pair.OriginToken = null;
            pair.Transform.Matrix = Matrix.Identity;
            if (pair.Target is SharedElementFrame frame)
            {
                frame.EndFlight();
            }
        }
        _movers.Clear();
        _active = false;
    }

    private void Apply(double progress)
    {
        if (!_active)
        {
            ProgressChanged?.Invoke(progress);
            return;
        }
        foreach (var pair in _pairs)
        {
            var target = pair.Target;
            var size = target.Bounds.Size;
            if (size.Width <= 0 || size.Height <= 0 ||
                !TryChain(pair.Source, includeSelf: true, skipMovers: false, out var sourceChain) ||
                !TryChain(target, includeSelf: true, skipMovers: true, out var naturalChain) ||
                !TryChain(target, includeSelf: false, skipMovers: false, out var chain))
            {
                continue;
            }
            var source = new Rect(pair.Source.Bounds.Size).TransformToAABB(sourceChain);
            var natural = new Rect(size).TransformToAABB(naturalChain);
            var flight = SharedElementMath.FlightRect(pair.Mode, source, natural, progress);
            pair.Transform.Matrix = SharedElementMath.Place(size, chain, flight);
            if (target is SharedElementFrame frame && natural.Width > 0 && natural.Height > 0)
            {
                frame.ApplyFlight(flight.Width / natural.Width, flight.Height / natural.Height,
                    SharedElementMath.Lerp(pair.SourceRadius, frame.CornerRadius, progress));
            }
        }
        ProgressChanged?.Invoke(progress);
    }

    /// <summary>
    /// Local → root matrix of <paramref name="visual"/> (layout offsets and render transforms, like
    /// <c>TransformToVisual</c>), optionally without its own transform and without the transforms this scope applies.
    /// </summary>
    private bool TryChain(Visual visual, bool includeSelf, bool skipMovers, out Matrix matrix)
    {
        matrix = Matrix.Identity;
        var current = visual;
        var self = true;
        while (current is not null && !ReferenceEquals(current, _root))
        {
            if ((includeSelf || !self) && current.RenderTransform is { } transform && !(skipMovers && _movers.Contains(current)))
            {
                var origin = current.RenderTransformOrigin.ToPixels(current.Bounds.Size);
                matrix *= Matrix.CreateTranslation(-origin.X, -origin.Y) * transform.Value * Matrix.CreateTranslation(origin.X, origin.Y);
            }
            var position = current.Bounds.Position;
            matrix *= Matrix.CreateTranslation(position.X, position.Y);
            current = current.GetVisualParent();
            self = false;
        }
        return current is not null;
    }
}
