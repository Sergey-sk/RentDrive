using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using RentDrive.db.models;
using RentDrive.dto.itemsDto;
using RentDrive.services.interfaces;
using Serilog;

namespace RentDrive.services.implementations
{
    public class ItemsService : IItemsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly IDeleteQueue<List<string>> _deleteQueue;
        private readonly Serilog.ILogger _logger = Log.ForContext<ItemsService>();

        public ItemsService(ApplicationDbContext context, IFileService fileService, IDeleteQueue<List<string>> deleteQueue)
        {
            _context = context;
            _fileService = fileService;
            _deleteQueue = deleteQueue;
        }

        public async Task<(List<RentItemDto>, int)> GetItemsAsync(ItemQueryParameters queryParams)
        {
            var query = _context.RentItems.Include(i => i.Owner).AsQueryable();

            query = GetSortedList(query, queryParams);

            var page = queryParams.Page ?? 1;
            var pageSize = queryParams.PageSize ?? 10;

            var totalItems = await query.CountAsync();
            var pageCount = (int)Math.Ceiling((double)totalItems / pageSize);

            var pagedEntities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var itemsDto = pagedEntities.Select(i => RentItemDto.ToDto(i)).ToList();

            return (itemsDto, pageCount);
        }

        public async Task<(List<RentItemDto>, int)> GetUserItemsAsync(string userId, ItemQueryParameters queryParams)
        {
            var query = _context.RentItems
                .Include(i => i.Owner)
                .Where(i => i.OwnerId == userId)
                .AsQueryable();

            query = GetSortedList(query, queryParams);

            var page = queryParams.Page ?? 1;
            var pageSize = queryParams.PageSize ?? 10;

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

            _logger.Information("Объект с Id: {Id} успешно создан пользователем {UserId}", entity.Id, user.Id);
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
                await _deleteQueue.Enqueue(imagesToRemove);

            var finalImageUrls = item.ImageUrls.Intersect(editDto.KeepImageUrls).ToList();

            if (editDto.NewImageFiles != null && editDto.NewImageFiles.Count > 0)
            {
                var newPaths = await _fileService.SaveImageAsync(editDto.NewImageFiles);
                finalImageUrls.AddRange(newPaths);
            }

            item.ImageUrls = finalImageUrls;
            _context.RentItems.Update(item);
            await _context.SaveChangesAsync();

            _logger.Information("Объект с Id: {Id} успешно изменен пользователем {UserId}", item.Id, ownerId);
            return RentItemDto.ToDto(item);
        }

        public async Task<bool> RemoveItemByIdAsync(int id, string ownerId, bool isAdminOrModer)
        {
            var item = await _context.RentItems.
                FirstOrDefaultAsync(i => i.Id == id);

            if(item == null) return false;

            if (item.OwnerId != ownerId && !isAdminOrModer) return false;

            bool hasBookings = await _context.Bookings.AnyAsync(b => b.RentItemId == id);

            if (hasBookings)
            {
                item.IsDeleted = true;
                _context.RentItems.Update(item);
                await _context.SaveChangesAsync();

                _logger.Information("Товар Id {ItemId} имеет историю броней. Выполнено мягкое удаление.", id);
                return true;
            }

            var imagePaths = item.ImageUrls.ToList();

            _context.RentItems.Remove(item);
            await _context.SaveChangesAsync();

            if (imagePaths != null && imagePaths.Count > 0)
                await _deleteQueue.Enqueue(imagePaths);

            _logger.Information("Объект с Id: {Id} успешно удален пользователем {UserId}", id, ownerId);
            return true;
        }

        public async Task<bool> RemoveItemsAsync(string ownerId)
        {
            var items = await _context.RentItems
                .IgnoreQueryFilters()
                .Where(i => i.OwnerId == ownerId)
                .ToListAsync();

            if (items.Count == 0) return false;

            List<int> itemsForSoftDelete = [];
            List<int> itemsForHardDelete = [];
            List<string> imgUrls = [];

            var itemIdsWithBookings = await _context.Bookings
                .Where(b => b.RentItem.OwnerId == ownerId)
                .Select(b => b.RentItemId)
                .Distinct()
                .ToListAsync();

            foreach (var item in items)
            {
                if (itemIdsWithBookings.Contains(item.Id))
                    itemsForSoftDelete.Add(item.Id);
                else
                {
                    itemsForHardDelete.Add(item.Id);
                    imgUrls.AddRange(item.ImageUrls);
                }
            }

            await _context.RentItems
                .Where(i => i.OwnerId == ownerId && itemsForSoftDelete.Contains(i.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDeleted, true));

            var isDeletionSuccessful = await _context.RentItems
                .Where(i => i.OwnerId == ownerId && itemsForHardDelete.Contains(i.Id))
                .ExecuteDeleteAsync() != 0;

            if (isDeletionSuccessful)
                await _deleteQueue.Enqueue(imgUrls);

            _logger.Information("Успешное удаление всех объектов у пользователя Id: {Id}", ownerId);
            return true;
        }

        private IQueryable<RentItem> GetSortedList(IQueryable<RentItem> source, ItemQueryParameters queryParams)
        {
            IQueryable<RentItem> query = source;

            if (!string.IsNullOrWhiteSpace(queryParams.Search))
            {
                var formattedSearch = string.Join(" ", queryParams.Search.Trim().Split(' ').Select(w => $"+{w}*"));

                query = query.Where(i => EF.Functions.Match(
                    new[] { i.Title, i.Description },
                    formattedSearch,
                    MySqlMatchSearchMode.Boolean
                ) > 0);
            }

            if (queryParams.MinPrice.HasValue)
                query = query.Where(i => i.PricePerDay >= queryParams.MinPrice.Value);

            if (queryParams.MaxPrice.HasValue)
                query = query.Where(i => i.PricePerDay <= queryParams.MaxPrice.Value);

            bool isDesc = queryParams.SortDirection?.ToLower() == "desc";

            return queryParams.SortBy?.ToLower() switch
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
