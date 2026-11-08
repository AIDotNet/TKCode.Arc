using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// One page in the thumbnail list, self-drawn because there may be thousands (performance rule 1): shadow, paper, the bitmap
/// from the viewer's shared cache, and the page label. It asks for its bitmap outside the render pass, since a request can
/// finish inline and invalidate visuals; a render pass only reads the cache.
/// </summary>
internal sealed class PdfThumbnail : Control
{
    public static readonly StyledProperty<double> ThumbnailWidthProperty =
        AvaloniaProperty.Register<PdfThumbnail, double>(nameof(ThumbnailWidth), 120);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PdfThumbnail, double>(nameof(Spacing), 12);

    public static readonly StyledProperty<BoxShadows> PageShadowProperty =
        AvaloniaProperty.Register<PdfThumbnail, BoxShadows>(nameof(PageShadow));

    public static readonly StyledProperty<IBrush?> CurrentBrushProperty =
        AvaloniaProperty.Register<PdfThumbnail, IBrush?>(nameof(CurrentBrush));

    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<PdfThumbnail, IBrush?>(nameof(FocusBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<PdfThumbnail, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> CurrentLabelBrushProperty =
        AvaloniaProperty.Register<PdfThumbnail, IBrush?>(nameof(CurrentLabelBrush));

    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<PdfThumbnail, double>(nameof(LabelFontSize), 12);

    private const double LabelGap = 6;
    private const double CurrentRingWidth = 2;

    private static readonly IBrush s_paper = new ImmutableSolidColorBrush(Colors.White);

    /// <summary>What this thumbnail did about its bitmap: asking again is for bitmaps that were seen and then evicted, not for ones that never came.</summary>
    private enum BitmapState
    {
        NotAsked,
        Asked,
        Seen
    }

    private PdfViewer? _owner;
    private int _page = -1;
    private bool _isCurrent;
    private PixelSize _target;
    private BitmapState _state;
    private bool _requestPosted;
    private FormattedText? _label;
    private Size _paper;
    private IPen? _currentPen;
    private IPen? _focusPen;

    static PdfThumbnail()
    {
        FocusableProperty.OverrideDefaultValue<PdfThumbnail>(true);
        AffectsMeasure<PdfThumbnail>(ThumbnailWidthProperty, SpacingProperty, LabelFontSizeProperty);
        AffectsRender<PdfThumbnail>(PageShadowProperty, CurrentBrushProperty, FocusBrushProperty, LabelBrushProperty, CurrentLabelBrushProperty);
    }

    public double ThumbnailWidth { get => GetValue(ThumbnailWidthProperty); set => SetValue(ThumbnailWidthProperty, value); }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public BoxShadows PageShadow { get => GetValue(PageShadowProperty); set => SetValue(PageShadowProperty, value); }

    public IBrush? CurrentBrush { get => GetValue(CurrentBrushProperty); set => SetValue(CurrentBrushProperty, value); }

    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    public IBrush? CurrentLabelBrush { get => GetValue(CurrentLabelBrushProperty); set => SetValue(CurrentLabelBrushProperty, value); }

    public double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }

    /// <summary>The page shown; -1 while the container is in the pool.</summary>
    public int PageIndex => _page;

    /// <summary>True for the viewer's current page.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent != value)
            {
                _isCurrent = value;
                PseudoClasses.Set(":current", value);
                InvalidateVisual();
            }
        }
    }

    /// <summary>The pixel size asked of the viewer.</summary>
    public PixelSize TargetSize => _target;

    /// <summary>Takes a page out of the pool's container.</summary>
    public void Prepare(PdfViewer owner, int page)
    {
        _owner = owner;
        _page = page;
        _state = BitmapState.NotAsked;
        _label = null;
        IsCurrent = page == owner.CurrentPageIndex;
        AutomationProperties.SetName(this, PdfViewerLangResources.Format(PdfViewerLangResourceKind.PageNumber, owner.ThumbnailLabel(page)));
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>The container goes back to the pool; a request that has not started is taken back.</summary>
    public void Clear()
    {
        if (_owner is { } owner && _state == BitmapState.Asked && owner.PeekThumbnail(_page, _target) is null)
        {
            owner.DropThumbnailRequest(_page, _target);
        }
        _owner = null;
        _page = -1;
        _state = BitmapState.NotAsked;
    }

    /// <summary>The bitmap of <see cref="TargetSize"/> arrived or was given up on.</summary>
    public void OnBitmapReady() => InvalidateVisual();

    /// <summary>The page's label became known. Its height is the same with any text.</summary>
    public void OnLabelChanged()
    {
        if (_owner is { } owner && _page >= 0)
        {
            _label = null;
            AutomationProperties.SetName(this, PdfViewerLangResources.Format(PdfViewerLangResourceKind.PageNumber, owner.ThumbnailLabel(_page)));
            InvalidateVisual();
        }
    }

    /// <summary>The sidebar was hidden, which cancels what was on its way: ask again.</summary>
    public void OnReopened()
    {
        _state = BitmapState.NotAsked;
        InvalidateVisual();
    }

    /// <summary>Goes to this page.</summary>
    public void Activate()
    {
        if (_owner is { } owner && _page >= 0)
        {
            owner.GoToPage(_page);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = ThumbnailWidth;
        var page = _owner?.PagesForThumbnails is { } source && _page >= 0 && _page < source.PageCount ? source.GetPageSize(_page) : new Size(width, width * 1.3);
        var height = page.Width > 0 ? width * page.Height / page.Width : width;
        _paper = new Size(width, height);
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var target = new PixelSize(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        if (target != _target)
        {
            _target = target;
            _state = BitmapState.NotAsked;
        }
        var label = EnsureLabel();
        // The gap goes above the page, so bringing a thumbnail into view leaves room for its ring.
        var totalHeight = Spacing + height + LabelGap + (label?.Height ?? 0);
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, totalHeight);
    }

    public override void Render(DrawingContext context)
    {
        // The whole slot is clickable.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_owner is not { } owner || _page < 0)
        {
            return;
        }
        var paper = new Rect(new Point(Math.Round((Bounds.Width - _paper.Width) / 2), Spacing), _paper);
        context.DrawRectangle(s_paper, null, new RoundedRect(paper), PageShadow);
        if (owner.PeekThumbnail(_page, _target) is { } bitmap)
        {
            _state = BitmapState.Seen;
            // The source rectangle is in pixels (handbook section 13, high-DPI pitfall).
            context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), paper);
        }
        else if (_state != BitmapState.Asked && !_requestPosted)
        {
            _requestPosted = true;
            Dispatcher.UIThread.Post(RequestBitmap, DispatcherPriority.Background);
        }
        if (_isCurrent && CurrentBrush is { } current)
        {
            _currentPen ??= new Pen(current, CurrentRingWidth);
            context.DrawRectangle(null, _currentPen, paper.Inflate(CurrentRingWidth / 2));
        }
        if (PseudoClasses.Contains(":focus-visible") && FocusBrush is { } focus)
        {
            _focusPen ??= new Pen(focus, CurrentRingWidth);
            context.DrawRectangle(null, _focusPen, paper.Inflate(CurrentRingWidth + 2));
        }
        if (EnsureLabel() is { } label && (_isCurrent ? CurrentLabelBrush : LabelBrush) is { } labelBrush)
        {
            label.SetForegroundBrush(labelBrush);
            context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, paper.Bottom + LabelGap));
        }
    }

    private void RequestBitmap()
    {
        _requestPosted = false;
        if (_owner is not { } owner || _page < 0 || !IsEffectivelyVisible || _state == BitmapState.Asked)
        {
            return;
        }
        _state = BitmapState.Asked;
        if (owner.RequestThumbnail(_page, _target) is not null)
        {
            InvalidateVisual();
        }
    }

    private FormattedText? EnsureLabel()
    {
        if (_label is null && _owner is { } owner && _page >= 0)
        {
            _label = new FormattedText(owner.ThumbnailLabel(_page), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(TextElement.GetFontFamily(this)), LabelFontSize, LabelBrush);
        }
        return _label;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentBrushProperty)
        {
            _currentPen = null;
        }
        else if (change.Property == FocusBrushProperty)
        {
            _focusPen = null;
        }
        else if (change.Property == LabelFontSizeProperty)
        {
            _label = null;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Activate();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
        {
            Activate();
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PdfThumbnailAutomationPeer(this);

    private sealed class PdfThumbnailAutomationPeer : ControlAutomationPeer
    {
        public PdfThumbnailAutomationPeer(PdfThumbnail owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
