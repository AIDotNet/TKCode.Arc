namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

// Passes, the bindings of a draw (bind groups), and the draws.
public sealed unsafe partial class WgpuDevice
{
    // The pass that is open, and what it takes to open it again after an interruption (SplitPass).
    private nint _encoder;
    private nint _pass;
    private int _passSerial;
    private WgpuFramebuffer? _passTarget;
    private int _viewportX, _viewportY, _viewportWidth, _viewportHeight;
    private bool _scissorEnabled;
    private int _scissorX, _scissorY, _scissorWidth, _scissorHeight;
    private readonly List<string> _debugGroups = [];
    private int _passDebugGroups;

    // What the renderer selected. It outlives passes, as OpenGL state does.
    private WgpuProgram? _program;
    private RenderState _state;
    private int _stateId;
    private bool _stateValid;
    private WgpuVertexInput? _vertexInput;
    private readonly WgpuBuffer?[] _slotBuffer = new WgpuBuffer?[MaxSlots];
    private readonly int[] _slotOffset = new int[MaxSlots];
    private readonly int[] _slotSize = new int[MaxSlots];
    private readonly WgpuTexture?[] _unitTexture = new WgpuTexture?[MaxUnits];
    private readonly WgpuSampler?[] _unitSampler = new WgpuSampler?[MaxUnits];
    private readonly int[] _drawConstants = new int[4];

    // What the open pass has been told, so that unchanged state is not sent again.
    private nint _appliedPipeline;
    private BindGroup? _appliedUniforms;
    private BindGroup? _appliedTextures;
    private readonly uint[] _appliedOffsets = new uint[MaxSlots];
    private WgpuVertexInput? _appliedInput;
    private bool _constantsDirty;

    // The bind groups of the current program and bindings; rebuilt when one of them changes.
    private bool _uniformsDirty = true;
    private bool _texturesDirty = true;
    private WgpuProgramBase? _groupsProgram;
    private BindGroup? _uniformGroup;
    private BindGroup? _textureGroup;
    private uint _unfilterable;
    private PipelineKey _lastPipelineKey;
    private nint _lastPipeline;
    private bool _lastPipelineValid;

    private readonly Dictionary<ulong, BindGroup> _bindGroups = [];
    private readonly long[] _keyScratch = new long[1 + (MaxUnits * 2)];
    private bool _bindGroupsStale;

    /// <summary>A bind group and what it was made of. Groups are found again by their contents, so a material that is
    /// drawn every frame creates its group once.</summary>
    private sealed class BindGroup
    {
        public nint Handle;
        public required long[] Key;
        public required GpuResource?[] Resources;
        public BindGroup? Next;
    }

    // ---- Passes ----

    /// <inheritdoc/>
    public override void BeginPass(in RenderPassDescriptor pass)
    {
        if (_pass != 0)
        {
            EndPass();
        }

        var target = Target(pass.Target);
        _passTarget = target;
        _viewportX = pass.ViewportX;
        _viewportY = pass.ViewportY;
        _viewportWidth = pass.ViewportWidth > 0 ? pass.ViewportWidth : target.Width;
        _viewportHeight = pass.ViewportHeight > 0 ? pass.ViewportHeight : target.Height;
        _scissorEnabled = pass.ScissorToViewport;
        _scissorX = _viewportX;
        _scissorY = _viewportY;
        _scissorWidth = _viewportWidth;
        _scissorHeight = _viewportHeight;
        if (_bindGroupsStale)
        {
            SweepBindGroups();
        }

        // A WebGPU pass clears whole attachments. A clear that is limited to part of the target is drawn instead.
        var partial = pass.ScissorToViewport && (_viewportX != 0 || _viewportY != 0 || _viewportWidth != target.Width || _viewportHeight != target.Height);
        OpenPass(target, pass.ClearColor && !partial, pass.ClearColorValue, pass.ClearDepth && !partial, pass.ClearDepthValue, pass.ClearStencil && !partial);
        if (partial && (pass.ClearColor || pass.ClearDepth))
        {
            DrawClear(target, pass.ClearColor, pass.ClearColorValue, pass.ClearDepth, pass.ClearDepthValue);
        }

        Statistics.Passes++;
    }

