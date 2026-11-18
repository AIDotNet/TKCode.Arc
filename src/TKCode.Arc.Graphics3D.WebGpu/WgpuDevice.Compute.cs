using System.Runtime.InteropServices;

namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

// Compute programs, storage buffers and reading a buffer back.
public sealed unsafe partial class WgpuDevice
{
    private readonly WgpuBuffer?[] _storageBuffer = new WgpuBuffer?[MaxStorageSlots];
    private readonly int[] _storageOffset = new int[MaxStorageSlots];
    private readonly int[] _storageSize = new int[MaxStorageSlots];

    private sealed class WgpuComputeProgram : GpuComputeProgram
    {
        private readonly WgpuDevice _device;
        private readonly Dictionary<uint, nint> _pipelines = [];

        public WgpuComputeProgram(WgpuDevice device, ComputeInfo info, nint module)
        {
            _device = device;
            Info = info;
            Module = module;
        }

        public ComputeInfo Info { get; }

        public nint Module { get; }

        /// <summary>The pipeline for one filtering class of the bound textures (see <see cref="GetLayout"/>).</summary>
        public nint Pipeline(uint unfilterable, ProgramLayout layout)
        {
            if (!_pipelines.TryGetValue(unfilterable, out var pipeline))
            {
                var main = stackalloc byte[4] { (byte)'m', (byte)'a', (byte)'i', (byte)'n' };
                var labelBytes = stackalloc byte[LabelBytes];
                var descriptor = new WgpuComputePipelineDescriptor
                {
                    Label = Label(Info.Label, labelBytes, LabelBytes),
                    Layout = layout.Pipeline,
                    Compute = new WgpuComputeState { Module = Module, EntryPoint = new WgpuStringView { Data = main, Length = 4 } },
                };
                pipeline = _device._api.DeviceCreateComputePipeline(_device._device, &descriptor);
                _pipelines.Add(unfilterable, pipeline);
            }

            return pipeline;
        }

        protected override void Release()
        {
            foreach (var pipeline in _pipelines.Values)
            {
                if (pipeline != 0)
                {
                    _device._api.ComputePipelineRelease(pipeline);
                }
            }

            _pipelines.Clear();
            _device.ReleaseLayouts(Info);
            _device._api.ShaderModuleRelease(Module);
        }
    }

    private sealed class ComputeInfo : WgpuProgramBase
    {
    }

    /// <summary>Compiles a compute program from a <see cref="ShaderLanguage.Glsl450"/> compute stage. It can declare
    /// uniform blocks (set 0), textures with samplers (set 1) and storage buffers (set 2), bound through
    /// <see cref="SetUniformBuffer"/>, <see cref="SetTexture"/> and <see cref="SetStorageBuffer"/>.</summary>
    public override GpuComputeProgram CreateComputeProgram(ComputeProgramDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var info = new ComputeInfo { Id = _nextProgramId++, Label = descriptor.Label ?? "" };
        Reflect(descriptor.Source, info);
        var module = Compile(Wgpu.StageCompute, descriptor.Source, descriptor.Label, descriptor.Source);
        return new WgpuComputeProgram(this, info, module);
    }

    /// <inheritdoc/>
    public override void SetStorageBuffer(int slot, GpuBuffer buffer, int offset = 0, int size = 0)
    {
        var b = (WgpuBuffer)buffer;
        if (b.Usage != BufferUsage.Storage)
        {
            throw new ArgumentException("Only a buffer created with BufferUsage.Storage can be bound to a storage slot.", nameof(buffer));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, MaxStorageSlots);
        _storageBuffer[slot] = b;
        _storageOffset[slot] = offset;
        _storageSize[slot] = size;
    }

    /// <inheritdoc/>
    public override void DispatchCompute(GpuComputeProgram program, int groupsX, int groupsY = 1, int groupsZ = 1)
    {
        var p = (WgpuComputeProgram)program;
        if (_pass != 0)
        {
            throw new InvalidOperationException("DispatchCompute cannot be called inside a render pass.");
        }

        if (groupsX <= 0 || groupsY <= 0 || groupsZ <= 0)
        {
            return;
        }

        var info = p.Info;
        foreach (var slot in info.StorageSlots)
        {
            if (_storageBuffer[slot] is null)
            {
                throw new InvalidOperationException($"The compute program '{info.Label}' uses storage slot {slot}, which has no buffer (SetStorageBuffer).");
            }
        }

        var unfilterable = UnfilterableUnits(info);
        var layout = GetLayout(info, unfilterable);
        // Not inside an error scope: a scope would hide from Submit that the commands are invalid.
        var errors = _errorSerial;
        var reported = _errors.Count;
        DispatchCore(this, new DispatchRequest(p, layout, unfilterable, groupsX, groupsY, groupsZ));
        if (errors != _errorSerial)
        {
            var error = _errors.Count > reported ? _errors[reported] : "a validation error";
            _errors.RemoveRange(reported, _errors.Count - reported);
            throw new InvalidOperationException($"The compute program '{info.Label}' could not run: {error}");
        }

        // The render state caches refer to the render program's groups.
        _groupsProgram = null;
    }

