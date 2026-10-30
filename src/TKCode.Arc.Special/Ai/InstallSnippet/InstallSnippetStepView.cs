using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One numbered step of an <see cref="InstallSnippet"/>: a marker on the rail (the number, a check once the step is done),
/// the title with an optional control at its right (package manager tabs, Generate key), a line that crossfades, and a
/// code block whose file name and snippet crossfade in a space that never changes size: the line reserves the height of
/// the step's longest description and the snippet the height of its longest snippet. A footer sits under the code.
/// </summary>
[TemplatePart(NumberPart, typeof(TextBlock))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(ArcContentSwap))]
[TemplatePart(DescriptionReservePart, typeof(Panel))]
[TemplatePart(FilePart, typeof(ArcContentSwap))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(CodeScrollPart, typeof(ScrollViewer))]
[TemplatePart(CodePart, typeof(ArcContentSwap))]
[PseudoClasses(DonePseudoClass, LastPseudoClass)]
internal sealed class InstallSnippetStepView : TemplatedControl
{
    internal const string NumberPart = "PART_Number";
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string DescriptionReservePart = "PART_DescriptionReserve";
    internal const string FilePart = "PART_File";
    internal const string CopyPart = "PART_Copy";
    internal const string CodeScrollPart = "PART_CodeScroll";
    internal const string CodePart = "PART_Code";
    internal const string DonePseudoClass = ":done";
    internal const string LastPseudoClass = ":last";

    /// <summary>The step's number (1 to 3).</summary>
    public static readonly StyledProperty<int> NumberProperty =
        AvaloniaProperty.Register<InstallSnippetStepView, int>(nameof(Number), 1);

    /// <summary>The step's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<InstallSnippetStepView, string?>(nameof(Title));

    /// <summary>Whether the step is done: its number turns into a check.</summary>
    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<InstallSnippetStepView, bool>(nameof(IsDone));

    /// <summary>The last step: no rail below its marker and no gap under it.</summary>
    public static readonly StyledProperty<bool> IsLastProperty =
        AvaloniaProperty.Register<InstallSnippetStepView, bool>(nameof(IsLast));

    /// <summary>A control at the right of the title.</summary>
    public static readonly StyledProperty<object?> AccessoryProperty =
        AvaloniaProperty.Register<InstallSnippetStepView, object?>(nameof(Accessory));

    /// <summary>Content under the code block.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<InstallSnippetStepView, object?>(nameof(Footer));

    private TextBlock? _number;
    private TextBlock? _title;
    private ArcContentSwap? _description;
    private Panel? _reserve;
    private ArcContentSwap? _file;
    private CopyButton? _copy;
    private ArcContentSwap? _code;
    private string? _descriptionText;
    private IReadOnlyList<string> _reserveTexts = [];
    private string? _fileName;
    private bool _fileIsCommand;
    private InstallSnippetCodeView? _codeView;
    private string? _copyValue;
    private string? _copyName;
    private string? _spokenTitle;

    /// <inheritdoc cref="NumberProperty"/>
    public int Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="IsDoneProperty"/>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <inheritdoc cref="IsLastProperty"/>
    public bool IsLast { get => GetValue(IsLastProperty); set => SetValue(IsLastProperty, value); }

    /// <inheritdoc cref="AccessoryProperty"/>
    public object? Accessory { get => GetValue(AccessoryProperty); set => SetValue(AccessoryProperty, value); }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>Raised after the copy button put the snippet on the clipboard, with the copied text.</summary>
    public event EventHandler<string>? Copied;

    /// <summary>The snippet on screen.</summary>
    internal InstallSnippetCodeView? CodeView => _codeView;

    /// <summary>The line on screen.</summary>
    internal string? DescriptionText => _descriptionText;

    /// <summary>The file name on screen.</summary>
    internal string? FileName => _fileName;

    /// <summary>The copy button.</summary>
    internal CopyButton? CopyButton => _copy;

    /// <summary>The title's text block.</summary>
    internal TextBlock? TitleBlock => _title;

    /// <summary>The text the copy button copies.</summary>
    internal string? CopyValue => _copyValue;

