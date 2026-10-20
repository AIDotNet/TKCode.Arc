using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

public enum TooltipSide
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>
/// A short label on hover or keyboard focus. Attach it to any control with <c>arc:Tooltip.Tip="Copy link"</c>
/// (and optionally <c>arc:Tooltip.Side</c>). The first tooltip waits 250ms and rises 3px from its trigger;
/// while any tooltip is open, and for 300ms after the last one closes, the next opens at once with a fade only.
/// String content that changes while open crossfades and the bubble springs to the new size.
/// The <see cref="Tooltip"/> instance itself is the bubble shown in the popup.
/// </summary>
public class Tooltip : ContentControl
{
    public static readonly AttachedProperty<object?> TipProperty =
        AvaloniaProperty.RegisterAttached<Tooltip, Control, object?>("Tip");

    public static readonly AttachedProperty<TooltipSide> SideProperty =
        AvaloniaProperty.RegisterAttached<Tooltip, Control, TooltipSide>("Side");

    public static readonly AttachedProperty<TimeSpan> ShowDelayProperty =
        AvaloniaProperty.RegisterAttached<Tooltip, Control, TimeSpan>("ShowDelay", TimeSpan.FromMilliseconds(250));

    public static readonly AttachedProperty<bool> IsOpenProperty =
        AvaloniaProperty.RegisterAttached<Tooltip, Control, bool>("IsOpen");

    private static readonly AttachedProperty<TooltipSession?> SessionProperty =
        AvaloniaProperty.RegisterAttached<Tooltip, Control, TooltipSession?>("Session");

    public static object? GetTip(Control element) => element.GetValue(TipProperty);
    public static void SetTip(Control element, object? value) => element.SetValue(TipProperty, value);
    public static TooltipSide GetSide(Control element) => element.GetValue(SideProperty);
    public static void SetSide(Control element, TooltipSide value) => element.SetValue(SideProperty, value);
    public static TimeSpan GetShowDelay(Control element) => element.GetValue(ShowDelayProperty);
    public static void SetShowDelay(Control element, TimeSpan value) => element.SetValue(ShowDelayProperty, value);

    /// <summary>Opens or closes the tooltip programmatically (no delay).</summary>
    public static bool GetIsOpen(Control element) => element.GetValue(IsOpenProperty);
    public static void SetIsOpen(Control element, bool value) => element.SetValue(IsOpenProperty, value);

    /// <summary>The popup hosting <paramref name="target"/>'s bubble (created on first open).</summary>
    internal static ArcPopup? GetPopup(Control target) => target.GetValue(SessionProperty)?.Popup;

    private static readonly TimeSpan SkipWindow = TimeSpan.FromMilliseconds(300);
    private static int s_openCount;
    private static bool s_warm;
    private static DispatcherTimer? s_coolTimer;

