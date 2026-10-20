using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// "On this page": the sections of the current page on a rail. The bar on the rail glides to the section being
/// read; clicking a row scrolls to that section.
/// </summary>
public sealed class GalleryToc : Border
{
    private readonly List<GalleryNavRow> _rows = new();
    private readonly Panel _host = new();
    private readonly GlidingIndicator _indicator = new() { IsHitTestVisible = false, Spring = ArcSpring.Snappy };
    private int _active = -1;
    private Rect _lastRect;

    public GalleryToc(IReadOnlyList<string> titles, Action<int> navigate, Action backToTop, Action openOriginal)
    {
        var rows = new StackPanel();
        for (var i = 0; i < titles.Count; i++)
        {
            var index = i;
            var row = new GalleryNavRow(new TextBlock { Text = titles[i], TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center },
                titles[i], height: 28);
            row.Click += () => navigate(index);
            rows.Children.Add(row);
            _rows.Add(row);
        }
        var rail = GalleryUi.Rail(rows, 0);

        ArcTokens.Bind(_indicator, GlidingIndicator.FillProperty, ArcDesignTokenKind.Foreground);
        _indicator.CornerRadius = new CornerRadius(1);
        _host.Children.Add(rail);
        _host.Children.Add(_indicator);
        _host.LayoutUpdated += (_, _) => Sync(animate: false);

        var title = GalleryUi.Text("On this page", ArcDesignTokenKind.Foreground, 13, FontWeight.Medium);
        title.Margin = new Thickness(0, 0, 0, 10);

        var stack = new StackPanel();
        stack.Children.Add(title);
        stack.Children.Add(_host);
        stack.Children.Add(BuildLinks(backToTop, openOriginal));
        Child = stack;
        SetActive(0);
    }

    private static Control BuildLinks(Action backToTop, Action openOriginal)
    {
        var divider = new Border { Height = 1, Margin = new Thickness(0, 16, 0, 12) };
        ArcTokens.Bind(divider, BackgroundProperty, ArcDesignTokenKind.BorderSubtle);

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(divider);
        stack.Children.Add(Link("View on uiarc.dev", ArcIcons.ExternalLink, openOriginal));
        stack.Children.Add(Link("Back to top", ArcIcons.ArrowUp, backToTop));
        return stack;
    }

    private static Control Link(string text, Geometry glyph, Action action)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new ArcGlyph { Data = glyph, Size = 13 });
        var link = new GalleryNavRow(row, text, fontSize: 12, height: 28, padding: new Thickness(0), radius: 8)
        {
            HorizontalAlignment = HorizontalAlignment.Left
        };
        link.Click += action;
        return link;
    }

    public void SetActive(int index)
    {
        if (index == _active || index < 0 || index >= _rows.Count)
        {
            return;
        }
        var first = _active < 0;
        _active = index;
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].IsSelected = i == index;
        }
        Sync(animate: !first);
    }

    private void Sync(bool animate)
    {
        if (_active < 0 || _rows[_active].Bounds.Height <= 0 || _rows[_active].TranslatePoint(default, _host) is not { } origin)
        {
            return;
        }
        var rect = new Rect(-0.5, origin.Y + 6, 2, Math.Max(0, _rows[_active].Bounds.Height - 12));
        if (!animate && rect == _lastRect)
        {
            return;
        }
        _lastRect = rect;
        _indicator.MoveTo(rect, animate);
    }
}
