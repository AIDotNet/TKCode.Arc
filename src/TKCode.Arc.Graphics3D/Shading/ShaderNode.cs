using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace TKCode.Arc.Graphics3D.Shading;

/// <summary>The value type a shader node produces.</summary>
public enum ShaderType
{
    Float,
    Vec2,
    Vec3,
    Vec4,
    Mat3,
    Mat4,
    Bool,
}

/// <summary>Values the engine provides to material graphs.</summary>
public enum ShaderInput
{
    /// <summary>Vertex position in object space, after morphing and skinning (vec3).</summary>
    PositionLocal,

    /// <summary>Vertex normal in object space (vec3).</summary>
    NormalLocal,

    /// <summary>Position in world space (vec3). Not available inside a position node.</summary>
    PositionWorld,

    /// <summary>The interpolated surface normal in world space, facing the viewer (vec3).</summary>
    NormalWorld,

    /// <summary>Normalized direction from the surface to the camera, in world space (vec3).</summary>
    ViewDirection,

    /// <summary>First texture coordinate set (vec2).</summary>
    Uv,

    /// <summary>Second texture coordinate set (vec2).</summary>
    Uv1,

    /// <summary>Vertex color, linear RGBA (vec4); white when the geometry has none.</summary>
    VertexColor,

    /// <summary>Seconds since the renderer started (float).</summary>
    Time,

    /// <summary>Camera position in world space (vec3).</summary>
    CameraPosition,

    /// <summary>Position on the target in 0..1, origin bottom-left (vec2). Fragment stage only.</summary>
    ScreenUv,

    /// <summary>Distance from the camera plane, positive in front of the camera (float).</summary>
    ViewDepth,

    /// <summary>Coordinate inside a point sprite, 0..1 (vec2). Fragment stage of point materials only.</summary>
    PointCoord,
}

internal enum ShaderStage
{
    Vertex,
    Fragment,
}

/// <summary>A node of a material graph: an expression that the engine turns into shader code for whatever shading
/// language the device uses. Nodes are immutable values; combine them with operators and the functions of
/// <see cref="Sl"/>. The same node may be used any number of times and is evaluated once per stage.</summary>
public abstract class ShaderNode
{
    private protected ShaderNode(ShaderType type) => Type = type;

    /// <summary>The value type.</summary>
    public ShaderType Type { get; }

    /// <summary>True when the generated expression is cheap enough to repeat instead of storing it in a variable.</summary>
    internal virtual bool IsInline => false;

    internal abstract string Generate(ShaderBuilder b);

    public static implicit operator ShaderNode(float value) => new ConstNode(ShaderType.Float, new Vector4(value, 0f, 0f, 0f));

    public static implicit operator ShaderNode(Vector2 value) => new ConstNode(ShaderType.Vec2, new Vector4(value, 0f, 0f));

    public static implicit operator ShaderNode(Vector3 value) => new ConstNode(ShaderType.Vec3, new Vector4(value, 0f));

    public static implicit operator ShaderNode(Vector4 value) => new ConstNode(ShaderType.Vec4, value);

    public static implicit operator ShaderNode(ColorF value) => new ConstNode(ShaderType.Vec3, new Vector4(value.R, value.G, value.B, 0f));

    public static ShaderNode operator +(ShaderNode a, ShaderNode b) => new BinaryNode('+', a, b);

    public static ShaderNode operator -(ShaderNode a, ShaderNode b) => new BinaryNode('-', a, b);

    public static ShaderNode operator *(ShaderNode a, ShaderNode b) => new BinaryNode('*', a, b);

    public static ShaderNode operator /(ShaderNode a, ShaderNode b) => new BinaryNode('/', a, b);

    public static ShaderNode operator -(ShaderNode a) => new CallNode("-", a.Type, [a], prefix: true);

    /// <summary>The first component.</summary>
    public ShaderNode X => Swizzle("x");

    /// <summary>The second component.</summary>
    public ShaderNode Y => Swizzle("y");

    /// <summary>The third component.</summary>
    public ShaderNode Z => Swizzle("z");

    /// <summary>The fourth component.</summary>
    public ShaderNode W => Swizzle("w");

