using NetVips;

static class ImageColorClassifier
{
    public static bool IsBlackAndWhite(byte[] sourceData, double colorPixelRatioThreshold = 0.05)
    {
        // Thu nhỏ về 128px để phân loại màu trong nháy mắt (<1ms)
        using var thumb = Image.ThumbnailBuffer(sourceData, 128, height: 128);

        if (thumb.Bands == 1)
            return true;

        using var flattened = thumb.HasAlpha() ? thumb.Flatten(background: [255, 255, 255]) : thumb;
        using var srgb = flattened.Interpretation != Enums.Interpretation.Srgb
            ? flattened.Colourspace(Enums.Interpretation.Srgb)
            : flattened;

        if (srgb.Bands < 3)
            return true;

        using var r = srgb.ExtractBand(0);
        using var g = srgb.ExtractBand(1);
        using var b = srgb.ExtractBand(2);

        // Kiểm tra độ lệch kênh: chỉ cần |R - G| và |G - B| là đủ suy ra sai biệt màu
        using var diffRg = (r - g).Abs();
        using var diffGb = (g - b).Abs();

        using var maskRg = diffRg > 25;
        using var maskGb = diffGb > 25;
        using var colorMask = maskRg | maskGb;

        // Tỷ lệ diện tích pixel có màu
        double colorRatio = colorMask.Avg() / 255.0;

        return colorRatio <= colorPixelRatioThreshold;
    }
}