namespace TKCode.Arc.Graphics3D.Rhi;

/// <summary>A GPU object owned by a <see cref="GraphicsDevice"/>. Dispose it on the thread that owns the device, while
/// the device's context is current.</summary>
public abstract class GpuResource : IDisposable
{
    /// <summary>True after <see cref="Dispose"/>.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        Release();
    }

    /// <summary>Frees the GPU object.</summary>
    protected abstract void Release();
}

/// <summary>A vertex, index, uniform or storage buffer.</summary>
public abstract class GpuBuffer : GpuResource
{
    public BufferUsage Usage { get; protected init; }
    public int SizeInBytes { get; protected set; }
}

/// <summary>A texture.</summary>
public abstract class GpuTexture : GpuResource
{
    public TextureDescriptor Descriptor { get; protected init; }
    public int Width => Descriptor.Width;
    public int Height => Descriptor.Height;
}

/// <summary>A sampler.</summary>
public abstract class GpuSampler : GpuResource
{
}

/// <summary>A linked vertex + fragment program.</summary>
public abstract class GpuProgram : GpuResource
{
    /// <summary>False while the device is still compiling it in the background; draws with it are skipped until then.</summary>
    public abstract bool IsReady { get; }
}

/// <summary>A compiled compute program.</summary>
public abstract class GpuComputeProgram : GpuResource
{
}

/// <summary>Vertex buffers with their attribute layout (a vertex array object in OpenGL).</summary>
public abstract class GpuVertexInput : GpuResource
{
}

/// <summary>A set of render target attachments.</summary>
public abstract class GpuFramebuffer : GpuResource
{
    public int Width { get; protected set; }
    public int Height { get; protected set; }
}

/// <summary>Raised when a shader fails to compile or link.</summary>
public sealed class ShaderCompilationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ShaderCompilationException(string message, string? source)
        : base(message)
    {
        ShaderSource = source;
    }

    /// <summary>The source that failed, when known.</summary>
    public string? ShaderSource { get; }
}

/// <summary>The rendering hardware interface: the only thing the renderer talks to. Shaped after WebGPU (immutable
/// resources created from descriptors, slot-based bindings, explicit passes) so that other backends can implement it
/// without changing the renderer. All calls happen on one thread, with the device's context current.</summary>
public abstract class GraphicsDevice : IDisposable
{
    /// <summary>What the device supports.</summary>
    public abstract DeviceCapabilities Capabilities { get; }

    /// <summary>Counters of the work since <see cref="ResetStatistics"/>.</summary>
    public DeviceStatistics Statistics;

    /// <summary>Clears <see cref="Statistics"/>.</summary>
    public void ResetStatistics() => Statistics = default;

    /// <summary>Creates a buffer of a fixed size.</summary>
    public abstract GpuBuffer CreateBuffer(BufferUsage usage, int sizeInBytes, bool dynamic, string? label = null);

    /// <summary>Writes bytes into a buffer.</summary>
    public abstract void WriteBuffer(GpuBuffer buffer, int offset, ReadOnlySpan<byte> data);

    /// <summary>Creates a texture with undefined contents.</summary>
    public abstract GpuTexture CreateTexture(in TextureDescriptor descriptor);

    /// <summary>Writes one rectangle of one mip level. <paramref name="layer"/> is the cube face or array layer.</summary>
    public abstract void WriteTexture(GpuTexture texture, int mipLevel, int layer, int x, int y, int width, int height, ReadOnlySpan<byte> data);

    /// <summary>Fills the mip chain from level 0.</summary>
    public abstract void GenerateMipmaps(GpuTexture texture);

    /// <summary>Creates a sampler.</summary>
    public abstract GpuSampler CreateSampler(in SamplerDescriptor descriptor);

    /// <summary>Compiles and links a program. Throws <see cref="ShaderCompilationException"/> when it fails.</summary>
    public abstract GpuProgram CreateProgram(ProgramDescriptor descriptor);

    /// <summary>Creates a vertex input.</summary>
    public abstract GpuVertexInput CreateVertexInput(in VertexInputDescriptor descriptor);

    /// <summary>False when the device cannot read an attribute of this format at this byte distance between
    /// vertices; the caller then uploads the attribute as floats instead. OpenGL reads every format.</summary>
    public virtual bool SupportsVertexFormat(VertexFormat format, int stride) => true;

    /// <summary>Creates a framebuffer.</summary>
    public abstract GpuFramebuffer CreateFramebuffer(in FramebufferDescriptor descriptor);

    /// <summary>Names the surface that passes with a null target draw to. The host calls it before each frame (in
    /// Avalonia: the framebuffer handed to <c>OnOpenGlRender</c>).</summary>
    public abstract void SetSurface(nint nativeHandle, int width, int height);

