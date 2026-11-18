namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

// The C structures of webgpu.h and wgpu.h (wgpu-native v29) that the backend passes, field for field. All of them are
// blittable; handles are pointers (nint), WGPUBool is a uint, WGPUFlags a ulong, enums are uints.

internal unsafe struct WgpuStringView
{
    public byte* Data;
    public nuint Length;
}

internal unsafe struct WgpuChainedStruct
{
    public WgpuChainedStruct* Next;
    public uint SType;
}

internal struct WgpuFuture
{
    public ulong Id;
}

internal unsafe struct WgpuRequestAdapterOptions
{
    public WgpuChainedStruct* NextInChain;
    public uint FeatureLevel;
    public uint PowerPreference;
    public uint ForceFallbackAdapter;
    public uint BackendType;
    public nint CompatibleSurface;
}

/// <summary>The shape shared by WGPURequestAdapterCallbackInfo, WGPURequestDeviceCallbackInfo, WGPUBufferMapCallbackInfo,
/// WGPUPopErrorScopeCallbackInfo and WGPUDeviceLostCallbackInfo.</summary>
internal unsafe struct WgpuCallbackInfo
{
    public WgpuChainedStruct* NextInChain;
    public uint Mode;
    public void* Callback;
    public void* Userdata1;
    public void* Userdata2;
}

internal unsafe struct WgpuUncapturedErrorCallbackInfo
{
    public WgpuChainedStruct* NextInChain;
    public void* Callback;
    public void* Userdata1;
    public void* Userdata2;
}

internal unsafe struct WgpuQueueDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
}

internal unsafe struct WgpuDeviceDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public nuint RequiredFeatureCount;
    public uint* RequiredFeatures;
    public WgpuLimits* RequiredLimits;
    public WgpuQueueDescriptor DefaultQueue;
    public WgpuCallbackInfo DeviceLostCallbackInfo;
    public WgpuUncapturedErrorCallbackInfo UncapturedErrorCallbackInfo;
}

internal unsafe struct WgpuLimits
{
    public WgpuChainedStruct* NextInChain;
    public uint MaxTextureDimension1D;
    public uint MaxTextureDimension2D;
    public uint MaxTextureDimension3D;
    public uint MaxTextureArrayLayers;
    public uint MaxBindGroups;
    public uint MaxBindGroupsPlusVertexBuffers;
    public uint MaxBindingsPerBindGroup;
    public uint MaxDynamicUniformBuffersPerPipelineLayout;
    public uint MaxDynamicStorageBuffersPerPipelineLayout;
    public uint MaxSampledTexturesPerShaderStage;
    public uint MaxSamplersPerShaderStage;
    public uint MaxStorageBuffersPerShaderStage;
    public uint MaxStorageTexturesPerShaderStage;
    public uint MaxUniformBuffersPerShaderStage;
    public ulong MaxUniformBufferBindingSize;
    public ulong MaxStorageBufferBindingSize;
    public uint MinUniformBufferOffsetAlignment;
    public uint MinStorageBufferOffsetAlignment;
    public uint MaxVertexBuffers;
    public ulong MaxBufferSize;
    public uint MaxVertexAttributes;
    public uint MaxVertexBufferArrayStride;
    public uint MaxInterStageShaderVariables;
    public uint MaxColorAttachments;
    public uint MaxColorAttachmentBytesPerSample;
    public uint MaxComputeWorkgroupStorageSize;
    public uint MaxComputeInvocationsPerWorkgroup;
    public uint MaxComputeWorkgroupSizeX;
    public uint MaxComputeWorkgroupSizeY;
    public uint MaxComputeWorkgroupSizeZ;
    public uint MaxComputeWorkgroupsPerDimension;
    public uint MaxImmediateSize;
}

internal unsafe struct WgpuAdapterInfo
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Vendor;
    public WgpuStringView Architecture;
    public WgpuStringView Device;
    public WgpuStringView Description;
    public uint BackendType;
    public uint AdapterType;
    public uint VendorId;
    public uint DeviceId;
    public uint SubgroupMinSize;
    public uint SubgroupMaxSize;
}

