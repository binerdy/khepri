namespace Khepri.Domain.Timelapse;

/// <summary>
/// Persistence boundary for timelapse projects.
/// Implemented in Infrastructure; consumed by Application.
/// </summary>
public interface ITimelapseRepository
{
    Task<IReadOnlyList<TimelapseProject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TimelapseProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(TimelapseProject project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
