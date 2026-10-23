using System.Collections.Generic;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>AgentMemory.MemoryChanged</c>: a change was committed; <see cref="Memories"/> is the whole list after it.</summary>
public class MemoryChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The memory control.</param>
    /// <param name="change">What changed.</param>
    /// <param name="memories">The memories after the change.</param>
    public MemoryChangedEventArgs(RoutedEvent routedEvent, object source, MemoryChange change, IReadOnlyList<MemoryItem> memories)
        : base(routedEvent, source)
    {
        Change = change;
        Memories = memories;
    }

    /// <summary>What changed.</summary>
    public MemoryChange Change { get; }

    /// <summary>The whole list of memories after the change.</summary>
    public IReadOnlyList<MemoryItem> Memories { get; }
}

/// <summary>Arguments of <c>AgentMemory.SettingToggled</c>: one of the two switches changed.</summary>
public class MemorySettingToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The memory control.</param>
    /// <param name="setting">The switch that changed.</param>
    /// <param name="isOn">Its new state.</param>
    public MemorySettingToggledEventArgs(RoutedEvent routedEvent, object source, MemorySetting setting, bool isOn)
        : base(routedEvent, source)
    {
        Setting = setting;
        IsOn = isOn;
    }

    /// <summary>The switch that changed.</summary>
    public MemorySetting Setting { get; }

    /// <summary>Its new state.</summary>
    public bool IsOn { get; }
}

/// <summary>Arguments of <c>AgentMemory.SourceRequested</c>: the source text of a memory with a <see cref="MemoryItem.SourceUri"/> was pressed.</summary>
public class MemorySourceRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The memory control.</param>
    /// <param name="memory">The memory whose source was pressed.</param>
    public MemorySourceRequestedEventArgs(RoutedEvent routedEvent, object source, MemoryItem memory)
        : base(routedEvent, source)
    {
        Memory = memory;
    }

    /// <summary>The memory whose source was pressed; its <see cref="MemoryItem.SourceUri"/> is the link.</summary>
    public MemoryItem Memory { get; }
}
