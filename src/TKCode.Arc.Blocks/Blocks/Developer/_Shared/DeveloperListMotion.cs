using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a <see cref="DeveloperListPanel"/> item that appears enters.</summary>
internal enum DeveloperListEnter
{
    /// <summary>Appears at once.</summary>
    None,

    /// <summary>
    /// Appears at its final place and fades in (optionally out of a blur, <see cref="DeveloperListMotion.EnterBlur"/>):
    /// Integrations rows coming back when a filter widens.
    /// </summary>
    Fade,

    /// <summary>
    /// Grows from height 0 on <see cref="DeveloperListMotion.Grow"/> while it fades in; the rows below are pushed down by
    /// layout (no FLIP): a new key, endpoint, delivery or variable.
    /// </summary>
    Grow
}

/// <summary>How a <see cref="DeveloperListPanel"/> item that disappears leaves.</summary>
internal enum DeveloperListExit
{
    /// <summary>Removed at once.</summary>
    None,

    /// <summary>
    /// Pops out of the layout and fades where it was drawn (optionally into a blur, <see cref="DeveloperListMotion.ExitBlur"/>);
    /// the rows that stay glide into the gap (FLIP). Arc's AnimatePresence popLayout: Integrations filtering.
    /// </summary>
    Fade,

    /// <summary>
    /// Stays in the layout while its height collapses to 0 on <see cref="DeveloperListMotion.Collapse"/> and it fades; the
    /// rows below follow the height: a revoked key, a removed variable.
    /// </summary>
    Collapse
}

/// <summary>
/// A flash behind one row of a <see cref="DeveloperListPanel"/> (drawn in <see cref="DeveloperListPanel.FlashBrush"/>,
/// AccentSubtle by default): it shows at once, holds, then fades.
/// </summary>
internal sealed record DeveloperFlash
{
    /// <summary>How long the flash stays before it starts to fade; null holds it until <see cref="DeveloperListPanel.EndFlash"/>.</summary>
    public TimeSpan? Hold { get; init; }

    /// <summary>Delay between the release (end of <see cref="Hold"/> or EndFlash) and the fade (default 300ms).</summary>
    public TimeSpan FadeDelay { get; init; } = DeveloperSprings.FlashFadeDelay;

    /// <summary>Fade duration (default 1200ms).</summary>
    public TimeSpan FadeDuration { get; init; } = DeveloperSprings.FlashFade;

    /// <summary>Fade curve (default Arc's standard curve, .22,1,.36,1).</summary>
    public Easing Easing { get; init; } = ArcEasings.Standard;

    /// <summary>
    /// Whether the flash still shows (without fading) under reduced motion: true for the "new" highlights of API keys and
    /// Webhooks (Arc only drops the transition), false for Env variables' save glow (Arc drops it entirely). Under Off a
    /// held flash still shows (it marks state), a timed one does not.
    /// </summary>
    public bool ShowsWhenReduced { get; init; } = true;

    /// <summary>Held until released, then 300ms + 1200ms fade: a new API key until its reveal closes, a new endpoint until Done.</summary>
    public static readonly DeveloperFlash Held = new();

    /// <summary>Holds 1.4s, then 300ms + 1200ms fade: a new Webhooks delivery (retry or test).</summary>
    public static readonly DeveloperFlash Timed = new() { Hold = DeveloperSprings.FlashHold };

    /// <summary>Env variables' save glow: 1600ms in all, holding for 480ms then fading for 1120ms; none under reduced motion.</summary>
    public static readonly DeveloperFlash Glow = new()
    {
        Hold = DeveloperSprings.GlowHold,
        FadeDelay = TimeSpan.Zero,
        FadeDuration = DeveloperSprings.GlowFade,
        ShowsWhenReduced = false,
    };
}

/// <summary>
/// The motion a <see cref="DeveloperListPanel"/> plays when its items change: how new items enter, how removed items
/// leave, how staying items glide (FLIP) and whether the panel's own height springs. A record: derive variants with
/// <c>with</c> (<c>DeveloperListMotion.Insert with { Flash = DeveloperFlash.Held }</c>).
/// </summary>
internal sealed record DeveloperListMotion
{
    /// <summary>How new items enter (default Fade).</summary>
    public DeveloperListEnter Enter { get; init; } = DeveloperListEnter.Fade;

    /// <summary>How removed items leave (default Fade).</summary>
    public DeveloperListExit Exit { get; init; } = DeveloperListExit.Fade;

