using RentDrive.services.interfaces;
using System.Collections.Concurrent;

namespace RentDrive.services.implementations
{
    public class FileDeleteQueue : IFileDeleteQueue
    {
        private readonly ConcurrentQueue<List<string>> _queue = new();

        public void Enqueue(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return;
            _queue.Enqueue(filePaths);
        }

        public bool TryDequeue(out List<string>? filePaths)
        {
            return _queue.TryDequeue(out filePaths);
        }
    }
}
