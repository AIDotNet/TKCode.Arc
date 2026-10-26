using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>Data of <see cref="PullToRefresh.RefreshRequested"/>. A handler that works asynchronously takes a deferral.</summary>
public sealed class PullRefreshRequestedEventArgs : EventArgs
{
    private readonly Action _completed;
    private int _pending;
    private bool _deferred;
    private bool _sealed;

    internal PullRefreshRequestedEventArgs(Action completed)
    {
        _completed = completed;
    }

    /// <summary>
    /// Takes a deferral: the refresh stays in progress until every deferral taken has been disposed, and then
    /// <see cref="PullToRefresh.IsRefreshing"/> goes back to false by itself. Dispose it on any thread.
    /// </summary>
    public IDisposable GetDeferral()
    {
        _deferred = true;
        Interlocked.Increment(ref _pending);
        return new Deferral(this);
    }

    /// <summary>Called once the handlers have returned.</summary>
    internal void Seal()
    {
        _sealed = true;
        if (_deferred && Volatile.Read(ref _pending) == 0)
        {
            _completed();
        }
    }

    private void Release()
    {
        if (Interlocked.Decrement(ref _pending) == 0 && _sealed)
        {
            _completed();
        }
    }

    private sealed class Deferral(PullRefreshRequestedEventArgs owner) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                owner.Release();
            }
        }
    }
}

/// <summary>
/// Pull to refresh: wraps scrollable content. Pulling down while the content is at its top drags the content along with
/// rubber-band resistance and shows an arrow that turns over as the pull nears <see cref="PullThreshold"/>. Releasing past
/// it raises <see cref="RefreshRequested"/> and runs <see cref="Command"/>; the content rests below the indicator, which
/// spins, until <see cref="IsRefreshing"/> is false again (set it, or take a deferral from the event data). Releasing
/// early springs the content back. A pull that starts while the inner scroll viewer is scrolled, or that goes sideways or
/// up, is left to the content.
/// <para>
/// The content's position is a <see cref="MotionValue{T}"/>, not a ControlTheme transition: a pull writes it on every
/// move, and a release hands the finger's velocity to the spring, which a new pull can catch mid-flight. Under reduced
/// motion the content does not stretch with the finger; the refresh still runs and the content jumps.
/// </para>
/// </summary>
[TemplatePart("PART_Root", typeof(Control))]
[TemplatePart("PART_Content", typeof(Control))]
[TemplatePart("PART_Indicator", typeof(Control))]
[TemplatePart("PART_Arrow", typeof(Control))]
[TemplatePart("PART_Spinner", typeof(Control))]
public class PullToRefresh : ContentControl
{
    /// <summary>The limit the content's stretch approaches however far the finger goes.</summary>
    private const double MaxStretch = 200;

    /// <summary>Resistance constant: how much of the finger's first movement the content follows.</summary>
    private const double Resistance = 0.8;

    private const double DefaultExtent = 56;

    /// <summary>Whether a refresh is in progress. Two-way: a release past the threshold sets it, and setting it false ends the refresh.</summary>
    public static readonly StyledProperty<bool> IsRefreshingProperty =
        AvaloniaProperty.Register<PullToRefresh, bool>(nameof(IsRefreshing), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>How far the content has to be pulled down, in px, for a release to start a refresh: 64.</summary>
    public static readonly StyledProperty<double> PullThresholdProperty =
        AvaloniaProperty.Register<PullToRefresh, double>(nameof(PullThreshold), 64);

    /// <summary>The command a refresh runs, with <see cref="CommandParameter"/>.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PullToRefresh, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<PullToRefresh, object?>(nameof(CommandParameter));

    private readonly TranslateTransform _contentShift = new();
    private readonly TranslateTransform _indicatorShift = new();
    private readonly RotateTransform _arrowTurn = new();
    private Control? _root;
    private Control? _content;
    private Control? _indicator;
    private Control? _arrow;
    private Control? _spinner;
    private MotionValue<double>? _y;
    private DragTracker? _drag;
    private double _dragOrigin;
    private double _pull;
    private double _releaseVelocity;
    private bool _dragging;

    /// <summary>Raised when a release past the threshold starts a refresh. Take a deferral if the work is asynchronous.</summary>
    public event EventHandler<PullRefreshRequestedEventArgs>? RefreshRequested;

    /// <inheritdoc cref="IsRefreshingProperty"/>
    public bool IsRefreshing
    {
        get => GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    /// <inheritdoc cref="PullThresholdProperty"/>
    public double PullThreshold
    {
        get => GetValue(PullThresholdProperty);
        set => SetValue(PullThresholdProperty, value);
    }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>How far the content is pushed down, in px (0 at rest). For tests and diagnostics.</summary>
    public double PullOffset => _y?.Value ?? 0;

    /// <summary>How far the finger has pulled the content, as the stretch it maps to, whether or not the content shows it. For tests.</summary>
    public double PullDistance => _pull;

    protected override Type StyleKeyOverride => typeof(PullToRefresh);

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double Extent => _indicator is { Bounds.Height: > 0 } indicator ? indicator.Bounds.Height : DefaultExtent;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _root = e.NameScope.Find<Control>("PART_Root");
        _content = e.NameScope.Find<Control>("PART_Content");
        _indicator = e.NameScope.Find<Control>("PART_Indicator");
        _arrow = e.NameScope.Find<Control>("PART_Arrow");
        _spinner = e.NameScope.Find<Control>("PART_Spinner");
        if (_root is null || _content is null)
        {
            return;
        }
        _content.RenderTransform = _contentShift;
        if (_indicator is not null)
        {
            _indicator.RenderTransform = _indicatorShift;
        }
        if (_arrow is not null)
        {
            _arrow.RenderTransform = _arrowTurn;
        }
        _y = new MotionValue<double>(_root, IsRefreshing ? DefaultExtent : 0, MotionVectors.Double);
        _y.Changed += _ => Apply();
        _drag = new DragTracker(_root, new DragOptions { Axis = DragAxis.Vertical, Threshold = 4, HandledEventsToo = true })
        {
            CanBegin = _ => IsEffectivelyEnabled,
        };
        _drag.Starting += OnStarting;
        _drag.Started += (_, _) => BeginDrag();
        _drag.Moved += (_, args) => MoveDrag(args.Offset.Y);
        _drag.Completed += (_, args) => EndDrag(args.Velocity.Y, false);
        _drag.Cancelled += (_, _) => EndDrag(0, true);
        Apply();
    }

    private void Detach()
    {
        _drag?.Dispose();
        _drag = null;
        _y?.Dispose();
        _y = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRefreshingProperty && !_dragging)
        {
            Rest();
        }
    }

