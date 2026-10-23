// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): the form container and the copy column.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The form: the pill (input and button) over the message line. It follows its own width, like Arc's <c>signup</c>
/// container: below 300 (<see cref="StackedWidth"/>) the pill's frame goes away and the input and button stack, both full
/// width, and the note's inset shrinks to 4.
/// </summary>
[TemplatePart(PillPart, typeof(NewsletterPill))]
[TemplatePart(InputPart, typeof(NewsletterEmailBox))]
[TemplatePart(SubmitPart, typeof(NewsletterSubmitButton))]
[TemplatePart(MessagePart, typeof(NewsletterMessageLine))]
[PseudoClasses(StackedPseudoClass, NarrowPseudoClass)]
internal sealed class NewsletterForm : TemplatedControl
{
    public const string PillPart = "PART_Pill";
    public const string InputPart = "PART_Input";
    public const string SubmitPart = "PART_Submit";
    public const string MessagePart = "PART_Message";
    public const string StackedPseudoClass = ":stacked";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The form is stacked below this width (300).</summary>
    public const double StackedWidth = 300;

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<NewsletterForm, bool>(nameof(IsStacked));

    /// <summary>Set by the block while it is at most 520 wide: the note under the pill starts 4 in instead of 22.</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<NewsletterForm, bool>(nameof(IsNarrow));

    static NewsletterForm()
    {
        FocusableProperty.OverrideDefaultValue<NewsletterForm>(false);
    }

    /// <summary>True while the form is narrower than <see cref="StackedWidth"/>.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); private set => SetValue(IsStackedProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public NewsletterPill? Pill { get; private set; }

    public NewsletterEmailBox? Input { get; private set; }

    public NewsletterSubmitButton? Submit { get; private set; }

    public NewsletterMessageLine? Message { get; private set; }

    /// <summary>Raised once the template is applied and the parts can be used.</summary>
    public event EventHandler? PartsReady;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Pill = e.NameScope.Find<NewsletterPill>(PillPart);
        Input = e.NameScope.Find<NewsletterEmailBox>(InputPart);
        Submit = e.NameScope.Find<NewsletterSubmitButton>(SubmitPart);
        Message = e.NameScope.Find<NewsletterMessageLine>(MessagePart);
        ApplyStacked();
        PartsReady?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        IsStacked = e.NewSize.Width > 0 && e.NewSize.Width < StackedWidth;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStackedProperty)
        {
            PseudoClasses.Set(StackedPseudoClass, IsStacked);
            ApplyStacked();
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        }
    }

    private void ApplyStacked()
    {
        if (Pill is not null)
        {
            Pill.IsStacked = IsStacked;
        }
        if (Input is not null)
        {
            Input.IsStacked = IsStacked;
        }
    }
}

/// <summary>
/// A column at most <see cref="MaxColumnWidth"/> wide, aligned to the start of its slot (CSS <c>max-width</c> on a grid
/// item that is not centered): Avalonia centers a stretched element that has a maximum width, so the copy column needs
/// this to stay at the start.
/// </summary>
internal sealed class NewsletterColumn : Decorator
{
    public static readonly StyledProperty<double> MaxColumnWidthProperty =
        AvaloniaProperty.Register<NewsletterColumn, double>(nameof(MaxColumnWidth), double.PositiveInfinity);

    static NewsletterColumn()
    {
        AffectsMeasure<NewsletterColumn>(MaxColumnWidthProperty);
    }

    public double MaxColumnWidth { get => GetValue(MaxColumnWidthProperty); set => SetValue(MaxColumnWidthProperty, value); }

    private double ColumnWidth(double available) => Math.Min(double.IsInfinity(available) ? MaxColumnWidth : available, MaxColumnWidth);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null)
        {
            return default;
        }
        var width = ColumnWidth(availableSize.Width);
        Child.Measure(new Size(width, availableSize.Height));
        return new Size(width, Child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = ColumnWidth(finalSize.Width);
        Child?.Arrange(new Rect(0, 0, width, finalSize.Height));
        return new Size(width, finalSize.Height);
    }
}

/// <summary>
/// A heading that wraps the way CSS <c>text-wrap: balance</c> does: when the text needs several lines, it is measured at the
/// narrowest width that still fits in the same number of lines, so the lines come out about equally long. Keep it left
/// aligned (the control takes the balanced width, not the slot's).
/// </summary>
internal sealed class NewsletterBalancedText : TextBlock
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var full = base.MeasureOverride(availableSize);
        if (double.IsInfinity(availableSize.Width) || full.Height <= 0 || TextWrapping == TextWrapping.NoWrap)
        {
            return full;
        }
        var lineHeight = LineHeight > 0 ? LineHeight : FontSize * 1.2;
        if (full.Height < lineHeight * 1.5)
        {
            return full;
        }
        // The narrowest width that keeps the height (the number of lines) of the full-width layout.
        double low = 0;
        var high = Math.Min(availableSize.Width, full.Width);
        for (var i = 0; i < 9; i++)
        {
            var mid = (low + high) / 2;
            var probe = base.MeasureOverride(new Size(mid, availableSize.Height));
            if (probe.Height <= full.Height + 0.5)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }
        return base.MeasureOverride(new Size(Math.Ceiling(high) + 1, availableSize.Height));
    }
}
