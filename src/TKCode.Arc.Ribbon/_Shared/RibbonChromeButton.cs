namespace TKCode.Arc;

/// <summary>
/// A small icon button of the ribbon's own (collapse the ribbon, customize the quick access toolbar): it looks like a small
/// command but is not one, so it is not reported in <see cref="Ribbon.ItemInvoked"/>.
/// </summary>
internal sealed class RibbonChromeButton : RibbonButton
{
    public RibbonChromeButton()
    {
        Size = RibbonItemSize.Small;
    }

    protected override void OnClick() => InvokeWithoutReport();
}
