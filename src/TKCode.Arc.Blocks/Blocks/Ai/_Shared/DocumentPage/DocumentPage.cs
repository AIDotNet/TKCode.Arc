using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A document drawn as a sheet of white paper: a Pdf document is an A4 page (<see cref="BaseWidth"/> × <see cref="PdfHeight"/>)
/// with its footer pinned to the bottom, an Email document is as wide with the height of its content. The paper and its ink
/// are fixed (white in the dark theme too, spec 3.3); the viewer scales the page with a render transform, so every size here
/// is in unscaled base units. The text stays real text; the spans a value can cite are character ranges of it that the owner
/// tints with <see cref="SetSpan"/> and measures with <see cref="TryGetSpanRects"/>.
/// </summary>
/// <remarks>
/// Spans are not focusable (the form is the keyboard path). Pointer events are raised only for painted spans: a span
/// <see cref="SetSpan"/> never painted is plain text. The page is an article for assistive technology, named by
/// <see cref="ArticleName"/>; its text controls read the document's text.
/// </remarks>
[TemplatePart(ContentPart, typeof(Decorator))]
internal sealed partial class DocumentPage : TemplatedControl
{
    /// <summary>Name of the template part the document is built into.</summary>
    public const string ContentPart = "PART_Content";

    /// <summary>The width of a page in base units (A4 at 540).</summary>
    public const double BaseWidth = 540;

    /// <summary>The height of a Pdf page in base units (the A4 ratio of <see cref="BaseWidth"/>).</summary>
    public const double PdfHeight = 764;

    /// <summary>The document to draw; changing it builds the page again and drops every span's paint.</summary>
    public static readonly StyledProperty<ExtractionDocument?> DocumentProperty =
        AvaloniaProperty.Register<DocumentPage, ExtractionDocument?>(nameof(Document));

    /// <summary>The accessible name of the page (the owner passes the file name).</summary>
    public static readonly StyledProperty<string?> ArticleNameProperty =
        AvaloniaProperty.Register<DocumentPage, string?>(nameof(ArticleName));

    /// <summary>The painted span under the pointer, if any (read-only).</summary>
    public static readonly DirectProperty<DocumentPage, string?> HoveredSpanIdProperty =
        AvaloniaProperty.RegisterDirect<DocumentPage, string?>(nameof(HoveredSpanId), o => o.HoveredSpanId);

    /// <summary>Raised when the pointer enters a painted span.</summary>
    public static readonly RoutedEvent<DocumentSpanEventArgs> SpanPointerEnteredEvent =
        RoutedEvent.Register<DocumentPage, DocumentSpanEventArgs>(nameof(SpanPointerEntered), RoutingStrategies.Bubble);

    /// <summary>Raised when the pointer leaves a painted span (or the span stops being painted under it).</summary>
    public static readonly RoutedEvent<DocumentSpanEventArgs> SpanPointerExitedEvent =
        RoutedEvent.Register<DocumentPage, DocumentSpanEventArgs>(nameof(SpanPointerExited), RoutingStrategies.Bubble);

    /// <summary>Raised when a painted span is pressed with the primary button (or tapped).</summary>
    public static readonly RoutedEvent<DocumentSpanEventArgs> SpanPressedEvent =
        RoutedEvent.Register<DocumentPage, DocumentSpanEventArgs>(nameof(SpanPressed), RoutingStrategies.Bubble);

    private static Cursor? s_hand;

    private Decorator? _content;
    private string? _hoveredSpanId;
    private bool _isPdf = true;
    private double _height = PdfHeight;

    static DocumentPage()
    {
        FocusableProperty.OverrideDefaultValue<DocumentPage>(false);
    }

    /// <summary>Creates an empty page.</summary>
    public DocumentPage()
    {
        ActualThemeVariantChanged += (_, _) => RefreshInk();
        ResourcesChanged += (_, _) => RefreshInk();
    }

    protected override Type StyleKeyOverride => typeof(DocumentPage);

    /// <inheritdoc cref="DocumentProperty"/>
    public ExtractionDocument? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

    /// <inheritdoc cref="ArticleNameProperty"/>
    public string? ArticleName { get => GetValue(ArticleNameProperty); set => SetValue(ArticleNameProperty, value); }

