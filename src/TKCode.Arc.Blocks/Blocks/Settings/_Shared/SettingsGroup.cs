using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>How a <see cref="SettingsGroup"/> is drawn.</summary>
public enum SettingsGroupAppearance
{
    /// <summary>A card: surface, 1px border, panel radius, inner padding.</summary>
    Card,

    /// <summary>No frame or padding: rows on the page itself, for sections separated by hairlines.</summary>
    Plain
}

/// <summary>
/// A group of related settings: an optional header with a description and actions, a stack of rows (usually
/// <see cref="SettingsRow"/>, written inline or bound through <see cref="ItemsControl.ItemsSource"/>), and an optional
/// footer strip for a note or a save button. Drawn as a card by default, or plain inside a page. The rows sit in one
/// column unless <see cref="Columns"/> or <see cref="MinColumnWidth"/> asks for more, and <see cref="LabelWidth"/>,
/// <see cref="ContentAlignment"/> and <see cref="ContentSpacing"/> set the layout of every row in the group.
/// </summary>
[PseudoClasses(PlainPseudoClass)]
public class SettingsGroup : HeaderedItemsControl
{
    public const string PlainPseudoClass = ":plain";

    /// <summary>A sentence under the header.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsGroup, string?>(nameof(Description));

    /// <summary>Content at the right of the header, such as a button.</summary>
    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<SettingsGroup, object?>(nameof(HeaderActions));

    /// <summary>Content of the strip under the rows, such as a note and a save button.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SettingsGroup, object?>(nameof(Footer));

    /// <summary>Card (default) or Plain.</summary>
    public static readonly StyledProperty<SettingsGroupAppearance> AppearanceProperty =
        AvaloniaProperty.Register<SettingsGroup, SettingsGroupAppearance>(nameof(Appearance));

    /// <summary>
    /// The number of columns the rows are laid out in (default 1). With <see cref="MinColumnWidth"/> it is the most columns
    /// the group uses; 0 means as many as fit at <see cref="MinColumnWidth"/>, but no more than there are rows.
    /// </summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<SettingsGroup, int>(nameof(Columns), 1, validate: v => v >= 0);

    /// <summary>
    /// The narrowest a column may be (default 0, off). When set, the group drops columns while they would be narrower than
    /// this, down to one, so a two-column group becomes one column in a narrow window.
    /// </summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<SettingsGroup, double>(nameof(MinColumnWidth), 0, validate: v => v >= 0 && !double.IsInfinity(v));

    /// <summary>The horizontal gap between columns (32 by default).</summary>
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<SettingsGroup, double>(nameof(ColumnSpacing), 0, validate: v => v >= 0 && !double.IsInfinity(v));

    /// <summary>The vertical gap between lines of rows (default 0: rows carry their own padding and hairline).</summary>
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<SettingsGroup, double>(nameof(RowSpacing), 0, validate: v => v >= 0 && !double.IsInfinity(v));

    /// <summary>
    /// How many columns a row of the group spans (default 1); 0 spans the whole line. A span wider than the current number
    /// of columns is cut to the whole line.
    /// </summary>
    public static readonly AttachedProperty<int> ColumnSpanProperty =
        AvaloniaProperty.RegisterAttached<SettingsGroup, Control, int>("ColumnSpan", 1, validate: v => v >= 0);

    /// <summary>The number of columns the rows are laid out in right now, after <see cref="MinColumnWidth"/> is applied.</summary>
    public static readonly DirectProperty<SettingsGroup, int> ActualColumnsProperty =
        AvaloniaProperty.RegisterDirect<SettingsGroup, int>(nameof(ActualColumns), o => o.ActualColumns);

    /// <summary>
    /// The width from each row's left edge to its control, icon included, inherited by the rows of the group (default
    /// NaN: as wide as the text needs). A row's own <see cref="SettingsRow.LabelWidth"/> overrides it.
    /// </summary>
    public static readonly StyledProperty<double> LabelWidthProperty =
        SettingsRow.LabelWidthProperty.AddOwner<SettingsGroup>();

    /// <summary>
    /// Where each row's control sits in the space beside a fixed <see cref="LabelWidth"/>, inherited by the rows of the
    /// group (default Right). A row's own <see cref="SettingsRow.ContentAlignment"/> overrides it.
    /// </summary>
    public static readonly StyledProperty<HorizontalAlignment> ContentAlignmentProperty =
        SettingsRow.ContentAlignmentProperty.AddOwner<SettingsGroup>();

    /// <summary>
    /// The gap between each row's text and its control, inherited by the rows of the group (default NaN: 16, or 12 above a
    /// control under the text). A row's own <see cref="SettingsRow.ContentSpacing"/> overrides it.
    /// </summary>
    public static readonly StyledProperty<double> ContentSpacingProperty =
        SettingsRow.ContentSpacingProperty.AddOwner<SettingsGroup>();

    private int _actualColumns = 1;

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="MinColumnWidthProperty"/>
    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <inheritdoc cref="ColumnSpacingProperty"/>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <inheritdoc cref="RowSpacingProperty"/>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <inheritdoc cref="ActualColumnsProperty"/>
    public int ActualColumns
    {
        get => _actualColumns;
        private set => SetAndRaise(ActualColumnsProperty, ref _actualColumns, value);
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

    /// <summary>Gets how many columns <paramref name="element"/> spans in its group.</summary>
    public static int GetColumnSpan(Control element) => element.GetValue(ColumnSpanProperty);

    /// <summary>Sets how many columns <paramref name="element"/> spans in its group; 0 spans the whole line.</summary>
    public static void SetColumnSpan(Control element, int value) => element.SetValue(ColumnSpanProperty, value);

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="HeaderActionsProperty"/>
    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <inheritdoc cref="AppearanceProperty"/>
    public SettingsGroupAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    static SettingsGroup()
    {
        FocusableProperty.OverrideDefaultValue<SettingsGroup>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsGroup);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AppearanceProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, Appearance == SettingsGroupAppearance.Plain);
        }
        else if (change.Property == ColumnsProperty || change.Property == MinColumnWidthProperty
                 || change.Property == ColumnSpacingProperty || change.Property == RowSpacingProperty)
        {
            ItemsPanelRoot?.InvalidateMeasure();
        }
    }

    /// <summary>Called by <see cref="SettingsGroupPanel"/> once it has worked out the number of columns.</summary>
    internal void SetActualColumns(int columns) => ActualColumns = columns;
}
