using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One message in the log of an <see cref="AgentOrchestrator"/>: a dot in the color of its kind, "sender → receiver", the
/// kind word, the run time and the text. Its <see cref="StyledElement.DataContext"/> is the
/// <see cref="OrchestratorMessage"/>, so a recycled row follows its new item. The kind is also a word, never color alone.
/// </summary>
[PseudoClasses(DelegatePseudoClass, ResultPseudoClass, ErrorPseudoClass, NotePseudoClass)]
internal sealed class AgentOrchestratorLogRow : TemplatedControl
{
    public const string DelegatePseudoClass = ":delegate";
    public const string ResultPseudoClass = ":result";
    public const string ErrorPseudoClass = ":error";
    public const string NotePseudoClass = ":note";

    public static readonly DirectProperty<AgentOrchestratorLogRow, string> FromNameProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorLogRow, string>(nameof(FromName), o => o.FromName);

    public static readonly DirectProperty<AgentOrchestratorLogRow, string> ToNameProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorLogRow, string>(nameof(ToName), o => o.ToName);

    public static readonly DirectProperty<AgentOrchestratorLogRow, string> KindTextProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorLogRow, string>(nameof(KindText), o => o.KindText);

    public static readonly DirectProperty<AgentOrchestratorLogRow, string> TimeTextProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorLogRow, string>(nameof(TimeText), o => o.TimeText);

    public static readonly DirectProperty<AgentOrchestratorLogRow, string> TextProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorLogRow, string>(nameof(Text), o => o.Text);

    private readonly AgentOrchestrator _owner;
    private string _from = string.Empty;
    private string _to = string.Empty;
    private string _kind = string.Empty;
    private string _time = string.Empty;
    private string _text = string.Empty;

    public AgentOrchestratorLogRow(AgentOrchestrator owner)
    {
        _owner = owner;
    }

    public string FromName { get => _from; private set => SetAndRaise(FromNameProperty, ref _from, value); }
    public string ToName { get => _to; private set => SetAndRaise(ToNameProperty, ref _to, value); }
    public string KindText { get => _kind; private set => SetAndRaise(KindTextProperty, ref _kind, value); }
    public string TimeText { get => _time; private set => SetAndRaise(TimeTextProperty, ref _time, value); }
    public string Text { get => _text; private set => SetAndRaise(TextProperty, ref _text, value); }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Refresh();
    }

    /// <summary>Reads the message and the owner's names and labels again.</summary>
    public void Refresh()
    {
        if (DataContext is not OrchestratorMessage message)
        {
            return;
        }
        var labels = _owner.Labels;
        FromName = _owner.NameOf(message.From);
        ToName = _owner.NameOf(message.To);
        KindText = labels.KindName(message.Kind);
        TimeText = FormatTime(message.Elapsed);
        Text = message.Text;
        PseudoClasses.Set(DelegatePseudoClass, message.Kind == OrchestratorMessageKind.Delegate);
        PseudoClasses.Set(ResultPseudoClass, message.Kind == OrchestratorMessageKind.Result);
        PseudoClasses.Set(ErrorPseudoClass, message.Kind == OrchestratorMessageKind.Error);
        PseudoClasses.Set(NotePseudoClass, message.Kind is OrchestratorMessageKind.Progress or OrchestratorMessageKind.Question
            or OrchestratorMessageKind.Answer);
        AutomationProperties.SetName(this, labels.MessageName(FromName, ToName, KindText, Text));
    }

    /// <summary>Run time as m:ss (h:mm:ss past an hour).</summary>
    internal static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : $"{(int)time.TotalMinutes}:{time.Seconds.ToString("00", CultureInfo.InvariantCulture)}";
    }
}
