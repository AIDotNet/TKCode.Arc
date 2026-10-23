using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>AiComposer.Sent</c>: the person sent the prompt.</summary>
public class AiComposerSentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The composer.</param>
    /// <param name="message">What was sent.</param>
    public AiComposerSentEventArgs(RoutedEvent routedEvent, object source, AiComposerMessage message)
        : base(routedEvent, source)
    {
        Message = message;
    }

    /// <summary>The prompt, its tokens, the ready attachments, the model, the effort and the tools.</summary>
    public AiComposerMessage Message { get; }
}

/// <summary>Arguments of <c>AiComposer.AttachmentAdded</c> and <c>AttachmentRemoved</c>.</summary>
public class AiAttachmentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The composer.</param>
    /// <param name="attachment">The attachment that was added or removed.</param>
    public AiAttachmentEventArgs(RoutedEvent routedEvent, object source, AiAttachment attachment)
        : base(routedEvent, source)
    {
        Attachment = attachment;
    }

    /// <summary>The attachment that was added or removed.</summary>
    public AiAttachment Attachment { get; }
}

/// <summary>Arguments of <c>AiComposer.ToolToggled</c>.</summary>
public class AiComposerToolToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The composer.</param>
    /// <param name="tool">The tool that changed.</param>
    public AiComposerToolToggledEventArgs(RoutedEvent routedEvent, object source, AiComposerTool tool)
        : base(routedEvent, source)
    {
        Tool = tool;
    }

    /// <summary>The tool that changed; <see cref="AiComposerTool.IsChecked"/> is its new state.</summary>
    public AiComposerTool Tool { get; }
}
