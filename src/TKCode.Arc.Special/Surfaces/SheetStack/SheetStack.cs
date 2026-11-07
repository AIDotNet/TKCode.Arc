using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A stack of nested modal sheets for short drill-down flows (settings, filters, account details): each level opens on
/// top of the last, and the levels below shrink by 5%, darken and keep a strip of their top edge in view. Below
/// <see cref="Breakpoint"/> the levels are bottom sheets that can be dragged or flicked down to close; from it up they are
/// centered dialogs. The page content is the control's <see cref="ContentControl.Content"/>; the levels are declared in
/// <see cref="Sheets"/> and opened by id with <see cref="Push"/> or a button carrying <see cref="OpenSheetProperty"/>.
/// </summary>
[TemplatePart(LayerHostPart, typeof(Border))]
[PseudoClasses(OpenPseudoClass, SheetModePseudoClass, DialogModePseudoClass)]
public class SheetStack : ContentControl
{
    public const string LayerHostPart = "PART_LayerHost";
    public const string OpenPseudoClass = ":open";
    public const string SheetModePseudoClass = ":sheet-mode";
    public const string DialogModePseudoClass = ":dialog-mode";

    /// <summary>
    /// The ids of the open levels, bottom first (two-way). Setting it opens and closes levels to match: the levels past the
    /// common part leave and the new ones rise. Unknown and repeated ids are ignored.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string>> StackProperty =
        AvaloniaProperty.Register<SheetStack, IReadOnlyList<string>>(nameof(Stack), Array.Empty<string>(),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sheets, dialogs, or by width (<see cref="SheetStackMode.Auto"/>, the default).</summary>
    public static readonly StyledProperty<SheetStackMode> ModeProperty =
        AvaloniaProperty.Register<SheetStack, SheetStackMode>(nameof(Mode));

    /// <summary>
    /// The width from which <see cref="SheetStackMode.Auto"/> shows dialogs (default 640): the control's own width when
    /// <see cref="IsContained"/>, otherwise the window's.
    /// </summary>
    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(Breakpoint), 640);

    /// <summary>
    /// When true the levels and the scrim cover only this control (clipped to its corner radius) and the rest of the page
    /// stays usable; when false (default) they cover the whole window.
    /// </summary>
    public static readonly StyledProperty<bool> IsContainedProperty =
        AvaloniaProperty.Register<SheetStack, bool>(nameof(IsContained));

    /// <summary>The words on the close button and the back button's accessible name; English by default.</summary>
    public static readonly StyledProperty<SheetStackLabels> LabelsProperty =
        AvaloniaProperty.Register<SheetStack, SheetStackLabels>(nameof(Labels), new SheetStackLabels());

    /// <summary>The presentation in use: <see cref="SheetStackMode.Sheet"/> or <see cref="SheetStackMode.Dialog"/>.</summary>
    public static readonly DirectProperty<SheetStack, SheetStackMode> ActualModeProperty =
        AvaloniaProperty.RegisterDirect<SheetStack, SheetStackMode>(nameof(ActualMode), o => o.ActualMode);

    /// <summary>True while at least one level is open.</summary>
    public static readonly DirectProperty<SheetStack, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<SheetStack, bool>(nameof(IsOpen), o => o.IsOpen);

    /// <summary>
    /// Makes any button a trigger: a click pushes the sheet with this id onto the nearest <see cref="SheetStack"/> it sits
    /// in (page content or a sheet), after <see cref="Pushing"/>. A sheet that is already open is popped back to.
    /// </summary>
    public static readonly AttachedProperty<string?> OpenSheetProperty =
        AvaloniaProperty.RegisterAttached<SheetStack, AvaloniaButton, string?>("OpenSheet");

    /// <summary>
    /// Keeps a press on this element (and its descendants) from dragging the top sheet, for content with its own drag:
    /// sliders, maps, canvases. Text boxes are always excluded.
    /// </summary>
    public static readonly AttachedProperty<bool> IsDragExcludedProperty =
        AvaloniaProperty.RegisterAttached<SheetStack, Control, bool>("IsDragExcluded");

