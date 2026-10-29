using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>The page transition a <see cref="ThemeSwitcher"/> plays (and the icon nudge that matches it).</summary>
public enum ThemeSwitcherVariant
{
    /// <summary>The next appearance spreads in a circle from the button.</summary>
    Reveal,

    /// <summary>The next appearance crosses the page like an eclipse (for showcase pages).</summary>
    Eclipse,

    /// <summary>The next appearance opens from a slim centre seam (independent of where the button is).</summary>
    Split,

    /// <summary>The next appearance rises into place from the bottom (the quietest).</summary>
    Rise
}

/// <summary>Light or dark appearance.</summary>
public enum ThemeSwitcherAppearance
{
    Light,
    Dark
}

/// <summary>Arguments of <see cref="ThemeSwitcher.ThemeChangeRequested"/>.</summary>
public sealed class ThemeSwitcherChangeRequestedEventArgs : RoutedEventArgs
{
    public ThemeSwitcherChangeRequestedEventArgs(RoutedEvent routedEvent, ThemeSwitcherAppearance next, ThemeSwitcherVariant variant,
                                                 Control trigger, Point origin) : base(routedEvent)
    {
        Next = next;
        Variant = variant;
        Trigger = trigger;
        Origin = origin;
    }

    /// <summary>The appearance to switch to.</summary>
    public ThemeSwitcherAppearance Next { get; }

    /// <summary>The page transition the switcher asks for.</summary>
    public ThemeSwitcherVariant Variant { get; }

    /// <summary>The button that was pressed.</summary>
    public Control Trigger { get; }

    /// <summary>Centre of the trigger in its top level's coordinates (the reveal origin).</summary>
    public Point Origin { get; }
}

/// <summary>
/// A single light / dark switch for a header: a secondary button that swaps sun and moon on a shared turn and names the
/// next appearance. Pressing it raises <see cref="ThemeChangeRequested"/>; unless a handler marks it handled, the
/// switcher applies the change through <see cref="ArcThemeManager"/> (changing the appearance and keeping the rest of the
/// current theme) inside the page transition of its <see cref="Variant"/>. Arc hands that transition to the View
/// Transitions API; here the old window is snapshotted into an overlay whose animated clip reveals the new theme
/// beneath (<see cref="RunTransitionAsync"/>, also usable from a handler). Reduced motion switches without a transition.
/// </summary>
[TemplatePart(ButtonPart, typeof(Control))]
[TemplatePart(IconPart, typeof(Control))]
[PseudoClasses(DarkPseudoClass)]
public class ThemeSwitcher : TemplatedControl
{
    public const string ButtonPart = "PART_Button";
    public const string IconPart = "PART_Icon";
    public const string DarkPseudoClass = ":dark";

    #region 公共属性定义

    /// <summary>The appearance shown; null follows the control's actual theme variant (default).</summary>
    public static readonly StyledProperty<ThemeSwitcherAppearance?> AppearanceProperty =
        AvaloniaProperty.Register<ThemeSwitcher, ThemeSwitcherAppearance?>(nameof(Appearance), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ThemeSwitcherVariant> VariantProperty =
        AvaloniaProperty.Register<ThemeSwitcher, ThemeSwitcherVariant>(nameof(Variant));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ThemeSwitcher, string?>(nameof(Label));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ThemeSwitcher, string>(nameof(Text), "Switch theme");

    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<ThemeSwitcher, bool>(nameof(IsIconOnly));

    public static readonly RoutedEvent<ThemeSwitcherChangeRequestedEventArgs> ThemeChangeRequestedEvent =
        RoutedEvent.Register<ThemeSwitcher, ThemeSwitcherChangeRequestedEventArgs>(nameof(ThemeChangeRequested), RoutingStrategies.Bubble);

    /// <summary>The appearance shown (icon, pressed state, name). Null follows the actual theme variant.</summary>
    public ThemeSwitcherAppearance? Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }

