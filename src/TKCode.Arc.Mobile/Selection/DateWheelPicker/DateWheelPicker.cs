using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>Which wheel columns a <see cref="DateWheelPicker"/> shows.</summary>
public enum DateWheelMode
{
    /// <summary>Year, month and day wheels.</summary>
    Date,

    /// <summary>Hour (00–23) and minute wheels.</summary>
    Time,

    /// <summary>Year, month, day, hour and minute wheels.</summary>
    DateTime,
}

/// <summary>Event data of <see cref="DateWheelPicker.ValueChanged"/>.</summary>
public sealed class DateWheelValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public DateWheelValueChangedEventArgs(RoutedEvent routedEvent, DateTime? oldValue, DateTime? newValue)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The previous value, or null.</summary>
    public DateTime? OldValue { get; }

    /// <summary>The confirmed value, or null.</summary>
    public DateTime? NewValue { get; }
}

/// <summary>
/// A mobile list row for picking a date, time or date-and-time using <see cref="WheelPicker"/> columns inside a bottom
/// <see cref="Sheet"/>. Derives from <see cref="ListItem"/> so it fits directly inside a <see cref="ListGroup"/>, mounts
/// its sheet on the nearest <see cref="Page"/> (inheriting safe area and Android system back handling), and keeps day
/// counts valid for leap years and short months.
/// </summary>
[PseudoClasses(":placeholder")]
public class DateWheelPicker : ListItem
{
    /// <summary>Selected date/time value (two-way, null shows <see cref="PlaceholderText"/>).</summary>
    public static readonly StyledProperty<DateTime?> SelectedDateTimeProperty =
        AvaloniaProperty.Register<DateWheelPicker, DateTime?>(nameof(SelectedDateTime), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Which columns to present: <see cref="DateWheelMode.Date"/> (default), <see cref="DateWheelMode.Time"/> or <see cref="DateWheelMode.DateTime"/>.</summary>
    public static readonly StyledProperty<DateWheelMode> ModeProperty =
        AvaloniaProperty.Register<DateWheelPicker, DateWheelMode>(nameof(Mode));

    /// <summary>Earliest selectable year in <see cref="DateWheelMode.Date"/> and <see cref="DateWheelMode.DateTime"/> modes: 1920.</summary>
    public static readonly StyledProperty<int> MinYearProperty =
        AvaloniaProperty.Register<DateWheelPicker, int>(nameof(MinYear), 1920);

    /// <summary>Latest selectable year in <see cref="DateWheelMode.Date"/> and <see cref="DateWheelMode.DateTime"/> modes: 2100.</summary>
    public static readonly StyledProperty<int> MaxYearProperty =
        AvaloniaProperty.Register<DateWheelPicker, int>(nameof(MaxYear), 2100);

    /// <summary>Minute step interval (1 to 30): 1.</summary>
    public static readonly StyledProperty<int> MinuteIntervalProperty =
        AvaloniaProperty.Register<DateWheelPicker, int>(nameof(MinuteInterval), 1);

    /// <summary>Text shown at the row's end while <see cref="SelectedDateTime"/> is null: "Not set".</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<DateWheelPicker, string?>(nameof(PlaceholderText), "Not set");

    /// <summary>Optional format string for the row readout (null chooses a mode-appropriate format).</summary>
    public static readonly StyledProperty<string?> FormatStringProperty =
        AvaloniaProperty.Register<DateWheelPicker, string?>(nameof(FormatString));

    /// <summary>Optional title in the sheet header (null falls back to the row's string content).</summary>
    public static readonly StyledProperty<string?> SheetTitleProperty =
        AvaloniaProperty.Register<DateWheelPicker, string?>(nameof(SheetTitle));

    /// <summary>Label of the sheet's cancel button: "Cancel".</summary>
    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<DateWheelPicker, string?>(nameof(CancelText), "Cancel");

    /// <summary>Label of the sheet's confirm button: "Done".</summary>
    public static readonly StyledProperty<string?> ConfirmTextProperty =
        AvaloniaProperty.Register<DateWheelPicker, string?>(nameof(ConfirmText), "Done");

    /// <summary>Whether the wheel sheet is currently open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<DateWheelPicker, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Formatted readout text shown at the trailing edge of the row.</summary>
    public static readonly DirectProperty<DateWheelPicker, string?> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<DateWheelPicker, string?>(nameof(DisplayText), p => p.DisplayText);

    /// <summary>Raised whenever <see cref="SelectedDateTime"/> changes.</summary>
    public static readonly RoutedEvent<DateWheelValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<DateWheelPicker, DateWheelValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    private Sheet? _sheet;
    private Page? _hostPage;
    private Panel? _hostLayer;
    private Button? _cancelButton;
    private Button? _confirmButton;
    private TextBlock? _titleBlock;
    private Grid? _wheelsGrid;
    private WheelPicker? _yearWheel;
    private WheelPicker? _monthWheel;
    private WheelPicker? _dayWheel;
    private WheelPicker? _hourWheel;
    private WheelPicker? _minuteWheel;
    private bool _rebuildingDays;
    private string? _displayText = "Not set";

    static DateWheelPicker()
    {
        ShowChevronProperty.OverrideDefaultValue<DateWheelPicker>(true);
    }

    /// <inheritdoc cref="SelectedDateTimeProperty"/>
    public DateTime? SelectedDateTime
    {
        get => GetValue(SelectedDateTimeProperty);
        set => SetValue(SelectedDateTimeProperty, value);
    }

    /// <inheritdoc cref="ModeProperty"/>
    public DateWheelMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <inheritdoc cref="MinYearProperty"/>
    public int MinYear
    {
        get => GetValue(MinYearProperty);
        set => SetValue(MinYearProperty, value);
    }

    /// <inheritdoc cref="MaxYearProperty"/>
    public int MaxYear
    {
        get => GetValue(MaxYearProperty);
        set => SetValue(MaxYearProperty, value);
    }

    /// <inheritdoc cref="MinuteIntervalProperty"/>
    public int MinuteInterval
    {
        get => GetValue(MinuteIntervalProperty);
        set => SetValue(MinuteIntervalProperty, value);
    }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <inheritdoc cref="FormatStringProperty"/>
    public string? FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    /// <inheritdoc cref="SheetTitleProperty"/>
    public string? SheetTitle
    {
        get => GetValue(SheetTitleProperty);
        set => SetValue(SheetTitleProperty, value);
    }

    /// <inheritdoc cref="CancelTextProperty"/>
    public string? CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    /// <inheritdoc cref="ConfirmTextProperty"/>
    public string? ConfirmText
    {
        get => GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="DisplayTextProperty"/>
    public string? DisplayText => _displayText;

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<DateWheelValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>The hosted wheel sheet once opened, for tests.</summary>
    internal Sheet? PickerSheet => _sheet;

    /// <summary>Commits the wheel selection currently shown in the sheet (same as tapping Done).</summary>
    internal void CommitCurrentWheels()
    {
        if (_sheet is null)
        {
            return;
        }
        SetCurrentValue(SelectedDateTimeProperty, ReadCurrentWheels());
        SetCurrentValue(IsOpenProperty, false);
    }

    protected override Type StyleKeyOverride => typeof(DateWheelPicker);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateDisplay();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_sheet is not null)
        {
            _sheet.IsOpen = false;
            if (_hostPage?.IsAttachedToVisualTree() == true || _hostLayer?.IsAttachedToVisualTree() == true)
            {
                Unmount();
            }
        }
        SetCurrentValue(IsOpenProperty, false);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (!IsOpen)
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateTimeProperty)
        {
            UpdateDisplay();
            var oldVal = change.GetOldValue<DateTime?>();
            var newVal = change.GetNewValue<DateTime?>();
            if (oldVal != newVal)
            {
                RaiseEvent(new DateWheelValueChangedEventArgs(ValueChangedEvent, oldVal, newVal));
            }
        }
        else if (change.Property == ModeProperty || change.Property == PlaceholderTextProperty || change.Property == FormatStringProperty)
        {
            UpdateDisplay();
            if (IsOpen)
            {
                BuildWheels();
            }
        }
        else if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else if (_sheet is not null)
            {
                _sheet.IsOpen = false;
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled && IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        else if (change.Property == SheetTitleProperty || change.Property == CancelTextProperty
                 || change.Property == ConfirmTextProperty || change.Property == ContentProperty)
        {
            UpdateSheetChrome();
        }
    }

