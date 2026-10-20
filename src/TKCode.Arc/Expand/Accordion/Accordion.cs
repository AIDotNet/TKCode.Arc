using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>How many rows of an <see cref="Accordion"/> can be open at once.</summary>
public enum AccordionExpandMode
{
    /// <summary>One row at a time; opening another closes the open one, and the open row can be closed (Arc).</summary>
    Single,

    /// <summary>Rows open and close independently (not in Arc).</summary>
    Multiple
}

/// <summary>Size of an <see cref="Accordion"/>.</summary>
public enum AccordionSize
{
    /// <summary>Questions at text-sm on 50px rows (Arc "md").</summary>
    Medium,

    /// <summary>Page-level FAQs: questions at text-lg on 76px rows, answers at body size (Arc "lg").</summary>
    Large
}

/// <summary>
/// Arc accordion: a list of question rows that reveal their answers in place. The panel height follows the content
/// on a smooth spring that never overshoots and retargets mid-flight, the answer settles down 6px out of a subtle
/// blur as it opens, and the chevron turns on a snappy spring. Single mode (Arc) keeps one row open and lets it
/// close; <see cref="AccordionExpandMode.Multiple"/> opens rows independently. Arrow keys move between the row
/// triggers, Home / End jump to the ends, Enter / Space toggle.
/// <para>
/// Rows are <see cref="AccordionItem"/>s written inline (<c>&lt;arc:AccordionItem Header="Question"&gt;Answer&lt;/arc:AccordionItem&gt;</c>)
/// or generated from <see cref="ItemsControl.ItemsSource"/> (<see cref="ItemsControl.ItemTemplate"/> renders the
/// question, <see cref="ContentTemplate"/> the answer).
/// </para>
/// </summary>
public class Accordion : ItemsControl
{
    /// <summary>Index of the open row; −1 closes every row (Arc defaultOpen, default 0). Two-way.</summary>
    public static readonly StyledProperty<int> ExpandedIndexProperty =
        AvaloniaProperty.Register<Accordion, int>(nameof(ExpandedIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Single (Arc) or multiple open rows.</summary>
    public static readonly StyledProperty<AccordionExpandMode> ExpandModeProperty =
        AvaloniaProperty.Register<Accordion, AccordionExpandMode>(nameof(ExpandMode));

    /// <summary>Row size; <see cref="AccordionSize.Large"/> suits page-level FAQs.</summary>
    public static readonly StyledProperty<AccordionSize> SizeProperty =
        AvaloniaProperty.Register<Accordion, AccordionSize>(nameof(Size));

    /// <summary>Template for the answers of data items (the question uses <see cref="ItemsControl.ItemTemplate"/>).</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<Accordion, IDataTemplate?>(nameof(ContentTemplate));

    /// <inheritdoc cref="ExpandedIndexProperty"/>
    public int ExpandedIndex { get => GetValue(ExpandedIndexProperty); set => SetValue(ExpandedIndexProperty, value); }

    /// <inheritdoc cref="ExpandModeProperty"/>
    public AccordionExpandMode ExpandMode { get => GetValue(ExpandModeProperty); set => SetValue(ExpandModeProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public AccordionSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }

    private bool _syncing;

    static Accordion()
    {
        FocusableProperty.OverrideDefaultValue<Accordion>(false);
    }

    /// <summary>Opens or closes the row at <paramref name="index"/>.</summary>
    public void Toggle(int index)
    {
        if (ContainerFromIndex(index) is AccordionItem item)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    /// <summary>Closes every row.</summary>
    public void CollapseAll()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is AccordionItem item)
            {
                item.IsExpanded = false;
            }
        }
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AccordionItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<AccordionItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is AccordionItem row)
        {
            if (!ReferenceEquals(container, item) && ContentTemplate is { } template && !row.IsSet(ContentControl.ContentTemplateProperty))
            {
                row.ContentTemplate = template;
            }
            row.Size = Size;
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is not AccordionItem row)
        {
            return;
        }
        _syncing = true;
        try
        {
            if (row.IsExpanded)
            {
                // An inline row written open wins over the default index.
                OnItemExpandedChanged(row, true);
            }
            else if (index == ExpandedIndex)
            {
                row.IsExpanded = true;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ExpandedIndexProperty && !_syncing)
        {
            ApplyExpandedIndex(ExpandedIndex);
        }
        else if (change.Property == SizeProperty)
        {
            for (var i = 0; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is AccordionItem row)
                {
                    row.Size = Size;
                }
            }
        }
    }

    private void ApplyExpandedIndex(int index)
    {
        _syncing = true;
        try
        {
            for (var i = 0; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is not AccordionItem row)
                {
                    continue;
                }
                if (i == index)
                {
                    row.IsExpanded = true;
                }
                else if (ExpandMode == AccordionExpandMode.Single)
                {
                    row.IsExpanded = false;
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Called by a row when it opens or closes.</summary>
    internal void OnItemExpandedChanged(AccordionItem row, bool expanded)
    {
        var index = IndexFromContainer(row);
        if (index < 0)
        {
            return;
        }
        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            if (expanded)
            {
                if (ExpandMode == AccordionExpandMode.Single)
                {
                    for (var i = 0; i < ItemCount; i++)
                    {
                        if (i != index && ContainerFromIndex(i) is AccordionItem other)
                        {
                            other.IsExpanded = false;
                        }
                    }
                }
                SetCurrentValue(ExpandedIndexProperty, index);
            }
            else if (ExpandedIndex == index)
            {
                SetCurrentValue(ExpandedIndexProperty, FirstExpanded());
            }
        }
        finally
        {
            _syncing = wasSyncing;
        }
    }

    private int FirstExpanded()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is AccordionItem { IsExpanded: true })
            {
                return i;
            }
        }
        return -1;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Source is Visual source && source.FindAncestorOfType<AccordionItem>(includeSelf: true) is { } row &&
            source is AccordionTrigger or AccordionItem && IndexFromContainer(row) is var index and >= 0)
        {
            int? target = e.Key switch
            {
                Key.Down => Step(index, 1),
                Key.Up => Step(index, -1),
                Key.Home => Step(-1, 1),
                Key.End => Step(ItemCount, -1),
                _ => null
            };
            if (target is { } next && next >= 0 && ContainerFromIndex(next) is AccordionItem nextRow)
            {
                nextRow.FocusTrigger();
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private int Step(int from, int delta)
    {
        var count = ItemCount;
        if (count == 0)
        {
            return -1;
        }
        var index = from;
        for (var i = 0; i < count; i++)
        {
            // Radix accordion triggers loop at the ends.
            index = ((index + delta) % count + count) % count;
            if (ContainerFromIndex(index) is { IsEffectivelyEnabled: true })
            {
                return index;
            }
        }
        return -1;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccordionAutomationPeer(this);

    private sealed class AccordionAutomationPeer : ControlAutomationPeer
    {
        public AccordionAutomationPeer(Accordion owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
