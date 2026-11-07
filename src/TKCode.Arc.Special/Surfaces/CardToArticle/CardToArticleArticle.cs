using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The open article on the window's overlay layer (the panel of a <see cref="ModalPresence"/>): a sheet centred in the
/// window (<see cref="CardToArticleLayout.Sheet"/>) holding a scroller with the hero picture and the article text, the
/// reading progress bar and the close button. The whole layer is the "lift" the pull gesture moves and shrinks.
/// <para>
/// While the shared-element flight scales the sheet non-uniformly, the text and the close button are counter-scaled
/// every frame so they keep their size and are only clipped by the sheet, the text riding on the hero's bottom edge
/// (<see cref="UpdateFlightCorrection"/>). Code-driven: the correction depends on the flight transform written by
/// <see cref="SharedElementScope"/> each frame, which no ControlTheme transition can follow.
/// </para>
/// </summary>
[TemplatePart(FramePart, typeof(SharedElementFrame))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(HeroPart, typeof(CardToArticleHero))]
[TemplatePart(BodyPart, typeof(Border))]
[TemplatePart(ProgressPart, typeof(Border))]
[TemplatePart(ClosePart, typeof(CardToArticleCloseButton))]
internal sealed class CardToArticleArticle : TemplatedControl
{
    public const string FramePart = "PART_Frame";
    public const string ScrollerPart = "PART_Scroller";
    public const string HeroPart = "PART_Hero";
    public const string BodyPart = "PART_Body";
    public const string ProgressPart = "PART_Progress";
    public const string ClosePart = "PART_Close";

    /// <summary>Snap back after a short pull (Arc: critically damped, 0.38s).</summary>
    internal static readonly SpringSpec SnapBackSpring = SpringSpec.FromVisual(0.38, 0);

    public static readonly StyledProperty<CardToArticleItem?> ItemProperty =
        CardToArticleHero.ItemProperty.AddOwner<CardToArticleArticle>();

    public static readonly StyledProperty<double> AspectRatioProperty =
        CardToArticleHero.AspectRatioProperty.AddOwner<CardToArticleArticle>();

    public static readonly StyledProperty<IDataTemplate?> BodyTemplateProperty =
        AvaloniaProperty.Register<CardToArticleArticle, IDataTemplate?>(nameof(BodyTemplate));

    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<CardToArticleArticle, string>(nameof(CloseLabel), "Close article");

    public static readonly StyledProperty<double> ArticleGapProperty =
        AvaloniaProperty.Register<CardToArticleArticle, double>(nameof(ArticleGap), 18);

    public static readonly StyledProperty<double> ParagraphLineHeightProperty =
        AvaloniaProperty.Register<CardToArticleArticle, double>(nameof(ParagraphLineHeight), 25.6);

    public static readonly StyledProperty<double> QuoteLineHeightProperty =
        AvaloniaProperty.Register<CardToArticleArticle, double>(nameof(QuoteLineHeight), 30.8);

    public static readonly StyledProperty<Thickness> QuoteRuleThicknessProperty =
        AvaloniaProperty.Register<CardToArticleArticle, Thickness>(nameof(QuoteRuleThickness), new Thickness(2, 0, 0, 0));

    public static readonly StyledProperty<Thickness> QuotePaddingProperty =
        AvaloniaProperty.Register<CardToArticleArticle, Thickness>(nameof(QuotePadding), new Thickness(16, 0, 0, 0));

    public static readonly StyledProperty<Thickness> QuoteMarginProperty =
        AvaloniaProperty.Register<CardToArticleArticle, Thickness>(nameof(QuoteMargin), new Thickness(0, 6));

    public static readonly StyledProperty<double> AvatarDiameterProperty =
        AvaloniaProperty.Register<CardToArticleArticle, double>(nameof(AvatarDiameter), 28);

    public static readonly StyledProperty<double> BylineGapProperty =
        AvaloniaProperty.Register<CardToArticleArticle, double>(nameof(BylineGap), 8);

    public static readonly StyledProperty<double> BodyPaddingTopProperty =
        AvaloniaProperty.Register<CardToArticleArticle, double>(nameof(BodyPaddingTop), 28);