    /// <summary>Shows a line; <paramref name="reserve"/> are every line the step can show, which together set its height.</summary>
    public void SetDescription(string text, IReadOnlyList<string> reserve)
    {
        _descriptionText = text;
        if (!_reserveTexts.SequenceEqual(reserve))
        {
            _reserveTexts = [.. reserve];
            BuildReserve();
        }
        if (_description is not null && !Equals(_description.Content, text))
        {
            _description.Content = text;
        }
    }

    /// <summary>Shows a file name in the code block's header (a terminal glyph for a command, a file glyph otherwise).</summary>
    public void SetFile(string fileName, bool isCommand)
    {
        if (fileName == _fileName && isCommand == _fileIsCommand && _file?.Content is not null)
        {
            return;
        }
        _fileName = fileName;
        _fileIsCommand = isCommand;
        if (_file is not null)
        {
            _file.Content = FileLabel(fileName, isCommand);
        }
    }

    /// <summary>Shows a snippet; the old one crossfades out in the same cell.</summary>
    public void SetCode(InstallSnippetCodeView view)
    {
        _codeView = view;
        if (_code is not null)
        {
            _code.Content = view;
        }
    }

    /// <summary>The text the copy button copies and its name.</summary>
    public void SetCopy(string value, string name)
    {
        _copyValue = value;
        _copyName = name;
        if (_copy is not null)
        {
            _copy.Value = value;
            AutomationProperties.SetName(_copy, name);
        }
    }

    /// <summary>The title as screen readers read it ("Install, done").</summary>
    public void SetSpokenTitle(string spoken)
    {
        _spokenTitle = spoken;
        if (_title is not null)
        {
            AutomationProperties.SetName(_title, spoken);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        _number = e.NameScope.Find<TextBlock>(NumberPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<ArcContentSwap>(DescriptionPart);
        _reserve = e.NameScope.Find<Panel>(DescriptionReservePart);
        _file = e.NameScope.Find<ArcContentSwap>(FilePart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _code = e.NameScope.Find<ArcContentSwap>(CodePart);
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
            _copy.Value = _copyValue;
            if (_copyName is not null)
            {
                AutomationProperties.SetName(_copy, _copyName);
            }
        }
        if (_number is not null)
        {
            _number.Text = Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (_title is not null && _spokenTitle is not null)
        {
            AutomationProperties.SetName(_title, _spokenTitle);
        }
        BuildReserve();
        if (_description is not null)
        {
            _description.Content = _descriptionText;
        }
        if (_file is not null && _fileName is not null)
        {
            _file.Content = FileLabel(_fileName, _fileIsCommand);
        }
        if (_code is not null)
        {
            _code.Content = _codeView;
        }
        PseudoClasses.Set(DonePseudoClass, IsDone);
        PseudoClasses.Set(LastPseudoClass, IsLast);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDoneProperty)
        {
            PseudoClasses.Set(DonePseudoClass, IsDone);
        }
        else if (change.Property == IsLastProperty)
        {
            PseudoClasses.Set(LastPseudoClass, IsLast);
        }
        else if (change.Property == NumberProperty && _number is not null)
        {
            _number.Text = Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StepPeer(this);

    private void OnCopied(object? sender, EventArgs e) => NotifyCopied();

    /// <summary>Reports a finished copy of the current snippet (the copy button calls it; tests call it directly).</summary>
    internal void NotifyCopied()
    {
        if (_copyValue is not null)
        {
            Copied?.Invoke(this, _copyValue);
        }
    }

    /// <summary>Invisible copies of every line the step can show: the cell is as tall as the tallest, so nothing moves.</summary>
    private void BuildReserve()
    {
        if (_reserve is null)
        {
            return;
        }
        _reserve.Children.Clear();
        foreach (var text in _reserveTexts)
        {
            _reserve.Children.Add(new TextBlock { Text = text });
        }
    }

    private static Control FileLabel(string fileName, bool isCommand)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new ArcGlyph
        {
            Data = isCommand ? ArcIcons.Terminal : ArcIcons.FileCode,
            Size = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(new TextBlock { Text = fileName, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private sealed class StepPeer(InstallSnippetStepView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}
