using System.Globalization;
using System.Text;

namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

// Shader modules, what they bind (read from their declarations), bind group layouts and render pipelines.
public sealed unsafe partial class WgpuDevice
{
    private const int DrawConstantBytes = 16;
    private const ulong AllStages = Wgpu.StageVertex | Wgpu.StageFragment | Wgpu.StageCompute;

    private readonly Dictionary<string, BindLayout> _bindLayouts = new(StringComparer.Ordinal);
    private readonly Dictionary<PipelineKey, nint> _pipelines = [];
    private int _nextProgramId = 1;

    /// <summary>A bind group layout, shared by every program that binds the same things.</summary>
    private sealed class BindLayout
    {
        public nint Handle;
        public int Id;
    }

    /// <summary>The pipeline layout of a program for one filtering class of its textures.</summary>
    private sealed class ProgramLayout
    {
        public nint Pipeline;
        public required BindLayout Uniforms;
        public required BindLayout Textures;
        public BindLayout? Storage;
    }

    /// <summary>What a program binds, by the device's slots and units.</summary>
    private abstract class WgpuProgramBase
    {
        public int Id;
        public string Label = "";

        // Uniform slots and texture units the sources declare, ascending.
        public int[] Slots = [];
        public int[] Units = [];

        // Per unit: the view dimension the shader samples, and whether it compares depth.
        public readonly uint[] UnitDimension = new uint[MaxUnits];
        public readonly bool[] UnitShadow = new bool[MaxUnits];
        public int[] StorageSlots = [];
        public readonly bool[] StorageReadOnly = new bool[MaxStorageSlots];
        public bool UsesDrawConstants;

        // A bit per color location the fragment stage writes.
        public uint ColorOutputs;
        public readonly Dictionary<uint, ProgramLayout> Layouts = [];
    }

    private sealed class WgpuProgram : GpuProgram
    {
        private readonly WgpuDevice _device;

        public WgpuProgram(WgpuDevice device, ProgramInfo info, nint vertex, nint fragment)
        {
            _device = device;
            Info = info;
            Vertex = vertex;
            Fragment = fragment;
        }

        public ProgramInfo Info { get; }

        public nint Vertex { get; }

        public nint Fragment { get; }

        public override bool IsReady => true;

        protected override void Release()
        {
            _device.ForgetProgram(this);
            _device._api.ShaderModuleRelease(Vertex);
            _device._api.ShaderModuleRelease(Fragment);
        }
    }

    private sealed class ProgramInfo : WgpuProgramBase
    {
    }

    /// <summary>Compiles and links a program from <see cref="ShaderLanguage.Glsl450"/> sources. The bindings are the
    /// ones the sources declare; the descriptor's block and sampler tables are not needed.</summary>
    public override GpuProgram CreateProgram(ProgramDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // Keep OpenGL's picture orientation (row 0 at the bottom): WebGPU puts clip-space +Y at row 0, so the vertex
        // stage ends by negating Y. Pipelines swap the front face to match.
        const string Main = "void main()";
        if (!descriptor.VertexSource.Contains(Main, StringComparison.Ordinal))
        {
            throw new ShaderCompilationException($"The vertex shader of '{descriptor.Label}' has no '{Main}'.", descriptor.VertexSource);
        }

        var vertexSource = descriptor.VertexSource.Replace(Main, "void arc_vertex_main()", StringComparison.Ordinal)
            + "\nvoid main() {\n    arc_vertex_main();\n    gl_Position.y = -gl_Position.y;\n}\n";

        // The point coordinate runs along the rows; with the rows upside down it has to be turned back.
        var fragmentSource = descriptor.FragmentSource;
        if (fragmentSource.Contains("gl_PointCoord", StringComparison.Ordinal))
        {
            fragmentSource = InsertAfterVersion(fragmentSource, "#define gl_PointCoord vec2(gl_PointCoord.x, 1.0 - gl_PointCoord.y)\n");
        }

        var info = new ProgramInfo { Id = _nextProgramId++, Label = descriptor.Label ?? "" };
        Reflect(descriptor.VertexSource, info);
        Reflect(fragmentSource, info, fragmentStage: true);
        var vertex = Compile(Wgpu.StageVertex, vertexSource, descriptor.Label, descriptor.VertexSource);
        nint fragment;
        try
        {
            fragment = Compile(Wgpu.StageFragment, fragmentSource, descriptor.Label, descriptor.FragmentSource);
        }
        catch
        {
            _api.ShaderModuleRelease(vertex);
            throw;
        }

        return new WgpuProgram(this, info, vertex, fragment);
    }

