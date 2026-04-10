// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        var destDir = repository.GetProjectFolderPath(projectId);
        var filePath = await camera.CapturePhotoAsync(project.LatestFrame?.FilePath, destDir, cancellationToken);
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
        {
            throw new InvalidOperationException("No frames to retake.");
        }

        // Overlay the second-last frame so the user can align against what came before.
        var overlayPath = project.Frames.Count >= 2 ? project.Frames[^2].FilePath : null;
        var destDir = repository.GetProjectFolderPath(projectId);
        var filePath = await camera.CapturePhotoAsync(overlayPath, destDir, cancellationToken);
        var frame = new TimelapseFrame(Guid.NewGuid(), project.LatestFrame.Index, DateTimeOffset.UtcNow, filePath);
        project.ReplaceLatestFrame(frame);
        await repository.SaveAsync(project, cancellationToken);
        return frame;
    }

    public async Task DeleteFrameAsync(Guid projectId, Guid frameId, CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        project.RemoveFrame(frameId);
        await repository.SaveAsync(project, cancellationToken);
    }

    public async Task MoveFrameAsync(Guid projectId, Guid frameId, int toPosition, CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        project.MoveFrame(frameId, toPosition);
        await repository.SaveAsync(project, cancellationToken);
    }

    public async Task RenameProjectAsync(Guid projectId, string newName, CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        project.Rename(newName);
        await repository.SaveAsync(project, cancellationToken);
    }
}
