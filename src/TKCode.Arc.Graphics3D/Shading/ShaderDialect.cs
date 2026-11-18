using System.Globalization;
using System.Text;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D.Shading;

/// <summary>Turns the engine's shader text into <see cref="ShaderLanguage.Glsl450"/>. The engine writes every shader
/// once, in the subset GLSL ES 3.00 and GLSL 3.30 share; the statements of that subset are valid GLSL 4.50 too, and
/// only the declarations differ. This class rewrites those, line by line:
/// <list type="bullet">
/// <item><c>uniform sampler2D name;</c> becomes a texture and a sampler at the bindings of the sampler's unit, and a
/// macro <c>name</c> that combines them, so the code that samples it stays as it is;</item>
/// <item><c>layout(std140) uniform Block {</c> gets the set and binding of the block's slot;</item>
/// <item><c>uniform ivec4 u_draw;</c> (the draw constants) becomes a push constant block;</item>
/// <item>values passed between the stages get locations, the same for a name in both stages;</item>
/// <item>a <c>mat4</c> vertex attribute becomes four <c>vec4</c> attributes and a macro that builds the matrix;</item>
/// <item>in the vertex stage <c>texture(s, uv)</c> reads level 0 explicitly (there are no derivatives to choose one).</item>
/// </list>
/// The header (<see cref="ShaderLibrary.Header"/>) names the vertex and instance index the old way.</summary>
internal static class ShaderDialect
{
    /// <summary>The set of uniform blocks; the binding is the slot.</summary>
    public const int UniformSet = 0;

    /// <summary>The set of textures and samplers; unit <c>u</c> has its texture at binding <c>2u</c> and its sampler
    /// at <c>2u + 1</c>.</summary>
    public const int TextureSet = 1;

    /// <summary>The set of storage buffers (compute programs); the binding is the slot.</summary>
    public const int StorageSet = 2;

    private const string DrawConstants = "uniform ivec4 u_draw;";
    private const string Block = "layout(std140) uniform ";
    private const string VertexTextureMacro = "#define texture(s, uv) textureLod(s, uv, 0.0)\n";

    /// <summary>The declaration of a storage buffer in a compute program: an array of <paramref name="elementType"/>
    /// named <paramref name="name"/>.</summary>
    public static string StorageBuffer(int slot, string elementType, string name, bool readOnly) => string.Create(
        CultureInfo.InvariantCulture,
        $"layout(std430, set = {StorageSet}, binding = {slot}) {(readOnly ? "readonly " : "")}buffer {name}Buffer {{\n    {elementType} {name}[];\n}};\n");

    /// <summary>The declaration of a uniform block in a compute program.</summary>
    public static string UniformBlock(int slot, string blockName, string members) => string.Create(
        CultureInfo.InvariantCulture,
        $"layout(std140, set = {UniformSet}, binding = {slot}) uniform {blockName} {{\n{members}}};\n");

    /// <summary>Rewrites a vertex and a fragment shader. The tables give the slot of each uniform block and the unit
    /// of each sampler, as a program descriptor does for OpenGL.</summary>
    public static void ToGlsl450(ref string vertex, ref string fragment, ReadOnlySpan<UniformBlockBinding> blocks, ReadOnlySpan<SamplerBinding> samplers)
    {
        var varyings = new Dictionary<string, int>(StringComparer.Ordinal);
        vertex = Convert(vertex, vertexStage: true, blocks, samplers, varyings);
        fragment = Convert(fragment, vertexStage: false, blocks, samplers, varyings);
    }

    private static string Convert(
        string source, bool vertexStage, ReadOnlySpan<UniformBlockBinding> blocks, ReadOnlySpan<SamplerBinding> samplers, Dictionary<string, int> varyings)
    {
        var sb = new StringBuilder(source.Length + 1024);
        var inBody = false;
        var outputs = 0;
        foreach (var line in source.AsSpan().EnumerateLines())
        {
            var t = line.TrimStart();
            if (!inBody && t.Length > 0 && t[0] != '#')
            {
                inBody = true;
                if (vertexStage)
                {
                    sb.Append(VertexTextureMacro);
                }
            }

            if (t.StartsWith("uniform sampler", StringComparison.Ordinal))
            {
                AppendSampler(sb, t, samplers);
            }
            else if (t.StartsWith(Block, StringComparison.Ordinal))
            {
                var rest = t[Block.Length..];
                var end = rest.IndexOfAny(' ', '{');
                var name = (end < 0 ? rest : rest[..end]).ToString();
                sb.Append("layout(std140, set = ").Append(UniformSet).Append(", binding = ").Append(SlotOf(blocks, name)).Append(") uniform ").Append(rest).Append('\n');
            }
            else if (t.StartsWith(DrawConstants, StringComparison.Ordinal))
            {
                sb.Append("layout(push_constant) uniform DrawConstants {\n    ivec4 u_draw;\n};\n");
            }
            else if (TryParseInterface(t, out var flat, out var input, out var declaration, out var variable))
            {
                int location;
                if (vertexStage == input)
                {
                    // A vertex input without a location, or a fragment output: numbered in order.
                    location = outputs++;
                }
                else if (vertexStage)
                {
                    location = varyings.Count;
                    varyings[variable] = location;
                }
                else if (!varyings.TryGetValue(variable, out location))
                {
                    throw new InvalidOperationException($"The fragment stage reads '{variable}', which the vertex stage does not write.");
                }

                sb.Append("layout(location = ").Append(location).Append(") ").Append(flat ? "flat " : "").Append(input ? "in " : "out ").Append(declaration).Append('\n');
            }
            else if (vertexStage && TryParseMatrixAttribute(t, out var matrixLocation, out var matrixName))
            {
                for (var column = 0; column < 4; column++)
                {
                    sb.Append("layout(location = ").Append(matrixLocation + column).Append(") in vec4 ").Append(matrixName).Append(column).Append(";\n");
                }

                sb.Append("#define ").Append(matrixName).Append(" mat4(").Append(matrixName).Append("0, ").Append(matrixName).Append("1, ")
                    .Append(matrixName).Append("2, ").Append(matrixName).Append("3)\n");
            }
            else
            {
                sb.Append(line).Append('\n');
            }
        }

        // Every line got a line end, the last one too: one more than the source has.
        return sb.ToString(0, sb.Length - 1);
    }

