using FluentValidation;
using Kart.Review.Domain.Reviews;

namespace Kart.Review.Application.Features.SubmitReview;

public sealed class SubmitReviewCommandValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Sku).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(Rating.MinValue, Rating.MaxValue);
        RuleFor(x => x.BodyText).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}
