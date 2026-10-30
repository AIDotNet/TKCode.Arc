using Avalonia;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The built-in status loop of <see cref="PixelMascot"/> (idle, thinking, writing, done, idle, thinking, error) as a
/// shared clock. A mascot without a <see cref="PixelMascot.Status"/> plays this loop on its own; put one
/// <see cref="PixelMascotLoop"/> in resources and bind several mascots' <see cref="PixelMascot.Status"/> to its
/// <see cref="Status"/> to keep them in step. It switches only at step boundaries (one timer tick every few seconds),
/// never per frame.
/// </summary>
public sealed class PixelMascotLoop : AvaloniaObject
{
    /// <summary>The loop's steps and how long each lasts.</summary>
    public static IReadOnlyList<(PixelMascotStatus Status, TimeSpan Duration)> Steps { get; } =
    [
        (PixelMascotStatus.Idle, TimeSpan.FromSeconds(3.2)),
        (PixelMascotStatus.Thinking, TimeSpan.FromSeconds(3.36)),
        (PixelMascotStatus.Writing, TimeSpan.FromSeconds(3.2)),
        (PixelMascotStatus.Done, TimeSpan.FromSeconds(2.0)),
        (PixelMascotStatus.Idle, TimeSpan.FromSeconds(2.4)),
        (PixelMascotStatus.Thinking, TimeSpan.FromSeconds(2.24)),
        (PixelMascotStatus.Error, TimeSpan.FromSeconds(2.4))
    ];

    /// <summary>One full pass through <see cref="Steps"/>.</summary>
    public static TimeSpan CycleDuration { get; } = TimeSpan.FromTicks(Steps.Sum(s => s.Duration.Ticks));

    /// <summary>The status the loop is on.</summary>
    public static readonly DirectProperty<PixelMascotLoop, PixelMascotStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<PixelMascotLoop, PixelMascotStatus>(nameof(Status), o => o.Status);

    /// <summary>Whether the loop moves on. False holds the current status.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<PixelMascotLoop, bool>(nameof(IsEnabled), true);

    private readonly DispatcherTimer _timer;
    private int _index;
    private PixelMascotStatus _status = Steps[0].Status;

    /// <summary>Creates the loop on its first step; it starts moving at once unless <see cref="IsEnabled"/> is false.</summary>
    public PixelMascotLoop()
    {
        _timer = new DispatcherTimer { Interval = Steps[0].Duration };
        _timer.Tick += (_, _) => Advance();
        _timer.Start();
    }

    /// <inheritdoc cref="StatusProperty"/>
    public PixelMascotStatus Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <summary>The index of the current step in <see cref="Steps"/>.</summary>
    public int StepIndex => _index;

    /// <summary>The status of the loop <paramref name="elapsed"/> after it started (it repeats after <see cref="CycleDuration"/>).</summary>
    public static PixelMascotStatus StatusAt(TimeSpan elapsed) => Steps[StepAt(elapsed)].Status;

    /// <summary>The index in <see cref="Steps"/> of the step <paramref name="elapsed"/> after the loop started.</summary>
    public static int StepAt(TimeSpan elapsed)
    {
        var ticks = elapsed.Ticks % CycleDuration.Ticks;
        if (ticks < 0)
        {
            ticks += CycleDuration.Ticks;
        }
        for (var i = 0; i < Steps.Count; i++)
        {
            ticks -= Steps[i].Duration.Ticks;
            if (ticks < 0)
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary>Moves to the next step now, whether or not the loop is enabled.</summary>
    public void Advance()
    {
        _index = (_index + 1) % Steps.Count;
        Status = Steps[_index].Status;
        Restart();
    }

    /// <summary>Goes back to the first step.</summary>
    public void Reset()
    {
        _index = 0;
        Status = Steps[0].Status;
        Restart();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Restart();
        }
    }

    private void Restart()
    {
        _timer.Stop();
        if (IsEnabled)
        {
            _timer.Interval = Steps[_index].Duration;
            _timer.Start();
        }
    }
}
