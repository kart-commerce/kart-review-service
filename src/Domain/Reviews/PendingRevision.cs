namespace Kart.Review.Domain.Reviews;

/// <summary>
/// The staged content of an edit submitted against an already-<see cref="ModerationStatus.Published"/>
/// review that the content-safety classifier flagged — held until a moderator decides, leaving the
/// currently-public content untouched (ddd-model.md). A second edit submitted while one revision
/// is already staged replaces it outright (latest-edit-wins, not a queue) — see
/// <see cref="Review.Edit"/>.
/// </summary>
public sealed record PendingRevision(string NewBodyText, Rating NewRating, DateTimeOffset SubmittedAt);
