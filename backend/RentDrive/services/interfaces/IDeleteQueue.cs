using System.Threading.Channels;

namespace RentDrive.services.interfaces
{
    public interface IDeleteQueue<T>
    {
        public ChannelReader<T> Reader { get; }
        ValueTask Enqueue(T element);
        void Complete();
    }
}
