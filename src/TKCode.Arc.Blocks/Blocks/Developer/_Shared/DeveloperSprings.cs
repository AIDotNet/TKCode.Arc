using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Springs, durations and tweens measured on the Developer blocks (Arc API keys, Webhooks, Integrations and Env variables,
/// design reference; fitted from frame captures, see <c>design/components/blocks/developer.md</c>). Core presets are
/// re-exported under the name of the motion they drive; the rest are built with <see cref="SpringSpec.FromPhysical"/>.
/// </summary>
internal static class DeveloperSprings
{
    // ---- Springs ----

    /// <summary>
    /// k 171, c 26.2 (critically damped): the pill to panel morph, row heights growing and collapsing, detail panels,
    /// Webhooks selection highlights and bar heights, container heights, Env variables' expanding panels.
    /// </summary>
    public static readonly SpringSpec Smooth = ArcMotionPresets.Smooth;

    /// <summary>k 439, c 41.9: the plus turning into a close cross, count rolls, press scales, the copy slot popping in.</summary>
    public static readonly SpringSpec Quick = ArcMotionPresets.Quick;

    /// <summary>k 252, c 31.7: Integrations rows gliding to their new place (FLIP) and the filter highlight.</summary>
    public static readonly SpringSpec Settle = ArcMotionPresets.Settle;

    /// <summary>Arc's <c>--ease-spring</c> as a spring (critically damped, ω 13.8): chevrons, button press release.</summary>
    public static readonly SpringSpec CssSpring = SpringSpec.FromPhysical(190, 27.6);

    /// <summary>The hold button narrowing after a confirmed revoke (k ≈ 85, c ≈ 16, ζ .88; a rough fit).</summary>
    public static readonly SpringSpec ConfirmWidth = SpringSpec.FromPhysical(85, 16);

    // ---- Durations (Arc measurements) ----

    /// <summary>120ms: count fade-out, reduced-motion crossfades.</summary>
    public static readonly TimeSpan Instant = ArcMotionPresets.Instant;

    /// <summary>160ms: blur swap exits, hover colors, Integrations rows leaving.</summary>
    public static readonly TimeSpan Fast = ArcMotionPresets.Fast;

    /// <summary>180ms: every Env variables collapse (EaseExit).</summary>
    public static readonly TimeSpan Exit = ArcMotionPresets.Exit;

    /// <summary>240ms: entering rows, blur swap entries, row fades.</summary>
    public static readonly TimeSpan Standard = ArcMotionPresets.Standard;

    /// <summary>The morph panel body fades in over 280ms ...</summary>
    public static readonly TimeSpan BodyIn = TimeSpan.FromMilliseconds(280);

    /// <summary>... after 140ms (half way through the morph, hiding the reflow).</summary>
    public static readonly TimeSpan BodyInDelay = TimeSpan.FromMilliseconds(140);

    /// <summary>The morph panel body fades out in 100ms before the panel folds.</summary>
    public static readonly TimeSpan BodyOut = TimeSpan.FromMilliseconds(100);

    /// <summary>Form to reveal inside the panel: the form fades out in 120ms ...</summary>
    public static readonly TimeSpan ContentOut = TimeSpan.FromMilliseconds(120);

    /// <summary>... and the reveal fades in over 320ms ...</summary>
    public static readonly TimeSpan ContentIn = TimeSpan.FromMilliseconds(320);

    /// <summary>... 60ms later, rising 8px.</summary>
    public static readonly TimeSpan ContentInDelay = TimeSpan.FromMilliseconds(60);

    /// <summary>The leading count fades back in over 240ms after 100ms when the panel closes.</summary>
    public static readonly TimeSpan LeadingInDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>A one-time secret develops out of an 8px blur over 480ms ...</summary>
    public static readonly TimeSpan SecretReveal = TimeSpan.FromMilliseconds(480);

    /// <summary>... starting 140ms after the reveal appears.</summary>
    public static readonly TimeSpan SecretRevealDelay = TimeSpan.FromMilliseconds(140);

    /// <summary>Entering rows (Grow) start fading in 80ms after their height starts to grow.</summary>
    public static readonly TimeSpan GrowFadeDelay = TimeSpan.FromMilliseconds(80);

    /// <summary>A new delivery's flash holds for 1.4s before it fades.</summary>
    public static readonly TimeSpan FlashHold = TimeSpan.FromMilliseconds(1400);

    /// <summary>A flash starts fading 300ms after it is released ...</summary>
    public static readonly TimeSpan FlashFadeDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>... and fades over 1200ms.</summary>
    public static readonly TimeSpan FlashFade = TimeSpan.FromMilliseconds(1200);

    /// <summary>Env variables' save glow: 1600ms, holding for the first 30% (480ms) ...</summary>
    public static readonly TimeSpan GlowHold = TimeSpan.FromMilliseconds(480);

    /// <summary>... then fading for 1120ms.</summary>
    public static readonly TimeSpan GlowFade = TimeSpan.FromMilliseconds(1120);

    /// <summary>A revoke strike draws over 480ms (EaseEnter) ...</summary>
    public static readonly TimeSpan Strike = ArcMotionPresets.Considered;

    /// <summary>... the second line 80ms after the first.</summary>
    public static readonly TimeSpan StrikeStagger = TimeSpan.FromMilliseconds(80);

    /// <summary>Hold to revoke / disconnect: 1200ms, linear.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(1200);

    /// <summary>A revoked key row stays (struck through) for 900ms before it collapses.</summary>
    public static readonly TimeSpan RevokeExitDelay = TimeSpan.FromMilliseconds(900);

    /// <summary>A disconnected integration shows "disconnected" for 660ms before it returns to idle.</summary>
    public static readonly TimeSpan DisconnectSettle = TimeSpan.FromMilliseconds(660);

    /// <summary>Copy buttons return to idle after 1.9s.</summary>
    public static readonly TimeSpan CopyReset = TimeSpan.FromMilliseconds(1900);

    /// <summary>Webhooks moves the selection to a new delivery about 500ms after it is inserted (measured 470–545ms).</summary>
    public static readonly TimeSpan SelectionFollow = TimeSpan.FromMilliseconds(500);

    /// <summary>Per-slot stagger of the Webhooks rate swap, right to left.</summary>
    public static readonly TimeSpan DigitStagger = TimeSpan.FromMilliseconds(30);

    /// <summary>Per-bar stagger of the Webhooks volume strip (48 bars ≈ 200ms).</summary>
    public static readonly TimeSpan BarStagger = TimeSpan.FromMilliseconds(4.2);

    /// <summary>Env variables' scope conflict note closes after 3.8s.</summary>
    public static readonly TimeSpan ConflictDismiss = TimeSpan.FromMilliseconds(3800);

    /// <summary>Env variables' "redeployed" banner closes after 2.8s.</summary>
    public static readonly TimeSpan RedeployDoneDismiss = TimeSpan.FromMilliseconds(2800);

    // Simulated server round trips (used only when the block has no action delegate: the Gallery).

    /// <summary>API keys: generating a key (≈ 730ms).</summary>
    public static readonly TimeSpan SimulatedGenerate = TimeSpan.FromMilliseconds(730);

    /// <summary>Webhooks: retrying a delivery (≈ 1.1s).</summary>
    public static readonly TimeSpan SimulatedRetry = TimeSpan.FromMilliseconds(1100);

    /// <summary>Webhooks: adding an endpoint (≈ 760ms).</summary>
    public static readonly TimeSpan SimulatedAddEndpoint = TimeSpan.FromMilliseconds(760);

    /// <summary>Webhooks: sending a test event (≈ 900ms).</summary>
    public static readonly TimeSpan SimulatedSendTest = TimeSpan.FromMilliseconds(900);

    /// <summary>Integrations: connecting an app (1400ms).</summary>
    public static readonly TimeSpan SimulatedConnect = TimeSpan.FromMilliseconds(1400);

    /// <summary>Env variables: redeploying (≈ 1.7s).</summary>
    public static readonly TimeSpan SimulatedRedeploy = TimeSpan.FromMilliseconds(1700);

    // ---- Motion specs ----

    /// <summary>A spring motion.</summary>
    public static MotionSpec Spring(SpringSpec spec, TimeSpan delay = default) => MotionSpec.Spring(spec, delay);

    /// <summary>160ms on Arc's standard curve (.22,1,.36,1): fades out, hover colors.</summary>
    public static MotionSpec FadeOut(TimeSpan delay = default) => MotionSpec.Tween(Fast, ArcEasings.Standard, delay);

    /// <summary>240ms on the enter curve (.16,1,.3,1): fades in.</summary>
    public static MotionSpec FadeIn(TimeSpan delay = default) => MotionSpec.Tween(Standard, ArcEasings.Enter, delay);

    /// <summary>180ms EaseExit (.7,0,.84,0): Env variables collapses.</summary>
    public static MotionSpec ExitTween(TimeSpan delay = default) => MotionSpec.Tween(Exit, ArcEasings.Exit, delay);

    /// <summary>The 120ms fade reduced motion keeps in place of travel, blur and springs.</summary>
    public static MotionSpec ReducedFade(TimeSpan delay = default) => MotionSpec.Tween(Instant, ArcEasings.Standard, delay);

    /// <summary>A tween of <paramref name="duration"/> on <paramref name="easing"/> (default: Arc's standard curve).</summary>
    public static MotionSpec Tween(TimeSpan duration, Easing? easing = null, TimeSpan delay = default) =>
        MotionSpec.Tween(duration, easing ?? ArcEasings.Standard, delay);

    /// <summary>A critically damped spring with natural frequency <paramref name="omega"/> rad/s (k = ω², c = 2ω).</summary>
    public static SpringSpec Critical(double omega)
    {
        if (!(omega > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(omega));
        }
        return SpringSpec.FromPhysical(omega * omega, 2 * omega);
    }
}
