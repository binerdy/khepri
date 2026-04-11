// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri;

public partial class FrameViewerPage : ContentPage
{
    private readonly string _filePath;

    public FrameViewerPage(string filePath, string label)
    {
        InitializeComponent();
        _filePath = filePath;
        FrameLabel.Text = label;
        FrameImage.Source = ImageSource.FromFile(filePath);
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync(animated: false);

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        try
        {
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share Frame",
                File = new ShareFile(_filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Share Failed", ex.Message, "OK");
        }
    }
}
