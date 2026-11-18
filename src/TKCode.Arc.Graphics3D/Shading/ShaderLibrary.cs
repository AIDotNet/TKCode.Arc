namespace TKCode.Arc.Graphics3D.Shading;

/// <summary>The engine's own shader code for the GLSL family (GLSL ES 3.00, GLSL 3.30 and GLSL 4.50 share it; the
/// header differs, and <see cref="ShaderDialect"/> rewrites the declarations for GLSL 4.50). Material graphs produce
/// surface values; this library turns them into lit pixels. A backend with a language outside the family supplies its
/// own version of this file.</summary>
internal static class ShaderLibrary
{
    public const int MaxDirectionalLights = 4;
    public const int MaxPointLights = 16;
    public const int MaxSpotLights = 8;
    public const int MaxHemisphereLights = 2;
    public const int MaxShadows = 16;

    public const int UnitFrameData = 0;
    public const int UnitShadowMap = 1;
    public const int UnitEnvironment = 2;
    public const int UnitMorph = 3;
    public const int UnitSceneColor = 4;
    public const int FirstMaterialUnit = 5;

    public const int SlotView = 0;
    public const int SlotScene = 1;
    public const int SlotMaterial = 2;

    /// <summary>Width in texels of the per-frame data texture (object records, bones, morph weights).</summary>
    public const int FrameDataWidth = 1024;

    /// <summary>Width in texels of a geometry's morph target texture.</summary>
    public const int MorphTextureWidth = 2048;

    public const string Blocks = """
layout(std140) uniform ViewBlock {
    mat4 u_viewProj;
    mat4 u_view;
    mat4 u_proj;
    vec4 u_cameraPos;
    vec4 u_viewport;
    vec4 u_cameraDir;
    vec4 u_clip;
};
layout(std140) uniform SceneBlock {
    vec4 u_ambient;
    vec4 u_fogColor;
    vec4 u_fogParams;
    vec4 u_envParams;
    vec4 u_sh[9];
    ivec4 u_lightCounts;
    vec4 u_dirLights[8];
    vec4 u_pointLights[48];
    vec4 u_spotLights[32];
    vec4 u_hemiLights[6];
    mat4 u_shadowMatrices[16];
    vec4 u_shadowParams[16];
    vec4 u_shadowRects[16];
    vec4 u_shadowAtlas;
};

""";

    public const string VertexCommon = """
uniform sampler2D u_frameData;
uniform ivec4 u_draw;

vec4 arcFrameData(int i) {
    return texelFetch(u_frameData, ivec2(i & 1023, i >> 10), 0);
}
mat4 arcAffine(int i) {
    vec4 a = arcFrameData(i);
    vec4 b = arcFrameData(i + 1);
    vec4 c = arcFrameData(i + 2);
    return mat4(vec4(a.xyz, 0.0), vec4(b.xyz, 0.0), vec4(c.xyz, 0.0), vec4(a.w, b.w, c.w, 1.0));
}
mat3 arcNormalMatrix(mat4 m) {
    vec3 a = m[0].xyz;
    vec3 b = m[1].xyz;
    vec3 c = m[2].xyz;
    vec3 bc = cross(b, c);
    float s = dot(a, bc) < 0.0 ? -1.0 : 1.0;
    return mat3(bc * s, cross(c, a) * s, cross(a, b) * s);
}

""";

    public const string Hash = """
float arcHash12(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

""";

