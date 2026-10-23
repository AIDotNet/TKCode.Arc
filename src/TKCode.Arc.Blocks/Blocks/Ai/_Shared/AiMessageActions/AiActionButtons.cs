using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A Lucide glyph for the reply actions: stroked like <see cref="ArcGlyph"/> (24 × 24 box, stroke 1.75 in box units) and,
/// when <see cref="IsFilled"/>, also filled with the same color (Arc's pressed rating: <c>fill: currentColor</c>).
/// </summary>
internal sealed class AiActionGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<AiActionGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiActionGlyph, double>(nameof(Size), 15);

    /// <summary>Fills the shape as well as stroking it.</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<AiActionGlyph, bool>(nameof(IsFilled));

    /// <summary>The color (inherited text color).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<AiActionGlyph>();

    private Pen? _pen;
    private IBrush? _penBrush;

    static AiActionGlyph()
    {
        AffectsMeasure<AiActionGlyph>(SizeProperty);
        AffectsRender<AiActionGlyph>(DataProperty, IsFilledProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiActionGlyph>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<AiActionGlyph>(Avalonia.Layout.HorizontalAlignment.Center);
        VerticalAlignmentProperty.OverrideDefaultValue<AiActionGlyph>(Avalonia.Layout.VerticalAlignment.Center);
    }

    /// <inheritdoc cref="DataProperty"/>
    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="IsFilledProperty"/>
    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            // The color animates through new brushes on hover; a steady glyph reuses its pen.
            _pen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(IsFilled ? brush : null, _pen, data);
        }
    }
}

/// <summary>
/// One reply action (Copy, Retry): 30 × 30, radius 10, the glyph in TextMuted; hover fills it SurfaceMuted and turns the
/// glyph Foreground (160ms); a press scales it to 0.92 and back on Arc's CSS spring (full motion only). A new
/// <see cref="Glyph"/> crossfades (Copy → Check). Its name and tooltip (below) come from the owner.
/// </summary>
[TemplatePart(GlyphPart, typeof(ArcContentSwap))]
[PseudoClasses(HoverPseudoClass)]
internal class AiActionButton : AvaloniaButton
{
    public const string GlyphPart = "PART_Glyph";

    /// <summary>Mouse or pen over the button.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The Lucide glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiActionButton, Geometry?>(nameof(Glyph));

    /// <summary>The glyph's size: 16 for Copy, 15 for the others.</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AiActionButton, double>(nameof(GlyphSize), 15);

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="GlyphSizeProperty"/>
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    private ArcContentSwap? _swap;
    private AiActionGlyph? _glyph;

    protected override Type StyleKeyOverride => typeof(AiActionButton);

    /// <summary>The glyph on screen now.</summary>
    internal AiActionGlyph? ShownGlyph => _glyph;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(GlyphPart);
        _glyph = null;
        UpdateGlyph(instant: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty || change.Property == GlyphSizeProperty)
        {
            UpdateGlyph(instant: false);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
        else
        {
            OnStateChanged(change);
        }
    }

    /// <summary>Lets a subclass follow its own state (a rating's pressed look).</summary>
    protected virtual void OnStateChanged(AvaloniaPropertyChangedEventArgs change)
    {
    }

    /// <summary>Whether the glyph is filled (a pressed rating).</summary>
    protected virtual bool IsGlyphFilled => false;

    private void UpdateGlyph(bool instant)
    {
        if (_swap is null)
        {
            return;
        }
        if (_glyph is not null && ReferenceEquals(_glyph.Data, Glyph) && _glyph.Size.Equals(GlyphSize))
        {
            return;
        }
        _glyph = new AiActionGlyph { Data = Glyph, Size = GlyphSize, IsFilled = IsGlyphFilled };
        if (instant)
        {
            var mode = _swap.Mode;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
            _swap.Content = _glyph;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
        }
        else
        {
            _swap.Content = _glyph;
        }
    }

    /// <summary>Fills or clears the glyph on screen in place (no crossfade: Arc fills at once, the color eases).</summary>
    protected void RefreshFill()
    {
        if (_glyph is not null)
        {
            _glyph.IsFilled = IsGlyphFilled;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiActionButtonAutomationPeer(this);

    /// <summary>A button named by the owner's wording; the glyph is not read.</summary>
    private sealed class AiActionButtonAutomationPeer(AiActionButton owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// "Good response" or "Bad response": an <see cref="AiActionButton"/> with a pressed state (<see cref="IsPressedOn"/>, set
/// by the owner from the rating) that fills the glyph in Foreground, no fill behind it. Automation reports it as a toggle
/// (Arc's <c>aria-pressed</c>); toggling asks the owner, which decides.
/// </summary>
[PseudoClasses(OnPseudoClass)]
internal sealed class AiRatingButton : AiActionButton
{
    /// <summary>Set while the rating is chosen.</summary>
    public const string OnPseudoClass = ":on";

    /// <summary>Whether this rating is the chosen one.</summary>
    public static readonly StyledProperty<bool> IsPressedOnProperty =
        AvaloniaProperty.Register<AiRatingButton, bool>(nameof(IsPressedOn));

    /// <inheritdoc cref="IsPressedOnProperty"/>
    public bool IsPressedOn { get => GetValue(IsPressedOnProperty); set => SetValue(IsPressedOnProperty, value); }

    // The action button's theme serves both (its :on style is the pressed rating).
    protected override Type StyleKeyOverride => typeof(AiActionButton);

    protected override bool IsGlyphFilled => IsPressedOn;

    protected override void OnStateChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsPressedOnProperty)
        {
            var on = change.GetNewValue<bool>();
            PseudoClasses.Set(OnPseudoClass, on);
            RefreshFill();
            (ControlAutomationPeer.FromElement(this) as AiRatingButtonAutomationPeer)?.RaiseToggleChanged(on);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiRatingButtonAutomationPeer(this);

    /// <summary>A toggle button (<c>aria-pressed</c>) named by the owner's wording.</summary>
    private sealed class AiRatingButtonAutomationPeer(AiRatingButton owner)
        : ButtonAutomationPeer(owner), Avalonia.Automation.Provider.IToggleProvider
    {
        private AiRatingButton Rating => (AiRatingButton)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        public Avalonia.Automation.Provider.ToggleState ToggleState =>
            Rating.IsPressedOn ? Avalonia.Automation.Provider.ToggleState.On : Avalonia.Automation.Provider.ToggleState.Off;

        public void Toggle()
        {
            EnsureEnabled();
            ((Avalonia.Automation.Provider.IInvokeProvider)this).Invoke();
        }

        public void RaiseToggleChanged(bool on) =>
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                on ? Avalonia.Automation.Provider.ToggleState.Off : Avalonia.Automation.Provider.ToggleState.On,
                on ? Avalonia.Automation.Provider.ToggleState.On : Avalonia.Automation.Provider.ToggleState.Off);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
