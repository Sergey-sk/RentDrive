using FluentValidation;
using Serilog;

namespace RentDrive.endpoints.filters
{
    public class ValidationFilter<T> : IEndpointFilter where T : class
    {
        private static readonly Serilog.ILogger log = Log.ForContext<IEndpointFilter>();

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var model = context.Arguments.FirstOrDefault(x => x is T) as T;
            if (model == null)
            {
                log.Warning("Данные запроса при валидации данных не найдены");
                return Results.BadRequest(new { message = "Данные запроса не найдены." });
            }

            var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
            if (validator == null) return await next(context);

            var result = await validator.ValidateAsync(model);
            if (!result.IsValid)
            {
                log.Error("Ошибка валидации: {@errors}", result.Errors);
                return Results.ValidationProblem(result.ToDictionary());
            }

            return await next(context);
        }
    }
}
