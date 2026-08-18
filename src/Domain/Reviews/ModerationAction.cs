namespace Kart.Review.Domain.Reviews;

/// <summary>The moderator's decision on <c>PATCH /v1/reviews/{id}/moderate</c>.</summary>
public enum ModerationAction
{
    Accept,
    Reject,
}