    private void UpdateDisplay()
    {
        var value = SelectedDateTime;
        var placeholder = value is null;
        PseudoClasses.Set(":placeholder", placeholder);
        string? text;
        if (placeholder)
        {
            text = PlaceholderText;
        }
        else
        {
            var format = FormatString ?? Mode switch
            {
                DateWheelMode.Time => "HH:mm",
                DateWheelMode.DateTime => "yyyy-MM-dd HH:mm",
                _ => "yyyy-MM-dd",
            };
            text = value!.Value.ToString(format, CultureInfo.CurrentCulture);
        }
        SetAndRaise(DisplayTextProperty, ref _displayText, text);
        AutomationProperties.SetHelpText(this, text);
    }

    private Sheet EnsureSheet()
    {
        if (_sheet is not null)
        {
            return _sheet;
        }
        _cancelButton = new Button { Variant = ButtonVariant.Plain, Content = CancelText };
        _cancelButton.Click += (_, _) => SetCurrentValue(IsOpenProperty, false);

        _confirmButton = new Button { Variant = ButtonVariant.Plain, Content = ConfirmText };
        _confirmButton.Click += (_, _) => CommitCurrentWheels();

        _titleBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ArcTokens.Bind(_titleBlock, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            MinHeight = HeaderHeight,
            Margin = new Thickness(8, 0),
        };
        Grid.SetColumn(_cancelButton, 0);
        Grid.SetColumn(_titleBlock, 1);
        Grid.SetColumn(_confirmButton, 2);
        header.Children.Add(_cancelButton);
        header.Children.Add(_titleBlock);
        header.Children.Add(_confirmButton);

        var divider = new Border { Height = 1 };
        ArcTokens.Bind(divider, Border.BackgroundProperty, ArcDesignTokenKind.Border);

        _wheelsGrid = new Grid { Margin = new Thickness(8, 8, 8, 12) };

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(divider);
        body.Children.Add(_wheelsGrid);

        _sheet = new Sheet { Content = body };
        _sheet.PropertyChanged += OnSheetPropertyChanged;
        UpdateSheetChrome();
        return _sheet;
    }

