using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// Arc radio group: choose one option from a visible set. One highlight (a <c>ControlOn</c> border with a faint fill)
/// glides to the chosen row on the morph spring while the new dot springs in and the old one shrinks away, so a change
/// reads as a single physical move. Arrow keys take the same path; the group is one tab stop (the selected row).
/// Options come from <see cref="ItemsControl.ItemsSource"/> or inline <see cref="RadioOption"/> items;
/// <see cref="SelectingItemsControl.SelectedValue"/> (two-way) is the selected option's <see cref="RadioOption.Value"/>.
/// The <see cref="HeaderedSelectingItemsControl.Header"/> is the legend and the group's accessible name.
/// </summary>
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
public class RadioGroup : HeaderedSelectingItemsControl
{
    public const string HighlightPart = "PART_Highlight";

    private readonly TogglesGroupHost _host;
    private bool _usesHeaderName;

    static RadioGroup()
    {
        FocusableProperty.OverrideDefaultValue<RadioGroup>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<RadioGroup>(KeyboardNavigationMode.Once);
    }

    public RadioGroup()
    {
        _host = new TogglesGroupHost(this, flipInRtl: false);
        SelectedValueBinding = TogglesValueBinding.Create();
        SelectionChanged += (_, _) => _host.OnSelectionChanged();
    }

    protected override Type StyleKeyOverride => typeof(RadioGroup);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<RadioGroupItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new RadioGroupItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is RadioGroupItem row && item is RadioOption option)
        {
            row.Content = option.Label ?? option.Value?.ToString();
            row.Description = option.Description;
            row.IsEnabled = !option.IsDisabled;
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        _host.UpdateTabStop();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is RadioGroupItem row)
        {
            row.ClearValue(ContentControl.ContentProperty);
            row.ClearValue(RadioGroupItem.DescriptionProperty);
            row.ClearValue(IsEnabledProperty);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        if (highlight is not null)
        {
            highlight.PropertyChanged += (_, args) =>
            {
                // The fill is the stroke color at 5% (color-mix with transparent), which depends on the accent.
                if (args.Property == GlidingIndicator.StrokeProperty)
                {
                    highlight.Fill = TogglesBrushes.WithOpacity(highlight.Stroke, 0.05);
                }
            };
            highlight.Fill = TogglesBrushes.WithOpacity(highlight.Stroke, 0.05);
        }
        _host.AttachIndicator(highlight, inset: 0.5, fadeInFirst: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty)
        {
            var name = AutomationProperties.GetName(this);
            if (Header is string header && (string.IsNullOrEmpty(name) || _usesHeaderName))
            {
                AutomationProperties.SetName(this, header);
                _usesHeaderName = true;
            }
            else if (_usesHeaderName && Header is not string)
            {
                ClearValue(AutomationProperties.NameProperty);
                _usesHeaderName = false;
            }
        }
        else if (change.Property == IsEnabledProperty)
        {
            _host.UpdateTabStop();
        }
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TogglesGroupAutomationPeer(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_host.HandleKey(e))
        {
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _host.UpdateTabStop();
    }
}
