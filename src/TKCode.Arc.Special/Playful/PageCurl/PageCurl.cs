using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Data of <see cref="PageCurl.SpreadChangedEvent"/>.</summary>
public sealed class PageCurlSpreadChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PageCurlSpreadChangedEventArgs(RoutedEvent routedEvent, int spread)
        : base(routedEvent)
    {
        Spread = spread;
    }

    /// <summary>The spread the book now shows.</summary>
    public int Spread { get; }
}

/// <summary>
/// A small book or magazine that opens on its cover (Arc Page curl, design reference). Tap the right page to go forward
/// and the left page to go back, or drag a page: it folds along a line that follows the pointer, showing its back, a
/// lit curl and a shadow on the page below. A page count with previous and next buttons sits underneath.
/// </summary>
/// <remarks>
/// Page one is the cover and sits alone on the right; spread <c>n</c> shows pages <c>2n</c> and <c>2n + 1</c>
/// (one-based), and a book with an even number of pages ends on a back cover alone on the left. Each page renders into a
/// slot of the same width and aspect ratio, so keep pages simple and fixed in size. Page size follows the control's
/// width: the width holds two pages side by side. Under reduced motion spreads swap without the curl and the tease is
/// skipped.
/// </remarks>
[TemplatePart(SurfacePart, typeof(PageCurlSurface))]
[TemplatePart(PreviousButtonPart, typeof(Button))]
[TemplatePart(NextButtonPart, typeof(Button))]
[TemplatePart(CountPart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(TextBlock))]
public class PageCurl : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string PreviousButtonPart = "PART_PreviousButton";
    public const string NextButtonPart = "PART_NextButton";
    public const string CountPart = "PART_Count";
    public const string StatusPart = "PART_Status";

    /// <summary>The spread on show: 0 is the closed cover, spread <c>n</c> shows pages <c>2n</c> and <c>2n + 1</c>. Two-way.</summary>
    public static readonly StyledProperty<int> SpreadProperty =
        AvaloniaProperty.Register<PageCurl, int>(nameof(Spread), 0, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceSpread);

    /// <summary>The template that builds a page from an item that is not a control.</summary>
    public static readonly StyledProperty<IDataTemplate?> PageTemplateProperty =
        AvaloniaProperty.Register<PageCurl, IDataTemplate?>(nameof(PageTemplate));

    /// <summary>A page's width divided by its height.</summary>
    public static readonly StyledProperty<double> PageAspectProperty =
        AvaloniaProperty.Register<PageCurl, double>(nameof(PageAspect), 0.72);

    /// <summary>The accessible name of the book.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PageCurl, string>(nameof(Label), "Book");

    /// <summary>Whether the cover corner curls once when the book first comes into view, and a little on hover until the first turn.</summary>
    public static readonly StyledProperty<bool> TeaseProperty =
        AvaloniaProperty.Register<PageCurl, bool>(nameof(Tease), true);

    /// <summary>The hint under the lower outer corner, shown until the first interaction. Null hides it.</summary>
    public static readonly StyledProperty<object?> HintProperty =
        AvaloniaProperty.Register<PageCurl, object?>(nameof(Hint), "Drag the corner");

    /// <summary>Whether the page count with previous and next buttons shows under the book.</summary>
    public static readonly StyledProperty<bool> ShowControlsProperty =
        AvaloniaProperty.Register<PageCurl, bool>(nameof(ShowControls), true);

    /// <summary>The number of spreads: the closed cover plus one per two pages.</summary>
    public static readonly DirectProperty<PageCurl, int> SpreadCountProperty =
        AvaloniaProperty.RegisterDirect<PageCurl, int>(nameof(SpreadCount), o => o.SpreadCount);

    /// <summary>Raised when a page finishes turning, by a drag, a tap, a key or a button.</summary>
    public static readonly RoutedEvent<PageCurlSpreadChangedEventArgs> SpreadChangedEvent =
        RoutedEvent.Register<PageCurl, PageCurlSpreadChangedEventArgs>(nameof(SpreadChanged), RoutingStrategies.Bubble);

    private readonly AvaloniaList<object> _pages = new();
    private PageCurlSurface? _surface;
    private Button? _previous;
    private Button? _next;
    private TextBlock? _count;
    private Run? _countSpread;
    private Run? _countTotal;
    private TextBlock? _status;

    static PageCurl()
    {
        FocusableProperty.OverrideDefaultValue<PageCurl>(true);
        AffectsMeasure<PageCurl>(PageAspectProperty);
    }

    /// <summary>Creates an empty book.</summary>
    public PageCurl()
    {
        _pages.CollectionChanged += OnPagesChanged;
    }

    /// <summary>
    /// The pages in reading order. A control is used as the page; any other item is built with
    /// <see cref="PageTemplate"/>. Page one is the cover.
    /// </summary>
    [Content]
    public IList<object> Pages => _pages;

    /// <inheritdoc cref="SpreadProperty"/>
    public int Spread
    {
        get => GetValue(SpreadProperty);
        set => SetValue(SpreadProperty, value);
    }

    /// <inheritdoc cref="PageTemplateProperty"/>
    public IDataTemplate? PageTemplate
    {
        get => GetValue(PageTemplateProperty);
        set => SetValue(PageTemplateProperty, value);
    }

    /// <inheritdoc cref="PageAspectProperty"/>
    public double PageAspect
    {
        get => GetValue(PageAspectProperty);
        set => SetValue(PageAspectProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="TeaseProperty"/>
    public bool Tease
    {
        get => GetValue(TeaseProperty);
        set => SetValue(TeaseProperty, value);
    }

    /// <inheritdoc cref="HintProperty"/>
    public object? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <inheritdoc cref="ShowControlsProperty"/>
    public bool ShowControls
    {
        get => GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    /// <inheritdoc cref="SpreadCountProperty"/>
    public int SpreadCount => PageCurlGeometry.SpreadCount(_pages.Count);

    /// <summary>The number of pages.</summary>
    public int PageCount => _pages.Count;

    /// <inheritdoc cref="SpreadChangedEvent"/>
    public event EventHandler<PageCurlSpreadChangedEventArgs>? SpreadChanged
    {
        add => AddHandler(SpreadChangedEvent, value);
        remove => RemoveHandler(SpreadChangedEvent, value);
    }

    internal PageCurlSurface? Surface => _surface;

    internal object? PageAt(int pageNumber) => pageNumber >= 1 && pageNumber <= _pages.Count ? _pages[pageNumber - 1] : null;

    /// <summary>Turns to the next spread, along a lifted arc. Returns false on the back cover.</summary>
    public bool NextSpread() => Spread < SpreadCount - 1 && Turn(1);

    /// <summary>Turns to the previous spread, along a lifted arc. Returns false on the cover.</summary>
    public bool PreviousSpread() => Spread > 0 && Turn(-1);

    private bool Turn(int direction)
    {
        if (_surface is not null)
        {
            return _surface.TurnBy(direction);
        }
        var target = Spread + direction;
        SetCurrentValue(SpreadProperty, target);
        RaiseSpreadChanged();
        return true;
    }

    /// <summary>Called by the surface when a turn has finished: stores the spread, raises the event, updates the readouts.</summary>
    internal void CommitSpread(int spread, bool raise)
    {
        if (Spread != spread)
        {
            SetCurrentValue(SpreadProperty, spread);
        }
        RefreshReadouts();
        if (raise)
        {
            RaiseSpreadChanged();
        }
    }

    private void RaiseSpreadChanged() => RaiseEvent(new PageCurlSpreadChangedEventArgs(SpreadChangedEvent, Spread));

    /// <summary>The visible page numbers of the spread on show, such as "Page 2 and 3 of 8".</summary>
    internal string StatusText()
    {
        var shown = _surface?.ShownSpread ?? Spread;
        var left = PageCurlGeometry.LeftPage(shown, PageCount);
        var right = PageCurlGeometry.RightPage(shown, PageCount);
        if (PageCount == 0)
        {
            return "No pages";
        }
        return (left, right) switch
        {
            (0, var r) => $"Page {r} of {PageCount}",
            (var l, 0) => $"Page {l} of {PageCount}",
            _ => $"Page {left} and {right} of {PageCount}"
        };
    }

    /// <summary>The visible count: the spread in the text color, then the total in the muted one ("Pages 2–3" "of 8").</summary>
    private (string Spread, string Total) CountText()
    {
        var shown = _surface?.ShownSpread ?? Spread;
        var left = PageCurlGeometry.LeftPage(shown, PageCount);
        var right = PageCurlGeometry.RightPage(shown, PageCount);
        if (PageCount == 0)
        {
            return (string.Empty, string.Empty);
        }
        if (shown == 0)
        {
            return ("Cover", $"{PageCount} pages");
        }
        return (right == 0 ? $"Page {left}" : $"Pages {left}–{right}", $"of {PageCount}");
    }

    private void UpdateCount(TextBlock count)
    {
        var (spread, total) = CountText();
        if (_countSpread is null || _countTotal is null)
        {
            _countSpread = new Run();
            ArcTokens.Bind(_countSpread, TextElement.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Foreground);
            _countTotal = new Run();
            ArcTokens.Bind(_countTotal, TextElement.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        }
        _countSpread.Text = spread;
        _countTotal.Text = total.Length == 0 ? string.Empty : "  " + total;
        if (count.Inlines is not { Count: 2 } inlines || inlines[0] != _countSpread)
        {
            count.Inlines = [_countSpread, _countTotal];
        }
    }

    /// <summary>Updates the count text, the status and the button states from the spread on show.</summary>
    internal void RefreshReadouts()
    {
        var shown = _surface?.ShownSpread ?? Spread;
        if (_count is not null)
        {
            UpdateCount(_count);
        }
        if (_status is not null)
        {
            _status.Text = StatusText();
        }
        if (_previous is not null)
        {
            _previous.IsEnabled = shown > 0;
        }
        if (_next is not null)
        {
            _next.IsEnabled = shown < SpreadCount - 1;
        }
    }

    private static int CoerceSpread(AvaloniaObject owner, int value)
        => owner is PageCurl book ? Math.Clamp(value, 0, book.SpreadCount - 1) : Math.Max(0, value);

    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(SpreadCountProperty, 0, SpreadCount);
        CoerceValue(SpreadProperty);
        _surface?.Reload();
        RefreshReadouts();
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        _surface?.Detach();
        _surface = e.NameScope.Find<PageCurlSurface>(SurfacePart);
        _previous = e.NameScope.Find<Button>(PreviousButtonPart);
        _next = e.NameScope.Find<Button>(NextButtonPart);
        _count = e.NameScope.Find<TextBlock>(CountPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        _surface?.Attach(this);
        RefreshReadouts();
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => PreviousSpread();

    private void OnNextClick(object? sender, RoutedEventArgs e) => NextSpread();

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SpreadProperty)
        {
            _surface?.GoTo(change.GetNewValue<int>());
            RefreshReadouts();
        }
        else if (change.Property == PageAspectProperty)
        {
            _surface?.ResizeBook();
        }
        else if (change.Property == HintProperty)
        {
            _surface?.SetHint(change.NewValue);
        }
        else if (change.Property == PageTemplateProperty)
        {
            _surface?.Reload();
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is Button)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Right:
            case Key.PageDown:
                NextSpread();
                e.Handled = true;
                break;
            case Key.Left:
            case Key.PageUp:
                PreviousSpread();
                e.Handled = true;
                break;
        }
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new PageCurlAutomationPeer(this);

    private sealed class PageCurlAutomationPeer(PageCurl owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "book";

        protected override string? GetNameCore() => ((PageCurl)Owner).Label;

        protected override bool IsControlElementCore() => true;
    }
}
