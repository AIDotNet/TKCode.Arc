using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace TKCode.Arc.Graphics3D.Rhi.OpenGL;

/// <summary>The OpenGL entry points the backend uses, loaded as function pointers from a host-supplied lookup
/// (no reflection, no marshalling stubs). The set is the common subset of OpenGL ES 3.0 and OpenGL 4.1 core.</summary>
internal sealed unsafe class GlApi
{
    public readonly delegate* unmanaged[Stdcall]<int> GetError;
    public readonly delegate* unmanaged[Stdcall]<int, nint> GetString;
    public readonly delegate* unmanaged[Stdcall]<int, int, nint> GetStringi;
    public readonly delegate* unmanaged[Stdcall]<int, int*, void> GetIntegerv;
    public readonly delegate* unmanaged[Stdcall]<int, float*, void> GetFloatv;
    public readonly delegate* unmanaged[Stdcall]<int, void> Enable;
    public readonly delegate* unmanaged[Stdcall]<int, void> Disable;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, void> Viewport;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, void> Scissor;
    public readonly delegate* unmanaged[Stdcall]<float, float, float, float, void> ClearColor;
    public readonly delegate* unmanaged[Stdcall]<float, void> ClearDepthf;
    public readonly delegate* unmanaged[Stdcall]<double, void> ClearDepth;
    public readonly delegate* unmanaged[Stdcall]<int, void> ClearStencil;
    public readonly delegate* unmanaged[Stdcall]<int, void> Clear;
    public readonly delegate* unmanaged[Stdcall]<byte, byte, byte, byte, void> ColorMask;
    public readonly delegate* unmanaged[Stdcall]<byte, void> DepthMask;
    public readonly delegate* unmanaged[Stdcall]<int, void> DepthFunc;
    public readonly delegate* unmanaged[Stdcall]<int, void> CullFace;
    public readonly delegate* unmanaged[Stdcall]<int, void> FrontFace;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, void> BlendFuncSeparate;
    public readonly delegate* unmanaged[Stdcall]<int, void> BlendEquation;
    public readonly delegate* unmanaged[Stdcall]<float, float, void> PolygonOffset;
    public readonly delegate* unmanaged[Stdcall]<int, int, void> PixelStorei;
    public readonly delegate* unmanaged[Stdcall]<void> Flush;
    public readonly delegate* unmanaged[Stdcall]<void> Finish;

    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> GenBuffers;
    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> DeleteBuffers;
    public readonly delegate* unmanaged[Stdcall]<int, uint, void> BindBuffer;
    public readonly delegate* unmanaged[Stdcall]<int, nint, void*, int, void> BufferData;
    public readonly delegate* unmanaged[Stdcall]<int, nint, nint, void*, void> BufferSubData;
    public readonly delegate* unmanaged[Stdcall]<int, uint, uint, nint, nint, void> BindBufferRange;

    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> GenVertexArrays;
    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> DeleteVertexArrays;
    public readonly delegate* unmanaged[Stdcall]<uint, void> BindVertexArray;
    public readonly delegate* unmanaged[Stdcall]<uint, void> EnableVertexAttribArray;
    public readonly delegate* unmanaged[Stdcall]<uint, int, int, byte, int, nint, void> VertexAttribPointer;
    public readonly delegate* unmanaged[Stdcall]<uint, int, int, int, nint, void> VertexAttribIPointer;
    public readonly delegate* unmanaged[Stdcall]<uint, uint, void> VertexAttribDivisor;

    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> GenTextures;
    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> DeleteTextures;
    public readonly delegate* unmanaged[Stdcall]<int, uint, void> BindTexture;
    public readonly delegate* unmanaged[Stdcall]<int, void> ActiveTexture;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, void*, void> TexImage2D;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, void*, void> TexSubImage2D;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, int, void*, void> TexImage3D;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, int, int, void*, void> TexSubImage3D;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, void*, void> CompressedTexImage2D;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, void> TexParameteri;
    public readonly delegate* unmanaged[Stdcall]<int, void> GenerateMipmap;

    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> GenSamplers;
    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> DeleteSamplers;
    public readonly delegate* unmanaged[Stdcall]<uint, uint, void> BindSampler;
    public readonly delegate* unmanaged[Stdcall]<uint, int, int, void> SamplerParameteri;
    public readonly delegate* unmanaged[Stdcall]<uint, int, float, void> SamplerParameterf;

