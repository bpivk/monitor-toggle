using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RigToggle.IconGen;

/// <summary>
/// Hand-rolled multi-frame .ico writer. System.Drawing.Icon has no public API for
/// writing a multi-size .ico file — only for reading one — so this assembles the
/// ICONDIR/ICONDIRENTRY binary layout directly (13-RESEARCH.md Pattern 3).
///
/// Byte-layout sources: Wikipedia "ICO (file format)" (ICONDIR/ICONDIRENTRY layout,
/// the 0=256 width/height convention) cross-referenced against community write-ups
/// (Edi Wang, csharphelper.com) for the BMP-in-ICO bottom-up row / padded AND-mask
/// technique. See 13-RESEARCH.md Pitfall 3 (256px byte=0) and Pitfall 4 (bottom-up
/// BGRA rows, 32-bit-padded 1bpp AND mask, doubled biHeight) for the specific
/// failure modes this implementation guards against.
///
/// Pitfall 5 (discovered during Task 3 execution, not in 13-RESEARCH.md's original
/// Pitfall 3/4): frames at 256x256 must be encoded as an embedded PNG blob, not raw
/// BMP-in-ICO. Pitfall 3's "0 byte means 256" ICONDIRENTRY encoding is necessary but
/// not sufficient — readers (confirmed via System.Drawing.Icon's own sized
/// constructor) do not reliably recognize a raw-BMP 256px frame and silently fall
/// back to a smaller embedded frame instead. Since Vista, 256px ICO frames are
/// conventionally PNG-compressed; every other size keeps using the proven BMP-in-ICO
/// path unchanged.
/// </summary>
internal static class IconWriter
{
    private const int PngFrameThreshold = 256;

    /// <summary>
    /// Packs the given frames into a standards-compliant multi-frame .ico byte stream.
    /// Frames may be supplied in any order — offsets are computed from each frame's
    /// own encoded size, not from list position.
    /// </summary>
    public static void WriteIco(Stream output, IReadOnlyList<Bitmap> frames)
    {
        using var bw = new BinaryWriter(output);
        int count = frames.Count;

        // ICONDIR (6 bytes)
        bw.Write((ushort)0);   // reserved
        bw.Write((ushort)1);   // type = icon
        bw.Write((ushort)count);

        var entryDataOffsets = new int[count];
        var entryDataSizes = new int[count];
        var frameBytes = new byte[count][];

        // Pre-encode every frame's bytes so we know sizes before writing the
        // directory (offsets must be known up front). Pitfall 5: 256px frames use
        // PNG encoding; every smaller frame keeps the proven BMP-in-ICO path.
        for (int i = 0; i < count; i++)
        {
            var bmp = frames[i];
            frameBytes[i] = bmp.Width >= PngFrameThreshold || bmp.Height >= PngFrameThreshold
                ? EncodePngInIco(bmp)
                : EncodeBmpInIco(bmp);
        }

        int headerSize = 6 + 16 * count;
        int runningOffset = headerSize;
        for (int i = 0; i < count; i++)
        {
            entryDataOffsets[i] = runningOffset;
            entryDataSizes[i] = frameBytes[i].Length;
            runningOffset += frameBytes[i].Length;
        }

        for (int i = 0; i < count; i++)
        {
            var bmp = frames[i];
            // Pitfall 3: the ICONDIRENTRY width/height fields are single bytes; the
            // format spec reserves 0 to mean 256. Keep this explicit ternary rather
            // than a plain (byte) cast, which would "accidentally" produce the same
            // byte for 256 but silently do the wrong thing for any other value >255.
            bw.Write((byte)(bmp.Width >= 256 ? 0 : bmp.Width));
            bw.Write((byte)(bmp.Height >= 256 ? 0 : bmp.Height));
            bw.Write((byte)0);      // color count (0 = no palette, >=8bpp)
            bw.Write((byte)0);      // reserved
            bw.Write((ushort)1);    // planes
            bw.Write((ushort)32);   // bit count
            bw.Write((uint)entryDataSizes[i]);
            bw.Write((uint)entryDataOffsets[i]);
        }

        foreach (var data in frameBytes)
        {
            bw.Write(data);
        }
    }

    /// <summary>
    /// Encodes a single 32bpp ARGB bitmap as an uncompressed BMP-in-ICO frame:
    /// a 40-byte BITMAPINFOHEADER (biHeight doubled per the ICO convention) followed
    /// by bottom-up BGRA pixel rows and an all-zero 1bpp AND mask padded to a 4-byte
    /// row boundary (Pitfall 4). Real per-pixel transparency comes from the 32bpp
    /// alpha channel, which Vista+ icon rendering honors directly.
    /// </summary>
    public static byte[] EncodeBmpInIco(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        var locked = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = locked.Stride;
            var pixels = new byte[stride * h];
            Marshal.Copy(locked.Scan0, pixels, 0, pixels.Length);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // BITMAPINFOHEADER (40 bytes) -- height is DOUBLED (XOR + AND masks stacked)
            bw.Write((uint)40);
            bw.Write(w);
            bw.Write(h * 2);
            bw.Write((ushort)1);      // planes
            bw.Write((ushort)32);     // bit count
            bw.Write((uint)0);        // BI_RGB, uncompressed
            bw.Write((uint)(stride * h));
            bw.Write(0); bw.Write(0); // x/y pels per meter
            bw.Write((uint)0); bw.Write((uint)0); // colors used/important

            // XOR mask: bottom-up, BGRA per pixel (Bitmap's Format32bppArgb is already
            // BGRA in memory on little-endian Windows, so rows are copied as-is,
            // just in reverse row order).
            for (int y = h - 1; y >= 0; y--)
            {
                bw.Write(pixels, y * stride, stride);
            }

            // AND mask: 1bpp, bottom-up, each row padded to a 4-byte boundary.
            // All-zero (fully opaque per legacy XOR convention) -- the real
            // transparency comes from the 32bpp alpha channel above.
            int maskRowBytes = ((w + 31) / 32) * 4;
            var zeroRow = new byte[maskRowBytes];
            for (int y = 0; y < h; y++)
            {
                bw.Write(zeroRow);
            }

            return ms.ToArray();
        }
        finally
        {
            bmp.UnlockBits(locked);
        }
    }

    /// <summary>
    /// Encodes a single bitmap as a standard PNG blob for embedding directly as an
    /// ICO frame's data (Pitfall 5). Used only for frames at 256x256 -- the
    /// well-established convention for large ICO frames since Windows Vista, and the
    /// encoding System.Drawing.Icon's own reader actually recognizes at that size
    /// (confirmed by round-trip testing: a raw BMP-in-ICO 256px frame silently fails
    /// to load as 256x256, while a PNG-in-ICO frame at the same size loads correctly).
    /// PNG's own header carries width/height/alpha, so no BITMAPINFOHEADER or AND
    /// mask is written here -- the ICONDIRENTRY's data-size field alone tells readers
    /// how many bytes to treat as one opaque blob (PNG signature is how they detect
    /// it isn't a BMP).
    /// </summary>
    private static byte[] EncodePngInIco(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
