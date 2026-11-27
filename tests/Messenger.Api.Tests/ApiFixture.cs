using Messenger.Domain.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Messenger.Api.Tests;

/// <summary>Controllable clock: real time plus an offset that tests can advance.</summary>
public sealed class TestClock : IClock
{
    private long _offsetTicks;
    public DateTime UtcNow => DateTime.UtcNow + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));
    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);
}

public sealed class ApiFactory(TestClock clock) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(clock);
        });
    }
}

/// <summary>Shared PostgreSQL container + two factories (normal one and an isolated one for time-travel tests).</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public string UploadPath { get; } = Path.Combine(Path.GetTempPath(), "messenger-tests-" + Guid.NewGuid().ToString("N"));
    public TestClock Clock { get; } = new();
    public TestClock TimeClock { get; } = new();
    public ApiFactory Factory { get; private set; } = null!;
    public ApiFactory TimeFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        Directory.CreateDirectory(UploadPath);
        // Env vars are read by Program.cs at CreateBuilder (before any factory callback), so use them.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _pg.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-0123456789-abcdef");
        Environment.SetEnvironmentVariable("RunMigrations", "true");
        Environment.SetEnvironmentVariable("Storage__UploadPath", UploadPath);
        Environment.SetEnvironmentVariable("RateLimit__AuthPerMinute", "100000");
        Factory = new ApiFactory(Clock);
        _ = Factory.Server; // start host (runs migrations)
        TimeFactory = new ApiFactory(TimeClock);
        _ = TimeFactory.Server;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (TimeFactory is not null) await TimeFactory.DisposeAsync();
        await _pg.DisposeAsync();
        try { Directory.Delete(UploadPath, true); } catch { /* best effort */ }
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
