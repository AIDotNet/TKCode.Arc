using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One level of a <see cref="SheetStack"/>: a modal panel with a header (back button, title, close button), a scrolling
/// body (<see cref="ContentControl.Content"/>) and an optional footer pinned under it. Declare every level in
/// <see cref="SheetStack.Sheets"/>; a level is shown only while its <see cref="Id"/> is in <see cref="SheetStack.Stack"/>.
/// </summary>
[TemplatePart(BackButtonPart, typeof(AvaloniaButton))]
[TemplatePart(CloseButtonPart, typeof(AvaloniaButton))]
[TemplatePart(BodyPart, typeof(ScrollViewer))]
[PseudoClasses(TopPseudoClass, LowerPseudoClass, SheetModePseudoClass, DialogModePseudoClass, DraggingPseudoClass,
    HasBackPseudoClass, HasDescriptionPseudoClass, HasFooterPseudoClass)]
public class SheetStackSheet : ContentControl
{
    public const string BackButtonPart = "PART_BackButton";
    public const string CloseButtonPart = "PART_CloseButton";
    public const string BodyPart = "PART_Body";
    public const string TopPseudoClass = ":top";
    public const string LowerPseudoClass = ":lower";
    public const string SheetModePseudoClass = ":sheet-mode";
    public const string DialogModePseudoClass = ":dialog-mode";
    public const string DraggingPseudoClass = ":dragging";
    public const string HasBackPseudoClass = ":has-back";
    public const string HasDescriptionPseudoClass = ":has-description";
    public const string HasFooterPseudoClass = ":has-footer";

    /// <summary>The id <see cref="SheetStack.Push"/> and the stack's triggers open this level by. Required and unique in its stack.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<SheetStackSheet, string?>(nameof(Id));

    /// <summary>The heading, centered in the header; also the panel's accessible name and the next level's back label by default.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SheetStackSheet, string?>(nameof(Title));

    /// <summary>An optional line under the header that describes the panel (its accessible help text).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SheetStackSheet, string?>(nameof(Description));

    /// <summary>Optional content pinned under the scrolling body, right aligned (usually buttons).</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SheetStackSheet, object?>(nameof(Footer));

    /// <summary>What the back button of a level opened from this one shows; null shows this level's <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> BackLabelProperty =
        AvaloniaProperty.Register<SheetStackSheet, string?>(nameof(BackLabel));

    /// <summary>
    /// Whether dragging or flicking the panel down closes it (default true). When false the panel still follows a drag
    /// with resistance and springs back; the back button, close button, Escape and the scrim still close it.
    /// </summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<SheetStackSheet, bool>(nameof(IsDismissible), true);

    /// <summary>True while this level is the top of the stack (the only one that takes input).</summary>
    public static readonly DirectProperty<SheetStackSheet, bool> IsTopProperty =
        AvaloniaProperty.RegisterDirect<SheetStackSheet, bool>(nameof(IsTop), o => o.IsTop);

    /// <summary>How many open levels are above this one (0 for the top level and for a closed sheet).</summary>
    public static readonly DirectProperty<SheetStackSheet, int> DepthProperty =
        AvaloniaProperty.RegisterDirect<SheetStackSheet, int>(nameof(Depth), o => o.Depth);

    internal static readonly StyledProperty<string?> BackTextProperty =
        AvaloniaProperty.Register<SheetStackSheet, string?>(nameof(BackText));

    internal static readonly StyledProperty<string?> BackNameProperty =
        AvaloniaProperty.Register<SheetStackSheet, string?>(nameof(BackName));

    internal static readonly StyledProperty<string> CloseNameProperty =
        AvaloniaProperty.Register<SheetStackSheet, string>(nameof(CloseName), "Close");

    internal static readonly StyledProperty<double> DimOpacityProperty =
        AvaloniaProperty.Register<SheetStackSheet, double>(nameof(DimOpacity));

    internal static readonly StyledProperty<CornerRadius> SurfaceCornerRadiusProperty =
        AvaloniaProperty.Register<SheetStackSheet, CornerRadius>(nameof(SurfaceCornerRadius));

    private bool _isTop;
    private int _depth;
    private bool _isDialog;
    private AvaloniaButton? _backButton;
    private AvaloniaButton? _closeButton;

    static SheetStackSheet()
    {
        FocusableProperty.OverrideDefaultValue<SheetStackSheet>(true);
        IsTabStopProperty.OverrideDefaultValue<SheetStackSheet>(false);
    }

    /// <summary>Creates a sheet.</summary>
    public SheetStackSheet()
    {
        PseudoClasses.Set(SheetModePseudoClass, true);
        UpdateCornerRadius();
    }

    /// <summary>The id <see cref="SheetStack.Push"/> and the stack's triggers open this level by. Required and unique in its stack.</summary>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The heading, centered in the header; also the panel's accessible name and the next level's back label by default.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>An optional line under the header that describes the panel (its accessible help text).</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Optional content pinned under the scrolling body, right aligned (usually buttons).</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>What the back button of a level opened from this one shows; null shows this level's <see cref="Title"/>.</summary>
    public string? BackLabel { get => GetValue(BackLabelProperty); set => SetValue(BackLabelProperty, value); }

    /// <summary>
    /// Whether dragging or flicking the panel down closes it (default true). When false the panel still follows a drag
    /// with resistance and springs back; the back button, close button, Escape and the scrim still close it.
    /// </summary>
    public bool IsDismissible { get => GetValue(IsDismissibleProperty); set => SetValue(IsDismissibleProperty, value); }

