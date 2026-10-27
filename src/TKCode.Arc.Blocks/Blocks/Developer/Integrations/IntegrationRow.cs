using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One app of the <see cref="Integrations"/> list (Arc design reference): the 20px mark, the name (a text button that opens
/// the drawer) with its category, the one-line description, and at the right the settings button (connected only) and the
/// status pill. At least 60 high with 10 / 20 padding; compact rows top-align, take two description lines, hide the
/// category and the settings button. The row itself is not clickable; its hover tint sits on an inner layer.
/// Tab stops: the name, then the settings button (connected) or the pill (idle, connecting, failed): one per action.
/// </summary>
[TemplatePart(NamePart, typeof(IntegrationNameButton))]
[TemplatePart(ConfigureHostPart, typeof(IntegrationReveal))]
[TemplatePart(ConfigurePart, typeof(Button))]
[TemplatePart(PillPart, typeof(IntegrationConnectPill))]
[PseudoClasses(CompactPseudoClass, TightPseudoClass, DividedPseudoClass, ConnectedPseudoClass)]
internal sealed class IntegrationRow : TemplatedControl
{
    internal const string NamePart = "PART_Name";
    internal const string ConfigureHostPart = "PART_ConfigureHost";
    internal const string ConfigurePart = "PART_Configure";
    internal const string PillPart = "PART_Pill";
    internal const string CompactPseudoClass = ":compact";
    internal const string TightPseudoClass = ":tight";
    internal const string DividedPseudoClass = ":divided";
    internal const string ConnectedPseudoClass = ":connected";

    /// <summary>The app.</summary>
    public static readonly StyledProperty<IntegrationItem?> ItemProperty =
        AvaloniaProperty.Register<IntegrationRow, IntegrationItem?>(nameof(Item));

    /// <summary>The category's label shown after the name.</summary>
    public static readonly StyledProperty<string?> CategoryTextProperty =
        AvaloniaProperty.Register<IntegrationRow, string?>(nameof(CategoryText));

    /// <summary>The app's name (mirrors the item).</summary>
    public static readonly StyledProperty<string?> AppNameProperty =
        AvaloniaProperty.Register<IntegrationRow, string?>(nameof(AppName));

    /// <summary>The description (mirrors the item).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<IntegrationRow, string?>(nameof(Description));

    /// <summary>The words of the row.</summary>
    public static readonly StyledProperty<IntegrationLabels?> LabelsProperty =
        AvaloniaProperty.Register<IntegrationRow, IntegrationLabels?>(nameof(Labels));

    /// <summary>The compact layout (the block is 480 wide or less).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<IntegrationRow, bool>(nameof(IsCompact));

    /// <summary>The tight paddings (the block is 520 wide or less).</summary>
    public static readonly StyledProperty<bool> IsTightProperty =
        AvaloniaProperty.Register<IntegrationRow, bool>(nameof(IsTight));

    /// <summary>Whether a line separates it from the row above (every row but the first).</summary>
    public static readonly StyledProperty<bool> ShowsDividerProperty =
        AvaloniaProperty.Register<IntegrationRow, bool>(nameof(ShowsDivider));

    private IntegrationItem? _observed;
    private IntegrationNameButton? _name;
    private IntegrationReveal? _configureHost;
    private Button? _configure;
    private IntegrationConnectPill? _pill;

    /// <inheritdoc cref="ItemProperty"/>
    public IntegrationItem? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

    /// <inheritdoc cref="CategoryTextProperty"/>
    public string? CategoryText { get => GetValue(CategoryTextProperty); set => SetValue(CategoryTextProperty, value); }

    /// <inheritdoc cref="AppNameProperty"/>
    public string? AppName { get => GetValue(AppNameProperty); set => SetValue(AppNameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public IntegrationLabels? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <inheritdoc cref="IsTightProperty"/>
    public bool IsTight { get => GetValue(IsTightProperty); set => SetValue(IsTightProperty, value); }

    /// <inheritdoc cref="ShowsDividerProperty"/>
    public bool ShowsDivider { get => GetValue(ShowsDividerProperty); set => SetValue(ShowsDividerProperty, value); }

    /// <summary>The block that owns the row (actions go to it).</summary>
    internal Integrations? Owner { get; set; }

    /// <summary>The name button (drawer trigger, first tab stop).</summary>
    internal IntegrationNameButton? NameButton => _name;

    /// <summary>The settings button.</summary>
    internal Button? ConfigureButton => _configure;

    /// <summary>Its presence wrapper.</summary>
    internal IntegrationReveal? ConfigureHost => _configureHost;

    /// <summary>The status pill.</summary>
    internal IntegrationConnectPill? Pill => _pill;

    /// <summary>The action that takes the focus after a connect: the settings button, or the name when it is hidden.</summary>
    internal Control? FocusAfterConnect => _configureHost is { IsShown: true, IsSuppressed: false } ? _configure : _name;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_name is not null)
        {
            _name.Click -= OnNameClick;
        }
        if (_configure is not null)
        {
            _configure.Click -= OnConfigureClick;
        }
        if (_pill is not null)
        {
            _pill.Click -= OnPillClick;
        }
        _name = e.NameScope.Find<IntegrationNameButton>(NamePart);
        _configureHost = e.NameScope.Find<IntegrationReveal>(ConfigureHostPart);
        _configure = e.NameScope.Find<Button>(ConfigurePart);
        _pill = e.NameScope.Find<IntegrationConnectPill>(PillPart);
        if (_name is not null)
        {
            _name.Click += OnNameClick;
        }
        if (_configure is not null)
        {
            _configure.Click += OnConfigureClick;
        }
        if (_pill is not null)
        {
            _pill.Click += OnPillClick;
        }
        SyncAll(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            if (_observed is not null)
            {
                _observed.PropertyChanged -= OnItemChanged;
            }
            _observed = Item;
            if (_observed is not null)
            {
                _observed.PropertyChanged += OnItemChanged;
            }
            SyncAll(animate: false);
        }
        else if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
            if (_configureHost is not null)
            {
                _configureHost.IsSuppressed = IsCompact;
            }
        }
        else if (change.Property == IsTightProperty)
        {
            PseudoClasses.Set(TightPseudoClass, IsTight);
        }
        else if (change.Property == ShowsDividerProperty)
        {
            PseudoClasses.Set(DividedPseudoClass, ShowsDivider);
        }
        else if (change.Property == LabelsProperty)
        {
            SyncAll(animate: false);
        }
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IntegrationItem.StatusProperty)
        {
            SyncStatus(animate: true);
        }
        else if (e.Property == IntegrationItem.NameProperty || e.Property == IntegrationItem.DescriptionProperty)
        {
            SyncText();
        }
    }

    private void SyncAll(bool animate)
    {
        SyncText();
        SyncStatus(animate);
        if (_configureHost is not null)
        {
            _configureHost.IsSuppressed = IsCompact;
        }
    }

    private void SyncText()
    {
        var labels = Labels ?? Integrations.DefaultLabels;
        AppName = Item?.Name;
        Description = Item?.Description;
        if (_name is not null)
        {
            IntegrationAutomation.Describe(_name, Item?.Name, Item?.Description);
        }
        if (_configure is not null)
        {
            _configure.Content = labels.Configure;
            IntegrationAutomation.Describe(_configure, labels.ConfigureName(Item?.Name ?? string.Empty), null);
        }
        if (_pill is not null)
        {
            _pill.Labels = labels;
            _pill.AppName = Item?.Name;
        }
    }

    private void SyncStatus(bool animate)
    {
        var status = Item?.Status ?? IntegrationStatus.Idle;
        PseudoClasses.Set(ConnectedPseudoClass, status == IntegrationStatus.Connected);
        if (_pill is not null)
        {
            _pill.Status = status;
        }
        _configureHost?.Show(status == IntegrationStatus.Connected, animate);
    }

    private void OnNameClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Item is { } item)
        {
            Owner?.OpenConfiguration(item, _name);
        }
    }

    private void OnConfigureClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Item is { } item)
        {
            Owner?.OpenConfiguration(item, _configure);
        }
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Item is { } item)
        {
            Owner?.ConnectFromUser(item, _pill);
        }
    }

    /// <summary>True when the keyboard focus is somewhere in this row.</summary>
    internal bool HasFocusWithin => IsKeyboardFocusWithin;

    /// <summary>Moves the focus to the row's first tab stop.</summary>
    internal bool FocusFirst(NavigationMethod method) => _name?.Focus(method) == true;
}
