using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>Compute shaders: real kernels on the GPU, compared with the same computation on the CPU. They need a device
/// with compute shaders, which is the WebGPU backend (<c>TKCODE_ARC_WGPU</c>); OpenGL 3.3 / OpenGL ES 3.0 have none, and
/// the last test pins what the OpenGL device does instead.</summary>
public class ComputeTests
{
    private static GlTestContext CreateComputeDeviceOrSkip()
    {
        var context = GlTestContext.CreateOrSkip();
        if (!context.Device.Capabilities.ComputeShaders)
        {
            context.Dispose();
            Assert.Skip("The device has no compute shaders (set TKCODE_ARC_WGPU to run on the WebGPU backend).");
        }

        return context;
    }

    // Particles under gravity that bounce on the floor and lose speed there.
    private const string ParticleKernel = """
    vec4 p = position[index];
    vec4 v = velocity[index];
    float dt = u_params[1].x;
    v.xyz += u_params[0].xyz * dt;
    p.xyz += v.xyz * dt;
    if (p.y < 0.0) {
        p.y = -p.y;
        v.y = -v.y * u_params[1].y;
    }
    position[index] = p;
    velocity[index] = v;
""";

    [Fact]
    public void Kernel_Integrates_Particles_Like_The_Cpu()
    {
        using var gl = CreateComputeDeviceOrSkip();
        const int Count = 5000;
        const int Steps = 120;
        const float Dt = 1f / 60f;
        const float Restitution = 0.6f;
        var gravity = new Vector3(0f, -9.81f, 0f);
        var random = new Random(7);
        var positions = new Vector4[Count];
        var velocities = new Vector4[Count];
        for (var i = 0; i < Count; i++)
        {
            positions[i] = new Vector4((random.NextSingle() * 10f) - 5f, random.NextSingle() * 4f, (random.NextSingle() * 10f) - 5f, 1f);
            velocities[i] = new Vector4((random.NextSingle() * 2f) - 1f, random.NextSingle() * 3f, (random.NextSingle() * 2f) - 1f, 0f);
        }

        using var compute = new GpuCompute(gl.Device, new GpuComputeDescriptor
        {
            Kernel = ParticleKernel,
            Buffers = [new ComputeBufferLayout("position", ComputeElement.Vec4), new ComputeBufferLayout("velocity", ComputeElement.Vec4)],
            Parameters = 2,
            Label = "particles",
        });
        using var positionBuffer = compute.CreateBuffer<Vector4>(0, positions);
        using var velocityBuffer = compute.CreateBuffer<Vector4>(1, velocities);
        compute.SetParameter(0, new Vector4(gravity, 0f));
        compute.SetParameter(1, new Vector4(Dt, Restitution, 0f, 0f));
        GpuBuffer[] buffers = [positionBuffer, velocityBuffer];
        for (var step = 0; step < Steps; step++)
        {
            compute.Run(Count, buffers);
        }

        // The same steps on the CPU, in the same order of operations.
        for (var step = 0; step < Steps; step++)
        {
            for (var i = 0; i < Count; i++)
            {
                var p = positions[i];
                var v = velocities[i];
                v.X += gravity.X * Dt;
                v.Y += gravity.Y * Dt;
                v.Z += gravity.Z * Dt;
                p.X += v.X * Dt;
                p.Y += v.Y * Dt;
                p.Z += v.Z * Dt;
                if (p.Y < 0f)
                {
                    p.Y = -p.Y;
                    v.Y = -v.Y * Restitution;
                }

                positions[i] = p;
                velocities[i] = v;
            }
        }

        var gpuPositions = new Vector4[Count];
        var gpuVelocities = new Vector4[Count];
        compute.Read<Vector4>(positionBuffer, gpuPositions);
        compute.Read<Vector4>(velocityBuffer, gpuVelocities);
        gl.Device.DrainErrors().ShouldBeEmpty();

        // The GPU may contract a multiply and an add into one operation, so the results agree closely, not bit for bit.
        // A particle that lands a hair to the other side of the floor in one of the two runs bounces a step apart; such
        // particles are few and are counted instead of compared.
        var apart = 0;
        var worst = 0f;
        for (var i = 0; i < Count; i++)
        {
            var error = MathF.Max((gpuPositions[i] - positions[i]).Length(), (gpuVelocities[i] - velocities[i]).Length());
            if (error > 0.05f)
            {
                apart++;
            }
            else
            {
                worst = MathF.Max(worst, error);
            }
        }

        worst.ShouldBeLessThan(2e-3f);
        apart.ShouldBeLessThan(Count / 100);

        // Something happened at all: the particles fell and bounced.
        gpuPositions.ShouldAllBe(p => p.Y >= 0f && p.W == 1f);
        gpuVelocities.Count(v => v.Y > 0f).ShouldBeGreaterThan(Count / 20);
    }

