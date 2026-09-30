using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentDrive.db.models;
using RentDrive.dto.itemsDto;
using RentDrive.endpoints.filters;
using RentDrive.services.interfaces;
using Serilog;
using System.Security.Claims;

namespace RentDrive.endpoints
{
    public static class ItemsEndpoints
    {
        private static readonly Serilog.ILogger logger = Log.ForContext(typeof(ItemsEndpoints));

        public static IEndpointRouteBuilder MapItemsEndpoint(this IEndpointRouteBuilder app)
        {
            var itemsGroup = app.MapGroup("/items")
                .WithTags("items");

            itemsGroup.MapGet("/", async (IItemsService itemsService,
                                    [AsParameters] ItemQueryParameters queryParams) =>
            {
                var (items, pageCount) = await itemsService.GetItemsAsync(queryParams);

                return Results.Ok(new { items, totalCount = items.Count, totalPages = pageCount, queryParams.Page, queryParams.PageSize });
            });

            itemsGroup.MapGet("/{id}", async (int id, IItemsService itemsService) =>
            {
                var item = await itemsService.GetItemByIdAsync(id);

                if (item == null)
                {
                    logger.Warning("Попытка получения несуществующего объекта по Id: {Id}", id);
                    return Results.NotFound($"Объект с id {id} не найден.");
                }

                return Results.Ok(item);
            })
                .WithName("GetItemById");

            itemsGroup.MapGet("/my", async ([AsParameters] ItemQueryParameters queryParams,
                                      ClaimsPrincipal principal,
                                      IItemsService itemsService) =>
            {
                string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    logger.Warning("Попытка получения объекта неавторизованным пользователем");
                    return Results.Unauthorized();
                }

                var (items, pageCount) = await itemsService.GetUserItemsAsync(userId, queryParams);

                return Results.Ok(new {items, totalCount = items.Count, totalPages = pageCount, queryParams.Page, queryParams.PageSize});
            }).RequireAuthorization();

            var createEndpoint = itemsGroup.MapPost("/", async ([AsParameters] CreateRentItemDto createDto,
                                                                      IItemsService itemsService,
                                                                      ClaimsPrincipal principal,
                                                                      UserManager<User> userManager) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await userManager.GetUserAsync(principal);

                if (user == null)
                {
                    logger.Warning("Попытка загрузки объекта неавторизованным пользователем");
                    return Results.Unauthorized();
                }

                var newItem = await itemsService.CreateItemAsync(createDto, user);

                return Results.CreatedAtRoute("GetItemById", new { id = newItem.Id }, newItem);
            }).AddEndpointFilter<CheckUserProfileFilter>().RequireAuthorization();

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                createEndpoint.DisableAntiforgery();
            }

            itemsGroup.MapPut("/{id}", async (int id,
                                            [FromForm] EditItemDto editDto,
                                            IItemsService itemsService,
                                            IWebHostEnvironment env,
                                            ClaimsPrincipal principal) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (ownerId == null)
                {
                    logger.Warning("Попытка изменения объекта неавторизованным пользователем");
                    return Results.Unauthorized();
                }

                var isAdminOrModer = principal.IsInRole("Admin") || principal.IsInRole("Moderator");

                try
                {
                    var editedItem = await itemsService.UpdateItemAsync(id, ownerId, isAdminOrModer, editDto);

                    if (editedItem == null)
                    {
                        logger.Warning("Попытка изменения несуществующего объекта по Id: {Id}", id);
                        return Results.NotFound(new { error = "Такой объект не найден" });
                    }

                    return Results.Ok(editedItem);
                }
                catch (UnauthorizedAccessException)
                {
                    logger.Warning("Попытка изменить объект другого пользователя пользователем с Id: {Id}", ownerId);
                    return Results.Forbid();
                }
            }).RequireAuthorization().DisableAntiforgery();

            itemsGroup.MapDelete("/{id}", async (int id, IItemsService itemsService, ClaimsPrincipal principal) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (ownerId == null)
                {
                    logger.Warning("Попытка удалить объект неавторизованным пользователем");
                    return Results.Unauthorized();
                }

                var isAdminOrModer = principal.FindFirstValue(ClaimTypes.Role) == "Admin" ||
                                     principal.FindFirstValue(ClaimTypes.Role) == "Moderator";

                var isRemoved = await itemsService.RemoveItemByIdAsync(id, ownerId, isAdminOrModer);

                if (!isRemoved)
                {
                    logger.Warning("Попытка удалить несуществующий или чужой объект с Id: {ItemId}, пользователем Id: {UserId}", id, ownerId);
                    return Results.BadRequest();
                }

                return Results.NoContent();
            }).RequireAuthorization();

            itemsGroup.MapDelete("/", async (IItemsService itemsService, ClaimsPrincipal principal) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (ownerId == null)
                {
                    logger.Warning("Попытка удалить объекты неавторизованным пользователем");
                    return Results.Unauthorized();
                }

                var isSuccess = await itemsService.RemoveItemsAsync(ownerId);

                if (!isSuccess)
                {
                    logger.Warning("Пользователь с Id: {UserId} попытался удалить объекты, которые не были найдены", ownerId);
                    return Results.NotFound();
                }

                return Results.NoContent();
            }).RequireAuthorization();

            return app;
        }
    }
}
