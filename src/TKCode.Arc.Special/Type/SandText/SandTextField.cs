namespace TKCode.Arc;

/// <summary>
/// The sand of a <see cref="SandText"/>: a grid with one cell per grain, stepped at a fixed 60 Hz. Cells are empty,
/// <em>stuck</em> (part of a letter) or <em>loose</em> (falling sand). Three kinds of motion live on it:
/// <list type="bullet">
/// <item>the <b>pour</b>: one falling grain per missing letter cell, released bottom row first over a fixed window,
/// falling under gravity straight through whatever lies above its target and stopping exactly on it;</item>
/// <item><b>thrown</b> grains: knocked out of the letters by the pointer, flying with its velocity under gravity until
/// they hit sand or the floor, where they become loose;</item>
/// <item>the <b>automaton</b> for loose sand: two bottom-up passes a frame, each moving a grain one cell down, or
/// diagonally down when blocked (45° piles). A loose grain that comes to rest in an empty letter cell sticks; one that
/// rests outside the letters disappears after a random 0.3–1.9 s.</item>
/// </list>
/// About 0.68 s after the last grain was dug out the sand is at rest: missing letter cells are poured again (healing)
/// and loose sand outside the letters starts to disappear.
/// All buffers are allocated when the grid is built or grows; a step allocates nothing.
/// </summary>
internal sealed class SandTextField
{
    public const byte Empty = 0;
    public const byte Stuck = 1;
    public const byte Loose = 2;

    /// <summary>Shade variants added to each RGB channel of a palette color.</summary>
    public const int Variants = 5;

    /// <summary>Palette colors a grain can pick from (a shade is one byte: palette index × 5 + variant).</summary>
    public const int MaxPalette = 51;

    // Pour kinematics (Arc Sand text, design reference, in cells and 60 Hz frames).
    internal const float PourSpeed = 2.6f;
    internal const float PourSpeedJitter = 0.5f;
    internal const float Gravity = 0.225f;
    internal const float PourMaxSpeed = 5f;
    internal const float PourStartAbove = 3f;
    internal const float RowJitter = 2.5f;

    // Pour windows: a new word pours within 45 frames, the first show within 54; a small heal goes quicker.
    public const int ChangeWindow = 45;
    public const int IntroWindow = 54;
    internal const int HealFullCount = 600;

    // Thrown grains.
    internal const float PointerShare = 0.2f;
    internal const float OutwardSpeed = 1f;
    internal const float OutwardJitter = 1f;
    internal const float Lift = 0.4f;
    internal const float MaxThrowSpeed = 12f;
    internal const float Drag = 0.985f;

    // Loose sand outside the letters lives this many frames once it rests (0.3–1.9 s).
    internal const int MinLife = 18;
    internal const int MaxLife = 114;

    // The sand is at rest (heals, loose sand disappears) this many frames after the last grain was dug out (≈0.68 s).
    internal const int RestFrames = 41;

    private byte[] _state = [];
    private byte[] _shade = [];
    private short[] _life = [];
    private bool[] _mask = [];

    // Pour queue: targets sorted bottom first, each with its release frame.
    private int[] _queue = [];
    private float[] _queueKey = [];
    private float[] _queueTime = [];
    private int _queueHead;
    private int _queueCount;

    // Pour grains in flight.
    private int[] _pourTarget = [];
    private float[] _pourY = [];
    private float[] _pourV = [];
    private byte[] _pourShade = [];
    private int _pourCount;

    // Thrown grains in flight.
    private float[] _flyX = [];
    private float[] _flyY = [];
    private float[] _flyVX = [];
    private float[] _flyVY = [];
    private byte[] _flyShade = [];
    private int _flyCount;

    private uint _seed = 0x9E3779B9u;
    private int _paletteCount = 1;
    private long _frame;
    private int _sinceDig = RestFrames;
    private bool _moved;
    private bool _gatherPending;
    private bool _dissolvePending;

    /// <summary>Grid width in cells.</summary>
    public int Columns { get; private set; }

    /// <summary>Grid height in cells.</summary>
    public int Rows { get; private set; }

    /// <summary>Letter cells of the current text.</summary>
    public int MaskCount { get; private set; }

    /// <summary>Stuck grains (always inside the letters).</summary>
    public int StuckCount { get; private set; }

