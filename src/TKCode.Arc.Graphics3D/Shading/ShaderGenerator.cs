using System.Globalization;
using System.Text;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D.Shading;

/// <summary>Everything outside the material that changes the generated program.</summary>
[Flags]
internal enum ShaderFeatures : uint
{
    None = 0,
    HasNormal = 1 << 0,
    HasTangent = 1 << 1,
    HasUv0 = 1 << 2,
    HasUv1 = 1 << 3,
    HasColor = 1 << 4,
    Skinning = 1 << 5,
    Morph = 1 << 6,
    MorphNormals = 1 << 7,
    Instancing = 1 << 8,
    InstanceColor = 1 << 9,
    Fog = 1 << 10,
    Shadows = 1 << 11,
    Environment = 1 << 12,
    SrgbOutput = 1 << 13,
    DepthPass = 1 << 14,
    OpaqueOutput = 1 << 15,

    // Bits 16..18: the tone mapping mode.
    ToneMappingShift = 16,
    ToneMappingMask = 7u << 16,
    SceneColor = 1 << 19,
}

/// <summary>A generated program: sources, and the layout of what the renderer has to feed it.</summary>
internal sealed class GeneratedShader
{
    public required string Vertex { get; init; }
    public required string Fragment { get; init; }
    public required UniformNode[] Uniforms { get; init; }

    /// <summary>Offset of each uniform in the material block, in floats.</summary>
    public required int[] UniformOffsets { get; init; }

    /// <summary>Size of the material block in floats (a multiple of 4); 0 when there is no block.</summary>
    public required int UniformFloats { get; init; }
    public required TextureSlot[] Textures { get; init; }
    public bool UsesShadows { get; init; }
    public bool UsesEnvironment { get; init; }
    public bool UsesMorph { get; init; }
    public bool UsesSceneColor { get; init; }

    public ProgramDescriptor ToDescriptor(string label) => new()
    {
        VertexSource = Vertex,
        FragmentSource = Fragment,
        Label = label,
        UniformBlocks = MaterialBlocks,
        Samplers = MaterialSamplers(UsesShadows, UsesEnvironment, UsesMorph, UsesSceneColor, Textures.Length),
    };

    /// <summary>The uniform blocks of a material program and their slots.</summary>
    public static readonly UniformBlockBinding[] MaterialBlocks =
    [
        new("ViewBlock", ShaderLibrary.SlotView),
        new("SceneBlock", ShaderLibrary.SlotScene),
        new("MaterialBlock", ShaderLibrary.SlotMaterial),
    ];

    /// <summary>The samplers of a material program and their units.</summary>
    public static SamplerBinding[] MaterialSamplers(bool shadows, bool environment, bool morph, bool sceneColor, int textures)
    {
        var samplers = new List<SamplerBinding>
        {
            new("u_frameData", ShaderLibrary.UnitFrameData),
        };
        if (shadows)
        {
            samplers.Add(new("u_shadowMap", ShaderLibrary.UnitShadowMap));
        }

        if (environment)
        {
            samplers.Add(new("u_envMap", ShaderLibrary.UnitEnvironment));
        }

        if (morph)
        {
            samplers.Add(new("u_morphTex", ShaderLibrary.UnitMorph));
        }

        if (sceneColor)
        {
            samplers.Add(new("u_sceneColor", ShaderLibrary.UnitSceneColor));
        }

        for (var i = 0; i < textures; i++)
        {
            samplers.Add(new("u_tex" + i.ToString(CultureInfo.InvariantCulture), ShaderLibrary.FirstMaterialUnit + i));
        }

        return [.. samplers];
    }
}

