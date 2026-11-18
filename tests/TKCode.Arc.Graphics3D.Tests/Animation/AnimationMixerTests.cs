using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests.Animation;

public class AnimationMixerTests
{
    /// <summary>A clip that moves the target's X from <paramref name="from"/> to <paramref name="to"/> in a duration.</summary>
    private static AnimationClip MoveX(Object3D target, float from, float to, float duration = 1f, string name = "move") =>
        new(name, [new KeyframeTrack(target, TrackProperty.Position, [0f, duration], [from, 0f, 0f, to, 0f, 0f])]);

    [Fact]
    public void ClipAction_Returns_One_Action_Per_Clip()
    {
        var mixer = new AnimationMixer();
        var clip = MoveX(new Object3D(), 0f, 1f);

        var action = mixer.ClipAction(clip);

        mixer.ClipAction(clip).ShouldBeSameAs(action);
        mixer.ExistingAction(clip).ShouldBeSameAs(action);
        mixer.ExistingAction(MoveX(new Object3D(), 0f, 1f)).ShouldBeNull();
        action.Clip.ShouldBeSameAs(clip);
        action.Mixer.ShouldBeSameAs(mixer);
    }

    [Fact]
    public void Nothing_Happens_Before_Play()
    {
        var target = new Object3D { Position = new Vector3(5f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f));

        mixer.Update(0.5f).ShouldBeFalse();

        target.Position.X.ShouldBe(5f);
        action.IsScheduled.ShouldBeFalse();
        action.IsRunning.ShouldBeFalse();
        mixer.ScheduledActionCount.ShouldBe(0);
    }

