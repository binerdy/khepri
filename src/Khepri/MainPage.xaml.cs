// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Application.Timelapse;

namespace Khepri;

public partial class MainPage : ContentPage
{
    private enum PendingAction { None, Delete, Clone }

    private readonly ProjectListViewModel _vm;
    private PendingAction _pending;

    public MainPage(ProjectListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element { BindingContext: ProjectDisplayItem item })
        {
            return;
        }

        if (_vm.IsSelecting)
        {
            item.IsSelected = !item.IsSelected;
            _vm.SelectedCount = _vm.Projects.Count(p => p.IsSelected);
            return;
        }

        await Shell.Current.GoToAsync($"ProjectDetail?projectId={item.Project.Id}");
    }

    private async void OnNewProjectClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync(
            "New Project",
            "Enter a project name:",
            "Create",
            "Cancel",
            placeholder: "e.g. Sunrise June 2026");
        if (!string.IsNullOrWhiteSpace(name))
        {
            await _vm.CreateProjectCommand.ExecuteAsync(name);
        }
    }

    private void OnDeleteSelectedClicked(object? sender, EventArgs e)
    {
        _pending = PendingAction.Delete;
        _vm.EnterSelectModeCommand.Execute(null);
    }

    private void OnCloneSelectedClicked(object? sender, EventArgs e)
    {
        _pending = PendingAction.Clone;
        _vm.EnterSelectModeCommand.Execute(null);
    }

    private void OnExitSelectModeClicked(object? sender, EventArgs e)
    {
        _pending = PendingAction.None;
        _vm.ExitSelectModeCommand.Execute(null);
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        switch (_pending)
        {
            case PendingAction.Delete:
                await ConfirmDeleteAsync();
                break;
            case PendingAction.Clone:
                await ConfirmCloneAsync();
                break;
        }
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_vm.SelectedCount == 0)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            "Delete Projects",
            $"Delete {_vm.SelectedCount} project(s)? This cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        var ids = _vm.Projects
            .Where(p => p.IsSelected)
            .Select(p => p.Project.Id)
            .ToList();

        _pending = PendingAction.None;
        _vm.ExitSelectModeCommand.Execute(null);
        await _vm.DeleteSelectedProjectsAsync(ids);
    }

    private async Task ConfirmCloneAsync()
    {
        var source = _vm.Projects.FirstOrDefault(p => p.IsSelected);
        if (source is null)
        {
            return;
        }

        _pending = PendingAction.None;
        _vm.ExitSelectModeCommand.Execute(null);

        var newName = await DisplayPromptAsync(
            "Clone Project",
            $"Clone of \"{source.Project.Name}\":",
            "Clone",
            "Cancel",
            placeholder: source.Project.Name + " (clone)");

        if (!string.IsNullOrWhiteSpace(newName))
        {
            await _vm.CloneAsync(source.Project.Id, newName);
        }
    }
}
