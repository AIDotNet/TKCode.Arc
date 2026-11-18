using System.Numerics;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

public sealed partial class Renderer
{
    private GpuTexture? _sceneColor;
    private GpuTexture? _sceneDepth;
    private GpuFramebuffer? _sceneColorFramebuffer;
    private float _sceneColorMaxLod;

    private static SamplerDescriptor SceneColorSampler => new()
    {
        MagFilter = TextureFilter.Linear,
        MinFilter = TextureFilter.Linear,
        MipFilter = MipFilter.Linear,
        WrapU = TextureWrap.ClampToEdge,
        WrapV = TextureWrap.ClampToEdge,
        WrapW = TextureWrap.ClampToEdge,
        MaxAnisotropy = 1f,
    };

    private void DropSceneColor(bool release)
    {
        if (release)
        {
            _sceneColorFramebuffer?.Dispose();
            _sceneColor?.Dispose();
            _sceneDepth?.Dispose();
        }

        _sceneColorFramebuffer = null;
        _sceneColor = null;
        _sceneDepth = null;
    }

    /// <summary>Draws the opaque objects and the background once more, in linear HDR at half size with a mip chain: the
    /// picture that transmissive surfaces refract (blurrier levels for rougher surfaces). Only runs in frames that have
    /// a visible transmissive surface.</summary>
    private void RenderSceneColor(Scene scene, ViewPass main, EnvironmentBinding? environment, ShaderFeatures sceneFeatures, RenderWorld world, int width, int height)
    {
        var w = Math.Max(1, width / 2);
        var h = Math.Max(1, height / 2);
        if (_sceneColor is null || _sceneColor.Width != w || _sceneColor.Height != h)
        {
            DropSceneColor(release: true);
            var mips = TextureDescriptor.FullMipCount(w, h);
            _sceneColor = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA16F, w, h, mips));
            _sceneDepth = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, w, h));
            _sceneColorFramebuffer = Device.CreateFramebuffer(new FramebufferDescriptor { Colors = [_sceneColor], Depth = _sceneDepth, Label = "sceneColor" });
            _sceneColorMaxLod = mips - 1;
            _sceneData[OffsetFogParams + 3] = _sceneColorMaxLod;
            Device.WriteBuffer(_sceneBuffer!, 0, System.Runtime.InteropServices.MemoryMarshal.AsBytes<float>(_sceneData));
        }

        // Scene-referred values: no tone mapping and no encoding, whatever the main pass does.
        var features = sceneFeatures & ~(ShaderFeatures.SrgbOutput | ShaderFeatures.ToneMappingMask);
        var background = (scene.Background ?? ClearColor).ToVector3();
        Device.BeginPass(new RenderPassDescriptor
        {
            Target = _sceneColorFramebuffer,
            ViewportWidth = w,
            ViewportHeight = h,
            ClearColor = true,
            ClearColorValue = new Vector4(background, 1f),
            ClearDepth = true,
            ClearDepthValue = 1f,
        });
        BindFrameResources(main, environment);
        DrawList(main, main.Opaque, main.OpaqueBase, features, world);
        DrawBackground(scene, main, environment, display: false);
        Device.EndPass();
        Device.GenerateMipmaps(_sceneColor!);
    }
}
