using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Rhi.OpenGL;
using Camera = TKCode.Arc.Graphics3D.Camera;
using ControlKey = TKCode.Arc.Graphics3D.ControlKey;
using ControlModifiers = TKCode.Arc.Graphics3D.ControlModifiers;
using OrbitControls = TKCode.Arc.Graphics3D.OrbitControls;
using PostProcessing = TKCode.Arc.Graphics3D.PostProcessing;
using PointerButton = TKCode.Arc.Graphics3D.PointerButton;
using PerspectiveCamera = TKCode.Arc.Graphics3D.PerspectiveCamera;
using Renderer = TKCode.Arc.Graphics3D.Renderer;
using RenderStatistics = TKCode.Arc.Graphics3D.RenderStatistics;
using Scene = TKCode.Arc.Graphics3D.Scene;
using TextureImage = TKCode.Arc.Graphics3D.TextureImage;
using ToneMapping = TKCode.Arc.Graphics3D.ToneMapping;

namespace TKCode.Arc;

/// <summary>When a <see cref="Viewport3D"/> draws.</summary>
public enum Viewport3DRenderMode
{
    /// <summary>Only when something changed: the scene graph, the camera, the size, a property, or after
    /// <see cref="Viewport3D.RequestRender"/>. A still scene costs nothing.</summary>
    OnDemand,

    /// <summary>On every display frame while the control is visible.</summary>
    Continuous,
}

/// <summary>How a <see cref="Viewport3D"/> gets its pictures to the screen.</summary>
public enum Viewport3DPresentation
{
    /// <summary>Not presenting: the viewport is not in a window, or no OpenGL context could be had.</summary>
    None,

    /// <summary>Drawn in Avalonia's own OpenGL context family (Windows, Linux).</summary>
    SharedContext,

    /// <summary>Drawn in the viewport's own context into surfaces the compositor imports (macOS).</summary>
    SharedSurface,

    /// <summary>Drawn in the viewport's own context, read back and shown as a bitmap: the fallback under software
    /// rendering, and the way the viewport presents in the browser (a WebGL 2 context of the page, which needs the
    /// TKCode.Arc.Graphics3D.Browser package). Slower per frame than the others.</summary>
    Bitmap,
}

/// <summary>Data of <see cref="Viewport3D.FrameRendering"/> and <see cref="Viewport3D.FrameRendered"/>.</summary>
public sealed class Viewport3DFrameEventArgs : EventArgs
{
    internal Viewport3DFrameEventArgs(Renderer renderer) => Renderer = renderer;

    /// <summary>The renderer that draws the frame.</summary>
    public Renderer Renderer { get; }

    /// <summary>Time since the previous frame; zero for the first frame after a pause.</summary>
    public TimeSpan Elapsed { get; internal set; }

    /// <summary>Set to true to draw another frame right after this one (an animation is still running).</summary>
    public bool RequestNextFrame { get; set; }
}

/// <summary>
/// Draws a <see cref="TKCode.Arc.Graphics3D.Scene"/> from a <see cref="TKCode.Arc.Graphics3D.Camera"/> inside the Avalonia
/// composition tree: other controls can sit on top of it, and it can be clipped, transformed and faded. Has no chrome of its
/// own.
/// </summary>
/// <remarks>
/// The picture is rendered with OpenGL on the UI thread: through Avalonia's own context where its compositor uses OpenGL
/// (OpenGL ES 3.0 through ANGLE on Windows, desktop OpenGL on Linux), and on macOS through a context of the viewport's own
/// whose pictures are handed to the compositor as shared surfaces, whatever Avalonia renders with there. In the browser
/// (WebAssembly) it draws in a WebGL 2 context of the page once the app has enabled TKCode.Arc.Graphics3D.Browser
/// (<c>UseArcGraphics3DBrowser()</c>), and shows the picture as a bitmap (<see cref="Viewport3DPresentation.Bitmap"/>). By default the viewport draws on demand (<see cref="Viewport3DRenderMode.OnDemand"/>): it schedules a
/// frame when the scene graph reports a change, when the camera, size or a property changes, and when
/// <see cref="RequestRender"/> is called; changes to materials, textures and lights' colors are not observed, so call
/// <see cref="RequestRender"/> after those. Animate in <see cref="FrameRendering"/> and set
/// <see cref="Viewport3DFrameEventArgs.RequestNextFrame"/> while the animation runs.
/// </remarks>
public class Viewport3D : Control
{
    /// <summary>The scene to draw.</summary>
    public static readonly StyledProperty<Scene?> SceneProperty =
        AvaloniaProperty.Register<Viewport3D, Scene?>(nameof(Scene));

