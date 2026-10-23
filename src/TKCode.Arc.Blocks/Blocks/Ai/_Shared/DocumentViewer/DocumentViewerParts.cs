using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The looks of the buttons of a <see cref="DocumentViewer"/>'s toolbar (spec 3.3).</summary>
internal enum DocumentViewerButtonKind
{
    /// <summary>The file button: 32 high, the file mark, the name and (with a menu) a chevron.</summary>
    File,

    /// <summary>Zoom out or in: 28 × 26 inside the zoom group, a 14px glyph.</summary>
    Step,

    /// <summary>The percentage between them: 46 × 26, 12px tabular figures; resets to 100%.</summary>
    Percent,

    /// <summary>Fit: 30 high, outlined when off, a SurfaceMuted block when on (<c>aria-pressed</c>).</summary>
    Fit
}

/// <summary>
/// A toolbar button of <see cref="DocumentViewer"/>, styled by <see cref="Kind"/>. <see cref="IsOn"/> is the pressed Fit
/// (<c>aria-pressed</c>) or the open file menu (<c>aria-expanded</c>). Hover uses <c>:hover</c>, set for mouse and pen only
/// (Arc's hover rules sit behind <c>(hover: hover) and (pointer: fine)</c>), so a tap leaves no hover look behind.
/// </summary>
internal sealed class DocumentViewerButton : AvaloniaButton
{
    /// <summary>Mouse or pen over the button.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>Fit pressed or the file menu open.</summary>
    public const string OnPseudoClass = ":on";

    /// <summary>The look.</summary>
    public static readonly StyledProperty<DocumentViewerButtonKind> KindProperty =
        AvaloniaProperty.Register<DocumentViewerButton, DocumentViewerButtonKind>(nameof(Kind));

    /// <summary>A Lucide glyph drawn in the middle (the zoom steps).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<DocumentViewerButton, Geometry?>(nameof(Glyph));

    /// <summary>Fit pressed, or the file button's menu open.</summary>
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<DocumentViewerButton, bool>(nameof(IsOn));

    /// <summary>Whether the file button opens a menu (an expand / collapse button for assistive technology).</summary>
    public static readonly StyledProperty<bool> HasMenuProperty =
        AvaloniaProperty.Register<DocumentViewerButton, bool>(nameof(HasMenu));

    /// <inheritdoc cref="KindProperty"/>
    public DocumentViewerButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="IsOnProperty"/>
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    /// <inheritdoc cref="HasMenuProperty"/>
    public bool HasMenu { get => GetValue(HasMenuProperty); set => SetValue(HasMenuProperty, value); }

    protected override Type StyleKeyOverride => typeof(DocumentViewerButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty)
        {
            var on = change.GetNewValue<bool>();
            PseudoClasses.Set(OnPseudoClass, on);
            if (ControlAutomationPeer.FromElement(this) is DocumentViewerButtonPeer peer)
            {
                peer.RaiseStateChanged(on);
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DocumentViewerButtonPeer(this);

    /// <summary>A button; Fit is a toggle, the file button with a menu expands and collapses it.</summary>
    private sealed class DocumentViewerButtonPeer(DocumentViewerButton owner) : ButtonAutomationPeer(owner), IToggleProvider, IExpandCollapseProvider
    {
        private DocumentViewerButton Button => (DocumentViewerButton)Owner;

        public ToggleState ToggleState => Button.IsOn ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public ExpandCollapseState ExpandCollapseState => Button.IsOn ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand()
        {
            if (!Button.IsOn)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Button.IsOn)
            {
                Invoke();
            }
        }

        public void RaiseStateChanged(bool on)
        {
            if (Button.Kind == DocumentViewerButtonKind.Fit)
            {
                RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                    on ? ToggleState.Off : ToggleState.On, on ? ToggleState.On : ToggleState.Off);
            }
            else if (Button.Kind == DocumentViewerButtonKind.File && Button.HasMenu)
            {
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    on ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                    on ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
            }
        }

        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(IToggleProvider))
            {
                return Button.Kind == DocumentViewerButtonKind.Fit ? this : null;
            }
            if (providerType == typeof(IExpandCollapseProvider))
            {
                return Button.Kind == DocumentViewerButtonKind.File && Button.HasMenu ? this : null;
            }
            return base.GetProviderCore(providerType);
        }
    }
}

/// <summary>
/// The mark of a document on the file button and in the document menu. A Pdf (or any paper) document is a drawn sheet,
/// 15 × 19: a page with a folded corner, outlined in <see cref="Stroke"/> on <see cref="Fill"/>, and a small type tag in
/// <see cref="TagFill"/> over its lower left edge with three white marks for its letters (our own generic mark). An email
/// is the Lucide <c>mail</c> glyph at 16 with a stroke of 2 in <see cref="MailStroke"/>.
/// </summary>
internal sealed class DocumentFileMark : Control
{
    /// <summary>Paper or email.</summary>
    public static readonly StyledProperty<DocumentKind> KindProperty =
        AvaloniaProperty.Register<DocumentFileMark, DocumentKind>(nameof(Kind), DocumentKind.Pdf);

    /// <summary>The sheet's fill.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<DocumentFileMark, IBrush?>(nameof(Fill));

    /// <summary>The sheet's outline.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<DocumentFileMark, IBrush?>(nameof(Stroke));

    /// <summary>The type tag.</summary>
    public static readonly StyledProperty<IBrush?> TagFillProperty =
        AvaloniaProperty.Register<DocumentFileMark, IBrush?>(nameof(TagFill));

