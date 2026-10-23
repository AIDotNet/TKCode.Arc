using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Arguments of the variable field events: which variable.</summary>
internal sealed class PromptVariableEventArgs : RoutedEventArgs
{
    public PromptVariableEventArgs(RoutedEvent routedEvent, object source, string name)
        : base(routedEvent, source)
    {
        Name = name;
    }

    /// <summary>The variable's name.</summary>
    public string Name { get; }
}

/// <summary>
/// Prompt studio's variable fields: one <see cref="PromptVariableField"/> per name of <see cref="Variables"/>, in order,
/// on a grid of four equal columns (10 apart, rows 12 apart) where a field with a long value takes two. The grid has two
/// columns from <see cref="TwoColumnMaxWidth"/> down (<c>:two-col</c>) and one from <see cref="OneColumnMaxWidth"/> down
/// (<c>:one-col</c>), measured on the control's own width. Each field's dot, label and focus border take its
/// variable's series color (<see cref="PromptTone"/>: the n-th variable takes tone n modulo 4).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Values"/> is two-way per field: typing writes the field's entry (and raises <see cref="ValueEdited"/> on
/// every edit, so the owner can flash the chips), and changes to the dictionary from elsewhere (a test case loaded) show
/// in the fields at once. A name without an entry shows an empty field with the <see cref="EmptyText"/> placeholder.
/// </para>
/// <para>
/// Whether a field is wide is decided from its value when the value comes from outside the field (the field appears, a
/// test case is loaded): longer than <see cref="WideThreshold"/> characters. Typing never changes it, so a wide field
/// stays wide while the person clears it (as in Arc).
/// </para>
/// <para>
/// <see cref="ActiveVariable"/> is the variable whose field has the focus (the owner highlights its chips);
/// <see cref="FieldFocused"/> is raised when a field takes the focus. <see cref="FocusField"/> focuses a field with the
/// caret at the end (the owner calls it when a chip is clicked). Fields added after the control was shown fade in while
/// the grid's height follows on the smooth spring; removed fields go at once. Reduced motion keeps the short fade, Off
/// jumps.
/// </para>
/// </remarks>
[TemplatePart(PanelPart, typeof(PromptVariableFieldsPanel))]
[PseudoClasses(TwoColumnPseudoClass, OneColumnPseudoClass)]
internal class PromptVariableFields : TemplatedControl
{
    public const string PanelPart = "PART_Panel";
    public const string TwoColumnPseudoClass = ":two-col";
    public const string OneColumnPseudoClass = ":one-col";

    /// <summary>Share of the series color in a label (the chip text color, Arc <c>color-mix(series 68%, foreground)</c>).</summary>
    internal const double LabelMix = 0.68;

    /// <summary>Share of the series color in a focused field's border (Arc <c>color-mix(series 65%, border)</c>).</summary>
    internal const double FocusMix = 0.65;

    #region Properties

    /// <summary>The variables, in the order of their colors and fields; repeated names are shown once.</summary>
    public static readonly StyledProperty<IList<string>?> VariablesProperty =
        AvaloniaProperty.Register<PromptVariableFields, IList<string>?>(nameof(Variables));

    /// <summary>The value of each variable by name, two-way per field.</summary>
    public static readonly StyledProperty<AvaloniaDictionary<string, string>?> ValuesProperty =
        AvaloniaProperty.Register<PromptVariableFields, AvaloniaDictionary<string, string>?>(nameof(Values));

    /// <summary>The placeholder of an empty field ("Empty").</summary>
    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<PromptVariableFields, string?>(nameof(EmptyText));

    /// <summary>A value longer than this many characters makes its field wide (two columns); 24.</summary>
    public static readonly StyledProperty<int> WideThresholdProperty =
        AvaloniaProperty.Register<PromptVariableFields, int>(nameof(WideThreshold), 24);

    /// <summary>From this width down the grid has two columns (528: Arc's 560 card less the 16px section padding on each side).</summary>
    public static readonly StyledProperty<double> TwoColumnMaxWidthProperty =
        AvaloniaProperty.Register<PromptVariableFields, double>(nameof(TwoColumnMaxWidth), 528);

    /// <summary>From this width down the grid has one column (316: Arc's 340 card less the 12px narrow padding on each side).</summary>
    public static readonly StyledProperty<double> OneColumnMaxWidthProperty =
        AvaloniaProperty.Register<PromptVariableFields, double>(nameof(OneColumnMaxWidth), 316);

    /// <summary>The accessible name of the group of fields ("Variables").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<PromptVariableFields, string?>(nameof(AccessibleName));

