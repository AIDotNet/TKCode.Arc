using System.Buffers;
using System.Runtime.ExceptionServices;

namespace TKCode.Arc.Graphics3D.Rhi;

/// <summary>
/// A device that moves the graphics work to a render thread. The renderer calls it like any device; the calls are
/// written into a command buffer, and a thread of its own, which owns the real device and its context, plays them back.
/// The calling thread then spends its frame on the scene (culling, sorting, building the frame data) and the render
/// thread on the driver (uploads, state changes, draws, shader compilation), one frame behind at most.
/// </summary>
/// <remarks>
/// <para>Call <see cref="Submit"/> at the end of each frame to hand the recorded commands over. Calls that return data
/// (<see cref="ReadPixels"/>, <see cref="ReadPixelsFloat"/>) and <see cref="CreateProgram"/> wait for the render thread,
/// so compile errors still arrive as exceptions from the call. An error on the render thread stops playback; it is
/// thrown from the next <see cref="Submit"/> or <see cref="Flush"/>.</para>
/// <para>Everything here is called from one thread (the one that renders the scene), never from the render thread
/// except inside <see cref="Post"/> callbacks.</para>
/// </remarks>
public sealed class ThreadedGraphicsDevice : GraphicsDevice
{
    private const int MaxQueuedFrames = 1;
    private const int ArenaTrimBytes = 32 << 20;
    private readonly object _gate = new();
    private readonly Queue<Work> _queue = new();
    private readonly Stack<CommandBuffer> _pool = new();
    private readonly Dictionary<long, bool> _vertexFormats = [];
    private readonly ManualResetEventSlim _done = new(false);
    private readonly Thread _thread;
    private readonly Func<GraphicsDevice> _create;
    private readonly Action? _destroyed;
    private GraphicsDevice? _inner;
    private DeviceCapabilities? _capabilities;
    private CommandBuffer _recording = new();
    private volatile ExceptionDispatchInfo? _error;
    private int _queuedFrames;
    private bool _disposed;

