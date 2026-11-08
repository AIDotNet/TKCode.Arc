using Avalonia;
using Avalonia.Controls;

namespace ArcStarterApp.Navigation;

/// <summary>
/// Shows one <see cref="AppPage"/> at a time: set <see cref="Page"/> (or bind it) or call <see cref="Navigate"/>. Each
/// page is created the first time it is shown and kept, so a page keeps its state (scroll position, entered text) when
/// the person comes back to it.
/// </summary>
public sealed class PageHost : Decorator
{
    /// <summary>Defines the <see cref="Page"/> property.</summary>
    public static readonly StyledProperty<AppPage?> PageProperty =
        AvaloniaProperty.Register<PageHost, AppPage?>(nameof(Page));

    private readonly Dictionary<string, Control> _views = new();

    /// <summary>The page shown, or null before the first navigation.</summary>
    public AppPage? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <summary>The page shown (the same as <see cref="Page"/>).</summary>
    public AppPage? Current => Page;

    /// <summary>Raised after the page shown changed.</summary>
    public event EventHandler? Navigated;

    /// <summary>Shows the page with this key; returns false when there is no such page.</summary>
    public bool Navigate(string? key)
    {
        if (AppPages.Find(key) is not { } page)
        {
            return false;
        }
        SetCurrentValue(PageProperty, page);
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty)
        {
            Show(change.GetNewValue<AppPage?>());
        }
    }

    private void Show(AppPage? page)
    {
        if (page is null)
        {
            Child = null;
        }
        else
        {
            if (!_views.TryGetValue(page.Key, out var view))
            {
                view = page.Create();
                _views[page.Key] = view;
            }
            Child = view;
        }
        Navigated?.Invoke(this, EventArgs.Empty);
    }
}