    private double HeaderHeight =>
        this.TryFindResource(DateWheelPickerTokens.Key(DateWheelPickerTokenKind.HeaderHeight), out var v) && v is double h && h > 0 ? h : 44;

    private void UpdateSheetChrome()
    {
        if (_cancelButton is not null)
        {
            _cancelButton.Content = CancelText;
        }
        if (_confirmButton is not null)
        {
            _confirmButton.Content = ConfirmText;
        }
        if (_titleBlock is not null)
        {
            _titleBlock.Text = SheetTitle ?? Content as string;
        }
    }

    private void BuildWheels()
    {
        if (_wheelsGrid is null)
        {
            return;
        }
        _wheelsGrid.Children.Clear();
        _wheelsGrid.ColumnDefinitions.Clear();
        _yearWheel = _monthWheel = _dayWheel = _hourWheel = _minuteWheel = null;

        var minYear = Math.Clamp(MinYear, 1, 9999);
        var maxYear = Math.Clamp(Math.Max(minYear, MaxYear), 1, 9999);
        var step = Math.Clamp(MinuteInterval, 1, 30);
        var seed = SelectedDateTime ?? DateTime.Now;
        var year = Math.Clamp(seed.Year, minYear, maxYear);
        var month = seed.Month;
        var day = seed.Day;
        var hour = seed.Hour;
        var minuteIndex = Math.Clamp((int)Math.Round(seed.Minute / (double)step), 0, Math.Max(0, (60 - 1) / step));

        var col = 0;
        if (Mode is DateWheelMode.Date or DateWheelMode.DateTime)
        {
            _yearWheel = new WheelPicker();
            for (var y = minYear; y <= maxYear; y++)
            {
                _yearWheel.Items.Add(y.ToString("0000", CultureInfo.InvariantCulture));
            }
            _yearWheel.SelectedIndex = year - minYear;
            _yearWheel.SelectionChanged += (_, _) => SyncDayCount();

            _monthWheel = new WheelPicker();
            for (var m = 1; m <= 12; m++)
            {
                _monthWheel.Items.Add(m.ToString("00", CultureInfo.InvariantCulture));
            }
            _monthWheel.SelectedIndex = month - 1;
            _monthWheel.SelectionChanged += (_, _) => SyncDayCount();

            _dayWheel = new WheelPicker();
            PopulateDays(year, month, day);

            AddWheelColumn(_yearWheel, col++, Mode == DateWheelMode.DateTime ? 1.3 : 1.2);
            AddWheelColumn(_monthWheel, col++, 1);
            AddWheelColumn(_dayWheel, col++, 1);
        }

        if (Mode is DateWheelMode.Time or DateWheelMode.DateTime)
        {
            _hourWheel = new WheelPicker();
            for (var h = 0; h < 24; h++)
            {
                _hourWheel.Items.Add(h.ToString("00", CultureInfo.InvariantCulture));
            }
            _hourWheel.SelectedIndex = hour;

            _minuteWheel = new WheelPicker();
            for (var m = 0; m < 60; m += step)
            {
                _minuteWheel.Items.Add(m.ToString("00", CultureInfo.InvariantCulture));
            }
            _minuteWheel.SelectedIndex = Math.Clamp(minuteIndex, 0, Math.Max(0, _minuteWheel.Items.Count - 1));

            AddWheelColumn(_hourWheel, col++, 1);
            AddWheelColumn(_minuteWheel, col, 1);
        }
    }

    private void AddWheelColumn(WheelPicker wheel, int column, double starWeight)
    {
        if (_wheelsGrid is null)
        {
            return;
        }
        _wheelsGrid.ColumnDefinitions.Add(new ColumnDefinition(starWeight, GridUnitType.Star));
        Grid.SetColumn(wheel, column);
        _wheelsGrid.Children.Add(wheel);
    }

