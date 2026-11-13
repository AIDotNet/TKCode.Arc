using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A list row for choosing one value from a short list: the row shows its title and, at its end, the current value (or
/// <see cref="PlaceholderText"/>); a tap opens an <see cref="ActionSheet"/> of the choices with the current one checked,
/// and choosing a row selects it. The choices come from <see cref="ItemsSource"/> (or <see cref="Items"/>);
/// <see cref="SelectedItem"/> and <see cref="SelectedIndex"/> (both two-way) follow each other, and
/// <see cref="SelectionChanged"/> reports every change of the selection.
/// <para>
/// A picker is a <see cref="ListItem"/>: put it in a <see cref="ListGroup"/> and give it a title (the content), an
/// <c>Icon</c> or <c>Leading</c> content and a <c>Subtitle</c> as on any row; <c>Trailing</c> is not shown, because the value
/// is. It opens its sheet over the <see cref="Page"/> it is in, the way a sheet in <see cref="Page.Overlay"/> covers the
/// page, so nothing has to be added to the page. Enter and Space open it from the keyboard.
/// </para>
/// </summary>
[TemplatePart(ValuePart, typeof(ContentPresenter))]
[PseudoClasses(PlaceholderPseudoClass)]
public class Picker : ListItem
{
    /// <summary>The presenter of the value at the row's end, filled in code.</summary>
    public const string ValuePart = "PART_Value";

    /// <summary>Set while nothing is selected and the row shows <see cref="PlaceholderText"/>.</summary>
    public const string PlaceholderPseudoClass = ":placeholder";

    /// <summary>The choices. While it is null, <see cref="Items"/> are the choices.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Picker, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// The selected choice, or null. An item that is not one of the choices is still shown (its <see cref="SelectedIndex"/>
    /// is -1).
    /// </summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<Picker, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The index of the selected choice, or -1. An index set before the choices arrive waits for them.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Picker, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// A binding that reads the text of a choice (for the value at the row's end and the rows of the sheet), for example
    /// <c>{Binding Name}</c>. Without it, and without <see cref="ItemTemplate"/>, a choice shows its <c>ToString()</c>.
    /// </summary>
    public static readonly StyledProperty<BindingBase?> DisplayMemberBindingProperty =
        AvaloniaProperty.Register<Picker, BindingBase?>(nameof(DisplayMemberBinding));

    /// <summary>A template that builds the value at the row's end and each row of the sheet; it wins over <see cref="DisplayMemberBinding"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<Picker, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>The text at the row's end while nothing is selected: "Not set".</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<Picker, string?>(nameof(PlaceholderText), "Not set");

    /// <summary>The title of the sheet. Null uses the row's title when it is text.</summary>
    public static readonly StyledProperty<string?> SheetTitleProperty =
        AvaloniaProperty.Register<Picker, string?>(nameof(SheetTitle));

    /// <summary>A second, smaller line under the sheet's title.</summary>
    public static readonly StyledProperty<string?> SheetMessageProperty =
        AvaloniaProperty.Register<Picker, string?>(nameof(SheetMessage));

    /// <summary>The label of the sheet's Cancel row: "Cancel". Null or empty hides the row.</summary>
    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<Picker, string?>(nameof(CancelText), "Cancel");

    /// <summary>Whether the sheet of choices is open. A picker that is disabled or has no choices does not open.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Picker, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Raised whenever the selection changes: a row chosen in the sheet, <see cref="SelectedItem"/> or
    /// <see cref="SelectedIndex"/> set in code or through a binding, or the selection cleared. Choosing the row that is
    /// already selected raises <see cref="ItemInvoked"/> only.
    /// </summary>
    public static readonly RoutedEvent<PickerSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<Picker, PickerSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person chooses a row of the sheet, even the one that is already selected.</summary>
    public static readonly RoutedEvent<PickerItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<Picker, PickerItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    private ContentPresenter? _value;
    private TextBlock? _valueText;
    private ActionSheet? _sheet;
    private Page? _hostPage;
    private Panel? _hostLayer;
    private List<object?>? _choices;
    private INotifyCollectionChanged? _watched;
    private bool _attached;
    private bool _syncing;
    private object? _raisedItem;
    private int _raisedIndex = -1;
    private bool _keyboardOpen;

    static Picker()
    {
        ShowChevronProperty.OverrideDefaultValue<Picker>(true);
    }

    /// <summary>Creates a picker with no choices.</summary>
    public Picker()
    {
        Items.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                OnChoicesChanged();
            }
        };
    }

    /// <summary>The choices written in XAML (<c>&lt;arcm:Picker.Items&gt;</c>), used while <see cref="ItemsSource"/> is null.</summary>
    public AvaloniaList<object?> Items { get; } = [];

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="DisplayMemberBindingProperty"/>
    [AssignBinding]
    public BindingBase? DisplayMemberBinding
    {
        get => GetValue(DisplayMemberBindingProperty);
        set => SetValue(DisplayMemberBindingProperty, value);
    }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <inheritdoc cref="SheetTitleProperty"/>
    public string? SheetTitle
    {
        get => GetValue(SheetTitleProperty);
        set => SetValue(SheetTitleProperty, value);
    }

    /// <inheritdoc cref="SheetMessageProperty"/>
    public string? SheetMessage
    {
        get => GetValue(SheetMessageProperty);
        set => SetValue(SheetMessageProperty, value);
    }

    /// <inheritdoc cref="CancelTextProperty"/>
    public string? CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<PickerSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<PickerItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <summary>The sheet of choices once it was first opened, for tests.</summary>
    internal ActionSheet? ChoiceSheet => _sheet;

    protected override Type StyleKeyOverride => typeof(Picker);

    /// <summary>The choices as a list (a snapshot of <see cref="ItemsSource"/> or <see cref="Items"/>, rebuilt when they change).</summary>
    private List<object?> Choices
    {
        get
        {
            if (_choices is null)
            {
                _choices = [];
                foreach (var item in ItemsSource ?? Items)
                {
                    _choices.Add(item);
                }
            }
            return _choices;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_value is not null)
        {
            _value.ContentTemplate = null;
            _value.Content = null;
        }
        _value = e.NameScope.Find<ContentPresenter>(ValuePart);
        UpdateValue();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Watch();
        OnChoicesChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Watch();
        if (_sheet is not null)
        {
            _sheet.IsOpen = false;
            // Taken off a page that stays: the sheet goes too. When the whole page leaves (a navigation stack covering it),
            // the sheet stays hosted there, out of the tree with it, and comes back with it; the tree is not changed while
            // it is being detached.
            if (_hostPage?.IsAttachedToVisualTree() == true || _hostLayer?.IsAttachedToVisualTree() == true)
            {
                Unmount();
            }
        }
        SetCurrentValue(IsOpenProperty, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedItemProperty)
        {
            Sync(fromIndex: false);
        }
        else if (change.Property == SelectedIndexProperty)
        {
            Sync(fromIndex: true);
        }
        else if (change.Property == ItemsSourceProperty)
        {
            Watch();
            OnChoicesChanged();
        }
        else if (change.Property == DisplayMemberBindingProperty || change.Property == ItemTemplateProperty)
        {
            UpdateValue();
            if (IsOpen)
            {
                BuildRows();
            }
        }
        else if (change.Property == PlaceholderTextProperty)
        {
            UpdateValue();
        }
        else if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                if (_sheet is not null)
                {
                    _sheet.IsOpen = false;
                }
                _keyboardOpen = false;
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled && IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        else if (change.Property == SheetTitleProperty || change.Property == SheetMessageProperty
                 || change.Property == CancelTextProperty || change.Property == ContentProperty)
        {
            ApplySheetText();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // The button opens on Enter (here) and Space (on release); remember that the keyboard did it.
        if (e.Key is Key.Enter or Key.Space)
        {
            _keyboardOpen = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _keyboardOpen = false;
        base.OnPointerPressed(e);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (!IsOpen)
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    /// <summary>Listens to the collection of choices while the picker is in a visual tree (a view model's list may outlive it).</summary>
    private void Watch()
    {
        if (_watched is not null)
        {
            _watched.CollectionChanged -= OnSourceChanged;
        }
        _watched = _attached ? ItemsSource as INotifyCollectionChanged : null;
        if (_watched is not null)
        {
            _watched.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnChoicesChanged();

    /// <summary>The choices changed: the selected item keeps its place (its index follows), or a waiting index takes its item.</summary>
    private void OnChoicesChanged()
    {
        _choices = null;
        Sync(fromIndex: SelectedItem is null);
        if (IsOpen)
        {
            BuildRows();
        }
    }

    private int IndexOf(object? item)
    {
        if (item is null)
        {
            return -1;
        }
        var choices = Choices;
        for (var i = 0; i < choices.Count; i++)
        {
            if (Equals(choices[i], item))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Brings <see cref="SelectedItem"/> and <see cref="SelectedIndex"/> in line, from one or the other.</summary>
    private void Sync(bool fromIndex)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        try
        {
            var choices = Choices;
            if (fromIndex)
            {
                var index = SelectedIndex;
                if (index >= 0 && index < choices.Count)
                {
                    SetCurrentValue(SelectedItemProperty, choices[index]);
                }
                else if (index < 0 || choices.Count > 0)
                {
                    // Out of range among the choices there are clears the selection; with no choices yet the index waits.
                    if (index != -1)
                    {
                        SetCurrentValue(SelectedIndexProperty, -1);
                    }
                    SetCurrentValue(SelectedItemProperty, null);
                }
            }
            else
            {
                SetCurrentValue(SelectedIndexProperty, IndexOf(SelectedItem));
            }
        }
        finally
        {
            _syncing = false;
        }
        OnSelectionSettled();
    }

    private void OnSelectionSettled()
    {
        UpdateValue();
        UpdateChecks();
        var item = SelectedItem;
        var index = SelectedIndex;
        if (item is null && index >= 0)
        {
            // An index waiting for its choices is not a selection yet.
            return;
        }
        if (Equals(item, _raisedItem))
        {
            // The same item at another place (a choice inserted before it) is the same selection.
            _raisedIndex = index;
            return;
        }
        var args = new PickerSelectionChangedEventArgs(SelectionChangedEvent, _raisedItem, item, _raisedIndex, index);
        _raisedItem = item;
        _raisedIndex = index;
        RaiseEvent(args);
    }

    /// <summary>The text of a choice: <see cref="DisplayMemberBinding"/> read on a private element, else <c>ToString()</c>.</summary>
    private string? TextOf(object? item)
    {
        if (item is null)
        {
            return null;
        }
        if (DisplayMemberBinding is not { } binding)
        {
            return item.ToString();
        }
        var reader = new DisplayReader { DataContext = item };
        using (reader.Bind(DisplayReader.TextProperty, binding))
        {
            return reader.Text;
        }
    }

    /// <summary>Shows the value (or the placeholder) at the row's end and names it for assistive technology.</summary>
    private void UpdateValue()
    {
        var item = SelectedItem;
        var placeholder = item is null;
        PseudoClasses.Set(PlaceholderPseudoClass, placeholder);
        var text = placeholder ? PlaceholderText : TextOf(item);
        AutomationProperties.SetHelpText(this, text);
        if (_value is null)
        {
            return;
        }
        // A template is attached after its content and detached before it is cleared, so it never builds for null.
        _value.ContentTemplate = null;
        if (!placeholder && ItemTemplate is { } template)
        {
            _value.Content = item;
            _value.ContentTemplate = template;
            return;
        }
        _valueText ??= new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
        _valueText.Text = text;
        _value.Content = _valueText;
    }

    private void UpdateChecks()
    {
        if (_sheet is null)
        {
            return;
        }
        var index = SelectedIndex;
        for (var i = 0; i < _sheet.Actions.Count; i++)
        {
            _sheet.Actions[i].IsChecked = i == index;
        }
    }

    private ActionSheet EnsureSheet()
    {
        if (_sheet is null)
        {
            _sheet = new ActionSheet();
            _sheet.ActionInvoked += OnActionInvoked;
            _sheet.PropertyChanged += OnSheetPropertyChanged;
            ApplySheetText();
        }
        return _sheet;
    }

    private void ApplySheetText()
    {
        if (_sheet is null)
        {
            return;
        }
        _sheet.Title = SheetTitle ?? Content as string;
        _sheet.Message = SheetMessage;
        _sheet.CancelText = CancelText;
    }

    /// <summary>One sheet row per choice, the selected one checked.</summary>
    private void BuildRows()
    {
        if (_sheet is not { } sheet)
        {
            return;
        }
        sheet.Actions.Clear();
        var template = ItemTemplate;
        var choices = Choices;
        var selected = SelectedIndex;
        for (var i = 0; i < choices.Count; i++)
        {
            var row = new ActionSheetItem { IsChecked = i == selected };
            if (template is not null)
            {
                row.Content = choices[i];
                row.ContentTemplate = template;
            }
            else
            {
                row.Content = TextOf(choices[i]);
            }
            sheet.Actions.Add(row);
        }
    }

    private void Open()
    {
        if (!IsEffectivelyEnabled || Choices.Count == 0 || !this.IsAttachedToVisualTree())
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
        ApplySheetText();
        BuildRows();
        // A sheet whose template exists when it opens rises; one that only gets it later would appear in place.
        sheet.ApplyTemplate();
        sheet.IsOpen = true;
        if (_keyboardOpen)
        {
            Dispatcher.UIThread.Post(FocusCheckedRow, DispatcherPriority.Loaded);
        }
    }

    /// <summary>Opened from the keyboard: the current choice (or the first) takes the keyboard focus.</summary>
    private void FocusCheckedRow()
    {
        if (_sheet is not { IsOpen: true } sheet || sheet.Actions.Count == 0)
        {
            return;
        }
        var row = sheet.Actions.FirstOrDefault(a => a.IsChecked) ?? sheet.Actions[0];
        row.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// Puts the sheet over the page the picker is in (above the page's overlay slot, with the page's safe area), or else on
    /// the window's overlay layer, sized to it.
    /// </summary>
    private bool Mount(ActionSheet sheet)
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
        if (e.Property != ActionSheet.IsOpenProperty || _sheet is not { IsOpen: false } sheet)
        {
            return;
        }
        if (IsOpen)
        {
            // Closed by a choice, Cancel, the scrim, a pull or Escape: the focus comes back to the row, with a ring only
            // when the keyboard opened the sheet.
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
            if (focused is null || sheet.IsVisualAncestorOf(focused))
            {
                Focus(_keyboardOpen ? NavigationMethod.Tab : NavigationMethod.Pointer);
            }
        }
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnActionInvoked(object? sender, ActionSheetActionEventArgs e)
    {
        if (_sheet is null)
        {
            return;
        }
        var index = _sheet.Actions.IndexOf(e.Action);
        var choices = Choices;
        if (index < 0 || index >= choices.Count)
        {
            return;
        }
        var item = choices[index];
        SetCurrentValue(SelectedIndexProperty, index);
        RaiseEvent(new PickerItemInvokedEventArgs(ItemInvokedEvent, item, index));
    }

    /// <summary>Reads <see cref="DisplayMemberBinding"/> for one choice (the choice is its data context).</summary>
    private sealed class DisplayReader : StyledElement
    {
        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<DisplayReader, string?>(nameof(Text));

        public string? Text => GetValue(TextProperty);
    }
}
