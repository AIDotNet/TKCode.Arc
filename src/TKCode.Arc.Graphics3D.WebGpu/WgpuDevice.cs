using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

/// <summary>The graphics API wgpu-native runs on.</summary>
public enum WgpuBackend
{
    /// <summary>The platform's own: Metal on macOS, Direct3D 12 on Windows, Vulkan elsewhere.</summary>
    Default,

    /// <summary>Metal (macOS, iOS).</summary>
    Metal,

    /// <summary>Vulkan (Linux, Android, Windows).</summary>
    Vulkan,

    /// <summary>Direct3D 12 (Windows).</summary>
    Direct3D12,

    /// <summary>OpenGL ES 3 (or desktop OpenGL), wgpu's compatibility backend. Not usable yet: multisampled targets
    /// and viewport changes inside a pass come out wrong on it.</summary>
    OpenGl,
}

/// <summary>How <see cref="WgpuDevice.Create"/> chooses the adapter.</summary>
public sealed class WgpuDeviceOptions
{
    /// <summary>The graphics API to run on.</summary>
    public WgpuBackend Backend { get; init; }

    /// <summary>True prefers the integrated GPU of a machine with two.</summary>
    public bool LowPower { get; init; }

    /// <summary>True asks for a software adapter (for example Mesa's lavapipe).</summary>
    public bool ForceFallbackAdapter { get; init; }
}

/// <summary>The WebGPU backend, over wgpu-native (Metal, Vulkan, Direct3D 12, OpenGL ES). It compiles the engine's
/// <see cref="ShaderLanguage.Glsl450"/> dialect and supports compute shaders. The device keeps OpenGL's picture
/// conventions so that the renderer needs no second code path: row 0 of every render target and texture is the
/// bottom row (it negates clip-space Y after the vertex stage and swaps the front face to do so). Clip-space depth is
/// 0..1. All calls happen on one thread.</summary>
/// <remarks>The native library is not part of the package: get <c>libwgpu_native</c> from a wgpu-native release
/// (https://github.com/gfx-rs/wgpu-native, v29) and pass its path to <see cref="Create"/>.</remarks>
public sealed unsafe partial class WgpuDevice : GraphicsDevice
{
    /// <summary>The wgpu-native release the bindings were written against.</summary>
    public const string NativeVersion = "29.0.1.1";

    private readonly WgpuApi _api;
    private readonly nint _instance;
    private readonly nint _adapter;
    private readonly nint _device;
    private readonly nint _queue;
    private readonly DeviceCapabilities _caps;
    private readonly List<string> _errors = [];
    private readonly bool _float32Filterable;
    private readonly bool _rg11b10Renderable;
    private GCHandle _self;
    private bool _disposed;

    // Counts the validation errors reported so far (Report).
    private int _errorSerial;

