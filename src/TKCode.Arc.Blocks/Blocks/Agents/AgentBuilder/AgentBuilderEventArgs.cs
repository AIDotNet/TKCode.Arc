using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which part of the settings an edit touched.</summary>
public enum AgentBuilderField
{
    /// <summary>The name.</summary>
    Name,

    /// <summary>The description.</summary>
    Description,

    /// <summary>The instructions.</summary>
    Instructions,

    /// <summary>The model.</summary>
    Model,

    /// <summary>A tool was switched on or off.</summary>
    Tools,

    /// <summary>A knowledge source was added or removed.</summary>
    Sources,

    /// <summary>Discard brought back the saved settings.</summary>
    All
}

/// <summary>Arguments of <c>AgentBuilder.ConfigChanged</c>: the draft changed.</summary>
public class AgentBuilderConfigChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The builder.</param>
    /// <param name="field">What changed.</param>
    /// <param name="config">The draft after the change.</param>
    /// <param name="isDirty">Whether the draft now differs from the saved settings.</param>
    public AgentBuilderConfigChangedEventArgs(RoutedEvent routedEvent, object source, AgentBuilderField field, AgentBuilderConfig config, bool isDirty)
        : base(routedEvent, source)
    {
        Field = field;
        Config = config;
        IsDirty = isDirty;
    }

    /// <summary>What changed.</summary>
    public AgentBuilderField Field { get; }

    /// <summary>The draft after the change.</summary>
    public AgentBuilderConfig Config { get; }

    /// <summary>Whether the draft differs from the saved settings.</summary>
    public bool IsDirty { get; }
}

/// <summary>Arguments of <c>AgentBuilder.Saved</c> and <c>AgentBuilder.Published</c>.</summary>
public class AgentBuilderSavedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The builder.</param>
    /// <param name="config">What was saved or published.</param>
    public AgentBuilderSavedEventArgs(RoutedEvent routedEvent, object source, AgentBuilderConfig config)
        : base(routedEvent, source)
    {
        Config = config;
    }

    /// <summary>What was saved or published.</summary>
    public AgentBuilderConfig Config { get; }
}

/// <summary>Arguments of <c>AgentBuilder.SourceIndexed</c>: a knowledge source finished indexing or failed.</summary>
public class AgentBuilderSourceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The builder.</param>
    /// <param name="item">The source.</param>
    public AgentBuilderSourceEventArgs(RoutedEvent routedEvent, object source, AgentBuilderSource item)
        : base(routedEvent, source)
    {
        Item = item;
    }

    /// <summary>The source; its <see cref="AgentBuilderSource.Status"/> says how it ended.</summary>
    public AgentBuilderSource Item { get; }
}
