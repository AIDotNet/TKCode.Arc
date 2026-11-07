#if ARC_VIDEO_NATIVE
using Avalonia.Browser;
using Avalonia.Controls;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// Puts a <see cref="BrowserVideoPlayer"/>'s video element on the page through Avalonia's browser native-control support: the
/// browser host hands a <see cref="JSObjectControlHandle"/> parent, and accepts a <see cref="JSObjectControlHandle"/> child,
/// which it places in a positioned element above the Avalonia canvas.
/// </summary>
internal sealed class BrowserVideoNativeHost : NativeControlHost
{
    private readonly BrowserVideoPlayer _player;

    public BrowserVideoNativeHost(BrowserVideoPlayer player)
    {
        _player = player;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (!OperatingSystem.IsBrowser() || parent is not JSObjectControlHandle)
        {
            return base.CreateNativeControlCore(parent);
        }
        return new JSObjectControlHandle(_player.AttachNative());
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (control is JSObjectControlHandle handle && OperatingSystem.IsBrowser())
        {
            _player.DetachNative();
            // Releases this .NET proxy of the element only; the element itself stays with the player.
            handle.Destroy();
            return;
        }
        base.DestroyNativeControlCore(control);
    }
}
#endif
