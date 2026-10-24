namespace TKCode.Arc.Gallery.Demos;

/// <summary>Sample data of the OnboardingIllustration demos: a research notes app with its own steps and button words.</summary>
internal static class OnboardingIllustrationDemoData
{
    /// <summary>Four steps of an invented research notes app, one per scene.</summary>
    public static OnboardingStep[] ResearchSteps() =>
    [
        new(OnboardingScene.Folder,
            "Name your research space",
            "A space holds one study. Start with the April interviews and add more studies later.",
            "An empty open box with a plus sign beside it"),
        new(OnboardingScene.Files,
            "Import your recordings",
            "Drop audio, transcripts and notes. Fieldnote tags each file by speaker and date.",
            "Recordings dropping into the open box while a note flies in"),
        new(OnboardingScene.Team,
            "Tag quotes with your team",
            "Invite Lena and Omar. Highlights stay linked to the moment of the interview they quote.",
            "Three researchers around a shared board with one highlighted line"),
        new(OnboardingScene.Badge,
            "Your study is ready",
            "April interviews now has 12 recordings and 3 researchers. Rename it any time.",
            "A ribbon badge with a check mark")
    ];

    /// <summary>The research app's button words and announcements.</summary>
    public static OnboardingIllustrationLabels ResearchLabels() => new()
    {
        Skip = "Skip tour",
        GetStarted = "Open study",
        ReplayIntro = "Show tour",
        StepsGroupName = "Tour steps",
        StepStatus = (step, count) => $"Tour step {step} of {count}",
        CompletedStatus = "Tour finished",
        DotName = (step, title) => $"Tour step {step}: {title}"
    };

    /// <summary>Three steps of an invented team chat app's invite flow.</summary>
    public static OnboardingStep[] InviteSteps() =>
    [
        new(OnboardingScene.Folder,
            "Create a channel",
            "Channels keep one topic together. Start with #launch-week.",
            "An empty open box with a plus sign beside it"),
        new(OnboardingScene.Team,
            "Invite the people who need it",
            "Add Priya, Jonas and Mei. They see the history from the first message.",
            "Three people gathered around one shared board"),
        new(OnboardingScene.Badge,
            "The channel is live",
            "#launch-week has 3 members. Pin the plan so everyone finds it.",
            "A ribbon badge with a check mark")
    ];
}
