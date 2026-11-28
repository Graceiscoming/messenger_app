using Messenger.Data.Repositories;
using Messenger.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MessengerDbContext>(o => o.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MessengerDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IUserBlockRepository, UserBlockRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IStickerRepository, StickerRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
        await db.Database.MigrateAsync();
    }
}

/// <summary>Lets `dotnet ef` work without the Api project.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<MessengerDbContext>
{
    public MessengerDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("MESSENGER_DB")
                 ?? "Host=localhost;Port=5432;Database=messenger;Username=messenger;Password=messenger_dev";
        var options = new DbContextOptionsBuilder<MessengerDbContext>()
            .UseNpgsql(cs).UseSnakeCaseNamingConvention().Options;
        return new MessengerDbContext(options);
    }
}