    public readonly delegate* unmanaged[Stdcall]<int, uint> CreateShader;
    public readonly delegate* unmanaged[Stdcall]<uint, int, byte**, int*, void> ShaderSource;
    public readonly delegate* unmanaged[Stdcall]<uint, void> CompileShader;
    public readonly delegate* unmanaged[Stdcall]<uint, int, int*, void> GetShaderiv;
    public readonly delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void> GetShaderInfoLog;
    public readonly delegate* unmanaged[Stdcall]<uint, void> DeleteShader;
    public readonly delegate* unmanaged[Stdcall]<uint> CreateProgram;
    public readonly delegate* unmanaged[Stdcall]<uint, uint, void> AttachShader;
    public readonly delegate* unmanaged[Stdcall]<uint, void> LinkProgram;
    public readonly delegate* unmanaged[Stdcall]<uint, int, int*, void> GetProgramiv;
    public readonly delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void> GetProgramInfoLog;
    public readonly delegate* unmanaged[Stdcall]<uint, void> DeleteProgram;
    public readonly delegate* unmanaged[Stdcall]<uint, void> UseProgram;
    public readonly delegate* unmanaged[Stdcall]<uint, byte*, int> GetUniformLocation;
    public readonly delegate* unmanaged[Stdcall]<int, int, void> Uniform1i;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, void> Uniform4i;
    public readonly delegate* unmanaged[Stdcall]<uint, byte*, uint> GetUniformBlockIndex;
    public readonly delegate* unmanaged[Stdcall]<uint, uint, uint, void> UniformBlockBinding;

    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> GenFramebuffers;
    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> DeleteFramebuffers;
    public readonly delegate* unmanaged[Stdcall]<int, uint, void> BindFramebuffer;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, uint, int, void> FramebufferTexture2D;
    public readonly delegate* unmanaged[Stdcall]<int, int, uint, int, int, void> FramebufferTextureLayer;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, uint, void> FramebufferRenderbuffer;
    public readonly delegate* unmanaged[Stdcall]<int, int> CheckFramebufferStatus;
    public readonly delegate* unmanaged[Stdcall]<int, int*, void> DrawBuffers;
    public readonly delegate* unmanaged[Stdcall]<int, void> ReadBuffer;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, int, int, void> BlitFramebuffer;
    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> GenRenderbuffers;
    public readonly delegate* unmanaged[Stdcall]<int, uint*, void> DeleteRenderbuffers;
    public readonly delegate* unmanaged[Stdcall]<int, uint, void> BindRenderbuffer;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, void> RenderbufferStorageMultisample;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, void*, void> ReadPixels;

    public readonly delegate* unmanaged[Stdcall]<int, int, int, void> DrawArrays;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, nint, void> DrawElements;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, int, void> DrawArraysInstanced;
    public readonly delegate* unmanaged[Stdcall]<int, int, int, nint, int, void> DrawElementsInstanced;

