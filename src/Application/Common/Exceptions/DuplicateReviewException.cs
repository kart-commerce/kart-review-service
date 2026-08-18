namespace Kart.Review.Application.Common.Exceptions;

/// <summary>
/// The independent <c>(order_id, sku)</c> uniqueness invariant — a permanent rejection (unlike
/// <see cref="VerifiedPurchaseNotFoundException"/>): a Review already exists for this pair, the
/// client should use <c>PATCH /reviews/{id}</c> to revise instead of retrying this call.
/// </summary>
public sealed class DuplicateReviewException() : Exception("a review already exists for this order/sku pair");