/// <summary>Turns a material's surface description into a vertex and a fragment shader for the GLSL family. The text
/// is written once for all its languages; for <see cref="ShaderLanguage.Glsl450"/> <see cref="ShaderDialect"/> then
/// rewrites the declarations.</summary>
internal static class ShaderGenerator
{
    public static GeneratedShader Generate(Material material, ShaderFeatures features, ShaderLanguage language, int maxMaterialTextures)
    {
        var s = material.GetSurface();
        var depthPass = (features & ShaderFeatures.DepthPass) != 0;
        var graph = new ShaderGraph();
        var fb = new ShaderBuilder(graph, ShaderStage.Fragment);
        var hasNormalAttr = (features & ShaderFeatures.HasNormal) != 0;
        var flat = s.FlatShading || !hasNormalAttr;
        var model = s.Model;
        var lit = !depthPass && model is not (LightingModel.Unlit or LightingModel.Normal);
        var shadows = lit && (features & ShaderFeatures.Shadows) != 0;
        var pbr = model is LightingModel.Standard or LightingModel.Physical;
        var environment = !depthPass && pbr && (features & ShaderFeatures.Environment) != 0;
        var fog = !depthPass && s.Fog && (features & ShaderFeatures.Fog) != 0;
        var toneMode = (ToneMapping)(((uint)features & (uint)ShaderFeatures.ToneMappingMask) >> (int)ShaderFeatures.ToneMappingShift);
        var toneFunction = s.ToneMapped && model != LightingModel.Normal ? ShaderLibrary.ToneMappingFunction(toneMode) : null;
        var srgb = (features & ShaderFeatures.SrgbOutput) != 0 && model != LightingModel.Normal;
        var instanceColor = (features & ShaderFeatures.InstanceColor) != 0;

        // ---- Fragment: emit the surface graph ----
        string? alphaTest = null;
        var opacity = "1.0";
        string color = "vec3(1.0)", normal = "N", emissive = "", roughness = "1.0", metalness = "0.0", ao = "", envIntensity = "1.0";
        string specularColor = "", shininess = "", specularIntensity = "", ior = "";
        string clearcoat = "", clearcoatRoughness = "", sheenColor = "", sheenRoughness = "";
        string clearcoatNormal = "", iridescence = "", iridescenceIor = "", iridescenceThickness = "", anisotropy = "";
        string transmission = "", thickness = "", attenuationColor = "", attenuationDistance = "";
        var sceneColor = !depthPass && (features & ShaderFeatures.SceneColor) != 0;
        var needOpacity = !depthPass || s.AlphaTest is not null;
        if (needOpacity)
        {
            opacity = fb.Emit(Sl.ToFloat(s.Opacity));
        }

        if (s.AlphaTest is not null)
        {
            alphaTest = fb.Emit(s.AlphaTest);
        }

        if (!depthPass)
        {
            color = fb.Emit(Sl.ToVec3(s.Color));
            if (s.Normal is not null && model != LightingModel.Unlit)
            {
                normal = fb.Emit(s.Normal);
            }

            if (s.Emissive is not null && lit)
            {
                emissive = fb.Emit(s.Emissive);
            }

            if (s.Ao is not null && lit)
            {
                ao = fb.Emit(s.Ao);
            }

            if (pbr)
            {
                roughness = s.Roughness is null ? "1.0" : fb.Emit(s.Roughness);
                metalness = s.Metalness is null ? "0.0" : fb.Emit(s.Metalness);
                envIntensity = s.EnvIntensity is null ? "1.0" : fb.Emit(s.EnvIntensity);
            }

            if (model == LightingModel.Phong)
            {
                specularColor = s.SpecularColor is null ? "vec3(0.07)" : fb.Emit(s.SpecularColor);
                shininess = s.Shininess is null ? "30.0" : fb.Emit(s.Shininess);
            }

            if (model == LightingModel.Physical)
            {
                specularColor = s.SpecularColor is null ? "vec3(1.0)" : fb.Emit(s.SpecularColor);
                specularIntensity = s.SpecularIntensity is null ? "1.0" : fb.Emit(s.SpecularIntensity);
                ior = s.Ior is null ? "1.5" : fb.Emit(s.Ior);
                if (s.Clearcoat is not null)
                {
                    clearcoat = fb.Emit(s.Clearcoat);
                    clearcoatRoughness = s.ClearcoatRoughness is null ? "0.0" : fb.Emit(s.ClearcoatRoughness);
                    if (s.ClearcoatNormal is not null)
                    {
                        clearcoatNormal = fb.Emit(s.ClearcoatNormal);
                    }
                }

                if (s.Iridescence is not null)
                {
                    iridescence = fb.Emit(s.Iridescence);
                    iridescenceIor = s.IridescenceIor is null ? "1.3" : fb.Emit(s.IridescenceIor);
                    iridescenceThickness = s.IridescenceThickness is null ? "400.0" : fb.Emit(s.IridescenceThickness);
                }

                if (s.Anisotropy is not null)
                {
                    anisotropy = fb.Emit(Sl.ToVec3(s.Anisotropy));
                }

                if (s.SheenColor is not null)
                {
                    sheenColor = fb.Emit(s.SheenColor);
                    sheenRoughness = s.SheenRoughness is null ? "1.0" : fb.Emit(s.SheenRoughness);
                }

                // Transmission needs the picture of what is behind the surface; without it the surface is drawn solid.
                if (s.Transmission is not null && sceneColor)
                {
                    transmission = fb.Emit(s.Transmission);
                    thickness = s.Thickness is null ? "0.0" : fb.Emit(s.Thickness);
                    attenuationColor = s.AttenuationColor is null ? "vec3(1.0)" : fb.Emit(s.AttenuationColor);
                    attenuationDistance = s.AttenuationDistance is null ? "1e9" : fb.Emit(s.AttenuationDistance);
                }
            }
        }

        var hasTransmission = transmission.Length > 0;
        var hasClearcoat = clearcoat.Length > 0;
        var hasSheen = sheenColor.Length > 0;
        var usesNormalMapLib = graph.Libraries.Contains("normalmap");
        var tangents = usesNormalMapLib && (features & ShaderFeatures.HasTangent) != 0 && hasNormalAttr;

        // What the fragment stage reads from the vertex stage.
        var needNormal = lit || model == LightingModel.Normal || graph.FragmentNeeds(ShaderInput.NormalWorld);
        var needView = lit || graph.FragmentNeeds(ShaderInput.ViewDirection);
        var needWorldPos = lit || needView || (needNormal && flat) || graph.FragmentNeeds(ShaderInput.PositionWorld);
        var needUv0 = graph.FragmentNeeds(ShaderInput.Uv);
        var needUv1 = graph.FragmentNeeds(ShaderInput.Uv1);
        var needColor = graph.FragmentNeeds(ShaderInput.VertexColor);
        var needLocalPos = graph.FragmentNeeds(ShaderInput.PositionLocal);
        var needLocalNormal = graph.FragmentNeeds(ShaderInput.NormalLocal);
        var needViewDepth = fog || graph.FragmentNeeds(ShaderInput.ViewDepth);
        var needNormalVarying = needNormal && !flat;
        var needObjParams = shadows;
        if (graph.FragmentNeeds(ShaderInput.PointCoord) && s.Kind != VertexKind.Points)
        {
            throw new InvalidOperationException("Sl.PointCoord is only available in point materials.");
        }

        // ---- Vertex: position node, then the values the fragment stage asked for ----
        var vb = new ShaderBuilder(graph, ShaderStage.Vertex);
        string? positionExpr = null;
        if (s.Position is not null)
        {
            vb.InPositionNode = true;
            positionExpr = vb.Emit(Sl.ToVec3(s.Position));
            vb.InPositionNode = false;
        }

        var positionCode = vb.Code.ToString();
        vb.Code.Clear();
        string? pointSize = null;
        string? spriteRotation = null;
        if (s.Kind == VertexKind.Points)
        {
            pointSize = s.PointSize is null ? "1.0" : vb.Emit(Sl.ToFloat(s.PointSize));
        }
        else if (s.Kind == VertexKind.Sprite && s.SpriteRotation is not null)
        {
            spriteRotation = vb.Emit(Sl.ToFloat(s.SpriteRotation));
        }

        var varyingAssignments = new StringBuilder();
        for (var i = 0; i < graph.Varyings.Count; i++)
        {
            var (name, source) = graph.Varyings[i];
            var expr = vb.Emit(source);
            varyingAssignments.Append("    ").Append(name).Append(" = ").Append(expr).Append(";\n");
        }

        var varyingCode = vb.Code.ToString();
        if (graph.Textures.Count > maxMaterialTextures)
        {
            throw new InvalidOperationException(
                $"Material '{material.Name}' samples {graph.Textures.Count} textures; this device allows {maxMaterialTextures} per material.");
        }

        // ---- Shared declarations ----
        var header = new StringBuilder(ShaderLibrary.Header(language));
        void Define(bool on, string name)
        {
            if (on)
            {
                header.Append("#define ").Append(name).Append('\n');
            }
        }

        Define(shadows, "ARC_SHADOWS");
        Define(environment, "ARC_ENV");
        Define(tangents, "ARC_HAS_TANGENT");
        Define(hasClearcoat, "ARC_CLEARCOAT");
        Define(hasSheen, "ARC_SHEEN");
        Define(iridescence.Length > 0, "ARC_IRIDESCENCE");
        Define(anisotropy.Length > 0, "ARC_ANISOTROPY");
        var materialBlock = BuildMaterialBlock(graph, out var offsets, out var floats);
        var samplerDecl = new StringBuilder();
        for (var i = 0; i < graph.Textures.Count; i++)
        {
            samplerDecl.Append("uniform ").Append(graph.Textures[i].IsCube ? "samplerCube" : "sampler2D").Append(" u_tex")
                .Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
        }

        var varyingDecl = new StringBuilder();
        void Varying(bool on, string decl)
        {
            if (on)
            {
                varyingDecl.Append("%IO% ").Append(decl).Append(";\n");
            }
        }

        Varying(needWorldPos, "vec3 v_worldPos");
        Varying(needNormalVarying, "vec3 v_normal");
        Varying(tangents, "vec4 v_tangent");
        Varying(needUv0, "vec2 v_uv0");
        Varying(needUv1, "vec2 v_uv1");
        Varying(needColor, "vec4 v_color");
        Varying(needLocalPos, "vec3 v_localPos");
        Varying(needLocalNormal, "vec3 v_localNormal");
        Varying(needViewDepth, "float v_viewDepth");
        Varying(instanceColor && !depthPass, "vec3 v_instanceColor");
        if (needObjParams)
        {
            varyingDecl.Append("flat %IO% vec4 v_objParams;\n");
        }

        foreach (var (name, source) in graph.Varyings)
        {
            varyingDecl.Append("%IO% ").Append(ShaderNode.GlslName(source.Type)).Append(' ').Append(name).Append(";\n");
        }

        // ---- Vertex shader ----
        var v = new StringBuilder(4096);
        v.Append(header);
        Feature(v, features, ShaderFeatures.Morph, "ARC_MORPH");
        Feature(v, features, ShaderFeatures.MorphNormals, "ARC_MORPH_NORMALS");
        v.Append(ShaderLibrary.Blocks).Append(materialBlock).Append(samplerDecl).Append(ShaderLibrary.VertexCommon);
        AppendLibraries(v, graph, fragment: false);
        v.Append("layout(location = 0) in vec3 a_position;\n");
        Attr(v, features, ShaderFeatures.HasNormal, "layout(location = 1) in vec3 a_normal;\n");
        Attr(v, features, ShaderFeatures.HasTangent, "layout(location = 2) in vec4 a_tangent;\n");
        Attr(v, features, ShaderFeatures.HasUv0, "layout(location = 3) in vec2 a_uv0;\n");
        Attr(v, features, ShaderFeatures.HasUv1, "layout(location = 4) in vec2 a_uv1;\n");
        Attr(v, features, ShaderFeatures.HasColor, "layout(location = 5) in vec4 a_color;\n");
        Attr(v, features, ShaderFeatures.Skinning, "layout(location = 6) in vec4 a_joints;\nlayout(location = 7) in vec4 a_weights;\n");
        Attr(v, features, ShaderFeatures.Instancing, "layout(location = 8) in mat4 a_instanceMatrix;\n");
        Attr(v, features, ShaderFeatures.InstanceColor, "layout(location = 12) in vec3 a_instanceColor;\n");
        if ((features & ShaderFeatures.Morph) != 0)
        {
            v.Append("uniform sampler2D u_morphTex;\nvec4 arcMorph(int i) {\n    return texelFetch(u_morphTex, ivec2(i % 2048, i / 2048), 0);\n}\n");
        }

        v.Append(varyingDecl.ToString().Replace("%IO%", "out"));
        v.Append("void main() {\n");
        v.Append("    int rec = (u_draw.x + gl_InstanceID * u_draw.y) * 4;\n");
        v.Append("    mat4 world = arcAffine(rec);\n");
        v.Append("    vec4 objParams = arcFrameData(rec + 3);\n");
        v.Append("    vec3 position = a_position;\n");
        v.Append(hasNormalAttr ? "    vec3 normal = a_normal;\n" : "    vec3 normal = vec3(0.0, 0.0, 1.0);\n");
        v.Append((features & ShaderFeatures.HasUv0) != 0 ? "    vec2 uv0 = a_uv0;\n" : "    vec2 uv0 = vec2(0.0);\n");
        v.Append((features & ShaderFeatures.HasUv1) != 0 ? "    vec2 uv1 = a_uv1;\n" : "    vec2 uv1 = uv0;\n");
        v.Append((features & ShaderFeatures.HasColor) != 0 ? "    vec4 vertexColor = a_color;\n" : "    vec4 vertexColor = vec4(1.0);\n");
        if ((features & ShaderFeatures.Morph) != 0)
        {
            var stride = (features & ShaderFeatures.MorphNormals) != 0 ? 2 : 1;
            v.Append("    int morphBase = int(objParams.y);\n");
            v.Append("    for (int i = 0; i < u_draw.z; i++) {\n");
            v.Append("        float mw = arcFrameData(morphBase + i).x;\n");
            v.Append("        if (mw != 0.0) {\n");
            v.Append("            int mt = (i * u_draw.w + gl_VertexID) * ").Append(stride).Append(";\n");
            v.Append("            position += mw * arcMorph(mt).xyz;\n");
            if (stride == 2)
            {
                v.Append("            normal += mw * arcMorph(mt + 1).xyz;\n");
            }

            v.Append("        }\n    }\n");
        }

        v.Append(positionCode);
        if (positionExpr is not null)
        {
            v.Append("    position = ").Append(positionExpr).Append(";\n");
        }

        if ((features & ShaderFeatures.Skinning) != 0)
        {
            v.Append("    int boneBase = int(objParams.x);\n");
            v.Append("    vec4 sw = a_weights / max(a_weights.x + a_weights.y + a_weights.z + a_weights.w, 1e-6);\n");
            v.Append("    world = arcAffine(boneBase + int(a_joints.x) * 3) * sw.x + arcAffine(boneBase + int(a_joints.y) * 3) * sw.y\n");
            v.Append("        + arcAffine(boneBase + int(a_joints.z) * 3) * sw.z + arcAffine(boneBase + int(a_joints.w) * 3) * sw.w;\n");
        }

        if ((features & ShaderFeatures.Instancing) != 0)
        {
            v.Append("    world = world * a_instanceMatrix;\n");
        }

        if (s.Kind == VertexKind.Sprite)
        {
            v.Append("    vec4 worldPos = world * vec4(0.0, 0.0, 0.0, 1.0);\n");
            v.Append("    vec4 viewPos = u_view * worldPos;\n");
            v.Append("    vec2 spriteScale = vec2(length(world[0].xyz), length(world[1].xyz));\n");
            if (!s.SizeAttenuation)
            {
                v.Append("    if (u_cameraDir.w < 0.5) {\n        spriteScale *= -viewPos.z;\n    }\n");
            }

            v.Append("    vec2 corner = position.xy * spriteScale;\n");
            if (spriteRotation is not null)
            {
                v.Append(varyingCode);
                varyingCode = "";
                v.Append("    float sr = sin(").Append(spriteRotation).Append(");\n    float cr = cos(").Append(spriteRotation).Append(");\n");
                v.Append("    corner = vec2(cr * corner.x - sr * corner.y, sr * corner.x + cr * corner.y);\n");
            }

            v.Append("    viewPos.xy += corner;\n");
            v.Append("    vec3 worldNormal = -u_cameraDir.xyz;\n");
            v.Append("    gl_Position = u_proj * viewPos;\n");
        }
        else
        {
            v.Append("    vec4 worldPos = world * vec4(position, 1.0);\n");
            v.Append("    vec3 worldNormal = normalize(arcNormalMatrix(world) * normal);\n");
            v.Append("    gl_Position = u_viewProj * worldPos;\n");
        }

        v.Append(varyingCode);
        if (pointSize is not null)
        {
            v.Append("    gl_PointSize = ").Append(pointSize).Append(";\n");
            if (s.SizeAttenuation)
            {
                v.Append("    if (u_cameraDir.w < 0.5) {\n        gl_PointSize *= u_clip.z * u_viewport.y * 0.5 / max(gl_Position.w, 1e-6);\n    } else {\n        gl_PointSize *= u_clip.z * u_viewport.y * 0.5;\n    }\n");
            }
        }

        v.Append(varyingAssignments);
        Assign(v, needWorldPos, "v_worldPos = worldPos.xyz");
        Assign(v, needNormalVarying, "v_normal = worldNormal");
        Assign(v, tangents, "v_tangent = vec4(normalize(mat3(world) * a_tangent.xyz), a_tangent.w)");
        Assign(v, needUv0, "v_uv0 = uv0");
        Assign(v, needUv1, "v_uv1 = uv1");
        Assign(v, needColor, "v_color = vertexColor");
        Assign(v, needLocalPos, "v_localPos = position");
        Assign(v, needLocalNormal, "v_localNormal = normal");
        Assign(v, needViewDepth, "v_viewDepth = -(u_view * worldPos).z");
        Assign(v, instanceColor && !depthPass, "v_instanceColor = a_instanceColor");
        Assign(v, needObjParams, "v_objParams = objParams");
        v.Append("}\n");

        // ---- Fragment shader ----
        var f = new StringBuilder(8192);
        f.Append(header);
        f.Append(ShaderLibrary.Blocks).Append(materialBlock).Append(samplerDecl);
        f.Append(varyingDecl.ToString().Replace("%IO%", "in"));
        if (!depthPass)
        {
            f.Append("out vec4 o_color;\n");
            f.Append(ShaderLibrary.FragmentCommon);
        }

        AppendLibraries(f, graph, fragment: true);
        if (lit)
        {
            if (shadows)
            {
                f.Append(ShaderLibrary.Shadows);
            }

            if (environment)
            {
                f.Append(ShaderLibrary.Environment);
            }

            if (hasTransmission)
            {
                f.Append("uniform sampler2D u_sceneColor;\n");
            }

            f.Append(ShaderLibrary.Brdf);
            if (iridescence.Length > 0 || anisotropy.Length > 0)
            {
                f.Append(ShaderLibrary.PhysicalLayers);
            }
            AppendLightingModel(f, model, s.ToonSteps);
        }

        if (toneFunction is not null || srgb)
        {
            f.Append(ShaderLibrary.ToneMapping);
        }

        f.Append("void main() {\n");
        if (needView)
        {
            f.Append("    vec3 V = u_cameraDir.w > 0.5 ? -u_cameraDir.xyz : normalize(u_cameraPos.xyz - v_worldPos);\n");
        }

        if (needNormal)
        {
            if (flat)
            {
                f.Append("    vec3 N = normalize(cross(dFdx(v_worldPos), dFdy(v_worldPos)));\n");
            }
            else
            {
                f.Append("    vec3 N = normalize(v_normal);\n    if (!gl_FrontFacing) {\n        N = -N;\n    }\n");
            }
        }

        f.Append(fb.Code);
        if (needOpacity)
        {
            f.Append("    float s_opacity = ").Append(opacity).Append(";\n");
        }

        if (alphaTest is not null)
        {
            f.Append("    if (s_opacity < ").Append(alphaTest).Append(") {\n        discard;\n    }\n");
        }

        if (!depthPass)
        {
            f.Append("    vec3 s_color = ").Append(color).Append(";\n");
            if (instanceColor)
            {
                f.Append("    s_color *= v_instanceColor;\n");
            }

            switch (model)
            {
                case LightingModel.Unlit:
                    f.Append("    vec3 outColor = s_color;\n");
                    break;
                case LightingModel.Normal:
                    f.Append("    vec3 outColor = normalize(").Append(normal).Append(") * 0.5 + 0.5;\n");
                    break;
                case LightingModel.ShadowCatcher:
                    f.Append("    vec3 outColor = s_color;\n");
                    f.Append(shadows
                        ? "    s_opacity *= 1.0 - arcShadowMask(v_worldPos, N, v_objParams.z);\n"
                        : "    s_opacity = 0.0;\n");
                    break;
                default:
                    AppendLitMain(f, model, flat, shadows, environment, normal, emissive, ao, roughness, metalness, envIntensity,
                        specularColor, shininess, specularIntensity, ior, clearcoat, clearcoatRoughness, sheenColor, sheenRoughness,
                        transmission, thickness, attenuationColor, attenuationDistance,
                        new LayerInputs(clearcoatNormal, iridescence, iridescenceIor, iridescenceThickness, anisotropy));
                    break;
            }

            if (fog)
            {
                f.Append("    outColor = arcFog(outColor, v_viewDepth);\n");
            }

            if (toneFunction is not null)
            {
                f.Append("    outColor = ").Append(toneFunction).Append("(outColor * u_ambient.w);\n");
            }

            if (srgb)
            {
                f.Append("    outColor = arcLinearToSrgb(outColor);\n");
            }

            f.Append((features & ShaderFeatures.OpaqueOutput) != 0 ? "    o_color = vec4(outColor, 1.0);\n" : "    o_color = vec4(outColor, s_opacity);\n");
        }

        f.Append("}\n");
        var usesMorph = (features & ShaderFeatures.Morph) != 0;
        var vertexSource = v.ToString();
        var fragmentSource = f.ToString();
        if (language == ShaderLanguage.Glsl450)
        {
            ShaderDialect.ToGlsl450(ref vertexSource, ref fragmentSource, GeneratedShader.MaterialBlocks,
                GeneratedShader.MaterialSamplers(shadows, environment, usesMorph, hasTransmission, graph.Textures.Count));
        }

        return new GeneratedShader
        {
            Vertex = vertexSource,
            Fragment = fragmentSource,
            Uniforms = [.. graph.Uniforms],
            UniformOffsets = offsets,
            UniformFloats = floats,
            Textures = [.. graph.Textures],
            UsesShadows = shadows,
            UsesEnvironment = environment,
            UsesMorph = usesMorph,
            UsesSceneColor = hasTransmission,
        };
    }

