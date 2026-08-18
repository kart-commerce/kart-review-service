using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kart.Review.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory <c>dotnet ef migrations add</c>/<c>database update</c> use to build
/// <see cref="ReviewDbContext"/> without spinning up the full Api host. Never used at runtime —
/// the app's own DI registration (<see cref="DependencyInjection.AddInfrastructure"/>) takes over
/// there.
/// </summary>
public sealed class ReviewDbContextFactory : IDesignTimeDbContextFactory<ReviewDbContext>
{
    public ReviewDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("REVIEW_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_review;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ReviewDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ReviewDbContext(optionsBuilder.Options);
    }
}
