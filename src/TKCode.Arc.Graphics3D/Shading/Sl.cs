using System.Numerics;

namespace TKCode.Arc.Graphics3D.Shading;

/// <summary>The functions that build material graphs ("shading language"). Typical use:
/// <c>using static TKCode.Arc.Graphics3D.Shading.Sl;</c> and then
/// <c>material.ColorNode = Mix(colorA, colorB, Sin(Time) * 0.5f + 0.5f);</c>. Floats, vectors and colors convert to
/// nodes implicitly.</summary>
public static class Sl
{
    // ---- Inputs ----

    /// <summary>Vertex position in object space (vec3).</summary>
    public static ShaderNode PositionLocal { get; } = new InputNode(ShaderInput.PositionLocal);

    /// <summary>Vertex normal in object space (vec3).</summary>
    public static ShaderNode NormalLocal { get; } = new InputNode(ShaderInput.NormalLocal);

    /// <summary>Position in world space (vec3).</summary>
    public static ShaderNode PositionWorld { get; } = new InputNode(ShaderInput.PositionWorld);

    /// <summary>Surface normal in world space, facing the viewer (vec3).</summary>
    public static ShaderNode NormalWorld { get; } = new InputNode(ShaderInput.NormalWorld);

    /// <summary>Direction from the surface to the camera (vec3).</summary>
    public static ShaderNode ViewDirection { get; } = new InputNode(ShaderInput.ViewDirection);

    /// <summary>First texture coordinate set (vec2).</summary>
    public static ShaderNode Uv { get; } = new InputNode(ShaderInput.Uv);

    /// <summary>Second texture coordinate set (vec2).</summary>
    public static ShaderNode Uv1 { get; } = new InputNode(ShaderInput.Uv1);

    /// <summary>Vertex color (vec4).</summary>
    public static ShaderNode VertexColor { get; } = new InputNode(ShaderInput.VertexColor);

    /// <summary>Seconds since the renderer started (float).</summary>
    public static ShaderNode Time { get; } = new InputNode(ShaderInput.Time);

    /// <summary>Camera position in world space (vec3).</summary>
    public static ShaderNode CameraPosition { get; } = new InputNode(ShaderInput.CameraPosition);

    /// <summary>Position on the target in 0..1 (vec2, fragment stage).</summary>
    public static ShaderNode ScreenUv { get; } = new InputNode(ShaderInput.ScreenUv);

    /// <summary>Distance from the camera plane (float).</summary>
    public static ShaderNode ViewDepth { get; } = new InputNode(ShaderInput.ViewDepth);

    /// <summary>Coordinate inside a point sprite (vec2, point materials).</summary>
    public static ShaderNode PointCoord { get; } = new InputNode(ShaderInput.PointCoord);

    // ---- Constants and uniforms ----

    /// <summary>A float constant.</summary>
    public static ShaderNode Float(float value) => value;

    /// <summary>A vec2 from components or nodes.</summary>
    public static ShaderNode Vec2(ShaderNode x, ShaderNode y) => new ComposeNode(ShaderType.Vec2, [x, y]);

    /// <summary>A vec2 with both components equal.</summary>
    public static ShaderNode Vec2(ShaderNode value) => new ComposeNode(ShaderType.Vec2, [value]);

    /// <summary>A vec3 from three floats.</summary>
    public static ShaderNode Vec3(ShaderNode x, ShaderNode y, ShaderNode z) => new ComposeNode(ShaderType.Vec3, [x, y, z]);

    /// <summary>A vec3 from a vec2 and a float (in either order), or a float repeated.</summary>
    public static ShaderNode Vec3(ShaderNode a, ShaderNode b) => new ComposeNode(ShaderType.Vec3, [a, b]);

    /// <summary>A vec3 with all components equal, or the xyz of a vec4.</summary>
    public static ShaderNode Vec3(ShaderNode value) => value.Type == ShaderType.Vec4 ? value.Xyz : new ComposeNode(ShaderType.Vec3, [value]);