    public const string Noise = """
float arcHash13(vec3 p3) {
    p3 = fract(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return fract((p3.x + p3.y) * p3.z);
}
float arcNoise2(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    float a = arcHash13(vec3(i, 0.0));
    float b = arcHash13(vec3(i + vec2(1.0, 0.0), 0.0));
    float c = arcHash13(vec3(i + vec2(0.0, 1.0), 0.0));
    float d = arcHash13(vec3(i + vec2(1.0, 1.0), 0.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}
float arcNoise3(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    vec3 u = f * f * (3.0 - 2.0 * f);
    float n000 = arcHash13(i);
    float n100 = arcHash13(i + vec3(1.0, 0.0, 0.0));
    float n010 = arcHash13(i + vec3(0.0, 1.0, 0.0));
    float n110 = arcHash13(i + vec3(1.0, 1.0, 0.0));
    float n001 = arcHash13(i + vec3(0.0, 0.0, 1.0));
    float n101 = arcHash13(i + vec3(1.0, 0.0, 1.0));
    float n011 = arcHash13(i + vec3(0.0, 1.0, 1.0));
    float n111 = arcHash13(i + vec3(1.0, 1.0, 1.0));
    return mix(mix(mix(n000, n100, u.x), mix(n010, n110, u.x), u.y), mix(mix(n001, n101, u.x), mix(n011, n111, u.x), u.y), u.z);
}
float arcFbm3(vec3 p) {
    float sum = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 5; i++) {
        sum += amp * arcNoise3(p);
        p = p * 2.02 + vec3(13.1, 7.7, 3.3);
        amp *= 0.5;
    }
    return sum / 0.96875;
}

""";

    /// <summary>Tangent-space normal to world space. With ARC_HAS_TANGENT the interpolated tangent frame is used;
    /// otherwise the frame is derived from screen-space derivatives.</summary>
    public const string NormalMap = """
void arcTangentFrame(vec3 N, vec3 worldPos, vec2 uv, out vec3 T, out vec3 B) {
#ifdef ARC_HAS_TANGENT
    T = normalize(v_tangent.xyz - N * dot(N, v_tangent.xyz));
    B = cross(N, T) * v_tangent.w;
    if (!gl_FrontFacing) {
        T = -T;
        B = -B;
    }
#else
    vec3 q0 = dFdx(worldPos);
    vec3 q1 = dFdy(worldPos);
    vec2 st0 = dFdx(uv);
    vec2 st1 = dFdy(uv);
    vec3 q1perp = cross(q1, N);
    vec3 q0perp = cross(N, q0);
    T = q1perp * st0.x + q0perp * st1.x;
    B = q1perp * st0.y + q0perp * st1.y;
    float det = max(dot(T, T), dot(B, B));
    float s = det == 0.0 ? 0.0 : inversesqrt(det);
    T *= s;
    B *= s;
#endif
}
vec3 arcPerturbNormal(vec3 N, vec3 worldPos, vec2 uv, vec3 mapN, vec2 scale) {
    mapN = mapN * 2.0 - 1.0;
    mapN.xy *= scale;
    vec3 T;
    vec3 B;
    arcTangentFrame(N, worldPos, uv, T, B);
    return normalize(T * mapN.x + B * mapN.y + N * mapN.z);
}
// A direction in tangent space to world space; its length (the anisotropy strength) is kept.
vec3 arcAnisotropyDirection(vec3 N, vec3 worldPos, vec2 uv, vec2 direction) {
    vec3 T;
    vec3 B;
    arcTangentFrame(N, worldPos, uv, T, B);
    float lt = dot(T, T);
    float lb = dot(B, B);
    T = lt > 0.0 ? T * inversesqrt(lt) : vec3(0.0);
    B = lb > 0.0 ? B * inversesqrt(lb) : vec3(0.0);
    return T * direction.x + B * direction.y;
}

""";

    public const string FragmentCommon = """
#define PI 3.141592653589793
#define RECIPROCAL_PI 0.3183098861837907

float arcPow2(float x) { return x * x; }

float arcDistanceAttenuation(float d, float cutoff, float decay) {
    float f = 1.0 / max(pow(d, decay), 0.01);
    if (cutoff > 0.0) {
        f *= arcPow2(clamp(1.0 - arcPow2(arcPow2(d / cutoff)), 0.0, 1.0));
    }
    return f;
}

vec3 arcFog(vec3 color, float depth) {
    float mode = u_fogColor.w;
    float f = 0.0;
    if (mode > 1.5) {
        f = 1.0 - exp(-u_fogParams.z * u_fogParams.z * depth * depth);
    } else if (mode > 0.5) {
        f = smoothstep(u_fogParams.x, u_fogParams.y, depth);
    }
    return mix(color, u_fogColor.rgb, f);
}

""";

