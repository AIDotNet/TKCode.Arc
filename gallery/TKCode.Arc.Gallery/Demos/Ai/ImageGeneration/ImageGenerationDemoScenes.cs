namespace TKCode.Arc.Gallery.Demos;

/// <summary>The kinds of picture the demos paint.</summary>
internal enum ImageGenerationDemoScene
{
    /// <summary>A lake under mountains at golden hour, with mist and the sun's reflection.</summary>
    Landscape,

    /// <summary>A city street at night after rain: lit windows and their reflections.</summary>
    City,

    /// <summary>A ceramic cup on a sunlit sill.</summary>
    StillLife,

    /// <summary>A pastel room: an arched window, soft shapes and a plant.</summary>
    Pastel
}

/// <summary>
/// Paints the demos' pictures in code (layered gradients and simple shapes), so the card never downloads
/// anything. Each picture is a function of its scene, a seed and its size; the result is opaque BGRA, row by row.
/// </summary>
internal static class ImageGenerationDemoScenes
{
    /// <summary>Picks the scene whose words appear in the prompt, or one from the prompt's hash.</summary>
    public static ImageGenerationDemoScene Pick(string? prompt)
    {
        var raw = (prompt ?? string.Empty).ToLowerInvariant();
        // Whole words only ("grain" is not "rain"): letters kept, everything else a space, padded at both ends.
        var letters = new char[raw.Length + 2];
        letters[0] = letters[^1] = ' ';
        for (var i = 0; i < raw.Length; i++)
        {
            letters[i + 1] = char.IsLetter(raw[i]) ? raw[i] : ' ';
        }
        var text = new string(letters);
        if (Has(text, "city", "street", "neon", "night", "rain", "urban", "tokyo", "skyline"))
        {
            return ImageGenerationDemoScene.City;
        }
        if (Has(text, "cup", "coffee", "mug", "ceramic", "still life", "windowsill", "table", "product"))
        {
            return ImageGenerationDemoScene.StillLife;
        }
        if (Has(text, "room", "nook", "interior", "pastel", "plant", "cozy", "cosy", "isometric"))
        {
            return ImageGenerationDemoScene.Pastel;
        }
        if (Has(text, "lake", "mountain", "landscape", "sunset", "golden hour", "sunrise", "valley", "forest", "sea"))
        {
            return ImageGenerationDemoScene.Landscape;
        }
        return (ImageGenerationDemoScene)(int)(Hash(raw) % 4);
    }

