using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// One setting: an optional icon (or any <see cref="Leading"/> content, such as an avatar), a header and description on
/// the left with optional <see cref="Details"/> under them, and the control that changes it
/// (<see cref="ContentControl.Content"/>) on the right. Below <see cref="StackWidth"/> the control moves under the text.
/// Rows in a <see cref="SettingsGroup"/> (or any panel) are separated by a hairline; the first row has none. When the
/// control is a toggle (Switch, Checkbox), clicking the header or description toggles it too, and the control takes
/// the header as its accessible name and the description as its help text unless it has its own.
/// <see cref="LabelWidth"/>, <see cref="ContentAlignment"/> and <see cref="ContentSpacing"/> are inherited from the
/// <see cref="SettingsGroup"/> that holds the row, so the controls of a group line up; set them on a row to override.
/// </summary>
[TemplatePart(LabelPart, typeof(Control))]
[TemplatePart(LayoutPart, typeof(Grid))]
[TemplatePart(ContentPart, typeof(ContentPresenter))]
[TemplatePart(IconTilePart, typeof(Control))]
[PseudoClasses(StackedPseudoClass, IconPseudoClass, LeadingPseudoClass, DetailsPseudoClass, FirstLinePseudoClass)]
public class SettingsRow : ContentControl
{
    public const string LabelPart = "PART_Label";
    public const string LayoutPart = "PART_Layout";
    public const string ContentPart = "PART_ContentPresenter";
    public const string IconTilePart = "PART_IconTile";

    /// <summary>The presenter of <see cref="Leading"/>, built in the icon's place the first time the row has leading content.</summary>
    public const string LeadingPart = "PART_Leading";

    /// <summary>The presenter of <see cref="Details"/>, built under the description the first time the row has details.</summary>
    public const string DetailsPart = "PART_Details";

    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the icon tile shows: the row has an <see cref="Icon"/> and no <see cref="Leading"/> content.</summary>
    public const string IconPseudoClass = ":icon";

    /// <summary>Set while the row has <see cref="Leading"/> content.</summary>
    public const string LeadingPseudoClass = ":leading";

    /// <summary>Set while the row has <see cref="Details"/>.</summary>
    public const string DetailsPseudoClass = ":details";

    /// <summary>Set on the rows of a <see cref="SettingsGroup"/>'s first line, which have no hairline above them.</summary>
    public const string FirstLinePseudoClass = ":first-line";

    /// <summary>The setting's name, shown in medium weight; also the control's accessible name.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsRow, string?>(nameof(Header));

    /// <summary>A sentence under the header that explains the setting; also the control's help text.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsRow, string?>(nameof(Description));

    /// <summary>
    /// A 24×24 stroke icon (for example an <see cref="ArcIcons"/> member) shown in a tile before the header. Not shown while
    /// the row has <see cref="Leading"/> content.
    /// </summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsRow, Geometry?>(nameof(Icon));

    /// <summary>
    /// Content before the header in place of the icon tile, such as an <see cref="Avatar"/> or a logo; it keeps its own size.
    /// When set, <see cref="Icon"/> is not shown.
    /// </summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<SettingsRow, object?>(nameof(Leading));

    /// <summary>
    /// Content under the header and description, such as a status line, an error or a row of tags. Text is shown small
    /// and muted; other content keeps its own look.
    /// </summary>
    public static readonly StyledProperty<object?> DetailsProperty =
        AvaloniaProperty.Register<SettingsRow, object?>(nameof(Details));

    /// <summary>Below this width the control moves under the header and description (default 440).</summary>
    public static readonly StyledProperty<double> StackWidthProperty =
        AvaloniaProperty.Register<SettingsRow, double>(nameof(StackWidth), 440);

    /// <summary>Whether the control currently sits under the text (the row is narrower than <see cref="StackWidth"/>).</summary>
    public static readonly DirectProperty<SettingsRow, bool> IsStackedProperty =
        AvaloniaProperty.RegisterDirect<SettingsRow, bool>(nameof(IsStacked), o => o.IsStacked);

