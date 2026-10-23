using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Arguments of the version menu's <c>CompareRequested</c> and <c>PromoteRequested</c>: the selected version.</summary>
internal sealed class PromptVersionEventArgs : RoutedEventArgs
{
    public PromptVersionEventArgs(RoutedEvent routedEvent, object source, PromptVersion version)
        : base(routedEvent, source)
    {
        Version = version;
    }

    /// <summary>The version the action is for (the selected row).</summary>
    public PromptVersion Version { get; }
}

/// <summary>
/// One version in the version menu, a radio item: 62.4px (padding 8 10, radius 18), the author's 28px avatar (2px down)
/// and 10px after it the commit message (16 / 22.4, the text color, ellipsis) over a 22px meta line (12 / 16.8, muted;
/// the label medium in the secondary color: "v3 · Lena Brooks · last week"). Checked: SurfaceMuted; under the pointer:
/// SurfaceMuted at 60% (120ms). The keyboard ring sits 2px inside the edge. The row of the version in production keeps
/// room at the end of its meta line for the production marker, which the menu draws over the rows so it can glide.
/// Focusable but not a tab stop (the menu moves focus); a click selects it.
/// </summary>
[PseudoClasses(CheckedPseudoClass)]
internal sealed class PromptVersionRow : TemplatedControl
{
    public const string CheckedPseudoClass = ":checked";

    /// <summary>A character that stands for the label while the meta format runs, so the label can be styled.</summary>
    private const string LabelSlot = "";

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<PromptVersionRow, string?>(nameof(Message));

    public static readonly StyledProperty<string?> AuthorNameProperty =
        AvaloniaProperty.Register<PromptVersionRow, string?>(nameof(AuthorName));

    public static readonly StyledProperty<IImage?> AuthorImageProperty =
        AvaloniaProperty.Register<PromptVersionRow, IImage?>(nameof(AuthorImage));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<PromptVersionRow, bool>(nameof(IsChecked));

    /// <summary>Room kept at the end of the meta line for the production marker (its width plus a gap), or 0.</summary>
    public static readonly StyledProperty<double> MarkerRoomProperty =
        AvaloniaProperty.Register<PromptVersionRow, double>(nameof(MarkerRoom));

    private TextBlock? _meta;
    private Control? _metaLine;
    private Run? _labelRun;
    private string _metaBefore = string.Empty;
    private string _metaLabel = string.Empty;
    private string _metaAfter = string.Empty;
    private bool _pressed;

    static PromptVersionRow()
    {
        FocusableProperty.OverrideDefaultValue<PromptVersionRow>(true);
        IsTabStopProperty.OverrideDefaultValue<PromptVersionRow>(false);
    }

    public PromptVersionRow(PromptVersion version)
    {
        Version = version;
        ActualThemeVariantChanged += (_, _) => ApplyLabelColor();
    }

    public PromptVersion Version { get; }

    /// <summary>The menu that built the row.</summary>
    public PromptVersionMenu? Owner { get; init; }

    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public string? AuthorName { get => GetValue(AuthorNameProperty); set => SetValue(AuthorNameProperty, value); }

    public IImage? AuthorImage { get => GetValue(AuthorImageProperty); set => SetValue(AuthorImageProperty, value); }

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    public double MarkerRoom { get => GetValue(MarkerRoomProperty); set => SetValue(MarkerRoomProperty, value); }

    /// <summary>The meta line as shown (label included).</summary>
    public string MetaText => _metaBefore + _metaLabel + _metaAfter;

    /// <summary>The meta line's text block (the template part), or null before the template.</summary>
    internal TextBlock? MetaBlock => _meta;

    /// <summary>The 22px meta line (the production marker sits at its end), or null before the template.</summary>
    internal Control? MetaLine => _metaLine;

    /// <summary>
    /// Sets the meta line from the menu's format: the label is found by formatting with a placeholder for it, so it can be
    /// medium and secondary while the rest stays muted (a format without the label shows as plain muted text).
    /// </summary>
    public void SetMeta(Func<string, string, string, string>? format, string label, string author, string when)
    {
        var withSlot = format?.Invoke(LabelSlot, author, when) ?? string.Empty;
        var index = withSlot.IndexOf(LabelSlot, StringComparison.Ordinal);
        if (index < 0)
        {
            _metaBefore = format?.Invoke(label, author, when) ?? string.Empty;
            _metaLabel = string.Empty;
            _metaAfter = string.Empty;
        }
        else
        {
            _metaBefore = withSlot[..index];
            _metaLabel = label;
            _metaAfter = withSlot[(index + LabelSlot.Length)..].Replace(LabelSlot, label, StringComparison.Ordinal);
        }
        UpdateMeta();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _meta = e.NameScope.Find<TextBlock>("PART_Meta");
        _metaLine = e.NameScope.Find<Control>("PART_MetaLine");
        UpdateMeta();
        ApplyMarkerRoom();
    }

