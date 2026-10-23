using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented runs for <see cref="AgentTimeline"/>: the incident investigation the timeline replays when it is given no
/// run, and a small finished run. Every name, model, number and sentence is made up; model marks and tool marks are
/// Lucide glyphs. Each call returns new objects, so a run can be changed (or fed to a live driver) without touching
/// another timeline's copy.
/// </summary>
public static class AgentTimelineSamples
{
    private const string Lead = "lead";
    private const string Status = "status";
    private const string Config = "config";
    private const string Logs = "logs";
    private const string Scribe = "scribe";

    /// <summary>
    /// The default run, "Signup emails bouncing since Mar 3": a coordinator plans three parallel checks (provider status,
    /// mail config, mail logs), waits, combines their reports, has a scribe write the incident note and files it. Five
    /// agents, 30 spans (a failed log scan and its retry among them), eight handoffs; 26.8 s, 49,920 tokens, $0.15,
    /// 1.5x parallel speedup.
    /// </summary>
    public static AgentTimelineRun CreateIncidentRun()
    {
        var run = new AgentTimelineRun { Title = "Signup emails bouncing since Mar 3" };
        run.Agents.Add(new AgentTimelineAgent(Lead, "Coordinator", new AgentModel("Nimbus Pro 3", ArcIcons.Sparkles), 8)
        {
            Role = "Plans the checks and writes the answer"
        });
        run.Agents.Add(new AgentTimelineAgent(Status, "Status watcher", new AgentModel("Vela 4 Swift", ArcIcons.Zap), 2.6)
        {
            Role = "Checks outside services"
        });
        run.Agents.Add(new AgentTimelineAgent(Config, "Config auditor", new AgentModel("Nimbus Core 3", ArcIcons.Sparkles), 2.5)
        {
            Role = "Reads configuration history"
        });
        run.Agents.Add(new AgentTimelineAgent(Logs, "Log reader", new AgentModel("Nimbus Lite 3", ArcIcons.Sparkles), 0.8)
        {
            Role = "Queries the mail logs"
        });
        run.Agents.Add(new AgentTimelineAgent(Scribe, "Scribe", new AgentModel("Orion Flash 2", ArcIcons.Star), 0.6)
        {
            Role = "Writes the incident note"
        });

        var web = ArcIcons.Globe;
        var repo = ArcIcons.GitBranch;
        var mail = ArcIcons.Database;
        var wiki = ArcIcons.BookOpen;

        Think(run, Lead, 0, 2.1, "Planned three parallel checks", 2050);
        Wait(run, Lead, 2.1, 14.9, "Waiting on logs, config and status");
        Think(run, Lead, 14.9, 17.0, "Combined the reports: the SPF edit is the cause", 4120);
        Wait(run, Lead, 17.0, 23.4, "Waiting on the scribe");
        Think(run, Lead, 23.4, 25.5, "Edited the note for clarity", 2980);
        Tool(run, Lead, 25.5, 26.8, "Filed the note in the team wiki", "Wiki", wiki, 240);

        Think(run, Status, 3.6, 4.2, "Made a list of external suspects", 520);
        Tool(run, Status, 4.2, 5.8, "Checked the mail provider status page", "Web search", web, 3600);
        Think(run, Status, 5.8, 6.4, "Moved on to blocklists", 590);
        Tool(run, Status, 6.4, 7.9, "Searched blocklist reports for our domain", "Web search", web, 4210);
        Think(run, Status, 7.9, 9.3, "Provider looks healthy", 1240);

        Think(run, Config, 3.0, 3.6, "Chose which configs to read", 640);
        Tool(run, Config, 3.6, 4.9, "Found 6 mail config commits this month", "Repo", repo, 2300);
        Think(run, Config, 4.9, 5.6, "Picked the two DNS edits", 910);
        Tool(run, Config, 5.6, 7.7, "Compared the SPF record before and after", "Repo", repo, 6400);
        Think(run, Config, 7.7, 8.6, "Found a missing include for the mail relay", 1380);
        Tool(run, Config, 8.6, 9.9, "Found when the edit went live", "Repo", repo, 1690);
        Think(run, Config, 9.9, 12.3, "Summarized the SPF edit and its timing", 2150);

        Think(run, Logs, 2.4, 3.1, "Drafted a March bounce query", 560);
        var failed = Tool(run, Logs, 3.1, 5.2, "Bounce scan took too long", "Mail logs", mail, 430);
        failed.Status = AgentTimelineSpanStatus.Failed;
        failed.Detail = "Full scan of mail events ran past the time limit";
        Think(run, Logs, 5.2, 5.8, "Narrowed the query to hard bounces", 400);
        var retry = Tool(run, Logs, 5.8, 7.4, "Second try: hard bounces per domain", "Mail logs", mail, 2050);
        retry.IsRetry = true;
        retry.Detail = "Worked once the query was narrowed";
        Think(run, Logs, 7.4, 8.4, "Saw bounces from one sender domain only", 700);
        Tool(run, Logs, 8.4, 10.0, "Grouped bounce codes for that domain", "Mail logs", mail, 2280);
        Think(run, Logs, 10.0, 11.9, "Matched the bounces to SPF failures", 880);
        Tool(run, Logs, 11.9, 13.2, "2,316 signups affected", "Mail logs", mail, 1090);
        Think(run, Logs, 13.2, 14.6, "Wrote the log summary", 660);

        Think(run, Scribe, 17.3, 18.1, "Read the coordinator's brief", 1780);
        Tool(run, Scribe, 18.1, 19.0, "Fetched the SPF edit's commit message", "Repo", repo, 1050);
        Think(run, Scribe, 19.0, 23.1, "Wrote a 380-word incident note", 3020);

        // Dispatches go to the deepest row first and results come back from the top, so each vertical crosses idle rows.
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Logs, 2.1, "Bounce counts by day for March"));
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Config, 2.7, "Mail config changes since Mar 1"));
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Status, 3.3, "Is the mail provider down?"));
        run.Handoffs.Add(new AgentTimelineHandoff(Status, Lead, 9.6, "Provider status green all week"));
        run.Handoffs.Add(new AgentTimelineHandoff(Config, Lead, 12.6, "SPF edit on Mar 2 dropped the relay"));
        run.Handoffs.Add(new AgentTimelineHandoff(Logs, Lead, 14.9, "One sender domain, 2,316 signups hit"));
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Scribe, 17.0, "Write up the incident"));
        run.Handoffs.Add(new AgentTimelineHandoff(Scribe, Lead, 23.4, "Note drafted, please review"));
        return run;
    }

    /// <summary>
    /// A small finished run, "Monthly refund summary": a planner asks a data clerk which refund reasons grew and waits for
    /// the answer. Two agents, four spans, two handoffs, 5.8 s.
    /// </summary>
    public static AgentTimelineRun CreateRefundRun()
    {
        var run = new AgentTimelineRun { Title = "Monthly refund summary" };
        run.Agents.Add(new AgentTimelineAgent("planner", "Planner", new AgentModel("Nimbus Pro 3", ArcIcons.Sparkles), 8));
        run.Agents.Add(new AgentTimelineAgent("clerk", "Data clerk", new AgentModel("Nimbus Lite 3", ArcIcons.Sparkles), 0.2));
        Think(run, "planner", 0, 1.2, "Outlined the summary", 1100);
        Wait(run, "planner", 1.2, 5.8, "Waiting on the data clerk");
        Tool(run, "clerk", 1.5, 3.9, "Exported last month's refunds", "Ledger", ArcIcons.Database, 2200);
        Think(run, "clerk", 3.9, 5.5, "Ranked the refund reasons", 800);
        run.Handoffs.Add(new AgentTimelineHandoff("planner", "clerk", 1.2, "Which refund reasons grew?"));
        run.Handoffs.Add(new AgentTimelineHandoff("clerk", "planner", 5.8, "Damaged items lead the refunds"));
        return run;
    }

    private static AgentTimelineSpan Think(AgentTimelineRun run, string agent, double start, double end, string label, int tokens)
        => Add(run, new AgentTimelineSpan(agent, AgentTimelineSpanKind.Thinking, start, end, label) { Tokens = tokens });

    private static AgentTimelineSpan Wait(AgentTimelineRun run, string agent, double start, double end, string label)
        => Add(run, new AgentTimelineSpan(agent, AgentTimelineSpanKind.Waiting, start, end, label));

    private static AgentTimelineSpan Tool(AgentTimelineRun run, string agent, double start, double end, string label, string tool,
        Geometry mark, int tokens)
        => Add(run, new AgentTimelineSpan(agent, AgentTimelineSpanKind.Tool, start, end, label) { Tool = tool, ToolMark = mark, Tokens = tokens });

    private static AgentTimelineSpan Add(AgentTimelineRun run, AgentTimelineSpan span)
    {
        run.Spans.Add(span);
        return span;
    }
}
