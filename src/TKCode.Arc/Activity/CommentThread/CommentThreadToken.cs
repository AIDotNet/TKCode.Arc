using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// CommentThread geometry from Arc's comment-thread.module.css, plus the accent-independent tints it mixes
/// (send hover, danger hover, the resolved check). Colors that follow the accent come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CommentThreadToken : AbstractControlDesignToken
{
    /// <summary>Radius of the resolved chip (22px; the open thread uses RadiusPanel).</summary>
    public CornerRadius ResolvedRadius { get; set; }

    /// <summary>Body text line height: text-sm × leading-body (14 × 1.4).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Small text line height: text-xs × leading-body (12 × 1.4).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Header: <c>padding: 12px 12px 8px 18px</c>.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Gap between the heading and the resolve button (12px).</summary>
    public double HeaderGap { get; set; }

    /// <summary>The list: <c>padding: 0 8px</c>.</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>A comment: <c>padding: 10px</c>.</summary>
    public Thickness CommentPadding { get; set; }

    /// <summary>Gap between the avatar and the comment (10px).</summary>
    public double CommentGap { get; set; }

    /// <summary>Comment radius (16px).</summary>
    public CornerRadius CommentRadius { get; set; }

    /// <summary>Avatar diameter in comments and in the composer (28px).</summary>
    public double AvatarDiameter { get; set; }

    /// <summary>Vertical gap inside a comment (2px).</summary>
    public double MainGap { get; set; }

    /// <summary>Gap between name and time (8px).</summary>
    public double MetaGap { get; set; }

    /// <summary>Footer: <c>min-height: 30px; margin-top: 4px</c>.</summary>
    public double FooterMinHeight { get; set; }

    public Thickness FooterMargin { get; set; }

    /// <summary>Footer gaps: 8px between columns, 4px between wrapped rows.</summary>
    public double FooterColumnGap { get; set; }

    public double FooterRowGap { get; set; }

    /// <summary>Text actions: 28px tall, <c>padding: 0 8px</c>; icon actions are 28px squares.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    /// <summary>Gap between actions (2px).</summary>
    public double ActionsGap { get; set; }

    /// <summary>The delete prompt: <c>margin-right: 4px</c>.</summary>
    public Thickness PromptMargin { get; set; }

    /// <summary>Picker emoji buttons: 28px circles with 16px emoji.</summary>
    public double EmojiButtonSize { get; set; }

    public double EmojiFontSize { get; set; }

    /// <summary>Emoji are drawn with a color emoji font first (so ❤️ stays red), then the default fallback.</summary>
    public FontFamily EmojiFontFamily { get; set; } = FontFamily.Default;

    /// <summary>Reply toggle: 28px tall, <c>margin: 2px 0 0 -6px; padding: 0 8px 0 6px; gap: 6px</c>.</summary>
    public Thickness ToggleMargin { get; set; }

    public Thickness TogglePadding { get; set; }

    public double ToggleGap { get; set; }

    /// <summary>Avatar stacks: each overlaps by 5px over a 2px surface ring; <c>padding-left: 4px</c>.</summary>
    public double StackOverlap { get; set; }

    public Thickness StackPadding { get; set; }

    public Thickness StackRing { get; set; }

    /// <summary>Replies: <c>margin-left: 24px; padding-left: 12px</c> (12px indent under 420px).</summary>
    public Thickness ReplyIndent { get; set; }

    public Thickness NarrowReplyIndent { get; set; }

    public Thickness ReplyRailGap { get; set; }

    /// <summary>The reply rail runs from 2px below the top to 14px above the bottom.</summary>
    public Thickness ReplyRailInset { get; set; }

    /// <summary>The deleted placeholder: <c>margin: 4px 0 0</c>.</summary>
    public Thickness DeletedMargin { get; set; }

    /// <summary>Empty text: <c>padding: 8px 18px 16px</c>.</summary>
    public Thickness EmptyPadding { get; set; }

    /// <summary>Composer area: <c>margin-top: 4px; padding: 10px 12px 12px</c>.</summary>
    public Thickness ComposerAreaMargin { get; set; }

    public Thickness ComposerAreaPadding { get; set; }

    /// <summary>"Replying to": <c>padding: 0 2px 8px 38px; gap: 6px</c> (2px left under 420px), 22px close button.</summary>
    public Thickness ReplyingPadding { get; set; }

    public Thickness NarrowReplyingPadding { get; set; }

    public double ReplyingGap { get; set; }

    public double ReplyingCloseSize { get; set; }

    /// <summary>Composer grid gaps: 10px between columns, 8px between rows; the avatar sits 5px down.</summary>
    public double ComposerColumnGap { get; set; }

    public double ComposerRowGap { get; set; }

    public Thickness ComposerAvatarMargin { get; set; }

    /// <summary>The compact (edit) composer: <c>margin-top: 4px</c>.</summary>
    public Thickness CompactComposerMargin { get; set; }

    /// <summary>The field: 38–160px tall, <c>padding: 8px 12px</c>, 14px radius.</summary>
    public double FieldMinHeight { get; set; }

    public double FieldMaxHeight { get; set; }

    public Thickness FieldPadding { get; set; }

    public CornerRadius FieldRadius { get; set; }

    /// <summary>Send / Cancel: 38px (32px compact), <c>padding: 0 14px</c>; actions 4px apart.</summary>
    public double SendHeight { get; set; }

    public double CompactSendHeight { get; set; }

    public Thickness SendPadding { get; set; }

    public double ComposerActionsGap { get; set; }

    /// <summary>Send hover: <c>color-mix(in oklab, foreground 86%, background)</c>.</summary>
    public Color SendHoverBackground { get; set; }

    /// <summary>Danger action hover: <c>color-mix(in oklab, danger 9%, transparent)</c>.</summary>
    public Color DangerHoverBackground { get; set; }

    /// <summary>Resolved check circle: <c>color-mix(in oklab, success 14%, transparent)</c>.</summary>
    public Color ResolvedIconBackground { get; set; }

    /// <summary>Suggestions: <c>margin: 6px 0 0; padding: 4px</c>, 14px radius; rows 32px, <c>padding: 0 8px</c>, 10px radius, 8px gap.</summary>
    public Thickness SuggestionsMargin { get; set; }

    public Thickness SuggestionsPadding { get; set; }

    public CornerRadius SuggestionsRadius { get; set; }

    public double SuggestionHeight { get; set; }

    public Thickness SuggestionPadding { get; set; }

    public CornerRadius SuggestionRadius { get; set; }

    public double SuggestionGap { get; set; }

    /// <summary>Resolved chip: <c>padding: 10px 10px 10px 14px; gap: 12px</c>, 24px check circle.</summary>
    public Thickness ResolvedPadding { get; set; }

    public double ResolvedGap { get; set; }

    public double ResolvedIconSize { get; set; }

    /// <summary>Resolve / Reopen: 32px, <c>padding: 0 12px 0 10px; gap: 6px</c>.</summary>
    public double ResolveHeight { get; set; }

    public Thickness ResolvePadding { get; set; }

    public double ResolveGap { get; set; }

    /// <summary>Opacity of a disabled resolve button (.45) and of a disabled send button (.35).</summary>
    public double ResolveDisabledOpacity { get; set; }

    public double SendDisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ResolvedRadius = new CornerRadius(22);
        BodyLineHeight = 14 * 1.4;
        SmallLineHeight = 12 * 1.4;
        HeaderPadding = new Thickness(18, 12, 12, 8);
        HeaderGap = 12;
        ListPadding = new Thickness(8, 0);
        CommentPadding = new Thickness(10);
        CommentGap = 10;
        CommentRadius = new CornerRadius(16);
        AvatarDiameter = 28;
        MainGap = 2;
        MetaGap = 8;
        FooterMinHeight = 30;
        FooterMargin = new Thickness(0, 4, 0, 0);
        FooterColumnGap = 8;
        FooterRowGap = 4;
        ActionHeight = 28;
        ActionPadding = new Thickness(8, 0);
        ActionsGap = 2;
        PromptMargin = new Thickness(0, 0, 4, 0);
        EmojiButtonSize = 28;
        EmojiFontSize = 16;
        EmojiFontFamily = new FontFamily("Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji, $Default");
        ToggleMargin = new Thickness(-6, 2, 0, 0);
        TogglePadding = new Thickness(6, 0, 8, 0);
        ToggleGap = 6;
        StackOverlap = -5;
        StackPadding = new Thickness(4, 0, 0, 0);
        StackRing = new Thickness(2);
        ReplyIndent = new Thickness(24, 0, 0, 0);
        NarrowReplyIndent = new Thickness(12, 0, 0, 0);
        ReplyRailGap = new Thickness(12, 0, 0, 0);
        ReplyRailInset = new Thickness(0, 2, 0, 14);
        DeletedMargin = new Thickness(0, 4, 0, 0);
        EmptyPadding = new Thickness(18, 8, 18, 16);
        ComposerAreaMargin = new Thickness(0, 4, 0, 0);
        ComposerAreaPadding = new Thickness(12, 10, 12, 12);
        ReplyingPadding = new Thickness(38, 0, 2, 8);
        NarrowReplyingPadding = new Thickness(2, 0, 2, 8);
        ReplyingGap = 6;
        ReplyingCloseSize = 22;
        ComposerColumnGap = 10;
        ComposerRowGap = 8;
        ComposerAvatarMargin = new Thickness(0, 5, 0, 0);
        CompactComposerMargin = new Thickness(0, 4, 0, 0);
        FieldMinHeight = 38;
        FieldMaxHeight = 160;
        FieldPadding = new Thickness(12, 8);
        FieldRadius = new CornerRadius(14);
        SendHeight = 38;
        CompactSendHeight = 32;
        SendPadding = new Thickness(14, 0);
        ComposerActionsGap = 4;
        SendHoverBackground = Oklch.MixOklab(n.Foreground, n.Background, 0.14);
        DangerHoverBackground = Oklch.WithAlpha(n.Danger, 0.09);
        ResolvedIconBackground = Oklch.WithAlpha(n.Success, 0.14);
        SuggestionsMargin = new Thickness(0, 6, 0, 0);
        SuggestionsPadding = new Thickness(4);
        SuggestionsRadius = new CornerRadius(14);
        SuggestionHeight = 32;
        SuggestionPadding = new Thickness(8, 0);
        SuggestionRadius = new CornerRadius(10);
        SuggestionGap = 8;
        ResolvedPadding = new Thickness(14, 10, 10, 10);
        ResolvedGap = 12;
        ResolvedIconSize = 24;
        ResolveHeight = 32;
        ResolvePadding = new Thickness(10, 0, 12, 0);
        ResolveGap = 6;
        ResolveDisabledOpacity = 0.45;
        SendDisabledOpacity = 0.35;
    }
}
