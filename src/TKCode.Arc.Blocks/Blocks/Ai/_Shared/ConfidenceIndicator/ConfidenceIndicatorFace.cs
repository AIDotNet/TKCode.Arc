using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One state of a <see cref="ConfidenceIndicator"/>: an optional 13px icon and the text, 4 apart (the theme colors them by
/// kind). A face is made for every change, so the old one fades out with its own colors while the new one comes in.
/// </summary>
/// <remarks>
/// The pop of a new check is code-driven (<see cref="MotionValue{T}"/>): it starts at 0.4 the moment the face appears, a
/// state the face never rests in, and runs on the snappy spring; a ControlTheme transition only moves between resting values.
/// </remarks>
[TemplatePart(GlyphPart, typeof(ArcGlyph))]
[PseudoClasses(TypePseudoClass, PercentPseudoClass, LowPseudoClass, CheckedPseudoClass, EditedPseudoClass)]
internal sealed class ConfidenceIndicatorFace : TemplatedControl
{
    public const string GlyphPart = "PART_Glyph";
    public const string TypePseudoClass = ":type";
    public const string PercentPseudoClass = ":percent";
    public const string LowPseudoClass = ":low";
    public const string CheckedPseudoClass = ":checked";
    public const string EditedPseudoClass = ":edited";

    /// <summary>Where a popping check starts.</summary>
    public const double PopFrom = 0.4;

    /// <summary>Lucide pencil-line (not in the Core subset).</summary>
    internal static readonly Geometry PencilLine = ArcIcons.Parse(
        "M13 21h8 M15 5l4 4 M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z");

    /// <summary>What the face shows.</summary>
    public static readonly StyledProperty<ConfidenceKind> KindProperty =
        AvaloniaProperty.Register<ConfidenceIndicatorFace, ConfidenceKind>(nameof(Kind));

    /// <summary>The text ("99%", "Date", "Edited").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ConfidenceIndicatorFace, string?>(nameof(Text));

    /// <summary>The icon (none for a type name or a plain percent).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ConfidenceIndicatorFace, Geometry?>(nameof(Glyph));

    private readonly ScaleTransform _scale = new(1, 1);
    private ArcGlyph? _glyph;
    private MotionValue<double>? _pop;

    static ConfidenceIndicatorFace()
    {
        FocusableProperty.OverrideDefaultValue<ConfidenceIndicatorFace>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<ConfidenceIndicatorFace>(false);
    }

    public ConfidenceIndicatorFace()
    {
        PseudoClasses.Set(TypePseudoClass, true);
    }

    /// <inheritdoc cref="KindProperty"/>
    public ConfidenceKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <summary>Whether the check pops from 0.4 when the face appears (full motion only).</summary>
    public bool Pops { get; init; }

    /// <summary>The icon's scale now (1 at rest).</summary>
    internal double GlyphScale => _scale.ScaleX;

    /// <summary>Whether the pop is running.</summary>
    internal bool IsPopping => _pop?.IsAnimating == true;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            var kind = change.GetNewValue<ConfidenceKind>();
            PseudoClasses.Set(TypePseudoClass, kind is ConfidenceKind.Type or ConfidenceKind.None);
            PseudoClasses.Set(PercentPseudoClass, kind == ConfidenceKind.Percent);
            PseudoClasses.Set(LowPseudoClass, kind == ConfidenceKind.Low);
            PseudoClasses.Set(CheckedPseudoClass, ConfidenceIndicator.HasCheck(kind));
            PseudoClasses.Set(EditedPseudoClass, kind == ConfidenceKind.Edited);
            Glyph = kind switch
            {
                ConfidenceKind.Low => ArcIcons.TriangleAlert,
                ConfidenceKind.Checked or ConfidenceKind.Accepted or ConfidenceKind.EditedAccepted => ArcIcons.Check,
                ConfidenceKind.Edited => PencilLine,
                _ => null
            };
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<ArcGlyph>(GlyphPart);
        if (_glyph is not null)
        {
            _glyph.RenderTransformOrigin = RelativePoint.Center;
            _glyph.RenderTransform = _scale;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!Pops || _pop is not null || !ArcMotion.IsFull(this))
        {
            return;
        }
        _pop = new MotionValue<double>(this, PopFrom, MotionVectors.Double);
        _pop.Changed += v =>
        {
            _scale.ScaleX = v;
            _scale.ScaleY = v;
        };
        SetScale(PopFrom);
        _ = _pop.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pop?.Stop();
        SetScale(1);
    }

    private void SetScale(double value)
    {
        _scale.ScaleX = value;
        _scale.ScaleY = value;
    }
}
