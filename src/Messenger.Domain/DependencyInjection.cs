using Messenger.Domain.Options;
using Messenger.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.Domain;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Domain services (scoped) and the options types (defaults; the host binds configuration:
    /// Chat, Jwt, Storage, Auth). Repositories, notifier and infrastructure abstractions are registered by Data/Api.
    /// </summary>
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        services.AddOptions<ChatOptions>();
        services.AddOptions<JwtOptions>();
        services.AddOptions<StorageOptions>();
        services.AddOptions<AuthOptions>();

        services.AddScoped<ConversationDtoBuilder>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        return services;
    }
}
