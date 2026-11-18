using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>What a keyframe track animates on its target.</summary>
public enum TrackProperty
{
    /// <summary><see cref="Object3D.Position"/> (3 values per key).</summary>
    Position,

    /// <summary><see cref="Object3D.Rotation"/> as a quaternion xyzw (4 values per key).</summary>
    Rotation,

    /// <summary><see cref="Object3D.Scale"/> (3 values per key).</summary>
    Scale,

    /// <summary><see cref="Mesh.MorphWeights"/> (one value per morph target per key).</summary>
    MorphWeights,

    /// <summary>Anything else: the track calls its own setter.</summary>
    Custom,
}

/// <summary>How values between two keys are computed.</summary>
public enum Interpolation
{
    /// <summary>The value of the earlier key holds until the next key.</summary>
    Step,

    /// <summary>Linear (rotations: spherical linear).</summary>
    Linear,

    /// <summary>Cubic Hermite spline as in glTF: each key stores in-tangent, value, out-tangent.</summary>
    CubicSpline,
}

/// <summary>Receives the sampled values of a custom track.</summary>
public delegate void TrackSetter(ReadOnlySpan<float> values);

/// <summary>Keyframes of one property of one object: key times in seconds (ascending) and the values at those times.</summary>
public sealed class KeyframeTrack
{
    /// <summary>Creates a track for a transform property or morph weights of an object.</summary>
    public KeyframeTrack(Object3D target, TrackProperty property, float[] times, float[] values, Interpolation interpolation = Interpolation.Linear)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (property == TrackProperty.Custom)
        {
            throw new ArgumentException("Use the constructor that takes a setter for custom tracks.", nameof(property));
        }

        Property = property;
        Times = times ?? throw new ArgumentNullException(nameof(times));
        Values = values ?? throw new ArgumentNullException(nameof(values));
        Interpolation = interpolation;
        ValueSize = Validate();
    }

    /// <summary>Creates a track that hands its sampled values to a setter (for material uniforms, light intensity, ...).</summary>
    public KeyframeTrack(TrackSetter setter, int valueSize, float[] times, float[] values, Interpolation interpolation = Interpolation.Linear)
    {
        Setter = setter ?? throw new ArgumentNullException(nameof(setter));
        Property = TrackProperty.Custom;
        Times = times ?? throw new ArgumentNullException(nameof(times));
        Values = values ?? throw new ArgumentNullException(nameof(values));
        Interpolation = interpolation;
        ValueSize = valueSize;
        Validate();
    }

    /// <summary>The animated object; null for custom tracks.</summary>
    public Object3D? Target { get; }

    /// <summary>The setter of a custom track.</summary>
    public TrackSetter? Setter { get; }

    public TrackProperty Property { get; }

    /// <summary>Key times in seconds, ascending.</summary>
    public float[] Times { get; }

    /// <summary><see cref="ValueSize"/> floats per key (three times as many with <see cref="Interpolation.CubicSpline"/>).</summary>
    public float[] Values { get; }

    public Interpolation Interpolation { get; }

    /// <summary>Floats per sampled value.</summary>
    public int ValueSize { get; }

    /// <summary>The time of the last key.</summary>
    public float Duration => Times.Length > 0 ? Times[^1] : 0f;

    private int Validate()
    {
        if (Times.Length == 0)
        {
            throw new ArgumentException("A track needs at least one key.");
        }

        var perKey = Interpolation == Interpolation.CubicSpline ? 3 : 1;
        var size = Property switch
        {
            TrackProperty.Position or TrackProperty.Scale => 3,
            TrackProperty.Rotation => 4,
            TrackProperty.Custom => ValueSize,
            _ => Values.Length / (Times.Length * perKey),
        };
        if (size <= 0 || Values.Length != Times.Length * perKey * size)
        {
            throw new ArgumentException($"The track has {Times.Length} keys of {size} values{(perKey == 3 ? " with tangents" : "")} but {Values.Length} values.");
        }

        return size;
    }

    /// <summary>Computes the value at a time (clamped to the first and last key) into <paramref name="result"/>.</summary>
    public void Sample(float time, Span<float> result)
    {
        var times = Times;
        var size = ValueSize;
        var cubic = Interpolation == Interpolation.CubicSpline;
        var stride = cubic ? size * 3 : size;
        var valueOffset = cubic ? size : 0;
        var last = times.Length - 1;
        if (time <= times[0] || last == 0)
        {
            Values.AsSpan(valueOffset, size).CopyTo(result);
            return;
        }

        if (time >= times[last])
        {
            Values.AsSpan((last * stride) + valueOffset, size).CopyTo(result);
            return;
        }

        // Binary search for the key at or before the time.
        int lo = 0, hi = last;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) >> 1;
            if (times[mid] <= time)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        var t0 = times[lo];
        var dt = times[hi] - t0;
        var t = dt > 0f ? (time - t0) / dt : 0f;
        var a = Values.AsSpan((lo * stride) + valueOffset, size);
        var b = Values.AsSpan((hi * stride) + valueOffset, size);
        switch (Interpolation)
        {
            case Interpolation.Step:
                a.CopyTo(result);
                break;
            case Interpolation.CubicSpline:
            {
                var outTangent = Values.AsSpan((lo * stride) + (size * 2), size);
                var inTangent = Values.AsSpan(hi * stride, size);
                float t2 = t * t, t3 = t2 * t;
                float h00 = (2f * t3) - (3f * t2) + 1f, h10 = t3 - (2f * t2) + t, h01 = (-2f * t3) + (3f * t2), h11 = t3 - t2;
                for (var i = 0; i < size; i++)
                {
                    result[i] = (h00 * a[i]) + (h10 * dt * outTangent[i]) + (h01 * b[i]) + (h11 * dt * inTangent[i]);
                }

                if (Property == TrackProperty.Rotation)
                {
                    var q = Quaternion.Normalize(new Quaternion(result[0], result[1], result[2], result[3]));
                    result[0] = q.X;
                    result[1] = q.Y;
                    result[2] = q.Z;
                    result[3] = q.W;
                }

                break;
            }

            default:
                if (Property == TrackProperty.Rotation)
                {
                    var q = Quaternion.Slerp(new Quaternion(a[0], a[1], a[2], a[3]), new Quaternion(b[0], b[1], b[2], b[3]), t);
                    result[0] = q.X;
                    result[1] = q.Y;
                    result[2] = q.Z;
                    result[3] = q.W;
                }
                else
                {
                    for (var i = 0; i < size; i++)
                    {
                        result[i] = a[i] + ((b[i] - a[i]) * t);
                    }
                }

                break;
        }
    }
}

/// <summary>A named set of tracks that play together, such as "Walk".</summary>
public sealed class AnimationClip
{
    /// <summary>Creates a clip. A negative duration means "the end of the longest track".</summary>
    public AnimationClip(string name, KeyframeTrack[] tracks, float duration = -1f)
    {
        Name = name ?? "";
        Tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        if (duration < 0f)
        {
            duration = 0f;
            foreach (var track in tracks)
            {
                duration = MathF.Max(duration, track.Duration);
            }
        }

        Duration = duration;
    }

    public string Name { get; }
    public KeyframeTrack[] Tracks { get; }

    /// <summary>Length in seconds.</summary>
    public float Duration { get; }
}
