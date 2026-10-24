using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// One check of <see cref="ReleaseReadiness"/>: the main package's <see cref="Checkbox"/> holding the title and the
/// description, so the whole label toggles and pointing at it lights the box. The row tints while the pointer is over
/// it and its title dims once the check is done. Built in code for each check, so it has its own theme.
/// </summary>
[TemplatePart(CheckboxPart, typeof(Checkbox))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[PseudoClasses(FirstPseudoClass, CompactPseudoClass, DonePseudoClass)]
internal sealed class ReleaseReadinessRow : TemplatedControl
{
    public const string CheckboxPart = "PART_Checkbox";
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";
    public const string FirstPseudoClass = ":first";
    public const string CompactPseudoClass = ":compact";
    public const string DonePseudoClass = ":done";

    private Checkbox? _checkbox;
    private TextBlock? _title;
    private TextBlock? _description;
    private bool _syncing;
    private bool _isLocked;
    private bool _watching;

    public ReleaseReadinessRow(ReleaseCheck check)
    {
        Check = check;
        PseudoClasses.Set(DonePseudoClass, check.IsDone);
    }

    /// <summary>The check shown.</summary>
    public ReleaseCheck Check { get; }

    /// <summary>The row's checkbox (null before the template is applied).</summary>
    public Checkbox? Checkbox => _checkbox;

    /// <summary>True disables the checkbox (while releasing and after a release).</summary>
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            _isLocked = value;
            if (_checkbox is not null)
            {
                _checkbox.IsEnabled = !value;
            }
        }
    }

    /// <summary>The first row has no line above it.</summary>
    public bool IsFirst
    {
        set => PseudoClasses.Set(FirstPseudoClass, value);
    }

    /// <summary>Tighter side padding (the block is 388 wide or less).</summary>
    public bool IsCompact
    {
        set => PseudoClasses.Set(CompactPseudoClass, value);
    }

    /// <summary>Stops following the check (the row is thrown away).</summary>
    public void Detach() => WatchCheck(false);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchCheck(true);
        // The check may have changed while the row was out of the tree.
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchCheck(false);
    }

    /// <summary>Follows the check only while attached: the app's checks outlive the rows.</summary>
    private void WatchCheck(bool watch)
    {
        if (watch == _watching)
        {
            return;
        }
        _watching = watch;
        if (watch)
        {
            Check.PropertyChanged += OnCheckChanged;
        }
        else
        {
            Check.PropertyChanged -= OnCheckChanged;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_checkbox is not null)
        {
            _checkbox.IsCheckedChanged -= OnCheckboxChanged;
        }
        _checkbox = e.NameScope.Find<Checkbox>(CheckboxPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        if (_checkbox is not null)
        {
            _checkbox.IsEnabled = !_isLocked;
            _checkbox.IsCheckedChanged += OnCheckboxChanged;
        }
        Refresh();
    }

    private void OnCheckChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ReleaseCheck.IsDoneProperty || e.Property == ReleaseCheck.TitleProperty
            || e.Property == ReleaseCheck.DescriptionProperty || e.Property == ReleaseCheck.IsRequiredProperty)
        {
            Refresh();
        }
    }

    private void OnCheckboxChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_syncing || _checkbox is null)
        {
            return;
        }
        // The person ticked the box: write it back to the check (two-way bindings on IsDone see it).
        Check.SetCurrentValue(ReleaseCheck.IsDoneProperty, _checkbox.IsChecked == true);
    }

    private void Refresh()
    {
        PseudoClasses.Set(DonePseudoClass, Check.IsDone);
        if (_title is not null)
        {
            _title.Text = Check.Title;
        }
        if (_description is not null)
        {
            _description.Text = Check.Description;
            _description.IsVisible = !string.IsNullOrEmpty(Check.Description);
        }
        if (_checkbox is null)
        {
            return;
        }
        AutomationProperties.SetName(_checkbox, Check.Title);
        AutomationProperties.SetHelpText(_checkbox, string.IsNullOrEmpty(Check.Description) ? null : Check.Description);
        _checkbox.IsRequired = Check.IsRequired;
        if ((_checkbox.IsChecked == true) != Check.IsDone)
        {
            _syncing = true;
            _checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, Check.IsDone);
            _syncing = false;
        }
    }
}

/// <summary>
/// The footer's two groups (the notify switch and the buttons) on one line, the switch at the start and the buttons at
/// the end, both centered vertically. When they do not fit side by side the buttons move to a second line, still at the
/// end (Arc's wrapping flex row with an auto margin).
/// </summary>
internal sealed class ReleaseReadinessFooter : Panel
{
    /// <summary>Space between the two groups on one line (16).</summary>
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ReleaseReadinessFooter, double>(nameof(ColumnGap), 16);