    /// <summary>The variable whose field has the focus, or null (read-only).</summary>
    public static readonly DirectProperty<PromptVariableFields, string?> ActiveVariableProperty =
        AvaloniaProperty.RegisterDirect<PromptVariableFields, string?>(nameof(ActiveVariable), o => o.ActiveVariable);

    /// <summary>Raised when a field takes the focus.</summary>
    public static readonly RoutedEvent<PromptVariableEventArgs> FieldFocusedEvent =
        RoutedEvent.Register<PromptVariableFields, PromptVariableEventArgs>(nameof(FieldFocused), RoutingStrategies.Bubble);

    /// <summary>Raised after every edit the person makes in a field, once <see cref="Values"/> holds the new value.</summary>
    public static readonly RoutedEvent<PromptVariableEventArgs> ValueEditedEvent =
        RoutedEvent.Register<PromptVariableFields, PromptVariableEventArgs>(nameof(ValueEdited), RoutingStrategies.Bubble);

    // Theme colors the per-variable brushes are mixed from.
    private static readonly StyledProperty<IBrush?> Series1ProbeProperty =
        AvaloniaProperty.Register<PromptVariableFields, IBrush?>("Series1Probe");

    private static readonly StyledProperty<IBrush?> Series2ProbeProperty =
        AvaloniaProperty.Register<PromptVariableFields, IBrush?>("Series2Probe");

    private static readonly StyledProperty<IBrush?> Series3ProbeProperty =
        AvaloniaProperty.Register<PromptVariableFields, IBrush?>("Series3Probe");

    private static readonly StyledProperty<IBrush?> Series4ProbeProperty =
        AvaloniaProperty.Register<PromptVariableFields, IBrush?>("Series4Probe");

    private static readonly StyledProperty<IBrush?> ForegroundProbeProperty =
        AvaloniaProperty.Register<PromptVariableFields, IBrush?>("ForegroundProbe");

    private static readonly StyledProperty<IBrush?> BorderProbeProperty =
        AvaloniaProperty.Register<PromptVariableFields, IBrush?>("BorderProbe");