    /// <summary>Loose grains on the grid.</summary>
    public int LooseCount { get; private set; }

    /// <summary>Pour grains in flight plus those still waiting to be released.</summary>
    public int PouringCount => _pourCount + (_queueCount - _queueHead);

    /// <summary>Thrown grains in flight.</summary>
    public int FlyingCount => _flyCount;

    /// <summary>Whether the letters are let go: nothing sticks and nothing is poured (<see cref="SandText.IsDissolved"/>).</summary>
    public bool IsDissolved { get; private set; }

    /// <summary>Whether anything still moves, waits to be poured, or waits to disappear.</summary>
    public bool IsActive => _pourCount > 0 || _queueHead < _queueCount || _flyCount > 0 || LooseCount > 0 || _moved
                            || NeedsHeal;

    private bool NeedsHeal => !IsDissolved && StuckCount < MaskCount;

    /// <summary>Raised from <see cref="Step"/> when the letters became whole at the end of a pour.</summary>
    public bool TakeGathered()
    {
        if (!_gatherPending || IsDissolved || StuckCount < MaskCount || PouringCount > 0)
        {
            return false;
        }
        _gatherPending = false;
        return true;
    }

    /// <summary>True once after <see cref="SetDissolved"/> when the last grain has gone.</summary>
    public bool TakeDissolved()
    {
        if (!_dissolvePending || !IsDissolved || StuckCount > 0 || LooseCount > 0 || _flyCount > 0 || PouringCount > 0)
        {
            return false;
        }
        _dissolvePending = false;
        return true;
    }

    /// <summary>The cell's state (<see cref="Empty"/>, <see cref="Stuck"/> or <see cref="Loose"/>).</summary>
    public byte State(int column, int row) => _state[row * Columns + column];

    /// <summary>The shade byte of the grain in a cell (palette index × 5 + variant).</summary>
    public byte Shade(int column, int row) => _shade[row * Columns + column];

    /// <summary>Whether a cell belongs to the letters.</summary>
    public bool IsLetter(int column, int row) => _mask[row * Columns + column];

    /// <summary>How many palette colors new grains pick from (1–51).</summary>
    public int PaletteCount
    {
        get => _paletteCount;
        set => _paletteCount = Math.Clamp(value, 1, MaxPalette);
    }

    /// <summary>
    /// Builds an empty grid of the given size with no letters, or keeps the buffers when they are large enough.
    /// </summary>
    public void Resize(int columns, int rows)
    {
        columns = Math.Max(0, columns);
        rows = Math.Max(0, rows);
        var cells = columns * rows;
        if (_state.Length < cells)
        {
            _state = new byte[cells];
            _shade = new byte[cells];
            _life = new short[cells];
            _mask = new bool[cells];
            _queue = new int[cells];
            _queueKey = new float[cells];
            _queueTime = new float[cells];
        }
        Columns = columns;
        Rows = rows;
        Clear();
        Array.Clear(_mask, 0, cells);
        MaskCount = 0;
    }

    /// <summary>
    /// Takes the letters of a new text (a mask of <see cref="Columns"/> × <see cref="Rows"/>) and shows them at once:
    /// every letter cell stuck (or nothing when dissolved), no loose sand, nothing in flight.
    /// </summary>
    public void SetFormed(ReadOnlySpan<bool> mask)
    {
        CopyMask(mask);
        Clear();
        if (IsDissolved)
        {
            return;
        }
        var cells = Columns * Rows;
        for (var i = 0; i < cells; i++)
        {
            if (_mask[i])
            {
                _state[i] = Stuck;
                _shade[i] = NextShade();
            }
        }
        StuckCount = MaskCount;
    }

    /// <summary>Takes the letters of a new text and pours them into an empty grid (the first show).</summary>
    public void SetPoured(ReadOnlySpan<bool> mask, int window)
    {
        CopyMask(mask);
        Clear();
        if (!IsDissolved)
        {
            SchedulePour(window);
        }
    }

