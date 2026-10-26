namespace TKCode.Arc.Mobile;

/// <summary>
/// A source of the soft keyboard's height for the <see cref="Page"/>s below it in the tree. The platform's own input pane
/// (<c>TopLevel.InputPane</c>) is used where there is one; a host that has none, such as a desktop simulation of a phone,
/// creates one of these, calls <see cref="Set"/> when its keyboard moves and hands it to the pages with
/// <see cref="Page.SetKeyboardInsetSource"/>.
/// </summary>
public sealed class KeyboardInsetSource
{
    /// <summary>The height the keyboard covers at the bottom edge, in device-independent pixels; 0 when it is hidden.</summary>
    public double Inset { get; private set; }

    /// <summary>
    /// How long the keyboard takes to reach <see cref="Inset"/>, or null when it is not known and the page uses its own spring.
    /// </summary>
    public TimeSpan? Duration { get; private set; }

    /// <summary>Raised after <see cref="Set"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Reports that the keyboard now covers <paramref name="inset"/> pixels, arriving over <paramref name="duration"/> if known.</summary>
    public void Set(double inset, TimeSpan? duration = null)
    {
        Inset = Math.Max(0, inset);
        Duration = duration;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
