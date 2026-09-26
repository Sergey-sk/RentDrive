using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using RentDrive.db.models;
using RentDrive.dto.itemsDto;
using RentDrive.services.interfaces;

namespace RentDrive.services.implementations
{
    public class ItemsService : IItemsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;

        public ItemsService(ApplicationDbContext context, IFileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        public async Task<List<RentItemDto>> GetItemsAsync(int page, int pageSize, IWebHostEnvironment env)
        {
            var itemsDto = await _context.RentItems
                .Include(ri => ri.Owner)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ri => RentItemDto.ToDto(ri))
                .ToListAsync();

            var wwwrootPath = env.WebRootPath;

            foreach (var item in itemsDto)
            {
                for (int i = 0; i < item.ImageUrls.Count; i++)
                {
                    var fullPath = Path.Combine(wwwrootPath, item.ImageUrls[i]);

                    if (!File.Exists(fullPath)) item.ImageUrls[i] = string.Empty;
                }
            }

            return itemsDto;
        }

        public async Task<RentItemDto?> GetItemByIdAsync(int id, IWebHostEnvironment env)
        {
            var item = await _context.RentItems
                .Include(ri => ri.Owner)
                .FirstOrDefaultAsync(ri => ri.Id == id);

            if (item == null) return null;

            var wwwrootPath = env.WebRootPath;

            for (int i = 0; i < item.ImageUrls.Count; i++)
            {
                var fullPath = Path.Combine(wwwrootPath, item.ImageUrls[i]);

                if (!File.Exists(fullPath)) item.ImageUrls[i] = string.Empty;
            }

            return RentItemDto.ToDto(item);
        }

        public async Task<RentItemDto> CreateItemAsync(CreateRentItemDto createDto, User user)
        {
            List<string>? imagePaths = null;

            if (createDto.ImageFiles != null)
            {
                imagePaths = await _fileService.SaveImageAsync(createDto.ImageFiles);
            }

            var entity = new RentItem
            {
                Title = createDto.Title,
                Description = createDto.Description,
                PricePerDay = createDto.PricePerDay,
                ImageUrls = imagePaths ?? [],
                OwnerId = user.Id,
                Owner = user
            };

            _context.RentItems.Add(entity);
            await _context.SaveChangesAsync();

            return RentItemDto.ToDto(entity);
        }

        public async Task<RentItemDto?> UpdateItemAsync(int id, string ownerId, bool isAdminOrModer, EditItemDto editDto)
        {
            var item = await _context.RentItems
                .Include(ri => ri.Owner)
                .FirstOrDefaultAsync(ri => ri.Id == id);

            if (item == null) return null;

            if (item.OwnerId != ownerId && !isAdminOrModer)
                throw new UnauthorizedAccessException("Нет прав на редактирование этого объекта");

            if (editDto.Title != null) item.Title = editDto.Title;
            if (editDto.Description != null) item.Description = editDto.Description;
            if (editDto.PricePerDay != null) item.PricePerDay = (decimal)editDto.PricePerDay;

            var imagesToRemove = item.ImageUrls.Except(editDto.KeepImageUrls).ToList();
            if(imagesToRemove.Count > 0)
                _fileService.RemoveImage(imagesToRemove);

            var finalImageUrls = item.ImageUrls.Intersect(editDto.KeepImageUrls).ToList();

            if(editDto.NewImageFiles != null && editDto.NewImageFiles.Count > 0)
            {
                var newPaths = await _fileService.SaveImageAsync(editDto.NewImageFiles);
                finalImageUrls.AddRange(newPaths);
            }

            item.ImageUrls = finalImageUrls;
            _context.RentItems.Update(item);
            await _context.SaveChangesAsync();

            return RentItemDto.ToDto(item);
        }

        public async Task<bool> RemoveItemByIdAsync(int id, string ownerId, bool isAdminOrModer = true)
        {
            List<string>? imgUrl = await _context.RentItems
                .Where(ri => ri.Id == id)
                .Select(ri => ri.ImageUrls)
                .FirstOrDefaultAsync();

            if (imgUrl != null)
                _fileService.RemoveImage(imgUrl);

            if (isAdminOrModer)
            {
                return await _context.RentItems
                   .Where(ri => ri.Id == id)
                   .ExecuteDeleteAsync() != 0;
            }

            return await _context.RentItems
                .Where(ri => ri.Id == id && ri.OwnerId == ownerId)
                .ExecuteDeleteAsync() != 0;
        }
    }
}
