using Avalonia.Controls;

namespace ArcStarterApp.Navigation;

/// <summary>
/// Shows one <see cref="AppPage"/> at a time. Each page is created the first time it is shown and kept, so a page keeps
/// its state (scroll position, entered text) when the person comes back to it.
/// </summary>
public sealed class PageHost : Decorator
{
    private readonly Dictionary<string, Control> _views = new();

    /// <summary>The page shown, or null before the first navigation.</summary>
    public AppPage? Current { get; private set; }

    /// <summary>Raised after the page shown changed.</summary>
    public event EventHandler? Navigated;

    /// <summary>Shows the page with this key; returns false when there is no such page.</summary>
    public bool Navigate(string? key)
    {
        if (AppPages.Find(key) is not { } page)
        {
            return false;
        }
        if (page == Current)
        {
            return true;
        }
        if (!_views.TryGetValue(page.Key, out var view))
        {
            view = page.Create();
            _views[page.Key] = view;
        }
        Child = view;
        Current = page;
        Navigated?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