    private static string InsertAfterVersion(string source, string text)
    {
        var at = source.StartsWith("#version", StringComparison.Ordinal) ? source.IndexOf('\n') + 1 : 0;
        return source.Insert(at, text);
    }

    private readonly record struct CompileRequest(ulong Stage, string Source, string? Label);

    private nint _compiled;

    private nint Compile(ulong stage, string source, string? label, string reportedSource)
    {
        _compiled = 0;
        var error = CaptureError(new CompileRequest(stage, source, label), &CompileCore);
        var module = _compiled;
        if (error is null && module != 0)
        {
            return module;
        }

        if (module != 0)
        {
            _api.ShaderModuleRelease(module);
        }

        var kind = stage == Wgpu.StageVertex ? "vertex" : stage == Wgpu.StageFragment ? "fragment" : "compute";
        throw new ShaderCompilationException($"The {kind} shader of '{label}' failed to compile: {error ?? "no shader module was created"}", reportedSource);
    }

    private static void CompileCore(WgpuDevice device, CompileRequest request)
    {
        var bytes = Encoding.UTF8.GetBytes(request.Source);
        var labelBytes = stackalloc byte[LabelBytes];
        fixed (byte* p = bytes)
        {
            var glsl = new WgpuShaderSourceGlsl
            {
                Chain = new WgpuChainedStruct { SType = Wgpu.STypeShaderSourceGlsl },
                Stage = request.Stage,
                Code = new WgpuStringView { Data = p, Length = (nuint)bytes.Length },
            };
            var descriptor = new WgpuShaderModuleDescriptor { NextInChain = (WgpuChainedStruct*)&glsl, Label = Label(request.Label, labelBytes, LabelBytes) };
            device._compiled = device._api.DeviceCreateShaderModule(device._device, &descriptor);
        }
    }