    /// <summary>True while this level is the top of the stack (the only one that takes input).</summary>
    public bool IsTop { get => _isTop; private set => SetAndRaise(IsTopProperty, ref _isTop, value); }

    /// <summary>How many open levels are above this one (0 for the top level and for a closed sheet).</summary>
    public int Depth { get => _depth; private set => SetAndRaise(DepthProperty, ref _depth, value); }

    internal string? BackText { get => GetValue(BackTextProperty); set => SetValue(BackTextProperty, value); }

    internal string? BackName { get => GetValue(BackNameProperty); set => SetValue(BackNameProperty, value); }

    internal string CloseName { get => GetValue(CloseNameProperty); set => SetValue(CloseNameProperty, value); }

    internal double DimOpacity { get => GetValue(DimOpacityProperty); set => SetValue(DimOpacityProperty, value); }

    internal CornerRadius SurfaceCornerRadius { get => GetValue(SurfaceCornerRadiusProperty); set => SetValue(SurfaceCornerRadiusProperty, value); }

    internal bool IsDialog => _isDialog;

    internal ScrollViewer? Body { get; private set; }

    internal AvaloniaButton? BackButton => _backButton;

    internal AvaloniaButton? CloseButton => _closeButton;

    internal event EventHandler? BackRequested;

    internal event EventHandler? CloseRequested;

    /// <summary>The title or back label changed (the level above shows it on its back button).</summary>
    internal event EventHandler? HeaderChanged;

    internal void SetLevel(bool isTop, int depth, bool open)
    {
        IsTop = isTop;
        Depth = depth;
        PseudoClasses.Set(TopPseudoClass, isTop);
        PseudoClasses.Set(LowerPseudoClass, open && !isTop);
    }

    internal void SetDialog(bool dialog)
    {
        if (_isDialog == dialog && PseudoClasses.Contains(dialog ? DialogModePseudoClass : SheetModePseudoClass))
        {
            return;
        }
        _isDialog = dialog;
        PseudoClasses.Set(DialogModePseudoClass, dialog);
        PseudoClasses.Set(SheetModePseudoClass, !dialog);
        UpdateCornerRadius();
    }

    internal void SetDragging(bool dragging) => PseudoClasses.Set(DraggingPseudoClass, dragging);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_backButton is not null)
        {
            _backButton.Click -= OnBackClick;
        }
        if (_closeButton is not null)
        {
            _closeButton.Click -= OnCloseClick;
        }
        base.OnApplyTemplate(e);
        _backButton = e.NameScope.Find<AvaloniaButton>(BackButtonPart);
        _closeButton = e.NameScope.Find<AvaloniaButton>(CloseButtonPart);
        Body = e.NameScope.Find<ScrollViewer>(BodyPart);
        if (_backButton is not null)
        {
            _backButton.Click += OnBackClick;
        }
        if (_closeButton is not null)
        {
            _closeButton.Click += OnCloseClick;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Tab from the panel itself (where focus lands when the level opens) enters it at its first or last stop; from
        // there the panel's cycle keeps focus inside, skipping disabled controls.
        if (e.Key == Key.Tab && ReferenceEquals(e.Source, this) && IsTop)
        {
            var target = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                ? FocusManager.FindLastFocusableElement(this)
                : FocusManager.FindFirstFocusableElement(this);
            if (target is InputElement element && element.Focus(NavigationMethod.Tab))
            {
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title ?? string.Empty);
            HeaderChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == BackLabelProperty)
        {
            HeaderChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == DescriptionProperty)
        {
            AutomationProperties.SetHelpText(this, Description ?? string.Empty);
            PseudoClasses.Set(HasDescriptionPseudoClass, !string.IsNullOrEmpty(Description));
        }
        else if (change.Property == BackTextProperty)
        {
            PseudoClasses.Set(HasBackPseudoClass, !string.IsNullOrEmpty(BackText));
        }
        else if (change.Property == CornerRadiusProperty)
        {
            UpdateCornerRadius();
        }
        else if (change.Property == FooterProperty)
        {
            PseudoClasses.Set(HasFooterPseudoClass, change.NewValue is not null);
            // Footer controls join the logical tree here (so DataContext and styles flow) unless their owner holds them.
            if (change.OldValue is ILogical { LogicalParent: var oldParent } oldLogical && ReferenceEquals(oldParent, this))
            {
                LogicalChildren.Remove(oldLogical);
            }
            if (change.NewValue is ILogical { LogicalParent: null } newLogical)
            {
                LogicalChildren.Add(newLogical);
            }
        }
    }

    private void UpdateCornerRadius()
    {
        var r = CornerRadius;
        SurfaceCornerRadius = _isDialog ? r : new CornerRadius(r.TopLeft, r.TopRight, 0, 0);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SheetStackSheetAutomationPeer(this);

    /// <summary>
    /// A modal panel (a window in the automation tree) named by its title. A level below the top exposes no children:
    /// it stays visible behind the top level but is out of reach, like inert content.
    /// </summary>
    private sealed class SheetStackSheetAutomationPeer : ControlAutomationPeer
    {
        public SheetStackSheetAutomationPeer(SheetStackSheet owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
            => ((SheetStackSheet)Owner).IsTop ? base.GetChildrenCore() : Array.Empty<AutomationPeer>();
    }
}