    /// <summary>The first two components.</summary>
    public ShaderNode Xy => Swizzle("xy");

    /// <summary>The first three components.</summary>
    public ShaderNode Xyz => Swizzle("xyz");

    /// <summary>Picks and reorders components, for example <c>"zyx"</c> or <c>"xx"</c>.</summary>
    public ShaderNode Swizzle(string components) => new SwizzleNode(this, components);

    internal static int ComponentCount(ShaderType type) => type switch
    {
        ShaderType.Float or ShaderType.Bool => 1,
        ShaderType.Vec2 => 2,
        ShaderType.Vec3 => 3,
        ShaderType.Vec4 => 4,
        ShaderType.Mat3 => 9,
        _ => 16,
    };

    internal static ShaderType VectorOf(int components) => components switch
    {
        1 => ShaderType.Float,
        2 => ShaderType.Vec2,
        3 => ShaderType.Vec3,
        4 => ShaderType.Vec4,
        _ => throw new ArgumentOutOfRangeException(nameof(components)),
    };

    internal static string GlslName(ShaderType type) => type switch
    {
        ShaderType.Float => "float",
        ShaderType.Vec2 => "vec2",
        ShaderType.Vec3 => "vec3",
        ShaderType.Vec4 => "vec4",
        ShaderType.Mat3 => "mat3",
        ShaderType.Mat4 => "mat4",
        _ => "bool",
    };

    internal static string Literal(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new InvalidOperationException("A shader constant must be a finite number.");
        }

        var s = value.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') || s.Contains('e') ? s : s + ".0";
    }
}

internal sealed class ConstNode : ShaderNode
{
    private readonly Vector4 _value;

    public ConstNode(ShaderType type, Vector4 value)
        : base(type) => _value = value;

    internal override bool IsInline => true;

    internal override string Generate(ShaderBuilder b) => Type switch
    {
        ShaderType.Float => Literal(_value.X),
        ShaderType.Vec2 => $"vec2({Literal(_value.X)}, {Literal(_value.Y)})",
        ShaderType.Vec3 => $"vec3({Literal(_value.X)}, {Literal(_value.Y)}, {Literal(_value.Z)})",
        _ => $"vec4({Literal(_value.X)}, {Literal(_value.Y)}, {Literal(_value.Z)}, {Literal(_value.W)})",
    };
}

/// <summary>A value that can change every frame without rebuilding the shader. Set it through the typed properties.</summary>
public sealed class UniformNode : ShaderNode
{
    private Matrix4x4 _value;

    internal UniformNode(ShaderType type, Matrix4x4 value)
        : base(type) => _value = value;

    /// <summary>Changes when the value changes.</summary>
    public int Version { get; private set; }

    /// <summary>Called before each frame in which a material using this uniform is drawn; set the value here to drive
    /// it from application state.</summary>
    public Action<UniformNode>? OnUpdate { get; set; }

    internal override bool IsInline => true;

    /// <summary>The value of a float uniform.</summary>
    public float Float
    {
        get => _value.M11;
        set => Vector4 = new Vector4(value, 0f, 0f, 0f);
    }

    /// <summary>The value of a vec2 uniform.</summary>
    public Vector2 Vector2
    {
        get => new(_value.M11, _value.M12);
        set => Vector4 = new Vector4(value, 0f, 0f);
    }

    /// <summary>The value of a vec3 uniform.</summary>
    public Vector3 Vector3
    {
        get => new(_value.M11, _value.M12, _value.M13);
        set => Vector4 = new Vector4(value, 0f);
    }

    /// <summary>The value of a vec4 uniform.</summary>
    public Vector4 Vector4
    {
        get => new(_value.M11, _value.M12, _value.M13, _value.M14);
        set
        {
            if (Vector4 != value)
            {
                _value.M11 = value.X;
                _value.M12 = value.Y;
                _value.M13 = value.Z;
                _value.M14 = value.W;
                Version++;
            }
        }
    }

    /// <summary>The value of a vec3 uniform as a color.</summary>
    public ColorF Color
    {
        get => new(_value.M11, _value.M12, _value.M13);
        set => Vector3 = value.ToVector3();
    }