    /// <inheritdoc cref="VariablesProperty"/>
    public IList<string>? Variables { get => GetValue(VariablesProperty); set => SetValue(VariablesProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public AvaloniaDictionary<string, string>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="WideThresholdProperty"/>
    public int WideThreshold { get => GetValue(WideThresholdProperty); set => SetValue(WideThresholdProperty, value); }

    /// <inheritdoc cref="TwoColumnMaxWidthProperty"/>
    public double TwoColumnMaxWidth { get => GetValue(TwoColumnMaxWidthProperty); set => SetValue(TwoColumnMaxWidthProperty, value); }

    /// <inheritdoc cref="OneColumnMaxWidthProperty"/>
    public double OneColumnMaxWidth { get => GetValue(OneColumnMaxWidthProperty); set => SetValue(OneColumnMaxWidthProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="ActiveVariableProperty"/>
    public string? ActiveVariable { get => _activeVariable; private set => SetAndRaise(ActiveVariableProperty, ref _activeVariable, value); }

    /// <summary>Raised when a field takes the focus.</summary>
    public event EventHandler<PromptVariableEventArgs>? FieldFocused { add => AddHandler(FieldFocusedEvent, value); remove => RemoveHandler(FieldFocusedEvent, value); }

    /// <summary>Raised after every edit the person makes in a field.</summary>
    public event EventHandler<PromptVariableEventArgs>? ValueEdited { add => AddHandler(ValueEditedEvent, value); remove => RemoveHandler(ValueEditedEvent, value); }

    #endregion

    private static readonly Avalonia.Animation.Easings.Easing s_linear = new Avalonia.Animation.Easings.LinearEasing();

    private readonly List<PromptVariableField> _fields = new();
    private readonly Dictionary<string, PromptVariableField> _byName = new(StringComparer.Ordinal);
    private readonly IBrush?[] _dotBrushes = new IBrush?[4];
    private readonly IBrush?[] _labelBrushes = new IBrush?[4];
    private readonly IBrush?[] _focusBrushes = new IBrush?[4];
    private PromptVariableFieldsPanel? _panel;
    private INotifyCollectionChanged? _observedVariables;
    private AvaloniaDictionary<string, string>? _observedValues;
    private string? _activeVariable;
    private bool _attached;
    private bool _shown;
    private int _syncing;
    private int _columns = 4;

    static PromptVariableFields()
    {
        FocusableProperty.OverrideDefaultValue<PromptVariableFields>(false);
    }

    public PromptVariableFields()
    {
        ArcTokens.Bind(this, Series1ProbeProperty, ArcDesignTokenKind.Series1);
        ArcTokens.Bind(this, Series2ProbeProperty, ArcDesignTokenKind.Series2);
        ArcTokens.Bind(this, Series3ProbeProperty, ArcDesignTokenKind.Series3);
        ArcTokens.Bind(this, Series4ProbeProperty, ArcDesignTokenKind.Series4);
        ArcTokens.Bind(this, ForegroundProbeProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, BorderProbeProperty, ArcDesignTokenKind.Border);
    }

    /// <summary>The fields, in variable order.</summary>
    internal IReadOnlyList<PromptVariableField> Fields => _fields;

    /// <summary>The grid (template part).</summary>
    internal PromptVariableFieldsPanel? Panel => _panel;

    /// <summary>The number of columns the grid has now (4, 2 or 1).</summary>
    internal int ColumnCount => _columns;

    /// <summary>The field of <paramref name="name"/>, or null.</summary>
    internal PromptVariableField? FieldOf(string name) => _byName.GetValueOrDefault(name);

    /// <summary>The number of columns for a width: 4, then 2 from <paramref name="twoColumns"/> down, 1 from <paramref name="oneColumn"/> down.</summary>
    internal static int ColumnsFor(double width, double twoColumns, double oneColumn) =>
        double.IsNaN(width) || double.IsInfinity(width) ? 4 : width <= oneColumn ? 1 : width <= twoColumns ? 2 : 4;

    /// <summary>
    /// Focuses the field of <paramref name="name"/> with the caret at the end of its value (a chip was clicked). False
    /// when there is no such field or it cannot take the focus.
    /// </summary>
    public bool FocusField(string name)
    {
        if (!_byName.TryGetValue(name, out var field) || !field.IsEffectivelyVisible || !field.IsEffectivelyEnabled)
        {
            return false;
        }
        if (!field.Focus(NavigationMethod.Pointer))
        {
            return false;
        }
        var end = field.Text?.Length ?? 0;
        field.SelectionStart = end;
        field.SelectionEnd = end;
        field.CaretIndex = end;
        return true;
    }

    #region Template and lifetime

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            foreach (var field in _fields)
            {
                _panel.Children.Remove(field);
            }
        }
        _panel = e.NameScope.Find<PromptVariableFieldsPanel>(PanelPart);
        if (_panel is not null)
        {
            _panel.Columns = _columns;
            foreach (var field in _fields)
            {
                _panel.Children.Add(field);
            }
        }
        RebuildFields();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveVariables(Variables);
        ObserveValues(Values);
        RebuildFields();
        SyncAllValues();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveVariables(null);
        ObserveValues(null);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Fields that exist when the control is first shown appear with it; later ones fade in.
        _shown = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariablesProperty)
        {
            if (_attached)
            {
                ObserveVariables(change.GetNewValue<IList<string>?>());
            }
            RebuildFields();
        }
        else if (change.Property == ValuesProperty)
        {
            if (_attached)
            {
                ObserveValues(change.GetNewValue<AvaloniaDictionary<string, string>?>());
            }
            SyncAllValues();
        }
        else if (change.Property == EmptyTextProperty)
        {
            foreach (var field in _fields)
            {
                field.PlaceholderText = EmptyText;
            }
        }
        else if (change.Property == WideThresholdProperty)
        {
            foreach (var field in _fields)
            {
                UpdateWide(field);
            }
        }
        else if (change.Property == TwoColumnMaxWidthProperty || change.Property == OneColumnMaxWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == AccessibleNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
        else if (change.Property == Series1ProbeProperty || change.Property == Series2ProbeProperty ||
                 change.Property == Series3ProbeProperty || change.Property == Series4ProbeProperty ||
                 change.Property == ForegroundProbeProperty || change.Property == BorderProbeProperty)
        {
            UpdateBrushes();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Arc's container query: the columns follow the control's own width, decided before the grid measures.
        var columns = ColumnsFor(availableSize.Width - Padding.Left - Padding.Right, TwoColumnMaxWidth, OneColumnMaxWidth);
        if (columns != _columns)
        {
            _columns = columns;
            PseudoClasses.Set(TwoColumnPseudoClass, columns == 2);
            PseudoClasses.Set(OneColumnPseudoClass, columns == 1);
        }
        if (_panel is not null)
        {
            _panel.Columns = _columns;
        }
        return base.MeasureOverride(availableSize);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FieldsPeer(this);

    #endregion

    #region Fields

    private void ObserveVariables(IList<string>? variables)
    {
        var observed = variables as INotifyCollectionChanged;
        if (ReferenceEquals(observed, _observedVariables))
        {
            return;
        }
        if (_observedVariables is not null)
        {
            _observedVariables.CollectionChanged -= OnVariablesChanged;
        }
        _observedVariables = observed;
        if (_observedVariables is not null)
        {
            _observedVariables.CollectionChanged += OnVariablesChanged;
        }
    }

    private void OnVariablesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildFields();

    /// <summary>One field per distinct name, in order: fields of names that stay are kept (with their focus), new ones are made.</summary>
    private void RebuildFields()
    {
        if (_panel is null)
        {
            return;
        }
        var names = DistinctNames(Variables);
        var keep = new HashSet<string>(names, StringComparer.Ordinal);
        for (var i = _fields.Count - 1; i >= 0; i--)
        {
            var field = _fields[i];
            if (field.VariableName is { } name && keep.Contains(name))
            {
                continue;
            }
            _fields.RemoveAt(i);
            if (field.VariableName is { } gone)
            {
                _byName.Remove(gone);
            }
            Detach(field);
            _panel.Children.Remove(field);
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var fade = _shown && IsLoaded && level != ArcMotionLevel.Off;
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (!_byName.TryGetValue(name, out var field))
            {
                field = CreateField(name);
                _byName[name] = field;
                SetFieldText(field, ValueOf(name));
                UpdateWide(field);
                if (fade)
                {
                    FadeIn(field, level == ArcMotionLevel.Full);
                }
            }
            // The shared rule (the n-th variable takes tone n modulo 4); names are distinct here, so it is the position's.
            field.Tone = PromptTone.ToneOf(name, names);
            ApplyBrushes(field);
            var at = _fields.IndexOf(field);
            if (at != i)
            {
                if (at >= 0)
                {
                    _fields.RemoveAt(at);
                }
                _fields.Insert(i, field);
            }
            var childAt = _panel.Children.IndexOf(field);
            if (childAt != i)
            {
                if (childAt >= 0)
                {
                    _panel.Children.RemoveAt(childAt);
                }
                _panel.Children.Insert(i, field);
            }
            AutomationProperties.SetPositionInSet(field, i + 1);
            AutomationProperties.SetSizeOfSet(field, names.Count);
        }
        if (_activeVariable is { } active && !_byName.ContainsKey(active))
        {
            ActiveVariable = null;
        }
    }

    private static List<string> DistinctNames(IList<string>? variables)
    {
        var names = new List<string>();
        if (variables is null)
        {
            return names;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in variables)
        {
            if (!string.IsNullOrEmpty(name) && seen.Add(name))
            {
                names.Add(name);
            }
        }
        return names;
    }

    private PromptVariableField CreateField(string name)
    {
        var field = new PromptVariableField { VariableName = name, PlaceholderText = EmptyText };
        field.PropertyChanged += OnFieldPropertyChanged;
        field.GotFocus += OnFieldGotFocus;
        field.LostFocus += OnFieldLostFocus;
        return field;
    }

    private void Detach(PromptVariableField field)
    {
        field.PropertyChanged -= OnFieldPropertyChanged;
        field.GotFocus -= OnFieldGotFocus;
        field.LostFocus -= OnFieldLostFocus;
    }

    /// <summary>A new field fades in (opacity 0 → 1, 160ms standard; 120ms linear when reduced) while the grid's height springs.</summary>
    private static void FadeIn(PromptVariableField field, bool full)
    {
        field.Opacity = 0;
        var fade = new MotionValue<double>(field, 0, MotionVectors.Double);
        fade.Changed += v => field.Opacity = v;
        field.AttachedToVisualTree += Start;

        async void Start(object? sender, VisualTreeAttachmentEventArgs e)
        {
            field.AttachedToVisualTree -= Start;
            // Code-driven: the field is created in code when a variable is added, so there is no state for a ControlTheme
            // transition to start from.
            await fade.AnimateTo(1, full
                ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
            field.Opacity = 1;
            fade.Dispose();
        }
    }

    #endregion

    #region Values

    private void ObserveValues(AvaloniaDictionary<string, string>? values)
    {
        if (ReferenceEquals(values, _observedValues))
        {
            return;
        }
        if (_observedValues is not null)
        {
            _observedValues.CollectionChanged -= OnValuesChanged;
        }
        _observedValues = values;
        if (_observedValues is not null)
        {
            _observedValues.CollectionChanged += OnValuesChanged;
        }
    }

    private void OnValuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncing > 0)
        {
            // Our own write of a typed value.
            return;
        }
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace && e.NewItems is { } items)
        {
            foreach (var item in items)
            {
                if (item is KeyValuePair<string, string> pair && _byName.TryGetValue(pair.Key, out var field))
                {
                    SetFieldText(field, pair.Value);
                    UpdateWide(field);
                }
            }
            return;
        }
        SyncAllValues();
    }

    /// <summary>Shows every value of <see cref="Values"/> in its field (an entry that is gone shows an empty field).</summary>
    private void SyncAllValues()
    {
        foreach (var field in _fields)
        {
            if (field.VariableName is { } name)
            {
                SetFieldText(field, ValueOf(name));
                UpdateWide(field);
            }
        }
    }

    private string ValueOf(string name) => Values is { } values && values.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;

    private void SetFieldText(PromptVariableField field, string text)
    {
        // An empty value is an empty string, never null (a new text box starts with null).
        if (string.Equals(field.Text, text, StringComparison.Ordinal))
        {
            return;
        }
        _syncing++;
        try
        {
            field.Text = text;
        }
        finally
        {
            _syncing--;
        }
    }

    /// <summary>The value came from outside the field: it decides whether the field is wide.</summary>
    private void UpdateWide(PromptVariableField field) => field.IsWide = (field.Text?.Length ?? 0) > WideThreshold;

    private void OnFieldPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncing > 0 || sender is not PromptVariableField { VariableName: { } name } field)
        {
            return;
        }
        var text = field.Text ?? string.Empty;
        if (Values is { } values)
        {
            _syncing++;
            try
            {
                values[name] = text;
            }
            finally
            {
                _syncing--;
            }
        }
        RaiseEvent(new PromptVariableEventArgs(ValueEditedEvent, this, name));
    }

