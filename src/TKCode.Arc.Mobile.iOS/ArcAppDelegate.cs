using Avalonia;
using Avalonia.Input.TextInput;
using Avalonia.iOS;
using Foundation;
using ObjCRuntime;

namespace TKCode.Arc.Mobile;

/// <summary>
/// An <see cref="AvaloniaAppDelegate{TApp}"/> that answers the iOS edit actions for Avalonia text boxes, so the
/// keyboard's "Paste from …" suggestion and the system edit menu work in a <see cref="TextField"/> or any
/// <c>TextBox</c>, and <see cref="TextEditMenu"/> shows the system edit menu on iOS 16 and later. Derive the app's
/// delegate from it in place of <c>AvaloniaAppDelegate&lt;App&gt;</c>; see <see cref="ArcIosTextEditing"/>.
/// </summary>
/// <typeparam name="TApp">The Avalonia application.</typeparam>
public class ArcAppDelegate<TApp> : AvaloniaAppDelegate<TApp> where TApp : Application, new()
{
    /// <summary>Creates the delegate and installs <see cref="ArcIosTextEditing"/>.</summary>
    public ArcAppDelegate()
    {
        ArcIosTextEditing.Install();
    }

    /// <inheritdoc />
    public override bool CanPerform(Selector action, NSObject? withSender)
        => ArcIosTextEditing.CanPerform(action) ?? base.CanPerform(action, withSender);

    /// <inheritdoc />
    public override void Cut(NSObject? sender) => ArcIosTextEditing.Perform(ContextMenuAction.Cut);

    /// <inheritdoc />
    public override void Copy(NSObject? sender) => ArcIosTextEditing.Perform(ContextMenuAction.Copy);

    /// <inheritdoc />
    public override void Paste(NSObject? sender) => ArcIosTextEditing.Perform(ContextMenuAction.Paste);

    /// <inheritdoc />
    public override void SelectAll(NSObject? sender) => ArcIosTextEditing.Perform(ContextMenuAction.SelectAll);
}
