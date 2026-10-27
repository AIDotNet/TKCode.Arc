using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamDirectoryFlatDemo : UserControl
{
    public TeamDirectoryFlatDemo()
    {
        InitializeComponent();
        Directory.People.AddRange(TeamDirectoryDemoData.LargerDirectory());

        // The default search covers name, role, team and city; this one adds the email address.
        Directory.SearchFilter = (person, text) =>
        {
            var query = text.Trim();
            return query.Length == 0
                   || (person.Name + " " + person.Role + " " + person.Team + " " + person.City + " " + person.Email)
                      .Contains(query, StringComparison.OrdinalIgnoreCase);
        };
    }
}
