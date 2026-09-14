using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using UglyToad.PdfPig;

static class PdfImageExtractor
{
    // Extract embedded raster images from a PDF file. Returns list of (suggestedName, data).
    public static List<(string Name, byte[] Data)> ExtractImages(string pdfPath)
    {
        var result = new List<(string, byte[])>();

        using var document = PdfDocument.Open(pdfPath);
        int pageIndex = 0;
        foreach (var page in document.GetPages())
        {
            pageIndex++;
            try
            {
                // PdfPig exposes page.GetImages() which returns image objects with RawBytes
                var images = page.GetImages();
                int imageIndex = 0;
                foreach (var img in images)
                {
                    imageIndex++;
                    try
                    {
                        // RawBytes keeps stream filters (e.g. FlateDecode/zlib) applied, so raw
                        // pixel data is still compressed and not a valid image file by itself.
                        // TryGetPng decodes the filter + color space into a proper PNG; fall back
                        // to RawBytes only for formats PdfPig can't convert (e.g. embedded JPEGs).
                        byte[] bytes;
                        string extension;
                        if (img.TryGetPng(out var png))
                        {
                            bytes = png;
                            extension = ".png";
                        }
                        else
                        {
                            bytes = img.RawBytes.ToArray();

                            // Filter chains like [/FlateDecode /DCTDecode] leave a zlib-wrapped
                            // JPEG in RawBytes; inflate it to recover the actual image bytes.
                            if (TryInflateZlib(bytes, out var inflated))
                                bytes = inflated;

                            extension = GetExtension(bytes);
                        }

                        var name = $"p{pageIndex:D4}_{imageIndex:D2}{extension}";
                        result.Add((name, bytes));
                    }
                    catch { /* skip problematic images */ }
                }
            }
            catch { /* continue on pages we can't inspect */ }
        }

        return result;
    }

    // Detects a zlib stream header (RFC 1950) and inflates it if present.
    private static bool TryInflateZlib(byte[] data, out byte[] inflated)
    {
        inflated = [];
        if (data.Length < 2 || data[0] != 0x78) return false;

        // The second byte's checksum must make the 16-bit header a multiple of 31
        if ((data[0] << 8 | data[1]) % 31 != 0) return false;

        try
        {
            using var input = new MemoryStream(data);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            inflated = output.ToArray();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetExtension(byte[] data)
    {
        if (data.Length >= 2)
        {
            // JPEG: FF D8
            if (data[0] == 0xFF && data[1] == 0xD8) return ".jpg";
            // PNG: 89 50 4E 47
            if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
            // GIF: 47 49 46 38
            if (data.Length >= 4 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38) return ".gif";
            // BMP: 42 4D
            if (data[0] == 0x42 && data[1] == 0x4D) return ".bmp";
            // TIFF: II (49 49) or MM (4D 4D)
            if ((data[0] == 0x49 && data[1] == 0x49) || (data[0] == 0x4D && data[1] == 0x4D)) return ".tif";
        }

        return ".bin";
    }
}