    private void OpenPass(WgpuFramebuffer target, bool clearColor, System.Numerics.Vector4 color, bool clearDepth, float depth, bool clearStencil)
    {
        _encoder = _api.DeviceCreateCommandEncoder(_device, null);
        var colors = stackalloc WgpuRenderPassColorAttachment[Math.Max(1, target.Colors.Length)];
        for (var i = 0; i < target.Colors.Length; i++)
        {
            colors[i] = new WgpuRenderPassColorAttachment
            {
                View = target.ColorViews[i],
                DepthSlice = target.ColorSlices[i],
                LoadOp = clearColor ? Wgpu.LoadOpClear : Wgpu.LoadOpLoad,
                StoreOp = Wgpu.StoreOpStore,
                ClearValue = new WgpuColor { R = color.X, G = color.Y, B = color.Z, A = color.W },
            };
        }

        var depthAttachment = new WgpuRenderPassDepthStencilAttachment
        {
            View = target.DepthView,
            DepthLoadOp = clearDepth ? Wgpu.LoadOpClear : Wgpu.LoadOpLoad,
            DepthStoreOp = Wgpu.StoreOpStore,
            DepthClearValue = Math.Clamp(depth, 0f, 1f),
            StencilLoadOp = target.HasStencil ? (clearStencil ? Wgpu.LoadOpClear : Wgpu.LoadOpLoad) : 0,
            StencilStoreOp = target.HasStencil ? Wgpu.StoreOpStore : 0,
        };
        var labelBytes = stackalloc byte[LabelBytes];
        var descriptor = new WgpuRenderPassDescriptor
        {
            // Debug groups opened outside a pass name the passes inside them.
            Label = Label(_debugGroups.Count > 0 ? _debugGroups[^1] : target.Label, labelBytes, LabelBytes),
            ColorAttachmentCount = (nuint)target.Colors.Length,
            ColorAttachments = colors,
            DepthStencilAttachment = target.DepthView == 0 ? null : &depthAttachment,
        };
        _pass = _api.CommandEncoderBeginRenderPass(_encoder, &descriptor);
        _passSerial++;
        _passDebugGroups = 0;
        foreach (var texture in target.Colors)
        {
            texture.LastUsedPass = _passSerial;
        }

        if (target.Depth is not null)
        {
            target.Depth.LastUsedPass = _passSerial;
        }

        // Row 0 is the bottom row in the device's convention, and the top row in WebGPU's: the same row either way.
        _api.RenderPassSetViewport(_pass, _viewportX, _viewportY, _viewportWidth, _viewportHeight, 0f, 1f);
        ApplyScissor();
        _appliedPipeline = 0;
        _appliedUniforms = null;
        _appliedTextures = null;
        _appliedInput = null;
        _constantsDirty = true;
    }

    private void ApplyScissor()
    {
        var target = _passTarget!;
        var x0 = _scissorEnabled ? Math.Clamp(_scissorX, 0, target.Width) : 0;
        var y0 = _scissorEnabled ? Math.Clamp(_scissorY, 0, target.Height) : 0;
        var x1 = _scissorEnabled ? Math.Clamp(_scissorX + _scissorWidth, x0, target.Width) : target.Width;
        var y1 = _scissorEnabled ? Math.Clamp(_scissorY + _scissorHeight, y0, target.Height) : target.Height;
        _api.RenderPassSetScissorRect(_pass, (uint)x0, (uint)y0, (uint)(x1 - x0), (uint)(y1 - y0));
    }

    /// <inheritdoc/>
    public override void EndPass()
    {
        if (_pass == 0)
        {
            return;
        }

        for (; _passDebugGroups > 0; _passDebugGroups--)
        {
            _api.RenderPassPopDebugGroup(_pass);
        }

        ClosePass();
        _passTarget = null;
    }

    private void ClosePass()
    {
        _api.RenderPassEnd(_pass);
        _api.RenderPassRelease(_pass);
        _pass = 0;
        Submit(_encoder);
        _encoder = 0;
    }

    private void Submit(nint encoder)
    {
        // A validation error while the commands were recorded makes the command buffer invalid, and wgpu-native ends
        // the process when one is submitted. The commands are dropped instead; the error has been reported.
        var errors = _errorSerial;
        var commands = _api.CommandEncoderFinish(encoder, null);
        if (commands != 0)
        {
            if (errors == _errorSerial)
            {
                _api.QueueSubmit(_queue, 1, &commands);
            }

            _api.CommandBufferRelease(commands);
        }

        _api.CommandEncoderRelease(encoder);
    }