    public const string Shadows = """
uniform sampler2DShadow u_shadowMap;

float arcShadowTap(vec2 uv, float z, vec4 rect) {
    uv = clamp(uv, rect.xy, rect.zw);
    return texture(u_shadowMap, vec3(uv, z));
}
float arcShadow(int index, vec3 worldPos, vec3 N) {
    vec4 p = u_shadowParams[index];
    vec4 sc = u_shadowMatrices[index] * vec4(worldPos + N * p.y, 1.0);
    if (sc.w <= 0.0) {
        return 1.0;
    }
    vec3 c = sc.xyz / sc.w;
    vec4 rect = u_shadowRects[index];
    if (c.z >= 1.0 || c.z <= 0.0 || c.x < rect.x || c.x > rect.z || c.y < rect.y || c.y > rect.w) {
        return 1.0;
    }
    float z = c.z + p.x;
    vec2 t = u_shadowAtlas.xy;
    rect.xy += t * 0.5;
    rect.zw -= t * 0.5;
    vec2 r = t * p.z;
    // 3x3 taps, each a bilinear 2x2 comparison done by the hardware.
    float s = arcShadowTap(c.xy, z, rect) * 0.25;
    s += (arcShadowTap(c.xy + vec2(-r.x, 0.0), z, rect) + arcShadowTap(c.xy + vec2(r.x, 0.0), z, rect)
        + arcShadowTap(c.xy + vec2(0.0, -r.y), z, rect) + arcShadowTap(c.xy + vec2(0.0, r.y), z, rect)) * 0.125;
    s += (arcShadowTap(c.xy + vec2(-r.x, -r.y), z, rect) + arcShadowTap(c.xy + vec2(r.x, -r.y), z, rect)
        + arcShadowTap(c.xy + vec2(-r.x, r.y), z, rect) + arcShadowTap(c.xy + vec2(r.x, r.y), z, rect)) * 0.0625;
    return mix(1.0, s, p.w);
}
int arcCubeFace(vec3 d) {
    vec3 a = abs(d);
    if (a.x >= a.y && a.x >= a.z) {
        return d.x > 0.0 ? 0 : 1;
    }
    if (a.y >= a.z) {
        return d.y > 0.0 ? 2 : 3;
    }
    return d.z > 0.0 ? 4 : 5;
}

""";

    public const string Environment = """
uniform samplerCube u_envMap;

vec3 arcEnvRotate(vec3 d) {
    float s = u_envParams.z;
    float c = u_envParams.w;
    return vec3(c * d.x + s * d.z, d.y, c * d.z - s * d.x);
}
vec3 arcEnvSpecular(vec3 R, float roughness) {
    return textureLod(u_envMap, arcEnvRotate(R), roughness * u_envParams.y).rgb * u_envParams.x;
}
vec3 arcEnvDiffuse(vec3 n) {
    n = arcEnvRotate(n);
    vec3 r = u_sh[0].rgb + u_sh[1].rgb * n.y + u_sh[2].rgb * n.z + u_sh[3].rgb * n.x
        + u_sh[4].rgb * (n.x * n.y) + u_sh[5].rgb * (n.y * n.z) + u_sh[6].rgb * (3.0 * n.z * n.z - 1.0)
        + u_sh[7].rgb * (n.x * n.z) + u_sh[8].rgb * (n.x * n.x - n.y * n.y);
    return max(r, vec3(0.0)) * u_envParams.x;
}

""";

