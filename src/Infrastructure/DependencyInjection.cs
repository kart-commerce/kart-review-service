using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Infrastructure.ContentSafetyClassifier;
using Kart.Review.Infrastructure.Idempotency;
using Kart.Review.Infrastructure.Messaging;
using Kart.Review.Infrastructure.Persistence;
using Kart.Review.Infrastructure.Persistence.ReadModel;
using Kart.Review.Infrastructure.Security;
using Kart.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Kart.Review.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddWriteSidePersistence(services, configuration);
        AddReadSidePersistence(services, configuration);
        AddMessaging(services, configuration);
        AddContentSafetyClassifier(services, configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPrincipal, HttpCurrentPrincipal>();
        services.AddScoped<IIdempotencyGuard, EfIdempotencyGuard>();
        services.AddHostedService<IdempotencyCleanupHostedService>();

        return services;
    }

    /// <summary>PostgreSQL — the sole write-side source of truth for every aggregate (database-design.md).</summary>
    private static void AddWriteSidePersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IRlsPrincipalContextAccessor, HttpRlsPrincipalContextAccessor>();
        services.AddScoped<RlsConnectionInterceptor>();

        // (sp, options) overload resolves RlsConnectionInterceptor fresh per scoped ReviewDbContext
        // instance (one per request/unit-of-work) — see that interceptor's own remarks for why this
        // is what keeps pooled-connection RLS session variables from leaking across requests.
        services.AddDbContext<ReviewDbContext>((sp, options) =>
            options.UseNpgsql(configuration.GetConnectionString("ReviewDatabase"))
                .AddInterceptors(sp.GetRequiredService<RlsConnectionInterceptor>()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ReviewDbContext>());
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IProductRatingRepository, ProductRatingRepository>();
        services.AddScoped<IProductRatingLedgerRepository, ProductRatingLedgerRepository>();
        services.AddScoped<IVerifiedPurchaseRepository, VerifiedPurchaseRepository>();
    }

    /// <summary>MongoDB — the CQRS read side `GET /v1/reviews` (REV-8) serves from, kept in sync by <see cref="ReviewReadModelProjectionHostedService"/> (REV-5).</summary>
    private static void AddReadSidePersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            // requirement-spec.md's P95<150ms/P99<400ms read-path SLA: fail fast during a Mongo
            // outage rather than hang for the driver's 30s default server-selection timeout.
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(settings);
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return new ReviewReadDbContext(sp.GetRequiredService<IMongoClient>().GetDatabase(options.Database));
        });
        services.AddHostedService<MongoIndexInitializerHostedService>();

        services.AddScoped<IReviewReadModelRepository, ReviewReadModelRepository>();
        services.AddSingleton<IUserDisplayNameResolver, MaskedUserDisplayNameResolver>();
        services.AddHostedService<ReviewReadModelProjectionHostedService>();
    }

    /// <summary>
    /// `contracts/message-bus-manifest.json` is the single source of truth for this service's
    /// entire RabbitMQ topology — nothing messaging-related is hardcoded in C#. REV-4 (outbox
    /// relay), REV-2 (order events consumer), REV-6 (rating projection self-consumer).
    /// </summary>
    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddKartMessageBusManifest(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value.ManifestPath);
        services.AddKartRabbitMqConnectionFactory(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqConnectionSettings(options.HostName, options.Port, options.UserName, options.Password);
        });
        services.AddKartRabbitMqTopologyStartup();

        services.AddHostedService<OutboxRelayHostedService>();
        services.AddHostedService<OrderEventsConsumerHostedService>();
        services.AddHostedService<RatingProjectionConsumerHostedService>();
    }

    /// <summary>REV-1: the synchronous content-safety pre-check, resilience-wrapped internally (see <see cref="RuleBasedContentSafetyClassifier"/>).</summary>
    private static void AddContentSafetyClassifier(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ContentSafetyClassifierOptions>(configuration.GetSection(ContentSafetyClassifierOptions.SectionName));
        services.AddSingleton<IContentSafetyClassifier, RuleBasedContentSafetyClassifier>();
    }
}