    /// <summary>Sends the draws recorded so far and opens the pass again, keeping its contents. Needed before
    /// something that takes effect at once (a buffer or texture write) when the recorded draws must see the state
    /// from before it.</summary>
    private void SplitPass() => ResumePass(SuspendPass());

    /// <summary>Sends the draws of the open pass, if there is one, so that something else can be recorded
    /// (a copy between framebuffers). <see cref="ResumePass"/> opens it again.</summary>
    private (WgpuFramebuffer? Target, int DebugGroups) SuspendPass()
    {
        if (_pass == 0)
        {
            return default;
        }

        var groups = _passDebugGroups;
        for (var i = 0; i < groups; i++)
        {
            _api.RenderPassPopDebugGroup(_pass);
        }

        ClosePass();
        return (_passTarget, groups);
    }

    private void ResumePass((WgpuFramebuffer? Target, int DebugGroups) suspended)
    {
        if (suspended.Target is null)
        {
            return;
        }

        OpenPass(suspended.Target, false, default, false, 1f, false);
        for (var i = Math.Max(0, _debugGroups.Count - suspended.DebugGroups); i < _debugGroups.Count; i++)
        {
            PushPassDebugGroup(_debugGroups[i]);
        }
    }

    /// <summary>Sends everything recorded so far to the GPU.</summary>
    private void Flush()
    {
        if (_pass != 0)
        {
            SplitPass();
        }
    }

    private void DisposePasses()
    {
        if (_pass != 0)
        {
            EndPass();
        }

        foreach (var first in _bindGroups.Values)
        {
            for (var group = first; group is not null; group = group.Next)
            {
                _api.BindGroupRelease(group.Handle);
            }
        }

        _bindGroups.Clear();
        foreach (var pipeline in _pipelines.Values)
        {
            if (pipeline != 0)
            {
                _api.RenderPipelineRelease(pipeline);
            }
        }

        _pipelines.Clear();
        foreach (var layout in _bindLayouts.Values)
        {
            _api.BindGroupLayoutRelease(layout.Handle);
        }

        _bindLayouts.Clear();
    }

    /// <inheritdoc/>
    public override void SetViewport(int x, int y, int width, int height)
    {
        _viewportX = x;
        _viewportY = y;
        _viewportWidth = width;
        _viewportHeight = height;
        if (_pass != 0)
        {
            _api.RenderPassSetViewport(_pass, x, y, width, height, 0f, 1f);
        }
    }

    /// <inheritdoc/>
    public override void SetScissor(int x, int y, int width, int height, bool enabled)
    {
        _scissorEnabled = enabled;
        _scissorX = x;
        _scissorY = y;
        _scissorWidth = width;
        _scissorHeight = height;
        if (_pass != 0)
        {
            ApplyScissor();
        }
    }

    // ---- State ----

    /// <inheritdoc/>
    public override void SetProgram(GpuProgram program)
    {
        var p = (WgpuProgram)program;
        if (!ReferenceEquals(_program, p))
        {
            _program = p;
            Statistics.ProgramSwitches++;
        }
    }

    /// <inheritdoc/>
    public override void SetRenderState(in RenderState state)
    {
        if (_stateValid && _state.Equals(state))
        {
            return;
        }

        _state = state;
        _stateId = StateId(state);
        _stateValid = true;
        Statistics.StateChanges++;
    }

    /// <inheritdoc/>
    public override void SetUniformBuffer(int slot, GpuBuffer buffer, int offset, int size)
    {
        var b = (WgpuBuffer)buffer;
        if (!ReferenceEquals(_slotBuffer[slot], b) || _slotSize[slot] != size)
        {
            _slotBuffer[slot] = b;
            _slotSize[slot] = size;
            _uniformsDirty = true;
        }

        _slotOffset[slot] = offset;
    }

