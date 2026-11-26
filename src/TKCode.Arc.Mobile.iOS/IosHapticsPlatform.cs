using UIKit;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Native iOS haptic feedback bridge for <see cref="Haptics"/>, using UIKit's feedback generators
/// (<c>UISelectionFeedbackGenerator</c>, <c>UIImpactFeedbackGenerator</c>, <c>UINotificationFeedbackGenerator</c>).
/// Installed automatically by <see cref="ArcAppDelegate{TApp}"/>.
/// </summary>
internal sealed class IosHapticsPlatform : IHapticsPlatform
{
    private readonly UISelectionFeedbackGenerator _selection = new();
    private readonly UIImpactFeedbackGenerator _light = new(UIImpactFeedbackStyle.Light);
    private readonly UIImpactFeedbackGenerator _medium = new(UIImpactFeedbackStyle.Medium);
    private readonly UIImpactFeedbackGenerator _heavy = new(UIImpactFeedbackStyle.Heavy);
    private readonly UINotificationFeedbackGenerator _notification = new();

    public void Trigger(HapticKind kind)
    {
        switch (kind)
        {
            case HapticKind.Selection:
                _selection.SelectionChanged();
                break;
            case HapticKind.Light:
                _light.ImpactOccurred();
                break;
            case HapticKind.Medium:
                _medium.ImpactOccurred();
                break;
            case HapticKind.Heavy:
                _heavy.ImpactOccurred();
                break;
            case HapticKind.Success:
                _notification.NotificationOccurred(UINotificationFeedbackType.Success);
                break;
            case HapticKind.Warning:
                _notification.NotificationOccurred(UINotificationFeedbackType.Warning);
                break;
            case HapticKind.Error:
                _notification.NotificationOccurred(UINotificationFeedbackType.Error);
                break;
        }
    }
}