    static Tooltip()
    {
        TipProperty.Changed.AddClassHandler<Control>(OnTipChanged);
        IsOpenProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            var session = c.GetValue(SessionProperty);
            if (session is null)
            {
                return;
            }
            if (e.GetNewValue<bool>())
            {
                session.Open();
            }
            else
            {
                session.Close();
            }
        });
        IsHitTestVisibleProperty.OverrideDefaultValue<Tooltip>(false);
        FocusableProperty.OverrideDefaultValue<Tooltip>(false);
    }

    private static void OnTipChanged(Control target, AvaloniaPropertyChangedEventArgs e)
    {
        var session = target.GetValue(SessionProperty);
        if (e.NewValue is null)
        {
            session?.Dispose();
            target.ClearValue(SessionProperty);
            return;
        }
        if (session is null)
        {
            target.SetValue(SessionProperty, new TooltipSession(target));
        }
        else
        {
            session.UpdateContent();
        }
    }

    private static void MarkOpened()
    {
        s_openCount++;
        s_coolTimer?.Stop();
        s_warm = true;
    }

    private static void MarkClosed()
    {
        s_openCount = Math.Max(0, s_openCount - 1);
        if (s_openCount > 0)
        {
            return;
        }
        s_coolTimer ??= new DispatcherTimer(SkipWindow, DispatcherPriority.Normal, (_, _) =>
        {
            s_coolTimer!.Stop();
            s_warm = false;
        });
        s_coolTimer.Stop();
        s_coolTimer.Start();
    }

    /// <summary>Per-target state: one popup and bubble, created on first open.</summary>
    private sealed class TooltipSession : IDisposable
    {
        private readonly Control _target;
        private ArcPopup? _popup;
        private Tooltip? _bubble;
        private DispatcherTimer? _delay;
        private bool _counted;

        public ArcPopup? Popup => _popup;

        public TooltipSession(Control target)
        {
            _target = target;
            target.PointerEntered += OnPointerEntered;
            target.PointerExited += OnPointerExited;
            target.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            target.GotFocus += OnGotFocus;
            target.LostFocus += OnLostFocus;
            target.DetachedFromVisualTree += OnDetached;
        }

        private void OnPointerEntered(object? sender, PointerEventArgs e) => Schedule();

        private void OnPointerExited(object? sender, PointerEventArgs e) => Close();

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => Close();

        private void OnGotFocus(object? sender, FocusChangedEventArgs e)
        {
            if (e.NavigationMethod == NavigationMethod.Tab)
            {
                Schedule();
            }
        }

        private void OnLostFocus(object? sender, FocusChangedEventArgs e) => Close();

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Close(immediate: true);

        private void Schedule()
        {
            if (GetTip(_target) is null || !_target.IsEffectivelyEnabled && !_target.IsPointerOver)
            {
                return;
            }
            if (s_warm)
            {
                Open();
                return;
            }
            if (_delay is null)
            {
                _delay = new DispatcherTimer(DispatcherPriority.Normal);
                _delay.Tick += OnDelayElapsed;
            }
            _delay.Stop();
            _delay.Interval = GetShowDelay(_target);
            _delay.Start();
        }

        private void OnDelayElapsed(object? sender, EventArgs e)
        {
            _delay?.Stop();
            Open();
        }

        public void Open()
        {
            _delay?.Stop();
            var tip = GetTip(_target);
            if (tip is null || TopLevel.GetTopLevel(_target) is null)
            {
                return;
            }
            EnsurePopup();
            var popup = _popup!;
            var instant = s_warm;
            popup.IsInstant = instant;
            popup.Placement = GetSide(_target) switch
            {
                TooltipSide.Bottom => PlacementMode.Bottom,
                TooltipSide.Left => PlacementMode.Left,
                TooltipSide.Right => PlacementMode.Right,
                _ => PlacementMode.Top
            };
            UpdateContent();
            if (!_counted)
            {
                _counted = true;
                MarkOpened();
            }
            if (popup.Parent is null)
            {
                ((ISetLogicalParent)popup).SetParent(_target);
            }
            popup.IsOpen = true;
        }

        public void UpdateContent()
        {
            if (_bubble is not null)
            {
                _bubble.Content = GetTip(_target);
            }
        }

        public void Close(bool immediate = false)
        {
            _delay?.Stop();
            if (_counted)
            {
                _counted = false;
                MarkClosed();
            }
            if (_popup is { IsOpen: true } popup)
            {
                if (immediate)
                {
                    popup.IsMotionEnabled = false;
                }
                popup.IsOpen = false;
            }
        }

        private void EnsurePopup()
        {
            if (_popup is not null)
            {
                return;
            }
            _bubble = new Tooltip();
            _popup = new ArcPopup
            {
                Child = _bubble,
                PlacementTarget = _target,
                IsLightDismissEnabled = false,
                EnterOffset = 3,
                EnterScale = 0.97,
                MarginToAnchor = 8,
                TakesFocusFromNativeControl = false,
                WindowManagerAddShadowHint = false
            };
            _popup.Closed += (_, _) =>
            {
                if (_popup is { IsOpen: false } closed && closed.Parent is not null)
                {
                    ((ISetLogicalParent)closed).SetParent(null);
                }
            };
        }

        public void Dispose()
        {
            Close(immediate: true);
            _target.PointerEntered -= OnPointerEntered;
            _target.PointerExited -= OnPointerExited;
            _target.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            _target.GotFocus -= OnGotFocus;
            _target.LostFocus -= OnLostFocus;
            _target.DetachedFromVisualTree -= OnDetached;
        }
    }
}

/// <summary>Wraps string tooltip content in a wrapping text block limited to the bubble's text width.</summary>
internal sealed class TooltipTextTemplate : Avalonia.Controls.Templates.IDataTemplate
{
    public double MaxTextWidth { get; set; } = 206;

    public Control? Build(object? param) => new TextBlock
    {
        Text = param as string ?? Convert.ToString(param, System.Globalization.CultureInfo.CurrentCulture),
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        MaxWidth = MaxTextWidth
    };

    public bool Match(object? data) => data is string or IFormattable;
}
