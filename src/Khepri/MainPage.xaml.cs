// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Application.Timelapse;
using Khepri.Infrastructure;

namespace Khepri;

public partial class MainPage : ContentPage
{
    private enum PendingAction { None, Delete, Clone, Export }

    private readonly ProjectListViewModel _vm;
    private readonly IProjectImportService _import;
    private readonly IProjectExportService _export;
    private PendingAction _pending;

    public MainPage(ProjectListViewModel vm, IProjectImportService import, IProjectExportService export)
    {
        InitializeComponent();
        _vm = vm;
        _import = import;
        _export = export;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);

#if ANDROID
        // Handle import intents delivered before this page was ready
        MainActivity.ImportRequested += OnImportRequested;
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        MainActivity.ImportRequested -= OnImportRequested;
#endif
    }

#if ANDROID
    private async void OnImportRequested(Stream stream)
        => await RunImportAsync(stream);
#endif

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a .khepri file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, ["application/octet-stream", "*/*"] },
                    { DevicePlatform.iOS,     ["public.data"] },
                    { DevicePlatform.macOS,   ["public.data"] },
                    { DevicePlatform.WinUI,   [".khepri"] },
                })
            });

            if (result is null)
            {
                return;
            }

            await using var stream = await result.OpenReadAsync();
            await RunImportAsync(stream);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
    }

    private async Task RunImportAsync(Stream stream)
    {
        try
        {
            var names = await _import.ImportProjectsAsync(stream);
            if (names.Count == 0)
            {
                await DisplayAlertAsync("Import Failed",
                    "The file does not appear to contain valid Khepri projects.", "OK");
                return;
            }
            await _vm.LoadCommand.ExecuteAsync(null);
            var message = names.Count == 1
                ? $"\u201c{names[0]}\u201d was imported successfully."
                : $"{names.Count} projects imported successfully.";
            await DisplayAlertAsync("Imported", message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
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

    private void OnExportClicked(object? sender, EventArgs e)
    {
        _pending = PendingAction.Export;
        _vm.EnterSelectModeCommand.Execute(null);
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
            case PendingAction.Export:
                await ConfirmExportAsync();
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

    private async Task ConfirmExportAsync()
    {
        var selected = _vm.Projects.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _pending = PendingAction.None;
        _vm.ExitSelectModeCommand.Execute(null);

        try
        {
            var projects = selected
                .Select(p => (p.Project.Id, p.Project.Name))
                .ToList();
            await _export.ExportAsync(projects);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export Failed", ex.Message, "OK");
        }
    }
}