internal unsafe struct WgpuBufferDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public ulong Usage;
    public ulong Size;
    public uint MappedAtCreation;
}

internal struct WgpuExtent3D
{
    public uint Width;
    public uint Height;
    public uint DepthOrArrayLayers;
}

internal struct WgpuOrigin3D
{
    public uint X;
    public uint Y;
    public uint Z;
}

internal unsafe struct WgpuTextureDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public ulong Usage;
    public uint Dimension;
    public WgpuExtent3D Size;
    public uint Format;
    public uint MipLevelCount;
    public uint SampleCount;
    public nuint ViewFormatCount;
    public uint* ViewFormats;
}

internal unsafe struct WgpuTextureViewDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public uint Format;
    public uint Dimension;
    public uint BaseMipLevel;
    public uint MipLevelCount;
    public uint BaseArrayLayer;
    public uint ArrayLayerCount;
    public uint Aspect;
    public ulong Usage;
}

internal unsafe struct WgpuSamplerDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public uint AddressModeU;
    public uint AddressModeV;
    public uint AddressModeW;
    public uint MagFilter;
    public uint MinFilter;
    public uint MipmapFilter;
    public float LodMinClamp;
    public float LodMaxClamp;
    public uint Compare;
    public ushort MaxAnisotropy;
}

internal unsafe struct WgpuShaderModuleDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
}

internal struct WgpuShaderSourceWgsl
{
    public WgpuChainedStruct Chain;
    public WgpuStringView Code;
}

internal unsafe struct WgpuShaderSourceGlsl
{
    public WgpuChainedStruct Chain;
    public ulong Stage;
    public WgpuStringView Code;
    public uint DefineCount;
    public void* Defines;
}

internal unsafe struct WgpuBufferBindingLayout
{
    public WgpuChainedStruct* NextInChain;
    public uint Type;
    public uint HasDynamicOffset;
    public ulong MinBindingSize;
}

internal unsafe struct WgpuSamplerBindingLayout
{
    public WgpuChainedStruct* NextInChain;
    public uint Type;
}

internal unsafe struct WgpuTextureBindingLayout
{
    public WgpuChainedStruct* NextInChain;
    public uint SampleType;
    public uint ViewDimension;
    public uint Multisampled;
}

internal unsafe struct WgpuStorageTextureBindingLayout
{
    public WgpuChainedStruct* NextInChain;
    public uint Access;
    public uint Format;
    public uint ViewDimension;
}

internal unsafe struct WgpuBindGroupLayoutEntry
{
    public WgpuChainedStruct* NextInChain;
    public uint Binding;
    public ulong Visibility;
    public uint BindingArraySize;
    public WgpuBufferBindingLayout Buffer;
    public WgpuSamplerBindingLayout Sampler;
    public WgpuTextureBindingLayout Texture;
    public WgpuStorageTextureBindingLayout StorageTexture;
}

internal unsafe struct WgpuBindGroupLayoutDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public nuint EntryCount;
    public WgpuBindGroupLayoutEntry* Entries;
}

internal unsafe struct WgpuPipelineLayoutDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public nuint BindGroupLayoutCount;
    public nint* BindGroupLayouts;
    public uint ImmediateSize;
}

internal unsafe struct WgpuBindGroupEntry
{
    public WgpuChainedStruct* NextInChain;
    public uint Binding;
    public nint Buffer;
    public ulong Offset;
    public ulong Size;
    public nint Sampler;
    public nint TextureView;
}

internal unsafe struct WgpuBindGroupDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public nint Layout;
    public nuint EntryCount;
    public WgpuBindGroupEntry* Entries;
}

internal unsafe struct WgpuVertexAttribute
{
    public WgpuChainedStruct* NextInChain;
    public uint Format;
    public ulong Offset;
    public uint ShaderLocation;
}

internal unsafe struct WgpuVertexBufferLayout
{
    public WgpuChainedStruct* NextInChain;
    public uint StepMode;
    public ulong ArrayStride;
    public nuint AttributeCount;
    public WgpuVertexAttribute* Attributes;
}

