using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A step heading the form can focus from code (Arc's <c>legend</c> / <c>h2</c> with <c>tabindex=-1</c>): never a tab stop and
/// without a visible ring, so the focus can land on the new step after Continue or Back without drawing anything.
/// </summary>
internal sealed class MultiStepLegend : TextBlock
{
    static MultiStepLegend()
    {
        FocusableProperty.OverrideDefaultValue<MultiStepLegend>(true);
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<MultiStepLegend>(false);
        FocusAdornerProperty.OverrideDefaultValue<MultiStepLegend>(null);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);
}

/// <summary>Reports a control as a named group (a step's fieldset, the success block).</summary>
internal sealed class MultiStepGroupPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>
/// One step on screen (Arc's fieldset): the heading, the optional description and the step's fields. The form creates one
/// per step and keeps it, hidden while another step is shown, so the fields keep what was typed.
/// </summary>
[TemplatePart(LegendPart, typeof(MultiStepLegend))]
[PseudoClasses(DescribedPseudoClass)]
internal sealed class MultiStepStepView : ContentControl
{
    public const string LegendPart = "PART_Legend";

    /// <summary>Set while the step has a description (the fields then follow its 20px bottom margin).</summary>
    public const string DescribedPseudoClass = ":described";

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MultiStepStepView, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<MultiStepStepView, string?>(nameof(Description));

    private MultiStepLegend? _legend;

    public MultiStepStepView(MultiStepFormStep step)
    {
        Step = step;
        LayerMotion = new MultiStepLayerMotion(this);
        Sync();
    }

    /// <summary>The step this view shows.</summary>
    public MultiStepFormStep Step { get; }

    /// <summary>Slide and fade of this layer.</summary>
    public MultiStepLayerMotion LayerMotion { get; }

    /// <summary>The heading.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The optional line under the heading.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The heading part (focused after a step change).</summary>
    public MultiStepLegend? Legend => _legend;

    /// <summary>Copies the step's title, description and content.</summary>
    public void Sync()
    {
        Title = Step.Title;
        Description = Step.Description;
        AutomationProperties.SetName(this, Step.Title);
        // Content first, template second, and no template while the content is null (a presenter would build it with null).
        if (Step.Content is null)
        {
            ContentTemplate = null;
            Content = null;
        }
        else
        {
            Content = Step.Content;
            ContentTemplate = Step.ContentTemplate;
        }
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _legend = e.NameScope.Find<MultiStepLegend>(LegendPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DescriptionProperty)
        {
            PseudoClasses.Set(DescribedPseudoClass, !string.IsNullOrEmpty(Description));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MultiStepGroupPeer(this);
}

/// <summary>
/// The block shown after the last step (Arc's <c>role=status</c> section): a check that draws itself, a title the form
/// focuses, and one line of text, centered in the step area. It rises 8 px while fading in over 240 ms; the check starts
/// drawing at 80 ms (400 ms, enter curve), the title rises 0.3em out of a 4 px blur from 100 ms and the line rises 0.3em
/// from 140 ms (240 ms each, enter curve). Reduced motion and Off show it at once.
/// </summary>
[TemplatePart(GlyphPart, typeof(MultiStepCheck))]
[TemplatePart(TitlePart, typeof(MultiStepLegend))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
internal sealed class MultiStepSuccessView : Avalonia.Controls.Primitives.TemplatedControl
{
    public const string GlyphPart = "PART_Glyph";
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MultiStepSuccessView, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<MultiStepSuccessView, string?>(nameof(Description));

    /// <summary>The check starts drawing at 80 ms and takes 400 ms (fitted 395).</summary>
    internal static readonly TimeSpan GlyphDelay = TimeSpan.FromMilliseconds(80);
    internal static readonly TimeSpan GlyphDuration = TimeSpan.FromMilliseconds(400);

    /// <summary>The title enters at 100 ms, the line at 140 ms.</summary>
    internal static readonly TimeSpan TitleDelay = TimeSpan.FromMilliseconds(100);
    internal static readonly TimeSpan DescriptionDelay = TimeSpan.FromMilliseconds(140);

    /// <summary>The block rises from 8 px below its place.</summary>
    internal const double RiseOffset = 8;

    private MultiStepCheck? _glyph;
    private MultiStepLegend? _title;
    private TextBlock? _description;
    private Reveal? _titleReveal;
    private Reveal? _descriptionReveal;
    private MotionValue<double>? _draw;

    public MultiStepSuccessView()
    {
        LayerMotion = new MultiStepLayerMotion(this);
    }

    /// <summary>Fade and rise of the whole block.</summary>
    public MultiStepLayerMotion LayerMotion { get; }

    /// <summary>The title.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The line under the title.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The title part (focused when the form completes).</summary>
    public MultiStepLegend? TitleBlock => _title;

    /// <summary>The check part (tests read it).</summary>
    internal MultiStepCheck? Glyph => _glyph;

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<MultiStepCheck>(GlyphPart);
        _title = e.NameScope.Find<MultiStepLegend>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _titleReveal = _title is null ? null : new Reveal(_title, MultiStepSwap.EnterBlur);
        _descriptionReveal = _description is null ? null : new Reveal(_description, 0);
        if (_glyph is not null)
        {
            _draw = new MotionValue<double>(_glyph, 1, MotionVectors.Double);
            var glyph = _glyph;
            _draw.Changed += p => glyph.Progress = p;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }

    /// <summary>Plays the entrance of the check, title and line (the block's own fade and rise run on <see cref="LayerMotion"/>).</summary>
    public void PlayEntrance(bool animate)
    {
        ApplyTemplate();
        if (!animate)
        {
            _draw?.Jump(1);
            _titleReveal?.Show();
            _descriptionReveal?.Show();
            return;
        }
        _draw?.Jump(0);
        _ = _draw?.AnimateTo(1, MotionSpec.Tween(GlyphDuration, ArcEasings.Enter, GlyphDelay));
        _titleReveal?.Play(TitleDelay);
        _descriptionReveal?.Play(DescriptionDelay);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MultiStepGroupPeer(this);

    /// <summary>Opacity, 0.3em rise and optional blur of one text line, on one 240 ms enter-curve tween after a delay.</summary>
    private sealed class Reveal
    {
        private readonly TextBlock _block;
        private readonly Avalonia.Media.TranslateTransform _translate = new();
        private readonly MotionValue<double> _progress;
        private readonly double _blur;
        private double _offset;

        public Reveal(TextBlock block, double blur)
        {
            _block = block;
            _blur = blur;
            block.RenderTransform = _translate;
            _progress = new MotionValue<double>(block, 1, MotionVectors.Double);
            _progress.Changed += p =>
            {
                _block.Opacity = Math.Clamp(p, 0, 1);
                _translate.Y = _offset * (1 - p);
                _block.Effect = ArcBlur.Get(_blur * (1 - p));
            };
        }

        public void Show() => _progress.Jump(1);

        public void Play(TimeSpan delay)
        {
            _offset = MultiStepSwap.TravelEm * _block.FontSize;
            _progress.Jump(0);
            _ = _progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
        }
    }
}
