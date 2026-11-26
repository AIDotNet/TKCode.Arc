using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Gestures;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>Semantic tone of a <see cref="BannerNotification"/>.</summary>
public enum BannerTone
{
    /// <summary>Default neutral card styling.</summary>
    Neutral,

    /// <summary>Accent-tinted informational banner.</summary>
    Info,

    /// <summary>Success banner (triggers <see cref="HapticKind.Success"/>).</summary>
    Success,

    /// <summary>Warning banner (triggers <see cref="HapticKind.Warning"/>).</summary>
    Warning,

    /// <summary>Error / danger banner (triggers <see cref="HapticKind.Error"/>).</summary>
    Danger,
}

/// <summary>
/// Mobile in-app top banner notification: slides down below the device's top safe area (status bar / notch / Dynamic
/// Island) inside <see cref="Page.Overlay"/>, auto-dismisses after <see cref="Duration"/> (paused while dragging), and
/// can be swiped upward to dismiss. Supports an optional inline action button and tone-matched <see cref="Haptics"/>.
/// </summary>
[TemplatePart("PART_Host", typeof(Border))]
[TemplatePart("PART_Card", typeof(Border))]
[TemplatePart("PART_IconWrap", typeof(Control))]
[TemplatePart("PART_Icon", typeof(ArcGlyph))]
[TemplatePart("PART_Title", typeof(TextBlock))]
[TemplatePart("PART_Message", typeof(TextBlock))]
[TemplatePart("PART_Action", typeof(Button))]
[TemplatePart("PART_Handle", typeof(Control))]
[PseudoClasses(":info", ":success", ":warning", ":danger")]
public class BannerNotification : TemplatedControl
{
    private const double HiddenOffset = -140;
    private const double FlickUpVelocity = -360;
    private const double DownStretch = 24;

    /// <summary>Whether the banner is currently showing (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<BannerNotification, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Short headline of the notification.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BannerNotification, string?>(nameof(Title));

    /// <summary>Optional body message below <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<BannerNotification, string?>(nameof(Message));

    /// <summary>Optional leading glyph geometry (null uses a tone-appropriate icon when tone is non-neutral).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<BannerNotification, Geometry?>(nameof(Icon));

    /// <summary>Visual tone and haptic cue of the notification: <see cref="BannerTone.Neutral"/>.</summary>
    public static readonly StyledProperty<BannerTone> ToneProperty =
        AvaloniaProperty.Register<BannerNotification, BannerTone>(nameof(Tone));

    /// <summary>How long the banner stays open before dismissing itself (<see cref="TimeSpan.Zero"/> disables auto-dismiss): 4 seconds.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<BannerNotification, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(4));

    /// <summary>Whether the person can swipe upward to dismiss the banner: true.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<BannerNotification, bool>(nameof(IsDismissible), true);

    /// <summary>Optional label for an inline action button on the trailing side of the banner.</summary>
    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<BannerNotification, string?>(nameof(ActionText));

    /// <summary>Command executed when the inline action button is tapped.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<BannerNotification, ICommand?>(nameof(ActionCommand));

    /// <summary>Optional parameter passed to <see cref="ActionCommand"/>.</summary>
    public static readonly StyledProperty<object?> ActionCommandParameterProperty =
        AvaloniaProperty.Register<BannerNotification, object?>(nameof(ActionCommandParameter));

    /// <summary>Raised when the person taps the inline action button.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<BannerNotification, RoutedEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when the banner closes (by auto-dismiss, swipe-up, action tap or setting <see cref="IsOpen"/> false).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DismissedEvent =
        RoutedEvent.Register<BannerNotification, RoutedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    private readonly TranslateTransform _translate = new();
    private Border? _host;
    private Border? _card;
    private Control? _iconWrap;
    private ArcGlyph? _iconGlyph;
    private TextBlock? _titleBlock;
    private TextBlock? _messageBlock;
    private Button? _actionButton;
    private Control? _handle;
    private MotionValue<double>? _y;
    private DragTracker? _drag;
    private DispatcherTimer? _timer;
    private bool _present;
    private bool _dragging;

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

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="ToneProperty"/>
    public BannerTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <inheritdoc cref="ActionTextProperty"/>
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <inheritdoc cref="ActionCommandProperty"/>
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <inheritdoc cref="ActionCommandParameterProperty"/>
    public object? ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<RoutedEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <inheritdoc cref="DismissedEvent"/>
    public event EventHandler<RoutedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    /// <summary>Current vertical offset of the banner card in px (0 at rest). For tests and diagnostics.</summary>
    public double CardOffset => _translate.Y;

