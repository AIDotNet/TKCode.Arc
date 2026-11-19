using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Graphics3D;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Graphics3D;

/// <summary>The 3D controls without a graphics context (headless): properties, input routing and the viewer's logic.
/// Real rendering is covered by TKCode.Arc.Graphics3D.Tests and by tools/graphics3d/Probe.</summary>
public class Viewport3DTests
{
    [AvaloniaFact]
    public void Viewport_Has_Sensible_Defaults_And_Falls_Back_Or_Says_Why()
    {
        var viewport = new Viewport3D { Width = 300, Height = 200 };
        viewport.ToneMapping.ShouldBe(ToneMapping.Neutral);
        viewport.RenderMode.ShouldBe(Viewport3DRenderMode.OnDemand);
        viewport.Shadows.ShouldBeTrue();
        viewport.AutoAspect.ShouldBeTrue();
        viewport.StageBrush.ShouldBeNull();
        viewport.Focusable.ShouldBeTrue();
        viewport.Presentation.ShouldBe(Viewport3DPresentation.None);
        viewport.PostProcessing.IsActive.ShouldBeFalse();

        // Asking for frames before there is a renderer is harmless.
        viewport.Scene = new Scene();
        viewport.Camera = new PerspectiveCamera();
        viewport.RequestRender();
        TestHelpers.Show(viewport);
        viewport.Bounds.Width.ShouldBe(300);

        // The headless platform composites in software and shares no OpenGL context. The viewport then either finds an
        // off-screen context of its own and presents through a bitmap, or says why there is no picture.
        TestHelpers.PumpUntil(() => viewport.IsRendererReady || viewport.RendererError is not null, 8000).ShouldBeTrue();
        if (viewport.IsRendererReady)
        {
            viewport.Presentation.ShouldBe(Viewport3DPresentation.Bitmap);
            viewport.Renderer.ShouldNotBeNull();
        }
        else
        {
            viewport.RendererError.ShouldNotBeNull().ShouldContain("OpenGL");
            viewport.Renderer.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public void The_Bitmap_Fallback_Draws_The_Scene()
    {
        // Runs where an off-screen OpenGL context exists (macOS, or a machine with EGL); elsewhere there is nothing to see.
        var scene = new Scene { Background = ColorF.FromSrgb(0f, 0f, 1f) };
        scene.Add(new Mesh(new SphereGeometry(1f), new BasicMaterial { Color = ColorF.FromSrgb(1f, 0f, 0f) }));
        var camera = new OrthographicCamera(-2f, 2f, 2f, -2f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };
        var viewport = new Viewport3D { Width = 200, Height = 200, Scene = scene, Camera = camera, ToneMapping = ToneMapping.None, AutoAspect = false };
        TestHelpers.Show(viewport);
        TestHelpers.PumpUntil(() => viewport.IsRendererReady || viewport.RendererError is not null, 8000).ShouldBeTrue();
        Assert.SkipUnless(viewport.IsRendererReady, "No off-screen OpenGL context on this machine.");

        TextureImage? captured = null;
        viewport.Capture(image => captured = image);
        TestHelpers.PumpUntil(() => captured is not null, 8000).ShouldBeTrue();
        var data = (byte[])captured!.Data;
        byte At(int x, int y, int channel) => data[(((y * captured.Width) + x) * 4) + channel];
        var cx = captured.Width / 2;
        var cy = captured.Height / 2;
        At(cx, cy, 0).ShouldBeGreaterThan((byte)240);
        At(cx, cy, 2).ShouldBeLessThan((byte)15);
        At(4, 4, 2).ShouldBeGreaterThan((byte)240);
        viewport.Statistics.DrawCalls.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Render_Thread_Draws_The_Same_Picture()
    {
        var scene = new Scene { Background = ColorF.FromSrgb(0f, 0f, 1f) };
        scene.Add(new Mesh(new SphereGeometry(1f), new BasicMaterial { Color = ColorF.FromSrgb(1f, 0f, 0f) }));
        var camera = new OrthographicCamera(-2f, 2f, 2f, -2f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };
        var viewport = new Viewport3D
        {
            Width = 200,
            Height = 200,
            Scene = scene,
            Camera = camera,
            ToneMapping = ToneMapping.None,
            AutoAspect = false,
            UseRenderThread = true,
        };
        viewport.IsRenderThreadActive.ShouldBeFalse();
        TestHelpers.Show(viewport);
        TestHelpers.PumpUntil(() => viewport.IsRendererReady || viewport.RendererError is not null, 8000).ShouldBeTrue();
        Assert.SkipUnless(viewport.IsRendererReady, "No off-screen OpenGL context on this machine.");

        // The headless platform composites in software, so the viewport owns its context and may give it to a thread.
        viewport.Presentation.ShouldBe(Viewport3DPresentation.Bitmap);
        viewport.IsRenderThreadActive.ShouldBeTrue();
        TextureImage? captured = null;
        viewport.Capture(image => captured = image);
        TestHelpers.PumpUntil(() => captured is not null, 8000).ShouldBeTrue();
        var data = (byte[])captured!.Data;
        byte At(int x, int y, int channel) => data[(((y * captured.Width) + x) * 4) + channel];
        At(captured.Width / 2, captured.Height / 2, 0).ShouldBeGreaterThan((byte)240);
        At(4, 4, 2).ShouldBeGreaterThan((byte)240);
        viewport.RendererError.ShouldBeNull();

        // A few more frames cross to the thread and back, then the viewport leaves the window and the thread ends.
        var frames = 0;
        viewport.FrameRendered += (_, _) => frames++;
        for (var i = 1; i <= 5; i++)
        {
            viewport.RequestRender();
            TestHelpers.PumpUntil(() => frames >= i, 4000).ShouldBeTrue();
        }

        ((Window)TopLevel.GetTopLevel(viewport)!).Content = null;
        viewport.IsRenderThreadActive.ShouldBeFalse();
        viewport.IsRendererReady.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Device_Factory_That_Fails_Is_Reported_Not_Thrown()
    {
        // The factory replaces OpenGL altogether (for example with the WebGPU backend); when it cannot make a device, the
        // viewport says why and stays empty. Drawing through a real supplied device is checked by the Probe (--wgpu).
        var calls = 0;
        var viewport = new Viewport3D
        {
            Width = 200,
            Height = 200,
            Scene = new Scene(),
            Camera = new PerspectiveCamera(),
            DeviceFactory = () =>
            {
                calls++;
                throw new NotSupportedException("no such graphics library");
            },
        };
        TestHelpers.Show(viewport);
        calls.ShouldBe(1);
        viewport.IsRendererReady.ShouldBeFalse();
        viewport.Presentation.ShouldBe(Viewport3DPresentation.None);
        viewport.RendererError.ShouldBe("no such graphics library");
        viewport.RequestRender();
        TestHelpers.PumpUntil(() => true);
    }

    [AvaloniaFact]
    public void Viewport_Tokens_Are_Registered()
    {
        var viewport = new Viewport3D { Width = 100, Height = 100 };
        TestHelpers.Show(viewport);
        viewport.TryFindResource(Viewport3DTokens.Key(Viewport3DTokenKind.Samples), out var samples).ShouldBeTrue();
        samples.ShouldBe(4);
        viewport.ThemeStageBrush.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Pointer_Wheel_And_Keys_Drive_The_Camera_Controls()
    {
        var camera = new PerspectiveCamera(50f, 1f, 0.1f, 100f) { Position = new Vector3(0f, 0f, 10f) };
        var controls = new OrbitControls(camera) { EnableDamping = false };
        var viewport = new Viewport3D { Width = 400, Height = 400, Camera = camera, Scene = new Scene(), CameraControls = controls };
        var window = TestHelpers.Show(viewport);
        var origin = viewport.TranslatePoint(default, window)!.Value;

        var azimuth = controls.AzimuthAngle;
        window.MouseDown(origin + new Point(200, 200), MouseButton.Left);
        window.MouseMove(origin + new Point(260, 200));
        window.MouseUp(origin + new Point(260, 200), MouseButton.Left);
        controls.Update(0.016f);
        controls.AzimuthAngle.ShouldNotBe(azimuth);

        var distance = controls.Distance;
        window.MouseWheel(origin + new Point(200, 200), new Avalonia.Vector(0, 1));
        controls.Update(0.016f);
        controls.Distance.ShouldBeLessThan(distance);

        var target = controls.Target;
        viewport.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        controls.Update(0.016f);
        controls.Target.ShouldNotBe(target);
    }

    [AvaloniaFact]
    public void Viewport_Without_Controls_Ignores_Input()
    {
        var camera = new PerspectiveCamera { Position = new Vector3(0f, 0f, 10f) };
        var viewport = new Viewport3D { Width = 200, Height = 200, Camera = camera, Scene = new Scene() };
        var window = TestHelpers.Show(viewport);
        var origin = viewport.TranslatePoint(default, window)!.Value;
        window.MouseDown(origin + new Point(100, 100), MouseButton.Left);
        window.MouseMove(origin + new Point(150, 100));
        window.MouseUp(origin + new Point(150, 100), MouseButton.Left);
        window.MouseWheel(origin + new Point(100, 100), new Avalonia.Vector(0, 1));
        camera.Position.ShouldBe(new Vector3(0f, 0f, 10f));
    }
}

public class ModelViewerTests
{
    private static Mesh Box(float size = 2f) => new(new BoxGeometry(size, size, size), new StandardMaterial());

    [AvaloniaFact]
    public void Starts_Empty_With_Its_Parts()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300 };
        var window = TestHelpers.Show(viewer);
        TestHelpers.Descendant<Viewport3D>(viewer).Scene.ShouldBe(viewer.Scene);
        viewer.Classes.Contains(ModelViewer.EmptyPseudoClass).ShouldBeTrue();
        TestHelpers.Descendant<StackPanel>(viewer, "PART_Toolbar").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<ArcSpinner>(viewer, "PART_Spinner").IsVisible.ShouldBeFalse();
        viewer.Bounds.Height.ShouldBe(300);
        window.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Setting_A_Model_Frames_It_And_Shows_The_Toolbar()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300 };
        TestHelpers.Show(viewer);
        var loaded = 0;
        viewer.ModelLoaded += (_, _) => loaded++;
        var box = Box();
        box.Position = new Vector3(5f, 1f, -3f);

        viewer.Model = box;

        loaded.ShouldBe(1);
        viewer.Classes.Contains(ModelViewer.EmptyPseudoClass).ShouldBeFalse();
        TestHelpers.Descendant<StackPanel>(viewer, "PART_Toolbar").IsVisible.ShouldBeTrue();
        Vector3.Distance(viewer.CameraControls.Target, new Vector3(5f, 1f, -3f)).ShouldBeLessThan(1e-3f);

        // The whole box is in view: every corner projects inside the frame.
        viewer.Camera.Aspect = 400f / 300f;
        foreach (var corner in new[] { new Vector3(4f, 0f, -4f), new Vector3(6f, 2f, -2f), new Vector3(4f, 2f, -2f), new Vector3(6f, 0f, -4f) })
        {
            var ndc = viewer.Camera.Project(corner);
            MathF.Abs(ndc.X).ShouldBeLessThan(1f);
            MathF.Abs(ndc.Y).ShouldBeLessThan(1f);
        }

        // The ground sits under the model and catches its shadow.
        var ground = viewer.Scene.Find("ground")!;
        ground.Visible.ShouldBeTrue();
        ground.Position.Y.ShouldBe(0f, 1e-4f);
        box.CastShadow.ShouldBeTrue();

        viewer.ShowToolbar = false;
        TestHelpers.Descendant<StackPanel>(viewer, "PART_Toolbar").IsVisible.ShouldBeFalse();
        viewer.Model = null;
        viewer.Classes.Contains(ModelViewer.EmptyPseudoClass).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Toolbar_Buttons_Toggle_Rotation_And_Reset_The_View()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300, Model = Box() };
        TestHelpers.Show(viewer);
        var rotate = TestHelpers.Descendant<AvaloniaButton>(viewer, ModelViewer.AutoRotateButtonPart);
        var reset = TestHelpers.Descendant<AvaloniaButton>(viewer, ModelViewer.ResetButtonPart);
        Avalonia.Automation.AutomationProperties.GetName(rotate).ShouldBe("Auto-rotate");
        Avalonia.Automation.AutomationProperties.GetName(reset).ShouldBe("Reset view");

        rotate.Focus();
        var window = (Window)TopLevel.GetTopLevel(viewer)!;
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        viewer.AutoRotate.ShouldBeTrue();
        viewer.Classes.Contains(ModelViewer.AutoRotatePseudoClass).ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        viewer.AutoRotate.ShouldBeFalse();

        var home = viewer.Camera.Position;
        viewer.CameraControls.RotateLeft(1f);
        viewer.CameraControls.Update(0.016f);
        viewer.Camera.Position.ShouldNotBe(home);
        reset.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        viewer.CameraControls.Update(0.016f);
        Vector3.Distance(viewer.Camera.Position, home).ShouldBeLessThan(1e-3f);

        viewer.ResetViewLabel = "Recenter";
        Avalonia.Automation.AutomationProperties.GetName(reset).ShouldBe("Recenter");
    }

    [AvaloniaFact]
    public void Built_In_Text_Follows_The_Language()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300, Model = Box() };
        TestHelpers.Show(viewer);
        var rotate = TestHelpers.Descendant<AvaloniaButton>(viewer, ModelViewer.AutoRotateButtonPart);
        var reset = TestHelpers.Descendant<AvaloniaButton>(viewer, ModelViewer.ResetButtonPart);
        Avalonia.Automation.AutomationProperties.GetName(viewer).ShouldBe("3D model viewer");
        var languages = TKCode.Arc.Localization.ArcLanguageManager.Current!;
        try
        {
            languages.Set(TKCode.Arc.Localization.LanguageTags.ZhCN);
            Avalonia.Automation.AutomationProperties.GetName(rotate).ShouldBe("自动旋转");
            Avalonia.Automation.AutomationProperties.GetName(reset).ShouldBe("重置视图");
            Avalonia.Automation.AutomationProperties.GetName(viewer).ShouldBe("3D 模型查看器");
            languages.Set(TKCode.Arc.Localization.LanguageTags.ZhTW);
            Avalonia.Automation.AutomationProperties.GetName(reset).ShouldBe("重設檢視");

            // A label the app set wins in every language.
            viewer.AutoRotateLabel = "Spin";
            Avalonia.Automation.AutomationProperties.GetName(rotate).ShouldBe("Spin");
        }
        finally
        {
            languages.Set(TKCode.Arc.Localization.LanguageTags.EnUS);
        }

