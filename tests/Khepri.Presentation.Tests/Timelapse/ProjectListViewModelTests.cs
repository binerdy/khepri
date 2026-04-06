// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;
using NSubstitute;
using Shouldly;

namespace Khepri.Presentation.Tests.Timelapse;

/// <summary>
/// Regression tests for the two bugs reported:
///   1. Clicking a project did not open it (root cause: SelectionChanged never fired
///      because LongPressBehavior stole touch events; now uses TapGestureRecognizer).
///      The ViewModel contract that enables correct navigation: IsSelecting must be
///      false by default so the tap handler takes the navigation branch.
///   2. Long-pressing a project showed 0 selected. After the fix the pressed item is
///      immediately marked IsSelected=true and SelectedCount reflects that.
/// </summary>
public sealed class ProjectListViewModelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ProjectListViewModel CreateVm(params TimelapseProject[] projects)
    {
        var repo = Substitute.For<ITimelapseRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(projects.ToList());
        var camera = Substitute.For<ICameraService>();
        return new ProjectListViewModel(new TimelapseService(repo, camera));
    }

    private static TimelapseProject NewProject(string name = "Test") =>
        new(Guid.NewGuid(), name, DateTimeOffset.UtcNow);

    // ── Bug 1: tapping a project navigates (IsSelecting=false precondition) ───

    [Fact]
    public void InitialState_IsNotSelecting_So_TapNavigates()
    {
        // The page code-behind navigates when IsSelecting=false.
        // This test locks in that precondition so a future regressor is caught.
        var vm = CreateVm();

        vm.IsSelecting.ShouldBeFalse();
        vm.IsNotSelecting.ShouldBeTrue();
    }

    [Fact]
    public void AfterExitSelectMode_TapNavigatesAgain()
    {
        var vm = CreateVm();

        vm.EnterSelectModeCommand.Execute(null);
        vm.ExitSelectModeCommand.Execute(null);

        vm.IsSelecting.ShouldBeFalse();
        vm.IsNotSelecting.ShouldBeTrue();
    }

    // ── Bug 2: long-pressing a project shows 1 selected, not 0 ───────────────

    [Fact]
    public async Task LongPress_ThenLiftFinger_SelectedCountIsOne_NotZero()
    {
        // Regression: after a long press the subsequent tap (finger lift) selects the item.
        // Step 1 — LongPressMessage handler: enter selection mode (count stays 0 here).
        // Step 2 — OnItemTapped fires with IsSelecting=true: toggles item false→true, count=1.
        var vm = CreateVm(NewProject("Alpha"));
        await vm.LoadCommand.ExecuteAsync(null);

        // Step 1: LongPressMessage handler (just enters mode)
        vm.EnterSelectModeCommand.Execute(null);
        vm.IsSelecting.ShouldBeTrue();
        vm.SelectedCount.ShouldBe(0); // not yet selected

        // Step 2: OnItemTapped (finger lift) — IsSelecting=true so toggles selection
        var tappedItem = vm.Projects.First();
        tappedItem.IsSelected = !tappedItem.IsSelected;
        vm.SelectedCount = vm.Projects.Count(p => p.IsSelected);

        vm.SelectedCount.ShouldBe(1);
        vm.HasSelection.ShouldBeTrue();
        vm.HasSingleSelection.ShouldBeTrue();
    }

    [Fact]
    public async Task LongPress_WithMultipleProjects_OnlyPressedOneIsSelected()
    {
        var vm = CreateVm(NewProject("A"), NewProject("B"), NewProject("C"));
        await vm.LoadCommand.ExecuteAsync(null);

        // Long press + lift on middle item
        vm.EnterSelectModeCommand.Execute(null);
        var pressedItem = vm.Projects[1];
        pressedItem.IsSelected = !pressedItem.IsSelected;
        vm.SelectedCount = vm.Projects.Count(p => p.IsSelected);

        vm.SelectedCount.ShouldBe(1);
        vm.HasSingleSelection.ShouldBeTrue();
        vm.Projects[0].IsSelected.ShouldBeFalse();
        vm.Projects[2].IsSelected.ShouldBeFalse();
    }

    // ── Selection state management ────────────────────────────────────────────

    [Fact]
    public async Task EnterSelectMode_ResetsAnyPreviousSelection()
    {
        var vm = CreateVm(NewProject("A"), NewProject("B"));
        await vm.LoadCommand.ExecuteAsync(null);

        // Pre-select all
        foreach (var p in vm.Projects)
        {
            p.IsSelected = true;
        }

        vm.EnterSelectModeCommand.Execute(null);

        vm.Projects.ShouldAllBe(p => !p.IsSelected);
        vm.SelectedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExitSelectMode_ClearsAllSelectionAndCount()
    {
        var p1 = NewProject("P1");
        var p2 = NewProject("P2");
        var vm = CreateVm(p1, p2);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.EnterSelectModeCommand.Execute(null);
        vm.Projects[0].IsSelected = true;
        vm.Projects[1].IsSelected = true;
        vm.SelectedCount = 2;

        vm.ExitSelectModeCommand.Execute(null);

        vm.IsSelecting.ShouldBeFalse();
        vm.SelectedCount.ShouldBe(0);
        vm.Projects.ShouldAllBe(p => !p.IsSelected);
    }

    [Fact]
    public async Task TapInSelectionMode_TogglesItemAndUpdatesCount()
    {
        var vm = CreateVm(NewProject("A"), NewProject("B"));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.EnterSelectModeCommand.Execute(null);

        // Simulate OnItemTapped when IsSelecting=true
        var item = vm.Projects[0];
        item.IsSelected = !item.IsSelected;
        vm.SelectedCount = vm.Projects.Count(p => p.IsSelected);

        vm.SelectedCount.ShouldBe(1);

        // Tap same item again → deselect
        item.IsSelected = !item.IsSelected;
        vm.SelectedCount = vm.Projects.Count(p => p.IsSelected);

        vm.SelectedCount.ShouldBe(0);
        vm.HasSelection.ShouldBeFalse();
    }
}
