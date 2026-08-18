using Kart.Review.Domain.Common.ValueObjects;

namespace Kart.Review.Application.Common.Interfaces;

/// <summary>The authenticated caller, resolved from the JWT bearer token by Infrastructure (never taken from a request body — requirement-spec: "userId is the authenticated caller's identity").</summary>
public interface ICurrentPrincipal
{
    UserId UserId { get; }

    /// <summary>The raw string form stamped on <c>CreatedBy</c>/<c>UpdatedBy</c> and audit log entries.</summary>
    string PrincipalId { get; }

    bool IsInRole(string role);
}
