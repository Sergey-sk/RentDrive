namespace RentDrive.services.interfaces
{
    public interface IFileDeleteQueue
    {
        void Enqueue(List<string> filePaths);
        bool TryDequeue(out List<string>? filePaths);
    }
}
