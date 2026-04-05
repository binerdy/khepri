using Khepri.Domain.Timelapse;

namespace Khepri.Application.Timelapse;

public sealed class TimelapseService(ITimelapseRepository repository, ICameraService camera)
{
    public Task<IReadOnlyList<TimelapseProject>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
        => repository.GetAllAsync(cancellationToken);

    public async Task<TimelapseProject> CreateProjectAsync(string name, CancellationToken cancellationToken = default)
    {
        var project = new TimelapseProject(Guid.NewGuid(), name, DateTimeOffset.UtcNow);
        await repository.SaveAsync(project, cancellationToken);
        return project;
    }

    public async Task<TimelapseProject> CloneProjectAsync(Guid sourceId, string newName, CancellationToken cancellationToken = default)
    {
        var source = await repository.GetByIdAsync(sourceId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {sourceId} not found.");

        var clonedFrames = source.Frames.Select(f =>
            new TimelapseFrame(Guid.NewGuid(), f.Index, f.CapturedAt, f.FilePath, f.AlignedFilePath));

        var clone = new TimelapseProject(Guid.NewGuid(), newName, DateTimeOffset.UtcNow, source.Id, clonedFrames);
        await repository.SaveAsync(clone, cancellationToken);
        return clone;
    }

    public async Task DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default)
        => await repository.DeleteAsync(id, cancellationToken);

    public async Task<TimelapseFrame> CaptureFrameAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var filePath = await camera.CapturePhotoAsync(project.LatestFrame?.FilePath, cancellationToken);
        var frame = new TimelapseFrame(Guid.NewGuid(), project.Frames.Count, DateTimeOffset.UtcNow, filePath);
        project.AddFrame(frame);
        await repository.SaveAsync(project, cancellationToken);
        return frame;
    }

    public async Task<TimelapseFrame> RetakeLastFrameAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        if (project.LatestFrame is null)
            throw new InvalidOperationException("No frames to retake.");

        var filePath = await camera.CapturePhotoAsync(project.LatestFrame.FilePath, cancellationToken);
        var frame = new TimelapseFrame(Guid.NewGuid(), project.LatestFrame.Index, DateTimeOffset.UtcNow, filePath);
        project.ReplaceLatestFrame(frame);
        await repository.SaveAsync(project, cancellationToken);
        return frame;
    }
}
