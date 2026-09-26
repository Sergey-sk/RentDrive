namespace RentDrive.services.interfaces
{
    public interface IFileService
    {
        Task<List<string>> SaveImageAsync(IFormFileCollection? files);
        void RemoveImage(List<string> imgUrl);
    }
}
