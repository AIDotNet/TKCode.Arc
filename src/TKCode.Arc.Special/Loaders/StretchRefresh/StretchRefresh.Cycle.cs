using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The refresh cycle: start (sheet to 60, line runs, glyph spins), result (line gives way to a drawn check and the result
/// text, the header glyph becomes a check), sheet return at +1210 ms and rest at +1860 ms (reduced motion: both at
/// +1200 ms, the sheet jumps). Measured on Arc Stretch refresh (design reference).
/// </summary>
public partial class StretchRefresh
{
    private const string IdleGlyphKey = "idle";
    private const string DoneGlyphKey = "done";
    private const string FailedGlyphKey = "failed";

    private CancellationTokenSource? _work;
    private int _cycle;
    private ArcGlyph? _doneGlyph;
    private ArcGlyph? _failedGlyph;
    private ArcGlyph? _spinGlyph;

    private Task<bool> StartRefresh(StretchRefreshTrigger trigger)
    {
        if (!_machine.Trigger())
        {
            return Task.FromResult(false);
        }
        // A button or code refresh ends a wheel pull that is still waiting for its idle release.
        StopWheelTimer();
        _gesture.EndWheel();
        UpdateState();
        return RunCycle(trigger);
    }

    private async Task<bool> RunCycle(StretchRefreshTrigger trigger)
    {
        var cycle = ++_cycle;
        _work?.Dispose();
        _work = new CancellationTokenSource();
        var token = _work.Token;
        ShowRefreshing();
        Announce(Labels.Refreshing);
        RefreshStarted?.Invoke(this, new StretchRefreshStartedEventArgs(trigger));
        string? result = null;
        Exception? error = null;
        try
        {
            if (RefreshHandler is { } handler)
            {
                result = await handler(token);
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        if (cycle != _cycle)
        {
            return true;
        }
        var text = error is null ? StretchRefreshMath.ResultText(result, Labels) : Labels.Error;
        _machine.Complete(error is not null);
        UpdateState();
        ShowResult(text, error is not null);
        Announce(text);
        RefreshCompleted?.Invoke(this, new StretchRefreshCompletedEventArgs(trigger, text, error));
        _ = Settle(cycle);
        return true;
    }

    /// <summary>Waits on <see cref="Time"/> (tests step it).</summary>
    private Task Delay(TimeSpan span) => Task.Delay(span, Time, CancellationToken.None);

    private async Task Settle(int cycle)
    {
        if (ArcMotion.IsFull(this))
        {
            await Delay(StretchRefreshMath.SheetReturnDelay);
            if (cycle != _cycle)
            {
                return;
            }
            _ = _y?.AnimateTo(0, MotionSpec.Spring(ReturnSpring));
            await Delay(StretchRefreshMath.SettleDelay - StretchRefreshMath.SheetReturnDelay);
        }
        else
        {
            await Delay(StretchRefreshMath.ReducedSettleDelay);
            if (cycle != _cycle)
            {
                return;
            }
            _y?.Stop();
            _y?.Jump(0);
        }
        if (cycle != _cycle || !_machine.Settle())
        {
            return;
        }
        UpdateState();
        ShowIdle(cycle);
    }

    private void UpdateState()
    {
        var state = _machine.State;
        State = state;
        IsRefreshing = state == StretchRefreshState.Refreshing;
        PseudoClasses.Set(IdlePseudoClass, state == StretchRefreshState.Idle);
        PseudoClasses.Set(PullingPseudoClass, state == StretchRefreshState.Pulling);
        PseudoClasses.Set(ArmedPseudoClass, state == StretchRefreshState.Armed);
        PseudoClasses.Set(RefreshingPseudoClass, state == StretchRefreshState.Refreshing);
        PseudoClasses.Set(DonePseudoClass, state == StretchRefreshState.Done);
        PseudoClasses.Set(FailedPseudoClass, state == StretchRefreshState.Failed);
        PseudoClasses.Set(BusyPseudoClass, _machine.IsBusy);
        PseudoClasses.Set(HintPseudoClass, state is StretchRefreshState.Idle or StretchRefreshState.Pulling);
        if (_button is not null)
        {
            _button.Classes.Set("busy", _machine.IsBusy);
        }
        if (_scroller is not null)
        {
            // Stands in for aria-busy on the list.
            AutomationProperties.SetItemStatus(_scroller, state == StretchRefreshState.Refreshing ? Labels.Refreshing : null);
        }
    }

    /// <summary>Past the threshold (or back under it): the track over-stretches to 1.12 (Full only), the fill turns to the foreground, the hint changes.</summary>
    private void ShowArmed(bool armed)
    {
        if (ArcMotion.IsFull(this))
        {
            _ = _trackScale?.AnimateTo(armed ? StretchRefreshMath.ArmedStretch : 1, MotionSpec.Spring(QuickSpring));
        }
        _ = _armedMix?.AnimateTo(armed ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        _status?.ShowText(armed ? Labels.ReleaseHint : Labels.PullHint);
    }

    private void ShowRefreshing()
    {
        _ = _y?.AnimateTo(StretchRefreshMath.RestOffset, MotionSpec.Spring(RestSpring));
        _ = _tension?.AnimateTo(0, MotionSpec.Spring(RestSpring));
        _ = _trackScale?.AnimateTo(1, MotionSpec.Spring(QuickSpring));
        _trackOpacity?.Jump(1);
        _ = _fillOpacity?.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut));
        _ = _runOpacity?.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut));
        _runTime = 0;
        if (_meter is not null)
        {
            _meter.RunPosition = 0;
        }
        _runLoop?.Start();
        _status?.ShowText(Labels.Refreshing);
        if (_spin is not null)
        {
            _spin.Angle = 0;
        }
        _spinLoop?.Start();
    }

    private void ShowResult(string text, bool failed)
    {
        // The segment stops where it is and fades; the track shrinks to 0.4 and fades on the standard curve.
        _runLoop?.Stop();
        _ = _runOpacity?.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut));
        _ = _trackScale?.AnimateTo(StretchRefreshMath.ShrunkStretch, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        _ = _trackOpacity?.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        _spinLoop?.Stop();
        ShowMark(failed);
        _status?.ShowText(text);
        if (_headerIcon is not null)
        {
            // The result glyphs are made once and reused by every cycle.
            ArcGlyph glyph;
            if (failed)
            {
                glyph = _failedGlyph ??= new ArcGlyph { Data = ArcIcons.X, Size = 16, StrokeThickness = 2.2 };
                glyph.Foreground = DangerInk;
            }
            else
            {
                glyph = _doneGlyph ??= new ArcGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2.2 };
                glyph.Foreground = SuccessInk;
            }
            _headerIcon.Show(failed ? FailedGlyphKey : DoneGlyphKey, glyph);
        }
    }

    /// <summary>
    /// The status mark: in from transparent and a 2 px blur (160 ms, +60 ms), up from 0.5 on the quick spring (+75 ms),
    /// and its stroke drawn over 320 ms (+160 ms). A failure shows a cross in the danger colour.
    /// </summary>
    private void ShowMark(bool failed)
    {
        if (_markHost is null || _mark is null || _markFade is null || _markScale is null)
        {
            return;
        }
        _markFull = ArcMotion.IsFull(this);
        _mark.Data = failed ? ArcIcons.X : ArcIcons.Check;
        _mark.Foreground = failed ? DangerInk : SuccessInk;
        _markHost.IsVisible = true;
        _markFade.Jump(0);
        _ = _markFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter, TimeSpan.FromMilliseconds(60)));
        if (_markFull)
        {
            _markScale.Jump(0.5);
            _ = _markScale.AnimateTo(1, MotionSpec.Spring(QuickSpring, TimeSpan.FromMilliseconds(75)));
            _mark.Progress = 0;
            _ = _mark.Play(TimeSpan.FromMilliseconds(320), ArcEasings.Enter, TimeSpan.FromMilliseconds(160));
        }
        else
        {
            _markScale.Jump(1);
            _mark.Progress = 1;
        }
    }

    private void ShowIdle(int cycle)
    {
        _status?.ShowText(Labels.PullHint);
        var back = ArcMotion.IsFull(this)
            ? MotionSpec.Spring(QuickSpring)
            : MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard);
        _ = _trackScale?.AnimateTo(1, back);
        _ = _trackOpacity?.AnimateTo(1, back);
        _armedMix?.Jump(0);
        _runOpacity?.Jump(0);
        _ = _fillOpacity?.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut));
        _ = HideMark(cycle);
        _headerIcon?.Show(IdleGlyphKey, CreateSpinGlyph());
    }

    private async Task HideMark(int cycle)
    {
        if (_markFade is null)
        {
            return;
        }
        _markFull = false;
        await _markFade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, EaseOut));
        if (cycle == _cycle && _machine.State is StretchRefreshState.Idle or StretchRefreshState.Pulling or StretchRefreshState.Armed && _markHost is not null)
        {
            _markHost.IsVisible = false;
        }
    }

    /// <summary>The header's rotate-cw glyph, made once; its turn is reset whenever it comes back.</summary>
    private ArcGlyph CreateSpinGlyph()
    {
        _spin ??= new RotateTransform();
        _spin.Angle = 0;
        return _spinGlyph ??= new ArcGlyph
        {
            Data = ArcIcons.RotateCw,
            Size = 16,
            StrokeThickness = 2,
            RenderTransformOrigin = Avalonia.RelativePoint.Center,
            RenderTransform = _spin
        };
    }
}
