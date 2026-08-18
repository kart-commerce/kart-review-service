namespace Kart.Review.Application.Common.Exceptions;

/// <summary>More than 30 days have elapsed since the review's <c>CreatedAt</c> — the edit window is closed (ddd-model.md invariant #4).</summary>
public sealed class EditWindowClosedException() : Exception("the 30-day edit window for this review has closed");
