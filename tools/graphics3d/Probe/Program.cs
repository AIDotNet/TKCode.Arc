using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc;
using TKCode.Arc.Graphics3D;

namespace TKCode.Arc.Graphics3D.Probe;

internal static partial class Program
{
    public static string OutputDirectory = "";

    [STAThread]
    public static int Main(string[] args)
    {
        // Arguments: [output-directory] [--opengl] [--glcontrol] [--software] [--bitmap] [--egl=<directory>] [--x11=glx|egl] [--wgl] [--render-thread] [--wgpu=<libwgpu_native>] [--viewer] [--snapshot]
        // [--screenshot] (macOS: this window, needs the Screen Recording permission; Linux: the X screen, needs ImageMagick). --opengl asks Avalonia's macOS backend to render with OpenGL
        // instead of its default.
        var forceOpenGl = args.Contains("--opengl");
        if (args.Contains("--glcontrol"))
        {
            // Present through OpenGlControlBase even on macOS (together with --opengl): the path Windows and Linux use.
            AppContext.SetSwitch("TKCode.Arc.Graphics3D.UseOpenGlControl", true);
        }

        // The first argument, when it is not an option.
        var directory = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : null;
        OutputDirectory = directory ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "graphics3d");
        Directory.CreateDirectory(OutputDirectory);
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out));
        if (args.Contains("--bitmap"))
        {
            // Skip the shared paths and present through the fallback: an own context, read back into a bitmap.
            AppContext.SetSwitch("TKCode.Arc.Graphics3D.UseBitmapPresenter", true);
        }

        if (Option("--egl") is { } egl)
        {
            // The fallback's context through EGL from this directory (ANGLE's libEGL and libGLESv2), as on Windows.
            AppContext.SetData("TKCode.Arc.Graphics3D.EglDirectory", egl);
        }

        var builder = AppBuilder.Configure<ProbeApp>().UsePlatformDetect();
        if (args.Contains("--software"))
        {
            // Avalonia composites on the CPU: no context or surface to share, so the viewport must fall back.
            builder = builder.With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] })
                .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
                .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        }
        else if (args.Contains("--wgl"))
        {
            // Windows: Avalonia renders with the graphics driver's own OpenGL (WGL) instead of ANGLE, so the viewport
            // shares a desktop OpenGL context and the engine emits GLSL 3.30.
            builder = builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Wgl] });
        }
        else if (args.FirstOrDefault(a => a.StartsWith("--x11=", StringComparison.Ordinal)) is { } x11)
        {
            // Linux: render through GLX or EGL only, and accept a software OpenGL (Avalonia refuses llvmpipe by
            // default), so that a machine without a GPU, such as a container under Xvfb, still shares a context.
            builder = builder.With(new X11PlatformOptions
            {
                RenderingMode = [x11.EndsWith("egl", StringComparison.Ordinal) ? X11RenderingMode.Egl : X11RenderingMode.Glx],
                GlxRendererBlacklist = [],
            });
        }
        else if (forceOpenGl)
        {
            builder = builder.With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
            });
        }

        return builder
            .LogToTrace(Avalonia.Logging.LogEventLevel.Warning)
            .UseArc(arc => arc.UseArcGraphics3D())
            .StartWithClassicDesktopLifetime(args);
    }
}

