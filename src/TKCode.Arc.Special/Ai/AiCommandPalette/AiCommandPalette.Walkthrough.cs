using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class AiCommandPalette
{
    private LiveLoop? _walkthroughLoop;
    private IEnumerator<object>? _script;
    private double _scriptWait;
    private Func<bool>? _scriptUntil;
    private double _scriptUntilLeft;
    private bool _walkthroughStopped;
    private bool _walkthroughDirty;
    private bool _isScripted;

    /// <summary>True while the walkthrough is wanted and running (on screen, full motion).</summary>
    internal bool IsWalkthroughRunning => _walkthroughLoop?.IsRunning == true;

    /// <summary>True once a real pointer or key press has ended the walkthrough.</summary>
    internal bool IsWalkthroughStopped => _walkthroughStopped;

    private bool WalkthroughWanted => (AutoPlay ?? IsSample) && !_walkthroughStopped && this.IsAttachedToVisualTreeSafe();

    /// <summary>
    /// Starts or stops the walkthrough. It runs on a Core <see cref="LiveLoop"/> (code-driven: it is a script of key presses and
    /// typing over time), which only runs while the palette is attached, on screen and at full motion, so it never plays
    /// with reduced motion and pauses off screen; when it comes back it starts over from a clean state.
    /// </summary>
    private void UpdateWalkthrough()
    {
        if (WalkthroughWanted)
        {
            if (_walkthroughLoop is null)
            {
                _walkthroughLoop = new LiveLoop(this, StepWalkthrough);
                _walkthroughLoop.RunningChanged += OnWalkthroughRunningChanged;
            }
            _walkthroughLoop.Start();
        }
        else if (_walkthroughLoop is not null)
        {
            _walkthroughLoop.Stop();
            EndScript();
        }
    }

    private void OnWalkthroughRunningChanged(bool running)
    {
        if (running)
        {
            if (_script is null || _walkthroughDirty)
            {
                RestartScript();
            }
        }
        else if (_script is not null)
        {
            // Off screen (or motion turned down): the timers stop and the next run starts over from a clean state.
            CancelAsk();
            _walkthroughDirty = true;
        }
    }

    private void RestartScript()
    {
        _walkthroughDirty = false;
        Scripted(Reset);
        _script = Script().GetEnumerator();
        _scriptWait = 0;
        _scriptUntil = null;
    }

    private void EndScript()
    {
        if (_script is null)
        {
            return;
        }
        _script.Dispose();
        _script = null;
        _scriptUntil = null;
        Reset();
    }

    /// <summary>A real pointer or key press inside ends the walkthrough for good and leaves a clean palette.</summary>
    internal void StopWalkthroughForGood()
    {
        if (_walkthroughStopped)
        {
            return;
        }
        _walkthroughStopped = true;
        if (_walkthroughLoop is not null)
        {
            _walkthroughLoop.Stop();
            EndScript();
        }
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isScripted)
        {
            StopWalkthroughForGood();
        }
    }

    /// <summary>One frame of the walkthrough: waits, then runs the script's next beats.</summary>
    internal bool StepWalkthrough(double seconds)
    {
        if (_script is null)
        {
            RestartScript();
        }
        if (_scriptUntil is { } until)
        {
            _scriptUntilLeft -= seconds;
            if (!until() && _scriptUntilLeft > 0)
            {
                return true;
            }
            _scriptUntil = null;
        }
        _scriptWait -= seconds;
        while (_scriptWait <= 0 && _scriptUntil is null && _script is not null)
        {
            if (!_script.MoveNext())
            {
                RestartScript();
                continue;
            }
            switch (_script.Current)
            {
                case double wait:
                    _scriptWait += wait;
                    break;
                case Func<bool> condition:
                    _scriptUntil = condition;
                    _scriptUntilLeft = 15;
                    break;
            }
        }
        return true;
    }

    private void Scripted(Action action)
    {
        _isScripted = true;
        try
        {
            action();
        }
        finally
        {
            _isScripted = false;
        }
    }

    private void TypeScripted(string text) => Scripted(() => SetQuery((Query ?? string.Empty) + text));

    /// <summary>
    /// The walkthrough: search for the simulation's <see cref="AiCommandPaletteSimulation.SearchQuery"/> and look through the
    /// results, open Ask AI with Tab and browse the suggested questions, type and ask the first, let the answer stream, run
    /// its first suggested action, then go back and start over. Yields a pause in seconds or a condition to wait for.
    /// </summary>
    private IEnumerable<object> Script()
    {
        yield return 1.2;
        if (Simulation?.SearchQuery is { Length: > 0 } search)
        {
            foreach (var c in search)
            {
                TypeScripted(c.ToString());
                yield return 0.12;
            }
            yield return 0.9;
            Scripted(() => Move(1));
            yield return 0.8;
            Scripted(() => Move(1));
            yield return 0.9;
            Scripted(() => PressEscape());
            yield return 0.7;
        }
        Scripted(PressTab);
        yield return 1.1;
        Scripted(() => Move(1));
        yield return 0.6;
        Scripted(() => Move(-1));
        yield return 0.6;
        if (EffectiveQuestions.Count == 0)
        {
            // Nothing to ask: rest, go back and start over.
            yield return 2.4;
            Scripted(() => PressEscape());
            yield return 1.4;
            yield break;
        }
        foreach (var c in EffectiveQuestions[0])
        {
            TypeScripted(c.ToString());
            yield return 0.035;
        }
        yield return 0.5;
        Scripted(PressEnter);
        yield return (Func<bool>)(() => AnswerState is AiCommandPaletteAnswerState.Done or AiCommandPaletteAnswerState.Failed);
        yield return 1.4;
        Scripted(PressEnter);
        yield return (Func<bool>)(() => _session is null || _session.ActionStates.Values.Any(s => s != AiCommandPaletteActionState.Running));
        yield return 2.4;
        Scripted(() => Move(1));
        yield return 1.4;
        Scripted(() => PressEscape());
        yield return 1.4;
    }
}

internal static class AiCommandPaletteVisualExtensions
{
    public static bool IsAttachedToVisualTreeSafe(this Avalonia.Visual visual) =>
        Avalonia.VisualTree.VisualExtensions.IsAttachedToVisualTree(visual);
}