    /// <inheritdoc/>
    public override void SetTexture(int unit, GpuTexture texture, GpuSampler sampler)
    {
        var t = (WgpuTexture)texture;
        var s = (WgpuSampler)sampler;
        if (!ReferenceEquals(_unitTexture[unit], t))
        {
            _unitTexture[unit] = t;
            _texturesDirty = true;
            Statistics.TextureBinds++;
        }

        if (!ReferenceEquals(_unitSampler[unit], s))
        {
            _unitSampler[unit] = s;
            _texturesDirty = true;
        }
    }

    /// <inheritdoc/>
    public override void SetDrawConstants(int c0, int c1, int c2, int c3)
    {
        var d = _drawConstants;
        if (d[0] != c0 || d[1] != c1 || d[2] != c2 || d[3] != c3)
        {
            d[0] = c0;
            d[1] = c1;
            d[2] = c2;
            d[3] = c3;
            _constantsDirty = true;
        }
    }

    /// <inheritdoc/>
    public override void SetVertexInput(GpuVertexInput input) => _vertexInput = (WgpuVertexInput)input;

    /// <summary>Forgets what the device believes is bound. Nothing else can change a WebGPU device's state, so this
    /// only makes the next draw send everything again.</summary>
    public override void InvalidateState()
    {
        _stateValid = false;
        _uniformsDirty = true;
        _texturesDirty = true;
        _appliedPipeline = 0;
        _appliedUniforms = null;
        _appliedTextures = null;
        _appliedInput = null;
        _constantsDirty = true;
        _lastPipelineValid = false;
    }

    private void ForgetBuffer(WgpuBuffer buffer)
    {
        for (var i = 0; i < MaxSlots; i++)
        {
            if (ReferenceEquals(_slotBuffer[i], buffer))
            {
                _slotBuffer[i] = null;
                _uniformsDirty = true;
            }
        }

        for (var i = 0; i < MaxStorageSlots; i++)
        {
            if (ReferenceEquals(_storageBuffer[i], buffer))
            {
                _storageBuffer[i] = null;
            }
        }
    }

    private void ForgetTexture(WgpuTexture texture)
    {
        for (var i = 0; i < MaxUnits; i++)
        {
            if (ReferenceEquals(_unitTexture[i], texture))
            {
                _unitTexture[i] = null;
                _texturesDirty = true;
            }
        }
    }

    private void ForgetSampler(WgpuSampler sampler)
    {
        for (var i = 0; i < MaxUnits; i++)
        {
            if (ReferenceEquals(_unitSampler[i], sampler))
            {
                _unitSampler[i] = null;
                _texturesDirty = true;
            }
        }
    }

    // ---- Bind groups ----

    /// <summary>The bind group of the uniform blocks a program declares. Offsets are not part of it: they are given
    /// when it is set (dynamic offsets), so the views of a frame share one group.</summary>
    private BindGroup UniformGroup(WgpuProgramBase program, BindLayout layout)
    {
        var slots = program.Slots;
        var key = _keyScratch;
        key[0] = layout.Id;
        for (var i = 0; i < slots.Length; i++)
        {
            var buffer = _slotBuffer[slots[i]] ?? _dummyUniform;
            key[1 + i] = ((long)buffer.Id << 32) | (uint)BindingSize(buffer, _slotSize[slots[i]]);
        }

        var length = 1 + slots.Length;
        if (FindGroup(key, length, out var hash) is { } found)
        {
            return found;
        }

        var entries = stackalloc WgpuBindGroupEntry[Math.Max(1, slots.Length)];
        var resources = new GpuResource?[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            var buffer = _slotBuffer[slots[i]] ?? _dummyUniform;
            resources[i] = buffer;
            entries[i] = new WgpuBindGroupEntry { Binding = (uint)slots[i], Buffer = buffer.Handle, Offset = 0, Size = (ulong)BindingSize(buffer, _slotSize[slots[i]]) };
        }

        return AddGroup(layout, entries, slots.Length, key.AsSpan(0, length).ToArray(), hash, resources);
    }

    private static int BindingSize(WgpuBuffer buffer, int size) => size > 0 && size <= buffer.SizeInBytes ? size : buffer.SizeInBytes;