    #endregion

    #region Focus

    private void OnFieldGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not PromptVariableField { VariableName: { } name } || string.Equals(_activeVariable, name, StringComparison.Ordinal))
        {
            return;
        }
        ActiveVariable = name;
        RaiseEvent(new PromptVariableEventArgs(FieldFocusedEvent, this, name));
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not PromptVariableField field || !string.Equals(_activeVariable, field.VariableName, StringComparison.Ordinal))
        {
            return;
        }
        // Moving to another field sets that one when it gets the focus; anywhere else clears the active variable.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is PromptVariableField other && !ReferenceEquals(other, field) && _fields.Contains(other))
        {
            return;
        }
        ActiveVariable = null;
    }

    #endregion

    #region Colors

    /// <summary>Mixes the per-tone brushes from the theme (the dot, the label and the focus border) and hands them to the fields.</summary>
    private void UpdateBrushes()
    {
        var foreground = (GetValue(ForegroundProbeProperty) as ISolidColorBrush)?.Color;
        var border = (GetValue(BorderProbeProperty) as ISolidColorBrush)?.Color;
        for (var tone = 1; tone <= 4; tone++)
        {
            var probe = tone switch
            {
                1 => Series1ProbeProperty,
                2 => Series2ProbeProperty,
                3 => Series3ProbeProperty,
                _ => Series4ProbeProperty
            };
            if (GetValue(probe) is not ISolidColorBrush series)
            {
                _dotBrushes[tone - 1] = _labelBrushes[tone - 1] = _focusBrushes[tone - 1] = null;
                continue;
            }
            _dotBrushes[tone - 1] = new ImmutableSolidColorBrush(series.Color);
            _labelBrushes[tone - 1] = foreground is { } fg ? new ImmutableSolidColorBrush(PromptTone.Mix(fg, series.Color, LabelMix)) : null;
            _focusBrushes[tone - 1] = border is { } b ? new ImmutableSolidColorBrush(PromptTone.Mix(b, series.Color, FocusMix)) : null;
        }
        foreach (var field in _fields)
        {
            ApplyBrushes(field);
        }
    }

    private void ApplyBrushes(PromptVariableField field)
    {
        var index = Math.Clamp(field.Tone, 1, 4) - 1;
        field.DotBrush = _dotBrushes[index];
        field.LabelBrush = _labelBrushes[index];
        field.FocusBorderBrush = _focusBrushes[index];
    }

    #endregion

    /// <summary>A named group of edit fields (each named by its variable).</summary>
    private sealed class FieldsPeer(PromptVariableFields owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
