using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

/// <summary>What one element of a compute buffer is. The sizes are those of a GLSL <c>std430</c> array, which are
/// also the sizes of the matching .NET types (<see cref="float"/>, <see cref="int"/>, <see cref="uint"/>,
/// <see cref="Vector2"/>, <see cref="Vector4"/>, <see cref="Matrix4x4"/>). There is no three-component vector: it would
/// occupy four components in the buffer.</summary>
public enum ComputeElement
{
    /// <summary>GLSL <c>float</c>, .NET <see cref="float"/>.</summary>
    Float,

    /// <summary>GLSL <c>int</c>, .NET <see cref="int"/>.</summary>
    Int,

    /// <summary>GLSL <c>uint</c>, .NET <see cref="uint"/>.</summary>
    UInt,

    /// <summary>GLSL <c>vec2</c>, .NET <see cref="Vector2"/>.</summary>
    Vec2,

    /// <summary>GLSL <c>vec4</c>, .NET <see cref="Vector4"/>.</summary>
    Vec4,

    /// <summary>GLSL <c>mat4</c>, .NET <see cref="Matrix4x4"/> (the same memory layout).</summary>
    Mat4,
}

/// <summary>One buffer of a compute kernel: the name the kernel reads and writes it by, as an array.</summary>
/// <param name="Name">The GLSL name of the array.</param>
/// <param name="Element">The type of one element.</param>
/// <param name="ReadOnly">True when the kernel only reads it.</param>
public readonly record struct ComputeBufferLayout(string Name, ComputeElement Element, bool ReadOnly = false);

/// <summary>Describes a <see cref="GpuCompute"/>.</summary>
public sealed class GpuComputeDescriptor
{
    /// <summary>The statements run for every element, in GLSL. <c>index</c> (a <c>uint</c>) is the element; the
    /// buffers are arrays under their names; <c>u_params[i]</c> are the parameters and <c>u_count</c> is the number of
    /// elements of the run.</summary>
    public required string Kernel { get; init; }

    /// <summary>GLSL functions and constants the kernel uses, placed before it.</summary>
    public string Functions { get; init; } = "";

    /// <summary>The buffers, in the order <see cref="GpuCompute.Run"/> takes them.</summary>
    public required ComputeBufferLayout[] Buffers { get; init; }

    /// <summary>The number of <c>vec4</c> parameters (<see cref="GpuCompute.SetParameter"/>).</summary>
    public int Parameters { get; init; }

    /// <summary>A short name shown in compile errors and graphics debuggers.</summary>
    public string? Label { get; init; }
}

/// <summary>Runs a kernel over arrays on the GPU: buffers in, a GLSL kernel per element, buffers out. For work that is
/// the same small computation on many elements (particles, instance transforms). It needs a device with
/// <see cref="DeviceCapabilities.ComputeShaders"/> (the WebGPU backend; OpenGL 3.3 / OpenGL ES 3.0 have none).</summary>
/// <remarks>Call it between frames, on the device's thread, not inside a render pass. A run leaves its own buffers
/// bound to uniform slot <see cref="UniformSlot"/> and to the storage slots.</remarks>
public sealed class GpuCompute : IDisposable
{
    /// <summary>Elements per work group.</summary>
    public const int GroupSize = 64;

    /// <summary>The uniform buffer slot of the kernel's parameters: the first one the renderer does not use.</summary>
    public const int UniformSlot = 4;

    private readonly GraphicsDevice _device;
    private readonly GpuComputeProgram _program;
    private readonly GpuBuffer _uniforms;
    private readonly float[] _uniformData;
    private readonly ComputeBufferLayout[] _buffers;
    private bool _disposed;

    /// <summary>Compiles the kernel.</summary>
    /// <exception cref="NotSupportedException">The device has no compute shaders.</exception>
    /// <exception cref="ShaderCompilationException">The kernel does not compile.</exception>
    public GpuCompute(GraphicsDevice device, GpuComputeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(descriptor);
        var caps = device.Capabilities;
        if (!caps.ComputeShaders || caps.ShaderLanguage != ShaderLanguage.Glsl450)
        {
            throw new NotSupportedException("GpuCompute needs a device with compute shaders (DeviceCapabilities.ComputeShaders).");
        }

        if (descriptor.Buffers.Length == 0 || descriptor.Buffers.Length > caps.MaxStorageBuffers)
        {
            throw new ArgumentException($"A kernel has 1 to {caps.MaxStorageBuffers} buffers on this device.", nameof(descriptor));
        }

        _device = device;
        _buffers = [.. descriptor.Buffers];
        Parameters = Math.Max(0, descriptor.Parameters);
        Source = BuildSource(descriptor, Parameters);
        _program = device.CreateComputeProgram(new ComputeProgramDescriptor { Source = Source, Label = descriptor.Label ?? "compute" });

        // ivec4 u_range, then the parameters (at least one: GLSL has no empty arrays).
        _uniformData = new float[4 + (Math.Max(1, Parameters) * 4)];
        _uniforms = device.CreateBuffer(BufferUsage.Uniform, _uniformData.Length * 4, true, descriptor.Label ?? "compute");
    }

    /// <summary>True when the device can run kernels.</summary>
    public static bool IsSupported(GraphicsDevice device) =>
        device is not null && device.Capabilities.ComputeShaders && device.Capabilities.ShaderLanguage == ShaderLanguage.Glsl450;

    /// <summary>The number of <c>vec4</c> parameters.</summary>
    public int Parameters { get; }

    /// <summary>The compute shader built around the kernel (for diagnostics).</summary>
    public string Source { get; }