    // Keeps the delegates of GlSignatures, one per signature shape below, in a trimmed application: the WebAssembly build
    // reads the shapes it must be able to call from their Invoke methods (see there). Each is named, not the nested types
    // as a whole: that would keep Delegate's reflection members too, and the trimmer warns about those.
    [DynamicDependency(nameof(GlSignatures.I.Invoke), typeof(GlSignatures.I))]
    [DynamicDependency(nameof(GlSignatures.II.Invoke), typeof(GlSignatures.II))]
    [DynamicDependency(nameof(GlSignatures.III.Invoke), typeof(GlSignatures.III))]
    [DynamicDependency(nameof(GlSignatures.V.Invoke), typeof(GlSignatures.V))]
    [DynamicDependency(nameof(GlSignatures.VI.Invoke), typeof(GlSignatures.VI))]
    [DynamicDependency(nameof(GlSignatures.VII.Invoke), typeof(GlSignatures.VII))]
    [DynamicDependency(nameof(GlSignatures.VIII.Invoke), typeof(GlSignatures.VIII))]
    [DynamicDependency(nameof(GlSignatures.VIIII.Invoke), typeof(GlSignatures.VIIII))]
    [DynamicDependency(nameof(GlSignatures.VIIIII.Invoke), typeof(GlSignatures.VIIIII))]
    [DynamicDependency(nameof(GlSignatures.VIIIIII.Invoke), typeof(GlSignatures.VIIIIII))]
    [DynamicDependency(nameof(GlSignatures.VIIIIIII.Invoke), typeof(GlSignatures.VIIIIIII))]
    [DynamicDependency(nameof(GlSignatures.VIIIIIIII.Invoke), typeof(GlSignatures.VIIIIIIII))]
    [DynamicDependency(nameof(GlSignatures.VIIIIIIIII.Invoke), typeof(GlSignatures.VIIIIIIIII))]
    [DynamicDependency(nameof(GlSignatures.VIIIIIIIIII.Invoke), typeof(GlSignatures.VIIIIIIIIII))]
    [DynamicDependency(nameof(GlSignatures.VIIIIIIIIIII.Invoke), typeof(GlSignatures.VIIIIIIIIIII))]
    [DynamicDependency(nameof(GlSignatures.VF.Invoke), typeof(GlSignatures.VF))]
    [DynamicDependency(nameof(GlSignatures.VD.Invoke), typeof(GlSignatures.VD))]
    [DynamicDependency(nameof(GlSignatures.VFF.Invoke), typeof(GlSignatures.VFF))]
    [DynamicDependency(nameof(GlSignatures.VFFFF.Invoke), typeof(GlSignatures.VFFFF))]
    [DynamicDependency(nameof(GlSignatures.VIIF.Invoke), typeof(GlSignatures.VIIF))]
    public GlApi(Func<string, nint> getProcAddress)
    {
        nint L(string name)
        {
            var p = getProcAddress(name);
            if (p == 0)
            {
                throw new NotSupportedException($"The OpenGL context does not provide {name}. OpenGL ES 3.0 or OpenGL 3.3 is required.");
            }

            return p;
        }

        GetError = (delegate* unmanaged[Stdcall]<int>)L("glGetError");
        GetString = (delegate* unmanaged[Stdcall]<int, nint>)L("glGetString");
        GetStringi = (delegate* unmanaged[Stdcall]<int, int, nint>)L("glGetStringi");
        GetIntegerv = (delegate* unmanaged[Stdcall]<int, int*, void>)L("glGetIntegerv");
        GetFloatv = (delegate* unmanaged[Stdcall]<int, float*, void>)L("glGetFloatv");
        Enable = (delegate* unmanaged[Stdcall]<int, void>)L("glEnable");
        Disable = (delegate* unmanaged[Stdcall]<int, void>)L("glDisable");
        Viewport = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)L("glViewport");
        Scissor = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)L("glScissor");
        ClearColor = (delegate* unmanaged[Stdcall]<float, float, float, float, void>)L("glClearColor");
        // glClearDepthf belongs to OpenGL ES and to desktop OpenGL from 4.1; an older desktop context has glClearDepth,
        // which takes a double.
        var clearDepthf = getProcAddress("glClearDepthf");
        ClearDepthf = (delegate* unmanaged[Stdcall]<float, void>)clearDepthf;
        ClearDepth = clearDepthf == 0 ? (delegate* unmanaged[Stdcall]<double, void>)L("glClearDepth") : null;
        ClearStencil = (delegate* unmanaged[Stdcall]<int, void>)L("glClearStencil");
        Clear = (delegate* unmanaged[Stdcall]<int, void>)L("glClear");
        ColorMask = (delegate* unmanaged[Stdcall]<byte, byte, byte, byte, void>)L("glColorMask");
        DepthMask = (delegate* unmanaged[Stdcall]<byte, void>)L("glDepthMask");
        DepthFunc = (delegate* unmanaged[Stdcall]<int, void>)L("glDepthFunc");
        CullFace = (delegate* unmanaged[Stdcall]<int, void>)L("glCullFace");
        FrontFace = (delegate* unmanaged[Stdcall]<int, void>)L("glFrontFace");
        BlendFuncSeparate = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)L("glBlendFuncSeparate");
        BlendEquation = (delegate* unmanaged[Stdcall]<int, void>)L("glBlendEquation");
        PolygonOffset = (delegate* unmanaged[Stdcall]<float, float, void>)L("glPolygonOffset");
        PixelStorei = (delegate* unmanaged[Stdcall]<int, int, void>)L("glPixelStorei");
        Flush = (delegate* unmanaged[Stdcall]<void>)L("glFlush");
        Finish = (delegate* unmanaged[Stdcall]<void>)L("glFinish");

        GenBuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenBuffers");
        DeleteBuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteBuffers");
        BindBuffer = (delegate* unmanaged[Stdcall]<int, uint, void>)L("glBindBuffer");
        BufferData = (delegate* unmanaged[Stdcall]<int, nint, void*, int, void>)L("glBufferData");
        BufferSubData = (delegate* unmanaged[Stdcall]<int, nint, nint, void*, void>)L("glBufferSubData");
        BindBufferRange = (delegate* unmanaged[Stdcall]<int, uint, uint, nint, nint, void>)L("glBindBufferRange");

        GenVertexArrays = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenVertexArrays");
        DeleteVertexArrays = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteVertexArrays");
        BindVertexArray = (delegate* unmanaged[Stdcall]<uint, void>)L("glBindVertexArray");
        EnableVertexAttribArray = (delegate* unmanaged[Stdcall]<uint, void>)L("glEnableVertexAttribArray");
        VertexAttribPointer = (delegate* unmanaged[Stdcall]<uint, int, int, byte, int, nint, void>)L("glVertexAttribPointer");
        VertexAttribIPointer = (delegate* unmanaged[Stdcall]<uint, int, int, int, nint, void>)L("glVertexAttribIPointer");
        VertexAttribDivisor = (delegate* unmanaged[Stdcall]<uint, uint, void>)L("glVertexAttribDivisor");

        GenTextures = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenTextures");
        DeleteTextures = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteTextures");
        BindTexture = (delegate* unmanaged[Stdcall]<int, uint, void>)L("glBindTexture");
        ActiveTexture = (delegate* unmanaged[Stdcall]<int, void>)L("glActiveTexture");
        TexImage2D = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, void*, void>)L("glTexImage2D");
        TexSubImage2D = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, void*, void>)L("glTexSubImage2D");
        TexImage3D = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, int, void*, void>)L("glTexImage3D");
        TexSubImage3D = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, int, int, void*, void>)L("glTexSubImage3D");
        CompressedTexImage2D = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, void*, void>)L("glCompressedTexImage2D");
        TexParameteri = (delegate* unmanaged[Stdcall]<int, int, int, void>)L("glTexParameteri");
        GenerateMipmap = (delegate* unmanaged[Stdcall]<int, void>)L("glGenerateMipmap");

        GenSamplers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenSamplers");
        DeleteSamplers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteSamplers");
        BindSampler = (delegate* unmanaged[Stdcall]<uint, uint, void>)L("glBindSampler");
        SamplerParameteri = (delegate* unmanaged[Stdcall]<uint, int, int, void>)L("glSamplerParameteri");
        SamplerParameterf = (delegate* unmanaged[Stdcall]<uint, int, float, void>)L("glSamplerParameterf");

        CreateShader = (delegate* unmanaged[Stdcall]<int, uint>)L("glCreateShader");
        ShaderSource = (delegate* unmanaged[Stdcall]<uint, int, byte**, int*, void>)L("glShaderSource");
        CompileShader = (delegate* unmanaged[Stdcall]<uint, void>)L("glCompileShader");
        GetShaderiv = (delegate* unmanaged[Stdcall]<uint, int, int*, void>)L("glGetShaderiv");
        GetShaderInfoLog = (delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void>)L("glGetShaderInfoLog");
        DeleteShader = (delegate* unmanaged[Stdcall]<uint, void>)L("glDeleteShader");
        CreateProgram = (delegate* unmanaged[Stdcall]<uint>)L("glCreateProgram");
        AttachShader = (delegate* unmanaged[Stdcall]<uint, uint, void>)L("glAttachShader");
        LinkProgram = (delegate* unmanaged[Stdcall]<uint, void>)L("glLinkProgram");
        GetProgramiv = (delegate* unmanaged[Stdcall]<uint, int, int*, void>)L("glGetProgramiv");
        GetProgramInfoLog = (delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void>)L("glGetProgramInfoLog");
        DeleteProgram = (delegate* unmanaged[Stdcall]<uint, void>)L("glDeleteProgram");
        UseProgram = (delegate* unmanaged[Stdcall]<uint, void>)L("glUseProgram");
        GetUniformLocation = (delegate* unmanaged[Stdcall]<uint, byte*, int>)L("glGetUniformLocation");
        Uniform1i = (delegate* unmanaged[Stdcall]<int, int, void>)L("glUniform1i");
        Uniform4i = (delegate* unmanaged[Stdcall]<int, int, int, int, int, void>)L("glUniform4i");
        GetUniformBlockIndex = (delegate* unmanaged[Stdcall]<uint, byte*, uint>)L("glGetUniformBlockIndex");
        UniformBlockBinding = (delegate* unmanaged[Stdcall]<uint, uint, uint, void>)L("glUniformBlockBinding");

        GenFramebuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenFramebuffers");
        DeleteFramebuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteFramebuffers");
        BindFramebuffer = (delegate* unmanaged[Stdcall]<int, uint, void>)L("glBindFramebuffer");
        FramebufferTexture2D = (delegate* unmanaged[Stdcall]<int, int, int, uint, int, void>)L("glFramebufferTexture2D");
        FramebufferTextureLayer = (delegate* unmanaged[Stdcall]<int, int, uint, int, int, void>)L("glFramebufferTextureLayer");
        FramebufferRenderbuffer = (delegate* unmanaged[Stdcall]<int, int, int, uint, void>)L("glFramebufferRenderbuffer");
        CheckFramebufferStatus = (delegate* unmanaged[Stdcall]<int, int>)L("glCheckFramebufferStatus");
        DrawBuffers = (delegate* unmanaged[Stdcall]<int, int*, void>)L("glDrawBuffers");
        ReadBuffer = (delegate* unmanaged[Stdcall]<int, void>)L("glReadBuffer");
        BlitFramebuffer = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, int, int, void>)L("glBlitFramebuffer");
        GenRenderbuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenRenderbuffers");
        DeleteRenderbuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteRenderbuffers");
        BindRenderbuffer = (delegate* unmanaged[Stdcall]<int, uint, void>)L("glBindRenderbuffer");
        RenderbufferStorageMultisample = (delegate* unmanaged[Stdcall]<int, int, int, int, int, void>)L("glRenderbufferStorageMultisample");
        ReadPixels = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, void*, void>)L("glReadPixels");

        DrawArrays = (delegate* unmanaged[Stdcall]<int, int, int, void>)L("glDrawArrays");
        DrawElements = (delegate* unmanaged[Stdcall]<int, int, int, nint, void>)L("glDrawElements");
        DrawArraysInstanced = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)L("glDrawArraysInstanced");
        DrawElementsInstanced = (delegate* unmanaged[Stdcall]<int, int, int, nint, int, void>)L("glDrawElementsInstanced");
    }

    public string GetStringValue(int name) => Marshal.PtrToStringUTF8(GetString(name)) ?? "";

    public int GetInteger(int name)
    {
        int v = 0;
        GetIntegerv(name, &v);
        return v;
    }

    public HashSet<string> GetExtensions()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var n = GetInteger(GlConst.NUM_EXTENSIONS);
        for (var i = 0; i < n; i++)
        {
            var s = Marshal.PtrToStringUTF8(GetStringi(GlConst.EXTENSIONS, i));
            if (s is not null)
            {
                set.Add(s);
            }
        }

        return set;
    }

    public static byte[] Utf8Z(string s)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
        Encoding.UTF8.GetBytes(s, bytes);
        return bytes;
    }
}

