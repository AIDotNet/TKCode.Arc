namespace TKCode.Arc;

/// <summary>
/// Paints a <see cref="PixelMascotPose"/> into a 24 × 24 grid of palette indices. The art is authored here as pixel
/// coordinates and small row strings; <see cref="PixelMascot"/> draws the grid as whole-device-pixel rectangles.
/// <code>
///  antenna (11–12, 2–4)      bubble (15–23, 0–4) + tail (20, 6)
///  head    (5–18, 5–14) with the screen (7–16, 7–12) and ears (4 / 19, 9–10)
///  neck    (10–13, 15)
///  body    (7–16, 16–21) with the chest panel (8–15, 18)
///  feet    (9–10 / 13–14, 22), shadow (7–16, 23), keyboard (4–19, 19–22 when in place)
/// </code>
/// </summary>
internal static class PixelMascotSprite
{
    public const int Size = 24;

    // Palette indices (0 is empty).
    public const byte Empty = 0;
    public const byte Outline = 1;
    public const byte Shell = 2;
    public const byte Shade = 3;
    public const byte Screen = 4;
    public const byte Eye = 5;
    public const byte Accent = 6;
    public const byte Shadow = 7;
    public const byte Danger = 8;
    public const byte EyeDim = 9;
    public const byte ScreenTintHalf = 10;
    public const byte ScreenTintFull = 11;
    public const int PaletteSize = 12;

    // Left arm poses as (x, y) pixels, mirrored for the right arm: down, forward on the keyboard, out to the side, up.
    private static readonly (int X, int Y)[][] s_arms =
    [
        [(6, 17), (5, 18), (5, 19), (4, 20), (5, 20)],
        [(6, 17), (5, 18), (5, 19), (6, 20), (7, 20)],
        [(6, 17), (5, 17), (4, 17), (3, 16), (3, 15)],
        [(6, 17), (5, 16), (4, 15), (3, 14), (3, 13), (2, 12), (3, 12)]
    ];

    // Eye shapes as rows ('#' lit), drawn from their top-left corner; the last row is what stays when the lids are half shut.
    private static readonly string[] s_open = ["##", "##"];
    private static readonly string[] s_happy = [".#.", "#.#"];
    private static readonly string[] s_crossed = ["#.#", ".#.", "#.#"];

    // The full thought bubble (15–23, 0–4) and the keyboard (4–19, four rows), as rows of palette characters.
    private static readonly string[] s_bubble =
    [
        ".ooooooo.",
        "ossssssso",
        "ossssssso",
        "ossssssso",
        ".ooooooo."
    ];

    private static readonly string[] s_keyboard =
    [
        ".oooooooooooooo.",
        "oshshshshshshsho",
        "ohshshshshshshso",
        ".oooooooooooooo."
    ];

    /// <summary>Paints <paramref name="pose"/> into <paramref name="grid"/> (<see cref="Size"/>² indices, row by row).</summary>
    public static void Compose(in PixelMascotPose pose, byte[] grid)
    {
        Array.Clear(grid);
        var ox = pose.X;
        var oy = pose.Y;

        // The shadow stays on the floor and narrows while the robot is in the air.
        var lift = Math.Min(2, Math.Max(0, -oy) / 2);
        Fill(grid, 7 + lift, 23, 10 - 2 * lift, 1, Shadow);

        // Feet and neck.
        Fill(grid, 9 + ox, 22 + oy, 2, 1, Outline);
        Fill(grid, 13 + ox, 22 + oy, 2, 1, Outline);
        Fill(grid, 10 + ox, 15 + oy, 4, 1, Outline);

        // Body: outline ring with cut corners, shell, a shaded bottom row and the chest panel.
        Box(grid, 7 + ox, 16 + oy, 10, 6);
        Fill(grid, 8 + ox, 20 + oy, 8, 1, Shade);
        if (pose.Typed > 0 || pose.Keyboard > 0)
        {
            Fill(grid, 8 + ox, 18 + oy, PixelMascotPose.BarLength, 1, Shade);
            Fill(grid, 8 + ox, 18 + oy, pose.Typed, 1, Accent);
        }
        else
        {
            Fill(grid, 11 + ox, 18 + oy, 2, 1, Accent);
        }

        // Antenna: a stem and a light that turns to the danger color with the screen tint.
        Fill(grid, 11 + ox, 4 + oy, 2, 1, Outline);
        Fill(grid, 11 + ox, 2 + oy, 2, 2, pose.Antenna ? pose.Tint > 0 ? Danger : Accent : Shade);

        // Head: outline ring, shell, a shaded bottom row, ears and the screen with rounded corners.
        Box(grid, 5 + ox, 5 + oy, 14, 10);
        Fill(grid, 6 + ox, 13 + oy, 12, 1, Shade);
        Fill(grid, 4 + ox, 9 + oy, 1, 2, Outline);
        Fill(grid, 19 + ox, 9 + oy, 1, 2, Outline);
        var screen = pose.Tint switch
        {
            1 => ScreenTintHalf,
            >= 2 => ScreenTintFull,
            _ => Screen
        };
        Fill(grid, 7 + ox, 7 + oy, 10, 6, screen);
        Set(grid, 7 + ox, 7 + oy, Shell);
        Set(grid, 16 + ox, 7 + oy, Shell);
        Set(grid, 7 + ox, 12 + oy, Shell);
        Set(grid, 16 + ox, 12 + oy, Shell);

        DrawEyes(grid, pose, ox, oy);

        // Thought bubble: a dot, a small bubble, then the full bubble with three dots.
        if (pose.Bubble >= 1)
        {
            Set(grid, 20 + ox, 6 + oy, Outline);
        }
        if (pose.Bubble == 2)
        {
            Ring(grid, 19 + ox, 3 + oy, 3, 3);
            Set(grid, 20 + ox, 4 + oy, Shell);
        }
        else if (pose.Bubble >= 3)
        {
            Rows(grid, s_bubble, 15 + ox, 0 + oy);
            for (var i = 0; i < 3; i++)
            {
                Set(grid, 17 + 2 * i + ox, 2 + oy, i < pose.Dots ? Screen : Shade);
            }
        }

        // Keyboard: slides up from below the sprite, in front of the body; it does not bob or shake.
        if (pose.Keyboard > 0)
        {
            Rows(grid, s_keyboard, 4, 19 + (PixelMascotPose.KeyboardIn - pose.Keyboard));
        }

        DrawArm(grid, pose.ArmLeft, pose.TapLeft, ox, oy, mirror: false);
        DrawArm(grid, pose.ArmRight, pose.TapRight, ox, oy, mirror: true);

        // Done sparkles in the corners above the head (they stay put while the robot jumps).
        if (pose.Sparkle > 0)
        {
            Sparkle(grid, 3, 3, pose.Sparkle);
            Sparkle(grid, 20, 2, pose.Sparkle);
        }
    }

