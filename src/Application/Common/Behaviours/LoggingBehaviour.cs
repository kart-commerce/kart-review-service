using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Review.Application.Common.Behaviours;

/// <summary>
/// Information log with request type name + elapsed ms on completion — deliberately never logs
/// the request/response payload itself (some commands carry a review's free-text body). Mirrors
/// kart-identity-service's <c>LoggingBehaviour</c>. Exceptions are left to bubble to the single
/// global exception handler (<c>Kart.Shared.ErrorHandling</c>) for the one point of exception
/// logging (kart-conventions.md: "one log per exception, not zero, not two").
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        logger.LogInformation("Stage {Stage}: {RequestName} completed in {ElapsedMs}ms", "RequestHandled", requestName, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