internal static class GlConst
{
    public const int NO_ERROR = 0;
    public const int VENDOR = 0x1F00;
    public const int RENDERER = 0x1F01;
    public const int VERSION = 0x1F02;
    public const int EXTENSIONS = 0x1F03;
    public const int NUM_EXTENSIONS = 0x821D;

    public const int DEPTH_BUFFER_BIT = 0x100;
    public const int STENCIL_BUFFER_BIT = 0x400;
    public const int COLOR_BUFFER_BIT = 0x4000;

    public const int POINTS = 0;
    public const int LINES = 1;
    public const int LINE_STRIP = 3;
    public const int TRIANGLES = 4;
    public const int TRIANGLE_STRIP = 5;

    public const int NEVER = 0x200;

    public const int ZERO = 0;
    public const int ONE = 1;
    public const int SRC_COLOR = 0x300;
    public const int ONE_MINUS_SRC_COLOR = 0x301;
    public const int SRC_ALPHA = 0x302;
    public const int ONE_MINUS_SRC_ALPHA = 0x303;
    public const int DST_COLOR = 0x306;
    public const int FUNC_ADD = 0x8006;

    public const int FRONT = 0x404;
    public const int BACK = 0x405;
    public const int CW = 0x900;
    public const int CCW = 0x901;

