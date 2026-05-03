// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

public sealed class TimelapseFrame
{
    public Guid Id { get; }
    public int Index { get; private set; }
    public DateTimeOffset CapturedAt { get; }
    public string FilePath { get; }
    public string? AlignedFilePath { get; private set; }
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public double Rotation { get; private set; }
    public double Scale { get; private set; } = 1d;
    /// <summary>
    /// Width of the alignment viewer in dp at the time this frame's offset was saved.
    /// Used to convert dp offsets to video pixel offsets during export.
    /// Zero means unknown (old data) — the export falls back to a sensible default.
    /// </summary>
    public double ReferenceViewWidth { get; private set; }

    public TimelapseFrame(Guid id, int index, DateTimeOffset capturedAt, string filePath, string? alignedFilePath = null, double offsetX = 0d, double offsetY = 0d, double rotation = 0d, double scale = 1d, double referenceViewWidth = 0d)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be zero or greater.");
        }

        Id = id;
        Index = index;
        CapturedAt = capturedAt;
        FilePath = filePath;
        AlignedFilePath = alignedFilePath;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Rotation = rotation;
        Scale = scale;
        ReferenceViewWidth = referenceViewWidth;
    }

    public string ActiveFilePath => AlignedFilePath ?? FilePath;

    public void SetAlignedFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AlignedFilePath = path;
    }

    public void SetTransform(double x, double y, double rotation = 0d, double scale = 1d, double referenceViewWidth = 0d)
    {
        OffsetX = x;
        OffsetY = y;
        Rotation = rotation;
        Scale = scale;
        if (referenceViewWidth > 0)
        {
            ReferenceViewWidth = referenceViewWidth;
        }
    }

    internal void Reindex(int newIndex) => Index = newIndex;
}