    /// <summary>Bytes of one element.</summary>
    public static int ElementSize(ComputeElement element) => element switch
    {
        ComputeElement.Vec2 => 8,
        ComputeElement.Vec4 => 16,
        ComputeElement.Mat4 => 64,
        _ => 4,
    };

    private static string GlslType(ComputeElement element) => element switch
    {
        ComputeElement.Float => "float",
        ComputeElement.Int => "int",
        ComputeElement.UInt => "uint",
        ComputeElement.Vec2 => "vec2",
        ComputeElement.Vec4 => "vec4",
        _ => "mat4",
    };

    private static string BuildSource(GpuComputeDescriptor descriptor, int parameters)
    {
        var sb = new StringBuilder("#version 450\n");
        sb.Append("layout(local_size_x = ").Append(GroupSize.ToString(CultureInfo.InvariantCulture)).Append(") in;\n");
        for (var i = 0; i < descriptor.Buffers.Length; i++)
        {
            var buffer = descriptor.Buffers[i];
            if (string.IsNullOrEmpty(buffer.Name))
            {
                throw new ArgumentException("Every buffer of a kernel has a name.", nameof(descriptor));
            }

            sb.Append(ShaderDialect.StorageBuffer(i, GlslType(buffer.Element), buffer.Name, buffer.ReadOnly));
        }

        sb.Append(ShaderDialect.UniformBlock(
            UniformSlot, "ComputeBlock", "    ivec4 u_range;\n    vec4 u_params[" + Math.Max(1, parameters).ToString(CultureInfo.InvariantCulture) + "];\n"));
        sb.Append("#define u_count uint(u_range.y)\n");
        sb.Append(descriptor.Functions).Append('\n');

        // The last work group reaches past the end of the run: those invocations do nothing.
        sb.Append("void main() {\n    uint index = gl_GlobalInvocationID.x + uint(u_range.x);\n    if (index >= u_count) {\n        return;\n    }\n");
        sb.Append(descriptor.Kernel).Append("\n}\n");
        return sb.ToString();
    }

    /// <summary>Creates a buffer for the kernel's buffer at <paramref name="index"/>, with room for
    /// <paramref name="elementCount"/> elements, filled with zeros.</summary>
    public GpuBuffer CreateBuffer(int index, int elementCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementCount);
        var layout = _buffers[index];
        return _device.CreateBuffer(BufferUsage.Storage, checked(elementCount * ElementSize(layout.Element)), true, layout.Name);
    }

    /// <summary>Creates a buffer for the kernel's buffer at <paramref name="index"/> holding <paramref name="data"/>.
    /// <typeparamref name="T"/> is the .NET type of the element (see <see cref="ComputeElement"/>).</summary>
    public GpuBuffer CreateBuffer<T>(int index, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(data);
        var size = ElementSize(_buffers[index].Element);
        if (bytes.Length == 0 || bytes.Length % size != 0)
        {
            throw new ArgumentException($"The data is not a whole number of {_buffers[index].Element} elements.", nameof(data));
        }

        var buffer = CreateBuffer(index, bytes.Length / size);
        _device.WriteBuffer(buffer, 0, bytes);
        return buffer;
    }

    /// <summary>Sets <c>u_params[index]</c> for the following runs.</summary>
    public void SetParameter(int index, Vector4 value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Parameters);
        var o = 4 + (index * 4);
        _uniformData[o] = value.X;
        _uniformData[o + 1] = value.Y;
        _uniformData[o + 2] = value.Z;
        _uniformData[o + 3] = value.W;
    }

    /// <summary>Runs the kernel for elements 0 to <paramref name="count"/> - 1. <paramref name="buffers"/> are bound in
    /// the order of <see cref="GpuComputeDescriptor.Buffers"/>; each needs room for <paramref name="count"/> elements.</summary>
    public void Run(int count, ReadOnlySpan<GpuBuffer> buffers)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffers.Length != _buffers.Length)
        {
            throw new ArgumentException($"The kernel takes {_buffers.Length} buffers.", nameof(buffers));
        }

        if (count <= 0)
        {
            return;
        }

        for (var i = 0; i < buffers.Length; i++)
        {
            if (buffers[i].SizeInBytes < (long)count * ElementSize(_buffers[i].Element))
            {
                throw new ArgumentException($"The buffer '{_buffers[i].Name}' is smaller than {count} elements.", nameof(buffers));
            }

            _device.SetStorageBuffer(i, buffers[i]);
        }

        // One dispatch covers a limited number of work groups; a longer run takes several, each starting further on.
        var perDispatch = Math.Max(1, _device.Capabilities.MaxComputeWorkGroups) * (long)GroupSize;
        var range = MemoryMarshal.Cast<float, int>(_uniformData.AsSpan(0, 4));
        range[1] = count;
        for (long first = 0; first < count; first += perDispatch)
        {
            var elements = (int)Math.Min(perDispatch, count - first);
            range[0] = (int)first;
            _device.WriteBuffer(_uniforms, 0, MemoryMarshal.AsBytes<float>(_uniformData));
            _device.SetUniformBuffer(UniformSlot, _uniforms, 0, _uniformData.Length * 4);
            _device.DispatchCompute(_program, (elements + GroupSize - 1) / GroupSize);
        }
    }

    /// <summary>Reads the first elements of a buffer back, after the runs so far. Slow: it waits for the GPU.</summary>
    public void Read<T>(GpuBuffer buffer, Span<T> destination)
        where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);
        _device.ReadBuffer(buffer, 0, MemoryMarshal.AsBytes(destination));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _program.Dispose();
        _uniforms.Dispose();
    }
}