    /// <summary>The camera to draw from.</summary>
    public static readonly StyledProperty<Camera?> CameraProperty =
        AvaloniaProperty.Register<Viewport3D, Camera?>(nameof(Camera));

    /// <summary>How bright scene colors are compressed into the displayable range. Default
    /// <see cref="Graphics3D.ToneMapping.Neutral"/>, which keeps base colors true.</summary>
    public static readonly StyledProperty<ToneMapping> ToneMappingProperty =
        AvaloniaProperty.Register<Viewport3D, ToneMapping>(nameof(ToneMapping), ToneMapping.Neutral);

    /// <summary>Exposure multiplier applied before tone mapping.</summary>
    public static readonly StyledProperty<double> ExposureProperty =
        AvaloniaProperty.Register<Viewport3D, double>(nameof(Exposure), 1d);

    /// <summary>Samples per pixel of multisample anti-aliasing; 0 uses the theme's value, 1 turns it off.</summary>
    public static readonly StyledProperty<int> SamplesProperty =
        AvaloniaProperty.Register<Viewport3D, int>(nameof(Samples));

    /// <summary>True draws on a render thread of the viewport's own where it can: the UI thread then only prepares
    /// each frame (animation, culling, sorting) and the graphics driver's work happens beside it. It applies when the
    /// viewport owns its OpenGL context (<see cref="Viewport3DPresentation.SharedSurface"/> and
    /// <see cref="Viewport3DPresentation.Bitmap"/>) and is read when the viewport joins a window; with
    /// <see cref="Viewport3DPresentation.SharedContext"/> the context belongs to Avalonia's renderer and the setting has
    /// no effect. <see cref="IsRenderThreadActive"/> says whether it is in use.</summary>
    public static readonly StyledProperty<bool> UseRenderThreadProperty =
        AvaloniaProperty.Register<Viewport3D, bool>(nameof(UseRenderThread));

    /// <summary>False turns all shadows off.</summary>
    public static readonly StyledProperty<bool> ShadowsProperty =
        AvaloniaProperty.Register<Viewport3D, bool>(nameof(Shadows), true);

    /// <summary>The backdrop behind the scene: a solid color brush, or null (default) for a transparent viewport that
    /// shows what is behind it. A scene background takes precedence.</summary>
    public static readonly StyledProperty<IBrush?> StageBrushProperty =
        AvaloniaProperty.Register<Viewport3D, IBrush?>(nameof(StageBrush));

    /// <summary>When the viewport draws: on demand (default) or on every display frame.</summary>
    public static readonly StyledProperty<Viewport3DRenderMode> RenderModeProperty =
        AvaloniaProperty.Register<Viewport3D, Viewport3DRenderMode>(nameof(RenderMode));

    /// <summary>True (default) keeps a perspective camera's aspect ratio equal to the viewport's.</summary>
    public static readonly StyledProperty<bool> AutoAspectProperty =
        AvaloniaProperty.Register<Viewport3D, bool>(nameof(AutoAspect), true);

    /// <summary>Camera controls that receive this viewport's pointer, wheel and arrow-key input (orbit, zoom, pan), or
    /// null (default) for a viewport that does not react to input. The viewport advances them every frame and keeps
    /// drawing while they are still moving.</summary>
    public static readonly StyledProperty<OrbitControls?> CameraControlsProperty =
        AvaloniaProperty.Register<Viewport3D, OrbitControls?>(nameof(CameraControls));

    /// <summary>What the last frame cost: objects, draw calls, triangles, CPU time.</summary>
    public static readonly DirectProperty<Viewport3D, RenderStatistics> StatisticsProperty =
        AvaloniaProperty.RegisterDirect<Viewport3D, RenderStatistics>(nameof(Statistics), static v => v.Statistics);

