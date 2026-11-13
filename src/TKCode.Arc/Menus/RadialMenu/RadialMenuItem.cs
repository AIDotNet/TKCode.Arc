using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One command of a <see cref="RadialMenu"/>: a sector of the ring with an icon and a label. Declare items inline
/// (<c>&lt;arc:RadialMenuItem Header="Copy" Icon="{x:Static arc:ArcIcons.Copy}" Command="{Binding CopyCommand}" /&gt;</c>), or
/// let the menu generate one per <see cref="ItemsControl.ItemsSource"/> item (its <c>DataContext</c> is the item, so
/// styles can bind <see cref="Header"/>, <see cref="Icon"/> and <see cref="Command"/>). The sector draws itself and the
/// menu does all hit-testing by angle and radius, so the item does not take pointer input of its own.
/// </summary>
[TemplatePart(ContentPart, typeof(Control))]
[PseudoClasses(HighlightedPseudoClass, PressedPseudoClass)]
public class RadialMenuItem : TemplatedControl
{
    public const string ContentPart = "PART_Content";
    public const string HighlightedPseudoClass = ":highlighted";
    public const string PressedPseudoClass = ":pressed";

    #region 公共属性定义

    /// <summary>The label: text, or any content (with <see cref="HeaderTemplate"/>). Text is also the centre label and the name read by screen readers.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<RadialMenuItem, object?>(nameof(Header));

    /// <summary>Template that builds the sector's content from <see cref="Header"/> (the menu's <c>ItemTemplate</c> for generated items).</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<RadialMenuItem, IDataTemplate?>(nameof(HeaderTemplate));

    /// <summary>The icon above the label: a 24×24 Lucide stroke <see cref="Geometry"/> (drawn at 20px), or any control.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RadialMenuItem, object?>(nameof(Icon));

    /// <summary>Command run when the item is chosen; the item is disabled while the command cannot execute.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<RadialMenuItem, ICommand?>(nameof(Command));

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<RadialMenuItem, object?>(nameof(CommandParameter));