    /// <summary>The value of a mat4 (or mat3: the upper-left 3x3) uniform.</summary>
    public Matrix4x4 Matrix
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                Version++;
            }
        }
    }

    internal override string Generate(ShaderBuilder b) => "u_m" + b.Graph.RegisterUniform(this).ToString(CultureInfo.InvariantCulture);

    /// <summary>Writes the value in std140 layout.</summary>
    internal void Write(Span<float> destination)
    {
        switch (Type)
        {
            case ShaderType.Float:
            case ShaderType.Bool:
                destination[0] = _value.M11;
                break;
            case ShaderType.Vec2:
                destination[0] = _value.M11;
                destination[1] = _value.M12;
                break;
            case ShaderType.Vec3:
                destination[0] = _value.M11;
                destination[1] = _value.M12;
                destination[2] = _value.M13;
                break;
            case ShaderType.Vec4:
                destination[0] = _value.M11;
                destination[1] = _value.M12;
                destination[2] = _value.M13;
                destination[3] = _value.M14;
                break;
            case ShaderType.Mat3:
                destination[0] = _value.M11;
                destination[1] = _value.M12;
                destination[2] = _value.M13;
                destination[4] = _value.M21;
                destination[5] = _value.M22;
                destination[6] = _value.M23;
                destination[8] = _value.M31;
                destination[9] = _value.M32;
                destination[10] = _value.M33;
                break;
            default:
                System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref _value.M11, 16).CopyTo(destination);
                break;
        }
    }

    /// <summary>std140 alignment and size in floats.</summary>
    internal (int Align, int Size) Layout => Type switch
    {
        ShaderType.Float or ShaderType.Bool => (1, 1),
        ShaderType.Vec2 => (2, 2),
        ShaderType.Vec3 => (4, 3),
        ShaderType.Vec4 => (4, 4),
        ShaderType.Mat3 => (4, 12),
        _ => (4, 16),
    };
}

/// <summary>A place in a material graph where a texture is sampled. The texture can be replaced at any time without
/// rebuilding the shader, as long as it keeps its kind (2D or cube).</summary>
public sealed class TextureSlot
{
    /// <summary>Creates a slot.</summary>
    public TextureSlot(Texture? texture = null, bool cube = false)
    {
        Texture = texture;
        IsCube = cube || texture is CubeTexture;
    }

    /// <summary>The texture; null samples opaque white.</summary>
    public Texture? Texture { get; set; }

    /// <summary>True for cube textures.</summary>
    public bool IsCube { get; }
}

internal sealed class TextureSampleNode : ShaderNode
{
    private readonly TextureSlot _slot;
    private readonly ShaderNode _coordinate;
    private readonly ShaderNode? _lod;

    public TextureSampleNode(TextureSlot slot, ShaderNode coordinate, ShaderNode? lod)
        : base(ShaderType.Vec4)
    {
        var expected = slot.IsCube ? ShaderType.Vec3 : ShaderType.Vec2;
        if (coordinate.Type != expected)
        {
            throw new ArgumentException($"A {(slot.IsCube ? "cube" : "2D")} texture is sampled with a {GlslName(expected)}, not a {GlslName(coordinate.Type)}.");
        }

        _slot = slot;
        _coordinate = coordinate;
        _lod = lod;
    }

    internal override string Generate(ShaderBuilder b)
    {
        var name = "u_tex" + b.Graph.RegisterTexture(_slot).ToString(CultureInfo.InvariantCulture);
        var uv = b.Emit(_coordinate);
        return _lod is null ? $"texture({name}, {uv})" : $"textureLod({name}, {uv}, {b.Emit(_lod)})";
    }
}

internal sealed class InputNode : ShaderNode
{
    private readonly ShaderInput _input;

    public InputNode(ShaderInput input)
        : base(input switch
        {
            ShaderInput.Uv or ShaderInput.Uv1 or ShaderInput.ScreenUv or ShaderInput.PointCoord => ShaderType.Vec2,
            ShaderInput.VertexColor => ShaderType.Vec4,
            ShaderInput.Time or ShaderInput.ViewDepth => ShaderType.Float,
            _ => ShaderType.Vec3,
        }) => _input = input;

    internal override bool IsInline => true;

