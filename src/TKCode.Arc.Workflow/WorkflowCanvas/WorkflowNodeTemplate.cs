using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Content for the cards of one kind of step, written in XAML: put it in <see cref="WorkflowCanvas.NodeTemplates"/>.
/// It matches the steps whose <see cref="WorkflowNode.Kind"/> is <see cref="Kind"/> (every step when null), and its
/// content replaces the card's icon, title, subtitle and state; the data is the <see cref="WorkflowNode"/>. Write
/// <c>x:DataType="arc:WorkflowNode"</c> on it for compiled bindings.
/// </summary>
public class WorkflowNodeTemplate : ITypedDataTemplate
{
    /// <summary>The <see cref="WorkflowNode.Kind"/> of the steps this template is for; null matches every step.</summary>
    public string? Kind { get; set; }

    /// <summary>The type of the data, <see cref="WorkflowNode"/>.</summary>
    [DataType]
    public Type? DataType { get; set; } = typeof(WorkflowNode);

    /// <summary>The card's content, written in XAML.</summary>
    [Content]
    [TemplateContent]
    public object? Content { get; set; }

    /// <summary>Whether <paramref name="data"/> is a step of <see cref="Kind"/>.</summary>
    public bool Match(object? data) => data is WorkflowNode node && (Kind is null || node.Kind == Kind);

    /// <summary>Builds the content for a step.</summary>
    public Control? Build(object? param) => TemplateContent.Load(Content)?.Result;
}