    /// <summary>Whether this is the menu's highlighted item (under the pointer or at the keyboard position).</summary>
    public static readonly DirectProperty<RadialMenuItem, bool> IsHighlightedProperty =
        AvaloniaProperty.RegisterDirect<RadialMenuItem, bool>(nameof(IsHighlighted), o => o.IsHighlighted);

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="HeaderTemplateProperty"/>
    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <inheritdoc cref="IsHighlightedProperty"/>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        private set => SetAndRaise(IsHighlightedProperty, ref _isHighlighted, value);
    }

    #endregion

    internal static readonly DirectProperty<RadialMenuItem, Geometry?> IconGeometryProperty =
        AvaloniaProperty.RegisterDirect<RadialMenuItem, Geometry?>(nameof(IconGeometry), o => o.IconGeometry);

    internal static readonly DirectProperty<RadialMenuItem, object?> IconContentProperty =
        AvaloniaProperty.RegisterDirect<RadialMenuItem, object?>(nameof(IconContent), o => o.IconContent);

    internal static readonly DirectProperty<RadialMenuItem, bool> HasIconProperty =
        AvaloniaProperty.RegisterDirect<RadialMenuItem, bool>(nameof(HasIcon), o => o.HasIcon);

    internal static readonly DirectProperty<RadialMenuItem, bool> ShowsHeaderProperty =
        AvaloniaProperty.RegisterDirect<RadialMenuItem, bool>(nameof(ShowsHeader), o => o.ShowsHeader);

    internal static readonly DirectProperty<RadialMenuItem, IDataTemplate?> EffectiveHeaderTemplateProperty =
        AvaloniaProperty.RegisterDirect<RadialMenuItem, IDataTemplate?>(nameof(EffectiveHeaderTemplate), o => o.EffectiveHeaderTemplate);

    private static readonly RadialMenuItemTextTemplate s_textTemplate = new();

    private bool _isHighlighted;
    private Geometry? _iconGeometry;
    private object? _iconContent;
    private bool _hasIcon;
    private bool _showsHeader;
    private bool _labelAllowed = true;
    private IDataTemplate? _effectiveHeaderTemplate;
    private bool _commandCanExecute = true;
    private RadialMenuSlot _slot = new(0, 360);
    private double _inner = 44;
    private double _outer = 124;
    private double _gap = 4;
    private bool _wholeRing;
    private StreamGeometry? _geometry;
    private Size _geometrySize;
    private IPen? _pen;

    static RadialMenuItem()
    {
        AffectsRender<RadialMenuItem>(BackgroundProperty, BorderBrushProperty, BorderThicknessProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<RadialMenuItem>(false);
        FocusableProperty.OverrideDefaultValue<RadialMenuItem>(true);
    }

    internal Geometry? IconGeometry
    {
        get => _iconGeometry;
        private set => SetAndRaise(IconGeometryProperty, ref _iconGeometry, value);
    }

    internal object? IconContent
    {
        get => _iconContent;
        private set => SetAndRaise(IconContentProperty, ref _iconContent, value);
    }

    internal bool HasIcon
    {
        get => _hasIcon;
        private set => SetAndRaise(HasIconProperty, ref _hasIcon, value);
    }

    /// <summary>Whether the sector shows its header (it has one, and the menu shows item labels or the header is templated).</summary>
    internal bool ShowsHeader
    {
        get => _showsHeader;
        private set => SetAndRaise(ShowsHeaderProperty, ref _showsHeader, value);
    }

    internal IDataTemplate? EffectiveHeaderTemplate
    {
        get => _effectiveHeaderTemplate;
        private set => SetAndRaise(EffectiveHeaderTemplateProperty, ref _effectiveHeaderTemplate, value);
    }

    /// <summary>The data item this container shows (itself for a declared item).</summary>
    internal object? Item { get; set; }

    /// <summary>The sector this item occupies (set by the panel).</summary>
    internal RadialMenuSlot Slot => _slot;

    /// <summary>The cached sector shape (tests).</summary>
    internal StreamGeometry? SectorGeometry => _geometry;

    /// <summary>The menu this item belongs to.</summary>
    internal RadialMenu? Menu => ItemsControl.ItemsControlFromItemContainer(this) as RadialMenu;

    protected override bool IsEnabledCore => base.IsEnabledCore && _commandCanExecute;

    /// <summary>The item's text: its accessible name, the menu's <c>TextSelector</c> for data, or a text header.</summary>
    internal string? GetText(Func<object?, string?>? selector)
    {
        if (AutomationProperties.GetName(this) is { Length: > 0 } name)
        {
            return name;
        }
        if (selector is not null && Item is { } item && !ReferenceEquals(item, this) && selector(item) is { } selected)
        {
            return selected;
        }
        return Header switch
        {
            string text => text,
            null or Control => null,
            { } other => other.ToString()
        };
    }

    /// <summary>Places the item in its sector (called by the panel during measure).</summary>
    internal void SetLayout(RadialMenuSlot slot, double inner, double outer, double gap, bool wholeRing, bool labelAllowed)
    {
        var changed = slot != _slot || inner != _inner || outer != _outer || gap != _gap || wholeRing != _wholeRing;
        _slot = slot;
        _inner = inner;
        _outer = outer;
        _gap = gap;
        _wholeRing = wholeRing;
        if (_labelAllowed != labelAllowed)
        {
            _labelAllowed = labelAllowed;
            UpdateHeader();
        }
        if (changed)
        {
            _geometry = null;
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    internal void SetHighlighted(bool value)
    {
        IsHighlighted = value;
        PseudoClasses.Set(HighlightedPseudoClass, value);
    }

    internal void SetPressed(bool value) => PseudoClasses.Set(PressedPseudoClass, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (VisualChildren.Count > 0 && VisualChildren[0] is Layoutable content)
        {
            content.Measure(RadialMenuLayout.ContentBox(_slot, _inner, _outer, _gap, Padding.Left, _wholeRing));
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 2 * _outer;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 2 * _outer;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var center = new Point(finalSize.Width / 2, finalSize.Height / 2);
        if (VisualChildren.Count > 0 && VisualChildren[0] is Layoutable content)
        {
            var label = RadialMenuLayout.LabelPoint(center, _slot, _inner, _outer);
            var size = content.DesiredSize;
            content.Arrange(new Rect(label.X - size.Width / 2, label.Y - size.Height / 2, size.Width, size.Height));
        }
        if (_geometry is null || _geometrySize != finalSize)
        {
            _geometrySize = finalSize;
            _geometry = RadialMenuLayout.Sector(center, _inner, _outer, _slot, _gap / 2, CornerRadius.TopLeft, _wholeRing);
            InvalidateVisual();
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_geometry is null)
        {
            return;
        }
        var thickness = BorderThickness.Left;
        IPen? pen = null;
        if (BorderBrush is { } stroke && thickness > 0)
        {
            if (_pen is not Pen cached || !ReferenceEquals(cached.Brush, stroke) || cached.Thickness != thickness)
            {
                _pen = new Pen(stroke, thickness);
            }
            pen = _pen;
        }
        context.DrawGeometry(Background, pen, _geometry);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            UpdateIcon();
        }
        else if (change.Property == HeaderProperty || change.Property == HeaderTemplateProperty)
        {
            UpdateHeader();
            Menu?.OnItemTextChanged(this);
        }
        else if (change.Property == CornerRadiusProperty)
        {
            _geometry = null;
            InvalidateArrange();
        }
        else if (change.Property == IsEffectivelyEnabledProperty || change.Property == IsVisibleProperty)
        {
            Menu?.OnItemAvailabilityChanged(this);
        }
        else if (change.Property == CommandProperty)
        {
            if (((ILogical)this).IsAttachedToLogicalTree)
            {
                if (change.OldValue is ICommand oldCommand)
                {
                    oldCommand.CanExecuteChanged -= OnCanExecuteChanged;
                }
                if (change.NewValue is ICommand newCommand)
                {
                    newCommand.CanExecuteChanged += OnCanExecuteChanged;
                }
            }
            UpdateCanExecute();
        }
        else if (change.Property == CommandParameterProperty)
        {
            UpdateCanExecute();
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (Command is { } command)
        {
            command.CanExecuteChanged += OnCanExecuteChanged;
        }
        UpdateCanExecute();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        if (Command is { } command)
        {
            command.CanExecuteChanged -= OnCanExecuteChanged;
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e) => UpdateCanExecute();

    private void UpdateCanExecute()
    {
        var canExecute = Command is not { } command || command.CanExecute(CommandParameter);
        if (canExecute != _commandCanExecute)
        {
            _commandCanExecute = canExecute;
            UpdateIsEffectivelyEnabled();
        }
    }

    private void UpdateIcon()
    {
        var icon = Icon;
        IconGeometry = icon as Geometry;
        IconContent = icon is Geometry ? null : icon;
        HasIcon = icon is not null;
    }

    private void UpdateHeader()
    {
        var header = Header;
        EffectiveHeaderTemplate = HeaderTemplate ?? (header is string ? s_textTemplate : null);
        // A templated header is the sector's content (a swatch, a badge): it shows even when the menu hides text labels.
        ShowsHeader = header is not null && (_labelAllowed || HeaderTemplate is not null);
    }

    /// <summary>Chooses this item as if it were clicked (the menu raises <see cref="RadialMenu.ItemInvoked"/>, runs the commands and closes).</summary>
    public void PerformClick() => Menu?.Invoke(this);

    protected override AutomationPeer OnCreateAutomationPeer() => new RadialMenuItemAutomationPeer(this);

    private sealed class RadialMenuItemAutomationPeer : ControlAutomationPeer, IInvokeProvider
    {
        public RadialMenuItemAutomationPeer(RadialMenuItem owner)
            : base(owner)
        {
        }

        private RadialMenuItem Item => (RadialMenuItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override string? GetNameCore() => Item.GetText(Item.Menu?.TextSelector) ?? base.GetNameCore();

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => Item.PerformClick();
    }
}

/// <summary>The default sector label: one line of text, centred, cut with an ellipsis.</summary>
internal sealed class RadialMenuItemTextTemplate : IDataTemplate
{
    public Control? Build(object? param) => new TextBlock
    {
        Text = param as string,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    public bool Match(object? data) => data is string;
}
