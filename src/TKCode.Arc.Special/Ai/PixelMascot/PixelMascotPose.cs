namespace TKCode.Arc;

/// <summary>The eye shapes of <see cref="PixelMascot"/>.</summary>
internal enum PixelMascotEyes
{
    /// <summary>Two 2 × 2 eyes.</summary>
    Open,

    /// <summary>Happy carets (done).</summary>
    Happy,

    /// <summary>Crossed 3 × 3 eyes in the danger color (error).</summary>
    Crossed
}

/// <summary>
/// One frame of the sprite as whole-pixel part states. Every part moves one step per 40ms tick toward the target of the
/// current status (the vertical offset up to two), so a status change never cuts and a change mid-animation reverses
/// from wherever the parts are.
/// </summary>
internal struct PixelMascotPose
{
    /// <summary>Eye shape.</summary>
    public PixelMascotEyes Eyes;

    /// <summary>Lids: 0 open, 1 half (bottom row only), 2 closed (a dim line).</summary>
    public int Lid;

    /// <summary>Where the eyes look, −1 to 1 pixel each way.</summary>
    public int EyeX;

    public int EyeY;

    /// <summary>Arm poses: 0 down, 1 forward on the keyboard, 2 out to the side, 3 up.</summary>
    public int ArmLeft;

    public int ArmRight;

    /// <summary>A hand pressing a key (one pixel lower), while typing.</summary>
    public bool TapLeft;

    public bool TapRight;

    /// <summary>The thought bubble: 0 hidden, 1 a dot, 2 a small bubble, 3 the full bubble.</summary>
    public int Bubble;

    /// <summary>How many of the bubble's three dots are lit.</summary>
    public int Dots;

    /// <summary>The keyboard: 0 hidden below the sprite, 5 in place.</summary>
    public int Keyboard;

    /// <summary>Strokes of the chest bar typed so far, 0 to 8.</summary>
    public int Typed;

    /// <summary>The screen's danger tint: 0 none, 1 half, 2 full.</summary>
    public int Tint;

    /// <summary>The whole robot's offset in pixels (shake, bob, jump); the shadow and the keyboard stay put.</summary>
    public int X;

    public int Y;

    /// <summary>The antenna light is on.</summary>
    public bool Antenna;

    /// <summary>The done sparkles: 0 none, 1 a dot, 2 a plus.</summary>
    public int Sparkle;

    public const int KeyboardIn = 5;
    public const int BarLength = 8;

    /// <summary>The idle resting pose.</summary>
    public static PixelMascotPose Rest => new() { Antenna = true };
}

/// <summary>
/// The status animations of <see cref="PixelMascot"/>: what each part aims for at a tick of a status, the clear resting
/// pose of each status (reduced motion, paused), and the one-step-per-tick approach that morphs between them.
/// </summary>
internal static class PixelMascotMotion
{
    /// <summary>The fixed clock: 40ms per step.</summary>
    public const double TickSeconds = 0.04;

    /// <summary>Idle bobs on a 2s cycle (50 ticks); blinks and a glance repeat every 8s (200 ticks).</summary>
    public const int BobTicks = 50;

    public const int IdleCycleTicks = 200;

    /// <summary>Thinking cycles its three dots every 1.12s (28 ticks, four phases of 280ms).</summary>
    public const int DotsCycleTicks = 28;

    /// <summary>Writing types an eight-stroke bar every 1.6s (40 ticks: a stroke every 4 ticks, then a 160ms hold).</summary>
    public const int TypeCycleTicks = 40;

    public const int StrokeTicks = 4;

    /// <summary>
    /// The done jump, one value per tick from the status change: rises two pixels in one step, holds at the top for
    /// three ticks (120ms) and settles a pixel at a time on an ease-out (one tick, then two) without a rebound.
    /// </summary>
    public static ReadOnlySpan<int> JumpCurve => [-2, -2, -2, -1, -1, 0];

    /// <summary>How long the arms stay up after a done jump (ticks).</summary>
    public const int CheerTicks = 16;

    /// <summary>The error shake, one value per tick: right, left, right, then still.</summary>
    public static ReadOnlySpan<int> ShakeCurve => [1, 1, -1, -1, -1, 1, 1, 0];

