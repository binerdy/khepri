using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;

namespace Khepri.Presentation.Timelapse;

public sealed partial class ProjectListViewModel(TimelapseService timelapseService) : ObservableObject
{
    [ObservableProperty]
    public partial IReadOnlyList<TimelapseProject> Projects { get; set; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

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
