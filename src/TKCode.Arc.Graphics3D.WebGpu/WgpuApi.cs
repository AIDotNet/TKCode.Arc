using System.Runtime.InteropServices;

namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

/// <summary>The entry points of wgpu-native (webgpu.h and wgpu.h, v29) the backend uses, loaded as function pointers
/// from the library (no reflection, no marshalling stubs).</summary>
internal sealed unsafe class WgpuApi
{
    public readonly nint Library;

    public readonly delegate* unmanaged[Cdecl]<void*, nint> CreateInstance;
    public readonly delegate* unmanaged[Cdecl]<nint, void> InstanceRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> InstanceProcessEvents;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuRequestAdapterOptions*, WgpuCallbackInfo, WgpuFuture> InstanceRequestAdapter;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuAdapterInfo*, uint> AdapterGetInfo;
    public readonly delegate* unmanaged[Cdecl]<WgpuAdapterInfo, void> AdapterInfoFreeMembers;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuLimits*, uint> AdapterGetLimits;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, uint> AdapterHasFeature;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuDeviceDescriptor*, WgpuCallbackInfo, WgpuFuture> AdapterRequestDevice;
    public readonly delegate* unmanaged[Cdecl]<nint, void> AdapterRelease;
    public readonly delegate* unmanaged[Cdecl]<uint> GetVersion;

    public readonly delegate* unmanaged[Cdecl]<nint, nint> DeviceGetQueue;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, ulong*, uint> DevicePoll;
    public readonly delegate* unmanaged[Cdecl]<nint, void> DeviceDestroy;
    public readonly delegate* unmanaged[Cdecl]<nint, void> DeviceRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, void> DevicePushErrorScope;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuCallbackInfo, WgpuFuture> DevicePopErrorScope;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuBufferDescriptor*, nint> DeviceCreateBuffer;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuTextureDescriptor*, nint> DeviceCreateTexture;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuSamplerDescriptor*, nint> DeviceCreateSampler;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuShaderModuleDescriptor*, nint> DeviceCreateShaderModule;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuBindGroupLayoutDescriptor*, nint> DeviceCreateBindGroupLayout;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuPipelineLayoutDescriptor*, nint> DeviceCreatePipelineLayout;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuBindGroupDescriptor*, nint> DeviceCreateBindGroup;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuRenderPipelineDescriptor*, nint> DeviceCreateRenderPipeline;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuComputePipelineDescriptor*, nint> DeviceCreateComputePipeline;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuLabelDescriptor*, nint> DeviceCreateCommandEncoder;

    public readonly delegate* unmanaged[Cdecl]<nint, void> QueueRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, nuint, nint*, void> QueueSubmit;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, ulong, void*, nuint, void> QueueWriteBuffer;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuTexelCopyTextureInfo*, void*, nuint, WgpuTexelCopyBufferLayout*, WgpuExtent3D*, void> QueueWriteTexture;

    public readonly delegate* unmanaged[Cdecl]<nint, void> BufferDestroy;
    public readonly delegate* unmanaged[Cdecl]<nint, void> BufferRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, ulong, nuint, nuint, WgpuCallbackInfo, WgpuFuture> BufferMapAsync;
    public readonly delegate* unmanaged[Cdecl]<nint, nuint, nuint, void*> BufferGetConstMappedRange;
    public readonly delegate* unmanaged[Cdecl]<nint, void> BufferUnmap;

    public readonly delegate* unmanaged[Cdecl]<nint, WgpuTextureViewDescriptor*, nint> TextureCreateView;
    public readonly delegate* unmanaged[Cdecl]<nint, void> TextureDestroy;
    public readonly delegate* unmanaged[Cdecl]<nint, void> TextureRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> TextureViewRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> SamplerRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> ShaderModuleRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> BindGroupLayoutRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> PipelineLayoutRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> BindGroupRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> RenderPipelineRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> ComputePipelineRelease;
    public readonly delegate* unmanaged[Cdecl]<nint, void> CommandBufferRelease;