    /// <summary>A vec4 from four floats.</summary>
    public static ShaderNode Vec4(ShaderNode x, ShaderNode y, ShaderNode z, ShaderNode w) => new ComposeNode(ShaderType.Vec4, [x, y, z, w]);

    /// <summary>A vec4 from a vec3 and a float, or two vec2.</summary>
    public static ShaderNode Vec4(ShaderNode a, ShaderNode b) => new ComposeNode(ShaderType.Vec4, [a, b]);

    /// <summary>A vec4 with all components equal.</summary>
    public static ShaderNode Vec4(ShaderNode value) => new ComposeNode(ShaderType.Vec4, [value]);

    /// <summary>A linear color constant (vec3).</summary>
    public static ShaderNode Color(ColorF color) => color;

    /// <summary>A linear color constant from an sRGB hex value (vec3).</summary>
    public static ShaderNode Color(uint hex) => ColorF.FromHex(hex);

    /// <summary>A float uniform.</summary>
    public static UniformNode Uniform(float value) => new(ShaderType.Float, new Matrix4x4 { M11 = value });

    /// <summary>A vec2 uniform.</summary>
    public static UniformNode Uniform(Vector2 value) => new(ShaderType.Vec2, new Matrix4x4 { M11 = value.X, M12 = value.Y });

    /// <summary>A vec3 uniform.</summary>
    public static UniformNode Uniform(Vector3 value) => new(ShaderType.Vec3, new Matrix4x4 { M11 = value.X, M12 = value.Y, M13 = value.Z });

    /// <summary>A vec4 uniform.</summary>
    public static UniformNode Uniform(Vector4 value) => new(ShaderType.Vec4, new Matrix4x4 { M11 = value.X, M12 = value.Y, M13 = value.Z, M14 = value.W });

    /// <summary>A vec3 uniform holding a color.</summary>
    public static UniformNode Uniform(ColorF value) => Uniform(value.ToVector3());

    /// <summary>A mat4 uniform.</summary>
    public static UniformNode Uniform(Matrix4x4 value) => new(ShaderType.Mat4, value);

    // ---- Textures ----

    /// <summary>Samples a 2D texture at <paramref name="uv"/> (default: the first UV set). Returns RGBA (vec4).</summary>
    public static ShaderNode Texture(Texture texture, ShaderNode? uv = null) => Texture(new TextureSlot(texture), uv);

    /// <summary>Samples a texture slot. Returns RGBA (vec4).</summary>
    public static ShaderNode Texture(TextureSlot slot, ShaderNode? uv = null) => new TextureSampleNode(slot, uv ?? Uv, null);

    /// <summary>Samples one mip level of a texture.</summary>
    public static ShaderNode TextureLod(TextureSlot slot, ShaderNode uv, ShaderNode lod) => new TextureSampleNode(slot, uv, lod);

    /// <summary>Samples a cube texture in a direction. Returns RGBA (vec4).</summary>
    public static ShaderNode CubeTexture(Texture texture, ShaderNode direction) => new TextureSampleNode(new TextureSlot(texture, cube: true), direction, null);

    // ---- Stages ----

    /// <summary>Evaluates a node per vertex and interpolates the result across the triangle.</summary>
    public static ShaderNode Varying(ShaderNode node) => new VaryingNode(node);

    /// <summary>Inserts code in the device's shading language; <c>{0}</c>, <c>{1}</c>, ... stand for the arguments. Ties
    /// the material to that language: prefer the other functions.</summary>
    public static ShaderNode Raw(ShaderType type, string format, params ShaderNode[] args) => new RawNode(type, format, args);

    // ---- Math ----

    private static ShaderNode Same(string name, ShaderNode x) => new CallNode(name, x.Type, [x]);

    private static ShaderNode Same(string name, ShaderNode x, ShaderNode y) => new CallNode(name, Widest(x, y), [x, y]);

    private static ShaderType Widest(ShaderNode a, ShaderNode b) => a.Type == ShaderType.Float ? b.Type : a.Type;

    public static ShaderNode Sin(ShaderNode x) => Same("sin", x);

    public static ShaderNode Cos(ShaderNode x) => Same("cos", x);