internal unsafe struct WgpuVertexState
{
    public WgpuChainedStruct* NextInChain;
    public nint Module;
    public WgpuStringView EntryPoint;
    public nuint ConstantCount;
    public void* Constants;
    public nuint BufferCount;
    public WgpuVertexBufferLayout* Buffers;
}

internal unsafe struct WgpuPrimitiveState
{
    public WgpuChainedStruct* NextInChain;
    public uint Topology;
    public uint StripIndexFormat;
    public uint FrontFace;
    public uint CullMode;
    public uint UnclippedDepth;
}

internal struct WgpuStencilFaceState
{
    public uint Compare;
    public uint FailOp;
    public uint DepthFailOp;
    public uint PassOp;
}

internal unsafe struct WgpuDepthStencilState
{
    public WgpuChainedStruct* NextInChain;
    public uint Format;
    public uint DepthWriteEnabled;
    public uint DepthCompare;
    public WgpuStencilFaceState StencilFront;
    public WgpuStencilFaceState StencilBack;
    public uint StencilReadMask;
    public uint StencilWriteMask;
    public int DepthBias;
    public float DepthBiasSlopeScale;
    public float DepthBiasClamp;
}

internal unsafe struct WgpuMultisampleState
{
    public WgpuChainedStruct* NextInChain;
    public uint Count;
    public uint Mask;
    public uint AlphaToCoverageEnabled;
}

internal struct WgpuBlendComponent
{
    public uint Operation;
    public uint SrcFactor;
    public uint DstFactor;
}

internal struct WgpuBlendState
{
    public WgpuBlendComponent Color;
    public WgpuBlendComponent Alpha;
}

internal unsafe struct WgpuColorTargetState
{
    public WgpuChainedStruct* NextInChain;
    public uint Format;
    public WgpuBlendState* Blend;
    public ulong WriteMask;
}

internal unsafe struct WgpuFragmentState
{
    public WgpuChainedStruct* NextInChain;
    public nint Module;
    public WgpuStringView EntryPoint;
    public nuint ConstantCount;
    public void* Constants;
    public nuint TargetCount;
    public WgpuColorTargetState* Targets;
}

internal unsafe struct WgpuRenderPipelineDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public nint Layout;
    public WgpuVertexState Vertex;
    public WgpuPrimitiveState Primitive;
    public WgpuDepthStencilState* DepthStencil;
    public WgpuMultisampleState Multisample;
    public WgpuFragmentState* Fragment;
}

internal unsafe struct WgpuComputeState
{
    public WgpuChainedStruct* NextInChain;
    public nint Module;
    public WgpuStringView EntryPoint;
    public nuint ConstantCount;
    public void* Constants;
}

internal unsafe struct WgpuComputePipelineDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public nint Layout;
    public WgpuComputeState Compute;
}

internal struct WgpuColor
{
    public double R;
    public double G;
    public double B;
    public double A;
}

internal unsafe struct WgpuRenderPassColorAttachment
{
    public WgpuChainedStruct* NextInChain;
    public nint View;
    public uint DepthSlice;
    public nint ResolveTarget;
    public uint LoadOp;
    public uint StoreOp;
    public WgpuColor ClearValue;
}

internal unsafe struct WgpuRenderPassDepthStencilAttachment
{
    public WgpuChainedStruct* NextInChain;
    public nint View;
    public uint DepthLoadOp;
    public uint DepthStoreOp;
    public float DepthClearValue;
    public uint DepthReadOnly;
    public uint StencilLoadOp;
    public uint StencilStoreOp;
    public uint StencilClearValue;
    public uint StencilReadOnly;
}

internal unsafe struct WgpuRenderPassDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public nuint ColorAttachmentCount;
    public WgpuRenderPassColorAttachment* ColorAttachments;
    public WgpuRenderPassDepthStencilAttachment* DepthStencilAttachment;
    public nint OcclusionQuerySet;
    public void* TimestampWrites;
}

internal unsafe struct WgpuComputePassDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
    public void* TimestampWrites;
}

internal unsafe struct WgpuLabelDescriptor
{
    public WgpuChainedStruct* NextInChain;
    public WgpuStringView Label;
}

