using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One tool of an open server: its name (monospace) with a "Changes data" tag when it writes, its description, and a switch
/// at the end that offers or withholds it. The switch is named with the tool and described with its sentence. The row
/// follows its <see cref="McpTool"/>: a change from code moves the switch, and a flip of the switch is reported to the
/// owner (which writes the tool and raises the event).
/// </summary>
internal sealed class McpServerToolRow : Grid
{
    private readonly TextBlock _name;
    private readonly Border _tag;
    private readonly TextBlock _description;
    private readonly Switch _switch;
    private readonly Border _divider;
    private bool _syncing;

    public McpServerToolRow(McpTool tool, Action<McpTool, bool> toggled, McpServersLabels labels)
    {
        Tool = tool;
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        RowDefinitions = new RowDefinitions("Auto,Auto");

        _divider = McpServersUi.Divider();
        SetColumnSpan(_divider, 2);
        Children.Add(_divider);

        var text = new StackPanel { Spacing = 2, Margin = new Thickness(0, 9, 12, 9), VerticalAlignment = VerticalAlignment.Center };
        var head = new ArcShrinkRow { Spacing = 8 };
        _name = McpServersUi.Text(tool.Name, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, mono: true, medium: true, lineHeight: 18);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        ArcShrinkRow.SetIsShrinkable(_name, true);
        head.Children.Add(_name);
        _tag = McpServersUi.Tag(labels.ChangesDataTag, McpServersGlyphs.PencilLine);
        head.Children.Add(_tag);
        text.Children.Add(head);
        _description = McpServersUi.Text(tool.Description, ArcDesignTokenKind.TextSecondary);
        _description.TextWrapping = TextWrapping.Wrap;
        text.Children.Add(_description);
        SetRow(text, 1);
        Children.Add(text);

        _switch = new Switch { VerticalAlignment = VerticalAlignment.Center };
        SetRow(_switch, 1);
        SetColumn(_switch, 1);
        Children.Add(_switch);
        _switch.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing)
            {
                toggled(Tool, _switch.IsChecked == true);
            }
        };

        Sync(labels);
    }

    public McpTool Tool { get; }

    public Switch Toggle => _switch;

    /// <summary>Whether a line separates it from the tool above.</summary>
    public bool ShowsDivider
    {
        get => _divider.IsVisible;
        set => _divider.IsVisible = value;
    }

    /// <summary>Copies the tool into the row (name, description, tag, switch, accessible names).</summary>
    public void Sync(McpServersLabels labels)
    {
        _syncing = true;
        try
        {
            _name.Text = Tool.Name;
            _description.Text = Tool.Description;
            _description.IsVisible = !string.IsNullOrWhiteSpace(Tool.Description);
            _tag.IsVisible = Tool.ChangesData;
            ((TextBlock)((StackPanel)_tag.Child!).Children[1]).Text = labels.ChangesDataTag;
            _switch.IsChecked = Tool.IsEnabled;
            AutomationProperties.SetName(_switch, Tool.ChangesData ? $"{Tool.Name}, {labels.ChangesDataTag}" : Tool.Name);
            AutomationProperties.SetHelpText(_switch, Tool.Description);
        }
        finally
        {
            _syncing = false;
        }
    }
}
