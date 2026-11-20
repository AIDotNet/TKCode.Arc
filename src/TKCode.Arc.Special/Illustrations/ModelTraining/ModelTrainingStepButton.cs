using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One step of a <see cref="ModelTraining"/>'s step bar (Arc Model training, design reference): the number and the name
/// in 12 px medium, 7 px above a 2 px track whose fill shows the step's progress (muted when done, the accent while it
/// plays). The pseudo-classes say whether the step is done, current or still to come; the name hides on compact cards.
/// </summary>
[TemplatePart(FillPart, typeof(Control))]
[PseudoClasses(CurrentPseudoClass, DonePseudoClass, CompactPseudoClass)]
internal sealed class ModelTrainingStepButton : AvaloniaButton
{
    public const string FillPart = "PART_Fill";
    public const string CurrentPseudoClass = ":current";
    public const string DonePseudoClass = ":done";
    public const string CompactPseudoClass = ":compact";

    internal static readonly StyledProperty<string?> NumberProperty =
        AvaloniaProperty.Register<ModelTrainingStepButton, string?>(nameof(Number));

    internal static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ModelTrainingStepButton, string?>(nameof(Text));

    private readonly ScaleTransform _scale = new(0, 1);
    private Control? _fill;
    private double _progress;

    public ModelTrainingStepButton(int index)
    {
        Index = index;
        Number = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override Type StyleKeyOverride => typeof(ModelTrainingStepButton);

    public int Index { get; }

    public string? Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The share of the track filled (0..1).</summary>
    public double Progress
    {
        get => _progress;
        set
        {
            value = Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);
            if (Math.Abs(value - _progress) < 0.0005)
            {
                return;
            }
            _progress = value;
            _scale.ScaleX = value;
        }
    }

    /// <summary>Sets the step's state: 0 still to come, 1 current, 2 done.</summary>
    public void SetState(int state)
    {
        PseudoClasses.Set(CurrentPseudoClass, state == 1);
        PseudoClasses.Set(DonePseudoClass, state == 2);
    }

    public void SetCompact(bool compact) => PseudoClasses.Set(CompactPseudoClass, compact);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _fill = e.NameScope.Find<Control>(FillPart);
        if (_fill is not null)
        {
            _fill.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
            _fill.RenderTransform = _scale;
        }
    }
}
