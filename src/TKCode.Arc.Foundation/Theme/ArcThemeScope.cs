using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace TKCode.Arc;

/// <summary>
/// Gives a subtree its own theme: the tokens are computed for the scope's state and published into the element's
/// <see cref="StyledElement.Resources"/>, which resource lookups inside the subtree reach before the application's.
/// Used for side-by-side light and dark previews and for screenshots.
/// </summary>
/// <remarks>
/// <see cref="AppearanceProperty"/> keeps following the application's state (accent, overrides, motion) and only fixes
/// light or dark. <see cref="StateProperty"/> fixes the whole state. When the element is a
/// <see cref="ThemeVariantScope"/> or a <see cref="TopLevel"/>, its requested theme variant is set to match.
/// </remarks>
public static class ArcThemeScope
{
    /// <summary>Fixes light or dark for the subtree; everything else follows the application's theme.</summary>
    public static readonly AttachedProperty<ArcAppearance?> AppearanceProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, ArcAppearance?>("Appearance", typeof(ArcThemeScope));

    /// <summary>Fixes the whole theme state of the subtree.</summary>
    public static readonly AttachedProperty<ArcThemeState?> StateProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, ArcThemeState?>("State", typeof(ArcThemeScope));

    private static readonly AttachedProperty<ScopeHost?> HostProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, ScopeHost?>("Host", typeof(ArcThemeScope));

    static ArcThemeScope()
    {
        AppearanceProperty.Changed.AddClassHandler<StyledElement>((element, _) => Update(element));
        StateProperty.Changed.AddClassHandler<StyledElement>((element, _) => Update(element));
    }

    /// <summary>Gets the fixed appearance of <paramref name="element"/>'s scope.</summary>
    public static ArcAppearance? GetAppearance(StyledElement element) => element.GetValue(AppearanceProperty);

    /// <summary>Fixes light or dark for <paramref name="element"/>'s subtree; null removes the scope.</summary>
    public static void SetAppearance(StyledElement element, ArcAppearance? value) => element.SetValue(AppearanceProperty, value);

    /// <summary>Gets the fixed state of <paramref name="element"/>'s scope.</summary>
    public static ArcThemeState? GetState(StyledElement element) => element.GetValue(StateProperty);

    /// <summary>Fixes the whole theme state of <paramref name="element"/>'s subtree; null removes the scope.</summary>
    public static void SetState(StyledElement element, ArcThemeState? value) => element.SetValue(StateProperty, value);

    private static void Update(StyledElement element)
    {
        var host = element.GetValue(HostProperty);
        if (GetAppearance(element) is null && GetState(element) is null)
        {
            if (host is not null)
            {
                host.Dispose();
                element.ClearValue(HostProperty);
            }
            return;
        }
        if (host is null)
        {
            host = new ScopeHost(element);
            element.SetValue(HostProperty, host);
        }
        host.Refresh();
    }

    private sealed class ScopeHost : IDisposable
    {
        private readonly StyledElement _element;
        private readonly ResourceDictionary _tokens = new();
        private ArcThemeManager? _manager;

        public ScopeHost(StyledElement element)
        {
            _element = element;
            element.Resources.MergedDictionaries.Add(_tokens);
            element.AttachedToLogicalTree += OnAttached;
            element.DetachedFromLogicalTree += OnDetached;
            if (((ILogical)element).IsAttachedToLogicalTree)
            {
                Subscribe();
            }
        }

        public void Refresh()
        {
            var manager = ArcThemeManager.Current;
            if (manager is null)
            {
                // Not set up yet: refreshed when the element joins the tree.
                return;
            }
            var fixedState = GetState(_element);
            var appearance = GetAppearance(_element);
            var state = fixedState ?? manager.State;
            if (appearance is { } fixedAppearance)
            {
                state = state.With(t => t.Appearance(fixedAppearance));
            }
            var isDark = state.ResolveIsDark(manager.SystemIsDark);
            // Without a fixed state the scope differs from the application only in light or dark, so it shares the
            // manager's cached values for that appearance.
            var values = fixedState is null ? manager.GetCachedValues(isDark) : manager.Compute(state, isDark);
            ArcTokenCalculator.Publish(_tokens, values);
            var variant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
            if (_element is ThemeVariantScope scope)
            {
                scope.RequestedThemeVariant = variant;
            }
            else if (_element is TopLevel topLevel)
            {
                topLevel.RequestedThemeVariant = variant;
            }
        }

        public void Dispose()
        {
            Unsubscribe();
            _element.AttachedToLogicalTree -= OnAttached;
            _element.DetachedFromLogicalTree -= OnDetached;
            _element.Resources.MergedDictionaries.Remove(_tokens);
        }

        private void OnAttached(object? sender, LogicalTreeAttachmentEventArgs e)
        {
            Subscribe();
            Refresh();
        }

        private void OnDetached(object? sender, LogicalTreeAttachmentEventArgs e) => Unsubscribe();

        private void Subscribe()
        {
            if (_manager is null && ArcThemeManager.Current is { } manager)
            {
                _manager = manager;
                manager.StateChanged += OnStateChanged;
            }
        }

        private void Unsubscribe()
        {
            if (_manager is not null)
            {
                _manager.StateChanged -= OnStateChanged;
                _manager = null;
            }
        }

        private void OnStateChanged(object? sender, ArcThemeChangedEventArgs e)
        {
            if (GetState(_element) is null || GetState(_element)!.Appearance == ArcAppearance.System)
            {
                Refresh();
            }
        }
    }
}