    /// <summary>Which page transition to play (default Reveal).</summary>
    public ThemeSwitcherVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>Accessible name; null says "Switch to dark mode" or "Switch to light mode".</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The visible text beside the icon (default "Switch theme").</summary>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Shows only the icon in a 38×38 square.</summary>
    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }

    /// <summary>
    /// Raised before the theme changes. Set <see cref="RoutedEventArgs.Handled"/> to apply the theme yourself (optionally
    /// inside <see cref="RunTransitionAsync"/>); otherwise the switcher applies it through <see cref="ArcThemeManager"/>.
    /// </summary>
    public event EventHandler<ThemeSwitcherChangeRequestedEventArgs>? ThemeChangeRequested
    {
        add => AddHandler(ThemeChangeRequestedEvent, value);
        remove => RemoveHandler(ThemeChangeRequestedEvent, value);
    }

    #endregion

    private ThemeSwitcherButton? _button;
    private ButtonsGlyphSwap? _icon;
    private ThemeSwitcherAppearance? _shown;
    private bool _settled;

    /// <summary>The appearance currently shown.</summary>
    public ThemeSwitcherAppearance EffectiveAppearance =>
        Appearance ?? (ActualThemeVariant == ThemeVariant.Dark ? ThemeSwitcherAppearance.Dark : ThemeSwitcherAppearance.Light);

    private ThemeSwitcherAppearance Next => EffectiveAppearance == ThemeSwitcherAppearance.Light ? ThemeSwitcherAppearance.Dark : ThemeSwitcherAppearance.Light;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_button is not null)
        {
            _button.Click -= OnButtonClick;
        }
        _button = e.NameScope.Find<ThemeSwitcherButton>(ButtonPart);
        _icon = e.NameScope.Find<ButtonsGlyphSwap>(IconPart);
        if (_button is not null)
        {
            _button.Click += OnButtonClick;
            _button.Owner = this;
        }
        _shown = null;
        Refresh();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // A stored preference usually lands just after the first frames: only changes after that animate.
        Dispatcher.UIThread.Post(() => _settled = IsLoaded, DispatcherPriority.Background);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _settled = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AppearanceProperty || change.Property == LabelProperty)
        {
            Refresh();
        }
    }

    public ThemeSwitcher()
    {
        ActualThemeVariantChanged += (_, _) =>
        {
            if (Appearance is null)
            {
                Refresh();
            }
        };
    }

    private void Refresh()
    {
        var appearance = EffectiveAppearance;
        PseudoClasses.Set(DarkPseudoClass, appearance == ThemeSwitcherAppearance.Dark);
        var name = Label ?? (appearance == ThemeSwitcherAppearance.Light ? "Switch to dark mode" : "Switch to light mode");
        if (_button is not null)
        {
            AutomationProperties.SetName(_button, name);
            _button.RaiseToggleChanged();
        }
        if (_icon is null || _shown == appearance)
        {
            return;
        }
        var previous = _shown;
        _shown = appearance;
        var glyph = new ArcGlyph
        {
            Data = appearance == ThemeSwitcherAppearance.Light ? ArcIcons.Sun : ArcIcons.Moon,
            Size = 16,
            StrokeThickness = 1.75
        };
        // Both icons turn the same way (clockwise into dark, back out of it), so the swap reads as one rotation.
        var enter = new ButtonsGlyphPose(Scale: 0.7, Rotate: AngleOf(appearance), Blur: 2);
        var exit = new ButtonsGlyphPose(Scale: 0.7, Rotate: previous is { } p ? AngleOf(p) : 0, Blur: 2);
        if (_settled && previous is not null)
        {
            _icon.Show(glyph, enter, exit);
        }
        else
        {
            _icon.Show(null, default, default);
            _icon.Show(glyph, default, default);
        }
    }

    private static double AngleOf(ThemeSwitcherAppearance appearance) => appearance == ThemeSwitcherAppearance.Light ? 30 : -30;

    /// <summary>Switches to the other appearance, as a press would.</summary>
    public void Toggle()
    {
        if (_button is not null)
        {
            _ = SwitchAsync(_button);
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control trigger)
        {
            _ = SwitchAsync(trigger);
        }
    }

    private async Task SwitchAsync(Control trigger)
    {
        var next = Next;
        var topLevel = TopLevel.GetTopLevel(trigger);
        var origin = topLevel is not null && trigger.TranslatePoint(new Point(trigger.Bounds.Width / 2, trigger.Bounds.Height / 2), topLevel) is { } p
            ? p
            : default;
        var args = new ThemeSwitcherChangeRequestedEventArgs(ThemeChangeRequestedEvent, next, Variant, trigger, origin);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        if (Appearance is not null)
        {
            SetCurrentValue(AppearanceProperty, next);
        }
        await RunTransitionAsync(trigger, Variant, () => ThemeSwitcherThemeApplier.ApplyAsync(this, next == ThemeSwitcherAppearance.Dark));
    }

    /// <summary>
    /// Runs <paramref name="applyTheme"/> inside the page transition of <paramref name="variant"/>: the window is
    /// snapshotted into its overlay layer, the theme is applied underneath, and the snapshot's clip opens to reveal it
    /// (Reveal grows a circle from <paramref name="trigger"/>). Reduced motion, Off, or no overlay layer apply directly.
    /// </summary>
    public static Task RunTransitionAsync(Visual trigger, ThemeSwitcherVariant variant, Func<Task> applyTheme)
        => ThemeSwitcherTransition.RunAsync(trigger, variant, applyTheme);

    /// <summary>The inner Arc button: aria-pressed (Toggle state on in dark), Arc Button's theme.</summary>
    internal sealed class ThemeSwitcherButtonPeer : ButtonAutomationPeer, IToggleProvider
    {
        public ThemeSwitcherButtonPeer(ThemeSwitcherButton owner) : base(owner)
        {
        }

        public ToggleState ToggleState => ((ThemeSwitcherButton)Owner).Owner?.EffectiveAppearance == ThemeSwitcherAppearance.Dark
            ? ToggleState.On
            : ToggleState.Off;

        public void Toggle() => ((ThemeSwitcherButton)Owner).Owner?.Toggle();
    }
}

/// <summary>The switcher's button: an Arc secondary button (same theme) with a toggle automation peer.</summary>
internal sealed class ThemeSwitcherButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public ThemeSwitcher? Owner { get; set; }

    private ToggleState _lastState;

    internal void RaiseToggleChanged()
    {
        if (ControlAutomationPeer.FromElement(this) is ThemeSwitcher.ThemeSwitcherButtonPeer peer)
        {
            var state = peer.ToggleState;
            if (state != _lastState)
            {
                peer.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, _lastState, state);
                _lastState = state;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ThemeSwitcher.ThemeSwitcherButtonPeer(this);
}
