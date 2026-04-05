namespace Khepri;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("ProjectDetail", typeof(ProjectDetailPage));
    }
}
