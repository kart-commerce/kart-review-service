using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Application.Common.Models;
using Kart.Review.Domain.Common.ValueObjects;
using MediatR;

namespace Kart.Review.Application.Features.GetProductRatingSummary;

public sealed class GetProductRatingSummaryQueryHandler(IProductRatingRepository productRatings) : IRequestHandler<GetProductRatingSummaryQuery, ProductRatingResponse>
{
    public async Task<ProductRatingResponse> Handle(GetProductRatingSummaryQuery request, CancellationToken cancellationToken)
    {
        var sku = Sku.From(request.Sku);
        var rating = await productRatings.GetBySkuAsync(sku, cancellationToken) ?? throw new ProductRatingNotFoundException(request.Sku);
        return ProductRatingResponse.FromDomain(rating);
    }
}
