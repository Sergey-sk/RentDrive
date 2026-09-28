using RentDrive.services.interfaces;
using System.Threading.Channels;

namespace RentDrive.services.implementations
{
    public class FileDeleteQueue : IFileDeleteQueue
    {
        private readonly Channel<List<string>> _queue = Channel.CreateBounded<List<string>>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        public ChannelReader<List<string>> Reader => _queue.Reader;

        public ValueTask Enqueue(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return ValueTask.CompletedTask;
            return _queue.Writer.WriteAsync(filePaths);
            //_queue.Enqueue(filePaths);
        }

        public void Complete()
        {
            _queue.Writer.TryComplete();
        }
    }
}