    [Fact]
    public void Kernel_Transforms_Instance_Matrices_Like_The_Cpu()
    {
        using var gl = CreateComputeDeviceOrSkip();
        const int Count = 3000;
        var parent = Matrix4x4.CreateScale(1.5f, 0.75f, 2f) * Matrix4x4.CreateFromYawPitchRoll(0.4f, -0.3f, 0.9f) * Matrix4x4.CreateTranslation(3f, -2f, 5f);
        var random = new Random(3);
        var local = new Matrix4x4[Count];
        for (var i = 0; i < Count; i++)
        {
            local[i] = Matrix4x4.CreateFromYawPitchRoll(random.NextSingle() * 6f, random.NextSingle() * 6f, random.NextSingle() * 6f)
                * Matrix4x4.CreateTranslation((random.NextSingle() * 20f) - 10f, (random.NextSingle() * 20f) - 10f, (random.NextSingle() * 20f) - 10f);
        }

        // The engine's matrices have the memory layout of GLSL's (design/architecture/graphics3d.md): "local, then parent"
        // is local * parent here and parent * local in the shader.
        using var compute = new GpuCompute(gl.Device, new GpuComputeDescriptor
        {
            Kernel = "    world[index] = mat4(u_params[0], u_params[1], u_params[2], u_params[3]) * local[index];\n    done[index] = index + 1u;",
            Buffers =
            [
                new ComputeBufferLayout("local", ComputeElement.Mat4, ReadOnly: true),
                new ComputeBufferLayout("world", ComputeElement.Mat4),
                new ComputeBufferLayout("done", ComputeElement.UInt),
            ],
            Parameters = 4,
            Label = "instance-transforms",
        });
        using var localBuffer = compute.CreateBuffer<Matrix4x4>(0, local);

        // Larger than the run: the elements after it must stay untouched.
        using var worldBuffer = compute.CreateBuffer(1, Count + 100);
        using var doneBuffer = compute.CreateBuffer(2, Count + 100);
        compute.SetParameter(0, new Vector4(parent.M11, parent.M12, parent.M13, parent.M14));
        compute.SetParameter(1, new Vector4(parent.M21, parent.M22, parent.M23, parent.M24));
        compute.SetParameter(2, new Vector4(parent.M31, parent.M32, parent.M33, parent.M34));
        compute.SetParameter(3, new Vector4(parent.M41, parent.M42, parent.M43, parent.M44));
        compute.Run(Count, [localBuffer, worldBuffer, doneBuffer]);

        var world = new Matrix4x4[Count + 100];
        var done = new uint[Count + 100];
        compute.Read<Matrix4x4>(worldBuffer, world);
        compute.Read<uint>(doneBuffer, done);
        gl.Device.DrainErrors().ShouldBeEmpty();
        var worst = 0f;
        for (var i = 0; i < Count; i++)
        {
            var expected = local[i] * parent;
            var a = MemoryMarshal.CreateReadOnlySpan(ref expected.M11, 16);
            var b = MemoryMarshal.CreateReadOnlySpan(ref world[i].M11, 16);
            for (var c = 0; c < 16; c++)
            {
                worst = MathF.Max(worst, MathF.Abs(a[c] - b[c]));
            }

            done[i].ShouldBe((uint)i + 1);
        }

        worst.ShouldBeLessThan(1e-4f);
        for (var i = Count; i < Count + 100; i++)
        {
            done[i].ShouldBe(0u);
            world[i].ShouldBe(default);
        }
    }