    private static void Feature(StringBuilder sb, ShaderFeatures features, ShaderFeatures flag, string define)
    {
        if ((features & flag) != 0)
        {
            sb.Append("#define ").Append(define).Append('\n');
        }
    }

    private static void Attr(StringBuilder sb, ShaderFeatures features, ShaderFeatures flag, string declaration)
    {
        if ((features & flag) != 0)
        {
            sb.Append(declaration);
        }
    }

    private static void Assign(StringBuilder sb, bool on, string statement)
    {
        if (on)
        {
            sb.Append("    ").Append(statement).Append(";\n");
        }
    }

    /// <summary>A full-screen program for a <see cref="PostPass"/>: one triangle over the target, and a fragment
    /// shader that evaluates the node. The picture so far is the sampler <c>u_input</c>.</summary>
    public static GeneratedShader GeneratePostPass(ShaderNode color, ShaderLanguage language, int maxMaterialTextures)
    {
        var graph = new ShaderGraph();
        var fb = new ShaderBuilder(graph, ShaderStage.Fragment);
        var result = color.Type == ShaderType.Vec4
            ? fb.Emit(color)
            : "vec4(" + fb.Emit(Sl.ToVec3(color)) + ", texture(u_input, v_uv0).a)";

        // Only what exists without a surface.
        const uint Allowed = (1u << (int)ShaderInput.Uv) | (1u << (int)ShaderInput.Time) | (1u << (int)ShaderInput.ScreenUv) | (1u << (int)ShaderInput.CameraPosition);
        if ((graph.FragmentInputs & ~Allowed) != 0 || graph.Varyings.Count > 0)
        {
            throw new InvalidOperationException("A post pass has no surface: positions, normals, vertex colors, the second UV set and per-vertex values are not available. Use PostPass.Uv, Sl.Time, uniforms and textures.");
        }

        if (graph.Textures.Count > maxMaterialTextures)
        {
            throw new InvalidOperationException($"The post pass uses {graph.Textures.Count} textures; this device allows {maxMaterialTextures}.");
        }

        var header = ShaderLibrary.Header(language);
        var materialBlock = BuildMaterialBlock(graph, out var offsets, out var floats);
        var f = new StringBuilder(header);
        f.Append(ShaderLibrary.Blocks).Append(materialBlock);
        for (var i = 0; i < graph.Textures.Count; i++)
        {
            f.Append("uniform ").Append(graph.Textures[i].IsCube ? "samplerCube" : "sampler2D").Append(" u_tex")
                .Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
        }

        f.Append("uniform sampler2D u_input;\nin vec2 v_uv0;\nout vec4 o_color;\n");
        f.Append(ShaderLibrary.FragmentCommon);
        AppendLibraries(f, graph, fragment: true);
        f.Append("void main() {\n").Append(fb.Code).Append("    o_color = ").Append(result).Append(";\n}\n");
        var vertex = header + """
out vec2 v_uv0;
void main() {
    vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
    v_uv0 = p;
    gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}

""";
        var fragment = f.ToString();
        if (language == ShaderLanguage.Glsl450)
        {
            ShaderDialect.ToGlsl450(ref vertex, ref fragment, GeneratedShader.MaterialBlocks, PostPassSamplers(graph.Textures.Count));
        }

        return new GeneratedShader
        {
            Vertex = vertex,
            Fragment = fragment,
            Uniforms = [.. graph.Uniforms],
            UniformOffsets = offsets,
            UniformFloats = floats,
            Textures = [.. graph.Textures],
        };
    }