internal static partial class Program
{
    /// <summary>The value of an option given as <c>--name=value</c> or as <c>--name value</c> (the Windows command
    /// interpreter splits arguments at <c>=</c>, so a batch file passes the second form).</summary>
    public static string? Option(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.Ordinal))
            {
                return args[i][(name.Length + 1)..];
            }

            if (args[i] == name && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>Linux: what the X server shows (the whole screen of the display the probe runs on, which under Xvfb is
    /// only this window), taken with ImageMagick's <c>import</c>. Proves that the picture reaches the screen.</summary>
    public static string CaptureX11Screen(string fileName)
    {
        try
        {
            var file = Path.Combine(OutputDirectory, fileName);
            using var process = System.Diagnostics.Process.Start("import", $"-window root \"{file}\"");
            process.WaitForExit(10000);
            return $"screen capture: exit code {process.ExitCode}";
        }
        catch (Exception ex)
        {
            return "screen capture failed: " + ex.Message;
        }
    }
}

internal sealed class ProbeApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Environment.GetCommandLineArgs().Contains("--viewer") ? new ViewerWindow() : new ProbeWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class ProbeWindow : Window
{
    private readonly Viewport3D _viewport;
    private readonly StringBuilder _report = new();
    private readonly Mesh _box;
    private int _frames;
    private bool _done;
    private double _cpu;

    public ProbeWindow()
    {
        Title = "TKCode.Arc.Graphics3D probe";
        Width = 800;
        Height = 600;
        var scene = new Scene();
        var floor = new Mesh(new PlaneGeometry(12f, 12f), new StandardMaterial { Color = ColorF.FromHex(0x8a8f98), Roughness = 0.9f }) { ReceiveShadow = true };
        floor.RotateX(-MathF.PI / 2f);
        _box = new Mesh(new BoxGeometry(1f, 1f, 1f), new StandardMaterial { Color = ColorF.FromHex(0xe5484d), Roughness = 0.4f })
        {
            Position = new Vector3(-1.2f, 0.5f, 0f),
            CastShadow = true,
            ReceiveShadow = true,
        };
        var sphere = new Mesh(new SphereGeometry(0.6f, 48, 24), new StandardMaterial { Color = ColorF.FromHex(0x3e63dd), Roughness = 0.25f, Metalness = 0.1f })
        {
            Position = new Vector3(0.9f, 0.6f, 0.3f),
            CastShadow = true,
            ReceiveShadow = true,
        };
        var sun = new DirectionalLight(3f) { Position = new Vector3(3f, 5f, 2f), CastShadow = true };
        scene.Add(floor, _box, sphere, sun, new AmbientLight(0.4f), new HemisphereLight(ColorF.FromHex(0xbfd4ff), ColorF.FromHex(0x30281c), 0.6f));
        var camera = new PerspectiveCamera(45f, 4f / 3f, 0.1f, 100f) { Position = new Vector3(3.2f, 2.6f, 4.2f) };
        camera.LookAt(new Vector3(0f, 0.4f, 0f));
        _viewport = new Viewport3D
        {
            Scene = scene,
            Camera = camera,
            ToneMapping = ToneMapping.AcesFilmic,
            CameraControls = new OrbitControls(camera) { Target = new Vector3(0f, 0.4f, 0f) },
            UseRenderThread = Environment.GetCommandLineArgs().Contains("--render-thread"),
        };
        if (Program.Option("--wgpu") is { } library)
        {
            // Draw with the WebGPU backend instead of OpenGL: the path of libwgpu_native (a wgpu-native release).
            _viewport.DeviceFactory = () => TKCode.Arc.Graphics3D.Rhi.WebGpu.WgpuDevice.Create(library);
        }

        _viewport.FrameRendering += OnFrameRendering;
        _viewport.FrameRendered += OnFrameRendered;

        // A translucent Arc-side overlay on top of the viewport: proves that controls compose over the 3D picture.
        var overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(160, 20, 20, 24)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 6),
            Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = "Viewport3D inside the composition tree", Foreground = Brushes.White },
        };
        Content = new Panel { Background = new SolidColorBrush(Color.FromRgb(24, 26, 31)), Children = { _viewport, overlay } };

        // Fail loudly instead of hanging when no frame ever arrives (no OpenGL, software rendering).
        DispatcherTimer.RunOnce(() => Finish($"timeout: {_frames} frames were rendered; renderer ready: {_viewport.IsRendererReady}; error: {_viewport.RendererError ?? "none"}"), TimeSpan.FromSeconds(20));
    }

    private void OnFrameRendering(object? sender, Viewport3DFrameEventArgs e)
    {
        _box.RotateY(0.02f);
        e.RequestNextFrame = !_done;
    }

    private void OnFrameRendered(object? sender, Viewport3DFrameEventArgs e)
    {
        if (_done)
        {
            return;
        }

        _frames++;
        _cpu += e.Renderer.Statistics.CpuMilliseconds;
        if (_frames == 1)
        {
            var caps = e.Renderer.Device.Capabilities;
            _report.AppendLine($"os: {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})");
            _report.AppendLine($"vendor: {caps.Vendor}");
            _report.AppendLine($"renderer: {caps.Renderer}");
            _report.AppendLine($"version: {caps.Version}");
            _report.AppendLine($"shader language: {caps.ShaderLanguage}");
            _report.AppendLine($"max texture size: {caps.MaxTextureSize}, max samples: {caps.MaxSamples}, max anisotropy: {caps.MaxAnisotropy}");
            _report.AppendLine($"fragment texture units: {caps.MaxFragmentTextures}, uniform block size: {caps.MaxUniformBlockSize}, ubo alignment: {caps.UniformBufferOffsetAlignment}");
            _report.AppendLine($"float render targets: {caps.FloatRenderTargets}, float linear filtering: {caps.FloatLinearFiltering}");
            _report.AppendLine($"compression: bc={caps.TextureCompressionBc} etc2={caps.TextureCompressionEtc2} astc={caps.TextureCompressionAstc}");
            _report.AppendLine($"render callback on UI thread: {Dispatcher.UIThread.CheckAccess()}");
            _report.AppendLine($"presentation: {_viewport.Presentation}");
            _report.AppendLine($"render thread: {_viewport.IsRenderThreadActive}");
        }

        if (_frames == 90 && Environment.GetCommandLineArgs().Contains("--screenshot") && OperatingSystem.IsLinux())
        {
            _report.AppendLine(Program.CaptureX11Screen("probe-linux-window.png"));
        }

        if (_frames == 90 && Environment.GetCommandLineArgs().Contains("--snapshot"))
        {
            // The window's content as Avalonia's compositor draws it, surfaces of the viewport included: the same
            // compositor code that draws the window, into a bitmap. Needs no screen capture permission.
            SnapshotComposition();
        }

        if (_frames == 90 && Environment.GetCommandLineArgs().Contains("--screenshot") && OperatingSystem.IsMacOS())
        {
            // The composited window as the window server sees it (this window only): proves that the picture reaches
            // the screen and that the overlay sits on top of it. Needs the Screen Recording permission.
            CaptureOwnWindow();
        }

        if (_frames == 120)
        {
            var s = e.Renderer.Statistics;
            _report.AppendLine($"frames: {_frames}, average CPU per frame: {_cpu / _frames:0.000} ms, draw calls: {s.DrawCalls}, triangles: {s.Triangles}, shadow views: {s.ShadowViews}");
            var image = e.Renderer.ReadSurface();
            File.WriteAllBytes(Path.Combine(Program.OutputDirectory, $"probe-{OsName()}.png"), Png.Encode(image));
            _report.AppendLine($"picture: {image.Width} x {image.Height}");
            _report.AppendLine("result: ok");
            _done = true;
            Dispatcher.UIThread.Post(() => Finish(null));
        }
    }

    private async void SnapshotComposition()
    {
        try
        {
            if (Content is not Visual content || Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(content) is not { } visual)
            {
                return;
            }

            using var bitmap = await visual.Compositor.CreateCompositionVisualSnapshot(visual, RenderScaling);
            bitmap.Save(Path.Combine(Program.OutputDirectory, $"probe-{OsName()}-composition.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            _report.AppendLine($"composition snapshot: {bitmap.PixelSize.Width} x {bitmap.PixelSize.Height}");
        }
        catch (Exception ex)
        {
            _report.AppendLine("composition snapshot failed: " + ex.Message);
        }
    }

    private unsafe void CaptureOwnWindow()
    {
        try
        {
            var handle = TryGetPlatformHandle()?.Handle ?? 0;
            if (handle == 0)
            {
                return;
            }

            var objc = System.Runtime.InteropServices.NativeLibrary.Load("/usr/lib/libobjc.A.dylib");
            var msgSend = System.Runtime.InteropServices.NativeLibrary.GetExport(objc, "objc_msgSend");
            var selRegisterName = (delegate* unmanaged<byte*, nint>)System.Runtime.InteropServices.NativeLibrary.GetExport(objc, "sel_registerName");
            nint selector;
            fixed (byte* name = "windowNumber\0"u8)
            {
                selector = selRegisterName(name);
            }

            var number = ((delegate* unmanaged<nint, nint, nint>)msgSend)(handle, selector);
            var file = Path.Combine(Program.OutputDirectory, "probe-macos-window.png");
            using var process = System.Diagnostics.Process.Start("/usr/sbin/screencapture", $"-x -o -l {number} \"{file}\"");
            process.WaitForExit(5000);
            _report.AppendLine($"window screenshot: exit code {process.ExitCode}");
        }
        catch (Exception ex)
        {
            _report.AppendLine("window screenshot failed: " + ex.Message);
        }
    }

    private static string OsName() => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : "other";

    private void Finish(string? error)
    {
        if (error is not null)
        {
            if (_done)
            {
                return;
            }

            _done = true;
            _report.AppendLine("result: " + error);
        }

        File.WriteAllText(Path.Combine(Program.OutputDirectory, $"probe-{OsName()}.txt"), _report.ToString());
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(error is null ? 0 : 1);
    }
}

/// <summary>The <c>--viewer</c> mode: a <see cref="ModelViewer"/> with a model built in code; writes what its viewport drew.</summary>
internal sealed class ViewerWindow : Window
{
    private int _frames;

    public ViewerWindow()
    {
        Title = "TKCode.Arc.Graphics3D probe: ModelViewer";
        Width = 800;
        Height = 600;
        var model = new Group();
        model.Add(new Mesh(new TorusKnotGeometry(0.6f, 0.2f, 160, 24), new PhysicalMaterial
        {
            Color = ColorF.FromHex(0xc2410c),
            Roughness = 0.35f,
            Clearcoat = 1f,
            ClearcoatRoughness = 0.08f,
        })
        {
            Position = new Vector3(0f, 1.1f, 0f),
        });
        model.Add(new Mesh(new CylinderGeometry(0.9f, 1f, 0.18f, 64), new StandardMaterial { Color = ColorF.FromHex(0xd9d9de), Roughness = 0.25f, Metalness = 1f })
        {
            Position = new Vector3(0f, 0.09f, 0f),
        });
        var viewer = new ModelViewer { Model = model, Margin = new Thickness(24), UseRenderThread = Environment.GetCommandLineArgs().Contains("--render-thread") };
        Content = new Panel { Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)), Children = { viewer } };
        viewer.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Viewport3D>(ModelViewer.ViewportPart) is { } viewport)
            {
                viewport.FrameRendering += (_, args) => args.RequestNextFrame = _frames < 30;
                viewport.FrameRendered += (_, args) =>
                {
                    if (_frames + 1 == 20 && Environment.GetCommandLineArgs().Contains("--snapshot"))
                    {
                        SnapshotViewer();
                    }

                    if (++_frames == 20 && Environment.GetCommandLineArgs().Contains("--screenshot") && OperatingSystem.IsLinux())
                    {
                        Program.CaptureX11Screen("probe-viewer-window.png");
                    }

                    if (_frames == 30)
                    {
                        File.WriteAllBytes(Path.Combine(Program.OutputDirectory, "probe-viewer.png"), Png.Encode(args.Renderer.ReadSurface()));
                        Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(0));
                    }
                };
            }
        };
        DispatcherTimer.RunOnce(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(1), TimeSpan.FromSeconds(20));
    }

    private async void SnapshotViewer()
    {
        try
        {
            if (Content is Visual content && Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(content) is { } visual)
            {
                using var bitmap = await visual.Compositor.CreateCompositionVisualSnapshot(visual, RenderScaling);
                bitmap.Save(Path.Combine(Program.OutputDirectory, "probe-viewer-composition.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("composition snapshot failed: " + ex.Message);
        }
    }
}