    /// <summary>How the pictures reach the screen; <see cref="Viewport3DPresentation.Bitmap"/> is the slower fallback
    /// used when Avalonia renders in software, and the way used in the browser.</summary>
    public static readonly DirectProperty<Viewport3D, Viewport3DPresentation> PresentationProperty =
        AvaloniaProperty.RegisterDirect<Viewport3D, Viewport3DPresentation>(nameof(Presentation), static v => v.Presentation);

    /// <summary>True while the renderer exists (the control is in a window with a working graphics context).</summary>
    public static readonly DirectProperty<Viewport3D, bool> IsRendererReadyProperty =
        AvaloniaProperty.RegisterDirect<Viewport3D, bool>(nameof(IsRendererReady), static v => v.IsRendererReady);

    /// <summary>Why the renderer could not be created or draw, or null.</summary>
    public static readonly DirectProperty<Viewport3D, string?> RendererErrorProperty =
        AvaloniaProperty.RegisterDirect<Viewport3D, string?>(nameof(RendererError), static v => v.RendererError);

    private readonly Action _onSceneChanged;
    private GlDevice? _device;
    private ThreadedGraphicsDevice? _threaded;

    // A device the application supplied (DeviceFactory), used directly on this thread.
    private GraphicsDevice? _supplied;
    private Renderer? _renderer;
    private Viewport3DFrameEventArgs? _frameArgs;
    private Scene? _subscribedScene;
    private RenderStatistics _statistics;
    private bool _isRendererReady;
    private string? _rendererError;
    private long _lastFrame;
    private Action<TextureImage>? _pendingCapture;
    private ViewportPresenter? _presenter;
    private Viewport3DPresentation _presentation;
    private int _attachGeneration;
    private Action? _onPlatformFactoryChanged;

    static Viewport3D()
    {
        FocusableProperty.OverrideDefaultValue<Viewport3D>(true);
    }

    /// <summary>Creates a viewport.</summary>
    public Viewport3D()
    {
        _onSceneChanged = RequestRender;
    }

    /// <summary>Raised on the UI thread before each frame is drawn: advance animations and move objects here.</summary>
    public event EventHandler<Viewport3DFrameEventArgs>? FrameRendering;

    /// <summary>Raised on the UI thread after each frame was drawn, while the graphics context is still current.</summary>
    public event EventHandler<Viewport3DFrameEventArgs>? FrameRendered;

    /// <summary>Raised when the renderer was created (GPU resources may be created from here on).</summary>
    public event EventHandler? RendererReady;

    /// <summary>Raised when the renderer is gone: the control left the window or the graphics context was lost.</summary>
    public event EventHandler? RendererLost;

    /// <inheritdoc cref="SceneProperty"/>
    public Scene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <inheritdoc cref="CameraProperty"/>
    public Camera? Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    /// <inheritdoc cref="ToneMappingProperty"/>
    public ToneMapping ToneMapping
    {
        get => GetValue(ToneMappingProperty);
        set => SetValue(ToneMappingProperty, value);
    }

    /// <inheritdoc cref="ExposureProperty"/>
    public double Exposure
    {
        get => GetValue(ExposureProperty);
        set => SetValue(ExposureProperty, value);
    }

    /// <inheritdoc cref="SamplesProperty"/>
    public int Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <inheritdoc cref="UseRenderThreadProperty"/>
    public bool UseRenderThread
    {
        get => GetValue(UseRenderThreadProperty);
        set => SetValue(UseRenderThreadProperty, value);
    }

    /// <summary>Creates the graphics device the viewport draws with, in place of OpenGL: for example
    /// <c>() =&gt; WgpuDevice.Create(path)</c> from <c>TKCode.Arc.Graphics3D.WebGpu</c>. The device draws off screen and the
    /// picture is read back and shown as a bitmap (<see cref="Presentation"/> reads
    /// <see cref="Viewport3DPresentation.Bitmap"/>), so it costs one read-back per frame. The viewport disposes the
    /// device. Read when the viewport joins a window; with <see cref="UseRenderThread"/> the factory runs on the render
    /// thread.</summary>
    public Func<GraphicsDevice>? DeviceFactory { get; set; }

    /// <summary>True while frames are drawn on the viewport's render thread (see <see cref="UseRenderThread"/>).</summary>
    public bool IsRenderThreadActive => _threaded is not null;