    /// <summary>Starts a pass: binds the target, sets the viewport and clears.</summary>
    public abstract void BeginPass(in RenderPassDescriptor pass);

    /// <summary>Ends the current pass.</summary>
    public abstract void EndPass();

    /// <summary>Changes the viewport inside a pass.</summary>
    public abstract void SetViewport(int x, int y, int width, int height);

    /// <summary>Limits drawing to a rectangle, or removes the limit with null.</summary>
    public abstract void SetScissor(int x, int y, int width, int height, bool enabled);

    /// <summary>Selects the program of the following draws.</summary>
    public abstract void SetProgram(GpuProgram program);

    /// <summary>Selects the fixed-function state of the following draws.</summary>
    public abstract void SetRenderState(in RenderState state);

    /// <summary>Binds a range of a uniform buffer to a slot.</summary>
    public abstract void SetUniformBuffer(int slot, GpuBuffer buffer, int offset, int size);

    /// <summary>Binds a texture and sampler to a unit.</summary>
    public abstract void SetTexture(int unit, GpuTexture texture, GpuSampler sampler);

    /// <summary>Four integers the shaders of the following draws read as the built-in draw constants (the equivalent of
    /// push constants).</summary>
    public abstract void SetDrawConstants(int c0, int c1, int c2, int c3);

    /// <summary>Selects the vertex input of the following draws.</summary>
    public abstract void SetVertexInput(GpuVertexInput input);

    /// <summary>Draws non-indexed primitives.</summary>
    public abstract void Draw(PrimitiveTopology topology, int firstVertex, int vertexCount, int instanceCount);

    /// <summary>Draws indexed primitives.</summary>
    public abstract void DrawIndexed(PrimitiveTopology topology, int firstIndex, int indexCount, int instanceCount);

    /// <summary>Copies (and resolves, for multisampled sources) color and/or depth between framebuffers. A null
    /// framebuffer is the surface.</summary>
    public abstract void Blit(GpuFramebuffer? source, GpuFramebuffer? destination, bool color, bool depth, bool linear);

    /// <summary>Reads RGBA8 pixels, rows bottom to top.</summary>
    public abstract void ReadPixels(GpuFramebuffer? source, int x, int y, int width, int height, Span<byte> rgba);

    /// <summary>Reads RGBA float pixels, rows bottom to top.</summary>
    public abstract void ReadPixelsFloat(GpuFramebuffer? source, int x, int y, int width, int height, Span<float> rgba);

    /// <summary>Forgets cached state. Call after foreign code used the same context (the host's own renderer).</summary>
    public abstract void InvalidateState();

    // ---- Compute (devices with DeviceCapabilities.ComputeShaders) ----

    /// <summary>Compiles a compute program. Throws <see cref="ShaderCompilationException"/> when it fails and
    /// <see cref="NotSupportedException"/> on a device without compute shaders.</summary>
    public virtual GpuComputeProgram CreateComputeProgram(ComputeProgramDescriptor descriptor) => throw NoCompute();

    /// <summary>Binds a range of a <see cref="BufferUsage.Storage"/> buffer to a storage slot of the following
    /// dispatches. A size of 0 binds the buffer from <paramref name="offset"/> to its end.</summary>
    public virtual void SetStorageBuffer(int slot, GpuBuffer buffer, int offset = 0, int size = 0) => throw NoCompute();

    /// <summary>Runs a compute program over a grid of work groups, outside a render pass. It reads the uniform
    /// buffers, textures and storage buffers that are bound to the slots its source names.</summary>
    public virtual void DispatchCompute(GpuComputeProgram program, int groupsX, int groupsY = 1, int groupsZ = 1) => throw NoCompute();

    /// <summary>Reads bytes of a <see cref="BufferUsage.Storage"/> buffer back, after the work submitted so far.
    /// Slow: it waits for the GPU.</summary>
    public virtual void ReadBuffer(GpuBuffer buffer, int offset, Span<byte> data) => throw NoCompute();

    private static NotSupportedException NoCompute() => new("This device has no compute shaders (DeviceCapabilities.ComputeShaders is false).");

    /// <summary>Marks the start of a named group in graphics debuggers.</summary>
    public virtual void PushDebugGroup(string name)
    {
    }

    /// <summary>Ends the last debug group.</summary>
    public virtual void PopDebugGroup()
    {
    }

    /// <inheritdoc/>
    public abstract void Dispose();
}

/// <summary>Work counters of a device.</summary>
public struct DeviceStatistics
{
    public int DrawCalls;
    public long Triangles;
    public int ProgramSwitches;
    public int StateChanges;
    public int TextureBinds;
    public int BufferUploads;
    public long BufferUploadBytes;
    public int Passes;
}