    private void UpdateMeta()
    {
        if (_meta?.Inlines is not { } inlines)
        {
            return;
        }
        inlines.Clear();
        if (_metaBefore.Length > 0)
        {
            inlines.Add(new Run(_metaBefore));
        }
        if (_metaLabel.Length > 0)
        {
            _labelRun = new Run(_metaLabel) { FontWeight = FontWeight.Medium };
            ApplyLabelColor();
            inlines.Add(_labelRun);
        }
        else
        {
            _labelRun = null;
        }
        if (_metaAfter.Length > 0)
        {
            inlines.Add(new Run(_metaAfter));
        }
    }

    /// <summary>The label is medium in the secondary text color (the rest of the line inherits the muted color).</summary>
    private void ApplyLabelColor()
    {
        if (_labelRun is not null && TopLevel.GetTopLevel(this) is not null)
        {
            _labelRun.Foreground = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyLabelColor();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == MarkerRoomProperty)
        {
            ApplyMarkerRoom();
        }
    }

    private void ApplyMarkerRoom()
    {
        if (_meta is not null)
        {
            _meta.Margin = new Thickness(0, 0, MarkerRoom, 0);
        }
    }

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
            Owner?.OnRowInvoked(this, NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>A radio button (checked = selected); its name is the message and the meta line.</summary>
    private sealed class RowPeer(PromptVersionRow owner) : ControlAutomationPeer(owner), IToggleProvider, IInvokeProvider
    {
        private PromptVersionRow Row => (PromptVersionRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Row.Message;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public ToggleState ToggleState => Row.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public void Invoke()
        {
            EnsureEnabled();
            Row.Owner?.OnRowInvoked(Row, NavigationMethod.Unspecified);
        }
    }
}

/// <summary>
/// The author of a version as a 28px avatar: Arc's <see cref="Avatar"/> (small, 1px border, SurfaceMuted, initials) with
/// the author's picture, when there is one, as a 26px circle inside the border. Assistive technology sees an image named
/// after the author.
/// </summary>
internal sealed class PromptVersionAvatar : TemplatedControl
{
    public static readonly StyledProperty<string?> AuthorNameProperty =
        AvaloniaProperty.Register<PromptVersionAvatar, string?>(nameof(AuthorName));

    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<PromptVersionAvatar, IImage?>(nameof(Image));

    static PromptVersionAvatar()
    {
        FocusableProperty.OverrideDefaultValue<PromptVersionAvatar>(false);
    }

    public string? AuthorName { get => GetValue(AuthorNameProperty); set => SetValue(AuthorNameProperty, value); }

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AuthorNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarPeer(this);

    private sealed class AvatarPeer(PromptVersionAvatar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// An action of the version menu ("Compare with v3", "Promote to production"): a 36px row (padding 0 10, radius 16) with a
/// 16px icon (stroke 1.75, secondary) 10px before its text (14, the text color). Hover: SurfaceMuted; disabled: the icon
/// and text muted, no pointer cursor. The keyboard ring sits 2px inside the edge. A button: Enter, Space and clicks run it.
/// </summary>
internal sealed class PromptMenuAction : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<PromptMenuAction, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptMenuAction, string?>(nameof(Text));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override Type StyleKeyOverride => typeof(PromptMenuAction);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }
}

/// <summary>
/// The line under the actions: what production serves ("Production serves v4"), or after a promotion from this menu which
/// version is in production with an Undo button ("v3 is in production" · Undo). 12px muted text 10px in; Undo is a small
/// ghost button at the end. A new layer is made for every change so the menu can crossfade it.
/// </summary>
internal sealed class PromptVersionStatus : TemplatedControl
{
    public const string UndoPart = "PART_Undo";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptVersionStatus, string?>(nameof(Text));

    public static readonly StyledProperty<string?> UndoTextProperty =
        AvaloniaProperty.Register<PromptVersionStatus, string?>(nameof(UndoText));

    public static readonly StyledProperty<bool> ShowsUndoProperty =
        AvaloniaProperty.Register<PromptVersionStatus, bool>(nameof(ShowsUndo));

    private Button? _undo;

    static PromptVersionStatus()
    {
        FocusableProperty.OverrideDefaultValue<PromptVersionStatus>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? UndoText { get => GetValue(UndoTextProperty); set => SetValue(UndoTextProperty, value); }

    public bool ShowsUndo { get => GetValue(ShowsUndoProperty); set => SetValue(ShowsUndoProperty, value); }

    /// <summary>The menu that made the layer.</summary>
    public PromptVersionMenu? Owner { get; init; }

    /// <summary>The Undo button (null before the template).</summary>
    internal Button? UndoButton => _undo;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_undo is not null)
        {
            _undo.Click -= OnUndoClick;
        }
        _undo = e.NameScope.Find<Button>(UndoPart);
        if (_undo is not null)
        {
            _undo.Click += OnUndoClick;
        }
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Owner?.OnUndoClicked(this);
    }
}

/// <summary>
/// Lays out the version rows and, over them, the production marker: the marker spans the rows' area and the menu moves it
/// after every arrange of the host or of the rows (a resize lands it at once, a new production version glides it).
/// Assistive technology sees the radio group named by the menu.
/// </summary>
internal sealed class PromptVersionRowsHost : Panel
{
    public PromptVersionRowsHost(PromptVersionMenu owner, PromptProductionMarker marker)
    {
        Owner = owner;
        Rows = new PromptVersionRowsPanel(owner);
        Marker = marker;
        Children.Add(Rows);
        Children.Add(marker);
    }

