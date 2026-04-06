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

    public TimelapseFrame(Guid id, int index, DateTimeOffset capturedAt, string filePath, string? alignedFilePath = null, double offsetX = 0d, double offsetY = 0d)
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
    }

    public string ActiveFilePath => AlignedFilePath ?? FilePath;

    public void SetAlignedFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AlignedFilePath = path;
    }

    public void SetOffset(double x, double y)
    {
        OffsetX = x;
        OffsetY = y;
    }

    internal void Reindex(int newIndex) => Index = newIndex;
}
