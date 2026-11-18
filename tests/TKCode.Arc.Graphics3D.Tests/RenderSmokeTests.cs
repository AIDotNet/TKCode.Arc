using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class RenderSmokeTests
{
    [Fact]
    public void Lit_Scene_With_Shadows_Renders()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic };
        var scene = new Scene { Background = ColorF.FromHex(0x20242c) };
        var floor = new Mesh(new PlaneGeometry(12f, 12f), new StandardMaterial { Color = ColorF.FromHex(0x8a8f98), Roughness = 0.9f })
        {
            ReceiveShadow = true,
        };
        floor.RotateX(-MathF.PI / 2f);
        var box = new Mesh(new BoxGeometry(1f, 1f, 1f), new StandardMaterial { Color = ColorF.FromHex(0xe5484d), Roughness = 0.4f })
        {
            Position = new Vector3(-1.2f, 0.5f, 0f),
            CastShadow = true,
            ReceiveShadow = true,
        };
        box.RotateY(0.5f);
        var sphere = new Mesh(new SphereGeometry(0.6f, 48, 24), new StandardMaterial { Color = ColorF.FromHex(0x3e63dd), Roughness = 0.25f, Metalness = 0.1f })
        {
            Position = new Vector3(0.9f, 0.6f, 0.3f),
            CastShadow = true,
            ReceiveShadow = true,
        };
        var sun = new DirectionalLight(3f) { Position = new Vector3(3f, 5f, 2f), CastShadow = true };
        sun.Shadow.MapSize = 2048;
        scene.Add(floor, box, sphere, sun, new AmbientLight(0.4f), new HemisphereLight(ColorF.FromHex(0xbfd4ff), ColorF.FromHex(0x30281c), 0.6f));
        var camera = new PerspectiveCamera(45f, 4f / 3f, 0.1f, 100f) { Position = new Vector3(3.2f, 2.6f, 4.2f) };
        camera.LookAt(new Vector3(0f, 0.4f, 0f));

        var image = RenderHarness.Render(renderer, scene, camera, 800, 600, "smoke-lit");

        gl.Device.DrainErrors().ShouldBeEmpty();
        var center = RenderHarness.Pixel(image, 400, 300);
        (center.R + center.G + center.B).ShouldBeGreaterThan(30);
        renderer.Statistics.DrawCalls.ShouldBeGreaterThan(3);
    }
}