    public PromptVersionMenu Owner { get; }

    public StackPanel Rows { get; }

    public PromptProductionMarker Marker { get; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        Owner.PlaceMarker();
        return arranged;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HostPeer(this);

    private sealed class HostPeer(PromptVersionRowsHost owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The rows, 2px apart. Rows can be laid out in a pass the host does not take part in (a row's meta line changed width),
/// so the rows' own arrange also places the marker.
/// </summary>
internal sealed class PromptVersionRowsPanel : StackPanel
{
    private readonly PromptVersionMenu _owner;

    public PromptVersionRowsPanel(PromptVersionMenu owner)
    {
        _owner = owner;
        Spacing = 2;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        _owner.PlaceMarker();
        return arranged;
    }
}

/// <summary>
/// The production marker of the version menu: a 22px pill (1px border of Success mixed 26% into Border, the Surface
/// inside, padding 0 8) holding a 6px Success dot and, 5px after it, the word (12px, secondary). It is a Core
/// <see cref="GlidingIndicator"/> that also draws its dot and word, so promoting a version (or undoing it) glides the
/// whole marker to the new row on the smooth spring (Arc: ω ≈ 13.4, critically damped); Reduced motion and Off jump.
/// The menu keeps room for it at the end of the production row's meta line. Text and colors are cached and follow the
/// theme; a glide draws without allocating.
/// </summary>
internal sealed class PromptProductionMarker : GlidingIndicator
{
    internal const double PillHeight = 22;
    internal const double InnerPadding = 8;
    internal const double DotSize = 6;
    internal const double DotGap = 5;
    internal const double BorderMix = 0.26;

    /// <summary>The menu moves the indicator to the pill's box deflated by this much, so the 1px stroke lies inside the box.</summary>
    internal const double StrokeInset = 0.5;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptProductionMarker, string?>(nameof(Text));

    private TextLayout? _layout;
    private IBrush? _dotBrush;
    private IBrush? _textBrush;

    static PromptProductionMarker()
    {
        AffectsRender<PromptProductionMarker>(TextProperty);
        FocusableProperty.OverrideDefaultValue<PromptProductionMarker>(false);
    }

    public PromptProductionMarker()
    {
        StrokeThickness = 1;
        CornerRadius = new CornerRadius(PillHeight / 2);
        ActualThemeVariantChanged += (_, _) => RefreshTheme();
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The pill's width for its word: border, padding, dot, gap, word, padding, border.</summary>
    public double PillWidth => 2 * (InnerPadding + 1) + DotSize + DotGap + (Layout()?.WidthIncludingTrailingWhitespace ?? 0);

    /// <summary>The colors the marker draws with (tests).</summary>
    internal (Color Fill, Color Stroke, Color Dot, Color Text) DrawColors =>
        (ColorOf(Fill), ColorOf(Stroke), ColorOf(_dotBrush), ColorOf(_textBrush));

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : default;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshTheme();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            DropLayout();
        }
    }

    /// <summary>Colors and font from the theme: Success 26% into Border for the edge, Surface inside, secondary text.</summary>
    private void RefreshTheme()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        var success = ArcTokens.GetColor(this, ArcDesignTokenKind.Success);
        var border = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        Fill = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Surface));
        Stroke = new ImmutableSolidColorBrush(PromptTone.Mix(border, success, BorderMix));
        _dotBrush = new ImmutableSolidColorBrush(success);
        _textBrush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary));
        DropLayout();
        InvalidateVisual();
    }

    private void DropLayout()
    {
        _layout?.Dispose();
        _layout = null;
        InvalidateVisual();
    }

    private TextLayout? Layout()
    {
        if (_layout is not null || string.IsNullOrEmpty(Text) || TopLevel.GetTopLevel(this) is null)
        {
            return _layout;
        }
        var family = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.BodyFontFamily);
        _layout = new TextLayout(Text, new Typeface(family), 12, _textBrush ?? Brushes.Gray, lineHeight: 16.8);
        return _layout;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var opacity = CurrentOpacity;
        var rect = CurrentRect;
        if (opacity <= 0.001 || rect.Width <= 0 || Layout() is not { } layout)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            // The indicator's rectangle is the pill deflated by half its 1px border (the stroke is centered on it).
            var x = rect.X - StrokeInset + 1 + InnerPadding;
            var middle = rect.Y + rect.Height / 2;
            if (_dotBrush is { } dot)
            {
                context.DrawEllipse(dot, null, new Point(x + DotSize / 2, middle), DotSize / 2, DotSize / 2);
            }
            layout.Draw(context, new Point(x + DotSize + DotGap, middle - layout.Height / 2));
        }
    }
}
