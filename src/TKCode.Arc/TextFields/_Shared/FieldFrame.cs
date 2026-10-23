using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The Arc input shell shared by every field (text fields, special inputs, picker and select triggers):
/// a 1px <c>BorderStrong</c> border on <c>Surface</c> with <c>RadiusControl</c> corners, optional prefix and
/// suffix slots, and the Arc state language. Hover and focus answer with the border colour alone
/// (<c>Foreground</c>), an invalid field shows a <c>Danger</c> border, a disabled one fades to 0.5 on
/// <c>SurfaceMuted</c>, and a read-only one ignores hover. Colours tween on fast + standard ease; the frame
/// never changes size. Arc's 3px focus halo is <c>--focus-ring: transparent</c>, so the border colour is the whole
/// focus indicator.
/// </summary>
/// <remarks>
/// Focus is <see cref="IsFocusActive"/> or keyboard focus anywhere inside the frame. A control whose editor is
/// the templated parent (a <c>TextBox</c> that hosts the frame in its own template) binds
/// <c>IsFocusActive="{TemplateBinding IsFocused}"</c>; a composite control that places its editor inside the
/// frame gets the state for free.
/// </remarks>
[TemplatePart(PrefixPart, typeof(ContentPresenter))]
[TemplatePart(SuffixPart, typeof(ContentPresenter))]
[PseudoClasses(FocusActivePseudoClass, InvalidPseudoClass, ReadOnlyPseudoClass)]
public class FieldFrame : ContentControl
{
    public const string PrefixPart = "PART_Prefix";
    public const string SuffixPart = "PART_Suffix";

    /// <summary>Set while the field has focus (see <see cref="IsFocusActive"/>).</summary>
    public const string FocusActivePseudoClass = ":focus-active";

    /// <summary>Set while <see cref="IsInvalid"/> is true.</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>Set while <see cref="IsReadOnly"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>Content placed before the editor (icons, currency, country trigger).</summary>
    public static readonly StyledProperty<object?> PrefixProperty =
        AvaloniaProperty.Register<FieldFrame, object?>(nameof(Prefix));

    /// <summary>Content placed after the editor (clear, reveal, steppers, units).</summary>
    public static readonly StyledProperty<object?> SuffixProperty =
        AvaloniaProperty.Register<FieldFrame, object?>(nameof(Suffix));

    /// <summary>Frame height: Small 36, Medium 44 (default), Large 50, with matching inline padding.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<FieldFrame, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>Shows the danger border.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<FieldFrame, bool>(nameof(IsInvalid));

    /// <summary>Read-only fields keep their look but do not react to hover.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<FieldFrame, bool>(nameof(IsReadOnly));

    /// <summary>Marks the field as focused when its editor is outside the frame (the templated parent).</summary>
    public static readonly StyledProperty<bool> IsFocusActiveProperty =
        AvaloniaProperty.Register<FieldFrame, bool>(nameof(IsFocusActive));

    /// <summary>Gap between the prefix / suffix and the editor (Arc: <c>gap: var(--space-2)</c>, 8px).</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<FieldFrame, double>(nameof(Spacing), 8);

    /// <summary>Gap between the prefix / suffix and the editor.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private ContentPresenter? _prefix;
    private ContentPresenter? _suffix;

    /// <summary>Content placed before the editor.</summary>
    public object? Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    /// <summary>Content placed after the editor.</summary>
    public object? Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    /// <summary>Frame height and inline padding.</summary>
    public ControlSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Shows the danger border.</summary>
    public bool IsInvalid
    {
        get => GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    /// <summary>Read-only fields do not react to hover.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Marks the field as focused when its editor is the templated parent.</summary>
    public bool IsFocusActive
    {
        get => GetValue(IsFocusActiveProperty);
        set => SetValue(IsFocusActiveProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _prefix = e.NameScope.Find<ContentPresenter>(PrefixPart);
        _suffix = e.NameScope.Find<ContentPresenter>(SuffixPart);
        UpdateSpacing();
    }

    private void UpdateSpacing()
    {
        if (_prefix is not null)
        {
            _prefix.Margin = new Thickness(0, 0, Spacing, 0);
        }
        if (_suffix is not null)
        {
            _suffix.Margin = new Thickness(Spacing, 0, 0, 0);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SpacingProperty)
        {
            UpdateSpacing();
        }
        else if (change.Property == PrefixProperty || change.Property == SuffixProperty)
        {
            // Slot controls join the frame's logical tree: they inherit resources and data context, and controls
            // declared in a templated parent's template get that parent (TemplateBinding works in slots).
            if (change.OldValue is Control oldChild && LogicalChildren.Contains(oldChild))
            {
                LogicalChildren.Remove(oldChild);
            }
            if (change.NewValue is Control newChild && newChild.Parent is null)
            {
                LogicalChildren.Add(newChild);
            }
        }
        else if (change.Property == IsFocusActiveProperty || change.Property == IsKeyboardFocusWithinProperty)
        {
            PseudoClasses.Set(FocusActivePseudoClass, IsFocusActive || IsKeyboardFocusWithin);
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
        }
    }
}