    public static ShaderNode Tan(ShaderNode x) => Same("tan", x);

    public static ShaderNode Asin(ShaderNode x) => Same("asin", x);

    public static ShaderNode Acos(ShaderNode x) => Same("acos", x);

    public static ShaderNode Atan(ShaderNode y, ShaderNode x) => Same("atan", y, x);

    public static ShaderNode Abs(ShaderNode x) => Same("abs", x);

    public static ShaderNode Sign(ShaderNode x) => Same("sign", x);

    public static ShaderNode Floor(ShaderNode x) => Same("floor", x);

    public static ShaderNode Ceil(ShaderNode x) => Same("ceil", x);

    public static ShaderNode Fract(ShaderNode x) => Same("fract", x);

    public static ShaderNode Sqrt(ShaderNode x) => Same("sqrt", x);

    public static ShaderNode Exp(ShaderNode x) => Same("exp", x);

    public static ShaderNode Log(ShaderNode x) => Same("log", x);

    public static ShaderNode Pow(ShaderNode x, ShaderNode y) => new CallNode("pow", x.Type, [x, Match(y, x.Type)]);

    public static ShaderNode Mod(ShaderNode x, ShaderNode y) => new CallNode("mod", x.Type, [x, y]);

    public static ShaderNode Min(ShaderNode x, ShaderNode y) => Same("min", x, y);

    public static ShaderNode Max(ShaderNode x, ShaderNode y) => Same("max", x, y);

    public static ShaderNode Clamp(ShaderNode x, ShaderNode min, ShaderNode max) => new CallNode("clamp", x.Type, [x, min, max]);

    /// <summary>Clamps to 0..1.</summary>
    public static ShaderNode Saturate(ShaderNode x) => new CallNode("clamp", x.Type, [x, 0f, 1f]);

    /// <summary>1 - x.</summary>
    public static ShaderNode OneMinus(ShaderNode x) => 1f - x;

    /// <summary>Linear blend: a where t is 0, b where t is 1.</summary>
    public static ShaderNode Mix(ShaderNode a, ShaderNode b, ShaderNode t) => new CallNode("mix", Widest(a, b), [Match(a, Widest(a, b)), Match(b, Widest(a, b)), t]);

    public static ShaderNode Step(ShaderNode edge, ShaderNode x) => new CallNode("step", x.Type, [edge, x]);

    public static ShaderNode SmoothStep(ShaderNode edge0, ShaderNode edge1, ShaderNode x) => new CallNode("smoothstep", x.Type, [edge0, edge1, x]);

    public static ShaderNode Length(ShaderNode x) => new CallNode("length", ShaderType.Float, [x]);

    public static ShaderNode Distance(ShaderNode a, ShaderNode b) => new CallNode("distance", ShaderType.Float, [a, b]);

    public static ShaderNode Dot(ShaderNode a, ShaderNode b) => new CallNode("dot", ShaderType.Float, [a, b]);

    public static ShaderNode Cross(ShaderNode a, ShaderNode b) => new CallNode("cross", ShaderType.Vec3, [a, b]);

    public static ShaderNode Normalize(ShaderNode x) => Same("normalize", x);

    public static ShaderNode Reflect(ShaderNode incident, ShaderNode normal) => new CallNode("reflect", incident.Type, [incident, normal]);

    /// <summary>Screen-space derivative along x (fragment stage).</summary>
    public static ShaderNode Dfdx(ShaderNode x) => Same("dFdx", x);

    /// <summary>Screen-space derivative along y (fragment stage).</summary>
    public static ShaderNode Dfdy(ShaderNode x) => Same("dFdy", x);

    /// <summary>Maps x from one range to another.</summary>
    public static ShaderNode Remap(ShaderNode x, ShaderNode inMin, ShaderNode inMax, ShaderNode outMin, ShaderNode outMax) =>
        outMin + ((x - inMin) * (outMax - outMin) / (inMax - inMin));

    /// <summary>Rim factor: 0 facing the camera, 1 at grazing angles, shaped by <paramref name="power"/>.</summary>
    public static ShaderNode Fresnel(ShaderNode power) => Pow(Saturate(1f - Dot(NormalWorld, ViewDirection)), power);