    /// <summary>The email glyph's stroke.</summary>
    public static readonly StyledProperty<IBrush?> MailStrokeProperty =
        AvaloniaProperty.Register<DocumentFileMark, IBrush?>(nameof(MailStroke));

    /// <summary>The sheet's width.</summary>
    public const double SheetWidth = 15;

    /// <summary>The sheet's height.</summary>
    public const double SheetHeight = 19;

    /// <summary>The email glyph's size.</summary>
    public const double MailSize = 16;

    // The Lucide "file" outline (24 box) scaled to 19 high and moved 1.5 left so the tag can stand out on the left.
    private const double SheetScale = SheetHeight / 24;
    private static readonly Geometry s_sheet = Geometry.Parse(
        "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z");
    private static readonly Geometry s_fold = Geometry.Parse("M14 2v5a1 1 0 0 0 1 1h5");
    private static readonly Rect s_tag = new(0.5, 10.25, 9.25, 5);
    private static readonly IPen s_tagEdge = new ImmutablePen(Brushes.White, 0.8);
    private static readonly Rect[] s_letters = [new(2, 11.6, 1.5, 2.3), new(4.4, 11.6, 1.5, 2.3), new(6.8, 11.6, 1.5, 2.3)];

    private IPen? _sheetPen;
    private IPen? _mailPen;

    static DocumentFileMark()
    {
        AffectsMeasure<DocumentFileMark>(KindProperty);
        AffectsRender<DocumentFileMark>(KindProperty, FillProperty, StrokeProperty, TagFillProperty, MailStrokeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<DocumentFileMark>(false);
        FocusableProperty.OverrideDefaultValue<DocumentFileMark>(false);
    }

    /// <inheritdoc cref="KindProperty"/>
    public DocumentKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <inheritdoc cref="StrokeProperty"/>
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    /// <inheritdoc cref="TagFillProperty"/>
    public IBrush? TagFill { get => GetValue(TagFillProperty); set => SetValue(TagFillProperty, value); }

    /// <inheritdoc cref="MailStrokeProperty"/>
    public IBrush? MailStroke { get => GetValue(MailStrokeProperty); set => SetValue(MailStrokeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrokeProperty)
        {
            _sheetPen = null;
        }
        else if (change.Property == MailStrokeProperty)
        {
            _mailPen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        Kind == DocumentKind.Email ? new Size(MailSize, MailSize) : new Size(SheetWidth, SheetHeight);

    public override void Render(DrawingContext context)
    {
        if (Kind == DocumentKind.Email)
        {
            if (MailStroke is not { } mail)
            {
                return;
            }
            _mailPen ??= new Pen(mail, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var scale = MailSize / 24;
            using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            {
                context.DrawGeometry(null, _mailPen, ArcIcons.Mail);
            }
            return;
        }
        if (Stroke is { } stroke)
        {
            _sheetPen ??= new Pen(stroke, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        using (context.PushTransform(Matrix.CreateScale(SheetScale, SheetScale) * Matrix.CreateTranslation(-1.5, 0)))
        {
            context.DrawGeometry(Fill, _sheetPen, s_sheet);
            context.DrawGeometry(null, _sheetPen, s_fold);
        }
        if (TagFill is { } tag)
        {
            context.DrawRectangle(tag, s_tagEdge, s_tag, 1, 1);
            foreach (var letter in s_letters)
            {
                context.FillRectangle(Brushes.White, letter);
            }
        }
    }
}

/// <summary>
/// Holds the page at its zoom without laying it out again: the child is measured and arranged at its own size (base units)
/// and drawn through a scale transform from its top left corner, while the sizer reports the scaled size, so the canvas'
/// scroll extent is right. Changing <see cref="Scale"/> only measures the sizer again (the child's measure is still valid).
/// </summary>
internal sealed class DocumentViewerSizer : Decorator
{
    private readonly ScaleTransform _transform = new(1, 1);
    private double _scale = 1;

    /// <summary>The zoom the child is drawn at.</summary>
    public double Scale
    {
        get => _scale;
        set
        {
            if (_scale == value)
            {
                return;
            }
            _scale = value;
            _transform.ScaleX = value;
            _transform.ScaleY = value;
            InvalidateMeasure();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty)
        {
            if (change.OldValue is Control old)
            {
                old.RenderTransform = null;
            }
            if (change.NewValue is Control child)
            {
                child.RenderTransformOrigin = RelativePoint.TopLeft;
                child.RenderTransform = _transform;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(Size.Infinity);
        var size = child.DesiredSize;
        return new Size(size.Width * _scale, size.Height * _scale);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(Child.DesiredSize));
        return finalSize;
    }
}

/// <summary>The two lines of a document menu row: the file name (14, Foreground) over its description (12, TextMuted).</summary>
internal sealed class DocumentMenuRowText : StackPanel
{
    public DocumentMenuRowText(ExtractionDocument document)
    {
        Document = document;
        VerticalAlignment = VerticalAlignment.Center;
        var name = new TextBlock
        {
            Text = document.Name,
            FontSize = 14,
            LineHeight = 19.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        ArcTokens.Bind(name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        Children.Add(name);
        if (!string.IsNullOrEmpty(document.Label))
        {
            var label = new TextBlock
            {
                Text = document.Label,
                FontSize = 12,
                LineHeight = 16.8,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            Children.Add(label);
        }
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The document of the row.</summary>
    public ExtractionDocument Document { get; }
}
