using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace RentDrive
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly Serilog.ILogger _logger = Log.ForContext<GlobalExceptionHandler>();

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (statusCode, title) = exception switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, "Неверный запрос или ошибка валидации"),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Требуется авторизация"),
                _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера")
            };

            if(statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.Error(exception, "Критическая ошибка при запросе {Path}", httpContext.Request.Path);
            }
            else
            {
                _logger.Warning("Предупреждение бизнес-логики на {Path}: {Message}", httpContext.Request.Path, exception.Message);
            }

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Внутренняя ошибка сервера",
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode = problemDetails.Status.Value;

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