    private void PopulateDays(int year, int month, int desiredDay)
    {
        if (_dayWheel is null)
        {
            return;
        }
        _rebuildingDays = true;
        try
        {
            var days = DateTime.DaysInMonth(year, month);
            _dayWheel.Items.Clear();
            for (var d = 1; d <= days; d++)
            {
                _dayWheel.Items.Add(d.ToString("00", CultureInfo.InvariantCulture));
            }
            _dayWheel.SelectedIndex = Math.Clamp(desiredDay - 1, 0, days - 1);
        }
        finally
        {
            _rebuildingDays = false;
        }
    }

    private void SyncDayCount()
    {
        if (_rebuildingDays || _yearWheel is null || _monthWheel is null || _dayWheel is null)
        {
            return;
        }
        var minYear = Math.Clamp(MinYear, 1, 9999);
        var year = Math.Clamp(minYear + Math.Max(0, _yearWheel.SelectedIndex), 1, 9999);
        var month = Math.Clamp(_monthWheel.SelectedIndex + 1, 1, 12);
        var currentDay = Math.Max(1, _dayWheel.SelectedIndex + 1);
        var expected = DateTime.DaysInMonth(year, month);
        if (_dayWheel.Items.Count != expected)
        {
            PopulateDays(year, month, Math.Min(currentDay, expected));
        }
    }

    private DateTime ReadCurrentWheels()
    {
        var baseDate = SelectedDateTime ?? DateTime.Today;
        var minYear = Math.Clamp(MinYear, 1, 9999);
        var year = _yearWheel is not null ? Math.Clamp(minYear + Math.Max(0, _yearWheel.SelectedIndex), 1, 9999) : baseDate.Year;
        var month = _monthWheel is not null ? Math.Clamp(_monthWheel.SelectedIndex + 1, 1, 12) : baseDate.Month;
        var maxDay = DateTime.DaysInMonth(year, month);
        var day = _dayWheel is not null ? Math.Clamp(_dayWheel.SelectedIndex + 1, 1, maxDay) : Math.Min(baseDate.Day, maxDay);
        var hour = _hourWheel is not null ? Math.Clamp(_hourWheel.SelectedIndex, 0, 23) : (Mode == DateWheelMode.Date ? 0 : baseDate.Hour);
        var step = Math.Clamp(MinuteInterval, 1, 30);
        var minute = _minuteWheel is not null ? Math.Clamp(_minuteWheel.SelectedIndex * step, 0, 59) : (Mode == DateWheelMode.Date ? 0 : baseDate.Minute);
        return new DateTime(year, month, day, hour, minute, 0);
    }

    private void Open()
    {
        if (!IsEffectivelyEnabled || !this.IsAttachedToVisualTree())
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }
        var sheet = EnsureSheet();
        if (!Mount(sheet))
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }
        UpdateSheetChrome();
        BuildWheels();
        sheet.ApplyTemplate();
        sheet.IsOpen = true;
    }

    private bool Mount(Sheet sheet)
    {
        var page = this.FindAncestorOfType<Page>();
        if (page is not null && ReferenceEquals(page, _hostPage) && sheet.GetVisualParent() is not null)
        {
            return true;
        }
        Unmount();
        if (page is not null && page.HostOverlay(sheet))
        {
            _hostPage = page;
            return true;
        }
        if (ArcOverlayLayer.Find(this) is not { } layer)
        {
            return false;
        }
        _hostLayer = layer;
        layer.Children.Add(sheet);
        layer.SizeChanged += OnLayerSizeChanged;
        SizeToLayer();
        return true;
    }

    private void Unmount()
    {
        if (_sheet is null)
        {
            return;
        }
        _sheet.IsOpen = false;
        if (_hostPage is not null)
        {
            _hostPage.UnhostOverlay(_sheet);
            _hostPage = null;
        }
        if (_hostLayer is not null)
        {
            _hostLayer.SizeChanged -= OnLayerSizeChanged;
            _hostLayer.Children.Remove(_sheet);
            _hostLayer = null;
            _sheet.ClearValue(WidthProperty);
            _sheet.ClearValue(HeightProperty);
        }
    }

    private void OnLayerSizeChanged(object? sender, SizeChangedEventArgs e) => SizeToLayer();

    private void SizeToLayer()
    {
        if (_sheet is null || _hostLayer is null)
        {
            return;
        }
        var size = _hostLayer.Bounds.Size;
        if ((size.Width <= 0 || size.Height <= 0) && _hostLayer is ArcOverlayLayer { Host: { } host })
        {
            size = host.Bounds.Size;
        }
        _sheet.Width = size.Width;
        _sheet.Height = size.Height;
    }

    private void OnSheetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Sheet.IsOpenProperty && _sheet is { IsOpen: false })
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }
}