    public static readonly StyledProperty<double> BodyPaddingBottomProperty =
        AvaloniaProperty.Register<CardToArticleArticle, double>(nameof(BodyPaddingBottom), 56);

    public CardToArticleItem? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }
    public IDataTemplate? BodyTemplate { get => GetValue(BodyTemplateProperty); set => SetValue(BodyTemplateProperty, value); }
    public string CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }
    public double ArticleGap { get => GetValue(ArticleGapProperty); set => SetValue(ArticleGapProperty, value); }
    public double ParagraphLineHeight { get => GetValue(ParagraphLineHeightProperty); set => SetValue(ParagraphLineHeightProperty, value); }
    public double QuoteLineHeight { get => GetValue(QuoteLineHeightProperty); set => SetValue(QuoteLineHeightProperty, value); }
    public Thickness QuoteRuleThickness { get => GetValue(QuoteRuleThicknessProperty); set => SetValue(QuoteRuleThicknessProperty, value); }
    public Thickness QuotePadding { get => GetValue(QuotePaddingProperty); set => SetValue(QuotePaddingProperty, value); }
    public Thickness QuoteMargin { get => GetValue(QuoteMarginProperty); set => SetValue(QuoteMarginProperty, value); }
    public double AvatarDiameter { get => GetValue(AvatarDiameterProperty); set => SetValue(AvatarDiameterProperty, value); }
    public double BylineGap { get => GetValue(BylineGapProperty); set => SetValue(BylineGapProperty, value); }
    public double BodyPaddingTop { get => GetValue(BodyPaddingTopProperty); set => SetValue(BodyPaddingTopProperty, value); }
    public double BodyPaddingBottom { get => GetValue(BodyPaddingBottomProperty); set => SetValue(BodyPaddingBottomProperty, value); }

    private readonly MatrixTransform _lift = new();
    private readonly MatrixTransform _bodyTransform = new();
    private readonly MatrixTransform _closeTransform = new();
    private readonly ScaleTransform _progressScale = new(0, 1);
    private readonly CardToArticlePullGesture _gesture = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private MotionValue<double>? _pull;
    private DispatcherTimer? _wheelIdle;
    private DispatcherTimer? _scrollBarIdle;
    private ScrollBar? _scrollBar;
    private IPointer? _pointer;
    private double _bodyReveal = 1;
    private double _bodyTravel;
    private double _closeReveal = 1;
    private double _closeScale = 1;
    private bool _bodyDirty = true;
    private Size _viewport;

    public CardToArticleArticle()
    {
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        RenderTransform = _lift;
        AddHandler(KeyDownEvent, OnArticleKeyDown);
    }

    /// <summary>Asks the owner to close (close button, a pull past the threshold, a wheel pull); the velocity is the release speed in px/s.</summary>
    public event Action<CardToArticleChangeReason, double>? CloseRequested;

    public SharedElementFrame? Frame { get; private set; }
    public ScrollViewer? Scroller { get; private set; }
    public CardToArticleHero? Hero { get; private set; }
    public Border? Body { get; private set; }
    public Border? Progress { get; private set; }
    public CardToArticleCloseButton? CloseButton { get; private set; }

    /// <summary>The current pull distance (the lift).</summary>
    public double PullDistance => _pull?.Value ?? 0;

    internal CardToArticlePullGesture Gesture => _gesture;

    /// <summary>The progress bar's scale (scroll offset / scrollable height).</summary>
    public double ReadingProgress => _progressScale.ScaleX;

    protected override Type StyleKeyOverride => typeof(CardToArticleArticle);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (Frame is { } oldFrame)
        {
            oldFrame.RemoveHandler(PointerPressedEvent, OnFramePressed);
            oldFrame.RemoveHandler(PointerMovedEvent, OnFrameMoved);
            oldFrame.RemoveHandler(PointerReleasedEvent, OnFrameReleased);
            oldFrame.RemoveHandler(PointerCaptureLostEvent, OnFrameCaptureLost);
            oldFrame.RemoveHandler(PointerWheelChangedEvent, OnFrameWheel);
        }
        if (Scroller is { } oldScroller)
        {
            oldScroller.ScrollChanged -= OnScrollChanged;
            oldScroller.TemplateApplied -= OnScrollerTemplateApplied;
        }
        if (CloseButton is { } oldClose)
        {
            oldClose.Click -= OnCloseClick;
        }
        Frame = e.NameScope.Find<SharedElementFrame>(FramePart);
        Scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        Hero = e.NameScope.Find<CardToArticleHero>(HeroPart);
        Body = e.NameScope.Find<Border>(BodyPart);
        Progress = e.NameScope.Find<Border>(ProgressPart);
        CloseButton = e.NameScope.Find<CardToArticleCloseButton>(ClosePart);
        if (Frame is { } frame)
        {
            const RoutingStrategies tunnel = RoutingStrategies.Tunnel;
            frame.AddHandler(PointerPressedEvent, OnFramePressed, tunnel, handledEventsToo: true);
            frame.AddHandler(PointerMovedEvent, OnFrameMoved, tunnel, handledEventsToo: true);
            frame.AddHandler(PointerReleasedEvent, OnFrameReleased, tunnel, handledEventsToo: true);
            frame.AddHandler(PointerCaptureLostEvent, OnFrameCaptureLost, RoutingStrategies.Direct | RoutingStrategies.Bubble, handledEventsToo: true);
            frame.AddHandler(PointerWheelChangedEvent, OnFrameWheel, tunnel, handledEventsToo: true);
        }
        if (Scroller is { } scroller)
        {
            scroller.ScrollChanged += OnScrollChanged;
            scroller.TemplateApplied += OnScrollerTemplateApplied;
        }
        if (Body is { } body)
        {
            body.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            body.RenderTransform = _bodyTransform;
        }
        if (CloseButton is { } close)
        {
            close.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            close.RenderTransform = _closeTransform;
            close.Click += OnCloseClick;
        }
        if (Progress is { } progress)
        {
            progress.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
            progress.RenderTransform = _progressScale;
        }
        _bodyDirty = true;
        ApplyReveal();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty || change.Property == BodyTemplateProperty)
        {
            _bodyDirty = true;
            InvalidateMeasure();
            AutomationProperties.SetName(this, Item?.Title ?? "");
        }
        else if (change.Property == BoundsProperty)
        {
            ApplyLift();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width) && double.IsFinite(availableSize.Height))
        {
            _viewport = availableSize;
        }
        if (Body is { } body)
        {
            var x = CardToArticleLayout.BodyPaddingX(_viewport.Width);
            var padding = new Thickness(x, BodyPaddingTop, x, BodyPaddingBottom);
            if (body.Padding != padding)
            {
                body.Padding = padding;
            }
            if (_bodyDirty)
            {
                _bodyDirty = false;
                body.Child = BuildBody();
            }
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _pull ??= CreatePull();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _wheelIdle?.Stop();
        _scrollBarIdle?.Stop();
        _pointer = null;
    }

    private MotionValue<double> CreatePull()
    {
        var pull = new MotionValue<double>(this, 0, MotionVectors.Double);
        pull.Changed += _ => ApplyLift();
        return pull;
    }

    #region Reset, reveal, flight correction

    /// <summary>Back to the top, no pull, no progress: called before the article opens.</summary>
    public void ResetForOpen()
    {
        _gesture.Reset();
        _pull?.Jump(0);
        _wheelIdle?.Stop();
        Scroller?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
        _progressScale.ScaleX = 0;
        _scrollBarIdle?.Stop();
        if (_scrollBar is { } bar)
        {
            bar.Opacity = 0;
        }
        ApplyLift();
    }

    /// <summary>The lift springs back to rest with <paramref name="spec"/> (a close lands while the sheet flies home).</summary>
    public Task<bool> SettlePull(MotionSpec spec)
    {
        _gesture.Settle();
        _pull ??= CreatePull();
        return _pull.AnimateTo(0, spec);
    }

    /// <summary>The text's entrance: opacity <paramref name="p"/>, rising from 16px below when <paramref name="travel"/>.</summary>
    public void SetBodyReveal(double p, bool travel)
    {
        _bodyReveal = Math.Clamp(p, 0, 1);
        _bodyTravel = travel ? 16 * (1 - p) : 0;
        ApplyReveal();
    }

    /// <summary>The close button's entrance: opacity <paramref name="p"/>, growing from 0.8 when <paramref name="pop"/>.</summary>
    public void SetCloseReveal(double p, bool pop)
    {
        _closeReveal = Math.Clamp(p, 0, 1);
        _closeScale = pop ? 0.8 + 0.2 * p : 1;
        ApplyReveal();
    }

    private void ApplyReveal()
    {
        if (Body is { } body)
        {
            body.Opacity = _bodyReveal;
        }
        if (CloseButton is { } close)
        {
            close.Opacity = _closeReveal;
            close.IsHitTestVisible = _closeReveal > 0.01;
        }
        UpdateFlightCorrection();
    }

    /// <summary>
    /// Counter-scales the text and the close button against the sheet's current flight scale, keeps the text on the
    /// hero's bottom edge and the button 14px inside the sheet's visible top-right corner.
    /// </summary>
    public void UpdateFlightCorrection()
    {
        var frameMatrix = Frame?.RenderTransform?.Value ?? Matrix.Identity;
        var sx = Math.Max(Math.Abs(frameMatrix.M11), 1e-4);
        var sy = Math.Max(Math.Abs(frameMatrix.M22), 1e-4);
        if (Body is { } body && Hero is { } hero)
        {
            var heroMatrix = hero.RenderTransform?.Value ?? Matrix.Identity;
            var heroBottom = heroMatrix.Transform(new Point(0, hero.Bounds.Height)).Y + hero.Bounds.Y;
            var shift = heroBottom - body.Bounds.Y + _bodyTravel / sy;
            _bodyTransform.Matrix = Matrix.CreateScale(1 / sx, 1 / sy) * Matrix.CreateTranslation(0, shift);
        }
        if (CloseButton is { } close && Frame is { } frame)
        {
            var width = frame.Bounds.Width;
            var natural = close.Bounds;
            var c = natural.Width / 2;
            var right = width - natural.Right;
            var desired = new Point(width - (right + natural.Width) / sx, natural.Y / sy);
            _closeTransform.Matrix = Matrix.CreateTranslation(-c, -c) * Matrix.CreateScale(_closeScale, _closeScale) *
                                     Matrix.CreateTranslation(c, c) * Matrix.CreateScale(1 / sx, 1 / sy) *
                                     Matrix.CreateTranslation(desired.X - natural.X, desired.Y - natural.Y);
        }
        Body?.InvalidateVisual();
        CloseButton?.InvalidateVisual();
    }

    private void ApplyLift()
    {
        var size = Bounds.Size;
        var origin = new Point(size.Width * CardToArticlePullGesture.Origin.Point.X, size.Height * CardToArticlePullGesture.Origin.Point.Y);
        _lift.Matrix = CardToArticlePullGesture.LiftMatrix(_pull?.Value ?? 0, origin);
    }

    #endregion

    #region Body

    private Control? BuildBody()
    {
        if (Item is not { } item)
        {
            return null;
        }
        var stack = new StackPanel { Spacing = ArticleGap };
        if (!string.IsNullOrWhiteSpace(item.Author))
        {
            stack.Children.Add(BuildByline(item));
        }
        switch (item.Body)
        {
            case null:
                break;
            case string text:
                foreach (var block in SplitProse(text))
                {
                    stack.Children.Add(block.IsQuote ? BuildQuote(block.Text) : BuildParagraph(block.Text));
                }
                break;
            case Control control when BodyTemplate is null:
                if (control.Parent is Panel oldParent)
                {
                    oldParent.Children.Remove(control);
                }
                else if (control.Parent is Decorator oldDecorator)
                {
                    oldDecorator.Child = null;
                }
                stack.Children.Add(control);
                break;
            default:
            {
                // Content first, then the template (a presenter must never build a template with null).
                var presenter = new ContentPresenter { Content = item.Body };
                if (BodyTemplate is { } template)
                {
                    presenter.ContentTemplate = template;
                }
                ArcTokens.Bind(presenter, ContentPresenter.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                stack.Children.Add(presenter);
                break;
            }
        }
        return stack;
    }

    /// <summary>A paragraph or a pull quote of string prose.</summary>
    internal readonly record struct ProseBlock(string Text, bool IsQuote);

    /// <summary>Splits prose at blank lines; a paragraph starting with "&gt; " is a pull quote.</summary>
    internal static List<ProseBlock> SplitProse(string text)
    {
        var blocks = new List<ProseBlock>();
        var normalized = text.Replace("\r\n", "\n");
        foreach (var raw in normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var paragraph = string.Join(' ', raw.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            if (paragraph.Length == 0)
            {
                continue;
            }
            if (paragraph.StartsWith("> ", StringComparison.Ordinal) || paragraph == ">")
            {
                blocks.Add(new ProseBlock(paragraph.TrimStart('>').Trim(), true));
            }
            else
            {
                blocks.Add(new ProseBlock(paragraph, false));
            }
        }
        return blocks;
    }

    private Control BuildParagraph(string text)
    {
        var block = new SelectableTextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = ParagraphLineHeight
        };
        KeyboardNavigation.SetIsTabStop(block, false);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeBase);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        return block;
    }

    private Control BuildQuote(string text)
    {
        var block = new SelectableTextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = QuoteLineHeight
        };
        KeyboardNavigation.SetIsTabStop(block, false);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXL);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var border = new Border
        {
            BorderThickness = QuoteRuleThickness,
            Padding = QuotePadding,
            Margin = QuoteMargin,
            Child = block
        };
        ArcTokens.Bind(border, Border.BorderBrushProperty, ArcDesignTokenKind.Accent);
        return border;
    }

    private Control BuildByline(CardToArticleItem item)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = BylineGap };
        row.Children.Add(BuildAvatar(item));
        var author = new TextBlock { Text = item.Author, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Medium };
        ArcTokens.Bind(author, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(author, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(author, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        row.Children.Add(author);
        if (!string.IsNullOrWhiteSpace(item.Published))
        {
            var date = new TextBlock { Text = item.Published, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(date, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
            ArcTokens.Bind(date, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
            ArcTokens.Bind(date, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            row.Children.Add(date);
        }
        return row;
    }

    private Control BuildAvatar(CardToArticleItem item)
    {
        var size = AvatarDiameter;
        var circle = new Border
        {
            Width = size,
            Height = size,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        if (item.Avatar is { } avatar)
        {
            circle.Child = new Image { Source = avatar, Stretch = Stretch.UniformToFill };
            return circle;
        }
        ArcTokens.Bind(circle, Border.BackgroundProperty, SeriesFor(item.Author ?? ""));
        var initials = new TextBlock
        {
            Text = Initials(item.Author ?? ""),
            FontSize = Math.Round(size * 0.4),
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(initials, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(initials, TextBlock.ForegroundProperty, ArcDesignTokenKind.Background);
        circle.Child = initials;
        return circle;
    }

    /// <summary>Up to two initials: the first letters of the first and last words.</summary>
    internal static string Initials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return "";
        }
        var first = char.ToUpperInvariant(words[0][0]).ToString();
        return words.Length == 1 ? first : first + char.ToUpperInvariant(words[^1][0]);
    }

    /// <summary>One of the four series colors, chosen by a stable hash of the name.</summary>
    internal static ArcDesignTokenKind SeriesFor(string name)
    {
        var hash = 0u;
        foreach (var ch in name)
        {
            hash = hash * 31 + ch;
        }
        return (hash % 4) switch
        {
            0 => ArcDesignTokenKind.Series1,
            1 => ArcDesignTokenKind.Series2,
            2 => ArcDesignTokenKind.Series3,
            _ => ArcDesignTokenKind.Series4
        };
    }

    #endregion

    #region Scroll, keyboard, close

    private bool AtTop => Scroller is null || Scroller.Offset.Y <= 0.5;

    private void OnScrollerTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _scrollBar = e.NameScope.Find<ScrollBar>("PART_VerticalScrollBar");
        if (_scrollBar is { } bar)
        {
            bar.Opacity = 0;
        }
    }

    /// <summary>
    /// The scroll bar floats over the text and shows only while the article scrolls (a browser's overlay scroll bar):
    /// it appears with each scroll and hides 900ms after the last one.
    /// </summary>
    private void ShowScrollBarBriefly()
    {
        if (_scrollBar is not { } bar)
        {
            return;
        }
        bar.Opacity = 1;
        _scrollBarIdle ??= CreateScrollBarIdle();
        _scrollBarIdle.Stop();
        _scrollBarIdle.Start();
    }

    private DispatcherTimer CreateScrollBarIdle()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_scrollBar is { IsPointerOver: false } bar)
            {
                bar.Opacity = 0;
            }
            else if (_scrollBar is not null)
            {
                timer.Start();
            }
        };
        return timer;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (Scroller is not { } scroller)
        {
            return;
        }
        if (e.OffsetDelta.Y != 0)
        {
            ShowScrollBarBriefly();
        }
        var max = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        // Linear and unsmoothed, like Arc: the bar is the scroll position.
        _progressScale.ScaleX = max > 0.5 ? Math.Clamp(scroller.Offset.Y / max, 0, 1) : 0;
    }

    private void OnArticleKeyDown(object? sender, KeyEventArgs e)
    {
        if (Scroller is not { } scroller || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        // Focus stays trapped on the close button; the arrows and page keys still read the article.
        var page = Math.Max(40, scroller.Viewport.Height * 0.9);
        double? delta = e.Key switch
        {
            Key.Down => 40,
            Key.Up => -40,
            Key.PageDown => page,
            Key.PageUp => -page,
            Key.End => double.PositiveInfinity,
            Key.Home => double.NegativeInfinity,
            _ => null
        };
        if (delta is not { } d)
        {
            return;
        }
        var max = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        var y = Math.Clamp(double.IsInfinity(d) ? (d > 0 ? max : 0) : scroller.Offset.Y + d, 0, max);
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(scroller.Offset.X, y));
        e.Handled = true;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(CardToArticleChangeReason.CloseButton, 0);

    #endregion

    #region Pull to dismiss

    private static TimeSpan Time(PointerEventArgs e) => TimeSpan.FromMilliseconds(e.Timestamp);

    private bool IsOnHero(PointerEventArgs e)
    {
        if (Hero is not { } hero || Scroller is not { } scroller)
        {
            return false;
        }
        // The close button sits over the picture and keeps its own presses.
        if (CloseButton is { } close && e.Source is Visual source && (ReferenceEquals(source, close) || close.IsVisualAncestorOf(source)))
        {
            return false;
        }
        // Decided by geometry rather than by the hit element: the part of the picture still visible in the scroller.
        var onHero = new Rect(hero.Bounds.Size).Contains(e.GetPosition(hero));
        var inView = new Rect(scroller.Bounds.Size).Contains(e.GetPosition(scroller));
        return onHero && inView;
    }

    private void OnFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pointer is not null || Frame is not { } frame)
        {
            return;
        }
        var y = e.GetPosition(null).Y;
        if (e.Pointer.Type == PointerType.Touch)
        {
            _pointer = e.Pointer;
            _gesture.PressTouch(y, AtTop, Time(e));
            return;
        }
        if (!e.GetCurrentPoint(frame).Properties.IsLeftButtonPressed || !IsOnHero(e))
        {
            return;
        }
        _pointer = e.Pointer;
        var current = _pull?.Value ?? 0;
        _pull?.Jump(current);
        _gesture.PressMouse(y, Time(e), current);
        e.Pointer.Capture(frame);
        Hero?.SetValue(CursorProperty, new Cursor(StandardCursorType.SizeNorthSouth));
        e.Handled = true;
    }

    private void OnFrameMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        var result = _gesture.Move(e.GetPosition(null).Y, AtTop, Time(e));
        if (result != CardToArticlePullResult.Follow)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Touch && Frame is { } frame && !ReferenceEquals(e.Pointer.Captured, frame))
        {
            e.Pointer.Capture(frame);
        }
        _pull?.Jump(_gesture.Distance);
        e.Handled = true;
    }

    private void OnFrameReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        _pointer = null;
        var result = _gesture.Release(Time(e));
        if (ReferenceEquals(e.Pointer.Captured, Frame))
        {
            e.Pointer.Capture(null);
        }
        Hero?.ClearValue(CursorProperty);
        Finish(result, CardToArticleChangeReason.Drag);
        if (result != CardToArticlePullResult.None)
        {
            e.Handled = true;
        }
    }

    private void OnFrameCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        _pointer = null;
        Hero?.ClearValue(CursorProperty);
        Finish(_gesture.Cancel(), CardToArticleChangeReason.Drag);
    }

    private void OnFrameWheel(object? sender, PointerWheelEventArgs e)
    {
        // One wheel notch is 50px of scrolling in Avalonia (a browser reports the pixels).
        var up = e.Delta.Y * 50;
        var result = _gesture.Wheel(up, AtTop, _clock.Elapsed);
        if (result == CardToArticlePullResult.None)
        {
            return;
        }
        e.Handled = true;
        if (result == CardToArticlePullResult.Follow && !_gesture.IsPulling)
        {
            // Already closing: later wheel events are swallowed.
            return;
        }
        if (result == CardToArticlePullResult.Follow)
        {
            _pull?.Jump(_gesture.Distance);
            _wheelIdle ??= CreateWheelIdle();
            _wheelIdle.Stop();
            _wheelIdle.Start();
            return;
        }
        _wheelIdle?.Stop();
        Finish(result, CardToArticleChangeReason.Wheel);
    }

    private DispatcherTimer CreateWheelIdle()
    {
        var timer = new DispatcherTimer { Interval = CardToArticlePullGesture.WheelIdle };
        timer.Tick += (_, _) =>
        {
            if (_gesture.WheelIdleElapsed(_clock.Elapsed + TimeSpan.FromMilliseconds(1)))
            {
                timer.Stop();
                Finish(CardToArticlePullResult.SnapBack, CardToArticleChangeReason.Wheel);
            }
        };
        return timer;
    }

    /// <summary>Checks the wheel idle now (tests: timers only fire while a frame is pushed).</summary>
    internal void CheckWheelIdle(TimeSpan now)
    {
        if (_gesture.WheelIdleElapsed(now))
        {
            _wheelIdle?.Stop();
            Finish(CardToArticlePullResult.SnapBack, CardToArticleChangeReason.Wheel);
        }
    }

    internal TimeSpan ClockNow => _clock.Elapsed;

    private void Finish(CardToArticlePullResult result, CardToArticleChangeReason reason)
    {
        switch (result)
        {
            case CardToArticlePullResult.Dismiss:
                CloseRequested?.Invoke(reason, _gesture.ReleaseVelocity);
                break;
            case CardToArticlePullResult.SnapBack:
                _ = SettlePull(MotionSpec.Spring(SnapBackSpring));
                break;
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ArticlePeer(this);

    /// <summary>A modal dialog named by the story's title (Arc: role=dialog, aria-modal, labelled by the hero's heading).</summary>
    private sealed class ArticlePeer : ControlAutomationPeer
    {
        public ArticlePeer(CardToArticleArticle owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;

        protected override string GetClassNameCore() => "CardToArticleArticle";
    }
}

/// <summary>The article's close button: a 36px frosted circle with the Lucide x; no hover or press change (Arc).</summary>
internal sealed class CardToArticleCloseButton : Avalonia.Controls.Button
{
    protected override Type StyleKeyOverride => typeof(CardToArticleCloseButton);
}

/// <summary>Places the article sheet in the window: at most 680 wide, inset 16 / 24 (8 / 12 in a window at most 520 wide).</summary>
internal sealed class CardToArticleSheetPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
        var sheet = CardToArticleLayout.Sheet(viewport);
        foreach (var child in Children)
        {
            child.Measure(sheet.Size);
        }
        return viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var sheet = CardToArticleLayout.Sheet(finalSize);
        foreach (var child in Children)
        {
            child.Arrange(sheet);
        }
        return finalSize;
    }
}
