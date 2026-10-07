using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Application.Chat;

public static class ChatServiceCollectionExtensions
{
    /// <summary>Live chat service and the in-memory agent presence (CRM-56). Crm.Api registers the SignalR notifier.</summary>
    public static IServiceCollection AddChat(this IServiceCollection services)
    {
        services.TryAddSingleton<IAgentPresence, InMemoryAgentPresence>();
        services.AddScoped<IChatService, ChatService>();
        return services;
    }
}