    /// <summary>
    /// Changes the letters while the sand is live: grains already on a new letter cell stay, every other stuck grain
    /// turns loose and falls, pour grains in flight keep falling as loose sand, and the new letters pour in.
    /// </summary>
    public void Retarget(ReadOnlySpan<bool> mask, int window)
    {
        CopyMask(mask);
        var cells = Columns * Rows;
        StuckCount = 0;
        for (var i = 0; i < cells; i++)
        {
            if (_state[i] == Stuck)
            {
                if (_mask[i] && !IsDissolved)
                {
                    StuckCount++;
                }
                else
                {
                    _state[i] = Loose;
                    _life[i] = -1;
                    LooseCount++;
                }
            }
        }
        DropPour();
        if (!IsDissolved)
        {
            SchedulePour(window);
        }
    }

    /// <summary>Lets the letters go (true): every stuck grain turns loose and all of it disappears; or pours them back.</summary>
    public void SetDissolved(bool dissolved, bool animate)
    {
        if (IsDissolved == dissolved)
        {
            return;
        }
        IsDissolved = dissolved;
        _dissolvePending = dissolved;
        _gatherPending = false;
        if (!animate)
        {
            if (dissolved)
            {
                Clear();
            }
            else
            {
                SetFormed(_mask.AsSpan(0, Columns * Rows));
                _gatherPending = true;
            }
            return;
        }
        if (dissolved)
        {
            Retarget(_mask.AsSpan(0, Columns * Rows), 0);
        }
        else
        {
            SchedulePour(ChangeWindow);
        }
    }

    /// <summary>Ends all motion at once: the letters whole (or nothing when dissolved).</summary>
    public void Settle()
    {
        if (IsDissolved)
        {
            Clear();
            return;
        }
        var cells = Columns * Rows;
        _pourCount = 0;
        _queueHead = _queueCount = 0;
        _flyCount = 0;
        LooseCount = 0;
        StuckCount = 0;
        for (var i = 0; i < cells; i++)
        {
            if (_mask[i])
            {
                if (_state[i] != Stuck)
                {
                    _shade[i] = NextShade();
                }
                _state[i] = Stuck;
                StuckCount++;
            }
            else
            {
                _state[i] = Empty;
            }
        }
        _moved = false;
    }

    /// <summary>Whether the sand is at rest: no grain has been dug out for <see cref="RestFrames"/> frames.</summary>
    public bool IsResting => _sinceDig >= RestFrames;

    /// <summary>
    /// Knocks every grain within <paramref name="radius"/> cells of (<paramref name="x"/>, <paramref name="y"/>) loose and
    /// throws it with a share of the pointer's velocity (cells per frame) plus an outward push. Returns how many grains
    /// were thrown.
    /// </summary>
    public int Dig(double x, double y, double radius, double velocityX, double velocityY)
    {
        if (Columns == 0 || Rows == 0 || radius <= 0)
        {
            return 0;
        }
        var r2 = radius * radius;
        var x0 = Math.Max(0, (int)Math.Floor(x - radius));
        var x1 = Math.Min(Columns - 1, (int)Math.Ceiling(x + radius));
        var y0 = Math.Max(0, (int)Math.Floor(y - radius));
        var y1 = Math.Min(Rows - 1, (int)Math.Ceiling(y + radius));
        var thrown = 0;
        for (var row = y0; row <= y1; row++)
        {
            for (var column = x0; column <= x1; column++)
            {
                var dx = column + 0.5 - x;
                var dy = row + 0.5 - y;
                var d2 = dx * dx + dy * dy;
                if (d2 > r2)
                {
                    continue;
                }
                var i = row * Columns + column;
                var state = _state[i];
                if (state == Empty)
                {
                    continue;
                }
                if (state == Stuck)
                {
                    StuckCount--;
                }
                else
                {
                    LooseCount--;
                }
                _state[i] = Empty;
                var d = Math.Sqrt(d2);
                double ux, uy;
                if (d < 1e-3)
                {
                    var a = Next() * Math.PI * 2;
                    ux = Math.Cos(a);
                    uy = Math.Sin(a);
                }
                else
                {
                    ux = dx / d;
                    uy = dy / d;
                }
                var push = OutwardSpeed + OutwardJitter * Next();
                var vx = PointerShare * velocityX + ux * push + (Next() - 0.5) * 0.8;
                var vy = PointerShare * velocityY + uy * push - Lift + (Next() - 0.5) * 0.8;
                var speed = Math.Sqrt(vx * vx + vy * vy);
                if (speed > MaxThrowSpeed)
                {
                    vx *= MaxThrowSpeed / speed;
                    vy *= MaxThrowSpeed / speed;
                }
                AddFlying(column + 0.5f, row + 0.5f, (float)vx, (float)vy, _shade[i]);
                thrown++;
            }
        }
        if (thrown > 0)
        {
            _sinceDig = 0;
        }
        return thrown;
    }

