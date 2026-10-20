using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A page or step button of <see cref="Pagination"/>; page buttons also carry their belt motion.</summary>
[PseudoClasses(CurrentPseudoClass)]
internal class PaginationButton : AvaloniaButton
{
    public const string CurrentPseudoClass = ":current";

    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<PaginationButton, bool>(nameof(IsCurrent));

    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    /// <summary>Page this button goes to (0 for step buttons and hidden slots).</summary>
    public int Page { get; set; }

    protected override Type StyleKeyOverride => typeof(PaginationButton);

    private TranslateTransform? _belt;
    private MotionValue<double>? _beltX;
    private MotionValue<double>? _beltOpacity;

    /// <summary>Current belt offset (0 at rest).</summary>
    public double BeltOffset => _beltX?.Value ?? 0;

    /// <summary>Starts the belt: the button is drawn <paramref name="from"/> px away and springs home; new numbers fade in.</summary>
    public void Travel(double from, bool fadeIn)
    {
        if (_belt is null)
        {
            _belt = new TranslateTransform();
            RenderTransform = _belt;
        }
        _beltX ??= CreateX();
        _beltOpacity ??= CreateOpacity();
        _beltX.Jump(from);
        _ = _beltX.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        if (fadeIn)
        {
            _beltOpacity.Jump(0);
            _ = _beltOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        else if (!_beltOpacity.IsAnimating)
        {
            _beltOpacity.Jump(1);
        }
    }

    /// <summary>Ends any belt motion at rest.</summary>
    public void Settle()
    {
        _beltX?.Jump(0);
        _beltOpacity?.Jump(1);
    }

    private MotionValue<double> CreateX()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_belt is not null)
            {
                _belt.X = v;
            }
        };
        return value;
    }

    private MotionValue<double> CreateOpacity()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(CurrentPseudoClass, IsCurrent);
        }
    }
}
