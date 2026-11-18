using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class GltfRenderTests
{
    /// <summary>A file with an unlit textured quad (left) and a lit, vertex-colored, normal-mapped quad with its own
    /// node transform (right), both using one 2 x 2 texture: red, green / blue, white.</summary>
    private static byte[] BuildScene()
    {
        var b = new GltfBuilder();
        var png = Png.Encode(2, 2, [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255]);
        var image = b.View(png);
        // A flat normal map: straight up everywhere.
        var flat = b.View(Png.Encode(1, 1, [128, 128, 255, 255]));
        // glTF UVs: (0, 0) is the top-left corner of the image.
        var position = b.Floats("VEC3", -1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0);
        var normal = b.Floats("VEC3", 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1);
        var uv = b.Floats("VEC2", 0, 1, 1, 1, 1, 0, 0, 0);
        var index = b.UShorts("SCALAR", false, 0, 1, 2, 0, 2, 3);
        var color = b.UBytes("VEC4", true, 255, 255, 255, 255, 255, 255, 255, 255, 255, 128, 0, 255, 255, 128, 0, 255);
        return b.Glb(
            "\"scenes\":[{\"nodes\":[0,1]}],\"nodes\":["
            + "{\"name\":\"picture\",\"mesh\":0,\"translation\":[-1.25,0,0]},"
            + "{\"name\":\"panel\",\"mesh\":1,\"translation\":[1.25,0,0],\"rotation\":[0,-0.2588190,0,0.9659258]}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"TEXCOORD_0\":{uv}}},\"indices\":{index},\"material\":0}}]}},"
            + $"{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"NORMAL\":{normal},\"TEXCOORD_0\":{uv},\"COLOR_0\":{color}}},\"indices\":{index},\"material\":1}}]}}],"
            + $"\"images\":[{{\"bufferView\":{image},\"mimeType\":\"image/png\"}},{{\"bufferView\":{flat},\"mimeType\":\"image/png\"}}],"
            + "\"samplers\":[{\"magFilter\":9728,\"minFilter\":9728,\"wrapS\":33071,\"wrapT\":33071}],"
            + "\"textures\":[{\"source\":0,\"sampler\":0},{\"source\":1}],"
            + "\"materials\":[{\"extensions\":{\"KHR_materials_unlit\":{}},\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0}}},"
            + "{\"doubleSided\":true,\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0},\"metallicFactor\":0,\"roughnessFactor\":0.7},\"normalTexture\":{\"index\":1}}]");
    }

    [Fact]
    public void Loaded_Textured_Model_Renders_With_Its_Images_The_Right_Way_Up()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device);
        using var asset = GltfLoader.Load(BuildScene());
        asset.Warnings.ShouldBeEmpty();
        var scene = new Scene { Background = ColorF.FromHex(0x202020) };
        scene.Add(asset.Scene);
        scene.Add(new DirectionalLight(3f) { Position = new Vector3(0f, 0f, 5f) }, new AmbientLight(0.6f));
        var camera = new PerspectiveCamera(40f, 2f, 0.1f, 50f) { Position = new Vector3(0f, 0f, 5f) };
        camera.LookAt(Vector3.Zero);

        var image = RenderHarness.Render(renderer, scene, camera, 800, 400, "gltf-textured");

        gl.Device.DrainErrors().ShouldBeEmpty();
        // Where the corners of the picture land on screen.
        (int X, int Y) At(float x, float y)
        {
            var ndc = camera.Project(new Vector3(-1.25f + x, y, 0f));
            return ((int)((ndc.X + 1f) * 400f), (int)((1f - ndc.Y) * 200f));
        }

        // Unlit and without tone mapping the texture colors come out as they went in.
        var (x, y) = At(-0.5f, 0.5f);
        RenderHarness.Pixel(image, x, y).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255), "top left is red");
        (x, y) = At(0.5f, 0.5f);
        RenderHarness.Pixel(image, x, y).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255), "top right is green");
        (x, y) = At(-0.5f, -0.5f);
        RenderHarness.Pixel(image, x, y).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255), "bottom left is blue");
        (x, y) = At(0.5f, -0.5f);
        RenderHarness.Pixel(image, x, y).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255), "bottom right is white");

        // The lit panel. Its vertex colors run from white at the bottom edge to orange at the top edge, so the white
        // texel is white only near the bottom.
        var panel = asset.Scene.Find("panel").ShouldNotBeNull();
        var ndcWhite = camera.Project(panel.LocalToWorld(new Vector3(0.5f, -0.95f, 0f)));
        var white = RenderHarness.Pixel(image, (int)((ndcWhite.X + 1f) * 400f), (int)((1f - ndcWhite.Y) * 200f));
        white.R.ShouldBeGreaterThan((byte)120);
        Math.Abs(white.R - white.G).ShouldBeLessThan(6);
        Math.Abs(white.R - white.B).ShouldBeLessThan(6);

        // Toward the top the orange vertex color dims green and removes blue.
        var ndcGreen = camera.Project(panel.LocalToWorld(new Vector3(0.5f, 0.8f, 0f)));
        var green = RenderHarness.Pixel(image, (int)((ndcGreen.X + 1f) * 400f), (int)((1f - ndcGreen.Y) * 200f));
        green.G.ShouldBeGreaterThan(green.R);
        green.G.ShouldBeLessThan(white.G);
        // The texel has no blue; what little there is comes from the white highlight.
        ((int)green.B).ShouldBeLessThan(green.G / 2);
    }

    [Fact]
    public void Loaded_Skinned_And_Morphed_Meshes_Render_Posed()
    {
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device);
        var b = new GltfBuilder();
        // A strip of two quads along Y: the lower one follows the first joint, the upper one the second.
        var position = b.Floats("VEC3", -0.25f, 0, 0, 0.25f, 0, 0, -0.25f, 1, 0, 0.25f, 1, 0, -0.25f, 2, 0, 0.25f, 2, 0);
        var joints = b.UBytes("VEC4", false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);
        var weights = b.Floats("VEC4", 1, 0, 0, 0, 1, 0, 0, 0, 0.5f, 0.5f, 0, 0, 0.5f, 0.5f, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);
        var index = b.UShorts("SCALAR", false, 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4);
        // Four joints (two of them unused) and, below, four morph targets: the renderer currently needs the bone and
        // weight data it stores in front of the object records to be a multiple of four texels.
        var inverse = b.Floats(
            "MAT4",
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1,
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, -1, 0, 1,
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, -2, 0, 1,
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, -3, 0, 1);
        // A second mesh: a quad whose first morph target moves its top edge two units to the right.
        var quad = b.Floats("VEC3", -0.25f, 0, 0, 0.25f, 0, 0, -0.25f, 1, 0, 0.25f, 1, 0);
        var quadIndex = b.UShorts("SCALAR", false, 0, 1, 2, 1, 3, 2);
        var lean = b.Floats("VEC3", 0, 0, 0, 0, 0, 0, 2, 0, 0, 2, 0, 0);
        var sink = b.Floats("VEC3", 0, -5, 0, 0, -5, 0, 0, -5, 0, 0, -5, 0);
        var material = "{\"extensions\":{\"KHR_materials_unlit\":{}},\"doubleSided\":true,\"pbrMetallicRoughness\":{\"baseColorFactor\":[1,1,1,1]}}";
        var glb = b.Glb(
            "\"scenes\":[{\"nodes\":[0,1,5]}],\"nodes\":["
            + "{\"name\":\"arm\",\"mesh\":0,\"skin\":0},{\"name\":\"shoulder\",\"children\":[2]},{\"name\":\"elbow\",\"translation\":[0,1,0],\"children\":[3]},"
            + "{\"name\":\"wrist\",\"translation\":[0,1,0],\"children\":[4]},{\"name\":\"tip\",\"translation\":[0,1,0]},"
            + "{\"name\":\"flag\",\"mesh\":1,\"translation\":[-2,-2,0]}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"JOINTS_0\":{joints},\"WEIGHTS_0\":{weights}}},\"indices\":{index},\"material\":0}}]}},"
            + $"{{\"weights\":[1,0,0,0],\"primitives\":[{{\"attributes\":{{\"POSITION\":{quad}}},\"indices\":{quadIndex},\"material\":0,"
            + $"\"targets\":[{{\"POSITION\":{lean}}},{{\"POSITION\":{sink}}},{{\"POSITION\":{sink}}},{{\"POSITION\":{sink}}}]}}]}}],"
            + $"\"skins\":[{{\"joints\":[1,2,3,4],\"inverseBindMatrices\":{inverse}}}],\"materials\":[{material}]");
        using var asset = GltfLoader.Load(glb);
        asset.Warnings.ShouldBeEmpty();
        var scene = new Scene { Background = ColorF.Black };
        scene.Add(asset.Scene);
        var camera = new OrthographicCamera(-3f, 3f, 3f, -3f, 0.1f, 20f) { Position = new Vector3(0f, 0f, 10f) };

        // Bend the elbow a quarter turn: the upper quad swings from +Y to -X (a positive turn about Z).
        asset.Scene.Find("elbow")!.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);
        var image = RenderHarness.Render(renderer, scene, camera, 300, 300, "gltf-skinned", samples: 1);

        gl.Device.DrainErrors().ShouldBeEmpty();
        static (int X, int Y) At(float x, float y) => ((int)((x + 3f) * 50f), (int)((3f - y) * 50f));
        bool Lit(float x, float y)
        {
            var (px, py) = At(x, y);
            return RenderHarness.Pixel(image, px, py).R > 128;
        }

        Lit(0f, 0.3f).ShouldBeTrue("the lower arm stays on the Y axis");
        Lit(0f, 1.7f).ShouldBeFalse("the upper arm left its bind position");
        Lit(-0.8f, 1f).ShouldBeTrue("the upper arm now points along -X from the elbow");

        // The first morph target at full weight leans the flag's top edge two units to the right; the others, at
        // weight zero, would have moved it out of view.
        Lit(-2f, -1.95f).ShouldBeTrue("the flag's bottom edge stays");
        Lit(-2f, -1.1f).ShouldBeFalse("the flag's top edge moved away");
        Lit(-0.1f, -1.05f).ShouldBeTrue("the flag's top edge is two units to the right");
    }
}
