using Selena.Messaging;
using System.Collections.Concurrent;

namespace Selena.Events
{
    /// <summary>
    /// Handles asynchronous event dispatching for received messages.
    /// </summary>
    internal class EventDispatcher : IDisposable
    {
        private readonly BlockingCollection<Message> _messageQueue;
        private readonly Thread _dispatchThread;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _channelName;
        private bool _disposed;

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        public EventDispatcher(string channelName = "DefaultChannel")
        {
            _channelName = channelName;
            _messageQueue = new BlockingCollection<Message>(1000); // Max 1000 pending messages
            _cancellationTokenSource = new CancellationTokenSource();

            _dispatchThread = new Thread(DispatchThreadProc)
            {
                Name = "Selena.EventDispatcher",
                IsBackground = true
            };
            _dispatchThread.Start();
        }

        /// <summary>
        /// Adds a message to the dispatch queue.
        /// </summary>
        public void DispatchMessage(Message message)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Try to add with timeout to avoid blocking
                if (!_messageQueue.TryAdd(message, TimeSpan.FromMilliseconds(100)))
                {
                    // Queue is full, drop oldest message
                    _messageQueue.TryTake(out _);
                    _messageQueue.TryAdd(message);
                }
            }
            catch (InvalidOperationException)
            {
                // Collection was completed, ignore
            }
        }

        private void DispatchThreadProc()
        {
            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for message with cancellation
                    if (_messageQueue.TryTake(out Message? message, 100, cancellationToken))
                    {
                        DispatchMessageToHandlers(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Console.WriteLine($"[Selena.EventDispatcher] Error dispatching message: {ex.Message}");
                }
            }
        }

        private void DispatchMessageToHandlers(Message message)
        {
            EventHandler<MessageReceivedEventArgs>? handlers = MessageReceived;
            if (handlers == null)
            {
                return;
            }

            MessageReceivedEventArgs args = new(message, _channelName);

            // Get all delegates
            Delegate[] invocationList = handlers.GetInvocationList();

            // Invoke each handler asynchronously
            Parallel.ForEach(invocationList, handler =>
            {
                try
                {
                    // Check if handler is async
                    if (handler.Method.ReturnType == typeof(Task))
                    {
                        // Async handler
                        Task? task = (Task?)handler.DynamicInvoke(this, args);
                        task?.Wait(TimeSpan.FromSeconds(30)); // Timeout for async handlers
                    }
                    else
                    {
                        // Sync handler
                        handler.DynamicInvoke(this, args);
                    }
                }
                catch (Exception ex)
                {
                    // Don't let one handler's exception affect others
                    Console.WriteLine($"[Selena.EventDispatcher] Handler error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Waits for all pending messages to be dispatched.
        /// </summary>
        public async Task FlushAsync(TimeSpan timeout)
        {
            DateTime startTime = DateTime.UtcNow;

            while (_messageQueue.Count > 0)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    break;
                }

                await Task.Delay(10);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Signal completion
            _messageQueue.CompleteAdding();

            // Cancel dispatch thread
            _cancellationTokenSource.Cancel();

            // Wait for thread to finish
            _dispatchThread.Join(TimeSpan.FromSeconds(5));

            // Clean up
            _messageQueue.Dispose();
            _cancellationTokenSource.Dispose();

            _disposed = true;
        }
    }
}
