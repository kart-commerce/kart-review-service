namespace Kart.Review.Domain.Reviews;

/// <summary>Outcome of <see cref="Review.Edit"/> — small, tightly-coupled result enums kept in one file rather than one-file-per-trivial-enum (kart-conventions.md's DRY/no-over-engineering guidance).</summary>
public enum EditOutcome
{
    /// <summary>Edit applied directly (author edit on a PendingModeration/Rejected review re-classified, or a Published+cleared edit applied in place).</summary>
    Applied,

    /// <summary>Edit staged as a <see cref="PendingRevision"/> awaiting a moderator decision; the currently-Published content is unchanged.</summary>
    Staged,

    /// <summary>More than 30 days have elapsed since <c>CreatedAt</c> — the edit window is closed.</summary>
    WindowClosed,

    /// <summary>The review is <see cref="ModerationStatus.Retracted"/> — terminal, no edit is legal.</summary>
    Terminal,
}

/// <summary>Outcome of <see cref="Review.Retract"/>.</summary>
public enum RetractOutcome
{
    Applied,

    /// <summary>Already <see cref="ModerationStatus.Retracted"/> — a safe, guarded no-op (retraction is itself idempotent, no <c>Idempotency-Key</c> required).</summary>
    AlreadyRetracted,
}

/// <summary>Outcome of <see cref="Review.Moderate"/>.</summary>
public enum ModerateOutcome
{
    Applied,

    /// <summary>Already resolved (Rejected, or Accept on an already-Published review with no staged revision) — a guarded no-op, never re-applied.</summary>
    NoOp,

    /// <summary>The review is <see cref="ModerationStatus.Retracted"/> — terminal, no moderator action is legal.</summary>
    Terminal,
}
