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
        private readonly IFileDeleteQueue _deleteQueue;

        public ItemsService(ApplicationDbContext context, IFileService fileService, IFileDeleteQueue deleteQueue)
        {
            _context = context;
            _fileService = fileService;
            _deleteQueue = deleteQueue;
        }

        public async Task<(List<RentItemDto>, int)> GetItemsAsync(int page, int pageSize, SortParams sortParams)
        {
            var query = _context.RentItems.Include(i => i.Owner).AsQueryable();

            query = GetSortedList(query, sortParams);

            var totalItems = await query.CountAsync();
            var pageCount = (int)Math.Ceiling((double)totalItems / pageSize);

            var pagedEntities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var itemsDto = pagedEntities.Select(i => RentItemDto.ToDto(i)).ToList();

            return (itemsDto, pageCount);
        }

        public async Task<(List<RentItemDto>, int)> GetUserItemsAsync(string userId, int page, int pageSize, SortParams sortParams)
        {
            var query = _context.RentItems
                .Include(i => i.Owner)
                .Where(i => i.OwnerId == userId)
                .AsQueryable();

            query = GetSortedList(query, sortParams);

            var totalItems = await query.CountAsync();
            var pageCount = (int)Math.Ceiling((double)totalItems / pageSize);

            var pagedEntities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var itemsDto = pagedEntities.Select(i => RentItemDto.ToDto(i)).ToList();

            return (itemsDto, pageCount);
        }

        public async Task<RentItemDto?> GetItemByIdAsync(int id)
        {
            var item = await _context.RentItems
                .Include(ri => ri.Owner)
                .FirstOrDefaultAsync(ri => ri.Id == id);

            if (item == null) return null;

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
            var item = await _context.RentItems.
                FirstOrDefaultAsync(i => i.Id == id);

            if(item == null) return false;

            if (item.OwnerId != ownerId && !isAdminOrModer) return false;

            var imagePaths = item.ImageUrls.ToList();

            _context.RentItems.Remove(item);
            await _context.SaveChangesAsync();

            if (imagePaths != null && imagePaths.Count > 0)
                _deleteQueue.Enqueue(imagePaths);

            return true;
        }

        public async Task<bool> RemoveItemsAsync(string ownerId)
        {
            var items = await _context.RentItems
                .Where(i => i.OwnerId == ownerId)
                .ToListAsync();

            var imgUrls = items.SelectMany(i => i.ImageUrls).ToList();

            var isDeletionSuccessful = await _context.RentItems
                .Where(i => i.OwnerId == ownerId)
                .ExecuteDeleteAsync() != 0;

            if (isDeletionSuccessful)
                _deleteQueue.Enqueue(imgUrls);

            return isDeletionSuccessful;
        }

        private IQueryable<RentItem> GetSortedList(IQueryable<RentItem> source, SortParams sortParams)
        {
            IQueryable<RentItem> query = source;

            if (!string.IsNullOrWhiteSpace(sortParams.Search))
            {
                var formattedSearch = string.Join(" ", sortParams.Search.Trim().Split(' ').Select(w => $"+{w}*"));

                query = query.Where(i => EF.Functions.Match(
                    new[] { i.Title, i.Description },
                    formattedSearch,
                    MySqlMatchSearchMode.Boolean
                ) > 0);
            }

            if (sortParams.MinPrice.HasValue)
                query = query.Where(i => i.PricePerDay >= sortParams.MinPrice.Value);

            if (sortParams.MaxPrice.HasValue)
                query = query.Where(i => i.PricePerDay <= sortParams.MaxPrice.Value);

            bool isDesc = sortParams.SortDirection?.ToLower() == "desc";

            return sortParams.SortBy?.ToLower() switch
            {
                "id" => isDesc ? query.OrderByDescending(i => i.Id)
                               : query.OrderBy(i => i.Id),

                "title" => isDesc ? query.OrderByDescending(i => i.Title)
                                  : query.OrderBy(i => i.Title),

                "priceperday" or
                "price" or
                "price_per_day" => isDesc ? query.OrderByDescending(i => i.PricePerDay)
                                          : query.OrderBy(i => i.PricePerDay),

                "created_at" or "createdat" => isDesc ? query.OrderByDescending(i => i.CreatedAt)
                                                      : query.OrderBy(i => i.CreatedAt),

                _ => isDesc ? query.OrderByDescending(i => i.Title)
                            : query.OrderBy(i => i.Title)
            };
        }
    }
}
