using Khepri.Domain.Timelapse;
using Shouldly;

namespace Khepri.Domain.Tests.Timelapse;

public sealed class TimelapseProjectTests
{
    private static TimelapseProject NewProject(string name = "Test") =>
        new(Guid.NewGuid(), name, DateTimeOffset.UtcNow);

    private static TimelapseFrame NewFrame(int index = 0) =>
        new(Guid.NewGuid(), index, DateTimeOffset.UtcNow, $"/frames/{index}.jpg");

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidName_CreatesProject()
    {
        var project = NewProject("My Timelapse");

        project.Name.ShouldBe("My Timelapse");
        project.Frames.ShouldBeEmpty();
        project.IsClone.ShouldBeFalse();
        project.LatestFrame.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        var act = () => NewProject(name);
        Should.Throw<ArgumentException>(act);
    }

    // ── AddFrame ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddFrame_AppendsFrameAndUpdatesLatest()
    {
        var project = NewProject();
        var frame = NewFrame(0);

        project.AddFrame(frame);

        project.Frames.Count.ShouldBe(1);
        project.LatestFrame.ShouldBe(frame);
    }

    [Fact]
    public void AddFrame_WithNullFrame_Throws()
    {
        var project = NewProject();
        var act = () => project.AddFrame(null!);
        Should.Throw<ArgumentNullException>(act);
    }

    // ── ReplaceLatestFrame ────────────────────────────────────────────────────

    [Fact]
    public void ReplaceLatestFrame_ReplacesLastFrameOnly()
    {
        var project = NewProject();
        var first = NewFrame(0);
        var original = NewFrame(1);
        var replacement = NewFrame(1);
        project.AddFrame(first);
        project.AddFrame(original);

        project.ReplaceLatestFrame(replacement);

        project.Frames.Count.ShouldBe(2);
        project.Frames[0].ShouldBe(first);
        project.LatestFrame.ShouldBe(replacement);
    }

    [Fact]
    public void ReplaceLatestFrame_WithNoFrames_Throws()
    {
        var project = NewProject();
        var act = () => project.ReplaceLatestFrame(NewFrame());
        Should.Throw<InvalidOperationException>(act);
    }

    // ── IsClone ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsClone_WhenClonedFromId_IsTrue()
    {
        var clone = new TimelapseProject(Guid.NewGuid(), "Clone", DateTimeOffset.UtcNow, Guid.NewGuid());
        clone.IsClone.ShouldBeTrue();
    }
}