    private nint CompileWgsl(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* p = bytes)
        {
            var wgsl = new WgpuShaderSourceWgsl
            {
                Chain = new WgpuChainedStruct { SType = Wgpu.STypeShaderSourceWgsl },
                Code = new WgpuStringView { Data = p, Length = (nuint)bytes.Length },
            };
            var descriptor = new WgpuShaderModuleDescriptor { NextInChain = (WgpuChainedStruct*)&wgsl };
            return _api.DeviceCreateShaderModule(_device, &descriptor);
        }
    }

    // ---- What a source binds ----

    /// <summary>Reads the resource declarations of a GLSL 4.50 source: every line that starts with
    /// <c>layout(...)</c>. That is all the device needs to build bind group layouts, and it is exact because the
    /// language makes every resource name its set and binding.</summary>
    private static void Reflect(string source, WgpuProgramBase info, bool fragmentStage = false)
    {
        var slots = new List<int>(info.Slots);
        var units = new List<int>(info.Units);
        var storage = new List<int>(info.StorageSlots);
        foreach (var raw in source.AsSpan().EnumerateLines())
        {
            var line = raw.TrimStart();
            if (!line.StartsWith("layout(", StringComparison.Ordinal))
            {
                continue;
            }

            var close = line.IndexOf(')');
            if (close < 0)
            {
                continue;
            }

            var qualifiers = line[7..close];
            var rest = line[(close + 1)..].TrimStart();
            if (qualifiers.Contains("push_constant", StringComparison.Ordinal))
            {
                info.UsesDrawConstants = true;
                continue;
            }

            if (qualifiers.StartsWith("location", StringComparison.Ordinal))
            {
                var location = Qualifier(qualifiers, "location");
                if (fragmentStage && location is >= 0 and < 8 && rest.StartsWith("out ", StringComparison.Ordinal))
                {
                    info.ColorOutputs |= 1u << location;
                }

                continue;
            }

            var set = Qualifier(qualifiers, "set");
            var binding = Qualifier(qualifiers, "binding");
            if (binding < 0)
            {
                continue;
            }

            var readOnly = rest.StartsWith("readonly ", StringComparison.Ordinal);
            if (readOnly)
            {
                rest = rest[9..].TrimStart();
            }

            if (rest.StartsWith("buffer ", StringComparison.Ordinal))
            {
                if (set != 2 || binding >= MaxStorageSlots)
                {
                    throw new NotSupportedException($"Storage buffers are declared in set 2 with bindings below {MaxStorageSlots}: '{line.ToString()}'.");
                }

                Add(storage, binding);
                info.StorageReadOnly[binding] = readOnly;
                continue;
            }

            if (!rest.StartsWith("uniform ", StringComparison.Ordinal))
            {
                continue;
            }

            // The type decides what it is; a block may be named anything (even "textureParams").
            rest = rest[8..].TrimStart();
            var typeEnd = rest.IndexOfAny(' ', '{', ';');
            var type = typeEnd < 0 ? rest : rest[..typeEnd];
            if (type is "texture2D" or "textureCube" or "texture2DArray" or "texture3D")
            {
                var unit = binding / 2;
                if (set != 1 || (binding & 1) != 0 || unit >= MaxUnits)
                {
                    throw new NotSupportedException($"The texture of unit u is declared in set 1 at binding 2u, with u below {MaxUnits}: '{line.ToString()}'.");
                }

                Add(units, unit);
                info.UnitDimension[unit] = type switch
                {
                    "textureCube" => Wgpu.ViewDimensionCube,
                    "texture2DArray" => Wgpu.ViewDimension2DArray,
                    "texture3D" => Wgpu.ViewDimension3D,
                    _ => Wgpu.ViewDimension2D,
                };
            }
            else if (type is "sampler" or "samplerShadow")
            {
                var unit = binding / 2;
                if (set != 1 || (binding & 1) == 0 || unit >= MaxUnits)
                {
                    throw new NotSupportedException($"The sampler of unit u is declared in set 1 at binding 2u + 1, with u below {MaxUnits}: '{line.ToString()}'.");
                }

                Add(units, unit);
                info.UnitShadow[unit] = type is "samplerShadow";
            }
            else
            {
                if (set != 0 || binding >= MaxSlots)
                {
                    throw new NotSupportedException($"Uniform blocks are declared in set 0 with bindings below {MaxSlots}: '{line.ToString()}'.");
                }

                Add(slots, binding);
            }
        }

        slots.Sort();
        units.Sort();
        storage.Sort();
        info.Slots = [.. slots];
        info.Units = [.. units];
        info.StorageSlots = [.. storage];

        static void Add(List<int> list, int value)
        {
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }
    }

    /// <summary>The value of <c>name = N</c> in a layout qualifier list; 0 for a missing set, -1 for a missing binding.</summary>
    private static int Qualifier(ReadOnlySpan<char> qualifiers, string name)
    {
        while (!qualifiers.IsEmpty)
        {
            var comma = qualifiers.IndexOf(',');
            var item = (comma < 0 ? qualifiers : qualifiers[..comma]).Trim();
            qualifiers = comma < 0 ? default : qualifiers[(comma + 1)..];
            var equals = item.IndexOf('=');
            if (equals > 0 && item[..equals].Trim().SequenceEqual(name)
                && int.TryParse(item[(equals + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return name == "set" ? 0 : -1;
    }

    // ---- Layouts ----

    /// <summary>The layouts of a program. <paramref name="unfilterable"/> has a bit per texture unit whose bound
    /// texture WebGPU does not filter: such a unit needs another binding type, so it is part of the layout.</summary>
    private ProgramLayout GetLayout(WgpuProgramBase program, uint unfilterable)
    {
        if (program.Layouts.TryGetValue(unfilterable, out var layout))
        {
            return layout;
        }

        var key = new StringBuilder("u");
        var entries = stackalloc WgpuBindGroupLayoutEntry[Math.Max(Math.Max(program.Slots.Length, program.Units.Length * 2), Math.Max(program.StorageSlots.Length, 1))];
        for (var i = 0; i < program.Slots.Length; i++)
        {
            entries[i] = new WgpuBindGroupLayoutEntry
            {
                Binding = (uint)program.Slots[i],
                Visibility = AllStages,
                Buffer = new WgpuBufferBindingLayout { Type = Wgpu.BufferBindingUniform, HasDynamicOffset = 1 },
            };
            key.Append(program.Slots[i]).Append(',');
        }

        var uniforms = InternLayout(key.ToString(), entries, program.Slots.Length);
        key.Clear().Append('t');
        for (var i = 0; i < program.Units.Length; i++)
        {
            var unit = program.Units[i];
            var shadow = program.UnitShadow[unit];
            var plain = (unfilterable & (1u << unit)) != 0;
            entries[i * 2] = new WgpuBindGroupLayoutEntry
            {
                Binding = (uint)(unit * 2),
                Visibility = AllStages,
                Texture = new WgpuTextureBindingLayout
                {
                    SampleType = shadow ? Wgpu.SampleTypeDepth : plain ? Wgpu.SampleTypeUnfilterableFloat : Wgpu.SampleTypeFloat,
                    ViewDimension = program.UnitDimension[unit],
                },
            };
            entries[(i * 2) + 1] = new WgpuBindGroupLayoutEntry
            {
                Binding = (uint)((unit * 2) + 1),
                Visibility = AllStages,
                Sampler = new WgpuSamplerBindingLayout
                {
                    Type = shadow ? Wgpu.SamplerBindingComparison : plain ? Wgpu.SamplerBindingNonFiltering : Wgpu.SamplerBindingFiltering,
                },
            };
            key.Append(unit).Append(shadow ? 's' : plain ? 'n' : 'f').Append(program.UnitDimension[unit]).Append(',');
        }

        var textures = InternLayout(key.ToString(), entries, program.Units.Length * 2);
        BindLayout? storage = null;
        var groups = stackalloc nint[3];
        groups[0] = uniforms.Handle;
        groups[1] = textures.Handle;
        var groupCount = 2;
        if (program.StorageSlots.Length > 0)
        {
            key.Clear().Append('s');
            for (var i = 0; i < program.StorageSlots.Length; i++)
            {
                var slot = program.StorageSlots[i];
                entries[i] = new WgpuBindGroupLayoutEntry
                {
                    Binding = (uint)slot,
                    Visibility = Wgpu.StageCompute,
                    Buffer = new WgpuBufferBindingLayout { Type = program.StorageReadOnly[slot] ? Wgpu.BufferBindingReadOnlyStorage : Wgpu.BufferBindingStorage },
                };
                key.Append(slot).Append(program.StorageReadOnly[slot] ? 'r' : 'w').Append(',');
            }

            storage = InternLayout(key.ToString(), entries, program.StorageSlots.Length);
            groups[groupCount++] = storage.Handle;
        }

        var descriptor = new WgpuPipelineLayoutDescriptor
        {
            BindGroupLayoutCount = (nuint)groupCount,
            BindGroupLayouts = groups,
            ImmediateSize = program.UsesDrawConstants ? DrawConstantBytes : 0u,
        };
        layout = new ProgramLayout
        {
            Pipeline = _api.DeviceCreatePipelineLayout(_device, &descriptor),
            Uniforms = uniforms,
            Textures = textures,
            Storage = storage,
        };
        program.Layouts.Add(unfilterable, layout);
        return layout;
    }

    private BindLayout InternLayout(string key, WgpuBindGroupLayoutEntry* entries, int count)
    {
        if (!_bindLayouts.TryGetValue(key, out var layout))
        {
            var descriptor = new WgpuBindGroupLayoutDescriptor { EntryCount = (nuint)count, Entries = entries };
            layout = new BindLayout { Handle = _api.DeviceCreateBindGroupLayout(_device, &descriptor), Id = _bindLayouts.Count + 1 };
            _bindLayouts.Add(key, layout);
        }

        return layout;
    }

    private void ReleaseLayouts(WgpuProgramBase program)
    {
        foreach (var layout in program.Layouts.Values)
        {
            _api.PipelineLayoutRelease(layout.Pipeline);
        }

        program.Layouts.Clear();
    }

    // ---- Render pipelines ----

    /// <summary>Everything a WebGPU render pipeline is fixed to. The device builds one the first time a combination
    /// is drawn.</summary>
    private readonly record struct PipelineKey(int Program, uint Unfilterable, int VertexLayout, int State, int Topology, uint StripIndex, int Formats);

    private readonly Dictionary<RenderState, int> _stateIds = [];
    private readonly List<RenderState> _states = [default];

    private int StateId(in RenderState state)
    {
        if (!_stateIds.TryGetValue(state, out var id))
        {
            id = _states.Count;
            _states.Add(state);
            _stateIds.Add(state, id);
        }

        return id;
    }

    private readonly record struct PipelineRequest(PipelineKey Key, WgpuProgram Program, ProgramLayout Layout, VertexLayout Vertex, WgpuFramebuffer Target);

    private nint _createdPipeline;

    private nint GetPipeline(in PipelineKey key, WgpuProgram program, ProgramLayout layout, VertexLayout vertex, WgpuFramebuffer target)
    {
        if (_pipelines.TryGetValue(key, out var pipeline))
        {
            return pipeline;
        }

        _createdPipeline = 0;
        var error = CaptureError(new PipelineRequest(key, program, layout, vertex, target), &CreatePipelineCore);
        pipeline = _createdPipeline;
        if (error is not null)
        {
            // Nothing is drawn with it; the error is reported once (DrainErrors).
            if (pipeline != 0)
            {
                _api.RenderPipelineRelease(pipeline);
                pipeline = 0;
            }

            Report($"The render pipeline of program '{program.Info.Label}' could not be created: {error}");
        }

        _pipelines.Add(key, pipeline);
        return pipeline;
    }

    private static void CreatePipelineCore(WgpuDevice device, PipelineRequest request)
    {
        var key = request.Key;
        var state = device._states[key.State];
        var target = request.Target;
        var formats = target.Formats;
        var triangles = key.Topology is (int)PrimitiveTopology.Triangles or (int)PrimitiveTopology.TriangleStrip;
        var vertexLayout = request.Vertex;
        var buffers = stackalloc WgpuVertexBufferLayout[Math.Max(1, vertexLayout.Buffers.Length)];
        var main = stackalloc byte[4] { (byte)'m', (byte)'a', (byte)'i', (byte)'n' };
        var entry = new WgpuStringView { Data = main, Length = 4 };
        var labelBytes = stackalloc byte[LabelBytes];
        fixed (WgpuVertexAttribute* attributes = vertexLayout.Attributes)
        {
            var next = attributes;
            for (var i = 0; i < vertexLayout.Buffers.Length; i++)
            {
                buffers[i] = vertexLayout.Buffers[i];
                buffers[i].AttributeCount = (nuint)vertexLayout.Counts[i];
                buffers[i].Attributes = next;
                next += vertexLayout.Counts[i];
            }

            var blend = BlendOf(state.Blend);
            var targets = stackalloc WgpuColorTargetState[Math.Max(1, formats.ColorCount)];
            for (var i = 0; i < formats.ColorCount; i++)
            {
                targets[i] = new WgpuColorTargetState
                {
                    Format = (uint)((formats.Colors >> (i * 8)) & 0xFF),
                    Blend = state.Blend == BlendMode.Opaque ? null : &blend,

                    // A target the program has no output for (a depth pass) is left alone.
                    WriteMask = state.ColorWrite && (request.Program.Info.ColorOutputs & (1u << i)) != 0 ? Wgpu.ColorWriteAll : 0,
                };
            }

            var fragment = new WgpuFragmentState
            {
                Module = request.Program.Fragment,
                EntryPoint = entry,
                TargetCount = (nuint)formats.ColorCount,
                Targets = targets,
            };
            var keep = new WgpuStencilFaceState { Compare = Wgpu.CompareAlways, FailOp = Wgpu.StencilKeep, DepthFailOp = Wgpu.StencilKeep, PassOp = Wgpu.StencilKeep };
            var depth = new WgpuDepthStencilState
            {
                Format = formats.Depth,
                DepthWriteEnabled = state.DepthTest && state.DepthWrite ? Wgpu.OptionalBoolTrue : Wgpu.OptionalBoolFalse,
                DepthCompare = state.DepthTest ? Wgpu.CompareNever + (uint)state.DepthCompare : Wgpu.CompareAlways,
                StencilFront = keep,
                StencilBack = keep,
                StencilReadMask = 0xFFFFFFFF,
                StencilWriteMask = 0,

                // Lines and points have no depth bias in WebGPU.
                DepthBias = triangles ? (int)MathF.Round(state.DepthBiasConstant) : 0,
                DepthBiasSlopeScale = triangles ? state.DepthBiasSlope : 0f,
            };
            var descriptor = new WgpuRenderPipelineDescriptor
            {
                Label = Label(request.Program.Info.Label, labelBytes, LabelBytes),
                Layout = request.Layout.Pipeline,
                Vertex = new WgpuVertexState
                {
                    Module = request.Program.Vertex,
                    EntryPoint = entry,
                    BufferCount = (nuint)vertexLayout.Buffers.Length,
                    Buffers = buffers,
                },
                Primitive = new WgpuPrimitiveState
                {
                    Topology = (PrimitiveTopology)key.Topology switch
                    {
                        PrimitiveTopology.TriangleStrip => Wgpu.TopologyTriangleStrip,
                        PrimitiveTopology.Lines => Wgpu.TopologyLineList,
                        PrimitiveTopology.LineStrip => Wgpu.TopologyLineStrip,
                        PrimitiveTopology.Points => Wgpu.TopologyPointList,
                        _ => Wgpu.TopologyTriangleList,
                    },
                    StripIndexFormat = key.StripIndex,

                    // The vertex stage negates Y, which turns every triangle around.
                    FrontFace = state.FrontFaceClockwise ? Wgpu.FrontFaceCcw : Wgpu.FrontFaceCw,
                    CullMode = state.Cull switch
                    {
                        CullMode.Back => Wgpu.CullBack,
                        CullMode.Front => Wgpu.CullFront,
                        _ => Wgpu.CullNone,
                    },
                },
                DepthStencil = formats.Depth == 0 ? null : &depth,
                Multisample = new WgpuMultisampleState
                {
                    Count = (uint)formats.Samples,
                    Mask = 0xFFFFFFFF,
                    AlphaToCoverageEnabled = state.AlphaToCoverage && formats.Samples > 1 && formats.ColorCount > 0 ? 1u : 0u,
                },
                Fragment = &fragment,
            };
            device._createdPipeline = device._api.DeviceCreateRenderPipeline(device._device, &descriptor);
        }
    }

    private static WgpuBlendState BlendOf(BlendMode mode)
    {
        static WgpuBlendComponent C(uint source, uint destination) => new() { Operation = Wgpu.BlendOpAdd, SrcFactor = source, DstFactor = destination };
        return mode switch
        {
            BlendMode.Alpha => new WgpuBlendState { Color = C(Wgpu.BlendSrcAlpha, Wgpu.BlendOneMinusSrcAlpha), Alpha = C(Wgpu.BlendOne, Wgpu.BlendOneMinusSrcAlpha) },
            BlendMode.PremultipliedAlpha => new WgpuBlendState { Color = C(Wgpu.BlendOne, Wgpu.BlendOneMinusSrcAlpha), Alpha = C(Wgpu.BlendOne, Wgpu.BlendOneMinusSrcAlpha) },
            BlendMode.Additive => new WgpuBlendState { Color = C(Wgpu.BlendSrcAlpha, Wgpu.BlendOne), Alpha = C(Wgpu.BlendOne, Wgpu.BlendOne) },
            BlendMode.Multiply => new WgpuBlendState { Color = C(Wgpu.BlendDst, Wgpu.BlendZero), Alpha = C(Wgpu.BlendZero, Wgpu.BlendOne) },
            _ => new WgpuBlendState { Color = C(Wgpu.BlendZero, Wgpu.BlendOneMinusSrc), Alpha = C(Wgpu.BlendZero, Wgpu.BlendOne) },
        };
    }

    private void ForgetProgram(WgpuProgram program)
    {
        if (ReferenceEquals(_program, program))
        {
            _program = null;
        }

        // Its pipelines go with it.
        List<PipelineKey>? stale = null;
        foreach (var (key, pipeline) in _pipelines)
        {
            if (key.Program == program.Info.Id)
            {
                (stale ??= []).Add(key);
                if (pipeline != 0)
                {
                    _api.RenderPipelineRelease(pipeline);
                }
            }
        }

        if (stale is not null)
        {
            foreach (var key in stale)
            {
                _pipelines.Remove(key);
            }
        }

        _appliedPipeline = 0;
        _lastPipelineValid = false;
        ReleaseLayouts(program.Info);
    }
}
