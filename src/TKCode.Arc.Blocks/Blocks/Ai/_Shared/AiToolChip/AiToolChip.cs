using TKCode.Arc.Motion;
using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One tool call of an AI reply as a chip (Arc chat): 30px, pill, 1px Border on Surface, 10px left and 12px right. It
/// follows its <see cref="Tool"/>:
/// <list type="bullet">
/// <item>Running: a LoaderCircle turning every 900ms and the shimmering <see cref="AiChatTool.RunningLabel"/>; disabled
/// (it cannot open yet);</item>
/// <item>Done: the tool's icon (Search for a search, Database for a query, Wrench otherwise, unless
/// <see cref="AiChatTool.Icon"/> is set), <see cref="AiChatTool.DoneLabel"/>, for a search the marks of its sources' sites
/// (one per site, at most three) and the muted <see cref="AiChatTool.Summary"/>;</item>
/// <item>Error: a Danger CircleAlert, the running label (what it was doing) and the summary (why it stopped).</item>
/// </list>
/// Hover (mouse and pen) turns the border BorderStrong and the text Foreground; open (<see cref="IsExpanded"/>, two-way,
/// toggled by a press, Enter or Space) fills it SurfaceMuted with Foreground text. A change of state swaps the content in
/// Arc's label mode while the width follows on <see cref="ArcSpring.Quick"/>; a new summary or more sources update in place.
/// Automation: a button named by its label, the summary as help text, expanded or collapsed (a leaf while running).
/// </summary>
[TemplatePart(FacePart, typeof(ArcContentSwap))]
[PseudoClasses(RunningPseudoClass, DonePseudoClass, ErrorPseudoClass, ExpandedPseudoClass, HoverPseudoClass)]
internal class AiToolChip : AvaloniaButton
{
    public const string FacePart = "PART_Face";

