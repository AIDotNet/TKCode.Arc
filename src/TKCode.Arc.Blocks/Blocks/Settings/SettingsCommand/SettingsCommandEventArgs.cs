using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="SettingsCommand.SettingChanged"/>.</summary>
public sealed class SettingsCommandChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SettingsCommandChangedEventArgs(RoutedEvent routedEvent, SettingsCommandItem item, object? value)
        : base(routedEvent)
    {
        Item = item;
        Value = value;
    }

    /// <summary>The setting that changed.</summary>
    public SettingsCommandItem Item { get; }

    /// <summary>Its new value: a <see cref="bool"/> for a toggle, the option <see cref="string"/> for a choice.</summary>
    public object? Value { get; }
}

/// <summary>Arguments of <see cref="SettingsCommand.ItemInvoked"/>.</summary>
public sealed class SettingsCommandItemEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SettingsCommandItemEventArgs(RoutedEvent routedEvent, SettingsCommandItem item)
        : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The item that was invoked.</summary>
    public SettingsCommandItem Item { get; }
}