    protected override Type StyleKeyOverride => typeof(BannerNotification);

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double TopGap =>
        this.TryFindResource(BannerNotificationTokens.Key(BannerNotificationTokenKind.TopGap), out var v) && v is double d ? d : 8;

    private double HorizontalInset =>
        this.TryFindResource(BannerNotificationTokens.Key(BannerNotificationTokenKind.HorizontalInset), out var v) && v is double d ? d : 12;

    /// <summary>Updates the notification's content and tone, then opens (or restarts the timer of) the banner.</summary>
    public void Show(string title, string? message = null, BannerTone tone = BannerTone.Neutral, TimeSpan? duration = null)
    {
        Title = title;
        Message = message;
        Tone = tone;
        if (duration.HasValue)
        {
            Duration = duration.Value;
        }
        if (IsOpen)
        {
            RestartTimer();
            TriggerToneHaptic();
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _host = e.NameScope.Find<Border>("PART_Host");
        _card = e.NameScope.Find<Border>("PART_Card");
        _iconWrap = e.NameScope.Find<Control>("PART_IconWrap");
        _iconGlyph = e.NameScope.Find<ArcGlyph>("PART_Icon");
        _titleBlock = e.NameScope.Find<TextBlock>("PART_Title");
        _messageBlock = e.NameScope.Find<TextBlock>("PART_Message");
        _actionButton = e.NameScope.Find<Button>("PART_Action");
        _handle = e.NameScope.Find<Control>("PART_Handle");

        if (_actionButton is not null)
        {
            _actionButton.Click += OnActionClick;
        }
        if (_card is not null)
        {
            _card.RenderTransform = _translate;
            _y = new MotionValue<double>(_card, IsOpen ? 0 : HiddenOffset, MotionVectors.Double);
            _y.Changed += _ => Apply();
            _drag = new DragTracker(_card, new DragOptions { Axis = DragAxis.Vertical, Threshold = 4, RejectCrossAxis = false })
            {
                CanBegin = _ => IsOpen && IsDismissible,
            };
            _drag.Started += (_, _) => BeginDrag();
            _drag.Moved += (_, args) => MoveDrag(args.Offset.Y);
            _drag.Completed += (_, args) => EndDrag(args.Velocity.Y);
            _drag.Cancelled += (_, _) => EndDrag(0);
        }

        _present = IsOpen;
        UpdateToneAndParts();
        UpdateInsets();
        UpdatePresence();
        Apply();
        if (IsOpen)
        {
            RestartTimer();
        }
    }

    private void Detach()
    {
        StopTimer();
        _drag?.Dispose();
        _drag = null;
        _y?.Dispose();
        _y = null;
        if (_actionButton is not null)
        {
            _actionButton.Click -= OnActionClick;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateInsets();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimer();
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
                Close();
            }
        }
        else if (change.Property == ToneProperty || change.Property == IconProperty
                 || change.Property == TitleProperty || change.Property == MessageProperty
                 || change.Property == ActionTextProperty || change.Property == IsDismissibleProperty)
        {
            UpdateToneAndParts();
        }
        else if (change.Property == DurationProperty && IsOpen)
        {
            RestartTimer();
        }
        else if (change.Property == Page.InheritedSafeAreaProperty)
        {
            UpdateInsets();
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ActionInvokedEvent));
        var command = ActionCommand;
        var param = ActionCommandParameter;
        if (command is not null && command.CanExecute(param))
        {
            command.Execute(param);
        }
        SetCurrentValue(IsOpenProperty, false);
    }

    private void UpdateToneAndParts()
    {
        var tone = Tone;
        PseudoClasses.Set(":info", tone == BannerTone.Info);
        PseudoClasses.Set(":success", tone == BannerTone.Success);
        PseudoClasses.Set(":warning", tone == BannerTone.Warning);
        PseudoClasses.Set(":danger", tone == BannerTone.Danger);

        var icon = Icon ?? DefaultIconFor(tone);
        if (_iconGlyph is not null)
        {
            _iconGlyph.Data = icon;
        }
        if (_iconWrap is not null)
        {
            _iconWrap.IsVisible = icon is not null;
        }
        if (_titleBlock is not null)
        {
            _titleBlock.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_messageBlock is not null)
        {
            _messageBlock.IsVisible = !string.IsNullOrEmpty(Message);
        }
        if (_actionButton is not null)
        {
            _actionButton.IsVisible = !string.IsNullOrEmpty(ActionText);
        }
        if (_handle is not null)
        {
            _handle.IsVisible = IsDismissible;
        }
    }

    private static Geometry? DefaultIconFor(BannerTone tone) => tone switch
    {
        BannerTone.Info => ArcIcons.Info,
        BannerTone.Success => ArcIcons.Check,
        BannerTone.Warning => ArcIcons.TriangleAlert,
        BannerTone.Danger => ArcIcons.CircleAlert,
        _ => null,
    };

    private void UpdateInsets()
    {
        if (_host is null)
        {
            return;
        }
        var safe = Page.GetInheritedSafeArea(this);
        var side = HorizontalInset;
        _host.Margin = new Thickness(safe.Left + side, safe.Top + TopGap, safe.Right + side, 0);
    }

    private void UpdatePresence()
    {
        IsHitTestVisible = _present;
        if (_card is not null)
        {
            _card.IsVisible = _present;
        }
    }

    private void Open()
    {
        _present = true;
        UpdatePresence();
        UpdateInsets();
        TriggerToneHaptic();
        RestartTimer();
        if (_y is null)
        {
            return;
        }
        if (Reduced)
        {
            _y.Jump(0);
            Apply();
            return;
        }
        _y.Jump(HiddenOffset);
        _ = _y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
    }

    private void Close()
    {
        StopTimer();
        RaiseEvent(new RoutedEventArgs(DismissedEvent));
        if (_y is null)
        {
            _present = false;
            UpdatePresence();
            return;
        }
        if (Reduced)
        {
            _y.Jump(HiddenOffset);
            _present = false;
            UpdatePresence();
            Apply();
            return;
        }
        _ = _y.AnimateTo(HiddenOffset, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(
            t => Dispatcher.UIThread.Post(() =>
            {
                if (t.Result && !IsOpen)
                {
                    _present = false;
                    UpdatePresence();
                }
            }));
    }

    private void TriggerToneHaptic()
    {
        var kind = Tone switch
        {
            BannerTone.Success => HapticKind.Success,
            BannerTone.Warning => HapticKind.Warning,
            BannerTone.Danger => HapticKind.Error,
            _ => HapticKind.Light,
        };
        Haptics.Trigger(kind);
    }

    private void RestartTimer()
    {
        StopTimer();
        var duration = Duration;
        if (duration <= TimeSpan.Zero || !IsOpen)
        {
            return;
        }
        _timer = new DispatcherTimer { Interval = duration };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        StopTimer();
        if (IsOpen && !_dragging)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void BeginDrag()
    {
        if (_y is null)
        {
            return;
        }
        _dragging = true;
        StopTimer();
        _y.Stop();
    }

    private void MoveDrag(double deltaY)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        if (deltaY > 0)
        {
            _y.Jump(RubberBand.Apply(deltaY, DownStretch));
        }
        else
        {
            _y.Jump(Math.Max(HiddenOffset, deltaY));
        }
    }

    private void EndDrag(double velocityY)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        _dragging = false;
        if ((velocityY <= FlickUpVelocity || _y.Value <= -32) && IsDismissible)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        else
        {
            if (Reduced)
            {
                _y.Jump(0);
                Apply();
            }
            else
            {
                _ = _y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy), velocityY);
            }
            RestartTimer();
        }
    }

    private void Apply()
    {
        var y = _y?.Value ?? (IsOpen ? 0 : HiddenOffset);
        _translate.Y = y;
        if (_card is not null)
        {
            var ratio = y < 0 ? 1 - Math.Clamp(-y / Math.Abs(HiddenOffset), 0, 1) : 1;
            _card.Opacity = ratio;
        }
    }
}
