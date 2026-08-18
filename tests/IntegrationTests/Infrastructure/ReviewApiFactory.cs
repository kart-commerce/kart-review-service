using Kart.Review.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Kart.Review.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the full API against REAL Postgres/Mongo/RabbitMQ Testcontainers — not fakes/in-memory —
/// so integration tests exercise the actual EF migrations (including the RLS policies and the
/// status-guard trigger), the actual Mongo read-model projector, and the actual RabbitMQ
/// self-consumption/relay hosted services end to end, matching the user's "test with a real db"
/// requirement.
/// </summary>
public sealed class ReviewApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("kart_review_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    // Pinned to 7.0 explicitly — Testcontainers.MongoDb's own default (6.0) has a known
    // docker-entrypoint bootstrap race in this environment (the temp setup instance's port isn't
    // always released before the "real" mongod tries to bind 27017, surfacing as exit code 48
    // "Address already in use").
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    /// <summary>Exposed so tests can publish directly onto the real broker (e.g. simulating kart-order-service's own OrderCreated/OrderDelivered) instead of only exercising this service's own HTTP surface.</summary>
    public string RabbitMqHostName => _rabbitMq.Hostname;

    public int RabbitMqPort => _rabbitMq.GetMappedPublicPort(5672);

    public const string RabbitMqUserName = "test";
    public const string RabbitMqPassword = "test";

    /// <summary>
    /// `AddKartGlobalConfig` throws unless `GlobalConfig:Path` resolves to an existing file — a
    /// module-level static constructor runs once per test assembly load, well before any factory
    /// instance boots a host, guaranteeing the env var is set before `Program.cs`'s top-level
    /// statements ever read it (a `ConfigureAppConfiguration` callback registered on the factory
    /// runs too late for this specific setting — Program.cs reads it synchronously before
    /// `builder.Build()`, which is before any WebApplicationFactory hook applies).
    /// </summary>
    static ReviewApiFactory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kart-review-test-globalconfig-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"Global":{},"Services":{"kart-review-service":{}}}""");
        Environment.SetEnvironmentVariable("GlobalConfig__Path", path);
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync(), _rabbitMq.StartAsync());

        var optionsBuilder = new DbContextOptionsBuilder<ReviewDbContext>().UseNpgsql(_postgres.GetConnectionString());
        await using var dbContext = new ReviewDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _mongo.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReviewDatabase"] = _postgres.GetConnectionString(),
                ["Mongo:ConnectionString"] = _mongo.GetConnectionString(),
                ["Mongo:Database"] = "kart_review_test",
                ["RabbitMq:HostName"] = _rabbitMq.Hostname,
                ["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:UserName"] = "test",
                ["RabbitMq:Password"] = "test",
                // Fast poll intervals aren't configurable on the hosted services (fixed constants),
                // so async assertions below poll-and-wait instead of relying on a tight interval.
                ["ContentSafetyClassifier:BannedTerms:0"] = "scam",
                ["ContentSafetyClassifier:BannedTerms:1"] = "flagged-content-marker",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}

/// <summary>xUnit collection so every test in `IntegrationTests` shares one set of containers instead of starting three per test class.</summary>
[CollectionDefinition(Name)]
public sealed class ReviewApiCollection : ICollectionFixture<ReviewApiFactory>
{
    public const string Name = "ReviewApi";
}
