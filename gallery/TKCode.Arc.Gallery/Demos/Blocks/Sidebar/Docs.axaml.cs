using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class SidebarDocsDemo : UserControl
{
    private readonly List<(Control Heading, Border Entry)> _sections = new();
    private int _active = -1;

    public SidebarDocsDemo()
    {
        InitializeComponent();
        // "On this page": one entry per heading. The bar is a GlidingIndicator in the entries' coordinate space.
        foreach (var heading in new[] { Section1, Section2, Section3, Section4, Section5, Section6 })
        {
            var entry = new Border
            {
                Child = new TextBlock { Text = heading.Text },
                Focusable = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            entry.Classes.Add("entry");
            var captured = heading;
            entry.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                {
                    ScrollTo(captured);
                }
            };
            entry.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space)
                {
                    ScrollTo(captured);
                    e.Handled = true;
                }
            };
            OutlineItems.Children.Add(entry);
            _sections.Add((heading, entry));
        }
        Page.ScrollChanged += (_, _) => UpdateActive(animate: true);
        // The entries wrap with the column: place the bar again whenever the outline resizes (also the first layout).
        OutlineItems.SizeChanged += (_, _) => UpdateActive(animate: false, force: true);
    }

    private void ScrollTo(Control heading)
    {
        if (heading.TranslatePoint(default, Article) is { } point)
        {
            Page.Offset = new Vector(0, Math.Max(0, point.Y - 24));
        }
    }

    /// <summary>The active section is the last heading that has reached the top of the page (the last one at the end).</summary>
    private void UpdateActive(bool animate, bool force = false)
    {
        var top = Page.Offset.Y + 40;
        var atEnd = Page.Offset.Y + Page.Viewport.Height >= Page.Extent.Height - 1;
        var index = 0;
        for (var i = 0; i < _sections.Count; i++)
        {
            if (_sections[i].Heading.TranslatePoint(default, Article) is { } point && point.Y <= top)
            {
                index = i;
            }
        }
        if (atEnd && Page.Extent.Height > Page.Viewport.Height)
        {
            index = _sections.Count - 1;
        }
        if (index == _active && !force)
        {
            return;
        }
        _active = index;
        for (var i = 0; i < _sections.Count; i++)
        {
            _sections[i].Entry.Classes.Set("active", i == index);
        }
        var entry = _sections[index].Entry;
        if (entry.Bounds.Height > 0)
        {
            OutlineBar.MoveTo(new Rect(0, entry.Bounds.Y, 2, entry.Bounds.Height), animate && !force);
        }
    }
}
