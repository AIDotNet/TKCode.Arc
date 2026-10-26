using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A bottom sheet: a panel that rises over a <see cref="Page"/> with a scrim behind it. The person closes it by tapping
/// the scrim, pulling the handle down or flicking it, or with Escape. Put it in <see cref="Page.Overlay"/> and open it
/// with <see cref="IsOpen"/>; it sizes to its content, up to <see cref="MaxHeightFraction"/> of the page, and the
/// content scrolls beyond that. Closed, it does not catch touches.
/// <para>
/// The panel's position is a <see cref="MotionValue{T}"/>, not a ControlTheme transition: a drag writes it on every
/// move, a release hands the finger's velocity to the spring, and the scrim is computed from the position each frame.
/// Under reduced motion the panel jumps instead of gliding.
/// </para>
/// </summary>
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_Panel", typeof(Border))]
[TemplatePart("PART_Handle", typeof(Control))]
[TemplatePart("PART_Body", typeof(ScrollViewer))]
public class Sheet : ContentControl
{
    /// <summary>The panel slides this far past its own height when closed, so its outline and shadow are out of sight.</summary>
    private const double ClosedGap = 24;

    /// <summary>A release coasts this long at its speed before it is decided whether the sheet closes.</summary>
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.2);

    /// <summary>A downward release faster than this (px/s) closes the sheet even if it was not pulled far.</summary>
    private const double FlickVelocity = 500;

    /// <summary>The limit an upward pull approaches past the open position.</summary>
    private const double Stretch = 80;

    /// <summary>Whether the sheet is open.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Sheet, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The title shown in the handle area, under the grabber.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Sheet, string?>(nameof(Title));

    /// <summary>Whether the person can close the sheet by tapping the scrim, pulling the panel down or pressing Escape.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<Sheet, bool>(nameof(IsDismissible), true);

    /// <summary>The tallest the panel gets, as a fraction of the sheet's height: 0.9.</summary>
    public static readonly StyledProperty<double> MaxHeightFractionProperty =
        AvaloniaProperty.Register<Sheet, double>(nameof(MaxHeightFraction), 0.9);

    private readonly TranslateTransform _translate = new();
    private Border? _scrim;
    private Border? _panel;
    private Control? _handle;
    private MotionValue<double>? _y;
    private DragTracker? _drag;
    private double _dragOrigin;
    private bool _dragging;
    private bool _mounted;
    private bool _present;

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <inheritdoc cref="MaxHeightFractionProperty"/>
    public double MaxHeightFraction
    {
        get => GetValue(MaxHeightFractionProperty);
        set => SetValue(MaxHeightFractionProperty, value);
    }

    /// <summary>The panel's offset below its open position, in px (0 when open). For tests and diagnostics.</summary>
    public double PanelOffset => _y?.Value ?? 0;

    protected override Type StyleKeyOverride => typeof(Sheet);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _panel = e.NameScope.Find<Border>("PART_Panel");
        _handle = e.NameScope.Find<Control>("PART_Handle");
        if (_panel is null)
        {
            return;
        }
        _panel.RenderTransform = _translate;
        _y = new MotionValue<double>(_panel, 4000, MotionVectors.Double);
        _y.Changed += _ => Apply();
        _panel.SizeChanged += OnPanelSizeChanged;
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        if (_handle is not null)
        {
            _drag = new DragTracker(_handle, new DragOptions { Axis = DragAxis.Vertical, Threshold = 3, RejectCrossAxis = false })
            {
                CanBegin = _ => IsOpen,
            };
            _drag.Started += (_, _) => BeginDrag();
            _drag.Moved += (_, args) => MoveDrag(args.Offset.Y);
            _drag.Completed += (_, args) => EndDrag(args.Velocity.Y);
            _drag.Cancelled += (_, _) => EndDrag(0);
        }
        _mounted = false;
        _present = false;
        UpdatePresence();
        Apply();
    }

    private void Detach()
    {
        _drag?.Dispose();
        _drag = null;
        _y?.Dispose();
        _y = null;
        if (_panel is not null)
        {
            _panel.SizeChanged -= OnPanelSizeChanged;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Close(null);
            }
        }
        else if (change.Property == MaxHeightFractionProperty || change.Property == BoundsProperty)
        {
            UpdateMaxHeight();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen && IsDismissible)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double ClosedOffset => (_panel?.Bounds.Height ?? 0) + ClosedGap;

    private void UpdateMaxHeight()
    {
        if (_panel is not null && Bounds.Height > 0)
        {
            _panel.MaxHeight = Bounds.Height * Math.Clamp(MaxHeightFraction, 0.1, 1);
        }
    }

    /// <summary>The sheet catches touches while it is open or still leaving; otherwise the page under it does.</summary>
    private void UpdatePresence()
    {
        IsHitTestVisible = IsOpen || _present;
        if (_panel is not null)
        {
            _panel.IsVisible = IsOpen || _present;
        }
    }

    private void Apply()
    {
        if (_y is null)
        {
            return;
        }
        _translate.Y = _y.Value;
        if (_scrim is not null)
        {
            var closed = ClosedOffset;
            _scrim.Opacity = closed > 0 ? Math.Clamp(1 - _y.Value / closed, 0, 1) : (IsOpen ? 1 : 0);
        }
    }

    private void Open()
    {
        if (_y is null || _panel is null)
        {
            return;
        }
        _present = true;
        UpdatePresence();
        _panel.UpdateLayout();
        if (!_mounted)
        {
            _mounted = true;
            _y.Jump(ClosedOffset);
        }
        if (Reduced)
        {
            _y.Jump(0);
            return;
        }
        _ = _y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        _panel.Focus();
    }

    /// <param name="velocity">The finger's downward speed in px/s when a flick closed the sheet, or null.</param>
    private void Close(double? velocity)
    {
        if (_y is null)
        {
            return;
        }
        if (Reduced)
        {
            _y.Jump(ClosedOffset);
            Finish();
            return;
        }
        var spec = MotionSpec.Spring(SpringSpec.FromVisual(0.3, 0));
        _ = _y.AnimateTo(ClosedOffset, spec, velocity).ContinueWith(
            t => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (t.Result)
                {
                    Finish();
                }
            }));
    }

    /// <summary>The exit landed: the sheet stops catching touches.</summary>
    private void Finish()
    {
        if (!IsOpen)
        {
            _present = false;
            UpdatePresence();
        }
    }

    private void OnPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_y is null || _dragging || _y.IsAnimating)
        {
            return;
        }
        // The panel changed size (content or window): keep it where it rests.
        _y.Jump(IsOpen ? 0 : ClosedOffset);
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOpen && IsDismissible)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private void BeginDrag()
    {
        if (_y is null)
        {
            return;
        }
        // Catch the panel where it is, even in flight; a stretched panel converts back to finger distance.
        _y.Stop();
        var current = _y.Value;
        _dragOrigin = current < 0 ? -RubberBand.Invert(-current, Stretch) : current;
        _dragging = true;
    }

    private void MoveDrag(double offset)
    {
        if (!_dragging || _y is null || Reduced)
        {
            return;
        }
        var raw = _dragOrigin + offset;
        // Down follows the finger 1:1 so it can dismiss (when it may); up past the open position resists like a rubber band.
        if (raw < 0)
        {
            _y.Jump(-RubberBand.Apply(-raw, Stretch));
        }
        else
        {
            _y.Jump(IsDismissible ? raw : RubberBand.Apply(raw, Stretch));
        }
    }

    private void EndDrag(double velocity)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        _dragging = false;
        var current = _y.Value;
        // A stretched panel springs back without the finger's speed, so it never launches past the stretch.
        if (current < 0)
        {
            velocity = 0;
        }
        Span<double> points = [0, ClosedOffset];
        var target = IsDismissible
            ? MomentumProjector.SelectSnapPoint(points, current, velocity, SnapProjection.Linear(Projection, 0, FlickVelocity))
            : 0;
        if (target == 1)
        {
            IsOpen = false;
            return;
        }
        _ = _y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth), velocity);
    }
}
