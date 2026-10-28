using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>One view of a page with a view switcher (Projects: List, Board).</summary>
/// <param name="Id">What <see cref="DemoIntent.View"/> calls it.</param>
/// <param name="Label">The switcher's label.</param>
/// <param name="Build">Builds the view the first time it is shown; it is kept after that.</param>
public sealed record DemoView(string Id, string Label, Func<Control> Build);

/// <summary>
/// A page of the Northstar demo, as uiarc.dev's <c>DemoPage</c>: it scrolls, keeps uiarc.dev's padding (24 at the top, a
/// gutter of 2.6% of the width between 16 and 32 at the sides, 96 at the bottom) and stacks its <see cref="DemoRow"/>s
/// 16 apart. A page can have a toolbar: a segmented switch between views (<see cref="SetViews"/>) and actions on the
/// right. The page name is in the breadcrumb, so there is no visible title.
/// </summary>
public abstract class DemoPageView : UserControl
{
    private readonly StackPanel _column = new() { Spacing = DemoRow.Gap };
    private readonly Border _padding = new();
    private readonly ContentControl _viewHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly Dictionary<string, Control> _builtViews = new();
    private SegmentedControl? _switcher;
    private IReadOnlyList<DemoView> _views = [];
    private bool _held = true;
    private double _heldAt;

    protected DemoPageView(IDemoHost host, DemoPage page)
    {
        Host = host;
        Page = page;
        Scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BringIntoViewOnFocusChange = false,
            Content = _padding
        };
        HoldUntilTouched();
        _padding.Child = _column;
        Content = Scroll;
        Scroll.SizeChanged += (_, e) => _padding.Padding = PaddingFor(e.NewSize.Width);
        Avalonia.Automation.AutomationProperties.SetName(this, page.Title);
    }

    protected IDemoHost Host { get; }

    public DemoPage Page { get; }

    protected ScrollViewer Scroll { get; }

    /// <summary>The page's column: rows go here, or the view switcher's views.</summary>
    protected StackPanel Column => _column;

    /// <summary>The id of the view that is showing, or null on a page without views.</summary>
    public string? CurrentView => _switcher?.SelectedValue as string;

    /// <summary>The gutter between the page and the edges of the window: 2.6% of the width, from 16 to 32.</summary>
    public static double Gutter(double width) => Math.Clamp(width * 0.026, 16, 32);

    protected virtual Thickness PaddingFor(double width)
    {
        var gutter = Gutter(width);
        return new Thickness(gutter, width <= 640 ? 16 : 24, gutter, 96);
    }

    /// <summary>
    /// The page opens at the top, or at the section an intent asked for, and stays there until the visitor scrolls, presses
    /// or types. Blocks that are built with the page (a timeline scrolling to its newest event, a control asking to be
    /// brought into view) would otherwise carry it halfway down, like the gallery's home page.
    /// </summary>
    private void HoldUntilTouched()
    {
        void Touch(object? sender, RoutedEventArgs e) => _held = false;
        Scroll.AddHandler(PointerWheelChangedEvent, Touch, RoutingStrategies.Tunnel);
        Scroll.AddHandler(PointerPressedEvent, Touch, RoutingStrategies.Tunnel);
        Scroll.AddHandler(KeyDownEvent, Touch, RoutingStrategies.Tunnel);
        Scroll.ScrollChanged += (_, _) =>
        {
            if (_held && Math.Abs(Scroll.Offset.Y - _heldAt) > 0.5)
            {
                Scroll.Offset = new Vector(0, _heldAt);
            }
        };
    }

    /// <summary>Moves the page to <paramref name="y"/> and holds it there until the visitor takes over.</summary>
    protected void ScrollTo(double y)
    {
        _heldAt = Math.Max(0, y);
        _held = true;
        Scroll.Offset = new Vector(0, _heldAt);
    }

    /// <summary>Adds a row of cards to the page's column.</summary>
    protected DemoRow AddRow(DemoRowLayout layout, string? id, params Control[] cards)
    {
        var row = new DemoRow(layout, id, cards);
        _column.Children.Add(row);
        return row;
    }

    /// <summary>
    /// Gives the page a toolbar that switches between <paramref name="views"/> (the first one shows), with
    /// <paramref name="actions"/> on its right. Call it before adding rows: the views take the column.
    /// </summary>
    protected void SetViews(IReadOnlyList<DemoView> views, Control? actions = null)
    {
        _views = views;
        _switcher = new SegmentedControl { HorizontalAlignment = HorizontalAlignment.Left };
        Avalonia.Automation.AutomationProperties.SetName(_switcher, $"{Page.Title} views");
        foreach (var view in views)
        {
            _switcher.Items.Add(new SegmentedOption { Value = view.Id, Label = view.Label });
        }
        _switcher.SelectedValue = views[0].Id;
        _switcher.SelectionChanged += (_, _) => ShowView(CurrentView);

        var toolbar = new DockPanel();
        if (actions is not null)
        {
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(actions, Dock.Right);
            toolbar.Children.Add(actions);
        }
        toolbar.Children.Add(_switcher);
        _column.Children.Add(toolbar);
        _column.Children.Add(_viewHost);
        ShowView(views[0].Id);
    }

    /// <summary>Switches to the view <paramref name="id"/>; false when the page has no such view.</summary>
    public bool SelectView(string id)
    {
        if (_switcher is null || _views.All(view => view.Id != id))
        {
            return false;
        }
        _switcher.SelectedValue = id;
        ShowView(id);
        return true;
    }

    private void ShowView(string? id)
    {
        if (_views.FirstOrDefault(view => view.Id == id) is not { } view)
        {
            return;
        }
        if (!_builtViews.TryGetValue(view.Id, out var control))
        {
            control = view.Build();
            _builtViews[view.Id] = control;
            // A view built now can pull the page down as well.
            ScrollTo(Scroll.Offset.Y);
        }
        _viewHost.Content = control;
    }

    /// <summary>
    /// Follows a search result or a menu item into the page: its view when the page has one by that name, otherwise the
    /// row with that id scrolls to the top. Pages that open something more (a project) override it.
    /// </summary>
    public virtual void ApplyIntent(DemoIntent intent)
    {
        if (intent.View is not { } view || SelectView(view))
        {
            return;
        }
        if (this.GetVisualDescendantsOfType<DemoRow>().FirstOrDefault(row => row.Id == view) is { } target
            && target.TranslatePoint(default, _column) is { } origin)
        {
            ScrollTo(origin.Y + _padding.Padding.Top - 16);
        }
    }
}

internal static class DemoVisualExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual visual) where T : Visual =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(visual).OfType<T>();
}