    public const string Brdf = """
vec3 arcFresnelSchlick(vec3 f0, float f90, float VoH) {
    float f = pow(1.0 - VoH, 5.0);
    return f0 * (1.0 - f) + vec3(f90 * f);
}
float arcVisibilitySmith(float a, float NoL, float NoV) {
    float a2 = a * a;
    float gv = NoL * sqrt(a2 + (1.0 - a2) * NoV * NoV);
    float gl = NoV * sqrt(a2 + (1.0 - a2) * NoL * NoL);
    return 0.5 / max(gv + gl, 1e-6);
}
float arcDistributionGgx(float a, float NoH) {
    float a2 = a * a;
    float d = NoH * NoH * (a2 - 1.0) + 1.0;
    return RECIPROCAL_PI * a2 / max(d * d, 1e-7);
}
vec3 arcSpecularGgx(vec3 L, vec3 V, vec3 N, vec3 f0, float f90, float roughness) {
    float a = roughness * roughness;
    vec3 H = normalize(L + V);
    float NoL = clamp(dot(N, L), 0.0, 1.0);
    float NoV = clamp(dot(N, V), 0.0, 1.0);
    float NoH = clamp(dot(N, H), 0.0, 1.0);
    float VoH = clamp(dot(V, H), 0.0, 1.0);
    return arcFresnelSchlick(f0, f90, VoH) * (arcVisibilitySmith(a, NoL, NoV) * arcDistributionGgx(a, NoH));
}
vec2 arcDfg(float NoV, float roughness) {
    const vec4 c0 = vec4(-1.0, -0.0275, -0.572, 0.022);
    const vec4 c1 = vec4(1.0, 0.0425, 1.04, -0.04);
    vec4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    return vec2(-1.04, 1.04) * a004 + r.zw;
}
// Charlie sheen distribution and Neubelt visibility.
float arcSheenD(float roughness, float NoH) {
    float a = max(roughness * roughness, 0.0001);
    float invA = 1.0 / a;
    float sin2 = max(1.0 - NoH * NoH, 0.0078125);
    return (2.0 + invA) * pow(sin2, invA * 0.5) / (2.0 * PI);
}
vec3 arcSheen(vec3 L, vec3 V, vec3 N, vec3 sheenColor, float roughness) {
    vec3 H = normalize(L + V);
    float NoL = clamp(dot(N, L), 0.0, 1.0);
    float NoV = clamp(dot(N, V), 0.0, 1.0);
    float NoH = clamp(dot(N, H), 0.0, 1.0);
    float vis = 1.0 / (4.0 * (NoL + NoV - NoL * NoV) + 1e-5);
    return sheenColor * (arcSheenD(roughness, NoH) * vis);
}
float arcSheenEnv(float NoV, float roughness) {
    float r2 = roughness * roughness;
    float a = roughness < 0.25 ? -339.2 * r2 + 161.4 * roughness - 25.9 : -8.48 * r2 + 14.3 * roughness - 9.95;
    float b = roughness < 0.25 ? 44.0 * r2 - 23.7 * roughness + 3.26 : 1.97 * r2 - 3.27 * roughness + 0.72;
    float dg = exp(a * NoV + b) + (roughness < 0.25 ? 0.0 : 0.1 * (roughness - 0.25));
    return clamp(dg * RECIPROCAL_PI, 0.0, 1.0);
}

""";