    /// <summary>Advances one 60 Hz frame.</summary>
    public void Step()
    {
        _frame++;
        if (_sinceDig < RestFrames)
        {
            _sinceDig++;
        }
        ReleasePour();
        StepPour();
        StepFlying();
        _moved = false;
        if (LooseCount > 0)
        {
            StepAutomaton(false);
            StepAutomaton(true);
        }
        if (NeedsHeal && IsResting && _pourCount == 0 && _queueHead >= _queueCount)
        {
            var missing = MaskCount - StuckCount;
            SchedulePour((int)Math.Round(ChangeWindow * Math.Min(1.0, missing / (double)HealFullCount)));
        }
    }

    /// <summary>
    /// Writes the grid as premultiplied BGRA pixels, one per cell, using <paramref name="colors"/> (256 entries, indexed
    /// by shade byte).
    /// </summary>
    public void Paint(Span<uint> pixels, uint[] colors)
    {
        var cells = Columns * Rows;
        for (var i = 0; i < cells; i++)
        {
            pixels[i] = _state[i] == Empty ? 0u : colors[_shade[i]];
        }
        for (var p = 0; p < _pourCount; p++)
        {
            var row = (int)Math.Floor(_pourY[p]);
            if (row < 0 || row >= Rows)
            {
                continue;
            }
            var column = _pourTarget[p] % Columns;
            pixels[row * Columns + column] = colors[_pourShade[p]];
        }
        for (var f = 0; f < _flyCount; f++)
        {
            var column = (int)Math.Floor(_flyX[f]);
            var row = (int)Math.Floor(_flyY[f]);
            if (column < 0 || column >= Columns || row < 0 || row >= Rows)
            {
                continue;
            }
            pixels[row * Columns + column] = colors[_flyShade[f]];
        }
    }

    /// <summary>
    /// Builds the shade table: 256 premultiplied BGRA colors, entry <c>p × 5 + v</c> being palette color <c>p</c>
    /// (wrapped to the palette) with <c>(v − 2) × 18</c> added to each channel.
    /// </summary>
    public static void BuildColors(ReadOnlySpan<(byte R, byte G, byte B)> palette, uint[] colors)
    {
        var count = Math.Max(1, Math.Min(palette.Length, MaxPalette));
        for (var s = 0; s < 256; s++)
        {
            var (r, g, b) = palette.Length == 0 ? ((byte)128, (byte)128, (byte)128) : palette[(s / Variants) % count];
            var shift = (s % Variants - 2) * 18;
            colors[s] = 0xFF000000u
                        | ((uint)Math.Clamp(r + shift, 0, 255) << 16)
                        | ((uint)Math.Clamp(g + shift, 0, 255) << 8)
                        | (uint)Math.Clamp(b + shift, 0, 255);
        }
    }

    private void CopyMask(ReadOnlySpan<bool> mask)
    {
        var cells = Columns * Rows;
        MaskCount = 0;
        for (var i = 0; i < cells; i++)
        {
            var on = i < mask.Length && mask[i];
            _mask[i] = on;
            if (on)
            {
                MaskCount++;
            }
        }
    }

    private void Clear()
    {
        var cells = Columns * Rows;
        Array.Clear(_state, 0, cells);
        _pourCount = 0;
        _queueHead = _queueCount = 0;
        _flyCount = 0;
        StuckCount = 0;
        LooseCount = 0;
        _moved = false;
    }

    /// <summary>Pour grains still falling turn into thrown grains (they keep falling, then become loose); the queue empties.</summary>
    private void DropPour()
    {
        for (var p = 0; p < _pourCount; p++)
        {
            var column = _pourTarget[p] % Columns;
            AddFlying(column + 0.5f, _pourY[p], 0, _pourV[p], _pourShade[p]);
        }
        _pourCount = 0;
        _queueHead = _queueCount = 0;
    }

