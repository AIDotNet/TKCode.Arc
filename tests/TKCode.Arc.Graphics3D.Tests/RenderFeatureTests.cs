using System.Numerics;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>One picture per renderer feature, checked by a few pixels and kept under artifacts/graphics3d for review.</summary>
public class RenderFeatureTests
{
    private static Mesh Floor(float size = 14f, ColorF? color = null)
    {
        var floor = new Mesh(new PlaneGeometry(size, size), new StandardMaterial { Color = color ?? ColorF.FromHex(0x9aa0a6), Roughness = 0.85f })
        {
            ReceiveShadow = true,
            Name = "floor",
        };
        floor.RotateX(-MathF.PI / 2f);
        return floor;
    }

    private static PerspectiveCamera Camera(Vector3 position, Vector3 target, float aspect = 4f / 3f)
    {
        var camera = new PerspectiveCamera(45f, aspect, 0.1f, 200f) { Position = position };
        camera.LookAt(target);
        return camera;
    }

    private static int Brightness((byte R, byte G, byte B, byte A) p) => p.R + p.G + p.B;

    [Fact]
    public void Spot_And_Point_Lights_Cast_Shadows()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var scene = new Scene { Background = ColorF.FromHex(0x101216) };
        scene.Add(Floor());
        var material = new StandardMaterial { Color = ColorF.FromHex(0xdddddd), Roughness = 0.5f };
        for (var i = 0; i < 5; i++)
        {
            var box = new Mesh(new BoxGeometry(0.6f, 1.2f, 0.6f), material)
            {
                Position = new Vector3((i - 2) * 1.6f, 0.6f, -0.5f),
                CastShadow = true,
                ReceiveShadow = true,
            };
            scene.Add(box);
        }

        var spot = new SpotLight(ColorF.FromHex(0xffd9a0), 60f, 20f, 0.5f, 0.4f) { Position = new Vector3(-3f, 5f, 3f), CastShadow = true };
        spot.Target = new Vector3(-1.5f, 0f, -0.5f);
        var point = new PointLight(ColorF.FromHex(0x8ab4ff), 25f, 15f) { Position = new Vector3(2.4f, 1.6f, 1.2f), CastShadow = true };
        scene.Add(spot, point, new AmbientLight(0.05f));
        var camera = Camera(new Vector3(0f, 5f, 8f), new Vector3(0f, 0.3f, 0f));

        var image = RenderHarness.Render(renderer, scene, camera, 800, 600, "feature-spot-point-shadows");

