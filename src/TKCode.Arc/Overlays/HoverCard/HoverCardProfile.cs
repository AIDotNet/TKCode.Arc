using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A number shown in a <see cref="HoverCardProfile"/> (value over its label).</summary>
public sealed class HoverCardStat
{
    public HoverCardStat()
    {
    }

    public HoverCardStat(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// A person preview for <see cref="HoverCard"/>: portrait (or initials), name, role, a short bio, a few numbers and a
/// quiet footer line. Each time the card opens its rows settle in from 4px below with a tiny stagger
/// (40ms + 35ms per row, standard duration, ease enter); reduced motion shows them at rest.
/// The stagger replays on every open, which a ControlTheme transition (first frame never animates) cannot express, so one
/// linear <see cref="MotionValue{T}"/> drives a shared timeline for all rows.
/// </summary>
[TemplatePart(HeadPart, typeof(Control))]
[TemplatePart(BioPart, typeof(Control))]
[TemplatePart(StatsPart, typeof(Control))]
[TemplatePart(MetaPart, typeof(Control))]
[PseudoClasses(HasRolePseudoClass, HasBioPseudoClass, HasStatsPseudoClass, HasMetaPseudoClass)]
public class HoverCardProfile : TemplatedControl
{
    public const string HeadPart = "PART_Head";
    public const string BioPart = "PART_Bio";
    public const string StatsPart = "PART_Stats";
    public const string MetaPart = "PART_Meta";
    public const string HasRolePseudoClass = ":has-role";
    public const string HasBioPseudoClass = ":has-bio";
    public const string HasStatsPseudoClass = ":has-stats";
    public const string HasMetaPseudoClass = ":has-meta";

    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<HoverCardProfile, string?>(nameof(DisplayName));

    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<HoverCardProfile, string?>(nameof(Role));

    public static readonly StyledProperty<ImageSource?> AvatarSourceProperty =
        AvaloniaProperty.Register<HoverCardProfile, ImageSource?>(nameof(AvatarSource));

    /// <summary>Short biography shown under the name; empty when not set.</summary>
    public static readonly StyledProperty<string?> BioProperty =
        AvaloniaProperty.Register<HoverCardProfile, string?>(nameof(Bio));

    public static readonly StyledProperty<object?> MetaProperty =
        AvaloniaProperty.Register<HoverCardProfile, object?>(nameof(Meta));

    public static readonly DirectProperty<HoverCardProfile, AvaloniaList<HoverCardStat>> StatsProperty =
        AvaloniaProperty.RegisterDirect<HoverCardProfile, AvaloniaList<HoverCardStat>>(nameof(Stats), o => o.Stats);

    /// <summary>The person's name (initials come from it when there is no portrait).</summary>
    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    /// <summary>Role and team, such as "Product designer, Payments".</summary>
    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <summary>Portrait; a downloading portrait fades in from a soft blur.</summary>
    public ImageSource? AvatarSource { get => GetValue(AvatarSourceProperty); set => SetValue(AvatarSourceProperty, value); }

    /// <inheritdoc cref="BioProperty"/>
    public string? Bio { get => GetValue(BioProperty); set => SetValue(BioProperty, value); }

    /// <summary>Footer line, such as a location and local time (string or content).</summary>
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <summary>Label / value pairs shown as a wrapping row of numbers.</summary>
    public AvaloniaList<HoverCardStat> Stats { get; } = new();

    private readonly Control?[] _rows = new Control?[4];
    private readonly TranslateTransform?[] _rowTransforms = new TranslateTransform?[4];
    private MotionValue<double>? _stagger;

    private static readonly double RowDuration = ArcMotionPresets.Standard.TotalSeconds;
    private static readonly double FirstDelay = 0.04;
    private static readonly double StaggerStep = ArcMotionPresets.StaggerItem.TotalSeconds;
    private static readonly double Total = FirstDelay + 3 * StaggerStep + RowDuration;

    public HoverCardProfile()
    {
        Stats.CollectionChanged += (_, _) => PseudoClasses.Set(HasStatsPseudoClass, Stats.Count > 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RoleProperty)
        {
            PseudoClasses.Set(HasRolePseudoClass, !string.IsNullOrEmpty(Role));
        }
        else if (change.Property == BioProperty)
        {
            PseudoClasses.Set(HasBioPseudoClass, !string.IsNullOrEmpty(Bio));
        }
        else if (change.Property == MetaProperty)
        {
            PseudoClasses.Set(HasMetaPseudoClass, change.NewValue is not null);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rows[0] = e.NameScope.Find<Control>(HeadPart);
        _rows[1] = e.NameScope.Find<Control>(BioPart);
        _rows[2] = e.NameScope.Find<Control>(StatsPart);
        _rows[3] = e.NameScope.Find<Control>(MetaPart);
        for (var i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] is { } row)
            {
                _rowTransforms[i] = new TranslateTransform();
                row.RenderTransform = _rowTransforms[i];
            }
        }
        ApplyStagger(1);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Each opening of the card settles the rows in again.
        _stagger ??= CreateStagger();
        if (!ArcMotion.IsFull(this))
        {
            _stagger.Jump(1);
            return;
        }
        _stagger.Jump(0);
        _ = _stagger.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(Total), s_linear));
    }

    private static readonly Avalonia.Animation.Easings.Easing s_linear = new Avalonia.Animation.Easings.LinearEasing();

    private MotionValue<double> CreateStagger()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += ApplyStagger;
        return value;
    }

    private void ApplyStagger(double t)
    {
        var elapsed = t * Total;
        for (var i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] is not { } row || _rowTransforms[i] is not { } transform)
            {
                continue;
            }
            var local = Math.Clamp((elapsed - FirstDelay - i * StaggerStep) / RowDuration, 0, 1);
            var eased = t >= 1 ? 1 : ArcEasings.Enter.Ease(local);
            row.Opacity = eased;
            transform.Y = 4 * (1 - eased);
        }
    }
}
