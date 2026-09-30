using Microsoft.AspNetCore.Mvc;
using RentDrive.dto.bookingsDto;
using RentDrive.endpoints.filters;
using RentDrive.services.interfaces;
using Serilog;
using System.Security.Claims;
using System.Security.Principal;

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
                    return Results.Unauthorized();
                }

                var (bookings, pageCount) = await bookingsService.GetUserBookingsAsync(userId, queryParameters);

                return Results.Ok(new { bookings, totalCount = bookings.Count, totalPages = pageCount, queryParameters.Page, queryParameters.PageSize });
            });

            bookingsGroup.MapGet("/{id}", async (int id,
                                           ClaimsPrincipal principal,
                                           IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка получить бронирование неавторизованным пользователем.");
                    return Results.Unauthorized();
                }

                var booking = await bookingsService.GetUserBookingByIdAsync(userId, id);

                if (booking == null)
                {

                    _logger.Warning("Попытка получения несуществующего бронирования пользователем {Id}.", userId);
                    return Results.NotFound();
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
                    return Results.Unauthorized();
                }

                try
                {
                    var booking = await bookingsService.CreateBookingAsync(userId, itemId, createBookingDto);

                    return Results.Ok(booking);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }).AddEndpointFilter<CheckUserProfileFilter>();

            bookingsGroup.MapDelete("/{id}", async (int id,
                                            ClaimsPrincipal principal,
                                            IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка отменить бронь неавторизованным пользователем.");
                    return Results.Unauthorized();
                }

                bool isDeleted = await bookingsService.DeleteBookingAsync(userId, id);

                if (!isDeleted) return Results.NotFound();

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
                    return Results.Unauthorized();
                }

                var (bookings, pageCount) = await bookingsService.GetBookingRequestsAsync(userId, queryParameters);

                return Results.Ok(new { bookings, totalCount = bookings.Count, totalPages = pageCount, queryParameters.Page, queryParameters.PageSize });
            });

            bookingsGroup.MapGet("/requests/{id}", async (int id,
                                                    ClaimsPrincipal principal,
                                                    IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка получить запрос на бронирование неавторизованным пользователем.");
                    return Results.Unauthorized();
                }

                var booking = await bookingsService.GetBookingRequestByIdAsync(id, userId);

                if (booking == null) return Results.NotFound();

                return Results.Ok(booking);
            });

            bookingsGroup.MapPatch("/requests/{id}/confirm", async (int id,
                                                              ClaimsPrincipal principal,
                                                              IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if(userId == null)
                {
                    _logger.Warning("Попытка подтвердить бронь неавторизованным пользователем.");
                    return Results.Unauthorized();
                }

                try
                {
                    var booking = await bookingsService.UpdateBookingStatusAsync(id, userId, true);

                    return booking is null ? Results.NotFound() : Results.Ok(booking);
                }
                catch (ArgumentException ex)
                {
                    _logger.Warning(ex, "Ошибка подтверждения брони");
                    return Results.BadRequest(ex.Message);
                }
            });

            bookingsGroup.MapPatch("/requests/{id}/reject", async (int id,
                                                              ClaimsPrincipal principal,
                                                              IBookingsService bookingsService) =>
            {
                var userId = GetUserId(principal);
                if (userId == null)
                {
                    _logger.Warning("Попытка отклонить бронь неавторизованным пользователем.");
                    return Results.Unauthorized();
                }

                try
                {
                    var booking = await bookingsService.UpdateBookingStatusAsync(id, userId, false);

                    return booking is null ? Results.NotFound() : Results.Ok(booking);
                }
                catch (ArgumentException ex)
                {
                    _logger.Warning(ex, "Ошибка отклонения брони");
                    return Results.BadRequest(ex.Message);
                }
            });

            return app;
        }

        private static string? GetUserId(ClaimsPrincipal principal)
        {
            return principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