        gl.Device.DrainErrors().ShouldBeEmpty();
        renderer.Statistics.ShadowViews.ShouldBe(7);
        Brightness(RenderHarness.Pixel(image, 400, 420)).ShouldBeGreaterThan(40);
    }

    [Fact]
    public void Textures_Normal_Maps_And_Transparency()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic };
        var sky = TestScenes.SkyEnvironment();
        var scene = new Scene { Environment = sky, BackgroundTexture = sky, BackgroundBlurriness = 0.4f };
        var checker = TestScenes.Checker();
        checker.Repeat = new Vector2(4f, 4f);
        var floor = Floor();
        ((StandardMaterial)floor.Material).Map = checker;
        scene.Add(floor);

        // A bumpy normal map made of a grid of domes.
        const int N = 128;
        var normal = new byte[N * N * 4];
        for (var y = 0; y < N; y++)
        {
            for (var x = 0; x < N; x++)
            {
                float u = (((x + 0.5f) / N * 4f) % 1f) - 0.5f, v = (((y + 0.5f) / N * 4f) % 1f) - 0.5f;
                var r2 = (u * u) + (v * v);
                var n = r2 < 0.16f ? Vector3.Normalize(new Vector3(u * 2.2f, v * 2.2f, MathF.Sqrt(MathF.Max(0.02f, 0.16f - r2)))) : Vector3.UnitZ;
                var o = ((y * N) + x) * 4;
                normal[o] = (byte)((n.X * 0.5f + 0.5f) * 255f);
                normal[o + 1] = (byte)((n.Y * 0.5f + 0.5f) * 255f);
                normal[o + 2] = (byte)((n.Z * 0.5f + 0.5f) * 255f);
                normal[o + 3] = 255;
            }
        }

        var normalMap = new Texture(new TextureImage(N, N, normal));
        var bumpy = new Mesh(new SphereGeometry(0.9f, 64, 32), new StandardMaterial
        {
            Color = ColorF.FromHex(0xb87333),
            Metalness = 1f,
            Roughness = 0.35f,
            NormalMap = normalMap,
        })
        {
            Position = new Vector3(-1.6f, 0.9f, 0f),
            CastShadow = true,
        };
        var glass = new Mesh(new BoxGeometry(1.4f, 1.4f, 1.4f), new StandardMaterial
        {
            Color = ColorF.FromHex(0x66ccff),
            Roughness = 0.1f,
            Transparent = true,
            Opacity = 0.45f,
            Side = Side.Double,
        })
        {
            Position = new Vector3(1.2f, 0.7f, 0.6f),
        };
        var behind = new Mesh(new SphereGeometry(0.5f), new StandardMaterial { Color = ColorF.FromHex(0xe5484d), Roughness = 0.4f })
        {
            Position = new Vector3(1.5f, 0.5f, -1.5f),
            CastShadow = true,
        };
        var cutout = new Mesh(new PlaneGeometry(1.6f, 1.6f), new StandardMaterial
        {
            ColorNode = Sl.Color(0x2ecc71),
            OpacityNode = Sl.Step(0.5f, Sl.Fract(Sl.Uv.X * 6f)) * Sl.Step(0.3f, Sl.Fract(Sl.Uv.Y * 6f)),
            AlphaTest = 0.5f,
            Side = Side.Double,
        })
        {
            Position = new Vector3(-0.2f, 0.8f, -2.2f),
            CastShadow = true,
        };
        var sun = new DirectionalLight(2.5f) { Position = new Vector3(4f, 6f, 3f), CastShadow = true };
        sun.Shadow.AutoFit = true;
        scene.Add(bumpy, glass, behind, cutout, sun);
        var camera = Camera(new Vector3(0.5f, 2.6f, 6f), new Vector3(0f, 0.6f, 0f));

        RenderHarness.Render(renderer, scene, camera, 800, 600, "feature-textures-transparency");

        gl.Device.DrainErrors().ShouldBeEmpty();
    }

    [Fact]
    public void Instancing_And_Automatic_Batching()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic };
        var scene = new Scene { Background = ColorF.FromHex(0x15171c) };
        const int Side = 30;
        var instanced = new InstancedMesh(new BoxGeometry(0.5f, 0.5f, 0.5f), new StandardMaterial { Roughness = 0.5f }, Side * Side);
        for (var z = 0; z < Side; z++)
        {
            for (var x = 0; x < Side; x++)
            {
                var i = (z * Side) + x;
                var h = 0.5f + (2f * (0.5f + (0.5f * MathF.Sin(x * 0.45f) * MathF.Cos(z * 0.35f))));
                instanced.Matrices[i] = Matrix4x4.CreateScale(1f, h * 2f, 1f) * Matrix4x4.CreateTranslation((x - (Side / 2f)) * 0.7f, h * 0.5f, (z - (Side / 2f)) * 0.7f - 6f);
                instanced.SetColor(i, ColorF.FromHsl(0.55f + (0.25f * x / Side), 0.7f, 0.35f + (0.3f * z / Side)));
            }
        }

        instanced.NeedsUpdate();
        scene.Add(instanced);

        // 400 separate meshes that share geometry and material: the renderer merges them into instanced draws.
        var sharedGeometry = new SphereGeometry(0.18f, 16, 8);
        var sharedMaterial = new StandardMaterial { Color = ColorF.FromHex(0xffb224), Roughness = 0.3f, Metalness = 0.6f };
        for (var i = 0; i < 400; i++)
        {
            var a = i * 0.35f;
            scene.Add(new Mesh(sharedGeometry, sharedMaterial) { Position = new Vector3(MathF.Cos(a) * (2f + (i * 0.02f)), 0.3f + (i * 0.012f), 4f + (MathF.Sin(a) * (2f + (i * 0.02f)))) });
        }

        scene.Add(new DirectionalLight(3f) { Position = new Vector3(5f, 10f, 6f) }, new HemisphereLight(ColorF.FromHex(0xcfe0ff), ColorF.FromHex(0x1c1a17), 1.2f));
        var camera = Camera(new Vector3(8f, 9f, 16f), new Vector3(0f, 1f, -1f));

        var image = RenderHarness.Render(renderer, scene, camera, 800, 600, "feature-instancing");

        gl.Device.DrainErrors().ShouldBeEmpty();
        // One draw for the instanced mesh (its six sides share a material) and one for all the visible spheres.
        renderer.Statistics.DrawCalls.ShouldBe(2);
        renderer.Statistics.Visible.ShouldBeGreaterThan(300);
        renderer.Statistics.MergedDraws.ShouldBe(renderer.Statistics.Visible - 2);
        Brightness(RenderHarness.Pixel(image, 400, 250)).ShouldBeGreaterThan(40);
    }

    [Fact]
    public void Skinning_And_Morph_Targets()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic };
        var scene = new Scene { Background = ColorF.FromHex(0x1b1d22) };
        scene.Add(Floor());

        // A column bent by a three-joint chain.
        var column = new BoxGeometry(0.6f, 3f, 0.6f, 2, 24, 2);
        column.Transform(Matrix4x4.CreateTranslation(0f, 1.5f, 0f));
        var positions = column.GetAttribute(BufferGeometry.Position)!;
        var joints = new ushort[positions.Count * 4];
        var weights = new float[positions.Count * 4];
        for (var i = 0; i < positions.Count; i++)
        {
            var t = Math.Clamp(positions.GetVector3(i).Y / 3f, 0f, 1f) * 2f;
            var j = Math.Min((int)t, 1);
            var f = t - j;
            joints[i * 4] = (ushort)j;
            joints[(i * 4) + 1] = (ushort)(j + 1);
            weights[i * 4] = 1f - f;
            weights[(i * 4) + 1] = f;
        }

        column.SetAttribute(BufferGeometry.SkinIndex, VertexAttribute.UShort(joints, 4, false));
        column.SetAttribute(BufferGeometry.SkinWeight, VertexAttribute.Float(weights, 4));
        var root = new Bone { Position = new Vector3(-2f, 0f, 0f) };
        var mid = new Bone { Position = new Vector3(0f, 1.5f, 0f) };
        var tip = new Bone { Position = new Vector3(0f, 1.5f, 0f) };
        root.Add(mid);
        mid.Add(tip);
        var skinned = new SkinnedMesh(column, new StandardMaterial { Color = ColorF.FromHex(0x30a46c), Roughness = 0.5f })
        {
            CastShadow = true,
            // The geometry is modelled at the origin; the bind pose puts joint 0 there.
            BindMatrix = Matrix4x4.CreateTranslation(-2f, 0f, 0f),
        };
        scene.Add(root, skinned);
        skinned.Skeleton = new Skeleton([root, mid, tip]);
        mid.RotateZ(-0.6f);
        tip.RotateZ(-0.8f);

        // A sphere that morphs toward a cube.
        var sphere = new SphereGeometry(0.8f, 48, 24);
        var p = sphere.GetAttribute(BufferGeometry.Position)!;
        var deltas = new float[p.Count * 3];
        for (var i = 0; i < p.Count; i++)
        {
            var v = p.GetVector3(i);
            var m = MathF.Max(MathF.Abs(v.X), MathF.Max(MathF.Abs(v.Y), MathF.Abs(v.Z)));
            var cube = v / m * 0.7f;
            deltas[i * 3] = cube.X - v.X;
            deltas[(i * 3) + 1] = cube.Y - v.Y;
            deltas[(i * 3) + 2] = cube.Z - v.Z;
        }

        sphere.AddMorphTarget(new MorphTarget(deltas, name: "cube"));
        var material = new StandardMaterial { Color = ColorF.FromHex(0xf76b15), Roughness = 0.4f, FlatShading = true };
        for (var i = 0; i < 3; i++)
        {
            scene.Add(new Mesh(sphere, material)
            {
                Position = new Vector3(0.2f + (i * 2f), 0.9f, 0.5f),
                MorphWeights = [i * 0.5f],
                CastShadow = true,
            });
        }

        var sun = new DirectionalLight(3f) { Position = new Vector3(3f, 6f, 5f), CastShadow = true };
        sun.Shadow.AutoFit = true;
        scene.Add(sun, new AmbientLight(0.5f));
        var camera = Camera(new Vector3(1f, 3.2f, 8f), new Vector3(1f, 1.2f, 0f));

        var image = RenderHarness.Render(renderer, scene, camera, 800, 600, "feature-skinning-morph");

        gl.Device.DrainErrors().ShouldBeEmpty();
        Brightness(RenderHarness.Pixel(image, 400, 300)).ShouldBeGreaterThan(30);
    }

    [Fact]
    public void Lines_Points_And_Sprites()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device);
        var scene = new Scene { Background = ColorF.FromHex(0x0d1117) };

        var helix = new List<Vector3>();
        var colors = new List<float>();
        for (var i = 0; i <= 400; i++)
        {
            var t = i / 400f;
            helix.Add(new Vector3(MathF.Cos(t * 25f) * 1.2f, (t * 4f) - 2f, MathF.Sin(t * 25f) * 1.2f));
            var c = ColorF.FromHsl(t, 0.9f, 0.6f);
            colors.AddRange([c.R, c.G, c.B]);
        }

        var lineGeometry = new BufferGeometry();
        lineGeometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(helix)));
        lineGeometry.SetAttribute(BufferGeometry.Color, VertexAttribute.Float([.. colors], 3));
        scene.Add(new Line(lineGeometry, new LineMaterial { VertexColors = true }) { Position = new Vector3(-3f, 0f, 0f) });

        var random = new Random(7);
        var cloud = new float[3000 * 3];
        var cloudColors = new float[3000 * 3];
        for (var i = 0; i < 3000; i++)
        {
            var v = Vector3.Normalize(new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f)) * (0.6f + random.NextSingle());
            cloud[i * 3] = v.X;
            cloud[(i * 3) + 1] = v.Y;
            cloud[(i * 3) + 2] = v.Z;
            var c = ColorF.FromHsl(0.55f + (0.2f * random.NextSingle()), 0.8f, 0.6f);
            cloudColors[i * 3] = c.R;
            cloudColors[(i * 3) + 1] = c.G;
            cloudColors[(i * 3) + 2] = c.B;
        }

        var pointGeometry = new BufferGeometry();
        pointGeometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(cloud, 3));
        pointGeometry.SetAttribute(BufferGeometry.Color, VertexAttribute.Float(cloudColors, 3));
        scene.Add(new Points(pointGeometry, new PointsMaterial { Size = 0.06f, VertexColors = true, Round = true, AlphaTest = 0.5f }));

        var checker = TestScenes.Checker(64, 4, 0xffcc00, 0x222222);
        for (var i = 0; i < 4; i++)
        {
            scene.Add(new Sprite(new SpriteMaterial { Map = checker, Rotation = i * 0.4f, Opacity = 1f - (i * 0.2f) })
            {
                Position = new Vector3(3f, -1.5f + i, 0f),
                Scale = new Vector3(0.8f, 0.8f, 1f),
            });
        }

        var camera = Camera(new Vector3(0f, 0.5f, 8f), Vector3.Zero);

        var image = RenderHarness.Render(renderer, scene, camera, 800, 600, "feature-lines-points-sprites");

        gl.Device.DrainErrors().ShouldBeEmpty();
        Brightness(RenderHarness.Pixel(image, 400, 300)).ShouldBeGreaterThan(20);
    }

    [Fact]
    public void Node_Materials_Drive_Color_And_Vertices()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic, Time = 1.25 };
        var sky = TestScenes.SkyEnvironment();
        var scene = new Scene { Environment = sky, Background = ColorF.FromHex(0x14161a) };

        // Marble: fractal noise picks between two colors and drives roughness.
        var veins = Sl.Fbm(Sl.PositionLocal * 3f + Sl.Vec3(Sl.Fbm(Sl.PositionLocal * 2f) * 2f));
        var marble = new NodeMaterial
        {
            ColorNode = Sl.Mix(Sl.Color(0xf2efe9), Sl.Color(0x39424e), Sl.SmoothStep(0.45f, 0.7f, veins)),
            RoughnessNode = Sl.Mix(0.15f, 0.6f, veins),
        };
        scene.Add(new Mesh(new SphereGeometry(1f, 64, 32), marble) { Position = new Vector3(-2.4f, 0f, 0f) });

        // A wobbling blob: vertices move along their normals, color from a rim term, animated by time.
        var wave = Sl.Sin(Sl.PositionLocal.Y * 8f + Sl.Time * 3f) * 0.08f;
        var blob = new NodeMaterial
        {
            PositionNode = Sl.PositionLocal + Sl.NormalLocal * wave,
            ColorNode = Sl.Mix(Sl.Color(0x3e63dd), Sl.Color(0xff5fd2), Sl.Fresnel(2f)),
            RoughnessNode = 0.3f,
            FlatShading = true,
        };
        scene.Add(new Mesh(new SphereGeometry(1f, 96, 48), blob));

        // Unlit hologram stripes with a uniform the application can change.
        var speed = Sl.Uniform(4f);
        var hologram = new NodeMaterial
        {
            Lit = false,
            Transparent = true,
            Side = Side.Double,
            ColorNode = Sl.Color(0x4cc9f0) * (0.6f + Sl.Fresnel(1.5f) * 2f),
            OpacityNode = Sl.Saturate(Sl.Step(0.5f, Sl.Fract(Sl.PositionWorld.Y * 10f - Sl.Time * speed)) * 0.6f + Sl.Fresnel(2f)),
        };
        scene.Add(new Mesh(new SphereGeometry(1f, 48, 24), hologram) { Position = new Vector3(2.4f, 0f, 0f) });
        scene.Add(new DirectionalLight(2f) { Position = new Vector3(3f, 4f, 5f) });
        var camera = Camera(new Vector3(0f, 0.6f, 7f), Vector3.Zero, 16f / 9f);

        var image = RenderHarness.Render(renderer, scene, camera, 960, 540, "feature-node-materials");

        gl.Device.DrainErrors().ShouldBeEmpty();
        Brightness(RenderHarness.Pixel(image, 480, 270)).ShouldBeGreaterThan(30);
    }

    [Fact]
    public void A_Single_Morphed_Mesh_Is_Drawn_Where_It_Stands()
    {
        // One morph weight puts one texel in front of the object records; the records must still start on a multiple
        // of four texels, or the mesh reads another object's matrix.
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device);
        var scene = new Scene { Background = ColorF.Black };
        var geometry = new BoxGeometry(1f, 1f, 1f);
        geometry.AddMorphTarget(new MorphTarget(new float[geometry.VertexCount * 3]));
        scene.Add(new Mesh(geometry, new BasicMaterial { Color = ColorF.White }) { Position = new Vector3(1.5f, 0f, 0f), MorphWeights = [0.5f] });
        var camera = new OrthographicCamera(-3f, 3f, 3f, -3f, 0.1f, 20f) { Position = new Vector3(0f, 0f, 5f) };

        var image = RenderHarness.Render(renderer, scene, camera, 120, 120, null, samples: 1);

        gl.Device.DrainErrors().ShouldBeEmpty();
        // The box covers x in 1..2 of -3..3: pixels 80..100. Its center is white, the mirrored position is empty.
        Brightness(RenderHarness.Pixel(image, 90, 60)).ShouldBe(765);
        Brightness(RenderHarness.Pixel(image, 30, 60)).ShouldBe(0);
    }
}

