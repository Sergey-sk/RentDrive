using RentDrive.services.interfaces;
using System.Threading.Channels;

namespace RentDrive.services.implementations
{
    public class DetachUserQueue : IDeleteQueue<string>
    {

        private Channel<string> _queue = Channel.CreateUnbounded<string>();

        public ChannelReader<string> Reader => _queue.Reader;

        public void Complete()
        {
            _queue.Writer.TryComplete();
        }

        public ValueTask Enqueue(string userId)
        {
            if(string.IsNullOrEmpty(userId)) return ValueTask.CompletedTask;
            return _queue.Writer.WriteAsync(userId);
        }
    }
}
