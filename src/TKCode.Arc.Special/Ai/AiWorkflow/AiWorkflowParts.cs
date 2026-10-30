using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A step's remove button: 24 × 24, a 14px x in TextMuted. On a fine pointer it shows only while its row is hovered
/// (class <c>revealed</c>) or while it has keyboard focus; on touch screens (class <c>touch</c>) it always shows.
/// </summary>
internal sealed class AiWorkflowIconButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(AiWorkflowIconButton);
}

/// <summary>One tab of a branch's paths: a radio in a group with roving focus (only the selected tab is a tab stop).</summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class AiWorkflowPathTab : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";

    private bool _isSelected;

    public AiWorkflowPathTab(string pathId)
    {
        PathId = pathId;
    }

    protected override Type StyleKeyOverride => typeof(AiWorkflowPathTab);

    public string PathId { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PseudoClasses.Set(SelectedPseudoClass, value);
            IsTabStop = value;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    private sealed class TabPeer(AiWorkflowPathTab owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;
    }
}

/// <summary>
/// A row of the add step menu or of the empty state's triggers: the app's mark, the title, the app or detail in muted
/// text and an optional shortcut number. The menu's highlight is drawn behind the rows by a gliding indicator; the empty
/// state's rows (class <c>trigger</c>) get their own hover fill and focus ring.
/// </summary>
internal sealed class AiWorkflowMenuRow : AvaloniaButton
{
    private readonly TextBlock _detail;

    public AiWorkflowMenuRow(AiWorkflowCatalogItem item, int? shortcut)
    {
        Item = item;
        Shortcut = shortcut;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Center;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 8 };
        grid.Children.Add(AiWorkflowViews.Mark(item.App, item.Kind));
        var title = AiWorkflowViews.Text(item.Title, 13, ArcDesignTokenKind.Foreground, medium: true);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        _detail = AiWorkflowViews.Text(item.Kind == AiWorkflowStepKind.Trigger ? item.Detail : item.App?.Name ?? item.Detail, 12,
            ArcDesignTokenKind.TextMuted);
        Grid.SetColumn(_detail, 2);
        grid.Children.Add(_detail);
        if (shortcut is { } n)
        {
            var key = new Border
            {
                MinWidth = 18,
                Height = 18,
                Padding = new Thickness(4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = AiWorkflowViews.Text(n.ToString(System.Globalization.CultureInfo.InvariantCulture), 11, ArcDesignTokenKind.TextMuted,
                    mono: true, center: true)
            }.BindCornerRadius(ArcDesignTokenKind.RadiusStep5);
            ArcTokens.Bind(key, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(key, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
            Grid.SetColumn(key, 3);
            grid.Children.Add(key);
        }
        Content = grid;
        AutomationProperties.SetName(this, item.App is { } app ? item.Title + ", " + app.Name : item.Title);
    }

    protected override Type StyleKeyOverride => typeof(AiWorkflowMenuRow);

    public AiWorkflowCatalogItem Item { get; }

    public int? Shortcut { get; }

    /// <summary>Hides the detail text (empty state rows under 480px).</summary>
    public bool ShowsDetail
    {
        get => _detail.IsVisible;
        set => _detail.IsVisible = value;
    }
}
