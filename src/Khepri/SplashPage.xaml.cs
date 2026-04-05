// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri;

public partial class SplashPage : ContentPage
{
    private readonly AppShell            _shell;
    private readonly IStorageRootService _storageRoot;
    private readonly StorageSetupPage    _setupPage;

    public SplashPage(AppShell shell, IStorageRootService storageRoot, StorageSetupPage setupPage)
    {
        _shell       = shell;
        _storageRoot = storageRoot;
        _setupPage   = setupPage;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunIntroAsync();
    }

    private async Task RunIntroAsync()
    {
        try
        {
            // Lines expand from centre while text waits
            var linesIn = Task.WhenAll(
                TopLine.ScaleToAsync(1, 550, Easing.CubicOut),
                BottomLine.ScaleToAsync(1, 550, Easing.CubicOut));

            await Task.Delay(160);

            // Text rises in slightly behind the lines
            var textIn = Task.WhenAll(
                KhepriLabel.FadeToAsync(1, 430, Easing.CubicOut),
                KhepriLabel.ScaleToAsync(1.0, 430, Easing.CubicOut));

            await Task.WhenAll(linesIn, textIn);

            // Hold on the composed mark
            await Task.Delay(900);

            // Fade everything out together
            await Task.WhenAll(
                KhepriLabel.FadeToAsync(0, 320),
                TopLine.FadeToAsync(0, 320),
                BottomLine.FadeToAsync(0, 320));

            // Hand off to the shell (or storage setup on first launch)
            if (Microsoft.Maui.Controls.Application.Current?.Windows is [{ } window, ..])
            {
                window.Page = _storageRoot.HasRootFolder ? _shell : _setupPage;
            }
        }
        catch (TaskCanceledException) { }
    }
}
