using Kart.Review.Domain.Reviews;

namespace Kart.Review.Domain.ProductRatings;

/// <summary>
/// The current weighted running average on a <see cref="ProductRating"/> — maintained by
/// incremental adjustment, never a full recompute (ddd-model.md's Modeling Decision #4 / #10;
/// design-decisions.md's Concurrency-Control decision, which explicitly rejects both a pessimistic
/// per-SKU lock and a full-recompute strategy in favor of this).
/// </summary>
public readonly record struct RatingAverage
{
    public double Value { get; }

    private RatingAverage(double value) => Value = value;

    public static RatingAverage Zero => new(0);

    public static RatingAverage From(double value) => new(value);

    /// <summary>Applied on <c>ReviewSubmitted</c>: a new rating joins the population. <paramref name="newCount"/> is the count AFTER incrementing.</summary>
    public RatingAverage AdjustForNewRating(Rating rating, RatingCount newCount) =>
        newCount.Value == 0 ? Zero : new RatingAverage(Value + (rating.Value - Value) / newCount.Value);

    /// <summary>Applied on <c>ReviewUpdated</c>: one existing rating's value changes; population size is unchanged.</summary>
    public RatingAverage AdjustForRatingChange(Rating oldRating, Rating newRating, RatingCount count) =>
        count.Value == 0 ? Zero : new RatingAverage(Value + (double)(newRating.Value - oldRating.Value) / count.Value);

    /// <summary>Applied on <c>ReviewUnpublished</c>: a previously-counted rating leaves the population. <paramref name="countBeforeRemoval"/> is the count BEFORE decrementing.</summary>
    public RatingAverage AdjustForRemoval(Rating rating, RatingCount countBeforeRemoval, RatingCount newCount) =>
        newCount.Value == 0 ? Zero : new RatingAverage((Value * countBeforeRemoval.Value - rating.Value) / newCount.Value);

    public override string ToString() => Value.ToString("F2");
}
