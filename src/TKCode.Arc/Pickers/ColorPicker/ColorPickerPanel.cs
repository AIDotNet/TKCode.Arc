using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The ColorPicker panel the swatch grows into: a head that repeats the trigger (chip, label, hex) with the Done
/// action, then the saturation / brightness area, hue and opacity sliders, the format button and value field, the
/// contrast readout and the saved swatches. Created on the first open and reused.
/// </summary>
[TemplatePart("PART_Head", typeof(Control))]
[PseudoClasses(":grade-pass", ":grade-large", ":grade-fail", ":invalid", ":narrow")]
internal sealed class ColorPickerPanel : TemplatedControl
{
    public static readonly StyledProperty<double> PreferredWidthProperty =
        AvaloniaProperty.Register<ColorPickerPanel, double>(nameof(PreferredWidth), 304);

    public double PreferredWidth { get => GetValue(PreferredWidthProperty); set => SetValue(PreferredWidthProperty, value); }

    public ColorPickerChip? HeadChip { get; private set; }
    public TextBlock? HeadName { get; private set; }
    public TextBlock? HeadHex { get; private set; }
    public Control? Actions { get; private set; }
    public AvaloniaButton? DoneButton { get; private set; }
    public Control? Body { get; private set; }
    public ColorPickerPad? Area { get; private set; }
    public ColorPickerPad? Hue { get; private set; }
    public ColorPickerPad? Alpha { get; private set; }
    public Control? Field { get; private set; }
    public AvaloniaButton? FormatButton { get; private set; }
    public TextMorph? FormatLabel { get; private set; }
    public AvaloniaTextBox? Input { get; private set; }
    public Panel? MorphHost { get; private set; }
    public TextBlock? Error { get; private set; }
    public Border? Sample { get; private set; }
    public TextBlock? SampleText { get; private set; }
    public ColorPickerRatioText? Ratio { get; private set; }
    public TextBlock? Against { get; private set; }
    public PickerRollText? Grade { get; private set; }
    public TextBlock? ContrastText { get; private set; }
    public AvaloniaButton? AddButton { get; private set; }
    public ColorPickerSwatchRow? Swatches { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        HeadChip = e.NameScope.Find<ColorPickerChip>("PART_HeadChip");
        HeadName = e.NameScope.Find<TextBlock>("PART_HeadName");
        HeadHex = e.NameScope.Find<TextBlock>("PART_HeadHex");
        Actions = e.NameScope.Find<Control>("PART_Actions");
        DoneButton = e.NameScope.Find<AvaloniaButton>("PART_Done");
        Body = e.NameScope.Find<Control>("PART_Body");
        Area = e.NameScope.Find<ColorPickerPad>("PART_Area");
        Hue = e.NameScope.Find<ColorPickerPad>("PART_Hue");
        Alpha = e.NameScope.Find<ColorPickerPad>("PART_Alpha");
        Field = e.NameScope.Find<Control>("PART_Field");
        FormatButton = e.NameScope.Find<AvaloniaButton>("PART_Format");
        FormatLabel = e.NameScope.Find<TextMorph>("PART_FormatLabel");
        Input = e.NameScope.Find<AvaloniaTextBox>("PART_Input");
        MorphHost = e.NameScope.Find<Panel>("PART_MorphHost");
        Error = e.NameScope.Find<TextBlock>("PART_Error");
        Sample = e.NameScope.Find<Border>("PART_Sample");
        SampleText = e.NameScope.Find<TextBlock>("PART_SampleText");
        Ratio = e.NameScope.Find<ColorPickerRatioText>("PART_Ratio");
        Against = e.NameScope.Find<TextBlock>("PART_Against");
        Grade = e.NameScope.Find<PickerRollText>("PART_Grade");
        ContrastText = e.NameScope.Find<TextBlock>("PART_ContrastText");
        AddButton = e.NameScope.Find<AvaloniaButton>("PART_Add");
        Swatches = e.NameScope.Find<ColorPickerSwatchRow>("PART_Swatches");
    }

    public void SetGrade(string grade)
    {
        PseudoClasses.Set(":grade-pass", grade is "AAA" or "AA");
        PseudoClasses.Set(":grade-large", grade == "AA large");
        PseudoClasses.Set(":grade-fail", grade == "Fails");
    }

    public void SetInvalid(bool invalid) => PseudoClasses.Set(":invalid", invalid);

    /// <summary>Phone widths hide "against background" (Arc: max-width 360px).</summary>
    public void SetNarrow(bool narrow) => PseudoClasses.Set(":narrow", narrow);
}

/// <summary>The color value field (no frame of its own: the panel's field row draws it).</summary>
internal sealed class ColorPickerInput : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(ColorPickerInput);
}

/// <summary>The format button (HEX / RGB / HSL / OKLCH) at the start of the value field.</summary>
internal sealed class ColorPickerFormatButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ColorPickerFormatButton);
}

/// <summary>The dashed "save swatch" button.</summary>
internal sealed class ColorPickerAddButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ColorPickerAddButton);
}
