using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How a <see cref="SandText"/> is set off by the person reading it.</summary>
public enum SandTextTrigger
{
    /// <summary>Only <see cref="SandText.IsDissolved"/> and the methods change it.</summary>
    None,

    /// <summary>A click, a tap, Enter or Space blows the text away, and the next one gathers it back.</summary>
    Click,

    /// <summary>The text blows away while the pointer is over it and gathers when it leaves.</summary>
    Hover
}

/// <summary>The way the wind blows the grains of a <see cref="SandText"/>.</summary>
public enum SandTextDrift
{
    /// <summary>Grains blow to the right; the left edge crumbles first.</summary>
    Right,

    /// <summary>Grains blow to the left; the right edge crumbles first.</summary>
    Left,

    /// <summary>Grains rise; the top crumbles first.</summary>
    Up
}

/// <summary>
/// A line of text that crumbles into grains of sand and drifts away on the wind, then gathers back grain by grain into
/// the same letters.
/// </summary>
/// <remarks>
/// At rest the text is drawn as ordinary text. While it moves, a front sweeps across from the windward side: behind it
/// the letters are grains drawn by this one control, each lifting, fluttering and fading on its own; ahead of it the
/// letters are still text. Gathering runs the same paths backwards, so turning around half way is seamless. Under
/// reduced motion the text only fades out and in.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(DissolvedPseudoClass)]
public class SandText : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string DissolvedPseudoClass = ":dissolved";

    private const double ReducedSeconds = 0.24;
    private const int ToneLevels = 8;

    /// <summary>The text to show.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SandText, string?>(nameof(Text));

    /// <summary>Whether the text has blown away (true) or stands gathered (false). Two-way.</summary>
    public static readonly StyledProperty<bool> IsDissolvedProperty =
        AvaloniaProperty.Register<SandText, bool>(nameof(IsDissolved), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>What sets the text off: a click (the default), hovering, or nothing but code.</summary>
    public static readonly StyledProperty<SandTextTrigger> TriggerProperty =
        AvaloniaProperty.Register<SandText, SandTextTrigger>(nameof(Trigger), SandTextTrigger.Click);

    /// <summary>The way the wind blows the grains (right by default).</summary>
    public static readonly StyledProperty<SandTextDrift> DriftProperty =
        AvaloniaProperty.Register<SandText, SandTextDrift>(nameof(Drift));

    /// <summary>How long the text takes to blow away completely, and to gather again.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<SandText, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(1.6));

    /// <summary>The side of one grain in px; long or large text uses larger grains to stay within <see cref="MaxGrains"/>.</summary>
    public static readonly StyledProperty<double> GrainSizeProperty =
        AvaloniaProperty.Register<SandText, double>(nameof(GrainSize), 2);

    /// <summary>How far a typical grain travels before it has faded, in px.</summary>
    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<SandText, double>(nameof(Distance), 110);

    /// <summary>The most grains the text is broken into.</summary>
    public static readonly StyledProperty<int> MaxGrainsProperty =
        AvaloniaProperty.Register<SandText, int>(nameof(MaxGrains), 4000);

    /// <summary>Raised when the last grain has blown away.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DissolvedEvent =
        RoutedEvent.Register<SandText, RoutedEventArgs>(nameof(Dissolved), RoutingStrategies.Bubble);

    /// <summary>Raised when the last grain is back in its letter.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> GatheredEvent =
        RoutedEvent.Register<SandText, RoutedEventArgs>(nameof(Gathered), RoutingStrategies.Bubble);

    private readonly SandTextGrains _grains = new();
    private readonly LiveLoop _loop;
    private readonly IImmutableBrush[] _tones = new IImmutableBrush[ToneLevels];
    private LazyFocusRing? _focusRing;
    private TextLayout? _layout;
    private bool _grainsDirty = true;
    private bool _shown;
    private double _progress;

    static SandText()
    {
        AffectsMeasure<SandText>(TextProperty, FontSizeProperty, FontFamilyProperty, FontWeightProperty, FontStyleProperty);
        ClipToBoundsProperty.OverrideDefaultValue<SandText>(false);
    }

    public SandText()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced, ViewportMargin = 160 };
        BuildTones(Colors.Gray);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="IsDissolvedProperty"/>
    public bool IsDissolved
    {
        get => GetValue(IsDissolvedProperty);
        set => SetValue(IsDissolvedProperty, value);
    }

    /// <inheritdoc cref="TriggerProperty"/>
    public SandTextTrigger Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <inheritdoc cref="DriftProperty"/>
    public SandTextDrift Drift
    {
        get => GetValue(DriftProperty);
        set => SetValue(DriftProperty, value);
    }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <inheritdoc cref="GrainSizeProperty"/>
    public double GrainSize
    {
        get => GetValue(GrainSizeProperty);
        set => SetValue(GrainSizeProperty, value);
    }

    /// <inheritdoc cref="DistanceProperty"/>
    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    /// <inheritdoc cref="MaxGrainsProperty"/>
    public int MaxGrains
    {
        get => GetValue(MaxGrainsProperty);
        set => SetValue(MaxGrainsProperty, value);
    }

    /// <inheritdoc cref="DissolvedEvent"/>
    public event EventHandler<RoutedEventArgs>? Dissolved
    {
        add => AddHandler(DissolvedEvent, value);
        remove => RemoveHandler(DissolvedEvent, value);
    }

    /// <inheritdoc cref="GatheredEvent"/>
    public event EventHandler<RoutedEventArgs>? Gathered
    {
        add => AddHandler(GatheredEvent, value);
        remove => RemoveHandler(GatheredEvent, value);
    }

    /// <summary>How far the text has blown away: 0 gathered, 1 gone.</summary>
    public double Progress => _progress;

    internal SandTextGrains Grains
    {
        get
        {
            EnsureGrains();
            return _grains;
        }
    }

    /// <summary>Blows the text away.</summary>
    public void Dissolve() => SetCurrentValue(IsDissolvedProperty, true);

    /// <summary>Gathers the grains back into the text.</summary>
    public void Gather() => SetCurrentValue(IsDissolvedProperty, false);

    /// <summary>Blows the text away when it stands, gathers it when it has gone (or is going).</summary>
    public void Toggle() => SetCurrentValue(IsDissolvedProperty, !IsDissolved);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, SandTextTokens.Key(SandTextTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep8));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateFocusable();
        UpdateName();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_shown)
        {
            // The first state shows as it is, with no animation.
            _shown = true;
            _progress = IsDissolved ? 1 : 0;
        }
        if (_progress != Target)
        {
            _loop.Start();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsDissolvedProperty)
        {
            PseudoClasses.Set(DissolvedPseudoClass, IsDissolved);
            StartMoving();
        }
        else if (property == TextProperty)
        {
            ResetText();
            UpdateName();
        }
        else if (property == FontSizeProperty || property == FontFamilyProperty || property == FontWeightProperty
                 || property == FontStyleProperty)
        {
            ResetText();
        }
        else if (property == ForegroundProperty)
        {
            BuildTones(Foreground is ISolidColorBrush solid ? solid.Color : Colors.Gray);
            ResetText();
        }
        else if (property == DriftProperty || property == GrainSizeProperty || property == DistanceProperty
                 || property == MaxGrainsProperty)
        {
            _grainsDirty = true;
            InvalidateVisual();
        }
        else if (property == TriggerProperty)
        {
            UpdateFocusable();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            StartMoving();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        var layout = EnsureLayout();
        return layout is null ? default : new Size(layout.WidthIncludingTrailingWhitespace, layout.Height);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Trigger == SandTextTrigger.None)
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Toggle();
        }
        else if (e.Key == Key.Escape && IsDissolved)
        {
            e.Handled = true;
            Gather();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (Trigger == SandTextTrigger.Hover && e.Pointer.Type == PointerType.Mouse)
        {
            Dissolve();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (Trigger == SandTextTrigger.Hover && e.Pointer.Type == PointerType.Mouse)
        {
            Gather();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // Touch has no hover, so a tap toggles in both modes.
        if (Trigger == SandTextTrigger.Click || (Trigger == SandTextTrigger.Hover && e.Pointer.Type != PointerType.Mouse))
        {
            e.Handled = true;
            Focus(NavigationMethod.Pointer);
            Toggle();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SandTextAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        // The whole box takes the pointer, not only the letters.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_layout is not { } layout)
        {
            return;
        }
        var progress = _progress;
        if (progress <= 0)
        {
            layout.Draw(context, default);
            return;
        }
        if (progress >= 1)
        {
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            using (context.PushOpacity(1 - progress))
            {
                layout.Draw(context, default);
            }
            return;
        }
        EnsureGrains();
        var grains = _grains;
        var t = progress * SandTextGrains.Span;
        var front = SandTextGrains.Front(t);
        if (front < 1 || grains.Count == 0)
        {
            // Ahead of the front the letters are still text.
            using (context.PushClip(StandingRegion(front)))
            {
                layout.Draw(context, default);
            }
        }
        for (var i = 0; i < grains.Count; i++)
        {
            if (grains.Order(i) > front)
            {
                continue;
            }
            grains.Place(i, t, out var x, out var y, out var opacity, out var size);
            var level = (int)Math.Round(opacity * grains.Tone(i) * (ToneLevels - 1));
            if (level <= 0)
            {
                continue;
            }
            context.FillRectangle(_tones[level], new Rect(x, y, size, size));
        }
    }

    private double Target => IsDissolved ? 1 : 0;

    /// <summary>The part of the box ahead of the front, where the letters have not let go yet.</summary>
    private Rect StandingRegion(double front)
    {
        var box = _grains.Bounds.Inflate(_grains.Size * 2).Union(new Rect(Bounds.Size));
        var ink = _grains.Bounds;
        front = Math.Clamp(front, 0, 1);
        return Drift switch
        {
            SandTextDrift.Left => new Rect(box.Left, box.Top, Math.Max(0, ink.Left + ink.Width * (1 - front) - box.Left), box.Height),
            SandTextDrift.Up => new Rect(box.Left, ink.Top + ink.Height * front, box.Width, Math.Max(0, box.Bottom - (ink.Top + ink.Height * front))),
            _ => new Rect(ink.Left + ink.Width * front, box.Top, Math.Max(0, box.Right - (ink.Left + ink.Width * front)), box.Height)
        };
    }

    private void StartMoving()
    {
        if (!_shown)
        {
            return;
        }
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || VisualRoot is null)
        {
            Settle();
            return;
        }
        if (_progress != Target)
        {
            _loop.Start();
        }
    }

    private bool Step(double dt)
    {
        var target = Target;
        var seconds = ArcMotion.IsFull(this) ? Math.Max(0.05, Duration.TotalSeconds) : ReducedSeconds;
        var delta = dt / seconds;
        _progress = target > _progress ? Math.Min(target, _progress + delta) : Math.Max(target, _progress - delta);
        InvalidateVisual();
        if (_progress != target)
        {
            return true;
        }
        RaiseArrived();
        return false;
    }

    private void Settle()
    {
        _loop.Stop();
        var arrived = _progress != Target;
        _progress = Target;
        InvalidateVisual();
        if (arrived)
        {
            RaiseArrived();
        }
    }

    private void RaiseArrived()
    {
        RaiseEvent(new RoutedEventArgs(IsDissolved ? DissolvedEvent : GatheredEvent, this));
    }

    private TextLayout? EnsureLayout()
    {
        if (_layout is not null)
        {
            return _layout;
        }
        if (string.IsNullOrEmpty(Text))
        {
            return null;
        }
        _layout = new TextLayout(Text, new Typeface(FontFamily, FontStyle, FontWeight), FontSize, Foreground);
        return _layout;
    }

    private void ResetText()
    {
        _layout?.Dispose();
        _layout = null;
        _grainsDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EnsureGrains()
    {
        if (!_grainsDirty)
        {
            return;
        }
        _grainsDirty = false;
        if (EnsureLayout() is not { } layout || string.IsNullOrEmpty(Text))
        {
            _grains.Build(default, GrainSize, static _ => false, Drift, Distance, 0);
            return;
        }
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight);
        var formatted = new FormattedText(Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, Brushes.Black);
        var geometry = formatted.BuildGeometry(default);
        var ink = geometry?.Bounds ?? default;
        if (geometry is not null && ink.Width > 0 && ink.Height > 0)
        {
            _grains.Build(ink, GrainSize, geometry.FillContains, Drift, Distance, MaxGrains);
        }
        if (_grains.Count == 0 && !string.IsNullOrWhiteSpace(Text))
        {
            // No outlines (a platform without glyph geometry): crumble the layout box instead.
            var box = new Rect(0, 0, layout.WidthIncludingTrailingWhitespace, layout.Height);
            _grains.Build(box, GrainSize, static _ => true, Drift, Distance, MaxGrains);
        }
    }

    private void BuildTones(Color color)
    {
        for (var i = 0; i < ToneLevels; i++)
        {
            var alpha = (byte)Math.Round(color.A * i / (double)(ToneLevels - 1));
            _tones[i] = new ImmutableSolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }
    }

    private void UpdateFocusable()
    {
        Focusable = Trigger != SandTextTrigger.None;
        Cursor = Trigger == SandTextTrigger.Click ? new Cursor(StandardCursorType.Hand) : null;
    }

    private void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } || _ownsName)
        {
            _ownsName = true;
            AutomationProperties.SetName(this, Text ?? string.Empty);
        }
    }

    private bool _ownsName;

    private sealed class SandTextAutomationPeer(SandText owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.Trigger == SandTextTrigger.Click ? AutomationControlType.Button : AutomationControlType.Text;

        protected override string? GetHelpTextCore() =>
            base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.IsDissolved ? "Blown away" : null;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke()
        {
            if (owner.IsEffectivelyEnabled && owner.Trigger != SandTextTrigger.None)
            {
                owner.Toggle();
            }
        }
    }
}
