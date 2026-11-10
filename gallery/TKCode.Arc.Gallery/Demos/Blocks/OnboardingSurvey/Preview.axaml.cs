using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OnboardingSurveyPreviewDemo : UserControl
{
    public OnboardingSurveyPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnCompleted(object? sender, OnboardingSurveyCompletedEventArgs e)
    {
        var answered = e.Answers.Count(a => !a.IsEmpty);
        Toast.Show(this, e.IsSkipped ? "Survey skipped" : $"{answered} answers saved", ToastKind.Success);
    }
}
