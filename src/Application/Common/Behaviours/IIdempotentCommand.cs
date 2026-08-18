using Kart.Review.Domain.Idempotency;

namespace Kart.Review.Application.Common.Behaviours;

/// <summary>
/// Opt-in marker for a MediatR command that must run through <see cref="IdempotencyBehaviour{TRequest,TResponse}"/>.
/// <c>SubmitReviewCommand</c>/<c>EditReviewCommand</c> implement this to declare their
/// <c>Idempotency-Key</c> header value and their endpoint scope; any other request type is
/// untouched by the behaviour (MediatR only invokes it for requests satisfying the
/// <c>where TRequest : IIdempotentCommand</c> constraint).
/// </summary>
public interface IIdempotentCommand
{
    string IdempotencyKey { get; }

    IdempotencyEndpoint Endpoint { get; }
}
