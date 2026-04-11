// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using Khepri.Presentation.Timelapse;

namespace Khepri;

public sealed partial class FrameAlignPage : ContentPage
{
    private readonly FrameAlignViewModel _vm;

    // Precision panel state
    private bool _panelExpanded;

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
        _panelExpanded = false;
        PrecisionPanel.IsVisible = false;
    }

    // ── Filmstrip scroll ──────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrameAlignViewModel.CurrentIndex) && _vm.CurrentIndex >= 0)
        {
            var activeIdx = Math.Min(_vm.CurrentIndex + 1, _vm.FrameCount - 1);
            FilmstripView.ScrollTo(activeIdx, position: ScrollToPosition.Center, animate: true);
        }

        if (e.PropertyName == nameof(FrameAlignViewModel.BackgroundPath))
        {
            if (_panelExpanded)
            {
                RefreshPrecisionEntries();
            }
        }

        // Keep precision entries in sync while panel is open.
        if (_panelExpanded && e.PropertyName is
                nameof(FrameAlignViewModel.OffsetX) or
                nameof(FrameAlignViewModel.OffsetY) or
                nameof(FrameAlignViewModel.Rotation) or
                nameof(FrameAlignViewModel.Scale))
        {
            RefreshPrecisionEntries();
        }
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

    // ── Precision panel ───────────────────────────────────────────────────────

    /// <summary>
    /// Writes the current VM offset/rotation/scale values into the four entries.
    /// Skips any entry that is currently focused to avoid overwriting mid-edit.
    /// </summary>
    private void RefreshPrecisionEntries()
    {
        if (!OffsetXEntry.IsFocused)
        {
            OffsetXEntry.Text = _vm.OffsetX.ToString("F1", CultureInfo.InvariantCulture);
        }

        if (!OffsetYEntry.IsFocused)
        {
            OffsetYEntry.Text = _vm.OffsetY.ToString("F1", CultureInfo.InvariantCulture);
        }

        if (!RotationEntry.IsFocused)
        {
            RotationEntry.Text = _vm.Rotation.ToString("F2", CultureInfo.InvariantCulture);
        }

        if (!ScaleEntry.IsFocused)
        {
            ScaleEntry.Text = _vm.Scale.ToString("F3", CultureInfo.InvariantCulture);
        }
    }

    private void OnPrecisionToggleClicked(object? sender, EventArgs e)
    {
        _panelExpanded = !_panelExpanded;
        PrecisionPanel.IsVisible = _panelExpanded;
        PrecisionToggleButton.Text = _panelExpanded ? "▲ PRECISION" : "▼ PRECISION";
        if (_panelExpanded) { RefreshPrecisionEntries(); }
    }

    // ─ Step buttons ──────────────────────────────────────────────────────────

    private void OnOffsetXDec(object? s, EventArgs e) { _vm.OffsetX -= 1.0; OffsetXEntry.Text = _vm.OffsetX.ToString("F1", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }
    private void OnOffsetXInc(object? s, EventArgs e) { _vm.OffsetX += 1.0; OffsetXEntry.Text = _vm.OffsetX.ToString("F1", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }
    private void OnOffsetYDec(object? s, EventArgs e) { _vm.OffsetY -= 1.0; OffsetYEntry.Text = _vm.OffsetY.ToString("F1", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }
    private void OnOffsetYInc(object? s, EventArgs e) { _vm.OffsetY += 1.0; OffsetYEntry.Text = _vm.OffsetY.ToString("F1", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }
    private void OnRotationDec(object? s, EventArgs e) { _vm.Rotation -= 0.5; RotationEntry.Text = _vm.Rotation.ToString("F2", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }
    private void OnRotationInc(object? s, EventArgs e) { _vm.Rotation += 0.5; RotationEntry.Text = _vm.Rotation.ToString("F2", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }
    private void OnScaleDec(object? s, EventArgs e) { _vm.Scale = Math.Max(0.01, _vm.Scale - 0.01); ScaleEntry.Text = _vm.Scale.ToString("F3", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }
    private void OnScaleInc(object? s, EventArgs e) { _vm.Scale += 0.01; ScaleEntry.Text = _vm.Scale.ToString("F3", CultureInfo.InvariantCulture); _ = _vm.AutoSaveAsync(); }

    // ─ Direct entry input ────────────────────────────────────────────────────

    private void OnOffsetXCompleted(object? s, EventArgs e) => CommitOffsetX();
    private void OnOffsetXUnfocused(object? s, FocusEventArgs e) => CommitOffsetX();
    private void CommitOffsetX()
    {
        if (double.TryParse(OffsetXEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        { _vm.OffsetX = v; _ = _vm.AutoSaveAsync(); }
        OffsetXEntry.Text = _vm.OffsetX.ToString("F1", CultureInfo.InvariantCulture);
    }

    private void OnOffsetYCompleted(object? s, EventArgs e) => CommitOffsetY();
    private void OnOffsetYUnfocused(object? s, FocusEventArgs e) => CommitOffsetY();
    private void CommitOffsetY()
    {
        if (double.TryParse(OffsetYEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        { _vm.OffsetY = v; _ = _vm.AutoSaveAsync(); }
        OffsetYEntry.Text = _vm.OffsetY.ToString("F1", CultureInfo.InvariantCulture);
    }

    private void OnRotationCompleted(object? s, EventArgs e) => CommitRotation();
    private void OnRotationUnfocused(object? s, FocusEventArgs e) => CommitRotation();
    private void CommitRotation()
    {
        if (double.TryParse(RotationEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        { _vm.Rotation = v; _ = _vm.AutoSaveAsync(); }
        RotationEntry.Text = _vm.Rotation.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void OnScaleCompleted(object? s, EventArgs e) => CommitScale();
    private void OnScaleUnfocused(object? s, FocusEventArgs e) => CommitScale();
    private void CommitScale()
    {
        if (double.TryParse(ScaleEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        { _vm.Scale = Math.Max(0.01, v); _ = _vm.AutoSaveAsync(); }
        ScaleEntry.Text = _vm.Scale.ToString("F3", CultureInfo.InvariantCulture);
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

