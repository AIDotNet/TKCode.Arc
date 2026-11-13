namespace TKCode.Arc;

/// <summary>
/// The clock of a <see cref="TypewriterTerminal"/>: which line is being revealed, how many characters of a command are
/// typed, and when the next thing happens. Pure and allocation free, so the timing can be tested without a window.
/// </summary>
/// <remarks>
/// A command shows its prompt first, waits <see cref="CommandLead"/>, types its characters one by one at a human,
/// slightly uneven pace, then waits <see cref="AfterCommand"/> before its output. Each output line appears whole after
/// <see cref="BetweenOutput"/>. A line's own pause is added before it. After the last line the terminal sits at a fresh
/// prompt; when looping it waits <see cref="LoopDelay"/> and starts again.
/// </remarks>
internal sealed class TypewriterTerminalPlayer
{
    private int[] _lengths = [];
    private bool[] _commands = [];
    private double[] _pauses = [];
    private double _wait;
    private bool _typing;
    private double _sinceKey;

    /// <summary>Characters per second while typing.</summary>
    public double Speed { get; set; } = 32;

    /// <summary>The wait between a command's prompt appearing and its first keystroke.</summary>
    public double CommandLead { get; set; } = 0.5;

    /// <summary>The wait between a command's last keystroke and its first line of output (the "Enter").</summary>
    public double AfterCommand { get; set; } = 0.45;

    /// <summary>The wait between two lines of output.</summary>
    public double BetweenOutput { get; set; } = 0.08;

    /// <summary>The wait at the final prompt before a looping script starts over.</summary>
    public double LoopDelay { get; set; } = 3;

    /// <summary>Whether the script starts over after it finishes.</summary>
    public bool IsLooping { get; set; }

    /// <summary>Whether commands appear whole instead of being typed (reduced motion).</summary>
    public bool TypesInstantly { get; set; }

    /// <summary>The number of lines in the script.</summary>
    public int Count => _lengths.Length;

    /// <summary>The line being revealed; every line before it is complete. Equals <see cref="Count"/> when finished.</summary>
    public int Line { get; private set; }

    /// <summary>How many characters of the current command are typed.</summary>
    public int Chars { get; private set; }

    /// <summary>True between a command's first and last keystroke.</summary>
    public bool IsTyping => _typing;

    /// <summary>Seconds since the last keystroke; the caret stays solid shortly after one.</summary>
    public double SinceKey => _sinceKey;

    /// <summary>Every line is out and the terminal waits at its final prompt.</summary>
    public bool IsFinished => Line >= _lengths.Length;

    /// <summary>The current line is a command whose prompt is showing.</summary>
    public bool IsAtPrompt => !IsFinished && _commands[Line];

    /// <summary>Loads a script: each line's length, whether it is a command, and its own extra pause in seconds.</summary>
    public void Load(int[] lengths, bool[] commands, double[] pauses)
    {
        _lengths = lengths;
        _commands = commands;
        _pauses = pauses;
        Restart();
    }

    /// <summary>Goes back to the first line.</summary>
    public void Restart()
    {
        Line = 0;
        Chars = 0;
        _typing = false;
        _sinceKey = 1;
        _wait = _lengths.Length == 0 ? 0 : WaitBefore(0);
    }

    /// <summary>Reveals every line at once.</summary>
    public void Complete()
    {
        Line = _lengths.Length;
        Chars = 0;
        _typing = false;
        _wait = LoopDelay;
    }

    /// <summary>
    /// Advances the clock by <paramref name="dt"/> seconds. Returns how the picture changed: <c>0</c> nothing,
    /// <c>1</c> characters were typed, <c>2</c> a line was completed or the script restarted.
    /// </summary>
    public int Step(double dt)
    {
        _sinceKey += dt;
        var change = 0;
        // A long frame may cover several keystrokes or lines; the loop catches up without skipping any.
        var guard = 0;
        while (dt > 0 && guard++ < 4096)
        {
            if (IsFinished)
            {
                if (!IsLooping || _lengths.Length == 0)
                {
                    return change;
                }
                if (_wait > dt)
                {
                    _wait -= dt;
                    return change;
                }
                dt -= _wait;
                Restart();
                change = 2;
                continue;
            }
            if (_wait > dt)
            {
                _wait -= dt;
                return change;
            }
            dt -= _wait;
            _wait = 0;
            if (_commands[Line] && Chars < _lengths[Line])
            {
                if (TypesInstantly)
                {
                    Chars = _lengths[Line];
                }
                else
                {
                    Chars++;
                }
                _typing = Chars < _lengths[Line];
                _sinceKey = 0;
                change = Math.Max(change, 1);
                _wait = _typing ? KeyInterval(Line, Chars) : AfterCommand;
                continue;
            }
            // The line is done (a typed command after its Enter pause, or an output line after its wait).
            _typing = false;
            Line++;
            Chars = 0;
            change = 2;
            _wait = IsFinished ? LoopDelay : WaitBefore(Line);
        }
        return change;
    }

    /// <summary>The wait before line <paramref name="index"/> appears (or, for a command, before its first keystroke).</summary>
    private double WaitBefore(int index)
    {
        var pause = _pauses[index];
        if (_commands[index])
        {
            return pause + CommandLead;
        }
        // The first output after a command already waited for the Enter.
        var afterCommand = index > 0 && _commands[index - 1];
        return pause + (afterCommand ? 0 : BetweenOutput);
    }

    /// <summary>The time to the next keystroke: uneven like a person's, between 0.55 and 1.45 times the mean.</summary>
    private double KeyInterval(int line, int typed)
    {
        var mean = 1 / Math.Max(1, Speed);
        var hash = (uint)(line * 7919 + typed * 104729) * 2654435761u;
        var jitter = 0.55 + (hash >> 8) / (double)(1 << 24) * 0.9;
        return mean * jitter;
    }
}
