using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Reserves space while content is still loading (Arc Skeleton): an optional round avatar and 1–6 text lines that
/// pulse top to bottom in a calm, finite wave. With <see cref="ContentControl.Content"/> it crossfades into the
/// content when <see cref="IsLoading"/> turns false (the content rises 4px into place) while the height springs from
/// the placeholder to the content. Without content the placeholder always shows.
/// </summary>
[TemplatePart(FramePart, typeof(ProgressHeightFrame))]
[PseudoClasses(LoadingPseudoClass)]
public class Skeleton : ContentControl
{
    public const string FramePart = "PART_Frame";
    public const string LoadingPseudoClass = ":loading";

    /// <summary>Placeholder text lines (Arc clamps to 1–6). Defaults to 3.</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<Skeleton, int>(nameof(Lines), 3, coerce: (_, v) => Math.Clamp(v, 1, 6));

    /// <summary>Adds a round avatar placeholder before the lines.</summary>
    public static readonly StyledProperty<bool> IsAvatarVisibleProperty =
        AvaloniaProperty.Register<Skeleton, bool>(nameof(IsAvatarVisible));

    /// <summary>Keeps the placeholder visible while true; only used when <see cref="ContentControl.Content"/> is set.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<Skeleton, bool>(nameof(IsLoading), true);

    /// <summary>Accessible name of the placeholder (Arc <c>label</c>, "Loading content").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Skeleton, string>(nameof(Label), "Loading content");

    internal static readonly DirectProperty<Skeleton, bool> IsPlaceholderShownProperty =
        AvaloniaProperty.RegisterDirect<Skeleton, bool>(nameof(IsPlaceholderShown), o => o.IsPlaceholderShown);

    internal static readonly DirectProperty<Skeleton, bool> IsContentShownProperty =
        AvaloniaProperty.RegisterDirect<Skeleton, bool>(nameof(IsContentShown), o => o.IsContentShown);

    public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    public bool IsAvatarVisible { get => GetValue(IsAvatarVisibleProperty); set => SetValue(IsAvatarVisibleProperty, value); }
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private bool _isPlaceholderShown = true;

    internal bool IsPlaceholderShown
    {
        get => _isPlaceholderShown;
        private set => SetAndRaise(IsPlaceholderShownProperty, ref _isPlaceholderShown, value);
    }

    private bool _isContentShown;

    internal bool IsContentShown
    {
        get => _isContentShown;
        private set => SetAndRaise(IsContentShownProperty, ref _isContentShown, value);
    }

    private ProgressHeightFrame? _frame;

    static Skeleton()
    {
        FocusableProperty.OverrideDefaultValue<Skeleton>(false);
        AutomationProperties.LiveSettingProperty.OverrideDefaultValue<Skeleton>(AutomationLiveSetting.Polite);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Skeleton>(AutomationControlType.Group);
    }

    public Skeleton()
    {
        UpdateState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _frame = e.NameScope.Find<ProgressHeightFrame>(FramePart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLoadingProperty || change.Property == ContentProperty)
        {
            _frame?.Arm();
            UpdateState();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        var loading = IsLoading || Content is null;
        IsPlaceholderShown = loading;
        IsContentShown = !loading;
        PseudoClasses.Set(LoadingPseudoClass, loading);
        // Busy has no direct equivalent: the status reads the label while loading.
        AutomationProperties.SetName(this, loading ? Label : string.Empty);
        AutomationProperties.SetItemStatus(this, loading ? "Loading" : string.Empty);
    }
}
