using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using TKCode.Arc.A2UI;

namespace TKCode.Arc;

/// <summary>
/// Shows one A2UI surface with Arc controls. Give it a <see cref="Renderer"/> and a <see cref="SurfaceId"/> (it waits
/// for the surface to be created and clears when it is deleted), or a <see cref="Surface"/> directly. User input is
/// written to the surface's data model and actions are sent through the renderer's <see cref="A2UIRenderer.Outbound"/>.
/// </summary>
public class A2UISurfaceView : Decorator
{
    /// <summary>The renderer that owns the surface (with <see cref="SurfaceId"/>).</summary>
    public static readonly StyledProperty<A2UIRenderer?> RendererProperty =
        AvaloniaProperty.Register<A2UISurfaceView, A2UIRenderer?>(nameof(Renderer));

    /// <summary>The id of the surface to show; the view waits until the renderer creates it and clears when it is deleted.</summary>
    public static readonly StyledProperty<string?> SurfaceIdProperty =
        AvaloniaProperty.Register<A2UISurfaceView, string?>(nameof(SurfaceId));

    /// <summary>The surface to show; takes precedence over <see cref="SurfaceId"/>.</summary>
    public static readonly StyledProperty<A2UISurface?> SurfaceProperty =
        AvaloniaProperty.Register<A2UISurfaceView, A2UISurface?>(nameof(Surface));

    /// <summary>The component views to use; null uses <see cref="A2UIComponentRegistry.Default"/> (the basic catalog).</summary>
    public static readonly StyledProperty<A2UIComponentRegistry?> RegistryProperty =
        AvaloniaProperty.Register<A2UISurfaceView, A2UIComponentRegistry?>(nameof(Registry));

    /// <summary>Shown while the surface or its root component has not arrived; null shows a skeleton.</summary>
    public static readonly StyledProperty<object?> PendingContentProperty =
        AvaloniaProperty.Register<A2UISurfaceView, object?>(nameof(PendingContent));

    /// <summary>Builds the placeholder of a child that has not arrived yet (data: <see cref="A2UIFallbackInfo"/>); null shows nothing.</summary>
    public static readonly StyledProperty<IDataTemplate?> PendingTemplateProperty =
        AvaloniaProperty.Register<A2UISurfaceView, IDataTemplate?>(nameof(PendingTemplate));

    /// <summary>Builds the placeholder of an unknown or cyclic component (data: <see cref="A2UIFallbackInfo"/>).</summary>
    public static readonly StyledProperty<IDataTemplate?> ErrorTemplateProperty =
        AvaloniaProperty.Register<A2UISurfaceView, IDataTemplate?>(nameof(ErrorTemplate));

    /// <summary>When true, unknown or cyclic components show a small error strip (useful while developing an agent).</summary>
    public static readonly StyledProperty<bool> ShowErrorPlaceholdersProperty =
        AvaloniaProperty.Register<A2UISurfaceView, bool>(nameof(ShowErrorPlaceholders));

    private A2UISurface? _shown;
    private A2UIRenderer? _listening;
    private A2UIBuildContext? _context;
    private Control? _pending;

    /// <summary>The renderer that owns the surface (with <see cref="SurfaceId"/>).</summary>
    public A2UIRenderer? Renderer
    {
        get => GetValue(RendererProperty);
        set => SetValue(RendererProperty, value);
    }

    /// <summary>The id of the surface to show; the view waits until the renderer creates it.</summary>
    public string? SurfaceId
    {
        get => GetValue(SurfaceIdProperty);
        set => SetValue(SurfaceIdProperty, value);
    }

    /// <summary>The surface to show; takes precedence over <see cref="SurfaceId"/>.</summary>
    public A2UISurface? Surface
    {
        get => GetValue(SurfaceProperty);
        set => SetValue(SurfaceProperty, value);
    }

    /// <summary>The component views to use; null uses <see cref="A2UIComponentRegistry.Default"/>.</summary>
    public A2UIComponentRegistry? Registry
    {
        get => GetValue(RegistryProperty);
        set => SetValue(RegistryProperty, value);
    }

    /// <summary>Shown while the surface or its root component has not arrived; null shows a skeleton.</summary>
    public object? PendingContent
    {
        get => GetValue(PendingContentProperty);
        set => SetValue(PendingContentProperty, value);
    }

