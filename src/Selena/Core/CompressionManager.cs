using Selena.Messaging;
using System.Buffers;
using System.IO.Compression;

namespace Selena.Core
{
    /// <summary>
    /// Manages message compression for large payloads.
    /// </summary>
    internal static class CompressionManager
    {
        // Minimum size for compression to be beneficial (typically > 1KB)
        private const int MinCompressionSize = 1024;

        // Compression level - balanced between speed and ratio
        private const CompressionLevel DefaultLevel = CompressionLevel.Fastest;

        /// <summary>
        /// Compresses data if beneficial.
        /// </summary>
        public static (byte[] data, bool compressed, int originalSize) CompressIfBeneficial(ReadOnlySpan<byte> data)
        {
            if (data.Length < MinCompressionSize)
            {
                // Too small, compression overhead not worth it
                return (data.ToArray(), false, data.Length);
            }

#if NETSTANDARD2_0
            MemoryStream compressedStream = new();
            try
            {
                using (GZipStream gzipStream = new(compressedStream, DefaultLevel))
                {
                    byte[] dataArray = data.ToArray();
                    gzipStream.Write(dataArray, 0, dataArray.Length);
                }

                byte[] compressed = compressedStream.ToArray();

                // Only use compression if it actually reduces size by at least 10%
                if (compressed.Length < data.Length * 0.9)
                {
                    return (compressed, true, data.Length);
                }

                // Compression not beneficial
                return (data.ToArray(), false, data.Length);
            }
            finally
            {
                compressedStream?.Dispose();
            }
#else
            using MemoryStream compressedStream = new();
            using (GZipStream gzipStream = new(compressedStream, DefaultLevel))
            {
                gzipStream.Write(data);
            }

            byte[] compressed = compressedStream.ToArray();

            // Only use compression if it actually reduces size by at least 10%
            if (compressed.Length < data.Length * 0.9)
            {
                return (compressed, true, data.Length);
            }

            // Compression not beneficial
            return (data.ToArray(), false, data.Length);
#endif
        }

        /// <summary>
        /// Compresses data using pooled buffers.
        /// </summary>
        public static PooledBuffer CompressPooled(ReadOnlySpan<byte> data)
        {
#if NETSTANDARD2_0
            MemoryStream compressedStream = new();
            try
            {
                using (GZipStream gzipStream = new(compressedStream, DefaultLevel))
                {
                    byte[] dataArray = data.ToArray();
                    gzipStream.Write(dataArray, 0, dataArray.Length);
                }

                byte[] compressed = compressedStream.ToArray();
                PooledBuffer pooledBuffer = BufferPoolManager.RentPooledBuffer(compressed.Length);
                compressed.CopyTo(pooledBuffer.Span);

                return pooledBuffer;
            }
            finally
            {
                compressedStream?.Dispose();
            }
#else
            using MemoryStream compressedStream = new();
            using (GZipStream gzipStream = new(compressedStream, DefaultLevel))
            {
                gzipStream.Write(data);
            }

            byte[] compressed = compressedStream.ToArray();
            PooledBuffer pooledBuffer = BufferPoolManager.RentPooledBuffer(compressed.Length);
            compressed.CopyTo(pooledBuffer.Span);

            return pooledBuffer;
#endif
        }

        /// <summary>
        /// Decompresses data.
        /// </summary>
        public static byte[] Decompress(ReadOnlySpan<byte> compressedData, int originalSize)
        {
#if NETSTANDARD2_0
            MemoryStream compressedStream = new();
            try
            {
                byte[] dataArray = compressedData.ToArray();
                compressedStream.Write(dataArray, 0, dataArray.Length);
                compressedStream.Position = 0;

                using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress);
                byte[] buffer = new byte[originalSize];

                int totalRead = 0;
                int bytesRead;
                while (totalRead < originalSize && (bytesRead = gzipStream.Read(buffer, totalRead, originalSize - totalRead)) > 0)
                {
                    totalRead += bytesRead;
                }

                if (totalRead != originalSize)
                {
                    throw new InvalidOperationException($"Decompression size mismatch. Expected: {originalSize}, Got: {totalRead}");
                }

                return buffer;
            }
            finally
            {
                compressedStream?.Dispose();
            }
#else
            using MemoryStream compressedStream = new();
            compressedStream.Write(compressedData);
            compressedStream.Position = 0;

