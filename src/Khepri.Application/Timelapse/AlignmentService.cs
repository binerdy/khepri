using Khepri.Domain.Timelapse;

namespace Khepri.Application.Timelapse;

public sealed class AlignmentService(ITimelapseRepository repository, IFrameAlignmentService aligner)
{
    /// <summary>
    /// Aligns all frames in a clone project using the first frame as the reference.
    /// Reports progress as (framesProcessed, total).
    /// </summary>
    public async Task AlignProjectAsync(
        Guid projectId,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        if (!project.IsClone)
            throw new InvalidOperationException("Alignment can only be run on cloned projects.");

        var frames = project.Frames;
        if (frames.Count < 2) return;

        var referenceFilePath = frames[0].FilePath;

        for (var i = 1; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var alignedPath = await aligner.AlignAsync(referenceFilePath, frames[i].FilePath, cancellationToken);
            frames[i].SetAlignedFilePath(alignedPath);
            progress?.Report((i, frames.Count - 1));
        }

        await repository.SaveAsync(project, cancellationToken);
    }
}
