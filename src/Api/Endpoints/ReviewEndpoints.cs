using Kart.Review.Api.Security;
using Kart.Review.Application.Common.Models;
using Kart.Review.Application.Features.EditReview;
using Kart.Review.Application.Features.ListReviewsBySku;
using Kart.Review.Application.Features.ModerateReview;
using Kart.Review.Application.Features.RetractReview;
using Kart.Review.Application.Features.SubmitReview;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Kart.Review.Api.Endpoints;

/// <summary>api-contract.yaml's `/v1/reviews`, `/v1/reviews/{id}`, `/v1/reviews/{id}/moderate` operations — minimal-API endpoints (kart-identity-service's style), each a one-liner delegating to <see cref="ISender"/>.</summary>
public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/reviews").WithTags("Reviews");

        group.MapPost("/", SubmitReview).RequireAuthorization();
        group.MapGet("/", ListReviews).AllowAnonymous();
        group.MapPatch("/{id:guid}", EditReview).RequireAuthorization();
        group.MapDelete("/{id:guid}", RetractReview).RequireAuthorization();
        group.MapPatch("/{id:guid}/moderate", ModerateReview).RequireAuthorization(AuthenticationExtensions.ModeratorPolicy);

        return app;
    }

    private static async Task<Created<ReviewResponse>> SubmitReview(
        [FromBody] SubmitReviewRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new SubmitReviewCommand(request.OrderId, request.Sku, request.Rating, request.BodyText, idempotencyKey), cancellationToken);
        return TypedResults.Created($"/v1/reviews/{response.ReviewId}", response);
    }

    private static async Task<Ok<ReviewListResponse>> ListReviews(
        [FromQuery] string sku,
        // C# default values (not just runtime `== 0` fallbacks) are what makes these OPTIONAL to
        // minimal-API's own parameter binder — a non-nullable int query parameter with no default
        // is implicitly required, and an omitted one fails binding with an empty-bodied 400 before
        // this method ever runs (api-contract.yaml's own page/pageSize defaults, applied here).
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        ISender sender = null!,
        CancellationToken cancellationToken = default)
    {
        var response = await sender.Send(new ListReviewsBySkuQuery(sku, page, pageSize), cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<ReviewResponse>> EditReview(
        Guid id,
        [FromBody] EditReviewRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new EditReviewCommand(id, request.Rating, request.BodyText, idempotencyKey), cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<ReviewResponse>> RetractReview(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new RetractReviewCommand(id), cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<ReviewResponse>> ModerateReview(
        Guid id,
        [FromBody] ModerateReviewRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ModerateReviewCommand(id, request.Action, request.Reason), cancellationToken);
        return TypedResults.Ok(response);
    }

    private sealed record SubmitReviewRequest(Guid OrderId, string Sku, int Rating, string BodyText);

    private sealed record EditReviewRequest(int? Rating, string? BodyText);

    private sealed record ModerateReviewRequest(string Action, string? Reason);
}
