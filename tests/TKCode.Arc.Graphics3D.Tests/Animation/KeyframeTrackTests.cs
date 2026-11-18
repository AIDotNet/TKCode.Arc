using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests.Animation;

public class KeyframeTrackTests
{
    private static Vector3 Sample3(KeyframeTrack track, float time)
    {
        Span<float> v = stackalloc float[3];
        track.Sample(time, v);
        return new Vector3(v[0], v[1], v[2]);
    }

    private static Quaternion SampleQ(KeyframeTrack track, float time)
    {
        Span<float> v = stackalloc float[4];
        track.Sample(time, v);
        return new Quaternion(v[0], v[1], v[2], v[3]);
    }

    private static void ShouldBeClose(Vector3 actual, Vector3 expected, float tolerance = 1e-5f) =>
        Vector3.Distance(actual, expected).ShouldBeLessThan(tolerance, $"expected {expected} but was {actual}");

    [Fact]
    public void Linear_Interpolates_Between_Keys()
    {
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f, 1f, 3f], [0f, 0f, 0f, 2f, 4f, 6f, 2f, 0f, 6f]);

        track.ValueSize.ShouldBe(3);
        track.Duration.ShouldBe(3f);
        ShouldBeClose(Sample3(track, 0.5f), new Vector3(1f, 2f, 3f));
        ShouldBeClose(Sample3(track, 1f), new Vector3(2f, 4f, 6f));
        ShouldBeClose(Sample3(track, 2f), new Vector3(2f, 2f, 6f));
    }

    [Fact]
    public void Time_Is_Clamped_To_First_And_Last_Key()
    {
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Scale, [1f, 2f], [1f, 1f, 1f, 3f, 3f, 3f]);

        ShouldBeClose(Sample3(track, -5f), Vector3.One);
        ShouldBeClose(Sample3(track, 0.99f), Vector3.One);
        ShouldBeClose(Sample3(track, 2f), new Vector3(3f));
        ShouldBeClose(Sample3(track, 100f), new Vector3(3f));
    }

    [Theory]
    [InlineData(Interpolation.Step)]
    [InlineData(Interpolation.Linear)]
    public void Single_Key_Is_Constant(Interpolation interpolation)
    {
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Position, [0.5f], [7f, 8f, 9f], interpolation);

        ShouldBeClose(Sample3(track, 0f), new Vector3(7f, 8f, 9f));
        ShouldBeClose(Sample3(track, 0.5f), new Vector3(7f, 8f, 9f));
        ShouldBeClose(Sample3(track, 3f), new Vector3(7f, 8f, 9f));
    }

    [Fact]
    public void Single_Key_Cubic_Spline_Returns_The_Value_Not_A_Tangent()
    {
        // in-tangent, value, out-tangent
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f], [9f, 9f, 9f, 1f, 2f, 3f, -9f, -9f, -9f], Interpolation.CubicSpline);

        ShouldBeClose(Sample3(track, 0f), new Vector3(1f, 2f, 3f));
        ShouldBeClose(Sample3(track, 4f), new Vector3(1f, 2f, 3f));
    }

    [Fact]
    public void Step_Holds_The_Earlier_Key()
    {
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f, 1f, 2f], [0f, 0f, 0f, 1f, 1f, 1f, 5f, 5f, 5f], Interpolation.Step);

        ShouldBeClose(Sample3(track, 0.999f), Vector3.Zero);
        ShouldBeClose(Sample3(track, 1f), Vector3.One);
        ShouldBeClose(Sample3(track, 1.5f), Vector3.One);
        ShouldBeClose(Sample3(track, 2f), new Vector3(5f));
    }

    [Fact]
    public void Cubic_Spline_With_Zero_Tangents_Eases_Between_Keys()
    {
        float[] values =
        [
            0f, 0f, 0f, /* value */ 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, /* value */ 4f, 8f, -2f, 0f, 0f, 0f,
        ];
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f, 2f], values, Interpolation.CubicSpline);

        ShouldBeClose(Sample3(track, 0f), Vector3.Zero);
        ShouldBeClose(Sample3(track, 2f), new Vector3(4f, 8f, -2f));
        ShouldBeClose(Sample3(track, 1f), new Vector3(2f, 4f, -1f));

        // Hermite basis h01(0.25) = -2t^3 + 3t^2 = 0.15625.
        ShouldBeClose(Sample3(track, 0.5f), new Vector3(4f, 8f, -2f) * 0.15625f);
    }

    [Fact]
    public void Cubic_Spline_Uses_Tangents_Scaled_By_The_Key_Interval()
    {
        // Both values 0; out-tangent of key 0 is 1 per second, in-tangent of key 1 is -2 per second; interval 2 s.
        float[] values =
        [
            0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f,
            -2f, -2f, -2f, 0f, 0f, 0f, 0f, 0f, 0f,
        ];
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Position, [1f, 3f], values, Interpolation.CubicSpline);

        // t = 0.5: h10 = 0.125, h11 = -0.125 -> 0.125 * 2 * 1 + (-0.125) * 2 * (-2) = 0.75.
        ShouldBeClose(Sample3(track, 2f), new Vector3(0.75f));
    }

    [Fact]
    public void Rotation_Takes_The_Shortest_Path()
    {
        var from = Quaternion.Identity;

        // 350 degrees about Y: the same orientation as -10 degrees, stored with a negative w.
        var to = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtil.ToRadians(350f));
        to.W.ShouldBeLessThan(0f);
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Rotation, [0f, 1f], [from.X, from.Y, from.Z, from.W, to.X, to.Y, to.Z, to.W]);

        var mid = SampleQ(track, 0.5f);

        var direction = Vector3.Transform(Vector3.UnitZ, mid);
        var expected = Vector3.Transform(Vector3.UnitZ, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtil.ToRadians(-5f)));
        ShouldBeClose(direction, expected, 1e-4f);
        mid.Length().ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void Rotation_Linear_Is_Spherical()
    {
        var to = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f);
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Rotation, [0f, 2f], [0f, 0f, 0f, 1f, to.X, to.Y, to.Z, to.W]);

        var quarter = SampleQ(track, 0.5f);

        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 8f);
        MathF.Abs(Quaternion.Dot(quarter, expected)).ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void Rotation_Cubic_Spline_Is_Normalized()
    {
        var to = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 2f);
        float[] values =
        [
            0f, 0f, 0f, 0f, /* value */ 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, /* value */ to.X, to.Y, to.Z, to.W, 0f, 0f, 0f, 0f,
        ];
        var track = new KeyframeTrack(new Object3D(), TrackProperty.Rotation, [0f, 1f], values, Interpolation.CubicSpline);

        SampleQ(track, 0.3f).Length().ShouldBe(1f, 1e-5f);
        MathF.Abs(Quaternion.Dot(SampleQ(track, 1f), to)).ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void Morph_Weight_Track_Infers_The_Number_Of_Weights()
    {
        var track = new KeyframeTrack(new Object3D(), TrackProperty.MorphWeights, [0f, 1f], [0f, 0f, 0f, 1f, 0.5f, 0f]);

        track.ValueSize.ShouldBe(3);
        ShouldBeClose(Sample3(track, 0.5f), new Vector3(0.5f, 0.25f, 0f));
    }

    [Fact]
    public void Morph_Weight_Cubic_Spline_Track_Infers_The_Number_Of_Weights()
    {
        var track = new KeyframeTrack(new Object3D(), TrackProperty.MorphWeights, [0f, 1f], new float[2 * 3 * 2], Interpolation.CubicSpline);

        track.ValueSize.ShouldBe(2);
    }

    [Fact]
    public void Custom_Track_Samples_Its_Value_Size()
    {
        var track = new KeyframeTrack(_ => { }, 2, [0f, 4f], [0f, 10f, 4f, 30f]);
        Span<float> v = stackalloc float[2];

        track.Sample(1f, v);

        track.Property.ShouldBe(TrackProperty.Custom);
        track.Target.ShouldBeNull();
        v[0].ShouldBe(1f, 1e-6f);
        v[1].ShouldBe(15f, 1e-5f);
    }

    [Fact]
    public void Wrong_Value_Count_Is_Rejected()
    {
        Should.Throw<ArgumentException>(() => new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f, 1f], [0f, 0f, 0f, 1f]));
        Should.Throw<ArgumentException>(() => new KeyframeTrack(new Object3D(), TrackProperty.Rotation, [0f], [0f, 0f, 1f]));
        Should.Throw<ArgumentException>(() => new KeyframeTrack(new Object3D(), TrackProperty.Position, [], []));
        Should.Throw<ArgumentException>(() => new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f], [0f, 0f, 0f], Interpolation.CubicSpline));
        Should.Throw<ArgumentException>(() => new KeyframeTrack(_ => { }, 2, [0f, 1f], [0f, 1f, 2f]));
        Should.Throw<ArgumentException>(() => new KeyframeTrack(new Object3D(), TrackProperty.Custom, [0f], [0f]));
    }

    [Fact]
    public void Clip_Duration_Is_The_Longest_Track_Unless_Given()
    {
        var a = new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f, 2f], new float[6]);
        var b = new KeyframeTrack(new Object3D(), TrackProperty.Scale, [0f, 5f], new float[6]);

        new AnimationClip("auto", [a, b]).Duration.ShouldBe(5f);
        new AnimationClip("given", [a, b], 3f).Duration.ShouldBe(3f);
        new AnimationClip("empty", []).Duration.ShouldBe(0f);
    }

    [Fact]
    public void Many_Keys_Are_Found_By_Search()
    {
        const int Keys = 1000;
        var times = new float[Keys];
        var values = new float[Keys * 3];
        for (var i = 0; i < Keys; i++)
        {
            times[i] = i * 0.1f;
            values[i * 3] = i;
        }

        var track = new KeyframeTrack(new Object3D(), TrackProperty.Position, times, values);

        for (var i = 0; i < Keys - 1; i += 37)
        {
            Sample3(track, (i * 0.1f) + 0.05f).X.ShouldBe(i + 0.5f, 1e-2f);
        }
    }
}