    /// <summary>The target of every part at <paramref name="tick"/> ticks into <paramref name="status"/>.</summary>
    public static PixelMascotPose Target(PixelMascotStatus status, int tick)
    {
        var pose = new PixelMascotPose { Antenna = true };
        switch (status)
        {
            case PixelMascotStatus.Idle:
            {
                var cycle = tick % IdleCycleTicks;
                pose.Y = tick % BobTicks < BobTicks / 2 ? 0 : -1;
                // Blinks at 2.4s and 6.8s: lids half, closed for 80ms, half, open.
                pose.Lid = cycle is >= 60 and < 63 or >= 170 and < 173 ? 2 : 0;
                // A one-second glance to the side at 4s.
                pose.EyeX = cycle is >= 100 and < 125 ? -1 : 0;
                break;
            }
            case PixelMascotStatus.Thinking:
            {
                var phase = tick % DotsCycleTicks / (DotsCycleTicks / 4);
                pose.Bubble = 3;
                pose.Dots = phase < 3 ? phase + 1 : 0;
                pose.EyeX = 1;
                pose.EyeY = -1;
                pose.ArmRight = 2;
                pose.Lid = tick % 75 is >= 50 and < 53 ? 2 : 0;
                pose.Antenna = phase % 2 == 0;
                break;
            }
            case PixelMascotStatus.Writing:
            {
                var cycle = tick % TypeCycleTicks;
                var typed = Math.Min(PixelMascotPose.BarLength, cycle / StrokeTicks);
                pose.Keyboard = PixelMascotPose.KeyboardIn;
                pose.Typed = typed;
                pose.EyeY = 1;
                pose.EyeX = typed <= 2 ? -1 : typed <= 5 ? 0 : 1;
                pose.ArmLeft = 1;
                pose.ArmRight = 1;
                var striking = cycle < PixelMascotPose.BarLength * StrokeTicks && cycle % StrokeTicks < 2;
                var stroke = cycle / StrokeTicks;
                pose.TapLeft = striking && stroke % 2 == 0;
                pose.TapRight = striking && stroke % 2 == 1;
                break;
            }
            case PixelMascotStatus.Done:
            {
                var jump = JumpCurve;
                pose.Eyes = PixelMascotEyes.Happy;
                pose.Y = tick < jump.Length ? jump[tick] : 0;
                pose.ArmLeft = tick < CheerTicks ? 3 : 0;
                pose.ArmRight = pose.ArmLeft;
                pose.Sparkle = tick >= 2 && (tick - 2) % 16 < 5 ? 2 : 0;
                break;
            }
            case PixelMascotStatus.Error:
            {
                var shake = ShakeCurve;
                pose.Eyes = PixelMascotEyes.Crossed;
                pose.Tint = 2;
                pose.X = tick < shake.Length ? shake[tick] : 0;
                pose.Antenna = tick / 13 % 2 == 0;
                break;
            }
        }
        return pose;
    }

    /// <summary>The clear resting pose of a status: open eyes, bubble with three dots, keyboard, happy eyes or crossed eyes.</summary>
    public static PixelMascotPose Rest(PixelMascotStatus status) => status switch
    {
        PixelMascotStatus.Thinking => new PixelMascotPose
        {
            Antenna = true, Bubble = 3, Dots = 3, EyeX = 1, EyeY = -1, ArmRight = 2
        },
        PixelMascotStatus.Writing => new PixelMascotPose
        {
            Antenna = true, Keyboard = PixelMascotPose.KeyboardIn, Typed = 5, EyeY = 1, ArmLeft = 1, ArmRight = 1
        },
        PixelMascotStatus.Done => new PixelMascotPose { Antenna = true, Eyes = PixelMascotEyes.Happy, Sparkle = 2 },
        PixelMascotStatus.Error => new PixelMascotPose { Antenna = true, Eyes = PixelMascotEyes.Crossed, Tint = 2 },
        _ => PixelMascotPose.Rest
    };

    /// <summary>
    /// Moves <paramref name="pose"/> one tick toward <paramref name="target"/>: every part one step (the vertical offset
    /// up to two). Eye shapes change behind closed lids: the lids close, the shape swaps, then they open again. The
    /// bubble's dots light only once it is whole; the chest bar resets at once while writing and backspaces otherwise.
    /// </summary>
    public static void Step(ref PixelMascotPose pose, in PixelMascotPose target, bool writing)
    {
        if (pose.Eyes != target.Eyes)
        {
            if (pose.Lid >= 2)
            {
                pose.Eyes = target.Eyes;
            }
            else
            {
                pose.Lid++;
            }
        }
        else
        {
            pose.Lid = Toward(pose.Lid, target.Lid, 1);
        }
        pose.EyeX = Toward(pose.EyeX, target.EyeX, 1);
        pose.EyeY = Toward(pose.EyeY, target.EyeY, 1);
        pose.ArmLeft = Toward(pose.ArmLeft, target.ArmLeft, 1);
        pose.ArmRight = Toward(pose.ArmRight, target.ArmRight, 1);
        pose.TapLeft = target.TapLeft && pose.ArmLeft == 1;
        pose.TapRight = target.TapRight && pose.ArmRight == 1;
        pose.Bubble = Toward(pose.Bubble, target.Bubble, 1);
        pose.Dots = pose.Bubble == 3 && target.Bubble == 3 ? target.Dots : 0;
        pose.Keyboard = Toward(pose.Keyboard, target.Keyboard, 1);
        pose.Typed = writing && pose.Keyboard == PixelMascotPose.KeyboardIn ? target.Typed : Toward(pose.Typed, target.Typed, 1);
        pose.Tint = Toward(pose.Tint, target.Tint, 1);
        pose.X = Toward(pose.X, target.X, 1);
        pose.Y = Toward(pose.Y, target.Y, 2);
        pose.Antenna = target.Antenna;
        pose.Sparkle = Toward(pose.Sparkle, target.Sparkle, 1);
    }

    private static int Toward(int value, int target, int step) =>
        value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
}
