using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One bulk action of a <see cref="SelectionToolbar"/>.</summary>
public class SelectionToolbarAction : AvaloniaObject
{
    /// <summary>A stable id you get back in <see cref="SelectionToolbar.ActionInvoked"/>.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<SelectionToolbarAction, string?>(nameof(Key));

    /// <summary>The button text; also the tooltip and accessible name when the bar shows icons only.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SelectionToolbarAction, string>(nameof(Label), string.Empty);

    /// <summary>A 24x24 Lucide icon before the label (for example an <c>ArcIcons</c> glyph).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SelectionToolbarAction, Geometry?>(nameof(Icon));

    /// <summary>Marks a destructive action (Delete): it is drawn in the danger color and set apart at the end.</summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<SelectionToolbarAction, bool>(nameof(IsDestructive));

    /// <summary>Whether the action can run for the current selection.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<SelectionToolbarAction, bool>(nameof(IsEnabled), true);

    /// <summary>Runs when the action is chosen; the parameter is the selected count.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SelectionToolbarAction, ICommand?>(nameof(Command));

    public SelectionToolbarAction()
    {
    }

    public SelectionToolbarAction(string key, string label, Geometry? icon = null, bool isDestructive = false)
    {
        Key = key;
        Label = label;
        Icon = icon;
        IsDestructive = isDestructive;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IsDestructiveProperty"/>
    public bool IsDestructive { get => GetValue(IsDestructiveProperty); set => SetValue(IsDestructiveProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public override string ToString() => Label;
}

/// <summary>Arguments of <see cref="SelectionToolbar.ActionInvoked"/>.</summary>
public sealed class SelectionToolbarActionEventArgs : RoutedEventArgs
{
    public SelectionToolbarActionEventArgs(RoutedEvent routedEvent, SelectionToolbarAction action, int selectedCount) : base(routedEvent)
    {
        Action = action;
        SelectedCount = selectedCount;
    }

    /// <summary>The action that was chosen.</summary>
    public SelectionToolbarAction Action { get; }

    /// <summary>How many items were selected when it was chosen.</summary>
    public int SelectedCount { get; }
}

/// <summary>
/// The built-in text of a <see cref="SelectionToolbar"/>. Replace the whole object (with an object initializer) to
/// localize or reword it.
/// </summary>
public sealed class SelectionToolbarLabels
{
    /// <summary>The culture of the numbers (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The count: formatted number ⇒ "3 selected".</summary>
    public Func<string, string> Count { get; init; } = static count => $"{count} selected";

    /// <summary>The button that selects everything: formatted total ⇒ "Select all 48".</summary>
    public Func<string, string> SelectAll { get; init; } = static total => $"Select all {total}";

    /// <summary>The clear button's accessible name and tooltip.</summary>
    public string Clear { get; init; } = "Clear selection";

    /// <summary>The bar's accessible name.</summary>
    public string Toolbar { get; init; } = "Bulk actions";

    /// <summary>The built-in actions shown while <see cref="SelectionToolbar.Actions"/> is empty.</summary>
    public Func<IReadOnlyList<SelectionToolbarAction>> DefaultActions { get; init; } = static () =>
    [
        new SelectionToolbarAction("archive", "Archive", ArcIcons.Archive),
        new SelectionToolbarAction("move", "Move", ArcIcons.FolderOpen),
        new SelectionToolbarAction("share", "Share", ArcIcons.Share2),
        new SelectionToolbarAction("delete", "Delete", ArcIcons.Trash2, isDestructive: true)
    ];
}
