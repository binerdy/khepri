// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.Runtime;
using Android.Views;
using Khepri.Presentation.Timelapse;
using AView = Android.Views.View;

namespace Khepri;

/// <summary>
/// Native Android touch listener that drives the alignment screen gestures:
///   • One finger  — pan (translate X / Y)
///   • Two fingers — pan centroid + pinch-to-zoom (Scale) + rotate (Rotation)
///
/// Auto-save is triggered when all fingers leave the screen.
/// </summary>
internal sealed class AlignmentTouchListener : Java.Lang.Object, AView.IOnTouchListener
{
    private readonly FrameAlignViewModel _vm;

    // ── One-finger pan ────────────────────────────────────────────────────────

    private float _panPrevX;
    private float _panPrevY;

    // ── Two-finger gesture baseline ───────────────────────────────────────────

    private bool _twoFingerActive;
    private float _baseInitMidX;
    private float _baseInitMidY;
    private float _baseInitDist;
    private float _baseInitAngle;  // radians
    private double _baseOffsetX;
    private double _baseOffsetY;
    private double _baseScale;
    private double _baseRotation;

    public AlignmentTouchListener(FrameAlignViewModel vm) => _vm = vm;

    [return: GeneratedEnum]
    public bool OnTouch(AView? v, MotionEvent? e)
    {
        if (e is null)
        {
            return false;
        }

        // Convert raw pixel deltas to density-independent pixels.
        var density = v?.Context?.Resources?.DisplayMetrics?.Density ?? 1f;

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _panPrevX = e.GetX(0);
                _panPrevY = e.GetY(0);
                _twoFingerActive = false;
                return true;

            case MotionEventActions.PointerDown when e.PointerCount == 2:
                {
                    _twoFingerActive = true;
                    float x0 = e.GetX(0), y0 = e.GetY(0);
                    float x1 = e.GetX(1), y1 = e.GetY(1);
                    _baseInitMidX = (x0 + x1) * 0.5f;
                    _baseInitMidY = (y0 + y1) * 0.5f;
                    _baseInitDist = Distance(x0, y0, x1, y1);
                    _baseInitAngle = AngleRad(x0, y0, x1, y1);
                    _baseOffsetX = _vm.OffsetX;
                    _baseOffsetY = _vm.OffsetY;
                    _baseScale = _vm.Scale;
                    _baseRotation = _vm.Rotation;
                    return true;
                }

            case MotionEventActions.Move:
                if (_twoFingerActive && e.PointerCount >= 2)
                {
                    float x0 = e.GetX(0), y0 = e.GetY(0);
                    float x1 = e.GetX(1), y1 = e.GetY(1);
                    var midX = (x0 + x1) * 0.5f;
                    var midY = (y0 + y1) * 0.5f;
                    var dist = Distance(x0, y0, x1, y1);
                    var angle = AngleRad(x0, y0, x1, y1);

                    _vm.Scale = _baseScale * (dist / _baseInitDist);
                    _vm.Rotation = _baseRotation + (angle - _baseInitAngle) * (180.0 / Math.PI);
                    _vm.OffsetX = ClampOffset(_baseOffsetX + (midX - _baseInitMidX) / density, v?.Width / density ?? 0);
                    _vm.OffsetY = ClampOffset(_baseOffsetY + (midY - _baseInitMidY) / density, v?.Height / density ?? 0);
                }
                else if (!_twoFingerActive && e.PointerCount == 1)
                {
                    var dx = e.GetX(0) - _panPrevX;
                    var dy = e.GetY(0) - _panPrevY;
                    _vm.OffsetX = ClampOffset(_vm.OffsetX + dx / density, v?.Width / density ?? 0);
                    _vm.OffsetY = ClampOffset(_vm.OffsetY + dy / density, v?.Height / density ?? 0);
                    _panPrevX = e.GetX(0);
                    _panPrevY = e.GetY(0);
                }
                return true;

            case MotionEventActions.PointerUp when e.PointerCount == 2:
                {
                    // One finger lifted from a two-finger touch.
                    // Switch back to one-finger pan from the remaining pointer.
                    _twoFingerActive = false;
                    var remaining = e.ActionIndex == 0 ? 1 : 0;
                    _panPrevX = e.GetX(remaining);
                    _panPrevY = e.GetY(remaining);
                    _vm.ReferenceViewWidth = v?.Width / density ?? 360.0;
                    _ = _vm.AutoSaveAsync();
                    return true;
                }

            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                _vm.ReferenceViewWidth = v?.Width / density ?? 360.0;
                _ = _vm.AutoSaveAsync();
                return true;
        }

        return false;
    }

    private static float Distance(float x1, float y1, float x2, float y2)
        => MathF.Sqrt(MathF.Pow(x2 - x1, 2) + MathF.Pow(y2 - y1, 2));

    private static float AngleRad(float x1, float y1, float x2, float y2)
        => MathF.Atan2(y2 - y1, x2 - x1);

    /// <summary>
    /// Clamps an offset so the image centre stays inside the viewbox.
    /// <paramref name="viewSizeDp"/> is the width (for X) or height (for Y) of the
    /// ViewerGrid in density-independent pixels.
    /// </summary>
    private static double ClampOffset(double offset, double viewSizeDp)
    {
        var limit = viewSizeDp * 0.5;
        return Math.Clamp(offset, -limit, limit);
    }
}