    private readonly record struct DispatchRequest(WgpuComputeProgram Program, ProgramLayout Layout, uint Unfilterable, int X, int Y, int Z);

    private static void DispatchCore(WgpuDevice device, DispatchRequest request)
    {
        var api = device._api;
        var info = request.Program.Info;
        var pipeline = request.Program.Pipeline(request.Unfilterable, request.Layout);
        var uniforms = device.UniformGroup(info, request.Layout.Uniforms);
        var textures = device.TextureGroup(info, request.Layout.Textures, request.Unfilterable);
        var slots = info.Slots;
        var offsets = stackalloc uint[MaxSlots];
        for (var i = 0; i < slots.Length; i++)
        {
            offsets[i] = device._slotBuffer[slots[i]] is null ? 0u : (uint)device._slotOffset[slots[i]];
        }

        nint storage = 0;
        if (request.Layout.Storage is { } storageLayout)
        {
            var storageSlots = info.StorageSlots;
            var entries = stackalloc WgpuBindGroupEntry[storageSlots.Length];
            for (var i = 0; i < storageSlots.Length; i++)
            {
                var slot = storageSlots[i];
                var buffer = device._storageBuffer[slot]!;
                var size = device._storageSize[slot];
                entries[i] = new WgpuBindGroupEntry
                {
                    Binding = (uint)slot,
                    Buffer = buffer.Handle,
                    Offset = (ulong)device._storageOffset[slot],
                    Size = size > 0 ? (ulong)size : Wgpu.WholeSize,
                };
            }

            var descriptor = new WgpuBindGroupDescriptor { Layout = storageLayout.Handle, EntryCount = (nuint)storageSlots.Length, Entries = entries };
            storage = api.DeviceCreateBindGroup(device._device, &descriptor);
        }

        var encoder = api.DeviceCreateCommandEncoder(device._device, null);
        var pass = api.CommandEncoderBeginComputePass(encoder, null);
        api.ComputePassSetPipeline(pass, pipeline);
        api.ComputePassSetBindGroup(pass, 0, uniforms.Handle, (nuint)slots.Length, offsets);
        api.ComputePassSetBindGroup(pass, 1, textures.Handle, 0, null);
        if (storage != 0)
        {
            api.ComputePassSetBindGroup(pass, 2, storage, 0, null);
        }

        api.ComputePassDispatchWorkgroups(pass, (uint)request.X, (uint)request.Y, (uint)request.Z);
        api.ComputePassEnd(pass);
        api.ComputePassRelease(pass);
        device.Submit(encoder);
        if (storage != 0)
        {
            api.BindGroupRelease(storage);
        }
    }

    /// <inheritdoc/>
    public override void ReadBuffer(GpuBuffer buffer, int offset, Span<byte> data)
    {
        var b = (WgpuBuffer)buffer;
        if (b.Usage != BufferUsage.Storage)
        {
            throw new ArgumentException("Only a buffer created with BufferUsage.Storage can be read back.", nameof(buffer));
        }

        if (offset < 0 || offset + data.Length > b.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The read does not fit in the buffer.");
        }

        if (data.IsEmpty)
        {
            return;
        }

        Flush();

        // Buffer copies work in units of four bytes.
        var start = offset & ~3;
        var size = (ulong)((offset + data.Length + 3) & ~3) - (ulong)start;
        EnsureReadback(size);
        var encoder = _api.DeviceCreateCommandEncoder(_device, null);
        _api.CommandEncoderCopyBufferToBuffer(encoder, b.Handle, (ulong)start, _readback, 0, size);
        Submit(encoder);
        var mapped = MapReadback(size);
        try
        {
            new ReadOnlySpan<byte>(mapped + (offset - start), data.Length).CopyTo(data);
        }
        finally
        {
            _api.BufferUnmap(_readback);
        }
    }
}
