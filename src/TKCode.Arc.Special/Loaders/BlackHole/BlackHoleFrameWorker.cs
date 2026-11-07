namespace TKCode.Arc;

/// <summary>
/// Paints a black hole run's frames off the UI thread, one in flight: <see cref="Start"/> asks for the frame at a time,
/// <see cref="Wait"/> blocks until it is painted (the renderer's double buffer then holds it as <c>Output</c>). The UI
/// tick waits for the frame it asked for one tick earlier, uploads it and asks for the next, so the sequence of frames
/// is the same whatever the thread timing (tests with a manual frame clock see exactly the frames of the synchronous
/// path). One dedicated thread per run and two semaphores; a frame allocates nothing.
/// </summary>
internal sealed class BlackHoleFrameWorker : IDisposable
{
    private readonly BlackHoleRenderer _renderer;
    private readonly SemaphoreSlim _request = new(0, 1);
    private readonly SemaphoreSlim _done = new(0, 1);
    private readonly Thread _thread;
    private double _time;
    private bool _inFlight;
    private volatile bool _stopping;
    private bool _disposed;
    private Exception? _failure;

    public BlackHoleFrameWorker(BlackHoleRenderer renderer)
    {
        _renderer = renderer;
        _thread = new Thread(Run) { IsBackground = true, Name = "BlackHole frames" };
        _thread.Start();
    }

    /// <summary>A frame was asked for and not yet waited for.</summary>
    public bool InFlight => _inFlight;

    /// <summary>Frames painted on the worker (tests).</summary>
    public int Painted { get; private set; }

    /// <summary>Asks for the frame at swallow time <paramref name="t"/> (ms, reference scale); waits first for one in flight.</summary>
    public void Start(double t)
    {
        if (_inFlight)
        {
            Wait();
        }
        _time = t;
        _inFlight = true;
        _request.Release();
    }

    /// <summary>Blocks until the frame asked for is painted; false if painting failed (the run should end).</summary>
    public bool Wait()
    {
        if (_inFlight)
        {
            _done.Wait();
            _inFlight = false;
        }
        return _failure is null;
    }

    private void Run()
    {
        while (true)
        {
            _request.Wait();
            if (_stopping)
            {
                return;
            }
            try
            {
                _renderer.Render(_time);
                Painted++;
            }
            catch (Exception ex)
            {
                // Reported through Wait; the run then ends with the content back in place.
                _failure = ex;
            }
            _done.Release();
        }
    }

    /// <summary>Waits for the frame in flight, then stops the thread. The renderer's buffers are free afterwards.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Wait();
        _stopping = true;
        _request.Release();
        _thread.Join();
        _request.Dispose();
        _done.Dispose();
    }
}
