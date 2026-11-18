using System.Numerics;
using System.Runtime.CompilerServices;

namespace TKCode.Arc.Graphics3D;

/// <summary>Raised when an action reached the end of its clip (or, playing backward, its start) and will not loop again.</summary>
public readonly struct AnimationFinishedEvent
{
    internal AnimationFinishedEvent(AnimationAction action, int direction)
    {
        Action = action;
        Direction = direction;
    }

    /// <summary>The action that finished.</summary>
    public AnimationAction Action { get; }

    /// <summary>1 when it finished playing forward, -1 backward.</summary>
    public int Direction { get; }
}

/// <summary>Raised when an action wrapped around the end of its clip and continues.</summary>
public readonly struct AnimationLoopedEvent
{
    internal AnimationLoopedEvent(AnimationAction action, int loopDelta)
    {
        Action = action;
        LoopDelta = loopDelta;
    }

    /// <summary>The action that looped.</summary>
    public AnimationAction Action { get; }

    /// <summary>How many passes were completed in the update: positive forward, negative backward.</summary>
    public int LoopDelta { get; }
}

/// <summary>Plays animation clips: advances the scheduled actions and blends what they animate. When several actions
/// animate the same property, their values are averaged by weight; when the weights sum to less than 1, the rest
/// comes from the value the property had before it was animated, which is also restored when the last action on the
/// property stops. Tracks made with a setter (<see cref="TrackProperty.Custom"/>) have no such rest value: they are
/// averaged by weight only and keep their last value. Call <see cref="Update"/> once per frame.</summary>
public sealed class AnimationMixer
{
    private readonly Dictionary<AnimationClip, AnimationAction> _actions = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<BindingKey, PropertyBinding> _bindings = [];
    private readonly List<AnimationAction> _active = [];
    private readonly List<PropertyBinding> _activeBindings = [];
    private readonly List<PendingEvent> _events = [];

    /// <summary>The mixer's clock in seconds: the sum of all update deltas, scaled by <see cref="TimeScale"/>.</summary>
    public double Time { get; private set; }

    /// <summary>Speed of everything the mixer plays; 0 freezes it, negative plays backward.</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>Number of scheduled actions (played and not stopped).</summary>
    public int ScheduledActionCount => _active.Count;

    /// <summary>Raised after an update in which an action finished.</summary>
    public event Action<AnimationFinishedEvent>? Finished;

    /// <summary>Raised after an update in which an action looped.</summary>
    public event Action<AnimationLoopedEvent>? Looped;

    /// <summary>The action that plays a clip in this mixer; created on first use, then always the same instance.</summary>
    public AnimationAction ClipAction(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (_actions.TryGetValue(clip, out var action))
        {
            return action;
        }

        var tracks = clip.Tracks;
        var bindings = new PropertyBinding[tracks.Length];
        for (var i = 0; i < tracks.Length; i++)
        {
            var track = tracks[i];
            var key = new BindingKey(track);
            if (!_bindings.TryGetValue(key, out var binding))
            {
                binding = new PropertyBinding(track);
                _bindings.Add(key, binding);
            }
            else if (binding.Size != track.ValueSize)
            {
                // Undo the references taken so far, so a failed call leaves no trace.
                for (var j = 0; j < i; j++)
                {
                    Release(tracks[j], bindings[j]);
                }

                throw new ArgumentException(
                    $"Track {i} of clip '{clip.Name}' animates {track.Property} with {track.ValueSize} values, but another clip animates the same property with {binding.Size}.",
                    nameof(clip));
            }

            binding.ReferenceCount++;
            bindings[i] = binding;
        }

        action = new AnimationAction(this, clip, bindings);
        _actions.Add(clip, action);
        return action;
    }

    /// <summary>The action of a clip when <see cref="ClipAction"/> was called for it, or null.</summary>
    public AnimationAction? ExistingAction(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return _actions.GetValueOrDefault(clip);
    }

