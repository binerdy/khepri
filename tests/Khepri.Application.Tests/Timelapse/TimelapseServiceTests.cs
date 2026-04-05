using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;
using NSubstitute;
using Shouldly;

namespace Khepri.Application.Tests.Timelapse;

public sealed class TimelapseServiceTests
{
    private readonly ITimelapseRepository _repository = Substitute.For<ITimelapseRepository>();
    private readonly ICameraService _camera = Substitute.For<ICameraService>();
    private readonly TimelapseService _sut;

    public TimelapseServiceTests()
    {
        _sut = new TimelapseService(_repository, _camera);
    }

    // ── CreateProject ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProject_SavesAndReturnsProject()
    {
        var project = await _sut.CreateProjectAsync("Face");

        project.Name.ShouldBe("Face");
        project.IsClone.ShouldBeFalse();
        await _repository.Received(1).SaveAsync(project, Arg.Any<CancellationToken>());
    }

    // ── CloneProject ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CloneProject_CreatesCloneWithCopiedFrames()
    {
        var source = new TimelapseProject(Guid.NewGuid(), "Original", DateTimeOffset.UtcNow);
        source.AddFrame(new TimelapseFrame(Guid.NewGuid(), 0, DateTimeOffset.UtcNow, "/f0.jpg"));

        _repository.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var clone = await _sut.CloneProjectAsync(source.Id, "Clone");

        clone.IsClone.ShouldBeTrue();
        clone.ClonedFromId.ShouldBe(source.Id);
        clone.Frames.Count.ShouldBe(1);
        clone.Frames[0].FilePath.ShouldBe("/f0.jpg");
        await _repository.Received(1).SaveAsync(clone, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloneProject_WhenSourceNotFound_Throws()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TimelapseProject?)null);

        var act = async () => await _sut.CloneProjectAsync(Guid.NewGuid(), "Clone");
        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    // ── CaptureFrame ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CaptureFrame_AddsFrameAndSavesProject()
    {
        var project = new TimelapseProject(Guid.NewGuid(), "Face", DateTimeOffset.UtcNow);
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _camera.CapturePhotoAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("/new-frame.jpg");

        var frame = await _sut.CaptureFrameAsync(project.Id);

        frame.FilePath.ShouldBe("/new-frame.jpg");
        project.Frames.Count.ShouldBe(1);
        await _repository.Received(1).SaveAsync(project, Arg.Any<CancellationToken>());
    }

    // ── RetakeLastFrame ───────────────────────────────────────────────────────

    [Fact]
    public async Task RetakeLastFrame_ReplacesLatestFrame()
    {
        var project = new TimelapseProject(Guid.NewGuid(), "Face", DateTimeOffset.UtcNow);
        project.AddFrame(new TimelapseFrame(Guid.NewGuid(), 0, DateTimeOffset.UtcNow, "/old.jpg"));
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _camera.CapturePhotoAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("/retake.jpg");

        var frame = await _sut.RetakeLastFrameAsync(project.Id);

        frame.FilePath.ShouldBe("/retake.jpg");
        project.Frames.Count.ShouldBe(1);
        project.LatestFrame!.FilePath.ShouldBe("/retake.jpg");
    }

    [Fact]
    public async Task RetakeLastFrame_WithNoFrames_Throws()
    {
        var project = new TimelapseProject(Guid.NewGuid(), "Face", DateTimeOffset.UtcNow);
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var act = async () => await _sut.RetakeLastFrameAsync(project.Id);
        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
