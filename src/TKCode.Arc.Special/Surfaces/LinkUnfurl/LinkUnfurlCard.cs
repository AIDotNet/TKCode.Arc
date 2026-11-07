using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>
/// One link's card in the lane of a <see cref="LinkUnfurl"/> (Arc Link unfurl, design reference): a plate (surface-raised
/// fill, 1px border, a shadow that lifts only in flight) drawn by this control, and the card itself (padding 8, a 30px
/// header with the favicon, the site and the collapse and remove buttons, then the title link, a two-line description and
/// the 1.91 : 1 image) as its child. The unfurl clips the child to the visible rectangle and moves it onto the link; see
/// <see cref="LinkUnfurlMorph"/>.
/// </summary>
internal sealed class LinkUnfurlCardHost : Control
{
    public static readonly StyledProperty<IBrush?> PlateFillProperty =
        AvaloniaProperty.Register<LinkUnfurlCardHost, IBrush?>(nameof(PlateFill));

    public static readonly StyledProperty<IBrush?> PlateBorderProperty =
        AvaloniaProperty.Register<LinkUnfurlCardHost, IBrush?>(nameof(PlateBorder));

    public static readonly StyledProperty<BoxShadows> LiftShadowProperty =
        AvaloniaProperty.Register<LinkUnfurlCardHost, BoxShadows>(nameof(LiftShadow));

    private readonly LinkUnfurl _owner;
    private readonly Border _card;
    private readonly Grid _header;
    private readonly StackPanel _body;
    private readonly LinkUnfurlFavicon _favicon;
    private readonly TextBlock _site;
    private readonly LinkUnfurlTitleLink _title;
    private readonly TextBlock _description;
    private readonly LinkUnfurlMedia _media;
    private readonly LinkUnfurlIconButton _collapse;
    private readonly LinkUnfurlIconButton _remove;
    private readonly TranslateTransform _shift = new();
    private readonly RectangleGeometry _clip = new();
    private readonly DragTracker _drag;
    private IPen? _platePen;
    private Rect _plate;
    private double _plateRadius = LinkUnfurlMorph.CardRadius;
    private double _lift;
    private bool _morphing;

    static LinkUnfurlCardHost()
    {
        AffectsRender<LinkUnfurlCardHost>(PlateFillProperty, PlateBorderProperty, LiftShadowProperty);
    }

