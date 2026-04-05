// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

public sealed class TimelapseProject
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public Guid? ClonedFromId { get; }
    public IReadOnlyList<TimelapseFrame> Frames => _frames.AsReadOnly();

    private readonly List<TimelapseFrame> _frames;

    public TimelapseProject(Guid id, string name, DateTimeOffset createdAt, Guid? clonedFromId = null, IEnumerable<TimelapseFrame>? frames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        CreatedAt = createdAt;
        ClonedFromId = clonedFromId;
        _frames = frames?.OrderBy(f => f.Index).ToList() ?? [];
    }

    public bool IsClone => ClonedFromId.HasValue;

    public TimelapseFrame? LatestFrame => _frames.Count > 0
        ? _frames[^1]
        : null;

    public void AddFrame(TimelapseFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frames.Add(frame);
    }

    public void ReplaceLatestFrame(TimelapseFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (_frames.Count == 0)
        {
            throw new InvalidOperationException("No frames to replace.");
        }

        _frames[^1] = frame;
    }

    public void Rename(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        Name = newName;
    }
}
