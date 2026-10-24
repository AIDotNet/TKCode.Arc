using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OnboardingIllustrationEventsLogDemo : UserControl
{
    private readonly List<string> _lines = new();

    public OnboardingIllustrationEventsLogDemo()
    {
        InitializeComponent();
        Card.Steps.AddRange(OnboardingIllustrationDemoData.InviteSteps());
        Card.StepChanged += (_, e) => Add($"StepChanged  {e.OldIndex} → {e.NewIndex}  ({e.Reason})");
        Card.Skipped += (_, _) => Add("Skipped");
        Card.GetStarted += (_, _) => Add("GetStarted   (send the invites here)");
        Card.Replayed += (_, _) => Add("Replayed");
    }

    private void Add(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 6)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
