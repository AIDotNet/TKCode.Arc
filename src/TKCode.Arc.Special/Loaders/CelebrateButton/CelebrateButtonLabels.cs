using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The label of <see cref="CelebrateButton"/>: one view per status (the button's content; a spinner and the pending text;
/// a check and the success text; the error text), stacked and centred. A status change crossfades the old view out and
/// the new one in, each with a 2 px blur and a 4 px vertical drift (Arc Celebrate button, design reference: out in 150 ms
/// ease-out, in over 200 ms ease-out with the drift settling on a critically damped spring), and the width springs to the
/// new view's width (stiffness 300, critically damped), which the button's capsule follows.
/// </summary>
/// <remarks>
/// The drift directions depend on the pair of statuses (the success text rises from below, the others drop in from
/// above), and the width spring keeps its speed when a second change interrupts it, so both run on
/// <see cref="MotionValue{T}"/> rather than theme transitions. Under reduced motion the views only crossfade in 100 ms and
/// the width jumps; with motion off everything jumps.
/// </remarks>
internal sealed class CelebrateButtonLabels : Panel
{
    internal static readonly SpringSpec WidthSpring = SpringSpec.FromPhysical(300, 34.6);
    internal static readonly SpringSpec DriftSpring = SpringSpec.FromPhysical(680, 52);
    internal const double Drift = 4;
    internal const double Blur = 2;

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly MotionSpec s_enterFull = MotionSpec.Tween(TimeSpan.FromMilliseconds(200), s_easeOut);
    private static readonly MotionSpec s_exitFull = MotionSpec.Tween(TimeSpan.FromMilliseconds(150), s_easeOut);
    private static readonly MotionSpec s_fadeReduced = MotionSpec.Tween(TimeSpan.FromMilliseconds(100), s_easeOut);
    private static readonly MotionSpec s_drift = MotionSpec.Spring(DriftSpring);
    private static readonly MotionSpec s_width = MotionSpec.Spring(WidthSpring);

    private readonly LabelView[] _views = new LabelView[4];
    private readonly TextBlock _pendingText = new();
    private readonly TextBlock _successText = new();
    private readonly TextBlock _errorText = new();
    private CelebrateButtonStatus _active = CelebrateButtonStatus.Idle;
    private MotionValue<double>? _width;
    private double _shownWidth = double.NaN;
    private double _targetWidth = double.NaN;
    private bool _measuring;

    public CelebrateButtonLabels(CelebrateButton owner)
    {
        IsHitTestVisible = false;
        var content = new ContentPresenter();
        content.Bind(ContentPresenter.ContentProperty, owner.GetObservable(ContentControl.ContentProperty));
        content.Bind(ContentPresenter.ContentTemplateProperty, owner.GetObservable(ContentControl.ContentTemplateProperty));
        content.VerticalAlignment = VerticalAlignment.Center;

        var spinner = new ArcSpinner { VerticalAlignment = VerticalAlignment.Center };
        spinner.Bind(WidthProperty, spinner.GetResourceObservable(CelebrateButtonTokens.Key(CelebrateButtonTokenKind.SpinnerSize)));
        spinner.Bind(HeightProperty, spinner.GetResourceObservable(CelebrateButtonTokens.Key(CelebrateButtonTokenKind.SpinnerSize)));
        spinner.Bind(ArcSpinner.ThicknessProperty, spinner.GetResourceObservable(CelebrateButtonTokens.Key(CelebrateButtonTokenKind.SpinnerThickness)));

        var check = new ArcGlyph { Data = ArcIcons.Check, VerticalAlignment = VerticalAlignment.Center };
        check.Bind(ArcGlyph.SizeProperty, check.GetResourceObservable(CelebrateButtonTokens.Key(CelebrateButtonTokenKind.CheckSize)));

        foreach (var text in new[] { _pendingText, _successText, _errorText })
        {
            text.VerticalAlignment = VerticalAlignment.Center;
        }

        _views[(int)CelebrateButtonStatus.Idle] = new LabelView(content);
        _views[(int)CelebrateButtonStatus.Pending] = new LabelView(spinner, _pendingText);
        _views[(int)CelebrateButtonStatus.Success] = new LabelView(check, _successText);
        _views[(int)CelebrateButtonStatus.Error] = new LabelView(_errorText);
        for (var i = 0; i < _views.Length; i++)
        {
            var view = _views[i];
            view.Bind(StackPanel.SpacingProperty, view.GetResourceObservable(CelebrateButtonTokens.Key(CelebrateButtonTokenKind.IconGap)));
            view.Apply(i == 0 ? 1 : 0, 0, false);
            view.IsVisible = i == 0;
            Children.Add(view);
        }
    }

    /// <summary>The status whose view shows (or is coming in).</summary>
    internal CelebrateButtonStatus Active => _active;

    /// <summary>The width the label reports now (springing toward the active view's width).</summary>
    internal double ShownWidth => _shownWidth;

    /// <summary>The view of <paramref name="status"/> (tests read its opacity, drift and visibility).</summary>
    internal StackPanel ViewOf(CelebrateButtonStatus status) => _views[(int)status];

    /// <summary>The current drift of the view of <paramref name="status"/> in px (positive is down).</summary>
    internal double DriftOf(CelebrateButtonStatus status) => _views[(int)status].Shift.Y;

    internal string? PendingText => _pendingText.Text;

    internal string? SuccessText => _successText.Text;

    internal string? ErrorText => _errorText.Text;

    /// <summary>Sets the texts of the pending, success and error views.</summary>
    public void SetTexts(string pending, string success, string error)
    {
        _pendingText.Text = pending;
        _successText.Text = success;
        _errorText.Text = error;
    }

    /// <summary>Swaps to the view of <paramref name="status"/>; <paramref name="animate"/> false jumps (first show, motion off).</summary>
    public void Show(CelebrateButtonStatus status, bool animate)
    {
        if (status == _active)
        {
            return;
        }
        _active = status;
        var level = animate ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (TopLevel.GetTopLevel(this) is null)
        {
            level = ArcMotionLevel.Off;
        }
        var full = level == ArcMotionLevel.Full;
        var incoming = _views[(int)status];
        incoming.IsVisible = true;
        if (level == ArcMotionLevel.Off)
        {
            foreach (var view in _views)
            {
                view.Stop();
                view.Apply(view == incoming ? 1 : 0, 0, false);
                view.IsVisible = view == incoming;
            }
            InvalidateMeasure();
            return;
        }
        // In: from 4 px above (or below, for the success text, which rises) and blurred, to rest.
        var from = status == CelebrateButtonStatus.Success ? Drift : -Drift;
        var presence = incoming.EnsureMotion(this, full);
        if (presence.Presence.Value <= 0.001 && full)
        {
            presence.Lift.Jump(from);
        }
        _ = presence.Presence.AnimateTo(1, full ? s_enterFull : s_fadeReduced);
        if (full)
        {
            _ = presence.Lift.AnimateTo(0, s_drift);
        }
        else
        {
            presence.Lift.Jump(0);
        }
        // Out: every other view still showing fades, blurs and drifts away (up when returning to rest, down otherwise).
        var to = status == CelebrateButtonStatus.Idle ? -Drift : Drift;
        foreach (var view in _views)
        {
            if (view == incoming || !view.IsVisible)
            {
                continue;
            }
            var motion = view.EnsureMotion(this, full);
            HideAfterExit(view, motion.Presence.AnimateTo(0, full ? s_exitFull : s_fadeReduced));
            if (full)
            {
                _ = motion.Lift.AnimateTo(to, s_exitFull);
            }
        }
        InvalidateMeasure();
    }

    private async void HideAfterExit(LabelView view, Task<bool> exit)
    {
        var done = await exit;
        if (done && view != _views[(int)_active])
        {
            view.IsVisible = false;
            view.Apply(0, 0, false);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        try
        {
            var height = 0.0;
            foreach (var view in _views)
            {
                view.Measure(Size.Infinity);
                height = Math.Max(height, view.DesiredSize.Height);
            }
            var target = _views[(int)_active].DesiredSize.Width;
            if (double.IsNaN(_shownWidth))
            {
                _shownWidth = target;
                _targetWidth = target;
            }
            else if (!target.Equals(_targetWidth))
            {
                _targetWidth = target;
                var level = TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
                if (level == ArcMotionLevel.Full)
                {
                    if (_width is null)
                    {
                        _width = new MotionValue<double>(this, _shownWidth, MotionVectors.Double);
                        _width.Changed += OnWidthChanged;
                    }
                    _ = _width.AnimateTo(target, s_width);
                }
                else
                {
                    _width?.Jump(target);
                    _shownWidth = target;
                }
            }
            return new Size(_shownWidth, height);
        }
        finally
        {
            _measuring = false;
        }
    }

    private void OnWidthChanged(double value)
    {
        _shownWidth = value;
        if (!_measuring)
        {
            InvalidateMeasure();
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var view in _views)
        {
            var size = view.DesiredSize;
            view.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }

    /// <summary>One status view: a horizontal row (icon, text) with its presence and drift.</summary>
    private sealed class LabelView : StackPanel
    {
        public readonly TranslateTransform Shift = new();
        private Animators? _motion;

        public LabelView(params Control[] children)
        {
            Orientation = Orientation.Horizontal;
            RenderTransform = Shift;
            foreach (var child in children)
            {
                Children.Add(child);
            }
        }

        public Animators EnsureMotion(Visual owner, bool full)
        {
            if (_motion is null)
            {
                _motion = new Animators(new MotionValue<double>(owner, Opacity, MotionVectors.Double),
                    new MotionValue<double>(owner, Shift.Y, MotionVectors.Double));
                _motion.Presence.Changed += _ => Sync();
                _motion.Lift.Changed += _ => Sync();
            }
            _motion.Full = full;
            return _motion;
        }

        public void Stop()
        {
            if (_motion is not null)
            {
                _motion.Presence.Stop();
                _motion.Lift.Stop();
                _motion.Presence.Jump(Opacity);
                _motion.Lift.Jump(Shift.Y);
            }
        }

        public void Apply(double presence, double lift, bool blur)
        {
            if (_motion is not null)
            {
                _motion.Presence.Jump(presence);
                _motion.Lift.Jump(lift);
            }
            Opacity = presence;
            Shift.Y = lift;
            Effect = blur ? ArcBlur.Get(Blur * (1 - presence)) : null;
        }

        private void Sync()
        {
            if (_motion is null)
            {
                return;
            }
            var p = Math.Clamp(_motion.Presence.Value, 0, 1);
            Opacity = p;
            Shift.Y = _motion.Lift.Value;
            Effect = _motion.Full ? ArcBlur.Get(Blur * (1 - p)) : null;
        }

        public sealed class Animators(MotionValue<double> presence, MotionValue<double> lift)
        {
            public MotionValue<double> Presence { get; } = presence;

            public MotionValue<double> Lift { get; } = lift;

            public bool Full { get; set; }
        }
    }
}
