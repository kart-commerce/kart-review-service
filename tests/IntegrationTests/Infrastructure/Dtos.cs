namespace Kart.Review.IntegrationTests.Infrastructure;

/// <summary>Test-side mirrors of the API's response shapes — kept independent of the Application project's own DTOs so these tests validate the actual wire format, not just that C# types line up.</summary>
public sealed record ReviewViewDto(
    Guid ReviewId, Guid OrderId, string Sku, Guid UserId, int Rating, string BodyText, string Status,
    PendingRevisionViewDto? PendingRevision, DateTimeOffset? FirstPublishedAt, DateTimeOffset CreatedAt, DateTimeOffset LastEditedAt, DateTimeOffset? RetractedAt);

public sealed record PendingRevisionViewDto(string NewBodyText, int NewRating, DateTimeOffset SubmittedAt);

public sealed record ReviewListViewDto(List<PublicReviewViewDto> Items, int Page, int PageSize, long TotalCount);

public sealed record PublicReviewViewDto(Guid ReviewId, Guid OrderId, string Sku, string AuthorDisplayName, int Rating, string BodyText, DateTimeOffset FirstPublishedAt, DateTimeOffset LastEditedAt);

public sealed record ProductRatingViewDto(string Sku, double Avg, int Count);

public sealed record ProblemDto(string? Title, int? Status, string? Detail, string? ErrorCode, string? TraceId);
