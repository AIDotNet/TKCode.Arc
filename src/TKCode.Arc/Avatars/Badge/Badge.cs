using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The accent wash or accent ring of an Info <see cref="Badge"/>, built by the badge when it first is Info. Styled by its
/// own ControlTheme in BadgeTheme.axaml (<c>:wash</c> or <c>:ring</c>, <c>:hover</c> mirrored from the badge), so a code-built
/// part looks like the template part it replaces, hover fade included.
/// </summary>
[PseudoClasses(WashPseudoClass, RingPseudoClass, HoverPseudoClass)]
internal sealed class BadgeToneLayer : Border
{
    public const string WashPseudoClass = ":wash";
    public const string RingPseudoClass = ":ring";
    public const string HoverPseudoClass = ":hover";

    public BadgeToneLayer(bool wash)
    {
        PseudoClasses.Set(WashPseudoClass, wash);
        PseudoClasses.Set(RingPseudoClass, !wash);
    }

    protected override Type StyleKeyOverride => typeof(BadgeToneLayer);

    internal void SetHover(bool hover) => PseudoClasses.Set(HoverPseudoClass, hover);
}

/// <summary>Color treatment of a <see cref="Badge"/>.</summary>
public enum BadgeTone
{
    Neutral,
    Success,

    /// <summary>Uses the accent color.</summary>
    Info,
    Warning,
    Danger
}

/// <summary>Size of a <see cref="Badge"/>: Medium is 26px tall, Small 22px.</summary>
public enum BadgeSize
{
    Small,
    Medium
}

/// <summary>
/// A small pill for status, category or metadata (Arc Badge): a tone, an optional leading icon and a short label.
/// It is a standalone inline label, not a count overlay. When a string or number label changes while the badge stays
/// mounted, the new text rises out of a soft blur, the old one lifts away, and the pill width springs to fit (P2, P5);
/// a new icon pops in from 0.6. Other content is shown as is. Reduced motion crossfades and the width jumps.
/// </summary>
[PseudoClasses(TextPseudoClass, IconPseudoClass, SmallPseudoClass, NeutralPseudoClass, SuccessPseudoClass, InfoPseudoClass,
    WarningPseudoClass, DangerPseudoClass)]
public class Badge : ContentControl
{
    public const string TextPseudoClass = ":text";
    public const string IconPseudoClass = ":icon";

    /// <summary><see cref="Size"/> Small as a pseudo-class (the theme selects on it, not on the property).</summary>
    public const string SmallPseudoClass = ":small";

    /// <summary><see cref="Tone"/> as a pseudo-class (the theme selects on these, not on the property).</summary>
    public const string NeutralPseudoClass = ":neutral";

    /// <inheritdoc cref="NeutralPseudoClass"/>
    public const string SuccessPseudoClass = ":success";

    /// <inheritdoc cref="NeutralPseudoClass"/>
    public const string InfoPseudoClass = ":info";

    /// <inheritdoc cref="NeutralPseudoClass"/>
    public const string WarningPseudoClass = ":warning";

    /// <inheritdoc cref="NeutralPseudoClass"/>
    public const string DangerPseudoClass = ":danger";

    /// <summary>Color treatment (Arc <c>tone</c>, neutral by default).</summary>
    public static readonly StyledProperty<BadgeTone> ToneProperty =
        AvaloniaProperty.Register<Badge, BadgeTone>(nameof(Tone));

    /// <summary>Medium (26px, default) or Small (22px).</summary>
    public static readonly StyledProperty<BadgeSize> SizeProperty =
        AvaloniaProperty.Register<Badge, BadgeSize>(nameof(Size), BadgeSize.Medium);

    /// <summary>Leading icon (for example an <see cref="ArcGlyph"/>); it takes the tone color and is hidden from assistive technology.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<Badge, object?>(nameof(Icon));

    public BadgeTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public BadgeSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    static Badge()
    {
        FocusableProperty.OverrideDefaultValue<Badge>(false);
    }

    private Panel? _root;
    private BadgeToneLayer? _wash;
    private BadgeToneLayer? _ring;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = VisualChildren.Count > 0 ? VisualChildren[0] as Panel : null;
        _wash = null;
        _ring = null;
        UpdateInfoLayers();
    }

    /// <summary>
    /// The Info tone lays an accent wash and an accent ring over the neutral pill (PART_Wash, PART_ToneRing). They are
    /// built the first time a badge is Info: the other four tones never show them, and the template used to carry both
    /// Borders and their bindings in every badge of a table. Their look and hover fade are their own theme.
    /// </summary>
    private void UpdateInfoLayers()
    {
        var info = Tone == BadgeTone.Info;
        if (_wash is null || _ring is null)
        {
            if (!info || _root is null)
            {
                return;
            }
            _wash = new BadgeToneLayer(wash: true) { Name = "PART_Wash" };
            _ring = new BadgeToneLayer(wash: false) { Name = "PART_ToneRing" };
            _wash.Bind(Border.CornerRadiusProperty, this.GetObservable(CornerRadiusProperty));
            _ring.Bind(Border.CornerRadiusProperty, this.GetObservable(CornerRadiusProperty));
            // Between the frame and the clipped content, as in the template.
            var at = Math.Min(1, _root.Children.Count);
            _root.Children.Insert(at, _wash);
            _root.Children.Insert(at + 1, _ring);
        }
        _wash.SetHover(IsPointerOver);
        _ring.SetHover(IsPointerOver);
        _wash.IsVisible = info;
        _ring.IsVisible = info;
    }

    public Badge()
    {
        UpdateContentState();
        UpdateTonePseudoClasses();
        PseudoClasses.Set(SmallPseudoClass, Size == BadgeSize.Small);
    }

    // Pseudo-classes, not property selectors: a `^[Tone=Success]` selector costs every badge an activator and a
    // subscription per selector, and badges repeat in every table row.
    private void UpdateTonePseudoClasses()
    {
        var tone = Tone;
        PseudoClasses.Set(NeutralPseudoClass, tone == BadgeTone.Neutral);
        PseudoClasses.Set(SuccessPseudoClass, tone == BadgeTone.Success);
        PseudoClasses.Set(InfoPseudoClass, tone == BadgeTone.Info);
        PseudoClasses.Set(WarningPseudoClass, tone == BadgeTone.Warning);
        PseudoClasses.Set(DangerPseudoClass, tone == BadgeTone.Danger);
    }

    /// <summary>True for content Arc animates in place: strings and numbers.</summary>
    internal static bool IsTextual(object? content) => content is string or sbyte or byte or short or ushort or int or uint
        or long or ulong or float or double or decimal;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == IconProperty)
        {
            UpdateContentState();
        }
        else if (change.Property == ToneProperty)
        {
            UpdateTonePseudoClasses();
            UpdateInfoLayers();
        }
        else if (change.Property == SizeProperty)
        {
            PseudoClasses.Set(SmallPseudoClass, Size == BadgeSize.Small);
        }
        else if (change.Property == IsPointerOverProperty && _wash is not null && _ring is not null)
        {
            // The layers' hover fade follows the badge's :pointerover.
            _wash.SetHover(IsPointerOver);
            _ring.SetHover(IsPointerOver);
        }
    }

    private void UpdateContentState()
    {
        PseudoClasses.Set(TextPseudoClass, IsTextual(Content));
        PseudoClasses.Set(IconPseudoClass, Icon is not null);
        if (IsTextual(Content))
        {
            Avalonia.Automation.AutomationProperties.SetName(this, Convert.ToString(Content, System.Globalization.CultureInfo.CurrentCulture));
        }
    }
}
