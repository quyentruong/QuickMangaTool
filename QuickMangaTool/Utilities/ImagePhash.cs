using NetVips;

static class ImagePhash
{
    private const int ResizeSize = 32;
    private const int HashSize = 8;

    public static ulong ComputeHash(Stream stream)
    {
        var sourceData = StreamByteReader.Read(stream, out _);
        using var image = Image.NewFromBuffer(sourceData, kwargs: new VOption
        {
            {"access", Enums.Access.Sequential}
        });

        using var mono = image.Colourspace(Enums.Interpretation.Bw).Cast(Enums.BandFormat.Uchar);
        using var resized = mono.Resize((double)ResizeSize / Math.Max(mono.Width, mono.Height));
        using var square = resized.Embed(0, 0, ResizeSize, ResizeSize, extend: Enums.Extend.Black);
        var values = square.WriteToMemory<byte>();

        var matrix = new double[ResizeSize, ResizeSize];
        for (var y = 0; y < ResizeSize; y++)
        {
            for (var x = 0; x < ResizeSize; x++)
            {
                matrix[y, x] = values[y * ResizeSize + x];
            }
        }

        var dct = ComputeDct(matrix);

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
