using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The TimePicker popup surface: a scrolling, drawn list of times, or (column layout) one drawn column each for the
/// hours, minutes, seconds and AM / PM. Created on the first open and reused.
/// </summary>
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ListPart, typeof(TimePickerList))]
[TemplatePart(ColumnsPart, typeof(Control))]
[TemplatePart(HourScrollPart, typeof(ScrollViewer))]
[TemplatePart(HourListPart, typeof(TimePickerList))]
[TemplatePart(MinuteScrollPart, typeof(ScrollViewer))]
[TemplatePart(MinuteListPart, typeof(TimePickerList))]
[TemplatePart(SecondScrollPart, typeof(ScrollViewer))]
[TemplatePart(SecondListPart, typeof(TimePickerList))]
[TemplatePart(MeridiemScrollPart, typeof(ScrollViewer))]
[TemplatePart(MeridiemListPart, typeof(TimePickerList))]
internal sealed class TimePickerPanel : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";
    public const string ListPart = "PART_List";
    public const string ColumnsPart = "PART_Columns";
    public const string HourScrollPart = "PART_HourScroll";
    public const string HourListPart = "PART_HourList";
    public const string MinuteScrollPart = "PART_MinuteScroll";
    public const string MinuteListPart = "PART_MinuteList";
    public const string SecondScrollPart = "PART_SecondScroll";
    public const string SecondListPart = "PART_SecondList";
    public const string MeridiemScrollPart = "PART_MeridiemScroll";
    public const string MeridiemListPart = "PART_MeridiemList";

    private readonly List<TimePickerColumn> _columns = new();
    private readonly List<TimePickerColumn> _visible = new();
    private Control? _columnsHost;
    private bool _usesColumns;
    private bool _showSeconds;
    private bool _showMeridiem;

    public ScrollViewer? Scroll { get; private set; }
    public TimePickerList? List { get; private set; }

    /// <summary>Every column of the template, in order (hour, minute, second, AM / PM).</summary>
    public IReadOnlyList<TimePickerColumn> Columns => _columns;

    /// <summary>The columns shown, in order; empty in the list layout.</summary>
    public IReadOnlyList<TimePickerColumn> VisibleColumns => _visible;

    /// <summary>True while the panel shows columns instead of the list.</summary>
    public bool UsesColumns => _usesColumns;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        List = e.NameScope.Find<TimePickerList>(ListPart);
        _columnsHost = e.NameScope.Find<Control>(ColumnsPart);
        _columns.Clear();
        Add(TimePickerColumnKind.Hour, HourScrollPart, HourListPart);
        Add(TimePickerColumnKind.Minute, MinuteScrollPart, MinuteListPart);
        Add(TimePickerColumnKind.Second, SecondScrollPart, SecondListPart);
        Add(TimePickerColumnKind.Meridiem, MeridiemScrollPart, MeridiemListPart);
        ApplyLayout();

        void Add(TimePickerColumnKind kind, string scrollPart, string listPart)
        {
            if (e.NameScope.Find<ScrollViewer>(scrollPart) is { } scroll && e.NameScope.Find<TimePickerList>(listPart) is { } list)
            {
                _columns.Add(new TimePickerColumn(kind, scroll, list));
            }
        }
    }

    /// <summary>Shows the list, or the columns with or without seconds and AM / PM.</summary>
    public void SetLayout(bool columns, bool seconds, bool meridiem)
    {
        _usesColumns = columns;
        _showSeconds = seconds;
        _showMeridiem = meridiem;
        ApplyLayout();
    }

    /// <summary>The visible column of <paramref name="kind"/>, or null.</summary>
    public TimePickerColumn? ColumnOf(TimePickerColumnKind kind)
    {
        foreach (var column in _visible)
        {
            if (column.Kind == kind)
            {
                return column;
            }
        }
        return null;
    }

    private void ApplyLayout()
    {
        if (Scroll is not null)
        {
            Scroll.IsVisible = !_usesColumns;
        }
        if (_columnsHost is not null)
        {
            _columnsHost.IsVisible = _usesColumns;
        }
        _visible.Clear();
        foreach (var column in _columns)
        {
            var shown = _usesColumns && column.Kind switch
            {
                TimePickerColumnKind.Second => _showSeconds,
                TimePickerColumnKind.Meridiem => _showMeridiem,
                _ => true
            };
            column.Scroll.IsVisible = shown;
            if (shown)
            {
                _visible.Add(column);
            }
        }
    }
}
