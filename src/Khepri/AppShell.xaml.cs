// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri;

public partial class AppShell : Shell
{
    private readonly SplashPage _splash;
    private bool _splashShown;

    public AppShell(SplashPage splash)
    {
        _splash = splash;
        InitializeComponent();
        Routing.RegisterRoute("ProjectDetail", typeof(ProjectDetailPage));
        Routing.RegisterRoute("TimelapsePreview", typeof(TimelapsePreviewPage));
        Routing.RegisterRoute("FrameAlign", typeof(FrameAlignPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_splashShown)
        {
            _splashShown = true;
            await Navigation.PushModalAsync(_splash, animated: false);
        }
    }
}
