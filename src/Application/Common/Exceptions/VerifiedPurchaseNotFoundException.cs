namespace Kart.Review.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml's exact wording for this rejection — a temporary, retry-later condition
/// (the eligibility gate's <see cref="Domain.VerifiedPurchases.VerifiedPurchaseRecord"/> hasn't
/// caught up with <c>OrderCreated</c>/<c>OrderDelivered</c> yet), never a permanent rejection of
/// the request as submitted.
/// </summary>
public sealed class VerifiedPurchaseNotFoundException() : Exception("no matching delivered order found yet, try again shortly");
