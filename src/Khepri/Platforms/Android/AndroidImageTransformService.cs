// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.Graphics;
using Android.Media;
using Khepri.Domain.Timelapse;
using AGColor = Android.Graphics.Color;
using AGMatrix = Android.Graphics.Matrix;
using AGPaint = Android.Graphics.Paint;
using IOPath = System.IO.Path;

namespace Khepri.Platforms.Android;

/// <summary>
/// Applies a pixel-level translation to an image using the Android Bitmap API.
/// The output is saved as a JPEG alongside the source file (suffix <c>_aligned</c>).
/// EXIF orientation is normalised before the transform so that the result is always
/// in display orientation.
/// </summary>
public sealed class AndroidImageTransformService : IImageTransformService
{
    public Task<string> ApplyTranslationAsync(
        string sourcePath,
        float translateX,
        float translateY,
        CancellationToken cancellationToken = default)
        => Task.Run(() => ApplyCore(sourcePath, translateX, translateY, cancellationToken), cancellationToken);

    private static string ApplyCore(string sourcePath, float translateX, float translateY, CancellationToken ct)
    {
        var src = DecodeAndOrient(sourcePath);
        ct.ThrowIfCancellationRequested();

        try
        {
            var matrix = new AGMatrix();
            matrix.PostTranslate(translateX, translateY);

            var outBmp = Bitmap.CreateBitmap(src.Width, src.Height, Bitmap.Config.Argb8888!)!;
            using var canvas = new Canvas(outBmp);
            canvas.DrawColor(AGColor.Black);
            using var paint = new AGPaint { FilterBitmap = true };
            canvas.DrawBitmap(src, matrix, paint);

            var dir = IOPath.GetDirectoryName(sourcePath)!;
            var stem = IOPath.GetFileNameWithoutExtension(sourcePath);
            var outPath = IOPath.Combine(dir, stem + "_aligned.jpg");

            using var stream = File.Create(outPath);
            outBmp.Compress(Bitmap.CompressFormat.Jpeg!, 92, stream);

            outBmp.Recycle();
            return outPath;
        }
        finally
        {
            src.Recycle();
        }
    }

    private static Bitmap DecodeAndOrient(string path)
    {
        var bmp = BitmapFactory.DecodeFile(path)
            ?? throw new InvalidOperationException($"Cannot decode image: {path}");

        var exif = new ExifInterface(path);
        var orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, 1);

        var degrees = orientation switch
        {
            6 => 90f,
            3 => 180f,
            8 => 270f,
            _ => 0f
        };

        if (degrees == 0f)
        {
            return bmp;
        }

        var rotMatrix = new AGMatrix();
        rotMatrix.PostRotate(degrees);
        var rotated = Bitmap.CreateBitmap(bmp, 0, 0, bmp.Width, bmp.Height, rotMatrix, true)!;
        bmp.Recycle();
        return rotated;
    }
}
