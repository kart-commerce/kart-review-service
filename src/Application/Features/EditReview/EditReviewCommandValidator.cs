using FluentValidation;
using Kart.Review.Domain.Reviews;

namespace Kart.Review.Application.Features.EditReview;

public sealed class EditReviewCommandValidator : AbstractValidator<EditReviewCommand>
{
    public EditReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(Rating.MinValue, Rating.MaxValue).When(x => x.Rating.HasValue);

        // api-contract.yaml: minProperties: 1 — at least one field must actually be changing.
        RuleFor(x => x)
            .Must(x => x.Rating.HasValue || !string.IsNullOrWhiteSpace(x.BodyText))
            .WithMessage("At least one of rating or bodyText must be provided.");
    }
}
