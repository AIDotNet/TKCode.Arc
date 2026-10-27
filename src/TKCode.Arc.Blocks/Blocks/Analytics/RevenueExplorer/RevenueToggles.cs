using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Shared behaviour of the block's two small self-drawn toggles (a series chip and the compare switch): hover and focus
/// state, Enter / Space and click, the toggle automation peer, a palette that follows the theme, and one small frame loop for
/// the 160 / 240 / 580ms mixes (colours, the swatch scale, the thumb).
/// </summary>
/// <remarks>Why code-driven: the swatch scale and the thumb move on the site's 580ms spring curve inside one drawing, with colour mixes read in the same Render.</remarks>
internal abstract class RevenueToggleBase : Control
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<RevenueToggleBase, string?>(nameof(Text));
    public static readonly StyledProperty<bool> IsOnProperty = AvaloniaProperty.Register<RevenueToggleBase, bool>(nameof(IsOn), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> IsCompactProperty = AvaloniaProperty.Register<RevenueToggleBase, bool>(nameof(IsCompact));

    protected readonly AnalyticsPalette Palette = new();
    protected readonly AnalyticsTextCache TextCache = new(16);
    private readonly LiveLoop _loop;
    private int _paletteVersion = -1;
    private bool _hover;

    /// <summary>Hover mix (colour / border, 160ms).</summary>
    protected AnalyticsTween HoverMix = new(0);

    static RevenueToggleBase()
    {
        FocusableProperty.OverrideDefaultValue<RevenueToggleBase>(true);
        AffectsMeasure<RevenueToggleBase>(TextProperty, IsCompactProperty);
        AffectsRender<RevenueToggleBase>(TextProperty, IsOnProperty, IsCompactProperty);
    }

    protected RevenueToggleBase()
    {
        // Function as well as decoration: also lands the mixes under reduced motion.
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        Cursor = new Cursor(StandardCursorType.Hand);
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
    }

    /// <summary>Raised when the person (not code) turns the toggle.</summary>
    public event Action<bool>? Toggled;

    /// <summary>The visible text.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Whether it is on.</summary>
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    /// <summary>Compact sizes (a block under 440 wide).</summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>The pointer is over it.</summary>
    protected bool IsHover => _hover;

    /// <summary>True while a mix runs (tests).</summary>
    internal bool IsMoving => _loop.IsActive;

    protected ArcMotionLevel Level => this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    protected bool IsDark => ActualThemeVariant == ThemeVariant.Dark;

    protected void EnsurePalette()
    {
        if (!Palette.IsReady || _paletteVersion < 0)
        {
            Palette.Refresh(this);
            _paletteVersion = Palette.Version;
        }
    }

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        TextCache.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Starts a mix: a tween at Full motion, a jump otherwise.</summary>
    protected void Mix(ref AnalyticsTween tween, double target, TimeSpan duration, Avalonia.Animation.Easings.Easing easing)
    {
        if (Level == ArcMotionLevel.Full)
        {
            tween.Start(target, duration, easing);
            _loop.Start();
        }
        else
        {
            tween.Jump(target);
        }
        InvalidateVisual();
    }

    protected abstract bool AdvanceMixes(double dt);

    private bool Advance(double dt)
    {
        var moving = HoverMix.Advance(dt);
        moving |= AdvanceMixes(dt);
        InvalidateVisual();
        return moving;
    }

    protected void StartLoop() => _loop.Start();

    /// <summary>Applies a change of <see cref="IsOn"/> to the mixes (animated).</summary>
    protected abstract void OnIsOnChanged(bool animate);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty)
        {
            OnIsOnChanged(animate: this.IsAttachedToVisualTree());
            (ControlAutomationPeer.FromElement(this) as RevenueTogglePeer)?.RaiseToggleChanged(IsOn);
            InvalidateVisual();
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        OnIsOnChanged(animate: false);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        Mix(ref HoverMix, 1, TimeSpan.FromMilliseconds(160), AnalyticsEasings.Standard);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        Mix(ref HoverMix, 0, TimeSpan.FromMilliseconds(160), AnalyticsEasings.Standard);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Invoke();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
        {
            Invoke();
            e.Handled = true;
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    /// <summary>Turns it as a person would (click, key, screen reader).</summary>
    public void Invoke()
    {
        IsOn = !IsOn;
        Toggled?.Invoke(IsOn);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RevenueTogglePeer(this);
}

/// <summary>A toggle's peer: a toggle button (<c>aria-pressed</c> / <c>role=switch</c>).</summary>
internal sealed class RevenueTogglePeer(RevenueToggleBase owner) : ControlAutomationPeer(owner), IToggleProvider
{
    private RevenueToggleBase Toggle => (RevenueToggleBase)Owner;

    public ToggleState ToggleState => Toggle.IsOn ? ToggleState.On : ToggleState.Off;

    public void Toggle_() => Toggle.Invoke();

    void IToggleProvider.Toggle() => Toggle.Invoke();

    public void RaiseToggleChanged(bool on) =>
        RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, on ? ToggleState.Off : ToggleState.On,
            on ? ToggleState.On : ToggleState.Off);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore() => AutomationProperties.GetName(Toggle) is { Length: > 0 } name ? name : Toggle.Text;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
}

/// <summary>
/// A series chip (<c>New</c>, <c>Expansion</c>, <c>Churn</c>): 32 high pill with a 1px border, a 10px swatch and the name.
/// On: the swatch is the series colour and the text Foreground; off: the swatch is an empty ring at 0.8 scale and the text
/// TextMuted. Hover darkens the border (160ms), the swatch fill fades in 240ms and its scale springs on 580ms.
/// </summary>
internal sealed class RevenueSeriesChip : RevenueToggleBase
{
    public static readonly StyledProperty<RevenueSeries> SeriesProperty = AvaloniaProperty.Register<RevenueSeriesChip, RevenueSeries>(nameof(Series));

    private AnalyticsTween _on = new(1);
    private AnalyticsTween _scale = new(1);

    /// <summary>Which series the swatch shows.</summary>
    public RevenueSeries Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }

    /// <summary>The swatch fill mix (tests).</summary>
    internal double FillMix => _on.Value;

    /// <summary>The swatch scale (tests).</summary>
    internal double SwatchScale => _scale.Value;

    static RevenueSeriesChip()
    {
        AffectsRender<RevenueSeriesChip>(SeriesProperty);
    }

    protected override void OnIsOnChanged(bool animate)
    {
        var target = IsOn ? 1.0 : 0.0;
        if (!animate)
        {
            _on.Jump(target);
            _scale.Jump(IsOn ? 1 : 0.8);
            return;
        }
        Mix(ref _on, target, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
        Mix(ref _scale, IsOn ? 1 : 0.8, TimeSpan.FromMilliseconds(580), AnalyticsEasings.CssSpring);
    }

    protected override bool AdvanceMixes(double dt)
    {
        var moving = _on.Advance(dt);
        moving |= _scale.Advance(dt);
        return moving;
    }

    private double FontSize => IsCompact ? 12 : 14;

    private double Height_ => IsCompact ? 30 : 32;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        var body = new Typeface(Palette.BodyFont);
        var text = TextCache.Get(Text ?? string.Empty, body, FontSize);
        var left = IsCompact ? 8 : 10;
        var right = IsCompact ? 10 : 12;
        return new Size(Math.Ceiling(left + 10 + 8 + text.Width + right + 2), Height_);
    }

    private Color SwatchColor => Series switch
    {
        RevenueSeries.New => Palette.Accent,
        RevenueSeries.Expansion => AnalyticsColors.Mix(Palette.Accent, 0.55, Palette.Surface),
        _ => Palette.Danger
    };

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        var size = Bounds.Size;
        var rect = new Rect(size);
        context.FillRectangle(Brushes.Transparent, rect);
        var body = new Typeface(Palette.BodyFont);
        var radius = size.Height / 2;
        var border = AnalyticsColors.Lerp(Palette.Border, Palette.BorderStrong, HoverMix.Value);
        context.DrawRectangle(null, Palette.Pen(border, 1, cap: PenLineCap.Flat), new RoundedRect(rect.Deflate(0.5), radius));
        var left = IsCompact ? 8 : 10;
        var swatch = new Rect(left + 1, (size.Height - 10) / 2, 10, 10);
        var color = SwatchColor;
        var c = swatch.Center;
        using (context.PushTransform(Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(_scale.Value, _scale.Value) * Matrix.CreateTranslation(c.X, c.Y)))
        {
            if (_on.Value > 0.002)
            {
                context.DrawRectangle(Palette.Brush(color, Math.Clamp(_on.Value, 0, 1)), null, new RoundedRect(swatch, 3));
            }
            // The 1.5px inset ring of the same colour stays in both states.
            context.DrawRectangle(null, Palette.Pen(color, 1.5, cap: PenLineCap.Flat), new RoundedRect(swatch.Deflate(0.75), 2.25));
        }
        var fore = AnalyticsColors.Lerp(Palette.TextMuted, Palette.Foreground, _on.Value);
        var text = TextCache.Get(Text ?? string.Empty, body, FontSize);
        text.Draw(context, Palette.Brush(fore), new Point(swatch.Right + 8 - 1, (size.Height - text.Height) / 2));
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, Palette, rect, radius);
        }
    }
}

