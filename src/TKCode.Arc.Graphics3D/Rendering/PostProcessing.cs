using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

/// <summary>Glow around bright parts of the picture.</summary>
public sealed class BloomSettings
{
    public bool Enabled { get; set; }

    /// <summary>How much glow is added (0 = none).</summary>
    public float Strength { get; set; } = 0.6f;

    /// <summary>How far the glow spreads, 0..1.</summary>
    public float Radius { get; set; } = 0.7f;

    /// <summary>Scene brightness above which a pixel glows (1 = just above white).</summary>
    public float Threshold { get; set; } = 1f;

    /// <summary>Softness of the threshold: pixels within this range below it glow a little.</summary>
    public float Knee { get; set; } = 0.5f;

    /// <summary>Number of blur levels (3 to 8); more spreads the glow wider.</summary>
    public int Levels { get; set; } = 6;
}

/// <summary>Darkening toward the corners.</summary>
public sealed class VignetteSettings
{
    public bool Enabled { get; set; }

    /// <summary>How dark the corners get, 0..1.</summary>
    public float Intensity { get; set; } = 0.35f;

    /// <summary>How gradually the darkening starts, 0..1.</summary>
    public float Smoothness { get; set; } = 0.5f;
}

/// <summary>Darkening of creases and contact areas, computed from the depth of the picture (screen-space ambient
/// occlusion).</summary>
public sealed class AmbientOcclusionSettings
{
    public bool Enabled { get; set; }

    /// <summary>How far, in world units, surfaces shade each other.</summary>
    public float Radius { get; set; } = 0.5f;

    /// <summary>How dark occluded areas get; 1 is the computed amount.</summary>
    public float Intensity { get; set; } = 1f;

    /// <summary>Depth tolerance in world units; larger values remove self-shading on flat surfaces.</summary>
    public float Bias { get; set; } = 0.03f;

    /// <summary>Samples per pixel (8 to 32).</summary>
    public int Samples { get; set; } = 16;
}

/// <summary>Blur that grows with the distance from a focus plane, like a camera lens.</summary>
public sealed class DepthOfFieldSettings
{
    public bool Enabled { get; set; }

    /// <summary>Distance from the camera that is sharp, in world units.</summary>
    public float FocusDistance { get; set; } = 5f;

    /// <summary>Depth of the fully sharp zone around <see cref="FocusDistance"/>, in world units.</summary>
    public float FocusRange { get; set; } = 1f;

    /// <summary>Distance beyond the sharp zone over which the blur grows to its maximum, in world units.</summary>
    public float Falloff { get; set; } = 4f;

    /// <summary>Largest blur radius in pixels.</summary>
    public float MaxBlur { get; set; } = 8f;
}

/// <summary>A colored line around chosen objects, for selection and hover. The line follows the objects' silhouette
/// and shows through whatever is in front of them.</summary>
public sealed class OutlineSettings
{
    /// <summary>The objects to outline; each one stands for itself and everything below it.</summary>
    public List<Object3D> Objects { get; } = [];

    /// <summary>The line color.</summary>
    public ColorF Color { get; set; } = ColorF.FromHex(0x3b82f6);

    /// <summary>Line width in pixels.</summary>
    public float Thickness { get; set; } = 2f;

    /// <summary>Opacity of the line, 0..1.</summary>
    public float Strength { get; set; } = 1f;

    /// <summary>True when there is something to outline.</summary>
    public bool Enabled => Objects.Count > 0 && Strength > 0f && Thickness > 0f;
}

/// <summary>A full-screen pass of your own, built from shader nodes like a material: it computes each pixel of the
/// picture from the picture so far (<see cref="Input"/>), the position on screen (<see cref="Uv"/>), uniforms and
/// textures. Passes run in list order on the linear, scene-referred picture, after depth of field and before bloom,
/// so the built-in grading and tone mapping still follow.</summary>
/// <example><code>
/// // Split the red and blue channels sideways.
/// var amount = Sl.Uniform(0.004f);
/// var pass = new PostPass(Sl.Vec3(
///     PostPass.Input(PostPass.Uv + Sl.Vec2(amount, 0f)).X,
///     PostPass.Input().Y,
///     PostPass.Input(PostPass.Uv - Sl.Vec2(amount, 0f)).Z));
/// renderer.PostProcessing.Passes.Add(pass);
/// </code></example>
public sealed class PostPass
{
    private ShaderNode _color;

    /// <summary>Creates a pass that writes <paramref name="color"/> (RGB keeps the picture's alpha; RGBA replaces it).</summary>
    public PostPass(ShaderNode color) => _color = color ?? throw new ArgumentNullException(nameof(color));

    /// <summary>The color of each pixel. Surface inputs (positions, normals, vertex colors) do not exist here: use
    /// <see cref="Uv"/>, <see cref="Sl.Time"/>, uniforms and textures.</summary>
    public ShaderNode Color
    {
        get => _color;
        set
        {
            _color = value ?? throw new ArgumentNullException(nameof(value));
            Version++;
        }
    }

    /// <summary>False skips the pass.</summary>
    public bool Enabled { get; set; } = true;

    internal int Version { get; private set; }

    /// <summary>The position on screen, 0..1 from the bottom left corner.</summary>
    public static ShaderNode Uv => Sl.Uv;

    /// <summary>The picture before this pass at a position on screen (default: this pixel), as linear RGBA. Values
    /// above 1 are highlights that bloom and tone mapping will handle.</summary>
    public static ShaderNode Input(ShaderNode? uv = null) => Sl.Raw(ShaderType.Vec4, "texture(u_input, {0})", uv ?? Sl.Uv);
}

/// <summary>Effects applied to the finished picture, in a fixed order: ambient occlusion, depth of field, bloom,
/// vignette, color grading, tone mapping, outlines, then anti-aliasing. All are off by default, and when all are off
/// the renderer skips the extra passes.</summary>
public sealed class PostProcessing
{
    /// <summary>Darker creases and contact areas.</summary>
    public AmbientOcclusionSettings AmbientOcclusion { get; } = new();

    /// <summary>Blur away from a focus distance.</summary>
    public DepthOfFieldSettings DepthOfField { get; } = new();

    /// <summary>A line around chosen objects.</summary>
    public OutlineSettings Outline { get; } = new();

    /// <summary>Glow around bright parts.</summary>
    public BloomSettings Bloom { get; } = new();

    /// <summary>Darker corners.</summary>
    public VignetteSettings Vignette { get; } = new();

    /// <summary>Full-screen passes of your own, run in order after depth of field and before bloom.</summary>
    public List<PostPass> Passes { get; } = [];

    /// <summary>Fast approximate anti-aliasing on the final picture. Smooths what multisampling does not: shader
    /// aliasing, alpha-tested edges and highlights.</summary>
    public bool Fxaa { get; set; }

    /// <summary>Color saturation; 1 leaves it, 0 is grayscale.</summary>
    public float Saturation { get; set; } = 1f;

    /// <summary>Contrast about middle gray; 1 leaves it.</summary>
    public float Contrast { get; set; } = 1f;

    /// <summary>True when any effect would change the picture.</summary>
    public bool IsActive => Bloom.Enabled || Vignette.Enabled || Fxaa || Saturation != 1f || Contrast != 1f
        || AmbientOcclusion.Enabled || DepthOfField.Enabled || Outline.Enabled || HasCustomPass;

    private bool HasCustomPass
    {
        get
        {
            foreach (var pass in Passes)
            {
                if (pass.Enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>True when an effect reads the depth of the picture.</summary>
    internal bool NeedsDepth => AmbientOcclusion.Enabled || DepthOfField.Enabled;
}
