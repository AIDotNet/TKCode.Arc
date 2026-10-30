using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;
using AvaloniaRadioButton = Avalonia.Controls.RadioButton;

namespace TKCode.Arc;

/// <summary>
/// The tone or length picker of a <see cref="ReplyDraft"/>: a radio group of small pills on a muted track, where the
/// selected pill glides between options on the bounce-free settle spring (0.26s). Arrow keys move the selection (and so
/// rewrite the draft), Home and End jump to the ends, and only the selected option is a tab stop (roving tabindex).
/// <see cref="Picked"/> reports the reader's picks only; setting <see cref="SelectedIndex"/> from code is silent.
/// <para>
/// Arc's <c>SegmentedControl</c> glides on the morph spring, which overshoots; this group needs the settle spring and
/// 28px options, so the pill is a Core <see cref="GlidingIndicator"/> moved with <see cref="AiGlyphMotion.Settle"/>.
/// </para>
/// </summary>
[TemplatePart(IndicatorPart, typeof(GlidingIndicator))]
[TemplatePart(ItemsPart, typeof(StackPanel))]
internal sealed class ReplyDraftOptionGroup : TemplatedControl
{
    internal const string IndicatorPart = "PART_Indicator";
    internal const string ItemsPart = "PART_Items";

    /// <summary>The selected option's index.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<ReplyDraftOptionGroup, int>(nameof(SelectedIndex));

    private readonly List<ReplyDraftOption> _options = [];
    private IReadOnlyList<string> _labels = [];
    private GlidingIndicator? _indicator;
    private StackPanel? _items;
    private bool _animateNext;
    private bool _coarse;

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>Raised when the reader picks an option (click, Space, Enter or an arrow key) with its index.</summary>
    public event EventHandler<int>? Picked;

    /// <summary>The options, in order.</summary>
    internal IReadOnlyList<ReplyDraftOption> Options => _options;

    /// <summary>The indicator, for tests.</summary>
    internal GlidingIndicator? Indicator => _indicator;

    /// <summary>The option names, in order; replacing them keeps the selection.</summary>
    public IReadOnlyList<string> Labels
    {
        get => _labels;
        set
        {
            _labels = value;
            Build();
        }
    }

    /// <summary>Grows the options to 32px for touch.</summary>
    internal void SetCoarse(bool coarse)
    {
        _coarse = coarse;
        foreach (var option in _options)
        {
            option.SetCoarse(coarse);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _indicator = e.NameScope.Find<GlidingIndicator>(IndicatorPart);
        _items = e.NameScope.Find<StackPanel>(ItemsPart);
        Build();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            _animateNext = true;
            Sync();
            InvalidateArrange();
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceIndicator();
        return size;
    }

    private void Build()
    {
        if (_items is null)
        {
            return;
        }
        foreach (var option in _options)
        {
            option.Click -= OnOptionClick;
        }
        _options.Clear();
        _items.Children.Clear();
        for (var i = 0; i < _labels.Count; i++)
        {
            var option = new ReplyDraftOption { Content = _labels[i], Group = this };
            AutomationProperties.SetName(option, _labels[i]);
            option.SetCoarse(_coarse);
            option.Click += OnOptionClick;
            _options.Add(option);
            _items.Children.Add(option);
        }
        _animateNext = false;
        Sync();
    }

    /// <summary>Checks the selected option and makes it the only tab stop.</summary>
    private void Sync()
    {
        for (var i = 0; i < _options.Count; i++)
        {
            var selected = i == SelectedIndex;
            _options[i].IsChecked = selected;
            _options[i].IsTabStop = selected;
        }
    }

    private void PlaceIndicator()
    {
        if (_indicator is null || SelectedIndex < 0 || SelectedIndex >= _options.Count)
        {
            _indicator?.Hide(animate: false);
            return;
        }
        var bounds = _options[SelectedIndex].Bounds;
        if (bounds.Width <= 0)
        {
            return;
        }
        var animate = _animateNext;
        _animateNext = false;
        _indicator.MoveTo(bounds, AiGlyphMotion.Settle, animate);
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ReplyDraftOption option)
        {
            Pick(_options.IndexOf(option), focus: false);
        }
    }

    /// <summary>Selects <paramref name="index"/> as the reader's pick: it moves focus with the selection when asked.</summary>
    internal void Pick(int index, bool focus)
    {
        if (index < 0 || index >= _options.Count)
        {
            return;
        }
        if (focus)
        {
            _options[index].Focus(NavigationMethod.Directional);
        }
        if (index == SelectedIndex)
        {
            Sync();
            return;
        }
        SelectedIndex = index;
        Picked?.Invoke(this, index);
    }

    /// <summary>Arrows, Home and End from an option; returns true when the key was used.</summary>
    internal bool HandleKey(ReplyDraftOption option, Key key)
    {
        var count = _options.Count;
        var index = _options.IndexOf(option);
        if (count == 0 || index < 0)
        {
            return false;
        }
        var target = key switch
        {
            Key.Left or Key.Up => (index - 1 + count) % count,
            Key.Right or Key.Down => (index + 1) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => -1,
        };
        if (target < 0)
        {
            return false;
        }
        Pick(target, focus: true);
        return true;
    }
}

/// <summary>One pill of a <see cref="ReplyDraftOptionGroup"/>: a radio button the group checks and keys.</summary>
[PseudoClasses(CoarsePseudoClass)]
internal sealed class ReplyDraftOption : AvaloniaRadioButton
{
    /// <summary>Set on touch: the option grows to 32px.</summary>
    internal const string CoarsePseudoClass = ":coarse";

    /// <summary>The group that owns the option.</summary>
    internal ReplyDraftOptionGroup? Group { get; init; }

    internal void SetCoarse(bool coarse) => PseudoClasses.Set(CoarsePseudoClass, coarse);

    protected override Type StyleKeyOverride => typeof(ReplyDraftOption);

    protected override void Toggle()
    {
        // The group owns the checked state.
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Group is { } group && e.KeyModifiers == KeyModifiers.None && group.HandleKey(this, e.Key))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
