using Selena.API;
using System.IO.MemoryMappedFiles;

namespace Selena.Core
{
    /// <summary>
    /// Thread-safe circular buffer implementation over Memory-Mapped File.
    /// </summary>
    internal class CircularBuffer
    {
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly int _bufferSize;
        private readonly int _dataStartOffset;
        private readonly OverflowStrategy _overflowStrategy;

        // Buffer layout:
        // [0-3]    Magic number (4 bytes)
        // [4-7]    Version (4 bytes)
        // [8-15]   Buffer size (8 bytes)
        // [16-23]  Write position (8 bytes)
        // [24-31]  Read position (8 bytes)
        // [32-63]  Reserved (32 bytes)
        // [64-...] Data

        private const int HEADER_SIZE = 64;
        private const int WRITE_POS_OFFSET = 16;
        private const int READ_POS_OFFSET = 24;

        public CircularBuffer(MemoryMappedViewAccessor accessor, int bufferSize, OverflowStrategy overflowStrategy)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _bufferSize = bufferSize;
            _overflowStrategy = overflowStrategy;
            _dataStartOffset = HEADER_SIZE;
            DataSize = _bufferSize - HEADER_SIZE;

            if (DataSize < 1024)
            {
                throw new ArgumentException("Buffer size too small. Must be at least 1KB + header size.");
            }
        }

        /// <summary>
        /// Gets the size of the data area (buffer size minus header).
        /// </summary>
        public int DataSize { get; }

