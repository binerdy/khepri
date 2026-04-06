// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Khepri.Presentation.Timelapse;

namespace Khepri;

public sealed partial class FrameAlignPage : ContentPage
{
    private readonly FrameAlignViewModel _vm;

    // Accumulated offset at the start of each pan gesture.
    private double _panBaseX;
    private double _panBaseY;

    public FrameAlignPage(FrameAlignViewModel vm)
    {
        InitializeComponent();
        _vm         = vm;
        BindingContext = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
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

    // ── Pan gesture ───────────────────────────────────────────────────────────

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panBaseX = _vm.OffsetX;
                _panBaseY = _vm.OffsetY;
                break;

            case GestureStatus.Running:
                _vm.OffsetX = _panBaseX + e.TotalX;
                _vm.OffsetY = _panBaseY + e.TotalY;
                break;
        }
    }
}
