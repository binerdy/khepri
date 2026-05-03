// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Android.Graphics;
using Android.Media;
using Khepri.Application.Timelapse;

namespace Khepri.Platforms.Android;

[SupportedOSPlatform("android21.0")]
public sealed class VideoExportService : IVideoExportService
{
    private const int Fps = 30;
    private const int BitRate = 4_000_000; // 4 Mbps
    private const int IFrameInterval = 2;
    private const int MaxWidth = 1280;
    // NV21 / YUV420SemiPlanar (value 21) — Android hardware encoders expect V before U
    private const int ColorFormatNv21 = 21;

    public Task<string> ExportAsync(
        IReadOnlyList<FrameRenderInfo> frames,
        double secondsPerFrame,
        TransitionEffect transition,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () => Encode(frames, secondsPerFrame, transition, progress, cancellationToken),
            cancellationToken);

    private static string Encode(
        IReadOnlyList<FrameRenderInfo> frames,
        double secondsPerFrame,
        TransitionEffect transition,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("No frames to export.");
        }

        // Determine output dimensions from the first frame, accounting for EXIF rotation.
        var sizeOpts = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(frames[0].FilePath, sizeOpts);
        // Rotations of 90 / 270 degrees swap the natural width and height.
        var firstOrientation = GetExifOrientation(frames[0].FilePath);
        var swapDims = firstOrientation is 5 or 6 or 7 or 8;
        var naturalW = swapDims ? sizeOpts.OutHeight : sizeOpts.OutWidth;
        var naturalH = swapDims ? sizeOpts.OutWidth : sizeOpts.OutHeight;
        var scale = Math.Min(1.0, (double)MaxWidth / naturalW);
        var width = (int)(naturalW * scale) & ~1;
        var height = (int)(naturalH * scale) & ~1;

        var outputPath = System.IO.Path.Combine(FileSystem.CacheDirectory, $"timelapse_{Guid.NewGuid():N}.mp4");

        var format = MediaFormat.CreateVideoFormat("video/avc", width, height);
        format.SetInteger(MediaFormat.KeyColorFormat, ColorFormatNv21);
        format.SetInteger(MediaFormat.KeyBitRate, BitRate);
        format.SetInteger(MediaFormat.KeyFrameRate, Fps);
        format.SetInteger(MediaFormat.KeyIFrameInterval, IFrameInterval);

        using var codec = MediaCodec.CreateEncoderByType("video/avc")!;
        codec.Configure(format, null, null, MediaCodecConfigFlags.Encode);
        codec.Start();

        using var muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
        var bufInfo = new MediaCodec.BufferInfo();
        var videoTrack = -1;
        var muxerStarted = false;
        var presentationUs = 0L;
        var frameDurationUs = (long)(1_000_000.0 / Fps);

        var yuvSize = width * height * 3 / 2;
        var yuv = new byte[yuvSize];
        var pixCur = new int[width * height];
        var pixNext = new int[width * height];
        var pixBlend = new int[width * height];

        var framesPerImage = Math.Max(1, (int)(secondsPerFrame * Fps));
        var fadeFrames = transition == TransitionEffect.Fade
            ? Math.Min(framesPerImage - 1, (int)(0.4 * secondsPerFrame * Fps))
            : 0;
        var holdFrames = framesPerImage - fadeFrames;
        var totalVideoFrames = (long)frames.Count * framesPerImage;
        long encodedCount = 0;

        // ── helpers ──────────────────────────────────────────────────────────

        void SubmitYuv(bool eos)
        {
            while (true)
            {
                var idx = codec.DequeueInputBuffer(10_000);
                if (idx < 0)
                {
                    continue;
                }

                var buf = codec.GetInputBuffer(idx)!;
                buf.Clear();
                if (eos)
                {
                    codec.QueueInputBuffer(idx, 0, 0, presentationUs, MediaCodecBufferFlags.EndOfStream);
                }
                else
                {
                    buf.Put(yuv);
                    codec.QueueInputBuffer(idx, 0, yuvSize, presentationUs, (MediaCodecBufferFlags)0);
                    presentationUs += frameDurationUs;

                    encodedCount++;
                    if (totalVideoFrames > 0)
                    {
                        progress?.Report((int)(encodedCount * 100L / totalVideoFrames));
                    }
                }

                return;
            }
        }