    private static void DrawEyes(byte[] grid, in PixelMascotPose pose, int ox, int oy)
    {
        var (shape, left, right, top, color) = pose.Eyes switch
        {
            PixelMascotEyes.Happy => (s_happy, 8, 13, 9, Eye),
            PixelMascotEyes.Crossed => (s_crossed, 8, 13, 8, Danger),
            _ => (s_open, 9, 13, 9, Eye)
        };
        var x = pose.EyeX + ox;
        var y = pose.EyeY + oy;
        Eye1(grid, shape, left + x, top + y, pose.Lid, color);
        Eye1(grid, shape, right + x, top + y, pose.Lid, color);
    }

    private static void Eye1(byte[] grid, string[] shape, int x, int y, int lid, byte color)
    {
        var first = lid > 0 ? shape.Length - 1 : 0;
        var ink = lid >= 2 ? EyeDim : color;
        for (var r = first; r < shape.Length; r++)
        {
            var row = shape[r];
            for (var c = 0; c < row.Length; c++)
            {
                if (row[c] == '#')
                {
                    Set(grid, x + c, y + r, ink);
                }
            }
        }
    }

    private static void DrawArm(byte[] grid, int pose, bool tap, int ox, int oy, bool mirror)
    {
        var pixels = s_arms[Math.Clamp(pose, 0, s_arms.Length - 1)];
        for (var i = 0; i < pixels.Length; i++)
        {
            var (x, y) = pixels[i];
            // The hand (the last pixels of the forward pose) presses one pixel lower on a stroke.
            if (tap && i >= pixels.Length - 2)
            {
                y++;
            }
            Set(grid, (mirror ? Size - 1 - x : x) + ox, y + oy, Outline);
        }
    }

    private static void Sparkle(byte[] grid, int x, int y, int stage)
    {
        Set(grid, x, y, Accent);
        if (stage >= 2)
        {
            Set(grid, x - 1, y, Accent);
            Set(grid, x + 1, y, Accent);
            Set(grid, x, y - 1, Accent);
            Set(grid, x, y + 1, Accent);
        }
    }

    // An outlined box with cut corners and a shell inside.
    private static void Box(byte[] grid, int x, int y, int w, int h)
    {
        Fill(grid, x + 1, y + 1, w - 2, h - 2, Shell);
        Ring(grid, x, y, w, h);
    }

    // An outline with cut corners.
    private static void Ring(byte[] grid, int x, int y, int w, int h)
    {
        Fill(grid, x + 1, y, w - 2, 1, Outline);
        Fill(grid, x + 1, y + h - 1, w - 2, 1, Outline);
        Fill(grid, x, y + 1, 1, h - 2, Outline);
        Fill(grid, x + w - 1, y + 1, 1, h - 2, Outline);
    }

    private static void Rows(byte[] grid, string[] rows, int x, int y)
    {
        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                var index = row[c] switch
                {
                    'o' => Outline,
                    's' => Shell,
                    'h' => Shade,
                    _ => Empty
                };
                if (index != Empty)
                {
                    Set(grid, x + c, y + r, index);
                }
            }
        }
    }

    private static void Fill(byte[] grid, int x, int y, int w, int h, byte index)
    {
        for (var j = 0; j < h; j++)
        {
            for (var i = 0; i < w; i++)
            {
                Set(grid, x + i, y + j, index);
            }
        }
    }

    private static void Set(byte[] grid, int x, int y, byte index)
    {
        if ((uint)x < Size && (uint)y < Size)
        {
            grid[y * Size + x] = index;
        }
    }
}