    public readonly delegate* unmanaged[Cdecl]<nint, WgpuRenderPassDescriptor*, nint> CommandEncoderBeginRenderPass;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuComputePassDescriptor*, nint> CommandEncoderBeginComputePass;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, ulong, nint, ulong, ulong, void> CommandEncoderCopyBufferToBuffer;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuTexelCopyTextureInfo*, WgpuTexelCopyBufferInfo*, WgpuExtent3D*, void> CommandEncoderCopyTextureToBuffer;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuTexelCopyTextureInfo*, WgpuTexelCopyTextureInfo*, WgpuExtent3D*, void> CommandEncoderCopyTextureToTexture;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuLabelDescriptor*, nint> CommandEncoderFinish;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuStringView, void> CommandEncoderPushDebugGroup;
    public readonly delegate* unmanaged[Cdecl]<nint, void> CommandEncoderPopDebugGroup;
    public readonly delegate* unmanaged[Cdecl]<nint, void> CommandEncoderRelease;

    public readonly delegate* unmanaged[Cdecl]<nint, nint, void> RenderPassSetPipeline;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, nint, nuint, uint*, void> RenderPassSetBindGroup;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, nint, ulong, ulong, void> RenderPassSetVertexBuffer;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, uint, ulong, ulong, void> RenderPassSetIndexBuffer;
    public readonly delegate* unmanaged[Cdecl]<nint, float, float, float, float, float, float, void> RenderPassSetViewport;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, uint, uint, uint, void> RenderPassSetScissorRect;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, void*, nuint, void> RenderPassSetImmediates;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, uint, uint, uint, void> RenderPassDraw;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, uint, uint, int, uint, void> RenderPassDrawIndexed;
    public readonly delegate* unmanaged[Cdecl]<nint, WgpuStringView, void> RenderPassPushDebugGroup;
    public readonly delegate* unmanaged[Cdecl]<nint, void> RenderPassPopDebugGroup;
    public readonly delegate* unmanaged[Cdecl]<nint, void> RenderPassEnd;
    public readonly delegate* unmanaged[Cdecl]<nint, void> RenderPassRelease;

    public readonly delegate* unmanaged[Cdecl]<nint, nint, void> ComputePassSetPipeline;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, nint, nuint, uint*, void> ComputePassSetBindGroup;
    public readonly delegate* unmanaged[Cdecl]<nint, uint, uint, uint, void> ComputePassDispatchWorkgroups;
    public readonly delegate* unmanaged[Cdecl]<nint, void> ComputePassEnd;
    public readonly delegate* unmanaged[Cdecl]<nint, void> ComputePassRelease;

