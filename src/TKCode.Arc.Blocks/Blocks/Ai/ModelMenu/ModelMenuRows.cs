using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>What a row of the model menu does when it is chosen.</summary>
internal enum ModelMenuRowKind
{
    /// <summary>Picks a model (featured or in "More models").</summary>
    Model,

    /// <summary>Picks an effort level.</summary>
    Level,

    /// <summary>Opens the effort levels (submenu or page).</summary>
    Effort,

    /// <summary>Opens the other models (submenu or page).</summary>
    More,

    /// <summary>Goes back from a page to the featured models.</summary>
    Back,

    /// <summary>The inline layout's segmented row of effort levels (one keyboard stop; ← → change the level).</summary>
    EffortStrip
}

/// <summary>
/// A row of the model menu: focusable (the keyboard position is the focused row), chosen by a click, Enter or Space.
/// Rows draw no hover or focus state of their own: the panel's single highlight shows where the pointer or the
/// keyboard is.
/// </summary>
internal abstract class ModelMenuItem : TemplatedControl
{
    /// <summary>True on the row of the selected model or effort level: it shows a check.</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ModelMenuItem, bool>(nameof(IsChecked));

    private bool _pressed;

    static ModelMenuItem()
    {
        FocusableProperty.OverrideDefaultValue<ModelMenuItem>(true);
        IsTabStopProperty.OverrideDefaultValue<ModelMenuItem>(false);
    }

    /// <inheritdoc cref="IsCheckedProperty"/>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    public abstract ModelMenuRowKind Kind { get; }

    /// <summary>The name typeahead matches (the visible name).</summary>
    public abstract string? TypeaheadText { get; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            OnClicked(e);
        }
    }

    /// <summary>A click (press and release on the row) chooses it; the segmented row chooses the level under the pointer.</summary>
    protected virtual void OnClicked(PointerReleasedEventArgs e) =>
        this.FindAncestorOfType<ModelMenuPanel>()?.OnRowInvoked(this, NavigationMethod.Pointer);

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    /// <summary>Chooses the row (automation Invoke).</summary>
    internal void Invoke() => this.FindAncestorOfType<ModelMenuPanel>()?.OnRowInvoked(this, NavigationMethod.Unspecified);

    protected override AutomationPeer OnCreateAutomationPeer() => new ModelMenuItemAutomationPeer(this);
}

/// <summary>
/// MenuItem role: model and level rows are radio items (toggle state = checked); Effort / More models report whether
/// their submenu is open (not in a narrow window, where they open a page); every row can be invoked.
/// </summary>
internal sealed class ModelMenuItemAutomationPeer : ControlAutomationPeer, IInvokeProvider, IToggleProvider, IExpandCollapseProvider
{
    public ModelMenuItemAutomationPeer(ModelMenuItem owner)
        : base(owner)
    {
    }

    private ModelMenuItem Item => (ModelMenuItem)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Item.TypeaheadText;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public void Invoke() => Item.Invoke();

    public ToggleState ToggleState => Item.IsChecked ? ToggleState.On : ToggleState.Off;

    public void Toggle() => Item.Invoke();

    public ExpandCollapseState ExpandCollapseState =>
        Item is ModelMenuActionRow { IsExpanded: true } ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => true;

    public void Expand()
    {
        if (Item is ModelMenuActionRow { IsExpanded: false })
        {
            Item.Invoke();
        }
    }

    public void Collapse()
    {
        if (Item is ModelMenuActionRow { IsExpanded: true } row)
        {
            row.FindAncestorOfType<ModelMenuPanel>()?.Owner?.CloseSubmenuFromAutomation();
        }
    }

    protected override object? GetProviderCore(Type providerType)
    {
        if (providerType == typeof(IToggleProvider) && Item.Kind is not (ModelMenuRowKind.Model or ModelMenuRowKind.Level))
        {
            return null;
        }
        if (providerType == typeof(IExpandCollapseProvider) && Item is not ModelMenuActionRow { HasSubmenu: true })
        {
            return null;
        }
        return base.GetProviderCore(providerType);
    }

