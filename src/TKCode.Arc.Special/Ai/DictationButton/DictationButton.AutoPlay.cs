using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The composer's autoplay: with a simulation it loops a scripted dictation (listen, insert, send) until the person first
// presses a pointer, a key or moves the focus inside the card. Its clock, and the simulated speech, stand still while the
// card is off screen or hidden.
public partial class DictationButton
{
    /// <summary>The pause before the mic is pressed, after an insert and after a send.</summary>
    internal static readonly TimeSpan AutoPlayIdle = TimeSpan.FromMilliseconds(1400);

    internal static readonly TimeSpan AutoPlayBeforeInsert = TimeSpan.FromMilliseconds(700);

    internal static readonly TimeSpan AutoPlayBeforeSend = TimeSpan.FromMilliseconds(1100);

    /// <summary>The longest a scripted dictation listens before the autoplay inserts anyway.</summary>
    internal static readonly TimeSpan AutoPlayListenLimit = TimeSpan.FromSeconds(14);

    private CancellationTokenSource? _autoPlay;
    private bool _interrupted;
    private bool _speechEnded;
    private string? _autoText;

    /// <summary>
    /// Whether the autoplay should run: there is something to dictate with, and it was asked for or left to default with a
    /// simulation and no recognizer.
    /// </summary>
    internal bool AutoPlays => !_interrupted && (Recognizer is not null || Simulation is not null) && (AutoPlay ?? Recognizer is null);

    /// <summary>The composer's simulation stands still while the card is off screen (a replaced one runs freely again).</summary>
    private void PauseOffScreen(SimulatedDictationRecognizer? old, SimulatedDictationRecognizer? simulation)
    {
        if (old is not null)
        {
            old.Paused = null;
        }
        if (simulation is not null)
        {
            simulation.Paused = () => !IsOnScreen();
        }
    }

    /// <summary>The autoplay loop runs.</summary>
    internal bool IsAutoPlaying => _autoPlay is not null;

    private bool IsOnScreen()
    {
        if (!IsEffectivelyVisible)
        {
            return false;
        }
        var viewport = ArcViewport.Calculate(this);
        return viewport.Width > 0 && viewport.Height > 0 && viewport.Intersects(new Rect(Bounds.Size));
    }

    /// <summary>
    /// Starts or stops the loop. When the app's inputs <paramref name="changed"/> so that it no longer plays (the simulation
    /// was cleared, a recognizer arrived, autoplay was turned off), its dictation and its words leave the card too.
    /// </summary>
    private void UpdateAutoPlay(bool changed = false)
    {
        if (AutoPlays && IsAttachedToVisualTree() && _dictation is not null)
        {
            if (_autoPlay is null)
            {
                _autoPlay = new CancellationTokenSource();
                _ = AutoPlayLoopAsync(_autoPlay.Token);
            }
        }
        else if (changed && _autoPlay is not null)
        {
            StopAutoPlayLoop();
            if (_dictation?.Controller.IsActive == true)
            {
                _dictation.Cancel();
            }
            if (_autoText is not null && Text == _autoText)
            {
                SetCurrentValue(TextProperty, string.Empty);
            }
            _autoText = null;
        }
        else
        {
            StopAutoPlayLoop();
        }
    }

    private bool IsAttachedToVisualTree() => Avalonia.Controls.TopLevel.GetTopLevel(this) is not null;

    private void StopAutoPlayLoop()
    {
        var cancel = _autoPlay;
        _autoPlay = null;
        cancel?.Cancel();
        cancel?.Dispose();
    }

    /// <summary>The person took over: the script stops for good and its words leave the field.</summary>
    internal void InterruptAutoPlay()
    {
        if (_interrupted || _autoPlay is null)
        {
            _interrupted = true;
            return;
        }
        _interrupted = true;
        StopAutoPlayLoop();
        if (_dictation?.Controller.IsActive == true)
        {
            _dictation.Cancel();
        }
        if (_autoText is not null && Text == _autoText)
        {
            SetCurrentValue(TextProperty, string.Empty);
        }
        _autoText = null;
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e) => InterruptAutoPlay();

    private void OnGotFocus(object? sender, FocusChangedEventArgs e) => InterruptAutoPlay();

    private async Task AutoPlayLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _dictation is { } dictation)
            {
                await Wait(AutoPlayIdle, token);
                if (dictation.Controller.IsActive || !string.IsNullOrEmpty(Text) && Text != _autoText)
                {
                    continue;
                }
                _speechEnded = false;
                dictation.SpeechEnded += OnAutoSpeechEnded;
                try
                {
                    dictation.Start();
                    await WaitUntil(() => dictation.Status != DictationStatus.Starting, TimeSpan.FromSeconds(5), token);
                    if (dictation.Status != DictationStatus.Listening)
                    {
                        continue;
                    }
                    await WaitUntil(() => _speechEnded || dictation.Status != DictationStatus.Listening, AutoPlayListenLimit, token);
                    await Wait(AutoPlayBeforeInsert, token);
                    if (dictation.Status == DictationStatus.Listening)
                    {
                        dictation.Confirm();
                    }
                    await WaitUntil(() => !dictation.Controller.IsActive, TimeSpan.FromSeconds(5), token);
                }
                finally
                {
                    dictation.SpeechEnded -= OnAutoSpeechEnded;
                }
                _autoText = Text;
                await Wait(AutoPlayBeforeSend, token);
                if (!string.IsNullOrEmpty(Text) && Text == _autoText)
                {
                    await SendAsync();
                }
                _autoText = null;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnAutoSpeechEnded(object? sender, RoutedEventArgs e) => _speechEnded = true;

    private Task Wait(TimeSpan span, CancellationToken token) =>
        DictationSimulationClock.Delay(span, () => !IsOnScreen(), token);

    private async Task WaitUntil(Func<bool> condition, TimeSpan limit, CancellationToken token)
    {
        var waited = TimeSpan.Zero;
        var step = TimeSpan.FromMilliseconds(50);
        while (!condition() && waited < limit)
        {
            await Wait(step, token);
            waited += step;
        }
    }
}