    /// <summary>
    /// The width from the row's left edge to the control, icon included, so controls line up across rows with and
    /// without icons (default NaN: as wide as the text needs). Inherited from the <see cref="SettingsGroup"/>. Ignored
    /// while the control sits under the text.
    /// </summary>
    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<SettingsRow, double>(nameof(LabelWidth), double.NaN, inherits: true,
            validate: v => double.IsNaN(v) || (v >= 0 && !double.IsInfinity(v)));

    /// <summary>
    /// Where the control sits in the space beside a fixed <see cref="LabelWidth"/> (default Right); Stretch fills it.
    /// Without a label width the control is as wide as it needs, at the right. Under the text the control is at the left,
    /// or fills the row with Stretch. Inherited from the <see cref="SettingsGroup"/>.
    /// </summary>
    public static readonly StyledProperty<HorizontalAlignment> ContentAlignmentProperty =
        AvaloniaProperty.Register<SettingsRow, HorizontalAlignment>(nameof(ContentAlignment), HorizontalAlignment.Right, inherits: true);

    /// <summary>
    /// The gap between the text and the control: before the control beside the text, above it under the text (default
    /// NaN: 16 beside, 12 under). Inherited from the <see cref="SettingsGroup"/>.
    /// </summary>
    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<SettingsRow, double>(nameof(ContentSpacing), double.NaN, inherits: true,
            validate: v => double.IsNaN(v) || (v >= 0 && !double.IsInfinity(v)));

    private Control? _label;
    private Grid? _layout;
    private ContentPresenter? _contentPresenter;
    private Control? _iconTile;
    private ContentPresenter? _leading;
    private ContentPresenter? _details;
    private bool _spacingApplied;
    private Control? _namedControl;
    private string? _assignedName;
    private string? _assignedHelp;
    private bool _isStacked;
    private bool _labelPressed;

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

    /// <inheritdoc cref="LeadingProperty"/>
    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    /// <inheritdoc cref="DetailsProperty"/>
    public object? Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    /// <inheritdoc cref="StackWidthProperty"/>
    public double StackWidth
    {
        get => GetValue(StackWidthProperty);
        set => SetValue(StackWidthProperty, value);
    }

