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
    private const int Fps         = 30;
    private const int BitRate     = 4_000_000; // 4 Mbps
    private const int IFrameInterval = 2;
    private const int MaxWidth    = 1280;
    // NV12 / YUV420SemiPlanar — universally supported on Android 21+
    private const int ColorFormatNv12 = 21;

    public Task<string> ExportAsync(
        IReadOnlyList<string> framePaths,
        double secondsPerFrame,
        TransitionEffect transition,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () => Encode(framePaths, secondsPerFrame, transition, progress, cancellationToken),
            cancellationToken);

    private static string Encode(
        IReadOnlyList<string> framePaths,
        double secondsPerFrame,
        TransitionEffect transition,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (framePaths.Count == 0)
        {
            throw new InvalidOperationException("No frames to export.");
        }

        // Determine output dimensions from the first frame (scale to max width, even numbers).
        var sizeOpts = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(framePaths[0], sizeOpts);
        var scale  = Math.Min(1.0, (double)MaxWidth / sizeOpts.OutWidth);
        var width  = (int)(sizeOpts.OutWidth  * scale) & ~1;
        var height = (int)(sizeOpts.OutHeight * scale) & ~1;

        var outputPath = System.IO.Path.Combine(FileSystem.CacheDirectory, $"timelapse_{Guid.NewGuid():N}.mp4");

        var format = MediaFormat.CreateVideoFormat("video/avc", width, height);
        format.SetInteger(MediaFormat.KeyColorFormat, ColorFormatNv12);
        format.SetInteger(MediaFormat.KeyBitRate, BitRate);
        format.SetInteger(MediaFormat.KeyFrameRate, Fps);
        format.SetInteger(MediaFormat.KeyIFrameInterval, IFrameInterval);

        using var codec  = MediaCodec.CreateEncoderByType("video/avc")!;
        codec.Configure(format, null, null, MediaCodecConfigFlags.Encode);
        codec.Start();

        using var muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
        var bufInfo = new MediaCodec.BufferInfo();
        var videoTrack = -1;
        var muxerStarted = false;
        long presentationUs = 0L;
        var frameDurationUs = (long)(1_000_000.0 / Fps);

        var yuvSize  = width * height * 3 / 2;
        var yuv      = new byte[yuvSize];
        var pixCur   = new int[width * height];
        var pixNext  = new int[width * height];
        var pixBlend = new int[width * height];

        var framesPerImage  = Math.Max(1, (int)(secondsPerFrame * Fps));
        var fadeFrames      = transition == TransitionEffect.Fade
            ? Math.Min(framesPerImage - 1, (int)(0.4 * secondsPerFrame * Fps))
            : 0;
        var holdFrames      = framesPerImage - fadeFrames;
        var totalVideoFrames = (long)framePaths.Count * framesPerImage;
        long encodedCount    = 0;

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

        for (var i = 0; i < framePaths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var bmpCur = DecodeBitmap(framePaths[i], width, height);
            bmpCur.GetPixels(pixCur, 0, width, 0, 0, width, height);

            FillNv12(pixCur, yuv, width, height);
            for (var f = 0; f < holdFrames; f++)
            {
                SubmitYuv(false);
                Drain(false);
            }

            if (fadeFrames > 0 && i < framePaths.Count - 1)
            {
                using var bmpNext = DecodeBitmap(framePaths[i + 1], width, height);
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

    private static Bitmap DecodeBitmap(string path, int width, int height)
    {
        using var src = BitmapFactory.DecodeFile(path)!;
        if (src.Width == width && src.Height == height)
        {
            return src.Copy(Bitmap.Config.Argb8888!, false)!;
        }

        return Bitmap.CreateScaledBitmap(src, width, height, true)!;
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

        // UV interleaved (NV12: U then V per 2×2 block)
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
                yuv[idx]     = (byte)Math.Clamp((-43 * r -  85 * g + 128 * b) / 256 + 128, 0, 255);
                yuv[idx + 1] = (byte)Math.Clamp((128 * r - 107 * g -  21 * b) / 256 + 128, 0, 255);
            }
        }
    }

    private static void BlendPixels(int[] a, int[] b, float alpha, int[] result)
    {
        var inv = 1f - alpha;
        for (var i = 0; i < a.Length; i++)
        {
            var r     = (int)(((a[i] >> 16) & 0xFF) * inv + ((b[i] >> 16) & 0xFF) * alpha);
            var g     = (int)(((a[i] >>  8) & 0xFF) * inv + ((b[i] >>  8) & 0xFF) * alpha);
            var bComp = (int)((a[i]         & 0xFF) * inv +  (b[i]        & 0xFF) * alpha);
            result[i] = (0xFF << 24) | (r << 16) | (g << 8) | bComp;
        }
    }
}