    /// <summary>Space between the two lines once wrapped (12).</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ReleaseReadinessFooter, double>(nameof(RowGap), 12);

    static ReleaseReadinessFooter()
    {
        AffectsMeasure<ReleaseReadinessFooter>(ColumnGapProperty, RowGapProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>True while the buttons sit on a line of their own.</summary>
    public bool IsWrapped { get; private set; }

    private (Control? Lead, Control? Trail) Groups()
    {
        Control? lead = null;
        Control? trail = null;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            if (i == 0)
            {
                lead = child;
            }
            else
            {
                trail = child;
            }
        }
        return (lead, trail);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
        foreach (var child in Children)
        {
            child.Measure(unbounded);
        }
        var (lead, trail) = Groups();
        var a = lead?.DesiredSize ?? default;
        var b = trail?.DesiredSize ?? default;
        if (lead is null || trail is null)
        {
            IsWrapped = false;
            var only = lead is null ? b : a;
            return new Size(Math.Min(only.Width, availableSize.Width), only.Height);
        }
        var oneLine = a.Width + ColumnGap + b.Width;
        IsWrapped = oneLine > availableSize.Width + 0.01;
        if (!IsWrapped)
        {
            return new Size(oneLine, Math.Max(a.Height, b.Height));
        }
        return new Size(Math.Min(Math.Max(a.Width, b.Width), availableSize.Width), a.Height + RowGap + b.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (lead, trail) = Groups();
        var width = finalSize.Width;
        if (lead is not null && trail is not null && IsWrapped)
        {
            var a = lead.DesiredSize;
            var b = trail.DesiredSize;
            lead.Arrange(new Rect(0, 0, Math.Min(a.Width, width), a.Height));
            trail.Arrange(new Rect(Math.Max(0, width - b.Width), a.Height + RowGap, Math.Min(b.Width, width), b.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        if (lead is not null)
        {
            var a = lead.DesiredSize;
            lead.Arrange(new Rect(0, (height - a.Height) / 2, Math.Min(a.Width, width), a.Height));
        }
        if (trail is not null)
        {
            var b = trail.DesiredSize;
            trail.Arrange(new Rect(Math.Max(0, width - b.Width), (height - b.Height) / 2, Math.Min(b.Width, width), b.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The owners' avatars as one named group for screen readers ("Owners: Rina Kato and Samir Aziz"); the avatars inside
/// are not read one by one.
/// </summary>
internal sealed class ReleaseReadinessOwners : Decorator
{
    static ReleaseReadinessOwners()
    {
        FocusableProperty.OverrideDefaultValue<ReleaseReadinessOwners>(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ReleaseReadinessGroupPeer(this);
}

/// <summary>Reports a control as a named group.</summary>
internal sealed class ReleaseReadinessGroupPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>
/// The line under the footer: the success text (secondary color) or the error (danger color). Focusable from code only
/// (not a tab stop): after a release without a reset button the focus lands here instead of being dropped.
/// </summary>
[TemplatePart(TextPart, typeof(TextBlock))]
[PseudoClasses(ErrorPseudoClass)]
internal sealed class ReleaseReadinessMessage : TemplatedControl
{
    public const string TextPart = "PART_Text";
    public const string ErrorPseudoClass = ":error";

    /// <summary>The text shown.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReleaseReadinessMessage, string?>(nameof(Text));

    /// <summary>True shows the text as an error.</summary>
    public static readonly StyledProperty<bool> IsErrorProperty =
        AvaloniaProperty.Register<ReleaseReadinessMessage, bool>(nameof(IsError));

    static ReleaseReadinessMessage()
    {
        FocusableProperty.OverrideDefaultValue<ReleaseReadinessMessage>(true);
        IsTabStopProperty.OverrideDefaultValue<ReleaseReadinessMessage>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsError { get => GetValue(IsErrorProperty); set => SetValue(IsErrorProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text ?? string.Empty);
        }
        else if (change.Property == IsErrorProperty)
        {
            PseudoClasses.Set(ErrorPseudoClass, IsError);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ReleaseReadinessTextPeer(this);
}

/// <summary>Reports the status line as text.</summary>
internal sealed class ReleaseReadinessTextPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>Builds the release button's two labels: the text with an arrow after it, or a check mark before it.</summary>
internal static class ReleaseReadinessLabel
{
    public static (StackPanel Panel, TextBlock Text) Create(bool glyphFirst, Avalonia.Media.Geometry glyph, double glyphSize,
        double glyphStroke, double gap)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var icon = new ArcGlyph
        {
            Data = glyph,
            Size = glyphSize,
            StrokeThickness = glyphStroke,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
        if (glyphFirst)
        {
            panel.Children.Add(icon);
            panel.Children.Add(text);
        }
        else
        {
            panel.Children.Add(text);
            panel.Children.Add(icon);
        }
        return (panel, text);
    }
}
