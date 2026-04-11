// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri;

public partial class FrameViewerPage : ContentPage
{
    private readonly IReadOnlyList<(string FilePath, string Label)> _frames;
    private int _index;

    public FrameViewerPage(IReadOnlyList<(string FilePath, string Label)> frames, int index)
    {
        InitializeComponent();
        _frames = frames;
        ShowFrame(index);
    }

    private void ShowFrame(int index)
    {
        _index = index;
        FrameLabel.Text = _frames[index].Label;
        FrameImage.Source = ImageSource.FromFile(_frames[index].FilePath);
    }

    private void OnImageTapped(object? sender, TappedEventArgs e)
    {
        var next = (_index + 1) % _frames.Count;
        ShowFrame(next);
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync(animated: false);
}
