using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>The TimePicker popup surface: a scrolling, drawn list of times. Created on the first open and reused.</summary>
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ListPart, typeof(TimePickerList))]
internal sealed class TimePickerPanel : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";
    public const string ListPart = "PART_List";

    public ScrollViewer? Scroll { get; private set; }
    public TimePickerList? List { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        List = e.NameScope.Find<TimePickerList>(ListPart);
    }
}