    /// <summary>Starts the render thread and creates the real device on it.</summary>
    /// <param name="createDevice">Runs on the render thread: makes the graphics context current there and returns the
    /// device that draws with it.</param>
    /// <param name="destroyed">Runs on the render thread after the device was disposed: releases the context.</param>
    /// <param name="threadName">Name of the render thread, for debuggers.</param>
    /// <exception cref="Exception">Whatever <paramref name="createDevice"/> threw, rethrown on the calling thread.</exception>
    public ThreadedGraphicsDevice(Func<GraphicsDevice> createDevice, Action? destroyed = null, string? threadName = null)
    {
        _create = createDevice ?? throw new ArgumentNullException(nameof(createDevice));
        _destroyed = destroyed;
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = threadName ?? "Arc render thread" };
        _thread.Start();
        _done.Wait();
        _done.Reset();
        if (_error is { } error)
        {
            _thread.Join();
            error.Throw();
        }
    }

    /// <inheritdoc/>
    public override DeviceCapabilities Capabilities => _capabilities!;

    /// <summary>True after a failure on the render thread: the device plays nothing more and every hand-over throws
    /// that failure.</summary>
    public bool HasFailed => _error is not null;

    /// <summary>The managed id of the render thread.</summary>
    public int RenderThreadId => _thread.ManagedThreadId;

    // ---- Handing work over ----

    /// <summary>Hands the commands recorded so far to the render thread and returns. Waits only when the render thread
    /// has not started on the frame before yet, so the caller never gets more than one frame ahead.</summary>
    public void Submit()
    {
        ThrowIfFailed();
        if (_recording.Commands.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            while (_queuedFrames >= MaxQueuedFrames && !_disposed)
            {
                Monitor.Wait(_gate);
            }

            _queue.Enqueue(new Work { Buffer = _recording });
            _queuedFrames++;
            _recording = _pool.Count > 0 ? _pool.Pop() : new CommandBuffer();
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>Hands the recorded commands over and waits until the render thread has played all of them.</summary>
    public void Flush()
    {
        Submit();
        WaitForRenderThread();
        ThrowIfFailed();
    }

    private void WaitForRenderThread()
    {
        lock (_gate)
        {
            _queue.Enqueue(new Work { Signal = true });
            Monitor.PulseAll(_gate);
        }

        _done.Wait();
        _done.Reset();
    }

    /// <summary>Records a callback that runs on the render thread, in order with the other commands, with the real
    /// device: for what the host does around a frame (resizing its target, reading the picture back, presenting).</summary>
    public void Post(Action<GraphicsDevice> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Record(Op.Post, o0: action);
    }

    /// <summary>Runs a callback on the render thread after everything recorded so far, and waits for it.</summary>
    public void Invoke(Action<GraphicsDevice> action)
    {
        Post(action);
        Flush();
    }

    private void ThrowIfFailed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _error?.Throw();
    }

    private void ThreadMain()
    {
        try
        {
            _inner = _create();
            _capabilities = _inner.Capabilities;
        }
        catch (Exception ex)
        {
            _error = ExceptionDispatchInfo.Capture(ex);
        }

        _done.Set();
        var inner = _inner;
        if (inner is null)
        {
            return;
        }

        while (true)
        {
            Work work;
            lock (_gate)
            {
                while (_queue.Count == 0)
                {
                    Monitor.Wait(_gate);
                }

                work = _queue.Dequeue();
            }

            if (work.Buffer is { } buffer)
            {
                // After a failure the device is in an unknown state: drop what follows.
                if (_error is null)
                {
                    try
                    {
                        Execute(inner, buffer);
                    }
                    catch (Exception ex)
                    {
                        _error = ExceptionDispatchInfo.Capture(ex);
                    }
                }

                buffer.Reset();
                lock (_gate)
                {
                    _pool.Push(buffer);
                    _queuedFrames--;
                    Monitor.PulseAll(_gate);
                }
            }

            if (work.Signal)
            {
                _done.Set();
            }

            if (work.Exit)
            {
                break;
            }
        }

        try
        {
            inner.Dispose();
        }
        finally
        {
            _destroyed?.Invoke();
            _done.Set();
        }
    }

    /// <summary>Plays everything that is still recorded, disposes the real device on the render thread and ends the thread.</summary>
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Whatever was recorded (resource releases above all) still has to reach the device.
        if (_error is null)
        {
            try
            {
                Flush();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The device is going away; the failure has nowhere to go.
            }
        }

        _disposed = true;
        lock (_gate)
        {
            _queue.Enqueue(new Work { Exit = true });
            Monitor.PulseAll(_gate);
        }

        _thread.Join();
        _done.Dispose();
    }

    // ---- Recording ----

    private enum Op : byte
    {
        CreateBuffer,
        WriteBuffer,
        CreateTexture,
        WriteTexture,
        GenerateMipmaps,
        CreateSampler,
        CreateProgram,
        CreateVertexInput,
        CreateFramebuffer,
        SetSurface,
        BeginPass,
        EndPass,
        SetViewport,
        SetScissor,
        SetProgram,
        SetRenderState,
        SetUniformBuffer,
        SetTexture,
        SetDrawConstants,
        SetVertexInput,
        Draw,
        DrawIndexed,
        Blit,
        ReadPixels,
        ReadPixelsFloat,
        InvalidateState,
        CreateComputeProgram,
        SetStorageBuffer,
        DispatchCompute,
        ReadBuffer,
        PushDebugGroup,
        PopDebugGroup,
        Destroy,
        Post,
    }

    private struct Command
    {
        public Op Op;
        public int I0, I1, I2, I3, I4, I5, I6, I7;
        public object? O0, O1;
    }

    private struct Work
    {
        public CommandBuffer? Buffer;
        public bool Signal;
        public bool Exit;
    }

    /// <summary>One frame's worth of calls: the commands, the bytes of buffer and texture writes, and the larger
    /// arguments. Reused from frame to frame, so recording allocates nothing once the lists have grown.</summary>
    private sealed class CommandBuffer
    {
        public readonly List<Command> Commands = new(1024);
        public readonly List<RenderState> States = new(64);
        public readonly List<RenderPassDescriptor> Passes = new(16);
        public byte[] Arena = new byte[1 << 16];
        public int ArenaUsed;

        public int Store(ReadOnlySpan<byte> data)
        {
            var offset = ArenaUsed;
            var needed = offset + data.Length;
            if (needed > Arena.Length)
            {
                Array.Resize(ref Arena, (int)Math.Min(Math.Max((long)Arena.Length * 2, needed), Array.MaxLength));
            }

            data.CopyTo(Arena.AsSpan(offset));
            ArenaUsed = needed;
            return offset;
        }

        public void Reset()
        {
            Commands.Clear();
            States.Clear();
            Passes.Clear();

            // One large texture upload should not pin its size forever.
            if (Arena.Length > ArenaTrimBytes && ArenaUsed < ArenaTrimBytes / 4)
            {
                Arena = new byte[ArenaTrimBytes / 4];
            }

            ArenaUsed = 0;
        }
    }

    private void Record(Op op, int i0 = 0, int i1 = 0, int i2 = 0, int i3 = 0, int i4 = 0, int i5 = 0, int i6 = 0, int i7 = 0, object? o0 = null, object? o1 = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _recording.Commands.Add(new Command { Op = op, I0 = i0, I1 = i1, I2 = i2, I3 = i3, I4 = i4, I5 = i5, I6 = i6, I7 = i7, O0 = o0, O1 = o1 });
    }

    // Stand-ins handed to the renderer at once; the real objects appear when the render thread plays the creation.
    private sealed class BufferProxy : GpuBuffer
    {
        private readonly ThreadedGraphicsDevice _device;
        public GpuBuffer? Inner;

        public BufferProxy(ThreadedGraphicsDevice device, BufferUsage usage, int size)
        {
            _device = device;
            Usage = usage;
            SizeInBytes = size;
        }

        protected override void Release() => _device.RecordDestroy(this);
    }

    private sealed class TextureProxy : GpuTexture
    {
        private readonly ThreadedGraphicsDevice _device;
        public GpuTexture? Inner;

        public TextureProxy(ThreadedGraphicsDevice device, in TextureDescriptor descriptor)
        {
            _device = device;
            Descriptor = descriptor;
        }

        protected override void Release() => _device.RecordDestroy(this);
    }

    private sealed class SamplerProxy : GpuSampler
    {
        private readonly ThreadedGraphicsDevice _device;
        public readonly SamplerDescriptor Descriptor;
        public GpuSampler? Inner;

        public SamplerProxy(ThreadedGraphicsDevice device, in SamplerDescriptor descriptor)
        {
            _device = device;
            Descriptor = descriptor;
        }

        protected override void Release() => _device.RecordDestroy(this);
    }

    private sealed class ProgramProxy : GpuProgram
    {
        private readonly ThreadedGraphicsDevice _device;
        public GpuProgram? Inner;

        public ProgramProxy(ThreadedGraphicsDevice device) => _device = device;

        // Creation waits for the render thread, so the program exists by the time anyone asks.
        public override bool IsReady => Inner?.IsReady ?? false;

        protected override void Release() => _device.RecordDestroy(this);
    }

    private sealed class ComputeProgramProxy : GpuComputeProgram
    {
        private readonly ThreadedGraphicsDevice _device;
        public GpuComputeProgram? Inner;

        public ComputeProgramProxy(ThreadedGraphicsDevice device) => _device = device;

        protected override void Release() => _device.RecordDestroy(this);
    }

    private sealed class VertexInputProxy : GpuVertexInput
    {
        private readonly ThreadedGraphicsDevice _device;
        public readonly VertexInputDescriptor Descriptor;
        public GpuVertexInput? Inner;

        public VertexInputProxy(ThreadedGraphicsDevice device, in VertexInputDescriptor descriptor)
        {
            _device = device;
            Descriptor = descriptor;
        }

        protected override void Release() => _device.RecordDestroy(this);
    }

    private sealed class FramebufferProxy : GpuFramebuffer
    {
        private readonly ThreadedGraphicsDevice _device;
        public readonly FramebufferDescriptor Descriptor;
        public GpuFramebuffer? Inner;

        public FramebufferProxy(ThreadedGraphicsDevice device, in FramebufferDescriptor descriptor, int width, int height)
        {
            _device = device;
            Descriptor = descriptor;
            Width = width;
            Height = height;
        }

        protected override void Release() => _device.RecordDestroy(this);
    }

    private void RecordDestroy(GpuResource resource)
    {
        // Resources released after the device are gone with it.
        if (!_disposed)
        {
            Record(Op.Destroy, o0: resource);
        }
    }

    /// <inheritdoc/>
    public override GpuBuffer CreateBuffer(BufferUsage usage, int sizeInBytes, bool dynamic, string? label = null)
    {
        if (usage == BufferUsage.Storage && !Capabilities.ComputeShaders)
        {
            // What the real device would say, said at the call.
            throw new NotSupportedException("This device has no compute shaders (DeviceCapabilities.ComputeShaders is false), so it has no storage buffers.");
        }

        var proxy = new BufferProxy(this, usage, sizeInBytes);
        Record(Op.CreateBuffer, (int)usage, sizeInBytes, dynamic ? 1 : 0, o0: proxy, o1: label);
        return proxy;
    }

    /// <inheritdoc/>
    public override void WriteBuffer(GpuBuffer buffer, int offset, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        if (offset < 0 || offset + data.Length > buffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The write does not fit in the buffer.");
        }

        Statistics.BufferUploads++;
        Statistics.BufferUploadBytes += data.Length;
        Record(Op.WriteBuffer, offset, _recording.Store(data), data.Length, o0: buffer);
    }

    /// <inheritdoc/>
    public override GpuTexture CreateTexture(in TextureDescriptor descriptor)
    {
        var proxy = new TextureProxy(this, descriptor);
        Record(Op.CreateTexture, o0: proxy);
        return proxy;
    }

    /// <inheritdoc/>
    public override void WriteTexture(GpuTexture texture, int mipLevel, int layer, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        Statistics.BufferUploads++;
        Statistics.BufferUploadBytes += data.Length;
        Record(Op.WriteTexture, mipLevel, layer, x, y, width, height, _recording.Store(data), data.Length, texture);
    }

    /// <inheritdoc/>
    public override void GenerateMipmaps(GpuTexture texture) => Record(Op.GenerateMipmaps, o0: texture);

    /// <inheritdoc/>
    public override GpuSampler CreateSampler(in SamplerDescriptor descriptor)
    {
        var proxy = new SamplerProxy(this, descriptor);
        Record(Op.CreateSampler, o0: proxy);
        return proxy;
    }

    /// <inheritdoc/>
    public override GpuProgram CreateProgram(ProgramDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        // Play what came before first, so that a failure of this program is this program's and nothing else's. A
        // shader that does not compile has to fail here, where the renderer can say which material it was.
        Flush();
        var proxy = new ProgramProxy(this);
        Record(Op.CreateProgram, o0: proxy, o1: descriptor);
        Submit();
        WaitForRenderThread();
        if (_error is { } error)
        {
            // Nothing else was in flight: the device is as it was before the call.
            _error = null;
            error.Throw();
        }

        return proxy;
    }

    /// <inheritdoc/>
    public override GpuVertexInput CreateVertexInput(in VertexInputDescriptor descriptor)
    {
        // The caller may reuse its arrays.
        var copy = new VertexInputDescriptor
        {
            Attributes = (VertexAttributeBinding[])descriptor.Attributes.Clone(),
            Buffers = (GpuBuffer[])descriptor.Buffers.Clone(),
            IndexBuffer = descriptor.IndexBuffer,
            IndexFormat = descriptor.IndexFormat,
        };
        var proxy = new VertexInputProxy(this, copy);
        Record(Op.CreateVertexInput, o0: proxy);
        return proxy;
    }

    /// <inheritdoc/>
    public override GpuFramebuffer CreateFramebuffer(in FramebufferDescriptor descriptor)
    {
        var copy = new FramebufferDescriptor
        {
            Colors = (FramebufferAttachment[]?)descriptor.Colors?.Clone(),
            Depth = descriptor.Depth,
            Label = descriptor.Label,
        };
        int width = 0, height = 0;
        if (copy.Colors is { Length: > 0 } colors)
        {
            width = Math.Max(1, colors[0].Texture.Width >> colors[0].MipLevel);
            height = Math.Max(1, colors[0].Texture.Height >> colors[0].MipLevel);
        }
        else if (copy.Depth is { } depth)
        {
            width = Math.Max(1, depth.Texture.Width >> depth.MipLevel);
            height = Math.Max(1, depth.Texture.Height >> depth.MipLevel);
        }

        var proxy = new FramebufferProxy(this, copy, width, height);
        Record(Op.CreateFramebuffer, o0: proxy);
        return proxy;
    }

    /// <inheritdoc/>
    public override void SetSurface(nint nativeHandle, int width, int height) =>
        Record(Op.SetSurface, (int)(nativeHandle & 0xFFFFFFFF), (int)((long)nativeHandle >> 32), width, height);

    /// <inheritdoc/>
    public override void BeginPass(in RenderPassDescriptor pass)
    {
        Statistics.Passes++;
        _recording.Passes.Add(pass);
        Record(Op.BeginPass, _recording.Passes.Count - 1);
    }

    /// <inheritdoc/>
    public override void EndPass() => Record(Op.EndPass);

    /// <inheritdoc/>
    public override void SetViewport(int x, int y, int width, int height) => Record(Op.SetViewport, x, y, width, height);

    /// <inheritdoc/>
    public override void SetScissor(int x, int y, int width, int height, bool enabled) => Record(Op.SetScissor, x, y, width, height, enabled ? 1 : 0);

    /// <inheritdoc/>
    public override void SetProgram(GpuProgram program) => Record(Op.SetProgram, o0: program);

    /// <inheritdoc/>
    public override void SetRenderState(in RenderState state)
    {
        _recording.States.Add(state);
        Record(Op.SetRenderState, _recording.States.Count - 1);
    }

    /// <inheritdoc/>
    public override void SetUniformBuffer(int slot, GpuBuffer buffer, int offset, int size) => Record(Op.SetUniformBuffer, slot, offset, size, o0: buffer);

    /// <inheritdoc/>
    public override void SetTexture(int unit, GpuTexture texture, GpuSampler sampler) => Record(Op.SetTexture, unit, o0: texture, o1: sampler);

    /// <inheritdoc/>
    public override void SetDrawConstants(int c0, int c1, int c2, int c3) => Record(Op.SetDrawConstants, c0, c1, c2, c3);

    /// <inheritdoc/>
    public override void SetVertexInput(GpuVertexInput input) => Record(Op.SetVertexInput, o0: input);

    /// <inheritdoc/>
    public override void Draw(PrimitiveTopology topology, int firstVertex, int vertexCount, int instanceCount)
    {
        Count(topology, vertexCount, instanceCount);
        Record(Op.Draw, (int)topology, firstVertex, vertexCount, instanceCount);
    }

    /// <inheritdoc/>
    public override void DrawIndexed(PrimitiveTopology topology, int firstIndex, int indexCount, int instanceCount)
    {
        Count(topology, indexCount, instanceCount);
        Record(Op.DrawIndexed, (int)topology, firstIndex, indexCount, instanceCount);
    }

    // The counters describe what was asked for; the real device keeps its own on the render thread.
    private void Count(PrimitiveTopology topology, int count, int instances)
    {
        Statistics.DrawCalls++;
        if (topology == PrimitiveTopology.Triangles)
        {
            Statistics.Triangles += (long)(count / 3) * instances;
        }
        else if (topology == PrimitiveTopology.TriangleStrip)
        {
            Statistics.Triangles += (long)Math.Max(0, count - 2) * instances;
        }
    }

    /// <inheritdoc/>
    public override void Blit(GpuFramebuffer? source, GpuFramebuffer? destination, bool color, bool depth, bool linear) =>
        Record(Op.Blit, color ? 1 : 0, depth ? 1 : 0, linear ? 1 : 0, o0: source, o1: destination);

    /// <inheritdoc/>
    public override void ReadPixels(GpuFramebuffer? source, int x, int y, int width, int height, Span<byte> rgba)
    {
        var length = checked(width * height * 4);
        var result = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Record(Op.ReadPixels, x, y, width, height, length, o0: source, o1: result);
            Flush();
            result.AsSpan(0, length).CopyTo(rgba);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(result);
        }
    }

    /// <inheritdoc/>
    public override void ReadPixelsFloat(GpuFramebuffer? source, int x, int y, int width, int height, Span<float> rgba)
    {
        var length = checked(width * height * 4);
        var result = ArrayPool<float>.Shared.Rent(length);
        try
        {
            Record(Op.ReadPixelsFloat, x, y, width, height, length, o0: source, o1: result);
            Flush();
            result.AsSpan(0, length).CopyTo(rgba);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(result);
        }
    }

    /// <inheritdoc/>
    public override void InvalidateState() => Record(Op.InvalidateState);

    /// <inheritdoc/>
    public override bool SupportsVertexFormat(VertexFormat format, int stride)
    {
        // Asked while geometry is prepared, a few times per vertex layout: remember what the real device said.
        var key = ((long)format << 32) | (uint)stride;
        if (!_vertexFormats.TryGetValue(key, out var supported))
        {
            Invoke(device => supported = device.SupportsVertexFormat(format, stride));
            _vertexFormats.Add(key, supported);
        }

        return supported;
    }

    /// <inheritdoc/>
    public override GpuComputeProgram CreateComputeProgram(ComputeProgramDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // Like CreateProgram: alone in its hand-over, so that a failure is this program's.
        Flush();
        var proxy = new ComputeProgramProxy(this);
        Record(Op.CreateComputeProgram, o0: proxy, o1: descriptor);
        Submit();
        WaitForRenderThread();
        if (_error is { } error)
        {
            _error = null;
            error.Throw();
        }

        return proxy;
    }

    /// <inheritdoc/>
    public override void SetStorageBuffer(int slot, GpuBuffer buffer, int offset = 0, int size = 0) => Record(Op.SetStorageBuffer, slot, offset, size, o0: buffer);

    /// <inheritdoc/>
    public override void DispatchCompute(GpuComputeProgram program, int groupsX, int groupsY = 1, int groupsZ = 1) =>
        Record(Op.DispatchCompute, groupsX, groupsY, groupsZ, o0: program);

    /// <inheritdoc/>
    public override void ReadBuffer(GpuBuffer buffer, int offset, Span<byte> data)
    {
        var result = ArrayPool<byte>.Shared.Rent(data.Length);
        try
        {
            Record(Op.ReadBuffer, offset, data.Length, o0: buffer, o1: result);
            Flush();
            result.AsSpan(0, data.Length).CopyTo(data);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(result);
        }
    }

    /// <inheritdoc/>
    public override void PushDebugGroup(string name) => Record(Op.PushDebugGroup, o0: name);

    /// <inheritdoc/>
    public override void PopDebugGroup() => Record(Op.PopDebugGroup);

    // ---- Playback (render thread) ----

    private static GpuBuffer Real(GpuBuffer buffer) => ((BufferProxy)buffer).Inner ?? throw Missing("buffer");

    private static GpuTexture Real(GpuTexture texture) => ((TextureProxy)texture).Inner ?? throw Missing("texture");

    private static GpuFramebuffer? Real(GpuFramebuffer? framebuffer) =>
        framebuffer is null ? null : ((FramebufferProxy)framebuffer).Inner ?? throw Missing("framebuffer");

    private static InvalidOperationException Missing(string what) => new($"A {what} was used after it was released or before it was created.");

    private static void Execute(GraphicsDevice device, CommandBuffer buffer)
    {
        var commands = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffer.Commands);
        for (var i = 0; i < commands.Length; i++)
        {
            ref readonly var c = ref commands[i];
            switch (c.Op)
            {
                case Op.CreateBuffer:
                    ((BufferProxy)c.O0!).Inner = device.CreateBuffer((BufferUsage)c.I0, c.I1, c.I2 != 0, (string?)c.O1);
                    break;
                case Op.WriteBuffer:
                    device.WriteBuffer(Real((GpuBuffer)c.O0!), c.I0, buffer.Arena.AsSpan(c.I1, c.I2));
                    break;
                case Op.CreateTexture:
                {
                    var proxy = (TextureProxy)c.O0!;
                    proxy.Inner = device.CreateTexture(proxy.Descriptor);
                    break;
                }

                case Op.WriteTexture:
                    device.WriteTexture(Real((GpuTexture)c.O0!), c.I0, c.I1, c.I2, c.I3, c.I4, c.I5, buffer.Arena.AsSpan(c.I6, c.I7));
                    break;
                case Op.GenerateMipmaps:
                    device.GenerateMipmaps(Real((GpuTexture)c.O0!));
                    break;
                case Op.CreateSampler:
                {
                    var proxy = (SamplerProxy)c.O0!;
                    proxy.Inner = device.CreateSampler(proxy.Descriptor);
                    break;
                }

                case Op.CreateProgram:
                    ((ProgramProxy)c.O0!).Inner = device.CreateProgram((ProgramDescriptor)c.O1!);
                    break;
                case Op.CreateVertexInput:
                {
                    var proxy = (VertexInputProxy)c.O0!;
                    var d = proxy.Descriptor;
                    var buffers = new GpuBuffer[d.Buffers.Length];
                    for (var b = 0; b < buffers.Length; b++)
                    {
                        buffers[b] = Real(d.Buffers[b]);
                    }

                    proxy.Inner = device.CreateVertexInput(new VertexInputDescriptor
                    {
                        Attributes = d.Attributes,
                        Buffers = buffers,
                        IndexBuffer = d.IndexBuffer is null ? null : Real(d.IndexBuffer),
                        IndexFormat = d.IndexFormat,
                    });
                    break;
                }

                case Op.CreateFramebuffer:
                {
                    var proxy = (FramebufferProxy)c.O0!;
                    var d = proxy.Descriptor;
                    FramebufferAttachment[]? colors = null;
                    if (d.Colors is not null)
                    {
                        colors = new FramebufferAttachment[d.Colors.Length];
                        for (var a = 0; a < colors.Length; a++)
                        {
                            colors[a] = new FramebufferAttachment { Texture = Real(d.Colors[a].Texture), MipLevel = d.Colors[a].MipLevel, Layer = d.Colors[a].Layer };
                        }
                    }

                    FramebufferAttachment? depth = null;
                    if (d.Depth is { } da)
                    {
                        depth = new FramebufferAttachment { Texture = Real(da.Texture), MipLevel = da.MipLevel, Layer = da.Layer };
                    }
                    proxy.Inner = device.CreateFramebuffer(new FramebufferDescriptor { Colors = colors, Depth = depth, Label = d.Label });
                    break;
                }

                case Op.SetSurface:
                    device.SetSurface((nint)(((long)c.I1 << 32) | (uint)c.I0), c.I2, c.I3);
                    break;
                case Op.BeginPass:
                {
                    var pass = buffer.Passes[c.I0];
                    pass.Target = Real(pass.Target);
                    device.BeginPass(pass);
                    break;
                }

                case Op.EndPass:
                    device.EndPass();
                    break;
                case Op.SetViewport:
                    device.SetViewport(c.I0, c.I1, c.I2, c.I3);
                    break;
                case Op.SetScissor:
                    device.SetScissor(c.I0, c.I1, c.I2, c.I3, c.I4 != 0);
                    break;
                case Op.SetProgram:
                    device.SetProgram(((ProgramProxy)c.O0!).Inner ?? throw Missing("program"));
                    break;
                case Op.SetRenderState:
                    device.SetRenderState(buffer.States[c.I0]);
                    break;
                case Op.SetUniformBuffer:
                    device.SetUniformBuffer(c.I0, Real((GpuBuffer)c.O0!), c.I1, c.I2);
                    break;
                case Op.SetTexture:
                    device.SetTexture(c.I0, Real((GpuTexture)c.O0!), ((SamplerProxy)c.O1!).Inner ?? throw Missing("sampler"));
                    break;
                case Op.SetDrawConstants:
                    device.SetDrawConstants(c.I0, c.I1, c.I2, c.I3);
                    break;
                case Op.SetVertexInput:
                    device.SetVertexInput(((VertexInputProxy)c.O0!).Inner ?? throw Missing("vertex input"));
                    break;
                case Op.Draw:
                    device.Draw((PrimitiveTopology)c.I0, c.I1, c.I2, c.I3);
                    break;
                case Op.DrawIndexed:
                    device.DrawIndexed((PrimitiveTopology)c.I0, c.I1, c.I2, c.I3);
                    break;
                case Op.Blit:
                    device.Blit(Real((GpuFramebuffer?)c.O0), Real((GpuFramebuffer?)c.O1), c.I0 != 0, c.I1 != 0, c.I2 != 0);
                    break;
                case Op.ReadPixels:
                    device.ReadPixels(Real((GpuFramebuffer?)c.O0), c.I0, c.I1, c.I2, c.I3, ((byte[])c.O1!).AsSpan(0, c.I4));
                    break;
                case Op.ReadPixelsFloat:
                    device.ReadPixelsFloat(Real((GpuFramebuffer?)c.O0), c.I0, c.I1, c.I2, c.I3, ((float[])c.O1!).AsSpan(0, c.I4));
                    break;
                case Op.InvalidateState:
                    device.InvalidateState();
                    break;
                case Op.CreateComputeProgram:
                    ((ComputeProgramProxy)c.O0!).Inner = device.CreateComputeProgram((ComputeProgramDescriptor)c.O1!);
                    break;
                case Op.SetStorageBuffer:
                    device.SetStorageBuffer(c.I0, Real((GpuBuffer)c.O0!), c.I1, c.I2);
                    break;
                case Op.DispatchCompute:
                    device.DispatchCompute(((ComputeProgramProxy)c.O0!).Inner ?? throw Missing("compute program"), c.I0, c.I1, c.I2);
                    break;
                case Op.ReadBuffer:
                    device.ReadBuffer(Real((GpuBuffer)c.O0!), c.I0, ((byte[])c.O1!).AsSpan(0, c.I1));
                    break;
                case Op.PushDebugGroup:
                    device.PushDebugGroup((string)c.O0!);
                    break;
                case Op.PopDebugGroup:
                    device.PopDebugGroup();
                    break;
                case Op.Destroy:
                    switch (c.O0)
                    {
                        case BufferProxy p:
                            p.Inner?.Dispose();
                            p.Inner = null;
                            break;
                        case TextureProxy p:
                            p.Inner?.Dispose();
                            p.Inner = null;
                            break;
                        case SamplerProxy p:
                            p.Inner?.Dispose();
                            p.Inner = null;
                            break;
                        case ProgramProxy p:
                            p.Inner?.Dispose();
                            p.Inner = null;
                            break;
                        case ComputeProgramProxy p:
                            p.Inner?.Dispose();
                            p.Inner = null;
                            break;
                        case VertexInputProxy p:
                            p.Inner?.Dispose();
                            p.Inner = null;
                            break;
                        case FramebufferProxy p:
                            p.Inner?.Dispose();
                            p.Inner = null;
                            break;
                    }

                    break;
                case Op.Post:
                    ((Action<GraphicsDevice>)c.O0!)(device);
                    break;
            }
        }
    }
}