            using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress);
            byte[] buffer = new byte[originalSize];

            int totalRead = 0;
            int bytesRead;
            while (totalRead < originalSize && (bytesRead = gzipStream.Read(buffer, totalRead, originalSize - totalRead)) > 0)
            {
                totalRead += bytesRead;
            }

            if (totalRead != originalSize)
            {
                throw new InvalidOperationException($"Decompression size mismatch. Expected: {originalSize}, Got: {totalRead}");
            }

            return buffer;
#endif
        }

        /// <summary>
        /// Decompresses data using pooled buffers.
        /// </summary>
        public static PooledBuffer DecompressPooled(ReadOnlySpan<byte> compressedData, int originalSize)
        {
            PooledBuffer pooledBuffer = BufferPoolManager.RentPooledBuffer(originalSize);

#if NETSTANDARD2_0
            MemoryStream compressedStream = new();
            try
            {
                byte[] dataArray = compressedData.ToArray();
                compressedStream.Write(dataArray, 0, dataArray.Length);
                compressedStream.Position = 0;

                using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress);

                int totalRead = 0;
                int bytesRead;
                byte[] buffer = pooledBuffer.Buffer;

                while (totalRead < originalSize && (bytesRead = gzipStream.Read(buffer, totalRead, originalSize - totalRead)) > 0)
                {
                    totalRead += bytesRead;
                }

                if (totalRead != originalSize)
                {
                    pooledBuffer.Dispose();
                    throw new InvalidOperationException($"Decompression size mismatch. Expected: {originalSize}, Got: {totalRead}");
                }

                return pooledBuffer;
            }
            finally
            {
                compressedStream?.Dispose();
            }
#else
            using MemoryStream compressedStream = new();
            compressedStream.Write(compressedData);
            compressedStream.Position = 0;

            using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress);

            int totalRead = 0;
            int bytesRead;
            byte[] buffer = pooledBuffer.Buffer;

            while (totalRead < originalSize && (bytesRead = gzipStream.Read(buffer, totalRead, originalSize - totalRead)) > 0)
            {
                totalRead += bytesRead;
            }

            if (totalRead != originalSize)
            {
                pooledBuffer.Dispose();
                throw new InvalidOperationException($"Decompression size mismatch. Expected: {originalSize}, Got: {totalRead}");
            }

            return pooledBuffer;
#endif
        }

        /// <summary>
        /// Checks if data is compressed by looking for GZip magic numbers.
        /// </summary>
        public static bool IsCompressed(ReadOnlySpan<byte> data)
        {
            return data.Length >= 2 && data[0] == 0x1f && data[1] == 0x8b; // GZip magic numbers
        }
    }

    /// <summary>
    /// Extended message header with compression support.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct CompressedMessageHeader
    {
        public MessageHeader BaseHeader;
        public int OriginalSize; // Original size before compression
        public byte Flags; // Bit 0: IsCompressed

        public const int Size = MessageHeader.Size + 5; // Base + 4 + 1

        public bool IsCompressed => (Flags & 1) != 0;

        public static CompressedMessageHeader Create(int payloadLength, int messageType, bool isCompressed, int originalSize)
        {
            return new CompressedMessageHeader
            {
                BaseHeader = MessageHeader.Create(payloadLength + 5, messageType),
                OriginalSize = isCompressed ? originalSize : payloadLength,
                Flags = (byte)(isCompressed ? 1 : 0)
            };
        }
    }
}