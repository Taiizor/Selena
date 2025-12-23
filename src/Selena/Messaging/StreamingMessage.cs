namespace Selena.Messaging
{
    /// <summary>
    /// Represents a message that can be sent in chunks for large payloads.
    /// </summary>
    public class StreamingMessage
    {
        /// <summary>
        /// Unique ID for this streaming message.
        /// </summary>
        public Guid StreamId { get; }

        /// <summary>
        /// Total number of chunks.
        /// </summary>
        public int TotalChunks { get; }

        /// <summary>
        /// Size of each chunk (except possibly the last one).
        /// </summary>
        public int ChunkSize { get; }

        /// <summary>
        /// Original message type.
        /// </summary>
        public int MessageType { get; }

        /// <summary>
        /// Total payload size.
        /// </summary>
        public long TotalSize { get; }

        private readonly byte[] _data;

        public StreamingMessage(byte[] data, int messageType, int chunkSize = 65536) // 64KB default chunk
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            StreamId = Guid.NewGuid();
            MessageType = messageType;
            ChunkSize = chunkSize;
            TotalSize = data.Length;
            TotalChunks = (int)Math.Ceiling((double)data.Length / chunkSize);
        }

        /// <summary>
        /// Gets a specific chunk.
        /// </summary>
        public ChunkedMessage GetChunk(int chunkIndex)
        {
            if (chunkIndex < 0 || chunkIndex >= TotalChunks)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkIndex));
            }

            int startOffset = chunkIndex * ChunkSize;
            int chunkLength = Math.Min(ChunkSize, _data.Length - startOffset);

            byte[] chunkData = new byte[chunkLength];
            Buffer.BlockCopy(_data, startOffset, chunkData, 0, chunkLength);

            return new ChunkedMessage
            {
                StreamId = StreamId,
                ChunkIndex = chunkIndex,
                TotalChunks = TotalChunks,
                ChunkData = chunkData,
                MessageType = MessageType,
                TotalSize = TotalSize
            };
        }

        /// <summary>
        /// Creates chunks as an enumerable for efficient processing.
        /// </summary>
        public IEnumerable<ChunkedMessage> GetChunks()
        {
            for (int i = 0; i < TotalChunks; i++)
            {
                yield return GetChunk(i);
            }
        }
    }

    /// <summary>
    /// Represents a single chunk of a streaming message.
    /// </summary>
    public class ChunkedMessage
    {
        public Guid StreamId { get; set; }
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public byte[] ChunkData { get; set; } = Array.Empty<byte>();
        public int MessageType { get; set; }
        public long TotalSize { get; set; }

        /// <summary>
        /// Checks if this is the last chunk.
        /// </summary>
        public bool IsLastChunk => ChunkIndex == TotalChunks - 1;
    }

    /// <summary>
    /// Assembles chunks back into a complete message.
    /// </summary>
    public class StreamingMessageAssembler
    {
        private readonly Dictionary<Guid, ChunkAssemblyState> _activeStreams = [];
        private readonly int _timeoutSeconds;

        public StreamingMessageAssembler(int timeoutSeconds = 60)
        {
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Adds a chunk and returns the complete data if all chunks are received.
        /// </summary>
        public (bool isComplete, byte[]? completeData, int messageType) AddChunk(ChunkedMessage chunk)
        {
            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            // Clean up old streams
            CleanupExpiredStreams();

            if (!_activeStreams.TryGetValue(chunk.StreamId, out ChunkAssemblyState? state))
            {
                state = new ChunkAssemblyState
                {
                    StreamId = chunk.StreamId,
                    TotalChunks = chunk.TotalChunks,
                    ReceivedChunks = new bool[chunk.TotalChunks],
                    Chunks = new byte[chunk.TotalChunks][],
                    MessageType = chunk.MessageType,
                    TotalSize = chunk.TotalSize,
                    FirstChunkTime = DateTime.UtcNow
                };
                _activeStreams[chunk.StreamId] = state;
            }

            // Validate chunk
            if (chunk.ChunkIndex >= state.TotalChunks)
            {
                throw new InvalidOperationException($"Invalid chunk index: {chunk.ChunkIndex}");
            }

            // Store chunk
            state.Chunks[chunk.ChunkIndex] = chunk.ChunkData;
            state.ReceivedChunks[chunk.ChunkIndex] = true;
            state.LastChunkTime = DateTime.UtcNow;

            // Check if complete
            if (state.IsComplete())
            {
                byte[] completeData = AssembleData(state);
                _activeStreams.Remove(chunk.StreamId);
                return (true, completeData, state.MessageType);
            }

            return (false, null, 0);
        }

        private byte[] AssembleData(ChunkAssemblyState state)
        {
            byte[] result = new byte[state.TotalSize];
            int offset = 0;

            for (int i = 0; i < state.TotalChunks; i++)
            {
                byte[] chunk = state.Chunks[i];
                Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
                offset += chunk.Length;
            }

            return result;
        }

        private void CleanupExpiredStreams()
        {
            List<Guid> expiredStreams = _activeStreams
                .Where(kvp => DateTime.UtcNow - kvp.Value.LastChunkTime > TimeSpan.FromSeconds(_timeoutSeconds))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (Guid streamId in expiredStreams)
            {
                _activeStreams.Remove(streamId);
            }
        }

        private class ChunkAssemblyState
        {
            public Guid StreamId { get; set; }
            public int TotalChunks { get; set; }
            public bool[] ReceivedChunks { get; set; } = Array.Empty<bool>();
            public byte[][] Chunks { get; set; } = Array.Empty<byte[]>();
            public int MessageType { get; set; }
            public long TotalSize { get; set; }
            public DateTime FirstChunkTime { get; set; }
            public DateTime LastChunkTime { get; set; }

            public bool IsComplete()
            {
                return ReceivedChunks.All(received => received);
            }
        }
    }
}