    /// <summary>The layers only <c>PhysicalMaterial</c> has and few materials use: thin-film iridescence (Belcour and
    /// Barla 2017, as in the glTF extension) and anisotropic GGX.</summary>
    public const string PhysicalLayers = """
vec3 arcSchlickToF0(vec3 f, float f90, float VoH) {
    float x = clamp(1.0 - VoH, 0.0, 1.0);
    float x2 = x * x;
    float x5 = clamp(x * x2 * x2, 0.0, 0.9999);
    return (f - vec3(f90) * x5) / (1.0 - x5);
}
vec3 arcIridSensitivity(float opd, vec3 shift) {
    // The XYZ color matching functions as Gaussians in Fourier space; opd in nanometers.
    float phase = 2.0 * PI * opd * 1.0e-9;
    vec3 val = vec3(5.4856e-13, 4.4201e-13, 5.2481e-13);
    vec3 pos = vec3(1.6810e+06, 1.7953e+06, 2.2084e+06);
    vec3 var = vec3(4.3278e+09, 9.3046e+09, 6.6121e+09);
    vec3 xyz = val * sqrt(2.0 * PI * var) * cos(pos * phase + shift) * exp(-phase * phase * var);
    xyz.x += 9.7470e-14 * sqrt(2.0 * PI * 4.5282e+09) * cos(2.2399e+06 * phase + shift.x) * exp(-4.5282e+09 * phase * phase);
    xyz /= 1.0685e-7;
    return mat3(3.2404542, -0.9692660, 0.0556434, -1.5371385, 1.8760108, -0.2040259, -0.4985314, 0.0415560, 1.0572252) * xyz;
}
// Fresnel reflectance of a base with reflectance baseF0 under a thin film of the given index and thickness (nm).
vec3 arcIridescence(float outsideIor, float filmIorIn, float cosTheta1, float thickness, vec3 baseF0) {
    float filmIor = mix(outsideIor, filmIorIn, smoothstep(0.0, 0.03, thickness));
    float sinTheta2Sq = arcPow2(outsideIor / filmIor) * (1.0 - cosTheta1 * cosTheta1);
    float cosTheta2Sq = 1.0 - sinTheta2Sq;
    if (cosTheta2Sq < 0.0) {
        return vec3(1.0);
    }
    float cosTheta2 = sqrt(cosTheta2Sq);
    float r0 = arcPow2((filmIor - outsideIor) / (filmIor + outsideIor));
    float r12 = r0 + (1.0 - r0) * pow(1.0 - cosTheta1, 5.0);
    float t121 = 1.0 - r12;
    float phi12 = filmIor < outsideIor ? PI : 0.0;
    float phi21 = PI - phi12;
    vec3 sqrtF0 = sqrt(clamp(baseF0, 0.0, 0.9999));
    vec3 baseIor = (vec3(1.0) + sqrtF0) / (vec3(1.0) - sqrtF0);
    vec3 r1 = (baseIor - vec3(filmIor)) / (baseIor + vec3(filmIor));
    r1 *= r1;
    vec3 r23 = r1 + (vec3(1.0) - r1) * pow(1.0 - cosTheta2, 5.0);
    vec3 phi23 = vec3(baseIor.x < filmIor ? PI : 0.0, baseIor.y < filmIor ? PI : 0.0, baseIor.z < filmIor ? PI : 0.0);
    float opd = 2.0 * filmIor * thickness * cosTheta2;
    vec3 phi = vec3(phi21) + phi23;
    vec3 r123 = clamp(r12 * r23, 1e-5, 0.9999);
    vec3 sr123 = sqrt(r123);
    vec3 rs = t121 * t121 * r23 / (vec3(1.0) - r123);
    vec3 result = r12 + rs;
    vec3 cm = rs - t121;
    for (int m = 1; m <= 2; m++) {
        cm *= sr123;
        result += cm * 2.0 * arcIridSensitivity(float(m) * opd, float(m) * phi);
    }
    return max(result, vec3(0.0));
}
float arcVisibilityAniso(float alphaT, float alphaB, float ToV, float BoV, float ToL, float BoL, float NoV, float NoL) {
    float gv = NoL * length(vec3(alphaT * ToV, alphaB * BoV, NoV));
    float gl = NoV * length(vec3(alphaT * ToL, alphaB * BoL, NoL));
    return clamp(0.5 / max(gv + gl, 1e-5), 0.0, 1.0);
}
float arcDistributionAniso(float alphaT, float alphaB, float NoH, float ToH, float BoH) {
    float a2 = alphaT * alphaB;
    highp vec3 v = vec3(alphaB * ToH, alphaT * BoH, a2 * NoH);
    highp float v2 = max(dot(v, v), 1e-9);
    float w2 = a2 / v2;
    return RECIPROCAL_PI * a2 * w2 * w2;
}

""";

