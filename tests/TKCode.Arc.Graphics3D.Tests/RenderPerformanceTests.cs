using System.Diagnostics;
using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>Guards the renderer's per-frame cost: no managed allocations once warm, and a frame of a large static
/// scene stays cheap on the CPU. The time bounds are generous; the report file has the measured numbers.</summary>
[Collection(nameof(SerialCollection))]
public class RenderPerformanceTests
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private static Scene BuildScene(int objects, int geometries, int materials, out PerspectiveCamera camera)
    {
        var scene = new Scene { Background = ColorF.FromHex(0x101010) };
        var random = new Random(11);
        var geometryPool = new BufferGeometry[geometries];
        for (var i = 0; i < geometries; i++)
        {
            geometryPool[i] = i % 2 == 0 ? new BoxGeometry(0.3f + (i * 0.01f), 0.3f, 0.3f) : new SphereGeometry(0.2f + (i * 0.005f), 12, 8);
        }

        var materialPool = new Material[materials];
        for (var i = 0; i < materials; i++)
        {
            materialPool[i] = new StandardMaterial { Color = ColorF.FromHsl(i / (float)materials, 0.7f, 0.5f), Roughness = 0.5f };
        }

        var side = (int)MathF.Ceiling(MathF.Cbrt(objects));
        for (var i = 0; i < objects; i++)
        {
            var mesh = new Mesh(geometryPool[random.Next(geometries)], materialPool[random.Next(materials)])
            {
                Position = new Vector3((i % side) - (side / 2f), ((i / side) % side) - (side / 2f), -(i / (side * side)) - 2f) * 0.8f,
            };
            scene.Add(mesh);
        }

        scene.Add(new DirectionalLight(3f) { Position = new Vector3(1f, 2f, 3f) }, new AmbientLight(0.3f));
        camera = new PerspectiveCamera(60f, 16f / 9f, 0.1f, 500f) { Position = new Vector3(0f, 0f, side * 0.6f) };
        camera.LookAt(new Vector3(0f, 0f, -side * 0.4f));
        return scene;
    }

    [Fact]
    public void Frames_Do_Not_Allocate_And_Stay_Cheap()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic };
        var report = new System.Text.StringBuilder();
        var results = new List<(int Objects, long Allocated, double Cpu)>();
        using var target = new RenderTarget(1280, 720, Rhi.TextureFormat.RGBA8, 4) { ColorSpace = TextureColorSpace.Srgb };
        foreach (var (objects, geometries, materials) in new[] { (2_000, 20, 20), (20_000, 50, 50), (100_000, 50, 50) })
        {
            var scene = BuildScene(objects, geometries, materials, out var camera);
            for (var i = 0; i < 5; i++)
            {
                renderer.Render(scene, camera, target);
            }

            // Two measured windows; the smaller allocation counts. A window can catch one-off work that is not the
            // renderer's (tiered JIT, thread pool start-up), which a second window does not repeat.
            const int Frames = 40;
            var allocated = long.MaxValue;
            var cpu = 0.0;
            var watch = new Stopwatch();
            for (var window = 0; window < 2; window++)
            {
                gl.Device.Finish();
                cpu = 0.0;
                var before = GC.GetAllocatedBytesForCurrentThread();
                watch.Restart();
                for (var i = 0; i < Frames; i++)
                {
                    // Move the camera so sorting and culling really run.
                    camera.Position += new Vector3(0.01f, 0f, 0f);
                    renderer.Render(scene, camera, target);
                    cpu += renderer.Statistics.CpuMilliseconds;
                }

                gl.Device.Finish();
                watch.Stop();
                allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            }

            var s = renderer.Statistics;
            report.AppendLine(
                $"{objects,7} objects: visible {s.Visible,6}, draw calls {s.DrawCalls,5} (merged {s.MergedDraws,6}), triangles {s.Triangles,9}, "
                + $"CPU {cpu / Frames:0.00} ms/frame, wall {watch.Elapsed.TotalMilliseconds / Frames:0.00} ms/frame, allocated {allocated / Frames} B/frame");
            results.Add((objects, allocated / Frames, cpu / Frames));
            scene.Clear();
        }

        // The report first, so that the numbers are there to read when a bound fails.
        var caps = gl.Device.Capabilities;
        File.WriteAllText(Path.Combine(RenderHarness.OutputDirectory, "performance.txt"), $"{caps.Renderer}, {caps.Version}, {Configuration} build\n{report}");
        gl.Device.DrainErrors().ShouldBeEmpty();

        // The time bound is for a hardware desktop OpenGL driver. A software rasterizer (CI machines without a GPU) and
        // a translation layer (ANGLE) have their own cost per call; for those the report is the record.
        var timed = caps.ShaderLanguage == Rhi.ShaderLanguage.Glsl330 && !caps.Renderer.Contains("Software", StringComparison.OrdinalIgnoreCase);
        foreach (var (objects, allocated, cpu) in results)
        {
            allocated.ShouldBeLessThan(64);
            if (timed)
            {
                cpu.ShouldBeLessThan(objects <= 20_000 ? 30.0 : 120.0);
            }
        }
    }
}

/// <summary>Tests in this collection run one at a time and not beside other tests: their measurements need a quiet machine.</summary>
[CollectionDefinition(nameof(SerialCollection), DisableParallelization = true)]
public sealed class SerialCollection
{
}
