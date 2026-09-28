using System.Threading.Channels;

namespace RentDrive.services.interfaces
{
    public interface IFileDeleteQueue
    {
        public ChannelReader<List<string>> Reader { get; }
        ValueTask Enqueue(List<string> filePaths);
        void Complete();
    }
}
