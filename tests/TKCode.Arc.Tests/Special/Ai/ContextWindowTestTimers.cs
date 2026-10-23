namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>Timers for ContextWindow tests: nothing fires until <see cref="Advance"/> moves the test clock past it.</summary>
internal sealed class ContextWindowTestTimers : IContextWindowTimers
{
    private readonly List<Shot> _shots = new();

    /// <summary>Milliseconds since the timers were created.</summary>
    public double Now { get; private set; }

    /// <summary>The delays asked for, in order (milliseconds).</summary>
    public List<double> Requested { get; } = new();

    /// <summary>Timers that have not fired or been cancelled.</summary>
    public int Pending => _shots.Count(s => !s.Done);

    public IDisposable Start(TimeSpan delay, Action callback)
    {
        Requested.Add(delay.TotalMilliseconds);
        var shot = new Shot(Now + delay.TotalMilliseconds, callback);
        _shots.Add(shot);
        return shot;
    }

    /// <summary>Moves the clock and fires every timer that comes due, in order (a timer started by a callback can fire too).</summary>
    public void Advance(double milliseconds)
    {
        var end = Now + milliseconds;
        while (true)
        {
            var next = _shots.Where(s => !s.Done && s.Due <= end).OrderBy(s => s.Due).FirstOrDefault();
            if (next is null)
            {
                break;
            }
            Now = Math.Max(Now, next.Due);
            next.Fire();
        }
        Now = end;
    }

    private sealed class Shot(double due, Action callback) : IDisposable
    {
        public double Due { get; } = due;

        public bool Done { get; private set; }

        public void Fire()
        {
            if (Done)
            {
                return;
            }
            Done = true;
            callback();
        }

        public void Dispose() => Done = true;
    }
}
