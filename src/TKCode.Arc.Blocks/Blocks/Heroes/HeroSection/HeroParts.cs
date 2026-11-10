// Ported from Arc's free hero-section block (MIT, Copyright (c) 2026 Elia Kuratli): ActionLink of hero-content.tsx, hero-motion.ts and the
// shared title styles of hero-section.module.css.

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The design of a <see cref="HeroSection"/>.</summary>
public enum HeroSectionVariant
{
    /// <summary>A headline over a drifting mesh, with a live dashboard rising from the bottom edge.</summary>
    Centered,

    /// <summary>The copy beside a live workflow graph that routes sample events node by node.</summary>
    Split,

    /// <summary>Large type over a mesh gradient, with customer marks along the bottom edge.</summary>
    Minimal
}

/// <summary>A hero's action: a <see cref="CtaAction"/> that can also mark its address as leaving the app.</summary>
public class HeroAction : CtaAction
{
    /// <summary>The address opens outside the app: the action shows an outward arrow.</summary>
    public static readonly StyledProperty<bool> IsExternalProperty =
        AvaloniaProperty.Register<HeroAction, bool>(nameof(IsExternal));

    /// <inheritdoc cref="IsExternalProperty"/>
    public bool IsExternal { get => GetValue(IsExternalProperty); set => SetValue(IsExternalProperty, value); }
}

/// <summary>The one entrance a hero plays, once: children rise in reading order on one clock.</summary>
/// <remarks>A clock in code, because the entrance is a sequence that spans parts built per design and includes a 3D tilt.</remarks>
internal sealed class HeroEntrance
{
    private const double RiseDuration = 640;
    private const double Stagger = 80;
    private const double FirstDelay = 40;

    private readonly Visual _owner;
    private readonly List<Action<double, bool>> _tracks = new();
    private MotionValue<double>? _clock;
    private double _total;
    private int _riseCount;

    public HeroEntrance(Visual owner)
    {
        _owner = owner;
    }

    /// <summary>Adds the next item of the reading order: it rises 12 px out of a 2 px blur in 640 ms.</summary>
    public void AddRise(Control control)
    {
        var delay = FirstDelay + _riseCount * Stagger;
        _riseCount++;
        var translate = new TranslateTransform();
        Add(delay + RiseDuration, (time, full) =>
        {
            var eased = ArcEasings.Enter.Ease(StatsBandTiming.Progress(time, delay, RiseDuration));
            control.Opacity = eased;
            if (full)
            {
                control.RenderTransform = eased >= 1 ? null : translate;
                translate.Y = 12 * (1 - eased);
                control.Effect = eased >= 1 ? null : ArcBlur.Get(2 * (1 - eased));
            }
        });
    }

    /// <summary>Adds a timed track that lasts until <paramref name="end"/> ms. It receives the time and whether travel is allowed.</summary>
    public void Add(double end, Action<double, bool> track)
    {
        _total = Math.Max(_total, end);
        _tracks.Add(track);
    }

    /// <summary>Plays the entrance, or shows the final state at once with <paramref name="animate"/> false or motion off.</summary>
    public async void Play(bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(_owner);
        if (!animate || level == ArcMotionLevel.Off)
        {
            Apply(double.PositiveInfinity, false);
            return;
        }
        var full = level == ArcMotionLevel.Full;
        Apply(0, full);
        _clock ??= new MotionValue<double>(_owner, 0, MotionVectors.Double);
        _clock.Changed += value => Apply(value, full);
        var finished = await _clock.AnimateTo(_total, MotionSpec.Tween(TimeSpan.FromMilliseconds(_total), new LinearEasing()));
        if (finished)
        {
            Apply(double.PositiveInfinity, full);
        }
    }

    private void Apply(double time, bool full)
    {
        foreach (var track in _tracks)
        {
            track(time, full);
        }
    }
}