/// <summary>
/// The compare switch: the whole 32px line is the button (a 30×18 track with a 14px thumb and the text); the thumb slides
/// 12px on a Quick spring, the track mixes between its off and on colours in 160ms, and hover brightens the text.
/// </summary>
internal sealed class RevenueCompareSwitch : RevenueToggleBase
{
    private AnalyticsSpringChannel _thumb = new(0, 0.002, 0.02);
    private AnalyticsTween _track = new(0);

    /// <summary>The thumb's offset in px (0–12; tests).</summary>
    internal double ThumbOffset => _thumb.Value;

    protected override void OnIsOnChanged(bool animate)
    {
        var target = IsOn ? 12.0 : 0.0;
        if (!animate || Level != ArcMotionLevel.Full)
        {
            _thumb.Jump(target);
            _track.Jump(IsOn ? 1 : 0);
            InvalidateVisual();
            return;
        }
        // The thumb is a spring (Quick); the track colour a 160ms mix.
        _thumb.SpringTo(target, AnalyticsSprings.Quick);
        Mix(ref _track, IsOn ? 1 : 0, TimeSpan.FromMilliseconds(160), AnalyticsEasings.Standard);
        StartLoop();
    }

    protected override bool AdvanceMixes(double dt)
    {
        var moving = _thumb.Step(dt);
        moving |= _track.Advance(dt);
        return moving;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        var text = TextCache.Get(Text ?? string.Empty, new Typeface(Palette.BodyFont), 14);
        return new Size(Math.Ceiling(4 + 30 + 8 + text.Width + 4 + 1), 32);
    }

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        var size = Bounds.Size;
        var rect = new Rect(size);
        context.FillRectangle(Brushes.Transparent, rect);
        var off = AnalyticsColors.Mix(Palette.Foreground, 0.13, Palette.Surface);
        var track = new Rect(4, (size.Height - 18) / 2, 30, 18);
        var color = AnalyticsColors.Lerp(off, Palette.AccentStrong, _track.Value);
        context.DrawRectangle(Palette.Brush(color), null, new RoundedRect(track, 9));
        var thumb = new Rect(track.X + 2 + _thumb.Value, track.Y + 2, 14, 14);
        var thumbColor = IsOn || !IsDark ? Palette.Background : Palette.Foreground;
        var shadow = IsDark
            ? new BoxShadows(new BoxShadow { Blur = 0, Spread = 0.5, Color = Color.FromArgb(77, 0, 0, 0) },
                [new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(89, 0, 0, 0) },
                 new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(51, 0, 0, 0) }])
            : new BoxShadows(new BoxShadow { Blur = 0, Spread = 0.5, Color = Color.FromArgb(18, 0, 0, 0) },
                [new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(36, 0, 0, 0) },
                 new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(15, 0, 0, 0) }]);
        context.DrawRectangle(Palette.Brush(thumbColor), null, new RoundedRect(thumb, 7), shadow);
        var fore = AnalyticsColors.Lerp(Palette.TextSecondary, Palette.Foreground, HoverMix.Value);
        var text = TextCache.Get(Text ?? string.Empty, new Typeface(Palette.BodyFont), 14);
        text.Draw(context, Palette.Brush(fore), new Point(track.Right + 8, (size.Height - text.Height) / 2));
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, Palette, rect, size.Height / 2);
        }
    }
}