    /// <inheritdoc cref="ShadowsProperty"/>
    public bool Shadows
    {
        get => GetValue(ShadowsProperty);
        set => SetValue(ShadowsProperty, value);
    }

    /// <inheritdoc cref="StageBrushProperty"/>
    public IBrush? StageBrush
    {
        get => GetValue(StageBrushProperty);
        set => SetValue(StageBrushProperty, value);
    }

    /// <inheritdoc cref="RenderModeProperty"/>
    public Viewport3DRenderMode RenderMode
    {
        get => GetValue(RenderModeProperty);
        set => SetValue(RenderModeProperty, value);
    }

    /// <inheritdoc cref="AutoAspectProperty"/>
    public bool AutoAspect
    {
        get => GetValue(AutoAspectProperty);
        set => SetValue(AutoAspectProperty, value);
    }

    /// <inheritdoc cref="CameraControlsProperty"/>
    public OrbitControls? CameraControls
    {
        get => GetValue(CameraControlsProperty);
        set => SetValue(CameraControlsProperty, value);
    }

    /// <summary>Effects applied to the finished picture: bloom, vignette, saturation, contrast and FXAA. All are off by
    /// default. Changes are not observed: call <see cref="RequestRender"/> after changing a setting.</summary>
    public PostProcessing PostProcessing { get; } = new();

    /// <inheritdoc cref="StatisticsProperty"/>
    public RenderStatistics Statistics
    {
        get => _statistics;
        private set => SetAndRaise(StatisticsProperty, ref _statistics, value);
    }

    /// <inheritdoc cref="PresentationProperty"/>
    public Viewport3DPresentation Presentation
    {
        get => _presentation;
        private set => SetAndRaise(PresentationProperty, ref _presentation, value);
    }

    /// <inheritdoc cref="IsRendererReadyProperty"/>
    public bool IsRendererReady
    {
        get => _isRendererReady;
        private set => SetAndRaise(IsRendererReadyProperty, ref _isRendererReady, value);
    }

    /// <inheritdoc cref="RendererErrorProperty"/>
    public string? RendererError
    {
        get => _rendererError;
        private set => SetAndRaise(RendererErrorProperty, ref _rendererError, value);
    }

    /// <summary>The renderer, or null while there is none. Use it only on the UI thread, inside
    /// <see cref="FrameRendering"/> or <see cref="FrameRendered"/>.</summary>
    public Renderer? Renderer => _renderer;

    /// <summary>The theme's stage color (<see cref="Viewport3DToken.StageColor"/>) as a brush for <see cref="StageBrush"/>.</summary>
    public IBrush? ThemeStageBrush => this.TryFindResource(Viewport3DTokens.Key(Viewport3DTokenKind.StageColor), ActualThemeVariant, out var value)
        ? value as IBrush
        : null;

    /// <summary>Asks for a frame. Cheap; any number of calls before the next frame draw one frame.</summary>
    public void RequestRender()
    {
        // Presenters coalesce requests themselves. No flag is kept here: a frame that was asked for may never be drawn
        // (the viewport was hidden or had no size), and a remembered request would then block every later one.
        _presenter?.RequestFrame();
    }

