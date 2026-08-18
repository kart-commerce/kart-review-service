namespace Kart.Review.Domain.Reviews;

/// <summary>
/// The two-stage-moderation lifecycle state of a <see cref="Review"/>. <see cref="Rejected"/> and
/// <see cref="Retracted"/> are both terminal with respect to further moderator action — no
/// transition exists between them, and no moderator action can move a review out of either
/// (ddd-model.md's Monotonic terminal states invariant). The <em>author's own edit path</em> is a
/// narrow, documented exception: editing a <see cref="Rejected"/> review (which was never public)
/// is explicitly legal within the edit window and re-runs the classifier exactly like a fresh
/// submission — only <see cref="Retracted"/> blocks the edit endpoint outright.
/// </summary>
public enum ModerationStatus
{
    PendingModeration,
    Published,
    Rejected,
    Retracted,
}