        Avalonia.Automation.AutomationProperties.GetName(reset).ShouldBe("Reset view");
    }

    [AvaloniaFact]
    public void A_Missing_File_Reports_An_Error()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300 };
        TestHelpers.Show(viewer);
        Exception? failure = null;
        viewer.LoadFailed += (_, e) => failure = e.Exception;

        viewer.Source = Path.Combine(Path.GetTempPath(), "tkcode-arc-no-such-model.glb");

        TestHelpers.PumpUntil(() => failure is not null).ShouldBeTrue();
        viewer.IsLoading.ShouldBeFalse();
        viewer.ErrorMessage.ShouldNotBeNullOrEmpty();
        viewer.Classes.Contains(ModelViewer.ErrorPseudoClass).ShouldBeTrue();
        TestHelpers.Descendant<StackPanel>(viewer, "PART_Error").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Gltf_Stream_Loads_And_Is_Shown()
    {
        // One triangle: three float vec3 positions in an embedded buffer.
        var positions = new float[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f };
        var bytes = new byte[positions.Length * 4];
        Buffer.BlockCopy(positions, 0, bytes, 0, bytes.Length);
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "scene": 0,
              "scenes": [ { "nodes": [ 0 ] } ],
              "nodes": [ { "mesh": 0, "name": "triangle" } ],
              "meshes": [ { "primitives": [ { "attributes": { "POSITION": 0 } } ] } ],
              "accessors": [ { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0, 0, 0], "max": [1, 1, 0] } ],
              "bufferViews": [ { "buffer": 0, "byteLength": {{bytes.Length}} } ],
              "buffers": [ { "byteLength": {{bytes.Length}}, "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String(bytes)}}" } ]
            }
            """;
        var viewer = new ModelViewer { Width = 400, Height = 300 };
        TestHelpers.Show(viewer);
        var loaded = false;
        viewer.ModelLoaded += (_, _) => loaded = true;

        var task = viewer.LoadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();
        task.IsCompletedSuccessfully.ShouldBeTrue();
        loaded.ShouldBeTrue();
        viewer.ErrorMessage.ShouldBeNull();
        viewer.Scene.Find("triangle").ShouldNotBeNull();
        Vector3.Distance(viewer.CameraControls.Target, new Vector3(0.5f, 0.5f, 0f)).ShouldBeLessThan(1e-3f);
    }

    [AvaloniaFact]
    public void A_Model_Loads_From_An_Application_Resource_With_Its_External_Buffer()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300 };
        TestHelpers.Show(viewer);
        var loaded = false;
        Exception? failure = null;
        viewer.ModelLoaded += (_, _) => loaded = true;
        viewer.LoadFailed += (_, e) => failure = e.Exception;

        viewer.Source = "avares://TKCode.Arc.Extras.Tests/Graphics3D/Assets/triangle.gltf";

        TestHelpers.PumpUntil(() => loaded || failure is not null, 8000).ShouldBeTrue();
        failure.ShouldBeNull();
        viewer.Scene.Find("triangle").ShouldNotBeNull();

        // A resource that is not there fails like a missing file.
        loaded = false;
        viewer.Source = "avares://TKCode.Arc.Extras.Tests/Graphics3D/Assets/none.glb";
        TestHelpers.PumpUntil(() => loaded || failure is not null, 8000).ShouldBeTrue();
        failure.ShouldBeOfType<FileNotFoundException>();
        viewer.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void Replacing_A_Load_In_Flight_Ends_With_The_New_Source_And_No_Exception()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300 };
        TestHelpers.Show(viewer);
        Exception? failure = null;
        viewer.LoadFailed += (_, e) => failure = e.Exception;

        // The first load is cancelled by the second source; its continuation must not touch the disposed source.
        viewer.Source = "avares://TKCode.Arc.Extras.Tests/Graphics3D/Assets/triangle.gltf";
        viewer.Source = "avares://TKCode.Arc.Extras.Tests/Graphics3D/Assets/none.glb";

        TestHelpers.PumpUntil(() => failure is not null, 8000).ShouldBeTrue();
        failure.ShouldBeOfType<FileNotFoundException>();
        viewer.IsLoading.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Model_Loads_From_A_Web_Address_With_Its_External_Buffer()
    {
        // A local server with the model and its buffer side by side; the port is whichever is free.
        var folder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Graphics3D", "Assets");
        var gltf = File.ReadAllBytes(Path.Combine(folder, "triangle.gltf"));
        var bin = File.ReadAllBytes(Path.Combine(folder, "triangle.bin"));
        System.Net.HttpListener? listener = null;
        var prefix = "";
        for (var port = 39417; port < 39437 && listener is null; port++)
        {
            var candidate = new System.Net.HttpListener();
            prefix = $"http://127.0.0.1:{port}/";
            candidate.Prefixes.Add(prefix);
            try
            {
                candidate.Start();
                listener = candidate;
            }
            catch (System.Net.HttpListenerException)
            {
                candidate.Close();
            }
        }

        Assert.SkipWhen(listener is null, "No local port could be opened.");
        var requested = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var serving = Task.Run(async () =>
        {
            while (listener!.IsListening)
            {
                System.Net.HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception ex) when (ex is System.Net.HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                var path = context.Request.Url!.AbsolutePath;
                requested.Enqueue(path);
                var body = path switch
                {
                    "/models/triangle.gltf" => gltf,
                    "/models/triangle.bin" => bin,
                    _ => null,
                };
                context.Response.StatusCode = body is null ? 404 : 200;
                if (body is not null)
                {
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body);
                }

                context.Response.Close();
            }
        });

        try
        {
            var viewer = new ModelViewer { Width = 400, Height = 300 };
            TestHelpers.Show(viewer);
            var loaded = false;
            Exception? failure = null;
            viewer.ModelLoaded += (_, _) => loaded = true;
            viewer.LoadFailed += (_, e) => failure = e.Exception;

            viewer.Source = prefix + "models/triangle.gltf";

            TestHelpers.PumpUntil(() => loaded || failure is not null, 15000).ShouldBeTrue();
            failure.ShouldBeNull();
            viewer.Scene.Find("triangle").ShouldNotBeNull();
            requested.ShouldContain("/models/triangle.bin");

            // An address that answers 404 is reported, not thrown.
            loaded = false;
            viewer.Source = prefix + "models/none.glb";
            TestHelpers.PumpUntil(() => loaded || failure is not null, 15000).ShouldBeTrue();
            failure.ShouldBeOfType<HttpRequestException>();
            viewer.ErrorMessage.ShouldNotBeNullOrEmpty();

            // Neither is an address the viewer has no way to read.
            failure = null;
            viewer.Source = "ftp://127.0.0.1/model.glb";
            TestHelpers.PumpUntil(() => failure is not null, 8000).ShouldBeTrue();
            failure.ShouldBeOfType<NotSupportedException>();
        }
        finally
        {
            listener!.Close();
        }
    }

    [AvaloniaFact]
    public void Lighting_Settings_Reach_The_Scene()
    {
        var viewer = new ModelViewer { Width = 400, Height = 300, Model = Box() };
        TestHelpers.Show(viewer);
        viewer.Scene.Environment.ShouldNotBeNull();
        viewer.Environment = ModelViewerEnvironment.None;
        viewer.Scene.Environment.ShouldBeNull();
        viewer.Environment = ModelViewerEnvironment.Outdoor;
        viewer.Scene.Environment.ShouldNotBeNull();
        viewer.EnvironmentIntensity = 0.5;
        viewer.Scene.EnvironmentIntensity.ShouldBe(0.5f);
        viewer.ShadowIntensity = 0;
        viewer.Scene.Find<DirectionalLight>()!.CastShadow.ShouldBeFalse();
        viewer.CameraControlsEnabled = false;
        viewer.CameraControls.Enabled.ShouldBeFalse();
        TestHelpers.Descendant<Viewport3D>(viewer).ToneMapping.ShouldBe(ToneMapping.Neutral);
        viewer.ToneMapping = ToneMapping.AcesFilmic;
        TestHelpers.Descendant<Viewport3D>(viewer).ToneMapping.ShouldBe(ToneMapping.AcesFilmic);
    }
}
