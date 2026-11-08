using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.SampleData;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Services;

/// <summary>
/// The app's services: where the workflow is kept (<see cref="IWorkflowStore"/>) and what runs it
/// (<see cref="IWorkflowRunner"/>; SampleData/SampleRunner.cs only pretends). Register your own here. Everything is
/// registered with its type written out, so the container needs no reflection over the assembly and the app can be trimmed.
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowStore, WorkflowStore>();
        services.AddSingleton<IWorkflowRunner, SampleRunner>();
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }
}
