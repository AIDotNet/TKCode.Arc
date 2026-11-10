using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>One section of a <see cref="TocProgress"/>: the words it shows, its depth and the heading it scrolls to.</summary>
public class TocProgressEntry : AvaloniaObject
{
    /// <summary>The words shown in the list (usually the heading's own text).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TocProgressEntry, string?>(nameof(Title));

    /// <summary>The depth: 1 for a top-level section, 2 and 3 for sub-sections (indented). Values are kept between 1 and 3.</summary>
    public static readonly StyledProperty<int> LevelProperty =
        AvaloniaProperty.Register<TocProgressEntry, int>(nameof(Level), 1, coerce: (_, value) => Math.Clamp(value, 1, 3));

    /// <summary>The heading element inside the scrolled article that starts this section.</summary>
    public static readonly StyledProperty<Control?> TargetProperty =
        AvaloniaProperty.Register<TocProgressEntry, Control?>(nameof(Target));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="LevelProperty"/>
    public int Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <inheritdoc cref="TargetProperty"/>
    public Control? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    internal TocProgress? Owner { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Owner?.OnEntryChanged(this);
    }
}

/// <summary>Arguments of <see cref="TocProgress.ActiveChanged"/>: the section now being read.</summary>
public sealed class TocProgressActiveChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TocProgressActiveChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex, TocProgressEntry? entry)
        : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Entry = entry;
    }

    /// <summary>The section that was active before, or -1.</summary>
    public int OldIndex { get; }

    /// <summary>The section that is active now, or -1 before the first heading.</summary>
    public int NewIndex { get; }

    /// <summary>The active entry, or null before the first heading.</summary>
    public TocProgressEntry? Entry { get; }
}

/// <summary>
/// The words a <see cref="TocProgress"/> shows and announces, in English by default. Replace the instance or set its
/// properties to translate; texts with numbers in them are functions so the word order can change.
/// </summary>
public sealed class TocProgressLabels
{
    /// <summary>The heading over the list, and the list's accessible name.</summary>
    public string Title { get; set; } = "On this page";

    /// <summary>The percentage beside the heading: the whole number read, "42%".</summary>
    public Func<int, string> Percent { get; set; } = percent => $"{percent}%";

    /// <summary>Read after the name of the section being read.</summary>
    public string Current { get; set; } = "current section";

    /// <summary>Read after the name of a section that has been read.</summary>
    public string Read { get; set; } = "read";

    /// <summary>The compact button's accessible name: the active section and the percentage, "Contents, Setup, 42% read".</summary>
    public Func<string?, int, string> CompactName { get; set; } =
        (section, percent) => section is null ? $"Contents, {percent}% read" : $"Contents, {section}, {percent}% read";

    /// <summary>The compact button's text before the first section is reached.</summary>
    public string CompactIdle { get; set; } = "Contents";
}
