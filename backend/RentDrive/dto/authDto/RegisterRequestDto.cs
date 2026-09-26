using FluentValidation;
using System.ComponentModel.DataAnnotations;

namespace RentDrive.dto.authDto
{
    public record RegisterRequestDto(
        string Email,
        string Password
    );

    public class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
    {

        public RegisterRequestDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email является обязательным полем.")
                .EmailAddress().WithMessage("Некорректный формат Email.")
                .MaximumLength(100).WithMessage("Email не может превышать 100 символов");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Пароль является обязательным полем.")
                .MinimumLength(4).WithMessage("Пароль должен содержать минимум 4 символа.");
        }
    }
}
