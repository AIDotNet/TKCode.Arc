using AtomUI.Controls;
using Avalonia;

namespace TKCode.Arc;

/// <summary>One person in an <see cref="AvatarGroup"/> (Arc <c>members[]</c> entry).</summary>
public class AvatarGroupMember : AvaloniaObject
{
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<AvatarGroupMember, string?>(nameof(DisplayName));

    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<AvatarGroupMember, ImageSource?>(nameof(Source));

    public static readonly StyledProperty<AvatarStatus> StatusProperty =
        AvaloniaProperty.Register<AvatarGroupMember, AvatarStatus>(nameof(Status));

    public AvatarGroupMember()
    {
    }

    public AvatarGroupMember(string displayName, ImageSource? source = null, AvatarStatus status = AvatarStatus.None)
    {
        DisplayName = displayName;
        Source = source;
        Status = status;
    }

    /// <summary>The person's name: initials, tooltip and accessible name.</summary>
    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    /// <summary>Optional photo; initials show while it loads or when it fails.</summary>
    public ImageSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>Optional online / offline dot.</summary>
    public AvatarStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public override string ToString() => DisplayName ?? string.Empty;
}