/// <summary>Text helpers for the code-built scenes of the hero.</summary>
internal static class HeroText
{
    /// <summary>A headline: display type, balanced, at most <paramref name="maxChars"/> characters wide.</summary>
    public static CtaBalancedText Title(string text, double maxChars)
    {
        var block = new CtaBalancedText { Text = text, TextWrapping = TextWrapping.WrapWithOverflow, IsBalanced = true, MaxChars = maxChars, HorizontalAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(block, CtaBalancedText.LeadingProperty, ArcDesignTokenKind.LeadingDisplay);
        ArcTokens.Bind(block, CtaBalancedText.TrackingProperty, ArcDesignTokenKind.TrackingDisplay);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetHeadingLevel(block, 1);
        return block;
    }

    /// <summary>
    /// Sets a headline's size. A display line height is tighter than the font, so the block's bounds would cut the descenders
    /// of its last line: a padding keeps them inside and a negative margin of the same size keeps the layout as it was.
    /// </summary>
    public static void SetTitleSize(CtaBalancedText block, double size)
    {
        block.FontSize = size;
        var room = size * 0.2;
        block.Padding = new Thickness(0, 0, 0, room);
        block.Margin = new Thickness(block.Margin.Left, block.Margin.Top, block.Margin.Right, -room);
    }

    /// <summary>A supporting paragraph: large text, wrapping.</summary>
    public static TextBlock Paragraph(string? text, double maxWidth)
    {
        var block = Line(text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeLG);
        block.TextWrapping = TextWrapping.Wrap;
        block.MaxWidth = maxWidth;
        return block;
    }

    public static TextBlock Line(string? text, ArcDesignTokenKind color, ArcDesignTokenKind size, bool isMedium = false)
    {
        var block = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        if (isMedium)
        {
            ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        return block;
    }

    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color, double stroke = 1.75)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }
}

/// <summary>The content of an action: the label and an arrow that leans 2 px on hover, an outward arrow, or a check once it has confirmed.</summary>
[PseudoClasses(LeanPseudoClass)]
internal sealed class HeroActionContent : TemplatedControl
{
    public const string LeanPseudoClass = ":lean";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<HeroActionContent, string?>(nameof(Text));

    public static readonly StyledProperty<Geometry?> LeadingGlyphProperty =
        AvaloniaProperty.Register<HeroActionContent, Geometry?>(nameof(LeadingGlyph));

    public static readonly StyledProperty<Geometry?> TrailingGlyphProperty =
        AvaloniaProperty.Register<HeroActionContent, Geometry?>(nameof(TrailingGlyph));

    public static readonly StyledProperty<bool> IsLeaningProperty =
        AvaloniaProperty.Register<HeroActionContent, bool>(nameof(IsLeaning));

    static HeroActionContent()
    {
        FocusableProperty.OverrideDefaultValue<HeroActionContent>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<HeroActionContent>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public Geometry? LeadingGlyph { get => GetValue(LeadingGlyphProperty); set => SetValue(LeadingGlyphProperty, value); }

    public Geometry? TrailingGlyph { get => GetValue(TrailingGlyphProperty); set => SetValue(TrailingGlyphProperty, value); }

    public bool IsLeaning { get => GetValue(IsLeaningProperty); set => SetValue(IsLeaningProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLeaningProperty)
        {
            PseudoClasses.Set(LeanPseudoClass, IsLeaning);
        }
    }
}

/// <summary>
/// One action of the hero. A press raises <see cref="HeroSection.ActionInvoked"/> and runs the command; an address then opens
/// unless a handler marked the event handled, and an action with a confirmed label and no address shows it for 2.4 seconds.
/// </summary>
internal sealed class HeroActionButton : Button
{
    private static readonly TimeSpan s_confirmed = TimeSpan.FromMilliseconds(2400);

    private readonly HeroSection _section;
    private readonly bool _isPrimary;
    private HeroActionContent? _content;
    private DispatcherTimer? _timer;
    private bool _hover;

