using Microsoft.AspNetCore.Mvc;
using RentDrive.dto.bookingsDto;
using RentDrive.endpoints.filters;
using RentDrive.services.interfaces;
using Serilog;
using System.Security.Claims;

namespace RentDrive.endpoints
{
    public static class BookingsEndpoints
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext(typeof(BookingsEndpoints));
        public static IEndpointRouteBuilder MapBookingsEndpoints(this IEndpointRouteBuilder app)
        {
            var bookingsGroup = app.MapGroup("/bookings").RequireAuthorization().WithTags("bookings");

            bookingsGroup.MapGet("/", async (ClaimsPrincipal principal,
                                        [AsParameters] BookingQueryParameters queryParameters,
                                        IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка получения списка бронирований неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var (bookings, pageCount, totalItems) = await bookingsService.GetUserBookingsAsync(userId, queryParameters);

                return Results.Ok(new { bookings, totalCount = totalItems, totalPages = pageCount, queryParameters.Page, queryParameters.PageSize });
            });

            bookingsGroup.MapGet("/{id}", async (int id,
                                           ClaimsPrincipal principal,
                                           IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка получить бронирование неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var booking = await bookingsService.GetUserBookingByIdAsync(userId, id);

                if (booking == null)
                {
                    _logger.Warning("Попытка получения несуществующего бронирования пользователем {Id}.", userId);
                    return Results.Problem(
                        detail: "Бронь не найдена.",
                        title: "Бронь не найдена",
                        statusCode: StatusCodes.Status404NotFound
                    );
                }

                return Results.Ok(booking);
            });

            bookingsGroup.MapPost("/{itemId}", async (int itemId,
                                              ClaimsPrincipal principal,
                                              IBookingsService bookingsService,
                                              [FromBody] CreateBookingDto createBookingDto) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка забронировать товар неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var booking = await bookingsService.CreateBookingAsync(userId, itemId, createBookingDto);

                return Results.Ok(booking);
            }).AddEndpointFilter<CheckUserProfileFilter>();

            bookingsGroup.MapDelete("/{id}", async (int id,
                                            ClaimsPrincipal principal,
                                            IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка отменить бронь неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                bool isDeleted = await bookingsService.DeleteBookingAsync(userId, id);

                if (!isDeleted)
                    return Results.Problem(
                        detail: "Бронирования не найдено.",
                        title: "Бронирования не найдено",
                        statusCode: StatusCodes.Status404NotFound
                    );

                return Results.Ok();
            });

            bookingsGroup.MapGet("/requests", async (ClaimsPrincipal principal,
                                                IBookingsService bookingsService,
                                                [AsParameters] BookingQueryParameters queryParameters) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка получить запросы на бронирования неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var (bookings, pageCount, totalItems) = await bookingsService.GetBookingRequestsAsync(userId, queryParameters);

                return Results.Ok(new { bookings, totalCount = totalItems, totalPages = pageCount, queryParameters.Page, queryParameters.PageSize });
            });

            bookingsGroup.MapGet("/requests/{id}", async (int id,
                                                    ClaimsPrincipal principal,
                                                    IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка получить запрос на бронирование неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var booking = await bookingsService.GetBookingRequestByIdAsync(id, userId);

                if (booking == null)
                    return Results.Problem(
                        detail: "Бронирование не найдено.",
                        title: "Бронирование не найдено",
                        statusCode: StatusCodes.Status404NotFound
                    );

                return Results.Ok(booking);
            });

            bookingsGroup.MapPatch("/requests/{id}/confirm", async (int id,
                                                              ClaimsPrincipal principal,
                                                              IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка подтвердить бронь неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var booking = await bookingsService.UpdateBookingStatusAsync(id, userId, true);

                if (booking == null)
                    return Results.Problem(
                    detail: "Бронирование не найдено.",
                    title: "Бронирование не найдено",
                    statusCode: StatusCodes.Status404NotFound
                );

                return Results.Ok(booking);
            });

            bookingsGroup.MapPatch("/requests/{id}/reject", async (int id,
                                                              ClaimsPrincipal principal,
                                                              IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка отклонить бронь неавторизованным пользователем.");
                    return Results.Problem(
                        detail: "Вы не авторизованы. Пожалуйста, войдите в аккаунт или зарегистрируйтесь.",
                        title: "Требуется авторизация",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var booking = await bookingsService.UpdateBookingStatusAsync(id, userId, false);

                if (booking == null)
                    return Results.Problem(
                    detail: "Бронирование не найдено.",
                    title: "Бронирование не найдено",
                    statusCode: StatusCodes.Status404NotFound
                );

                return Results.Ok(booking);
            });

            return app;
        }

        private static string? GetUserId(ClaimsPrincipal principal)
        {
            return principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
