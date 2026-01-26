using System.Collections.Generic;

namespace AIBridge.Editor
{
    /// <summary>
    /// Thread-safe command queue for managing pending commands
    /// </summary>
    public class CommandQueue
    {
        private readonly Queue<CommandRequest> _queue = new Queue<CommandRequest>();
        private readonly object _lock = new object();
        private readonly HashSet<string> _processedIds = new HashSet<string>();

        /// <summary>
        /// Number of pending commands
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count;
                }
            }
        }

        /// <summary>
        /// Enqueue a command if not already processed
        /// </summary>
        /// <returns>True if enqueued, false if already processed</returns>
        public bool Enqueue(CommandRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.id))
            {
                return false;
            }

            lock (_lock)
            {
                if (_processedIds.Contains(request.id))
                {
                    return false;
                }

                _processedIds.Add(request.id);
                _queue.Enqueue(request);
                return true;
            }
        }

        /// <summary>
        /// Try to dequeue a command
        /// </summary>
        public bool TryDequeue(out CommandRequest request)
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    request = _queue.Dequeue();
                    return true;
                }

                request = null;
                return false;
            }
        }

        /// <summary>
        /// Check if a command ID has been processed
        /// </summary>
        public bool IsProcessed(string id)
        {
            lock (_lock)
            {
                return _processedIds.Contains(id);
            }
        }

        /// <summary>
        /// Clear all pending commands and processed IDs
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _queue.Clear();
                _processedIds.Clear();
            }
        }

        /// <summary>
        /// Clear processed IDs older than a certain count to prevent memory growth
        /// </summary>
        public void TrimProcessedIds(int maxCount = 1000)
        {
            lock (_lock)
            {
                if (_processedIds.Count > maxCount)
                {
                    _processedIds.Clear();
                    AIBridgeLogger.LogDebug("Trimmed processed IDs cache");
                }
            }
        }
    }
}
