namespace Kart.Review.Application.Common.Interfaces;

/// <summary>The single commit point every repository call below shares — the EF <c>DbContext</c> itself is the Unit of Work (PLATFORM_BLUEPRINT.md's Data Access standard), never exposed to Application directly.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