    public const int CULL_FACE = 0xB44;
    public const int DEPTH_TEST = 0xB71;
    public const int BLEND = 0xBE2;
    public const int SCISSOR_TEST = 0xC11;
    public const int POLYGON_OFFSET_FILL = 0x8037;
    public const int SAMPLE_ALPHA_TO_COVERAGE = 0x809E;
    public const int TEXTURE_CUBE_MAP_SEAMLESS = 0x884F;
    public const int PROGRAM_POINT_SIZE = 0x8642;
    public const int FRAMEBUFFER_SRGB = 0x8DB9;

    public const int UNPACK_ALIGNMENT = 0xCF5;
    public const int PACK_ALIGNMENT = 0xD05;

    public const int MAX_TEXTURE_SIZE = 0xD33;
    public const int MAX_CUBE_MAP_TEXTURE_SIZE = 0x851C;
    public const int MAX_SAMPLES = 0x8D57;
    public const int MAX_TEXTURE_IMAGE_UNITS = 0x8872;
    public const int MAX_UNIFORM_BLOCK_SIZE = 0x8A30;
    public const int UNIFORM_BUFFER_OFFSET_ALIGNMENT = 0x8A34;
    public const int MAX_VERTEX_ATTRIBS = 0x8869;
    public const int MAX_COLOR_ATTACHMENTS = 0x8CDF;
    public const int MAX_TEXTURE_MAX_ANISOTROPY = 0x84FF;
    public const int TEXTURE_MAX_ANISOTROPY = 0x84FE;