        /// <summary>
        /// Writes data to the circular buffer.
        /// </summary>
        /// <returns>True if written successfully, false if buffer is full and blocking mode prevented write.</returns>
        public bool Write(byte[] data, int offset, int count, TimeSpan timeout)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || offset >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count > DataSize)
            {
                throw new ArgumentException($"Data size ({count}) exceeds buffer capacity ({DataSize})");
            }

            DateTime startTime = DateTime.UtcNow;

            while (true)
            {
                long writePos = GetWritePosition();
                long readPos = GetReadPosition();

                // Calculate available space
                long availableSpace = CalculateAvailableSpace(writePos, readPos);

                if (availableSpace >= count)
                {
                    // We have space, write the data
                    WriteData(writePos, data, offset, count);
                    SetWritePosition((writePos + count) % DataSize);
                    return true;
                }

                // Buffer is full
                if (_overflowStrategy == OverflowStrategy.Overwrite)
                {
                    // In overwrite mode, advance read position to make space
                    long newReadPos = (writePos + count - DataSize + 1) % DataSize;
                    SetReadPosition(newReadPos);

                    // Write the data
                    WriteData(writePos, data, offset, count);
                    SetWritePosition((writePos + count) % DataSize);
                    return true;
                }
                else // Block mode
                {
                    // Check timeout
                    if (DateTime.UtcNow - startTime > timeout)
                    {
                        return false;
                    }

                    // Wait a bit before retrying
                    Thread.Sleep(1);
                }
            }
        }

        /// <summary>
        /// Reads data from the circular buffer.
        /// </summary>
        /// <returns>Number of bytes read.</returns>
        public int Read(byte[] buffer, int offset, int maxCount)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (maxCount < 0 || offset + maxCount > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }

            long writePos = GetWritePosition();
            long readPos = GetReadPosition();

            // Calculate available data
            long availableData = CalculateAvailableData(writePos, readPos);
            int bytesToRead = (int)Math.Min(availableData, maxCount);

            if (bytesToRead == 0)
            {
                return 0;
            }

            // Read the data
            ReadData(readPos, buffer, offset, bytesToRead);
            SetReadPosition((readPos + bytesToRead) % DataSize);

            return bytesToRead;
        }

        /// <summary>
        /// Peeks at data without advancing the read position.
        /// </summary>
        public int Peek(byte[] buffer, int offset, int maxCount)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (maxCount < 0 || offset + maxCount > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }

            long writePos = GetWritePosition();
            long readPos = GetReadPosition();

            // Calculate available data
            long availableData = CalculateAvailableData(writePos, readPos);
            int bytesToRead = (int)Math.Min(availableData, maxCount);

            if (bytesToRead == 0)
            {
                return 0;
            }

            // Read the data without updating position
            ReadData(readPos, buffer, offset, bytesToRead);

            return bytesToRead;
        }

        /// <summary>
        /// Gets the number of bytes available to read.
        /// </summary>
        public long GetAvailableData()
        {
            long writePos = GetWritePosition();
            long readPos = GetReadPosition();
            return CalculateAvailableData(writePos, readPos);
        }

        /// <summary>
        /// Gets the number of bytes available to read from a specific local read position.
        /// </summary>
        /// <param name="localReadPos">The local read position to calculate from.</param>
        /// <returns>Number of bytes available to read.</returns>
        public long GetAvailableDataFromPosition(long localReadPos)
        {
            long writePos = GetWritePosition();
            return CalculateAvailableData(writePos, localReadPos);
        }

        /// <summary>
        /// Gets the current write position.
        /// </summary>
        /// <returns>The current write position in the buffer.</returns>
        public long GetCurrentWritePosition()
        {
            return GetWritePosition();
        }

        /// <summary>
        /// Peeks at data from a specific position without advancing any read position.
        /// </summary>
        /// <param name="buffer">Buffer to read into.</param>
        /// <param name="offset">Offset in buffer to start writing.</param>
        /// <param name="maxCount">Maximum bytes to read.</param>
        /// <param name="readPosition">The position to read from.</param>
        /// <returns>Number of bytes read.</returns>
        public int PeekFromPosition(byte[] buffer, int offset, int maxCount, long readPosition)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (maxCount < 0 || offset + maxCount > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }

            long writePos = GetWritePosition();

            // Calculate available data from the specified position
            long availableData = CalculateAvailableData(writePos, readPosition);
            int bytesToRead = (int)Math.Min(availableData, maxCount);

            if (bytesToRead == 0)
            {
                return 0;
            }

            // Read the data without updating any position
            ReadData(readPosition, buffer, offset, bytesToRead);

            return bytesToRead;
        }

        /// <summary>
        /// Reads data from a specific position and returns the new position after reading.
        /// Does not update the shared read position - useful for multi-reader scenarios.
        /// </summary>
        /// <param name="buffer">Buffer to read into.</param>
        /// <param name="offset">Offset in buffer to start writing.</param>
        /// <param name="maxCount">Maximum bytes to read.</param>
        /// <param name="readPosition">The position to read from (will be updated with new position).</param>
        /// <returns>Number of bytes read.</returns>
        public int ReadFromPosition(byte[] buffer, int offset, int maxCount, ref long readPosition)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (maxCount < 0 || offset + maxCount > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }

            long writePos = GetWritePosition();

            // Calculate available data from the specified position
            long availableData = CalculateAvailableData(writePos, readPosition);
            int bytesToRead = (int)Math.Min(availableData, maxCount);

            if (bytesToRead == 0)
            {
                return 0;
            }

            // Read the data
            ReadData(readPosition, buffer, offset, bytesToRead);

            // Update the caller's position (not the shared position)
            readPosition = (readPosition + bytesToRead) % DataSize;

            return bytesToRead;
        }

        /// <summary>
        /// Resets the buffer positions.
        /// </summary>
        public void Reset()
        {
            SetWritePosition(0);
            SetReadPosition(0);
        }

        private long GetWritePosition()
        {
            return _accessor.ReadInt64(WRITE_POS_OFFSET);
        }

        private void SetWritePosition(long position)
        {
            _accessor.Write(WRITE_POS_OFFSET, position);
        }

        private long GetReadPosition()
        {
            return _accessor.ReadInt64(READ_POS_OFFSET);
        }

        private void SetReadPosition(long position)
        {
            _accessor.Write(READ_POS_OFFSET, position);
        }

        private long CalculateAvailableSpace(long writePos, long readPos)
        {
            if (writePos >= readPos)
            {
                return DataSize - (writePos - readPos) - 1; // -1 to distinguish full from empty
            }
            else
            {
                return readPos - writePos - 1;
            }
        }

        private long CalculateAvailableData(long writePos, long readPos)
        {
            if (writePos >= readPos)
            {
                return writePos - readPos;
            }
            else
            {
                return DataSize - readPos + writePos;
            }
        }

        private void WriteData(long position, byte[] data, int offset, int count)
        {
            long absolutePosition = _dataStartOffset + position;

            if (position + count <= DataSize)
            {
                // Simple case: continuous write
                _accessor.WriteArray(absolutePosition, data, offset, count);
            }
            else
            {
                // Wrap around case
                int firstPartSize = (int)(DataSize - position);
                int secondPartSize = count - firstPartSize;

                _accessor.WriteArray(absolutePosition, data, offset, firstPartSize);
                _accessor.WriteArray(_dataStartOffset, data, offset + firstPartSize, secondPartSize);
            }
        }

        private void ReadData(long position, byte[] buffer, int offset, int count)
        {
            long absolutePosition = _dataStartOffset + position;

            if (position + count <= DataSize)
            {
                // Simple case: continuous read
                _accessor.ReadArray(absolutePosition, buffer, offset, count);
            }
            else
            {
                // Wrap around case
                int firstPartSize = (int)(DataSize - position);
                int secondPartSize = count - firstPartSize;

                _accessor.ReadArray(absolutePosition, buffer, offset, firstPartSize);
                _accessor.ReadArray(_dataStartOffset, buffer, offset + firstPartSize, secondPartSize);
            }
        }
    }
}
