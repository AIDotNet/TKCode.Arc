using System.Numerics;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D.Tests;

public class PostProcessingTests
{
    private static Scene GlowScene(out PerspectiveCamera camera)
    {
        var scene = new Scene { Background = ColorF.FromHex(0x0b0d12) };
        var floor = new Mesh(new PlaneGeometry(20f, 20f), new StandardMaterial { Color = ColorF.FromHex(0x30343c), Roughness = 0.6f });
        floor.RotateX(-MathF.PI / 2f);
        scene.Add(floor);
        ReadOnlySpan<uint> colors = [0xff3d5a, 0x3dd6ff, 0xffd23d, 0x7dff6b];
        for (var i = 0; i < 4; i++)
        {
            var color = ColorF.FromHex(colors[i]);
            scene.Add(new Mesh(new TorusGeometry(0.5f, 0.12f, 16, 64), new StandardMaterial
            {
                Color = ColorF.Black,
                Emissive = color,
                EmissiveIntensity = 6f,
            })
            {
                Position = new Vector3((i - 1.5f) * 1.6f, 0.8f, 0f),
            });
        }

        scene.Add(new Mesh(new SphereGeometry(0.5f, 48, 24), new StandardMaterial { Color = ColorF.FromHex(0xdddddd), Roughness = 0.3f }) { Position = new Vector3(0f, 0.5f, 1.8f) });
        scene.Add(new DirectionalLight(1.5f) { Position = new Vector3(2f, 4f, 3f) }, new AmbientLight(0.2f));
        camera = new PerspectiveCamera(45f, 16f / 9f, 0.1f, 100f) { Position = new Vector3(0f, 2.2f, 6.5f) };
        camera.LookAt(new Vector3(0f, 0.7f, 0f));
        return scene;
    }

