using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProjectBoardCustomStagesDemo : UserControl
{
    public ProjectBoardCustomStagesDemo()
    {
        InitializeComponent();
        var (rhea, tomas, ines) = ProjectBoardDemoData.CreateTeam();
        Board.Team.AddRange([rhea, tomas, ines]);
        Board.CurrentUser = tomas;
        Board.DraftTitles.AddRange(["Triage crash reports", "Check the tablet layout"]);
        Board.DraftArea = "QA";
        Board.Tasks.AddRange(
        [
            new ProjectBoardTask("Offline banner for the cart", "inbox") { Area = "Shop", DueText = "No date", Owner = ines },
            new ProjectBoardTask("Haptics on add to cart", "scoped") { Area = "Shop", DueText = "Mon", Owner = tomas, CommentCount = 2 },
            new ProjectBoardTask("Dark mode for the order history screen", "building") { Area = "Account", DueText = "Today", IsDueSoon = true, Owner = rhea, CommentCount = 5 },
            new ProjectBoardTask("Retry failed photo uploads", "building") { Area = "Reviews", DueText = "Thu", Owner = tomas, CommentCount = 1 },
            new ProjectBoardTask("Faster first launch", "shipped") { Area = "Platform", DueText = "Sep 30", Owner = rhea, CommentCount = 9 },
        ]);
    }
}