    /// <summary>The segmented row exposes its levels (radio buttons); other rows have no children of their own.</summary>
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
    {
        if (Item is not ModelMenuEffortStrip strip)
        {
            return base.GetChildrenCore();
        }
        var children = new List<AutomationPeer>(strip.Cells.Count);
        foreach (var cell in strip.Cells)
        {
            children.Add(CreatePeerForElement(cell));
        }
        return children;
    }
}

/// <summary>
/// A model: two lines with an 18px mark when featured, one line with a 16px mark and its tag in "More models"; in the
/// inline (composer) layout 48px with a 16px mark, a regular name, a muted line and a check in the text color.
/// </summary>
internal sealed class ModelMenuModelRow : ModelMenuItem
{
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, bool>(nameof(IsCompact));

    public static readonly StyledProperty<bool> IsInlineProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, bool>(nameof(IsInline));

    /// <summary>The simple layout's row (the chat): 36px, mark, name, the muted note at the end, the check.</summary>
    public static readonly StyledProperty<bool> IsSimpleProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, bool>(nameof(IsSimple));

    public static readonly StyledProperty<string?> NoteTextProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, string?>(nameof(NoteText));

    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, string?>(nameof(ModelName));

    public static readonly StyledProperty<string?> BlurbProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, string?>(nameof(Blurb));

    public static readonly StyledProperty<string?> TagTextProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, string?>(nameof(TagText));

    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, object?>(nameof(Mark));

    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<ModelMenuModelRow, IBrush?>(nameof(MarkColor));

    public ModelMenuModelRow(AiModel model)
    {
        Model = model;
    }

    public AiModel Model { get; }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public bool IsInline { get => GetValue(IsInlineProperty); set => SetValue(IsInlineProperty, value); }
    public bool IsSimple { get => GetValue(IsSimpleProperty); set => SetValue(IsSimpleProperty, value); }
    public string? NoteText { get => GetValue(NoteTextProperty); set => SetValue(NoteTextProperty, value); }
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }
    public string? Blurb { get => GetValue(BlurbProperty); set => SetValue(BlurbProperty, value); }
    public string? TagText { get => GetValue(TagTextProperty); set => SetValue(TagTextProperty, value); }
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }
    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }

    public override ModelMenuRowKind Kind => ModelMenuRowKind.Model;

    public override string? TypeaheadText => ModelName;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelNameProperty)
        {
            AutomationProperties.SetName(this, ModelName);
        }
    }
}

/// <summary>An effort level: its name (with "Default" after the model's default level) and a one-line description.</summary>
internal sealed class ModelMenuEffortRow : ModelMenuItem
{
    public static readonly StyledProperty<string?> LevelNameProperty =
        AvaloniaProperty.Register<ModelMenuEffortRow, string?>(nameof(LevelName));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ModelMenuEffortRow, string?>(nameof(Description));

    public static readonly StyledProperty<string?> DefaultTextProperty =
        AvaloniaProperty.Register<ModelMenuEffortRow, string?>(nameof(DefaultText));

    public static readonly StyledProperty<bool> IsDefaultProperty =
        AvaloniaProperty.Register<ModelMenuEffortRow, bool>(nameof(IsDefault));

    public ModelMenuEffortRow(ModelEffort level)
    {
        Level = level;
    }

    public ModelEffort Level { get; }

    public string? LevelName { get => GetValue(LevelNameProperty); set => SetValue(LevelNameProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? DefaultText { get => GetValue(DefaultTextProperty); set => SetValue(DefaultTextProperty, value); }
    public bool IsDefault { get => GetValue(IsDefaultProperty); set => SetValue(IsDefaultProperty, value); }

    public override ModelMenuRowKind Kind => ModelMenuRowKind.Level;

    public override string? TypeaheadText => LevelName;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LevelNameProperty || change.Property == IsDefaultProperty || change.Property == DefaultTextProperty)
        {
            AutomationProperties.SetName(this, IsDefault && !string.IsNullOrEmpty(DefaultText) ? $"{LevelName}, {DefaultText}" : LevelName);
        }
    }
}

