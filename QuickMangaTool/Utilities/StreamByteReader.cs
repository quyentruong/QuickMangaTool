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

        using var sourceBuffer = new MemoryStream();
        input.CopyTo(sourceBuffer);
        sourceBytes = sourceBuffer.Length;
        return sourceBuffer.ToArray();
    }
}