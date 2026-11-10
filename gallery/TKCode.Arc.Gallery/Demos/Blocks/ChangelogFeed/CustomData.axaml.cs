using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChangelogFeedCustomDataDemo : UserControl
{
    public ChangelogFeedCustomDataDemo()
    {
        InitializeComponent();
        Feed.Months =
        [
            new ChangelogMonth { Key = "2026-03", Label = "March 2026", ShortLabel = "Mar" },
            new ChangelogMonth { Key = "2026-02", Label = "February 2026", ShortLabel = "Feb" }
        ];
        Feed.Entries =
        [
            new ChangelogEntry
            {
                Id = "tags", Month = "2026-03", Date = "Mar 3", Version = "2.1.0", Kind = ChangelogKind.New,
                Title = "Tags for every note", Summary = "Group notes across folders.",
                Details = ["Type a hash sign to add a tag.", "Rename a tag once and every note follows."],
                Media = new ChangelogCode { File = "tags.json", Code = "{ \"tags\": [\"travel\", \"ideas\"] }" }
            },
            new ChangelogEntry
            {
                Id = "sync", Month = "2026-02", Date = "Feb 12", Version = "2.0.1", Kind = ChangelogKind.Fixed,
                Title = "Sync keeps long notes whole", Summary = "Notes over 1 MB no longer stop halfway.",
                Details = ["Large notes now upload in parts."]
            }
        ];
        Feed.Subscribed += (_, e) => Feed.Subtitle = "Thanks, " + e.Email;
    }
}
