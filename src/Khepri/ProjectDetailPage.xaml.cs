using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class ProjectDetailPage : ContentPage
{
    public ProjectDetailPage(ProjectDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