    /// <summary>The samplers of a post pass program and their units: the picture so far on unit 0, then the pass's own
    /// textures on the material units.</summary>
    public static SamplerBinding[] PostPassSamplers(int textures)
    {
        var samplers = new SamplerBinding[textures + 1];
        samplers[0] = new SamplerBinding("u_input", 0);
        for (var i = 0; i < textures; i++)
        {
            samplers[i + 1] = new SamplerBinding("u_tex" + i.ToString(CultureInfo.InvariantCulture), ShaderLibrary.FirstMaterialUnit + i);
        }

        return samplers;
    }

    private static void AppendLibraries(StringBuilder sb, ShaderGraph graph, bool fragment)
    {
        foreach (var id in graph.Libraries)
        {
            switch (id)
            {
                case "hash":
                    sb.Append(ShaderLibrary.Hash);
                    break;
                case "noise":
                    sb.Append(ShaderLibrary.Noise);
                    break;
                case "normalmap" when fragment:
                    sb.Append(ShaderLibrary.NormalMap);
                    break;
            }
        }
    }

    private static string BuildMaterialBlock(ShaderGraph graph, out int[] offsets, out int floats)
    {
        offsets = new int[graph.Uniforms.Count];
        floats = 0;
        if (graph.Uniforms.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder("layout(std140) uniform MaterialBlock {\n");
        var offset = 0;
        for (var i = 0; i < graph.Uniforms.Count; i++)
        {
            var u = graph.Uniforms[i];
            var (align, size) = u.Layout;
            offset = (offset + align - 1) / align * align;
            offsets[i] = offset;
            offset += size;
            sb.Append("    ").Append(ShaderNode.GlslName(u.Type == ShaderType.Bool ? ShaderType.Float : u.Type)).Append(" u_m")
                .Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
        }

        floats = (offset + 3) / 4 * 4;
        return sb.Append("};\n").ToString();
    }

    private static void AppendLightingModel(StringBuilder f, LightingModel model, int toonSteps)
    {
        f.Append("vec3 g_N;\nvec3 g_V;\nvec3 g_diffuse;\nvec3 g_directDiffuse;\nvec3 g_directSpecular;\n");
        switch (model)
        {
            case LightingModel.Standard:
            case LightingModel.Physical:
                f.Append("""
vec3 g_f0;
float g_f90;
float g_roughness;
#ifdef ARC_CLEARCOAT
vec3 g_ccN;
float g_ccRoughness;
vec3 g_ccSpecular;
#endif
#ifdef ARC_SHEEN
vec3 g_sheenColor;
float g_sheenRoughness;
vec3 g_sheenSpecular;
#endif
#ifdef ARC_IRIDESCENCE
vec3 g_iridFresnel;
float g_iridescence;
#endif
#ifdef ARC_ANISOTROPY
vec3 g_anisoT;
vec3 g_anisoB;
float g_alphaT;
#endif
void arcAddLight(vec3 L, vec3 radiance) {
    float NoL = clamp(dot(g_N, L), 0.0, 1.0);
    vec3 irradiance = radiance * NoL;
    g_directDiffuse += irradiance * g_diffuse * RECIPROCAL_PI;
#if defined(ARC_IRIDESCENCE) || defined(ARC_ANISOTROPY)
    vec3 H = normalize(L + g_V);
    float NoV = clamp(dot(g_N, g_V), 0.0, 1.0);
    float NoH = clamp(dot(g_N, H), 0.0, 1.0);
    vec3 F = arcFresnelSchlick(g_f0, g_f90, clamp(dot(g_V, H), 0.0, 1.0));
#ifdef ARC_IRIDESCENCE
    F = mix(F, g_iridFresnel, g_iridescence);
#endif
    float a = g_roughness * g_roughness;
#ifdef ARC_ANISOTROPY
    float DV = arcVisibilityAniso(g_alphaT, a, dot(g_anisoT, g_V), dot(g_anisoB, g_V), dot(g_anisoT, L), dot(g_anisoB, L), NoV, NoL)
        * arcDistributionAniso(g_alphaT, a, NoH, dot(g_anisoT, H), dot(g_anisoB, H));
#else
    float DV = arcVisibilitySmith(a, NoL, NoV) * arcDistributionGgx(a, NoH);
#endif
    g_directSpecular += irradiance * F * DV;
#else
    g_directSpecular += irradiance * arcSpecularGgx(L, g_V, g_N, g_f0, g_f90, g_roughness);
#endif
#ifdef ARC_CLEARCOAT
    g_ccSpecular += radiance * clamp(dot(g_ccN, L), 0.0, 1.0) * arcSpecularGgx(L, g_V, g_ccN, vec3(0.04), 1.0, g_ccRoughness);
#endif
#ifdef ARC_SHEEN
    g_sheenSpecular += irradiance * arcSheen(L, g_V, g_N, g_sheenColor, g_sheenRoughness);
#endif
}

""");
                break;
            case LightingModel.Phong:
                f.Append("""
vec3 g_specular;
float g_shininess;
void arcAddLight(vec3 L, vec3 radiance) {
    float NoL = clamp(dot(g_N, L), 0.0, 1.0);
    vec3 irradiance = radiance * NoL;
    g_directDiffuse += irradiance * g_diffuse * RECIPROCAL_PI;
    vec3 H = normalize(L + g_V);
    float NoH = clamp(dot(g_N, H), 0.0, 1.0);
    float VoH = clamp(dot(g_V, H), 0.0, 1.0);
    float D = RECIPROCAL_PI * (g_shininess * 0.5 + 1.0) * pow(NoH, g_shininess);
    g_directSpecular += irradiance * arcFresnelSchlick(g_specular, 1.0, VoH) * (0.25 * D);
}

""");
                break;
            case LightingModel.Toon:
                f.Append("void arcAddLight(vec3 L, vec3 radiance) {\n    float t = dot(g_N, L) * 0.5 + 0.5;\n    float steps = ")
                    .Append(ShaderNode.Literal(toonSteps))
                    .Append(";\n    float q = clamp(floor(t * steps) / (steps - 1.0), 0.0, 1.0);\n    g_directDiffuse += radiance * q * g_diffuse * RECIPROCAL_PI;\n}\n\n");
                break;
            default:
                f.Append("void arcAddLight(vec3 L, vec3 radiance) {\n    g_directDiffuse += radiance * clamp(dot(g_N, L), 0.0, 1.0) * g_diffuse * RECIPROCAL_PI;\n}\n\n");
                break;
        }

        f.Append("""
void arcLights(vec3 worldPos, vec3 Ng, float receiveShadow) {
    for (int i = 0; i < u_lightCounts.x; i++) {
        vec4 a = u_dirLights[i * 2];
        vec3 radiance = u_dirLights[i * 2 + 1].rgb;
#ifdef ARC_SHADOWS
        if (a.w >= 0.0 && receiveShadow > 0.5) {
            radiance *= arcShadow(int(a.w), worldPos, Ng);
        }
#endif
        arcAddLight(a.xyz, radiance);
    }
    for (int i = 0; i < u_lightCounts.y; i++) {
        vec4 a = u_pointLights[i * 3];
        vec4 b = u_pointLights[i * 3 + 1];
        vec3 d = a.xyz - worldPos;
        float dist = length(d);
        vec3 radiance = b.rgb * arcDistanceAttenuation(dist, a.w, b.w);
#ifdef ARC_SHADOWS
        float si = u_pointLights[i * 3 + 2].x;
        if (si >= 0.0 && receiveShadow > 0.5) {
            radiance *= arcShadow(int(si) + arcCubeFace(-d), worldPos, Ng);
        }
#endif
        arcAddLight(d / max(dist, 1e-5), radiance);
    }
    for (int i = 0; i < u_lightCounts.z; i++) {
        vec4 a = u_spotLights[i * 4];
        vec4 b = u_spotLights[i * 4 + 1];
        vec4 c = u_spotLights[i * 4 + 2];
        vec4 e = u_spotLights[i * 4 + 3];
        vec3 d = a.xyz - worldPos;
        float dist = length(d);
        vec3 L = d / max(dist, 1e-5);
        float spot = smoothstep(c.w, e.x, dot(L, b.xyz));
        if (spot > 0.0) {
            vec3 radiance = c.rgb * (spot * arcDistanceAttenuation(dist, a.w, b.w));
#ifdef ARC_SHADOWS
            if (e.y >= 0.0 && receiveShadow > 0.5) {
                radiance *= arcShadow(int(e.y), worldPos, Ng);
            }
#endif
            arcAddLight(L, radiance);
        }
    }
}
vec3 arcAmbientIrradiance(vec3 n) {
    vec3 irradiance = u_ambient.rgb;
    for (int i = 0; i < u_lightCounts.w; i++) {
        float w = 0.5 * dot(n, u_hemiLights[i * 3 + 2].xyz) + 0.5;
        irradiance += mix(u_hemiLights[i * 3 + 1].rgb, u_hemiLights[i * 3].rgb, w);
    }
    return irradiance;
}
#ifdef ARC_SHADOWS
float arcShadowMask(vec3 worldPos, vec3 Ng, float receiveShadow) {
    float mask = 1.0;
    if (receiveShadow < 0.5) {
        return mask;
    }
    for (int i = 0; i < u_lightCounts.x; i++) {
        float si = u_dirLights[i * 2].w;
        if (si >= 0.0) {
            mask *= arcShadow(int(si), worldPos, Ng);
        }
    }
    for (int i = 0; i < u_lightCounts.y; i++) {
        float si = u_pointLights[i * 3 + 2].x;
        if (si >= 0.0) {
            mask *= arcShadow(int(si) + arcCubeFace(worldPos - u_pointLights[i * 3].xyz), worldPos, Ng);
        }
    }
    for (int i = 0; i < u_lightCounts.z; i++) {
        float si = u_spotLights[i * 4 + 3].y;
        if (si >= 0.0) {
            mask *= arcShadow(int(si), worldPos, Ng);
        }
    }
    return mask;
}
#endif

""");
    }

    private static void AppendLitMain(
        StringBuilder f, LightingModel model, bool flat, bool shadows, bool environment, string normal, string emissive, string ao,
        string roughness, string metalness, string envIntensity, string specularColor, string shininess, string specularIntensity,
        string ior, string clearcoat, string clearcoatRoughness, string sheenColor, string sheenRoughness,
        string transmission, string thickness, string attenuationColor, string attenuationDistance, LayerInputs layers)
    {
        var pbr = model is LightingModel.Standard or LightingModel.Physical;
        f.Append("    g_N = normalize(").Append(normal).Append(");\n");
        f.Append("    g_V = V;\n");
        f.Append("    g_directDiffuse = vec3(0.0);\n    g_directSpecular = vec3(0.0);\n");
        f.Append("    float receiveShadow = ").Append(shadows ? "v_objParams.z" : "0.0").Append(";\n");
        f.Append("    float s_ao = ").Append(ao.Length > 0 ? ao : "1.0").Append(";\n");
        f.Append("    vec3 indirectSpecular = vec3(0.0);\n");
        if (pbr)
        {
            f.Append("    float s_roughness = clamp(").Append(roughness).Append(", 0.0525, 1.0);\n");
            f.Append("    float s_metalness = clamp(").Append(metalness).Append(", 0.0, 1.0);\n");
            if (!flat)
            {
                // Widen the highlight where the normal changes fast, so distant bumpy surfaces do not sparkle.
                f.Append("    vec3 dn = max(abs(dFdx(N)), abs(dFdy(N)));\n");
                f.Append("    s_roughness = min(s_roughness + max(max(dn.x, dn.y), dn.z), 1.0);\n");
            }

            f.Append("    g_roughness = s_roughness;\n");
            f.Append("    g_diffuse = s_color * (1.0 - s_metalness);\n");
            if (model == LightingModel.Physical)
            {
                f.Append("    float s_specularIntensity = ").Append(specularIntensity).Append(";\n");
                f.Append("    float iorRatio = (").Append(ior).Append(" - 1.0) / (").Append(ior).Append(" + 1.0);\n");
                f.Append("    vec3 dielectricF0 = min(iorRatio * iorRatio * ").Append(specularColor).Append(", vec3(1.0)) * s_specularIntensity;\n");
                f.Append("    g_f0 = mix(dielectricF0, s_color, s_metalness);\n");
                f.Append("    g_f90 = mix(s_specularIntensity, 1.0, s_metalness);\n");
            }
            else
            {
                f.Append("    g_f0 = mix(vec3(0.04), s_color, s_metalness);\n    g_f90 = 1.0;\n");
            }

            if (clearcoat.Length > 0)
            {
                f.Append("    float s_clearcoat = clamp(").Append(clearcoat).Append(", 0.0, 1.0);\n");
                f.Append("    g_ccN = ").Append(layers.ClearcoatNormal.Length > 0 ? "normalize(" + layers.ClearcoatNormal + ")" : "N").Append(";\n");
                f.Append("    g_ccRoughness = clamp(").Append(clearcoatRoughness).Append(", 0.0525, 1.0);\n    g_ccSpecular = vec3(0.0);\n");
            }

            if (layers.Iridescence.Length > 0)
            {
                // A thin film over the base changes its Fresnel reflectance with angle and wavelength.
                f.Append("    float iridNoV = clamp(dot(g_N, V), 0.0, 1.0);\n");
                f.Append("    float s_iridThickness = max(").Append(layers.IridescenceThickness).Append(", 0.0);\n");
                f.Append("    g_iridescence = s_iridThickness > 0.0 ? clamp(").Append(layers.Iridescence).Append(", 0.0, 1.0) : 0.0;\n");
                f.Append("    g_iridFresnel = g_iridescence > 0.0 ? arcIridescence(1.0, ").Append(layers.IridescenceIor).Append(", iridNoV, s_iridThickness, g_f0) : g_f0;\n");
                f.Append("    vec3 envF0 = mix(g_f0, arcSchlickToF0(g_iridFresnel, g_f90, iridNoV), g_iridescence);\n");
            }
            else
            {
                f.Append("    vec3 envF0 = g_f0;\n");
            }

            if (layers.Anisotropy.Length > 0)
            {
                // The highlight stretches across the given direction: rougher along it, as rough as before across it.
                f.Append("    vec3 anisoRaw = ").Append(layers.Anisotropy).Append(";\n");
                f.Append("    float anisoLength = length(anisoRaw);\n");
                f.Append("    float s_anisotropy = min(anisoLength, 1.0);\n");
                f.Append("    vec3 anisoDir = anisoLength > 1e-5 ? anisoRaw / anisoLength : vec3(1.0, 0.0, 0.0);\n");
                f.Append("    anisoDir -= g_N * dot(g_N, anisoDir);\n");
                f.Append("    g_anisoT = dot(anisoDir, anisoDir) > 1e-8 ? normalize(anisoDir) : normalize(cross(g_N, vec3(0.0, 1.0, 0.0)));\n");
                f.Append("    g_anisoB = cross(g_N, g_anisoT);\n");
                f.Append("    g_alphaT = mix(s_roughness * s_roughness, 1.0, s_anisotropy * s_anisotropy);\n");
            }

            if (sheenColor.Length > 0)
            {
                f.Append("    g_sheenColor = ").Append(sheenColor).Append(";\n    g_sheenRoughness = clamp(").Append(sheenRoughness)
                    .Append(", 0.07, 1.0);\n    g_sheenSpecular = vec3(0.0);\n");
            }
        }
        else
        {
            f.Append("    g_diffuse = s_color;\n");
            if (model == LightingModel.Phong)
            {
                f.Append("    g_specular = ").Append(specularColor).Append(";\n    g_shininess = max(").Append(shininess).Append(", 1e-4);\n");
            }
        }

        f.Append("    arcLights(v_worldPos, N, receiveShadow);\n");
        f.Append("    vec3 indirectDiffuse = arcAmbientIrradiance(g_N) * g_diffuse * RECIPROCAL_PI;\n");
        if (pbr)
        {
            f.Append("    float NoV = clamp(dot(g_N, V), 0.0001, 1.0);\n");
            if (environment)
            {
                f.Append("    float s_env = ").Append(envIntensity).Append(";\n");
                f.Append("    vec3 envIrradiance = arcEnvDiffuse(g_N) * s_env;\n");
                if (layers.Anisotropy.Length > 0)
                {
                    // The reflection of an anisotropic surface is smeared across the direction: bend the normal toward it.
                    f.Append("    vec3 bent = cross(cross(g_anisoB, V), g_anisoB);\n");
                    f.Append("    bent = dot(bent, bent) > 1e-8 ? normalize(bent) : g_N;\n");
                    f.Append("    vec3 envN = normalize(mix(bent, g_N, arcPow2(arcPow2(1.0 - s_anisotropy * (1.0 - s_roughness)))));\n");
                }
                else
                {
                    f.Append("    vec3 envN = g_N;\n");
                }

                f.Append("    vec3 R = normalize(mix(reflect(-V, envN), envN, s_roughness * s_roughness));\n");
                f.Append("    vec3 envRadiance = arcEnvSpecular(R, s_roughness) * s_env;\n");
                f.Append("    vec2 fab = arcDfg(NoV, s_roughness);\n");
                f.Append("    vec3 FssEss = envF0 * fab.x + g_f90 * fab.y;\n");
                f.Append("    float Ems = 1.0 - (fab.x + fab.y);\n");
                f.Append("    vec3 Favg = envF0 + (1.0 - envF0) * 0.047619;\n");
                f.Append("    vec3 Fms = FssEss * Favg / (1.0 - Ems * Favg);\n");
                f.Append("    vec3 multi = Fms * Ems;\n");
                f.Append("    vec3 totalScatter = FssEss + multi;\n");
                f.Append("    indirectSpecular = envRadiance * FssEss + multi * envIrradiance;\n");
                f.Append("    indirectDiffuse += g_diffuse * (1.0 - max(max(totalScatter.r, totalScatter.g), totalScatter.b)) * envIrradiance;\n");
                if (sheenColor.Length > 0)
                {
                    f.Append("    g_sheenSpecular += envIrradiance * g_sheenColor * arcSheenEnv(NoV, g_sheenRoughness);\n");
                }

                if (clearcoat.Length > 0)
                {
                    f.Append("    vec2 ccFab = arcDfg(clamp(dot(g_ccN, V), 0.0001, 1.0), g_ccRoughness);\n");
                    f.Append("    g_ccSpecular += arcEnvSpecular(normalize(mix(reflect(-V, g_ccN), g_ccN, g_ccRoughness * g_ccRoughness)), g_ccRoughness) * s_env * (0.04 * ccFab.x + ccFab.y);\n");
                }
            }

            f.Append("    indirectSpecular *= clamp(pow(NoV + s_ao, exp2(-16.0 * s_roughness - 1.0)) - 1.0 + s_ao, 0.0, 1.0);\n");
        }

        f.Append("    indirectDiffuse *= s_ao;\n");
        f.Append("    vec3 totalDiffuse = g_directDiffuse + indirectDiffuse;\n");
        if (transmission.Length > 0)
        {
            // What is behind the surface, seen through it: look up the scene picture where the refracted ray leaves the
            // volume, blurrier for rougher surfaces, tinted by the surface color and the volume's absorption.
            f.Append("    float s_transmission = clamp(").Append(transmission).Append(", 0.0, 1.0);\n");
            f.Append("    float s_thickness = max(").Append(thickness).Append(", 0.0);\n");
            f.Append("    float s_ior = ").Append(ior).Append(";\n");
            f.Append("    vec3 refracted = refract(-V, g_N, 1.0 / s_ior);\n");
            f.Append("    vec4 exitClip = u_viewProj * vec4(v_worldPos + refracted * s_thickness, 1.0);\n");
            f.Append("    vec2 exitUv = clamp(exitClip.xy / max(exitClip.w, 1e-5) * 0.5 + 0.5, vec2(0.0), vec2(1.0));\n");
            f.Append("    float sceneLod = u_fogParams.w * s_roughness * clamp(s_ior * 2.0 - 2.0, 0.0, 1.0);\n");
            f.Append("    vec3 transmitted = textureLod(u_sceneColor, exitUv, sceneLod).rgb * s_color;\n");
            f.Append("    float s_attenuationDistance = ").Append(attenuationDistance).Append(";\n");
            f.Append("    if (s_attenuationDistance < 1e8 && s_thickness > 0.0) {\n");
            f.Append("        transmitted *= pow(max(").Append(attenuationColor).Append(", vec3(1e-4)), vec3(s_thickness / max(s_attenuationDistance, 1e-5)));\n");
            f.Append("    }\n");
            f.Append("    totalDiffuse = mix(totalDiffuse, transmitted, s_transmission * (1.0 - s_metalness));\n");
        }

        f.Append("    vec3 outColor = totalDiffuse + g_directSpecular + indirectSpecular;\n");
        if (emissive.Length > 0)
        {
            f.Append("    outColor += ").Append(emissive).Append(";\n");
        }

        if (sheenColor.Length > 0)
        {
            f.Append("    outColor = outColor * (1.0 - 0.157 * max(max(g_sheenColor.r, g_sheenColor.g), g_sheenColor.b)) + g_sheenSpecular;\n");
        }

        if (clearcoat.Length > 0)
        {
            f.Append("    float ccFresnel = 0.04 + 0.96 * pow(1.0 - clamp(dot(g_ccN, V), 0.0, 1.0), 5.0);\n");
            f.Append("    outColor = outColor * (1.0 - s_clearcoat * ccFresnel) + g_ccSpecular * s_clearcoat;\n");
        }
    }
}

/// <summary>The fragment code of the inputs only a physical surface has, beyond those every lit surface passes.</summary>
internal readonly record struct LayerInputs(string ClearcoatNormal, string Iridescence, string IridescenceIor, string IridescenceThickness, string Anisotropy);
