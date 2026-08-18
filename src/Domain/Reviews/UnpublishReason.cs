namespace Kart.Review.Domain.Reviews;

/// <summary>Why a previously-public <see cref="Review"/> stopped being public — carried on <c>ReviewUnpublished</c>'s payload.</summary>
public enum UnpublishReason
{
    AuthorRetraction,
    ModeratorTakedown,
}
