using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Arc expandable content: the <see cref="ExpandableText"/> clamp, Read more toggle and motion around any content —
/// selectable text, a read-only editor, rendered Markdown. The clamp ends on a line of the content's first visible
/// text (a <see cref="TextBlock"/>, <see cref="SelectableTextBlock"/> or <see cref="IExpandableLineSource"/>, found
/// through plain panels, decorators and presenters), which is laid out only past the clamp while collapsed. Content
/// without such text clamps at <c>Lines × LineHeight</c>: set <see cref="ExpandableClamp.LineHeight"/> for it.
/// The content takes the Arc body text style unless it sets its own.
/// </summary>
public class ExpandableContent : ExpandableClamp
{
    /// <summary>The content to clamp.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<ExpandableContent>();

    /// <summary>Template for <see cref="Content"/> when it is not a control.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<ExpandableContent>();

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    [DependsOn(nameof(ContentTemplate))]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
}
