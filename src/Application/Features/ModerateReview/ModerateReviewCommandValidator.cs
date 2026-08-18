using FluentValidation;

namespace Kart.Review.Application.Features.ModerateReview;

public sealed class ModerateReviewCommandValidator : AbstractValidator<ModerateReviewCommand>
{
    private static readonly string[] AllowedActions = ["accept", "reject"];

    public ModerateReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.Action).NotEmpty().Must(a => AllowedActions.Contains(a.ToLowerInvariant()))
            .WithMessage($"action must be one of: {string.Join(", ", AllowedActions)}");
    }
}
