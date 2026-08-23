using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

static class ImagePhash
{
    private const int ResizeSize = 32;
    private const int HashSize = 8;

    public static ulong ComputeHash(Stream stream)
    {
        using var image = Image.Load<L8>(stream);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(ResizeSize, ResizeSize),
            Sampler = KnownResamplers.Bicubic,
            Mode = ResizeMode.Stretch
        }));

        var values = new double[ResizeSize, ResizeSize];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < ResizeSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < ResizeSize; x++)
                {
                    values[y, x] = row[x].PackedValue;
                }
            }
        });

        var dct = ComputeDct(values);

        var coefficients = new List<double>(HashSize * HashSize - 1);
        for (int y = 0; y < HashSize; y++)
        {
            for (int x = 0; x < HashSize; x++)
            {
                if (x == 0 && y == 0)
                    continue;

                coefficients.Add(dct[y, x]);
            }
        }

        var sorted = coefficients.OrderBy(v => v).ToArray();
        double median = sorted[sorted.Length / 2];

        ulong hash = 0;
        int bitIndex = 0;

        for (int y = 0; y < HashSize; y++)
        {
            for (int x = 0; x < HashSize; x++)
            {
                double coefficient = dct[y, x];
                if (coefficient > median)
                    hash |= 1UL << bitIndex;

                bitIndex++;
            }
        }

        return hash;
    }

    private static double[,] ComputeDct(double[,] values)
    {
        var result = new double[ResizeSize, ResizeSize];

        for (int v = 0; v < ResizeSize; v++)
        {
            double cv = v == 0 ? 1.0 / Math.Sqrt(2) : 1.0;

            for (int u = 0; u < ResizeSize; u++)
            {
                double cu = u == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                double sum = 0;

                for (int y = 0; y < ResizeSize; y++)
                {
                    double cosY = Math.Cos(((2 * y + 1) * v * Math.PI) / (2.0 * ResizeSize));
                    for (int x = 0; x < ResizeSize; x++)
                    {
                        double cosX = Math.Cos(((2 * x + 1) * u * Math.PI) / (2.0 * ResizeSize));
                        sum += values[y, x] * cosX * cosY;
                    }
                }

                result[v, u] = 0.25 * cu * cv * sum;
            }
        }

        return result;
    }
}
