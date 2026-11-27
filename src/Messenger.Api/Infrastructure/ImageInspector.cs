using Messenger.Domain.Abstractions;

namespace Messenger.Api.Infrastructure;

/// <summary>Detects jpeg/png/gif/webp by magic bytes and reads dimensions from the headers (no imaging library).</summary>
public sealed class ImageInspector : IImageInspector
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public ImageInfo? Inspect(Stream stream)
    {
        try
        {
            var head = new byte[32];
            stream.Position = 0;
            var n = ReadFully(stream, head);
            if (n < 12) return null;

            if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            {
                var (w, h) = JpegSize(stream);
                return new ImageInfo("image/jpeg", ".jpg", w, h);
            }
            if (head.AsSpan(0, 8).SequenceEqual(PngSignature))
            {
                if (n < 24) return new ImageInfo("image/png", ".png", null, null);
                return new ImageInfo("image/png", ".png", (int)BE32(head, 16), (int)BE32(head, 20));
            }
            if (head[0] == 'G' && head[1] == 'I' && head[2] == 'F' && head[3] == '8'
                && (head[4] == '7' || head[4] == '9') && head[5] == 'a')
                return new ImageInfo("image/gif", ".gif", head[6] | head[7] << 8, head[8] | head[9] << 8);
            if (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F'
                && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P')
            {
                var (w, h) = WebpSize(head, n);
                return new ImageInfo("image/webp", ".webp", w, h);
            }
            return null;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or ObjectDisposedException)
        {
            return null;
        }
    }

    private static int ReadFully(Stream s, byte[] buf, int count = -1)
    {
        if (count < 0) count = buf.Length;
        var total = 0;
        while (total < count)
        {
            var r = s.Read(buf, total, count - total);
            if (r <= 0) break;
            total += r;
        }
        return total;
    }

    private static uint BE32(byte[] b, int o)
        => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);

    private static (int? W, int? H) WebpSize(byte[] b, int len)
    {
        if (len < 30) return (null, null);
        var fourcc = new string(new[] { (char)b[12], (char)b[13], (char)b[14], (char)b[15] });
        switch (fourcc)
        {
            case "VP8 " when b[23] == 0x9D && b[24] == 0x01 && b[25] == 0x2A:
                return ((b[26] | b[27] << 8) & 0x3FFF, (b[28] | b[29] << 8) & 0x3FFF);
            case "VP8L" when b[20] == 0x2F:
            {
                int b0 = b[21], b1 = b[22], b2 = b[23], b3 = b[24];
                var w = 1 + (b0 | (b1 & 0x3F) << 8);
                var h = 1 + ((b1 >> 6) | b2 << 2 | (b3 & 0x0F) << 10);
                return (w, h);
            }
            case "VP8X":
                return (1 + (b[24] | b[25] << 8 | b[26] << 16), 1 + (b[27] | b[28] << 8 | b[29] << 16));
            default:
                return (null, null);
        }
    }

    /// <summary>Walks JPEG segments (starting after the SOI marker) until a Start-Of-Frame marker.</summary>
    private static (int? W, int? H) JpegSize(Stream s)
    {
        s.Position = 2;
        var seg = new byte[5];
        for (var guard = 0; guard < 4096; guard++)
        {
            int b;
            do { b = s.ReadByte(); } while (b != -1 && b != 0xFF);
            if (b == -1) return (null, null);
            int marker;
            do { marker = s.ReadByte(); } while (marker == 0xFF);
            if (marker == -1) return (null, null);
            if (marker == 0x00 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD8)) continue; // no length field
            if (marker == 0xD9 || marker == 0xDA) return (null, null); // EOI / scan data: no SOF seen

            var l1 = s.ReadByte();
            var l2 = s.ReadByte();
            if (l2 == -1) return (null, null);
            var len = l1 << 8 | l2;
            if (len < 2) return (null, null);

            var isSof = marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isSof)
            {
                if (ReadFully(s, seg) < 5) return (null, null);
                var h = seg[1] << 8 | seg[2];
                var w = seg[3] << 8 | seg[4];
                return (w, h);
            }
            s.Seek(len - 2, SeekOrigin.Current);
        }
        return (null, null);
    }
}
