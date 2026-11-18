using System.Diagnostics;
using System.Numerics;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>What <see cref="ThreadedGraphicsDevice"/> adds to a device: the work happens on its render thread, failures
/// still reach the caller, and the calling thread is free sooner. That it draws the same pictures is checked by running
/// the whole suite with <c>TKCODE_ARC_RENDER_THREAD=1</c>.</summary>
[Collection(nameof(SerialCollection))]
public class ThreadedDeviceTests
{
    [Fact]
    public void The_Graphics_Work_Happens_On_The_Render_Thread()
    {
        using var gl = GlTestContext.CreateOrSkip(renderThread: true);
        var device = gl.Device.ShouldBeOfType<ThreadedGraphicsDevice>();
        device.RenderThreadId.ShouldNotBe(Environment.CurrentManagedThreadId);
        var seen = 0;
        device.Invoke(_ => seen = Environment.CurrentManagedThreadId);
        seen.ShouldBe(device.RenderThreadId);

        // A frame drawn through it is the frame the direct device draws.
        using var renderer = new Renderer(device) { ToneMapping = ToneMapping.None };
        var scene = new Scene { Background = ColorF.FromSrgb(0f, 0f, 1f) };
        scene.Add(new Mesh(new SphereGeometry(1f), new BasicMaterial { Color = ColorF.FromSrgb(1f, 0f, 0f) }));
        var camera = new OrthographicCamera(-2f, 2f, 2f, -2f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };
        var image = RenderHarness.Render(renderer, scene, camera, 128, 128);
        RenderHarness.Pixel(image, 64, 64).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        RenderHarness.Pixel(image, 4, 4).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        renderer.Statistics.DrawCalls.ShouldBe(1);
        device.DrainErrors().ShouldBeEmpty();
    }

    [Fact]
    public void A_Shader_That_Does_Not_Compile_Fails_At_The_Call_And_Leaves_The_Device_Usable()
    {
        using var gl = GlTestContext.CreateOrSkip(renderThread: true);
        var device = (ThreadedGraphicsDevice)gl.Device;
        var header = Shading.ShaderLibrary.Header(device.Capabilities.ShaderLanguage);
        Should.Throw<ShaderCompilationException>(() => device.CreateProgram(new ProgramDescriptor
        {
            VertexSource = header + "void main() { gl_Position = vec4(0.0); }",
            FragmentSource = header + "out vec4 o; void main() { o = nonsense; }",
        }));

        using var good = device.CreateProgram(new ProgramDescriptor
        {
            VertexSource = header + "void main() { gl_Position = vec4(0.0); }",
            FragmentSource = header + "out vec4 o; void main() { o = vec4(1.0); }",
        });
        good.IsReady.ShouldBeTrue();
        device.Flush();
    }

    [Fact]
    public void A_Failure_On_The_Render_Thread_Reaches_The_Next_Hand_Over()
    {
        using var gl = GlTestContext.CreateOrSkip(renderThread: true);
        var device = (ThreadedGraphicsDevice)gl.Device;
        device.Post(static _ => throw new InvalidOperationException("from the render thread"));
        Should.Throw<InvalidOperationException>(device.Flush).Message.ShouldBe("from the render thread");

        // It stays failed: what was recorded after the failure was never played.
        Should.Throw<InvalidOperationException>(device.Flush);
    }

    [Fact]
    public void The_Calling_Thread_Only_Records_The_Frame()
    {
        // 20 000 different draws: the driver's share of the frame is large, and it moves to the render thread.
        static (double Direct, double Recorded, double Finished) Measure(bool renderThread)
        {
            using var gl = GlTestContext.CreateOrSkip(renderThread);
            using var renderer = new Renderer(gl.Device);
            var scene = new Scene { Background = ColorF.Black };
            scene.Add(new DirectionalLight(2f) { Position = new Vector3(1f, 2f, 3f) });
            // Every mesh is its own pair of geometry and material, so nothing merges into an instanced draw.
            var boxes = new BoxGeometry[10];
            for (var i = 0; i < boxes.Length; i++)
            {
                boxes[i] = new BoxGeometry(0.3f + (i * 0.01f), 0.4f, 0.4f);
            }

            var materials = new StandardMaterial[2000];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = new StandardMaterial { Color = ColorF.FromHsl(i / 2000f, 0.6f, 0.5f) };
            }

            for (var i = 0; i < 20000; i++)
            {
                scene.Add(new Mesh(boxes[i % 10], materials[(i / 10) % 2000]) { Position = new Vector3((i % 200) - 100f, ((i / 200) % 100) - 50f, -(i / 20000f)) });
            }

            var camera = new OrthographicCamera(-100f, 100f, 50f, -50f, 0.1f, 50f) { Position = new Vector3(0f, 0f, 10f) };
            using var target = new RenderTarget(640, 360, TextureFormat.RGBA8, 1);
            for (var i = 0; i < 5; i++)
            {
                renderer.Render(scene, camera, target);
            }

            gl.Device.Finish();
            double call = 0, total = 0;
            const int Frames = 20;
            for (var i = 0; i < Frames; i++)
            {
                var start = Stopwatch.GetTimestamp();
                renderer.Render(scene, camera, target);
                call += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                gl.Device.Finish();
                total += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }

            renderer.Statistics.DrawCalls.ShouldBeGreaterThan(15000);
            return (renderThread ? 0 : call / Frames, renderThread ? call / Frames : 0, total / Frames);
        }

        var direct = Measure(false);
        var threaded = Measure(true);
        File.WriteAllText(
            Path.Combine(RenderHarness.OutputDirectory, "render-thread-timing.txt"),
            FormattableString.Invariant($"20000 draws, ms per frame on the calling thread: direct {direct.Direct:0.00}, with a render thread {threaded.Recorded:0.00}; until the GPU is done: direct {direct.Finished:0.00}, with a render thread {threaded.Finished:0.00}\n"));

        // Recording is a fraction of issuing the calls; the bound is loose because machines differ.
        threaded.Recorded.ShouldBeLessThan(direct.Direct * 0.85);
    }
}
