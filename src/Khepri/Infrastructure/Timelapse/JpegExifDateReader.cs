// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Extracts the capture date from a JPEG file by parsing the raw EXIF/TIFF data.
/// Reads <c>DateTimeOriginal</c> (tag 0x9003, in ExifIFD) with a fallback to
/// <c>DateTime</c> (tag 0x0132, in IFD0). Returns <see langword="null"/> for
/// non-JPEG files, files without EXIF, or any parsing error.
/// </summary>
internal sealed class JpegExifDateReader : IExifDateReader
{
    public DateTimeOffset? ReadDateTaken(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return ReadFromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // JPEG segment scanner
    // -------------------------------------------------------------------------

    private static DateTimeOffset? ReadFromStream(Stream stream)
    {
        // Must start with JPEG SOI marker FF D8
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
        {
            return null;
        }

        while (true)
        {
            int b;
            do { b = stream.ReadByte(); } while (b == 0xFF);
            if (b <= 0)
            {
                break;
            }

            byte marker = (byte)b;

            int hi = stream.ReadByte();
            int lo = stream.ReadByte();
            if (hi < 0 || lo < 0)
            {
                break;
            }

            int segLen = (hi << 8) | lo;
            int dataLen = segLen - 2;
            if (dataLen < 0)
            {
                break;
            }

            // APP1 (0xE1) may contain EXIF.
            if (marker == 0xE1 && dataLen >= 6)
            {
                var header = new byte[6];
                int read = stream.Read(header, 0, 6);
                if (read == 6
                    && header[0] == (byte)'E' && header[1] == (byte)'x'
                    && header[2] == (byte)'i' && header[3] == (byte)'f'
                    && header[4] == 0 && header[5] == 0)
                {
                    int tiffLen = dataLen - 6;
                    var tiff = new byte[tiffLen];
                    if (stream.Read(tiff, 0, tiffLen) == tiffLen)
                    {
                        return ParseTiffDate(tiff);
                    }

                    break;
                }

                // Not EXIF APP1 — skip remaining bytes.
                stream.Seek(dataLen - read, SeekOrigin.Current);
            }
            else
            {
                stream.Seek(dataLen, SeekOrigin.Current);
            }

            // Stop before the image scan data (SOS = 0xDA).
            if (marker == 0xDA)
            {
                break;
            }
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // TIFF / EXIF IFD parser
    // -------------------------------------------------------------------------

    private static DateTimeOffset? ParseTiffDate(byte[] tiff)
    {
        if (tiff.Length < 8)
        {
            return null;
        }

        bool le;
        if (tiff[0] == 'I' && tiff[1] == 'I')
        {
            le = true;
        }
        else if (tiff[0] == 'M' && tiff[1] == 'M')
        {
            le = false;
        }
        else
        {
            return null;
        }

        if (ReadUShort(tiff, 2, le) != 42)
        {
            return null;
        }

        int ifd0Offset = (int)ReadUInt(tiff, 4, le);

        // Scan IFD0 for DateTime (0x0132) and ExifIFD pointer (0x8769).
        int exifIfdOffset = -1;
        ScanIfd(tiff, ifd0Offset, le, 0x0132, out string? dateTime, 0x8769, out int exifPtr);
        if (exifPtr > 0)
        {
            exifIfdOffset = exifPtr;
        }

        // Scan ExifIFD for DateTimeOriginal (0x9003), which is preferred.
        string? dateTimeOriginal = null;
        if (exifIfdOffset > 0)
        {
            ScanIfd(tiff, exifIfdOffset, le, 0x9003, out dateTimeOriginal, -1, out _);
        }

        return ParseExifDateString(dateTimeOriginal ?? dateTime);
    }

    /// <summary>
    /// Scans a single IFD for up to two tags.
    /// <paramref name="asciiTag"/> is read as an ASCII string into <paramref name="asciiValue"/>.
    /// <paramref name="longTag"/> is read as a LONG (uint) offset into <paramref name="longValue"/>.
    /// Pass -1 to skip either tag.
    /// </summary>
    private static void ScanIfd(
        byte[] tiff, int ifdOffset, bool le,
        int asciiTag, out string? asciiValue,
        int longTag, out int longValue)
    {
        asciiValue = null;
        longValue = -1;

        if (ifdOffset < 0 || ifdOffset + 2 > tiff.Length)
        {
            return;
        }

        int count = ReadUShort(tiff, ifdOffset, le);
        for (int i = 0; i < count; i++)
        {
            int e = ifdOffset + 2 + i * 12;
            if (e + 12 > tiff.Length)
            {
                break;
            }

            int tag = ReadUShort(tiff, e, le);
            int type = ReadUShort(tiff, e + 2, le);

            if (tag == asciiTag && type == 2 /* ASCII */)
            {
                asciiValue = ReadAsciiEntry(tiff, e, le);
            }
            else if (tag == longTag && type == 4 /* LONG */)
            {
                longValue = (int)ReadUInt(tiff, e + 8, le);
            }
        }
    }

    private static string? ReadAsciiEntry(byte[] tiff, int entryOffset, bool le)
    {
        int count = (int)ReadUInt(tiff, entryOffset + 4, le);
        int valueOffset = count <= 4 ? entryOffset + 8 : (int)ReadUInt(tiff, entryOffset + 8, le);
        if (valueOffset < 0 || valueOffset + count > tiff.Length)
        {
            return null;
        }

        return Encoding.ASCII.GetString(tiff, valueOffset, count).TrimEnd('\0');
    }

    private static DateTimeOffset? ParseExifDateString(string? s)
    {
        // EXIF date format: "YYYY:MM:DD HH:MM:SS"
        if (s is null || s.Length < 19)
        {
            return null;
        }

        if (!DateTimeOffset.TryParseExact(
                s[..19],
                "yyyy:MM:dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var result))
        {
            return null;
        }

        // EXIF stores local time without a timezone offset — apply the device's local offset.
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(result.DateTime);
        return new DateTimeOffset(result.DateTime, localOffset);
    }

    // -------------------------------------------------------------------------
    // Byte-order-aware primitives
    // -------------------------------------------------------------------------

    private static int ReadUShort(byte[] data, int offset, bool le)
        => le
            ? data[offset] | (data[offset + 1] << 8)
            : (data[offset] << 8) | data[offset + 1];

    private static uint ReadUInt(byte[] data, int offset, bool le)
        => le
            ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24))
            : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
}
