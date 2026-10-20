using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Which Arc thread a reaction chip belongs to (they differ in border, chosen colors and spring).</summary>
internal enum ActivityReactionChipVariant
{
    /// <summary>CommentThread: Border outline; chosen = AccentSubtle fill and a 45% accent border; snappy spring, enters from 0.7.</summary>
    Comment,

    /// <summary>ChatThread: BorderSubtle outline; chosen = 8% accent fill and a 28% accent border; pop spring, enters from 0.6.</summary>
    Chat
}

/// <summary>One reaction shown under a comment or message: its emoji, how many reacted, and whether the viewer did.</summary>
/// <param name="Emoji">The emoji (also the chip's key).</param>
/// <param name="Count">How many people reacted.</param>
/// <param name="Mine">True when the viewer's own reaction is included.</param>
internal readonly record struct ActivityReaction(string Emoji, int Count, bool Mine);

/// <summary>
/// A reaction chip (Arc's <c>.reaction</c> button): emoji and a count that rolls (Core <see cref="Primitives.RollingDigits"/>,
/// P14), a toggle whose pressed state is the viewer's reaction. The data owns the state: a click only raises
/// <see cref="Avalonia.Controls.Button.Click"/> and the thread updates its data. Presence (P26) is code-driven so the exit can gate the
/// chip's removal and share one transform with the bar's FLIP glide (P19).
/// </summary>
internal sealed class ActivityReactionChip : ToggleButton
{
    public static readonly StyledProperty<string?> EmojiProperty =
        AvaloniaProperty.Register<ActivityReactionChip, string?>(nameof(Emoji));

    public static readonly StyledProperty<double> CountProperty =
        AvaloniaProperty.Register<ActivityReactionChip, double>(nameof(Count));

    public static readonly StyledProperty<ActivityReactionChipVariant> VariantProperty =
        AvaloniaProperty.Register<ActivityReactionChip, ActivityReactionChipVariant>(nameof(Variant));

    public string? Emoji { get => GetValue(EmojiProperty); set => SetValue(EmojiProperty, value); }
    public double Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public ActivityReactionChipVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    protected override Type StyleKeyOverride => typeof(ActivityReactionChip);

    private static readonly SpringSpec s_pop = SpringSpec.FromVisual(0.3, 0.22);

    private MotionValue<double>? _scale;
    private MotionValue<double>? _fade;
    private int _generation;

    /// <summary>True while the exit plays (out of the flow, not focusable).</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>Presence scale applied by the bar together with its FLIP offset.</summary>
    public double PresenceScale => _scale?.Value ?? 1;

    /// <summary>The spring of this chip's presence and of its glide in the bar.</summary>
    public SpringSpec Spring => Variant == ActivityReactionChipVariant.Chat ? s_pop : ArcMotionPresets.Snappy;

    private double EnterScale => Variant == ActivityReactionChipVariant.Chat ? 0.6 : 0.7;

    /// <summary>The data owns the pressed state: a click asks the thread to toggle the reaction instead.</summary>
    protected override void Toggle()
    {
    }

    /// <summary>
    /// Enters: a <paramref name="fresh"/> chip starts at <see cref="EnterScale"/> and transparent; one revived mid-exit
    /// turns around from where it is. Reduced fades only (fast), Off shows it at once.
    /// </summary>
    public void Enter(bool animate, bool fresh)
    {
        _generation++;
        IsLeaving = false;
        Focusable = true;
        IsHitTestVisible = true;
        _scale ??= CreateValue();
        _fade ??= CreateValue();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _scale.Jump(1);
            _fade.Jump(1);
            Apply();
            return;
        }
        if (fresh)
        {
            _fade.Jump(0);
            _scale.Jump(level == ArcMotionLevel.Full ? EnterScale : 1);
        }
        if (level == ArcMotionLevel.Full)
        {
            _ = _scale.AnimateTo(1, MotionSpec.Spring(Spring));
            _ = _fade.AnimateTo(1, MotionSpec.Spring(Spring));
        }
        else
        {
            _scale.Jump(1);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        }
        Apply();
    }

    /// <summary>Leaves (back to <see cref="EnterScale"/>, fading); completes true when the exit finished and was not revived.</summary>
    public async Task<bool> LeaveAsync(bool animate)
    {
        var generation = ++_generation;
        IsLeaving = true;
        Focusable = false;
        IsHitTestVisible = false;
        _scale ??= CreateValue();
        _fade ??= CreateValue();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _fade.Jump(0);
            Apply();
            return generation == _generation;
        }
        Task<bool> fade;
        if (level == ArcMotionLevel.Full)
        {
            _ = _scale.AnimateTo(EnterScale, MotionSpec.Spring(Spring));
            fade = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            fade = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        var done = await fade;
        return done && generation == _generation && IsLeaving;
    }

    private MotionValue<double> CreateValue()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => Apply();
        return value;
    }

    private void Apply()
    {
        Opacity = Math.Clamp(_fade?.Value ?? 1, 0, 1);
        (this.GetVisualParent() as ActivityReactionBar)?.UpdateTransform(this);
    }
}
