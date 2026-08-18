using Kart.Review.Domain.Common.ValueObjects;

namespace Kart.Review.Infrastructure.Persistence.ReadModel;

/// <summary>
/// Resolves the public-facing display name projected onto <c>review_read_model</c> in place of a
/// raw <c>userId</c> (BRD §24.1.5). database-design.md describes this as "a denormalized lookup
/// against kart-user-service public profile data at write time" — no such cross-service profile
/// endpoint is defined in this service's own approved contracts, so this is a pluggable seam (like
/// <c>IContentSafetyClassifier</c>): swap <see cref="MaskedUserDisplayNameResolver"/> for a real
/// kart-user-service HTTP client once that public-profile endpoint exists, without touching the
/// projector that calls this.
/// </summary>
public interface IUserDisplayNameResolver
{
    Task<string> ResolveAsync(UserId userId, CancellationToken cancellationToken);
}

/// <summary>Default stand-in: a stable, non-reversible masked handle derived from the userId — never the raw GUID, satisfies the "never project the raw user_id" rule without a live dependency.</summary>
public sealed class MaskedUserDisplayNameResolver : IUserDisplayNameResolver
{
    public Task<string> ResolveAsync(UserId userId, CancellationToken cancellationToken) =>
        Task.FromResult($"Customer-{userId.Value.ToString("N")[..8]}");
}
