using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Xunit;
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif
#if (IsAiChat || IsWorkflow)
using ArcStarterApp.Services;
#endif

// Every test gets a fresh app on the headless platform, and the tests run one after another (Avalonia has one UI thread).
[assembly: AvaloniaTestApplication(typeof(ArcStarterApp.Tests.TestApp))]
[assembly: AvaloniaTestFramework]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ArcStarterApp.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
#if (UsesSettings)
        // Keep the settings in memory: tests must never read or change the person's own settings file.
        AppSettings.Store = Settings;
#endif
#if (IsAiChat)
        // The conversations too.
        ConversationStore.Store = Conversations;
#endif
#if (IsWorkflow)
        // The workflow too.
        WorkflowStore.Store = Workflow;
#endif
        return AppBuilder.Configure<App>()
                         .UseArcApp()
                         .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
#if (UsesSettings)

    /// <summary>Where the app under test saves its settings.</summary>
    public static MemorySettingsStore Settings { get; } = new();
#endif
#if (IsAiChat)

    /// <summary>Where the app under test saves its conversations.</summary>
    public static MemorySettingsStore Conversations { get; } = new();
#endif
#if (IsWorkflow)

    /// <summary>Where the app under test saves its workflow.</summary>
    public static MemorySettingsStore Workflow { get; } = new();
#endif
}