public class TransmissionTests
{
    [Fact]
    public void Glass_Shows_And_Tints_What_Is_Behind_It()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var sky = TestScenes.SkyEnvironment();
        var scene = new Scene { Environment = sky, BackgroundTexture = sky, BackgroundBlurriness = 0.5f };
        var floor = new Mesh(new PlaneGeometry(14f, 14f), new StandardMaterial { Map = TestScenes.Checker(256, 8, 0xffffff, 0x334455), Roughness = 0.8f });
        floor.RotateX(-MathF.PI / 2f);
        scene.Add(floor);
        for (var i = 0; i < 5; i++)
        {
            scene.Add(new Mesh(new BoxGeometry(0.5f, 1.6f, 0.5f), new StandardMaterial { Color = ColorF.FromHsl(i / 5f, 0.8f, 0.5f), Roughness = 0.5f })
            {
                Position = new Vector3((i - 2) * 1.1f, 0.8f, -1.6f),
            });
        }

        scene.Add(new Mesh(new SphereGeometry(0.9f, 64, 32), new PhysicalMaterial { Transmission = 1f, Roughness = 0.05f, Thickness = 0.8f, Ior = 1.5f })
        {
            Position = new Vector3(-1.4f, 0.9f, 0.6f),
        });
        scene.Add(new Mesh(new SphereGeometry(0.9f, 64, 32), new PhysicalMaterial
        {
            Transmission = 1f,
            Roughness = 0.45f,
            Thickness = 1.2f,
            Color = ColorF.FromHex(0xffffff),
            AttenuationColor = ColorF.FromHex(0x2e9e5b),
            AttenuationDistance = 0.6f,
        })
        {
            Position = new Vector3(1.4f, 0.9f, 0.6f),
        });
        scene.Add(new DirectionalLight(2f) { Position = new Vector3(3f, 6f, 4f) });
        var camera = new PerspectiveCamera(40f, 16f / 9f, 0.1f, 100f) { Position = new Vector3(0f, 1.6f, 6f) };
        camera.LookAt(new Vector3(0f, 0.8f, 0f));