    /// <summary>Which of a program's texture units have a texture bound that WebGPU does not filter. Such a unit is
    /// bound as "unfilterable" with a nearest sampler, which is another bind group layout.</summary>
    private uint UnfilterableUnits(WgpuProgramBase program)
    {
        uint mask = 0;
        foreach (var unit in program.Units)
        {
            if (!program.UnitShadow[unit] && _unitTexture[unit] is { } texture && Matches(program, unit, texture) && !IsFilterable(texture))
            {
                mask |= 1u << unit;
            }
        }

        return mask;
    }

    private bool IsFilterable(WgpuTexture texture)
    {
        var format = texture.Descriptor.Format;
        return !format.IsDepth() && (_float32Filterable || format is not (TextureFormat.R32F or TextureFormat.RG32F or TextureFormat.RGBA32F));
    }

    /// <summary>True when the bound texture can be what the program samples at the unit. Something else may be left
    /// there from another program; a default texture then takes its place, since WebGPU checks every declared binding.</summary>
    private static bool Matches(WgpuProgramBase program, int unit, WgpuTexture texture)
    {
        var d = texture.Descriptor;
        var dimension = d.Dimension switch
        {
            TextureDimension.Cube => Wgpu.ViewDimensionCube,
            TextureDimension.Texture2DArray => Wgpu.ViewDimension2DArray,
            TextureDimension.Texture3D => Wgpu.ViewDimension3D,
            _ => Wgpu.ViewDimension2D,
        };
        return d.Samples == 1 && dimension == program.UnitDimension[unit] && (!program.UnitShadow[unit] || d.Format.IsDepth());
    }

    private BindGroup TextureGroup(WgpuProgramBase program, BindLayout layout, uint unfilterable)
    {
        var units = program.Units;
        var key = _keyScratch;
        var views = stackalloc nint[MaxUnits];
        var samplers = stackalloc nint[MaxUnits];
        key[0] = layout.Id;
        for (var i = 0; i < units.Length; i++)
        {
            var unit = units[i];
            var shadow = program.UnitShadow[unit];
            var texture = _unitTexture[unit];
            if (texture is null || !Matches(program, unit, texture))
            {
                texture = shadow ? _dummyDepth : program.UnitDimension[unit] switch
                {
                    Wgpu.ViewDimensionCube => _dummyCube,
                    Wgpu.ViewDimension2DArray => _dummyArray,
                    Wgpu.ViewDimension3D => _dummy3D,
                    _ => _dummy2D,
                };
            }

            var sampler = _unitSampler[unit];
            if (sampler is null || sampler.IsComparison != shadow)
            {
                sampler = shadow ? _compareSampler : _linearSampler;
            }

            var plain = (unfilterable & (1u << unit)) != 0;
            views[i] = texture.DefaultView;
            samplers[i] = plain ? sampler.NonFiltering : sampler.Handle;
            key[1 + (i * 2)] = texture.Id;
            key[2 + (i * 2)] = ((long)sampler.Id << 1) | (plain ? 1L : 0L);
        }

        var length = 1 + (units.Length * 2);
        if (FindGroup(key, length, out var hash) is { } found)
        {
            return found;
        }

        var entries = stackalloc WgpuBindGroupEntry[Math.Max(1, units.Length * 2)];
        var kept = new GpuResource?[units.Length * 2];
        for (var i = 0; i < units.Length; i++)
        {
            var unit = units[i];
            entries[i * 2] = new WgpuBindGroupEntry { Binding = (uint)(unit * 2), TextureView = views[i] };
            entries[(i * 2) + 1] = new WgpuBindGroupEntry { Binding = (uint)((unit * 2) + 1), Sampler = samplers[i] };
            var texture = _unitTexture[unit];
            kept[i * 2] = texture is not null && texture.Id == key[1 + (i * 2)] ? texture : null;
            var sampler = _unitSampler[unit];
            kept[(i * 2) + 1] = sampler is not null && sampler.Id == key[2 + (i * 2)] >> 1 ? sampler : null;
        }

        return AddGroup(layout, entries, units.Length * 2, key.AsSpan(0, length).ToArray(), hash, kept);
    }

    private BindGroup? FindGroup(long[] key, int length, out ulong hash)
    {
        hash = 14695981039346656037UL;
        for (var i = 0; i < length; i++)
        {
            hash = (hash ^ (ulong)key[i]) * 1099511628211UL;
            hash ^= hash >> 29;
        }

        if (_bindGroups.TryGetValue(hash, out var group))
        {
            for (; group is not null; group = group.Next)
            {
                if (group.Key.AsSpan().SequenceEqual(key.AsSpan(0, length)))
                {
                    return group;
                }
            }
        }

        return null;
    }