    private WgpuDevice(WgpuApi api, nint instance, nint adapter, WgpuDeviceOptions options)
    {
        _api = api;
        _instance = instance;
        _adapter = adapter;
        _self = GCHandle.Alloc(this);

        WgpuLimits limits = default;
        api.AdapterGetLimits(adapter, &limits);
        limits.NextInChain = null;
        if (api.AdapterHasFeature(adapter, Wgpu.NativeFeatureImmediates) == 0 || limits.MaxImmediateSize < DrawConstantBytes)
        {
            _self.Free();
            throw new NotSupportedException("The WebGPU adapter has no immediate data (push constants), which the device needs for draw constants.");
        }

        var features = stackalloc uint[8];
        var featureCount = 0;
        features[featureCount++] = Wgpu.NativeFeatureImmediates;
        bool Optional(uint feature)
        {
            if (api.AdapterHasFeature(adapter, feature) == 0)
            {
                return false;
            }

            features[featureCount++] = feature;
            return true;
        }

        _float32Filterable = Optional(Wgpu.FeatureFloat32Filterable);
        _rg11b10Renderable = Optional(Wgpu.FeatureRg11b10UfloatRenderable);
        var bc = Optional(Wgpu.FeatureTextureCompressionBc);
        var etc2 = Optional(Wgpu.FeatureTextureCompressionEtc2);
        var astc = Optional(Wgpu.FeatureTextureCompressionAstc);

        var box = new CallbackBox();
        var boxHandle = GCHandle.Alloc(box);
        try
        {
            var descriptor = new WgpuDeviceDescriptor
            {
                RequiredFeatureCount = (nuint)featureCount,
                RequiredFeatures = features,
                RequiredLimits = &limits,
                UncapturedErrorCallbackInfo = new WgpuUncapturedErrorCallbackInfo
                {
                    Callback = (delegate* unmanaged[Cdecl]<nint*, uint, WgpuStringView, void*, void*, void>)&OnUncapturedError,
                    Userdata1 = (void*)GCHandle.ToIntPtr(_self),
                },
            };
            api.AdapterRequestDevice(adapter, &descriptor, new WgpuCallbackInfo
            {
                Mode = Wgpu.CallbackModeAllowSpontaneous,
                Callback = (delegate* unmanaged[Cdecl]<uint, nint, WgpuStringView, void*, void*, void>)&OnRequest,
                Userdata1 = (void*)GCHandle.ToIntPtr(boxHandle),
            });
            if (!box.Completed)
            {
                api.InstanceProcessEvents(instance);
            }
        }
        finally
        {
            boxHandle.Free();
        }

        if (box.Handle == 0)
        {
            _self.Free();
            throw new NotSupportedException($"wgpu-native could not create a device: {box.Message}");
        }

        _device = box.Handle;
        _queue = api.DeviceGetQueue(_device);

        WgpuAdapterInfo info = default;
        api.AdapterGetInfo(adapter, &info);
        var backend = info.BackendType switch
        {
            Wgpu.BackendMetal => "Metal",
            Wgpu.BackendVulkan => "Vulkan",
            Wgpu.BackendD3D12 => "Direct3D 12",
            Wgpu.BackendOpenGl => "OpenGL",
            Wgpu.BackendOpenGlEs => "OpenGL ES",
            _ => "unknown backend",
        };
        Backend = info.BackendType switch
        {
            Wgpu.BackendMetal => WgpuBackend.Metal,
            Wgpu.BackendVulkan => WgpuBackend.Vulkan,
            Wgpu.BackendD3D12 => WgpuBackend.Direct3D12,
            Wgpu.BackendOpenGl or Wgpu.BackendOpenGlEs => WgpuBackend.OpenGl,
            _ => options.Backend,
        };
        var version = api.GetVersion();
        var description = Text(info.Description);
        var deviceName = Text(info.Device);
        _caps = new DeviceCapabilities
        {
            ShaderLanguage = ShaderLanguage.Glsl450,
            ClipDepth = ClipDepthRange.ZeroToOne,
            Vendor = Text(info.Vendor),
            Renderer = description.Length > 0 ? $"{deviceName} ({description})" : deviceName,
            Version = $"WebGPU (wgpu-native {version >> 24}.{(version >> 16) & 0xFF}.{(version >> 8) & 0xFF}.{version & 0xFF}, {backend})",
            MaxTextureSize = (int)limits.MaxTextureDimension2D,
            MaxCubeTextureSize = (int)limits.MaxTextureDimension2D,
            MaxSamples = 4,
            MaxAnisotropy = 16f,
            MaxFragmentTextures = (int)Math.Min(MaxUnits, Math.Min(limits.MaxSampledTexturesPerShaderStage, limits.MaxSamplersPerShaderStage)),
            MaxUniformBlockSize = (int)Math.Min(int.MaxValue, limits.MaxUniformBufferBindingSize),
            UniformBufferOffsetAlignment = (int)limits.MinUniformBufferOffsetAlignment,
            MaxVertexAttributes = (int)limits.MaxVertexAttributes,
            MaxColorAttachments = (int)limits.MaxColorAttachments,
            FloatRenderTargets = CaptureError((Wgpu.FormatRGBA16Float, 1u), &TryRenderTarget) is null,
            FloatLinearFiltering = _float32Filterable,
            TextureCompressionBc = bc,
            TextureCompressionEtc2 = etc2,
            TextureCompressionAstc = astc,
            ComputeShaders = true,
            MaxComputeWorkGroupSize = (int)limits.MaxComputeWorkgroupSizeX,
            MaxComputeWorkGroups = (int)limits.MaxComputeWorkgroupsPerDimension,
            MaxStorageBuffers = (int)limits.MaxStorageBuffersPerShaderStage,
        };
        api.AdapterInfoFreeMembers(info);
        CreateDefaults();
    }