    public LinkUnfurlCardHost(LinkUnfurl owner, LinkUnfurlEntry entry)
    {
        _owner = owner;
        Entry = entry;
        ClipToBounds = false;
        _favicon = new LinkUnfurlFavicon { VerticalAlignment = VerticalAlignment.Center };
        _site = new TextBlock
        {
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            LineHeight = 16.8
        };
        ArcTokens.Bind(_site, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_site, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        _collapse = new LinkUnfurlIconButton { Glyph = ArcIcons.ChevronUp };
        _remove = new LinkUnfurlIconButton { Glyph = ArcIcons.X };
        _collapse.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.CollapsePreview(Entry.Id);
        };
        _remove.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.RemoveLink(Entry.Id);
        };
        _header = new Grid
        {
            Height = 30,
            Margin = new Thickness(7, 0, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("16,*,30,30"),
            Background = Brushes.Transparent
        };
        Grid.SetColumn(_site, 1);
        Grid.SetColumn(_collapse, 2);
        Grid.SetColumn(_remove, 3);
        _header.Children.Add(_favicon);
        _header.Children.Add(_site);
        _header.Children.Add(_collapse);
        _header.Children.Add(_remove);
        _title = new LinkUnfurlTitleLink();
        _title.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.OpenLink(Entry);
        };
        _description = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            LineHeight = 19.6
        };
        ArcTokens.Bind(_description, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_description, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        _media = new LinkUnfurlMedia { Margin = new Thickness(-6, 8, -6, 0) };
        _body = new StackPanel { Margin = new Thickness(6, 2, 6, 0), Spacing = 2 };
        _body.Children.Add(_title);
        _body.Children.Add(_description);
        _body.Children.Add(_media);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        Grid.SetRow(_body, 1);
        grid.Children.Add(_header);
        grid.Children.Add(_body);
        _card = new Border
        {
            Padding = new Thickness(8),
            Background = Brushes.Transparent,
            Child = grid,
            RenderTransform = _shift,
            Focusable = false
        };
        AutomationProperties.SetAccessibilityView(_card, AccessibilityView.Content);
        LogicalChildren.Add(_card);
        VisualChildren.Add(_card);
        _drag = new DragTracker(_card, new DragOptions { Axis = DragAxis.Vertical, Threshold = 6 })
        {
            CanBegin = CanDrag
        };
        _drag.Started += (_, e) => _owner.OnCardDragStarted(Entry, e);
        _drag.Moved += (_, e) => _owner.OnCardDragMoved(Entry, e);
        _drag.Completed += (_, e) => _owner.OnCardDragEnded(Entry, e, cancelled: false);
        _drag.Cancelled += (_, e) => _owner.OnCardDragEnded(Entry, e, cancelled: true);
    }

    public LinkUnfurlEntry Entry { get; }

    public IBrush? PlateFill
    {
        get => GetValue(PlateFillProperty);
        set => SetValue(PlateFillProperty, value);
    }

    public IBrush? PlateBorder
    {
        get => GetValue(PlateBorderProperty);
        set => SetValue(PlateBorderProperty, value);
    }

    public BoxShadows LiftShadow
    {
        get => GetValue(LiftShadowProperty);
        set => SetValue(LiftShadowProperty, value);
    }

    /// <summary>The card (the clipped child).</summary>
    internal Border Card => _card;

    internal Grid Header => _header;

    internal StackPanel Body => _body;

    internal LinkUnfurlMedia Media => _media;

    internal LinkUnfurlIconButton CollapseButton => _collapse;

    internal LinkUnfurlIconButton RemoveButton => _remove;

    internal LinkUnfurlTitleLink TitleLink => _title;

    internal TextBlock SiteText => _site;

    internal TextBlock DescriptionText => _description;

    /// <summary>The plate's rectangle in this control's coordinates, and its shadow's strength (tests).</summary>
    internal Rect Plate => _plate;

    internal double Lift => _lift;

    /// <summary>The clip on the card while it travels; null once it rests open.</summary>
    internal Rect? ClipRect => _card.Clip is null ? null : _clip.Rect;

    /// <summary>Fills the card from the preview and names its parts.</summary>
    public void SetPreview(LinkUnfurlPreview preview, LinkUnfurlLabels labels)
    {
        _favicon.Preview = preview;
        _site.Text = preview.Site;
        _title.Content = preview.Title;
        AutomationProperties.SetName(_title, preview.Title);
        _description.Text = preview.Description;
        _description.IsVisible = !string.IsNullOrEmpty(preview.Description);
        _media.Image = preview.Image;
        _media.IsVisible = preview.Image is not null;
        AutomationProperties.SetName(_media, preview.ImageAlt ?? string.Empty);
        AutomationProperties.SetAccessibilityView(_media, string.IsNullOrEmpty(preview.ImageAlt) ? AccessibilityView.Raw : AccessibilityView.Content);
        UpdateNames(preview, labels);
    }

    public void UpdateNames(LinkUnfurlPreview preview, LinkUnfurlLabels labels)
    {
        AutomationProperties.SetName(_card, labels.Card(preview.Site, preview.Title));
        AutomationProperties.SetName(_collapse, labels.CollapsePreview(preview.Title));
        AutomationProperties.SetName(_remove, labels.RemoveLink(preview.Title));
    }

    /// <summary>Places the card for one frame of the unfurl (<paramref name="slot"/> is where it rests, in the lane).</summary>
    public void Apply(in LinkUnfurlMorphFrame frame, Rect slot, bool resting)
    {
        Opacity = frame.Opacity;
        _body.Opacity = frame.BodyOpacity;
        _media.ImageScale = frame.ImageScale;
        _lift = resting ? 0 : frame.Lift;
        _plate = resting ? new Rect(slot.Size) : frame.Visible.Translate(-slot.Position);
        _plateRadius = frame.Radius;
        if (resting)
        {
            if (_morphing)
            {
                _morphing = false;
                _card.Clip = null;
                _shift.X = 0;
                _shift.Y = 0;
                _card.InvalidateVisual();
            }
        }
        else
        {
            _morphing = true;
            _clip.Rect = new Rect(frame.ClipLeft, frame.ClipTop, frame.Visible.Width, frame.Visible.Height);
            _clip.RadiusX = frame.Radius;
            _clip.RadiusY = frame.Radius;
            if (!ReferenceEquals(_card.Clip, _clip))
            {
                _card.Clip = _clip;
            }
            _shift.X = frame.Offset.X;
            _shift.Y = frame.Offset.Y;
            _card.InvalidateVisual();
        }
        InvalidateVisual();
    }

    /// <summary>Hides the card from input and assistive technology (folded) or shows it.</summary>
    public void SetShown(bool shown)
    {
        if (IsVisible == shown)
        {
            return;
        }
        if (!shown && this.IsKeyboardFocusWithin)
        {
            _owner.FocusEditor();
        }
        IsVisible = shown;
    }

    private bool CanDrag(PointerPressedEventArgs e)
    {
        if (!Entry.Link.IsExpanded || e.Source is not Visual source)
        {
            return false;
        }
        // Buttons and the title keep their clicks; on touch only the header drags so the page scrolls over the body.
        for (Visual? v = source; v is not null && !ReferenceEquals(v, _card); v = v.GetVisualParent())
        {
            if (v is Button)
            {
                return false;
            }
        }
        if (e.Pointer.Type == PointerType.Touch)
        {
            return source == _header || source.GetVisualAncestors().Contains(_header);
        }
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _card.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(availableSize.Width, _card.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _card.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlateBorderProperty)
        {
            _platePen = PlateBorder is { } brush ? new Avalonia.Media.Immutable.ImmutablePen(brush.ToImmutable(), 1) : null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_plate.Width <= 0 || _plate.Height <= 0)
        {
            return;
        }
        var radius = Math.Min(_plateRadius, Math.Min(_plate.Width, _plate.Height) / 2);
        var rounded = new RoundedRect(_plate, radius);
        if (_lift > 0.001 && PlateFill is { } fill)
        {
            using (context.PushOpacity(_lift))
            {
                context.DrawRectangle(fill, null, rounded, LiftShadow);
            }
        }
        context.DrawRectangle(PlateFill, null, rounded);
        if (_platePen is not null)
        {
            // An inset 1px ring.
            context.DrawRectangle(null, _platePen, new RoundedRect(_plate.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    private sealed class NoneAutomationPeer : ControlAutomationPeer
    {
        public NoneAutomationPeer(Control owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;
    }
}

/// <summary>A card's 16px favicon (or the site's initial, surface-colored on a text-colored square), rounded 4.</summary>
internal sealed class LinkUnfurlFavicon : Control
{
    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<LinkUnfurlFavicon, IBrush?>(nameof(Ink));

    public static readonly StyledProperty<IBrush?> PaperProperty =
        AvaloniaProperty.Register<LinkUnfurlFavicon, IBrush?>(nameof(Paper));

    private LinkUnfurlPreview? _preview;
    private Avalonia.Media.TextFormatting.TextLayout? _monogram;

    static LinkUnfurlFavicon()
    {
        AffectsRender<LinkUnfurlFavicon>(InkProperty, PaperProperty);
    }

    public LinkUnfurlFavicon()
    {
        Width = 16;
        Height = 16;
        IsHitTestVisible = false;
        ArcTokens.Bind(this, InkProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, PaperProperty, ArcDesignTokenKind.Surface);
    }

    /// <summary>Size of the icon (16 in a card, 14 on a sample button).</summary>
    public double Size
    {
        get => Width;
        set
        {
            Width = value;
            Height = value;
        }
    }

    public IBrush? Ink
    {
        get => GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    public IBrush? Paper
    {
        get => GetValue(PaperProperty);
        set => SetValue(PaperProperty, value);
    }

    public LinkUnfurlPreview? Preview
    {
        get => _preview;
        set
        {
            _preview = value;
            _monogram?.Dispose();
            _monogram = null;
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaperProperty)
        {
            _monogram?.Dispose();
            _monogram = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_preview is not { } preview)
        {
            return;
        }
        var slot = new Rect(Bounds.Size);
        if (slot.Width <= 0 || slot.Height <= 0)
        {
            return;
        }
        var rounded = new RoundedRect(slot, 4 * slot.Width / 16);
        if (preview.Favicon is { } icon)
        {
            using var clip = context.PushClip(rounded);
            context.DrawImage(icon, LinkUnfurlMedia.SourceRect(icon), slot);
            return;
        }
        context.DrawRectangle(Ink, null, rounded);
        _monogram ??= new Avalonia.Media.TextFormatting.TextLayout(preview.Monogram,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium), 10 * slot.Width / 16, Paper);
        _monogram.Draw(context, new Point((slot.Width - _monogram.WidthIncludingTrailingWhitespace) / 2, (slot.Height - _monogram.Height) / 2));
    }
}

/// <summary>
/// A card's image (Arc's media: margin 8 −6 0 so it spans the header's width, radius 10, surface-muted behind, a 1px
/// border-subtle inner ring, 1.91 : 1, cover-fitted), drawn at <see cref="ImageScale"/> about its centre (1.08 → 1 as the
/// card opens).
/// </summary>
internal sealed class LinkUnfurlMedia : Control
{
    public const double Ratio = 1.91;

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LinkUnfurlMedia, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> RingProperty =
        AvaloniaProperty.Register<LinkUnfurlMedia, IBrush?>(nameof(Ring));

    private IImage? _image;
    private double _scale = 1;
    private IPen? _ring;

    static LinkUnfurlMedia()
    {
        AffectsRender<LinkUnfurlMedia>(FillProperty, RingProperty);
    }

    public LinkUnfurlMedia()
    {
        ArcTokens.Bind(this, FillProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, RingProperty, ArcDesignTokenKind.BorderSubtle);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Ring
    {
        get => GetValue(RingProperty);
        set => SetValue(RingProperty, value);
    }

    public IImage? Image
    {
        get => _image;
        set
        {
            _image = value;
            InvalidateVisual();
        }
    }

    public double ImageScale
    {
        get => _scale;
        set
        {
            if (Math.Abs(_scale - value) < 1e-4)
            {
                return;
            }
            _scale = value;
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RingProperty)
        {
            _ring = Ring is { } brush ? new Avalonia.Media.Immutable.ImmutablePen(brush.ToImmutable(), 1) : null;
        }
    }

    /// <summary>The whole of <paramref name="image"/> as a source rectangle (a bitmap's is in pixels, not DIPs).</summary>
    internal static Rect SourceRect(IImage image) =>
        image is Avalonia.Media.Imaging.Bitmap bitmap ? new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height) : new Rect(image.Size);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 424 : availableSize.Width;
        return new Size(width, Math.Round(width / Ratio, 2));
    }

    public override void Render(DrawingContext context)
    {
        var box = new Rect(Bounds.Size);
        if (box.Width <= 0 || box.Height <= 0)
        {
            return;
        }
        var rounded = new RoundedRect(box, 10);
        using (context.PushClip(rounded))
        {
            context.DrawRectangle(Fill, null, box);
            if (_image is { } image && image.Size.Width > 0 && image.Size.Height > 0)
            {
                // Cover: fill the box, crop the overflow, centred; then the settle zoom about the centre.
                var fit = Math.Max(box.Width / image.Size.Width, box.Height / image.Size.Height) * _scale;
                var w = image.Size.Width * fit;
                var h = image.Size.Height * fit;
                context.DrawImage(image, SourceRect(image), new Rect((box.Width - w) / 2, (box.Height - h) / 2, w, h));
            }
        }
        if (_ring is not null)
        {
            context.DrawRectangle(null, _ring, new RoundedRect(box.Deflate(0.5), 9.5));
        }
    }
}
