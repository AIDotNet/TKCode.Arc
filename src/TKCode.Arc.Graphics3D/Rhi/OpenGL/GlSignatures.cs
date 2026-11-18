using System.Runtime.InteropServices;

namespace TKCode.Arc.Graphics3D.Rhi.OpenGL;

/// <summary>
/// One delegate for every shape of native signature that <see cref="GlApi"/> calls through a function pointer. Nothing
/// creates or calls them; they are here for WebAssembly.
/// </summary>
/// <remarks>
/// Under the .NET WebAssembly runtime's interpreter a native function can only be called when the application was built with
/// a trampoline for the shape of its signature. The build makes the trampolines from the P/Invoke declarations and the
/// <see cref="UnmanagedFunctionPointerAttribute"/> delegates it finds in the application's assemblies after trimming; it does
/// not look at function pointer types or at <c>calli</c> instructions. An application has few of those shapes by itself
/// (a trimmed Avalonia app: none with only float parameters, none with more than six integers), and a call through a shape it
/// lacks ends the program ("CANNOT HANDLE INTERP ICALL SIG VFFFF" for <c>glClearColor</c>). These delegates put every shape
/// the backend needs into every application that uses it, and <see cref="GlApi"/>'s constructor keeps them through trimming.
/// <para>A shape is the return type followed by the parameters, each reduced to what WebAssembly passes: V for no return
/// value, I for integers of up to 32 bits, pointers and <c>nint</c>, F for <c>float</c>, D for <c>double</c>. When a function
/// with a new shape is added to <see cref="GlApi"/>, add its delegate here (<c>GlSignatureTests</c> says which is missing).</para>
/// </remarks>
internal static class GlSignatures
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int I();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int II(int a);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int III(int a, int b);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void V();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VI(int a);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VII(int a, int b);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIII(int a, int b, int c);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIII(int a, int b, int c, int d);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIIII(int a, int b, int c, int d, int e);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIIIII(int a, int b, int c, int d, int e, int f);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIIIIII(int a, int b, int c, int d, int e, int f, int g);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIIIIIII(int a, int b, int c, int d, int e, int f, int g, int h);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIIIIIIII(int a, int b, int c, int d, int e, int f, int g, int h, int i);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIIIIIIIII(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIIIIIIIIII(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VF(float a);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VD(double a);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VFF(float a, float b);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VFFFF(float a, float b, float c, float d);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VIIF(int a, int b, float c);
}
