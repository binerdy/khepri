// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Khepri.Presentation.Timelapse;

namespace Khepri;

public sealed partial class FrameAlignPage : ContentPage
{
    private readonly FrameAlignViewModel _vm;

    public FrameAlignPage(FrameAlignViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.SaveCompleted += OnSaveCompleted;

        // Wire up the native Android multi-touch listener (pan + pinch + rotate).
        ViewerGrid.HandlerChanged += OnViewerGridHandlerChanged;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async void OnBackClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.ResetCommand.Execute(null);
    }

    // ── Filmstrip scroll ──────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FrameAlignViewModel.CurrentIndex) || _vm.CurrentIndex < 0)
        {
            return;
        }

        var activeIdx = Math.Min(_vm.CurrentIndex + 1, _vm.FrameCount - 1);
        FilmstripView.ScrollTo(activeIdx, position: ScrollToPosition.Center, animate: true);
    }

    // ── Saved indicator ───────────────────────────────────────────────────────

    private void OnSaveCompleted(object? sender, EventArgs e)
        => Dispatcher.Dispatch(() => _ = ShowSavedIndicatorAsync());

    private async Task ShowSavedIndicatorAsync()
    {
        SavedLabel.Opacity = 0;
        SavedLabel.IsVisible = true;
        await SavedLabel.FadeToAsync(1, 150);
        await Task.Delay(900);
        await SavedLabel.FadeToAsync(0, 350);
        SavedLabel.IsVisible = false;
    }

    // ── Android multi-touch ───────────────────────────────────────────────────

    private void OnViewerGridHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (ViewerGrid.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.Clickable = true;
            nativeView.SetOnTouchListener(new AlignmentTouchListener(_vm));
        }
#endif
    }
}

