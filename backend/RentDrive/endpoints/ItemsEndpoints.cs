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
                var (items, pageCount, totalItems) = await itemsService.GetItemsAsync(queryParams);

                return Results.Ok(new { items, totalCount = totalItems, totalPages = pageCount, queryParams.Page, queryParams.PageSize });
            });

            itemsGroup.MapGet("/{id}", async (int id, IItemsService itemsService) =>
            {
                var item = await itemsService.GetItemByIdAsync(id);

                if (item == null)
                {
                    logger.Warning("Попытка получения несуществующего объекта по Id: {Id}", id);
                    return Results.Problem(
                        detail: $"Объект с id {id} не найден.",
                        title: "Объект не найден",
                        statusCode: StatusCodes.Status404NotFound
                    );
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
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var (items, pageCount, totalItems) = await itemsService.GetUserItemsAsync(userId, queryParams);

                return Results.Ok(new { items, totalCount = totalItems, totalPages = pageCount, queryParams.Page, queryParams.PageSize });
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
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
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
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var isAdminOrModer = principal.IsInRole("Admin") || principal.IsInRole("Moderator");

                var editedItem = await itemsService.UpdateItemAsync(id, ownerId, isAdminOrModer, editDto);

                if (editedItem == null)
                {
                    logger.Warning("Попытка изменения несуществующего объекта по Id: {Id}", id);
                    return Results.Problem(
                        detail: "Объект не найден.",
                        title: "Объект не найден",
                        statusCode: StatusCodes.Status404NotFound
                    );
                }

                return Results.Ok(editedItem);
            }).RequireAuthorization().DisableAntiforgery();

            itemsGroup.MapDelete("/{id}", async (int id, IItemsService itemsService, ClaimsPrincipal principal) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (ownerId == null)
                {
                    logger.Warning("Попытка удалить объект неавторизованным пользователем");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var isAdminOrModer = principal.FindFirstValue(ClaimTypes.Role) == "Admin" ||
                                     principal.FindFirstValue(ClaimTypes.Role) == "Moderator";

                var isRemoved = await itemsService.RemoveItemByIdAsync(id, ownerId, isAdminOrModer);

                if (!isRemoved)
                {
                    logger.Warning("Попытка удалить несуществующий или чужой объект с Id: {ItemId}, пользователем Id: {UserId}", id, ownerId);
                    return Results.Problem(
                        detail: "Объект не найден.",
                        title: "Объект не найден",
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                return Results.NoContent();
            }).RequireAuthorization();

            itemsGroup.MapDelete("/", async (IItemsService itemsService, ClaimsPrincipal principal) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (ownerId == null)
                {
                    logger.Warning("Попытка удалить объекты неавторизованным пользователем");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var isSuccess = await itemsService.RemoveItemsAsync(ownerId);

                if (!isSuccess)
                {
                    logger.Warning("Пользователь с Id: {UserId} попытался удалить объекты, которые не были найдены", ownerId);
                    return Results.Problem(
                        detail: "Объекты не найдены.",
                        title: "Объекты не найдены",
                        statusCode: StatusCodes.Status404NotFound
                    );
                }

                return Results.NoContent();
            }).RequireAuthorization();

            return app;
        }
    }
}