    /// <summary>Builds the placeholder of a child that has not arrived (data: <see cref="A2UIFallbackInfo"/>); null shows nothing.</summary>
    public IDataTemplate? PendingTemplate
    {
        get => GetValue(PendingTemplateProperty);
        set => SetValue(PendingTemplateProperty, value);
    }

    /// <summary>Builds the placeholder of an unknown or cyclic component (data: <see cref="A2UIFallbackInfo"/>).</summary>
    public IDataTemplate? ErrorTemplate
    {
        get => GetValue(ErrorTemplateProperty);
        set => SetValue(ErrorTemplateProperty, value);
    }

    /// <summary>When true, unknown or cyclic components show a small error strip (useful while developing an agent).</summary>
    public bool ShowErrorPlaceholders
    {
        get => GetValue(ShowErrorPlaceholdersProperty);
        set => SetValue(ShowErrorPlaceholdersProperty, value);
    }

    public A2UISurfaceView()
    {
        // Text that sets no color of its own (Text, Icon) inherits; start from the body color, whatever the host uses.
        ArcTokens.Bind(this, Avalonia.Controls.Documents.TextElement.ForegroundProperty, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground);
    }

    /// <summary>The surface currently shown, or null.</summary>
    public A2UISurface? ShownSurface => _shown;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RendererProperty || change.Property == SurfaceIdProperty || change.Property == SurfaceProperty
            || change.Property == RegistryProperty)
        {
            Refresh();
        }
        else if (change.Property == PendingContentProperty && _shown?.Root is null)
        {
            ShowPending();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Show(null);
        Listen(null);
    }

    private void Refresh()
    {
        if (VisualRoot is null)
        {
            return;
        }

        var renderer = Surface?.Renderer ?? Renderer;
        Listen(renderer);
        var surface = Surface ?? (SurfaceId is { } id ? renderer?.GetSurface(id) : null);
        if (surface is { IsDeleted: true })
        {
            surface = null;
        }

        Show(surface);
    }

    private void Listen(A2UIRenderer? renderer)
    {
        if (ReferenceEquals(_listening, renderer))
        {
            return;
        }

        if (_listening is not null)
        {
            _listening.SurfaceCreated -= OnSurfacesChanged;
            _listening.SurfaceDeleted -= OnSurfacesChanged;
        }

        _listening = renderer;
        if (renderer is not null)
        {
            renderer.SurfaceCreated += OnSurfacesChanged;
            renderer.SurfaceDeleted += OnSurfacesChanged;
            if (renderer.UrlOpener is null && TopLevel.GetTopLevel(this) is { } top)
            {
                renderer.UrlOpener = uri => _ = top.Launcher.LaunchUriAsync(uri);
            }
        }
    }

    private void OnSurfacesChanged(object? sender, A2UISurfaceEventArgs e)
    {
        if (e.Surface.Id == SurfaceId || ReferenceEquals(e.Surface, _shown))
        {
            Refresh();
        }
    }

    private void Show(A2UISurface? surface)
    {
        if (ReferenceEquals(surface, _shown) && _context is not null)
        {
            return;
        }

        if (_shown is not null)
        {
            _shown.RootChanged -= OnRootChanged;
            _context?.ReleaseAll();
            _context = null;
        }

        _shown = surface;
        if (surface is null)
        {
            Child = null;
            return;
        }

        _context = new A2UIBuildContext(this, surface, Registry ?? A2UIComponentRegistry.Default);
        surface.RootChanged += OnRootChanged;
        ShowRoot();
    }

    private void OnRootChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ShowRoot);
            return;
        }

        ShowRoot();
    }

    private void ShowRoot()
    {
        if (_shown is null || _context is null)
        {
            return;
        }

        if (_shown.IsDeleted)
        {
            Show(null);
            return;
        }

        if (_shown.Root is { } root)
        {
            var first = !_context.IsStreaming;
            Child = _context.BuildChild(root);
            // The first tree fades in once as a whole; components that arrive later ease in one by one.
            if (first && Child is A2UINodePresenter presenter && presenter.View is not null)
            {
                presenter.PlayEnter();
            }

            _context.IsStreaming = true;
        }
        else
        {
            ShowPending();
        }
    }

    private void ShowPending()
    {
        Child = PendingContent switch
        {
            Control control => control,
            null => _pending ??= new Skeleton { Lines = 3, IsLoading = true },
            var other => new ContentControl { Content = other }
        };
    }
}