    public const int BYTE = 0x1400;
    public const int UNSIGNED_BYTE = 0x1401;
    public const int SHORT = 0x1402;
    public const int UNSIGNED_SHORT = 0x1403;
    public const int UNSIGNED_INT = 0x1405;
    public const int FLOAT = 0x1406;
    public const int HALF_FLOAT = 0x140B;
    public const int UNSIGNED_INT_24_8 = 0x84FA;

    public const int DEPTH_COMPONENT = 0x1902;
    public const int RED = 0x1903;
    public const int RGB = 0x1907;
    public const int RGBA = 0x1908;
    public const int RG = 0x8227;
    public const int DEPTH_STENCIL = 0x84F9;

    public const int R8 = 0x8229;
    public const int RG8 = 0x822B;
    public const int RGBA8 = 0x8058;
    public const int SRGB8_ALPHA8 = 0x8C43;
    public const int R16F = 0x822D;
    public const int RG16F = 0x822F;
    public const int RGBA16F = 0x881A;
    public const int R32F = 0x822E;
    public const int RG32F = 0x8230;
    public const int RGBA32F = 0x8814;
    public const int R11F_G11F_B10F = 0x8C3A;
    public const int DEPTH_COMPONENT16 = 0x81A5;
    public const int DEPTH_COMPONENT24 = 0x81A6;
    public const int DEPTH_COMPONENT32F = 0x8CAC;
    public const int DEPTH24_STENCIL8 = 0x88F0;

