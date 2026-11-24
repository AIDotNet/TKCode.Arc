using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TKCode.Arc;

/// <summary>Trailing content of <see cref="Sidebar"/>: the section headings' template and where the rows place theirs.</summary>
public partial class Sidebar
{
    /// <summary>
    /// Builds the content at the right end of each section heading's title row, such as an add or a sort button. The control
    /// gets the section's data as its <c>DataContext</c>: the object in <see cref="SectionsSource"/> the section was made
    /// from, otherwise the <see cref="SidebarSection"/> itself. It takes its own clicks (a click on it never opens or closes
    /// the section), fades with the title when the sidebar collapses to the icon rail, and Tab skips it there. A heading
    /// without a title (<see cref="SidebarSection.Header"/>) shows no title row, so its content does not show either.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> SectionHeaderTrailingTemplateProperty =
        AvaloniaProperty.Register<Sidebar, IDataTemplate?>(nameof(SectionHeaderTrailingTemplate));

    /// <summary>
    /// Where a row shows its trailing content (<see cref="SidebarItem.TrailingIcon"/>, or what <see cref="TrailingTemplate"/>
    /// builds): right after the label's text (<see cref="SidebarTrailingPlacement.AfterLabel"/>, the default) or at the end
    /// of the row (<see cref="SidebarTrailingPlacement.End"/>).
    /// </summary>
    public static readonly StyledProperty<SidebarTrailingPlacement> TrailingPlacementProperty =
        AvaloniaProperty.Register<Sidebar, SidebarTrailingPlacement>(nameof(TrailingPlacement));

    /// <inheritdoc cref="SectionHeaderTrailingTemplateProperty"/>
    public IDataTemplate? SectionHeaderTrailingTemplate
    {
        get => GetValue(SectionHeaderTrailingTemplateProperty);
        set => SetValue(SectionHeaderTrailingTemplateProperty, value);
    }

    /// <inheritdoc cref="TrailingPlacementProperty"/>
    public SidebarTrailingPlacement TrailingPlacement
    {
        get => GetValue(TrailingPlacementProperty);
        set => SetValue(TrailingPlacementProperty, value);
    }

    private void OnTrailingPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SectionHeaderTrailingTemplateProperty)
        {
            foreach (var sectionNode in _sections)
            {
                UpdateHeaderTrailing(sectionNode);
            }
        }
        else if (change.Property == TrailingPlacementProperty)
        {
            var atEnd = change.GetNewValue<SidebarTrailingPlacement>() == SidebarTrailingPlacement.End;
            foreach (var node in _byItem.Values)
            {
                if (node.Row is { } row)
                {
                    row.IsTrailingAtEnd = atEnd;
                }
            }
        }
    }

    /// <summary>Builds (or drops) the trailing content of a section heading from <see cref="SectionHeaderTrailingTemplate"/>.</summary>
    private void UpdateHeaderTrailing(SidebarNode sectionNode)
    {
        if (sectionNode.Header is not { } header)
        {
            return;
        }
        var data = SectionDataOf(sectionNode.Section);
        Control? content = null;
        if (SectionHeaderTrailingTemplate is { } template && template.Match(data) && template.Build(data) is { } built)
        {
            built.DataContext = data;
            content = built;
        }
        header.TrailingContent = content;
    }

    /// <summary>The object a section was made from in <see cref="SectionsSource"/>, otherwise the section itself.</summary>
    private object SectionDataOf(SidebarSection section)
    {
        foreach (var pair in _boundSections)
        {
            if (ReferenceEquals(pair.Value.Section, section))
            {
                return pair.Key;
            }
        }
        return section;
    }
}