    /// <summary>The inner scroll viewer, if the content has one.</summary>
    private ScrollViewer? FindScroller()
    {
        if (Content is ScrollViewer own)
        {
            return own;
        }
        return (Content as Visual)?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    }

    private void OnStarting(object? sender, DragGestureEventArgs e)
    {
        if (IsRefreshing)
        {
            e.Cancel = true;
            return;
        }
        // A content that is still settling from an earlier pull can be caught going either way.
        if (_y is { } y && y.Value > 0.5)
        {
            return;
        }
        // Pass through a gesture that goes up, or that starts while the content is scrolled: the content scrolls itself.
        if (e.Offset.Y <= 0 || FindScroller() is { Offset.Y: > 0.5 })
        {
            e.Cancel = true;
        }
    }

    private void BeginDrag()
    {
        if (_y is null)
        {
            return;
        }
        // Catch the content where it is, even in flight, and convert the stretch back to finger distance.
        _y.Stop();
        _dragOrigin = RubberBand.Invert(_y.Value, MaxStretch, Resistance);
        _dragging = true;
    }

    private void MoveDrag(double offset)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        var raw = _dragOrigin + offset;
        _pull = raw <= 0 ? 0 : RubberBand.Apply(raw, MaxStretch, Resistance);
        if (!Reduced)
        {
            _y.Jump(_pull);
        }
    }

    private void EndDrag(double velocity, bool cancelled)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        _dragging = false;
        var pulled = _pull;
        _pull = 0;
        // The finger's speed becomes the content's speed through the resistance curve at this point.
        var finger = RubberBand.Invert(_y.Value, MaxStretch, Resistance);
        var slope = Resistance / Math.Pow(finger * Resistance / MaxStretch + 1, 2);
        _releaseVelocity = velocity * slope;
        if (!cancelled && !IsRefreshing && pulled >= PullThreshold)
        {
            Trigger();
            return;
        }
        Rest();
    }

    private void Trigger()
    {
        var args = new PullRefreshRequestedEventArgs(() => Dispatcher.UIThread.Post(() => IsRefreshing = false));
        IsRefreshing = true;
        RefreshRequested?.Invoke(this, args);
        if (Command is { } command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }
        args.Seal();
    }

    /// <summary>Settles the content where it belongs: below the indicator while refreshing, at the top otherwise.</summary>
    private void Rest()
    {
        var velocity = _releaseVelocity;
        _releaseVelocity = 0;
        if (_y is null)
        {
            return;
        }
        var target = IsRefreshing ? Extent : 0;
        Apply();
        if (Reduced)
        {
            _y.Stop();
            _y.Jump(target);
            return;
        }
        _ = MomentumProjector.SettleTo(_y, target, velocity, ArcSpring.Smooth);
    }

    private void Apply()
    {
        if (_y is null)
        {
            return;
        }
        var y = _y.Value;
        _contentShift.Y = y;
        var extent = Extent;
        _indicatorShift.Y = y - extent;
        if (_indicator is not null)
        {
            _indicator.Opacity = Math.Clamp(y / extent, 0, 1);
        }
        var progress = PullThreshold > 0 ? Math.Clamp(y / PullThreshold, 0, 1) : 1;
        _arrowTurn.Angle = progress * 180;
        if (_arrow is not null)
        {
            _arrow.IsVisible = !IsRefreshing;
        }
        if (_spinner is not null)
        {
            _spinner.IsVisible = IsRefreshing;
        }
    }
}