/// <summary>
/// The Effort and More models rows (icon, label, current value, chevron) and the back row of a page (chevron left and
/// the page title).
/// </summary>
internal sealed class ModelMenuActionRow : ModelMenuItem
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ModelMenuActionRow, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ModelMenuActionRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ModelMenuActionRow, string?>(nameof(Value));

    /// <summary>True while the submenu this row opened is shown.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ModelMenuActionRow, bool>(nameof(IsExpanded));

    /// <summary>False in a narrow window, where the row opens a page instead of a submenu.</summary>
    public static readonly StyledProperty<bool> HasSubmenuProperty =
        AvaloniaProperty.Register<ModelMenuActionRow, bool>(nameof(HasSubmenu), true);

    public ModelMenuActionRow(ModelMenuRowKind kind)
    {
        Kind = kind;
        IsBack = kind == ModelMenuRowKind.Back;
        Glyph = kind switch
        {
            ModelMenuRowKind.Effort => ArcIcons.Gauge,
            ModelMenuRowKind.More => ArcIcons.Layers3,
            _ => ArcIcons.ChevronLeft
        };
        if (IsBack)
        {
            HasSubmenu = false;
            Classes.Add("back");
        }
    }

    public override ModelMenuRowKind Kind { get; }

    public bool IsBack { get; }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool HasSubmenu { get => GetValue(HasSubmenuProperty); set => SetValue(HasSubmenuProperty, value); }

    public override string? TypeaheadText => Label;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!IsBack && (change.Property == LabelProperty || change.Property == ValueProperty))
        {
            AutomationProperties.SetName(this, string.IsNullOrEmpty(Value) ? Label : $"{Label}, {Value}");
        }
    }
}

/// <summary>
/// The line between the featured models and the Effort / More models rows (and under a page's back row); in the inline
/// layout it reaches the panel's edges (margin 5 −5).
/// </summary>
internal sealed class ModelMenuSeparator : TemplatedControl
{
    public static readonly StyledProperty<bool> IsInlineProperty =
        AvaloniaProperty.Register<ModelMenuSeparator, bool>(nameof(IsInline));

    static ModelMenuSeparator()
    {
        FocusableProperty.OverrideDefaultValue<ModelMenuSeparator>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<ModelMenuSeparator>(false);
    }

    public bool IsInline { get => GetValue(IsInlineProperty); set => SetValue(IsInlineProperty, value); }

    protected override AutomationPeer OnCreateAutomationPeer() => new SeparatorPeer(this);

    private sealed class SeparatorPeer(ModelMenuSeparator owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>A provider heading in "More models", or the note of a menu without models (class <c>empty</c>).</summary>
internal sealed class ModelMenuGroupHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ModelMenuGroupHeader, string?>(nameof(Text));

    /// <summary>The first heading has 4px less space above it.</summary>
    public static readonly StyledProperty<bool> IsFirstProperty =
        AvaloniaProperty.Register<ModelMenuGroupHeader, bool>(nameof(IsFirst));

    static ModelMenuGroupHeader()
    {
        FocusableProperty.OverrideDefaultValue<ModelMenuGroupHeader>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsFirst { get => GetValue(IsFirstProperty); set => SetValue(IsFirstProperty, value); }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);

    private sealed class HeaderPeer(ModelMenuGroupHeader owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((ModelMenuGroupHeader)Owner).Text;
    }
}

/// <summary>A named group of rows: the featured models, the levels, one provider's models in "More models".</summary>
internal sealed class ModelMenuGroup : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer(ModelMenuGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}
