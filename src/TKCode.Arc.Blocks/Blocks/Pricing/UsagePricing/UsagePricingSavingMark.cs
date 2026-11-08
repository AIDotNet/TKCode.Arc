using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The check that grows in front of the yearly saving once yearly billing applies (notes 6.5): its slot opens from 0 to
/// 22px (16px check + 6px gap) on the Smooth spring with the opacity on the same spring, pushing the sentence right, and
/// the check draws its stroke over 350ms (enter curve) after 68ms. Going back to monthly closes the slot over 180ms
/// (enter curve) without undrawing the stroke. Width moves with layout because the sentence after it must follow.
/// </summary>
internal sealed class UsagePricingSavingMark : Control
{
    public static readonly StyledProperty<bool> IsAppliedProperty =
        AvaloniaProperty.Register<UsagePricingSavingMark, bool>(nameof(IsApplied));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<UsagePricingSavingMark>();

    public bool IsApplied { get => GetValue(IsAppliedProperty); set => SetValue(IsAppliedProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Width of the open slot: the 16px check and a 6px gap.</summary>
    internal const double OpenWidth = 22;

    private readonly StrokeDrawGlyph _glyph;
    private MotionValue<double>? _open;
    private double _openValue;

    public UsagePricingSavingMark()
    {
        _glyph = new StrokeDrawGlyph
        {
            Data = ArcIcons.Check,
            Size = 16,
            StrokeThickness = 1.75,
            Progress = 1
        };
        _glyph.Bind(StrokeDrawGlyph.ForegroundProperty, this.GetObservable(ForegroundProperty));
        VisualChildren.Add(_glyph);
        ClipToBounds = true;
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Opacity = 0;
    }

    /// <summary>How open the slot is (0–1; tests).</summary>
    internal double OpenAmount => _openValue;

    /// <summary>How much of the check's stroke is drawn (tests).</summary>
    internal double StrokeProgress => _glyph.Progress;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAppliedProperty)
        {
            Sync(IsLoaded);
        }
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_open is null)
        {
            Sync(animate: false);
        }
    }

    private void Sync(bool animate)
    {
        var target = IsApplied ? 1.0 : 0.0;
        _open ??= Create();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _open.Jump(target);
            _glyph.Progress = 1;
            return;
        }
        if (IsApplied)
        {
            // Reduced: the spring jumps (MotionValue) and Play draws the stroke at once.
            _ = _open.AnimateTo(1, MotionSpec.Spring(ArcMotionPresets.Smooth));
            _glyph.Progress = 0;
            _ = _glyph.Play(TimeSpan.FromMilliseconds(350), ArcEasings.Enter, TimeSpan.FromMilliseconds(68));
        }
        else
        {
            _ = _open.AnimateTo(0, level == ArcMotionLevel.Full
                ? MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Enter)
                : MotionSpec.Instant);
        }
    }

    private MotionValue<double> Create()
    {
        var value = new MotionValue<double>(this, _openValue, MotionVectors.Double);
        value.Changed += v =>
        {
            _openValue = v;
            Opacity = Math.Clamp(v, 0, 1);
            InvalidateMeasure();
        };
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _glyph.Measure(new Size(16, 16));
        return new Size(OpenWidth * Math.Clamp(_openValue, 0, 1), 16);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _glyph.Arrange(new Rect(0, (finalSize.Height - 16) / 2, 16, 16));
        return finalSize;
    }
}