    internal override string Generate(ShaderBuilder b) => b.Input(_input);
}

internal sealed class BinaryNode : ShaderNode
{
    private readonly char _op;
    private readonly ShaderNode _a;
    private readonly ShaderNode _b;

    public BinaryNode(char op, ShaderNode a, ShaderNode b)
        : base(Infer(op, a, b))
    {
        _op = op;
        _a = a;
        _b = b;
    }

    private static ShaderType Infer(char op, ShaderNode a, ShaderNode b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Type == ShaderType.Bool || b.Type == ShaderType.Bool)
        {
            throw new InvalidOperationException("Arithmetic on a bool node; use Sl.Select.");
        }

        if (a.Type == b.Type)
        {
            return a.Type;
        }

        if (a.Type == ShaderType.Float)
        {
            return b.Type;
        }

        if (b.Type == ShaderType.Float)
        {
            return a.Type;
        }

        if (op == '*')
        {
            if (a.Type == ShaderType.Mat4 && b.Type == ShaderType.Vec4)
            {
                return ShaderType.Vec4;
            }

            if (a.Type == ShaderType.Mat3 && b.Type == ShaderType.Vec3)
            {
                return ShaderType.Vec3;
            }
        }

        throw new InvalidOperationException($"Cannot apply '{op}' to {GlslName(a.Type)} and {GlslName(b.Type)}.");
    }

    internal override string Generate(ShaderBuilder b) => $"({b.Emit(_a)} {_op} {b.Emit(_b)})";
}

internal sealed class CallNode : ShaderNode
{
    private readonly string _name;
    private readonly ShaderNode[] _args;
    private readonly bool _prefix;
    private readonly string? _library;

    public CallNode(string name, ShaderType type, ShaderNode[] args, bool prefix = false, string? library = null)
        : base(type)
    {
        _name = name;
        _args = args;
        _prefix = prefix;
        _library = library;
    }

    internal override string Generate(ShaderBuilder b)
    {
        if (_prefix)
        {
            return $"({_name}{b.Emit(_args[0])})";
        }

        if (_library is not null)
        {
            b.Graph.RequireLibrary(_library);
        }

        var sb = new StringBuilder(_name).Append('(');
        for (var i = 0; i < _args.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(b.Emit(_args[i]));
        }

        return sb.Append(')').ToString();
    }
}

internal sealed class SwizzleNode : ShaderNode
{
    private readonly ShaderNode _source;
    private readonly string _components;

    public SwizzleNode(ShaderNode source, string components)
        : base(VectorOf(components.Length))
    {
        var count = ComponentCount(source.Type);
        if (source.Type is ShaderType.Mat3 or ShaderType.Mat4 or ShaderType.Bool)
        {
            throw new InvalidOperationException("Only floats and vectors have components.");
        }

        foreach (var c in components)
        {
            var index = c switch
            {
                'x' or 'r' => 0,
                'y' or 'g' => 1,
                'z' or 'b' => 2,
                'w' or 'a' => 3,
                _ => throw new ArgumentException($"'{c}' is not a component name.", nameof(components)),
            };
            if (index >= count)
            {
                throw new InvalidOperationException($"A {GlslName(source.Type)} has no '{c}' component.");
            }
        }

        _source = source;
        _components = components.Replace('r', 'x').Replace('g', 'y').Replace('b', 'z').Replace('a', 'w');
    }

    internal override string Generate(ShaderBuilder b)
    {
        var s = b.Emit(_source);
        if (_source.Type == ShaderType.Float)
        {
            // A float has no swizzle in GLSL ES; build the vector instead.
            return _components.Length == 1 ? s : $"{GlslName(Type)}({s})";
        }

        return $"{s}.{_components}";
    }
}

internal sealed class ComposeNode : ShaderNode
{
    private readonly ShaderNode[] _parts;

    public ComposeNode(ShaderType type, ShaderNode[] parts)
        : base(type)
    {
        var total = 0;
        foreach (var p in parts)
        {
            total += ComponentCount(p.Type);
        }

        var need = ComponentCount(type);
        if (total != need && !(parts.Length == 1 && parts[0].Type == ShaderType.Float))
        {
            throw new InvalidOperationException($"A {GlslName(type)} needs {need} components but got {total}.");
        }

        _parts = parts;
    }

