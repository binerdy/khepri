// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
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
            // Wait 1 s while splash image is visible
            await Task.Delay(1000);

            // Text fades in
            await Task.WhenAll(
                KhepriLabel.FadeToAsync(1, 430, Easing.CubicOut),
                KhepriLabel.ScaleToAsync(1.0, 430, Easing.CubicOut));

            // Hold briefly
            await Task.Delay(700);

            // Fade everything out together
            await KhepriLabel.FadeToAsync(0, 320);

            await Navigation.PopModalAsync(animated: false);
        }
        catch (TaskCanceledException) { }
    }
}
