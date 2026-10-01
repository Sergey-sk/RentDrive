using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using RentDrive.db.models;
using RentDrive.dto.accountDto;
using RentDrive.dto.authDto;
using RentDrive.endpoints.filters;
using RentDrive.services.interfaces;
using Serilog;
using Serilog.Core;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace RentDrive.endpoints
{
    public static class AccountEndpoints
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext(typeof(AccountEndpoints));

        public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
        {
            var accountGroup = app.MapGroup("/account").RequireAuthorization().WithTags("account");

            accountGroup.MapGet("/", async (SignInManager<User> signInManager, UserManager<User> userManager) =>
            {
                var isSignedIn = signInManager.IsSignedIn(signInManager.Context.User);

                if (!isSignedIn)
                {
                    _logger.Warning("Попытка доступа к информации о пользователе без авторизации. Данные токена: {User}", signInManager.Context.User.Identity?.Name);
                    return Results.Unauthorized();
                }

                var user = await userManager.GetUserAsync(signInManager.Context.User);

                if (user == null)
                {
                    _logger.Warning("Авторизованный пользователь не найден в БД. Сессия принудительно закрыта. Данные токена: {User}", signInManager.Context.User.Identity?.Name);
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

            accountGroup.MapPut("/complete-profile", async (CompleteProfileDto dto,
                                                                  UserManager<User> userManager,
                                                                  ClaimsPrincipal principal,
                                                                  SignInManager<User> signInManager) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                {
                    _logger.Warning("Попытка обновления профиля неавторизованным пользователем. Данные токена: {User}", principal.Identity?.Name);
                    return Results.Unauthorized();
                }

                user.FirstName = dto.FirstName;
                user.LastName = dto.LastName;
                user.PhoneNumber = dto.PhoneNumber;

                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    _logger.Warning("Ошибка заполнения имени, фамилии или телефона пользователя. Id: {Id}, Errors: {@errors}", user.Id, result.Errors);
                    return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] {e.Description}));
                }

                await signInManager.RefreshSignInAsync(user);

                _logger.Information("Успешно обновлен профиль пользователя с Id: {Id}", user.Id);

                return Results.Ok(new { message = "Профиль успешно обновлен!" });
            })
                .AddEndpointFilter<ValidationFilter<CompleteProfileDto>>();

            accountGroup.MapPatch("/profile", async (ChangeFirstLastNameDto changeDto,
                                                     UserManager<User> userManager,
                                                     ClaimsPrincipal principal) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                {
                    _logger.Warning("Попытка изменить данные неавторизованным пользователем");
                    return Results.Unauthorized();
                }

                if (!string.IsNullOrWhiteSpace(changeDto.FirstName)) user.FirstName = changeDto.FirstName;
                if (!string.IsNullOrWhiteSpace(changeDto.LastName)) user.LastName = changeDto.LastName;
                if (!string.IsNullOrWhiteSpace(changeDto.PhoneNumber)) user.PhoneNumber = changeDto.PhoneNumber;

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var errors = updateResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
                    _logger.Error("Ошибка изменения данных. Id: {UserId}, Error: {@errors}", user.Id, errors);
                    return Results.ValidationProblem(errors);
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
            }).AddEndpointFilter<ValidationFilter<ChangeFirstLastNameDto>>();

            accountGroup.MapPost("/change-password", async (ChangePasswordDto changeDto,
                                                       ClaimsPrincipal principal,
                                                       UserManager<User> userManager) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                {
                    _logger.Warning("Попытка изменить пароль неавторизованным пользователем.");
                    return Results.Unauthorized();
                }

                var passwordResult = await userManager.ChangePasswordAsync(user, changeDto.CurrentPassword, changeDto.NewPassword);
                if (!passwordResult.Succeeded)
                {
                    _logger.Warning("Ошибка изменения пароля: {@Errors}", passwordResult.Errors);
                    return Results.ValidationProblem(passwordResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                return Results.Ok(new { message = "Пароль успешно изменен" });
            }).AddEndpointFilter<ValidationFilter<ChangePasswordDto>>();

            accountGroup.MapPost("/email/change", async (ChangeEmailDto changeDto,
                                                   ClaimsPrincipal principal,
                                                   UserManager<User> userManager,
                                                   IEmailSender<User> emailSender,
                                                   LinkGenerator linkGenerator,
                                                   HttpContext context) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                {
                    _logger.Warning("Попытка изменить почту неавторизованным пользователем.");
                    return Results.Unauthorized();
                }

                var existingEmail = await userManager.FindByEmailAsync(changeDto.Email);
                if (existingEmail != null)
                {
                    _logger.Warning("Попытка изменить email на уже занятый: {Email}, пользователем {Id}.", changeDto.Email, user.Id);
                    return Results.BadRequest(new { error = $"Email {changeDto.Email} уже занят другим аккаунтом." });
                }

                var isPasswordCorrect = await userManager.CheckPasswordAsync(user, changeDto.CurrentPassword);
                if (!isPasswordCorrect)
                {
                    _logger.Warning("Указан неверный пароль при смене почты: {Password}, пользователем {Id}.", changeDto.CurrentPassword, user.Id);
                    return Results.BadRequest(new { error = "Указан неверный пароль." });
                }

                var code = await userManager.GenerateChangeEmailTokenAsync(user, changeDto.Email);
                var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = linkGenerator.GetUriByName(context, "ConfirmEmailChangeRoute", new { userId = user.Id, email = changeDto.Email, code = encodedCode });

                if (callbackUrl == null)
                {
                    _logger.Warning("Ошибка генерации ссылки подтверждения смены Email на {Email} пользователем {Id}.", changeDto.Email, user.Id);
                    return Results.InternalServerError(new { error = "Ошибка генерации ссылки подтверждения смены Email." });
                }

                await emailSender.SendConfirmationLinkAsync(user, changeDto.Email, HtmlEncoder.Default.Encode(callbackUrl));
                return Results.Ok(new { message = "На новую почту отправлено письмо для подтверждения смены Email." });
            }).AddEndpointFilter<ValidationFilter<ChangeEmailDto>>();

            accountGroup.MapGet("/email/confirm", async (string userId,
                                                   string email,
                                                   string code,
                                                   UserManager<User> userManager) =>
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.Warning("Пользователь не найден при смене почты {Id}.", userId);
                    return Results.NotFound(new { error = "Пользователь не найден." });
                }

                var decodeCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

                var result = await userManager.ChangeEmailAsync(user, email, decodeCode);

                if (!result.Succeeded)
                {
                    _logger.Error("Ошибка смены email. Id: {Id}, Email: {Email}", userId, email);
                    return Results.BadRequest(new { error = "Не удалось изменить Email. Ссылка устарела или неверна." });
                }

                await userManager.SetUserNameAsync(user, email);

                _logger.Information("Успешное изменение email. Id: {Id}, Email: {Email}", userId, email);
                return Results.Ok(new { message = "Email успешно изменен!" });
            }).WithName("ConfirmEmailChangeRoute");

            accountGroup.MapDelete("/", async (SignInManager<User> signInManager,
                                                   UserManager<User> userManager,
                                                   ClaimsPrincipal principal,
                                                   ApplicationDbContext context,
                                                   IDeleteQueue<string> detachQueue) =>
            {
                var user = await userManager.GetUserAsync(principal);
                if (user == null)
                {
                    _logger.Warning("Попытка удалить несуществующего пользователя.");
                    return Results.NotFound(new { error = "Пользователь не найден." });
                }

                bool hasActiveOrders = await context.Bookings.AnyAsync(b =>
                    b.Status == BookingStatus.Active &&
                    (b.CustomerId == user.Id || b.RentItem.OwnerId == user.Id));

                if (hasActiveOrders)
                {
                    _logger.Warning("Попытка удалить аккаунт с активной бронью, {Id}.", user.Id);
                    return Results.BadRequest(new { error = "Нельзя удалить аккаунт, пока у вас есть активные процессы аренды." });
                }

                await context.Bookings
                    .Where(b => b.CustomerId == user.Id && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled));

                var result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    _logger.Error("Ошибка при удалении пользователя: {@error}", result.Errors);
                    return Results.InternalServerError(result.Errors);
                }

                await detachQueue.Enqueue(user.Id);

                await signInManager.SignOutAsync();
                return Results.NoContent();
            }).RequireAuthorization();

            return app;
        }
    }
}
