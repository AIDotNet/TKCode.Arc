using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The call bar of a voice conversation (Arc Voice orb, design reference): a pill that shows one "Start voice chat"
/// button before a session and, during one, a mute toggle, the elapsed time and a red end button. The pill's width
/// springs between the two and the groups cross with a short scale-and-blur swap.
/// </summary>
/// <remarks>
/// Starting moves the keyboard focus to the mute button and ending moves it back to Start. The bar keeps
/// <see cref="IsActive"/> and <see cref="IsMuted"/> itself when the user presses its buttons (bind them two-way) and
/// raises <see cref="StartRequested"/>, <see cref="MuteChanged"/> and <see cref="EndRequested"/>; push
/// <see cref="Elapsed"/> from your session clock.
/// </remarks>
[TemplatePart(StartPart, typeof(Button))]
[TemplatePart(MutePart, typeof(ToggleButton))]
[TemplatePart(EndPart, typeof(Button))]
[TemplatePart(ClockPart, typeof(TextBlock))]
[TemplatePart(MuteGlyphPart, typeof(ArcContentSwap))]
[PseudoClasses(ActivePseudoClass, MutedPseudoClass)]
public class VoiceControls : TemplatedControl
{
    public const string StartPart = "PART_Start";
    public const string MutePart = "PART_Mute";
    public const string EndPart = "PART_End";
    public const string ClockPart = "PART_Clock";
    public const string MuteGlyphPart = "PART_MuteGlyph";
    public const string ActivePseudoClass = ":active";
    public const string MutedPseudoClass = ":muted";

