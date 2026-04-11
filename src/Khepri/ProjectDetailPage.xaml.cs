// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Infrastructure;
using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class ProjectDetailPage : ContentPage
{
    private enum PendingAction { None, Delete, Share }

    private readonly ProjectDetailViewModel _vm;
    private readonly ISubscriptionService _subscription;
    private readonly SubscriptionPage _subscriptionPage;
    private FrameDisplayItem? _dragSource;
    private PendingAction _pending;

    public ProjectDetailPage(ProjectDetailViewModel vm,
                             ISubscriptionService subscription,
                             SubscriptionPage subscriptionPage)
    {
        InitializeComponent();
        _vm = vm;
        _subscription = subscription;
        _subscriptionPage = subscriptionPage;
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

    // Enters selection mode for sharing frames.
    private void OnShareFramesClicked(object? sender, EventArgs e)
    {
        _pending = PendingAction.Share;
        _vm.EnterSelectModeCommand.Execute(null);
    }

    // Enters selection mode for deleting frames.
    private void OnDeleteSelectedClicked(object? sender, EventArgs e)
    {
        _pending = PendingAction.Delete;
        _vm.EnterSelectModeCommand.Execute(null);
    }

    private void OnExitSelectModeClicked(object? sender, EventArgs e)
    {
        _pending = PendingAction.None;
        _vm.ExitSelectModeCommand.Execute(null);
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        switch (_pending)
        {
            case PendingAction.Delete:
                await ConfirmDeleteAsync();
                break;
            case PendingAction.Share:
                await ConfirmShareFramesAsync();
                break;
        }
    }

    private async Task ConfirmDeleteAsync()
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

        _pending = PendingAction.None;
        _vm.ExitSelectModeCommand.Execute(null);
        await _vm.DeleteSelectedFramesAsync(items);
    }

    private async Task ConfirmShareFramesAsync()
    {
        var items = _vm.DisplayFrames.Where(f => f.IsSelected).ToList();
        if (items.Count == 0)
        {
            return;
        }

        _pending = PendingAction.None;
        _vm.ExitSelectModeCommand.Execute(null);

        try
        {
            var files = items.Select(i => new ShareFile(i.FrameFilePath)).ToList();
            await Share.RequestAsync(new ShareMultipleFilesRequest
            {
                Title = items.Count == 1 ? "Share Frame" : $"Share {items.Count} Frames",
                Files = files
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Share Failed", ex.Message, "OK");
        }
    }

    private async void OnImportFromGalleryClicked(object? sender, EventArgs e)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select photos to import",
                FileTypes = FilePickerFileType.Images
            };
            var results = await FilePicker.PickMultipleAsync(options);
            if (results is null || !results.Any())
            {
                return;
            }

            var paths = results
                .OfType<FileResult>()
                .Select(r => r.FullPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            if (paths.Count > 0)
            {
                await _vm.ImportFromGalleryAsync(paths);
            }
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync("Not Supported", "File picking is not available on this device.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
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
