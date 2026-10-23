using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The model picker's menu: a short list of featured models (name and a one-line blurb, a check on the selected one), then
/// an Effort row (the selected model's effort levels) and a More models row (every other model, grouped by provider) that
/// open submenus beside the menu, or pages inside it in a narrow window. It renders nothing in place: declare it next to
/// its trigger (usually a <c>ModelPill</c>) and call <see cref="Toggle"/> from the trigger.
/// <code>
/// &lt;arc:ModelMenu x:Name="Menu" FeaturedIds="{Binding Featured}" ModelsSource="{Binding Models}"
///               SelectedModel="{Binding Model}" Effort="{Binding Effort}" PlacementTarget="{Binding #Pill}" /&gt;
/// </code>
/// <para>
/// One highlight moves between the rows: it glides with the pointer, jumps with the keyboard and fades in or out in place.
/// Keys: ↑ ↓ (wrapping), Home, End, a letter jumps to the next row starting with it, Enter or Space chooses, → opens a
/// submenu (focus on the selected entry), ← closes it, Escape closes everything and gives focus back to the anchor.
/// Choosing a model or a level raises <see cref="ModelChosen"/> / <see cref="EffortChosen"/>, updates
/// <see cref="SelectedModel"/> / <see cref="Effort"/> (the effort snaps to a level the new model offers) and closes the menu.
/// </para>
/// <para>
/// The AI composer's menu is <see cref="EffortLayout"/> <see cref="ModelMenuEffortLayout.Inline"/> with
/// <see cref="ShowMoreModels"/> false and <see cref="Placement"/> <c>TopEdgeAlignedRight</c>: 296px wide, every model in
/// one list (a check in the text color on the selected one), then an "Effort" header with the current level's description
/// and a segmented row of the levels the selected model offers. The segmented row is one stop for ↑ ↓; ← → change the
/// level at once and a click on a level changes it too, without closing the menu (choosing a model still closes it).
/// </para>
/// </summary>
public partial class ModelMenu : TemplatedControl
{
    #region Properties

    /// <summary>The models to offer; when set it replaces <see cref="Models"/>. Collection changes are followed.</summary>
    public static readonly StyledProperty<IEnumerable<AiModel>?> ModelsSourceProperty =
        AvaloniaProperty.Register<ModelMenu, IEnumerable<AiModel>?>(nameof(ModelsSource));