    [Fact]
    public void Kernel_That_Does_Not_Compile_Says_Why()
    {
        using var gl = CreateComputeDeviceOrSkip();
        var error = Should.Throw<ShaderCompilationException>(() => new GpuCompute(gl.Device, new GpuComputeDescriptor
        {
            Kernel = "    values[index] = notDeclared;",
            Buffers = [new ComputeBufferLayout("values", ComputeElement.Float)],
        }));
        error.Message.ShouldContain("compute");
        error.ShaderSource.ShouldNotBeNull().ShouldContain("values[index] = notDeclared");
        gl.Device.DrainErrors().ShouldBeEmpty();
    }

    [Fact]
    public void Raw_Compute_Program_Reads_A_Uniform_Block_And_Writes_A_Storage_Buffer()
    {
        // The hardware interface without the helper: bindings by the slot mapping of ShaderLanguage.Glsl450.
        using var gl = CreateComputeDeviceOrSkip();
        var device = gl.Device;
        using var program = device.CreateComputeProgram(new ComputeProgramDescriptor
        {
            Label = "raw",
            Source = """
#version 450
layout(local_size_x = 16) in;
layout(std140, set = 0, binding = 5) uniform Params {
    vec4 u_scale;
};
layout(std430, set = 2, binding = 1) buffer Values {
    float values[];
};
void main() {
    uint i = gl_GlobalInvocationID.x;
    values[i] = values[i] * u_scale.x + u_scale.y;
}
""",
        });
        const int Count = 64;
        var input = new float[Count];
        for (var i = 0; i < Count; i++)
        {
            input[i] = i;
        }

        using var values = device.CreateBuffer(BufferUsage.Storage, Count * 4, true, "values");
        device.WriteBuffer(values, 0, MemoryMarshal.AsBytes<float>(input));
        using var uniforms = device.CreateBuffer(BufferUsage.Uniform, 16, true, "params");
        device.WriteBuffer(uniforms, 0, MemoryMarshal.AsBytes<float>([3f, 0.5f, 0f, 0f]));
        device.SetUniformBuffer(5, uniforms, 0, 16);
        device.SetStorageBuffer(1, values);
        device.DispatchCompute(program, Count / 16);
        var output = new float[Count];
        device.ReadBuffer(values, 0, MemoryMarshal.AsBytes(output.AsSpan()));

        // Part of the buffer, from an offset that is not a multiple of four bytes' worth of elements.
        var three = new float[3];
        device.ReadBuffer(values, 5 * 4, MemoryMarshal.AsBytes(three.AsSpan()));
        device.DrainErrors().ShouldBeEmpty();
        for (var i = 0; i < Count; i++)
        {
            output[i].ShouldBe((i * 3f) + 0.5f);
        }

        three.ShouldBe([15.5f, 18.5f, 21.5f]);
    }

    [Fact]
    public void Device_Without_Compute_Shaders_Says_So()
    {
        using var gl = GlTestContext.CreateOrSkip();
        var device = gl.Device;
        if (device.Capabilities.ComputeShaders)
        {
            GpuCompute.IsSupported(device).ShouldBeTrue();
            device.Capabilities.MaxStorageBuffers.ShouldBeGreaterThan(0);
            return;
        }

        // OpenGL 3.3 and OpenGL ES 3.0: no compute stage, no storage buffers.
        device.Capabilities.ShaderLanguage.ShouldNotBe(ShaderLanguage.Glsl450);
        GpuCompute.IsSupported(device).ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => device.CreateBuffer(BufferUsage.Storage, 64, false));
        Should.Throw<NotSupportedException>(() => device.CreateComputeProgram(new ComputeProgramDescriptor { Source = "void main() {}" }));
        Should.Throw<NotSupportedException>(() => new GpuCompute(device, new GpuComputeDescriptor
        {
            Kernel = "",
            Buffers = [new ComputeBufferLayout("values", ComputeElement.Float)],
        }));
        device.DrainErrors().ShouldBeEmpty();
    }
}