        void Drain(bool untilEos)
        {
            while (true)
            {
                var outIdx = codec.DequeueOutputBuffer(bufInfo, untilEos ? 100_000 : 0);
                if (outIdx == -1) // INFO_TRY_AGAIN_LATER
                {
                    if (!untilEos)
                    {
                        return;
                    }

                    continue;
                }

                if (outIdx == -2) // INFO_OUTPUT_FORMAT_CHANGED
                {
                    if (!muxerStarted)
                    {
                        videoTrack = muxer.AddTrack(codec.OutputFormat);
                        muxer.Start();
                        muxerStarted = true;
                    }

                    continue;
                }

                if (outIdx >= 0)
                {
                    var data = codec.GetOutputBuffer(outIdx)!;
                    if ((bufInfo.Flags & MediaCodecBufferFlags.CodecConfig) == 0
                        && bufInfo.Size > 0
                        && muxerStarted)
                    {
                        data.Position(bufInfo.Offset);
                        data.Limit(bufInfo.Offset + bufInfo.Size);
                        muxer.WriteSampleData(videoTrack, data, bufInfo);
                    }

                    var done = (bufInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
                    codec.ReleaseOutputBuffer(outIdx, false);
                    if (done)
                    {
                        return;
                    }
                }
            }
        }

        // ── main encode loop ──────────────────────────────────────────────────

        for (var i = 0; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var bmpCur = RenderFrameBitmap(frames[i], width, height);
            bmpCur.GetPixels(pixCur, 0, width, 0, 0, width, height);

            FillNv12(pixCur, yuv, width, height);
            for (var f = 0; f < holdFrames; f++)
            {
                SubmitYuv(false);
                Drain(false);
            }

            if (fadeFrames > 0 && i < frames.Count - 1)
            {
                using var bmpNext = RenderFrameBitmap(frames[i + 1], width, height);
                bmpNext.GetPixels(pixNext, 0, width, 0, 0, width, height);

                for (var f = 0; f < fadeFrames; f++)
                {
                    var alpha = (f + 1f) / (fadeFrames + 1f);
                    BlendPixels(pixCur, pixNext, alpha, pixBlend);
                    FillNv12(pixBlend, yuv, width, height);
                    SubmitYuv(false);
                    Drain(false);
                }
            }
        }

        // Signal end-of-stream and flush remaining encoded data.
        SubmitYuv(true);
        Drain(true);

        muxer.Stop();
        codec.Stop();

        progress?.Report(100);
        return outputPath;
    }

    // ── static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes <paramref name="frame"/>'s JPEG (EXIF-corrected), composites it AspectFit-centred
    /// on a <paramref name="videoWidth"/>×<paramref name="videoHeight"/> black canvas, then
    /// applies the stored alignment transforms (translate, rotate, scale) so the result
    /// exactly matches the playback screen.
    /// </summary>
    private static Bitmap RenderFrameBitmap(FrameRenderInfo frame, int videoWidth, int videoHeight)
    {
        var exifOri = GetExifOrientation(frame.FilePath);
        using var raw = BitmapFactory.DecodeFile(frame.FilePath)!;
        var oriented = ApplyExifOrientation(raw, exifOri);
        try
        {
            // AspectFit: scale image to fill video width without cropping.
            var fitScale = Math.Min((float)videoWidth / oriented.Width, (float)videoHeight / oriented.Height);
            var drawW = oriented.Width * fitScale;
            var drawH = oriented.Height * fitScale;

            // Convert the dp offset to video-pixel offset.
            // Both X and Y use the same factor because AspectFit is width-limited
            // for the typical portrait/landscape combinations encountered in this app.
            var refW = frame.ReferenceViewWidth > 0 ? frame.ReferenceViewWidth : 360.0;
            var pxOffsetX = (float)(frame.OffsetX * videoWidth / refW);
            var pxOffsetY = (float)(frame.OffsetY * videoWidth / refW);

            // Create output ARGB bitmap and draw via Canvas, replicating MAUI's
            // TranslationX/Y → Rotation → Scale transform chain (all around the
            // element centre, same as MAUI's default AnchorX/Y = 0.5).
            var output = Bitmap.CreateBitmap(videoWidth, videoHeight, Bitmap.Config.Argb8888!)!;
            using var canvas = new Canvas(output);
            canvas.DrawColor(global::Android.Graphics.Color.Black);

            canvas.Save();
            canvas.Translate(videoWidth / 2f + pxOffsetX, videoHeight / 2f + pxOffsetY);
            canvas.Rotate((float)frame.Rotation);
            canvas.Scale((float)frame.Scale, (float)frame.Scale);
            var dst = new global::Android.Graphics.RectF(-drawW / 2f, -drawH / 2f, drawW / 2f, drawH / 2f);
            canvas.DrawBitmap(oriented, null, dst, null);
            canvas.Restore();

            return output;
        }
        finally
        {
            if (!ReferenceEquals(oriented, raw))
            {
                oriented.Recycle();
            }
        }
    }