    /// <summary>The selected model (two-way). Null when the menu loads selects the first featured model once and writes it back.</summary>
    public static readonly StyledProperty<AiModel?> SelectedModelProperty =
        AvaloniaProperty.Register<ModelMenu, AiModel?>(nameof(SelectedModel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The <see cref="AiModel.Id"/> of the selected model (two-way), kept in sync with <see cref="SelectedModel"/>.</summary>
    public static readonly StyledProperty<string?> SelectedModelIdProperty =
        AvaloniaProperty.Register<ModelMenu, string?>(nameof(SelectedModelId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The effort level (two-way). It snaps to a level the selected model offers whenever the model changes (null for a
    /// model without effort levels); a level the model does not offer is moved to the nearest one it does.
    /// </summary>
    public static readonly StyledProperty<ModelEffort?> EffortProperty =
        AvaloniaProperty.Register<ModelMenu, ModelEffort?>(nameof(Effort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the menu is open (two-way). Opening needs a <see cref="PlacementTarget"/>.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModelMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The anchor the menu opens against (the pill). <see cref="Open"/> and <see cref="Toggle"/> set it.</summary>
    public static readonly StyledProperty<Control?> PlacementTargetProperty =
        AvaloniaProperty.Register<ModelMenu, Control?>(nameof(PlacementTarget));

    /// <summary>When the window's client width is at most this when the menu opens (default 640), submenus become pages inside the menu.</summary>
    public static readonly StyledProperty<double> PagedBelowWidthProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(PagedBelowWidth), 640);

    /// <summary>Gap between the anchor and the menu (default 8).</summary>
    public static readonly StyledProperty<double> AnchorGapProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(AnchorGap), 8);

    /// <summary>Every word the menu shows or announces, with English defaults.</summary>
    public static readonly StyledProperty<ModelMenuLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelMenu, ModelMenuLabels>(nameof(Labels), new ModelMenuLabels());

    /// <summary>
    /// Where the effort levels are offered: an Effort row with a submenu (<see cref="ModelMenuEffortLayout.Submenu"/>,
    /// the model picker) or a segmented row inside the menu (<see cref="ModelMenuEffortLayout.Inline"/>, the AI composer,
    /// which also gives the menu the composer's look: 296px, one-line model rows).
    /// </summary>
    public static readonly StyledProperty<ModelMenuEffortLayout> EffortLayoutProperty =
        AvaloniaProperty.Register<ModelMenu, ModelMenuEffortLayout>(nameof(EffortLayout));

    /// <summary>
    /// Whether a "More models" row opens the models that are not featured, grouped by provider (default true). False
    /// lists every model in the menu itself: the featured ones first, then the rest in catalog order.
    /// </summary>
    public static readonly StyledProperty<bool> ShowMoreModelsProperty =
        AvaloniaProperty.Register<ModelMenu, bool>(nameof(ShowMoreModels), true);

    /// <summary>
    /// Where the menu opens against its anchor (default <see cref="PlacementMode.Bottom"/>: below, centered). The AI
    /// composer opens it above its chip, right edges aligned (<see cref="PlacementMode.TopEdgeAlignedRight"/>). The menu
    /// flips to the other side when there is no room and always keeps 12px from the window's left and right edges.
    /// </summary>
    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<ModelMenu, PlacementMode>(nameof(Placement), PlacementMode.Bottom);

    /// <summary>
    /// How the models are listed: the model picker's two-line rows (<see cref="ModelMenuLayout.Featured"/>, default) or one
    /// short row each with its note (<see cref="ModelMenuLayout.Simple"/>, the AI chat's header menu).
    /// </summary>
    public static readonly StyledProperty<ModelMenuLayout> LayoutProperty =
        AvaloniaProperty.Register<ModelMenu, ModelMenuLayout>(nameof(Layout));

    /// <summary>Width of the main menu (the theme sets the token value).</summary>
    internal static readonly StyledProperty<double> SurfaceWidthProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(SurfaceWidth), 304);

    /// <summary>Width of the main menu in the inline layout (the theme sets the token value).</summary>
    internal static readonly StyledProperty<double> InlineSurfaceWidthProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(InlineSurfaceWidth), 296);

    /// <summary>The narrowest simple menu (the theme sets the token value).</summary>
    internal static readonly StyledProperty<double> SimpleMinWidthProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(SimpleMinWidth), 240);

    internal static readonly StyledProperty<double> EffortMenuWidthProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(EffortMenuWidth), 264);

    internal static readonly StyledProperty<double> MoreMenuWidthProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(MoreMenuWidth), 272);

