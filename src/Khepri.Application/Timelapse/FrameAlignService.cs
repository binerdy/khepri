// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri.Application.Timelapse;
/// <summary>
/// Persists a manual translation offset for a single timelapse frame.
/// The offset is stored in display units (dp) and applied at render time;
/// no JPEG re-encoding is performed.
/// </summary>
public sealed class FrameAlignService(ITimelapseRepository repository)
{
    /// <summary>
    /// Stores the transform offset (<paramref name="offsetX"/>, <paramref name="offsetY"/>, in dp),
    /// <paramref name="rotation"/> (degrees), and <paramref name="scale"/> on the frame and persists the project.
    /// </summary>
    public async Task SaveAlignmentAsync(
        Guid projectId,
        Guid frameId,
        double offsetX,
        double offsetY,
        double rotation = 0d,
        double scale = 1d,
        CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var frame = project.Frames.FirstOrDefault(f => f.Id == frameId)
            ?? throw new InvalidOperationException($"Frame {frameId} not found in project {projectId}.");

        frame.SetTransform(offsetX, offsetY, rotation, scale);
        await repository.SaveAsync(project, cancellationToken);
    }

    /// <summary>
    /// Resets ALL frame offsets in the project to zero and persists the result.
    /// The original image files are never modified.
    /// </summary>
    public async Task ResetAllAlignmentAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        foreach (var frame in project.Frames)
        {
            frame.SetTransform(0, 0, 0, 1);
        }

        await repository.SaveAsync(project, cancellationToken);
    }
}
