namespace Khepri.Domain.Timelapse;

public sealed class TimelapseFrame
{
    public Guid Id { get; }
    public int Index { get; }
    public DateTimeOffset CapturedAt { get; }
    public string FilePath { get; }
    public string? AlignedFilePath { get; private set; }

    public TimelapseFrame(Guid id, int index, DateTimeOffset capturedAt, string filePath, string? alignedFilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), "Index must be zero or greater.");

        Id = id;
        Index = index;
        CapturedAt = capturedAt;
        FilePath = filePath;
        AlignedFilePath = alignedFilePath;
    }

    public string ActiveFilePath => AlignedFilePath ?? FilePath;

    public void SetAlignedFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AlignedFilePath = path;
    }
}