    internal static readonly StyledProperty<double> ViewportInsetProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(ViewportInset), 24);

    internal static readonly StyledProperty<double> SubmenuOffsetProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(SubmenuOffset), -7);

    internal static readonly StyledProperty<double> SubmenuGapProperty =
        AvaloniaProperty.Register<ModelMenu, double>(nameof(SubmenuGap), 1);

    /// <summary>Raised on every model picked in the menu (also the one already selected: see <see cref="ModelMenuModelEventArgs.IsChange"/>), before <see cref="SelectedModel"/> changes.</summary>
    public static readonly RoutedEvent<ModelMenuModelEventArgs> ModelChosenEvent =
        RoutedEvent.Register<ModelMenu, ModelMenuModelEventArgs>(nameof(ModelChosen), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when a level is picked in the effort list (<see cref="ModelMenuEffortEventArgs.IsSnap"/> false, also for the
    /// current level) and when a change of model moves the effort to a level the new model offers (true). Not raised when
    /// code sets <see cref="Effort"/>.
    /// </summary>
    public static readonly RoutedEvent<ModelMenuEffortEventArgs> EffortChosenEvent =
        RoutedEvent.Register<ModelMenu, ModelMenuEffortEventArgs>(nameof(EffortChosen), RoutingStrategies.Bubble);

    /// <summary>Raised when the menu has opened.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<ModelMenu, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Bubble);

    /// <summary>Raised when the menu has closed (after its exit motion).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<ModelMenu, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ModelsSourceProperty"/>
    public IEnumerable<AiModel>? ModelsSource { get => GetValue(ModelsSourceProperty); set => SetValue(ModelsSourceProperty, value); }

    /// <inheritdoc cref="SelectedModelProperty"/>
    public AiModel? SelectedModel { get => GetValue(SelectedModelProperty); set => SetValue(SelectedModelProperty, value); }

    /// <inheritdoc cref="SelectedModelIdProperty"/>
    public string? SelectedModelId { get => GetValue(SelectedModelIdProperty); set => SetValue(SelectedModelIdProperty, value); }

    /// <inheritdoc cref="EffortProperty"/>
    public ModelEffort? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="PlacementTargetProperty"/>
    public Control? PlacementTarget { get => GetValue(PlacementTargetProperty); set => SetValue(PlacementTargetProperty, value); }

    /// <inheritdoc cref="PagedBelowWidthProperty"/>
    public double PagedBelowWidth { get => GetValue(PagedBelowWidthProperty); set => SetValue(PagedBelowWidthProperty, value); }

    /// <inheritdoc cref="AnchorGapProperty"/>
    public double AnchorGap { get => GetValue(AnchorGapProperty); set => SetValue(AnchorGapProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelMenuLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="EffortLayoutProperty"/>
    public ModelMenuEffortLayout EffortLayout { get => GetValue(EffortLayoutProperty); set => SetValue(EffortLayoutProperty, value); }

    /// <inheritdoc cref="ShowMoreModelsProperty"/>
    public bool ShowMoreModels { get => GetValue(ShowMoreModelsProperty); set => SetValue(ShowMoreModelsProperty, value); }

    /// <inheritdoc cref="PlacementProperty"/>
    public PlacementMode Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <inheritdoc cref="LayoutProperty"/>
    public ModelMenuLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    internal double SimpleMinWidth { get => GetValue(SimpleMinWidthProperty); set => SetValue(SimpleMinWidthProperty, value); }

    internal double SurfaceWidth { get => GetValue(SurfaceWidthProperty); set => SetValue(SurfaceWidthProperty, value); }
    internal double InlineSurfaceWidth { get => GetValue(InlineSurfaceWidthProperty); set => SetValue(InlineSurfaceWidthProperty, value); }
    internal double EffortMenuWidth { get => GetValue(EffortMenuWidthProperty); set => SetValue(EffortMenuWidthProperty, value); }
    internal double MoreMenuWidth { get => GetValue(MoreMenuWidthProperty); set => SetValue(MoreMenuWidthProperty, value); }
    internal double ViewportInset { get => GetValue(ViewportInsetProperty); set => SetValue(ViewportInsetProperty, value); }
    internal double SubmenuOffset { get => GetValue(SubmenuOffsetProperty); set => SetValue(SubmenuOffsetProperty, value); }
    internal double SubmenuGap { get => GetValue(SubmenuGapProperty); set => SetValue(SubmenuGapProperty, value); }

    /// <summary>The models declared inline (used when <see cref="ModelsSource"/> is null).</summary>
    [Content]
    public AvaloniaList<AiModel> Models { get; } = new();

    /// <summary>
    /// The providers: their order is the order of the groups in "More models", their names title the groups and their marks
    /// stand in for models without one. Providers that are not listed come last, in the order their models first appear.
    /// </summary>
    public AvaloniaList<AiModelProvider> Providers { get; } = new();

    /// <summary>The ids of the models shown first, in this order; empty shows the first four models.</summary>
    public AvaloniaList<string> FeaturedIds { get; } = new();

    /// <summary>The models at the top of the menu, resolved from <see cref="FeaturedIds"/> (or the first four).</summary>
    public IReadOnlyList<AiModel> FeaturedModels => _featured;

    /// <summary>Every other model, in "More models" order (grouped by provider).</summary>
    public IReadOnlyList<AiModel> OtherModels => _other;

    /// <inheritdoc cref="ModelChosenEvent"/>
    public event EventHandler<ModelMenuModelEventArgs>? ModelChosen
    {
        add => AddHandler(ModelChosenEvent, value);
        remove => RemoveHandler(ModelChosenEvent, value);
    }

    /// <inheritdoc cref="EffortChosenEvent"/>
    public event EventHandler<ModelMenuEffortEventArgs>? EffortChosen
    {
        add => AddHandler(EffortChosenEvent, value);
        remove => RemoveHandler(EffortChosenEvent, value);
    }

    /// <inheritdoc cref="OpenedEvent"/>
    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <inheritdoc cref="ClosedEvent"/>
    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    /// <summary>A provider group of "More models": its id, title and models in catalog order.</summary>
    internal sealed record ProviderGroup(string Id, string Name, List<AiModel> Models);

    private readonly SourceWatcher _watcher;
    private readonly List<AiModel> _all = new();
    private readonly List<AiModel> _featured = new();
    private readonly List<AiModel> _other = new();
    private readonly List<ProviderGroup> _groups = new();
    private HashSet<AvaloniaObject> _subscribed = new();
    private HashSet<AvaloniaObject> _current = new();
    private INotifyCollectionChanged? _observedSource;
    private bool _syncingSelection;
    private bool _quietSnap;
    private bool _snapping;
    private bool _deferSnap;
    private bool _defaultApplied;

    static ModelMenu()
    {
        IsVisibleProperty.OverrideDefaultValue<ModelMenu>(false);
        FocusableProperty.OverrideDefaultValue<ModelMenu>(false);
    }

    /// <summary>Creates a closed menu without models.</summary>
    public ModelMenu()
    {
        _watcher = new SourceWatcher(this);
        Models.CollectionChanged += OnCollectionChanged;
        Providers.CollectionChanged += OnCollectionChanged;
        FeaturedIds.CollectionChanged += OnCollectionChanged;
    }

    /// <summary>The provider groups of "More models", in order.</summary>
    internal IReadOnlyList<ProviderGroup> Groups => _groups;

    /// <summary>The clock of typeahead and the pointer's safe zone (tests replace it).</summary>
    internal Func<DateTime> Now { get; set; } = static () => DateTime.UtcNow;

    // A templated control without a template measures nothing: the menu never takes space in a layout.
    protected override Size MeasureOverride(Size availableSize) => default;

    #region Data

    private IEnumerable<AiModel> Source => ModelsSource ?? Models;

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Models) && ModelsSource is not null)
        {
            return;
        }
        Refresh();
    }

    internal void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    internal void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is AiModel model && ReferenceEquals(model, SelectedModel) &&
            (e.Property == AiModel.EffortLevelsProperty || e.Property == AiModel.DefaultEffortProperty))
        {
            // The selected model's levels changed: keep the effort on one it offers.
            SnapEffort(model, raise: false);
        }
        if (e.Property == AiModel.IdProperty || e.Property == AiModel.ProviderIdProperty || e.Property == AiModelProvider.IdProperty)
        {
            Refresh();
            return;
        }
        InvalidatePages();
    }

    private void ObserveSource()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= _watcher.OnCollectionChanged;
            _observedSource = null;
        }
        if (ModelsSource is INotifyCollectionChanged observable)
        {
            _observedSource = observable;
            observable.CollectionChanged += _watcher.OnCollectionChanged;
        }
    }

    /// <summary>Resolves the featured and other models and the provider groups, then shows them.</summary>
    private void Refresh()
    {
        _all.Clear();
        foreach (var model in Source)
        {
            if (model is not null && !_all.Contains(model))
            {
                _all.Add(model);
            }
        }
        Follow();
        _featured.Clear();
        if (FeaturedIds.Count > 0)
        {
            foreach (var id in FeaturedIds)
            {
                if (Find(id) is { } model && !_featured.Contains(model))
                {
                    _featured.Add(model);
                }
            }
        }
        else
        {
            for (var i = 0; i < _all.Count && _featured.Count < 4; i++)
            {
                _featured.Add(_all[i]);
            }
        }
        BuildGroups();
        ResolveSelection();
        TryApplyDefault();
        InvalidatePages();
    }

    /// <summary>Groups the other models by provider: listed providers first, in list order, then the rest as they appear.</summary>
    private void BuildGroups()
    {
        _groups.Clear();
        _other.Clear();
        var byProvider = new Dictionary<string, ProviderGroup>(StringComparer.Ordinal);
        var appearance = new List<ProviderGroup>();
        foreach (var model in _all)
        {
            if (_featured.Contains(model))
            {
                continue;
            }
            var id = model.ProviderId ?? string.Empty;
            if (!byProvider.TryGetValue(id, out var group))
            {
                group = new ProviderGroup(id, ProviderName(id), new List<AiModel>());
                byProvider.Add(id, group);
                appearance.Add(group);
            }
            group.Models.Add(model);
        }
        foreach (var provider in Providers)
        {
            if (provider is not null && byProvider.Remove(provider.Id, out var group))
            {
                _groups.Add(group);
            }
        }
        foreach (var group in appearance)
        {
            if (byProvider.ContainsKey(group.Id))
            {
                _groups.Add(group);
            }
        }
        foreach (var group in _groups)
        {
            _other.AddRange(group.Models);
        }
    }

    private string ProviderName(string id)
    {
        foreach (var provider in Providers)
        {
            if (provider is not null && provider.Id == id)
            {
                return string.IsNullOrEmpty(provider.Name) ? id : provider.Name;
            }
        }
        return id;
    }

    /// <summary>Follows the property changes of every model and provider (names, marks, levels) through a weak link.</summary>
    private void Follow()
    {
        _current.Clear();
        foreach (var model in _all)
        {
            _current.Add(model);
        }
        foreach (var provider in Providers)
        {
            if (provider is not null)
            {
                _current.Add(provider);
            }
        }
        foreach (var item in _subscribed)
        {
            if (!_current.Contains(item))
            {
                item.PropertyChanged -= _watcher.OnItemPropertyChanged;
            }
        }
        foreach (var item in _current)
        {
            if (!_subscribed.Contains(item))
            {
                item.PropertyChanged += _watcher.OnItemPropertyChanged;
            }
        }
        (_subscribed, _current) = (_current, _subscribed);
    }

    private AiModel? Find(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        foreach (var model in _all)
        {
            if (model.Id == id)
            {
                return model;
            }
        }
        return null;
    }

    #endregion

    #region Selection

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelsSourceProperty)
        {
            ObserveSource();
            Refresh();
        }
        else if (change.Property == SelectedModelProperty)
        {
            OnSelectedModelChanged(change.GetNewValue<AiModel?>());
        }
        else if (change.Property == SelectedModelIdProperty)
        {
            OnSelectedModelIdChanged(change.GetNewValue<string?>());
        }
        else if (change.Property == EffortProperty)
        {
            OnEffortChanged();
        }
        else if (change.Property == LabelsProperty)
        {
            InvalidatePages();
        }
        else if (change.Property == EffortLayoutProperty || change.Property == ShowMoreModelsProperty || change.Property == LayoutProperty)
        {
            OnLayoutChanged();
        }
        else
        {
            OnNavigationPropertyChanged(change);
        }
    }

    private void OnSelectedModelChanged(AiModel? model)
    {
        if (!_syncingSelection)
        {
            _syncingSelection = true;
            try
            {
                SetCurrentValue(SelectedModelIdProperty, model?.Id);
            }
            finally
            {
                _syncingSelection = false;
            }
        }
        if (model is not null)
        {
            _defaultApplied = true;
            if (!_deferSnap)
            {
                // Values given before the menu is in a tree are its starting state, not a change anyone made.
                SnapEffort(model, raise: !_quietSnap && ((ILogical)this).IsAttachedToLogicalTree);
            }
        }
        InvalidatePages();
    }

    private void OnSelectedModelIdChanged(string? id)
    {
        if (_syncingSelection)
        {
            return;
        }
        var model = Find(id);
        if (ReferenceEquals(model, SelectedModel))
        {
            return;
        }
        // An id the catalog does not have (yet) clears the model but is kept, so it resolves when the models arrive.
        _syncingSelection = true;
        try
        {
            SetCurrentValue(SelectedModelProperty, model);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>A pending <see cref="SelectedModelId"/> finds its model once the catalog has it.</summary>
    private void ResolveSelection()
    {
        if (SelectedModel is null && Find(SelectedModelId) is { } model)
        {
            Quietly(() =>
            {
                _syncingSelection = true;
                try
                {
                    SetCurrentValue(SelectedModelProperty, model);
                }
                finally
                {
                    _syncingSelection = false;
                }
            });
        }
    }

    /// <summary>Selects the first featured model once, when the menu is in a tree and nothing was selected.</summary>
    private void TryApplyDefault()
    {
        if (_defaultApplied || !((ILogical)this).IsAttachedToLogicalTree)
        {
            return;
        }
        if (SelectedModel is not null || !string.IsNullOrEmpty(SelectedModelId))
        {
            _defaultApplied = true;
            return;
        }
        if (_featured.Count > 0)
        {
            _defaultApplied = true;
            var first = _featured[0];
            Quietly(() => SetCurrentValue(SelectedModelProperty, first));
        }
    }

    private void Quietly(Action action)
    {
        var quiet = _quietSnap;
        _quietSnap = true;
        try
        {
            action();
        }
        finally
        {
            _quietSnap = quiet;
        }
    }

    /// <summary>Moves the effort to a level <paramref name="model"/> offers; reports it as a snap when asked.</summary>
    private void SnapEffort(AiModel model, bool raise)
    {
        var previous = Effort;
        var snapped = model.SnapEffort(previous);
        if (snapped == previous)
        {
            return;
        }
        _snapping = true;
        try
        {
            SetCurrentValue(EffortProperty, snapped);
        }
        finally
        {
            _snapping = false;
        }
        if (raise)
        {
            RaiseEvent(new ModelMenuEffortEventArgs(EffortChosenEvent, this, snapped, previous, isSnap: true));
        }
    }

    private void OnEffortChanged()
    {
        if (!_snapping && SelectedModel is { } model)
        {
            // Code chose a level the model does not offer (or none for a model that has levels): keep it valid, quietly.
            SnapEffort(model, raise: false);
        }
        if (!UpdateEffortInPlace())
        {
            InvalidatePages();
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        TryApplyDefault();
    }

    #endregion

    /// <summary>
    /// What the data (the <see cref="ModelsSource"/>, every model and provider) holds on to: a weak link to the menu, so a
    /// catalog that lives on in the app never keeps a menu alive; the link drops itself the next time a gone menu is told.
    /// </summary>
    internal sealed class SourceWatcher(ModelMenu owner)
    {
        private readonly WeakReference<ModelMenu> _owner = new(owner);

        public void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_owner.TryGetTarget(out var target))
            {
                target.OnSourceChanged(sender, e);
            }
            else if (sender is INotifyCollectionChanged source)
            {
                source.CollectionChanged -= OnCollectionChanged;
            }
        }

        public void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_owner.TryGetTarget(out var target))
            {
                target.OnItemPropertyChanged(sender, e);
            }
            else if (sender is AvaloniaObject item)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }
    }
}
