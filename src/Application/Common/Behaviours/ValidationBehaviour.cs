using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Common.Behaviours;

/// <summary>Runs every registered <see cref="IValidator{T}"/> for <typeparamref name="TRequest"/>, aggregating failures into one <see cref="ValidationException"/> — mapped to 400 by <c>Kart.Shared.ErrorHandling</c>. Mirrors kart-identity-service/kart-order-service's identically-shaped behaviour.</summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            logger.LogWarning("Stage {Stage}: {RequestName} rejected with {FailureCount} validation error(s)", "ValidationFailed", typeof(TRequest).Name, failures.Count);
            throw new ValidationException(failures);
        }

        return await next();
    }
}