        var image = RenderHarness.Render(renderer, scene, camera, 960, 540, "feature-transmission");

        gl.Device.DrainErrors().ShouldBeEmpty();

        // The frosted green sphere (right) is clearly greener than red or blue where the clear one (left) is not tinted.
        var green = RenderHarness.Pixel(image, 640, 300);
        green.G.ShouldBeGreaterThan((byte)(green.R + 15));
        var clear = RenderHarness.Pixel(image, 320, 300);
        Math.Abs(clear.G - clear.R).ShouldBeLessThan(60);
    }
}

public class RenderTargetTests
{
    [Fact]
    public void A_Render_Target_Can_Be_Used_As_A_Texture()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device);

        // Inner scene: a red sphere on blue, drawn into a texture.
        var inner = new Scene { Background = ColorF.FromSrgb(0f, 0f, 1f) };
        inner.Add(new Mesh(new SphereGeometry(1f), new BasicMaterial { Color = ColorF.FromSrgb(1f, 0f, 0f) }));
        var innerCamera = new OrthographicCamera(-2f, 2f, 2f, -2f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };
        using var screen = new RenderTarget(256, 256, Rhi.TextureFormat.RGBA8, samples: 4);
        renderer.Render(inner, innerCamera, screen);

        // Outer scene: a plane that shows that texture.
        var outer = new Scene { Background = ColorF.Black };
        outer.Add(new Mesh(new PlaneGeometry(2f, 2f), new BasicMaterial { Map = screen.Texture }));
        var camera = new OrthographicCamera(-2f, 2f, 2f, -2f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };
        var image = RenderHarness.Render(renderer, outer, camera, 200, 200, "feature-render-target", samples: 1);

        gl.Device.DrainErrors().ShouldBeEmpty();
        var center = RenderHarness.Pixel(image, 100, 100);
        center.R.ShouldBeGreaterThan((byte)240);
        center.B.ShouldBeLessThan((byte)15);
        var corner = RenderHarness.Pixel(image, 56, 56);
        corner.B.ShouldBeGreaterThan((byte)240);
        corner.R.ShouldBeLessThan((byte)15);
        RenderHarness.Pixel(image, 20, 20).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));

        // The sphere is at the top of the inner picture when it is moved up: the texture is not upside down.
        inner.Children[0].Position = new Vector3(0f, 1.5f, 0f);
        renderer.Render(inner, innerCamera, screen);
        image = RenderHarness.Render(renderer, outer, camera, 200, 200, null, samples: 1);
        RenderHarness.Pixel(image, 100, 60).R.ShouldBeGreaterThan((byte)240);
        RenderHarness.Pixel(image, 100, 140).B.ShouldBeGreaterThan((byte)240);
    }

    [Fact]
    public void Disposed_Resources_Are_Freed_And_Renderers_Can_Be_Replaced()
    {
        using var gl = GlTestContext.CreateOrSkip();
        var scene = new Scene { Background = ColorF.Black };
        var geometry = new BoxGeometry();
        var material = new StandardMaterial { Map = TestScenes.Checker(16, 2) };
        var mesh = new Mesh(geometry, material);
        scene.Add(mesh, new AmbientLight(3f));
        var camera = new OrthographicCamera(-1f, 1f, 1f, -1f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };

        var first = new Renderer(gl.Device);
        var a = RenderHarness.Pixel(RenderHarness.Render(first, scene, camera, 64, 64, null, samples: 1), 32, 32);
        geometry.Binding.ShouldNotBeNull();
        first.Dispose();

        // The scene's resources carry nothing of the disposed renderer and draw the same with a new one.
        geometry.Binding.ShouldBeNull();
        material.Binding.ShouldBeNull();
        material.Map!.Binding.ShouldBeNull();
        using var second = new Renderer(gl.Device);
        var b = RenderHarness.Pixel(RenderHarness.Render(second, scene, camera, 64, 64, null, samples: 1), 32, 32);
        b.ShouldBe(a);

        // Disposing a geometry that is still in the scene is reported instead of drawing garbage.
        scene.Remove(mesh);
        geometry.Dispose();
        RenderHarness.Render(second, scene, camera, 64, 64, null, samples: 1);
        geometry.Binding.ShouldBeNull();
        gl.Device.DrainErrors().ShouldBeEmpty();
    }
}