    /// <inheritdoc cref="HoveredSpanIdProperty"/>
    public string? HoveredSpanId { get => _hoveredSpanId; private set => SetAndRaise(HoveredSpanIdProperty, ref _hoveredSpanId, value); }

    /// <inheritdoc cref="SpanPointerEnteredEvent"/>
    public event EventHandler<DocumentSpanEventArgs>? SpanPointerEntered { add => AddHandler(SpanPointerEnteredEvent, value); remove => RemoveHandler(SpanPointerEnteredEvent, value); }

    /// <inheritdoc cref="SpanPointerExitedEvent"/>
    public event EventHandler<DocumentSpanEventArgs>? SpanPointerExited { add => AddHandler(SpanPointerExitedEvent, value); remove => RemoveHandler(SpanPointerExitedEvent, value); }

    /// <inheritdoc cref="SpanPressedEvent"/>
    public event EventHandler<DocumentSpanEventArgs>? SpanPressed { add => AddHandler(SpanPressedEvent, value); remove => RemoveHandler(SpanPressedEvent, value); }

    /// <summary>Whether the page is drawn as a fixed-height paper page (a Pdf document or no document).</summary>
    internal bool IsPdf => _isPdf;

    /// <summary>The built content (tests).</summary>
    internal Control? PageContent => _content?.Child;

    #region Building

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_content is not null)
        {
            _content.Child = null;
        }
        _content = e.NameScope.Find<Decorator>(ContentPart);
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
        {
            Rebuild();
        }
        else if (change.Property == ArticleNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    /// <summary>Builds the page for the current document: the spans of the old one are dropped (no fade: the text is new).</summary>
    private void Rebuild()
    {
        SetHover(null);
        ResetSpans();
        var document = Document;
        _isPdf = document is null || document.EffectiveKind != DocumentKind.Email;
        if (_content is null)
        {
            InvalidateMeasure();
            return;
        }
        _content.Child = document is null ? null : new DocumentPageBuilder(this, document).Build();
        InvalidateMeasure();
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        // The page has its own size whatever it is offered: the viewer scales it and scrolls it.
        var desired = base.MeasureOverride(new Size(BaseWidth, _isPdf ? PdfHeight : double.PositiveInfinity));
        _height = _isPdf ? PdfHeight : Math.Ceiling(desired.Height);
        return new Size(BaseWidth, _height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = new Size(BaseWidth, _height);
        base.ArrangeOverride(size);
        return size;
    }

    #endregion

    #region Pointer

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Touch has no hover (Arc's hover rules are behind (hover: hover) and (pointer: fine)).
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        SetHover(FindSpan(e.GetPosition(this), paintedOnly: true)?.Id);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (FindSpan(e.GetPosition(this), paintedOnly: true) is { } span)
        {
            var args = new DocumentSpanEventArgs(SpanPressedEvent, span.Id);
            RaiseEvent(args);
            e.Handled |= args.Handled;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SetHover(null);
        SettleSpans();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshInk();
    }

    /// <summary>Moves the hover to <paramref name="id"/>: the old span reports an exit, the new one an entry; the cursor is a hand over a span.</summary>
    private void SetHover(string? id)
    {
        var old = _hoveredSpanId;
        if (string.Equals(old, id, StringComparison.Ordinal))
        {
            return;
        }
        HoveredSpanId = id;
        if (id is null)
        {
            ClearValue(CursorProperty);
        }
        else
        {
            SetCurrentValue(CursorProperty, s_hand ??= new Cursor(StandardCursorType.Hand));
        }
        if (old is not null)
        {
            RaiseEvent(new DocumentSpanEventArgs(SpanPointerExitedEvent, old));
        }
        if (id is not null)
        {
            RaiseEvent(new DocumentSpanEventArgs(SpanPointerEnteredEvent, id));
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new DocumentPageAutomationPeer(this);

    /// <summary>The page as an article: a document named by <see cref="ArticleName"/> whose children are its texts.</summary>
    private sealed class DocumentPageAutomationPeer(DocumentPage owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override string? GetNameCore() => ((DocumentPage)Owner).ArticleName ?? base.GetNameCore();

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
