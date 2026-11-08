using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A chapter of the handbook in the data-bound FluidTabControl demo.</summary>
public sealed record HandbookChapter(string Number, string Title, string Subtitle, string Body);

public partial class FluidTabControlDataBoundDemo : UserControl
{
    public FluidTabControlDataBoundDemo()
    {
        InitializeComponent();
        var chapters = new List<HandbookChapter>
        {
            new("1", "Welcome", "Your first week",
                "Your laptop, accounts and badge are ready on day one. Your manager books a 30 minute check-in every day of the first week, and your buddy shows you where things are."),
            new("2", "How we work", "Hours and meetings",
                "Core hours are 10:00 to 15:00 in your time zone. Meetings have an agenda and end five minutes early. Decisions are written down in the team's channel the same day."),
            new("3", "Time off", "Leave and holidays",
                "You have 25 days of paid leave a year plus public holidays. Book leave in the HR portal at least two weeks ahead, and tell your team in the planning meeting."),
            new("4", "Expenses", "What we pay for",
                "Travel, a home office allowance of 400 per year and books are covered. Submit receipts within 30 days; finance pays them with the next salary."),
            new("5", "Security", "Devices and data",
                "Keep the disk encrypted and the screen locked. Report a lost device to security within an hour. Customer data stays in the systems that hold it."),
        };
        Chapters.ItemsSource = chapters;
        Chapters.SelectionChanged += (_, _) => ShowSelection();
        ShowSelection();
    }

    private void ShowSelection() =>
        Status.Text = Chapters.SelectedItem is HandbookChapter chapter
            ? $"SelectedIndex: {Chapters.SelectedIndex}, SelectedItem: {chapter.Title}"
            : "Nothing selected";
}