/// <summary>The layers of <see cref="PhysicalMaterial"/> beyond clear coat, sheen and transmission.</summary>
public class PhysicalLayerTests
{
    private static Scene LitScene(out PerspectiveCamera camera)
    {
        var scene = new Scene { Background = ColorF.FromHex(0x101216), Environment = TestScenes.SkyEnvironment() };
        scene.Add(new DirectionalLight(3f) { Position = new Vector3(2f, 4f, 3f) });
        camera = new PerspectiveCamera(35f, 16f / 9f, 0.1f, 50f) { Position = new Vector3(0f, 0.4f, 7.5f) };
        camera.LookAt(Vector3.Zero);
        return scene;
    }

    [Fact]
    public void Iridescence_Tints_The_Reflection_By_Film_Thickness()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var scene = LitScene(out var camera);
        var sphere = new SphereGeometry(0.8f, 64, 32);
        float[] thickness = [0f, 200f, 400f, 800f];
        for (var i = 0; i < thickness.Length; i++)
        {
            scene.Add(new Mesh(sphere, new PhysicalMaterial
            {
                Color = ColorF.FromHex(0xb0b0b0),
                Metalness = 1f,
                Roughness = 0.2f,
                Iridescence = 1f,
                IridescenceIor = 1.6f,
                IridescenceThicknessRange = new Vector2(0f, thickness[i]),
            })
            {
                Position = new Vector3((i - 1.5f) * 1.9f, 0f, 0f),
            });
        }

