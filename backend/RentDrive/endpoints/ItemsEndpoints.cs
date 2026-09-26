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

            itemsGroup.MapGet("/", async (IWebHostEnvironment env,
                                    IItemsService itemsService,
                                    [AsParameters] SortParams sortParams,
                                    int page = 1,
                                    int pageSize = 10) =>
            {
                var items = await itemsService.GetItemsAsync(page, pageSize, sortParams, env);

                return Results.Ok(new { items = items, totalCount = items.Count, page = page, pageSize = pageSize });
            });

            itemsGroup.MapGet("/{id}", async (int id, IWebHostEnvironment env, IItemsService itemsService) =>
            {
                var item = await itemsService.GetItemByIdAsync(id, env);

                if (item == null)
                    return Results.NotFound($"Объект с id {id} не найден.");

                return Results.Ok(item);
            })
                .WithName("GetItemById");

            var createEndpoint = itemsGroup.MapPost("/", async ([AsParameters] CreateRentItemDto createDto,
                                                                      IItemsService itemsService,
                                                                      ClaimsPrincipal principal,
                                                                      UserManager<User> userManager) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await userManager.GetUserAsync(principal);

                if (user == null) return Results.Unauthorized();

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

                if (ownerId == null) return Results.Unauthorized();

                var isAdminOrModer = principal.IsInRole("Admin") || principal.IsInRole("Moderator");

                try
                {
                    var editedItem = await itemsService.UpdateItemAsync(id, ownerId, isAdminOrModer, editDto);

                    if (editedItem == null) return Results.NotFound(new { error = "Такой объект не найден" });

                    return Results.Ok(editedItem);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            }).RequireAuthorization().DisableAntiforgery();

            itemsGroup.MapDelete("/{id}", async (int id, IItemsService itemsService, ClaimsPrincipal principal) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (ownerId == null) return Results.Unauthorized();

                var isAdminOrModer = principal.FindFirstValue(ClaimTypes.Role) == "Admin" ||
                                     principal.FindFirstValue(ClaimTypes.Role) == "Moderator";

                var isRemoved = await itemsService.RemoveItemByIdAsync(id, ownerId, isAdminOrModer);

                if (!isRemoved) return Results.BadRequest();

                return Results.NoContent();
            }).RequireAuthorization();

            itemsGroup.MapDelete("/", async (IItemsService itemsService, ClaimsPrincipal principal) =>
            {
                var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (ownerId == null) return Results.Unauthorized();

                var isSuccess = await itemsService.RemoveItemsAsync(ownerId);

                if (!isSuccess) return Results.NotFound();

                return Results.NoContent();
            }).RequireAuthorization();

            return app;
        }
    }
}
