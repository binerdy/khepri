// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;

namespace Khepri.Presentation.Timelapse;

public sealed partial class ProjectListViewModel(TimelapseService timelapseService) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredProjects))]
    public partial IReadOnlyList<TimelapseProject> Projects { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredProjects))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSelecting))]
    public partial bool IsSelecting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial int SelectedCount { get; set; }

    public bool IsNotSelecting => !IsSelecting;
    public bool HasSelection   => SelectedCount > 0;
    public string AppVersion   => AppInfo.VersionString;

    public IEnumerable<TimelapseProject> FilteredProjects =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Projects
            : Projects.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private void EnterSelectMode()
    {
        IsSelecting = true;
        SelectedCount = 0;
    }

    [RelayCommand]
    private void ExitSelectMode()
    {
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
            Projects = await timelapseService.GetAllProjectsAsync(cancellationToken);
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

    [RelayCommand]
    private async Task CloneProjectAsync((Guid SourceId, string NewName) args, CancellationToken cancellationToken)
    {
        await timelapseService.CloneProjectAsync(args.SourceId, args.NewName, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await timelapseService.DeleteProjectAsync(projectId, cancellationToken);
        await LoadAsync(cancellationToken);
    }
}