    public const string ToneMapping = """
vec3 arcToneReinhard(vec3 c) {
    return c / (vec3(1.0) + c);
}
vec3 arcToneCineon(vec3 c) {
    c = max(vec3(0.0), c - 0.004);
    return pow((c * (6.2 * c + 0.5)) / (c * (6.2 * c + 1.7) + 0.06), vec3(2.2));
}
vec3 arcRrtOdtFit(vec3 v) {
    vec3 a = v * (v + 0.0245786) - 0.000090537;
    vec3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
    return a / b;
}
vec3 arcToneAces(vec3 c) {
    const mat3 inputMat = mat3(0.59719, 0.07600, 0.02840, 0.35458, 0.90834, 0.13383, 0.04823, 0.01566, 0.83777);
    const mat3 outputMat = mat3(1.60475, -0.10208, -0.00327, -0.53108, 1.10813, -0.07276, -0.07367, -0.00605, 1.07602);
    c = inputMat * (c / 0.6);
    c = arcRrtOdtFit(c);
    return clamp(outputMat * c, 0.0, 1.0);
}
vec3 arcAgxContrast(vec3 x) {
    vec3 x2 = x * x;
    vec3 x4 = x2 * x2;
    return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
}
vec3 arcToneAgx(vec3 c) {
    const mat3 inMat = mat3(0.856627153315983, 0.137318972929847, 0.11189821299995, 0.0951212405381588, 0.761241990602591, 0.0767994186031903, 0.0482516061458583, 0.101439036467562, 0.811302368396859);
    const mat3 outMat = mat3(1.1271005818144368, -0.1413297634984383, -0.14132976349843826, -0.11060664309660323, 1.157823702216272, -0.11060664309660294, -0.016493938717834573, -0.016493938717834257, 1.2519364065950405);
    const mat3 rec2020 = mat3(0.6274, 0.0691, 0.0164, 0.3293, 0.9195, 0.0880, 0.0433, 0.0113, 0.8956);
    const mat3 rec709 = mat3(1.6605, -0.1246, -0.0182, -0.5876, 1.1329, -0.1006, -0.0728, -0.0083, 1.1187);
    const float minEv = -12.47393;
    const float maxEv = 4.026069;
    c = rec2020 * c;
    c = inMat * c;
    c = max(c, 1e-10);
    c = clamp((log2(c) - minEv) / (maxEv - minEv), 0.0, 1.0);
    c = arcAgxContrast(c);
    c = outMat * c;
    c = pow(max(vec3(0.0), c), vec3(2.2));
    c = rec709 * c;
    return clamp(c, 0.0, 1.0);
}
vec3 arcToneNeutral(vec3 c) {
    const float startCompression = 0.8 - 0.04;
    const float desaturation = 0.15;
    float x = min(c.r, min(c.g, c.b));
    float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
    c -= offset;
    float peak = max(c.r, max(c.g, c.b));
    if (peak < startCompression) {
        return c;
    }
    float d = 1.0 - startCompression;
    float newPeak = 1.0 - d * d / (peak + d - startCompression);
    c *= newPeak / peak;
    float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
    return mix(c, vec3(newPeak), g);
}
vec3 arcLinearToSrgb(vec3 c) {
    c = max(c, vec3(0.0));
    return mix(c * 12.92, 1.055 * pow(c, vec3(0.41666)) - 0.055, step(vec3(0.0031308), c));
}

""";

    /// <summary>The name of the tone mapping function for a mode, or null for none.</summary>
    public static string? ToneMappingFunction(ToneMapping mode) => mode switch
    {
        Graphics3D.ToneMapping.Linear => "",
        Graphics3D.ToneMapping.Reinhard => "arcToneReinhard",
        Graphics3D.ToneMapping.Cineon => "arcToneCineon",
        Graphics3D.ToneMapping.AcesFilmic => "arcToneAces",
        Graphics3D.ToneMapping.AgX => "arcToneAgx",
        Graphics3D.ToneMapping.Neutral => "arcToneNeutral",
        _ => null,
    };

    public static string Header(Rhi.ShaderLanguage language) => language switch
    {
        Rhi.ShaderLanguage.GlslEs300 =>
            "#version 300 es\nprecision highp float;\nprecision highp int;\nprecision highp sampler2D;\nprecision highp samplerCube;\nprecision highp sampler2DShadow;\n",

        // The vertex and instance index have other names there, and are unsigned in the front end wgpu uses (naga).
        // The declarations are rewritten by ShaderDialect.
        Rhi.ShaderLanguage.Glsl450 => "#version 450\n#define gl_VertexID int(gl_VertexIndex)\n#define gl_InstanceID int(gl_InstanceIndex)\n",
        _ => "#version 330 core\n",
    };

    /// <summary>Clip-space z of a depth buffer value: OpenGL's clip depth runs -1..1, the others' 0..1.</summary>
    public static string ClipDepthOf(Rhi.ClipDepthRange range, string depth) =>
        range == Rhi.ClipDepthRange.NegativeOneToOne ? depth + " * 2.0 - 1.0" : depth;
}