    private static void AppendSampler(StringBuilder sb, ReadOnlySpan<char> line, ReadOnlySpan<SamplerBinding> samplers)
    {
        // "uniform <type> <name>;"
        var rest = line["uniform ".Length..];
        var space = rest.IndexOf(' ');
        var semicolon = rest.IndexOf(';');
        if (space < 0 || semicolon < space)
        {
            throw new InvalidOperationException($"Cannot read the sampler declaration '{line.ToString()}'.");
        }

        var type = rest[..space].ToString();
        var name = rest[(space + 1)..semicolon].Trim().ToString();
        var shadow = type.EndsWith("Shadow", StringComparison.Ordinal);
        var texture = (shadow ? type[..^6] : type) switch
        {
            "sampler2D" => "texture2D",
            "samplerCube" => "textureCube",
            "sampler2DArray" => "texture2DArray",
            "sampler3D" => "texture3D",
            _ => throw new NotSupportedException($"Sampler type '{type}' has no GLSL 4.50 form in the engine."),
        };
        var unit = -1;
        foreach (var s in samplers)
        {
            if (s.Name == name)
            {
                unit = s.Unit;
                break;
            }
        }

        if (unit < 0)
        {
            throw new InvalidOperationException($"The sampler '{name}' has no texture unit in the program's sampler table.");
        }

        sb.Append("layout(set = ").Append(TextureSet).Append(", binding = ").Append(unit * 2).Append(") uniform ").Append(texture).Append(' ').Append(name).Append("_t;\n");
        sb.Append("layout(set = ").Append(TextureSet).Append(", binding = ").Append((unit * 2) + 1).Append(") uniform ").Append(shadow ? "samplerShadow " : "sampler ")
            .Append(name).Append("_s;\n");
        sb.Append("#define ").Append(name).Append(' ').Append(type).Append('(').Append(name).Append("_t, ").Append(name).Append("_s)\n");
    }

    private static int SlotOf(ReadOnlySpan<UniformBlockBinding> blocks, string name)
    {
        foreach (var b in blocks)
        {
            if (b.Name == name)
            {
                return b.Slot;
            }
        }

        throw new InvalidOperationException($"The uniform block '{name}' has no slot in the program's block table.");
    }

    /// <summary>Reads <c>[flat] in|out type name;</c> at the start of a line: a value passed between stages that has
    /// no location yet.</summary>
    private static bool TryParseInterface(ReadOnlySpan<char> line, out bool flat, out bool input, out ReadOnlySpan<char> declaration, out string variable)
    {
        flat = line.StartsWith("flat ", StringComparison.Ordinal);
        var rest = flat ? line[5..] : line;
        input = rest.StartsWith("in ", StringComparison.Ordinal);
        declaration = default;
        variable = "";
        if (!input && !rest.StartsWith("out ", StringComparison.Ordinal))
        {
            return false;
        }

        declaration = rest[(input ? 3 : 4)..].TrimEnd();
        var space = declaration.IndexOf(' ');
        if (space < 0 || declaration.Length == 0 || declaration[^1] != ';' || declaration[(space + 1)..].IndexOfAny(' ', ',', '(') >= 0)
        {
            return false;
        }

        variable = declaration[(space + 1)..^1].ToString();
        return true;
    }

    /// <summary>Reads <c>layout(location = N) in mat4 name;</c>.</summary>
    private static bool TryParseMatrixAttribute(ReadOnlySpan<char> line, out int location, out string name)
    {
        const string Prefix = "layout(location = ";
        const string Middle = ") in mat4 ";
        location = 0;
        name = "";
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var middle = line.IndexOf(Middle, StringComparison.Ordinal);
        var semicolon = line.IndexOf(';');
        if (middle < 0 || semicolon < middle
            || !int.TryParse(line[Prefix.Length..middle], NumberStyles.None, CultureInfo.InvariantCulture, out location))
        {
            return false;
        }

        name = line[(middle + Middle.Length)..semicolon].ToString();
        return true;
    }
}