internal struct WgpuTexelCopyTextureInfo
{
    public nint Texture;
    public uint MipLevel;
    public WgpuOrigin3D Origin;
    public uint Aspect;
}

internal struct WgpuTexelCopyBufferLayout
{
    public ulong Offset;
    public uint BytesPerRow;
    public uint RowsPerImage;
}

internal struct WgpuTexelCopyBufferInfo
{
    public WgpuTexelCopyBufferLayout Layout;
    public nint Buffer;
}

/// <summary>Enumeration values of webgpu.h and wgpu.h, by their C names.</summary>
internal static class Wgpu
{
    public const uint STypeShaderSourceWgsl = 0x00000002;
    public const uint STypeShaderSourceGlsl = 0x00030003;

    public const uint FeatureDepthClipControl = 0x00000002;
    public const uint FeatureTextureCompressionBc = 0x00000004;
    public const uint FeatureTextureCompressionEtc2 = 0x00000006;
    public const uint FeatureTextureCompressionAstc = 0x00000007;
    public const uint FeatureRg11b10UfloatRenderable = 0x0000000C;
    public const uint FeatureFloat32Filterable = 0x0000000E;
    public const uint FeatureFloat32Blendable = 0x0000000F;
    public const uint NativeFeatureImmediates = 0x00030001;

    public const uint BackendUndefined = 0;
    public const uint BackendD3D12 = 4;
    public const uint BackendMetal = 5;
    public const uint BackendVulkan = 6;
    public const uint BackendOpenGl = 7;
    public const uint BackendOpenGlEs = 8;

    public const uint CallbackModeAllowProcessEvents = 2;
    public const uint CallbackModeAllowSpontaneous = 3;

    public const uint ErrorNoError = 1;
    public const uint ErrorFilterValidation = 1;

    public const ulong BufferUsageMapRead = 0x1;
    public const ulong BufferUsageCopySrc = 0x4;
    public const ulong BufferUsageCopyDst = 0x8;
    public const ulong BufferUsageIndex = 0x10;
    public const ulong BufferUsageVertex = 0x20;
    public const ulong BufferUsageUniform = 0x40;
    public const ulong BufferUsageStorage = 0x80;

    public const ulong TextureUsageCopySrc = 0x1;
    public const ulong TextureUsageCopyDst = 0x2;
    public const ulong TextureUsageTextureBinding = 0x4;
    public const ulong TextureUsageRenderAttachment = 0x10;

    public const uint TextureDimension2D = 2;
    public const uint TextureDimension3D = 3;

    public const uint ViewDimension2D = 2;
    public const uint ViewDimension2DArray = 3;
    public const uint ViewDimensionCube = 4;
    public const uint ViewDimension3D = 6;

    public const uint AspectAll = 1;
    public const uint AspectDepthOnly = 3;

    public const uint AddressClampToEdge = 1;
    public const uint AddressRepeat = 2;
    public const uint AddressMirrorRepeat = 3;

    public const uint FilterNearest = 1;
    public const uint FilterLinear = 2;

    public const uint CompareUndefined = 0;
    public const uint CompareNever = 1;
    public const uint CompareAlways = 8;

    public const ulong StageVertex = 1;
    public const ulong StageFragment = 2;
    public const ulong StageCompute = 4;

    public const uint BufferBindingUniform = 2;
    public const uint BufferBindingStorage = 3;
    public const uint BufferBindingReadOnlyStorage = 4;

    public const uint SamplerBindingFiltering = 2;
    public const uint SamplerBindingNonFiltering = 3;
    public const uint SamplerBindingComparison = 4;

    public const uint SampleTypeFloat = 2;
    public const uint SampleTypeUnfilterableFloat = 3;
    public const uint SampleTypeDepth = 4;

    public const uint StepModeVertex = 1;
    public const uint StepModeInstance = 2;

    public const uint TopologyPointList = 1;
    public const uint TopologyLineList = 2;
    public const uint TopologyLineStrip = 3;
    public const uint TopologyTriangleList = 4;
    public const uint TopologyTriangleStrip = 5;

