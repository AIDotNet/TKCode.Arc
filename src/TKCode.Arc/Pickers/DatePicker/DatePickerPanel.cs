using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The DatePicker popup surface: the calendar over a footer with Clear and a status line that confirms the pick
/// (Arc: "Selected Oct 20, 2026" / "Choose a day"). Created on the first open and reused.
/// </summary>
[TemplatePart(CalendarPart, typeof(Calendar))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
[TemplatePart(StatusPart, typeof(PickerRollText))]
[PseudoClasses(CompactPseudoClass)]
internal sealed class DatePickerPanel : TemplatedControl
{
    public const string CalendarPart = "PART_Calendar";
    public const string ClearPart = "PART_Clear";
    public const string StatusPart = "PART_Status";
    public const string CompactPseudoClass = ":compact";

    /// <summary>Arc: min(320px, viewport − 32px).</summary>
    public static readonly StyledProperty<double> PreferredWidthProperty =
        AvaloniaProperty.Register<DatePickerPanel, double>(nameof(PreferredWidth), 320);

    public double PreferredWidth { get => GetValue(PreferredWidthProperty); set => SetValue(PreferredWidthProperty, value); }

    private bool _compact;

    /// <summary>Phone widths: the popup matches the field and uses a 12px inset.</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            _compact = value;
            PseudoClasses.Set(CompactPseudoClass, value);
        }
    }

    public Calendar? Calendar { get; private set; }
    public AvaloniaButton? ClearButton { get; private set; }
    public PickerRollText? Status { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Calendar = e.NameScope.Find<Calendar>(CalendarPart);
        ClearButton = e.NameScope.Find<AvaloniaButton>(ClearPart);
        Status = e.NameScope.Find<PickerRollText>(StatusPart);
    }
}