    [Fact]
    public void Bloom_Spreads_Light_Around_Bright_Objects()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic };
        var scene = GlowScene(out var camera);
        var plain = RenderHarness.Render(renderer, scene, camera, 960, 540, "post-off");

        renderer.PostProcessing.Bloom.Enabled = true;
        renderer.PostProcessing.Bloom.Strength = 0.9f;
        renderer.PostProcessing.Vignette.Enabled = true;
        renderer.PostProcessing.Fxaa = true;
        var glowing = RenderHarness.Render(renderer, scene, camera, 960, 540, "post-bloom");

        gl.Device.DrainErrors().ShouldBeEmpty();

        // A point just outside the first ring: dark without bloom, lit with it.
        var before = RenderHarness.Pixel(plain, 215, 165);
        var after = RenderHarness.Pixel(glowing, 215, 165);
        (after.R + after.G + after.B).ShouldBeGreaterThan(before.R + before.G + before.B + 20);

        // Turning everything off goes back to the direct path and the same picture.
        renderer.PostProcessing.Bloom.Enabled = false;
        renderer.PostProcessing.Vignette.Enabled = false;
        renderer.PostProcessing.Fxaa = false;
        var again = RenderHarness.Render(renderer, scene, camera, 960, 540);
        RenderHarness.Pixel(again, 480, 270).ShouldBe(RenderHarness.Pixel(plain, 480, 270));
    }

    [Fact]
    public void Grading_Alone_Keeps_The_Picture_Close_To_The_Direct_Path()
    {
        // The post chain with neutral settings must reproduce what materials output directly (same tone mapping and
        // encoding), so switching an effect on does not shift the whole picture.
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var scene = GlowScene(out var camera);
        var direct = RenderHarness.Render(renderer, scene, camera, 480, 270, null, samples: 1);
        renderer.PostProcessing.Saturation = 0.999f;
        var chained = RenderHarness.Render(renderer, scene, camera, 480, 270, null, samples: 1);
        foreach (var (x, y) in new[] { (240, 135), (100, 200), (240, 220), (400, 60) })
        {
            var a = RenderHarness.Pixel(direct, x, y);
            var b = RenderHarness.Pixel(chained, x, y);
            Math.Abs(a.R - b.R).ShouldBeLessThanOrEqualTo(3);
            Math.Abs(a.G - b.G).ShouldBeLessThanOrEqualTo(3);
            Math.Abs(a.B - b.B).ShouldBeLessThanOrEqualTo(3);
        }

        renderer.PostProcessing.Saturation = 0f;
        var gray = RenderHarness.Pixel(RenderHarness.Render(renderer, scene, camera, 480, 270, null, samples: 1), 100, 120);
        Math.Abs(gray.R - gray.G).ShouldBeLessThanOrEqualTo(2);
        Math.Abs(gray.G - gray.B).ShouldBeLessThanOrEqualTo(2);
    }

    private static Scene RoomScene(out PerspectiveCamera camera, out Mesh selected)
    {
        var scene = new Scene { Background = ColorF.FromHex(0xdfe3ea) };
        var white = new StandardMaterial { Color = ColorF.FromHex(0xf2f2f2), Roughness = 0.9f };
        var floor = new Mesh(new PlaneGeometry(30f, 30f), white);
        floor.RotateX(-MathF.PI / 2f);
        var wall = new Mesh(new PlaneGeometry(30f, 10f), white) { Position = new Vector3(0f, 5f, -3f) };
        scene.Add(floor, wall);
        selected = null!;
        for (var i = 0; i < 6; i++)
        {
            var box = new Mesh(new BoxGeometry(1f, 1f + (i % 3) * 0.5f, 1f), new StandardMaterial { Color = ColorF.FromHsl(i / 6f, 0.5f, 0.6f), Roughness = 0.7f })
            {
                Position = new Vector3((i - 2.5f) * 1.25f, 0.5f + (i % 3) * 0.25f, -1.5f + (i * 0.9f)),
            };
            box.RotateY(i * 0.3f);
            scene.Add(box);
            if (i == 2)
            {
                selected = box;
            }
        }

        scene.Add(new HemisphereLight(ColorF.White, ColorF.FromHex(0x999999), 2.6f), new DirectionalLight(1f) { Position = new Vector3(2f, 6f, 5f) });
        camera = new PerspectiveCamera(45f, 16f / 9f, 0.1f, 100f) { Position = new Vector3(0f, 3.2f, 8f) };
        camera.LookAt(new Vector3(0f, 0.7f, 0f));
        return scene;
    }

    [Fact]
    public void Ambient_Occlusion_Darkens_Creases_But_Not_Open_Surfaces()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var scene = RoomScene(out var camera, out _);
        var plain = RenderHarness.Render(renderer, scene, camera, 960, 540, "post-ao-off");
        renderer.PostProcessing.AmbientOcclusion.Enabled = true;
        renderer.PostProcessing.AmbientOcclusion.Radius = 0.6f;
        var shaded = RenderHarness.Render(renderer, scene, camera, 960, 540, "post-ao-on");

        gl.Device.DrainErrors().ShouldBeEmpty();
        static int Sum((byte R, byte G, byte B, byte A) p) => p.R + p.G + p.B;

        // Open floor in the foreground stays as it was; the crease where the floor meets the wall gets darker.
        Math.Abs(Sum(RenderHarness.Pixel(shaded, 480, 500)) - Sum(RenderHarness.Pixel(plain, 480, 500))).ShouldBeLessThan(15);
        var darker = 0;
        for (var x = 100; x < 860; x += 20)
        {
            for (var y = 150; y < 260; y += 5)
            {
                if (Sum(RenderHarness.Pixel(shaded, x, y)) < Sum(RenderHarness.Pixel(plain, x, y)) - 12)
                {
                    darker++;
                }
            }
        }

        darker.ShouldBeGreaterThan(20);
    }

    [Fact]
    public void Depth_Of_Field_Blurs_Away_From_The_Focus_Distance()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var scene = RoomScene(out var camera, out var focus);
        ((StandardMaterial)focus.Material).Map = TestScenes.Checker(64, 8, 0xffffff, 0x222222);
        var floor = (Mesh)scene.Children[0];
        floor.Material = new StandardMaterial { Map = TestScenes.Checker(256, 32, 0xffffff, 0x555555), Roughness = 0.9f };
        var sharp = RenderHarness.Render(renderer, scene, camera, 960, 540, null);
        var dof = renderer.PostProcessing.DepthOfField;
        dof.Enabled = true;
        dof.FocusDistance = Vector3.Distance(camera.WorldPosition, focus.WorldPosition);
        dof.FocusRange = 1f;
        dof.Falloff = 3f;
        dof.MaxBlur = 10f;
        var blurred = RenderHarness.Render(renderer, scene, camera, 960, 540, "post-dof");

        gl.Device.DrainErrors().ShouldBeEmpty();

        // The steepest step between neighbouring pixels along a row: an edge stays steep near the focus distance and
        // is spread over many pixels in the foreground.
        static int Contrast(TextureImage image, int y, int x0, int x1)
        {
            var steepest = 0;
            for (var x = x0; x < x1; x++)
            {
                steepest = Math.Max(steepest, Math.Abs(RenderHarness.Pixel(image, x, y).G - RenderHarness.Pixel(image, x + 1, y).G));
            }

            return steepest;
        }

        Contrast(blurred, 520, 100, 860).ShouldBeLessThan(Contrast(sharp, 520, 100, 860) / 2);
        var focusPixel = camera.Project(focus.WorldPosition);
        var fy = (int)((1f - focusPixel.Y) * 0.5f * 540f);
        var fx = (int)((focusPixel.X + 1f) * 0.5f * 960f);
        Contrast(blurred, fy, fx - 25, fx + 25).ShouldBeGreaterThan(Contrast(sharp, fy, fx - 25, fx + 25) / 2);
    }

    [Fact]
    public void Outline_Draws_A_Line_Around_Selected_Objects_Only()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var scene = RoomScene(out var camera, out var selected);

        // The comparison picture goes through the effect chain too (a neutral setting keeps it on), so that the only
        // difference is the outline.
        renderer.PostProcessing.Saturation = 0.999f;
        var plain = RenderHarness.Render(renderer, scene, camera, 960, 540, null);
        var outline = renderer.PostProcessing.Outline;
        outline.Objects.Add(selected);
        outline.Color = ColorF.FromSrgb(1f, 0f, 1f);
        outline.Thickness = 3f;
        var image = RenderHarness.Render(renderer, scene, camera, 960, 540, "post-outline");

        gl.Device.DrainErrors().ShouldBeEmpty();
        var magenta = 0;
        var changedElsewhere = 0;
        var center = camera.Project(selected.WorldPosition);
        var cx = (center.X + 1f) * 0.5f * 960f;
        var cy = (1f - center.Y) * 0.5f * 540f;
        for (var y = 0; y < 540; y += 2)
        {
            for (var x = 0; x < 960; x += 2)
            {
                var a = RenderHarness.Pixel(image, x, y);
                var b = RenderHarness.Pixel(plain, x, y);
                var isLine = a.R > 230 && a.B > 230 && a.G < 40;
                if (isLine)
                {
                    magenta++;
                    (MathF.Abs(x - cx) < 140f && MathF.Abs(y - cy) < 140f).ShouldBeTrue();
                }
                else if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 24 && (MathF.Abs(x - cx) > 160f || MathF.Abs(y - cy) > 160f))
                {
                    changedElsewhere++;
                }
            }
        }

        magenta.ShouldBeGreaterThan(100);
        changedElsewhere.ShouldBe(0);

        // Nothing selected: the effect chain is skipped again.
        outline.Objects.Clear();
        renderer.PostProcessing.Saturation = 1f;
        renderer.PostProcessing.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Custom_Passes_Rewrite_The_Picture_In_Order()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.None };
        var scene = new Scene { Background = ColorF.Black };

        // Left half red, right half green, both unlit so the values are exact.
        scene.Add(new Mesh(new PlaneGeometry(2f, 2f), new BasicMaterial { Color = new ColorF(1f, 0f, 0f) }) { Position = new Vector3(-1f, 0f, 0f) });
        scene.Add(new Mesh(new PlaneGeometry(2f, 2f), new BasicMaterial { Color = new ColorF(0f, 1f, 0f) }) { Position = new Vector3(1f, 0f, 0f) });
        var camera = new OrthographicCamera(-2f, 2f, 1f, -1f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };

        // First pass: mirror the picture left to right. Second pass: tint by a uniform and a texture.
        var tint = Sl.Uniform(new Vector3(1f, 1f, 1f));
        var white = new Texture(new TextureImage(1, 1, [255, 255, 255, 255]), TextureColorSpace.Linear);
        var slot = new TextureSlot(white);
        var mirror = new PostPass(PostPass.Input(Sl.Vec2(1f - PostPass.Uv.X, PostPass.Uv.Y)));
        var colorize = new PostPass(Sl.ToVec3(PostPass.Input()) * tint * Sl.ToVec3(Sl.Texture(slot, PostPass.Uv)));
        renderer.PostProcessing.Passes.Add(mirror);
        renderer.PostProcessing.Passes.Add(colorize);
        renderer.PostProcessing.IsActive.ShouldBeTrue();

        var image = RenderHarness.Render(renderer, scene, camera, 400, 200, "post-custom-pass");
        gl.Device.DrainErrors().ShouldBeEmpty();
        var left = RenderHarness.Pixel(image, 100, 100);
        var right = RenderHarness.Pixel(image, 300, 100);
        left.G.ShouldBeGreaterThan((byte)250);
        left.R.ShouldBeLessThan((byte)5);
        right.R.ShouldBeGreaterThan((byte)250);
        right.G.ShouldBeLessThan((byte)5);

        // Uniform and texture changes need no new program. The passes work on linear values: half of full red is 188
        // once the picture is encoded for the screen.
        renderer.Statistics.ProgramsCompiled.ShouldBeGreaterThan(0);
        tint.Vector3 = new Vector3(0.5f, 0.5f, 0.5f);
        slot.Texture = new Texture(new TextureImage(1, 1, [255, 0, 255, 255]), TextureColorSpace.Linear);
        image = RenderHarness.Render(renderer, scene, camera, 400, 200);
        left = RenderHarness.Pixel(image, 100, 100);
        right = RenderHarness.Pixel(image, 300, 100);
        left.G.ShouldBeLessThan((byte)5);
        ((int)right.R).ShouldBeInRange(182, 194);
        renderer.Statistics.ProgramsCompiled.ShouldBe(0);

        // A disabled pass is skipped: the picture is no longer mirrored.
        mirror.Enabled = false;
        slot.Texture = white;
        image = RenderHarness.Render(renderer, scene, camera, 400, 200);
        ((int)RenderHarness.Pixel(image, 100, 100).R).ShouldBeInRange(182, 194);
        ((int)RenderHarness.Pixel(image, 300, 100).G).ShouldBeInRange(182, 194);

        // A pass has no surface to ask about.
        renderer.PostProcessing.Passes.Add(new PostPass(Sl.NormalWorld));
        Should.Throw<InvalidOperationException>(() => RenderHarness.Render(renderer, scene, camera, 400, 200)).Message.ShouldContain("no surface");
    }
}
