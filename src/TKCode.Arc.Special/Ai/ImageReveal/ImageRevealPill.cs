using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The translucent progress pill of an <see cref="ImageReveal"/>. While generating it reports itself as a read-only
/// progress bar (the step out of the total, named "Step 18 of 30"); otherwise it is the status chip's text.
/// </summary>
internal sealed class ImageRevealPill : Border
{
    /// <summary>True while it counts steps (a progress bar); false for the queued or done chip.</summary>
    public bool IsProgress { get; set; }

    /// <summary>The step shown.</summary>
    public double Value { get; set; }

    /// <summary>The total steps.</summary>
    public double Maximum { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new ImageRevealPillPeer(this);
}

/// <summary>Reports the pill as a progress bar while it counts, as text otherwise.</summary>
internal sealed class ImageRevealPillPeer(ImageRevealPill owner) : ControlAutomationPeer(owner), IRangeValueProvider
{
    private ImageRevealPill Pill => (ImageRevealPill)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        Pill.IsProgress ? AutomationControlType.ProgressBar : AutomationControlType.Text;

    protected override string GetClassNameCore() => "ImageRevealPill";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public bool IsReadOnly => true;

    public double Minimum => 0;

    public double Maximum => Pill.Maximum;

    public double Value => Pill.Value;

    public double SmallChange => 1;

    public double LargeChange => 1;

    public void SetValue(double value) => throw new InvalidOperationException("The image reveal's progress is read only.");
}
