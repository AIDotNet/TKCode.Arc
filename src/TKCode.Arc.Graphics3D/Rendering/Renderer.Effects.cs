using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

public sealed partial class Renderer
{
    private const int SlotEffect = 3;
    private const int EffectFloats = 48;
    private readonly Dictionary<string, GpuProgram> _effects = new(StringComparer.Ordinal);
    private readonly float[] _effectData = new float[EffectFloats];
    private GpuBuffer? _effectBuffer;

    private const string EffectBlock = """
layout(std140) uniform EffectBlock {
    vec4 u_p[8];
    mat4 u_matrix;
};

""";

    // One triangle that covers the target; u_p[7].w is its clip-space depth.
    private const string EffectVertex = """
out vec2 v_uv;
void main() {
    vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
    v_uv = p;
    gl_Position = vec4(p * 2.0 - 1.0, u_p[7].w, 1.0);
}

""";

    private const string CubeDirectionGlsl = """
vec3 arcCubeDir(int face, vec2 uv) {
    vec2 st = uv * 2.0 - 1.0;
    if (face == 0) { return vec3(1.0, -st.y, -st.x); }
    if (face == 1) { return vec3(-1.0, -st.y, st.x); }
    if (face == 2) { return vec3(st.x, 1.0, st.y); }
    if (face == 3) { return vec3(st.x, -1.0, -st.y); }
    if (face == 4) { return vec3(st.x, -st.y, 1.0); }
    return vec3(-st.x, -st.y, -1.0);
}

""";

    private const string EquirectUvGlsl = """
vec2 arcEquirectUv(vec3 d) {
    return vec2(atan(d.z, d.x) * 0.15915494309189535 + 0.5, asin(clamp(d.y, -1.0, 1.0)) * 0.3183098861837907 + 0.5);
}

""";

    /// <summary>A full-screen fragment program. <paramref name="samplers"/> are bound to units 0, 1, 2, ... in order.</summary>
    private GpuProgram GetEffect(string key, Func<string> fragmentBody, params ReadOnlySpan<string> samplers)
    {
        if (_effects.TryGetValue(key, out var program))
        {
            return program;
        }

        var header = ShaderLibrary.Header(Device.Capabilities.ShaderLanguage);
        var bindings = new SamplerBinding[samplers.Length];
        for (var i = 0; i < samplers.Length; i++)
        {
            bindings[i] = new SamplerBinding(samplers[i], i);
        }

        var vertex = header + EffectBlock + EffectVertex;
        var fragment = header + EffectBlock + "in vec2 v_uv;\nout vec4 o_color;\n" + fragmentBody();
        UniformBlockBinding[] blocks = [new UniformBlockBinding("EffectBlock", SlotEffect)];
        if (Device.Capabilities.ShaderLanguage == ShaderLanguage.Glsl450)
        {
            ShaderDialect.ToGlsl450(ref vertex, ref fragment, blocks, bindings);
        }

        program = Device.CreateProgram(new ProgramDescriptor
        {
            VertexSource = vertex,
            FragmentSource = fragment,
            Label = key,
            UniformBlocks = blocks,
            Samplers = bindings,
        });
        _effects.Add(key, program);
        _stats.ProgramsCompiled++;
        return program;
    }

    /// <summary>Draws the full-screen triangle with an effect. Bind its textures to units 0.. before the call.</summary>
    private void DrawEffect(GpuProgram program, in RenderState state, ReadOnlySpan<Vector4> parameters, in Matrix4x4 matrix, float depth = 0f)
    {
        var d = _effectData;
        Array.Clear(d);
        MemoryMarshal.Cast<Vector4, float>(parameters).CopyTo(d);
        d[31] = depth;
        var m = matrix;
        MemoryMarshal.CreateReadOnlySpan(ref m.M11, 16).CopyTo(d.AsSpan(32));
        _effectBuffer ??= Device.CreateBuffer(BufferUsage.Uniform, EffectFloats * 4, true, "effect");
        Device.WriteBuffer(_effectBuffer, 0, MemoryMarshal.AsBytes<float>(d));
        Device.SetProgram(program);
        Device.SetRenderState(state);
        Device.SetUniformBuffer(SlotEffect, _effectBuffer, 0, EffectFloats * 4);
        Device.SetVertexInput(EmptyInput);
        Device.SetDrawConstants(0, 0, 0, 0);
        Device.Draw(PrimitiveTopology.Triangles, 0, 3, 1);
    }

    private static RenderState EffectState => new()
    {
        Cull = CullMode.None,
        DepthTest = false,
        DepthWrite = false,
        DepthCompare = CompareFunction.Always,
        Blend = BlendMode.Opaque,
        ColorWrite = true,
    };

    /// <summary>The projection as the device wants it (see <see cref="WriteView"/>).</summary>
    private Matrix4x4 DeviceProjection(in Matrix4x4 projection)
    {
        if (Device.Capabilities.ClipDepth != ClipDepthRange.NegativeOneToOne)
        {
            return projection;
        }

        var fix = Matrix4x4.Identity;
        fix.M33 = 2f;
        fix.M43 = -1f;
        return projection * fix;
    }

    private void DisposeEffects()
    {
        foreach (var program in _effects.Values)
        {
            program.Dispose();
        }

        _effects.Clear();
        _effectBuffer?.Dispose();
        _effectBuffer = null;
    }
}
