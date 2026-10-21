// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli): the item shape (CommandItem)
// and the filter and grouping rules of command-palette.tsx.

using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One command of a <see cref="CommandPalette"/>: a label with an optional description, group, icon, shortcut and
/// search keywords. Choosing it raises <see cref="CommandPalette.ItemSelected"/> and runs its <see cref="Command"/>.
/// </summary>
public class CommandPaletteItem : AvaloniaObject
{
    /// <summary>Stable id. Rows keep their place (and glide) across filtering and data updates by this id; empty uses the item itself.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<CommandPaletteItem, string>(nameof(Id), string.Empty);

    /// <summary>The command's name, shown in medium weight and searched.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CommandPaletteItem, string>(nameof(Label), string.Empty);

    /// <summary>A short muted note after the label on the same line; also searched.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<CommandPaletteItem, string?>(nameof(Description));

    /// <summary>The heading the command is listed under; null files it under <see cref="CommandPalette.DefaultGroup"/>. Also searched.</summary>
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<CommandPaletteItem, string?>(nameof(Group));

    /// <summary>Extra words that also find this command (replace the list to change them).</summary>
    public static readonly StyledProperty<IList<string>?> KeywordsProperty =
        AvaloniaProperty.Register<CommandPaletteItem, IList<string>?>(nameof(Keywords));

    /// <summary>The 16px leading icon: a 24×24 Lucide <see cref="Avalonia.Media.Geometry"/> or any control. Default: Lucide Command.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<CommandPaletteItem, object?>(nameof(Icon));

    /// <summary>Key cap text shown at the end of the row, such as "⌘ N" or "G D". The palette does not bind it.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<CommandPaletteItem, string?>(nameof(Shortcut));

    /// <summary>False dims the row; it cannot be highlighted or chosen.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<CommandPaletteItem, bool>(nameof(IsEnabled), true);

    /// <summary>Executed (when it can execute) after the command is chosen.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<CommandPaletteItem, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<CommandPaletteItem, object?>(nameof(CommandParameter));

    /// <summary>Any value you want back when the command is chosen.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<CommandPaletteItem, object?>(nameof(Tag));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="GroupProperty"/>
    public string? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    /// <inheritdoc cref="KeywordsProperty"/>
    public IList<string>? Keywords { get => GetValue(KeywordsProperty); set => SetValue(KeywordsProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    private string? _haystack;

    /// <summary>The lower-cased text the filter looks in (label, description, group and keywords joined by spaces), built once.</summary>
    internal string Haystack => _haystack ??= BuildHaystack();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == DescriptionProperty || change.Property == GroupProperty ||
            change.Property == KeywordsProperty)
        {
            _haystack = null;
        }
    }

    private string BuildHaystack()
    {
        // The fields are joined (empty ones left out), so a query may run across two of them, as in Arc.
        var builder = new System.Text.StringBuilder();
        Append(builder, Label);
        Append(builder, Description);
        Append(builder, Group);
        if (Keywords is { } keywords)
        {
            foreach (var keyword in keywords)
            {
                Append(builder, keyword);
            }
        }
        return builder.ToString().ToLowerInvariant();
    }

    private static void Append(System.Text.StringBuilder builder, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }
        builder.Append(text);
    }

    /// <summary>Returns the label.</summary>
    public override string ToString() => Label;
}

/// <summary>Arguments of <see cref="CommandPalette.ItemSelected"/>.</summary>
public sealed class CommandPaletteItemSelectedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>.</summary>
    public CommandPaletteItemSelectedEventArgs(RoutedEvent routedEvent, CommandPaletteItem item)
        : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The chosen command.</summary>
    public CommandPaletteItem Item { get; }
}

/// <summary>One heading of the result list and its commands, in list order.</summary>
internal sealed class CommandPaletteGroup
{
    public CommandPaletteGroup(string name) => Name = name;

    public string Name { get; }

    public List<CommandPaletteItem> Items { get; } = new();
}

/// <summary>Arc's filter: the trimmed, lower-cased query is a substring of the label, description, group and keywords joined by spaces.</summary>
internal static class CommandPaletteFilter
{
    /// <summary>Rows glide to their new place when every remaining row moves at most this many places (Arc's <c>GLIDE_ROWS</c>).</summary>
    public const int GlideRows = 6;

    public static string Normalize(string? query) => (query ?? string.Empty).Trim().ToLowerInvariant();

    public static bool Matches(CommandPaletteItem item, string normalized)
        => normalized.Length == 0 || item.Haystack.Contains(normalized, StringComparison.Ordinal);

    /// <summary>Filters <paramref name="items"/> and groups the matches in the order each group first appears among them.</summary>
    public static List<CommandPaletteGroup> Group(IReadOnlyList<CommandPaletteItem> items, string normalized, string defaultGroup)
    {
        var groups = new List<CommandPaletteGroup>();
        var index = new Dictionary<string, CommandPaletteGroup>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null || !Matches(item, normalized))
            {
                continue;
            }
            var name = string.IsNullOrEmpty(item.Group) ? defaultGroup : item.Group;
            if (!index.TryGetValue(name, out var group))
            {
                group = new CommandPaletteGroup(name);
                index.Add(name, group);
                groups.Add(group);
            }
            group.Items.Add(item);
        }
        return groups;
    }

    /// <summary>
    /// True when every row of <paramref name="next"/> sits within <see cref="GlideRows"/> places of where it was in
    /// <paramref name="previous"/> (new rows count as staying put): the list then closes its gaps with a glide. A longer
    /// jump snaps like a search result list instead of streaking rows through the frame.
    /// </summary>
    public static bool ShouldGlide(IReadOnlyList<CommandPaletteItem> previous, IReadOnlyList<CommandPaletteItem> next)
    {
        if (previous.Count == 0 || next.Count == 0)
        {
            return true;
        }
        // Filtering keeps the order, so the new list is normally a subsequence of the old one: walk both once, allocating nothing.
        var j = 0;
        var subsequence = true;
        for (var i = 0; i < next.Count; i++)
        {
            var key = KeyOf(next[i]);
            while (j < previous.Count && !Equals(KeyOf(previous[j]), key))
            {
                j++;
            }
            if (j == previous.Count)
            {
                subsequence = false;
                break;
            }
            if (Math.Abs(j - i) > GlideRows)
            {
                return false;
            }
            j++;
        }
        return subsequence || ShouldGlideReordered(previous, next);
    }

    private static bool ShouldGlideReordered(IReadOnlyList<CommandPaletteItem> previous, IReadOnlyList<CommandPaletteItem> next)
    {
        var before = new Dictionary<object, int>(previous.Count);
        for (var i = 0; i < previous.Count; i++)
        {
            before.TryAdd(KeyOf(previous[i]), i);
        }
        for (var i = 0; i < next.Count; i++)
        {
            var was = before.TryGetValue(KeyOf(next[i]), out var at) ? at : i;
            if (Math.Abs(was - i) > GlideRows)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The identity of a row: the id when there is one, otherwise the item itself.</summary>
    public static object KeyOf(CommandPaletteItem item) => string.IsNullOrEmpty(item.Id) ? item : item.Id;
}