    /// <summary>Advances the mixer by a time step and applies the blended poses.</summary>
    /// <returns>True when at least one property changed, so the scene needs to be drawn again.</returns>
    public bool Update(float deltaSeconds)
    {
        deltaSeconds *= TimeScale;
        var time = Time += deltaSeconds;
        var direction = MathF.Sign(deltaSeconds);
        var active = _active;
        for (var i = 0; i < active.Count; i++)
        {
            active[i].Update(time, deltaSeconds, direction);
        }

        // Backward, so a setter that stops an action (which swaps the last binding into its place) skips nothing.
        var changed = false;
        var bindings = _activeBindings;
        for (var i = bindings.Count - 1; i >= 0; i--)
        {
            if (i < bindings.Count)
            {
                changed |= bindings[i].Apply();
            }
        }

        // Events last: handlers see the applied state and may play or stop actions freely.
        if (_events.Count > 0)
        {
            DispatchEvents();
        }

        return changed;
    }

    /// <summary>Jumps to an absolute time: the mixer's clock and every action restart from 0 and advance by
    /// <paramref name="seconds"/>.</summary>
    /// <returns>True when at least one property changed.</returns>
    public bool SetTime(float seconds)
    {
        Time = 0d;
        foreach (var action in _actions.Values)
        {
            action.Time = 0f;
        }

        return Update(seconds);
    }

    /// <summary>Stops every scheduled action.</summary>
    public void StopAllActions()
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            if (i < _active.Count)
            {
                _active[i].Stop();
            }
        }
    }

    /// <summary>Stops the action of a clip and forgets it, releasing the mixer's references to the clip and to the
    /// objects only this clip animated.</summary>
    public void Uncache(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!_actions.Remove(clip, out var action))
        {
            return;
        }

        Deactivate(action);
        var tracks = clip.Tracks;
        for (var i = 0; i < tracks.Length; i++)
        {
            Release(tracks[i], action.Bindings[i]);
        }
    }

    private void Release(KeyframeTrack track, PropertyBinding binding)
    {
        if (--binding.ReferenceCount <= 0)
        {
            _bindings.Remove(new BindingKey(track));
        }
    }

    internal void Activate(AnimationAction action)
    {
        if (action.ActiveIndex >= 0)
        {
            return;
        }

        if (!_actions.TryGetValue(action.Clip, out var cached) || !ReferenceEquals(cached, action))
        {
            throw new InvalidOperationException("The action was removed from its mixer with Uncache; get a new one from ClipAction.");
        }

        action.ActiveIndex = _active.Count;
        _active.Add(action);
        var bindings = action.Bindings;
        for (var i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            if (binding.UseCount++ == 0)
            {
                binding.SaveOriginal();
                binding.ActiveIndex = _activeBindings.Count;
                _activeBindings.Add(binding);
            }
        }
    }

    internal void Deactivate(AnimationAction action)
    {
        var index = action.ActiveIndex;
        if (index < 0)
        {
            return;
        }

        var last = _active[^1];
        _active[index] = last;
        last.ActiveIndex = index;
        _active.RemoveAt(_active.Count - 1);
        action.ActiveIndex = -1;
        var bindings = action.Bindings;
        for (var i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            if (--binding.UseCount == 0)
            {
                binding.RestoreOriginal();
                var bindingIndex = binding.ActiveIndex;
                var lastBinding = _activeBindings[^1];
                _activeBindings[bindingIndex] = lastBinding;
                lastBinding.ActiveIndex = bindingIndex;
                _activeBindings.RemoveAt(_activeBindings.Count - 1);
                binding.ActiveIndex = -1;
            }
        }
    }

    internal void QueueFinished(AnimationAction action, int direction) => _events.Add(new PendingEvent(action, direction, false));

    internal void QueueLooped(AnimationAction action, int loopDelta) => _events.Add(new PendingEvent(action, loopDelta, true));

    private void DispatchEvents()
    {
        // Handlers may update the mixer again; index-based so that events added meanwhile are delivered too.
        for (var i = 0; i < _events.Count; i++)
        {
            var e = _events[i];
            if (e.IsLoop)
            {
                Looped?.Invoke(new AnimationLoopedEvent(e.Action, e.Value));
            }
            else
            {
                Finished?.Invoke(new AnimationFinishedEvent(e.Action, e.Value));
            }
        }

        _events.Clear();
    }

    private readonly struct PendingEvent
    {
        public PendingEvent(AnimationAction action, int value, bool isLoop)
        {
            Action = action;
            Value = value;
            IsLoop = isLoop;
        }

        public AnimationAction Action { get; }

        public int Value { get; }

        public bool IsLoop { get; }
    }

    /// <summary>Identifies what a track animates: an object and one of its properties, or a setter.</summary>
    private readonly struct BindingKey : IEquatable<BindingKey>
    {
        private readonly object _owner;
        private readonly TrackProperty _property;

        public BindingKey(KeyframeTrack track)
        {
            _property = track.Property;
            _owner = track.Property == TrackProperty.Custom ? track.Setter! : track.Target!;
        }

        public bool Equals(BindingKey other)
        {
            if (_property != other._property)
            {
                return false;
            }

            // Objects by identity; setters by delegate equality (same method on the same instance).
            return _property == TrackProperty.Custom ? _owner.Equals(other._owner) : ReferenceEquals(_owner, other._owner);
        }

        public override bool Equals(object? obj) => obj is BindingKey other && Equals(other);

        public override int GetHashCode() =>
            ((_property == TrackProperty.Custom ? _owner.GetHashCode() : RuntimeHelpers.GetHashCode(_owner)) * 397) ^ (int)_property;
    }
}

