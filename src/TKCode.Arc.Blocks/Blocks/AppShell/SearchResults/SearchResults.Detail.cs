using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class SearchResults
{
    // The detail (created when first opened): one surface, a dialog on wide screens and a bottom sheet on narrow ones.
    private SearchResultsSurface? _detailSurface;
    private ModalPresence? _detailPresence;
    private SearchResultsDetailBody? _detailBody;
    private AvaloniaButton? _openButton;
    private AvaloniaButton? _copyButton;
    private SearchResultItem? _detailItem;

    #region 测试与诊断

    /// <summary>The detail's modal (tests).</summary>
    internal ModalPresence? DetailPresence => _detailPresence;

    /// <summary>The detail's panel (tests).</summary>
    internal SearchResultsSurface? DetailSurface => _detailSurface;

    /// <summary>The built-in detail content (tests).</summary>
    internal SearchResultsDetailBody? DetailBody => _detailBody;

    internal AvaloniaButton? OpenButton => _openButton;

    internal AvaloniaButton? CopyLinkButton => _copyButton;

    #endregion

    private void OnOpenedItemChanged(SearchResultItem? old, SearchResultItem? item)
    {
        if (item is null)
        {
            _detailItem = null;
            _detailPresence?.Close(DismissReason.Programmatic);
            return;
        }
        if (TopLevel.GetTopLevel(this) is null)
        {
            // Not in a window yet: the detail opens once the control is attached.
            Dispatcher.UIThread.Post(() =>
            {
                if (OpenedItem is { } pending && TopLevel.GetTopLevel(this) is not null)
                {
                    ShowDetailCore(pending);
                }
            }, DispatcherPriority.Loaded);
            return;
        }
        ShowDetailCore(item);
    }

    private void ShowDetailCore(SearchResultItem item)
    {
        CloseSuggestions();
        _quietFocus = true;
        EnsureDetail();
        _detailItem = item;
        var sheet = _isNarrow;
        var surface = _detailSurface!;
        surface.IsSheet = sheet;
        surface.Title = KindLabelOf(item) is { Length: > 0 } kind ? kind : null;
        surface.CloseText = CloseText;
        var client = TopLevel.GetTopLevel(this)?.ClientSize ?? new Size(800, 600);
        if (sheet)
        {
            surface.HorizontalAlignment = HorizontalAlignment.Stretch;
            surface.VerticalAlignment = VerticalAlignment.Bottom;
            surface.MaxWidth = double.PositiveInfinity;
            surface.Margin = default;
            surface.MaxHeight = Math.Max(240, client.Height * 0.92);
        }
        else
        {
            surface.HorizontalAlignment = HorizontalAlignment.Center;
            surface.VerticalAlignment = VerticalAlignment.Center;
            surface.MaxWidth = 560;
            surface.Margin = new Thickness(16);
            surface.MaxHeight = Math.Max(240, client.Height - 32);
        }
        if (DetailTemplate is { } template)
        {
            surface.ContentTemplate = template;
            surface.Content = item;
        }
        else
        {
            _detailBody ??= new SearchResultsDetailBody();
            _detailBody.SetItem(item, SubtitleOf(item), _terms, MatchesLabel);
            surface.ContentTemplate = null;
            surface.Content = _detailBody;
        }
        var kindName = KindLabelOf(item).ToLower(System.Globalization.CultureInfo.CurrentCulture);
        _openButton!.Content = kindName.Length > 0 ? Format(OpenFormat, kindName) : Format(OpenFormat, string.Empty).Trim();
        _copyButton!.Content = CopyLinkContent();
        _detailPresence!.Entrance = sheet ? ModalEntrance.Slide : ModalEntrance.Rise;
        _detailPresence.Side = ModalSide.Bottom;
        _detailPresence.Open();
    }

    private void EnsureDetail()
    {
        if (_detailPresence is not null)
        {
            return;
        }
        _openButton = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Medium };
        _openButton.Click += (_, e) =>
        {
            e.Handled = true;
            RaiseDetailAction(SearchResultAction.Open);
        };
        _copyButton = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Medium,
            Classes = { "copy-link" }
        };
        _copyButton.Click += (_, e) =>
        {
            e.Handled = true;
            RaiseDetailAction(SearchResultAction.CopyLink);
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _openButton, _copyButton } };
        _detailSurface = new SearchResultsSurface { CloseText = CloseText, Actions = actions };
        ((ISetLogicalParent)_detailSurface).SetParent(this);
        _detailPresence = new ModalPresence(_detailSurface, this) { Entrance = ModalEntrance.Rise, InitialFocus = _detailSurface };
        _detailSurface.CloseRequested += (_, _) => _detailPresence?.Dismiss(DismissReason.CloseButton);
        _detailPresence.Dismissing += (_, _) =>
        {
            // Escape or a press on the scrim: the item is no longer open (the exit still plays).
            if (OpenedItem is not null)
            {
                SetCurrentValue(OpenedItemProperty, null);
            }
        };
        _detailPresence.Closed += (_, _) =>
        {
            if (OpenedItem is not null)
            {
                SetCurrentValue(OpenedItemProperty, null);
            }
        };
    }

    private object CopyLinkContent()
        => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new ArcGlyph { Data = ArcIcons.Link, Size = 16, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = CopyLinkText, VerticalAlignment = VerticalAlignment.Center }
            }
        };

    private void RaiseDetailAction(SearchResultAction action)
    {
        if (_detailItem is not { } item)
        {
            return;
        }
        if (action == SearchResultAction.CopyLink && !string.IsNullOrEmpty(item.Url))
        {
            _ = ArcClipboard.CopyTextAsync(this, item.Url);
        }
        RaiseEvent(new SearchResultActionEventArgs(DetailActionInvokedEvent, item, action));
    }
}
