namespace RentDrive.services.interfaces
{
    public interface IFileService
    {
        Task<List<string>> SaveImageAsync(IFormFileCollection? files);
        Task RemoveImageAsync(List<string> imgUrl);
    }
}
