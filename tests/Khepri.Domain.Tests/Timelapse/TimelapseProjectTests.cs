// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    // ── RemoveFrame ───────────────────────────────────────────────────────────

    [Fact]
    public void RemoveFrame_RemovesFrameAndReindexes()
    {
        var project = NewProject();
        var f0 = NewFrame(0);
        var f1 = NewFrame(1);
        var f2 = NewFrame(2);
        project.AddFrame(f0);
        project.AddFrame(f1);
        project.AddFrame(f2);

        project.RemoveFrame(f1.Id);

        project.Frames.Count.ShouldBe(2);
        project.Frames[0].ShouldBe(f0);
        project.Frames[1].ShouldBe(f2);
        project.Frames[0].Index.ShouldBe(0);
        project.Frames[1].Index.ShouldBe(1);
    }

    [Fact]
    public void RemoveFrame_WithUnknownId_DoesNothing()
    {
        var project = NewProject();
        project.AddFrame(NewFrame(0));

        project.RemoveFrame(Guid.NewGuid());

        project.Frames.Count.ShouldBe(1);
    }

    // ── MoveFrame ─────────────────────────────────────────────────────────────

    [Fact]
    public void MoveFrame_ReordersFramesAndReindexes()
    {
        var project = NewProject();
        var f0 = NewFrame(0);
        var f1 = NewFrame(1);
        var f2 = NewFrame(2);
        project.AddFrame(f0);
        project.AddFrame(f1);
        project.AddFrame(f2);

        project.MoveFrame(f0.Id, 2);

        project.Frames[0].ShouldBe(f1);
        project.Frames[1].ShouldBe(f2);
        project.Frames[2].ShouldBe(f0);
        project.Frames[0].Index.ShouldBe(0);
        project.Frames[1].Index.ShouldBe(1);
        project.Frames[2].Index.ShouldBe(2);
    }

    [Fact]
    public void MoveFrame_WithUnknownId_DoesNothing()
    {
        var project = NewProject();
        var f0 = NewFrame(0);
        project.AddFrame(f0);

        project.MoveFrame(Guid.NewGuid(), 0);

        project.Frames[0].ShouldBe(f0);
    }

    [Fact]
    public void MoveFrame_ToSamePosition_DoesNothing()
    {
        var project = NewProject();
        var f0 = NewFrame(0);
        var f1 = NewFrame(1);
        project.AddFrame(f0);
        project.AddFrame(f1);

        project.MoveFrame(f0.Id, 0);

        project.Frames[0].ShouldBe(f0);
    }

    // ── Rename ────────────────────────────────────────────────────────────────

    [Fact]
    public void Rename_UpdatesName()
    {
        var project = NewProject("OldName");

        project.Rename("NewName");

        project.Name.ShouldBe("NewName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithBlankName_Throws(string name)
    {
        var project = NewProject();

        var act = () => project.Rename(name);
        Should.Throw<ArgumentException>(act);
    }

    // ── IsClone ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsClone_WhenClonedFromId_IsTrue()
    {
        var clone = new TimelapseProject(Guid.NewGuid(), "Clone", DateTimeOffset.UtcNow, Guid.NewGuid());
        clone.IsClone.ShouldBeTrue();
    }
}