        var image = RenderHarness.Render(renderer, scene, camera, 960, 540, "feature-iridescence");
        gl.Device.DrainErrors().ShouldBeEmpty();

        // How far, on average, the share of red, green and blue at a point of one sphere is from that at the same
        // point of another. A film shifts the color of the reflection, differently for each angle and thickness.
        float TintDistance(int a, int b)
        {
            var sum = 0f;
            var count = 0;
            for (var y = 190; y < 350; y += 4)
            {
                for (var dx = -70; dx < 70; dx += 4)
                {
                    var p = RenderHarness.Pixel(image, a + dx, y);
                    var q = RenderHarness.Pixel(image, b + dx, y);
                    var u = new Vector3(p.R, p.G, p.B) / Math.Max(p.R + p.G + p.B, 1);
                    var v = new Vector3(q.R, q.G, q.B) / Math.Max(q.R + q.G + q.B, 1);
                    sum += Vector3.Distance(u, v);
                    count++;
                }
            }

            return sum / count;
        }

        int Column(int i) => (int)(480 + ((i - 1.5f) * 1.9f / 7.5f * 540f / (2f * MathF.Tan(17.5f * MathF.PI / 180f))));
        TintDistance(Column(1), Column(0)).ShouldBeGreaterThan(0.05f);
        TintDistance(Column(2), Column(0)).ShouldBeGreaterThan(0.05f);
        TintDistance(Column(3), Column(0)).ShouldBeGreaterThan(0.05f);
        TintDistance(Column(2), Column(1)).ShouldBeGreaterThan(0.05f);
        TintDistance(Column(3), Column(2)).ShouldBeGreaterThan(0.05f);
    }

    [Fact]
    public void Anisotropy_Stretches_The_Highlight_Across_Its_Direction()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.None };
        var scene = new Scene { Background = ColorF.Black };
        scene.Add(new PointLight(ColorF.White, 40f) { Position = new Vector3(0f, 0f, 4f) });
        var camera = new PerspectiveCamera(35f, 16f / 9f, 0.1f, 50f) { Position = new Vector3(0f, 0f, 7.5f) };
        camera.LookAt(Vector3.Zero);
        var sphere = new SphereGeometry(0.8f, 96, 48);

        // The last sphere carries vertex tangents; the others leave the tangent frame to screen-space derivatives.
        var withTangents = new SphereGeometry(0.8f, 96, 48);
        withTangents.ComputeTangents();
        (float Strength, float Rotation)[] cases = [(0f, 0f), (1f, 0f), (1f, MathF.PI / 2f), (1f, 0f)];
        for (var i = 0; i < cases.Length; i++)
        {
            scene.Add(new Mesh(i == 3 ? withTangents : sphere, new PhysicalMaterial
            {
                Color = ColorF.FromHex(0xb0b0b0),
                Metalness = 1f,
                Roughness = 0.3f,
                Anisotropy = cases[i].Strength,
                AnisotropyRotation = cases[i].Rotation,
            })
            {
                Position = new Vector3((i - 1.5f) * 1.9f, 0f, 0f),
            });
        }

        var image = RenderHarness.Render(renderer, scene, camera, 960, 540, "feature-anisotropy");
        gl.Device.DrainErrors().ShouldBeEmpty();

        // The extent of the bright highlight along x and along y, in pixels, around each sphere.
        (int Width, int Height) Extent(int cx)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            for (var y = 150; y < 390; y++)
            {
                for (var x = cx - 100; x < cx + 100; x++)
                {
                    var p = RenderHarness.Pixel(image, x, y);
                    if (p.R + p.G + p.B > 600)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            return minX > maxX ? (0, 0) : (maxX - minX + 1, maxY - minY + 1);
        }

        var scale = 540f / (2f * MathF.Tan(17.5f * MathF.PI / 180f)) / 7.5f;
        int Column(int i) => (int)(480 + ((i - 1.5f) * 1.9f * scale));
        var round = Extent(Column(0));
        var alongTangent = Extent(Column(1));
        var turned = Extent(Column(2));
        var fromVertexTangents = Extent(Column(3));
        round.Width.ShouldBeGreaterThan(4);
        ((float)round.Width / round.Height).ShouldBeInRange(0.7f, 1.4f);

        // A sphere's tangent runs around it (horizontally here): rougher along it, so the highlight grows wide; turned
        // by a quarter it grows tall instead.
        ((float)alongTangent.Width / alongTangent.Height).ShouldBeGreaterThan(1.8f);
        ((float)turned.Height / turned.Width).ShouldBeGreaterThan(1.8f);
        ((float)fromVertexTangents.Width / fromVertexTangents.Height).ShouldBeGreaterThan(1.8f);
    }

    [Fact]
    public void A_Clearcoat_Normal_Map_Bends_Only_The_Coat()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.None };
        var scene = new Scene { Background = ColorF.Black };
        scene.Add(new DirectionalLight(3f) { Position = new Vector3(0f, 0f, 5f) });
        var camera = new OrthographicCamera(-2f, 2f, 1f, -1f, 0.1f, 10f) { Position = new Vector3(0f, 0f, 5f) };

        // A normal map that leans every normal 45 degrees toward +X.
        var lean = MathF.Sqrt(0.5f);
        var texel = new byte[] { (byte)(((lean * 0.5f) + 0.5f) * 255f), 128, (byte)(((lean * 0.5f) + 0.5f) * 255f), 255 };
        var map = new Texture(new TextureImage(1, 1, texel), TextureColorSpace.Linear);
        PhysicalMaterial Coated(Texture? normal) => new()
        {
            Color = ColorF.Black,
            Roughness = 1f,
            Clearcoat = 1f,
            ClearcoatRoughness = 0.2f,
            ClearcoatNormalMap = normal,
        };
        scene.Add(new Mesh(new PlaneGeometry(1.6f, 1.6f), Coated(null)) { Position = new Vector3(-1f, 0f, 0f) });
        scene.Add(new Mesh(new PlaneGeometry(1.6f, 1.6f), Coated(map)) { Position = new Vector3(1f, 0f, 0f) });

        var image = RenderHarness.Render(renderer, scene, camera, 800, 400, "feature-clearcoat-normal");
        gl.Device.DrainErrors().ShouldBeEmpty();

        // Light and view both face the plane: the flat coat mirrors the light into the camera, the leaning coat does not.
        var flat = RenderHarness.Pixel(image, 200, 200);
        var leaning = RenderHarness.Pixel(image, 600, 200);
        flat.G.ShouldBeGreaterThan((byte)128);
        ((int)leaning.G).ShouldBeLessThan(flat.G / 5);
    }
}
