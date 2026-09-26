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

        public async Task<List<RentItemDto>> GetItemsAsync(int page, int pageSize, SortParams sortParams, IWebHostEnvironment env)
        {
            var query = _context.RentItems.Include(i => i.Owner).AsQueryable();

            query = GetSortedList(query, sortParams);

            var pagedEntities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var itemsDto = pagedEntities.Select(i => RentItemDto.ToDto(i)).ToList();

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

            if (!string.IsNullOrWhiteSpace(editDto.Title)) item.Title = editDto.Title;
            if (!string.IsNullOrWhiteSpace(editDto.Description)) item.Description = editDto.Description;
            if (editDto.PricePerDay != null) item.PricePerDay = (decimal)editDto.PricePerDay;

            var imagesToRemove = item.ImageUrls.Except(editDto.KeepImageUrls).ToList();
            if (imagesToRemove.Count > 0)
                _fileService.RemoveImage(imagesToRemove);

            var finalImageUrls = item.ImageUrls.Intersect(editDto.KeepImageUrls).ToList();

            if (editDto.NewImageFiles != null && editDto.NewImageFiles.Count > 0)
            {
                var newPaths = await _fileService.SaveImageAsync(editDto.NewImageFiles);
                finalImageUrls.AddRange(newPaths);
            }

            item.ImageUrls = finalImageUrls;
            _context.RentItems.Update(item);
            await _context.SaveChangesAsync();

            return RentItemDto.ToDto(item);
        }

        public async Task<bool> RemoveItemByIdAsync(int id, string ownerId, bool isAdminOrModer)
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

        public async Task<bool> RemoveItemsAsync(string ownerId)
        {
            return await _context.RentItems
                .Where(i => i.OwnerId == ownerId)
                .ExecuteDeleteAsync() != 0;
        }

        private IQueryable<RentItem> GetSortedList(IQueryable<RentItem> source, SortParams sortParams)
        {
            IQueryable<RentItem> query = source;

            if(!string.IsNullOrWhiteSpace(sortParams.Search))
                query = source.Where(i => i.Title.Contains(sortParams.Search) ||
                                    i.Description.Contains(sortParams.Search));

            bool isDesc = sortParams.SortDirection?.ToLower() == "desc";

            return sortParams.SortBy?.ToLower() switch
            {
                "id" => isDesc ? query.OrderByDescending(i => i.Id)
                               : query.OrderBy(i => i.Id),

                "title" => isDesc ? query.OrderByDescending(i => i.Title)
                                  : query.OrderBy(i => i.Title),

                "priceperday" => isDesc ? query.OrderByDescending(i => i.PricePerDay)
                                        : query.OrderBy(i => i.PricePerDay),

                "created_at" or "createdat" => isDesc ? query.OrderByDescending(i => i.CreatedAt)
                                                      : query.OrderBy(i => i.CreatedAt),

                _ => isDesc ? query.OrderByDescending(i => i.Title)
                            : query.OrderBy(i => i.Title)
            };
        }
    }
}