    /// <summary>The graphics API the device ended up on.</summary>
    public WgpuBackend Backend { get; }

    /// <inheritdoc/>
    public override DeviceCapabilities Capabilities => _caps;

    /// <summary>The file name of the wgpu-native library on this platform, as the system loader finds it.</summary>
    public static string DefaultLibraryName =>
        OperatingSystem.IsWindows() ? "wgpu_native.dll" : OperatingSystem.IsMacOS() ? "libwgpu_native.dylib" : "libwgpu_native.so";

    /// <summary>Creates a device.</summary>
    /// <param name="libraryPath">The path of <c>libwgpu_native</c> (<c>wgpu_native.dll</c> on Windows); null searches
    /// for <see cref="DefaultLibraryName"/> the way the system loader does.</param>
    /// <param name="options">The adapter choice; null takes the platform's default adapter.</param>
    /// <exception cref="DllNotFoundException">The library was not found.</exception>
    /// <exception cref="NotSupportedException">The library is not wgpu-native v29, or there is no usable adapter.</exception>
    public static WgpuDevice Create(string? libraryPath = null, WgpuDeviceOptions? options = null)
    {
        options ??= new WgpuDeviceOptions();
        var library = NativeLibrary.Load(libraryPath ?? DefaultLibraryName);
        var api = new WgpuApi(library);
        var instance = api.CreateInstance(null);
        if (instance == 0)
        {
            throw new NotSupportedException("wgpu-native could not create an instance.");
        }

        var box = new CallbackBox();
        var boxHandle = GCHandle.Alloc(box);
        try
        {
            var adapterOptions = new WgpuRequestAdapterOptions
            {
                PowerPreference = options.LowPower ? 1u : 2u,
                ForceFallbackAdapter = options.ForceFallbackAdapter ? 1u : 0u,
                BackendType = options.Backend switch
                {
                    WgpuBackend.Metal => Wgpu.BackendMetal,
                    WgpuBackend.Vulkan => Wgpu.BackendVulkan,
                    WgpuBackend.Direct3D12 => Wgpu.BackendD3D12,
                    WgpuBackend.OpenGl => Wgpu.BackendOpenGlEs,
                    _ => Wgpu.BackendUndefined,
                },
            };
            api.InstanceRequestAdapter(instance, &adapterOptions, new WgpuCallbackInfo
            {
                Mode = Wgpu.CallbackModeAllowSpontaneous,
                Callback = (delegate* unmanaged[Cdecl]<uint, nint, WgpuStringView, void*, void*, void>)&OnRequest,
                Userdata1 = (void*)GCHandle.ToIntPtr(boxHandle),
            });
            if (!box.Completed)
            {
                api.InstanceProcessEvents(instance);
            }
        }
        finally
        {
            boxHandle.Free();
        }

        if (box.Handle == 0)
        {
            api.InstanceRelease(instance);
            throw new NotSupportedException($"wgpu-native found no adapter for backend {options.Backend}: {box.Message}");
        }

        try
        {
            return new WgpuDevice(api, instance, box.Handle, options);
        }
        catch
        {
            api.AdapterRelease(box.Handle);
            api.InstanceRelease(instance);
            throw;
        }
    }

    /// <summary>Raised for every validation error wgpu reports, on the device's thread, while the failing call is
    /// still on the stack. The handler must not throw.</summary>
    public event Action<string>? ErrorReported;

