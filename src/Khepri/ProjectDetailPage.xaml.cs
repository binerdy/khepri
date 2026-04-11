// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Infrastructure;
using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class ProjectDetailPage : ContentPage
{
    private readonly ProjectDetailViewModel _vm;
    private readonly ISubscriptionService _subscription;
    private readonly SubscriptionPage _subscriptionPage;
    private readonly IProjectExportService _export;
    private FrameDisplayItem? _dragSource;

    public ProjectDetailPage(ProjectDetailViewModel vm,
                             ISubscriptionService subscription,
                             SubscriptionPage subscriptionPage,
                             IProjectExportService export)
    {
        InitializeComponent();
        _vm = vm;
        _subscription = subscription;
        _subscriptionPage = subscriptionPage;
        _export = export;
        BindingContext = vm;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var current = _vm.Project?.Name ?? string.Empty;
        var newName = await DisplayPromptAsync(
            "Rename Project",
            "Enter a new name:",
            "RENAME",
            "CANCEL",
            initialValue: current);

        if (!string.IsNullOrWhiteSpace(newName) && newName != current)
        {
            await _vm.RenameCommand.ExecuteAsync(newName);
        }
    }

    private async void OnPlayClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync($"TimelapsePreview?projectId={_vm.CurrentProjectId}");

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (_vm.CurrentProjectId == Guid.Empty || _vm.Project is null)
        {
            return;
        }
        try
        {
            await _export.ExportAsync([(_vm.CurrentProjectId, _vm.Project.Name)]);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export Failed", ex.Message, "OK");
        }
    }

    private async void OnAlignClicked(object? sender, EventArgs e)
    {
        var subscribed = await _subscription.IsSubscribedAsync();
        if (!subscribed)
        {
            await Navigation.PushModalAsync(_subscriptionPage, animated: false);
            subscribed = await _subscriptionPage.WaitForSubscriptionAsync();
        }
        if (subscribed)
        {
            await Shell.Current.GoToAsync($"FrameAlign?projectId={_vm.CurrentProjectId}");
        }
    }

    private void OnExitSelectModeClicked(object? sender, EventArgs e)
        => _vm.ExitSelectModeCommand.Execute(null);

    private async void OnDeleteSelectedClicked(object? sender, EventArgs e)
    {
        _vm.EnterSelectModeCommand.Execute(null);
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        var items = _vm.DisplayFrames.Where(f => f.IsSelected).ToList();
        var count = items.Count;
        if (count == 0)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            $"Delete {count} Frame{(count == 1 ? "" : "s")}",
            "This cannot be undone.",
            "DELETE",
            "CANCEL");

        if (!confirmed)
        {
            return;
        }

        OnExitSelectModeClicked(null, EventArgs.Empty);
        await _vm.DeleteSelectedFramesAsync(items);
    }

    private async void OnFrameTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.IsSelecting)
        {
            if (sender is Element { BindingContext: FrameDisplayItem item })
            {
                item.IsSelected = !item.IsSelected;
                _vm.SelectedCount = _vm.DisplayFrames.Count(f => f.IsSelected);
            }
            return;
        }

        // Not in selection mode — open full-screen viewer.
        if (sender is Element { BindingContext: FrameDisplayItem frame })
        {
            var viewer = new FrameViewerPage(frame.FrameFilePath, frame.Label);
            await Navigation.PushModalAsync(viewer, animated: false);
        }
    }

    private void OnFrameDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (_vm.IsSelecting)
        {
            e.Cancel = true;
            return;
        }

        if (sender is Element { BindingContext: FrameDisplayItem item })
        {
            _dragSource = item;
        }
    }

    protected async void OnFrameDrop(object? sender, DropEventArgs e)
    {
        if (_dragSource is null)
        {
            return;
        }

        if (sender is Element { BindingContext: FrameDisplayItem target } && target != _dragSource)
        {
            await _vm.MoveFrameAsync(_dragSource.Frame, target.Frame);
        }

        _dragSource = null;
    }
}
