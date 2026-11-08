using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>What a <see cref="PdfViewer"/> does with a link that leads out of the document.</summary>
public enum PdfExternalLinkBehavior
{
    /// <summary>Opens it with the system, if its scheme is http, https or mailto.</summary>
    Open,

    /// <summary>Only raises <see cref="PdfViewer.LinkActivated"/>.</summary>
    None
}

/// <summary>Arguments of <see cref="PdfViewer.LinkActivated"/>. Set <see cref="RoutedEventArgs.Handled"/> to keep the viewer from following the link.</summary>
public sealed class PdfLinkEventArgs : RoutedEventArgs
{
    internal PdfLinkEventArgs(RoutedEvent routedEvent, PdfLink link)
        : base(routedEvent)
    {
        Link = link;
    }

    /// <summary>The link. Its URI comes from an untrusted document: check the scheme before doing anything with it.</summary>
    public PdfLink Link { get; }
}

public partial class PdfViewer
{
    private static readonly TimeSpan s_linkTipDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>What a link can show of its address: a document decides how long that is.</summary>
    private const int MaxLinkTipLength = 200;

    #region Properties

    /// <summary>What to do with a link that leads out of the document. Only http, https and mailto ever open; a document is untrusted, so every other scheme is refused whatever this says.</summary>
    public static readonly StyledProperty<PdfExternalLinkBehavior> ExternalLinkBehaviorProperty =
        AvaloniaProperty.Register<PdfViewer, PdfExternalLinkBehavior>(nameof(ExternalLinkBehavior));

    /// <summary>A link was clicked. Setting Handled keeps the viewer from following it.</summary>
    public static readonly RoutedEvent<PdfLinkEventArgs> LinkActivatedEvent =
        RoutedEvent.Register<PdfViewer, PdfLinkEventArgs>(nameof(LinkActivated), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ExternalLinkBehaviorProperty"/>
    public PdfExternalLinkBehavior ExternalLinkBehavior { get => GetValue(ExternalLinkBehaviorProperty); set => SetValue(ExternalLinkBehaviorProperty, value); }

    /// <inheritdoc cref="LinkActivatedEvent"/>
    public event EventHandler<PdfLinkEventArgs>? LinkActivated
    {
        add => AddHandler(LinkActivatedEvent, value);
        remove => RemoveHandler(LinkActivatedEvent, value);
    }

    #endregion

    private PdfLinkLayer _linkLayer = null!;
    private Border? _linkTipAnchor;
    private DispatcherTimer? _linkTipTimer;
    private bool _linkTipPlacementPending;
    private Point _hoveredPointInPage;

    /// <summary>The link layer.</summary>
    internal PdfLinkLayer LinkLayer => _linkLayer;

    /// <summary>Opens an allowed address instead of the system launcher (tests).</summary>
    internal Func<Uri, Task<bool>>? UriOpener { get; set; }

    /// <summary>What sits where the tooltip of the link under the pointer points (tests).</summary>
    internal Control? LinkTipAnchor => _linkTipAnchor;

    /// <summary>The default action of a click on a link: the event first, then the place in the document or the allowed address.</summary>
    internal void ActivateLink(PdfLink link)
    {
        ClearLinkTip();
        var args = new PdfLinkEventArgs(LinkActivatedEvent, link);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        if (link.Destination is { } destination)
        {
            JumpTo(destination);
        }
        else if (ExternalLinkBehavior == PdfExternalLinkBehavior.Open && PdfLinkPolicy.IsAllowed(link.Uri))
        {
            _ = OpenUriAsync(link.Uri!);
        }
    }

    private async Task OpenUriAsync(Uri uri)
    {
        try
        {
            if (UriOpener is { } opener)
            {
                await opener(uri);
            }
            else if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                await launcher.LaunchUriAsync(uri);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // The system would not open it. There is nothing to show for that, and nothing worth crashing over.
        }
    }

    /// <summary>The layer has a new link under the pointer, or none. The tip's place is worked out in the move still on its way up.</summary>
    internal void OnLinkHoverChanged(PdfLink? link, int pageIndex, Point pointInPage)
    {
        ClearLinkTip();
        if (link is null || LinkTipText(link) is not { } text)
        {
            return;
        }
        EnsureLinkTipAnchor();
        Tooltip.SetTip(_linkTipAnchor!, text);
        _hoveredPointInPage = pointInPage;
        _linkTipPlacementPending = true;
        _linkTipTimer ??= new DispatcherTimer(s_linkTipDelay, DispatcherPriority.Normal, (_, _) =>
        {
            _linkTipTimer!.Stop();
            if (_linkTipAnchor is { } anchor)
            {
                Tooltip.SetIsOpen(anchor, true);
            }
        });
        _linkTipTimer.Start();
    }

    /// <summary>The address as a reader may see it: encoded, so a document cannot hide anything in invisible or look-alike characters, and of a sane length.</summary>
    internal static string? LinkTipText(PdfLink link)
    {
        if (link.Uri is { IsAbsoluteUri: true } uri)
        {
            var text = uri.AbsoluteUri;
            return text.Length > MaxLinkTipLength ? string.Concat(text.AsSpan(0, MaxLinkTipLength), "\u2026") : text;
        }
        return link.Destination is { } destination
            ? PdfViewerLangResources.Format(PdfViewerLangResourceKind.PageNumber, destination.PageIndex + 1)
            : null;
    }

    private void EnsureLinkTipAnchor()
    {
        if (_linkTipAnchor is null)
        {
            _linkTipAnchor = new Border
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            EnsureOverlay().Children.Insert(0, _linkTipAnchor);
        }
    }

    private void ClearLinkTip()
    {
        _linkTipTimer?.Stop();
        _linkTipPlacementPending = false;
        if (_linkTipAnchor is { } anchor)
        {
            Tooltip.SetIsOpen(anchor, false);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_linkLayer.Hovered is not { } link)
        {
            return;
        }
        // The move came from outside the pages, or from the canvas where layers hear nothing.
        var position = e.GetPosition(this);
        if (!IsPresenter(e.Source) || !PageHitTest(position, out _, out _))
        {
            _linkLayer.ClearHover();
            return;
        }
        if (_linkTipPlacementPending && _linkTipAnchor is { } anchor && _overlay is { } overlay)
        {
            _linkTipPlacementPending = false;
            // From the pointer and its place on the page, back to the link's corner in the overlay.
            var zoom = Zoom;
            var at = e.GetPosition(overlay);
            anchor.Margin = new Thickness(at.X - (_hoveredPointInPage.X - link.Bounds.X) * zoom, at.Y - (_hoveredPointInPage.Y - link.Bounds.Y) * zoom, 0, 0);
            anchor.Width = link.Bounds.Width * zoom;
            anchor.Height = link.Bounds.Height * zoom;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _linkLayer.ClearHover();
    }

    /// <summary>The wheel moves the page out from under the pointer.</summary>
    private void OnWheelTunnel(object? sender, PointerWheelEventArgs e) => _linkLayer.ClearHover();
}
