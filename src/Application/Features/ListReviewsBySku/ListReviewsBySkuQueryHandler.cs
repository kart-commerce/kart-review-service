using Kart.Review.Application.Common.Interfaces;
using Kart.Review.Application.Common.Models;
using MediatR;

namespace Kart.Review.Application.Features.ListReviewsBySku;

public sealed class ListReviewsBySkuQueryHandler(IReviewReadModelRepository readModel) : IRequestHandler<ListReviewsBySkuQuery, ReviewListResponse>
{
    public async Task<ReviewListResponse> Handle(ListReviewsBySkuQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await readModel.SearchBySkuAsync(request.Sku, request.Page, request.PageSize, cancellationToken);
        return new ReviewListResponse(items, request.Page, request.PageSize, totalCount);
    }
}
