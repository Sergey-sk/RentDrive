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

            if(principal.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var user = await _userManager.GetUserAsync(principal);

            if(user == null) return Results.Unauthorized();

            bool isProfileIncomplete = string.IsNullOrWhiteSpace(user.FirstName) ||
                string.IsNullOrWhiteSpace(user.LastName) ||
                string.IsNullOrWhiteSpace(user.PhoneNumber) ||
                !user.EmailConfirmed;

            if (isProfileIncomplete) return Results.Json(new
            {
                error = "isProfileIncomplete",
                message = "Необходимо заполнить имя, фамилию, номер телефона и подтвердить почту перед добавлением объявлений.",
                redirectUrl = "/auth/account/complete-profile"
            }, statusCode: StatusCodes.Status403Forbidden);

            return await next(context);
        }
    }
}