    /// <summary>Queues one grain for every empty letter cell, bottom rows first, spread over <paramref name="window"/> frames.</summary>
    private void SchedulePour(int window)
    {
        _queueHead = _queueCount = 0;
        var cells = Columns * Rows;
        for (var i = 0; i < cells; i++)
        {
            if (_mask[i] && _state[i] != Stuck)
            {
                var row = i / Columns;
                // Bottom first: a lower row (larger index) gets a smaller key; jitter lets rows overlap a little.
                _queue[_queueCount] = i;
                _queueKey[_queueCount] = (Rows - row) + (float)Next() * RowJitter;
                _queueCount++;
            }
        }
        if (_queueCount == 0)
        {
            return;
        }
        Array.Sort(_queueKey, _queue, 0, _queueCount);
        for (var k = 0; k < _queueCount; k++)
        {
            _queueTime[k] = _frame + (window <= 0 ? 0 : (float)k / _queueCount * window);
        }
        _gatherPending = true;
        EnsurePour(_queueCount);
    }

    private void ReleasePour()
    {
        while (_queueHead < _queueCount && _queueTime[_queueHead] <= _frame)
        {
            var target = _queue[_queueHead++];
            if (_state[target] == Stuck || !_mask[target])
            {
                continue;
            }
            var p = _pourCount++;
            _pourTarget[p] = target;
            _pourY[p] = -(float)Next() * PourStartAbove;
            _pourV[p] = PourSpeed + (float)Next() * PourSpeedJitter;
            _pourShade[p] = NextShade();
        }
    }

    private void StepPour()
    {
        for (var p = 0; p < _pourCount; p++)
        {
            var v = Math.Min(PourMaxSpeed, _pourV[p] + Gravity);
            _pourV[p] = v;
            var y = _pourY[p] + v;
            var target = _pourTarget[p];
            var targetRow = target / Columns;
            if (y < targetRow)
            {
                _pourY[p] = y;
                continue;
            }
            // Landed: the grain takes its cell (a loose grain already there sticks in its place).
            if (_state[target] == Empty && !IsDissolved)
            {
                _state[target] = Stuck;
                _shade[target] = _pourShade[p];
                StuckCount++;
            }
            else if (_state[target] == Loose && !IsDissolved)
            {
                _state[target] = Stuck;
                LooseCount--;
                StuckCount++;
            }
            _pourCount--;
            _pourTarget[p] = _pourTarget[_pourCount];
            _pourY[p] = _pourY[_pourCount];
            _pourV[p] = _pourV[_pourCount];
            _pourShade[p] = _pourShade[_pourCount];
            p--;
        }
    }

    private void StepFlying()
    {
        for (var f = 0; f < _flyCount; f++)
        {
            var vx = _flyVX[f] * Drag;
            var vy = _flyVY[f] + Gravity;
            _flyVX[f] = vx;
            _flyVY[f] = vy;
            var x = _flyX[f];
            var y = _flyY[f];
            var steps = (int)Math.Ceiling(Math.Max(Math.Abs(vx), Math.Abs(vy)));
            steps = Math.Max(1, steps);
            var sx = vx / steps;
            var sy = vy / steps;
            var gone = false;
            var landed = false;
            for (var s = 0; s < steps; s++)
            {
                var nx = x + sx;
                var ny = y + sy;
                if (nx < 0 || nx >= Columns)
                {
                    gone = true;
                    break;
                }
                if (ny >= Rows)
                {
                    landed = true;
                    break;
                }
                if (ny >= 0 && _state[(int)ny * Columns + (int)nx] != Empty)
                {
                    landed = true;
                    break;
                }
                x = nx;
                y = ny;
            }
            if (landed)
            {
                Settle(x, y, _flyShade[f]);
                gone = true;
            }
            if (gone)
            {
                _flyCount--;
                _flyX[f] = _flyX[_flyCount];
                _flyY[f] = _flyY[_flyCount];
                _flyVX[f] = _flyVX[_flyCount];
                _flyVY[f] = _flyVY[_flyCount];
                _flyShade[f] = _flyShade[_flyCount];
                f--;
                continue;
            }
            _flyX[f] = x;
            _flyY[f] = y;
        }
    }