    /// <summary>A stable hash of a string (FNV-1a), so the same prompt always paints the same pictures.</summary>
    public static uint Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 16777619u;
        }
        return hash;
    }

    /// <summary>Paints a <paramref name="width"/> × <paramref name="height"/> picture as opaque BGRA bytes.</summary>
    public static byte[] Paint(ImageGenerationDemoScene scene, uint seed, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var pixels = new byte[width * height * 4];
        var painter = new Painter(seed);
        for (var y = 0; y < height; y++)
        {
            var v = (y + 0.5) / height;
            for (var x = 0; x < width; x++)
            {
                var u = (x + 0.5) / width;
                var aspect = (double)width / height;
                var c = scene switch
                {
                    ImageGenerationDemoScene.City => painter.City(u, v, aspect),
                    ImageGenerationDemoScene.StillLife => painter.StillLife(u, v, aspect),
                    ImageGenerationDemoScene.Pastel => painter.Pastel(u, v, aspect),
                    _ => painter.Landscape(u, v, aspect),
                };
                // A little grain, like a photo.
                var grain = (Noise.Hash(x, y, seed) - 0.5) * 0.035;
                var i = (y * width + x) * 4;
                pixels[i] = ToByte(c.B + grain);
                pixels[i + 1] = ToByte(c.G + grain);
                pixels[i + 2] = ToByte(c.R + grain);
                pixels[i + 3] = 255;
            }
        }
        return pixels;
    }

    private static bool Has(string text, params string[] words)
    {
        foreach (var word in words)
        {
            if (text.Contains(" " + word + " ", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

    /// <summary>A color with channels from 0 to 1.</summary>
    private readonly record struct Rgb(double R, double G, double B)
    {
        public static Rgb Hex(int hex) => new(((hex >> 16) & 0xFF) / 255.0, ((hex >> 8) & 0xFF) / 255.0, (hex & 0xFF) / 255.0);

        public static Rgb Mix(Rgb a, Rgb b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        public Rgb Add(Rgb other, double amount) => new(R + other.R * amount, G + other.G * amount, B + other.B * amount);

        public Rgb Scale(double amount) => new(R * amount, G * amount, B * amount);
    }

    private static class Noise
    {
        public static double Hash(int x, int y, uint seed)
        {
            unchecked
            {
                var h = (uint)x * 374761393u + (uint)y * 668265263u + seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        /// <summary>Smooth value noise in [0, 1).</summary>
        public static double Value(double x, double y, uint seed)
        {
            var ix = (int)Math.Floor(x);
            var iy = (int)Math.Floor(y);
            var fx = x - ix;
            var fy = y - iy;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            var a = Hash(ix, iy, seed);
            var b = Hash(ix + 1, iy, seed);
            var c = Hash(ix, iy + 1, seed);
            var d = Hash(ix + 1, iy + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
    }

    private static double Smooth(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>The seeded choices of one picture.</summary>
    private sealed class Painter
    {
        private readonly uint _seed;
        private readonly double _a;
        private readonly double _b;
        private readonly double _c;
        private readonly double _d;

        public Painter(uint seed)
        {
            _seed = seed;
            _a = Noise.Hash(1, 7, seed);
            _b = Noise.Hash(3, 11, seed);
            _c = Noise.Hash(5, 13, seed);
            _d = Noise.Hash(9, 17, seed);
        }

        private double Ridge(double x, double baseline, double amplitude, double frequency, uint salt) =>
            baseline - amplitude * (0.55 * Math.Sin(x * frequency + _a * 6.28 + salt)
                                    + 0.3 * Math.Sin(x * frequency * 2.3 + _b * 6.28 + salt * 1.7)
                                    + 0.35 * (Noise.Value(x * frequency * 1.6, salt, _seed + salt) - 0.5));

        public Rgb Landscape(double u, double v, double aspect)
        {
            var horizon = 0.58 + (_c - 0.5) * 0.06;
            var sunX = 0.3 + _a * 0.4;
            var sunY = horizon - 0.12 - _b * 0.12;
            var warm = 0.7 + _d * 0.3;
            var top = Rgb.Mix(Rgb.Hex(0x3E4F86), Rgb.Hex(0x5A6FA8), _b);
            var mid = Rgb.Mix(Rgb.Hex(0xE88A6A), Rgb.Hex(0xF2A066), warm);
            var low = Rgb.Hex(0xFFE0A8);
            var sv = v < horizon ? v : horizon - (v - horizon) * 0.9;
            var t = Math.Clamp(sv / horizon, 0, 1);
            var sky = t < 0.6 ? Rgb.Mix(top, mid, t / 0.6) : Rgb.Mix(mid, low, (t - 0.6) / 0.4);
            var dx = (u - sunX) * aspect;
            var dy = sv - sunY;
            var d2 = dx * dx + dy * dy;
            sky = sky.Add(Rgb.Hex(0xFFD49A), 0.55 * Math.Exp(-d2 / 0.02)).Add(Rgb.Hex(0xFFF4DC), Math.Exp(-d2 / 0.0012));
            if (v < horizon)
            {
                var far = Ridge(u, horizon - 0.1, 0.08, 7, 1);
                var near = Ridge(u, horizon - 0.02, 0.1, 11, 2);
                var color = sky;
                if (v > far)
                {
                    color = Rgb.Mix(Rgb.Hex(0x8C7AA8), sky, 0.35 + 0.4 * (1 - (v - far) / 0.2));
                }
                if (v > near)
                {
                    // Pines on the near ridge: a jagged tree line, darker toward the shore.
                    color = Rgb.Mix(Rgb.Hex(0x3A3F55), Rgb.Hex(0x22283A), Smooth(near, horizon, v));
                }
                // Mist over the water's edge.
                var mist = Math.Exp(-Math.Pow((v - horizon + 0.015) / 0.035, 2)) * (0.55 + 0.3 * Noise.Value(u * 6, 3, _seed));
                return Rgb.Mix(color, Rgb.Hex(0xF6E2C8), mist);
            }
            // The lake mirrors the sky, darker and bluer, with ripples and a streak under the sun.
            var ripple = 0.5 + 0.5 * Math.Sin((v - horizon) * 260 + Noise.Value(u * 8, v * 30, _seed) * 4);
            var water = Rgb.Mix(sky.Scale(0.72), Rgb.Hex(0x2C3A5E), Smooth(horizon, 1, v) * 0.75);
            var streak = Math.Exp(-Math.Pow((u - sunX) * aspect / (0.02 + (v - horizon) * 0.25), 2)) * ripple;
            water = water.Add(Rgb.Hex(0xFFD9A0), 0.45 * streak);
            var edge = Math.Exp(-Math.Pow((v - horizon) / 0.02, 2)) * 0.5;
            return Rgb.Mix(water, Rgb.Hex(0xF6E2C8), edge);
        }

        public Rgb City(double u, double v, double aspect)
        {
            var street = 0.68 + (_c - 0.5) * 0.06;
            var sv = v < street ? v : street - (v - street) * 1.4;
            var glow = Rgb.Mix(Rgb.Hex(0xC2559A), Rgb.Hex(0x4E7FD8), _a);
            var sky = Rgb.Mix(Rgb.Hex(0x0E1230), Rgb.Hex(0x2A1E4A), Smooth(0, street, sv));
            sky = sky.Add(glow, 0.35 * Math.Exp(-Math.Pow((sv - street) / 0.18, 2)));
            // Buildings: columns of seeded width and height, with windows on a grid.
            var columns = 7 + (int)(_b * 4);
            var col = (int)Math.Floor(u * columns);
            var local = u * columns - col;
            var height = 0.18 + Noise.Hash(col, 3, _seed) * 0.42;
            var color = sky;
            var roof = street - height;
            if (sv > roof && sv < street)
            {
                var shade = 0.12 + Noise.Hash(col, 5, _seed) * 0.08;
                color = new Rgb(shade * 0.8, shade * 0.85, shade * 1.25);
                var wx = (int)Math.Floor(local * 5);
                var wy = (int)Math.Floor((sv - roof) * 60);
                var inX = local * 5 - wx;
                var inY = (sv - roof) * 60 - wy;
                if (local > 0.08 && local < 0.92 && inX > 0.25 && inX < 0.75 && inY > 0.3 && inY < 0.75
                    && Noise.Hash(col * 31 + wx, wy, _seed) > 0.55)
                {
                    var warm = Noise.Hash(col, wy, _seed + 1) > 0.3;
                    color = warm ? Rgb.Hex(0xFFD27A) : Rgb.Hex(0x8FE3FF);
                }
            }
            if (v < street)
            {
                return color;
            }
            // Wet street: the scene above, broken by ripples, with a neon streak.
            var wave = (Noise.Value(u * 40, v * 6, _seed) - 0.5) * 0.25;
            var reflected = color.Scale(0.55 + wave);
            var neon = Math.Exp(-Math.Pow((u - 0.25 - _d * 0.5) * aspect / 0.03, 2)) * Smooth(street, 1, v);
            return reflected.Add(glow, 0.5 * neon).Add(Rgb.Hex(0x101426), 0.4);
        }

        public Rgb StillLife(double u, double v, double aspect)
        {
            var sill = 0.64;
            var wall = Rgb.Mix(Rgb.Hex(0xEBDDC8), Rgb.Hex(0xC9B6A0), Smooth(0, sill, v));
            // A slanted patch of window light on the wall.
            var slant = u + v * 0.45 - 0.25 - _a * 0.2;
            var light = Smooth(0.0, 0.03, slant) * (1 - Smooth(0.36, 0.4, slant)) * (1 - Smooth(sill - 0.05, sill, v));
            var color = wall.Add(Rgb.Hex(0xFFE9C2), 0.28 * light);
            if (v >= sill)
            {
                var grain = Noise.Value(u * 3, v * 60, _seed) * 0.08;
                color = Rgb.Mix(Rgb.Hex(0xB98A5E), Rgb.Hex(0x8C6340), Smooth(sill, 1, v) + grain);
            }
            var cupX = 0.4 + _b * 0.2;
            var cupW = 0.11;
            var cupTop = 0.42;
            var cupBottom = sill + 0.04;
            var glaze = _c switch
            {
                < 0.25 => Rgb.Hex(0xC8664A),
                < 0.5 => Rgb.Hex(0x8FA88A),
                < 0.75 => Rgb.Hex(0xEDE6D8),
                _ => Rgb.Hex(0x5C7FA6),
            };
            var dx = (u - cupX) * aspect;
            // Its shadow falls away from the light.
            var sdx = dx - (v - cupBottom) * 0.9;
            if (v > cupBottom - 0.02 && v < cupBottom + 0.06 && Math.Abs(sdx - 0.06) < cupW * 1.1)
            {
                color = color.Scale(0.78 + 0.22 * Smooth(0, cupW * 1.1, Math.Abs(sdx - 0.06)));
            }
            var rim = (v - cupTop) / 0.022;
            var inBody = Math.Abs(dx) < cupW * (1 - 0.12 * Smooth(cupTop, cupBottom, v)) && v > cupTop && v < cupBottom;
            var handle = Math.Abs(Math.Sqrt(Math.Pow(dx - cupW - 0.02, 2) + Math.Pow((v - (cupTop + cupBottom) / 2) * 1.2, 2)) - 0.045) < 0.012
                         && dx > cupW * 0.9;
            if (inBody || handle)
            {
                var side = Math.Clamp(0.5 - dx / (cupW * 2), 0, 1);
                color = Rgb.Mix(glaze.Scale(0.62), glaze.Add(Rgb.Hex(0xFFFFFF), 0.18), side * (0.4 + 0.6 * light + 0.3));
            }
            if (Math.Abs(dx) < cupW && rim > -1 && rim < 1)
            {
                color = Rgb.Mix(glaze.Scale(0.45), Rgb.Hex(0x3B2418), Smooth(-1, 0.5, -Math.Abs(rim) + 0.5));
            }
            return color;
        }

        public Rgb Pastel(double u, double v, double aspect)
        {
            var floor = 0.72;
            var tint = Rgb.Mix(Rgb.Hex(0xF3D9E2), Rgb.Hex(0xDCE6F5), _a);
            var color = Rgb.Mix(tint, Rgb.Hex(0xFFF3E6), Smooth(0, floor, v) * 0.6);
            // An arched window letting in a soft light.
            var wx = (u - (0.3 + _b * 0.4)) * aspect;
            var archTop = 0.22;
            var inArch = Math.Abs(wx) < 0.14 && v > archTop + 0.14 && v < floor - 0.08
                         || (v <= archTop + 0.14 && v > archTop && wx * wx + Math.Pow(v - archTop - 0.14, 2) < 0.14 * 0.14);
            if (inArch)
            {
                color = Rgb.Mix(Rgb.Hex(0xFFFDF6), Rgb.Hex(0xBFDDF2), Smooth(archTop, floor, v));
            }
            if (v >= floor)
            {
                color = Rgb.Mix(Rgb.Hex(0xE8C9A8), Rgb.Hex(0xD3AF8E), Smooth(floor, 1, v));
            }
            // Soft round shapes: a chair and a lamp's glow.
            var cx = (u - 0.2 - _c * 0.2) * aspect;
            var chair = Math.Exp(-(cx * cx + Math.Pow((v - floor + 0.05) * 2.2, 2)) / 0.012);
            color = Rgb.Mix(color, Rgb.Mix(Rgb.Hex(0xE79A8A), Rgb.Hex(0x9BC4B0), _d), Smooth(0.45, 0.6, chair));
            var lx = (u - 0.78 + _d * 0.1) * aspect;
            color = color.Add(Rgb.Hex(0xFFE4B0), 0.35 * Math.Exp(-(lx * lx + Math.Pow(v - 0.35, 2)) / 0.01));
            // A plant: a pot and a few leaves.
            var px = (u - 0.85 + _b * 0.08) * aspect;
            if (Math.Abs(px) < 0.05 && v > floor - 0.1 && v < floor + 0.02)
            {
                color = Rgb.Hex(0xC97B5A);
            }
            var leaf = Noise.Value(px * 18, v * 18, _seed + 9);
            if (Math.Abs(px) < 0.11 * (1 - Smooth(floor - 0.32, floor - 0.1, v) * 0.3) && v > floor - 0.32 && v < floor - 0.1 && leaf > 0.38)
            {
                color = Rgb.Mix(Rgb.Hex(0x4F8A5B), Rgb.Hex(0x7DB37A), leaf);
            }
            return color;
        }
    }
}
