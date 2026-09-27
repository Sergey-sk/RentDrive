using RentDrive.db.models;
using RentDrive.dto.itemsDto;

namespace RentDrive.services.interfaces
{
    public interface IItemsService
    {
        Task<(List<RentItemDto>, int)> GetItemsAsync(int page, int pageSize, SortParams sortParams);
        Task<(List<RentItemDto>, int)> GetUserItemsAsync(string userId, int page, int pageSize, SortParams sortParams);
        Task<RentItemDto?> GetItemByIdAsync(int id);
        Task<RentItemDto> CreateItemAsync(CreateRentItemDto createDto, User user);
        Task<RentItemDto?> UpdateItemAsync(int id, string ownerId, bool isAdminOrModer, EditItemDto itemDto);
        Task<bool> RemoveItemByIdAsync(int id, string ownerId, bool isAdmin = true);
        Task<bool> RemoveItemsAsync(string ownerId);
    }
}