    /// <summary>Returns the EXIF Orientation tag value (1–8), defaulting to 1 (normal) on any error.</summary>
    private static int GetExifOrientation(string path)
    {
        try
        {
            var exif = new ExifInterface(path);
            return exif.GetAttributeInt(ExifInterface.TagOrientation, 1);
        }
        catch
        {
            return 1;
        }
    }

    /// <summary>
    /// Rotates/mirrors <paramref name="src"/> to match <paramref name="exifOrientation"/>.
    /// Returns <paramref name="src"/> unchanged (same reference) when no transformation is needed.
    /// </summary>
    private static Bitmap ApplyExifOrientation(Bitmap src, int exifOrientation)
    {
        using var matrix = new Matrix();
        switch (exifOrientation)
        {
            case 2: matrix.SetScale(-1f, 1f); break;                          // Flip horizontal
            case 3: matrix.SetRotate(180f); break;                            // Rotate 180
            case 4: matrix.SetScale(1f, -1f); break;                          // Flip vertical
            case 5: matrix.SetRotate(90f); matrix.PostScale(-1f, 1f); break;  // Transpose
            case 6: matrix.SetRotate(90f); break;                             // Rotate 90 CW
            case 7: matrix.SetRotate(-90f); matrix.PostScale(-1f, 1f); break; // Transverse
            case 8: matrix.SetRotate(-90f); break;                            // Rotate 270 CW
            default: return src;                                              // 0 or 1 = normal
        }
        return Bitmap.CreateBitmap(src, 0, 0, src.Width, src.Height, matrix, true)!;
    }

    private static void FillNv12(int[] pixels, byte[] yuv, int width, int height)
    {
        // Y plane
        for (var j = 0; j < height; j++)
        {
            for (var i = 0; i < width; i++)
            {
                var p = pixels[j * width + i];
                var r = (p >> 16) & 0xFF;
                var g = (p >> 8) & 0xFF;
                var b = p & 0xFF;
                yuv[j * width + i] = (byte)((77 * r + 150 * g + 29 * b) >> 8);
            }
        }

        // UV interleaved (NV21: V then U per 2×2 block — matches Android hardware encoder expectation)
        var uvBase = width * height;
        for (var j = 0; j < height; j += 2)
        {
            for (var i = 0; i < width; i += 2)
            {
                var p = pixels[j * width + i];
                var r = (p >> 16) & 0xFF;
                var g = (p >> 8) & 0xFF;
                var b = p & 0xFF;
                var idx = uvBase + (j / 2) * width + i;
                yuv[idx]     = (byte)Math.Clamp((128 * r - 107 * g - 21 * b) / 256 + 128, 0, 255); // Cr (V)
                yuv[idx + 1] = (byte)Math.Clamp((-43 * r - 85 * g + 128 * b) / 256 + 128, 0, 255); // Cb (U)
            }
        }
    }

    private static void BlendPixels(int[] a, int[] b, float alpha, int[] result)
    {
        var inv = 1f - alpha;
        for (var i = 0; i < a.Length; i++)
        {
            var r = (int)(((a[i] >> 16) & 0xFF) * inv + ((b[i] >> 16) & 0xFF) * alpha);
            var g = (int)(((a[i] >> 8) & 0xFF) * inv + ((b[i] >> 8) & 0xFF) * alpha);
            var bComp = (int)((a[i] & 0xFF) * inv + (b[i] & 0xFF) * alpha);
            result[i] = (0xFF << 24) | (r << 16) | (g << 8) | bComp;
        }
    }
}
