using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One tool of an <see cref="AgentBuilder"/>: its glyph in a square, the name, one line on what it does and a switch.
/// Pressing anywhere on the row flips the switch; the switch is the row's only tab stop and carries the tool's name.
/// </summary>
[TemplatePart(SwitchPart, typeof(Switch))]
[PseudoClasses(OnPseudoClass)]
internal sealed class AgentBuilderToolRow : TemplatedControl
{
    public const string SwitchPart = "PART_Switch";
    public const string NamePart = "PART_Name";
    public const string DescriptionPart = "PART_Description";
    public const string GlyphPart = "PART_Glyph";
    public const string OnPseudoClass = ":on";

    private Switch? _switch;
    private TextBlock? _name;
    private TextBlock? _description;
    private ArcGlyph? _glyph;
    private bool _syncing;

    public AgentBuilderToolRow(AgentBuilderTool tool)
    {
        Tool = tool;
    }

    /// <summary>The tool shown.</summary>
    public AgentBuilderTool Tool { get; }

    /// <summary>The switch (null until the template is applied).</summary>
    public Switch? Toggle => _switch;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_switch is not null)
        {
            _switch.IsCheckedChanged -= OnSwitchChanged;
        }
        _switch = e.NameScope.Find<Switch>(SwitchPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _glyph = e.NameScope.Find<ArcGlyph>(GlyphPart);
        if (_switch is not null)
        {
            _switch.IsCheckedChanged += OnSwitchChanged;
        }
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Tool.PropertyChanged += OnToolChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Tool.PropertyChanged -= OnToolChanged;
    }

    protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // The whole row is the switch's label: a press on the text flips it (the switch handles its own presses).
        if (!e.Handled && e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left && IsEffectivelyEnabled)
        {
            e.Handled = true;
            Tool.IsOn = !Tool.IsOn;
            _switch?.Focus(Avalonia.Input.NavigationMethod.Pointer);
        }
    }

    private void OnToolChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    private void OnSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && _switch is { } toggle)
        {
            Tool.IsOn = toggle.IsChecked == true;
        }
    }

    private void Refresh()
    {
        if (_name is not null)
        {
            _name.Text = Tool.Name;
        }
        if (_description is not null)
        {
            _description.Text = Tool.Description;
            _description.IsVisible = !string.IsNullOrEmpty(Tool.Description);
        }
        if (_glyph is not null)
        {
            _glyph.Data = Tool.Glyph ?? ArcIcons.Wrench;
        }
        if (_switch is { } toggle)
        {
            _syncing = true;
            try
            {
                toggle.IsChecked = Tool.IsOn;
            }
            finally
            {
                _syncing = false;
            }
            AutomationProperties.SetName(toggle, Tool.Name);
            AutomationProperties.SetHelpText(toggle, Tool.Description ?? string.Empty);
        }
        PseudoClasses.Set(OnPseudoClass, Tool.IsOn);
    }
}

/// <summary>
/// One knowledge source of an <see cref="AgentBuilder"/>: a file or globe glyph, the name, a status line (indexing with a
/// ring, ready with the passage count, or the reason it failed with Retry) and a Remove button.
/// </summary>
[TemplatePart(RemovePart, typeof(AiChatIconButton))]
[TemplatePart(RetryPart, typeof(Button))]
[PseudoClasses(IndexingPseudoClass, ReadyPseudoClass, FailedPseudoClass)]
internal sealed class AgentBuilderSourceRow : TemplatedControl
{
    public const string NamePart = "PART_Name";
    public const string DetailPart = "PART_Detail";
    public const string GlyphPart = "PART_Glyph";
    public const string RingPart = "PART_Ring";
    public const string RemovePart = "PART_Remove";
    public const string RetryPart = "PART_Retry";
    public const string IndexingPseudoClass = ":indexing";
    public const string ReadyPseudoClass = ":ready";
    public const string FailedPseudoClass = ":failed";

