using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using RentDrive.db.models;
using RentDrive.dto.authDto;
using RentDrive.endpoints.filters;
using RentDrive.services.interfaces;
using Serilog;
using System.Security.Claims;

namespace RentDrive.endpoints
{
    public static class AuthEndpoints
    {
        private static readonly Serilog.ILogger logger = Log.ForContext(typeof(AuthEndpoints));

        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var authGroup = app.MapGroup("/auth")
                           .WithTags("auth");

            authGroup.MapIdentityApi<User>()
                .AddEndpointFilter(async (context, next) =>
                {
                    var path = context.HttpContext.Request.Path.Value?.ToLower();
                    if (path != null && path.EndsWith("/register"))
                    {
                        logger.Warning("Попытка обращения к эндпоинту /auth/register");
                        return Results.Problem(
                            detail: "Стандартные методы отключены. Используйте /auth/custom-register",
                            title: "Стандартные методы отключены",
                            statusCode: StatusCodes.Status400BadRequest
                        );
                    }

                    return await next(context);
                });

            authGroup.MapPost("/custom-register", async (RegisterRequestDto requestDto,
                                                        UserManager<User> userManager,
                                                        SignInManager<User> signInManager) =>
            {
                if (signInManager.IsSignedIn(signInManager.Context.User))
                {
                    logger.Warning("Попытка регистрации. Пользователь уже авторизован");
                    return Results.Problem(
                        detail: "Вы уже авторизованы. Сначала выйдите из аккаунта.",
                        title: "Требуется выход из аккаунта",
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                var existingEmail = await userManager.FindByEmailAsync(requestDto.Email);
                if (existingEmail != null)
                {
                    logger.Warning("Попытка регистрации на существующий Email: {Email}", requestDto.Email);
                    return Results.Problem(
                        detail: "Пользователь с такой почтой уже существует.",
                        title: "Почта занята",
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                var newUser = new User
                {
                    Email = requestDto.Email,
                    UserName = requestDto.Email
                };

                var result = await userManager.CreateAsync(newUser, requestDto.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.ToDictionary(e => e.Code, e => e.Description);
                    logger.Warning("Ошибка при регистрации пользователя: {@errors}", errors);
                    return Results.Problem(
                        detail: $"Ошибка при регистрации пользователя: {errors}.",
                        title: "Ошибка регистрации",
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                await userManager.AddToRoleAsync(newUser, "Customer");

                await signInManager.PasswordSignInAsync(requestDto.Email, requestDto.Password, true, true);

                var response = new
                {
                    email = newUser.Email,
                    isEmailConfirmed = newUser.EmailConfirmed,
                    role = "Customer"
                };

                logger.Information("Успешно зарегистрирован пользователь. Id: {UserId}, Роль: Customer, Email: {Email}", newUser.Id, newUser.Email);
                return Results.Created("/auth/custom-register", response);
            })
                .AddEndpointFilter<ValidationFilter<RegisterRequestDto>>();

            authGroup.MapPost("/logout", async (SignInManager<User> singInManager, ClaimsPrincipal principal) =>
            {
                var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                await singInManager.SignOutAsync();

                logger.Information("Пользователь вышел из аккаунта. Id: {Id}", userId);
                return Results.Ok(new { message = "Успешный выход из системы" });
            }).RequireAuthorization();

            authGroup.MapGet("/antiforgery/token", (IAntiforgery antiforgery, HttpContext context) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Ok(new { token = tokens.RequestToken });
            }).RequireAuthorization();

            authGroup.MapGet("/access-denied", () =>
            {
                return Results.Problem(
                    detail: "Доступ запрещен.",
                    title: "Доступ запрещен.",
                    statusCode: StatusCodes.Status403Forbidden
                );
            });


            authGroup.MapPost("/create-admin", async (RegisterRequestDto requestDto,
                                                      UserManager<User> userManager,
                                                      SignInManager<User> signInManager) =>
            {
                var existingEmail = await userManager.FindByEmailAsync(requestDto.Email);
                if (existingEmail != null)
                {
                    logger.Warning("Попытка создать аминистратора с существующим Email: {Email}", existingEmail.Email);
                    return Results.Problem(
                        detail: "Пользователь с такой почтой уже существует.",
                        title: "Почта занята",
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                var admin = new User
                {
                    Email = requestDto.Email,
                    UserName = requestDto.Email
                };

                var result = await userManager.CreateAsync(admin, requestDto.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.ToDictionary(e => e.Code, e => e.Description);
                    logger.Error("Ошибка создания администратора: {@errors}", errors);
                    return Results.Problem(
                        detail: $"Не удалось создать администратора: {errors}.",
                        title: "Не удалось создать администратора",
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                await userManager.AddToRoleAsync(admin, "Admin");

                var response = new
                {
                    email = admin.Email,
                    isEmailConfirmed = admin.EmailConfirmed,
                    role = "Admin"
                };

                logger.Information("Успешно создан администратор. Id: {AdminId}, Email: {Email}", admin.Id, admin.Email);
                return Results.Created("/auth/create-admin", response);

            })
                .AddEndpointFilter<ValidationFilter<RegisterRequestDto>>()
                .RequireAuthorization("AdminOnly");

            authGroup.MapDelete("/account/{id}", async (string id,
                                                    UserManager<User> userManager,
                                                    SignInManager<User> signInManager,
                                                    ApplicationDbContext context,
                                                    IDeleteQueue<string> detachQueue) =>
            {
                var user = await userManager.FindByIdAsync(id);
                if (user == null)
                {
                    logger.Warning("Попытка удалить несуществующего пользователя с id: {id}.", id);
                    return Results.Problem(
                        detail: "Пользователь не найден.",
                        title: "Пользователь не найден",
                        statusCode: StatusCodes.Status404NotFound
                    );
                }

                bool hasActiveOrders = await context.Bookings.AnyAsync(b =>
                   b.Status == BookingStatus.Active &&
                   (b.CustomerId == user.Id || b.RentItem.OwnerId == user.Id));

                if (hasActiveOrders)
                {
                    logger.Warning("Попытка удалить аккаунт с активной бронью, {Id}", user.Id);
                    return Results.Problem(
                        detail: "Нельзя удалить аккаунт, пока у вас есть активные процессы аренды.",
                        title: "Нельзя удалить аккаунт",
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    await context.Bookings
                       .Where(b => b.CustomerId == user.Id && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                       .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled));

                    var result = await userManager.DeleteAsync(user);
                    if (!result.Succeeded)
                    {
                        var errors = result.Errors.ToDictionary(e => e.Code, e => e.Description);
                        logger.Error("Ошибка при удалении пользователя: {@error}", errors);
                        return Results.Problem(
                            detail: $"Ошибка удаления пользователя: {errors}.",
                            title: "Ошибка удаления пользователя",
                            statusCode: StatusCodes.Status500InternalServerError
                        );
                    }

                    await transaction.CommitAsync();

                    await detachQueue.Enqueue(user.Id);

                    return Results.NoContent();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }).RequireAuthorization("AdminOnly");

            return app;
        }
    }
}