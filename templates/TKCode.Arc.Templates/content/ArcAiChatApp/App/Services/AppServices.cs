using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.SampleData;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Services;

/// <summary>
/// The app's services. <see cref="IAiChatResponder"/> answers the messages: SampleData/SampleResponder.cs streams a
/// reply without any model. Register your own responder here (SampleData/SampleResponder.cs shows one over
/// Microsoft.Extensions.AI's IChatClient), and your own models in SampleData/SampleModels.cs. Everything is registered
/// with its type written out, so the container needs no reflection over the assembly and the app can be trimmed.
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAiChatResponder, SampleResponder>();
        services.AddSingleton<IConversationStore, ConversationStore>();
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }
}