/// <summary>One animated property of one object, shared by every action that animates it. Collects the weighted
/// samples of an update and writes the blend to the property.</summary>
internal sealed class PropertyBinding
{
    private readonly Object3D? _target;

    // The track whose setter a custom binding calls (kept as the track so this file does not name the delegate type).
    private readonly KeyframeTrack _track;
    private readonly TrackProperty _property;
    private readonly float[] _buffer;
    private float _cumulativeWeight;
    private bool _hasApplied;

    public PropertyBinding(KeyframeTrack track)
    {
        _target = track.Target;
        _track = track;
        _property = track.Property;
        Size = track.ValueSize;

        // Four values side by side: the incoming sample, the blend, the rest value, the value last written.
        _buffer = new float[Size * 4];
    }

    /// <summary>Floats per value.</summary>
    public int Size { get; }

    /// <summary>Cached actions that have a track on this property.</summary>
    public int ReferenceCount { get; set; }

    /// <summary>Scheduled actions that have a track on this property.</summary>
    public int UseCount { get; set; }

    /// <summary>Index in the mixer's list of bindings in use, or -1.</summary>
    public int ActiveIndex { get; set; } = -1;

    /// <summary>Where an action writes its sample before calling <see cref="Accumulate"/>.</summary>
    public Span<float> Incoming => _buffer.AsSpan(0, Size);

    private Span<float> Blend => _buffer.AsSpan(Size, Size);

    private Span<float> Original => _buffer.AsSpan(Size * 2, Size);

    private Span<float> Applied => _buffer.AsSpan(Size * 3, Size);

    private bool HasOriginal => _property != TrackProperty.Custom;

    /// <summary>Adds the incoming sample to the blend with a weight.</summary>
    public void Accumulate(float weight)
    {
        if (_cumulativeWeight == 0f)
        {
            Incoming.CopyTo(Blend);
            _cumulativeWeight = weight;
            return;
        }

        // A running weighted average: after n samples the blend holds sum(w_i * v_i) / sum(w_i).
        _cumulativeWeight += weight;
        Mix(Blend, Incoming, weight / _cumulativeWeight);
    }

