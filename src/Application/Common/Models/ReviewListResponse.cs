namespace Kart.Review.Application.Common.Models;

/// <summary>api-contract.yaml's <c>ReviewListView</c> schema.</summary>
public sealed record ReviewListResponse(IReadOnlyList<PublicReviewResponse> Items, int Page, int PageSize, long TotalCount);
