using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The add step menu of an <see cref="AiWorkflow"/>: a compact command menu in a Core <see cref="ArcPopup"/> (it fades and
/// settles in 160ms with no overshoot, and leaves in 120ms). A search field owns focus and filters by title, app or
/// detail; the rows show the app's mark, the title, the app and, with an empty search, a 1 to 9 shortcut. Up and Down
/// move the highlight (a gliding Surface-muted block on the 0.22s spring), Enter adds the highlighted step, a digit adds
/// the step with that shortcut, Escape closes and gives focus back to the Add step button. Filtering animates the list
/// to its new measured height.
/// </summary>
internal sealed class AiWorkflowAddMenu
{
    public const double Width = 300;

    public const double MaxListHeight = 288;

    private readonly ArcPopup _popup;
    private readonly Input _search;
    private readonly StackPanel _rows = new();
    private readonly GlidingIndicator _highlight = new() { FadeDuration = TimeSpan.FromMilliseconds(80) };
    private readonly TextBlock _empty;
    private readonly ScrollViewer _scroll;
    private readonly List<AiWorkflowMenuRow> _items = [];
    private IReadOnlyList<AiWorkflowCatalogItem> _catalog = [];
    private bool _allowsBranch = true;
    private int _index = -1;
    private bool _swallowText;
    private Control? _anchor;

    public AiWorkflowAddMenu()
    {
        _search = new Input
        {
            Size = ControlSize.Small,
            Margin = new Thickness(6, 6, 6, 4),
            Prefix = AiWorkflowViews.Glyph(ArcIcons.Search, 14, ArcDesignTokenKind.TextMuted)
        };
        _search.AddHandler(InputElement.KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        _search.AddHandler(InputElement.TextInputEvent, OnSearchTextInput, RoutingStrategies.Tunnel);
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Refilter();
            }
        };
        _highlight.BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        ArcTokens.Bind(_highlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.SurfaceMuted);
        var list = new Panel { Margin = new Thickness(4, 0, 4, 4) };
        list.Children.Add(_highlight);
        list.Children.Add(_rows);
        _scroll = new ScrollViewer
        {
            MaxHeight = MaxListHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = list
        };
        AutomationProperties.SetAccessibilityView(_rows, AccessibilityView.Control);
        _empty = AiWorkflowViews.Text(null, 12, ArcDesignTokenKind.TextMuted);
        _empty.Margin = new Thickness(12, 6, 12, 10);
        var height = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Height,
            Spring = ArcSpring.Quick,
            Child = new StackPanel { Children = { _scroll, _empty } }
        };
        var surface = new Border
        {
            Width = Width,
            Child = new StackPanel { Children = { _search, height } }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(surface, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(surface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(surface, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        _popup = new ArcPopup
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            MarginToAnchor = 6,
            EnterOffset = 4,
            EnterScale = 0.98,
            Child = surface
        };
        _popup.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised with the entry the person picked (the menu has closed).</summary>
    public event EventHandler<AiWorkflowCatalogItem>? Picked;

    /// <summary>Raised when the menu has closed.</summary>
    public event EventHandler? Closed;

    public ArcPopup Popup => _popup;

    public Input Search => _search;

    public bool IsOpen => _popup.IsOpen;

    internal IReadOnlyList<AiWorkflowMenuRow> Rows => _items;

    internal int HighlightIndex => _index;

    internal GlidingIndicator Highlight => _highlight;

    /// <summary>Opens the menu under <paramref name="anchor"/> with an empty search.</summary>
    public void Open(Control anchor, IReadOnlyList<AiWorkflowCatalogItem> catalog, bool allowsBranch, AiWorkflowLabels labels)
    {
        _anchor = anchor;
        _catalog = catalog;
        _allowsBranch = allowsBranch;
        _search.Placeholder = labels.SearchPlaceholder;
        AutomationProperties.SetName(_search, labels.SearchPlaceholder);
        AutomationProperties.SetName(_rows, labels.MenuName);
        _empty.Text = labels.NoMatches;
        _swallowText = false;
        _search.Text = string.Empty;
        Refilter();
        if (_popup.Parent is null && anchor is Control host)
        {
            ((ISetLogicalParent)_popup).SetParent(host);
        }
        _popup.PlacementTarget = anchor;
        _popup.IsOpen = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_popup.IsOpen)
            {
                _search.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Closes the menu; with <paramref name="returnFocus"/> the Add step button gets focus back.</summary>
    public void Close(bool returnFocus)
    {
        if (!_popup.IsOpen)
        {
            return;
        }
        if (returnFocus)
        {
            // Before the popup closes: closing it with focus inside puts focus back by itself, without the keyboard
            // ring, and focusing the already focused anchor then changes nothing. A pick with the mouse gets no ring.
            _anchor?.Focus(SpecialFocusModality.For(_anchor));
        }
        _popup.IsOpen = false;
    }

    private void Refilter()
    {
        var items = AiWorkflowFormat.Filter(_catalog, _search.Text, _allowsBranch);
        var numbered = string.IsNullOrEmpty(_search.Text);
        foreach (var row in _items)
        {
            row.Click -= OnRowClick;
            row.PointerEntered -= OnRowPointerEntered;
        }
        _items.Clear();
        _rows.Children.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var row = new AiWorkflowMenuRow(items[i], numbered && i < 9 ? i + 1 : null) { Focusable = false };
            row.Click += OnRowClick;
            row.PointerEntered += OnRowPointerEntered;
            _items.Add(row);
            _rows.Children.Add(row);
        }
        _empty.IsVisible = items.Count == 0;
        _scroll.IsVisible = items.Count > 0;
        _highlight.Hide(animate: false);
        _index = -1;
        MoveTo(items.Count > 0 ? 0 : -1, animate: false);
    }

    private void MoveTo(int index, bool animate)
    {
        _index = index;
        if (index < 0 || index >= _items.Count)
        {
            _highlight.Hide(animate);
            AutomationProperties.SetHelpText(_search, null);
            return;
        }
        var row = _items[index];
        AutomationProperties.SetHelpText(_search, AutomationProperties.GetName(row));
        if (row.Bounds.Width > 0)
        {
            _highlight.MoveTo(row.Bounds, AiWorkflowMetrics.Spring, animate);
            row.BringIntoView();
        }
        else
        {
            // Not laid out yet: place it once the rows have been arranged.
            Dispatcher.UIThread.Post(() =>
            {
                if (_index == index && index < _items.Count && ReferenceEquals(_items[index], row))
                {
                    _highlight.MoveTo(row.Bounds, AiWorkflowMetrics.Spring, animate: false);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is AiWorkflowMenuRow row)
        {
            MoveTo(_items.IndexOf(row), animate: true);
        }
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AiWorkflowMenuRow row)
        {
            Pick(row.Item);
        }
    }

    private void Pick(AiWorkflowCatalogItem item)
    {
        Close(returnFocus: true);
        Picked?.Invoke(this, item);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (_items.Count > 0)
                {
                    MoveTo((_index + 1) % _items.Count, animate: true);
                }
                e.Handled = true;
                return;
            case Key.Up:
                if (_items.Count > 0)
                {
                    MoveTo(_index <= 0 ? _items.Count - 1 : _index - 1, animate: true);
                }
                e.Handled = true;
                return;
            case Key.Enter:
                if (_index >= 0 && _index < _items.Count)
                {
                    Pick(_items[_index].Item);
                }
                e.Handled = true;
                return;
            case Key.Escape:
                Close(returnFocus: true);
                e.Handled = true;
                return;
        }
        if (string.IsNullOrEmpty(_search.Text) && Digit(e.Key) is { } n)
        {
            var row = _items.FirstOrDefault(r => r.Shortcut == n);
            if (row is not null)
            {
                _swallowText = true;
                e.Handled = true;
                Pick(row.Item);
            }
        }
    }

    private void OnSearchTextInput(object? sender, TextInputEventArgs e)
    {
        if (_swallowText)
        {
            _swallowText = false;
            e.Handled = true;
        }
    }

    private static int? Digit(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null
    };
}