    internal override string Generate(ShaderBuilder b)
    {
        var sb = new StringBuilder(GlslName(Type)).Append('(');
        for (var i = 0; i < _parts.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(b.Emit(_parts[i]));
        }

        return sb.Append(')').ToString();
    }
}

internal sealed class CompareNode : ShaderNode
{
    private readonly string _op;
    private readonly ShaderNode _a;
    private readonly ShaderNode _b;

    public CompareNode(string op, ShaderNode a, ShaderNode b)
        : base(ShaderType.Bool)
    {
        if (a.Type != ShaderType.Float || b.Type != ShaderType.Float)
        {
            throw new InvalidOperationException("Comparisons take two floats.");
        }

        _op = op;
        _a = a;
        _b = b;
    }

    internal override string Generate(ShaderBuilder b) => $"({b.Emit(_a)} {_op} {b.Emit(_b)})";
}

internal sealed class SelectNode : ShaderNode
{
    private readonly ShaderNode _condition;
    private readonly ShaderNode _a;
    private readonly ShaderNode _b;

    public SelectNode(ShaderNode condition, ShaderNode whenTrue, ShaderNode whenFalse)
        : base(whenTrue.Type)
    {
        if (condition.Type != ShaderType.Bool)
        {
            throw new InvalidOperationException("The condition of Sl.Select must be a comparison.");
        }

        if (whenTrue.Type != whenFalse.Type)
        {
            throw new InvalidOperationException("Both branches of Sl.Select must have the same type.");
        }

        _condition = condition;
        _a = whenTrue;
        _b = whenFalse;
    }

    internal override string Generate(ShaderBuilder b) => $"({b.Emit(_condition)} ? {b.Emit(_a)} : {b.Emit(_b)})";
}

internal sealed class VaryingNode : ShaderNode
{
    private readonly ShaderNode _source;

    public VaryingNode(ShaderNode source)
        : base(source.Type)
    {
        if (source.Type is ShaderType.Bool or ShaderType.Mat3 or ShaderType.Mat4)
        {
            throw new InvalidOperationException("Only floats and vectors can be interpolated.");
        }

        _source = source;
    }

    internal override bool IsInline => true;

    internal override string Generate(ShaderBuilder b) =>
        b.Stage == ShaderStage.Vertex ? b.Emit(_source) : b.Graph.RegisterVarying(this, _source);
}

internal sealed class RawNode : ShaderNode
{
    private readonly string _format;
    private readonly ShaderNode[] _args;

    public RawNode(ShaderType type, string format, ShaderNode[] args)
        : base(type)
    {
        _format = format;
        _args = args;
    }

    internal override string Generate(ShaderBuilder b)
    {
        var names = new object[_args.Length];
        for (var i = 0; i < _args.Length; i++)
        {
            names[i] = b.Emit(_args[i]);
        }

        return "(" + string.Format(CultureInfo.InvariantCulture, _format, names) + ")";
    }
}

/// <summary>Everything one generated program needs besides its code: the uniforms, textures and interpolated values
/// that its nodes registered, in a deterministic order (so equal graphs give equal source text).</summary>
internal sealed class ShaderGraph
{
    public readonly List<UniformNode> Uniforms = [];
    public readonly List<TextureSlot> Textures = [];
    public readonly List<(string Name, ShaderNode Source)> Varyings = [];
    public readonly List<string> Libraries = [];
    private readonly Dictionary<VaryingNode, string> _varyingNames = new(ReferenceEqualityComparer.Instance);
    public uint VertexInputs;
    public uint FragmentInputs;

    public int RegisterUniform(UniformNode node)
    {
        var i = Uniforms.IndexOf(node);
        if (i < 0)
        {
            i = Uniforms.Count;
            Uniforms.Add(node);
        }

        return i;
    }

    public int RegisterTexture(TextureSlot slot)
    {
        var i = Textures.IndexOf(slot);
        if (i < 0)
        {
            i = Textures.Count;
            Textures.Add(slot);
        }

        return i;
    }

