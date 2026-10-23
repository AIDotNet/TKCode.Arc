using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One source of the answer as a small card (Arc's <c>a</c> in the source list): a 1px Hairline, radius 14, Surface fill,
/// padding 11 12 12; on top the 14px site mark, 7px, the domain (12 / 16, TextMuted, trimmed, clear of the number); 6px
/// under it the title (14 / 19.6, medium, Foreground; one trimmed line in the grid, at most two in the narrow row); the
/// number in an 18px pill at 10px from the top right (12px, TextSecondary, tabular, SurfaceMuted).
/// <para>
/// Hover (mouse or pen), keyboard focus and <see cref="IsLit"/> (a citation of this source is active) look the same: SurfaceMuted fill,
/// a Border edge (BorderStrong in dark) and a darker number (SurfaceMuted mixed with 18% of the accent, Foreground text),
/// over 160ms on the standard curve at full motion; Reduced and Off change at once. The whole card is a link: a press,
/// Enter or Space raises <see cref="AiSourcesRow.CardInvokedEvent"/>. Its accessible name comes from the row's
/// <see cref="AiSourcesRow.CardName"/>; the parts inside are not read again.
/// </para>
/// </summary>
[PseudoClasses(HoverPseudoClass, LitPseudoClass, NarrowPseudoClass, DarkPseudoClass)]
internal sealed class AiSourceCard : AvaloniaButton
{
    /// <summary>Mouse or pen over the card (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The owner lit the card (a citation of its source is active).</summary>
    public const string LitPseudoClass = ":lit";

    /// <summary>The card is in the narrow scrolling row: the title may take two lines.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The page is dark: the hairline and the lit edge take darker-theme tokens.</summary>
    public const string DarkPseudoClass = ":dark";

    /// <summary>The share of the accent in the lit number's fill (Arc's <c>color-mix(SurfaceMuted, accent 18%)</c>).</summary>
    internal const double LitNumberAccentShare = 0.18;

    /// <summary>The card fades in over 200ms when it arrives (Arc's <c>arrive</c>, ease-out keyframes).</summary>
    internal static readonly TimeSpan ArriveDuration = TimeSpan.FromMilliseconds(200);

    /// <summary>The source shown.</summary>
    public static readonly StyledProperty<AiSource?> SourceProperty =
        AvaloniaProperty.Register<AiSourceCard, AiSource?>(nameof(Source));

    /// <summary>Whether the card shows its lit look.</summary>
    public static readonly StyledProperty<bool> IsLitProperty =
        AvaloniaProperty.Register<AiSourceCard, bool>(nameof(IsLit));

    /// <summary>Whether the card is in the narrow row.</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<AiSourceCard, bool>(nameof(IsNarrow));

    /// <summary>Names the card for automation: number, title, domain.</summary>
    public static readonly StyledProperty<Func<int, string, string?, string>?> CardNameProperty =
        AvaloniaProperty.Register<AiSourceCard, Func<int, string, string?, string>?>(nameof(CardName));

    /// <summary>The title as shown.</summary>
    public static readonly StyledProperty<string?> TitleTextProperty =
        AvaloniaProperty.Register<AiSourceCard, string?>(nameof(TitleText));

    /// <summary>The domain as shown.</summary>
    public static readonly StyledProperty<string?> DomainTextProperty =
        AvaloniaProperty.Register<AiSourceCard, string?>(nameof(DomainText));

    /// <summary>The number as shown.</summary>
    public static readonly StyledProperty<string?> NumberTextProperty =
        AvaloniaProperty.Register<AiSourceCard, string?>(nameof(NumberText));

    /// <summary>The number pill's fill when hovered or lit (follows the accent, so it is mixed here, not in a token).</summary>
    public static readonly StyledProperty<IBrush?> LitNumberFillProperty =
        AvaloniaProperty.Register<AiSourceCard, IBrush?>(nameof(LitNumberFill));

    // Token probes for the derived colors.
    private static readonly StyledProperty<IBrush?> PageProbeProperty =
        AvaloniaProperty.Register<AiSourceCard, IBrush?>("PageProbe");

    private static readonly StyledProperty<IBrush?> MutedProbeProperty =
        AvaloniaProperty.Register<AiSourceCard, IBrush?>("MutedProbe");

    private static readonly StyledProperty<IBrush?> AccentProbeProperty =
        AvaloniaProperty.Register<AiSourceCard, IBrush?>("AccentProbe");

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly Easing s_linear = new LinearEasing();

    private AiSource? _watched;
    private MotionValue<double>? _arrive;
    private bool _isHovered;
    private bool _isKeyboardFocused;

    public AiSourceCard()
    {
        ArcTokens.Bind(this, PageProbeProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(this, MutedProbeProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, AccentProbeProperty, ArcDesignTokenKind.Accent);
    }

    /// <inheritdoc cref="SourceProperty"/>
    public AiSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="IsLitProperty"/>
    public bool IsLit { get => GetValue(IsLitProperty); set => SetValue(IsLitProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="CardNameProperty"/>
    public Func<int, string, string?, string>? CardName { get => GetValue(CardNameProperty); set => SetValue(CardNameProperty, value); }

    /// <inheritdoc cref="TitleTextProperty"/>
    public string? TitleText { get => GetValue(TitleTextProperty); set => SetValue(TitleTextProperty, value); }

    /// <inheritdoc cref="DomainTextProperty"/>
    public string? DomainText { get => GetValue(DomainTextProperty); set => SetValue(DomainTextProperty, value); }

    /// <inheritdoc cref="NumberTextProperty"/>
    public string? NumberText { get => GetValue(NumberTextProperty); set => SetValue(NumberTextProperty, value); }

    /// <inheritdoc cref="LitNumberFillProperty"/>
    public IBrush? LitNumberFill { get => GetValue(LitNumberFillProperty); set => SetValue(LitNumberFillProperty, value); }

    /// <summary>The number of the card's source (<see cref="AiSource.Index"/>), 0 without a source.</summary>
    public int Number => Source?.Index ?? 0;

    /// <summary>Whether a mouse or pen is over the card.</summary>
    internal bool IsHovered => _isHovered;

    /// <summary>Whether the card has keyboard focus.</summary>
    internal bool IsKeyboardFocused => _isKeyboardFocused;

    /// <summary>Raised when <see cref="IsHovered"/> or <see cref="IsKeyboardFocused"/> changes.</summary>
    internal event EventHandler? ActivityChanged;

    /// <summary>Raised when the card's source number changes (the owner re-evaluates which card is lit).</summary>
    internal event EventHandler? NumberChanged;

    /// <summary>The card's opacity from its arrival fade (1 at rest).</summary>
    internal double ArriveOpacity => _arrive?.Value ?? 1;

    protected override Type StyleKeyOverride => typeof(AiSourceCard);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Watch(this.IsAttachedToVisualTree() ? change.GetNewValue<AiSource?>() : null);
            UpdateTexts();
            NumberChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == CardNameProperty)
        {
            UpdateName();
        }
        else if (change.Property == IsLitProperty)
        {
            PseudoClasses.Set(LitPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == PageProbeProperty)
        {
            PseudoClasses.Set(DarkPseudoClass, change.GetNewValue<IBrush?>() is ISolidColorBrush page && Oklch.FromColor(page.Color).L < 0.5);
        }
        else if (change.Property == MutedProbeProperty || change.Property == AccentProbeProperty)
        {
            UpdateLitNumberFill();
        }
    }

    /// <summary>SurfaceMuted mixed in OKLab with 18% of the accent; recomputed only when either token changes.</summary>
    private void UpdateLitNumberFill()
    {
        if (GetValue(MutedProbeProperty) is ISolidColorBrush muted && GetValue(AccentProbeProperty) is ISolidColorBrush accent)
        {
            LitNumberFill = new ImmutableSolidColorBrush(Oklch.MixOklab(muted.Color, accent.Color, LitNumberAccentShare));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Source);
        UpdateTexts();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
        // A card leaving the tree (the panel closed, the list changed) is neither hovered nor focused any more.
        SetHovered(false);
        SetKeyboardFocused(false);
        _arrive?.Jump(1);
        Opacity = 1;
    }

    private void Watch(AiSource? source)
    {
        if (ReferenceEquals(_watched, source))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnSourceChanged;
        }
        _watched = source;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiSource.TitleProperty || e.Property == AiSource.DomainProperty || e.Property == AiSource.IndexProperty)
        {
            UpdateTexts();
            if (e.Property == AiSource.IndexProperty)
            {
                NumberChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void UpdateTexts()
    {
        var source = Source;
        TitleText = source?.Title;
        DomainText = source?.Domain;
        NumberText = source is null ? null : source.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        UpdateName();
    }

    private void UpdateName()
    {
        if (Source is not { } source)
        {
            ClearValue(AutomationProperties.NameProperty);
            return;
        }
        AutomationProperties.SetName(this, CardName?.Invoke(source.Index, source.Title, source.Domain) ?? source.Title);
    }

    /// <summary>
    /// Fades the card in (Arc's <c>arrive</c>: 200ms, ease-out, after <paramref name="delay"/>, no movement). Code-driven
    /// because each card waits its own stagger, which a ControlTheme transition cannot give per item. Reduced motion: a
    /// 120ms linear fade without the stagger; Off: no fade.
    /// </summary>
    internal void Arrive(TimeSpan delay)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
        {
            return;
        }
        if (_arrive is null)
        {
            _arrive = new MotionValue<double>(this, 1, MotionVectors.Double);
            _arrive.Changed += value => Opacity = Math.Clamp(value, 0, 1);
        }
        _arrive.Jump(0);
        _ = _arrive.AnimateTo(1, level == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
            : MotionSpec.Tween(ArriveDuration, s_easeOut, delay));
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Source is { } source)
        {
            RaiseEvent(new AiSourceCardEventArgs(AiSourcesRow.CardInvokedEvent, this, source.Index, source));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look (or a lit answer) behind.
        SetHovered(e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHovered(false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Keyboard focus lights the answer like a hover; a click's focus does not (the light would stay after the pointer left).
        SetKeyboardFocused(e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetKeyboardFocused(false);
    }

    private void SetHovered(bool value)
    {
        PseudoClasses.Set(HoverPseudoClass, value);
        if (_isHovered != value)
        {
            _isHovered = value;
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetKeyboardFocused(bool value)
    {
        if (_isKeyboardFocused != value)
        {
            _isKeyboardFocused = value;
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiSourceCardAutomationPeer(this);

    /// <summary>A hyperlink named by the row's wording; the mark, domain, title and number inside are not read again.</summary>
    private sealed class AiSourceCardAutomationPeer(AiSourceCard owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
