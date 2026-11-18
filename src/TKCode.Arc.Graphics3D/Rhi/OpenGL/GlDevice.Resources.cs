using static TKCode.Arc.Graphics3D.Rhi.OpenGL.GlConst;

namespace TKCode.Arc.Graphics3D.Rhi.OpenGL;

// The device's resources: each wraps one OpenGL object and deletes it when released.
public sealed unsafe partial class GlDevice
{
    private sealed class GlBuffer : GpuBuffer
    {
        private readonly GlDevice _device;

        public GlBuffer(GlDevice device, uint id, BufferUsage usage, int size, bool dynamic)
        {
            _device = device;
            Id = id;
            Usage = usage;
            SizeInBytes = size;
            Dynamic = dynamic;
            Target = usage switch
            {
                BufferUsage.Index => ELEMENT_ARRAY_BUFFER,
                BufferUsage.Uniform => UNIFORM_BUFFER,
                _ => ARRAY_BUFFER,
            };
        }

        public uint Id { get; }

        public int Target { get; }

        public bool Dynamic { get; }

        protected override void Release()
        {
            var id = Id;
            _device._gl.DeleteBuffers(1, &id);
            Array.Fill(_device._slotBuffer, uint.MaxValue);
        }
    }

    private sealed class GlTexture : GpuTexture
    {
        private readonly GlDevice _device;

        public GlTexture(GlDevice device, uint id, int target, TextureDescriptor descriptor, bool isRenderbuffer)
        {
            _device = device;
            Id = id;
            Target = target;
            Descriptor = descriptor;
            IsRenderbuffer = isRenderbuffer;
        }

        public uint Id { get; }

        public int Target { get; }

        public bool IsRenderbuffer { get; }

        protected override void Release()
        {
            var id = Id;
            if (IsRenderbuffer)
            {
                _device._gl.DeleteRenderbuffers(1, &id);
            }
            else
            {
                _device._gl.DeleteTextures(1, &id);
                Array.Fill(_device._unitTexture, uint.MaxValue);
            }
        }
    }

    private sealed class GlSampler : GpuSampler
    {
        private readonly GlDevice _device;

        public GlSampler(GlDevice device, uint id)
        {
            _device = device;
            Id = id;
        }

        public uint Id { get; }

        protected override void Release()
        {
            var id = Id;
            _device._gl.DeleteSamplers(1, &id);
            Array.Fill(_device._unitSampler, uint.MaxValue);
        }
    }

    private sealed class GlProgram : GpuProgram
    {
        private readonly GlDevice _device;

        public GlProgram(GlDevice device, uint id, int drawLocation)
        {
            _device = device;
            Id = id;
            DrawLocation = drawLocation;
        }

        public uint Id { get; }

        public int DrawLocation { get; }

        public int Draw0, Draw1, Draw2, Draw3;

        public override bool IsReady => true;

        protected override void Release()
        {
            if (ReferenceEquals(_device._program, this))
            {
                _device._program = null;
            }

            _device._gl.DeleteProgram(Id);
        }
    }

    private sealed class GlVertexInput : GpuVertexInput
    {
        private readonly GlDevice _device;

        public GlVertexInput(GlDevice device, uint id, int indexSize)
        {
            _device = device;
            Id = id;
            IndexSize = indexSize;
        }

        public uint Id { get; }

        public int IndexSize { get; }

        protected override void Release()
        {
            if (ReferenceEquals(_device._vertexInput, this))
            {
                _device._vertexInput = null;
            }

            var id = Id;
            _device._gl.DeleteVertexArrays(1, &id);
            _device._boundVao = uint.MaxValue;
        }
    }

    private sealed class GlFramebuffer : GpuFramebuffer
    {
        private readonly GlDevice _device;

        public GlFramebuffer(GlDevice device, uint id, int width, int height)
        {
            _device = device;
            Id = id;
            Width = width;
            Height = height;
        }

        public uint Id { get; }

        protected override void Release()
        {
            var id = Id;
            _device._gl.DeleteFramebuffers(1, &id);
            _device._boundFbo = uint.MaxValue;
        }
    }
}
