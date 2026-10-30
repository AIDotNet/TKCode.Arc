namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented starters the starter prompts demos show: 24 cards over Write, Code, Analyze and Plan, some of them using a
/// connected app, with the greeting, description and placeholder around them. Names, numbers and projects are made up;
/// the model name is a real one from <see cref="AiModelCatalog"/>.
/// </summary>
internal static class StarterPromptsDemoData
{
    public const string Greeting = "What are we working on, Maya?";

    public const string Description = "Start from a starter, or ask anything. Connected apps are marked on the card.";

    public const string Placeholder = "Ask anything, or pick a starter above";

    /// <summary>The model in the composer's footer: Alibaba Cloud's Qwen3.8-Max (checked on <see cref="AiModelCatalog.CheckedOn"/>).</summary>
    public static readonly string ModelName = AiModelCatalog.Qwen38Max.Name;

    public static readonly IReadOnlyList<StarterPromptCategory> Categories =
    [
        new("write", "Write", ArcIcons.PenLine),
        new("code", "Code", ArcIcons.Code),
        new("analyze", "Analyze", ArcIcons.ChartBar),
        new("plan", "Plan", ArcIcons.ListTodo),
    ];

    /// <summary>The starters, in tab order.</summary>
    public static readonly IReadOnlyList<StarterPrompt> Prompts =
    [
        P("launch-email", "write", "Draft the launch email", "For the 1,200 beta customers", StarterPromptApp.Mail,
          "Draft a launch email for the 1,200 customers in the beta list. Lead with the two features they asked for most, keep it under 150 words and end with one clear call to action."),
        P("tighten-post", "write", "Tighten this blog post", "Cut it to a 4 minute read", null,
          "Edit the blog post I paste next so it reads in about 4 minutes. Keep my voice, cut repetition, and list the three biggest changes you made."),
        P("reply-threads", "write", "Answer the open threads", "6 customer emails waiting", StarterPromptApp.Mail,
          "Draft replies to the 6 open customer threads in my inbox. Match each sender's tone, answer the question they asked first, and flag any that need a refund decision."),
        P("release-notes", "write", "Write the release notes", "From last week's merged changes", StarterPromptApp.Repository,
          "Write release notes from the changes merged last week. Group them into New, Improved and Fixed, use plain words, and leave out internal refactors."),
        P("team-update", "write", "Write the weekly update", "Wins, risks and asks", StarterPromptApp.Docs,
          "Turn this week's project notes into a team update with three sections: wins, risks and asks. Keep each item to one sentence."),
        P("job-post", "write", "Draft a job post", "Senior product designer", null,
          "Draft a job post for a senior product designer on a small, remote team. Describe the work in the first 90 days, the must-haves and what we offer, without buzzwords."),
        P("review-pr", "code", "Review my open pull request", "Find bugs before review", StarterPromptApp.Repository,
          "Review my open pull request. Point out bugs, missing tests and unclear names first, then smaller style notes, each with the file and line."),
        P("explain-code", "code", "Explain this function", "Line by line, in plain words", null,
          "Explain the function I paste next line by line in plain words, then describe one input that would make it fail."),
        P("write-tests", "code", "Write tests for the parser", "Cover the edge cases", null,
          "Write unit tests for the date parser I paste next. Cover empty input, time zones, leap days and invalid formats, and name each test after the behavior it checks."),
        P("fix-issue", "code", "Fix the top bug", "The oldest open crash report", StarterPromptApp.Issues,
          "Look at the oldest open crash report in the tracker, explain the likely cause, and propose a minimal fix with a test that reproduces it."),
        P("sql-query", "code", "Write a SQL query", "Weekly active users by plan", null,
          "Write a SQL query for weekly active users by plan for the last 12 weeks, from an events table with user_id, plan and created_at. Explain any assumptions."),
        P("refactor", "code", "Refactor a long component", "Split it without changing behavior", null,
          "Refactor the component I paste next into smaller pieces without changing what it renders. Show the new files and explain why each split helps."),
        P("churn", "analyze", "Find why customers churned", "From last quarter's exit survey", StarterPromptApp.Docs,
          "Read last quarter's exit survey answers and group the reasons customers left into five themes, with a count and one representative quote for each."),
        P("metrics", "analyze", "Summarize this week's metrics", "What moved and why", null,
          "Summarize this week's product metrics I paste next: what moved more than 5%, the likely reason, and one question worth asking the team."),
        P("compare-plans", "analyze", "Compare our pricing plans", "Against what customers use", null,
          "Compare our three pricing plans against the usage data I paste next. Which limits do customers hit first, and which plan looks underpriced?"),
        P("bug-trends", "analyze", "Spot trends in bug reports", "The last 30 days", StarterPromptApp.Issues,
          "Look at the bug reports filed in the last 30 days and tell me which areas of the product get the most reports, and whether any of them is getting worse."),
        P("survey", "analyze", "Read the survey results", "412 answers, sample data", null,
          "Analyze the 412 survey answers I paste next. Give me the three strongest findings, any surprising splits by plan, and what we should not conclude from it."),
        P("meeting-load", "analyze", "Audit my meeting load", "Where the week went", StarterPromptApp.Calendar,
          "Look at my calendar for the last two weeks and show how my time split between meetings, focus time and one-on-ones, and which recurring meetings I could drop."),
        P("plan-week", "plan", "Plan my week", "Around the Thursday deadline", StarterPromptApp.Calendar,
          "Plan my week around the Thursday deadline. Block focus time for the proposal, keep my recurring meetings, and leave Friday afternoon free."),
        P("roadmap", "plan", "Draft a Q1 roadmap", "Three themes, six bets", StarterPromptApp.Docs,
          "Draft a Q1 roadmap with three themes and two bets under each, with the problem, the expected outcome and a rough size for every bet."),
        P("launch-plan", "plan", "Plan the launch", "Two weeks, step by step", StarterPromptApp.Issues,
          "Plan a two week launch for the new reporting feature as a checklist of issues: owners, due dates, and what blocks what."),
        P("offsite", "plan", "Plan a team offsite", "Eight people, two days", null,
          "Plan a two day offsite for eight people. Suggest an agenda that mixes planning and downtime, and list what to book three weeks ahead."),
        P("onboarding", "plan", "Plan a new hire's first week", "An engineer starting Monday", StarterPromptApp.Calendar,
          "Plan the first week for an engineer starting Monday: meetings to set up, the codebase tour, a first small task and who to pair with each day."),
        P("migration", "plan", "Break down the migration", "Into small, safe steps", StarterPromptApp.Repository,
          "Break the database migration into small steps we can ship one at a time, each with a rollback plan and a check that it worked."),
    ];

    /// <summary>Gives <paramref name="control"/> the demo's tabs and starters (they are only read, so the demos share them).</summary>
    public static void AddStarters(StarterPrompts control)
    {
        control.Categories.AddRange(Categories);
        control.Prompts.AddRange(Prompts);
    }

    private static StarterPrompt P(string id, string category, string title, string description, StarterPromptApp? app, string prompt) =>
        new() { Id = id, Category = category, Title = title, Description = description, App = app, Prompt = prompt };
}