    private TextBlock? _name;
    private TextBlock? _detail;
    private ArcGlyph? _glyph;
    private ArcRing? _ring;
    private AiChatIconButton? _remove;
    private Button? _retry;
    private AgentBuilderLabels _labels = AgentBuilderLabels.Default;
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");

    public AgentBuilderSourceRow(AgentBuilderSource source)
    {
        Source = source;
    }

    /// <summary>The source shown.</summary>
    public AgentBuilderSource Source { get; }

    /// <summary>Raised when Remove is pressed.</summary>
    public event EventHandler? RemoveRequested;

    /// <summary>Raised when Retry is pressed.</summary>
    public event EventHandler? RetryRequested;

    /// <summary>The Remove button (null until the template is applied).</summary>
    public AiChatIconButton? RemoveButton => _remove;

    /// <summary>The Retry button (null until the template is applied).</summary>
    public Button? RetryButton => _retry;

    /// <summary>The status line as shown.</summary>
    public string? DetailText => _detail?.Text;

    public void SetLabels(AgentBuilderLabels labels, CultureInfo culture)
    {
        _labels = labels;
        _culture = culture;
        Refresh();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _detail = e.NameScope.Find<TextBlock>(DetailPart);
        _glyph = e.NameScope.Find<ArcGlyph>(GlyphPart);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        _remove = e.NameScope.Find<AiChatIconButton>(RemovePart);
        _retry = e.NameScope.Find<Button>(RetryPart);
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Source.PropertyChanged += OnSourceChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Source.PropertyChanged -= OnSourceChanged;
    }

    private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The status line of <paramref name="source"/>: kind and size, then where indexing is.</summary>
    internal static string Detail(AgentBuilderSource source, AgentBuilderLabels labels, CultureInfo culture)
    {
        var kind = source.Kind == AgentBuilderSourceKind.Website ? labels.WebsiteKind : labels.FileKind;
        if (source.Size is { } size)
        {
            kind += " · " + FormatSize(size, culture);
        }
        var status = source.Status switch
        {
            AgentBuilderSourceStatus.Indexing => labels.Indexing((int)Math.Round(Math.Clamp(source.Progress, 0, 1) * 100)),
            AgentBuilderSourceStatus.Failed => string.IsNullOrWhiteSpace(source.ErrorMessage) ? labels.Failed : source.ErrorMessage!,
            _ => labels.Ready(source.ChunkCount, culture)
        };
        return kind + " · " + status;
    }

    internal static string FormatSize(long bytes, CultureInfo culture) => bytes switch
    {
        < 1_000 => bytes.ToString("N0", culture) + " B",
        < 1_000_000 => (bytes / 1_000.0).ToString("0", culture) + " KB",
        _ => (bytes / 1_000_000.0).ToString("0.0", culture) + " MB"
    };

    private void Refresh()
    {
        var source = Source;
        if (_name is not null)
        {
            _name.Text = source.Name;
        }
        if (_detail is not null)
        {
            _detail.Text = Detail(source, _labels, _culture);
        }
        if (_glyph is not null)
        {
            _glyph.Data = source.Kind == AgentBuilderSourceKind.Website ? ArcIcons.Globe : ArcIcons.FileText;
        }
        if (_ring is not null)
        {
            _ring.Value = Math.Clamp(source.Progress, 0, 1);
        }
        if (_remove is not null)
        {
            AutomationProperties.SetName(_remove, _labels.RemoveSource(source.Name));
            Tooltip.SetTip(_remove, _labels.RemoveSource(source.Name));
        }
        if (_retry is not null)
        {
            _retry.Content = _labels.Retry;
            AutomationProperties.SetName(_retry, _labels.RetrySource(source.Name));
        }
        AutomationProperties.SetName(this, source.Name + ", " + Detail(source, _labels, _culture));
        PseudoClasses.Set(IndexingPseudoClass, source.Status == AgentBuilderSourceStatus.Indexing);
        PseudoClasses.Set(ReadyPseudoClass, source.Status == AgentBuilderSourceStatus.Ready);
        PseudoClasses.Set(FailedPseudoClass, source.Status == AgentBuilderSourceStatus.Failed);
    }
}
