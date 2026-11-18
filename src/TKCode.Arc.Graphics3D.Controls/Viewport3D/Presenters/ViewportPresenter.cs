using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;

namespace TKCode.Arc;

/// <summary>How a <see cref="Viewport3D"/> gets its pictures into the Avalonia composition tree. The viewport owns
/// the renderer and the frame logic; a presenter owns the graphics context and the surface.</summary>
internal abstract class ViewportPresenter
{
    protected ViewportPresenter(Viewport3D owner) => Owner = owner;

    protected Viewport3D Owner { get; }

    /// <summary>Creates the context and visuals. The viewport is attached to a window.</summary>
    public abstract void Attach();

    /// <summary>Frees everything, including the viewport's renderer.</summary>
    public abstract void Detach();

    /// <summary>Schedules one frame.</summary>
    public abstract void RequestFrame();

    public virtual void Measure(Size availableSize)
    {
    }

    public virtual void Arrange(Size finalSize)
    {
    }

    /// <summary>Draws the presenter's own picture, when it presents through the control's drawing.</summary>
    public virtual void Draw(Avalonia.Media.DrawingContext context, Size size)
    {
    }
}

/// <summary>Presents through Avalonia's <see cref="OpenGlControlBase"/>: the compositor's own OpenGL context family
/// (ANGLE on Windows, desktop OpenGL on Linux).</summary>
internal sealed class GlControlPresenter : ViewportPresenter
{
    private readonly Host _host;

    public GlControlPresenter(Viewport3D owner)
        : base(owner) => _host = new Host(owner) { IsHitTestVisible = false };

    public override void Attach() => Owner.AddPresenterChild(_host);

    public override void Detach() => Owner.RemovePresenterChild(_host);

    public override void RequestFrame() => _host.RequestNextFrameRendering();

    public override void Measure(Size availableSize) => _host.Measure(availableSize);

    public override void Arrange(Size finalSize) => _host.Arrange(new Rect(finalSize));

    private sealed class Host : OpenGlControlBase
    {
        private readonly Viewport3D _owner;

        public Host(Viewport3D owner) => _owner = owner;

        protected override void OnOpenGlInit(GlInterface gl) => _owner.InitRenderer(gl.GetProcAddress);

        protected override void OnOpenGlDeinit(GlInterface gl) => _owner.DisposeRenderer(contextLost: false);

        protected override void OnOpenGlLost()
        {
            _owner.DisposeRenderer(contextLost: true);
            base.OnOpenGlLost();
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb) => _owner.RenderFrame(fb, _owner.PixelSize);
    }
}
