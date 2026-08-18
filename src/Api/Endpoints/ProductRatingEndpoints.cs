using Kart.Review.Application.Common.Models;
using Kart.Review.Application.Features.GetProductRatingSummary;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Kart.Review.Api.Endpoints;

/// <summary>api-contract.yaml's `GET /v1/product-ratings/{sku}` (REV-7).</summary>
public static class ProductRatingEndpoints
{
    public static IEndpointRouteBuilder MapProductRatingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/product-ratings/{sku}", GetProductRatingSummary).WithTags("ProductRatings").AllowAnonymous();
        return app;
    }

    private static async Task<Ok<ProductRatingResponse>> GetProductRatingSummary(string sku, ISender sender, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new GetProductRatingSummaryQuery(sku), cancellationToken);
        return TypedResults.Ok(response);
    }
}
