// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;
using Khepri.Infrastructure;
using Khepri.Infrastructure.Timelapse;
using Khepri.Presentation.Timelapse;
using Microsoft.Extensions.Logging;
namespace Khepri;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitCamera()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        builder.Services.AddSingleton<IStorageRootService, Platforms.Android.AndroidStorageRootService>();
#if DEBUG
        builder.Services.AddSingleton<ISubscriptionService, StubSubscriptionService>();
#else
        builder.Services.AddSingleton<ISubscriptionService, Platforms.Android.BillingSubscriptionService>();
#endif
        builder.Services.AddSingleton<IProjectExportService, Platforms.Android.AndroidProjectExportService>();
#else
        builder.Services.AddSingleton<IStorageRootService, DefaultStorageRootService>();
        builder.Services.AddSingleton<ISubscriptionService, StubSubscriptionService>();
        builder.Services.AddSingleton<IProjectExportService, DefaultProjectExportService>();
#endif
        builder.Services.AddSingleton<IProjectImportService, ProjectImportService>();
        // Infrastructure
        builder.Services.AddSingleton<JsonTimelapseRepository>();
        builder.Services.AddSingleton<ITimelapseRepository>(sp => sp.GetRequiredService<JsonTimelapseRepository>());
        builder.Services.AddSingleton<ICameraService, MauiCameraService>();
#if ANDROID
        builder.Services.AddSingleton<IImageTransformService, Platforms.Android.AndroidImageTransformService>();
        builder.Services.AddSingleton<IVideoExportService, Platforms.Android.VideoExportService>();
#else
        builder.Services.AddSingleton<IImageTransformService, StubImageTransformService>();
#endif

        // Application
        builder.Services.AddSingleton<TimelapseService>();
        builder.Services.AddSingleton<FrameAlignService>();

        // Presentation
        builder.Services.AddTransient<ProjectListViewModel>();
        builder.Services.AddTransient<ProjectDetailViewModel>();
        builder.Services.AddTransient<TimelapsePreviewViewModel>();
        builder.Services.AddTransient<FrameAlignViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ProjectDetailPage>();
        builder.Services.AddTransient<TimelapsePreviewPage>();
        builder.Services.AddTransient<FrameAlignPage>();
        builder.Services.AddTransient<CameraPage>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<SubscriptionPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
