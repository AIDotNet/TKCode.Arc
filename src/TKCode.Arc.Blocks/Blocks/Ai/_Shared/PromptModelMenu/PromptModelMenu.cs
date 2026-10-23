using System.Collections.Specialized;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The prompt studio's model and effort menu: a 320px popover (a non-modal dialog) right-aligned 6px below the model pill.
/// The models are radio rows (mark, full name, "$in · $out" per million tokens, a check on <see cref="SelectedModel"/>);
/// under a line, "Effort" and a segmented row of the levels the selected model offers (<see cref="Effort"/>), and a note
/// on the prices (<see cref="PriceNote"/>) when any of them is a sample. Choosing a model keeps the menu open and sets the
/// effort to the model's default (Arc), raising <see cref="ModelChosen"/> then, when the effort changed,
/// <see cref="EffortChosen"/> (<c>IsSnap</c> true); choosing a level keeps it open too (<see cref="EffortChosen"/>).
/// <para>
/// This is the studio's lean menu, not M1's <c>ModelMenu</c> (no featured rows, no providers' submenu, no paging; prices
/// per row; the effort inline as a segmented row; a choice never closes). Keys: the menu opens with focus on the selected
/// model; in the models ↑ ↓ ← → move and choose (wrapping), Home and End go to the ends; in the levels the arrows, Home
/// and End choose a level; Tab moves between the models and the levels; Escape closes and gives focus back to the pill.
/// The menu holds no words: the studio sets every text property.
/// </para>
/// </summary>
internal sealed class PromptModelMenu : PromptPopoverMenu
{
    internal const double MenuWidth = 320;

    #region Properties

    /// <summary>The models, in menu order.</summary>
    public static readonly StyledProperty<IEnumerable<AiModel>?> ModelsProperty =
        AvaloniaProperty.Register<PromptModelMenu, IEnumerable<AiModel>?>(nameof(Models));

    /// <summary>The providers that supply the mark of a model without its own (by <see cref="AiModel.ProviderId"/>).</summary>
    public static readonly StyledProperty<IEnumerable<AiModelProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<PromptModelMenu, IEnumerable<AiModelProvider>?>(nameof(Providers));