    /// <summary>Finishes the update: fills the missing weight from the rest value and writes the result when it changed.</summary>
    public bool Apply()
    {
        var weight = _cumulativeWeight;
        _cumulativeWeight = 0f;
        var blend = Blend;
        if (HasOriginal)
        {
            if (weight <= 0f)
            {
                Original.CopyTo(blend);
            }
            else if (weight < 1f)
            {
                Mix(blend, Original, 1f - weight);
            }
        }
        else if (weight <= 0f)
        {
            return false;
        }

        if (_property == TrackProperty.Rotation)
        {
            var q = new Quaternion(blend[0], blend[1], blend[2], blend[3]);
            var length = q.Length();
            q = length > 1e-12f ? q * (1f / length) : Quaternion.Identity;
            blend[0] = q.X;
            blend[1] = q.Y;
            blend[2] = q.Z;
            blend[3] = q.W;
        }

        var applied = Applied;
        if (_hasApplied && blend.SequenceEqual(applied))
        {
            return false;
        }

        blend.CopyTo(applied);
        _hasApplied = true;
        Write(blend);
        return true;
    }

    /// <summary>Remembers the property's current value as its rest value.</summary>
    public void SaveOriginal()
    {
        _cumulativeWeight = 0f;
        if (!HasOriginal)
        {
            _hasApplied = false;
            return;
        }

        var original = Original;
        switch (_property)
        {
            case TrackProperty.Position:
            {
                var v = _target!.Position;
                original[0] = v.X;
                original[1] = v.Y;
                original[2] = v.Z;
                break;
            }

            case TrackProperty.Scale:
            {
                var v = _target!.Scale;
                original[0] = v.X;
                original[1] = v.Y;
                original[2] = v.Z;
                break;
            }

            case TrackProperty.Rotation:
            {
                var q = _target!.Rotation;
                original[0] = q.X;
                original[1] = q.Y;
                original[2] = q.Z;
                original[3] = q.W;
                break;
            }

            default:
            {
                original.Clear();
                if (_target is Mesh { MorphWeights: { } weights })
                {
                    weights.AsSpan(0, Math.Min(weights.Length, Size)).CopyTo(original);
                }

                break;
            }
        }

        original.CopyTo(Applied);
        _hasApplied = true;
    }

    /// <summary>Writes the rest value back to the property.</summary>
    public void RestoreOriginal()
    {
        if (HasOriginal)
        {
            Write(Original);
        }
    }

    private void Mix(Span<float> destination, ReadOnlySpan<float> source, float t)
    {
        if (_property == TrackProperty.Rotation)
        {
            var q = Quaternion.Slerp(
                new Quaternion(destination[0], destination[1], destination[2], destination[3]),
                new Quaternion(source[0], source[1], source[2], source[3]),
                t);
            destination[0] = q.X;
            destination[1] = q.Y;
            destination[2] = q.Z;
            destination[3] = q.W;
            return;
        }

        var s = 1f - t;
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = (destination[i] * s) + (source[i] * t);
        }
    }

    private void Write(ReadOnlySpan<float> value)
    {
        switch (_property)
        {
            case TrackProperty.Position:
                _target!.Position = new Vector3(value[0], value[1], value[2]);
                break;
            case TrackProperty.Scale:
                _target!.Scale = new Vector3(value[0], value[1], value[2]);
                break;
            case TrackProperty.Rotation:
                _target!.Rotation = new Quaternion(value[0], value[1], value[2], value[3]);
                break;
            case TrackProperty.MorphWeights:
                if (_target is Mesh mesh)
                {
                    var weights = mesh.MorphWeights;
                    if (weights is null || weights.Length < value.Length)
                    {
                        // Once per mesh: keeps what was there and makes room for every animated weight.
                        var grown = new float[value.Length];
                        weights?.CopyTo(grown, 0);
                        mesh.MorphWeights = weights = grown;
                    }

                    value.CopyTo(weights);
                }

                break;
            default:
                _track.Setter!(value);
                break;
        }
    }
}