    /// <summary>Draws a frame and hands its pixels (RGBA8, rows top to bottom, at device resolution) to a callback on
    /// the UI thread. Does nothing while there is no renderer.</summary>
    public void Capture(Action<TextureImage> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _pendingCapture += callback;
        RequestRender();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            SubscribeScene(IsAttachedToVisualTreeCore ? Scene : null);
            RequestRender();
        }
        else if (change.Property == CameraProperty || change.Property == CameraControlsProperty || change.Property == ToneMappingProperty || change.Property == ExposureProperty
            || change.Property == SamplesProperty || change.Property == ShadowsProperty || change.Property == StageBrushProperty
            || change.Property == RenderModeProperty || change.Property == AutoAspectProperty)
        {
            RequestRender();
        }
    }

    private static ControlModifiers Modifiers(KeyModifiers modifiers)
    {
        var result = ControlModifiers.None;
        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            result |= ControlModifiers.Shift;
        }

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            result |= ControlModifiers.Control;
        }

        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            result |= ControlModifiers.Alt;
        }

        if ((modifiers & KeyModifiers.Meta) != 0)
        {
            result |= ControlModifiers.Meta;
        }

        return result;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (CameraControls is not { } controls)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var button = point.Pointer.Type == PointerType.Touch ? PointerButton.Touch
            : point.Properties.IsMiddleButtonPressed ? PointerButton.Middle
            : point.Properties.IsRightButtonPressed ? PointerButton.Right
            : PointerButton.Left;
        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(this);
        SyncControlsViewport(controls);
        controls.PointerDown(e.Pointer.Id, (float)point.Position.X, (float)point.Position.Y, button, Modifiers(e.KeyModifiers));
        e.Handled = true;
        RequestRender();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (CameraControls is { IsInteracting: true } controls)
        {
            var position = e.GetPosition(this);
            controls.PointerMove(e.Pointer.Id, (float)position.X, (float)position.Y);
            RequestRender();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (CameraControls is { } controls)
        {
            controls.PointerUp(e.Pointer.Id);
            e.Pointer.Capture(null);
            RequestRender();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        CameraControls?.PointerCancel(e.Pointer.Id);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (CameraControls is { EnableZoom: true, Enabled: true } controls)
        {
            var position = e.GetPosition(this);
            SyncControlsViewport(controls);

            // The controls follow the browser convention: positive zooms out, about 100 per wheel notch.
            controls.Wheel((float)(-e.Delta.Y * 100d), (float)position.X, (float)position.Y);
            e.Handled = true;
            RequestRender();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || CameraControls is not { } controls)
        {
            return;
        }

        ControlKey? key = e.Key switch
        {
            Key.Left => ControlKey.Left,
            Key.Right => ControlKey.Right,
            Key.Up => ControlKey.Up,
            Key.Down => ControlKey.Down,
            _ => null,
        };
        if (key is { } k && controls.KeyDown(k, Modifiers(e.KeyModifiers)))
        {
            e.Handled = true;
            RequestRender();
        }
    }

    private void SyncControlsViewport(OrbitControls controls)
    {
        controls.ViewportWidth = (float)Bounds.Width;
        controls.ViewportHeight = (float)Bounds.Height;
    }

    private bool IsAttachedToVisualTreeCore => TopLevel.GetTopLevel(this) is not null;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeScene(Scene);
        _lastFrame = 0;
        var generation = ++_attachGeneration;

        // Diagnostic switches: present through OpenGlControlBase on macOS too (it needs Avalonia's OpenGL rendering
        // mode), or go straight to the bitmap fallback.
        var forceGlControl = AppContext.TryGetSwitch("TKCode.Arc.Graphics3D.UseOpenGlControl", out var glControl) && glControl;
        var forceBitmap = AppContext.TryGetSwitch("TKCode.Arc.Graphics3D.UseBitmapPresenter", out var bitmap) && bitmap;
        if (DeviceFactory is { } factory)
        {
            SetPresenter(new DevicePresenter(this, factory));
        }
        else if (OperatingSystem.IsBrowser())
        {
            // Avalonia.Browser draws with WebGL, but an OpenGlControlBase never starts there: its context cannot create a
            // shared one (WebGlContext.CanCreateSharedContext is false), and nothing registers the context factory the
            // control asks for next, so it logs an error and stays empty. The viewport draws in a WebGL 2 context of its
            // own, which TKCode.Arc.Graphics3D.Browser supplies; when that package loads after the viewport was attached,
            // the viewport tries again.
            _onPlatformFactoryChanged ??= OnPlatformFactoryChanged;
            OffscreenContext.PlatformFactoryChanged += _onPlatformFactoryChanged;
            SetPresenter(BitmapPresenter.TryCreate(this));
        }
        else if (forceBitmap)
        {
            SetPresenter(BitmapPresenter.TryCreate(this));
        }
        else if (OperatingSystem.IsMacOS() && !forceGlControl)
        {
            ResolveMacPresenter(generation);
        }
        else if (forceGlControl)
        {
            SetPresenter(new GlControlPresenter(this));
        }
        else
        {
            ResolveSharedPresenter(generation);
        }
    }

    // Windows and Linux: OpenGlControlBase draws in a context of Avalonia's renderer, which exists only when the
    // compositor can share OpenGL textures or import GPU images. When it can do neither (software rendering; on Linux
    // Avalonia also refuses software OpenGL drivers), OpenGlControlBase would wait forever without a word, so ask
    // first and go straight to the viewport's own context and a bitmap.
    private async void ResolveSharedPresenter(int generation)
    {
        var shares = false;
        try
        {
            var compositor = Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(this)?.Compositor;
            if (compositor is not null)
            {
                shares = await compositor.TryGetRenderInterfaceFeature(typeof(Avalonia.OpenGL.IOpenGlTextureSharingRenderInterfaceContextFeature)) is not null
                    || await compositor.TryGetCompositionGpuInterop() is not null;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            shares = false;
        }

        if (generation == _attachGeneration)
        {
            SetPresenter(shares ? new GlControlPresenter(this) : (ViewportPresenter?)BitmapPresenter.TryCreate(this) ?? new GlControlPresenter(this));
        }
    }

    private void OnPlatformFactoryChanged()
    {
        if (_presenter is null && IsAttachedToVisualTreeCore)
        {
            SetPresenter(BitmapPresenter.TryCreate(this));
        }
    }

    /// <summary>Starts over with a new context after the presenter's own was lost. Called by the presenter.</summary>
    internal void ReplaceLostPresenter(ViewportPresenter presenter)
    {
        if (ReferenceEquals(_presenter, presenter))
        {
            SetPresenter(BitmapPresenter.TryCreate(this));
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attachGeneration++;
        if (_onPlatformFactoryChanged is not null)
        {
            OffscreenContext.PlatformFactoryChanged -= _onPlatformFactoryChanged;
        }

        SubscribeScene(null);
        var presenter = _presenter;
        _presenter = null;
        presenter?.Detach();
        Presentation = Viewport3DPresentation.None;
        base.OnDetachedFromVisualTree(e);
    }

    // On macOS Avalonia's compositor normally renders with Metal, where no OpenGL context can be shared with it. The
    // viewport then renders with a context of its own into IOSurfaces that the compositor imports. That path also works
    // when the application switched Avalonia to OpenGL, so it is the only one used on macOS when it is available.
    private async void ResolveMacPresenter(int generation)
    {
        ViewportPresenter? presenter = null;
        try
        {
            var compositor = Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(this)?.Compositor;
            if (compositor is not null)
            {
                var interop = await compositor.TryGetCompositionGpuInterop();
                if (generation != _attachGeneration)
                {
                    return;
                }

                if (interop is not null)
                {
                    presenter = MacSurfacePresenter.TryCreate(this, compositor, interop);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or EntryPointNotFoundException or DllNotFoundException)
        {
            presenter = null;
        }

        if (generation == _attachGeneration)
        {
            // No surface sharing means Avalonia renders in software here (or its renderer shares nothing): there is no
            // context for OpenGlControlBase either, so go to the viewport's own context and a bitmap.
            SetPresenter(presenter ?? (ViewportPresenter?)BitmapPresenter.TryCreate(this) ?? new GlControlPresenter(this));
        }
    }

    private void SetPresenter(ViewportPresenter? presenter)
    {
        var old = _presenter;
        _presenter = null;
        old?.Detach();
        Presentation = Viewport3DPresentation.None;
        if (presenter is null)
        {
            if (IsAttachedToVisualTreeCore)
            {
                RendererError = !OperatingSystem.IsBrowser() ? NoContextMessage
                    : OffscreenContext.PlatformFactory is null ? NoBrowserBackendMessage
                    : NoWebGlMessage;
            }

            InvalidateVisual();
            return;
        }

        try
        {
            presenter.Attach();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or EntryPointNotFoundException or DllNotFoundException)
        {
            RendererError = ex.Message;
            presenter.Detach();
            return;
        }

        _presenter = presenter;
        Presentation = presenter switch
        {
            GlControlPresenter => Viewport3DPresentation.SharedContext,
            MacSurfacePresenter => Viewport3DPresentation.SharedSurface,
            _ => Viewport3DPresentation.Bitmap,
        };
        InvalidateArrange();
        RequestRender();
        if (presenter is GlControlPresenter)
        {
            // OpenGlControlBase gives no sign when it cannot get a context (software rendering, a renderer that shares
            // none). Give it a moment to start; when it has not, fall back to the viewport's own context and a bitmap.
            var generation = _attachGeneration;
            Avalonia.Threading.DispatcherTimer.RunOnce(
                () =>
                {
                    if (generation == _attachGeneration && !IsRendererReady && ReferenceEquals(_presenter, presenter))
                    {
                        SetPresenter(BitmapPresenter.TryCreate(this));
                    }
                },
                TimeSpan.FromSeconds(2));
        }
    }

    private const string NoContextMessage =
        "No OpenGL context is available: Avalonia is rendering in software and no off-screen OpenGL context could be created.";

    private const string NoBrowserBackendMessage =
        "In the browser the viewport draws through the TKCode.Arc.Graphics3D.Browser package: add UseArcGraphics3DBrowser() to UseArc(...).";

    private const string NoWebGlMessage = "No WebGL 2 context is available in this browser.";

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _presenter?.Measure(availableSize);
        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _presenter?.Arrange(finalSize);
        RequestRender();
        return finalSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // Nothing visible: the transparent fill makes the whole viewport take pointer input.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        _presenter?.Draw(context, Bounds.Size);
    }

    internal void AddPresenterChild(Control child) => VisualChildren.Add(child);

    internal void RemovePresenterChild(Control child) => VisualChildren.Remove(child);

    private void SubscribeScene(Scene? scene)
    {
        if (ReferenceEquals(_subscribedScene, scene))
        {
            return;
        }

        if (_subscribedScene is not null)
        {
            _subscribedScene.Changed -= _onSceneChanged;
        }

        _subscribedScene = scene;
        if (scene is not null)
        {
            scene.Changed += _onSceneChanged;
        }
    }

    /// <summary>Creates the renderer over the graphics context that is current. Called by the presenter.</summary>
    internal bool InitRenderer(Func<string, nint> getProcAddress)
    {
        try
        {
            _device = new GlDevice(getProcAddress);
            _renderer = new Renderer(_device);
            _frameArgs = new Viewport3DFrameEventArgs(_renderer);
            _device.RestoreHostState();
            RendererError = null;
            IsRendererReady = true;
            RendererReady?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            // The context is older than OpenGL ES 3.0 / OpenGL 3.3: nothing to draw with.
            _device = null;
            _renderer = null;
            RendererError = ex.Message;
            return false;
        }
    }

    /// <summary>Creates the renderer over a device the application supplied; the viewport owns it from here on.</summary>
    internal bool InitRenderer(GraphicsDevice device)
    {
        try
        {
            _supplied = device;
            _renderer = new Renderer(device);
            _frameArgs = new Viewport3DFrameEventArgs(_renderer);
            RendererError = null;
            IsRendererReady = true;
            RendererReady?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            _supplied = null;
            _renderer = null;
            RendererError = ex.Message;
            return false;
        }
    }

    /// <summary>Creates the renderer over a device that lives on a render thread of the viewport's own.</summary>
    /// <param name="createDevice">Runs on the render thread: makes the context current there and returns the device.</param>
    /// <param name="destroyed">Runs on the render thread after the device was disposed.</param>
    internal bool InitRendererOnThread(Func<GraphicsDevice> createDevice, Action destroyed)
    {
        try
        {
            _threaded = new ThreadedGraphicsDevice(createDevice, destroyed, "Arc Viewport3D render thread");
            _renderer = new Renderer(_threaded);
            _frameArgs = new Viewport3DFrameEventArgs(_renderer);
            RendererError = null;
            IsRendererReady = true;
            RendererReady?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            _threaded = null;
            _renderer = null;
            RendererError = ex.Message;
            return false;
        }
    }

    /// <summary>The device on the render thread, when there is one: presenters post their own work to it.</summary>
    internal ThreadedGraphicsDevice? RenderThreadDevice => _threaded;

    /// <summary>Drops the renderer. With <paramref name="contextLost"/> no call is made on the (gone) context.</summary>
    internal void DisposeRenderer(bool contextLost)
    {
        if (_renderer is null)
        {
            return;
        }

        if (contextLost)
        {
            _renderer.Abandon();
        }
        else if (_threaded is not null)
        {
            // Releases are recorded like everything else; disposing the device plays them and ends the thread.
            try
            {
                _renderer.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // The render thread had failed: there is nothing left to release on it.
            }

            _threaded.Dispose();
        }
        else if (_supplied is not null)
        {
            _renderer.Dispose();
            _supplied.Dispose();
        }
        else
        {
            _device!.BeginFrame();
            _renderer.Dispose();
            _device.Dispose();
        }

        _renderer = null;
        _device = null;
        _threaded = null;
        _supplied = null;
        _frameArgs = null;
        _lastFrame = 0;
        IsRendererReady = false;
        RendererLost?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The size of the viewport in device pixels.</summary>
    internal PixelSize PixelSize => PixelSize.FromSize(Bounds.Size, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d);

    /// <summary>Draws one frame into a framebuffer of the current context. Called by the presenter.</summary>
    internal void RenderFrame(int fb, PixelSize size)
    {
        var renderer = _renderer;
        var device = _device;
        if (renderer is null || (device is null && _threaded is null && _supplied is null))
        {
            return;
        }

        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var args = _frameArgs!;
        var now = Stopwatch.GetTimestamp();
        args.Elapsed = _lastFrame == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(_lastFrame, now);
        args.RequestNextFrame = false;
        _lastFrame = now;
        try
        {
            // Avalonia's own renderer shares the context: start from a known state and hand a clean one back.
            // A render thread has its context to itself.
            device?.BeginFrame();
            device?.InvalidateState();
            renderer.SetSurface(fb, size.Width, size.Height);
            renderer.ToneMapping = ToneMapping;
            renderer.Exposure = (float)Exposure;
            renderer.ShadowsEnabled = Shadows;
            renderer.PostProcessing = PostProcessing;
            var samples = Samples;
            if (samples <= 0)
            {
                samples = 4;
                if (this.TryFindResource(Viewport3DTokens.Key(Viewport3DTokenKind.Samples), ActualThemeVariant, out var value) && value is int themed)
                {
                    samples = themed;
                }
            }

            renderer.Samples = Math.Max(1, samples);
            if (StageBrush is ISolidColorBrush { Color: var stage })
            {
                var opacity = stage.A / 255f * (float)StageBrush.Opacity;
                renderer.ClearColor = Graphics3D.ColorF.FromSrgb(stage.R, stage.G, stage.B);
                renderer.ClearAlpha = opacity;
            }
            else
            {
                renderer.ClearColor = Graphics3D.ColorF.Black;
                renderer.ClearAlpha = 0f;
            }

            if (CameraControls is { } controls)
            {
                SyncControlsViewport(controls);
                if (controls.Update((float)args.Elapsed.TotalSeconds))
                {
                    args.RequestNextFrame = true;
                }
            }

            FrameRendering?.Invoke(this, args);
            var scene = Scene;
            var camera = Camera;
            if (scene is null || camera is null)
            {
                renderer.Clear();
            }
            else
            {
                if (AutoAspect && camera is PerspectiveCamera perspective)
                {
                    var aspect = (float)size.Width / size.Height;
                    if (MathF.Abs(perspective.Aspect - aspect) > 1e-5f)
                    {
                        perspective.Aspect = aspect;
                    }
                }

                renderer.Render(scene, camera);
                Statistics = renderer.Statistics;
            }

            FrameRendered?.Invoke(this, args);
            if (_pendingCapture is { } capture)
            {
                _pendingCapture = null;
                capture(renderer.ReadSurface());
            }

            if (RendererError is not null)
            {
                RendererError = null;
            }
        }
        catch (Exception ex) when (ex is Graphics3D.Rhi.ShaderCompilationException or InvalidOperationException or NotSupportedException or ObjectDisposedException)
        {
            // A broken material or resource must not take the window down with it: report and stop drawing until asked again.
            RendererError = ex.Message;
            args.RequestNextFrame = false;
        }
        finally
        {
            device?.RestoreHostState();
        }

        _lastFrame = Stopwatch.GetTimestamp();
        if (RenderMode == Viewport3DRenderMode.Continuous || args.RequestNextFrame)
        {
            RequestRender();
        }
        else
        {
            // The clock restarts with the next requested frame, so a pause is not seen as one long step.
            _lastFrame = 0;
        }
    }
}
