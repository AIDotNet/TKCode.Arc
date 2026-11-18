using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class GeometryRenderTests
{
    [Fact]
    public void Every_Geometry_Renders_Lit_From_Outside()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        var scene = new Scene { Background = ColorF.FromHex(0x1c1f26) };
        var heart = new Shape2().MoveTo(0f, -0.45f).BezierCurveTo(-0.9f, 0.1f, -0.45f, 0.75f, 0f, 0.3f).BezierCurveTo(0.45f, 0.75f, 0.9f, 0.1f, 0f, -0.45f);
        var frame = new Shape2([new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f)]);
        var window = new Path2();
        window.AbsArc(0f, 0f, 0.28f, 0f, MathF.PI * 2f, true);
        frame.Holes.Add(window);
        var knotPath = new CatmullRomCurve3([new(-0.45f, -0.3f, 0f), new(-0.1f, 0.4f, 0.3f), new(0.2f, -0.3f, -0.3f), new(0.45f, 0.35f, 0f)]);
        BufferGeometry[] geometries =
        [
            new BoxGeometry(0.8f, 0.8f, 0.8f), new SphereGeometry(0.5f), new CylinderGeometry(0.4f, 0.4f, 0.9f), new ConeGeometry(0.45f, 0.9f),
            new CapsuleGeometry(0.3f, 0.5f, 8, 16), new TorusGeometry(0.38f, 0.14f, 16, 48), new TorusKnotGeometry(0.3f, 0.1f, 128, 12),
            new RoundedBoxGeometry(0.8f, 0.8f, 0.8f, 4, 0.16f), new TetrahedronGeometry(0.55f), new OctahedronGeometry(0.5f), new DodecahedronGeometry(0.5f),
            new IcosahedronGeometry(0.5f), new IcosahedronGeometry(0.5f, 3), new LatheGeometry([new(0f, -0.45f), new(0.35f, -0.4f), new(0.15f, 0f), new(0.4f, 0.4f), new(0.42f, 0.45f)], 32),
            new TubeGeometry(knotPath, 64, 0.08f, 12), new ExtrudeGeometry(heart, new ExtrudeOptions { Depth = 0.2f, BevelThickness = 0.05f, BevelSize = 0.04f, BevelSegments = 4 }),
            new ExtrudeGeometry(frame, new ExtrudeOptions { Depth = 0.25f, BevelEnabled = false, CurveSegments = 24 }), new ShapeGeometry(heart),
            new CircleGeometry(0.5f), new RingGeometry(0.25f, 0.5f), new PlaneGeometry(0.9f, 0.9f),
            new CylinderGeometry(0.4f, 0.25f, 0.8f, 24, 1, true, 0f, MathF.PI * 1.5f),
        ];
        const int Columns = 6;
        for (var i = 0; i < geometries.Length; i++)
        {
            // Open surfaces (lathe, tube, flat shapes, the cut cylinder) are shown from both sides.
            var open = geometries[i] is LatheGeometry or TubeGeometry or ShapeGeometry or CircleGeometry or RingGeometry or PlaneGeometry
                || (geometries[i] is CylinderGeometry && i == geometries.Length - 1);
            var material = new StandardMaterial
            {
                Color = ColorF.FromHsl(i / (float)geometries.Length, 0.6f, 0.55f), Roughness = 0.45f, Side = open ? Side.Double : Side.Front,
            };
            var mesh = new Mesh(geometries[i], material)
            {
                Position = new Vector3(((i % Columns) - ((Columns - 1) / 2f)) * 1.25f, 1.9f - ((i / Columns) * 1.25f), 0f),
            };
            mesh.RotateX(0.45f);
            mesh.RotateY(0.6f);
            scene.Add(mesh);
        }

        // Edges and wireframe, as lines.
        scene.Add(new LineSegments(new EdgesGeometry(new BoxGeometry(0.8f, 0.8f, 0.8f, 3, 3, 3)), new LineMaterial { Color = ColorF.FromHex(0xffd166) })
        {
            Position = new Vector3(1.875f, -1.85f, 0f), EulerAngles = new Vector3(0.45f, 0.6f, 0f),
        });
        scene.Add(new LineSegments(new WireframeGeometry(new IcosahedronGeometry(0.5f, 1)), new LineMaterial { Color = ColorF.FromHex(0x8ecae6) })
        {
            Position = new Vector3(3.125f, -1.85f, 0f), EulerAngles = new Vector3(0.45f, 0.6f, 0f),
        });
        scene.Add(new DirectionalLight(3f) { Position = new Vector3(3f, 5f, 6f) }, new AmbientLight(0.7f));
        var camera = new PerspectiveCamera(40f, 1200f / 900f, 0.1f, 100f) { Position = new Vector3(0f, 0f, 8.2f) };
        camera.LookAt(Vector3.Zero);

        var image = RenderHarness.Render(renderer, scene, camera, 1200, 900, "geometry-gallery");

        gl.Device.DrainErrors().ShouldBeEmpty();

        // Every cell shows something: its center is not the background.
        var background = RenderHarness.Pixel(image, 5, 5);
        for (var i = 0; i < geometries.Length; i++)
        {
            var ndc = camera.Project(scene.Children[i].Position);
            var x = (int)(((ndc.X * 0.5f) + 0.5f) * image.Width);
            var y = (int)((0.5f - (ndc.Y * 0.5f)) * image.Height);
            var hit = false;
            // Wide enough to reach past the hole of the ring.
            for (var dy = -48; dy <= 48 && !hit; dy += 6)
            {
                for (var dx = -48; dx <= 48 && !hit; dx += 6)
                {
                    hit = RenderHarness.Pixel(image, x + dx, y + dy) != background;
                }
            }

            hit.ShouldBeTrue($"geometry {i} ({geometries[i].Name}) is not visible");
        }
    }

    /// <summary>Renders one throw-away frame. In a scene without shadows the renderer creates its placeholder shadow
    /// texture while the first frame's textures are already bound, which unbinds the per-frame data texture: the first
    /// frame comes out empty (reported to the renderer's owner). From the second frame on everything exists.</summary>

    [Fact]
    public void Every_Helper_Renders()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device);
        var scene = new Scene { Background = ColorF.FromHex(0x1c1f26) };
        scene.Add(new GridHelper(10f, 10), new AxesHelper(2f));
        var polar = new PolarGridHelper(2f, 8, 4, 48) { Position = new Vector3(-3.5f, 0.01f, 3f) };
        scene.Add(polar);
        var box = new Mesh(new BoxGeometry(1f, 1f, 1f), new StandardMaterial { Color = ColorF.FromHex(0x3e63dd) }) { Position = new Vector3(-3f, 0.8f, -2f) };
        box.RotateY(0.6f);
        scene.Add(box, new BoxHelper(box), new Box3Helper(new BoundingBox(new Vector3(-4.2f, 0f, -0.8f), new Vector3(-3.2f, 0.6f, 0.2f)), ColorF.FromHex(0xff5d8f)));
        scene.Add(new ArrowHelper(Vector3.Normalize(new Vector3(1f, 1f, 0f)), new Vector3(0f, 0f, 3f), 2f, ColorF.FromHex(0xffd166)));
        scene.Add(new PlaneHelper(new Plane(Vector3.UnitX, -3.5f), 2f, ColorF.FromHex(0x06d6a0)));
        var sun = new DirectionalLight(ColorF.FromHex(0xffe08a), 3f) { Position = new Vector3(2f, 4f, -3f), Target = new Vector3(0f, 0f, -1f) };
        var point = new PointLight(ColorF.FromHex(0xff6b6b), 20f) { Position = new Vector3(-1f, 1.5f, 1.5f) };
        var spot = new SpotLight(ColorF.FromHex(0x8ecae6), 30f, 5f, 0.4f) { Position = new Vector3(3f, 3f, 2f), Target = new Vector3(2f, 0f, 1f) };
        var hemisphere = new HemisphereLight(ColorF.FromHex(0x9ad1ff), ColorF.FromHex(0x8a5a2b), 0.8f) { Position = new Vector3(0f, 3.5f, 0f) };
        scene.Add(sun, point, spot, hemisphere);
        scene.Add(new DirectionalLightHelper(sun, 0.5f), new PointLightHelper(point, 0.25f), new SpotLightHelper(spot), new HemisphereLightHelper(hemisphere, 0.4f));
        var watched = new PerspectiveCamera(35f, 1.5f, 0.6f, 3f) { Position = new Vector3(1.5f, 1f, -4.5f) };
        watched.LookAt(new Vector3(1.5f, 0.6f, 0f));
        scene.Add(new CameraHelper(watched));
        var hip = new Bone { Position = new Vector3(3.6f, 0.2f, -1.5f) };
        var knee = new Bone { Position = new Vector3(0.2f, 0.9f, 0f) };
        var foot = new Bone { Position = new Vector3(-0.3f, 0.9f, 0.2f) };
        hip.Add(knee);
        knee.Add(foot);
        scene.Add(hip, new SkeletonHelper(hip));
        var camera = new PerspectiveCamera(45f, 1200f / 800f, 0.1f, 100f) { Position = new Vector3(4.5f, 6f, 9f) };
        camera.LookAt(new Vector3(0f, 0.6f, 0f));

        var image = RenderHarness.Render(renderer, scene, camera, 1200, 800, "helpers");

        gl.Device.DrainErrors().ShouldBeEmpty();
        var background = RenderHarness.Pixel(image, 5, 5);
        var drawn = 0;
        for (var y = 0; y < image.Height; y += 2)
        {
            for (var x = 0; x < image.Width; x += 2)
            {
                if (RenderHarness.Pixel(image, x, y) != background)
                {
                    drawn++;
                }
            }
        }

        drawn.ShouldBeGreaterThan(3000);
    }
}
