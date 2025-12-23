using System.Buffers;

namespace Selena.Core
{
    /// <summary>
    /// Manages buffer pooling to reduce memory allocations and GC pressure.
    /// </summary>
    internal static class BufferPoolManager
    {
        // Different pool sizes for different message categories
        private static readonly ArrayPool<byte> SmallPool = ArrayPool<byte>.Create(1024, 100); // 1KB buffers
        private static readonly ArrayPool<byte> MediumPool = ArrayPool<byte>.Create(16 * 1024, 50); // 16KB buffers
        private static readonly ArrayPool<byte> LargePool = ArrayPool<byte>.Create(256 * 1024, 25); // 256KB buffers
        private static readonly ArrayPool<byte> SharedPool = ArrayPool<byte>.Shared; // For very large messages

        /// <summary>
        /// Rents a buffer from the appropriate pool based on size.
        /// </summary>
        public static byte[] RentBuffer(int minimumLength)
        {
            return GetPoolForSize(minimumLength).Rent(minimumLength);
        }

        /// <summary>
        /// Returns a buffer to the appropriate pool.
        /// </summary>
        public static void ReturnBuffer(byte[] buffer, bool clearBuffer = true)
        {
            if (buffer == null)
            {
                return;
            }

            GetPoolForSize(buffer.Length).Return(buffer, clearBuffer);
        }

        /// <summary>
        /// Rents a buffer and returns a disposable wrapper that will return it to the pool.
        /// </summary>
        public static PooledBuffer RentPooledBuffer(int minimumLength)
        {
            return new PooledBuffer(RentBuffer(minimumLength), minimumLength);
        }

        private static ArrayPool<byte> GetPoolForSize(int size)
        {
            return size switch
            {
                <= 1024 => SmallPool,
                <= 16 * 1024 => MediumPool,
                <= 256 * 1024 => LargePool,
                _ => SharedPool
            };
        }
    }

    /// <summary>
    /// Disposable wrapper for pooled buffers.
    /// </summary>
    public struct PooledBuffer : IDisposable
    {
        private byte[]? _buffer;

        internal PooledBuffer(byte[] buffer, int usedLength)
        {
            _buffer = buffer;
            Length = usedLength;
        }

        public byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(PooledBuffer));
        public int Length { get; }
        public Memory<byte> Memory => new(_buffer, 0, Length);
        public Span<byte> Span => new(_buffer, 0, Length);

        public void Dispose()
        {
            byte[]? buffer = _buffer;
            if (buffer != null)
            {
                _buffer = null;
                BufferPoolManager.ReturnBuffer(buffer);
            }
        }
    }
}