    /// <summary>Blur (px) an entering item clears as it fades in (Full motion only; Integrations 4).</summary>
    public double EnterBlur { get; init; }

    /// <summary>Blur (px) a leaving item reaches as it fades out (Full motion only; Integrations 4).</summary>
    public double ExitBlur { get; init; }

    /// <summary>Opacity motion of entering items (default 240ms enter curve).</summary>
    public MotionSpec EnterFade { get; init; } = DeveloperSprings.FadeIn();

    /// <summary>Grow only: the opacity follows the height spring instead of <see cref="EnterFade"/> (Env variables).</summary>
    public bool FadeFollowsHeight { get; init; }

    /// <summary>Opacity motion of leaving items, both exit modes (default 160ms standard curve).</summary>
    public MotionSpec ExitFade { get; init; } = DeveloperSprings.FadeOut();

    /// <summary>Height motion of growing items, 0 → 1 (default Smooth).</summary>
    public MotionSpec Grow { get; init; } = DeveloperSprings.Spring(DeveloperSprings.Smooth);

    /// <summary>Height motion of collapsing items, 1 → 0 (default Smooth).</summary>
    public MotionSpec Collapse { get; init; } = DeveloperSprings.Spring(DeveloperSprings.Smooth);

    /// <summary>FLIP motion of staying items whose place changed (default Smooth; a running glide keeps its velocity).</summary>
    public MotionSpec Move { get; init; } = DeveloperSprings.Spring(DeveloperSprings.Smooth);

    /// <summary>Motion of the panel's own height when its content height jumps; null follows the content at once.</summary>
    public MotionSpec? Container { get; init; }

    /// <summary>Flash played on every item that enters through a change (null: none).</summary>
    public DeveloperFlash? Flash { get; init; }

    /// <summary>
    /// Integrations filtering and search: leaving rows fade into a 4px blur in place (160ms), staying rows glide on Settle,
    /// returning rows fade out of a 4px blur (240ms enter curve), the list height springs (Smooth).
    /// </summary>
    public static readonly DeveloperListMotion Filter = new()
    {
        Enter = DeveloperListEnter.Fade,
        Exit = DeveloperListExit.Fade,
        EnterBlur = 4,
        ExitBlur = 4,
        EnterFade = DeveloperSprings.FadeIn(),
        ExitFade = DeveloperSprings.FadeOut(),
        Move = DeveloperSprings.Spring(DeveloperSprings.Settle),
        Container = DeveloperSprings.Spring(DeveloperSprings.Smooth),
    };

    /// <summary>
    /// API keys and Webhooks rows: new rows grow on Smooth and fade in over 240ms after 80ms; removed rows collapse on
    /// Smooth while fading over 240ms (a revoked key). Add a flash with <c>with { Flash = ... }</c>.
    /// </summary>
    public static readonly DeveloperListMotion Insert = new()
    {
        Enter = DeveloperListEnter.Grow,
        Exit = DeveloperListExit.Collapse,
        EnterFade = DeveloperSprings.FadeIn(DeveloperSprings.GrowFadeDelay),
        ExitFade = DeveloperSprings.Tween(DeveloperSprings.Standard),
        Grow = DeveloperSprings.Spring(DeveloperSprings.Smooth),
        Collapse = DeveloperSprings.Spring(DeveloperSprings.Smooth),
        Move = DeveloperSprings.Spring(DeveloperSprings.Smooth),
    };

    /// <summary>
    /// Env variables (asymmetric): new and returning rows grow on Smooth with the opacity on the same spring; removed and
    /// filtered-out rows collapse and fade in 180ms EaseExit. No flash (search must not glow): call
    /// <see cref="DeveloperListPanel.Flash"/> with <see cref="DeveloperFlash.Glow"/> after an add, import or save.
    /// </summary>
    public static readonly DeveloperListMotion Disclosure = new()
    {
        Enter = DeveloperListEnter.Grow,
        Exit = DeveloperListExit.Collapse,
        FadeFollowsHeight = true,
        ExitFade = DeveloperSprings.ExitTween(),
        Grow = DeveloperSprings.Spring(DeveloperSprings.Smooth),
        Collapse = DeveloperSprings.ExitTween(),
        Move = DeveloperSprings.Spring(DeveloperSprings.Smooth),
    };

    /// <summary>No motion: items appear and disappear at once (a reset, Restore sample data, a view switch).</summary>
    public static readonly DeveloperListMotion Instant = new()
    {
        Enter = DeveloperListEnter.None,
        Exit = DeveloperListExit.None,
    };
}
