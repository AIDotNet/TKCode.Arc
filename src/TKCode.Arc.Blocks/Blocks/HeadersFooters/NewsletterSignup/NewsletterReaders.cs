// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): Readers.
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The readers row: up to three overlapping faces and "12,480 readers" whose number is an <see cref="AnimatedCounter"/>.
/// Once the visitor has subscribed the number ticks up by one and ", including you" opens its own width on the Smooth
/// spring (so a centred row glides instead of jumping sideways) while it fades in 240 ms after a 120 ms delay.
/// </summary>
[TemplatePart(FacesPart, typeof(SiteFaceStack))]
[TemplatePart(CounterPart, typeof(AnimatedCounter))]
[TemplatePart(YouHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(YouPart, typeof(TextBlock))]
[PseudoClasses(TinyPseudoClass, MicroPseudoClass, NoFacesPseudoClass)]
internal sealed class NewsletterReaders : TemplatedControl
{
    public const string FacesPart = "PART_Faces";
    public const string CounterPart = "PART_Counter";
    public const string YouHostPart = "PART_YouHost";
    public const string YouPart = "PART_You";
    public const string TinyPseudoClass = ":tiny";
    public const string MicroPseudoClass = ":micro";
    public const string NoFacesPseudoClass = ":no-faces";

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<NewsletterReaders, int>(nameof(Count), 12480);

    public static readonly StyledProperty<IList<IImage>?> FacesProperty =
        AvaloniaProperty.Register<NewsletterReaders, IList<IImage>?>(nameof(Faces));

    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<NewsletterReaders, bool>(nameof(IsDone));

    public static readonly StyledProperty<string> ReadersWordProperty =
        AvaloniaProperty.Register<NewsletterReaders, string>(nameof(ReadersWord), "readers");

    public static readonly StyledProperty<string> IncludingYouProperty =
        AvaloniaProperty.Register<NewsletterReaders, string>(nameof(IncludingYou), ", including you");

    /// <summary>The surface the faces sit on (the ring around each face): the page in Inline, the card in Card.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<NewsletterReaders, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<bool> IsTinyProperty =
        AvaloniaProperty.Register<NewsletterReaders, bool>(nameof(IsTiny));

    public static readonly StyledProperty<bool> IsMicroProperty =
        AvaloniaProperty.Register<NewsletterReaders, bool>(nameof(IsMicro));

    internal static readonly DirectProperty<NewsletterReaders, double> DisplayCountProperty =
        AvaloniaProperty.RegisterDirect<NewsletterReaders, double>(nameof(DisplayCount), o => o.DisplayCount);

    internal static readonly DirectProperty<NewsletterReaders, string> WordTextProperty =
        AvaloniaProperty.RegisterDirect<NewsletterReaders, string>(nameof(WordText), o => o.WordText);

    private SiteFaceStack? _faces;
    private AnimatedCounter? _counter;
    private SpringSizeDecorator? _youHost;
    private TextBlock? _you;
    private MotionValue<double>? _youFade;
    private double _displayCount = 12480;
    private string _wordText = " readers";

    static NewsletterReaders()
    {
        FocusableProperty.OverrideDefaultValue<NewsletterReaders>(false);
    }

    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    public IList<IImage>? Faces { get => GetValue(FacesProperty); set => SetValue(FacesProperty, value); }

    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    public string ReadersWord { get => GetValue(ReadersWordProperty); set => SetValue(ReadersWordProperty, value); }

    public string IncludingYou { get => GetValue(IncludingYouProperty); set => SetValue(IncludingYouProperty, value); }

    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    public bool IsTiny { get => GetValue(IsTinyProperty); set => SetValue(IsTinyProperty, value); }

    public bool IsMicro { get => GetValue(IsMicroProperty); set => SetValue(IsMicroProperty, value); }

    /// <summary>The number the counter is heading to: the count, plus one once subscribed.</summary>
    public double DisplayCount => _displayCount;

    /// <summary>The word after the number, with its leading space (" readers").</summary>
    public string WordText => _wordText;

    /// <summary>The counter (tests read it).</summary>
    public AnimatedCounter? Counter => _counter;

    /// <summary>The ", including you" decorator whose width springs (tests read it).</summary>
    public SpringSizeDecorator? YouHost => _youHost;

    /// <summary>The ", including you" text (tests read it).</summary>
    public TextBlock? You => _you;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _faces = e.NameScope.Find<SiteFaceStack>(FacesPart);
        _counter = e.NameScope.Find<AnimatedCounter>(CounterPart);
        _youHost = e.NameScope.Find<SpringSizeDecorator>(YouHostPart);
        _you = e.NameScope.Find<TextBlock>(YouPart);
        _youFade?.Dispose();
        _youFade = null;
        if (_you is not null)
        {
            // Code-driven: the delay (120 ms) and the 240 ms enter curve apply to both directions and a ControlTheme
            // transition cannot carry a delay.
            _youFade = new MotionValue<double>(this, IsDone ? 1 : 0, MotionVectors.Double);
            _youFade.Changed += v => _you.Opacity = v;
            _you.Opacity = IsDone ? 1 : 0;
        }
        if (_youHost is not null)
        {
            _youHost.IsExpanded = IsDone;
        }
        SyncFaces();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _youFade?.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == CountProperty || property == IsDoneProperty)
        {
            SetAndRaise(DisplayCountProperty, ref _displayCount, Count + (IsDone ? 1 : 0));
        }
        if (property == ReadersWordProperty)
        {
            SetAndRaise(WordTextProperty, ref _wordText, " " + ReadersWord);
        }
        if (property == IsDoneProperty)
        {
            ApplyDone();
        }
        else if (property == FacesProperty)
        {
            if (_observedFaces is not null)
            {
                _observedFaces.CollectionChanged -= OnFacesChanged;
            }
            _observedFaces = Faces as INotifyCollectionChanged;
            if (_observedFaces is not null)
            {
                _observedFaces.CollectionChanged += OnFacesChanged;
            }
            SyncFaces();
        }
        else if (property == IsTinyProperty)
        {
            PseudoClasses.Set(TinyPseudoClass, IsTiny);
        }
        else if (property == IsMicroProperty)
        {
            PseudoClasses.Set(MicroPseudoClass, IsMicro);
        }
    }

    private INotifyCollectionChanged? _observedFaces;

    private void OnFacesChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncFaces();

    private void SyncFaces()
    {
        if (_faces is not null)
        {
            _faces.Images = Faces;
        }
        PseudoClasses.Set(NoFacesPseudoClass, (Faces?.Count ?? 0) == 0);
    }

    private void ApplyDone()
    {
        if (_youHost is not null)
        {
            _youHost.IsExpanded = IsDone;
        }
        if (_youFade is null)
        {
            return;
        }
        var target = IsDone ? 1.0 : 0.0;
        var spec = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(120));
        _ = _youFade.AnimateTo(target, spec);
    }
}