    [Fact]
    public void Update_Advances_And_Applies()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f, 2f)).Play();

        mixer.Update(0.5f).ShouldBeTrue();

        action.IsRunning.ShouldBeTrue();
        action.Time.ShouldBe(0.5f, 1e-6f);
        target.Position.X.ShouldBe(2.5f, 1e-5f);
        mixer.Time.ShouldBe(0.5d, 1e-6d);
    }

    [Fact]
    public void Update_Zero_Applies_The_First_Pose_And_Then_Reports_No_Change()
    {
        var target = new Object3D { Position = new Vector3(5f, 0f, 0f) };
        var mixer = new AnimationMixer();
        mixer.ClipAction(MoveX(target, 1f, 10f)).Play();

        mixer.Update(0f).ShouldBeTrue();
        target.Position.X.ShouldBe(1f);
        mixer.Update(0f).ShouldBeFalse();
    }

    [Fact]
    public void World_Matrix_Follows_The_Animated_Transform()
    {
        var parent = new Object3D();
        var child = new Object3D { Position = new Vector3(0f, 1f, 0f) };
        parent.Add(child);
        var mixer = new AnimationMixer();
        mixer.ClipAction(MoveX(parent, 0f, 4f)).Play();

        mixer.Update(0.5f);

        child.WorldPosition.X.ShouldBe(2f, 1e-5f);
        child.WorldPosition.Y.ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void Loop_Once_Finishes_And_Returns_To_The_Rest_Value()
    {
        var target = new Object3D { Position = new Vector3(5f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).SetLoop(AnimationLoop.Once).Play();
        var finished = new List<AnimationFinishedEvent>();
        mixer.Finished += finished.Add;

        mixer.Update(0.5f);
        target.Position.X.ShouldBe(5f, 1e-5f);
        finished.ShouldBeEmpty();
        mixer.Update(0.75f);

        finished.Count.ShouldBe(1);
        finished[0].Action.ShouldBeSameAs(action);
        finished[0].Direction.ShouldBe(1);
        action.Time.ShouldBe(1f);
        action.Enabled.ShouldBeFalse();
        action.IsRunning.ShouldBeFalse();
        action.IsScheduled.ShouldBeTrue();
        action.EffectiveWeight.ShouldBe(0f);
        target.Position.X.ShouldBe(5f);

        mixer.Update(1f).ShouldBeFalse();
        finished.Count.ShouldBe(1);
    }

    [Fact]
    public void Clamp_When_Finished_Holds_The_Last_Frame()
    {
        var target = new Object3D { Position = new Vector3(5f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f));
        action.Loop = AnimationLoop.Once;
        action.ClampWhenFinished = true;
        action.Play();

        mixer.Update(3f);

        action.Paused.ShouldBeTrue();
        action.Enabled.ShouldBeTrue();
        target.Position.X.ShouldBe(10f);
        mixer.Update(1f);
        target.Position.X.ShouldBe(10f);
    }

    [Fact]
    public void Finished_Action_Plays_Again_After_Reset()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).SetLoop(AnimationLoop.Once).Play();
        mixer.Update(2f);

        action.Reset().Play();
        mixer.Update(0.25f);

        action.IsRunning.ShouldBeTrue();
        target.Position.X.ShouldBe(2.5f, 1e-5f);
    }

    [Fact]
    public void Repeat_Wraps_And_Reports_Loops()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).Play();
        var loops = new List<AnimationLoopedEvent>();
        mixer.Looped += loops.Add;

        mixer.Update(0.75f);
        loops.ShouldBeEmpty();
        mixer.Update(0.5f);

        action.Time.ShouldBe(0.25f, 1e-5f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);
        loops.Count.ShouldBe(1);
        loops[0].Action.ShouldBeSameAs(action);
        loops[0].LoopDelta.ShouldBe(1);

        // Several passes in one step.
        mixer.Update(2.5f);
        loops.Count.ShouldBe(2);
        loops[1].LoopDelta.ShouldBe(2);
        action.Time.ShouldBe(0.75f, 1e-4f);
    }

    [Fact]
    public void Repetitions_Limit_The_Number_Of_Passes()
    {
        var target = new Object3D { Position = new Vector3(-1f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).SetLoop(AnimationLoop.Repeat, 2f);
        action.ClampWhenFinished = true;
        action.Play();
        var finished = 0;
        var looped = 0;
        mixer.Finished += _ => finished++;
        mixer.Looped += _ => looped++;

        mixer.Update(1.5f);
        looped.ShouldBe(1);
        finished.ShouldBe(0);
        target.Position.X.ShouldBe(5f, 1e-4f);
        mixer.Update(1f);

        finished.ShouldBe(1);
        looped.ShouldBe(1);
        action.Time.ShouldBe(1f);
        target.Position.X.ShouldBe(10f);
    }

    [Fact]
    public void Ping_Pong_Plays_Odd_Passes_Backward()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        mixer.ClipAction(MoveX(target, 0f, 10f)).SetLoop(AnimationLoop.PingPong).Play();

        mixer.Update(0.75f);
        target.Position.X.ShouldBe(7.5f, 1e-4f);
        mixer.Update(0.5f);
        target.Position.X.ShouldBe(7.5f, 1e-4f);
        mixer.Update(0.5f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);
        mixer.Update(0.5f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);
        mixer.Update(0.5f);
        target.Position.X.ShouldBe(7.5f, 1e-4f);
    }

    [Fact]
    public void Ping_Pong_With_Two_Repetitions_Ends_Where_It_Started()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 3f, 10f)).SetLoop(AnimationLoop.PingPong, 2f);
        action.ClampWhenFinished = true;
        action.Play();

        mixer.Update(1.5f);
        mixer.Update(1f);

        action.Paused.ShouldBeTrue();
        target.Position.X.ShouldBe(3f, 1e-4f);

        // The clamped pose holds in later updates.
        mixer.Update(1f);
        target.Position.X.ShouldBe(3f, 1e-4f);
    }

    [Fact]
    public void Ping_Pong_With_Three_Repetitions_Ends_At_The_Clip_End()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 3f, 10f)).SetLoop(AnimationLoop.PingPong, 3f);
        action.ClampWhenFinished = true;
        action.Play();

        // One large step that overshoots all three passes.
        mixer.Update(7.5f);

        action.Paused.ShouldBeTrue();
        target.Position.X.ShouldBe(10f, 1e-4f);
        mixer.Update(1f);
        target.Position.X.ShouldBe(10f, 1e-4f);
    }

    [Fact]
    public void Negative_Time_Scale_Plays_Backward_And_Finishes_At_The_Start()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).SetLoop(AnimationLoop.Once);
        action.ClampWhenFinished = true;
        action.Time = 1f;
        action.TimeScale = -1f;
        action.Play();
        var direction = 0;
        mixer.Finished += e => direction = e.Direction;

        mixer.Update(0.25f);
        target.Position.X.ShouldBe(7.5f, 1e-4f);
        action.EffectiveTimeScale.ShouldBe(-1f);
        mixer.Update(2f);

        direction.ShouldBe(-1);
        action.Time.ShouldBe(0f);
        target.Position.X.ShouldBe(0f);
    }

    [Fact]
    public void Negative_Time_Scale_Repeat_Wraps_To_The_End()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f));
        action.TimeScale = -1f;
        action.Play();
        var delta = 0;
        mixer.Looped += e => delta = e.LoopDelta;

        mixer.Update(0.25f);

        delta.ShouldBe(-1);
        action.Time.ShouldBe(0.75f, 1e-5f);
        target.Position.X.ShouldBe(7.5f, 1e-4f);
    }

    [Fact]
    public void Negative_Mixer_Delta_Plays_Backward()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).Play();
        mixer.Update(0.75f);

        mixer.Update(-0.5f);

        action.Time.ShouldBe(0.25f, 1e-5f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);
        mixer.Time.ShouldBe(0.25d, 1e-6d);
    }

    [Fact]
    public void Paused_Action_Keeps_Its_Pose()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).Play();
        mixer.Update(0.25f);

        action.Paused = true;
        mixer.Update(0.5f).ShouldBeFalse();

        action.Time.ShouldBe(0.25f, 1e-6f);
        action.IsRunning.ShouldBeFalse();
        action.EffectiveTimeScale.ShouldBe(0f);
        target.Position.X.ShouldBe(2.5f, 1e-5f);

        action.Paused = false;
        mixer.Update(0.25f).ShouldBeTrue();
        target.Position.X.ShouldBe(5f, 1e-4f);
    }

    [Fact]
    public void Disabled_Action_Contributes_Nothing_And_Keeps_Its_Time()
    {
        var target = new Object3D { Position = new Vector3(-3f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).Play();
        mixer.Update(0.25f);

        action.Enabled = false;
        mixer.Update(0.5f);

        target.Position.X.ShouldBe(-3f);
        action.Time.ShouldBe(0.25f, 1e-6f);
        action.EffectiveWeight.ShouldBe(0f);

        action.Enabled = true;
        mixer.Update(0.25f);
        target.Position.X.ShouldBe(5f, 1e-4f);
    }

    [Fact]
    public void Mixer_And_Action_Time_Scales_Multiply()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer { TimeScale = 2f };
        var action = mixer.ClipAction(MoveX(target, 0f, 10f, 4f));
        action.TimeScale = 0.5f;
        action.Play();

        mixer.Update(1f);

        mixer.Time.ShouldBe(2d, 1e-6d);
        action.Time.ShouldBe(1f, 1e-5f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);
    }

    [Fact]
    public void StartAt_Delays_The_Start_And_Keeps_The_Overshoot()
    {
        var target = new Object3D { Position = new Vector3(-1f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f)).StartAt(1d).Play();

        mixer.Update(0.75f);
        action.IsRunning.ShouldBeFalse();
        action.IsScheduled.ShouldBeTrue();
        action.Time.ShouldBe(0f);

        // Waiting shows the first frame, as in three.js.
        target.Position.X.ShouldBe(0f);

        mixer.Update(0.5f);
        action.IsRunning.ShouldBeTrue();
        action.Time.ShouldBe(0.25f, 1e-5f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);
    }

    [Fact]
    public void FadeIn_Blends_From_The_Rest_Value()
    {
        var target = new Object3D { Position = new Vector3(100f, 0f, 0f) };
        var mixer = new AnimationMixer();

        // A constant pose, so only the weight changes the result.
        var action = mixer.ClipAction(MoveX(target, 0f, 0f, 10f)).FadeIn(2f).Play();

        mixer.Update(0f);
        action.EffectiveWeight.ShouldBe(0f);
        target.Position.X.ShouldBe(100f);
        mixer.Update(0.5f);
        action.EffectiveWeight.ShouldBe(0.25f, 1e-5f);
        target.Position.X.ShouldBe(75f, 1e-3f);
        mixer.Update(1f);
        action.EffectiveWeight.ShouldBe(0.75f, 1e-5f);
        target.Position.X.ShouldBe(25f, 1e-3f);
        mixer.Update(1f);
        action.EffectiveWeight.ShouldBe(1f);
        target.Position.X.ShouldBe(0f, 1e-5f);
        action.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void FadeIn_Scales_The_Action_Weight()
    {
        var target = new Object3D { Position = new Vector3(100f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 0f, 10f));
        action.Weight = 0.5f;
        action.FadeIn(1f).Play();

        mixer.Update(0.5f);
        action.EffectiveWeight.ShouldBe(0.25f, 1e-5f);
        mixer.Update(1f);

        action.EffectiveWeight.ShouldBe(0.5f);
        target.Position.X.ShouldBe(50f, 1e-3f);
    }

    [Fact]
    public void FadeOut_Returns_To_The_Rest_Value_And_Disables_The_Action()
    {
        var target = new Object3D { Position = new Vector3(100f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 0f, 10f)).Play();
        mixer.Update(0.1f);
        target.Position.X.ShouldBe(0f);

        action.FadeOut(1f);
        mixer.Update(0.5f);
        target.Position.X.ShouldBe(50f, 1e-3f);
        action.Enabled.ShouldBeTrue();
        mixer.Update(0.75f);

        action.EffectiveWeight.ShouldBe(0f);
        action.Enabled.ShouldBeFalse();
        target.Position.X.ShouldBe(100f);
    }

    [Fact]
    public void StopFading_Keeps_The_Full_Weight()
    {
        var target = new Object3D { Position = new Vector3(100f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 0f, 10f)).FadeIn(10f).Play();
        mixer.Update(1f);

        action.StopFading();
        mixer.Update(0.1f);

        action.EffectiveWeight.ShouldBe(1f);
        target.Position.X.ShouldBe(0f, 1e-5f);
    }

    [Fact]
    public void Two_Actions_On_One_Property_Are_Averaged_By_Weight()
    {
        var target = new Object3D { Position = new Vector3(1000f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var a = mixer.ClipAction(MoveX(target, 0f, 0f, 1f, "a"));
        var b = mixer.ClipAction(MoveX(target, 8f, 8f, 1f, "b"));
        a.Weight = 1f;
        b.Weight = 3f;
        a.Play();
        b.Play();

        mixer.Update(0.1f);

        // Weights above 1 are normalized: (0 * 1 + 8 * 3) / 4.
        target.Position.X.ShouldBe(6f, 1e-4f);
    }

    [Fact]
    public void Weights_Below_One_Leave_The_Rest_To_The_Rest_Value()
    {
        var target = new Object3D { Position = new Vector3(100f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var a = mixer.ClipAction(MoveX(target, 0f, 0f, 1f, "a"));
        var b = mixer.ClipAction(MoveX(target, 40f, 40f, 1f, "b"));
        a.Weight = 0.25f;
        b.Weight = 0.25f;
        a.Play();
        b.Play();

        mixer.Update(0.1f);

        // Half of the average of the two (20) and half of the rest value (100).
        target.Position.X.ShouldBe(60f, 1e-3f);
    }

    [Fact]
    public void Rotations_Are_Blended_Spherically_And_Stay_Normalized()
    {
        var target = new Object3D();
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        var mixer = new AnimationMixer();
        var a = mixer.ClipAction(new AnimationClip("a", [new KeyframeTrack(target, TrackProperty.Rotation, [0f], [0f, 0f, 0f, 1f])]));
        var b = mixer.ClipAction(new AnimationClip("b", [new KeyframeTrack(target, TrackProperty.Rotation, [0f], [quarter.X, quarter.Y, quarter.Z, quarter.W])]));
        a.Play();
        b.Play();

        mixer.Update(0.1f);

        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4f);
        MathF.Abs(Quaternion.Dot(target.Rotation, expected)).ShouldBe(1f, 1e-5f);
        target.Rotation.Length().ShouldBe(1f, 1e-5f);

        // A single faded rotation blends with the rest rotation.
        b.Stop();
        a.Stop();
        target.Rotation.ShouldBe(Quaternion.Identity);
        b.Weight = 0.5f;
        b.Play();
        mixer.Update(0.1f);
        MathF.Abs(Quaternion.Dot(target.Rotation, expected)).ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void CrossFade_Moves_The_Weight_From_One_Action_To_The_Other()
    {
        var target = new Object3D { Position = new Vector3(1000f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var walk = mixer.ClipAction(MoveX(target, 0f, 0f, 1f, "walk")).Play();
        var run = mixer.ClipAction(MoveX(target, 10f, 10f, 1f, "run"));
        mixer.Update(0.1f);
        target.Position.X.ShouldBe(0f);

        run.Play();
        walk.CrossFadeTo(run, 1f);
        mixer.Update(0.25f);
        walk.EffectiveWeight.ShouldBe(0.75f, 1e-5f);
        run.EffectiveWeight.ShouldBe(0.25f, 1e-5f);
        target.Position.X.ShouldBe(2.5f, 1e-3f);
        mixer.Update(0.25f);
        target.Position.X.ShouldBe(5f, 1e-3f);
        mixer.Update(1f);

        walk.Enabled.ShouldBeFalse();
        run.EffectiveWeight.ShouldBe(1f);
        target.Position.X.ShouldBe(10f, 1e-4f);
    }

    [Fact]
    public void CrossFade_With_Warp_Keeps_Clips_Of_Different_Length_In_Step()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var walk = mixer.ClipAction(MoveX(target, 0f, 1f, 2f, "walk")).Play();
        var run = mixer.ClipAction(MoveX(target, 0f, 1f, 1f, "run")).Play();

        run.CrossFadeFrom(walk, 1f, warp: true);
        mixer.Update(0f);

        // At the start both advance at the pace of the clip that fades out: one pass in 2 s.
        walk.EffectiveTimeScale.ShouldBe(1f, 1e-5f);
        run.EffectiveTimeScale.ShouldBe(0.5f, 1e-5f);
        mixer.Update(0.5f);
        walk.EffectiveTimeScale.ShouldBe(1.5f, 1e-5f);
        run.EffectiveTimeScale.ShouldBe(0.75f, 1e-5f);
        mixer.Update(1f);

        // At the end both advance at the pace of the clip that fades in: one pass in 1 s.
        run.TimeScale.ShouldBe(1f, 1e-5f);
        run.EffectiveTimeScale.ShouldBe(1f, 1e-5f);
        walk.TimeScale.ShouldBe(2f, 1e-5f);
    }

    [Fact]
    public void Warp_Ramps_The_Time_Scale_And_Keeps_The_End_Value()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 100f, 100f)).Play();

        action.Warp(1f, 3f, 2f);
        mixer.Update(1f);
        action.EffectiveTimeScale.ShouldBe(2f, 1e-5f);
        action.Time.ShouldBe(2f, 1e-4f);
        mixer.Update(1.5f);

        action.TimeScale.ShouldBe(3f, 1e-5f);
        mixer.Update(1f);
        action.EffectiveTimeScale.ShouldBe(3f, 1e-5f);
    }

    [Fact]
    public void Halt_Slows_Down_And_Pauses()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 100f, 100f)).Play();
        mixer.Update(1f);

        action.Halt(1f);
        mixer.Update(0.5f);
        action.EffectiveTimeScale.ShouldBe(0.5f, 1e-5f);
        action.Paused.ShouldBeFalse();
        mixer.Update(1f);

        action.Paused.ShouldBeTrue();
        var time = action.Time;
        mixer.Update(1f);
        action.Time.ShouldBe(time);
    }

    [Fact]
    public void StopWarping_Keeps_The_Time_Scale()
    {
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(new Object3D(), 0f, 100f, 100f)).Play();
        action.Warp(1f, 5f, 10f);
        mixer.Update(1f);

        action.StopWarping();
        mixer.Update(1f);

        action.EffectiveTimeScale.ShouldBe(1f);
    }

    [Fact]
    public void SetDuration_SyncWith_And_Effective_Setters()
    {
        var mixer = new AnimationMixer();
        var a = mixer.ClipAction(MoveX(new Object3D(), 0f, 1f, 4f, "a"));
        var b = mixer.ClipAction(MoveX(new Object3D(), 0f, 1f, 4f, "b"));

        a.SetDuration(2f);
        a.TimeScale.ShouldBe(2f);
        a.Time = 1.5f;
        b.SyncWith(a);
        b.Time.ShouldBe(1.5f);
        b.TimeScale.ShouldBe(2f);

        a.FadeIn(10f).SetEffectiveWeight(0.3f);
        a.Weight.ShouldBe(0.3f);
        a.EffectiveWeight.ShouldBe(0.3f);
        a.Warp(1f, 9f, 10f).SetEffectiveTimeScale(0.25f);
        a.TimeScale.ShouldBe(0.25f);
        a.EffectiveTimeScale.ShouldBe(0.25f);
        a.Play();
        mixer.Update(1f);
        a.EffectiveWeight.ShouldBe(0.3f);
        a.EffectiveTimeScale.ShouldBe(0.25f);

        a.Paused = true;
        a.SetEffectiveTimeScale(1f).EffectiveTimeScale.ShouldBe(0f);
        a.Enabled = false;
        a.SetEffectiveWeight(1f).EffectiveWeight.ShouldBe(0f);
    }

    [Fact]
    public void Stop_Restores_The_Rest_Value_And_Resets_The_Action()
    {
        var target = new Object3D { Position = new Vector3(5f, 6f, 7f), Scale = new Vector3(2f) };
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1f);
        target.Rotation = rotation;
        var clip = new AnimationClip("all",
        [
            new KeyframeTrack(target, TrackProperty.Position, [0f, 1f], [0f, 0f, 0f, 1f, 1f, 1f]),
            new KeyframeTrack(target, TrackProperty.Rotation, [0f], [0f, 0f, 0f, 1f]),
            new KeyframeTrack(target, TrackProperty.Scale, [0f], [9f, 9f, 9f]),
        ]);
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(clip).Play();
        mixer.Update(0.5f);
        target.Position.ShouldBe(new Vector3(0.5f));
        target.Rotation.ShouldBe(Quaternion.Identity);
        target.Scale.ShouldBe(new Vector3(9f));

        action.Stop();

        target.Position.ShouldBe(new Vector3(5f, 6f, 7f));
        target.Rotation.ShouldBe(rotation);
        target.Scale.ShouldBe(new Vector3(2f));
        action.Time.ShouldBe(0f);
        action.IsScheduled.ShouldBeFalse();
        mixer.ScheduledActionCount.ShouldBe(0);
        mixer.Update(1f).ShouldBeFalse();
        target.Position.ShouldBe(new Vector3(5f, 6f, 7f));
    }

    [Fact]
    public void The_Rest_Value_Is_Read_When_The_Property_Is_First_Animated()
    {
        var target = new Object3D { Position = new Vector3(1f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 50f, 50f));

        // Moved after the action was created, before it plays.
        target.Position = new Vector3(2f, 0f, 0f);
        action.Play();
        mixer.Update(0.1f);
        action.Stop();
        target.Position.X.ShouldBe(2f);

        // Moved between two plays.
        target.Position = new Vector3(3f, 0f, 0f);
        action.Play();
        mixer.Update(0.1f);
        target.Position.X.ShouldBe(50f);
        action.Stop();
        target.Position.X.ShouldBe(3f);
    }

    [Fact]
    public void Property_Stays_Animated_While_Another_Action_Uses_It()
    {
        var target = new Object3D { Position = new Vector3(100f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var a = mixer.ClipAction(MoveX(target, 0f, 0f, 1f, "a")).Play();
        var b = mixer.ClipAction(MoveX(target, 8f, 8f, 1f, "b")).Play();
        mixer.Update(0.1f);
        target.Position.X.ShouldBe(4f, 1e-4f);

        a.Stop();
        mixer.Update(0.1f);
        target.Position.X.ShouldBe(8f, 1e-4f);
        b.Stop();

        target.Position.X.ShouldBe(100f);
    }

    [Fact]
    public void StopAllActions_Stops_Everything()
    {
        var one = new Object3D { Position = new Vector3(1f, 0f, 0f) };
        var two = new Object3D { Position = new Vector3(2f, 0f, 0f) };
        var mixer = new AnimationMixer();
        mixer.ClipAction(MoveX(one, 10f, 20f, 1f, "one")).Play();
        mixer.ClipAction(MoveX(two, 10f, 20f, 1f, "two")).Play();
        mixer.ClipAction(MoveX(two, 30f, 40f, 1f, "three")).Play();
        mixer.Update(0.5f);

        mixer.StopAllActions();

        mixer.ScheduledActionCount.ShouldBe(0);
        one.Position.X.ShouldBe(1f);
        two.Position.X.ShouldBe(2f);
    }

    [Fact]
    public void Morph_Weights_Are_Written_To_The_Mesh_And_Restored()
    {
        var mesh = new Mesh(new BufferGeometry(), new BasicMaterial());
        mesh.MorphWeights.ShouldBeNull();
        var clip = new AnimationClip("smile", [new KeyframeTrack(mesh, TrackProperty.MorphWeights, [0f, 1f], [0f, 0f, 1f, 0.5f])]);
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(clip).Play();

        mixer.Update(0.5f);

        mesh.MorphWeights.ShouldNotBeNull();
        mesh.MorphWeights!.Length.ShouldBe(2);
        mesh.MorphWeights[0].ShouldBe(0.5f, 1e-5f);
        mesh.MorphWeights[1].ShouldBe(0.25f, 1e-5f);
        var array = mesh.MorphWeights;
        mixer.Update(0.25f);
        mesh.MorphWeights.ShouldBeSameAs(array);
        mesh.MorphWeights[0].ShouldBe(0.75f, 1e-5f);

        action.Stop();
        mesh.MorphWeights[0].ShouldBe(0f);
        mesh.MorphWeights[1].ShouldBe(0f);
    }

    [Fact]
    public void Morph_Weights_Rest_Value_Is_The_Existing_Array()
    {
        var mesh = new Mesh(new BufferGeometry(), new BasicMaterial()) { MorphWeights = [0.2f, 0.4f, 0.9f] };
        var clip = new AnimationClip("smile", [new KeyframeTrack(mesh, TrackProperty.MorphWeights, [0f], [1f, 1f])]);
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(clip);
        action.Weight = 0.5f;
        action.Play();

        mixer.Update(0.1f);

        mesh.MorphWeights[0].ShouldBe(0.6f, 1e-5f);
        mesh.MorphWeights[1].ShouldBe(0.7f, 1e-5f);

        // The weight the track does not cover is left alone.
        mesh.MorphWeights[2].ShouldBe(0.9f);
        action.Stop();
        mesh.MorphWeights[0].ShouldBe(0.2f);
        mesh.MorphWeights[1].ShouldBe(0.4f);
    }

    [Fact]
    public void Morph_Tracks_With_Different_Counts_On_One_Mesh_Are_Rejected()
    {
        var mesh = new Mesh(new BufferGeometry(), new BasicMaterial());
        var mixer = new AnimationMixer();
        mixer.ClipAction(new AnimationClip("two", [new KeyframeTrack(mesh, TrackProperty.MorphWeights, [0f], [0f, 0f])]));
        var three = new AnimationClip("three",
        [
            new KeyframeTrack(new Object3D(), TrackProperty.Position, [0f], [0f, 0f, 0f]),
            new KeyframeTrack(mesh, TrackProperty.MorphWeights, [0f], [0f, 0f, 0f]),
        ]);

        Should.Throw<ArgumentException>(() => mixer.ClipAction(three));

        mixer.ExistingAction(three).ShouldBeNull();
    }

    [Fact]
    public void Morph_Track_On_A_Node_That_Is_Not_A_Mesh_Is_Ignored()
    {
        var group = new Group();
        var mixer = new AnimationMixer();
        mixer.ClipAction(new AnimationClip("weights", [new KeyframeTrack(group, TrackProperty.MorphWeights, [0f], [1f])])).Play();

        Should.NotThrow(() => mixer.Update(0.1f));
    }

    [Fact]
    public void Custom_Track_Calls_Its_Setter_With_The_Blended_Value()
    {
        var material = new StandardMaterial { Roughness = 0.9f };
        var calls = 0;
        void SetRoughness(ReadOnlySpan<float> v)
        {
            calls++;
            material.Roughness = v[0];
        }

        var mixer = new AnimationMixer();
        var a = mixer.ClipAction(new AnimationClip("a", [new KeyframeTrack(SetRoughness, 1, [0f, 1f], [0f, 1f])])).Play();

        mixer.Update(0.25f).ShouldBeTrue();
        material.Roughness.ShouldBe(0.25f, 1e-5f);
        calls.ShouldBe(1);

        // An unchanged value is not written again.
        a.Paused = true;
        mixer.Update(0.25f).ShouldBeFalse();
        calls.ShouldBe(1);
        a.Paused = false;

        // A second clip on the same setter (the same method on the same instance) blends with the first.
        var b = mixer.ClipAction(new AnimationClip("b", [new KeyframeTrack(SetRoughness, 1, [0f], [0.75f])])).Play();
        mixer.Update(0f);
        material.Roughness.ShouldBe(0.5f, 1e-5f);

        // No rest value for setters: a partial weight is not diluted, and stopping leaves the last value.
        b.Stop();
        a.Weight = 0.1f;
        mixer.Update(0.25f);
        material.Roughness.ShouldBe(0.5f, 1e-5f);
        a.Stop();
        material.Roughness.ShouldBe(0.5f, 1e-5f);
    }

    [Fact]
    public void Custom_Track_Writes_Again_After_A_Restart()
    {
        var value = -1f;
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(new AnimationClip("c", [new KeyframeTrack(v => value = v[0], 1, [0f], [3f])])).Play();
        mixer.Update(0.1f);
        action.Stop();
        value = -1f;

        action.Play();
        mixer.Update(0.1f);

        value.ShouldBe(3f);
    }

    [Fact]
    public void SetTime_Jumps_To_An_Absolute_Time()
    {
        var target = new Object3D();
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(MoveX(target, 0f, 10f, 4f)).Play();
        mixer.Update(3.3f);

        mixer.SetTime(1f).ShouldBeTrue();

        mixer.Time.ShouldBe(1d, 1e-6d);
        action.Time.ShouldBe(1f, 1e-5f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);
    }

    [Fact]
    public void Uncache_Stops_And_Forgets_The_Action()
    {
        var target = new Object3D { Position = new Vector3(5f, 0f, 0f) };
        var clip = MoveX(target, 0f, 10f);
        var mixer = new AnimationMixer();
        var action = mixer.ClipAction(clip).Play();
        mixer.Update(0.5f);

        mixer.Uncache(clip);

        target.Position.X.ShouldBe(5f);
        action.IsScheduled.ShouldBeFalse();
        mixer.ScheduledActionCount.ShouldBe(0);
        mixer.ExistingAction(clip).ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => action.Play());
        var again = mixer.ClipAction(clip);
        again.ShouldNotBeSameAs(action);
        again.Play();
        mixer.Update(0.25f);
        target.Position.X.ShouldBe(2.5f, 1e-4f);

        // Uncaching a clip the mixer does not know is a no-op.
        Should.NotThrow(() => mixer.Uncache(MoveX(target, 0f, 1f)));
    }

    [Fact]
    public void Uncache_Keeps_Properties_Other_Clips_Still_Use()
    {
        var target = new Object3D { Position = new Vector3(100f, 0f, 0f) };
        var first = MoveX(target, 0f, 0f, 1f, "first");
        var second = MoveX(target, 8f, 8f, 1f, "second");
        var mixer = new AnimationMixer();
        mixer.ClipAction(first).Play();
        mixer.ClipAction(second).Play();
        mixer.Update(0.1f);

        mixer.Uncache(first);
        mixer.Update(0.1f);
        target.Position.X.ShouldBe(8f, 1e-4f);
        mixer.Uncache(second);

        target.Position.X.ShouldBe(100f);
    }

    [Fact]
    public void Events_Are_Raised_After_The_Pose_Is_Applied_And_Handlers_May_Change_Actions()
    {
        var target = new Object3D { Position = new Vector3(-1f, 0f, 0f) };
        var mixer = new AnimationMixer();
        var intro = mixer.ClipAction(MoveX(target, 0f, 10f, 1f, "intro")).SetLoop(AnimationLoop.Once);
        intro.ClampWhenFinished = true;
        var idle = mixer.ClipAction(MoveX(target, 20f, 30f, 1f, "idle"));
        var seen = float.NaN;
        mixer.Finished += e =>
        {
            seen = target.Position.X;
            e.Action.Stop();
            idle.Play();
        };
        intro.Play();

        mixer.Update(1.5f);

        seen.ShouldBe(10f);
        intro.IsScheduled.ShouldBeFalse();
        idle.IsScheduled.ShouldBeTrue();
        mixer.Update(0.5f);
        target.Position.X.ShouldBe(25f, 1e-4f);
    }

    [Fact]
    public void A_Clip_Of_Zero_Length_Shows_Its_Pose()
    {
        var target = new Object3D();
        var clip = new AnimationClip("pose", [new KeyframeTrack(target, TrackProperty.Position, [0f], [4f, 5f, 6f])]);
        var mixer = new AnimationMixer();
        var looped = 0;
        mixer.Looped += _ => looped++;
        var action = mixer.ClipAction(clip).Play();

        for (var i = 0; i < 5; i++)
        {
            mixer.Update(0.1f);
        }

        target.Position.ShouldBe(new Vector3(4f, 5f, 6f));
        action.Time.ShouldBe(0f);
        looped.ShouldBe(0);
        float.IsFinite(action.Time).ShouldBeTrue();
    }

    [Fact]
    public void Update_Does_Not_Allocate_After_Warm_Up()
    {
        var mesh = new Mesh(new BufferGeometry(), new BasicMaterial());
        var bones = new Object3D[24];
        var tracks = new List<KeyframeTrack>();
        var other = new List<KeyframeTrack>();
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1f);
        for (var i = 0; i < bones.Length; i++)
        {
            bones[i] = new Object3D();
            if (i > 0)
            {
                bones[i - 1].Add(bones[i]);
            }

            tracks.Add(new KeyframeTrack(bones[i], TrackProperty.Position, [0f, 0.5f, 1f], [0f, 0f, 0f, 1f, 2f, 3f, 0f, 0f, 0f]));
            tracks.Add(new KeyframeTrack(bones[i], TrackProperty.Rotation, [0f, 1f], [0f, 0f, 0f, 1f, q.X, q.Y, q.Z, q.W]));
            tracks.Add(new KeyframeTrack(bones[i], TrackProperty.Scale, [0f, 1f], [1f, 1f, 1f, 2f, 2f, 2f], Interpolation.Step));
            other.Add(new KeyframeTrack(bones[i], TrackProperty.Rotation, [0f, 0.7f], [q.X, q.Y, q.Z, q.W, 0f, 0f, 0f, 1f]));
        }

        var sink = 0f;
        tracks.Add(new KeyframeTrack(mesh, TrackProperty.MorphWeights, [0f, 1f], [0f, 0f, 1f, 1f]));
        tracks.Add(new KeyframeTrack(v => sink = v[0], 1, [0f, 1f], [0f, 1f]));
        var mixer = new AnimationMixer();
        var loops = 0;
        var finishes = 0;
        mixer.Looped += _ => loops++;
        mixer.Finished += _ => finishes++;
        var a = mixer.ClipAction(new AnimationClip("a", [.. tracks])).Play();
        var b = mixer.ClipAction(new AnimationClip("b", [.. other])).SetLoop(AnimationLoop.PingPong).Play();
        b.Weight = 0.5f;
        var c = mixer.ClipAction(new AnimationClip("c", [.. other])).SetLoop(AnimationLoop.Once);

        // Warm up: the lists reach their sizes and the morph weight array is created.
        for (var i = 0; i < 200; i++)
        {
            mixer.Update(0.013f);
        }

        c.Play();
        mixer.Update(5f);
        c.Stop();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2000; i++)
        {
            mixer.Update(0.013f);
            if (i == 500)
            {
                a.CrossFadeTo(b, 1f, warp: true);
            }

            if (i == 1000)
            {
                c.Reset().Play();
            }

            if (i == 1500)
            {
                a.Stop().FadeIn(0.5f).Play();
                b.Halt(0.2f);
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0L);
        loops.ShouldBeGreaterThan(10);
        finishes.ShouldBeGreaterThan(0);
        sink.ShouldBeGreaterThanOrEqualTo(0f);
    }

    [Fact]
    public void Stopped_Actions_Cost_Nothing()
    {
        var mixer = new AnimationMixer();
        var calls = new int[50];
        for (var i = 0; i < calls.Length; i++)
        {
            // A setter per clip: tracks with the same setter would share one property.
            var index = i;
            var action = mixer.ClipAction(new AnimationClip("c" + i, [new KeyframeTrack(_ => calls[index]++, 1, [0f, 1f], [0f, 1f])]));
            action.Play();
        }

        mixer.Update(0.1f);
        calls.Sum().ShouldBe(50);
        mixer.StopAllActions();
        Array.Clear(calls);

        mixer.Update(0.1f).ShouldBeFalse();

        calls.Sum().ShouldBe(0);
        mixer.ScheduledActionCount.ShouldBe(0);
    }
}