    public HeroActionButton(HeroSection section, HeroAction action, bool isPrimary)
    {
        _section = section;
        Action = action;
        _isPrimary = isPrimary;
        Variant = isPrimary ? ButtonVariant.Primary : ButtonVariant.Secondary;
        Size = ControlSize.Medium;
        Refresh();
        Click += OnPressed;
    }

    protected override Type StyleKeyOverride => typeof(Button);

    public HeroAction Action { get; }

    public bool IsConfirmed { get; private set; }

    public string CurrentText => (IsConfirmed ? Action.ConfirmedLabel : null) ?? Action.Label ?? string.Empty;

    private void OnPressed(object? sender, RoutedEventArgs e)
    {
        var args = new CtaActionEventArgs(HeroSection.ActionInvokedEvent, _section, Action, _isPrimary, false);
        _section.RaiseEvent(args);
        if (Action.Command?.CanExecute(Action.CommandParameter) == true)
        {
            Action.Command.Execute(Action.CommandParameter);
        }
        if (!string.IsNullOrWhiteSpace(Action.Href))
        {
            if (!args.Handled)
            {
                SiteLinks.TryLaunch(this, Action.Href);
            }
            return;
        }
        if (!args.Handled && !string.IsNullOrEmpty(Action.ConfirmedLabel))
        {
            IsConfirmed = true;
            Refresh();
            _timer?.Stop();
            _timer = new DispatcherTimer { Interval = s_confirmed };
            _timer.Tick += (_, _) => ResetConfirmed();
            _timer.Start();
        }
    }

    internal void ResetConfirmed()
    {
        _timer?.Stop();
        _timer = null;
        IsConfirmed = false;
        Refresh();
    }

    private void Refresh()
    {
        _content = new HeroActionContent
        {
            Text = CurrentText,
            LeadingGlyph = IsConfirmed ? ArcIcons.Check : null,
            TrailingGlyph = IsConfirmed ? null : Action.IsExternal ? ArcIcons.ArrowUpRight : _isPrimary ? ArcIcons.ArrowRight : null,
            IsLeaning = _hover && IsPointerOver
        };
        Content = _content;
        AutomationProperties.SetName(this, CurrentText);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        _hover = e.Pointer.Type != PointerType.Touch;
        base.OnPointerEntered(e);
        if (_content is not null)
        {
            _content.IsLeaning = _hover;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        if (_content is not null)
        {
            _content.IsLeaning = false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }
}

/// <summary>The pill above a title of your own content, for real news.</summary>
internal sealed class HeroAnnouncementButton : Avalonia.Controls.Button
{
    protected override Type StyleKeyOverride => typeof(HeroAnnouncementButton);
}

/// <summary>What the designs have in common: they follow the hero's size and keep their actions.</summary>
internal abstract class HeroScene : Panel
{
    protected HeroScene(HeroSection section)
    {
        Section = section;
    }

    protected HeroSection Section { get; }

    /// <summary>The action buttons of this design (tests read them).</summary>
    public List<HeroActionButton> ActionButtons { get; } = new();

    /// <summary>Applies the width and the viewport height the hero has now.</summary>
    public abstract void UpdateSize(double width, double viewportHeight);

    /// <summary>Plays the entrance, once.</summary>
    public abstract void PlayEntrance(bool animate);

    protected CtaFlowPanel CreateActions(HeroAction? primary, HeroAction? secondary, HorizontalAlignment alignment)
    {
        var panel = new CtaFlowPanel { Spacing = 12, ContentAlignment = alignment };
        foreach (var (action, isPrimary) in new[] { (primary, true), (secondary, false) })
        {
            if (action is not null)
            {
                var button = new HeroActionButton(Section, action, isPrimary);
                ActionButtons.Add(button);
                panel.Children.Add(button);
            }
        }
        return panel;
    }
}