    public const uint IndexFormatUndefined = 0;
    public const uint IndexFormatUint16 = 1;
    public const uint IndexFormatUint32 = 2;

    public const uint FrontFaceCcw = 1;
    public const uint FrontFaceCw = 2;

    public const uint CullNone = 1;
    public const uint CullFront = 2;
    public const uint CullBack = 3;

    public const uint OptionalBoolFalse = 0;
    public const uint OptionalBoolTrue = 1;

    public const uint StencilKeep = 1;

    public const uint BlendOpAdd = 1;
    public const uint BlendZero = 1;
    public const uint BlendOne = 2;
    public const uint BlendSrc = 3;
    public const uint BlendOneMinusSrc = 4;
    public const uint BlendSrcAlpha = 5;
    public const uint BlendOneMinusSrcAlpha = 6;
    public const uint BlendDst = 7;

    public const ulong ColorWriteAll = 0xF;

    public const uint LoadOpLoad = 1;
    public const uint LoadOpClear = 2;
    public const uint StoreOpStore = 1;
    public const uint StoreOpDiscard = 2;

    public const uint DepthSliceUndefined = uint.MaxValue;
    public const ulong WholeSize = ulong.MaxValue;

    public const ulong MapModeRead = 1;
    public const uint MapAsyncSuccess = 1;

    public const uint FormatR8Unorm = 0x01;
    public const uint FormatR16Float = 0x09;
    public const uint FormatRG8Unorm = 0x0A;
    public const uint FormatR32Float = 0x0E;
    public const uint FormatRG16Float = 0x15;
    public const uint FormatRGBA8Unorm = 0x16;
    public const uint FormatRGBA8UnormSrgb = 0x17;
    public const uint FormatRG11B10Ufloat = 0x1F;
    public const uint FormatRG32Float = 0x21;
    public const uint FormatRGBA16Float = 0x28;
    public const uint FormatRGBA32Float = 0x29;
    public const uint FormatDepth16Unorm = 0x2D;
    public const uint FormatDepth24Plus = 0x2E;
    public const uint FormatDepth24PlusStencil8 = 0x2F;
    public const uint FormatDepth32Float = 0x30;
    public const uint FormatBc1RgbaUnorm = 0x32;
    public const uint FormatBc1RgbaUnormSrgb = 0x33;
    public const uint FormatBc3RgbaUnorm = 0x36;
    public const uint FormatBc3RgbaUnormSrgb = 0x37;
    public const uint FormatBc4RUnorm = 0x38;
    public const uint FormatBc5RgUnorm = 0x3A;
    public const uint FormatBc6hRgbUfloat = 0x3C;
    public const uint FormatBc7RgbaUnorm = 0x3E;
    public const uint FormatBc7RgbaUnormSrgb = 0x3F;
    public const uint FormatEtc2Rgb8Unorm = 0x40;
    public const uint FormatEtc2Rgb8UnormSrgb = 0x41;
    public const uint FormatEtc2Rgba8Unorm = 0x44;
    public const uint FormatEtc2Rgba8UnormSrgb = 0x45;
    public const uint FormatAstc4x4Unorm = 0x4A;
    public const uint FormatAstc4x4UnormSrgb = 0x4B;

    public const uint VertexUint8x4 = 0x03;
    public const uint VertexUnorm8x2 = 0x08;
    public const uint VertexUnorm8x4 = 0x09;
    public const uint VertexSnorm8x2 = 0x0B;
    public const uint VertexSnorm8x4 = 0x0C;
    public const uint VertexUint16x2 = 0x0E;
    public const uint VertexUint16x4 = 0x0F;
    public const uint VertexUnorm16x2 = 0x14;
    public const uint VertexUnorm16x4 = 0x15;
    public const uint VertexSnorm16x2 = 0x17;
    public const uint VertexSnorm16x4 = 0x18;
    public const uint VertexFloat32 = 0x1C;
    public const uint VertexFloat32x2 = 0x1D;
    public const uint VertexFloat32x3 = 0x1E;
    public const uint VertexFloat32x4 = 0x1F;
    public const uint VertexUint32x4 = 0x23;
}
