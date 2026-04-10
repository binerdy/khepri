// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.Messaging;

namespace Khepri;

/// <summary>
/// Message sent when a long-press gesture is detected on any item.
/// <see cref="Item"/> is the <see cref="BindableObject.BindingContext"/>
/// of the pressed view (e.g. a <c>ProjectDisplayItem</c> or <c>FrameDisplayItem</c>).
/// </summary>
public record LongPressMessage(object? Item = null);

/// <summary>
/// Attaches a native long-press listener to the host view and broadcasts
/// a <see cref="LongPressMessage"/> via <see cref="WeakReferenceMessenger"/>.
/// </summary>
public class LongPressBehavior : Behavior<View>
{
    protected override void OnAttachedTo(View view)
    {
        base.OnAttachedTo(view);
        view.HandlerChanged += OnHandlerChanged;
#if ANDROID
        AttachAndroid(view);
#endif
    }

    protected override void OnDetachingFrom(View view)
    {
        base.OnDetachingFrom(view);
        view.HandlerChanged -= OnHandlerChanged;
#if ANDROID
        DetachAndroid();
#endif
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        DetachAndroid();
        if (sender is View view && view.Handler is not null)
        {
            AttachAndroid(view);
        }
#endif
    }

#if ANDROID
    private View? _mauiView;
    private Android.Views.View? _nativeView;
    private CancellationTokenSource? _longPressCts;
    private const int LongPressDelayMs = 500;

    private void AttachAndroid(View view)
    {
        _mauiView = view;
        if (view.Handler?.PlatformView is Android.Views.View native)
        {
            _nativeView = native;
            _nativeView.Touch += OnNativeTouch;
        }
    }

    private void DetachAndroid()
    {
        _longPressCts?.Cancel();
        _longPressCts = null;
        _nativeView?.Touch -= OnNativeTouch;
        _nativeView = null;
        _mauiView = null;
    }

    private void OnNativeTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        switch (e.Event?.Action)
        {
            case Android.Views.MotionEventActions.Down:
                _longPressCts?.Cancel();
                var cts = _longPressCts = new CancellationTokenSource();
                var binding = _mauiView?.BindingContext;
                _ = FireAfterDelayAsync(binding, cts.Token);
                break;
            case Android.Views.MotionEventActions.Up:
            case Android.Views.MotionEventActions.Cancel:
                _longPressCts?.Cancel();
                break;
        }
        // Don't consume — TapGestureRecognizer must still receive events.
        e.Handled = false;
    }

    private static async Task FireAfterDelayAsync(object? binding, CancellationToken ct)
    {
        try
        {
            await Task.Delay(LongPressDelayMs, ct);
            MainThread.BeginInvokeOnMainThread(() =>
                WeakReferenceMessenger.Default.Send(new LongPressMessage(binding)));
        }
        catch (OperationCanceledException) { }
    }
#endif
}