    public WgpuApi(nint library)
    {
        Library = library;
        nint L(string name)
        {
            if (!NativeLibrary.TryGetExport(library, name, out var address))
            {
                throw new NotSupportedException($"The wgpu-native library does not export {name}. wgpu-native v29 is required.");
            }

            return address;
        }

        CreateInstance = (delegate* unmanaged[Cdecl]<void*, nint>)L("wgpuCreateInstance");
        InstanceRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuInstanceRelease");
        InstanceProcessEvents = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuInstanceProcessEvents");
        InstanceRequestAdapter = (delegate* unmanaged[Cdecl]<nint, WgpuRequestAdapterOptions*, WgpuCallbackInfo, WgpuFuture>)L("wgpuInstanceRequestAdapter");
        AdapterGetInfo = (delegate* unmanaged[Cdecl]<nint, WgpuAdapterInfo*, uint>)L("wgpuAdapterGetInfo");
        AdapterInfoFreeMembers = (delegate* unmanaged[Cdecl]<WgpuAdapterInfo, void>)L("wgpuAdapterInfoFreeMembers");
        AdapterGetLimits = (delegate* unmanaged[Cdecl]<nint, WgpuLimits*, uint>)L("wgpuAdapterGetLimits");
        AdapterHasFeature = (delegate* unmanaged[Cdecl]<nint, uint, uint>)L("wgpuAdapterHasFeature");
        AdapterRequestDevice = (delegate* unmanaged[Cdecl]<nint, WgpuDeviceDescriptor*, WgpuCallbackInfo, WgpuFuture>)L("wgpuAdapterRequestDevice");
        AdapterRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuAdapterRelease");
        GetVersion = (delegate* unmanaged[Cdecl]<uint>)L("wgpuGetVersion");

        DeviceGetQueue = (delegate* unmanaged[Cdecl]<nint, nint>)L("wgpuDeviceGetQueue");
        DevicePoll = (delegate* unmanaged[Cdecl]<nint, uint, ulong*, uint>)L("wgpuDevicePoll");
        DeviceDestroy = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuDeviceDestroy");
        DeviceRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuDeviceRelease");
        DevicePushErrorScope = (delegate* unmanaged[Cdecl]<nint, uint, void>)L("wgpuDevicePushErrorScope");
        DevicePopErrorScope = (delegate* unmanaged[Cdecl]<nint, WgpuCallbackInfo, WgpuFuture>)L("wgpuDevicePopErrorScope");
        DeviceCreateBuffer = (delegate* unmanaged[Cdecl]<nint, WgpuBufferDescriptor*, nint>)L("wgpuDeviceCreateBuffer");
        DeviceCreateTexture = (delegate* unmanaged[Cdecl]<nint, WgpuTextureDescriptor*, nint>)L("wgpuDeviceCreateTexture");
        DeviceCreateSampler = (delegate* unmanaged[Cdecl]<nint, WgpuSamplerDescriptor*, nint>)L("wgpuDeviceCreateSampler");
        DeviceCreateShaderModule = (delegate* unmanaged[Cdecl]<nint, WgpuShaderModuleDescriptor*, nint>)L("wgpuDeviceCreateShaderModule");
        DeviceCreateBindGroupLayout = (delegate* unmanaged[Cdecl]<nint, WgpuBindGroupLayoutDescriptor*, nint>)L("wgpuDeviceCreateBindGroupLayout");
        DeviceCreatePipelineLayout = (delegate* unmanaged[Cdecl]<nint, WgpuPipelineLayoutDescriptor*, nint>)L("wgpuDeviceCreatePipelineLayout");
        DeviceCreateBindGroup = (delegate* unmanaged[Cdecl]<nint, WgpuBindGroupDescriptor*, nint>)L("wgpuDeviceCreateBindGroup");
        DeviceCreateRenderPipeline = (delegate* unmanaged[Cdecl]<nint, WgpuRenderPipelineDescriptor*, nint>)L("wgpuDeviceCreateRenderPipeline");
        DeviceCreateComputePipeline = (delegate* unmanaged[Cdecl]<nint, WgpuComputePipelineDescriptor*, nint>)L("wgpuDeviceCreateComputePipeline");
        DeviceCreateCommandEncoder = (delegate* unmanaged[Cdecl]<nint, WgpuLabelDescriptor*, nint>)L("wgpuDeviceCreateCommandEncoder");

        QueueRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuQueueRelease");
        QueueSubmit = (delegate* unmanaged[Cdecl]<nint, nuint, nint*, void>)L("wgpuQueueSubmit");
        QueueWriteBuffer = (delegate* unmanaged[Cdecl]<nint, nint, ulong, void*, nuint, void>)L("wgpuQueueWriteBuffer");
        QueueWriteTexture = (delegate* unmanaged[Cdecl]<nint, WgpuTexelCopyTextureInfo*, void*, nuint, WgpuTexelCopyBufferLayout*, WgpuExtent3D*, void>)L("wgpuQueueWriteTexture");

        BufferDestroy = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuBufferDestroy");
        BufferRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuBufferRelease");
        BufferMapAsync = (delegate* unmanaged[Cdecl]<nint, ulong, nuint, nuint, WgpuCallbackInfo, WgpuFuture>)L("wgpuBufferMapAsync");
        BufferGetConstMappedRange = (delegate* unmanaged[Cdecl]<nint, nuint, nuint, void*>)L("wgpuBufferGetConstMappedRange");
        BufferUnmap = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuBufferUnmap");

        TextureCreateView = (delegate* unmanaged[Cdecl]<nint, WgpuTextureViewDescriptor*, nint>)L("wgpuTextureCreateView");
        TextureDestroy = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuTextureDestroy");
        TextureRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuTextureRelease");
        TextureViewRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuTextureViewRelease");
        SamplerRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuSamplerRelease");
        ShaderModuleRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuShaderModuleRelease");
        BindGroupLayoutRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuBindGroupLayoutRelease");
        PipelineLayoutRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuPipelineLayoutRelease");
        BindGroupRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuBindGroupRelease");
        RenderPipelineRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuRenderPipelineRelease");
        ComputePipelineRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuComputePipelineRelease");
        CommandBufferRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuCommandBufferRelease");

        CommandEncoderBeginRenderPass = (delegate* unmanaged[Cdecl]<nint, WgpuRenderPassDescriptor*, nint>)L("wgpuCommandEncoderBeginRenderPass");
        CommandEncoderBeginComputePass = (delegate* unmanaged[Cdecl]<nint, WgpuComputePassDescriptor*, nint>)L("wgpuCommandEncoderBeginComputePass");
        CommandEncoderCopyBufferToBuffer = (delegate* unmanaged[Cdecl]<nint, nint, ulong, nint, ulong, ulong, void>)L("wgpuCommandEncoderCopyBufferToBuffer");
        CommandEncoderCopyTextureToBuffer = (delegate* unmanaged[Cdecl]<nint, WgpuTexelCopyTextureInfo*, WgpuTexelCopyBufferInfo*, WgpuExtent3D*, void>)L("wgpuCommandEncoderCopyTextureToBuffer");
        CommandEncoderCopyTextureToTexture = (delegate* unmanaged[Cdecl]<nint, WgpuTexelCopyTextureInfo*, WgpuTexelCopyTextureInfo*, WgpuExtent3D*, void>)L("wgpuCommandEncoderCopyTextureToTexture");
        CommandEncoderFinish = (delegate* unmanaged[Cdecl]<nint, WgpuLabelDescriptor*, nint>)L("wgpuCommandEncoderFinish");
        CommandEncoderPushDebugGroup = (delegate* unmanaged[Cdecl]<nint, WgpuStringView, void>)L("wgpuCommandEncoderPushDebugGroup");
        CommandEncoderPopDebugGroup = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuCommandEncoderPopDebugGroup");
        CommandEncoderRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuCommandEncoderRelease");

        RenderPassSetPipeline = (delegate* unmanaged[Cdecl]<nint, nint, void>)L("wgpuRenderPassEncoderSetPipeline");
        RenderPassSetBindGroup = (delegate* unmanaged[Cdecl]<nint, uint, nint, nuint, uint*, void>)L("wgpuRenderPassEncoderSetBindGroup");
        RenderPassSetVertexBuffer = (delegate* unmanaged[Cdecl]<nint, uint, nint, ulong, ulong, void>)L("wgpuRenderPassEncoderSetVertexBuffer");
        RenderPassSetIndexBuffer = (delegate* unmanaged[Cdecl]<nint, nint, uint, ulong, ulong, void>)L("wgpuRenderPassEncoderSetIndexBuffer");
        RenderPassSetViewport = (delegate* unmanaged[Cdecl]<nint, float, float, float, float, float, float, void>)L("wgpuRenderPassEncoderSetViewport");
        RenderPassSetScissorRect = (delegate* unmanaged[Cdecl]<nint, uint, uint, uint, uint, void>)L("wgpuRenderPassEncoderSetScissorRect");
        RenderPassSetImmediates = (delegate* unmanaged[Cdecl]<nint, uint, void*, nuint, void>)L("wgpuRenderPassEncoderSetImmediates");
        RenderPassDraw = (delegate* unmanaged[Cdecl]<nint, uint, uint, uint, uint, void>)L("wgpuRenderPassEncoderDraw");
        RenderPassDrawIndexed = (delegate* unmanaged[Cdecl]<nint, uint, uint, uint, int, uint, void>)L("wgpuRenderPassEncoderDrawIndexed");
        RenderPassPushDebugGroup = (delegate* unmanaged[Cdecl]<nint, WgpuStringView, void>)L("wgpuRenderPassEncoderPushDebugGroup");
        RenderPassPopDebugGroup = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuRenderPassEncoderPopDebugGroup");
        RenderPassEnd = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuRenderPassEncoderEnd");
        RenderPassRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuRenderPassEncoderRelease");

        ComputePassSetPipeline = (delegate* unmanaged[Cdecl]<nint, nint, void>)L("wgpuComputePassEncoderSetPipeline");
        ComputePassSetBindGroup = (delegate* unmanaged[Cdecl]<nint, uint, nint, nuint, uint*, void>)L("wgpuComputePassEncoderSetBindGroup");
        ComputePassDispatchWorkgroups = (delegate* unmanaged[Cdecl]<nint, uint, uint, uint, void>)L("wgpuComputePassEncoderDispatchWorkgroups");
        ComputePassEnd = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuComputePassEncoderEnd");
        ComputePassRelease = (delegate* unmanaged[Cdecl]<nint, void>)L("wgpuComputePassEncoderRelease");
    }
}