    public const int TEXTURE_2D = 0xDE1;
    public const int TEXTURE_3D = 0x806F;
    public const int TEXTURE_CUBE_MAP = 0x8513;
    public const int TEXTURE_CUBE_MAP_POSITIVE_X = 0x8515;
    public const int TEXTURE_2D_ARRAY = 0x8C1A;
    public const int TEXTURE0 = 0x84C0;

    public const int TEXTURE_MAG_FILTER = 0x2800;
    public const int TEXTURE_MIN_FILTER = 0x2801;
    public const int TEXTURE_WRAP_S = 0x2802;
    public const int TEXTURE_WRAP_T = 0x2803;
    public const int TEXTURE_WRAP_R = 0x8072;
    public const int TEXTURE_BASE_LEVEL = 0x813C;
    public const int TEXTURE_MAX_LEVEL = 0x813D;
    public const int TEXTURE_COMPARE_MODE = 0x884C;
    public const int TEXTURE_COMPARE_FUNC = 0x884D;
    public const int COMPARE_REF_TO_TEXTURE = 0x884E;
    public const int NONE = 0;

    public const int NEAREST = 0x2600;
    public const int LINEAR = 0x2601;
    public const int NEAREST_MIPMAP_NEAREST = 0x2700;
    public const int LINEAR_MIPMAP_NEAREST = 0x2701;
    public const int NEAREST_MIPMAP_LINEAR = 0x2702;
    public const int LINEAR_MIPMAP_LINEAR = 0x2703;
    public const int REPEAT = 0x2901;
    public const int CLAMP_TO_EDGE = 0x812F;
    public const int MIRRORED_REPEAT = 0x8370;

    public const int ARRAY_BUFFER = 0x8892;
    public const int ELEMENT_ARRAY_BUFFER = 0x8893;
    public const int UNIFORM_BUFFER = 0x8A11;
    public const int STATIC_DRAW = 0x88E4;
    public const int DYNAMIC_DRAW = 0x88E8;

    public const int FRAGMENT_SHADER = 0x8B30;
    public const int VERTEX_SHADER = 0x8B31;
    public const int COMPILE_STATUS = 0x8B81;
    public const int LINK_STATUS = 0x8B82;
    public const int INFO_LOG_LENGTH = 0x8B84;
    public const uint INVALID_INDEX = 0xFFFFFFFF;

    public const int FRAMEBUFFER = 0x8D40;
    public const int READ_FRAMEBUFFER = 0x8CA8;
    public const int DRAW_FRAMEBUFFER = 0x8CA9;
    public const int RENDERBUFFER = 0x8D41;
    public const int COLOR_ATTACHMENT0 = 0x8CE0;
    public const int DEPTH_ATTACHMENT = 0x8D00;
    public const int DEPTH_STENCIL_ATTACHMENT = 0x821A;
    public const int FRAMEBUFFER_COMPLETE = 0x8CD5;
}
