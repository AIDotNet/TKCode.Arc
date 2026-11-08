using System.ComponentModel;
using System.Runtime.CompilerServices;

// An enum of another assembly: the generator describes it from this attribute.
[assembly: TKCode.Arc.EnumMetadata(typeof(System.DayOfWeek))]

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A plain enum: one member at a time.</summary>
[EnumMetadata]
public enum DemoPriority
{
    [Description("Low")] Low,
    [Description("Medium")] Medium,
    [Description("High")] High,
    [Description("Critical")] Critical
}

/// <summary>A [Flags] enum with a zero member and composite members.</summary>
[Flags, EnumMetadata]
public enum DemoFilePermissions
{
    [Description("No access")] None = 0,
    [Description("Read")] Read = 1,
    [Description("Write")] Write = 2,
    [Description("Execute")] Execute = 4,
    [Description("Delete")] Delete = 8,
    [Description("Read and write")] ReadWrite = Read | Write,
    [Description("Full control")] FullControl = Read | Write | Execute | Delete
}

/// <summary>Members whose code names differ from their descriptions.</summary>
[EnumMetadata]
public enum DemoShipping
{
    [Description("Standard (3 to 5 days)")] Standard,
    [Description("Express (next day)")] Express,
    [Description("Pick up in store")] Pickup
}

/// <summary>The view model of the binding demo.</summary>
public sealed class EnumSelectorDemoState : INotifyPropertyChanged
{
    private DemoPriority _priority = DemoPriority.Medium;
    private DemoFilePermissions _permissions = DemoFilePermissions.Read | DemoFilePermissions.Execute;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DemoPriority Priority
    {
        get => _priority;
        set => Set(ref _priority, value);
    }

    public DemoFilePermissions Permissions
    {
        get => _permissions;
        set => Set(ref _permissions, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
