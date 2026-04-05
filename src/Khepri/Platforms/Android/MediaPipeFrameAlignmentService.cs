// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.Graphics;
using Android.Media;
using Khepri.Domain.Timelapse;
using AGMatrix = Android.Graphics.Matrix;
using AGPaint = Android.Graphics.Paint;
using AGPointF = Android.Graphics.PointF;
using IOPath = System.IO.Path;

namespace Khepri.Platforms.Android;

/// <summary>
/// Facial-landmark-based frame alignment using Android's on-device FaceDetector.
///
/// Algorithm (similarity transform):
///   1. Detect the inter-ocular midpoint and eye distance in both the reference
///      and the source frame.
///   2. Build a 2-D similarity transform (translate → uniform scale → translate)
///      that maps the source eye midpoint onto the reference eye midpoint while
///      matching both eye spans.
///   3. Render the warped source frame on a canvas the same size as the reference
///      and save the result as a JPEG alongside the original source file.
///
/// If no face is found in either image the original source path is returned
/// unchanged so the frame is still shown in playback without modification.
/// </summary>
public sealed class MediaPipeFrameAlignmentService : IFrameAlignmentService
{
    public Task<string> AlignAsync(
        string referenceFilePath,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
        => Task.Run(() => AlignCore(referenceFilePath, sourceFilePath, cancellationToken), cancellationToken);

    // ── Core (runs on a thread-pool thread) ──────────────────────────────────

    private static string AlignCore(string referencePath, string sourcePath, CancellationToken ct)
    {
        var refBmp = BitmapFactory.DecodeFile(referencePath)
            ?? throw new InvalidOperationException($"Cannot decode reference image: {referencePath}");

        var srcBmp = BitmapFactory.DecodeFile(sourcePath)
            ?? throw new InvalidOperationException($"Cannot decode source image: {sourcePath}");

        ct.ThrowIfCancellationRequested();

        try
        {
            bool refOk = TryDetectEyes(refBmp, out var refMid, out var refDist);
            bool srcOk = TryDetectEyes(srcBmp, out var srcMid, out var srcDist);

            // No face detected — return the original path; AlignmentService will
            // set AlignedFilePath = FilePath, so ActiveFilePath still shows the frame.
            if (!refOk || !srcOk)
                return sourcePath;

            ct.ThrowIfCancellationRequested();

            // Similarity transform:
            //   • translate source so its eye midpoint sits at the world origin
            //   • scale uniformly so the eye span matches the reference
            //   • translate the origin to the reference eye midpoint
            float scale  = refDist / srcDist;
            var   matrix = new AGMatrix();
            matrix.PostTranslate(-srcMid.X, -srcMid.Y);
            matrix.PostScale(scale, scale);
            matrix.PostTranslate(refMid.X, refMid.Y);

            // Render onto a canvas the same pixel dimensions as the reference.
            var outBmp = Bitmap.CreateBitmap(refBmp.Width, refBmp.Height, Bitmap.Config.Argb8888!)!;
            using var canvas = new Canvas(outBmp);
            using var paint  = new AGPaint { FilterBitmap = true };
            canvas.DrawBitmap(srcBmp, matrix, paint);

            var dir     = IOPath.GetDirectoryName(sourcePath)!;
            var stem    = IOPath.GetFileNameWithoutExtension(sourcePath);
            var outPath = IOPath.Combine(dir, stem + "_aligned.jpg");

            using var stream = System.IO.File.Create(outPath);
            outBmp.Compress(Bitmap.CompressFormat.Jpeg!, 92, stream);

            outBmp.Recycle();
            return outPath;
        }
        finally
        {
            refBmp.Recycle();
            srcBmp.Recycle();
        }
    }

    // ── Face detection ───────────────────────────────────────────────────────

    private static bool TryDetectEyes(Bitmap bitmap, out AGPointF midPoint, out float eyeDistance)
    {
        midPoint    = new AGPointF();
        eyeDistance = 1f;

        // FaceDetector requires a mutable RGB_565 bitmap.
        var rgb565 = bitmap.Copy(Bitmap.Config.Rgb565!, false);
        if (rgb565 is null)
            return false;

        try
        {
            var detector = new FaceDetector(rgb565.Width, rgb565.Height, maxFaces: 1);
            var faces    = new FaceDetector.Face[1];
            int count    = detector.FindFaces(rgb565, faces);

            if (count < 1 || faces[0] is null)
                return false;

            var face = faces[0]!;
            face.GetMidPoint(midPoint);
            eyeDistance = face.EyesDistance();
            return eyeDistance > 0f;
        }
        finally
        {
            rgb565.Recycle();
        }
    }
}
