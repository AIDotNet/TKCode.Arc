using System.Globalization;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The color of an <see cref="InboxSidebarLabel"/>'s dot, from the theme's palette.</summary>
public enum InboxSidebarLabelTone
{
    /// <summary>The accent color.</summary>
    Accent,

    /// <summary>The first chart series color.</summary>
    Series1,

    /// <summary>The second chart series color.</summary>
    Series2,

    /// <summary>The third chart series color.</summary>
    Series3,

    /// <summary>The fourth chart series color.</summary>
    Series4,

    /// <summary>The success color (green).</summary>
    Success,

    /// <summary>The warning color (amber).</summary>
    Warning,

    /// <summary>The danger color (red).</summary>
    Danger,

    /// <summary>The muted text color (grey).</summary>
    Neutral
}

/// <summary>One mail folder of an <see cref="InboxSidebar"/>, such as Inbox or Sent.</summary>
public class InboxSidebarFolder : AvaloniaObject
{
    /// <summary>The folder's id; <see cref="InboxSidebar.SelectedKey"/> matches it.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<InboxSidebarFolder, string?>(nameof(Key));

    /// <summary>The folder name.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InboxSidebarFolder, string>(nameof(Title), string.Empty);

    /// <summary>The 24x24 Lucide icon (an <c>ArcIcons</c> glyph); it stays on the icon rail.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<InboxSidebarFolder, Geometry?>(nameof(Icon));

    /// <summary>The number at the end of the row (unread messages, or drafts); 0 shows nothing. Above 999 it shows "999+".</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<InboxSidebarFolder, int>(nameof(Count));

    /// <summary>Whether the count means unread mail: the row's label is stronger and the rail shows a dot. False for a plain count such as drafts.</summary>
    public static readonly StyledProperty<bool> IsCountUnreadProperty =
        AvaloniaProperty.Register<InboxSidebarFolder, bool>(nameof(IsCountUnread), true);

    public InboxSidebarFolder()
    {
    }

    public InboxSidebarFolder(string key, string title, Geometry? icon, int count = 0, bool isCountUnread = true)
    {
        Key = key;
        Title = title;
        Icon = icon;
        Count = count;
        IsCountUnread = isCountUnread;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <inheritdoc cref="IsCountUnreadProperty"/>
    public bool IsCountUnread { get => GetValue(IsCountUnreadProperty); set => SetValue(IsCountUnreadProperty, value); }

    public override string ToString() => Title;
}

/// <summary>One label of an <see cref="InboxSidebar"/>: a colored dot, a name and an unread count.</summary>
public class InboxSidebarLabel : AvaloniaObject
{
    /// <summary>The label's id; <see cref="InboxSidebar.SelectedKey"/> matches it.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<InboxSidebarLabel, string?>(nameof(Key));

    /// <summary>The label name.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InboxSidebarLabel, string>(nameof(Title), string.Empty);

    /// <summary>The dot's color from the theme's palette.</summary>
    public static readonly StyledProperty<InboxSidebarLabelTone> ToneProperty =
        AvaloniaProperty.Register<InboxSidebarLabel, InboxSidebarLabelTone>(nameof(Tone));

    /// <summary>A color of your own for the dot; it wins over <see cref="Tone"/>.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<InboxSidebarLabel, IBrush?>(nameof(Brush));

    /// <summary>Unread messages with this label; 0 shows nothing.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<InboxSidebarLabel, int>(nameof(Count));

    public InboxSidebarLabel()
    {
    }

    public InboxSidebarLabel(string key, string title, InboxSidebarLabelTone tone, int count = 0)
    {
        Key = key;
        Title = title;
        Tone = tone;
        Count = count;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public InboxSidebarLabelTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="BrushProperty"/>
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    public override string ToString() => Title;
}

/// <summary>Arguments of <see cref="InboxSidebar.ItemInvoked"/>: the folder or label that was chosen.</summary>
public sealed class InboxSidebarItemEventArgs : RoutedEventArgs
{
    public InboxSidebarItemEventArgs(RoutedEvent routedEvent, string? key, InboxSidebarFolder? folder, InboxSidebarLabel? label) : base(routedEvent)
    {
        Key = key;
        Folder = folder;
        Label = label;
    }

    /// <summary>The chosen key (now <see cref="InboxSidebar.SelectedKey"/>).</summary>
    public string? Key { get; }

    /// <summary>The folder, when a folder was chosen.</summary>
    public InboxSidebarFolder? Folder { get; }

    /// <summary>The label, when a label was chosen.</summary>
    public InboxSidebarLabel? Label { get; }
}

/// <summary>The built-in text of an <see cref="InboxSidebar"/>. Replace the whole object to localize it.</summary>
public sealed class InboxSidebarLabels
{
    /// <summary>The culture of the numbers (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The compose button.</summary>
    public string Compose { get; init; } = "Compose";

    /// <summary>The heading over the labels.</summary>
    public string LabelsHeading { get; init; } = "Labels";

    /// <summary>The row that creates a label.</summary>
    public string NewLabel { get; init; } = "New label";

    /// <summary>The button that collapses the sidebar to icons.</summary>
    public string Collapse { get; init; } = "Collapse sidebar";

    /// <summary>The button that opens the sidebar from the icon rail.</summary>
    public string Expand { get; init; } = "Expand sidebar";

    /// <summary>The storage line: (used, total) in gigabytes, formatted ⇒ "3.2 GB of 15 GB used".</summary>
    public Func<string, string, string> Storage { get; init; } = static (used, total) => $"{used} GB of {total} GB used";

    /// <summary>The accessible name of the storage meter.</summary>
    public string StorageName { get; init; } = "Storage";

    /// <summary>Added to the accessible name of a folder with unread mail.</summary>
    public string Unread { get; init; } = "unread";

    /// <summary>The navigation landmark's name.</summary>
    public string Navigation { get; init; } = "Mail";
}
