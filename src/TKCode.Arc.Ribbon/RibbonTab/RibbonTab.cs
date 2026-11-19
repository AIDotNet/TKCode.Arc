using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// One tab of a <see cref="Ribbon"/>: a header in the tab row and the <see cref="RibbonGroup"/>s shown under it while the tab
/// is selected. The groups sit side by side and shrink, step by step, when the ribbon is narrower than they are (see
/// <see cref="RibbonGroup.ReductionPriority"/>).
/// <para>
/// A tab with <see cref="IsContextual"/> is a tool tab that belongs to a selection (Table design, Node tools): its header is
/// drawn in the accent. Show and hide it with <c>IsVisible</c>; when the selected tab is hidden the ribbon selects the first
/// visible one.
/// </para>
/// </summary>
[TemplatePart(GroupsPanelPart, typeof(Control))]
public class RibbonTab : TemplatedControl
{
    public const string GroupsPanelPart = "PART_GroupsPanel";

    #region 公共属性定义

    /// <summary>The tab's name in the tab row.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<RibbonTab, string?>(nameof(Header));

    /// <summary>A tool tab that belongs to the current selection: its header is drawn in the accent (default false).</summary>
    public static readonly StyledProperty<bool> IsContextualProperty =
        AvaloniaProperty.Register<RibbonTab, bool>(nameof(IsContextual));

    /// <summary>Whether this is the ribbon's selected tab.</summary>
    public static readonly DirectProperty<RibbonTab, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<RibbonTab, bool>(nameof(IsSelected), o => o.IsSelected);

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="IsContextualProperty"/>
    public bool IsContextual
    {
        get => GetValue(IsContextualProperty);
        set => SetValue(IsContextualProperty, value);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
    }

    /// <summary>The tab's groups, left to right.</summary>
    [Content]
    public AvaloniaList<RibbonGroup> Groups { get; } = new();

    #endregion

    private bool _isSelected;
    private RibbonGroupsPanel? _panel;

    public RibbonTab()
    {
        Groups.CollectionChanged += OnGroupsChanged;
    }

    /// <summary>The panel that lays out the groups (null until the template is applied).</summary>
    internal RibbonGroupsPanel? GroupsPanel => _panel;

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var group in LogicalChildren.OfType<RibbonGroup>().ToArray())
            {
                LogicalChildren.Remove(group);
                _panel?.Remove(group);
            }
            foreach (var group in Groups)
            {
                LogicalChildren.Add(group);
            }
            FillPanel();
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (RibbonGroup group in e.OldItems)
                {
                    LogicalChildren.Remove(group);
                    _panel?.Remove(group);
                }
            }
            if (e.NewItems is not null)
            {
                var index = e.NewStartingIndex < 0 ? Groups.Count - e.NewItems.Count : e.NewStartingIndex;
                foreach (RibbonGroup group in e.NewItems)
                {
                    LogicalChildren.Add(group);
                    _panel?.Insert(index++, group);
                }
            }
        }
        this.FindLogicalAncestorOfType<Ribbon>()?.OnQuickAccessCatalogChanged();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_panel is not null)
        {
            foreach (var group in _panel.Groups.ToArray())
            {
                _panel.Remove(group);
            }
        }
        base.OnApplyTemplate(e);
        _panel = e.NameScope.Find<RibbonGroupsPanel>(GroupsPanelPart);
        if (_panel is not null)
        {
            _panel.Bind(RibbonGroupsPanel.SpacingProperty,
                this.GetResourceObservable(RibbonTabTokens.Key(RibbonTabTokenKind.GroupSpacing)), BindingPriority.Template);
        }
        FillPanel();
    }

    private void FillPanel()
    {
        if (_panel is null)
        {
            return;
        }
        foreach (var group in _panel.Groups.ToArray())
        {
            _panel.Remove(group);
        }
        for (var i = 0; i < Groups.Count; i++)
        {
            _panel.Insert(i, Groups[i]);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }
}