    /// <summary>A thrown grain hit something: it becomes loose sand in its cell, or the first free cell above.</summary>
    private void Settle(float x, float y, byte shade)
    {
        var column = Math.Clamp((int)x, 0, Columns - 1);
        var row = Math.Min(Rows - 1, (int)Math.Floor(y));
        for (var k = 0; k < 4 && row >= 0; k++, row--)
        {
            var i = row * Columns + column;
            if (_state[i] == Empty)
            {
                _state[i] = Loose;
                _shade[i] = shade;
                _life[i] = -1;
                LooseCount++;
                _moved = true;
                return;
            }
        }
    }

    /// <summary>
    /// One bottom-up pass of the loose sand: a grain moves one cell down, or diagonally down when blocked. A contiguous
    /// column falls as one body, so falling letters keep their shape and leaks leave unbroken one-cell drips.
    /// </summary>
    private void StepAutomaton(bool last)
    {
        var columns = Columns;
        var rows = Rows;
        var leftToRight = (_frame & 1) == 0;
        for (var row = rows - 1; row >= 0; row--)
        {
            leftToRight = !leftToRight;
            for (var k = 0; k < columns; k++)
            {
                var column = leftToRight ? k : columns - 1 - k;
                var i = row * columns + column;
                if (_state[i] != Loose)
                {
                    continue;
                }
                if (row < rows - 1)
                {
                    var below = i + columns;
                    if (_state[below] == Empty)
                    {
                        Move(i, below);
                        continue;
                    }
                    var d = (Next32() & 1) == 0 ? -1 : 1;
                    var c1 = column + d;
                    var c2 = column - d;
                    if (c1 >= 0 && c1 < columns && _state[below + d] == Empty)
                    {
                        Move(i, below + d);
                        continue;
                    }
                    if (c2 >= 0 && c2 < columns && _state[below - d] == Empty)
                    {
                        Move(i, below - d);
                        continue;
                    }
                }
                // At rest.
                if (_mask[i] && !IsDissolved)
                {
                    _state[i] = Stuck;
                    LooseCount--;
                    StuckCount++;
                    continue;
                }
                // Loose sand outside the letters lives a random while once it rests, but only while nobody digs.
                if (!last || _sinceDig < RestFrames)
                {
                    continue;
                }
                var life = _life[i];
                if (life < 0)
                {
                    _life[i] = (short)(MinLife + (int)(Next() * (MaxLife - MinLife)));
                }
                else if (life <= 1)
                {
                    _state[i] = Empty;
                    LooseCount--;
                }
                else
                {
                    _life[i] = (short)(life - 1);
                }
            }
        }
    }

    private void Move(int from, int to)
    {
        // A grain keeps its remaining life while it slides, so a shrinking pile still empties in bounded time.
        _state[to] = Loose;
        _shade[to] = _shade[from];
        _life[to] = _life[from];
        _state[from] = Empty;
        _moved = true;
    }

    private void AddFlying(float x, float y, float vx, float vy, byte shade)
    {
        if (_flyCount == _flyX.Length)
        {
            var size = Math.Max(256, _flyX.Length * 2);
            Array.Resize(ref _flyX, size);
            Array.Resize(ref _flyY, size);
            Array.Resize(ref _flyVX, size);
            Array.Resize(ref _flyVY, size);
            Array.Resize(ref _flyShade, size);
        }
        var f = _flyCount++;
        _flyX[f] = x;
        _flyY[f] = y;
        _flyVX[f] = vx;
        _flyVY[f] = vy;
        _flyShade[f] = shade;
    }

    private void EnsurePour(int count)
    {
        if (_pourTarget.Length >= count)
        {
            return;
        }
        var size = Math.Max(count, _pourTarget.Length * 2);
        Array.Resize(ref _pourTarget, size);
        Array.Resize(ref _pourY, size);
        Array.Resize(ref _pourV, size);
        Array.Resize(ref _pourShade, size);
    }

    private byte NextShade()
    {
        var p = (int)(Next() * _paletteCount);
        var v = (int)(Next() * Variants);
        return (byte)(Math.Min(p, _paletteCount - 1) * Variants + Math.Min(v, Variants - 1));
    }

    /// <summary>A small deterministic generator (xorshift), so a run is repeatable.</summary>
    private uint Next32()
    {
        var s = _seed;
        s ^= s << 13;
        s ^= s >> 17;
        s ^= s << 5;
        _seed = s;
        return s;
    }

    private double Next() => (Next32() & 0xFFFFFF) / (double)0x1000000;
}
