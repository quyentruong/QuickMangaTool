static class StreamByteReader
{
    public static byte[] Read(Stream input, out long sourceBytes)
    {
        if (input is MemoryStream memoryStream)
        {
            sourceBytes = memoryStream.Length;

            if (memoryStream.TryGetBuffer(out var buffer) && buffer.Array is not null)
            {
                return buffer.Offset == 0 && buffer.Count == buffer.Array.Length
                    ? buffer.Array
                    : memoryStream.ToArray();
            }

            return memoryStream.ToArray();
        }

        int capacity = input.CanSeek && input.Length is > 0 and <= int.MaxValue ? (int)input.Length : 65536;
        using var sourceBuffer = new MemoryStream(capacity);
        input.CopyTo(sourceBuffer);
        sourceBytes = sourceBuffer.Length;

        if (sourceBuffer.TryGetBuffer(out var outBuffer) &&
            outBuffer.Array is not null &&
            outBuffer.Offset == 0 &&
            outBuffer.Count == outBuffer.Array.Length)
        {
            return outBuffer.Array;
        }

        return sourceBuffer.ToArray();
    }
}