    /// <summary>Returns the validation errors wgpu reported since the last call (empty when none). The device goes on
    /// after an error, with the failed call ignored; for tests and diagnostics.</summary>
    public IReadOnlyList<string> DrainErrors()
    {
        if (_errors.Count == 0)
        {
            return [];
        }

        var errors = _errors.ToArray();
        _errors.Clear();
        return errors;
    }

    /// <summary>Blocks until the GPU finished all submitted work (for measurements).</summary>
    public void Finish()
    {
        Flush();
        _api.DevicePoll(_device, 1, null);
    }

    private sealed class CallbackBox
    {
        public bool Completed;
        public uint Status;
        public nint Handle;
        public string Message = "";
    }

    private static string Text(WgpuStringView view) =>
        view.Data is null || view.Length == 0 || view.Length == nuint.MaxValue ? "" : Encoding.UTF8.GetString(view.Data, (int)view.Length);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnRequest(uint status, nint handle, WgpuStringView message, void* userdata1, void* userdata2)
    {
        if (GCHandle.FromIntPtr((nint)userdata1).Target is CallbackBox box)
        {
            box.Completed = true;
            box.Status = status;
            box.Handle = handle;
            box.Message = Text(message);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnStatus(uint status, WgpuStringView message, void* userdata1, void* userdata2)
    {
        if (GCHandle.FromIntPtr((nint)userdata1).Target is CallbackBox box)
        {
            box.Completed = true;
            box.Status = status;
            box.Message = Text(message);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnErrorScope(uint status, uint type, WgpuStringView message, void* userdata1, void* userdata2)
    {
        if (GCHandle.FromIntPtr((nint)userdata1).Target is CallbackBox box)
        {
            box.Completed = true;
            box.Status = type;
            box.Message = Text(message);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnUncapturedError(nint* device, uint type, WgpuStringView message, void* userdata1, void* userdata2)
    {
        if (GCHandle.FromIntPtr((nint)userdata1).Target is WgpuDevice self)
        {
            self.Report(Text(message));
        }
    }

    private void Report(string error)
    {
        _errorSerial++;
        if (_errors.Count < 64)
        {
            _errors.Add(error);
        }

        ErrorReported?.Invoke(error);
    }

    /// <summary>Runs <paramref name="action"/> and returns the validation error it raised, or null.</summary>
    private string? CaptureError<TState>(TState state, delegate*<WgpuDevice, TState, void> action)
    {
        _api.DevicePushErrorScope(_device, Wgpu.ErrorFilterValidation);
        action(this, state);
        var box = new CallbackBox();
        var handle = GCHandle.Alloc(box);
        try
        {
            _api.DevicePopErrorScope(_device, new WgpuCallbackInfo
            {
                Mode = Wgpu.CallbackModeAllowSpontaneous,
                Callback = (delegate* unmanaged[Cdecl]<uint, uint, WgpuStringView, void*, void*, void>)&OnErrorScope,
                Userdata1 = (void*)GCHandle.ToIntPtr(handle),
            });
            if (!box.Completed)
            {
                _api.InstanceProcessEvents(_instance);
            }
        }
        finally
        {
            handle.Free();
        }

        return box.Completed && box.Status != Wgpu.ErrorNoError ? box.Message : null;
    }

    /// <summary>The label as UTF-8 in <paramref name="buffer"/>; an empty view when there is none or it does not fit.</summary>
    private static WgpuStringView Label(string? label, byte* buffer, int capacity)
    {
        if (string.IsNullOrEmpty(label) || Encoding.UTF8.GetByteCount(label) > capacity)
        {
            return default;
        }

        fixed (char* chars = label)
        {
            var length = Encoding.UTF8.GetBytes(chars, label.Length, buffer, capacity);
            return new WgpuStringView { Data = buffer, Length = (nuint)length };
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposePasses();
        DisposeTransfers();
        DisposeDefaults();
        _api.DevicePoll(_device, 1, null);
        _api.QueueRelease(_queue);
        _api.DeviceRelease(_device);
        _api.AdapterRelease(_adapter);
        _api.InstanceRelease(_instance);
        if (_self.IsAllocated)
        {
            _self.Free();
        }
    }
}