    /// <summary>Pseudo-random value in 0..1 from a vec2.</summary>
    public static ShaderNode Hash(ShaderNode p) => new CallNode("arcHash12", ShaderType.Float, [p], library: "hash");

    /// <summary>Smooth value noise in 0..1 from a vec2.</summary>
    public static ShaderNode Noise(ShaderNode p) => new CallNode("arcNoise2", ShaderType.Float, [p], library: "noise");

    /// <summary>Smooth value noise in 0..1 from a vec3.</summary>
    public static ShaderNode Noise3(ShaderNode p) => new CallNode("arcNoise3", ShaderType.Float, [p], library: "noise");

    /// <summary>Fractal noise (five octaves) in 0..1 from a vec3.</summary>
    public static ShaderNode Fbm(ShaderNode p) => new CallNode("arcFbm3", ShaderType.Float, [p], library: "noise");

    // ---- Conditions ----

    public static ShaderNode LessThan(ShaderNode a, ShaderNode b) => new CompareNode("<", a, b);

    public static ShaderNode GreaterThan(ShaderNode a, ShaderNode b) => new CompareNode(">", a, b);

    public static ShaderNode LessEqual(ShaderNode a, ShaderNode b) => new CompareNode("<=", a, b);

    public static ShaderNode GreaterEqual(ShaderNode a, ShaderNode b) => new CompareNode(">=", a, b);

    /// <summary>Chooses between two values by a comparison.</summary>
    public static ShaderNode Select(ShaderNode condition, ShaderNode whenTrue, ShaderNode whenFalse) => new SelectNode(condition, whenTrue, whenFalse);

    // ---- Surface helpers ----

    /// <summary>Turns a tangent-space normal map sample (RGB in 0..1) into a world-space normal. Uses the geometry's
    /// tangents when it has them and screen-space derivatives of <paramref name="uv"/> otherwise.</summary>
    public static ShaderNode NormalMap(ShaderNode sample, ShaderNode? scale = null, ShaderNode? uv = null) =>
        new CallNode("arcPerturbNormal", ShaderType.Vec3, [NormalWorld, PositionWorld, uv ?? Uv, Vec3(sample), scale ?? Vec2(1f)], library: "normalmap");

    /// <summary>Turns an anisotropy direction given in tangent space (x along the tangent, y along the bitangent; its
    /// length is the strength, 0..1) into the world-space vector a physical surface's <c>Anisotropy</c> input takes.
    /// Uses the geometry's tangents when it has them and derivatives of <paramref name="uv"/> otherwise.</summary>
    public static ShaderNode AnisotropyDirection(ShaderNode direction, ShaderNode? uv = null) =>
        new CallNode("arcAnisotropyDirection", ShaderType.Vec3, [NormalWorld, PositionWorld, uv ?? Uv, Vec2(direction)], library: "normalmap");

    /// <summary>Converts a node to vec3: floats repeat, vec4 drops alpha.</summary>
    internal static ShaderNode ToVec3(ShaderNode node) => node.Type switch
    {
        ShaderType.Vec3 => node,
        ShaderType.Float => new ComposeNode(ShaderType.Vec3, [node]),
        ShaderType.Vec4 => node.Xyz,
        _ => throw new InvalidOperationException($"Expected a color (vec3) but got {ShaderNode.GlslName(node.Type)}."),
    };

    /// <summary>Converts a node to float: vectors use their first component.</summary>
    internal static ShaderNode ToFloat(ShaderNode node) => node.Type switch
    {
        ShaderType.Float => node,
        ShaderType.Vec2 or ShaderType.Vec3 or ShaderType.Vec4 => node.X,
        _ => throw new InvalidOperationException($"Expected a float but got {ShaderNode.GlslName(node.Type)}."),
    };

    private static ShaderNode Match(ShaderNode node, ShaderType type) =>
        node.Type == type || node.Type != ShaderType.Float || type == ShaderType.Float ? node : new ComposeNode(type, [node]);
}
