using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class EnvironmentTests
{
    [Fact]
    public void Image_Based_Lighting_Shades_By_Roughness_And_Metalness()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.AcesFilmic };
        var sky = TestScenes.SkyEnvironment();
        var scene = new Scene { Environment = sky, BackgroundTexture = sky };
        var geometry = new SphereGeometry(0.42f, 64, 32);
        for (var row = 0; row < 2; row++)
        {
            for (var i = 0; i < 6; i++)
            {
                var material = new StandardMaterial
                {
                    Color = row == 0 ? ColorF.FromHex(0xd4af37) : ColorF.FromHex(0xc0392b),
                    Metalness = row == 0 ? 1f : 0f,
                    Roughness = i / 5f,
                };
                scene.Add(new Mesh(geometry, material) { Position = new Vector3((i - 2.5f) * 1f, 0.5f - row, 0f) });
            }
        }

        var camera = new PerspectiveCamera(40f, 16f / 9f, 0.1f, 100f) { Position = new Vector3(0f, 0.2f, 6f) };
        camera.LookAt(Vector3.Zero);

        var image = RenderHarness.Render(renderer, scene, camera, 960, 540, "environment-spheres");

        gl.Device.DrainErrors().ShouldBeEmpty();
        // The mirror-like gold sphere (top left) reflects the sky; the image must not be black there.
        var p = RenderHarness.Pixel(image, 215, 215);
        (p.R + p.G + p.B).ShouldBeGreaterThan(60);
    }
}