    /// <summary>The open levels changed (any cause; see <see cref="SheetStackChangedEventArgs.Reason"/>).</summary>
    public static readonly RoutedEvent<SheetStackChangedEventArgs> StackChangedEvent =
        RoutedEvent.Register<SheetStack, SheetStackChangedEventArgs>(nameof(StackChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<IBrush?> ScrimBrushProperty =
        AvaloniaProperty.Register<SheetStack, IBrush?>(nameof(ScrimBrush));

    internal static readonly StyledProperty<double> SheetMaxWidthProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(SheetMaxWidth), 560);

    internal static readonly StyledProperty<double> DialogWidthProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(DialogWidth), 440);

    internal static readonly StyledProperty<double> SheetTopInsetProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(SheetTopInset), 20);

    internal static readonly StyledProperty<double> DialogVerticalInsetProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(DialogVerticalInset), 40);

    internal static readonly StyledProperty<double> SheetPeekProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(SheetPeek), 10);

    internal static readonly StyledProperty<double> DialogPeekProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(DialogPeek), 14);

    internal static readonly StyledProperty<double> DepthScaleStepProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(DepthScaleStep), 0.05);

    internal static readonly StyledProperty<double> DepthDimStepProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(DepthDimStep), 0.5);

    internal static readonly StyledProperty<double> ExitOvershootProperty =
        AvaloniaProperty.Register<SheetStack, double>(nameof(ExitOvershoot), 24);

    private readonly SheetStackLayer _layer;
    private readonly List<SheetStackSheet> _live = new();
    private IReadOnlyList<string> _current = Array.Empty<string>();
    private SheetStackMode _actualMode = SheetStackMode.Sheet;
    private bool _isOpen;
    private bool _settingStack;
    private Border? _layerHost;
    private Panel? _overlay;
    private TopLevel? _topLevel;
    private IDisposable? _viewportWatch;
    private bool _keysHooked;

    static SheetStack()
    {
        OpenSheetProperty.Changed.AddClassHandler<AvaloniaButton>(OnOpenSheetChanged);
        AffectsMeasure<SheetStack>(IsContainedProperty);
    }

    /// <summary>Creates a stack.</summary>
    public SheetStack()
    {
        Sheets = new AvaloniaList<SheetStackSheet>();
        Sheets.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (SheetStackSheet sheet in e.OldItems)
                {
                    LogicalChildren.Remove(sheet);
                }
            }
            if (e.NewItems is not null)
            {
                foreach (SheetStackSheet sheet in e.NewItems)
                {
                    LogicalChildren.Add(sheet);
                }
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                LogicalChildren.RemoveAll(LogicalChildren.OfType<SheetStackSheet>().ToList());
                LogicalChildren.AddRange(Sheets);
            }
            // A removed sheet that was open leaves the stack.
            ApplyStack(_current, SheetStackChangeReason.Programmatic, null);
        };
        _layer = new SheetStackLayer(this) { IsVisible = false };
        UpdatePseudoClasses();
    }

    /// <summary>Every level the stack can open, bottom to top in no particular order; each needs a unique <see cref="SheetStackSheet.Id"/>.</summary>
    public AvaloniaList<SheetStackSheet> Sheets { get; }

    /// <summary>
    /// The ids of the open levels, bottom first (two-way). Setting it opens and closes levels to match: the levels past the
    /// common part leave and the new ones rise. Unknown and repeated ids are ignored.
    /// </summary>
    public IReadOnlyList<string> Stack { get => GetValue(StackProperty); set => SetValue(StackProperty, value); }

    /// <summary>Sheets, dialogs, or by width (<see cref="SheetStackMode.Auto"/>, the default).</summary>
    public SheetStackMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>
    /// The width from which <see cref="SheetStackMode.Auto"/> shows dialogs (default 640): the control's own width when
    /// <see cref="IsContained"/>, otherwise the window's.
    /// </summary>
    public double Breakpoint { get => GetValue(BreakpointProperty); set => SetValue(BreakpointProperty, value); }

    /// <summary>
    /// When true the levels and the scrim cover only this control (clipped to its corner radius) and the rest of the page
    /// stays usable; when false (default) they cover the whole window.
    /// </summary>
    public bool IsContained { get => GetValue(IsContainedProperty); set => SetValue(IsContainedProperty, value); }

    /// <summary>The words on the close button and the back button's accessible name; English by default.</summary>
    public SheetStackLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The presentation in use: <see cref="SheetStackMode.Sheet"/> or <see cref="SheetStackMode.Dialog"/>.</summary>
    public SheetStackMode ActualMode { get => _actualMode; private set => SetAndRaise(ActualModeProperty, ref _actualMode, value); }

    /// <summary>True while at least one level is open.</summary>
    public bool IsOpen { get => _isOpen; private set => SetAndRaise(IsOpenProperty, ref _isOpen, value); }

    /// <summary>The open levels changed (any cause; see <see cref="SheetStackChangedEventArgs.Reason"/>).</summary>
    public event EventHandler<SheetStackChangedEventArgs>? StackChanged
    {
        add => AddHandler(StackChangedEvent, value);
        remove => RemoveHandler(StackChangedEvent, value);
    }

    /// <summary>A level is about to open (a trigger or <see cref="Push"/>); set Cancel to keep it closed.</summary>
    public event EventHandler<SheetStackPushingEventArgs>? Pushing;

    /// <summary>Gets <see cref="OpenSheetProperty"/>.</summary>
    public static string? GetOpenSheet(AvaloniaButton button) => button.GetValue(OpenSheetProperty);

    /// <summary>Sets <see cref="OpenSheetProperty"/>.</summary>
    public static void SetOpenSheet(AvaloniaButton button, string? id) => button.SetValue(OpenSheetProperty, id);

    /// <summary>Gets <see cref="IsDragExcludedProperty"/>.</summary>
    public static bool GetIsDragExcluded(AvaloniaObject element) => element.GetValue(IsDragExcludedProperty);

    /// <summary>Sets <see cref="IsDragExcludedProperty"/>.</summary>
    public static void SetIsDragExcluded(AvaloniaObject element, bool value) => element.SetValue(IsDragExcludedProperty, value);

    internal IBrush? ScrimBrush { get => GetValue(ScrimBrushProperty); set => SetValue(ScrimBrushProperty, value); }

    internal double SheetMaxWidth { get => GetValue(SheetMaxWidthProperty); set => SetValue(SheetMaxWidthProperty, value); }

    internal double DialogWidth { get => GetValue(DialogWidthProperty); set => SetValue(DialogWidthProperty, value); }

    internal double SheetTopInset { get => GetValue(SheetTopInsetProperty); set => SetValue(SheetTopInsetProperty, value); }

    internal double DialogVerticalInset { get => GetValue(DialogVerticalInsetProperty); set => SetValue(DialogVerticalInsetProperty, value); }

    internal double SheetPeek { get => GetValue(SheetPeekProperty); set => SetValue(SheetPeekProperty, value); }

    internal double DialogPeek { get => GetValue(DialogPeekProperty); set => SetValue(DialogPeekProperty, value); }

    internal double DepthScaleStep { get => GetValue(DepthScaleStepProperty); set => SetValue(DepthScaleStepProperty, value); }

    internal double DepthDimStep { get => GetValue(DepthDimStepProperty); set => SetValue(DepthDimStepProperty, value); }

    internal double ExitOvershoot { get => GetValue(ExitOvershootProperty); set => SetValue(ExitOvershootProperty, value); }

    internal SheetStackLayer Layer => _layer;

    #region Public operations

    /// <summary>
    /// Opens the level <paramref name="id"/> on top (after <see cref="Pushing"/>). If it is already open the levels above
    /// it close instead. Returns false when no level has that id or the push was cancelled.
    /// </summary>
    public bool Push(string id) => RequestPush(id, null);

    /// <summary>Closes the top level. Returns false when nothing is open.</summary>
    public bool Pop() => PopTop(SheetStackChangeReason.Programmatic);

    /// <summary>Closes every level above <paramref name="id"/>. Returns false when it is not open.</summary>
    public bool PopTo(string id)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return false;
        }
        return ApplyStack(Take(index + 1), SheetStackChangeReason.Programmatic, null);
    }

    /// <summary>Closes every level at once (they leave together).</summary>
    public void Close() => CloseAll(SheetStackChangeReason.Programmatic);

    #endregion

    internal bool RequestPush(string id, object? trigger)
    {
        if (FindSheet(id) is null)
        {
            return false;
        }
        var args = new SheetStackPushingEventArgs(id, trigger);
        Pushing?.Invoke(this, args);
        if (args.Cancel)
        {
            return false;
        }
        var index = IndexOf(id);
        if (index >= 0)
        {
            return ApplyStack(Take(index + 1), SheetStackChangeReason.Push, null);
        }
        var next = new string[_current.Count + 1];
        for (var i = 0; i < _current.Count; i++)
        {
            next[i] = _current[i];
        }
        next[^1] = id;
        return ApplyStack(next, SheetStackChangeReason.Push, trigger);
    }

    internal bool PopTop(SheetStackChangeReason reason)
    {
        if (_current.Count == 0)
        {
            return false;
        }
        return ApplyStack(Take(_current.Count - 1), reason, null);
    }

    internal void CloseAll(SheetStackChangeReason reason)
    {
        if (_current.Count > 0)
        {
            ApplyStack(Array.Empty<string>(), reason, null);
        }
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < _current.Count; i++)
        {
            if (_current[i] == id)
            {
                return i;
            }
        }
        return -1;
    }

    private string[] Take(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = _current[i];
        }
        return result;
    }

    private SheetStackSheet? FindSheet(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        foreach (var sheet in Sheets)
        {
            if (sheet.Id == id)
            {
                return sheet;
            }
        }
        return null;
    }

    /// <summary>Makes <paramref name="requested"/> (filtered to known, unique ids) the open stack and animates the difference.</summary>
    private bool ApplyStack(IReadOnlyList<string> requested, SheetStackChangeReason reason, object? trigger)
    {
        var ids = new List<string>(requested.Count);
        var sheets = new List<SheetStackSheet>(requested.Count);
        foreach (var id in requested)
        {
            if (FindSheet(id) is { } sheet && !sheets.Contains(sheet))
            {
                ids.Add(id);
                sheets.Add(sheet);
            }
        }
        var same = ids.Count == _current.Count;
        for (var i = 0; same && i < ids.Count; i++)
        {
            same = ids[i] == _current[i] && ReferenceEquals(sheets[i], _live[i]);
        }
        if (same)
        {
            if (!ReferenceEquals(Stack, _current) && !SequenceEqual(Stack, _current))
            {
                SetStackValue(_current);
            }
            return false;
        }
        var old = _current;
        var oldLive = _live.ToArray();
        _current = ids.ToArray();
        _live.Clear();
        _live.AddRange(sheets);
        SetStackValue(_current);

        IInputElement? opener = null;
        var keyboard = false;
        if (reason == SheetStackChangeReason.Push || sheets.Count > oldLive.Length)
        {
            opener = trigger as IInputElement ?? TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            keyboard = opener is StyledElement styled && styled.Classes.Contains(":focus-visible");
        }
        if (_live.Count > 0)
        {
            MountLayer();
        }
        UpdateLayerGeometry();
        _layer.Sync(oldLive, _live, reason, opener, keyboard);
        IsOpen = _live.Count > 0;
        UpdatePseudoClasses();
        HookKeys(IsOpen);
        RaiseEvent(new SheetStackChangedEventArgs(StackChangedEvent, old, _current, reason));
        return true;
    }

    private static bool SequenceEqual(IReadOnlyList<string>? a, IReadOnlyList<string> b)
    {
        if (a is null || a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private void SetStackValue(IReadOnlyList<string> value)
    {
        _settingStack = true;
        try
        {
            SetCurrentValue(StackProperty, value);
        }
        finally
        {
            _settingStack = false;
        }
    }

    #region Layer hosting

    private void MountLayer()
    {
        _layer.IsVisible = true;
        if (IsContained)
        {
            UnmountFromOverlay();
            if (_layerHost is { } host && !ReferenceEquals(host.Child, _layer))
            {
                host.Child = _layer;
            }
            return;
        }
        if (_layerHost is { } contained && ReferenceEquals(contained.Child, _layer))
        {
            contained.Child = null;
        }
        if (_overlay is not null)
        {
            return;
        }
        if (ResolveOverlay() is not { } overlay)
        {
            return;
        }
        _overlay = overlay;
        _layer.SetValue(Motion.ArcMotion.LevelProperty, Motion.ArcMotion.GetLevel(this));
        overlay.Children.Add(_layer);
        overlay.SizeChanged += OnOverlaySizeChanged;
        SizeOverlayLayer();
    }

    /// <summary>The layer modals mount on: the nearest scoped overlay host first, otherwise the window's overlay layer.</summary>
    private Panel? ResolveOverlay() => Primitives.ArcOverlayLayer.Find(this);

    private void UnmountFromOverlay()
    {
        if (_overlay is not { } overlay)
        {
            return;
        }
        overlay.SizeChanged -= OnOverlaySizeChanged;
        overlay.Children.Remove(_layer);
        _overlay = null;
    }

    private void OnOverlaySizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SizeOverlayLayer();
        UpdateActualMode();
    }

    private void SizeOverlayLayer()
    {
        if (_overlay is not { } overlay)
        {
            return;
        }
        var size = overlay.Bounds.Size;
        if ((size.Width <= 0 || size.Height <= 0) && Primitives.ArcViewport.GetSize(this) is { } viewport)
        {
            size = viewport;
        }
        _layer.Width = size.Width;
        _layer.Height = size.Height;
        _layer.HostSize = size;
        Canvas.SetLeft(_layer, 0);
        Canvas.SetTop(_layer, 0);
    }

    /// <summary>Called by the layer once nothing is shown any more.</summary>
    internal void OnLayerIdle()
    {
        if (_live.Count > 0)
        {
            return;
        }
        _layer.IsVisible = false;
        UnmountFromOverlay();
    }

    private void UpdateLayerGeometry()
    {
        _layer.Metrics = new SheetStackMetrics(SheetMaxWidth, DialogWidth, SheetTopInset, DialogVerticalInset, SheetPeek, DialogPeek,
            DepthScaleStep, DepthDimStep, ExitOvershoot);
        _layer.ScrimBrush = ScrimBrush;
        if (IsContained)
        {
            _layer.HostSize = Bounds.Size;
        }
        else
        {
            SizeOverlayLayer();
        }
        UpdateActualMode();
    }

    private double ModeWidth()
    {
        if (IsContained)
        {
            return Bounds.Width;
        }
        if (_overlay is { Bounds.Width: > 0 } overlay)
        {
            return overlay.Bounds.Width;
        }
        return Primitives.ArcViewport.GetSize(this)?.Width ?? Bounds.Width;
    }

    private void UpdateActualMode()
    {
        var dialog = SheetStackLayout.IsDialog(Mode, ModeWidth(), Breakpoint);
        ActualMode = dialog ? SheetStackMode.Dialog : SheetStackMode.Sheet;
        _layer.SetDialog(dialog);
        UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(OpenPseudoClass, _live.Count > 0);
        PseudoClasses.Set(SheetModePseudoClass, _actualMode == SheetStackMode.Sheet);
        PseudoClasses.Set(DialogModePseudoClass, _actualMode == SheetStackMode.Dialog);
    }

    #endregion

    #region Escape

    private void HookKeys(bool open)
    {
        if (open == _keysHooked)
        {
            return;
        }
        if (open && _topLevel is { } topLevel)
        {
            topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
            _keysHooked = true;
        }
        else if (!open && _topLevel is { } hooked)
        {
            hooked.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _keysHooked = false;
        }
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || _live.Count == 0 || _layer.IsDragging)
        {
            return;
        }
        // A modal opened over the stack answers Escape first.
        if (_topLevel is { } topLevel)
        {
            foreach (var modal in ModalPresence.GetModals(topLevel))
            {
                if (modal.IsOpen)
                {
                    return;
                }
            }
        }
        e.Handled = true;
        PopTop(SheetStackChangeReason.Escape);
    }

    #endregion

    #region Overrides

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_layerHost is { } old && ReferenceEquals(old.Child, _layer))
        {
            old.Child = null;
        }
        base.OnApplyTemplate(e);
        _layerHost = e.NameScope.Find<Border>(LayerHostPart);
        if (_live.Count > 0 || !_layer.IsIdle)
        {
            MountLayer();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _viewportWatch = Primitives.ArcViewport.Observe(this, OnViewportChanged);
        HookKeys(_live.Count > 0);
        UpdateActualMode();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        HookKeys(false);
        _viewportWatch?.Dispose();
        _viewportWatch = null;
        UnmountFromOverlay();
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>The viewport (the nearest viewport host, otherwise the window) decides sheet or dialog when not contained.</summary>
    private void OnViewportChanged(Size viewport)
    {
        if (!IsContained)
        {
            UpdateActualMode();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (IsContained)
        {
            _layer.HostSize = e.NewSize;
        }
        UpdateActualMode();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StackProperty)
        {
            if (!_settingStack)
            {
                ApplyStack(change.GetNewValue<IReadOnlyList<string>>() ?? Array.Empty<string>(), SheetStackChangeReason.Programmatic, null);
            }
        }
        else if (change.Property == ModeProperty || change.Property == BreakpointProperty)
        {
            UpdateActualMode();
        }
        else if (change.Property == IsContainedProperty)
        {
            if (_live.Count > 0 || !_layer.IsIdle)
            {
                MountLayer();
            }
            UpdateLayerGeometry();
        }
        else if (change.Property == LabelsProperty)
        {
            _layer.Relevel();
        }
        else if (change.Property == ScrimBrushProperty)
        {
            _layer.ScrimBrush = ScrimBrush;
        }
        else if (change.Property == SheetMaxWidthProperty || change.Property == DialogWidthProperty ||
                 change.Property == SheetTopInsetProperty || change.Property == DialogVerticalInsetProperty ||
                 change.Property == SheetPeekProperty || change.Property == DialogPeekProperty ||
                 change.Property == DepthScaleStepProperty || change.Property == DepthDimStepProperty ||
                 change.Property == ExitOvershootProperty)
        {
            UpdateLayerGeometry();
        }
    }

    #endregion

    private static void OnOpenSheetChanged(AvaloniaButton button, AvaloniaPropertyChangedEventArgs e)
    {
        button.Click -= OnTriggerClick;
        if (e.NewValue is string { Length: > 0 })
        {
            button.Click += OnTriggerClick;
        }
    }

    private static void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not AvaloniaButton button || GetOpenSheet(button) is not { Length: > 0 } id)
        {
            return;
        }
        if (button.FindLogicalAncestorOfType<SheetStack>() is { } stack && stack.RequestPush(id, button))
        {
            e.Handled = true;
        }
    }
}