    /// <summary>The selected model (two-way): its row is checked and its levels make the effort row.</summary>
    public static readonly StyledProperty<AiModel?> SelectedModelProperty =
        AvaloniaProperty.Register<PromptModelMenu, AiModel?>(nameof(SelectedModel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The effort (two-way); choosing another model sets it to that model's default.</summary>
    public static readonly StyledProperty<ModelEffort?> EffortProperty =
        AvaloniaProperty.Register<PromptModelMenu, ModelEffort?>(nameof(Effort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The accessible name of the radio group of models ("Model").</summary>
    public static readonly StyledProperty<string?> ModelsNameProperty =
        AvaloniaProperty.Register<PromptModelMenu, string?>(nameof(ModelsName));

    /// <summary>The title over the effort row, also its accessible name ("Effort").</summary>
    public static readonly StyledProperty<string?> EffortTitleProperty =
        AvaloniaProperty.Register<PromptModelMenu, string?>(nameof(EffortTitle));

    /// <summary>The short word of a level in its cell ("Med", "XHigh"); null uses <see cref="EffortName"/>.</summary>
    public static readonly StyledProperty<Func<ModelEffort, string>?> EffortCellTextProperty =
        AvaloniaProperty.Register<PromptModelMenu, Func<ModelEffort, string>?>(nameof(EffortCellText));

    /// <summary>The full name of a level for assistive technology ("Medium", "Extra high"); null uses <see cref="ModelEffortNames.Name"/>.</summary>
    public static readonly StyledProperty<Func<ModelEffort, string>?> EffortNameProperty =
        AvaloniaProperty.Register<PromptModelMenu, Func<ModelEffort, string>?>(nameof(EffortName));

    /// <summary>The note under the effort row, shown when any model's price is a sample (<see cref="AiModel.IsSamplePrice"/>).</summary>
    public static readonly StyledProperty<string?> PriceNoteProperty =
        AvaloniaProperty.Register<PromptModelMenu, string?>(nameof(PriceNote));

    /// <summary>One price as shown ("$2", "$0.10"); null uses dollars in the invariant culture (<see cref="PromptPriceText"/>).</summary>
    public static readonly StyledProperty<Func<decimal, string>?> PriceFormatProperty =
        AvaloniaProperty.Register<PromptModelMenu, Func<decimal, string>?>(nameof(PriceFormat));

    /// <summary>
    /// The prices for assistive technology, from the input and output prices as shown and whether they are samples ("$2
    /// input, $10 output per million tokens, sample price"); null reads the prices as shown.
    /// </summary>
    public static readonly StyledProperty<Func<string, string, bool, string>?> PriceNameProperty =
        AvaloniaProperty.Register<PromptModelMenu, Func<string, string, bool, string>?>(nameof(PriceName));

    /// <summary>Raised when a model is chosen in the menu (before <see cref="SelectedModel"/> changes; also the selected one).</summary>
    public static readonly RoutedEvent<ModelMenuModelEventArgs> ModelChosenEvent =
        RoutedEvent.Register<PromptModelMenu, ModelMenuModelEventArgs>(nameof(ModelChosen), RoutingStrategies.Bubble);

    /// <summary>Raised when a level is chosen, or when a new model set the effort to its default (<c>IsSnap</c> true).</summary>
    public static readonly RoutedEvent<ModelMenuEffortEventArgs> EffortChosenEvent =
        RoutedEvent.Register<PromptModelMenu, ModelMenuEffortEventArgs>(nameof(EffortChosen), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ModelsProperty"/>
    public IEnumerable<AiModel>? Models { get => GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }

    /// <inheritdoc cref="ProvidersProperty"/>
    public IEnumerable<AiModelProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="SelectedModelProperty"/>
    public AiModel? SelectedModel { get => GetValue(SelectedModelProperty); set => SetValue(SelectedModelProperty, value); }

    /// <inheritdoc cref="EffortProperty"/>
    public ModelEffort? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <inheritdoc cref="ModelsNameProperty"/>
    public string? ModelsName { get => GetValue(ModelsNameProperty); set => SetValue(ModelsNameProperty, value); }

    /// <inheritdoc cref="EffortTitleProperty"/>
    public string? EffortTitle { get => GetValue(EffortTitleProperty); set => SetValue(EffortTitleProperty, value); }

    /// <inheritdoc cref="EffortCellTextProperty"/>
    public Func<ModelEffort, string>? EffortCellText { get => GetValue(EffortCellTextProperty); set => SetValue(EffortCellTextProperty, value); }

    /// <inheritdoc cref="EffortNameProperty"/>
    public Func<ModelEffort, string>? EffortName { get => GetValue(EffortNameProperty); set => SetValue(EffortNameProperty, value); }

    /// <inheritdoc cref="PriceNoteProperty"/>
    public string? PriceNote { get => GetValue(PriceNoteProperty); set => SetValue(PriceNoteProperty, value); }

    /// <inheritdoc cref="PriceFormatProperty"/>
    public Func<decimal, string>? PriceFormat { get => GetValue(PriceFormatProperty); set => SetValue(PriceFormatProperty, value); }

    /// <inheritdoc cref="PriceNameProperty"/>
    public Func<string, string, bool, string>? PriceName { get => GetValue(PriceNameProperty); set => SetValue(PriceNameProperty, value); }

    /// <inheritdoc cref="ModelChosenEvent"/>
    public event EventHandler<ModelMenuModelEventArgs>? ModelChosen { add => AddHandler(ModelChosenEvent, value); remove => RemoveHandler(ModelChosenEvent, value); }

    /// <inheritdoc cref="EffortChosenEvent"/>
    public event EventHandler<ModelMenuEffortEventArgs>? EffortChosen { add => AddHandler(EffortChosenEvent, value); remove => RemoveHandler(EffortChosenEvent, value); }

    #endregion

    private readonly List<PromptModelRow> _rows = new();
    private readonly List<AiModel> _observedModels = new();
    private PromptModelRowsPanel? _rowsPanel;
    private Border? _footer;
    private TextBlock? _effortTitle;
    private PromptEffortStrip? _strip;
    private TextBlock? _note;
    private INotifyCollectionChanged? _observedCollection;
    private INotifyCollectionChanged? _observedProviders;

    protected override PlacementMode Placement => PlacementMode.BottomEdgeAlignedRight;

    protected override double SurfaceWidth => MenuWidth;

    /// <summary>The model rows, in order (empty until the first open).</summary>
    internal IReadOnlyList<PromptModelRow> Rows => _rows;

    internal PromptEffortStrip? Strip => _strip;

    internal TextBlock? EffortTitleBlock => _effortTitle;

    internal TextBlock? Note => _note;

    internal Border? Footer => _footer;

    internal PromptModelRowsPanel? RowsPanel => _rowsPanel;

    #region Content

    protected override Control CreateContent()
    {
        _rowsPanel = new PromptModelRowsPanel();
        AutomationProperties.SetName(_rowsPanel, ModelsName);
        _effortTitle = new TextBlock
        {
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 12,
            LineHeight = 16.8,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ArcTokens.Bind(_effortTitle, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetAccessibilityView(_effortTitle, AccessibilityView.Raw);
        _strip = new PromptEffortStrip { Owner = this };
        _note = new TextBlock
        {
            Margin = new Thickness(4, 0),
            FontSize = 12,
            LineHeight = 16.8,
            TextWrapping = TextWrapping.Wrap
        };
        ArcTokens.Bind(_note, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        // 6px down, a 1px BorderSubtle line, padding 10 6 4; the title, the levels and the note 8px apart.
        _footer = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(6, 10, 6, 4),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Spacing = 8, Children = { _effortTitle, _strip, _note } }
        };
        ArcTokens.Bind(_footer, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return new StackPanel { Children = { _rowsPanel, _footer } };
    }

    protected override void OnOpening()
    {
        ObserveData(true);
        BuildRows();
        RefreshFooter();
        _strip?.ResetThumb();
    }

    protected override void OnHidden() => ObserveData(false);

    private List<AiModel> DistinctModels()
    {
        var result = new List<AiModel>();
        if (Models is { } source)
        {
            foreach (var model in source)
            {
                if (model is not null && !result.Contains(model))
                {
                    result.Add(model);
                }
            }
        }
        return result;
    }

    private PromptModelRow? RowOf(AiModel? model) => model is null ? null : _rows.FirstOrDefault(r => ReferenceEquals(r.Model, model));

    private void BuildRows()
    {
        if (_rowsPanel is null)
        {
            return;
        }
        var focused = _rows.FirstOrDefault(r => r.IsKeyboardFocusWithin)?.Model;
        _rowsPanel.Children.Clear();
        _rows.Clear();
        foreach (var model in DistinctModels())
        {
            var row = new PromptModelRow(model) { Owner = this };
            _rows.Add(row);
            _rowsPanel.Children.Add(row);
        }
        RefreshRows();
        if (focused is not null && RowOf(focused) is { } again)
        {
            again.Focus(UsesKeyboard ? NavigationMethod.Directional : NavigationMethod.Pointer);
        }
    }

    /// <summary>Mark, name, prices, check and accessible name of every row.</summary>
    private void RefreshRows()
    {
        foreach (var row in _rows)
        {
            var model = row.Model;
            row.Mark = ModelMark.Resolve(model, Providers, out var color);
            row.MarkColor = color;
            row.Text = model.Name;
            row.Price = PromptPriceText.Pair(model.InputPrice, model.OutputPrice, PriceFormat);
            row.IsChecked = ReferenceEquals(model, SelectedModel);
            AutomationProperties.SetName(row, RowName(model, row.Price));
        }
    }

    private string RowName(AiModel model, string? price)
    {
        if (price is null)
        {
            return model.Name;
        }
        if (PriceName is { } name)
        {
            var format = PriceFormat ?? PromptPriceText.Format;
            var input = model.InputPrice is { } i ? format(i) : "—";
            var output = model.OutputPrice is { } o ? format(o) : "—";
            return model.Name + ", " + name(input, output, model.IsSamplePrice);
        }
        return model.Name + ", " + price;
    }

    /// <summary>The effort row for the selected model's levels, the title and the note; the footer hides when it has nothing.</summary>
    private void RefreshFooter()
    {
        if (_footer is null || _strip is null || _effortTitle is null || _note is null)
        {
            return;
        }
        var levels = SelectedModel?.EffortLevels is { Count: > 0 } offered
            ? offered.Distinct().OrderBy(static l => (int)l).ToArray()
            : Array.Empty<ModelEffort>();
        var hasEffort = levels.Length > 0;
        var name = EffortName ?? ModelEffortNames.Name;
        _strip.SetLevels(levels, EffortCellText ?? name, name);
        _strip.Level = Effort;
        _strip.IsVisible = hasEffort;
        _effortTitle.Text = EffortTitle;
        _effortTitle.IsVisible = hasEffort && !string.IsNullOrEmpty(EffortTitle);
        AutomationProperties.SetName(_strip, EffortTitle);
        var sample = !string.IsNullOrEmpty(PriceNote) && DistinctModels().Any(m => m.IsSamplePrice);
        _note.Text = PriceNote;
        _note.IsVisible = sample;
        _footer.IsVisible = hasEffort || sample;
    }

    #endregion

    #region Input

    internal void OnRowInvoked(PromptModelRow row, NavigationMethod method)
    {
        if (!IsShown)
        {
            return;
        }
        if (method == NavigationMethod.Pointer)
        {
            UsesKeyboard = false;
        }
        Choose(row, method == NavigationMethod.Unspecified ? NavigationMethod.Directional : method);
    }

    /// <summary>
    /// Picks the row's model: <see cref="ModelChosen"/>, then <see cref="SelectedModel"/>, then (for another model) the
    /// effort goes to the model's default and <see cref="EffortChosen"/> reports it. The menu stays open.
    /// </summary>
    private void Choose(PromptModelRow row, NavigationMethod method)
    {
        var model = row.Model;
        var previous = SelectedModel;
        RaiseEvent(new ModelMenuModelEventArgs(ModelChosenEvent, this, model, previous));
        SetCurrentValue(SelectedModelProperty, model);
        row.Focus(method);
        if (ReferenceEquals(model, previous))
        {
            return;
        }
        var before = Effort;
        var reset = model.SupportsEffort ? ModelEffortSnap.Snap(null, model.EffortLevels, model.DefaultEffort) : null;
        if (reset != before)
        {
            SetCurrentValue(EffortProperty, reset);
            RaiseEvent(new ModelMenuEffortEventArgs(EffortChosenEvent, this, reset, before, isSnap: true));
        }
    }

    internal void OnCellInvoked(PromptEffortCell cell, NavigationMethod method)
    {
        if (!IsShown)
        {
            return;
        }
        if (method == NavigationMethod.Pointer)
        {
            UsesKeyboard = false;
        }
        ChooseLevel(cell.Level, method == NavigationMethod.Unspecified ? NavigationMethod.Directional : method);
    }

    private void ChooseLevel(ModelEffort level, NavigationMethod method)
    {
        var before = Effort;
        SetCurrentValue(EffortProperty, level);
        _strip?.CellOf(level)?.Focus(method);
        RaiseEvent(new ModelMenuEffortEventArgs(EffortChosenEvent, this, level, before, isSnap: false));
    }

    protected override void OnMenuKeyDown(KeyEventArgs e)
    {
        if (e.Source is not Visual source || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }
        var index = _rows.FindIndex(r => ReferenceEquals(r, source) || r.IsVisualAncestorOf(source));
        if (index >= 0)
        {
            var count = _rows.Count;
            int? next = e.Key switch
            {
                Key.Down or Key.Right => (index + 1) % count,
                Key.Up or Key.Left => (index - 1 + count) % count,
                Key.Home => 0,
                Key.End => count - 1,
                Key.Space => index,
                _ => null
            };
            if (next is { } target)
            {
                e.Handled = true;
                UsesKeyboard = true;
                Choose(_rows[target], NavigationMethod.Directional);
            }
            else if (e.Key == Key.Enter)
            {
                // Enter does nothing on a radio (the model under focus is already chosen).
                e.Handled = true;
            }
            return;
        }
        if (_strip is not { } strip || strip.Cells.FirstOrDefault(c => ReferenceEquals(c, source) || c.IsVisualAncestorOf(source)) is not { } cell)
        {
            return;
        }
        ModelEffort? level = e.Key switch
        {
            Key.Right or Key.Down => strip.Step(1),
            Key.Left or Key.Up => strip.Step(-1),
            Key.Home => strip.Cells.FirstOrDefault()?.Level,
            Key.End => strip.Cells.LastOrDefault()?.Level,
            Key.Space => cell.Level,
            _ => null
        };
        if (level is { } chosen)
        {
            e.Handled = true;
            UsesKeyboard = true;
            ChooseLevel(chosen, NavigationMethod.Directional);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
        }
    }

    protected override void ApplyOpenFocus(ModelMenuOpenFocus focus)
    {
        var row = RowOf(SelectedModel) ?? _rows.FirstOrDefault();
        if (row is null)
        {
            Surface?.Focus(NavigationMethod.Pointer);
            return;
        }
        row.Focus(focus == ModelMenuOpenFocus.Surface ? NavigationMethod.Pointer : NavigationMethod.Tab);
    }

    protected override IEnumerable<Control> TabStops()
    {
        if ((RowOf(SelectedModel) ?? _rows.FirstOrDefault()) is { } row)
        {
            yield return row;
        }
        if (_strip is { IsVisible: true } strip && (strip.CellOf(Effort) ?? strip.Cells.FirstOrDefault()) is { } cell)
        {
            yield return cell;
        }
    }

    #endregion

    #region Changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelsProperty)
        {
            OnModelsChanged();
        }
        else if (change.Property == SelectedModelProperty)
        {
            RefreshRows();
            RefreshFooter();
        }
        else if (change.Property == EffortProperty)
        {
            if (_strip is not null)
            {
                _strip.Level = Effort;
            }
        }
        else if (change.Property == ProvidersProperty)
        {
            if (IsOpen)
            {
                ObserveData(true);
            }
            RefreshRows();
        }
        else if (change.Property == PriceFormatProperty || change.Property == PriceNameProperty)
        {
            RefreshRows();
        }
        else if (change.Property == EffortTitleProperty || change.Property == EffortCellTextProperty || change.Property == EffortNameProperty ||
                 change.Property == PriceNoteProperty)
        {
            RefreshFooter();
        }
        else if (change.Property == ModelsNameProperty && _rowsPanel is not null)
        {
            AutomationProperties.SetName(_rowsPanel, ModelsName);
        }
    }

    private void OnModelsChanged()
    {
        if (IsOpen)
        {
            ObserveData(true);
            BuildRows();
            RefreshFooter();
        }
    }

    // The models and providers are app data that can outlive the menu: they are followed only while it is open.
    private void ObserveData(bool observe)
    {
        var next = observe ? Models as INotifyCollectionChanged : null;
        if (!ReferenceEquals(next, _observedCollection))
        {
            if (_observedCollection is not null)
            {
                _observedCollection.CollectionChanged -= OnCollectionChanged;
            }
            _observedCollection = next;
            if (next is not null)
            {
                next.CollectionChanged += OnCollectionChanged;
            }
        }
        var providers = observe ? Providers as INotifyCollectionChanged : null;
        if (!ReferenceEquals(providers, _observedProviders))
        {
            if (_observedProviders is not null)
            {
                _observedProviders.CollectionChanged -= OnProvidersChanged;
            }
            _observedProviders = providers;
            if (providers is not null)
            {
                providers.CollectionChanged += OnProvidersChanged;
            }
        }
        foreach (var model in _observedModels)
        {
            model.PropertyChanged -= OnModelPropertyChanged;
        }
        _observedModels.Clear();
        if (observe)
        {
            foreach (var model in DistinctModels())
            {
                model.PropertyChanged += OnModelPropertyChanged;
                _observedModels.Add(model);
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnModelsChanged();

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshRows();

    private void OnModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiModel.EffortLevelsProperty || e.Property == AiModel.DefaultEffortProperty || e.Property == AiModel.IsSamplePriceProperty)
        {
            RefreshFooter();
        }
        RefreshRows();
    }

    #endregion
}
