// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;
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

		// Storage root — user-selected external folder that survives uninstall
#if ANDROID
		builder.Services.AddSingleton<IStorageRootService, Khepri.Platforms.Android.AndroidStorageRootService>();
#else
		builder.Services.AddSingleton<IStorageRootService, DefaultStorageRootService>();
#endif

		// Infrastructure
		builder.Services.AddSingleton<ITimelapseRepository, JsonTimelapseRepository>();
		builder.Services.AddSingleton<ICameraService, MauiCameraService>();
		builder.Services.AddSingleton<IFrameAlignmentService, MediaPipeFrameAlignmentService>();
#if ANDROID
		builder.Services.AddSingleton<IVideoExportService, Khepri.Platforms.Android.VideoExportService>();
#endif

		// Application
		builder.Services.AddSingleton<TimelapseService>();
		builder.Services.AddSingleton<AlignmentService>();

		// Presentation
		builder.Services.AddTransient<ProjectListViewModel>();
		builder.Services.AddTransient<ProjectDetailViewModel>();
		builder.Services.AddTransient<TimelapsePreviewViewModel>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ProjectDetailPage>();
		builder.Services.AddTransient<TimelapsePreviewPage>();
		builder.Services.AddTransient<CameraPage>();
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<StorageSetupPage>();
		builder.Services.AddTransient<SplashPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
