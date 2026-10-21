using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One setting a <see cref="SettingsCommand"/> can find: its header, description, icon, group and search keywords.
/// Use <see cref="SettingsCommandToggle"/>, <see cref="SettingsCommandChoice"/> or <see cref="SettingsCommandAction"/>.
/// Every value is a styled property, so it can be bound; the palette follows changes made by the app.
/// </summary>
public abstract class SettingsCommandItem : AvaloniaObject
{
    /// <summary>The setting's name, shown in the row and searched first.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsCommandItem, string?>(nameof(Header));

    /// <summary>One line under the header (trimmed to a single line); also searched.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsCommandItem, string?>(nameof(Description));

    /// <summary>A 24×24 stroke icon (for example an <see cref="ArcIcons"/> member) shown in a tile before the header.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsCommandItem, Geometry?>(nameof(Icon));

    /// <summary>The group the row is listed under, such as "Appearance"; also searched. Without one it is listed under "Settings" when other settings have a group.</summary>
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<SettingsCommandItem, string?>(nameof(Group));

    /// <summary>Other words that find the setting (synonyms separated by spaces or commas), such as "dark light appearance".</summary>
    public static readonly StyledProperty<string?> KeywordsProperty =
        AvaloniaProperty.Register<SettingsCommandItem, string?>(nameof(Keywords));

    /// <summary>Whether the setting can be changed. Disabled rows are dimmed and skipped by the keyboard.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<SettingsCommandItem, bool>(nameof(IsEnabled), true);

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="GroupProperty"/>
    public string? Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    /// <inheritdoc cref="KeywordsProperty"/>
    public string? Keywords
    {
        get => GetValue(KeywordsProperty);
        set => SetValue(KeywordsProperty, value);
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>Raised when any value of the item changes (properties, or the options of a choice).</summary>
    internal event Action<SettingsCommandItem, AvaloniaProperty?>? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, change.Property);
    }

    /// <summary>Tells the palette that something not backed by a property changed (the options of a choice).</summary>
    private protected void RaiseChanged(AvaloniaProperty? property) => Changed?.Invoke(this, property);
}

/// <summary>An on / off setting. Enter or a click flips <see cref="IsChecked"/>; the row shows a switch.</summary>
public sealed class SettingsCommandToggle : SettingsCommandItem
{
    /// <summary>Whether the setting is on (two-way).</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<SettingsCommandToggle, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    /// <inheritdoc cref="IsCheckedProperty"/>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }
}

/// <summary>
/// A setting with a few named values. Enter or a click moves to the next option (after the last comes the first);
/// the left and right arrow keys, and the arrows beside the value, step without wrapping.
/// </summary>
public sealed class SettingsCommandChoice : SettingsCommandItem
{
    /// <summary>The values in order. In XAML a comma-separated string works: <c>Options="Light, Dark, System"</c>.</summary>
    public static readonly DirectProperty<SettingsCommandChoice, AvaloniaList<string>> OptionsProperty =
        AvaloniaProperty.RegisterDirect<SettingsCommandChoice, AvaloniaList<string>>(nameof(Options), o => o.Options, (o, v) => o.Options = v);

    /// <summary>The current value (two-way). When null it becomes the first option.</summary>
    public static readonly StyledProperty<string?> SelectedOptionProperty =
        AvaloniaProperty.Register<SettingsCommandChoice, string?>(nameof(SelectedOption), defaultBindingMode: BindingMode.TwoWay);

    private AvaloniaList<string> _options;

    /// <summary>Creates a choice without options.</summary>
    public SettingsCommandChoice()
    {
        _options = new AvaloniaList<string>();
        _options.CollectionChanged += OnOptionsCollectionChanged;
    }

    /// <inheritdoc cref="OptionsProperty"/>
    public AvaloniaList<string> Options
    {
        get => _options;
        set
        {
            var old = _options;
            value ??= new AvaloniaList<string>();
            if (SetAndRaise(OptionsProperty, ref _options, value))
            {
                old.CollectionChanged -= OnOptionsCollectionChanged;
                value.CollectionChanged += OnOptionsCollectionChanged;
                SelectFirstWhenEmpty();
            }
        }
    }

    /// <inheritdoc cref="SelectedOptionProperty"/>
    public string? SelectedOption
    {
        get => GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }

    /// <summary>The position of <see cref="SelectedOption"/> in <see cref="Options"/>, or -1.</summary>
    internal int SelectedIndex => SelectedOption is { } selected ? _options.IndexOf(selected) : -1;

    private void OnOptionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SelectFirstWhenEmpty();
        RaiseChanged(OptionsProperty);
    }

    private void SelectFirstWhenEmpty()
    {
        if (SelectedOption is null && _options.Count > 0)
        {
            SetCurrentValue(SelectedOptionProperty, _options[0]);
        }
    }
}

/// <summary>
/// A command such as "Change email" or "Sign out everywhere". Enter or a click runs <see cref="Command"/>, then the
/// palette raises <see cref="SettingsCommand.ItemInvoked"/>. The row shows the <see cref="Shortcut"/> in key caps
/// and, for links to another page, an arrow.
/// </summary>
public sealed class SettingsCommandAction : SettingsCommandItem
{
    /// <summary>The command to run.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SettingsCommandAction, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<SettingsCommandAction, object?>(nameof(CommandParameter));

    /// <summary>
    /// The app's shortcut for the action, shown in key caps, such as "mod+shift+d" (Command on a Mac, Ctrl elsewhere)
    /// or "Ctrl+,". The palette only shows it; the app handles the shortcut itself.
    /// </summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<SettingsCommandAction, string?>(nameof(Shortcut));

    /// <summary>Whether the action opens another page; the row shows an arrow.</summary>
    public static readonly StyledProperty<bool> IsNavigationProperty =
        AvaloniaProperty.Register<SettingsCommandAction, bool>(nameof(IsNavigation));

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut
    {
        get => GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    /// <inheritdoc cref="IsNavigationProperty"/>
    public bool IsNavigation
    {
        get => GetValue(IsNavigationProperty);
        set => SetValue(IsNavigationProperty, value);
    }
}