    public string RegisterVarying(VaryingNode node, ShaderNode source)
    {
        if (!_varyingNames.TryGetValue(node, out var name))
        {
            name = "v_c" + Varyings.Count.ToString(CultureInfo.InvariantCulture);
            _varyingNames.Add(node, name);
            Varyings.Add((name, source));
        }

        return name;
    }

    public void RequireLibrary(string id)
    {
        if (!Libraries.Contains(id))
        {
            Libraries.Add(id);
        }
    }

    public void Require(ShaderStage stage, ShaderInput input)
    {
        if (stage == ShaderStage.Vertex)
        {
            VertexInputs |= 1u << (int)input;
        }
        else
        {
            FragmentInputs |= 1u << (int)input;
        }
    }

    public bool FragmentNeeds(ShaderInput input) => (FragmentInputs & (1u << (int)input)) != 0;

    public bool VertexNeeds(ShaderInput input) => (VertexInputs & (1u << (int)input)) != 0;
}

/// <summary>Generates the statements of one stage. Each node is emitted once and its result kept in a local variable.</summary>
internal sealed class ShaderBuilder
{
    private readonly Dictionary<ShaderNode, string> _emitted = new(ReferenceEqualityComparer.Instance);
    private int _temp;

    public ShaderBuilder(ShaderGraph graph, ShaderStage stage)
    {
        Graph = graph;
        Stage = stage;
    }

    public ShaderGraph Graph { get; }

    public ShaderStage Stage { get; }

    public StringBuilder Code { get; } = new();

    /// <summary>True while the vertex position node is generated: world-space inputs do not exist yet.</summary>
    public bool InPositionNode { get; set; }

    public string Emit(ShaderNode node)
    {
        if (_emitted.TryGetValue(node, out var name))
        {
            return name;
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
        var expression = node.Generate(this);
        if (!node.IsInline)
        {
            name = (Stage == ShaderStage.Vertex ? "vn" : "n") + (_temp++).ToString(CultureInfo.InvariantCulture);
            Code.Append("    ").Append(ShaderNode.GlslName(node.Type)).Append(' ').Append(name).Append(" = ").Append(expression).Append(";\n");
            expression = name;
        }

        _emitted.Add(node, expression);
        return expression;
    }

    public string Input(ShaderInput input)
    {
        Graph.Require(Stage, input);
        if (Stage == ShaderStage.Vertex)
        {
            if (InPositionNode && input is ShaderInput.PositionWorld or ShaderInput.NormalWorld or ShaderInput.ViewDirection or ShaderInput.ViewDepth)
            {
                throw new InvalidOperationException($"{input} is not available in a position node: world space is computed from its result.");
            }

            return input switch
            {
                ShaderInput.PositionLocal => "position",
                ShaderInput.NormalLocal => "normal",
                ShaderInput.PositionWorld => "worldPos.xyz",
                ShaderInput.NormalWorld => "worldNormal",
                ShaderInput.ViewDirection => "normalize(u_cameraPos.xyz - worldPos.xyz)",
                ShaderInput.Uv => "uv0",
                ShaderInput.Uv1 => "uv1",
                ShaderInput.VertexColor => "vertexColor",
                ShaderInput.Time => "u_cameraPos.w",
                ShaderInput.CameraPosition => "u_cameraPos.xyz",
                ShaderInput.ViewDepth => "gl_Position.w",
                _ => throw new InvalidOperationException($"{input} is only available in the fragment stage."),
            };
        }

        return input switch
        {
            ShaderInput.PositionLocal => "v_localPos",
            ShaderInput.NormalLocal => "normalize(v_localNormal)",
            ShaderInput.PositionWorld => "v_worldPos",
            ShaderInput.NormalWorld => "N",
            ShaderInput.ViewDirection => "V",
            ShaderInput.Uv => "v_uv0",
            ShaderInput.Uv1 => "v_uv1",
            ShaderInput.VertexColor => "v_color",
            ShaderInput.Time => "u_cameraPos.w",
            ShaderInput.CameraPosition => "u_cameraPos.xyz",
            ShaderInput.ScreenUv => "(gl_FragCoord.xy * u_viewport.zw)",
            ShaderInput.ViewDepth => "v_viewDepth",
            _ => "gl_PointCoord",
        };
    }
}