    /// <summary>Whether a session is running: the bar shows mute, the timer and end instead of Start.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<VoiceControls, bool>(nameof(IsActive), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether the microphone is muted (the mute button shows pressed).</summary>
    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VoiceControls, bool>(nameof(IsMuted), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The session's elapsed time, shown as m:ss.</summary>
    public static readonly StyledProperty<TimeSpan> ElapsedProperty =
        AvaloniaProperty.Register<VoiceControls, TimeSpan>(nameof(Elapsed));

    /// <summary>The bar's text and accessible names (English defaults when null).</summary>
    public static readonly StyledProperty<VoiceControlsLabels?> LabelsProperty =
        AvaloniaProperty.Register<VoiceControls, VoiceControlsLabels?>(nameof(Labels));

    /// <summary>Raised when the user presses Start (or <see cref="Start"/> is called); <see cref="IsActive"/> is already true.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StartRequestedEvent =
        RoutedEvent.Register<VoiceControls, RoutedEventArgs>(nameof(StartRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the user presses End (or <see cref="End"/> is called); <see cref="IsActive"/> is already false.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> EndRequestedEvent =
        RoutedEvent.Register<VoiceControls, RoutedEventArgs>(nameof(EndRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the user toggles the microphone (or <see cref="IsMuted"/> changes).</summary>
    public static readonly RoutedEvent<VoiceControlsMuteChangedEventArgs> MuteChangedEvent =
        RoutedEvent.Register<VoiceControls, VoiceControlsMuteChangedEventArgs>(nameof(MuteChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<string> StartTextProperty =
        AvaloniaProperty.Register<VoiceControls, string>(nameof(StartText), "Start voice chat");

    internal static readonly StyledProperty<string> ClockTextProperty =
        AvaloniaProperty.Register<VoiceControls, string>(nameof(ClockText), "0:00");

    private static readonly VoiceControlsLabels s_defaultLabels = new();

    private Button? _start;
    private ToggleButton? _mute;
    private Button? _end;
    private TextBlock? _clock;
    private ArcContentSwap? _muteGlyph;
    private bool _syncingMute;

    static VoiceControls()
    {
        ClipToBoundsProperty.OverrideDefaultValue<VoiceControls>(false);
        FocusableProperty.OverrideDefaultValue<VoiceControls>(false);
    }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <inheritdoc cref="IsMutedProperty"/>
    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan Elapsed
    {
        get => GetValue(ElapsedProperty);
        set => SetValue(ElapsedProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public VoiceControlsLabels? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="StartRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? StartRequested
    {
        add => AddHandler(StartRequestedEvent, value);
        remove => RemoveHandler(StartRequestedEvent, value);
    }

    /// <inheritdoc cref="EndRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? EndRequested
    {
        add => AddHandler(EndRequestedEvent, value);
        remove => RemoveHandler(EndRequestedEvent, value);
    }

    /// <inheritdoc cref="MuteChangedEvent"/>
    public event EventHandler<VoiceControlsMuteChangedEventArgs>? MuteChanged
    {
        add => AddHandler(MuteChangedEvent, value);
        remove => RemoveHandler(MuteChangedEvent, value);
    }

    internal string StartText
    {
        get => GetValue(StartTextProperty);
        set => SetValue(StartTextProperty, value);
    }

    internal string ClockText
    {
        get => GetValue(ClockTextProperty);
        set => SetValue(ClockTextProperty, value);
    }

    internal Button? StartButton => _start;

    internal ToggleButton? MuteButton => _mute;

    internal Button? EndButton => _end;

    internal TextBlock? Clock => _clock;

    private VoiceControlsLabels EffectiveLabels => Labels ?? s_defaultLabels;

    /// <summary>Starts a session: shows the live controls and raises <see cref="StartRequested"/>.</summary>
    public void Start()
    {
        if (IsActive)
        {
            return;
        }
        SetCurrentValue(IsActiveProperty, true);
        RaiseEvent(new RoutedEventArgs(StartRequestedEvent, this));
    }

    /// <summary>Ends the session: shows Start again and raises <see cref="EndRequested"/>.</summary>
    public void End()
    {
        if (!IsActive)
        {
            return;
        }
        SetCurrentValue(IsActiveProperty, false);
        RaiseEvent(new RoutedEventArgs(EndRequestedEvent, this));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_start is not null)
        {
            _start.Click -= OnStartClick;
        }
        if (_end is not null)
        {
            _end.Click -= OnEndClick;
        }
        if (_mute is not null)
        {
            _mute.IsCheckedChanged -= OnMuteToggled;
        }
        _start = e.NameScope.Find<Button>(StartPart);
        _mute = e.NameScope.Find<ToggleButton>(MutePart);
        _end = e.NameScope.Find<Button>(EndPart);
        _clock = e.NameScope.Find<TextBlock>(ClockPart);
        _muteGlyph = e.NameScope.Find<ArcContentSwap>(MuteGlyphPart);
        if (_start is not null)
        {
            _start.Click += OnStartClick;
        }
        if (_end is not null)
        {
            _end.Click += OnEndClick;
        }
        if (_mute is not null)
        {
            _mute.IsChecked = IsMuted;
            _mute.IsCheckedChanged += OnMuteToggled;
        }
        if (_clock is not null)
        {
            _clock.FontFeatures = new Avalonia.Media.FontFeatureCollection { new Avalonia.Media.FontFeature { Tag = "tnum", Value = 1 } };
        }
        UpdateTexts();
        UpdateMuteGlyph(false);
        UpdatePseudoClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsActiveProperty)
        {
            UpdatePseudoClasses();
            MoveFocus(change.GetNewValue<bool>());
        }
        else if (property == IsMutedProperty)
        {
            UpdatePseudoClasses();
            if (_mute is not null && _mute.IsChecked != IsMuted)
            {
                _syncingMute = true;
                _mute.IsChecked = IsMuted;
                _syncingMute = false;
            }
            UpdateMuteGlyph(true);
            UpdateTexts();
            RaiseEvent(new VoiceControlsMuteChangedEventArgs(MuteChangedEvent, IsMuted));
        }
        else if (property == ElapsedProperty || property == LabelsProperty)
        {
            UpdateTexts();
        }
    }

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Start();
    }

    private void OnEndClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        End();
    }

    private void OnMuteToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncingMute || _mute is null)
        {
            return;
        }
        SetCurrentValue(IsMutedProperty, _mute.IsChecked == true);
    }

    private void MoveFocus(bool active)
    {
        // Starting moves the focus to Mute, ending moves it back to Start, but only when the focus was in the bar.
        var from = active ? (Control?)_start : _end;
        var to = active ? (Control?)_mute : _start;
        if (from is null || to is null || !from.IsFocused)
        {
            return;
        }
        var keyboard = from.Classes.Contains(":focus-visible");
        // The target's group turns visible when the template's pill sees the new state, after this handler.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (IsActive == active)
            {
                to.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
            }
        }, Avalonia.Threading.DispatcherPriority.Input);
    }

    private void UpdateTexts()
    {
        var labels = EffectiveLabels;
        StartText = labels.Start;
        var elapsed = Elapsed < TimeSpan.Zero ? TimeSpan.Zero : Elapsed;
        ClockText = VoiceControlsLabels.FormatClock(elapsed);
        if (_start is not null)
        {
            AutomationProperties.SetName(_start, labels.Start);
        }
        if (_mute is not null)
        {
            AutomationProperties.SetName(_mute, IsMuted ? labels.Unmute : labels.Mute);
        }
        if (_end is not null)
        {
            AutomationProperties.SetName(_end, labels.End);
        }
        if (_clock is not null)
        {
            var total = (int)Math.Floor(elapsed.TotalSeconds);
            AutomationProperties.SetName(_clock, labels.ClockName(total / 60, total % 60));
        }
    }

    private void UpdateMuteGlyph(bool animate)
    {
        if (_muteGlyph is null)
        {
            return;
        }
        var glyph = new ArcGlyph { Data = IsMuted ? VoiceControlsIcons.MicOff : ArcIcons.Mic, StrokeThickness = 1.75 };
        glyph.Bind(ArcGlyph.SizeProperty, _muteGlyph.GetResourceObservable(VoiceControlsTokens.Key(VoiceControlsTokenKind.GlyphSize)));
        if (!animate)
        {
            var mode = _muteGlyph.Mode;
            _muteGlyph.Mode = ContentSwapMode.None;
            _muteGlyph.Content = glyph;
            _muteGlyph.Mode = mode;
            return;
        }
        _muteGlyph.Content = glyph;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(ActivePseudoClass, IsActive);
        PseudoClasses.Set(MutedPseudoClass, IsMuted);
    }
}

/// <summary>Lucide glyphs the voice pill needs beyond Core's set (ISC license, Lucide 1.59.0).</summary>
internal static class VoiceControlsIcons
{
    // Lucide "mic-off" (ISC).
    public static Avalonia.Media.Geometry MicOff => ArcIcons.Parse(
        "M12 19v3 M15 9.34V5a3 3 0 0 0-5.68-1.33 M16.95 16.95A7 7 0 0 1 5 12v-2 M18.89 13.23A7 7 0 0 0 19 12v-2 M2 2l20 20 M9 9v3a3 3 0 0 0 5.12 2.12");

    // Lucide "phone-off" (ISC).
    public static Avalonia.Media.Geometry PhoneOff => ArcIcons.Parse(
        "M10.1 13.9a14 14 0 0 0 3.732 2.668 1 1 0 0 0 1.213-.303l.355-.465A2 2 0 0 1 17 15h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2 18 18 0 0 1-12.728-5.272 M22 2 2 22 M4.76 13.582A18 18 0 0 1 2 4a2 2 0 0 1 2-2h3a2 2 0 0 1 2 2v3a2 2 0 0 1-.8 1.6l-.468.351a1 1 0 0 0-.292 1.233 14 14 0 0 0 .244.473");
}