    /// <inheritdoc cref="LabelWidthProperty"/>
    public double LabelWidth
    {
        get => GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    /// <inheritdoc cref="ContentAlignmentProperty"/>
    public HorizontalAlignment ContentAlignment
    {
        get => GetValue(ContentAlignmentProperty);
        set => SetValue(ContentAlignmentProperty, value);
    }

    /// <inheritdoc cref="ContentSpacingProperty"/>
    public double ContentSpacing
    {
        get => GetValue(ContentSpacingProperty);
        set => SetValue(ContentSpacingProperty, value);
    }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked
    {
        get => _isStacked;
        private set
        {
            if (SetAndRaise(IsStackedProperty, ref _isStacked, value))
            {
                PseudoClasses.Set(StackedPseudoClass, value);
                AlignLeading();
            }
        }
    }

    static SettingsRow()
    {
        AffectsMeasure<SettingsRow>(LabelWidthProperty, ContentAlignmentProperty, ContentSpacingProperty, IconProperty, LeadingProperty);
    }

    protected override Type StyleKeyOverride => typeof(SettingsRow);

    /// <summary>Called by the group's panel: whether the row is in the first line, which has no hairline.</summary>
    internal void SetFirstLine(bool value) => PseudoClasses.Set(FirstLinePseudoClass, value);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_label is not null)
        {
            _label.PointerPressed -= OnLabelPointerPressed;
            _label.PointerReleased -= OnLabelPointerReleased;
            _label.PointerCaptureLost -= OnLabelPointerCaptureLost;
        }
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<Grid>(LayoutPart);
        _contentPresenter = e.NameScope.Find<ContentPresenter>(ContentPart);
        _iconTile = e.NameScope.Find<Control>(IconTilePart);
        _spacingApplied = false;
        _label = e.NameScope.Find<Control>(LabelPart);
        if (_label is not null)
        {
            _label.PointerPressed += OnLabelPointerPressed;
            _label.PointerReleased += OnLabelPointerReleased;
            _label.PointerCaptureLost += OnLabelPointerCaptureLost;
        }
        _leading = null;
        _details = null;
        UpdateLeading();
        UpdateDetails();
    }

    /// <summary>
    /// The leading presenter (PART_Leading) is built the first time the row has leading content, in the icon tile's cell,
    /// with the tile's gap after it; most rows have none.
    /// </summary>
    private void UpdateLeading()
    {
        var hasLeading = Leading is not null;
        PseudoClasses.Set(LeadingPseudoClass, hasLeading);
        PseudoClasses.Set(IconPseudoClass, Icon is not null && !hasLeading);
        if (!hasLeading)
        {
            if (_leading is not null)
            {
                _leading.IsVisible = false;
            }
            return;
        }
        if (_leading is null)
        {
            if (_layout is null)
            {
                return;
            }
            var presenter = new ContentPresenter { Name = LeadingPart };
            presenter.Bind(ContentPresenter.ContentProperty, this.GetObservable(LeadingProperty));
            presenter.Bind(MarginProperty, presenter.GetResourceObservable(SettingsRowTokens.Key(SettingsRowTokenKind.IconTileMargin)));
            _layout.Children.Add(presenter);
            _leading = presenter;
        }
        _leading.IsVisible = true;
        AlignLeading();
    }

    /// <summary>Like the icon tile, the leading content is centered beside the text and sits at the top once the control drops under it.</summary>
    private void AlignLeading()
    {
        if (_leading is not null)
        {
            _leading.VerticalAlignment = IsStacked ? VerticalAlignment.Top : VerticalAlignment.Center;
        }
    }

    /// <summary>
    /// The details presenter (PART_Details) is built the first time the row has details, under the description in
    /// PART_Label, in small muted text; most rows have none.
    /// </summary>
    private void UpdateDetails()
    {
        var hasDetails = Details is not null;
        PseudoClasses.Set(DetailsPseudoClass, hasDetails);
        if (!hasDetails)
        {
            if (_details is not null)
            {
                _details.IsVisible = false;
            }
            return;
        }
        if (_details is null)
        {
            if (_label is not Panel panel)
            {
                return;
            }
            var presenter = new ContentPresenter { Name = DetailsPart, TextWrapping = TextWrapping.Wrap };
            presenter.Bind(ContentPresenter.ContentProperty, this.GetObservable(DetailsProperty));
            presenter.Bind(MarginProperty, presenter.GetResourceObservable(SettingsRowTokens.Key(SettingsRowTokenKind.DetailsMargin)));
            presenter.Bind(ContentPresenter.LineHeightProperty, presenter.GetResourceObservable(SettingsRowTokens.Key(SettingsRowTokenKind.DetailsLineHeight)));
            ArcTokens.Bind(presenter, ContentPresenter.FontSizeProperty, ArcDesignTokenKind.TypeXS, BindingPriority.LocalValue);
            ArcTokens.Bind(presenter, ContentPresenter.ForegroundProperty, ArcDesignTokenKind.TextMuted, BindingPriority.LocalValue);
            panel.Children.Add(presenter);
            _details = presenter;
        }
        _details.IsVisible = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width is set by the parent, not by the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            IsStacked = availableSize.Width < StackWidth;
        }
        ApplyLayoutOptions();
        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// Applies <see cref="LabelWidth"/>, <see cref="ContentAlignment"/> and <see cref="ContentSpacing"/> to the template.
    /// Runs before the template is measured; with the defaults the template keeps the theme's layout (text column, then
    /// the control at its own width).
    /// </summary>
    private void ApplyLayoutOptions()
    {
        if (_layout is null || _contentPresenter is null)
        {
            return;
        }
        var stacked = IsStacked;
        var labelWidth = LabelWidth;
        var fixedLabel = !stacked && !double.IsNaN(labelWidth);
        if (_layout.ColumnDefinitions.Count == 3)
        {
            var label = fixedLabel ? new GridLength(Math.Max(0, labelWidth - IconExtent())) : GridLength.Star;
            var control = fixedLabel ? GridLength.Star : GridLength.Auto;
            if (_layout.ColumnDefinitions[1].Width != label)
            {
                _layout.ColumnDefinitions[1].Width = label;
            }
            if (_layout.ColumnDefinitions[2].Width != control)
            {
                _layout.ColumnDefinitions[2].Width = control;
            }
        }

        var alignment = ContentAlignment;
        if (stacked)
        {
            alignment = alignment == HorizontalAlignment.Stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        }
        _contentPresenter.HorizontalAlignment = alignment;

        var spacing = ContentSpacing;
        if (double.IsNaN(spacing))
        {
            if (_spacingApplied)
            {
                _contentPresenter.ClearValue(MarginProperty);
                _spacingApplied = false;
            }
        }
        else
        {
            _contentPresenter.Margin = stacked ? new Thickness(0, spacing, 0, 0) : new Thickness(spacing, 0, 0, 0);
            _spacingApplied = true;
        }
    }

    /// <summary>
    /// The width the icon tile (or the leading content) and its gap take before the text, which <see cref="LabelWidth"/>
    /// includes.
    /// </summary>
    private double IconExtent()
    {
        if (Leading is not null && _leading is { IsVisible: true } leading)
        {
            // Measured as the grid's Auto cell measures it, so the grid finds the result cached.
            leading.Measure(Size.Infinity);
            return leading.DesiredSize.Width;
        }
        if (Icon is null || _iconTile is null)
        {
            return 0;
        }
        var width = _iconTile.Width;
        if (double.IsNaN(width))
        {
            width = _iconTile.DesiredSize.Width;
        }
        return width + _iconTile.Margin.Left + _iconTile.Margin.Right;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == LeadingProperty)
        {
            UpdateLeading();
        }
        else if (change.Property == DetailsProperty)
        {
            UpdateDetails();
        }
        else if (change.Property == StackWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == ContentProperty)
        {
            ReleaseAccessibleText();
            ApplyAccessibleText();
        }
        else if (change.Property == HeaderProperty || change.Property == DescriptionProperty)
        {
            ApplyAccessibleText();
        }
    }

    /// <summary>Gives the control the header as its name and the description as its help text, unless it has its own.</summary>
    private void ApplyAccessibleText()
    {
        if (Content is not Control control)
        {
            return;
        }
        _namedControl = control;
        var name = AutomationProperties.GetName(control);
        if (string.IsNullOrEmpty(name) || name == _assignedName)
        {
            _assignedName = Header;
            AutomationProperties.SetName(control, Header);
        }
        var help = AutomationProperties.GetHelpText(control);
        if (string.IsNullOrEmpty(help) || help == _assignedHelp)
        {
            _assignedHelp = Description;
            AutomationProperties.SetHelpText(control, Description);
        }
    }

    /// <summary>Takes back the text this row gave a control that is no longer its content.</summary>
    private void ReleaseAccessibleText()
    {
        if (_namedControl is null)
        {
            return;
        }
        if (_assignedName is not null && AutomationProperties.GetName(_namedControl) == _assignedName)
        {
            _namedControl.ClearValue(AutomationProperties.NameProperty);
        }
        if (_assignedHelp is not null && AutomationProperties.GetHelpText(_namedControl) == _assignedHelp)
        {
            _namedControl.ClearValue(AutomationProperties.HelpTextProperty);
        }
        _namedControl = null;
        _assignedName = null;
        _assignedHelp = null;
    }

    private void OnLabelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Content is ToggleButton && e.GetCurrentPoint(_label).Properties.IsLeftButtonPressed)
        {
            _labelPressed = true;
            e.Pointer.Capture(_label);
            e.Handled = true;
        }
    }

    private void OnLabelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_labelPressed || _label is null)
        {
            return;
        }
        _labelPressed = false;
        e.Pointer.Capture(null);
        var inside = new Rect(_label.Bounds.Size).Contains(e.GetPosition(_label));
        if (inside && Content is ToggleButton toggle && toggle.IsEffectivelyEnabled)
        {
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, toggle.IsChecked != true);
            e.Handled = true;
        }
    }

    private void OnLabelPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _labelPressed = false;
}