    private BindGroup AddGroup(BindLayout layout, WgpuBindGroupEntry* entries, int count, long[] key, ulong hash, GpuResource?[] resources)
    {
        var descriptor = new WgpuBindGroupDescriptor { Layout = layout.Handle, EntryCount = (nuint)count, Entries = entries };
        var group = new BindGroup { Handle = _api.DeviceCreateBindGroup(_device, &descriptor), Key = key, Resources = resources };
        _bindGroups.TryGetValue(hash, out var first);
        group.Next = first;
        _bindGroups[hash] = group;
        return group;
    }

    /// <summary>Drops the bind groups that hold a disposed buffer, texture or sampler.</summary>
    private void SweepBindGroups()
    {
        _bindGroupsStale = false;
        List<ulong>? emptied = null;
        List<(ulong Hash, BindGroup First)>? replaced = null;
        foreach (var (hash, first) in _bindGroups)
        {
            BindGroup? head = null;
            BindGroup? tail = null;
            var changed = false;
            for (var group = first; group is not null;)
            {
                var next = group.Next;
                var dead = false;
                foreach (var resource in group.Resources)
                {
                    dead |= resource is { IsDisposed: true };
                }

                if (dead)
                {
                    _api.BindGroupRelease(group.Handle);
                    changed = true;
                }
                else
                {
                    group.Next = null;
                    if (tail is null)
                    {
                        head = group;
                    }
                    else
                    {
                        tail.Next = group;
                    }

                    tail = group;
                }

                group = next;
            }

            if (head is null)
            {
                (emptied ??= []).Add(hash);
            }
            else if (changed)
            {
                (replaced ??= []).Add((hash, head));
            }
        }

        if (emptied is not null)
        {
            foreach (var hash in emptied)
            {
                _bindGroups.Remove(hash);
            }
        }

        if (replaced is not null)
        {
            foreach (var (hash, first) in replaced)
            {
                _bindGroups[hash] = first;
            }
        }

        _uniformsDirty = true;
        _texturesDirty = true;
        _uniformGroup = null;
        _textureGroup = null;
        _appliedUniforms = null;
        _appliedTextures = null;
    }

    private void MarkUsed(BindGroup group)
    {
        foreach (var resource in group.Resources)
        {
            if (resource is WgpuBuffer buffer)
            {
                buffer.LastUsedPass = _passSerial;
            }
            else if (resource is WgpuTexture texture)
            {
                texture.LastUsedPass = _passSerial;
            }
        }
    }

    // ---- Draws ----

