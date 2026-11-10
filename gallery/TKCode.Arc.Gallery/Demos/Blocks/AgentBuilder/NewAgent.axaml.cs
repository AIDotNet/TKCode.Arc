using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentBuilderNewAgentDemo : UserControl
{
    public AgentBuilderNewAgentDemo()
    {
        InitializeComponent();
        // Any data given before the builder shows keeps the sample agent out.
        Builder.Providers.AddRange(AgentBuilderSamples.Providers());
        Builder.Models.AddRange(AgentBuilderSamples.Models());
        Builder.Tools.Add(new AgentBuilderTool
        {
            Id = "web", Name = "Web search", Glyph = ArcIcons.Globe, RunningLabel = "Searching the web",
            Description = "Searches the public web for recent facts."
        });
        Builder.Tools.Add(new AgentBuilderTool
        {
            Id = "files", Name = "Read shared drive", Glyph = ArcIcons.FolderOpen, RunningLabel = "Reading the drive",
            Description = "Opens documents in the team's shared drive."
        });
        Builder.Tools.Add(new AgentBuilderTool
        {
            Id = "mail", Name = "Draft emails", Glyph = ArcIcons.Mail, RunningLabel = "Drafting an email",
            Description = "Writes drafts in your outbox; it never sends them."
        });
        Builder.Instructions = "You write first drafts of release notes from merged pull requests.";
    }
}
