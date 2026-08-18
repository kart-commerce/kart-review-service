using FluentValidation;

namespace Kart.Review.Application.Features.ListReviewsBySku;

public sealed class ListReviewsBySkuQueryValidator : AbstractValidator<ListReviewsBySkuQuery>
{
    public ListReviewsBySkuQueryValidator()
    {
        RuleFor(x => x.Sku).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
