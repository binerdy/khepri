// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;
using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class MainPage : ContentPage
{
    private readonly ProjectListViewModel _vm;

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

    private async void OnProjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TimelapseProject project)
        {
            ProjectsList.SelectedItem = null;
            await Shell.Current.GoToAsync($"ProjectDetail?projectId={project.Id}");
        }
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
}