    private bool PrepareDraw(PrimitiveTopology topology)
    {
        var program = _program;
        var input = _vertexInput;
        if (_pass == 0 || program is null || input is null)
        {
            return false;
        }

        if (!_stateValid)
        {
            SetRenderState(RenderState.Default);
        }

        var info = program.Info;
        if (_texturesDirty || _uniformsDirty || !ReferenceEquals(_groupsProgram, info))
        {
            var texturesChanged = _texturesDirty || !ReferenceEquals(_groupsProgram, info);
            if (texturesChanged)
            {
                _unfilterable = UnfilterableUnits(info);
            }

            var groupLayout = GetLayout(info, _unfilterable);
            if (texturesChanged)
            {
                _textureGroup = TextureGroup(info, groupLayout.Textures, _unfilterable);
            }

            _uniformGroup = UniformGroup(info, groupLayout.Uniforms);
            _groupsProgram = info;
            _texturesDirty = false;
            _uniformsDirty = false;
        }

        var target = _passTarget!;
        var strip = topology is PrimitiveTopology.TriangleStrip or PrimitiveTopology.LineStrip ? input.IndexFormat : Wgpu.IndexFormatUndefined;
        var key = new PipelineKey(info.Id, _unfilterable, input.Layout.Id, _stateId, (int)topology, strip, target.FormatsId);
        if (!_lastPipelineValid || key != _lastPipelineKey)
        {
            _lastPipeline = GetPipeline(key, program, GetLayout(info, _unfilterable), input.Layout, target);
            _lastPipelineKey = key;
            _lastPipelineValid = true;
        }

        var pipeline = _lastPipeline;
        if (pipeline == 0)
        {
            return false;
        }

        if (pipeline != _appliedPipeline)
        {
            _api.RenderPassSetPipeline(_pass, pipeline);
            _appliedPipeline = pipeline;
            _constantsDirty = true;
        }

        var uniforms = _uniformGroup!;
        var slots = info.Slots;
        var offsets = stackalloc uint[MaxSlots];
        var offsetsChanged = false;
        for (var i = 0; i < slots.Length; i++)
        {
            offsets[i] = _slotBuffer[slots[i]] is null ? 0u : (uint)_slotOffset[slots[i]];
            offsetsChanged |= offsets[i] != _appliedOffsets[i];
        }

        if (!ReferenceEquals(uniforms, _appliedUniforms) || offsetsChanged)
        {
            _api.RenderPassSetBindGroup(_pass, 0, uniforms.Handle, (nuint)slots.Length, offsets);
            for (var i = 0; i < slots.Length; i++)
            {
                _appliedOffsets[i] = offsets[i];
            }

            _appliedUniforms = uniforms;
            MarkUsed(uniforms);
        }

        var textures = _textureGroup!;
        if (!ReferenceEquals(textures, _appliedTextures))
        {
            _api.RenderPassSetBindGroup(_pass, 1, textures.Handle, 0, null);
            _appliedTextures = textures;
            MarkUsed(textures);
        }

        if (!ReferenceEquals(input, _appliedInput))
        {
            var buffers = input.Buffers;
            for (var i = 0; i < buffers.Length; i++)
            {
                _api.RenderPassSetVertexBuffer(_pass, (uint)i, buffers[i].Handle, 0, Wgpu.WholeSize);
                buffers[i].LastUsedPass = _passSerial;
            }

            if (input.Index is { } index)
            {
                _api.RenderPassSetIndexBuffer(_pass, index.Handle, input.IndexFormat, 0, Wgpu.WholeSize);
                index.LastUsedPass = _passSerial;
            }

            _appliedInput = input;
        }

        if (_constantsDirty && info.UsesDrawConstants)
        {
            fixed (int* constants = _drawConstants)
            {
                _api.RenderPassSetImmediates(_pass, 0, constants, DrawConstantBytes);
            }

            _constantsDirty = false;
        }

        return true;
    }

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
    public override void Draw(PrimitiveTopology topology, int firstVertex, int vertexCount, int instanceCount)
    {
        if (vertexCount <= 0 || instanceCount <= 0 || !PrepareDraw(topology))
        {
            return;
        }

        _api.RenderPassDraw(_pass, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, 0);
        Count(topology, vertexCount, instanceCount);
    }

    /// <inheritdoc/>
    public override void DrawIndexed(PrimitiveTopology topology, int firstIndex, int indexCount, int instanceCount)
    {
        if (indexCount <= 0 || instanceCount <= 0 || !PrepareDraw(topology))
        {
            return;
        }

        if (_vertexInput!.Index is null)
        {
            throw new InvalidOperationException("DrawIndexed needs a vertex input with an index buffer.");
        }

        _api.RenderPassDrawIndexed(_pass, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, 0, 0);
        Count(topology, indexCount, instanceCount);
    }

    // ---- Debug groups ----

    /// <inheritdoc/>
    public override void PushDebugGroup(string name)
    {
        _debugGroups.Add(name);
        if (_pass != 0)
        {
            PushPassDebugGroup(name);
        }
    }

    private void PushPassDebugGroup(string name)
    {
        var bytes = stackalloc byte[LabelBytes];
        var label = Label(name, bytes, LabelBytes);
        if (label.Length > 0)
        {
            _api.RenderPassPushDebugGroup(_pass, label);
            _passDebugGroups++;
        }
    }

    /// <inheritdoc/>
    public override void PopDebugGroup()
    {
        if (_debugGroups.Count == 0)
        {
            return;
        }

        _debugGroups.RemoveAt(_debugGroups.Count - 1);
        if (_pass != 0 && _passDebugGroups > 0)
        {
            _api.RenderPassPopDebugGroup(_pass);
            _passDebugGroups--;
        }
    }
}