    public const string RunningPseudoClass = ":running";
    public const string DonePseudoClass = ":done";
    public const string ErrorPseudoClass = ":error";
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>Mouse or pen over the chip (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>How many site marks a finished search shows at most.</summary>
    internal const int MaxMarks = 3;

    /// <summary>The tool call shown; the chip follows its changes while it is in a window.</summary>
    public static readonly StyledProperty<AiChatTool?> ToolProperty =
        AvaloniaProperty.Register<AiToolChip, AiChatTool?>(nameof(Tool));

    /// <summary>Whether the detail panel is open (two-way); a press toggles it once the tool finished.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiToolChip, bool>(nameof(IsExpanded), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <inheritdoc cref="ToolProperty"/>
    public AiChatTool? Tool { get => GetValue(ToolProperty); set => SetValue(ToolProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    private ArcContentSwap? _swap;
    private AiToolChipFace? _face;
    private AiChatTool? _watched;

    protected override Type StyleKeyOverride => typeof(AiToolChip);

    /// <summary>The face on screen now.</summary>
    internal AiToolChipFace? Face => _face;

    /// <summary>The state shown (Running without a tool).</summary>
    internal AiChatToolStatus Status => Tool?.Status ?? AiChatToolStatus.Running;

    protected override bool IsEnabledCore => base.IsEnabledCore && Tool is { Status: not AiChatToolStatus.Running };

    /// <summary>The name for a state: the done label once done; the running label while running and after an error.</summary>
    internal static string LabelFor(AiChatTool tool) => tool.Status == AiChatToolStatus.Done
        ? string.IsNullOrEmpty(tool.DoneLabel) ? tool.RunningLabel : tool.DoneLabel
        : string.IsNullOrEmpty(tool.RunningLabel) ? tool.DoneLabel : tool.RunningLabel;

    /// <summary>The sources whose marks a chip shows: for a finished search, one per site (by domain, else by mark), at most <see cref="MaxMarks"/>.</summary>
    internal static IReadOnlyList<AiSource> MarksFor(AiChatTool? tool)
    {
        if (tool is not { Kind: AiChatToolKind.Search, Status: AiChatToolStatus.Done } || tool.Sources.Count == 0)
        {
            return Array.Empty<AiSource>();
        }
        var marks = new List<AiSource>(MaxMarks);
        foreach (var source in tool.Sources)
        {
            var seen = false;
            foreach (var shown in marks)
            {
                if (SameSite(shown, source))
                {
                    seen = true;
                    break;
                }
            }
            if (!seen)
            {
                marks.Add(source);
                if (marks.Count == MaxMarks)
                {
                    break;
                }
            }
        }
        return marks;
    }

    private static bool SameSite(AiSource a, AiSource b)
    {
        if (!string.IsNullOrWhiteSpace(a.Domain) || !string.IsNullOrWhiteSpace(b.Domain))
        {
            return string.Equals(a.Domain?.Trim(), b.Domain?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        return a.Mark is not null && ReferenceEquals(a.Mark, b.Mark);
    }

    #region Template and state

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(FacePart);
        _face = null;
        Refresh(instant: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToolProperty)
        {
            Watch(this.IsAttachedToVisualTree() ? change.GetNewValue<AiChatTool?>() : null);
            // Another tool is another chip: no swap motion from the old one.
            _face = null;
            Refresh(instant: true);
        }
        else if (change.Property == IsExpandedProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, open);
            (ControlAutomationPeer.FromElement(this) as AiToolChipAutomationPeer)?.RaiseExpandCollapseChanged(open);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A chip disabled under the pointer (it started running again) gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Tool);
        // The tool may have moved on while the chip was away.
        Refresh(instant: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Out of a window the chip lets go of the tool, so a long-lived reply does not keep it alive.
        Watch(null);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    private void Watch(AiChatTool? tool)
    {
        if (ReferenceEquals(_watched, tool))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnToolChanged;
            _watched.Sources.CollectionChanged -= OnSourcesChanged;
        }
        _watched = tool;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnToolChanged;
            _watched.Sources.CollectionChanged += OnSourcesChanged;
        }
    }

    private void OnToolChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiChatTool.StatusProperty || e.Property == AiChatTool.RunningLabelProperty || e.Property == AiChatTool.DoneLabelProperty
            || e.Property == AiChatTool.SummaryProperty || e.Property == AiChatTool.IconProperty || e.Property == AiChatTool.KindProperty)
        {
            Refresh(instant: false);
        }
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh(instant: false);

    /// <summary>
    /// Brings the chip in line with its tool. A new state or a new name is a new face that swaps in (at once when
    /// <paramref name="instant"/>); the summary, the marks and the icon of the same state change in place.
    /// </summary>
    private void Refresh(bool instant)
    {
        var tool = Tool;
        var status = Status;
        PseudoClasses.Set(RunningPseudoClass, status == AiChatToolStatus.Running);
        PseudoClasses.Set(DonePseudoClass, status == AiChatToolStatus.Done);
        PseudoClasses.Set(ErrorPseudoClass, status == AiChatToolStatus.Error);
        UpdateIsEffectivelyEnabled();

        var label = tool is null ? string.Empty : LabelFor(tool);
        var summary = string.IsNullOrWhiteSpace(tool?.Summary) ? null : tool!.Summary;
        AutomationProperties.SetName(this, label);
        AutomationProperties.SetHelpText(this, summary);
        if (_swap is null)
        {
            return;
        }
        if (_face is { } current && current.Status == status && current.Label == label)
        {
            current.Summary = summary;
            current.SetMarks(MarksFor(tool));
            if (status == AiChatToolStatus.Done && !SameIcon(current.Icon, tool))
            {
                current.Icon = IconFor(tool);
            }
            return;
        }
        // A control the owner gave as the icon can only sit in one face: the leaving face lets go of it.
        if (_face is { Icon: { } oldIcon } old && tool?.Icon is Control userIcon && ReferenceEquals(oldIcon, userIcon))
        {
            old.Icon = null;
        }
        var face = new AiToolChipFace
        {
            Status = status,
            Label = label,
            Summary = summary,
            Icon = status == AiChatToolStatus.Done ? IconFor(tool) : null
        };
        face.SetMarks(MarksFor(tool));
        _face = face;
        if (instant)
        {
            // SetCurrentValue keeps the template's Mode the value a theme can still change.
            var mode = _swap.Mode;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
            _swap.Content = face;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
        }
        else
        {
            _swap.Content = face;
        }
    }

    private static bool SameIcon(Control? shown, AiChatTool? tool) => shown switch
    {
        null => false,
        ArcGlyph glyph when tool?.Icon is null => ReferenceEquals(glyph.Data, DefaultIcon(tool)),
        ArcGlyph glyph => ReferenceEquals(glyph.Data, tool.Icon),
        Image image => ReferenceEquals(image.Source, tool?.Icon),
        _ => ReferenceEquals(shown, tool?.Icon)
    };

    private static Geometry DefaultIcon(AiChatTool? tool) => tool?.Kind switch
    {
        AiChatToolKind.Search => ArcIcons.Search,
        AiChatToolKind.Query => ArcIcons.Database,
        _ => ArcIcons.Wrench
    };

    /// <summary>The icon of a finished tool: a Lucide glyph (stroke 1.75) for a geometry, the picture for an image, the owner's control as it is.</summary>
    private static Control? IconFor(AiChatTool? tool)
    {
        switch (tool?.Icon)
        {
            case Geometry geometry:
                return new ArcGlyph { Data = geometry, Size = 14, StrokeThickness = 1.75 };
            case IImage image:
                return new Image { Source = image, Width = 14, Height = 14 };
            case Control control:
                return control;
            default:
                return new ArcGlyph { Data = DefaultIcon(tool), Size = 14, StrokeThickness = 1.75 };
        }
    }

    #endregion

    protected override void OnClick()
    {
        if (Status != AiChatToolStatus.Running)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
        }
        base.OnClick();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiToolChipAutomationPeer(this);

    /// <summary>A button that opens the tool's detail (<c>aria-expanded</c>); the face inside is not read again.</summary>
    private sealed class AiToolChipAutomationPeer(AiToolChip owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private AiToolChip Chip => (AiToolChip)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Chip.Status == AiChatToolStatus.Running ? ExpandCollapseState.LeafNode
            : Chip.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            EnsureEnabled();
            if (!Chip.IsExpanded)
            {
                ((IInvokeProvider)this).Invoke();
            }
        }

        public void Collapse()
        {
            EnsureEnabled();
            if (Chip.IsExpanded)
            {
                ((IInvokeProvider)this).Invoke();
            }
        }

        public void RaiseExpandCollapseChanged(bool open) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
