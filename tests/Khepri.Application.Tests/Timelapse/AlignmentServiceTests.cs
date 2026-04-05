using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;
using NSubstitute;
using Shouldly;

namespace Khepri.Application.Tests.Timelapse;

public sealed class AlignmentServiceTests
{
    private readonly ITimelapseRepository _repository = Substitute.For<ITimelapseRepository>();
    private readonly IFrameAlignmentService _aligner = Substitute.For<IFrameAlignmentService>();
    private readonly AlignmentService _sut;

    public AlignmentServiceTests()
    {
        _sut = new AlignmentService(_repository, _aligner);
    }

    [Fact]
    public async Task AlignProject_SetsAlignedFilePathOnEachFrame()
    {
        var project = new TimelapseProject(Guid.NewGuid(), "Clone", DateTimeOffset.UtcNow, Guid.NewGuid());
        project.AddFrame(new TimelapseFrame(Guid.NewGuid(), 0, DateTimeOffset.UtcNow, "/f0.jpg"));
        project.AddFrame(new TimelapseFrame(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, "/f1.jpg"));
        project.AddFrame(new TimelapseFrame(Guid.NewGuid(), 2, DateTimeOffset.UtcNow, "/f2.jpg"));

        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _aligner.AlignAsync("/f0.jpg", "/f1.jpg", Arg.Any<CancellationToken>()).Returns("/f1-aligned.jpg");
        _aligner.AlignAsync("/f0.jpg", "/f2.jpg", Arg.Any<CancellationToken>()).Returns("/f2-aligned.jpg");

        await _sut.AlignProjectAsync(project.Id);

        project.Frames[0].AlignedFilePath.ShouldBeNull();
        project.Frames[1].ActiveFilePath.ShouldBe("/f1-aligned.jpg");
        project.Frames[2].ActiveFilePath.ShouldBe("/f2-aligned.jpg");
        await _repository.Received(1).SaveAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlignProject_OnNonClone_Throws()
    {
        var project = new TimelapseProject(Guid.NewGuid(), "Original", DateTimeOffset.UtcNow);
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var act = async () => await _sut.AlignProjectAsync(project.Id);
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("clone");
    }

    [Fact]
    public async Task AlignProject_ReportsProgress()
    {
        var project = new TimelapseProject(Guid.NewGuid(), "Clone", DateTimeOffset.UtcNow, Guid.NewGuid());
        project.AddFrame(new TimelapseFrame(Guid.NewGuid(), 0, DateTimeOffset.UtcNow, "/f0.jpg"));
        project.AddFrame(new TimelapseFrame(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, "/f1.jpg"));
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _aligner.AlignAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("/aligned.jpg");

        var reports = new List<(int, int)>();
        var progress = new Progress<(int, int)>(r => reports.Add(r));

        await _sut.AlignProjectAsync(project.Id, progress);

        // Give the Progress<T> callbacks time to fire (they are posted to the sync context)
        await Task.Delay(10);
        reports.Count.ShouldBe(1);
        reports[0].ShouldBe((1, 1));
    }
}
