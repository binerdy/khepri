// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Khepri.Application.Timelapse;

public sealed partial class ProjectListViewModel(TimelapseService timelapseService) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredProjects))]
    public partial IReadOnlyList<ProjectDisplayItem> Projects { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredProjects))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSelecting))]
    [NotifyPropertyChangedFor(nameof(IsNotSelectingOrHasSingleSelection))]
    public partial bool IsSelecting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(HasSingleSelection))]
    [NotifyPropertyChangedFor(nameof(IsNotSelectingOrHasSingleSelection))]
    public partial int SelectedCount { get; set; }

    public bool IsNotSelecting => !IsSelecting;
    public bool HasSelection => SelectedCount > 0;
    public bool HasSingleSelection => SelectedCount == 1;
    // Clone button: always tappable when not selecting; disabled while selecting with ≠1 item
    public bool IsNotSelectingOrHasSingleSelection => !IsSelecting || SelectedCount == 1;

    public IEnumerable<ProjectDisplayItem> FilteredProjects =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Projects
            : Projects.Where(p => p.Project.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private void EnterSelectMode()
    {
        foreach (var item in Projects)
        {
            item.IsSelected = false;
        }

        IsSelecting = true;
        SelectedCount = 0;
    }

    [RelayCommand]
    private void ExitSelectMode()
    {
        foreach (var item in Projects)
        {
            item.IsSelected = false;
        }

        IsSelecting = false;
        SelectedCount = 0;
    }

    public async Task DeleteSelectedProjectsAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            foreach (var id in projectIds)
            {
                await timelapseService.DeleteProjectAsync(id, cancellationToken);
            }

            await LoadAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var projects = await timelapseService.GetAllProjectsAsync(cancellationToken);
            Projects = projects.Select(p => new ProjectDisplayItem(p)).ToList();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateProjectAsync(string name, CancellationToken cancellationToken)
    {
        await timelapseService.CreateProjectAsync(name, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task CloneAsync(Guid sourceId, string newName, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            await timelapseService.CloneProjectAsync(sourceId, newName, cancellationToken);
            await LoadAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
