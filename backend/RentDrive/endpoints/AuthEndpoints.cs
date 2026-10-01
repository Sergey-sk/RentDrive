using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using RentDrive.db.models;
using RentDrive.dto.authDto;
using RentDrive.endpoints.filters;
using RentDrive.services.implementations;
using RentDrive.services.interfaces;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

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
                            statusCode: 400
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
                    return Results.BadRequest(new { message = "Вы уже авторизованы. Сначала выйдите из аккаунта." });
                }

                var existingEmail = await userManager.FindByEmailAsync(requestDto.Email);
                if (existingEmail != null)
                {
                    logger.Warning("Попытка регистрации на существующий Email: {Email}", requestDto.Email);
                    return Results.BadRequest(new { message = "Пользователь с такой почтой уже существует" });
                }

                var newUser = new User
                {
                    Email = requestDto.Email,
                    UserName = requestDto.Email
                };

                var result = await userManager.CreateAsync(newUser, requestDto.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    logger.Warning("Ошибка при регистрации пользователя: {@errors}", errors);
                    return Results.BadRequest(new { message = "Ошибка при регистрации пользователя", errors });
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

            authGroup.MapPost("/create-admin", async (RegisterRequestDto requestDto,
                                                      UserManager<User> userManager,
                                                      SignInManager<User> signInManager) =>
            {
                var existingEmail = await userManager.FindByEmailAsync(requestDto.Email);
                if (existingEmail != null)
                {
                    logger.Warning("Попытка создать аминистратора с существующим Email: {Email}", existingEmail.Email);
                    return Results.BadRequest(new { message = "Пользователь с такой почтой уже существует" });
                }

                var admin = new User
                {
                    Email = requestDto.Email,
                    UserName = requestDto.Email
                };

                var result = await userManager.CreateAsync(admin, requestDto.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    logger.Error("Ошибка создания администратора: {@errors}", errors);
                    return Results.BadRequest(new { message = "Не удалось создать администратора", errors });
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

            authGroup.MapPost("/logout", async (SignInManager<User> singInManager, ClaimsPrincipal principal) =>
            {
                var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                await singInManager.SignOutAsync();
                logger.Information("Пользователь вышел из аккаунта. Id: {Id}", userId);
                return Results.Ok(new { message = "Успешный выход из системы" });
            }).RequireAuthorization();

            authGroup.MapGet("/account", async (SignInManager<User> signInManager, UserManager<User> userManager) =>
            {
                var isSignedIn = signInManager.IsSignedIn(signInManager.Context.User);

                if (!isSignedIn) return Results.Unauthorized();

                var user = await userManager.GetUserAsync(signInManager.Context.User);

                if (user == null)
                {
                    logger.Warning("Авторизованный пользователь не найден в БД. Сессия принудительно закрыта. Данные токена: {User}", signInManager.Context.User.Identity?.Name);
                    await signInManager.SignOutAsync();
                    return Results.NotFound(new { message = "Пользователь не найден" });
                }

                return Results.Ok(new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    phoneNumber = user.PhoneNumber,
                    isEmailConfirmed = user.EmailConfirmed,
                    role = (await userManager.GetRolesAsync(user)).FirstOrDefault()
                });
            });

            authGroup.MapPut("/account/complete-profile", async (CompleteProfileDto dto,
                                                                  UserManager<User> userManager,
                                                                  ClaimsPrincipal principal,
                                                                  SignInManager<User> signInManager) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                    return Results.Unauthorized();

                user.FirstName = dto.FirstName;
                user.LastName = dto.LastName;
                user.PhoneNumber = dto.PhoneNumber;

                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    logger.Warning("Ошибка заполнения имени и фамилии пользователя. Id: {Id}, Errors: {@errors}", user.Id, result.Errors);
                    return Results.BadRequest(result.Errors);
                }

                await signInManager.RefreshSignInAsync(user);

                logger.Information("Успешно обновлен профиль пользователя с Id: {Id}", user.Id);

                return Results.Ok(new { message = "Профиль успешно обновлен!" });
            })
                .AddEndpointFilter<ValidationFilter<CompleteProfileDto>>();

            authGroup.MapPut("/account/edit-profile", async (EditProfileDto reqDto,
                                                              UserManager<User> userManager,
                                                              ClaimsPrincipal principal,
                                                              IEmailSender<User> emailSender,
                                                              LinkGenerator linkGenerator,
                                                              HttpContext context) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                {
                    logger.Warning("Попытка изменить данные неавторизованным пользователем");
                    return Results.Unauthorized();
                }

                var statusMessages = new List<string>();

                if (!string.IsNullOrWhiteSpace(reqDto.FirstName)) user.FirstName = reqDto.FirstName;
                if (!string.IsNullOrWhiteSpace(reqDto.LastName)) user.LastName = reqDto.LastName;
                if (!string.IsNullOrWhiteSpace(reqDto.PhoneNumber)) user.PhoneNumber = reqDto.PhoneNumber;

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var errors = updateResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
                    logger.Error("Ошибка изменения данных. Id: {UserId}, Error: {@errors}", user.Id, errors);
                    return Results.ValidationProblem(errors);
                }
                statusMessages.Add("Профиль успешно обновлен.");

                if (!string.IsNullOrEmpty(reqDto.NewPassword))
                {
                    if (string.IsNullOrEmpty(reqDto.CurrentPassword))
                    {
                        logger.Warning("Попытка изменения пароля без указания текущего. Id: {UserId}", user.Id);
                        return Results.BadRequest(new { error = "Для смены пароля необходимо указать текущий пароль." });
                    }

                    var passwordResult = await userManager.ChangePasswordAsync(user, reqDto.CurrentPassword, reqDto.NewPassword);
                    if (!passwordResult.Succeeded)
                    {
                        var errors = passwordResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
                        logger.Error("Ошибка изменения пароля. Id: {UserId}, Errors: {@errors}", user.Id, errors);
                        return Results.ValidationProblem(errors);
                    }
                    statusMessages.Add("Пароль успешно изменен.");
                }

                if (!string.IsNullOrEmpty(reqDto.Email) && reqDto.Email != user.Email)
                {
                    var existingEmail = await userManager.FindByEmailAsync(reqDto.Email);
                    if (existingEmail != null)
                    {
                        logger.Warning("Попытка смены email на уже занятый. Id: {UserId}, Email: {Email}", user.Id, reqDto.Email);
                        return Results.BadRequest(new { error = $"Email {reqDto.Email} уже занят другим аккаунтом" });
                    }

                    var code = await userManager.GenerateChangeEmailTokenAsync(user, reqDto.Email);
                    var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    var callbackUrl = linkGenerator.GetUriByName(context, "ConfirmEmailChangeRoute", new { userId = user.Id, email = reqDto.Email, code = encodedCode });

                    if (callbackUrl != null)
                    {
                        await emailSender.SendConfirmationLinkAsync(user, reqDto.Email, HtmlEncoder.Default.Encode(callbackUrl));
                        statusMessages.Add("На новую почту отправлено письмо для подтверждения смены Email.");
                    }
                }

                logger.Information("Успешное изменение данных пользователя с Id: {Id}", user.Id);
                return Results.Ok(statusMessages);
            }).RequireAuthorization();

            authGroup.MapGet("/account/confirm-email-change", async (string userId,
                                                                     string email,
                                                                     string code,
                                                                     UserManager<User> userManager) =>
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    logger.Warning("Попытка поиска несуществующего пользователя при подтверждении почты. Id: {Id}", userId);
                    return Results.NotFound("Пользователь не найден");
                }

                var decodeCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

                var result = await userManager.ChangeEmailAsync(user, email, decodeCode);

                if (!result.Succeeded)
                {
                    logger.Error("Ошибка смены email. Id: {Id}, Email: {Email}", userId, email);
                    return Results.BadRequest("Не удалось изменить Email. Ссылка устарела или неверна.");
                }

                await userManager.SetUserNameAsync(user, email);

                logger.Information("Успешное изменение и подтверждение email. Id: {Id}, Email: {Email}", userId, email);
                return Results.Ok(new { message = "Email успешно изменен и подтвержден!" });
            }).WithName("ConfirmEmailChangeRoute");

            authGroup.MapGet("/antiforgery/token", (IAntiforgery antiforgery, HttpContext context) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Ok(new { token = tokens.RequestToken });
            }).RequireAuthorization();

            authGroup.MapGet("/access-denied", () =>
            {
                return Results.Json(new { message = "Доступ запрещен" }, statusCode: StatusCodes.Status403Forbidden);
            });

            authGroup.MapDelete("/account", async (SignInManager<User> signInManager,
                                                   UserManager<User> userManager,
                                                   ClaimsPrincipal principal,
                                                   ApplicationDbContext context,
                                                   IDeleteQueue<string> detachQueue) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                {
                    logger.Warning("Попытка удалить несуществующего пользователя.");
                    return Results.NotFound(new { error = "Пользователь не найден" });
                }

                bool hasActiveOrders = await context.Bookings.AnyAsync(b =>
                    b.Status == BookingStatus.Active &&
                    (b.CustomerId == user.Id || b.RentItem.OwnerId == user.Id));

                if(hasActiveOrders)
                {
                    logger.Warning("Попытка удалить аккаунт с активной бронью, {Id}", user.Id);
                    return Results.BadRequest("Нельзя удалить аккаунт, пока у вас есть активные процессы аренды.");
                }

                await context.Bookings
                    .Where(b => b.CustomerId == user.Id && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled));

                var result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    logger.Error("Ошибка при удалении пользователя: {@error}", result.Errors);
                    return Results.InternalServerError(result.Errors);
                }

                await detachQueue.Enqueue(user.Id);

                await signInManager.SignOutAsync();
                return Results.NoContent();
            }).RequireAuthorization();

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
                    return Results.NotFound(new { error = "Пользователь не найден" });
                }

                bool hasActiveOrders = await context.Bookings.AnyAsync(b =>
                   b.Status == BookingStatus.Active &&
                   (b.CustomerId == user.Id || b.RentItem.OwnerId == user.Id));

                if(hasActiveOrders)
                {
                    logger.Warning("Попытка удалить аккаунт с активной бронью, {Id}", user.Id);
                    return Results.BadRequest("Нельзя удалить аккаунт, пока у вас есть активные процессы аренды.");
                }

                await context.Bookings
                   .Where(b => b.CustomerId == user.Id && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                   .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled));

                var result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    logger.Error("Ошибка при удалении пользователя: {@error}", result.Errors);
                    return Results.InternalServerError(result.Errors);
                }

                await detachQueue.Enqueue(user.Id);

                return Results.NoContent();
            }).RequireAuthorization("AdminOnly");

            return app;
        }
    }
}