using Khepri.Domain.Timelapse;
using Shouldly;

namespace Khepri.Domain.Tests.Timelapse;

public sealed class TimelapseFrameTests
{
    [Fact]
    public void Constructor_WithValidArgs_CreatesFrame()
    {
        var frame = new TimelapseFrame(Guid.NewGuid(), 0, DateTimeOffset.UtcNow, "/path/frame.jpg");

        frame.FilePath.ShouldBe("/path/frame.jpg");
        frame.AlignedFilePath.ShouldBeNull();
        frame.ActiveFilePath.ShouldBe("/path/frame.jpg");
    }

    [Fact]
    public void ActiveFilePath_WhenAligned_ReturnsAlignedPath()
    {
        var frame = new TimelapseFrame(Guid.NewGuid(), 0, DateTimeOffset.UtcNow, "/original.jpg");
        frame.SetAlignedFilePath("/aligned.jpg");

        frame.ActiveFilePath.ShouldBe("/aligned.jpg");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithBlankFilePath_Throws(string path)
    {
        var act = () => new TimelapseFrame(Guid.NewGuid(), 0, DateTimeOffset.UtcNow, path);
        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_WithNegativeIndex_Throws()
    {
        var act = () => new TimelapseFrame(Guid.NewGuid(), -1, DateTimeOffset.UtcNow, "/path.jpg");
        Should.Throw<ArgumentOutOfRangeException>(act);
    }
}
