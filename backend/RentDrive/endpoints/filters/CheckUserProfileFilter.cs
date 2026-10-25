using Microsoft.AspNetCore.Identity;
using RentDrive.db.models;

namespace RentDrive.endpoints.filters
{
    public class CheckUserProfileFilter : IEndpointFilter
    {
        private readonly UserManager<User> _userManager;

        public CheckUserProfileFilter(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var principal = context.HttpContext.User;

            if (principal.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var user = await _userManager.GetUserAsync(principal);

            if (user == null) return Results.Unauthorized();

            bool isProfileIncomplete = string.IsNullOrWhiteSpace(user.FirstName) ||
                string.IsNullOrWhiteSpace(user.LastName) ||
                string.IsNullOrWhiteSpace(user.PhoneNumber) ||
                !user.EmailConfirmed;

            var extensions = new Dictionary<string, object?>
            {
                { "errorCode", "isProfileIncomplete"},
                { "redirectUrl", "/auth/account/complete-profile"}
            };

            if (isProfileIncomplete)
                return Results.Problem(
                    detail: "Необходимо заполнить имя, фамилию, номер телефона и подтвердить почту перед добавлением объявлений.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Необходимо заполнить недостающие данные.",
                    extensions: extensions
                );

            return await next(context);
        }
    }
}
